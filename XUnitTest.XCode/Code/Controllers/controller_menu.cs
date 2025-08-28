using Microsoft.AspNetCore.Mvc;
using XCode.Membership;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static XCode.Membership.Menu;

namespace Membership.Web.Areas.Admin.Controllers;

/// <summary>菜单。功能权限，大多数时候也是可见页面</summary>
[Menu(0, true, Icon = "fa-table")]
[AdminArea]
public class Menu : EntityController<Menu>
{
    static Menu()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();

        //{
        //    var df = ListFields.GetField("Code") as ListField;
        //    df.Url = "?code={Code}";
        //    df.Target = "_blank";
        //}
        //{
        //    var df = ListFields.AddListField("devices", null, "Onlines");
        //    df.DisplayName = "查看设备";
        //    df.Url = "Device?groupId={Id}";
        //    df.DataVisible = e => (e as Menu).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as Menu).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public Menu(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<Menu> Search(Pager p)
    {
        var parentId = p["parentId"].ToInt(-1);
        var visible = p["visible"]?.ToBoolean();
        var necessary = p["necessary"]?.ToBoolean();
        var newWindow = p["newWindow"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return Menu.Search(parentId, visible, necessary, newWindow, start, end, p["Q"], p);
    }
}