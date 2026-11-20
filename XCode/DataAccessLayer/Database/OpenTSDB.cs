using System.Data;
using System.Data.Common;
using NewLife.Data;
using NewLife.Serialization;
using XCode.OpenTSDB;

namespace XCode.DataAccessLayer;

/// <summary>OpenTSDB时序数据库。基于 HBase 的指标存储，使用 HTTP JSON 接口</summary>
/// <remarks>
/// 数据模型：metric + timestamp + value + tags。写入使用 /api/put，查询使用 /api/query。
/// 只支持追加写，同一 metric+timestamp+tags 的数据会被覆盖，不支持更新与删除。
/// </remarks>
class OpenTSDB : RemoteDb
{
    #region 属性
    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.OpenTSDB;

    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory CreateFactory() => OpenTSDBFactory.Instance;

    const String Server_Key = "Server";
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        // 确保 Server 地址以 http:// 或 https:// 开头
        var server = builder[Server_Key];
        if (server.IsNullOrEmpty())
        {
            builder[Server_Key] = "http://localhost:4242";
        }
        else if (!server.StartsWithIgnoreCase("http://", "https://"))
        {
            builder[Server_Key] = $"http://{server}";
        }
    }
    #endregion

    #region 方法
    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new OpenTSDBSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new OpenTSDBMetaData();

    /// <summary>是否支持该提供者</summary>
    /// <param name="providerName">提供者名称</param>
    /// <returns></returns>
    public override Boolean Support(String providerName) => !providerName.IsNullOrEmpty() && providerName.ToLower().Contains("opentsdb");

    /// <summary>系统数据库名</summary>
    public override String SystemDatabaseName => "opentsdb";
    #endregion

    #region 数据库特性
    /// <summary>创建能力配置。OpenTSDB 仅支持批量写入和原生查询</summary>
    /// <returns>能力配置</returns>
    protected override DatabaseProfile CreateProfile() => new()
    {
        Model = DatabaseModel.TimeSeries,
        QueryLanguage = QueryLanguage.Native,
        Capabilities = DatabaseCapability.BulkInsert | DatabaseCapability.BulkUpsert,
        MaxMigration = Migration.Off,
        LongTextLength = 65535,
        ParameterPrefix = "@",
    };

    protected override String ReservedWordsStr => "AND,OR,NOT";

    /// <summary>格式化关键字</summary>
    /// <param name="keyWord">关键字</param>
    /// <returns></returns>
    public override String FormatKeyWord(String keyWord) => keyWord;

    /// <summary>长文本长度</summary>
    public override Int32 LongTextLength => 65535;

    internal protected override String ParamPrefix => "@";

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => $"{left} + {right}";
    #endregion
}

/// <summary>OpenTSDB数据库会话</summary>
internal class OpenTSDBSession : RemoteDbSession
{
    private static readonly DateTime _epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    #region 构造函数
    public OpenTSDBSession(IDatabase db) : base(db) { }
    #endregion

    #region 基本方法 查询/执行
    /// <summary>执行插入语句并返回新增行的自动编号。OpenTSDB 没有自增主键</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>固定返回 0</returns>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        Execute(sql, type, ps);
        return 0;
    }

    /// <summary>执行插入语句并返回新增行的自动编号。OpenTSDB 没有自增主键</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>固定返回 0</returns>
    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        Execute(sql, type, ps);
        return Task.FromResult((Int64)0);
    }
    #endregion

    #region 批量操作
    /// <summary>批量插入</summary>
    /// <param name="table">数据表</param>
    /// <param name="columns">要插入的字段</param>
    /// <param name="list">实体列表</param>
    /// <returns>写入实体数</returns>
    public override Int32 Insert(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list)
    {
        var entities = list as ICollection<IModel> ?? list.ToList();
        if (entities.Count == 0) return 0;

        var json = BuildPutJson(table, columns, entities);
        if (json.IsNullOrEmpty()) return 0;

        Execute(json);

        return entities.Count;
    }

    /// <summary>批量插入或更新。OpenTSDB 按 metric+timestamp+tags 覆盖</summary>
    /// <param name="table">数据表</param>
    /// <param name="columns">要插入的字段</param>
    /// <param name="updateColumns">主键已存在时，要更新的字段</param>
    /// <param name="addColumns">主键已存在时，要累加更新的字段</param>
    /// <param name="list">实体列表</param>
    /// <returns>写入实体数</returns>
    public override Int32 Upsert(IDataTable table, IDataColumn[] columns, ICollection<String>? updateColumns, ICollection<String>? addColumns, IEnumerable<IModel> list) => Insert(table, columns, list);

    /// <summary>构建 /api/put 的 JSON 数组</summary>
    /// <param name="table">数据表</param>
    /// <param name="columns">字段集合</param>
    /// <param name="entities">实体集合</param>
    /// <returns>JSON 文本</returns>
    internal static String BuildPutJson(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> entities)
    {
        var timeColumn = FindTimeColumn(columns);
        var tagColumns = columns.Where(e => e != timeColumn && (e.PrimaryKey || e.Master || IsStringType(e))).ToArray();
        var valueColumns = columns.Where(e => e != timeColumn && !e.PrimaryKey && !e.Master && IsValueType(e)).ToArray();

        if (valueColumns.Length == 0) throw new InvalidOperationException(L.T("OpenTSDB requires at least one numeric field; table [{0}] has none.", table.TableName));

        var list = new List<Dictionary<String, Object?>>();
        foreach (var entity in entities)
        {
            var time = timeColumn?.Name is String timeName && entity[timeName] is DateTime dt ? dt : DateTime.Now;
            var timestamp = (Int64)(time.ToUniversalTime() - _epoch).TotalSeconds;

            var tags = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in tagColumns)
            {
                var name = column.Name ?? column.ColumnName;
                var value = entity[name] + "";
                if (value.IsNullOrEmpty()) continue;

                CheckIdentifier(name);
                CheckIdentifier(value);
                tags[name] = value;
            }

            foreach (var column in valueColumns)
            {
                var name = column.Name ?? column.ColumnName;
                var value = entity[name];
                if (value == null) continue;

                var metric = valueColumns.Length == 1 ? table.TableName : $"{table.TableName}.{name}";
                CheckIdentifier(metric);

                list.Add(new Dictionary<String, Object?>
                {
                    ["metric"] = metric,
                    ["timestamp"] = timestamp,
                    ["value"] = value is Boolean b ? (b ? 1 : 0) : value,
                    ["tags"] = tags,
                });
            }
        }

        return list.Count == 0 ? String.Empty : list.ToJson();
    }

    private static IDataColumn? FindTimeColumn(IDataColumn[] columns) => columns.FirstOrDefault(e =>
    {
        var name = e.Name ?? e.ColumnName;
        return name.EqualIgnoreCase("Time", "CreateTime", "UpdateTime");
    });

    private static Boolean IsStringType(IDataColumn column) => column.DataType == typeof(String) || column.DataType == typeof(Char);

    private static Boolean IsValueType(IDataColumn column)
    {
        var type = column.DataType;
        return type == typeof(Byte) || type == typeof(SByte) ||
            type == typeof(Int16) || type == typeof(UInt16) ||
            type == typeof(Int32) || type == typeof(UInt32) ||
            type == typeof(Int64) || type == typeof(UInt64) ||
            type == typeof(Single) || type == typeof(Double) || type == typeof(Decimal) ||
            type == typeof(Boolean);
    }

    /// <summary>校验 metric 与 tag 标识。OpenTSDB 不允许空白字符</summary>
    /// <param name="value">标识</param>
    private static void CheckIdentifier(String value)
    {
        foreach (var ch in value)
        {
            if (Char.IsWhiteSpace(ch)) throw new ArgumentException(L.T("OpenTSDB metric and tag values cannot contain whitespace: [{0}]", value));
        }
    }
    #endregion

    #region 架构
    /// <summary>获取架构。OpenTSDB 没有固定 schema</summary>
    /// <param name="conn">连接</param>
    /// <param name="collectionName">集合名</param>
    /// <param name="restrictionValues">限制值</param>
    /// <returns></returns>
    public override DataTable GetSchema(DbConnection? conn, String collectionName, String?[]? restrictionValues) => new();
    #endregion
}

/// <summary>OpenTSDB元数据</summary>
class OpenTSDBMetaData : RemoteDbMetaData
{
    #region 架构
    protected override List<IDataTable> OnGetTables(String[]? names)
    {
        var list = new List<IDataTable>();
        var hs = new HashSet<String>(names ?? [], StringComparer.OrdinalIgnoreCase);

        foreach (var name in GetTableNames())
        {
            if (hs.Count > 0 && !hs.Contains(name)) continue;

            var table = DAL.CreateTable();
            table.TableName = name;
            table.DbType = Database.Type;

            var timeField = table.CreateColumn();
            timeField.ColumnName = "time";
            timeField.DataType = typeof(DateTime);
            timeField.PrimaryKey = true;
            table.Columns.Add(timeField);

            var valueField = table.CreateColumn();
            valueField.ColumnName = "value";
            valueField.DataType = typeof(Double);
            table.Columns.Add(valueField);

            table.Fix();
            list.Add(table);
        }

        return list;
    }

    /// <summary>快速取得所有表名。OpenTSDB 的表名即 metric</summary>
    /// <returns></returns>
    public override IList<String> GetTableNames()
    {
        var list = new List<String>();

        if (Database.OpenConnection() is not OpenTSDBConnection conn) return list;

        var text = conn.Get("/api/suggest?type=metrics&max=1000000");
        if (text.IsNullOrWhiteSpace()) return list;

        if (JsonHelper.Parse(text) is IList<Object> array)
        {
            foreach (var item in array)
            {
                var name = item + "";
                if (!name.IsNullOrEmpty()) list.Add(name);
            }
        }

        return list;
    }

    public override String FieldClause(IDataColumn field, Boolean onlyDefine) => $"{FormatName(field)} {GetFieldType(field)}";
    #endregion

    #region 反向工程
    public override Boolean DatabaseExist(String? databaseName)
    {
        if (Database.OpenConnection() is not OpenTSDBConnection conn) return false;

        try
        {
            return !conn.Get("/api/version").IsNullOrEmpty();
        }
        catch
        {
            return false;
        }
    }

    public override String CreateDatabaseSQL(String dbname, String? file) => throw new NotSupportedException(L.T("This operation is not supported!"));

    public override String DropDatabaseSQL(String dbname) => throw new NotSupportedException(L.T("This operation is not supported!"));

    /// <summary>创建表 SQL。OpenTSDB 写入时自动创建 metric</summary>
    /// <param name="table">表</param>
    /// <returns>固定返回空</returns>
    public override String CreateTableSQL(IDataTable table) => String.Empty;

    public override String AddTableDescriptionSQL(IDataTable table) => String.Empty;

    public override String AlterColumnSQL(IDataColumn field, IDataColumn? oldfield) => throw new NotSupportedException(L.T("This operation is not supported!"));

    public override String AddColumnDescriptionSQL(IDataColumn field) => String.Empty;
    #endregion
}
