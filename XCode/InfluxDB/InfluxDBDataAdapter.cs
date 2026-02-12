using System.Data;
using System.Data.Common;

namespace XCode.InfluxDB;

/// <summary>InfluxDB数据适配器</summary>
public class InfluxDBDataAdapter : DbDataAdapter
{
    /// <summary>删除命令</summary>
    public new InfluxDBCommand? DeleteCommand { get; set; }

    /// <summary>插入命令</summary>
    public new InfluxDBCommand? InsertCommand { get; set; }

    /// <summary>选择命令</summary>
    public new InfluxDBCommand? SelectCommand { get; set; }

    /// <summary>更新命令</summary>
    public new InfluxDBCommand? UpdateCommand { get; set; }
}
