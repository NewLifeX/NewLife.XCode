using Microsoft.AspNetCore.Mvc;
using XCode.Membership;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static XCode.Membership.Log;

namespace Membership.Web.Areas.Admin.Controllers;

/// <summary>日志。应用系统审计日志，记录用户的各种操作，禁止修改和删除</summary>
[Menu(0, true, Icon = "fa-table")]
[AdminArea]
public class Log : ReadOnlyEntityController<Log>
{
    static Log()
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
        //    df.DataVisible = e => (e as Log).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as Log).Kind).ToString("X4");
        //}
        ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public Log(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<Log> Search(Pager p)
    {
        var category = p["category"];
        var action = p["action"];
        var linkId = p["linkId"].ToLong(-1);
        var createUserId = p["createUserId"].ToInt(-1);
        var success = p["success"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return Log.Search(category, action, linkId, createUserId, success, start, end, p["Q"], p);
    }
}