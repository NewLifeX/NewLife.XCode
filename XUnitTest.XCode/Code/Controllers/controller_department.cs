using Microsoft.AspNetCore.Mvc;
using XCode.Membership;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static XCode.Membership.Department;

namespace Membership.Web.Areas.Admin.Controllers;

/// <summary>部门。组织机构，多级树状结构，支持多租户</summary>
[Menu(0, true, Icon = "fa-table")]
[AdminArea]
public class Department : EntityController<Department>
{
    static Department()
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
        //    df.DataVisible = e => (e as Department).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as Department).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public Department(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<Department> Search(Pager p)
    {
        var tenantId = p["tenantId"].ToInt(-1);
        var code = p["code"];
        var name = p["name"];
        var parentId = p["parentId"].ToInt(-1);
        var visible = p["visible"]?.ToBoolean();
        var managerId = p["managerId"].ToInt(-1);
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return Department.Search(tenantId, code, name, parentId, visible, managerId, enable, start, end, p["Q"], p);
    }
}