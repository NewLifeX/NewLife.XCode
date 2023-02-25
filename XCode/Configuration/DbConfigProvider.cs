using NewLife;
using NewLife.Configuration;
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
    public String Category { get; set; }

    /// <summary>更新周期。默认10秒，0秒表示不做自动更新</summary>
    public Int32 Period { get; set; } = 10;

    private IDictionary<String, Object> _cache;
    #endregion

    #region 方法

    /// <summary>获取所有配置</summary>
    /// <returns></returns>
    protected virtual IDictionary<String, Object> GetAll()
    {
        var dic = new Dictionary<String, Object>(StringComparer.CurrentCultureIgnoreCase);

        var list = Parameter.FindAllByUserID(UserId, Category);
        foreach (var item in list)
        {
            if (!item.Enable) continue;

            dic[item.Name] = item.Value;

            if (!item.Remark.IsNullOrEmpty())
                dic["#" + item.Name] = item.Remark;
        }

        return dic;
    }

    /// <summary>加载配置字典为配置树</summary>
    /// <param name="configs"></param>
    /// <returns></returns>
    public virtual IConfigSection Build(IDictionary<String, Object> configs)
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
            }

            if (item.Value is IDictionary<String, Object> dic)
                section.Childs = Build(dic).Childs;
            else
                section.Value = item.Value + "";

            // 加载备注
            if (configs.TryGetValue("#" + item.Key, out var remark))
                section.Comment = remark as String;
        }
        return root;
    }

    /// <summary>加载配置</summary>
    public override Boolean LoadAll()
    {
        //// 换个对象，避免数组元素在多次加载后重叠
        //var root = new ConfigSection { };

        //var list = Parameter.FindAllByUserID(UserId, Category);
        //foreach (var item in list)
        //{
        //    if (!item.Enable) continue;

        //    var section = root.GetOrAddChild(item.Name);

        //    section.Value = item.Value;
        //    section.Comment = item.Remark;
        //}
        //Root = root;

        var dic = GetAll();
        Root = Build(dic);

        // 缓存
        _cache = dic;

        // 自动更新
        if (Period > 0) InitTimer();

        return true;
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

    void Save(IList<Parameter> list, IConfigSection root, String prefix)
    {
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

                pi.Value = section.Value;
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
    public override void Bind<T>(T model, Boolean autoReload = true, String path = null)
    {
        base.Bind<T>(model, autoReload, path);

        if (autoReload) InitTimer();
    }
    #endregion

    #region 定时
    /// <summary>定时器</summary>
    protected TimerX _timer;
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
        var dic = GetAll();
        if (dic == null) return;

        var changed = new Dictionary<String, Object>();
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
            _cache = dic;

            NotifyChange();
        }
    }
    #endregion
}