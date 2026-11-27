namespace XCode;

/// <summary>实体操作类型。包含创建、添删改、Upsert/Replace 与查询</summary>
/// <remarks>
/// 历史名称为“数据操作方法”，保留给 <see cref="EntityBase.Valid(DataMethod)"/> 等既有 API。
/// 批量写入通过 <see cref="EntityContext.Entities"/> 区分，不单独定义枚举值。
/// </remarks>
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

    /// <summary>创建实体对象。仅用于过滤器，不写数据库</summary>
    Create = 13,

    /// <summary>查询单个实体</summary>
    Find = 21,

    /// <summary>查询实体列表</summary>
    FindAll = 22,

    /// <summary>查询记录数</summary>
    FindCount = 23,

    /// <summary>原始SQL查询</summary>
    FindSQL = 24,
}