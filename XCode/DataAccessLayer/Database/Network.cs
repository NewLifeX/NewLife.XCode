using System.Data;
using System.Data.Common;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode.Services;

namespace XCode.DataAccessLayer;

/// <summary>网络虚拟数据库。通过HTTP接口将数据库操作转发到远端服务执行</summary>
/// <remarks>
/// 连接字符串格式：Server=http://127.0.0.1:3305;Database=Membership;Password=token123;Provider=Network
/// 
/// 应用层使用标准XCode实体操作，底层自动转为HTTP接口调用：
/// - SQL查询 → POST /Db/Query
/// - SQL执行 → POST /Db/Execute
/// - 插入并返回ID → POST /Db/InsertAndGetIdentity
/// - 快速记录数 → GET /Db/QueryCount
/// 
/// 不追求极致性能，面向普通场合（管理后台、数据同步、跨服务查询等）。
/// </remarks>
internal class NetworkDb : DbBase
{
    #region 属性
    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.Network;

    /// <summary>服务端数据库类型</summary>
    public DatabaseType RawType { get; private set; }

    /// <summary>服务端数据库对象，用于格式化SQL等操作</summary>
    public IDatabase? Server { get; private set; }

    /// <summary>创建工厂。网络数据库不使用本地工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory() => Server?.Factory;

    /// <summary>解析连接字符串</summary>
    /// <param name="builder"></param>
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        // 提取Server、Database、Password，保留在builder中避免连接字符串变空
        _serverUrl = builder["Server"];
        _database = builder["Database"];
        _token = builder["Password"];
    }
    #endregion

    #region 方法
    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new NetworkSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new NetworkMetaData();

    /// <summary>判断是否支持该提供者</summary>
    /// <param name="providerName">提供者名称</param>
    /// <returns></returns>
    public override Boolean Support(String providerName)
    {
        providerName = providerName.ToLower();
        if (providerName.Contains("network") || providerName.Contains("net")) return true;

        return false;
    }
    #endregion

    #region 网络通信
    private String? _serverUrl;
    private String? _database;
    private String? _token;
    private DbClient? _Client;

    /// <summary>获取或创建HTTP客户端</summary>
    /// <returns></returns>
    public DbClient GetClient()
    {
        if (_Client != null) return _Client;

        lock (this)
        {
            if (_Client != null) return _Client;

            if (_serverUrl.IsNullOrEmpty()) throw new InvalidOperationException("网络数据库未设置Server地址");
            if (_database.IsNullOrEmpty()) throw new InvalidOperationException("网络数据库未设置Database名称");

            var client = new DbClient(_serverUrl, _database, _token)
            {
                Log = XTrace.Log,
            };

            client.Open();

            // 同步登录获取服务端信息
            var task = client.LoginAsync();
            var info = task.ConfigureAwait(false).GetAwaiter().GetResult();

            //var info = client.Info;
            if (info != null)
            {
                _ServerVersion = info.Version;
                RawType = info.DbType;

                // 获取服务端同类型数据库实例，用于SQL格式化等
                Server = DbFactory.GetDefault(RawType);
            }

            _Client = client;
        }

        return _Client;
    }
    #endregion

    #region 分页
    /// <summary>构造分页SQL</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <param name="keyColumn">唯一键。用于not in分页</param>
    /// <returns>分页SQL</returns>
    public override String PageSplit(String sql, Int64 startRowIndex, Int64 maximumRows, String? keyColumn)
    {
        // 委托给服务端同类型数据库实现
        if (Server != null)
            return Server.PageSplit(sql, startRowIndex, maximumRows, keyColumn);

        // 默认使用LIMIT分页
        if (startRowIndex <= 0)
        {
            if (maximumRows < 1) return sql;
            return $"{sql} limit {maximumRows}";
        }
        if (maximumRows < 1) throw new NotSupportedException("不支持取第几条数据之后的所有数据！");
        return $"{sql} limit {startRowIndex}, {maximumRows}";
    }

    /// <summary>构造分页SQL</summary>
    /// <param name="builder">查询生成器</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>分页SQL</returns>
    public override SelectBuilder PageSplit(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows)
    {
        if (Server != null)
            return Server.PageSplit(builder, startRowIndex, maximumRows);

        // 默认LIMIT分页
        if (startRowIndex <= 0)
        {
            if (maximumRows > 0)
                builder.Limit = $"limit {maximumRows}";
        }
        else
        {
            builder.Limit = $"limit {startRowIndex}, {maximumRows}";
        }
        return builder;
    }
    #endregion

    #region 数据库特性
    /// <summary>长文本长度</summary>
    public override Int32 LongTextLength => Server?.LongTextLength ?? 4000;

    /// <summary>格式化时间为SQL字符串</summary>
    /// <param name="dateTime">时间值</param>
    /// <returns></returns>
    public override String FormatDateTime(DateTime dateTime) => Server?.FormatDateTime(dateTime) ?? $"'{dateTime:yyyy-MM-dd HH:mm:ss}'";

    /// <summary>格式化名称，如果不是关键字，则原样返回</summary>
    /// <param name="name">名称</param>
    /// <returns></returns>
    public override String FormatName(String name) => Server?.FormatName(name) ?? name;

    /// <summary>格式化数据为SQL数据</summary>
    /// <param name="field">字段</param>
    /// <param name="value">数值</param>
    /// <returns></returns>
    public override String FormatValue(IDataColumn field, Object? value) => Server?.FormatValue(field, value) ?? (value?.ToString() ?? "NULL");

    /// <summary>格式化参数名</summary>
    /// <param name="name">名称</param>
    /// <returns></returns>
    public override String FormatParameterName(String name) => Server?.FormatParameterName(name) ?? $"@{name}";

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => Server?.StringConcat(left, right) ?? $"{left}+{right}";

    /// <summary>创建参数</summary>
    /// <param name="name">名称</param>
    /// <param name="value">值</param>
    /// <param name="field">字段</param>
    /// <returns></returns>
    public override IDataParameter CreateParameter(String name, Object? value, IDataColumn? field = null)
    {
        if (Server != null) return Server.CreateParameter(name, value, field);

        var dp = new DataParameter { ParameterName = FormatParameterName(name), Value = value ?? DBNull.Value };
        return dp;
    }

    /// <summary>创建参数数组</summary>
    /// <param name="ps"></param>
    /// <returns></returns>
    public override IDataParameter[] CreateParameters(IDictionary<String, Object>? ps)
    {
        if (Server != null) return Server.CreateParameters(ps);

        if (ps == null) return [];

        var list = new List<IDataParameter>();
        foreach (var item in ps)
        {
            list.Add(CreateParameter(item.Key, item.Value, (IDataColumn?)null));
        }
        return list.ToArray();
    }
    #endregion
}

/// <summary>网络数据库会话。将SQL操作转为HTTP接口调用</summary>
/// <remarks>实例化网络数据库会话</remarks>
/// <param name="db">数据库实例</param>
internal class NetworkSession(IDatabase db) : DbSession(db)
{
    #region 重载
    /// <summary>执行SQL查询，返回记录集</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    public override DbTable Query(String sql, IDataParameter[]? ps)
    {
        var client = GetClient();

        var dps = ConvertParameters(ps);
        return client.QueryAsync(sql, dps).ConfigureAwait(false).GetAwaiter().GetResult() ?? new DbTable();
    }

    /// <summary>执行SQL查询，返回总记录数</summary>
    /// <param name="builder">查询生成器</param>
    /// <returns>总记录数</returns>
    public override Int64 QueryCount(SelectBuilder builder)
    {
        var ds = Query(builder.SelectCount().ToString(), builder.Parameters.ToArray());
        if (ds?.Rows == null || ds.Rows.Count == 0) return -1;

        return ds.Rows[0][0].ToLong();
    }

    /// <summary>快速查询单表记录数，稍有偏差</summary>
    /// <param name="tableName">表名</param>
    /// <returns></returns>
    public override Int64 QueryCountFast(String tableName)
    {
        var client = GetClient();
        return client.QueryCountAsync(tableName).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>执行SQL语句，返回受影响的行数</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    public override Int32 Execute(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        var client = GetClient();

        var dps = ConvertParameters(ps);
        return (Int32)client.ExecuteAsync(sql, dps).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>执行插入语句并返回新增行的自动编号</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        var client = GetClient();

        var dps = ConvertParameters(ps);
        return client.InsertAndGetIdentityAsync(sql, dps).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>异步查询</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    public override async Task<DbTable> QueryAsync(String sql, IDataParameter[]? ps)
    {
        var client = GetClient();

        var dps = ConvertParameters(ps);
        return await client.QueryAsync(sql, dps).ConfigureAwait(false) ?? new DbTable();
    }

    /// <summary>异步执行</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    public override async Task<Int32> ExecuteAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        var client = GetClient();

        var dps = ConvertParameters(ps);
        return (Int32)await client.ExecuteAsync(sql, dps).ConfigureAwait(false);
    }

    /// <summary>异步插入并返回自增ID</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    public override async Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        var client = GetClient();

        var dps = ConvertParameters(ps);
        return await client.InsertAndGetIdentityAsync(sql, dps).ConfigureAwait(false);
    }

    /// <summary>返回数据源的架构信息。网络数据库不支持架构查询</summary>
    /// <param name="conn">连接</param>
    /// <param name="collectionName">指定要返回的架构的名称</param>
    /// <param name="restrictionValues">为请求的架构指定一组限制值</param>
    /// <returns></returns>
    public override DataTable GetSchema(DbConnection? conn, String collectionName, String?[]? restrictionValues) => new();
    #endregion

    #region 辅助
    /// <summary>获取网络客户端</summary>
    /// <returns></returns>
    private DbClient GetClient() => (Database as NetworkDb)?.GetClient() ?? throw new InvalidOperationException("网络数据库未初始化");

    /// <summary>将IDataParameter数组转换为字典</summary>
    /// <param name="ps">参数数组</param>
    /// <returns></returns>
    private static IDictionary<String, Object?>? ConvertParameters(IDataParameter[]? ps)
    {
        if (ps == null || ps.Length == 0) return null;

        var dic = new Dictionary<String, Object?>();
        foreach (var p in ps)
        {
            dic[p.ParameterName] = p.Value == DBNull.Value ? null : p.Value;
        }
        return dic;
    }
    #endregion
}

/// <summary>网络数据库元数据。通过HTTP获取远端表结构</summary>
internal class NetworkMetaData : DbMetaData
{
    /// <summary>设置表结构。网络数据库不需要本地建表</summary>
    /// <param name="tables">表集合</param>
    /// <param name="mode">迁移模式</param>
    /// <param name="set">XCode设置</param>
    protected override void OnSetTables(IDataTable[] tables, Migration mode, XCodeSetting set) { }
}

/// <summary>简易数据参数。用于网络数据库不依赖具体ADO.NET提供者时创建参数</summary>
internal class DataParameter : IDataParameter
{
    /// <summary>数据库类型</summary>
    public DbType DbType { get; set; }

    /// <summary>方向</summary>
    public ParameterDirection Direction { get; set; } = ParameterDirection.Input;

    /// <summary>是否可空</summary>
    public Boolean IsNullable => true;

    /// <summary>参数名</summary>
    public String ParameterName { get; set; } = "";

    /// <summary>源列名</summary>
    public String SourceColumn { get; set; } = "";

    /// <summary>源数据行版本</summary>
    public DataRowVersion SourceVersion { get; set; } = DataRowVersion.Current;

    /// <summary>值</summary>
    public Object? Value { get; set; }
}