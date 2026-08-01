using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace XUnitTest.XCode.Cache;

/// <summary>字段缓存测试角色</summary>
[Serializable]
[DataObject]
[Description("字段缓存测试角色")]
[BindTable("FieldCacheRole", Description = "字段缓存测试角色", ConnName = "FieldCacheTest", DbType = DatabaseType.None)]
public partial class FieldCacheRole : Entity<FieldCacheRole>
{
    #region 属性
    private Int32 _ID;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("ID", "编号", "", DataScale = "identity:True")]
    public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "名称", "")]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns>字段值</returns>
    public override Object? this[String name]
    {
        get => name switch
        {
            "ID" => _ID,
            "Name" => _Name,
            _ => base[name],
        };
        set
        {
            switch (name)
            {
                case "ID": _ID = value.ToInt(); break;
                case "Name": _Name = (String)value!; break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion
}
