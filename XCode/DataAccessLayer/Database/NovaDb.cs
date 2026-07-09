using System.Data;
using System.Data.Common;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;

namespace XCode.DataAccessLayer;

/// <summary>NovaDb数据库。支持嵌入模式和网络模式</summary>
/// <remarks>
/// NovaDb是NewLife自研数据库，支持两种工作模式：
/// - 嵌入模式：类似SQLite，连接字符串为 Data Source=../data/mydb
/// - 网络模式：类似MySql，连接字符串为 Server=localhost;Port=3306;Database=mydb
/// </remarks>
internal class NovaDb : RemoteDb
{
    #region 属性

    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.NovaDb;

    /// <summary>批量操作能力。NovaDb支持批量Insert/InsertIgnore/Replace/Upsert</summary>
    public override BatchCapability BatchCapability => BatchCapability.Insert | BatchCapability.InsertIgnore | BatchCapability.Replace | BatchCapability.Upsert;

    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory()
    {
        var type = DriverLoader.Load("NewLife.NovaDb.Client.NovaClientFactory", null, "NewLife.NovaDb.dll", null);
        var factory = GetProviderFactory(type);
        if (factory != null) return factory;

        return GetProviderFactory(null, "NewLife.NovaDb.dll", "NewLife.NovaDb.Client.NovaClientFactory", true, true);
    }

    /// <summary>是否嵌入模式</summary>
    public Boolean IsEmbedded { get; private set; }

    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        // 判断是否嵌入模式。有 Data Source 且无 Server 时为嵌入模式
        var dataSource = builder["Data Source"];
        var server = builder["Server"];
        IsEmbedded = !dataSource.IsNullOrEmpty() && server.IsNullOrEmpty();

        if (IsEmbedded && !dataSource.IsNullOrEmpty())
        {
            // 嵌入模式下，解析文件路径作为数据库名
            DatabaseName = Path.GetFileName(dataSource);
        }
    }

    #endregion 属性

    #region 方法

    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new NovaDbSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new NovaDbMetaData();

    public override Boolean Support(String providerName)
    {
        providerName = providerName.ToLower();
        if (providerName.Contains("novadb")) return true;
        if (providerName.Contains("nova")) return true;

        return false;
    }

    #endregion 方法

    #region 数据库特性

    protected override String ReservedWordsStr => "ADD,ALL,ALTER,AND,AS,ASC,BETWEEN,BY,CASCADE,CASE,CHECK,COLUMN,CONSTRAINT,CREATE,CROSS,DATABASE,DEFAULT,DELETE,DESC,DISTINCT,DROP,ELSE,END,EXISTS,FOREIGN,FROM,FULL,GROUP,HAVING,IF,IN,INDEX,INNER,INSERT,INTO,IS,JOIN,KEY,LEFT,LIKE,LIMIT,NOT,NULL,ON,OR,ORDER,OUTER,PRIMARY,REFERENCES,RIGHT,SELECT,SET,TABLE,THEN,TO,UNION,UNIQUE,UPDATE,USING,VALUES,VIEW,WHEN,WHERE,WITH";

    /// <summary>格式化关键字</summary>
    /// <param name="keyWord">关键字</param>
    /// <returns></returns>
    public override String FormatKeyWord(String keyWord)
    {
        if (keyWord.IsNullOrEmpty()) return keyWord;

        if (keyWord.StartsWith("`") && keyWord.EndsWith("`")) return keyWord;

        return $"`{keyWord}`";
    }

    /// <summary>格式化数据为SQL数据</summary>
    /// <param name="field">字段</param>
    /// <param name="value">数值</param>
    /// <returns></returns>
    public override String FormatValue(IDataColumn field, Object? value)
    {
        var code = System.Type.GetTypeCode(field.DataType);
        if (code == TypeCode.Boolean)
        {
            return value.ToBoolean() ? "1" : "0";
        }

        return base.FormatValue(field, value);
    }

    /// <summary>长文本长度</summary>
    public override Int32 LongTextLength => 4000;

    protected internal override String ParamPrefix => "@";

    /// <summary>系统数据库名</summary>
    public override String SystemDatabaseName => "nova";

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => $"concat({(!String.IsNullOrEmpty(left) ? left : "\'\'")},{(!String.IsNullOrEmpty(right) ? right : "\'\'")})";

    /// <summary>生成批量删除SQL。支持分批删除</summary>
    /// <param name="tableName"></param>
    /// <param name="where"></param>
    /// <param name="batchSize"></param>
    /// <returns>不支持分批删除时返回null</returns>
    public override String? BuildDeleteSql(String tableName, String where, Int32 batchSize)
    {
        var sql = base.BuildDeleteSql(tableName, where, 0);

        if (batchSize <= 0) return sql;

        sql = $"{sql} limit {batchSize}";

        return sql;
    }

    #endregion 数据库特性
}

/// <summary>NovaDb数据库会话</summary>
internal class NovaDbSession : RemoteDbSession
{
    #region 构造函数

    public NovaDbSession(IDatabase db) : base(db) { }

    #endregion 构造函数

    #region 快速查询单表记录数

    /// <summary>快速查询单表记录数，大数据量时，稍有偏差</summary>
    /// <param name="tableName"></param>
    /// <returns></returns>
    public override Int64 QueryCountFast(String tableName)
    {
        tableName = tableName.Trim().Trim('`', '`').Trim();

        var db = Database.DatabaseName;
        var sql = $"select table_rows from _sys.tables where table_schema='{db}' and table_name='{tableName}'";
        return ExecuteScalar<Int64>(sql);
    }

    public override Task<Int64> QueryCountFastAsync(String tableName)
    {
        tableName = tableName.Trim().Trim('`', '`').Trim();

        var db = Database.DatabaseName;
        var sql = $"select table_rows from _sys.tables where table_schema='{db}' and table_name='{tableName}'";
        return ExecuteScalarAsync<Int64>(sql);
    }

    #endregion 快速查询单表记录数

    #region 基本方法 查询/执行

    /// <summary>执行插入语句并返回新增行的自动编号</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        sql += ";Select LAST_INSERT_ID()";
        return base.InsertAndGetIdentity(sql, type, ps);
    }

    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        sql += ";Select LAST_INSERT_ID()";
        return base.InsertAndGetIdentityAsync(sql, type, ps);
    }

    #endregion 基本方法 查询/执行

    #region 批量操作

    private String GetBatchSql(String action, IDataTable table, IDataColumn[] columns, ICollection<String>? updateColumns, ICollection<String>? addColumns, IEnumerable<IModel> list)
    {
        var sb = Pool.StringBuilder.Get();
        var db = (Database as DbBase)!;

        // 字段列表
        columns ??= table.Columns.ToArray();
        BuildInsert(sb, db, action, table, columns);
        DefaultSpan.Current?.AppendTag(sb.ToString());

        // 值列表
        sb.Append(" Values");
        BuildBatchValues(sb, db, action, table, columns, list);

        // 重复键执行update
        BuildDuplicateKey(sb, db, columns, updateColumns, addColumns);

        return sb.Return(true);
    }

    public override Int32 Insert(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list)
    {
        var sql = GetBatchSql("Insert Into", table, columns, null, null, list);
        return Execute(sql);
    }

    public override Int32 InsertIgnore(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list)
    {
        var sql = GetBatchSql("Insert Ignore Into", table, columns, null, null, list);
        return Execute(sql);
    }

    public override Int32 Replace(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list)
    {
        var sql = GetBatchSql("Replace Into", table, columns, null, null, list);
        return Execute(sql);
    }

    /// <summary>批量插入或更新</summary>
    /// <param name="table">数据表</param>
    /// <param name="columns">要插入的字段，默认所有字段</param>
    /// <param name="updateColumns">主键已存在时，要更新的字段。属性名，不是字段名</param>
    /// <param name="addColumns">主键已存在时，要累加更新的字段。属性名，不是字段名</param>
    /// <param name="list">实体列表</param>
    /// <returns></returns>
    public override Int32 Upsert(IDataTable table, IDataColumn[] columns, ICollection<String>? updateColumns, ICollection<String>? addColumns, IEnumerable<IModel> list)
    {
        var sql = GetBatchSql("Insert Into", table, columns, updateColumns, addColumns, list);
        return Execute(sql);
    }

    #endregion 批量操作
}

/// <summary>NovaDb元数据</summary>
internal class NovaDbMetaData : RemoteDbMetaData
{
    public NovaDbMetaData() => Types = _DataTypes;

    #region 数据类型

    /// <summary>数据类型映射</summary>
    private static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte[]), new String[] { "BLOB", "TINYBLOB", "MEDIUMBLOB", "LONGBLOB", "binary({0})", "varbinary({0})" } },
        { typeof(Byte), new String[] { "TINYINT", "TINYINT UNSIGNED" } },
        { typeof(Int16), new String[] { "SMALLINT", "SMALLINT UNSIGNED" } },
        { typeof(Int32), new String[] { "INT", "MEDIUMINT", "INT UNSIGNED" } },
        { typeof(Int64), new String[] { "BIGINT", "BIGINT UNSIGNED" } },
        { typeof(Single), new String[] { "FLOAT" } },
        { typeof(Double), new String[] { "DOUBLE" } },
        { typeof(Decimal), new String[] { "DECIMAL({0}, {1})" } },
        { typeof(DateTime), new String[] { "DATETIME", "DATE", "TIMESTAMP", "TIME" } },
        { typeof(String), new String[] { "VARCHAR({0})", "LONGTEXT", "TEXT", "CHAR({0})", "NCHAR({0})", "NVARCHAR({0})", "TINYTEXT", "MEDIUMTEXT" } },
        { typeof(Boolean), new String[] { "TINYINT" } },
        { typeof(Guid), new String[] { "CHAR(36)" } },
    };

    #endregion 数据类型

    #region 架构

    protected override List<IDataTable> OnGetTables(String[]? names)
    {
        var raw = base.OnGetTables(names);

        var ss = Database.CreateSession();
        var db = Database.DatabaseName;
        var list = new List<IDataTable>();

        var old = ss.ShowSQL;
        ss.ShowSQL = false;
        try
        {
            var sql = $"SHOW TABLE STATUS FROM `{db}`";
            var dt = ss.Query(sql, null);
            if (dt.Rows == null || dt.Rows.Count == 0) return list;

            sql = $"select * from information_schema.columns where table_schema='{db}'";
            if (names != null && names.Length > 0) sql += " and table_name in ('" + names.Join("','") + "')";
            var columns = ss.Query(sql, null);

            sql = $"select * from information_schema.STATISTICS where table_schema='{db}'";
            if (names != null && names.Length > 0) sql += " and table_name in ('" + names.Join("','") + "')";
            var indexes = ss.Query(sql, null);

            var hs = new HashSet<String>(names ?? [], StringComparer.OrdinalIgnoreCase);

            // 所有表
            foreach (var dr in dt)
            {
                var name = dr["Name"] + "";
                if (name.IsNullOrEmpty() || hs.Count > 0 && !hs.Contains(name)) continue;

                var table = DAL.CreateTable();
                table.TableName = name;
                table.Description = dr["Comment"] + "";
                table.DbType = Database.Type;

                #region 字段
                if (columns.Rows != null && columns.Rows.Count > 0)
                {
                    foreach (var dc in columns)
                    {
                        if (dc["TABLE_NAME"] + "" != table.TableName) continue;

                        var field = table.CreateColumn();

                        field.ColumnName = dc["COLUMN_NAME"] + "";
                        field.RawType = dc["COLUMN_TYPE"] + "";
                        field.Description = dc["COLUMN_COMMENT"] + "";

                        if (dc["Extra"] + "" == "auto_increment") field.Identity = true;
                        if (dc["COLUMN_KEY"] + "" == "PRI") field.PrimaryKey = true;
                        if (dc["IS_NULLABLE"] + "" == "YES") field.Nullable = true;

                        // 精度与位数
                        field.Precision = dc["NUMERIC_PRECISION"].ToInt();
                        field.Scale = dc["NUMERIC_SCALE"].ToInt();
                        field.DefaultValue = dc["COLUMN_DEFAULT"] as String;

                        field.Length = field.RawType.Substring("(", ")").ToInt();

                        var type = GetDataType(field);
                        if (type == null)
                        {
                            if (field.RawType.StartsWithIgnoreCase("varchar", "nvarchar")) field.DataType = typeof(String);
                        }
                        else
                            field.DataType = type;

                        field.Fix();

                        table.Columns.Add(field);
                    }
                }
                #endregion 字段

                #region 索引
                if (indexes.Rows != null && indexes.Rows.Count > 0)
                {
                    foreach (var dr2 in indexes)
                    {
                        if (dr2["TABLE_NAME"] + "" != table.TableName) continue;

                        var dname = dr2["INDEX_NAME"] + "";
                        var di = table.Indexes.FirstOrDefault(e => e.Name == dname) ?? table.CreateIndex();
                        di.Unique = dr2.Get<Int32>("Non_unique") == 0;

                        var cname = dr2.Get<String>("Column_name");
                        if (cname.IsNullOrEmpty()) continue;

                        var cs = new List<String>();
                        if (di.Columns != null && di.Columns.Length > 0) cs.AddRange(di.Columns);
                        cs.Add(cname);
                        di.Columns = cs.ToArray();

                        if (di.Name == null)
                        {
                            di.Name = dname;
                            table.Indexes.Add(di);
                        }
                    }
                }
                #endregion 索引

                // 修正关系数据
                table.Fix();

                list.Add(table);
            }
        }
        finally
        {
            ss.ShowSQL = old;
        }

        return list;
    }

    /// <summary>快速取得所有表名</summary>
    /// <returns></returns>
    public override IList<String> GetTableNames()
    {
        var list = new List<String>();

        var sql = $"SHOW TABLE STATUS FROM `{Database.DatabaseName}`";
        var dt = base.Database.CreateSession().Query(sql, null);
        if (dt.Rows == null || dt.Rows.Count == 0) return list;

        // 所有表
        foreach (var dr in dt)
        {
            var name = dr["Name"] + "";
            if (!name.IsNullOrEmpty()) list.Add(name);
        }

        return list;
    }

    #endregion 架构

    #region 反向工程

    public override Boolean DatabaseExist(String? databaseName)
    {
        if (databaseName.IsNullOrEmpty()) return base.DatabaseExist(databaseName);

        var dt = GetSchema(_.Databases, [databaseName]);
        return dt != null && dt.Rows != null && dt.Rows.Count > 0;
    }

    public override String CreateDatabaseSQL(String dbname, String? file) => base.CreateDatabaseSQL(dbname, file) + " DEFAULT CHARACTER SET utf8mb4";

    public override String DropDatabaseSQL(String dbname) => $"Drop Database If Exists {Database.FormatName(dbname)}";

    public override String CreateTableSQL(IDataTable table)
    {
        var fs = new List<IDataColumn>(table.Columns);

        var sb = Pool.StringBuilder.Get();

        sb.AppendFormat("Create Table If Not Exists {0}(", FormatName(table));
        for (var i = 0; i < fs.Count; i++)
        {
            sb.AppendLine();
            sb.Append('\t');
            sb.Append(FieldClause(fs[i], true));
            if (i < fs.Count - 1) sb.Append(',');
        }
        if (table.PrimaryKeys.Length > 0) sb.AppendFormat(",\r\n\tPrimary Key ({0})", table.PrimaryKeys.Join(",", FormatName));
        sb.AppendLine();
        sb.Append(')');

        sb.Append(" DEFAULT CHARSET=utf8mb4");
        sb.Append(';');

        return sb.Return(true);
    }

    public override String? AddTableDescriptionSQL(IDataTable table)
    {
        if (String.IsNullOrEmpty(table.Description)) return null;

        return $"Alter Table {FormatName(table)} Comment '{FormatComment(table.Description)}'";
    }

    public override String? AlterColumnSQL(IDataColumn field, IDataColumn? oldfield) => $"Alter Table {FormatName(field.Table)} Modify Column {FieldClause(field, false)}";

    public override String? AddColumnDescriptionSQL(IDataColumn field) =>
        // 返回String.Empty表示已经在别的SQL中处理
        String.Empty;

    public override String FieldClause(IDataColumn field, Boolean onlyDefine)
    {
        var sql = base.FieldClause(field, onlyDefine);

        // 加上注释
        if (!String.IsNullOrEmpty(field.Description)) sql = $"{sql} COMMENT '{FormatComment(field.Description)}'";

        return sql;
    }

    protected override String? GetFieldConstraints(IDataColumn field, Boolean onlyDefine)
    {
        String? str = null;
        if (!field.Nullable) str = " NOT NULL";

        // 默认值
        if (!field.Nullable && !field.Identity)
        {
            str += GetDefault(field, onlyDefine);
        }

        if (field.Identity) str += " AUTO_INCREMENT";

        return str;
    }

    #endregion 反向工程
}
