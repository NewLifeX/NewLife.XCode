namespace XCode;

/// <summary>数据操作方法。添删改</summary>
public enum DataMethod
{
    /// <summary>新增</summary>
    Insert = 1,

    /// <summary>更新</summary>
    Update = 2,

    /// <summary>删除</summary>
    Delete = 3,

    /// <summary>[专用]插入或更新</summary>
    Upsert = 11,

    /// <summary>[专用]替换</summary>
    Replace = 12,
}