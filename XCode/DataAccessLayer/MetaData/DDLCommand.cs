using NewLife;

namespace XCode.DataAccessLayer;

/// <summary>DDL 命令。用类型化参数描述建库、建表、改列、索引与维护操作</summary>
/// <remarks>
/// 命令对象用于替代 <see cref="DDLSchema"/> 加 <c>Object[]</c> 的调用方式；
/// 旧的 <see cref="ISchemaMigrator.GetSchemaSQL"/> 与 <see cref="ISchemaMigrator.SetSchema"/>
/// 仍作为兼容入口转发到命令对象。
/// </remarks>
public sealed class DDLCommand
{
    #region 属性
    /// <summary>操作类型</summary>
    public DDLSchema Schema { get; }

    /// <summary>数据库名。建库、删库、库存在性或备份时使用</summary>
    public String? DatabaseName { get; private set; }

    /// <summary>数据库文件路径。建库时使用</summary>
    public String? File { get; private set; }

    /// <summary>表模型。建表、删表或表说明操作时使用</summary>
    public IDataTable? Table { get; private set; }

    /// <summary>字段模型。加列、改列、删列或字段说明操作时使用</summary>
    public IDataColumn? Column { get; private set; }

    /// <summary>原字段模型。改列时用于对比</summary>
    public IDataColumn? OldColumn { get; private set; }

    /// <summary>索引模型。建索引或删索引时使用</summary>
    public IDataIndex? Index { get; private set; }

    /// <summary>新字段名。重命名列时使用</summary>
    public String? NewName { get; private set; }

    /// <summary>备份文件。备份或还原时使用</summary>
    public String? BackupFile { get; private set; }

    /// <summary>还原目录。还原时使用</summary>
    public String? RecoverDir { get; private set; }
    #endregion

    #region 构造
    private DDLCommand(DDLSchema schema) => Schema = schema;
    #endregion

    #region 工厂
    /// <summary>创建建库命令</summary>
    /// <param name="databaseName">数据库名；为空时仅用于生成提示 SQL</param>
    /// <param name="file">数据库文件路径</param>
    /// <returns>DDL 命令</returns>
    public static DDLCommand CreateDatabase(String? databaseName, String? file = null) => new(DDLSchema.CreateDatabase)
    {
        DatabaseName = databaseName,
        File = file,
    };

    /// <summary>创建删库命令</summary>
    /// <param name="databaseName">数据库名</param>
    /// <returns>DDL 命令</returns>
    public static DDLCommand DropDatabase(String databaseName)
    {
        if (databaseName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(databaseName));

        return new(DDLSchema.DropDatabase) { DatabaseName = databaseName };
    }

    /// <summary>创建数据库存在性检查命令</summary>
    /// <param name="databaseName">数据库名</param>
    /// <returns>DDL 命令</returns>
    public static DDLCommand DatabaseExist(String? databaseName) => new(DDLSchema.DatabaseExist) { DatabaseName = databaseName };

    /// <summary>创建建表命令</summary>
    /// <param name="table">表模型</param>
    /// <returns>DDL 命令</returns>
    public static DDLCommand CreateTable(IDataTable table) => WithTable(DDLSchema.CreateTable, table);

    /// <summary>创建删表命令</summary>
    /// <param name="table">表模型</param>
    /// <returns>DDL 命令</returns>
    public static DDLCommand DropTable(IDataTable table) => WithTable(DDLSchema.DropTable, table);

    /// <summary>创建添加表说明命令</summary>
    /// <param name="table">表模型</param>
    /// <returns>DDL 命令</returns>
    public static DDLCommand AddTableDescription(IDataTable table) => WithTable(DDLSchema.AddTableDescription, table);

    /// <summary>创建删除表说明命令</summary>
    /// <param name="table">表模型</param>
    /// <returns>DDL 命令</returns>
    public static DDLCommand DropTableDescription(IDataTable table) => WithTable(DDLSchema.DropTableDescription, table);

    /// <summary>创建添加字段命令</summary>
    /// <param name="column">字段模型</param>
    /// <returns>DDL 命令</returns>
    public static DDLCommand AddColumn(IDataColumn column) => WithColumn(DDLSchema.AddColumn, column);

    /// <summary>创建修改字段命令</summary>
    /// <param name="column">字段模型</param>
    /// <param name="oldColumn">原字段模型，可为空</param>
    /// <returns>DDL 命令</returns>
    public static DDLCommand AlterColumn(IDataColumn column, IDataColumn? oldColumn = null) => new(DDLSchema.AlterColumn)
    {
        Column = column ?? throw new ArgumentNullException(nameof(column)),
        OldColumn = oldColumn,
    };

    /// <summary>创建重命名字段命令</summary>
    /// <param name="column">字段模型</param>
    /// <param name="newName">新字段名</param>
    /// <returns>DDL 命令</returns>
    public static DDLCommand RenameColumn(IDataColumn column, String newName)
    {
        if (column == null) throw new ArgumentNullException(nameof(column));
        if (newName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(newName));

        return new(DDLSchema.RenameColumn)
        {
            Column = column,
            NewName = newName,
        };
    }

    /// <summary>创建删除字段命令</summary>
    /// <param name="column">字段模型</param>
    /// <returns>DDL 命令</returns>
    public static DDLCommand DropColumn(IDataColumn column) => WithColumn(DDLSchema.DropColumn, column);

    /// <summary>创建添加字段说明命令</summary>
    /// <param name="column">字段模型</param>
    /// <returns>DDL 命令</returns>
    public static DDLCommand AddColumnDescription(IDataColumn column) => WithColumn(DDLSchema.AddColumnDescription, column);

    /// <summary>创建删除字段说明命令</summary>
    /// <param name="column">字段模型</param>
    /// <returns>DDL 命令</returns>
    public static DDLCommand DropColumnDescription(IDataColumn column) => WithColumn(DDLSchema.DropColumnDescription, column);

    /// <summary>创建建索引命令</summary>
    /// <param name="index">索引模型</param>
    /// <returns>DDL 命令</returns>
    public static DDLCommand CreateIndex(IDataIndex index) => WithIndex(DDLSchema.CreateIndex, index);

    /// <summary>创建删索引命令</summary>
    /// <param name="index">索引模型</param>
    /// <returns>DDL 命令</returns>
    public static DDLCommand DropIndex(IDataIndex index) => WithIndex(DDLSchema.DropIndex, index);

    /// <summary>创建备份数据库命令</summary>
    /// <param name="databaseName">数据库名</param>
    /// <param name="backupFile">备份文件</param>
    /// <returns>DDL 命令</returns>
    public static DDLCommand BackupDatabase(String? databaseName = null, String? backupFile = null) => new(DDLSchema.BackupDatabase)
    {
        DatabaseName = databaseName,
        BackupFile = backupFile,
    };

    /// <summary>创建还原数据库命令</summary>
    /// <param name="backupFile">备份文件</param>
    /// <param name="recoverDir">还原目录</param>
    /// <returns>DDL 命令</returns>
    public static DDLCommand RestoreDatabase(String backupFile, String? recoverDir = null)
    {
        if (backupFile.IsNullOrEmpty()) throw new ArgumentNullException(nameof(backupFile));

        return new(DDLSchema.RestoreDatabase)
        {
            BackupFile = backupFile,
            RecoverDir = recoverDir,
        };
    }

    /// <summary>创建收缩数据库命令</summary>
    /// <returns>DDL 命令</returns>
    public static DDLCommand CompactDatabase() => new(DDLSchema.CompactDatabase);
    #endregion

    #region 兼容
    /// <summary>从旧版模式与参数创建命令</summary>
    /// <param name="schema">数据定义模式</param>
    /// <param name="values">旧版参数</param>
    /// <returns>DDL 命令</returns>
    internal static DDLCommand From(DDLSchema schema, Object?[]? values)
    {
        values ??= new Object?[0];

        return schema switch
        {
            DDLSchema.CreateDatabase => CreateDatabase(values.Length > 0 ? values[0] as String : null, values.Length > 1 ? values[1] as String : null),
            DDLSchema.DropDatabase => DropDatabase((String)values[0]!),
            DDLSchema.DatabaseExist => DatabaseExist(values.Length > 0 ? values[0] as String : null),
            DDLSchema.CreateTable => CreateTable((IDataTable)values[0]!),
            DDLSchema.DropTable => DropTable((IDataTable)values[0]!),
            DDLSchema.AddTableDescription => AddTableDescription((IDataTable)values[0]!),
            DDLSchema.DropTableDescription => DropTableDescription((IDataTable)values[0]!),
            DDLSchema.AddColumn => AddColumn((IDataColumn)values[0]!),
            DDLSchema.AlterColumn => AlterColumn((IDataColumn)values[0]!, values.Length > 1 ? (IDataColumn?)values[1] : null),
            DDLSchema.RenameColumn => RenameColumn((IDataColumn)values[0]!, (String)values[1]!),
            DDLSchema.DropColumn => DropColumn((IDataColumn)values[0]!),
            DDLSchema.AddColumnDescription => AddColumnDescription((IDataColumn)values[0]!),
            DDLSchema.DropColumnDescription => DropColumnDescription((IDataColumn)values[0]!),
            DDLSchema.CreateIndex => CreateIndex((IDataIndex)values[0]!),
            DDLSchema.DropIndex => DropIndex((IDataIndex)values[0]!),
            DDLSchema.BackupDatabase => BackupDatabase(values.Length > 0 ? values[0] as String : null, values.Length > 1 ? values[1] as String : null),
            DDLSchema.RestoreDatabase => RestoreDatabase((String)values[0]!, values.Length > 1 ? values[1] as String : null),
            DDLSchema.CompactDatabase => CompactDatabase(),
            _ => throw new NotSupportedException(L.T("This operation is not supported!")),
        };
    }
    #endregion

    #region 辅助
    private static DDLCommand WithTable(DDLSchema schema, IDataTable table)
    {
        if (table == null) throw new ArgumentNullException(nameof(table));

        return new(schema) { Table = table };
    }

    private static DDLCommand WithColumn(DDLSchema schema, IDataColumn column)
    {
        if (column == null) throw new ArgumentNullException(nameof(column));

        return new(schema) { Column = column };
    }

    private static DDLCommand WithIndex(DDLSchema schema, IDataIndex index)
    {
        if (index == null) throw new ArgumentNullException(nameof(index));

        return new(schema) { Index = index };
    }
    #endregion

    #region 日志
    /// <summary>返回命令的可读描述</summary>
    /// <returns>命令描述</returns>
    public override String ToString() => Schema switch
    {
        DDLSchema.CreateDatabase => $"{Schema}({DatabaseName},{File})",
        DDLSchema.DropDatabase or DDLSchema.DatabaseExist => $"{Schema}({DatabaseName})",
        DDLSchema.AddTableDescription or DDLSchema.DropTableDescription => $"{Schema}({Table?.TableName})",
        DDLSchema.AddColumn or DDLSchema.DropColumn or DDLSchema.AddColumnDescription or DDLSchema.DropColumnDescription => $"{Schema}({Column?.ColumnName})",
        DDLSchema.AlterColumn => $"{Schema}({Column?.ColumnName},{OldColumn?.ColumnName})",
        DDLSchema.RenameColumn => $"{Schema}({Column?.ColumnName},{NewName})",
        DDLSchema.CreateIndex or DDLSchema.DropIndex => $"{Schema}({Index?.Name})",
        DDLSchema.BackupDatabase => $"{Schema}({DatabaseName},{BackupFile})",
        DDLSchema.RestoreDatabase => $"{Schema}({BackupFile},{RecoverDir})",
        _ => Schema.ToString(),
    };
    #endregion
}

