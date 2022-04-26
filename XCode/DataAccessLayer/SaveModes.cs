namespace XCode.DataAccessLayer;

/// <summary>数据保存模式</summary>
public enum SaveModes
{
    /// <summary>标准插入</summary>
    Insert = 0,

    /// <summary>插入或更新，执行插入，主键已存在时更新</summary>
    Upsert = 1,

    /// <summary>插入或忽略，执行插入，主键已存在时跳过</summary>
    InsertIgnore = 2,

    /// <summary>插入或替换，执行插入，主键已存在时替换</summary>
    Replace = 3,
}