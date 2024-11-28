using XCode.DataAccessLayer;

namespace XCode.Model;

/// <summary>批操作选项</summary>
public class BatchOption
{
    ///// <summary>指定会话，分表分库时必用</summary>
    //public IEntitySession? Session { get; set; }

    /// <summary>字段集合。为空时表示使用所有字段</summary>
    public IDataColumn[]? Columns { get; set; }

    /// <summary>要更新的字段。用于Update/Upsert，默认脏数据。使用属性名而非字段名，各数据库生成SQL时从columns取字段名</summary>
    public ICollection<String>? UpdateColumns { get; set; }

    /// <summary>要累加更新的字段。用于Update/Upsert，默认累加。使用属性名而非字段名，各数据库生成SQL时从columns取字段名</summary>
    public ICollection<String>? AddColumns { get; set; }

    /// <summary>是否完全插入所有字段。用于Insert/Upsert，默认false表示不插入没有脏数据的字段</summary>
    public Boolean FullInsert { get; set; }

    /// <summary>批操作分批大小。默认0，使用数据链接设置或全局设置</summary>
    public Int32 BatchSize { get; set; }

    /// <summary>实例化批操作选项</summary>
    public BatchOption() { }

    /// <summary>实例化批操作选项</summary>
    public BatchOption(IDataColumn[]? columns, ICollection<String>? updateColumns, ICollection<String>? addColumns)
    {
        Columns = columns;
        UpdateColumns = updateColumns;
        AddColumns = addColumns;
    }
}
