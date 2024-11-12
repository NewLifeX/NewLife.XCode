using XCode.Configuration;

namespace XCode.Statistics;

/// <summary>聚合方式</summary>
public enum StatModes
{
    /// <summary>最大值</summary>
    Max = 1,

    /// <summary>最小值</summary>
    Min = 2,

    /// <summary>平均值</summary>
    Avg = 3,

    /// <summary>求和</summary>
    Sum = 4,

    /// <summary>计数</summary>
    Count = 5,
}

/// <summary>统计字段</summary>
public class StatField(FieldItem field, StatModes mode)
{
    /// <summary>字段</summary>
    public FieldItem Field { get; set; } = field;

    /// <summary>统计模式</summary>
    public StatModes Mode { get; set; } = mode;
}
