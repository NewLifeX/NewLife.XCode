using NewLife;

namespace XCode.DataAccessLayer;

/// <summary>数据库元数据接口</summary>
public interface IMetaData : IDisposable2
{
    #region 属性
    /// <summary>数据库</summary>
    IDatabase Database { get; }

    /// <summary>所有元数据集合</summary>
    ICollection<String> MetaDataCollections { get; }

    /// <summary>保留关键字</summary>
    ICollection<String> ReservedWords { get; }
    #endregion

    #region 构架
    /// <summary>取得表模型，正向工程</summary>
    /// <returns></returns>
    IList<IDataTable> GetTables();

    /// <summary>
    /// 取得所有表名
    /// </summary>
    /// <returns></returns>
    IList<String> GetTableNames();

    /// <summary>设置表模型，检查数据表是否匹配表模型，反向工程</summary>
    /// <param name="setting">设置</param>
    /// <param name="tables"></param>
    void SetTables(Migration setting, params IDataTable[] tables);

    /// <summary>获取数据定义语句</summary>
    /// <param name="schema">数据定义模式</param>
    /// <param name="values">其它信息</param>
    /// <returns>数据定义语句</returns>
    String? GetSchemaSQL(DDLSchema schema, params Object?[] values);

    /// <summary>设置数据定义模式</summary>
    /// <param name="schema">数据定义模式</param>
    /// <param name="values">其它信息</param>
    /// <returns></returns>
    Object? SetSchema(DDLSchema schema, params Object?[] values);
    #endregion

    #region DDL 执行方法
    /// <summary>建立数据库</summary>
    /// <param name="databaseName">数据库名</param>
    /// <param name="file">数据文件路径</param>
    /// <returns>是否成功</returns>
    Boolean CreateDatabase(String databaseName, String? file = null);

    /// <summary>删除数据库</summary>
    /// <param name="databaseName">数据库名</param>
    /// <returns>是否成功</returns>
    Boolean DropDatabase(String databaseName);

    /// <summary>数据库是否存在</summary>
    /// <param name="databaseName">数据库名</param>
    /// <returns></returns>
    Boolean DatabaseExist(String? databaseName);

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
    /// <param name="oldColumn">原字段。为null时忽略旧字段对比</param>
    /// <returns>是否成功</returns>
    Boolean AlterColumn(IDataColumn column, IDataColumn? oldColumn);

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

    /// <summary>备份数据库</summary>
    /// <param name="dbName">数据库名</param>
    /// <param name="backupFile">备份文件</param>
    /// <returns>备份文件路径</returns>
    String? BackupDatabase(String? dbName = null, String? backupFile = null);

    /// <summary>还原数据库</summary>
    /// <param name="backupFile">备份文件</param>
    /// <param name="recoverDir">还原目录</param>
    /// <returns>是否成功</returns>
    Boolean RestoreDatabase(String backupFile, String? recoverDir = null);

    /// <summary>收缩数据库</summary>
    /// <returns>是否成功</returns>
    Boolean CompactDatabase();
    #endregion
}