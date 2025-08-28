using Microsoft.AspNetCore.Mvc;
using XCode.Membership;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static XCode.Membership.Parameter;

namespace Membership.Web.Areas.Admin.Controllers;

/// <summary>字典参数。管理用户或系统全局的名值对数据，常用于参数配置场合</summary>
[Menu(0, true, Icon = "fa-table")]
[AdminArea]
public class Parameter : EntityController<Parameter>
{
    static Parameter()
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
        //    df.DataVisible = e => (e as Parameter).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as Parameter).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public Parameter(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<Parameter> Search(Pager p)
    {
        var userId = p["userId"].ToInt(-1);
        var category = p["category"];
        var kind = (XCode.Membership.ParameterKinds)p["kind"].ToInt(-1);
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return Parameter.Search(userId, category, kind, enable, start, end, p["Q"], p);
    }
}