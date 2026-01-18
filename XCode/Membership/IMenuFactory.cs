using System.Reflection;

namespace XCode.Membership;

/// <summary>菜单工厂接口</summary>
public interface IMenuFactory
{
    /// <summary>根菜单</summary>
    IMenu Root { get; }

    /// <summary>根据编号找到菜单</summary>
    /// <param name="id"></param>
    /// <returns></returns>
    IMenu? FindByID(Int32 id);

    /// <summary>根据全名找到菜单</summary>
    /// <param name="fullName"></param>
    /// <returns></returns>
    IMenu FindByFullName(String fullName);

    /// <summary>根据Url找到菜单</summary>
    /// <param name="url"></param>
    /// <returns></returns>
    IMenu FindByUrl(String url);

    /// <summary>获取指定菜单下，当前用户有权访问的子菜单。</summary>
    /// <param name="menuid"></param>
    /// <param name="user"></param>
    /// <param name="inclInvisible"></param>
    /// <returns></returns>
    IList<IMenu> GetMySubMenus(Int32 menuid, IUser user, Boolean inclInvisible);

    ///// <summary>扫描命名空间下的控制器并添加为菜单</summary>
    ///// <param name="rootName"></param>
    ///// <param name="asm"></param>
    ///// <param name="nameSpace"></param>
    ///// <returns></returns>
    //IList<IMenu> ScanController(String rootName, Assembly asm, String nameSpace);
}