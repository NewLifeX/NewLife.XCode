using System;
using System.Linq;
using NewLife.Configuration;
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
    #endregion

    #region 方法
    /// <summary>加载配置</summary>
    public override Boolean LoadAll()
    {
        // 换个对象，避免数组元素在多次加载后重叠
        var root = new ConfigSection { };

        var list = Parameter.FindAllByUserID(UserId, Category);
        foreach (var item in list)
        {
            if (!item.Enable) continue;

            //if (item.Category.IsNullOrEmpty())
            //{
            var section = root.GetOrAddChild(item.Name);

            section.Value = item.Value;
            section.Comment = item.Remark;
            //}
            //else
            //{
            //    var category = root.GetOrAddChild(item.Category);
            //    var section = category.GetOrAddChild(item.Name);

            //    section.Value = item.Value;
            //    section.Comment = item.Remark;
            //}
        }
        Root = root;

        return true;
    }

    /// <summary>保存配置树到数据源</summary>
    public override Boolean SaveAll()
    {
        var list = Parameter.FindAllByUserID(UserId, Category);
        foreach (var section in Root.Childs)
        {
            //if (section.Childs != null && section.Childs.Count > 0)
            //{
            //    foreach (var sec in section.Childs)
            //    {
            //        var pi = list.FirstOrDefault(_ => _.Category == section.Key && _.Name == sec.Key);
            //        if (pi == null)
            //        {
            //            pi = new Parameter { Category = section.Key, Name = sec.Key };
            //            list.Add(pi);
            //        }

            //        pi.Value = sec.Value;
            //        pi.UserID = UserId;
            //        pi.Enable = true;
            //        pi.Remark = sec.Comment;
            //    }
            //}
            //else
            //{
            var pi = list.FirstOrDefault(_ => _.Name == section.Key);
            if (pi == null)
            {
                pi = new Parameter { Category = Category, Name = section.Key };
                list.Add(pi);
            }

            pi.Value = section.Value;
            pi.UserID = UserId;
            pi.Enable = true;
            pi.Remark = section.Comment;
            //}
        }
        list.Save();

        // 通知绑定对象，配置数据有改变
        NotifyChange();

        return true;
    }
    #endregion
}