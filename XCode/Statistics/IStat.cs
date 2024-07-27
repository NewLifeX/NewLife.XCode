namespace XCode.Statistics;

/// <summary>统计接口</summary>
public interface IStat
{
    /// <summary>层级</summary>
    StatLevels Level { get; set; }

    /// <summary>时间</summary>
    DateTime Time { get; set; }

    /// <summary>创建时间</summary>
    DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    DateTime UpdateTime { get; set; }
}