using System.Data;
using System.Data.Common;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;

namespace XCode.DataAccessLayer;

/// <summary>MongoDB文档数据库。基于JSON文档模型，非关系型数据库</summary>
/// <remarks>
/// MongoDB是高性能文档型NoSQL数据库，适用于非结构化数据存储场景。
/// 连接字符串示例：mongodb://localhost:27017/mydb 或 Server=localhost;Port=27017;Database=mydb
/// 使用MongoDB.Driver作为驱动。
/// 注意：MongoDB不支持SQL，部分关系型数据库特性不可用（JOIN、事务、自增主键等）。
/// </remarks>
internal class MongoDB : RemoteDb
{
    #region 属性

    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.MongoDB;

    /// <summary>批量操作能力。MongoDB支持批量Insert</summary>
    public override BatchCapability BatchCapability => BatchCapability.Insert;

    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory()
    {
        // MongoDB使用自有驱动（MongoDB.Driver），不通过ADO.NET
        // 尝试加载MongoDB.Driver提供的工厂（如果存在）
        var type = DriverLoader.Load("MongoDB.Driver.MongoClientFactory", null, "MongoDB.Driver.dll", null);
        var factory = GetProviderFactory(type);
        if (factory != null) return factory;

        // MongoDB Driver不提供标准DbProviderFactory，返回null使用默认
        return null;
    }

    const String Server_Key = "Server";
    const String Port_Key = "Port";

    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        // 处理mongodb://格式的连接字符串
        var connStr = builder.ConnectionString;
        if (connStr.StartsWithIgnoreCase("mongodb://"))
        {
            // mongodb://连接字符串由MongoDB Driver直接处理，不做额外解析
            return;
        }

        // 标准键值对格式：设置默认端口
        if (builder[Port_Key].IsNullOrEmpty())
            builder[Port_Key] = "27017";
    }

    #endregion 属性

    #region 方法

    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new MongoDBSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new MongoDBMetaData();

    public override Boolean Support(String providerName)
    {
        providerName = providerName.ToLower();
        if (providerName.Contains("mongodb")) return true;
        if (providerName.Contains("mongo")) return true;

        return false;
    }

    #endregion 方法

    #region 数据库特性

    protected override String ReservedWordsStr => "ADD,ALL,AND,AS,ASC,BY,CASE,CURRENT,DELETE,DESC,DISTINCT,DROP,ELSE,END,EXISTS,FALSE,FROM,GROUP,HAVING,IF,IN,INSERT,INTO,IS,JOIN,LEFT,LIKE,LIMIT,MERGE,NOT,NULL,ON,OR,ORDER,RIGHT,SELECT,SET,SKIP,SORT,THEN,TRUE,UNION,UPDATE,USING,VALUES,WHEN,WHERE,WITH";

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
        if (code == TypeCode.Boolean)
        {
            return value.ToBoolean() ? "true" : "false";
        }

        return base.FormatValue(field, value);
    }

    /// <summary>长文本长度</summary>
    public override Int32 LongTextLength => 4000;

    protected internal override String ParamPrefix => "@";

    /// <summary>系统数据库名</summary>
    public override String SystemDatabaseName => "admin";

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => $"concat({(!String.IsNullOrEmpty(left) ? left : "\"\"")},{(!String.IsNullOrEmpty(right) ? right : "\"\"")})";

    #endregion 数据库特性
}

/// <summary>MongoDB数据库会话</summary>
/// <remarks>
/// MongoDB不使用SQL，大部分SQL操作方法会抛出NotSupportedException。
/// 通过MongoDB.Driver直接操作集合和文档。
/// </remarks>
internal class MongoDBSession : RemoteDbSession
{
    #region 构造函数

    public MongoDBSession(IDatabase db) : base(db) { }

    #endregion 构造函数

    #region 基本方法 查询/执行

    /// <summary>执行插入语句并返回新增行的自动编号。MongoDB使用ObjectId作为默认_id</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        // MongoDB使用ObjectId而非自增数字，插入后返回0
        Execute(sql, type, ps);
        return 0;
    }

    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        Execute(sql, type, ps);
        return Task.FromResult((Int64)0);
    }

    #endregion 基本方法 查询/执行
}

/// <summary>MongoDB数据库元数据</summary>
internal class MongoDBMetaData : RemoteDbMetaData
{
    #region 构造函数

    public MongoDBMetaData() { }

    #endregion 构造函数
}
