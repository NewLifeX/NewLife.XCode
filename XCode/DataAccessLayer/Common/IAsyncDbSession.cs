using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using NewLife;
using NewLife.Data;

namespace XCode.DataAccessLayer;

/// <summary>数据库会话异步接口</summary>
public interface IAsyncDbSession : IDisposable2
{
    #region 异步操作
    /// <summary>执行SQL查询，返回记录集</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    Task<DbTable> QueryAsync(String sql, IDataParameter[]? ps);

    /// <summary>执行SQL查询，返回总记录数</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns>总记录数</returns>
    Task<Int64> QueryCountAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps);

    /// <summary>执行SQL查询，返回总记录数</summary>
    /// <param name="builder">查询生成器</param>
    /// <returns>总记录数</returns>
    Task<Int64> QueryCountAsync(SelectBuilder builder);

    /// <summary>快速查询单表记录数，稍有偏差</summary>
    /// <param name="tableName">表名</param>
    /// <returns></returns>
    Task<Int64> QueryCountFastAsync(String tableName);

    /// <summary>执行SQL语句，返回受影响的行数</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    Task<Int32> ExecuteAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps);

    /// <summary>执行DbCommand，返回受影响的行数</summary>
    /// <param name="cmd">DbCommand</param>
    /// <returns></returns>
    Task<Int32> ExecuteAsync(DbCommand cmd);

    /// <summary>执行插入语句并返回新增行的自动编号</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps);

    /// <summary>执行SQL语句，返回结果中的第一行第一列</summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    Task<T?> ExecuteScalarAsync<T>(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps);
    #endregion
}