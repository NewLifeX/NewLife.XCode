using System.Security.Cryptography;
using NewLife;
using NewLife.Configuration;
using NewLife.Data;
using NewLife.Log;
using NewLife.Serialization;
using NewLife.Threading;
using XCode.Membership;

namespace XCode.Configuration;

/// <summary>数据库参数表文件提供者</summary>
public class DbConfigProvider : ConfigProvider
{
    #region 属性
    /// <summary>要加载配置的用户。默认0表示全局</summary>
    public Int32 UserId { get; set; }

    /// <summary>分类</summary>
    public String? Category { get; set; }

    /// <summary>本地缓存配置数据。即使网络断开，仍然能够加载使用本地数据，默认 Json</summary>
    public ConfigCacheLevel CacheLevel { get; set; } = ConfigCacheLevel.Json;

    /// <summary>更新周期。默认15秒，0秒表示不做自动更新</summary>
    public Int32 Period { get; set; } = 15;

    private IDictionary<String, Object?>? _cache;
    #endregion

    #region 方法
    /// <summary>初始化提供者，如有必要，此时加载缓存文件</summary>
    /// <remarks>
    /// 大多数基于数据的配置（如魔方CubeSetting），默认配置提供者都是Xml，在静态构造函数里面执行的是Xml配置提供者的Init。
    /// 后来基于数据库的配置提供者，再由LoadAll触发执行Init。
    /// </remarks>
    /// <param name="value"></param>
    public override void Init(String? value)
    {
        // 只有全局配置支持本地缓存
        if (UserId != 0) return;

        // 本地缓存。兼容旧版配置文件
        var name = Category;
        //var path = Path.GetTempPath().CombinePath(SysConfig.Current.Name);
        var path = NewLife.Setting.Current.DataPath;
        var file = path.CombinePath($"dbConfig_{name}.json").GetFullPath();
        var old = $"Config/dbConfig_{name}.json".GetFullPath();
        if (!File.Exists(file) && File.Exists(old))
        {
            try
            {
                File.Move(old, file);
            }
            catch
            {
                file = old;
            }
        }

        if ((Root == null || Root.Childs == null || Root.Childs.Count == 0) && CacheLevel > ConfigCacheLevel.NoCache && File.Exists(file))
        {
            XTrace.WriteLine("[{0}/{1}]加载缓存配置：{2}", Category, UserId, file);
            var txt = File.ReadAllText(file);

            // 删除旧版
            if (file.EndsWithIgnoreCase(".config")) File.Delete(file);

            // 加密存储
            if (CacheLevel == ConfigCacheLevel.Encrypted)
                txt = Aes.Create().Decrypt(txt.ToBase64(), name.GetBytes()).ToStr();

            txt = txt.Trim();
            var dic = txt.StartsWith("{") && txt.EndsWith("}") ?
                JsonParser.Decode(txt) :
                XmlParser.Decode(txt);
            if (dic != null) Root = Build(dic);

            // 如果位于分布式环境中，使用较短间隔，否则使用较长间隔
            if (Period == 15)
            {
                Period = Snowflake.GlobalWorkerId > 0 ? 15 : 60;
            }
        }
    }

    /// <summary>获取所有配置</summary>
    /// <returns></returns>
    protected virtual IDictionary<String, Object?> GetAll()
    {
        var dic = new Dictionary<String, Object?>(StringComparer.CurrentCultureIgnoreCase);

        // 减少日志
        using var showSql = Parameter.Meta.Session.Dal.Session.SetShowSql(false);

        var list = Parameter.FindAllByUserID(UserId, Category);
        foreach (var item in list)
        {
            if (!item.Enable || item.Name.IsNullOrEmpty()) continue;

            // 优先读取 Value，如为空则读取 LongValue（支持长文本）
            var value = item.Value?.Trim();
            if (value.IsNullOrEmpty()) value = item.LongValue?.Trim();
            dic[item.Name] = value;

            if (!item.Remark.IsNullOrEmpty())
                dic["#" + item.Name] = item.Remark;
        }

        return dic;
    }

    /// <summary>加载配置字典为配置树</summary>
    /// <param name="configs"></param>
    /// <returns></returns>
    public virtual IConfigSection Build(IDictionary<String, Object?> configs)
    {
        // 换个对象，避免数组元素在多次加载后重叠
        var root = new ConfigSection { };
        foreach (var item in configs)
        {
            // 跳过备注
            if (item.Key[0] == '#') continue;

            var ks = item.Key.Split(':');
            var section = root;
            for (var i = 0; i < ks.Length; i++)
            {
                section = section.GetOrAddChild(ks[i]) as ConfigSection;
                if (section == null) break;
            }
            if (section == null) break;

            if (item.Value is IDictionary<String, Object?> dic)
                section.Childs = Build(dic).Childs;
            else
                section.Value = item.Value + "";

            // 加载备注
            if (configs.TryGetValue("#" + item.Key, out var remark))
                section.Comment = remark as String;
        }
        return root;
    }

    private Int32 _inited;
    /// <summary>加载配置</summary>
    public override Boolean LoadAll()
    {
        try
        {
            // 首次访问，加载配置
            if (_inited == 0 && Interlocked.CompareExchange(ref _inited, 1, 0) == 0)
                Init(null);
        }
        catch { }

        try
        {
            IsNew = true;

            var dic = GetAll();
            if (dic != null && dic.Count > 0)
            {
                IsNew = false;

                Root = Build(dic);

                // 缓存
                SaveCache(dic);
            }
            else
            {
                // 首次加载，且配置文件存在，需要保存一份回去数据库。可能是从配置文件迁移为数据库配置
                if (Root != null && Root.Childs != null && Root.Childs.Count > 0)
                {
                    XTrace.WriteLine("[{0}/{1}]从文件保存配置到数据库", Category, UserId);

                    SaveAll();
                }
            }

            // 自动更新
            if (Period > 0) InitTimer();

            return true;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);

            return false;
        }
    }

    private void SaveCache(IDictionary<String, Object?> configs)
    {
        // 缓存
        _cache = configs;

        // 本地缓存
        if (CacheLevel > ConfigCacheLevel.NoCache)
        {
            var name = Category;
            //var path = Path.GetTempPath().CombinePath(SysConfig.Current.Name);
            var path = NewLife.Setting.Current.DataPath;
            var file = path.CombinePath($"dbConfig_{name}.json").GetFullPath();
            var txt = configs.ToJson(true);

            // 加密存储
            if (CacheLevel == ConfigCacheLevel.Encrypted)
                txt = Aes.Create().Encrypt(txt.GetBytes(), name.GetBytes()).ToBase64();

            File.WriteAllText(file.EnsureDirectory(true), txt);
        }
    }

    /// <summary>保存配置树到数据源</summary>
    public override Boolean SaveAll()
    {
        var list = Parameter.FindAllByUserID(UserId, Category);
        Save(list, Root, null);

        // 通知绑定对象，配置数据有改变
        NotifyChange();

        return true;
    }

    void Save(IList<Parameter> list, IConfigSection root, String? prefix)
    {
        if (root == null || root.Childs == null) return;

        foreach (var section in root.Childs)
        {
            var name = prefix + section.Key;
            if (section.Childs != null && section.Childs.Count > 0)
            {
                Save(list, section, name + ":");
            }
            else
            {
                var pi = list.FirstOrDefault(_ => _.Name == name);
                if (pi == null)
                {
                    pi = new Parameter { UserID = UserId, Category = Category, Name = name };
                    list.Add(pi);
                }

                // 根据长度自动选择 Value（<200字符）或 LongValue（≥200字符）
                var value = section.Value;
                if (value != null && value.Length < 200)
                {
                    pi.Value = value;
                    pi.LongValue = null;
                }
                else
                {
                    pi.Value = null;
                    pi.LongValue = value;
                }

                pi.Enable = true;

                if (!section.Comment.IsNullOrEmpty())
                    pi.Remark = section.Comment;

                pi.Save();
            }
        }
    }
    #endregion

    #region 绑定
    /// <summary>绑定模型，使能热更新，配置存储数据改变时同步修改模型属性</summary>
    /// <typeparam name="T">模型</typeparam>
    /// <param name="model">模型实例</param>
    /// <param name="autoReload">是否自动更新。默认true</param>
    /// <param name="path">路径。配置树位置，配置中心等多对象混合使用时</param>
    public override void Bind<T>(T model, Boolean autoReload = true, String? path = null)
    {
        base.Bind<T>(model, autoReload, path);

        if (autoReload) InitTimer();
    }
    #endregion

    #region 定时
    /// <summary>定时器</summary>
    protected TimerX? _timer;
    private void InitTimer()
    {
        if (_timer != null) return;
        lock (this)
        {
            if (_timer != null) return;

            var p = Period;
            if (p <= 0) p = 60;
            _timer = new TimerX(DoRefresh, null, p * 1000, p * 1000) { Async = true };
        }
    }

    /// <summary>定时刷新配置</summary>
    /// <param name="state"></param>
    protected void DoRefresh(Object state)
    {
        using var showSql = Parameter.Meta.Session.Dal.Session.SetShowSql(false);

        var dic = GetAll();
        if (dic == null) return;

        var changed = new Dictionary<String, Object?>();
        if (_cache != null)
        {
            foreach (var item in dic)
            {
                // 跳过备注
                if (item.Key[0] == '#') continue;
                if (!_cache.TryGetValue(item.Key, out var v) || v + "" != item.Value + "")
                {
                    changed.Add(item.Key, item.Value);
                }
            }
        }

        if (changed.Count > 0)
        {
            XTrace.WriteLine("[{0}/{1}]配置改变，重新加载如下键：{2}", Category, UserId, changed.ToJson());

            Root = Build(dic);

            // 缓存
            SaveCache(dic);

            NotifyChange();
        }
    }
    #endregion
}