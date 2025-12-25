using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace XCode.Membership666;

/// <summary>地区。行政区划数据，最高支持四级地址，9位数字</summary>
public partial interface IArea
{
    #region 属性
    /// <summary>编码。行政区划编码</summary>
    Int32 ID { get; set; }

    /// <summary>名称</summary>
    String Name { get; set; }

    /// <summary>全名</summary>
    String? FullName { get; set; }

    /// <summary>父级</summary>
    Int32 ParentID { get; set; }

    /// <summary>层级</summary>
    Int32 Level { get; set; }

    /// <summary>类型。省市县，自治州等</summary>
    String? Kind { get; set; }

    /// <summary>英文名</summary>
    String? English { get; set; }

    /// <summary>拼音</summary>
    String? PinYin { get; set; }

    /// <summary>简拼</summary>
    String? JianPin { get; set; }

    /// <summary>区号。电话区号</summary>
    String? TelCode { get; set; }

    /// <summary>邮编。邮政编码</summary>
    String? ZipCode { get; set; }

    /// <summary>经度</summary>
    Double Longitude { get; set; }

    /// <summary>纬度</summary>
    Double Latitude { get; set; }

    /// <summary>地址编码。字符串前缀相同越多，地理距离越近，8位精度19米，6位610米</summary>
    String? GeoHash { get; set; }

    /// <summary>启用</summary>
    Boolean Enable { get; set; }

    /// <summary>创建时间</summary>
    DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    DateTime UpdateTime { get; set; }

    /// <summary>备注</summary>
    String? Remark { get; set; }
    #endregion
}
