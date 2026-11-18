using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewLife;
using NewLife.Configuration;
using XCode.Configuration;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Configuration;

/// <summary>集群配置同步引擎测试</summary>
/// <remarks>
/// 使用专用测试配置 SyncConfig 与唯一临时文件，避免污染真实 Config 目录。
/// Parameter 表使用共享 Membership 库（SQLite），以唯一 Category 隔离并在结束时清理。
/// </remarks>
[Collection("Database")]
public class ConfigSyncTests : IDisposable
{
    /// <summary>测试配置类型。唯一文件名由 Provider.FileName 指定，避免污染真实配置目录</summary>
    public class SyncConfig : Config<SyncConfig>
    {
        /// <summary>名称</summary>
        public String Name { get; set; } = "default";

        /// <summary>是否调试</summary>
        public Boolean Debug { get; set; } = true;

        /// <summary>数量</summary>
        public Int32 Count { get; set; } = 42;

        /// <summary>嵌套配置</summary>
        public SyncSub Sub { get; set; } = new();

        /// <summary>密钥，作为跳过键</summary>
        public String Secret { get; set; } = "secret";
    }

    /// <summary>嵌套配置</summary>
    public class SyncSub
    {
        /// <summary>地址</summary>
        public String Url { get; set; } = "http://localhost";

        /// <summary>端口</summary>
        public Int32 Port { get; set; } = 80;
    }

    private String _dir = null!;
    private String _file = null!;
    private String _category = null!;

    private void ResetSyncConfig()
    {
        (SyncConfig.Provider as IDisposable)?.TryDispose();

        _dir = Path.Combine(Path.GetTempPath(), "ConfigSyncTests", Guid.NewGuid().ToString("N"));
        _file = Path.Combine(_dir, "sync.config");

        SyncConfig.Provider = new XmlConfigProvider { FileName = _file };
        SyncConfig.Current = null;
    }

    private ConfigSyncManager CreateManager(String appId, String scope, String[] skip = null)
    {
        _category = $"{appId}-{scope}";
        CleanCategory(_category);

        var manager = new ConfigSyncManager(appId);
        manager.Add<SyncConfig>(scope, skip);

        return manager;
    }

    private static void CleanCategory(String category)
    {
        using var showSql = Parameter.Meta.Session.Dal.Session.SetShowSql(false);

        foreach (var row in Parameter.FindAllByCategory(category))
        {
            row.Delete();
        }
    }

    private static IList<Parameter> ReadRows(String category)
    {
        using var showSql = Parameter.Meta.Session.Dal.Session.SetShowSql(false);

        return Parameter.FindAllByCategory(category);
    }

    private static void SetParameterValue(String category, String name, String value)
    {
        var row = ReadRows(category).FirstOrDefault(r => r.Name.EqualIgnoreCase(name));
        Assert.NotNull(row);

        row.Value = value;
        row.Save();
    }

    public void Dispose()
    {
        if (!_category.IsNullOrEmpty()) CleanCategory(_category);
        (SyncConfig.Provider as IDisposable)?.TryDispose();
        SyncConfig.Current = null;
        SyncConfig.Provider = null;

        if (!_dir.IsNullOrEmpty() && Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    #region 帮助方法
    [Fact(DisplayName = "Flatten 展开嵌套键为冒号分隔路径")]
    public void Flatten_NestedKeys()
    {
        var root = new ConfigSection { Childs = [] };
        root.GetOrAddChild("Debug").Value = "true";
        var sys = root.GetOrAddChild("Sys");
        sys.GetOrAddChild("Name").Value = "abc";
        sys.GetOrAddChild("Port").Value = "80";

        var dic = ConfigSyncHelper.Flatten(root);

        Assert.Equal(3, dic.Count);
        Assert.Equal("true", dic["Debug"]);
        Assert.Equal("abc", dic["Sys:Name"]);
        Assert.Equal("80", dic["Sys:Port"]);
    }

    [Fact(DisplayName = "IsSkipped 完整路径或叶子名匹配均视为跳过")]
    public void IsSkipped_FullPathOrLeaf()
    {
        var skip = new HashSet<String>(StringComparer.OrdinalIgnoreCase) { "DataPath", "Star:Secret" };

        Assert.True(ConfigSyncHelper.IsSkipped(skip, "DataPath"));
        Assert.True(ConfigSyncHelper.IsSkipped(skip, "Star:Secret"));
        Assert.False(ConfigSyncHelper.IsSkipped(skip, "Other:Secret"));
        Assert.False(ConfigSyncHelper.IsSkipped(skip, "Star:Name"));
        Assert.False(ConfigSyncHelper.IsSkipped(skip, "DataPath2"));
    }

    [Fact(DisplayName = "ApplySection 合并源树并跳过指定键")]
    public void ApplySection_Skips()
    {
        var dest = new ConfigSection { Childs = [] };
        dest.GetOrAddChild("Keep").Value = "local";
        dest.GetOrAddChild("DataPath").Value = "localPath";

        var src = new ConfigSection { Childs = [] };
        src.GetOrAddChild("Keep").Value = "fromdb";
        src.GetOrAddChild("DataPath").Value = "dbPath";
        var star = src.GetOrAddChild("Star");
        star.GetOrAddChild("Secret").Value = "dbSecret";
        star.GetOrAddChild("Name").Value = "dbName";

        var skip = new HashSet<String>(StringComparer.OrdinalIgnoreCase) { "DataPath", "Star:Secret" };
        ConfigSyncHelper.ApplySection(dest, src, skip);

        Assert.Equal("fromdb", dest["Keep"]);
        Assert.Equal("localPath", dest["DataPath"]);
        Assert.Equal("dbName", dest["Star:Name"]);
        Assert.Null(dest["Star:Secret"]);
    }
    #endregion

    [Fact(DisplayName = "AddCommon 自定义跳过键与各组默认跳过键合并")]
    public void AddCommon_MergesDefaultSkips()
    {
        var manager = new ConfigSyncManager("Test");
        manager.AddCommon(["Secret"]);

        var core = manager.Targets.First(e => e.Scope == "Core");
        Assert.Contains("DataPath", core.Skip);
        Assert.Contains("Secret", core.Skip);

        var sys = manager.Targets.First(e => e.Scope == "Sys");
        Assert.Contains("Instance", sys.Skip);
        Assert.Contains("Secret", sys.Skip);

        var xcode = manager.Targets.First(e => e.Scope == "XCode");
        Assert.Contains("Secret", xcode.Skip);
    }

    #region SaveBoth 与 LoadView
    [Fact(DisplayName = "SaveBoth 写 Parameter 非跳过键，LoadView 读回且跳过键不写入")]
    public void SaveBoth_LoadView_RoundTrip()
    {
        const String appId = "ConfigSyncTest";
        const String scope = "Test";
        ResetSyncConfig();

        // 确保本地文件存在
        _ = SyncConfig.Current;
        Assert.True(File.Exists(_file));

        var manager = CreateManager(appId, scope, skip: ["Secret", "Sub:Url"]);

        var model = new SyncConfig { Name = "hello", Debug = false, Count = 7, Secret = "modelSecret" };
        model.Sub = new SyncSub { Url = "http://skip", Port = 99 };

        manager.SaveBoth<SyncConfig>(scope, model);

        // Parameter 只含非跳过键
        var names = ReadRows(_category).Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Name", names);
        Assert.Contains("Debug", names);
        Assert.Contains("Count", names);
        Assert.Contains("Sub:Port", names);
        Assert.DoesNotContain("Secret", names);
        Assert.DoesNotContain("Sub:Url", names);

        // LoadView 读回非跳过键，跳过键保留本地值
        var view = manager.LoadView<SyncConfig>(scope);
        Assert.Equal("hello", view.Name);
        Assert.False(view.Debug);
        Assert.Equal(7, view.Count);
        Assert.Equal(99, view.Sub.Port);
        Assert.Equal("secret", view.Secret);
        Assert.Equal("http://localhost", view.Sub.Url);

        // 本地文件同样写回非跳过键，跳过键保留本机值
        using var local = new XmlConfigProvider { FileName = _file };
        local.LoadAll();
        Assert.Equal("hello", local["Name"]);
        Assert.Equal("99", local["Sub:Port"]);
        Assert.Equal("secret", local["Secret"]);
        Assert.Equal("http://localhost", local["Sub:Url"]);
    }
    #endregion

    #region 时间戳同步
    [Fact(DisplayName = "Parameter 更新后 CheckNow 覆盖本地文件")]
    public void CheckNow_OverwritesWhenParamNewer()
    {
        const String appId = "ConfigSyncTest";
        const String scope = "Overwrite";
        ResetSyncConfig();

        _ = SyncConfig.Current;
        var manager = CreateManager(appId, scope);

        // 首次检查：把本地配置播种进 Parameter
        manager.CheckNow();

        // 本地文件时间回到过去，模拟库比本地新
        File.SetLastWriteTimeUtc(_file, DateTime.UtcNow.AddMinutes(-10));

        SetParameterValue(_category, "Name", "fromdb");

        manager.CheckNow();

        Assert.Contains("fromdb", File.ReadAllText(_file));
    }

    [Fact(DisplayName = "本地文件更新后 CheckNow 不覆盖本地")]
    public void CheckNow_KeepsLocalWhenLocalNewer()
    {
        const String appId = "ConfigSyncTest";
        const String scope = "LocalNewer";
        ResetSyncConfig();

        _ = SyncConfig.Current;
        var manager = CreateManager(appId, scope);

        manager.CheckNow();

        // Parameter 里改成其它值
        SetParameterValue(_category, "Name", "fromdb");

        // 本地文件更新为本地值，并把修改时间推到未来
        SyncConfig.Current.Name = "fromlocal";
        SyncConfig.Current.Save();
        File.SetLastWriteTimeUtc(_file, DateTime.UtcNow.AddMinutes(10));

        manager.CheckNow();

        Assert.Contains("fromlocal", File.ReadAllText(_file));
        Assert.DoesNotContain("fromdb", File.ReadAllText(_file));
    }

    [Fact(DisplayName = "配置文件被删除后下一轮检查重新应用 Parameter")]
    public void CheckNow_AppliesWhenFileMissing()
    {
        const String appId = "ConfigSyncTest";
        const String scope = "MissingFile";
        ResetSyncConfig();

        _ = SyncConfig.Current;
        var manager = CreateManager(appId, scope);

        manager.CheckNow();
        File.Delete(_file);
        Assert.False(File.Exists(_file));

        SetParameterValue(_category, "Name", "restored");

        manager.CheckNow();

        Assert.True(File.Exists(_file));
        Assert.Contains("restored", File.ReadAllText(_file));
    }

    [Fact(DisplayName = "应用后用文件时间记录库版本，重复检查不再改写文件")]
    public void CheckNow_MarksAppliedVersion()
    {
        const String appId = "ConfigSyncTest";
        const String scope = "Mark";
        ResetSyncConfig();

        _ = SyncConfig.Current;
        var manager = CreateManager(appId, scope);

        manager.CheckNow();
        File.SetLastWriteTimeUtc(_file, DateTime.UtcNow.AddMinutes(-10));

        SetParameterValue(_category, "Name", "fromdb");
        manager.CheckNow();

        // 本地文件时间被推进到库版本时间
        var paramMax = ReadRows(_category).Max(e => e.UpdateTime).ToUniversalTime();
        var appliedTime = File.GetLastWriteTimeUtc(_file);
        Assert.True(appliedTime >= paramMax, $"applied={appliedTime:O} paramMax={paramMax:O}");

        // 库里没有新版本，下一轮不再改写文件
        manager.CheckNow();
        Assert.Equal(appliedTime, File.GetLastWriteTimeUtc(_file));
        Assert.Contains("fromdb", File.ReadAllText(_file));
    }
    #endregion
}
