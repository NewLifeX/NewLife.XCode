using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace XCode.Extension;

/// <summary>数据缓存</summary>
[Serializable]
[DataObject]
[Description("数据缓存")]
[BindIndex("IU_TableCache_Name", true, "Name")]
[BindIndex("IX_MyDbCache_ExpiredTime", false, "ExpiredTime")]
[BindTable("MyDbCache", Description = "数据缓存", ConnName = "DbCache", DbType = DatabaseType.None)]
public partial class MyDbCache
{
    #region 属性
    private String _Name = null!;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(true, false, false, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String? _Value;
    /// <summary>键值</summary>
    [DisplayName("键值")]
    [Description("键值")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Value", "键值", "")]
    public String? Value { get => _Value; set { if (OnPropertyChanging("Value", value)) { _Value = value; OnPropertyChanged("Value"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private DateTime _ExpiredTime;
    /// <summary>过期时间</summary>
    [DisplayName("过期时间")]
    [Description("过期时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("ExpiredTime", "过期时间", "")]
    public DateTime ExpiredTime { get => _ExpiredTime; set { if (OnPropertyChanging("ExpiredTime", value)) { _ExpiredTime = value; OnPropertyChanged("ExpiredTime"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object? this[String name]
    {
        get => name switch
        {
            "Name" => _Name,
            "Value" => _Value,
            "CreateTime" => _CreateTime,
            "ExpiredTime" => _ExpiredTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Name": _Name = Convert.ToString(value); break;
                case "Value": _Value = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "ExpiredTime": _ExpiredTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    #endregion

    #region 字段名
    /// <summary>取得数据缓存字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>键值</summary>
        public static readonly Field Value = FindByName("Value");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>过期时间</summary>
        public static readonly Field ExpiredTime = FindByName("ExpiredTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得数据缓存字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>键值</summary>
        public const String Value = "Value";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>过期时间</summary>
        public const String ExpiredTime = "ExpiredTime";
    }
    #endregion
}
