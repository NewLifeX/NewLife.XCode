using System.Text.RegularExpressions;
using NewLife;
using NewLife.Collections;
using NewLife.Data;

namespace XCode;

/// <summary>Sql构造器</summary>
public static class SqlBuilder
{
    /// <summary>根据排序参数生成排序字句，严格要求排序字段必须是数据字段</summary>
    /// <param name="page"></param>
    /// <param name="factory"></param>
    /// <returns></returns>
    public static String? BuildOrder(PageParameter page, IEntityFactory factory)
    {
        var orderby = page.OrderBy;
        if (!orderby.IsNullOrEmpty()) return orderby.CheckOrderBy();

        var dic = new Dictionary<String, Boolean>();
        if (!page.Sort.IsNullOrEmpty())
        {
            foreach (var item in page.Sort.Split(","))
            {
                var line = item.Trim();
                if (line.EndsWithIgnoreCase(" Desc"))
                {
                    dic[line.Substring(0, line.Length - 5).Trim()] = true;
                }
                else if (line.EndsWithIgnoreCase(" Asc"))
                {
                    dic[line.Substring(0, line.Length - 4).Trim()] = false;
                }
                else
                {
                    dic[line] = page.Desc;
                }
            }
        }
        //else if (!orderby.IsNullOrEmpty())
        //{
        //    foreach (var item in orderby.Split(","))
        //    {
        //        var line = item.Trim();
        //        if (line.EndsWithIgnoreCase(" Desc"))
        //        {
        //            dic[line.Substring(0, line.Length - 5).Trim()] = true;
        //        }
        //        else if (line.EndsWithIgnoreCase(" Asc"))
        //        {
        //            dic[line.Substring(0, line.Length - 4).Trim()] = false;
        //        }
        //        else
        //        {
        //            dic[line] = false;
        //        }
        //    }
        //}

        if (dic.Count == 0) return orderby;

        // 逐个检测并修正字段名
        var sb = Pool.StringBuilder.Get();
        foreach (var item in dic)
        {
            if (sb.Length > 0) sb.Append(",");

            var name = item.Key;
            var field = factory.Table.FindByName(name);

            // 严格要求排序字段必须是数据字段
            if (field is null)
                throw new XCodeException("Entity class [{0}] does not contain sort field [{1}].", factory.EntityType.Name, name);

            name = factory.Session.Dal.Db.FormatName(field);
            sb.Append(name);

            if (item.Value) sb.Append(" Desc");
        }

        return sb.Return(true);
    }

    /// <summary>获取排序子句</summary>
    /// <param name="page"></param>
    /// <returns></returns>
    public static String? GetOrderBy(this PageParameter page)
    {
        var orderby = page.OrderBy;
        if (!orderby.IsNullOrEmpty()) return orderby.CheckOrderBy();

        orderby = page.Sort;
        if (orderby.IsNullOrEmpty()) return orderby;
        if (page.Desc && !orderby.EndsWithIgnoreCase(" Asc", " Desc")) orderby += " Desc";

        return orderby.CheckOrderBy();
    }

    private static readonly Regex _orderByRegex = new(@"^\s*[\w\[\]`""\.]+(\s+(Asc|Desc))?(\s*,\s*[\w\[\]`""\.]+(\s+(Asc|Desc))?)*\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>校验排序子句，只允许字段名与升降序标记，防止排序位注入</summary>
    /// <remarks>
    /// 排序参数往往直接来自前端查询串，若拼入原生SQL而不校验，可借助子查询、注释或分号实施盲注。
    /// 此处限定为逗号分隔的标识符列表，每项可带 Asc/Desc，禁止括号、引号以外的符号与空白分隔的其他内容。
    /// </remarks>
    /// <param name="orderby">排序子句</param>
    /// <returns>原排序子句</returns>
    /// <exception cref="XCodeException">排序子句包含非法字符时抛出</exception>
    public static String? CheckOrderBy(this String? orderby)
    {
        if (orderby.IsNullOrEmpty()) return orderby;

        if (!_orderByRegex.IsMatch(orderby)) throw new XCodeException(L.T("Invalid sort field [{0}].", orderby));

        return orderby;
    }
}
