using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using NewLife;
using NewLife.Common;
using NewLife.Serialization;
using NewLife.Threading;

namespace XCode.Cache;

/// <summary>运行数据缓存</summary>
public class DataCache
{
    #region 静态
    private static DataCache? _Current;
    /// <summary>当前实例</summary>
    public static DataCache Current => _Current ??= Load();
    #endregion

    #region 属性
    /// <summary>名称</summary>
    public String Name { get; set; } = "XCode数据缓存，用于加速各实体类启动";
    #endregion

    #region 方法
    private static DataCache Load()
    {
        //var path = Path.GetTempPath().CombinePath(SysConfig.Current.Name);
        // 使用Data数据目录保存缓存数据，某些系统IIS无权访问Temp临时目录
        var path = NewLife.Setting.Current.DataPath;
        var file = path.CombinePath("DataCache.config").GetBasePath();
        var old = @"Config\DataCache.config".GetBasePath();

        if (!File.Exists(file) && File.Exists(old))
        {
            try
            {
                file.EnsureDirectory(true);
                File.Move(old, file);
            }
            catch
            {
                file = old;
            }
        }

        return Load(file, true)!;
    }

    private static void Save(DataCache? data)
    {
        //var path = Path.GetTempPath().CombinePath(SysConfig.Current.Name);
        var path = NewLife.Setting.Current.DataPath;
        var file = path.CombinePath("DataCache.config").GetBasePath();

        if (data != null)
            Save(file, data);
    }

    /// <summary>加载</summary>
    /// <param name="file"></param>
    /// <param name="create"></param>
    /// <returns></returns>
    public static DataCache? Load(String file, Boolean create = false)
    {
        DataCache? data = null;
        if (!file.IsNullOrEmpty() && File.Exists(file))
        {
            // 如果数据损坏，迟点异常
            try
            {
                data = File.ReadAllText(file).ToJsonEntity<DataCache>();
            }
            catch { }
        }

        if (data == null && create)
        {
            data = new DataCache();
            data.SaveAsync();
        }

        return data;
    }

    /// <summary>保存</summary>
    /// <param name="file"></param>
    /// <param name="data"></param>
    public static void Save(String file, DataCache data)
    {
        file.EnsureDirectory(true);
        var js = data.ToJson(true);

        try
        {
            File.WriteAllText(file, js, Encoding.UTF8);
        }
        catch (IOException) { }
    }

    private TimerX? _timer;
    /// <summary>异步保存</summary>
    public void SaveAsync()
    {
        _timer ??= new TimerX(s => Save(s as DataCache), this, 100, 10 * 60 * 1000) { Async = true };
        _timer.SetNext(100);
    }
    #endregion

    #region 总记录数
    /// <summary>每个表总记录数</summary>
    public IDictionary<String, Int64> Counts { get; set; } = new ConcurrentDictionary<String, Int64>();
    #endregion

    #region 字段缓存
    /// <summary>字段缓存，每个缓存项的值</summary>
    public IDictionary<String, Dictionary<String, String>> FieldCache { get; set; } = new ConcurrentDictionary<String, Dictionary<String, String>>();
    #endregion
}