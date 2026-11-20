using System.Data;
using System.Data.Common;
using NewLife.Serialization;
using XCode.Prometheus;

namespace XCode.DataAccessLayer;

/// <summary>Prometheus 时序数据库。只读 PromQL 查询，兼容 VictoriaMetrics</summary>
/// <remarks>
/// Prometheus 是监控系统而非完整数据库，XCode 只提供只读查询与指标名发现。
/// 写入请使用 Prometheus remote write 或直接写入采集端。
/// </remarks>
class Prometheus : RemoteDb
{
    #region 属性
    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.Prometheus;

    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory CreateFactory() => PrometheusFactory.Instance;

    const String Server_Key = "Server";
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        // 确保 Server 地址以 http:// 或 https:// 开头
        var server = builder[Server_Key];
        if (server.IsNullOrEmpty())
        {
            builder[Server_Key] = "http://localhost:9090";
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
    protected override IDbSession OnCreateSession() => new PrometheusSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new PrometheusMetaData();

    /// <summary>是否支持该提供者。VictoriaMetrics 兼容 Prometheus API</summary>
    /// <param name="providerName">提供者名称</param>
    /// <returns></returns>
    public override Boolean Support(String providerName)
    {
        if (providerName.IsNullOrEmpty()) return false;

        providerName = providerName.ToLower();
        return providerName.Contains("prometheus") || providerName.Contains("victoriametrics") || providerName.Contains("victoria");
    }

    /// <summary>系统数据库名</summary>
    public override String SystemDatabaseName => "prometheus";
    #endregion

    #region 数据库特性
    /// <summary>创建能力配置。Prometheus 只读，仅提供指标发现</summary>
    /// <returns>能力配置</returns>
    protected override DatabaseProfile CreateProfile() => new()
    {
        Model = DatabaseModel.TimeSeries,
        QueryLanguage = QueryLanguage.Native,
        Capabilities = DatabaseCapability.SchemaDiscovery,
        MaxMigration = Migration.Off,
        LongTextLength = 65535,
        ParameterPrefix = "@",
    };

    protected override String ReservedWordsStr => "AND,OR,NOT,BY,WITHOUT";

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

/// <summary>Prometheus数据库会话</summary>
internal class PrometheusSession : RemoteDbSession
{
    #region 构造函数
    public PrometheusSession(IDatabase db) : base(db) { }
    #endregion

    #region 架构
    /// <summary>获取架构。Prometheus 只有指标名</summary>
    /// <param name="conn">连接</param>
    /// <param name="collectionName">集合名</param>
    /// <param name="restrictionValues">限制值</param>
    /// <returns></returns>
    public override DataTable GetSchema(DbConnection? conn, String collectionName, String?[]? restrictionValues) => new();
    #endregion
}

/// <summary>Prometheus元数据</summary>
class PrometheusMetaData : RemoteDbMetaData
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

    /// <summary>快速取得所有表名。Prometheus 的表名即指标名</summary>
    /// <returns></returns>
    public override IList<String> GetTableNames()
    {
        var list = new List<String>();

        if (Database.OpenConnection() is not PrometheusConnection conn) return list;

        var text = conn.Get("/api/v1/label/__name__/values");
        if (text.IsNullOrWhiteSpace()) return list;

        if (JsonHelper.Parse(text) is IDictionary<String, Object?> root && root["data"] is IList<Object> array)
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
        if (Database.OpenConnection() is not PrometheusConnection conn) return false;

        try
        {
            return !conn.Get("/api/v1/status/buildinfo").IsNullOrEmpty();
        }
        catch
        {
            return false;
        }
    }

    public override String CreateDatabaseSQL(String dbname, String? file) => throw new NotSupportedException(L.T("This operation is not supported!"));

    public override String DropDatabaseSQL(String dbname) => throw new NotSupportedException(L.T("This operation is not supported!"));

    /// <summary>创建表 SQL。Prometheus 指标由采集端创建</summary>
    /// <param name="table">表</param>
    /// <returns>固定返回空</returns>
    public override String CreateTableSQL(IDataTable table) => String.Empty;

    public override String AddTableDescriptionSQL(IDataTable table) => String.Empty;

    public override String AlterColumnSQL(IDataColumn field, IDataColumn? oldfield) => throw new NotSupportedException(L.T("This operation is not supported!"));

    public override String AddColumnDescriptionSQL(IDataColumn field) => String.Empty;
    #endregion
}
