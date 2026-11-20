namespace XCode.DataAccessLayer;

/// <summary>数据库架构迁移接口。负责 DDL 生成与执行</summary>
public interface ISchemaMigrator
{
    /// <summary>设置表模型，检查数据表是否匹配表模型，反向工程</summary>
    /// <param name="setting">迁移模式</param>
    /// <param name="tables">表模型集合</param>
    void SetTables(Migration setting, params IDataTable[] tables);

    /// <summary>获取 DDL 命令对应的 SQL 语句</summary>
    /// <param name="command">DDL 命令</param>
    /// <returns>数据定义语句；无 SQL 语义时返回 null</returns>
    String? GetCommandSql(DDLCommand command);

    /// <summary>执行 DDL 命令</summary>
    /// <param name="command">DDL 命令</param>
    /// <returns>执行结果</returns>
    Object? ExecuteCommand(DDLCommand command);

    /// <summary>获取数据定义语句。兼容入口，内部转发到 <see cref="GetCommandSql"/></summary>
    /// <param name="schema">数据定义模式</param>
    /// <param name="values">其它信息</param>
    /// <returns>数据定义语句</returns>
    String? GetSchemaSQL(DDLSchema schema, params Object?[] values);

    /// <summary>设置数据定义模式。兼容入口，内部转发到 <see cref="ExecuteCommand"/></summary>
    /// <param name="schema">数据定义模式</param>
    /// <param name="values">其它信息</param>
    /// <returns>执行结果</returns>
    Object? SetSchema(DDLSchema schema, params Object?[] values);

    /// <summary>建立表</summary>
    /// <param name="table">表模型</param>
    /// <returns>是否成功</returns>
    Boolean CreateTable(IDataTable table);

    /// <summary>删除表</summary>
    /// <param name="table">表模型</param>
    /// <returns>是否成功</returns>
    Boolean DropTable(IDataTable table);

    /// <summary>添加表说明</summary>
    /// <param name="table">表模型</param>
    /// <returns>是否成功</returns>
    Boolean AddTableDescription(IDataTable table);

    /// <summary>删除表说明</summary>
    /// <param name="table">表模型</param>
    /// <returns>是否成功</returns>
    Boolean DropTableDescription(IDataTable table);

    /// <summary>添加字段</summary>
    /// <param name="column">字段</param>
    /// <returns>是否成功</returns>
    Boolean AddColumn(IDataColumn column);

    /// <summary>修改字段</summary>
    /// <param name="column">字段</param>
    /// <param name="oldColumn">原字段。为 null 时忽略旧字段对比</param>
    /// <returns>是否成功</returns>
    Boolean AlterColumn(IDataColumn column, IDataColumn? oldColumn);

    /// <summary>重命名字段</summary>
    /// <param name="field">字段</param>
    /// <param name="newName">新名称</param>
    /// <returns>是否成功</returns>
    Boolean RenameColumn(IDataColumn field, String newName);

    /// <summary>删除字段</summary>
    /// <param name="column">字段</param>
    /// <returns>是否成功</returns>
    Boolean DropColumn(IDataColumn column);

    /// <summary>添加字段说明</summary>
    /// <param name="column">字段</param>
    /// <returns>是否成功</returns>
    Boolean AddColumnDescription(IDataColumn column);

    /// <summary>删除字段说明</summary>
    /// <param name="column">字段</param>
    /// <returns>是否成功</returns>
    Boolean DropColumnDescription(IDataColumn column);

    /// <summary>建立索引</summary>
    /// <param name="index">索引</param>
    /// <returns>是否成功</returns>
    Boolean CreateIndex(IDataIndex index);

    /// <summary>删除索引</summary>
    /// <param name="index">索引</param>
    /// <returns>是否成功</returns>
    Boolean DropIndex(IDataIndex index);
}
