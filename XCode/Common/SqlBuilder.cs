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
        if (!orderby.IsNullOrEmpty()) return orderby;

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
                throw new XCodeException($"实体类[{factory.EntityType.Name}]不包含排序字段[{name}]");

            name = factory.Session.Dal.Db.FormatName(field);
            sb.Append(name);

            if (item.Value) sb.Append(" Desc");
        }

        return sb.Put(true);
    }

    /// <summary>获取排序子句</summary>
    /// <param name="page"></param>
    /// <returns></returns>
    public static String? GetOrderBy(this PageParameter page)
    {
        var orderby = page.OrderBy;
        if (!orderby.IsNullOrEmpty()) return orderby;

        orderby = page.Sort;
        if (orderby.IsNullOrEmpty()) return orderby;
        if (page.Desc && !orderby.EndsWithIgnoreCase(" Asc", " Desc")) orderby += " Desc";

        return orderby;
    }
}
