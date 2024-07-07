using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace XCode.Membership666;

/// <summary>地区。行政区划数据，最高支持四级地址，9位数字</summary>
public partial class AreaModel : IArea
{
    #region 属性
    /// <summary>编码。行政区划编码</summary>
    public Int32 ID { get; set; }

    /// <summary>名称</summary>
    public String? Name { get; set; }

    /// <summary>全名</summary>
    public String? FullName { get; set; }

    /// <summary>父级</summary>
    public Int32 ParentID { get; set; }

    /// <summary>层级</summary>
    public Int32 Level { get; set; }

    /// <summary>类型。省市县，自治州等</summary>
    public String? Kind { get; set; }

    /// <summary>英文名</summary>
    public String? English { get; set; }

    /// <summary>拼音</summary>
    public String? PinYin { get; set; }

    /// <summary>简拼</summary>
    public String? JianPin { get; set; }

    /// <summary>区号。电话区号</summary>
    public String? TelCode { get; set; }

    /// <summary>邮编。邮政编码</summary>
    public String? ZipCode { get; set; }

    /// <summary>经度</summary>
    public Double Longitude { get; set; }

    /// <summary>纬度</summary>
    public Double Latitude { get; set; }

    /// <summary>地址编码。字符串前缀相同越多，地理距离越近，8位精度19米，6位610米</summary>
    public String? GeoHash { get; set; }

    /// <summary>启用</summary>
    public Boolean Enable { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }

    /// <summary>备注</summary>
    public String? Remark { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IArea model)
    {
        ID = model.ID;
        Name = model.Name;
        FullName = model.FullName;
        ParentID = model.ParentID;
        Level = model.Level;
        Kind = model.Kind;
        English = model.English;
        PinYin = model.PinYin;
        JianPin = model.JianPin;
        TelCode = model.TelCode;
        ZipCode = model.ZipCode;
        Longitude = model.Longitude;
        Latitude = model.Latitude;
        GeoHash = model.GeoHash;
        Enable = model.Enable;
        CreateTime = model.CreateTime;
        UpdateTime = model.UpdateTime;
        Remark = model.Remark;
    }
    #endregion
}
