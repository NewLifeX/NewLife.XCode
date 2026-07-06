using System.Data;
using System.Data.Common;
using NewLife.Collections;
using NewLife.Log;

namespace XCode.DataAccessLayer;

/// <summary>连接池</summary>
/// <remarks>
/// 默认设置：
/// 1，最小连接为CPU个数，最小2个最大8个
/// 2，最大连接1000
/// 3，空闲时间30s
/// </remarks>
public class ConnectionPool : ObjectPool<DbConnection>
{
    #region 属性
    /// <summary>工厂</summary>
    public DbProviderFactory? Factory { get; set; }

    /// <summary>连接字符串</summary>
    public String? ConnectionString { get; set; }
    #endregion

    /// <summary>实例化一个连接池</summary>
    public ConnectionPool()
    {
        Min = Environment.ProcessorCount;
        if (Min < 2) Min = 2;
        if (Min > 8) Min = 8;

        Max = 1000;
        IdleTime = 30;
    }

    /// <summary>创建时连接数据库</summary>
    /// <returns></returns>
    protected override DbConnection OnCreate()
    {
        var conn = Factory?.CreateConnection();
        if (conn == null)
        {
            var msg = $"连接创建失败！请检查驱动是否正常";

            XTrace.WriteLine("CreateConnection failure " + msg);

            throw new Exception(Name + " " + msg);
        }

        conn.ConnectionString = ConnectionString;

        try
        {
            conn.Open();
        }
        catch (DbException ex)
        {
            DAL.WriteLog("Open错误：[{0}]{1}", ex?.GetTrue()?.Message, conn.ConnectionString);
            throw;
        }

        return conn;
    }

    /// <summary>异步创建时连接数据库</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    protected override async Task<DbConnection?> OnCreateAsync(CancellationToken cancellationToken = default)
    {
        var conn = Factory?.CreateConnection();
        if (conn == null)
        {
            var msg = "连接创建失败！请检查驱动是否正常";
            XTrace.WriteLine("CreateConnection failure " + msg);
            throw new Exception(Name + " " + msg);
        }

        conn.ConnectionString = ConnectionString;

        try
        {
            await conn.OpenAsync().ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            DAL.WriteLog("Open错误：[{0}]{1}", ex?.GetTrue()?.Message, conn.ConnectionString);
            throw;
        }

        return conn;
    }

    /// <summary>申请时检查可用性，连接已关闭则重新打开</summary>
    /// <param name="value">连接对象</param>
    /// <returns>是否可用</returns>
    protected override Boolean OnGet(DbConnection value)
    {
        if (value.State == ConnectionState.Closed) value.Open();
        return true;
    }

    /// <summary>异步申请时检查可用性，连接已关闭则重新打开</summary>
    /// <param name="value">连接对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否可用</returns>
    protected override async Task<Boolean> OnGetAsync(DbConnection value, CancellationToken cancellationToken = default)
    {
        if (value.State == ConnectionState.Closed)
            await value.OpenAsync().ConfigureAwait(false);
        return true;
    }

    /// <summary>归还时检查连接是否有效。无效对象将被抛弃</summary>
    /// <param name="value">连接对象</param>
    /// <returns>是否有效</returns>
    protected override Boolean OnReturn(DbConnection value) => value.State == ConnectionState.Open;

    /// <summary>借一个连接执行指定操作</summary>
    /// <typeparam name="TResult">返回值类型</typeparam>
    /// <param name="callback">回调函数</param>
    /// <returns>回调返回值</returns>
    public TResult Execute<TResult>(Func<DbConnection, TResult> callback)
    {
        var conn = Get();
        try
        {
            return callback(conn);
        }
        finally
        {
            Return(conn);
        }
    }
}