using System.Data;
using System.Data.Common;
using System.Text;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;
using XCode.InfluxDB;

namespace XCode.DataAccessLayer;

class InfluxDB : RemoteDb
{
    #region 属性
    /// <summary>返回数据库类型。</summary>
    public override DatabaseType Type => DatabaseType.InfluxDB;

    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory CreateFactory() => InfluxDBFactory.Instance;

    const String Server_Key = "Server";
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        // 确保 Server 地址以 http:// 或 https:// 开头
        var server = builder[Server_Key];
        if (!String.IsNullOrEmpty(server) && !server.StartsWithIgnoreCase("http://", "https://"))
        {
            builder[Server_Key] = $"http://{server}";
        }
    }
    #endregion

    #region 方法
    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new InfluxDBSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new InfluxDBMetaData();

    public override Boolean Support(String providerName)
    {
        providerName = providerName.ToLower();
        if (providerName.EqualIgnoreCase("InfluxDB", "Influx")) return true;

        return false;
    }
    #endregion

    #region 数据库特性
    protected override String ReservedWordsStr => "AND,OR,NOT,FROM,WHERE,SELECT,DELETE,DROP,SHOW,MEASUREMENT,TAG,FIELD,TIME";

    /// <summary>格式化关键字</summary>
    /// <param name="keyWord">关键字</param>
    /// <returns></returns>
    public override String FormatKeyWord(String keyWord)
    {
        if (keyWord.IsNullOrEmpty()) return keyWord;
        if (keyWord.StartsWith("\"") && keyWord.EndsWith("\"")) return keyWord;
        return $"\"{keyWord}\"";
    }

    /// <summary>格式化数据为SQL数据</summary>
    /// <param name="field">字段</param>
    /// <param name="value">数值</param>
    /// <returns></returns>
    public override String FormatValue(IDataColumn field, Object? value)
    {
        var code = System.Type.GetTypeCode(field.DataType);
        if (code == TypeCode.String)
        {
            if (value == null)
                return field.Nullable ? "null" : "\"\"";

            return "\"" + value.ToString()?.Replace("\"", "\\\"") + "\"";
        }
        else if (code == TypeCode.Boolean)
        {
            return value.ToBoolean() ? "true" : "false";
        }

        return base.FormatValue(field, value);
    }

    /// <summary>格式化时间为SQL字符串</summary>
    /// <param name="column">字段</param>
    /// <param name="dateTime">时间值</param>
    /// <returns></returns>
    public override String FormatDateTime(IDataColumn column, DateTime dateTime)
    {
        // InfluxDB 使用纳秒时间戳
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var nanos = (dateTime.ToUniversalTime() - epoch).Ticks * 100;
        return nanos.ToString();
    }

    /// <summary>长文本长度</summary>
    public override Int32 LongTextLength => 65535;

    internal protected override String ParamPrefix => "@";

    /// <summary>系统数据库名</summary>
    public override String SystemDatabaseName => "_internal";

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => $"{left} + {right}";
    #endregion
}

/// <summary>InfluxDB数据库会话</summary>
internal class InfluxDBSession : RemoteDbSession
{
    #region 构造函数
    public InfluxDBSession(IDatabase db) : base(db) { }
    #endregion

    #region 基本方法 查询/执行
    /// <summary>执行插入语句并返回新增行的自动编号</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        // InfluxDB 是时序数据库，通常使用时间戳作为主键，不支持自增ID
        Execute(sql, type, ps);
        return 0;
    }

    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        // InfluxDB 是时序数据库，通常使用时间戳作为主键，不支持自增ID
        ExecuteAsync(sql, type, ps).Wait();
        return Task.FromResult(0L);
    }
    #endregion

    #region 批量操作
    /// <summary>批量插入</summary>
    /// <param name="table">数据表</param>
    /// <param name="columns">要插入的字段</param>
    /// <param name="list">实体列表</param>
    /// <returns></returns>
    public override Int32 Insert(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list)
    {
        var sb = Pool.StringBuilder.Get();
        var db = (Database as DbBase)!;

        // InfluxDB 使用 Line Protocol 格式写入
        // 格式: measurement,tag1=value1,tag2=value2 field1=value1,field2=value2 timestamp
        foreach (var entity in list)
        {
            // measurement 名称（表名）
            sb.Append(db.FormatName(table));

            // tags（索引字段，通常是维度）
            var tags = columns.Where(c => c.PrimaryKey || c.Master).ToArray();
            if (tags.Length > 0)
            {
                sb.Append(',');
                sb.Append(tags.Join(",", c =>
                {
                    var value = entity[c.Name];
                    return $"{db.FormatName(c)}={value}";
                }));
            }

            // fields（数据字段）
            var fields = columns.Where(c => !c.PrimaryKey && !c.Master).ToArray();
            if (fields.Length > 0)
            {
                sb.Append(' ');
                sb.Append(fields.Join(",", c =>
                {
                    var value = entity[c.Name];
                    var strValue = value?.ToString() ?? "";
                    // 字符串字段需要加引号
                    if (c.DataType == typeof(String))
                        strValue = $"\"{strValue}\"";
                    return $"{db.FormatName(c)}={strValue}";
                }));
            }

            // timestamp（纳秒级时间戳）
            var timeCol = columns.FirstOrDefault(c => c.Name.EqualIgnoreCase("Time", "CreateTime", "UpdateTime"));
            if (timeCol != null)
            {
                var time = entity[timeCol.Name];
                if (time is DateTime dt)
                {
                    var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    var nanos = (dt.ToUniversalTime() - epoch).Ticks * 100;
                    sb.Append($" {nanos}");
                }
            }

            sb.AppendLine();
        }

        var lineProtocol = sb.Return(true);
        return Execute(lineProtocol);
    }

    /// <summary>批量插入或更新</summary>
    /// <param name="table">数据表</param>
    /// <param name="columns">要插入的字段</param>
    /// <param name="updateColumns">主键已存在时，要更新的字段</param>
    /// <param name="addColumns">主键已存在时，要累加更新的字段</param>
    /// <param name="list">实体列表</param>
    /// <returns></returns>
    public override Int32 Upsert(IDataTable table, IDataColumn[] columns, ICollection<String>? updateColumns, ICollection<String>? addColumns, IEnumerable<IModel> list)
    {
        // InfluxDB 自动处理相同 measurement + tags + timestamp 的写入，新值会覆盖旧值
        return Insert(table, columns, list);
    }
    #endregion

    #region 架构
    public override DataTable GetSchema(DbConnection? conn, String collectionName, String?[]? restrictionValues) => new DataTable();
    #endregion
}

/// <summary>InfluxDB元数据</summary>
class InfluxDBMetaData : RemoteDbMetaData
{
    public InfluxDBMetaData() => Types = _DataTypes;

    #region 数据类型
    /// <summary>数据类型映射</summary>
    private static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte), new String[] { "INTEGER" } },
        { typeof(Int16), new String[] { "INTEGER" } },
        { typeof(Int32), new String[] { "INTEGER" } },
        { typeof(Int64), new String[] { "INTEGER" } },
        { typeof(Single), new String[] { "FLOAT" } },
        { typeof(Double), new String[] { "FLOAT" } },
        { typeof(Decimal), new String[] { "FLOAT" } },
        { typeof(DateTime), new String[] { "TIMESTAMP" } },
        { typeof(String), new String[] { "STRING" } },
        { typeof(Boolean), new String[] { "BOOLEAN" } },
    };
    #endregion

    #region 架构
    protected override List<IDataTable> OnGetTables(String[]? names)
    {
        var ss = Database.CreateSession();
        var list = new List<IDataTable>();

        var old = ss.ShowSQL;
        ss.ShowSQL = false;
        try
        {
            // InfluxDB Flux 查询获取所有 measurement
            var flux = @"
import ""influxdata/influxdb/schema""
schema.measurements(bucket: v.bucket)
";
            var dt = ss.Query(flux, null);
            if (dt.Rows.Count == 0) return [];

            var hs = new HashSet<String>(names ?? [], StringComparer.OrdinalIgnoreCase);

            // 所有表（measurement）
            foreach (var dr in dt)
            {
                var name = dr["_value"] + "";
                if (name.IsNullOrEmpty() || hs.Count > 0 && !hs.Contains(name)) continue;

                var table = DAL.CreateTable();
                table.TableName = name;
                table.DbType = Database.Type;

                // InfluxDB 的 schema 需要通过查询数据来推断
                // 这里简化处理，不详细查询字段信息
                #region 字段
                // 默认添加 time 字段
                var timeField = table.CreateColumn();
                timeField.ColumnName = "time";
                timeField.DataType = typeof(DateTime);
                timeField.PrimaryKey = true;
                table.Columns.Add(timeField);
                #endregion

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

        var flux = @"
import ""influxdata/influxdb/schema""
schema.measurements(bucket: v.bucket)
";
        var dt = base.Database.CreateSession().Query(flux, null);
        if (dt.Rows.Count == 0) return list;

        foreach (var dr in dt)
        {
            var name = dr["_value"] + "";
            if (!name.IsNullOrEmpty()) list.Add(name);
        }

        return list;
    }

    public override String FieldClause(IDataColumn field, Boolean onlyDefine)
    {
        var sb = new StringBuilder();
        sb.AppendFormat("{0} ", FormatName(field));

        String? typeName = null;
        if (Database.Type == field.Table.DbType && !field.Identity) typeName = field.RawType;
        if (String.IsNullOrEmpty(typeName)) typeName = GetFieldType(field);

        sb.Append(typeName);
        return sb.ToString();
    }
    #endregion

    #region 反向工程
    protected override Boolean DatabaseExist(String databaseName)
    {
        // InfluxDB 2.x 使用 bucket 概念
        var flux = @"buckets() |> filter(fn: (r) => r.name == """ + databaseName + @""")";
        var dt = Database.CreateSession().Query(flux, null);
        return dt != null && dt.Rows != null && dt.Rows.Count > 0;
    }

    public override String CreateDatabaseSQL(String dbname, String? file)
    {
        // InfluxDB 2.x 不支持通过 SQL/Flux 创建 bucket，需要使用 HTTP API
        throw new NotSupportedException("InfluxDB does not support creating buckets via SQL. Use HTTP API or CLI.");
    }

    public override String DropDatabaseSQL(String dbname)
    {
        throw new NotSupportedException("InfluxDB does not support dropping buckets via SQL. Use HTTP API or CLI.");
    }

    public override String CreateTableSQL(IDataTable table)
    {
        // InfluxDB 不需要显式创建表（measurement），写入数据时自动创建
        return String.Empty;
    }

    public override String AddTableDescriptionSQL(IDataTable table) => String.Empty;

    public override String AlterColumnSQL(IDataColumn field, IDataColumn? oldfield)
    {
        // InfluxDB 不支持修改字段
        throw new NotSupportedException("InfluxDB does not support altering columns.");
    }

    public override String AddColumnDescriptionSQL(IDataColumn field) => String.Empty;
    #endregion
}
