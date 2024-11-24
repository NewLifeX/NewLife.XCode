using NewLife;
using NewLife.Model;
using XCode.DataAccessLayer;

namespace XCode.Code;

/// <summary>代码生成插件</summary>
public interface ICodePlugin : IPlugin
{
    /// <summary>修正数据表</summary>
    /// <param name="tables"></param>
    void FixTables(IList<IDataTable> tables);
}

/// <summary>代码生成插件基类</summary>
[Plugin("CodeBuild")]
public abstract class CodePlugin : DisposeBase, ICodePlugin
{
    /// <summary>服务提供者</summary>
    public IServiceProvider? Provider { get; set; }

    /// <summary>初始化插件</summary>
    /// <param name="identity"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public virtual Boolean Init(String? identity, IServiceProvider provider)
    {
        if (identity != "CodeBuild") return false;

        Provider = provider;

        return true;
    }

    /// <summary>修正数据表</summary>
    /// <param name="tables"></param>
    public virtual void FixTables(IList<IDataTable> tables) { }
}