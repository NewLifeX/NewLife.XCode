using System.Data;
using NewLife.Data;
using XCode.Cache;
using XCode.DataAccessLayer;

namespace XCode;

/// <summary>实体会话接口</summary>
public interface IEntitySession
{
    #region 属性
    /// <summary>连接名</summary>
    String ConnName { get; }

    /// <summary>表名</summary>
    String TableName { get; }

    /// <summary>数据表</summary>
    IDataTable DataTable { get; }

    /// <summary>用于标识会话的键值</summary>
    String Key { get; }

    /// <summary>已格式化的表名，带有中括号等</summary>
    String FormatedTableName { get; }

    /// <summary>数据操作层</summary>
    DAL Dal { get; }

    /// <summary>用户数据</summary>
    IDictionary<String, Object> Items { get; set; }
    #endregion

    #region 数据初始化
    /// <summary>检查并初始化数据。参数等待时间为0表示不等待</summary>
    /// <param name="ms">等待时间，-1表示不限，0表示不等待</param>
    /// <returns>如果等待，返回是否收到信号</returns>
    Boolean WaitForInitData(Int32 ms = 3000);
    #endregion

    #region 缓存
    /// <summary>实体缓存</summary>
    /// <returns></returns>
    IEntityCache Cache { get; }

    /// <summary>单对象实体缓存。
    /// 建议自定义查询数据方法，并从二级缓存中获取实体数据，以抵消因初次填充而带来的消耗。
    /// </summary>
    ISingleEntityCache SingleCache { get; }

    /// <summary>总记录数。10 万以下精确，10 万~100 万后台校准，100 万以上为估算值；需要精确值时使用 FindCount()</summary>
    Int32 Count { get; }

    /// <summary>总记录数。10 万以下精确，10 万~100 万后台校准，100 万以上为估算值；需要精确值时使用 FindCount()</summary>
    /// <remarks>
    /// 1，优先读取缓存值；
    /// 2，小表同步执行 count(*)；
    /// 3，中等表先返回索引估算值，后台异步精确校准；
    /// 4，大表只使用索引估算值，避免 count(*) 超时。
    /// </remarks>
    Int64 LongCount { get; }

    /// <summary>清除缓存</summary>
    /// <param name="reason">清除原因</param>
    [Obsolete]
    void ClearCache(String reason);

    /// <summary>清除缓存</summary>
    /// <param name="reason">清除原因</param>
    /// <param name="force">强制清除，下次访问阻塞等待。默认false仅置为过期，下次访问异步更新</param>
    void ClearCache(String reason, Boolean force);
    #endregion

    #region 数据库操作
    /// <summary>初始化数据，执行反向工程检查，建库建表</summary>
    void InitData();

    /// <summary>执行SQL查询，返回记录集</summary>
    /// <param name="builder">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns></returns>
    DbTable Query(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows);

    /// <summary>查询记录数</summary>
    /// <param name="builder">查询生成器</param>
    /// <returns>记录数</returns>
    Int32 QueryCount(SelectBuilder builder);

    /// <summary>执行</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns>影响的结果</returns>
    Int32 Execute(String sql, CommandType type = CommandType.Text, params IDataParameter[] ps);

    /// <summary>执行插入语句并返回新增行的自动编号</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[] ps);

    /// <summary>执行SQL查询，返回记录集</summary>
    /// <param name="builder">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns></returns>
    Task<DbTable> QueryAsync(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows);

    /// <summary>查询记录数</summary>
    /// <param name="builder">查询生成器</param>
    /// <returns>记录数</returns>
    Task<Int64> QueryCountAsync(SelectBuilder builder);

    /// <summary>执行</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns>影响的结果</returns>
    Task<Int32> ExecuteAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps);

    /// <summary>执行插入语句并返回新增行的自动编号</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps);

    /// <summary>执行Truncate语句</summary>
    /// <returns>影响的结果</returns>
    Int32 Truncate();

    /// <summary>数据改变后触发。参数指定触发该事件的实体类</summary>
    event Action<Type> OnDataChange;
    #endregion

    #region 事务保护
    /// <summary>开始事务</summary>
    /// <returns>剩下的事务计数</returns>
    Int32 BeginTrans();

    /// <summary>提交事务</summary>
    /// <returns>剩下的事务计数</returns>
    Int32 Commit();

    /// <summary>回滚事务，忽略异常</summary>
    /// <returns>剩下的事务计数</returns>
    Int32 Rollback();

    /// <summary>创建事务</summary>
    EntityTransaction CreateTrans();
    #endregion

    #region 参数化
    ///// <summary>创建参数</summary>
    ///// <returns></returns>
    //IDataParameter CreateParameter();

    ///// <summary>格式化参数名</summary>
    ///// <param name="name">名称</param>
    ///// <returns></returns>
    //String FormatParameterName(String name);
    #endregion

    #region 实体操作
    /// <summary>把该对象持久化到数据库，添加/更新实体缓存。</summary>
    /// <param name="entity">实体对象</param>
    /// <returns></returns>
    Int32 Insert(IEntity entity);

    /// <summary>更新数据库，同时更新实体缓存</summary>
    /// <param name="entity">实体对象</param>
    /// <returns></returns>
    Int32 Update(IEntity entity);

    /// <summary>从数据库中删除该对象，同时从实体缓存中删除</summary>
    /// <param name="entity">实体对象</param>
    /// <returns></returns>
    Int32 Delete(IEntity entity);

    /// <summary>把该对象持久化到数据库，添加/更新实体缓存。</summary>
    /// <param name="entity">实体对象</param>
    /// <returns></returns>
    Task<Int32> InsertAsync(IEntity entity);

    /// <summary>更新数据库，同时更新实体缓存</summary>
    /// <param name="entity">实体对象</param>
    /// <returns></returns>
    Task<Int32> UpdateAsync(IEntity entity);

    /// <summary>从数据库中删除该对象，同时从实体缓存中删除</summary>
    /// <param name="entity">实体对象</param>
    /// <returns></returns>
    Task<Int32> DeleteAsync(IEntity entity);
    #endregion
}