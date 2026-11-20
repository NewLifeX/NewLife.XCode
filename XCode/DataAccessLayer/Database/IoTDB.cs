using System.Data;
using System.Data.Common;
using NewLife.Serialization;
using XCode.IoTDB;

namespace XCode.DataAccessLayer;

/// <summary>Apache IoTDB 时序数据库。通过 REST v2 接口访问</summary>
/// <remarks>
/// IoTDB 使用树模型：root.{database}.{device}.{measurement}。
/// XCode 只提供 DAL 原生 SQL 访问，不提供实体 ORM；实体操作会被能力门禁拒绝。
/// </remarks>
class IoTDB : RemoteDb
{
    #region 属性
    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.IoTDB;

    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory CreateFactory() => IoTDBFactory.Instance;

    const String Server_Key = "Server";
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        // 确保 Server 地址以 http:// 或 https:// 开头
        var server = builder[Server_Key];
        if (server.IsNullOrEmpty())
        {
            builder[Server_Key] = "http://localhost:18080";
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
    protected override IDbSession OnCreateSession() => new IoTDBSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new IoTDBMetaData();

    /// <summary>是否支持该提供者</summary>
    /// <param name="providerName">提供者名称</param>
    /// <returns></returns>
    public override Boolean Support(String providerName) => !providerName.IsNullOrEmpty() && providerName.ToLower().Contains("iotdb");

    /// <summary>系统数据库名</summary>
    public override String SystemDatabaseName => "root";
    #endregion

    #region 数据库特性
    /// <summary>创建能力配置。IoTDB 只支持原生 SQL，不提供实体 ORM</summary>
    /// <returns>能力配置</returns>
    protected override DatabaseProfile CreateProfile() => new()
    {
        Model = DatabaseModel.TimeSeries,
        QueryLanguage = QueryLanguage.Sql,
        Capabilities = DatabaseCapability.Sql | DatabaseCapability.SchemaDiscovery,
        MaxMigration = Migration.Off,
        LongTextLength = 65535,
        ParameterPrefix = "@",
    };

    protected override String ReservedWordsStr => "AND,OR,NOT,FROM,WHERE,SELECT,INSERT,INTO,VALUES,GROUP,BY,ORDER,LIMIT";

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

/// <summary>IoTDB数据库会话</summary>
internal class IoTDBSession : RemoteDbSession
{
    #region 构造函数
    public IoTDBSession(IDatabase db) : base(db) { }
    #endregion

    #region 基本方法 查询/执行
    /// <summary>执行插入语句并返回新增行的自动编号。IoTDB 使用时间戳，没有自增主键</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>固定返回 0</returns>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        Execute(sql, type, ps);
        return 0;
    }

    /// <summary>执行插入语句并返回新增行的自动编号。IoTDB 使用时间戳，没有自增主键</summary>
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

    #region 架构
    /// <summary>获取架构。IoTDB 的 schema 由 SHOW 语句返回</summary>
    /// <param name="conn">连接</param>
    /// <param name="collectionName">集合名</param>
    /// <param name="restrictionValues">限制值</param>
    /// <returns></returns>
    public override DataTable GetSchema(DbConnection? conn, String collectionName, String?[]? restrictionValues) => new();
    #endregion
}

/// <summary>IoTDB元数据</summary>
class IoTDBMetaData : RemoteDbMetaData
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

            // IoTDB 的列取决于设备下的时间序列，这里给出时间列占位，详细建模请手写 Model.xml
            var timeField = table.CreateColumn();
            timeField.ColumnName = "Time";
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

    /// <summary>快速取得所有表名。IoTDB 以 database 作为一级表名</summary>
    /// <returns></returns>
    public override IList<String> GetTableNames()
    {
        var list = new List<String>();

        if (Database.OpenConnection() is not IoTDBConnection conn) return list;

        var text = conn.Query("SHOW DATABASES");
        var table = IoTDBResultParser.Parse(text);
        foreach (DataRow row in table.Rows)
        {
            var name = row["name"] + "";
            if (name.IsNullOrEmpty()) name = row["database"] + "";
            if (!name.IsNullOrEmpty()) list.Add(name);
        }

        return list;
    }

    public override String FieldClause(IDataColumn field, Boolean onlyDefine) => $"{FormatName(field)} {GetFieldType(field)}";
    #endregion

    #region 反向工程
    public override Boolean DatabaseExist(String? databaseName)
    {
        if (databaseName.IsNullOrEmpty()) return base.DatabaseExist(databaseName);

        foreach (var name in GetTableNames())
        {
            if (name.EqualIgnoreCase(databaseName)) return true;
        }

        return false;
    }

    public override String CreateDatabaseSQL(String dbname, String? file) => $"CREATE DATABASE {dbname}";

    public override String DropDatabaseSQL(String dbname) => $"DROP DATABASE {dbname}";

    /// <summary>创建表 SQL。IoTDB 写入时自动创建时间序列</summary>
    /// <param name="table">表</param>
    /// <returns>固定返回空</returns>
    public override String CreateTableSQL(IDataTable table) => String.Empty;

    public override String AddTableDescriptionSQL(IDataTable table) => String.Empty;

    public override String AlterColumnSQL(IDataColumn field, IDataColumn? oldfield) => throw new NotSupportedException(L.T("This operation is not supported!"));

    public override String AddColumnDescriptionSQL(IDataColumn field) => String.Empty;
    #endregion
}
