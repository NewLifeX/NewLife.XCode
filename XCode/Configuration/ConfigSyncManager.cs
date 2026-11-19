using System.Diagnostics.CodeAnalysis;
using NewLife.Common;
using NewLife.Configuration;
using NewLife.Log;
using NewLife.Threading;
using XCode.Membership;

namespace XCode.Configuration;

/// <summary>集群配置同步引擎。定时比较 Parameter 表与本地配置文件时间，用数据库整份覆盖本地文件</summary>
/// <remarks>
/// 各实例启动完成后，定期比较 Category="{AppId}-{Scope}" 的 Max(Parameter.UpdateTime) 与本地配置文件时间，
/// 决定是否用 Parameter 整份覆盖本地文件。本地文件仍是运行时唯一来源，不把 Config&lt;T&gt;.Provider 换成 DbConfigProvider。
/// </remarks>
/// <param name="appId">应用标识</param>
/// <example>
/// <code>
/// var sync = new ConfigSyncManager("StarServer");
/// sync.AddCommon()                                  // Core + Sys + XCode 三组
///     .Add&lt;StarSetting&gt;("Star", skip: [nameof(StarSetting.Secret)]);
///
/// sync.Start();                                     // 首次延迟5秒，之后默认每60秒检查
/// sync.CheckNow("Core");                            // 事件到达时立即检查
/// sync.Stop();
/// </code>
/// </example>
public class ConfigSyncManager(String appId)
{
    #region 属性
    /// <summary>应用标识</summary>
    public String AppId { get; set; } = appId;

    /// <summary>检查周期，单位秒，默认60秒，最小60秒</summary>
    public Int32 Period { get; set; } = 60;

    /// <summary>同步目标集合</summary>
    public IList<ConfigSyncTarget> Targets { get; } = [];

    /// <summary>配置发生变更事件。AppId 与 Scope 通过事件参数传递</summary>
    public event EventHandler<ConfigSyncEvent>? Changed;
    #endregion

    #region 注册
    /// <summary>注册一个配置类型</summary>
    /// <typeparam name="TConfig">配置类型</typeparam>
    /// <param name="scope">作用域</param>
    /// <param name="skip">跳过的键集合</param>
    /// <returns>当前管理器，支持链式调用</returns>
    public ConfigSyncManager Add<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)] TConfig>(String scope, String[]? skip = null) where TConfig : Config<TConfig>, new()
    {
        Targets.Add(ConfigSyncTarget.Create<TConfig>(scope, skip));

        return this;
    }

    /// <summary>注册核心、系统与 XCode 三组设置</summary>
    /// <param name="skip">跳过的键集合，与各组默认跳过键合并</param>
    /// <returns>当前管理器，支持链式调用</returns>
    public ConfigSyncManager AddCommon(String[]? skip = null)
    {
        Add<NewLife.Setting>("Core", Merge(["DataPath", "LogPath", "BackupPath", "PluginPath"], skip));
        Add<SysConfig>("Sys", Merge(["Instance", "InstallTime"], skip));
        Add<XCodeSetting>("XCode", skip);

        return this;
    }

    private static String[] Merge(String[] defaults, String[]? skip) =>
        skip == null || skip.Length == 0 ? defaults : defaults.Concat(skip).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private ConfigSyncTarget? FindTarget(String scope) => Targets.FirstOrDefault(e => e.Scope.EqualIgnoreCase(scope));
    #endregion

    #region 启停
    private TimerX? _timer;
    private readonly Object _syncRoot = new();

    /// <summary>启动定时同步。首次延迟5秒，之后按 Period 周期执行；重复调用幂等</summary>
    public void Start()
    {
        if (_timer != null) return;

        lock (_syncRoot)
        {
            _timer ??= new TimerX(DoCheck, null, 5_000, Math.Max(60, Period) * 1000) { Async = true };
        }
    }

    /// <summary>停止定时同步并释放定时器</summary>
    public void Stop() => Interlocked.Exchange(ref _timer, null).TryDispose();

    private void DoCheck(Object? state) => CheckNow();
    #endregion

    #region 同步
    private readonly HashSet<String> _seeded = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>立即执行一次同步检查</summary>
    /// <param name="scope">仅检查指定作用域；为空检查全部</param>
    public void CheckNow(String? scope = null)
    {
        // 串行执行，避免定时器与手动检查重入
        lock (_syncRoot)
        {
            foreach (var target in Targets)
            {
                if (!scope.IsNullOrEmpty() && !target.Scope.EqualIgnoreCase(scope)) continue;

                CheckTarget(target);
            }
        }
    }

    private void CheckTarget(ConfigSyncTarget target)
    {
        try
        {
            if (target.Provider?.Invoke() is not FileConfigProvider provider)
            {
                XTrace.WriteLine("ConfigSync[{0}] provider is not FileConfigProvider, skip", target.Scope);
                return;
            }

            var file = provider.FileName?.GetBasePath();
            if (file.IsNullOrEmpty())
            {
                XTrace.WriteLine("ConfigSync[{0}] FileName is empty, skip", target.Scope);
                return;
            }

            // 必须在访问 Current 之前判断文件是否存在，Current 会在文件缺失时创建默认文件
            var fileExisted = File.Exists(file);
            var current = target.Current?.Invoke();
            if (current == null) return;

            var localTime = fileExisted ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue;
            var category = $"{AppId}-{target.Scope}";

            // 本地配置树
            var localSection = new ConfigSection { Childs = [] };
            localSection.MapFrom(current);

            var rows = ReadRows(category);
            var paramMax = MaxUpdateTime(rows);

            // 每个进程首次检查或库里还没有该分类时，把本地键补齐进参数表，保证库拥有完整配置项
            if (rows.Count == 0 || _seeded.Add(category)) SaveRows(category, localSection, target.Skip, onlyMissing: true);

            // 库原本为空（刚播种），配置以本地为准
            if (rows.Count == 0) return;

            // 本地文件在参数之后修改过，保持本地
            if (paramMax <= localTime) return;

            // 数据库更新，整份覆盖本地
            provider.LoadAll();

            var root = provider.Root;
            if (root?.Childs == null || root.Childs.Count == 0)
            {
                root = new ConfigSection { Childs = [] };
                root.MapFrom(current);
                provider.Root = root;
            }

            ConfigSyncHelper.ApplySection(root, BuildDbSection(rows), target.Skip);
            provider.SaveAll();

            // 把本地文件时间推进到库版本时间，标记该版本已应用，避免下一轮重复应用
            if (File.Exists(file) && File.GetLastWriteTimeUtc(file) < paramMax) File.SetLastWriteTimeUtc(file, paramMax);

            if (current is NewLife.Setting setting) setting.ApplyLanguage();
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    private static DateTime MaxUpdateTime(IList<Parameter> rows) =>
        rows.Count > 0 ? rows.Max(e => e.UpdateTime).ToUniversalTime() : DateTime.MinValue;
    #endregion

    #region 数据库读写
    private static IList<Parameter> ReadRows(String category)
    {
        using var showSql = Parameter.Meta.Session.Dal.Session.SetShowSql(false);

        return Parameter.FindAllByCategory(category);
    }

    private static IConfigSection BuildDbSection(IList<Parameter> rows)
    {
        var root = new ConfigSection { Childs = [] };
        foreach (var row in rows)
        {
            if (!row.Enable || row.Name.IsNullOrEmpty()) continue;

            var value = row.Value?.Trim();
            if (value.IsNullOrEmpty()) value = row.LongValue?.Trim();

            IConfigSection section = root;
            foreach (var key in row.Name.Split(':'))
            {
                section = section.GetOrAddChild(key);
            }
            section.Value = value;
        }

        return root;
    }

    /// <summary>把配置树的非跳过叶子键写入参数表</summary>
    /// <param name="category">分类</param>
    /// <param name="section">配置树</param>
    /// <param name="skip">跳过键集合</param>
    /// <param name="onlyMissing">只写入库中缺失的键，不覆盖已有值</param>
    private static void SaveRows(String category, IConfigSection section, ICollection<String> skip, Boolean onlyMissing = false)
    {
        using var showSql = Parameter.Meta.Session.Dal.Session.SetShowSql(false);

        var rows = ReadRows(category).ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var kv in ConfigSyncHelper.Flatten(section))
        {
            if (ConfigSyncHelper.IsSkipped(skip, kv.Key)) continue;

            if (rows.TryGetValue(kv.Key, out var row))
            {
                if (onlyMissing) continue;
            }
            else
                row = new Parameter { UserId = 0, Category = category, Name = kv.Key };

            if (kv.Value != null) row.SetValue(kv.Value);
            row.Enable = true;
            try
            {
                row.Save();
            }
            catch
            {
                // 多实例同时写入可能撞唯一索引，整份重读分类后改为更新，后续键复用新快照
                rows = ReadRows(category).ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
                if (!rows.TryGetValue(kv.Key, out var row2)) throw;

                if (kv.Value != null) row2.SetValue(kv.Value);
                row2.Enable = true;
                row2.Save();
            }
        }
    }
    #endregion

    #region 配置页
    /// <summary>加载合并视图。以本地配置为底，合并 Parameter 的非跳过键</summary>
    /// <typeparam name="TConfig">配置类型</typeparam>
    /// <param name="scope">作用域</param>
    /// <returns>合并后的配置实例；数据库无行时返回本地当前实例</returns>
    public TConfig LoadView<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)] TConfig>(String scope) where TConfig : Config<TConfig>, new()
    {
        var current = Config<TConfig>.Current;

        var provider = Config<TConfig>.Provider;
        var target = FindTarget(scope);
        if (target == null || provider == null) return current;

        var rows = ReadRows($"{AppId}-{scope}");
        if (rows.Count == 0) return current;

        var section = new ConfigSection { Childs = [] };
        section.MapFrom(current);
        ConfigSyncHelper.ApplySection(section, BuildDbSection(rows), target.Skip);

        var model = new TConfig();
        section.MapTo(model, provider);

        return model;
    }

    /// <summary>同时保存到 Parameter 与本地文件，并触发变更事件</summary>
    /// <typeparam name="TConfig">配置类型</typeparam>
    /// <param name="scope">作用域</param>
    /// <param name="model">配置模型</param>
    public void SaveBoth<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)] TConfig>(String scope, TConfig model) where TConfig : Config<TConfig>, new()
    {
        var target = FindTarget(scope) ?? throw new InvalidOperationException($"scope [{scope}] not registered");
        if (model == null) throw new ArgumentNullException(nameof(model));

        var provider = target.Provider?.Invoke() ?? throw new InvalidOperationException($"scope [{scope}] provider is null");

        // 非跳过键写入 Parameter
        var section = new ConfigSection { Childs = [] };
        section.MapFrom(model);
        SaveRows($"{AppId}-{scope}", section, target.Skip);

        // 非跳过键写回本地，跳过键保留本机值
        var current = Config<TConfig>.Current;

        var local = new ConfigSection { Childs = [] };
        local.MapFrom(current);
        ConfigSyncHelper.ApplySection(local, section, target.Skip);
        local.MapTo(current, provider);

        current.Save();

        Changed?.Invoke(this, new ConfigSyncEvent(AppId, scope));
    }
    #endregion
}

/// <summary>集群配置同步目标。描述一个参与同步的本地文件配置</summary>
/// <param name="scope">作用域</param>
/// <param name="skip">跳过的键集合</param>
public class ConfigSyncTarget(String scope, String[]? skip = null)
{
    /// <summary>作用域。与 AppId 共同构成 Parameter 表的 Category</summary>
    public String Scope { get; } = scope;

    /// <summary>配置提供者工厂。返回该配置当前使用的提供者</summary>
    public Func<IConfigProvider?>? Provider { get; set; }

    /// <summary>当前配置实例工厂。返回 Config&lt;T&gt;.Current</summary>
    public Func<Object>? Current { get; set; }

    /// <summary>跳过的键集合。完整路径或叶子名匹配，忽略大小写</summary>
    public ICollection<String> Skip { get; } = new HashSet<String>(skip ?? [], StringComparer.OrdinalIgnoreCase);

    /// <summary>创建同步目标</summary>
    /// <typeparam name="TConfig">配置类型</typeparam>
    /// <param name="scope">作用域</param>
    /// <param name="skip">跳过的键集合</param>
    /// <returns>同步目标</returns>
    public static ConfigSyncTarget Create<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)] TConfig>(String scope, String[]? skip = null) where TConfig : Config<TConfig>, new() => new(scope, skip)
    {
        Provider = () => Config<TConfig>.Provider,
        Current = () => Config<TConfig>.Current,
    };
}

/// <summary>集群配置同步事件</summary>
/// <param name="appId">应用标识</param>
/// <param name="scope">作用域</param>
public class ConfigSyncEvent(String appId, String scope) : EventArgs
{
    /// <summary>应用标识。用于区分不同应用的配置命名空间</summary>
    public String AppId { get; } = appId;

    /// <summary>作用域。对应某一个已注册配置目标的 Scope</summary>
    public String Scope { get; } = scope;
}

/// <summary>集群配置同步帮助类。提供配置树的扁平化与合并等操作</summary>
internal static class ConfigSyncHelper
{
    /// <summary>扁平化配置树为键路径字典。叶子节点键名以冒号表示层级</summary>
    /// <param name="section">配置树</param>
    /// <returns>键路径到值的字典，忽略大小写</returns>
    public static Dictionary<String, String?> Flatten(IConfigSection section)
    {
        var dic = new Dictionary<String, String?>(StringComparer.OrdinalIgnoreCase);
        FlattenInto(section, null, dic);
        return dic;
    }

    private static void FlattenInto(IConfigSection section, String? prefix, IDictionary<String, String?> dic)
    {
        var childs = section.Childs;
        if (childs == null || childs.Count == 0)
        {
            // 叶子节点：prefix 即完整键路径
            if (!prefix.IsNullOrEmpty()) dic[prefix] = section.Value;

            return;
        }

        foreach (var child in childs)
        {
            var full = prefix == null ? child.Key : prefix + ":" + child.Key;
            FlattenInto(child, full, dic);
        }
    }

    /// <summary>判断键是否被跳过。完整路径或叶子名匹配其一即可</summary>
    /// <param name="skip">跳过键集合</param>
    /// <param name="fullPath">完整键路径</param>
    /// <returns>是否跳过</returns>
    public static Boolean IsSkipped(ICollection<String> skip, String fullPath)
    {
        if (skip == null || skip.Count == 0) return false;

        var name = fullPath;
        var p = fullPath.LastIndexOf(':');
        if (p >= 0) name = fullPath.Substring(p + 1);

        return skip.Any(s => s.EqualIgnoreCase(fullPath) || s.EqualIgnoreCase(name));
    }

    /// <summary>把源配置树的非跳过叶子键合并进目标配置树。保留目标本地其它键与跳过字段</summary>
    /// <param name="dest">目标配置树</param>
    /// <param name="src">源配置树</param>
    /// <param name="skip">跳过键集合</param>
    public static void ApplySection(IConfigSection dest, IConfigSection src, ICollection<String> skip)
    {
        if (dest == null || src == null) return;

        foreach (var kv in Flatten(src))
        {
            if (IsSkipped(skip, kv.Key)) continue;

            var section = dest.Find(kv.Key, true);
            if (section != null) section.Value = kv.Value;
        }
    }
}
