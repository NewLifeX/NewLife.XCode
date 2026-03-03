using System.ComponentModel;
using NewLife.Log;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Shards;

namespace XCode;

public partial class Entity<TEntity>
{
    /// <summary>实体元数据</summary>
    public static class Meta
    {
        static Meta()
        {
            // 避免实际应用中，直接调用Entity.Meta的静态方法时，没有引发TEntity的静态构造函数。
            var entity = new TEntity();
        }

        #region 主要属性
        /// <summary>实体类型</summary>
        public static Type ThisType => typeof(TEntity);

        //private static IEntityFactory _Factory;
        /// <summary>实体操作者</summary>
        public static IEntityFactory Factory
        {
            get
            {
                // 不能缓存Factory，因为后期可能会改变注册，比如Menu
                //if (_Factory != null) return _Factory;

                var type = ThisType;
                return type.AsFactory();
            }
        }
        #endregion

        #region 基本属性
        /// <summary>数据表元数据信息。来自实体类，并合并默认连接名上的文件模型</summary>
        public static TableItem Table { get => Wrap.Table; set => Wrap.Table = value; }

        /// <summary>当前链接名。线程内允许修改，修改者负责还原。若要还原默认值，设为null即可</summary>
        public static String ConnName { get => Wrap.ConnName; set { Wrap.ConnName = value; } }

        /// <summary>当前表名。线程内允许修改，修改者负责还原</summary>
        public static String TableName { get => Wrap.TableName; set { Wrap.TableName = value; } }

        /// <summary>所有数据属性</summary>
        public static FieldItem[] AllFields => Table.AllFields;

        /// <summary>所有绑定到数据表的属性</summary>
        public static FieldItem[] Fields => Table.Fields;

        /// <summary>字段名集合，不区分大小写的哈希表存储，外部不要修改元素数据</summary>
        public static ICollection<String> FieldNames => Table.FieldNames;

        /// <summary>唯一键，返回第一个标识列或者唯一的主键</summary>
        public static FieldItem? Unique
        {
            get
            {
                var dt = Table;
                if (dt.PrimaryKeys.Length == 1) return dt.PrimaryKeys[0];
                return dt.Identity != null ? dt.Identity : null;
            }
        }

        /// <summary>主字段。主字段作为业务主要字段，代表当前数据行意义</summary>
        public static FieldItem? Master => Table.Master ?? Unique;
        #endregion

        #region 会话
#if NET45
        private static readonly ThreadLocal<SessionWrap?> _wrap = new();
#else
        private static readonly AsyncLocal<SessionWrap?> _wrap = new();
#endif
        private static SessionWrap Wrap => _wrap.Value ??= new SessionWrap();

        /// <summary>实体会话。线程静态</summary>
        public static EntitySession<TEntity> Session => Wrap.Session;

        class SessionWrap
        {
            private String? _ConnName;
            public String ConnName
            {
                get => _ConnName ?? Table.ConnName;
                set { _ConnName = value; Reset(); }
            }

            private String? _TableName;
            public String TableName
            {
                get => _TableName ?? Table.TableName;
                set { _TableName = value; Reset(); }
            }

            private TableItem? _Table;
            public TableItem Table
            {
                get => _Table ??= TableItem.Create(ThisType);
                set { _Table = value; _Session = null; }
            }

            private EntitySession<TEntity>? _Session;
            private String? _SessionConnName;
            private String? _SessionTableName;
            public EntitySession<TEntity> Session
            {
                get
                {
                    var currentConn = ConnName;
                    var currentTable = TableName;
                    
                    // 如果连接名或表名变化，重置 Session
                    if (_Session != null && (_SessionConnName != currentConn || _SessionTableName != currentTable))
                    {
                        _Session = null;
                    }
                    
                    // 延迟初始化或重新创建 Session
                    if (_Session == null)
                    {
                        _Session = EntitySession<TEntity>.Create(currentConn, currentTable);
                        _SessionConnName = currentConn;
                        _SessionTableName = currentTable;
                    }
                    
                    return _Session;
                }
            }

            void Reset()
            {
                _Table = null;
                _Session = null;
                _SessionConnName = null;
                _SessionTableName = null;
            }
        }
        #endregion

        #region 事务保护
        /// <summary>开始事务</summary>
        /// <returns>剩下的事务计数</returns>
        //[Obsolete("=>Session")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Int32 BeginTrans() => Session.BeginTrans();

        /// <summary>提交事务</summary>
        /// <returns>剩下的事务计数</returns>
        //[Obsolete("=>Session")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Int32 Commit() => Session.Commit();

        /// <summary>回滚事务，忽略异常</summary>
        /// <returns>剩下的事务计数</returns>
        //[Obsolete("=>Session")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Int32 Rollback() => Session.Rollback();

        /// <summary>创建事务</summary>
        public static EntityTransaction CreateTrans() => new EntityTransaction<TEntity>();
        #endregion

        #region 辅助方法
        ///// <summary>格式化关键字</summary>
        ///// <param name="name">名称</param>
        ///// <returns></returns>
        //public static String FormatName(String name) => Session.Dal.Db.FormatName(name);

        ///// <summary>格式化时间</summary>
        ///// <param name="dateTime"></param>
        ///// <returns></returns>
        //public static String FormatDateTime(DateTime dateTime) => Session.Dal.Db.FormatDateTime(dateTime);

        ///// <summary>格式化数据为SQL数据</summary>
        ///// <param name="name">名称</param>
        ///// <param name="value">数值</param>
        ///// <returns></returns>
        //public static String FormatValue(String name, Object value) => FormatValue(Table.FindByName(name), value);

        ///// <summary>格式化数据为SQL数据</summary>
        ///// <param name="field">字段</param>
        ///// <param name="value">数值</param>
        ///// <returns></returns>
        //public static String FormatValue(FieldItem field, Object value) => Session.Dal.Db.FormatValue(field?.Field, value);
        #endregion

        #region 缓存
        /// <summary>实体缓存</summary>
        /// <returns></returns>
        //[Obsolete("=>Session")]
        //[EditorBrowsable(EditorBrowsableState.Never)]
        public static EntityCache<TEntity> Cache => Session.Cache;

        /// <summary>单对象实体缓存。</summary>
        //[Obsolete("=>Session")]
        //[EditorBrowsable(EditorBrowsableState.Never)]
        public static ISingleEntityCache<Object, TEntity> SingleCache => Session.SingleCache;

        /// <summary>总记录数，小于1000时是精确的，大于1000时缓存10分钟</summary>
        //[Obsolete("=>Session")]
        //[EditorBrowsable(EditorBrowsableState.Never)]
        public static Int32 Count => (Int32)Session.LongCount;
        #endregion

        #region 分表分库
        /// <summary>分表分库策略</summary>
        public static IShardPolicy? ShardPolicy { get; set; }

        [ThreadStatic]
        private static Boolean _InShard;
        /// <summary>是否正处于分表操作中</summary>
        public static Boolean InShard => _InShard;

        /// <summary>创建分库会话，using结束时自动还原</summary>
        /// <param name="connName">连接名</param>
        /// <param name="tableName">表名</param>
        /// <returns></returns>
        public static IDisposable CreateSplit(String connName, String tableName) => new SplitPackge(connName, tableName);

        ///// <summary>针对实体对象自动分库分表</summary>
        ///// <param name="entity"></param>
        ///// <returns></returns>
        //public static IDisposable? CreateShard(TEntity entity)
        //{
        //    // 使用自动分表分库策略
        //    var model = ShardPolicy?.Shard(entity);
        //    return model != null ? new SplitPackge(model.ConnName, model.TableName) : null;
        //}

        /// <summary>为实体对象、时间、雪花Id等计算分表分库</summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static IDisposable? CreateShard(Object value)
        {
            // 使用自动分表分库策略
            var model = ShardPolicy?.Shard(value);
            return model == null ? null : (IDisposable)new SplitPackge(model.ConnName, model.TableName);
        }

        /// <summary>针对时间区间自动分库分表，常用于多表顺序查询，支持倒序</summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public static IEnumerable<T> AutoShard<T>(DateTime start, DateTime end, Func<T> callback)
        {
            // 使用自动分表分库策略
            var models = ShardPolicy?.Shards(start, end);
            if (models == null) yield break;

            foreach (var shard in models)
            {
                // 如果目标分表不存在，则不要展开查询
                var dal = !shard.ConnName.IsNullOrEmpty() ? DAL.Create(shard.ConnName) : Session.Dal;
                if (shard.TableName.IsNullOrEmpty() || !dal.TableNames.Contains(shard.TableName)) continue;

                using var split = new SplitPackge(shard.ConnName, shard.TableName);
                yield return callback();
            }
        }

        private class SplitPackge : IDisposable
        {
            /// <summary>连接名</summary>
            public String? ConnName { get; set; }

            /// <summary>表名</summary>
            public String? TableName { get; set; }

            public SplitPackge(String? connName, String? tableName)
            {
                ConnName = Meta.ConnName;
                TableName = Meta.TableName;
#if DEBUG
                XTrace.WriteLine("CreateSplit: {0}->{2}, {1}->{3}", ConnName, TableName, connName, tableName);
#endif

                Meta.ConnName = connName!;
                Meta.TableName = tableName!;
                _InShard = true;
            }

            public void Dispose()
            {
#if DEBUG
                XTrace.WriteLine("RestoreSplit: {0}, {1}", ConnName, TableName);
#endif
                Meta.ConnName = ConnName!;
                Meta.TableName = TableName!;
                _InShard = false;
            }
        }

        /// <summary>针对时间区间自动分库分表，常用于多表顺序查询，支持倒序</summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public static IEnumerable<T> AutoShard<T>(DateTime start, DateTime end, Func<IEntitySession, T> callback)
        {
            // 使用自动分表分库策略
            var models = ShardPolicy?.Shards(start, end);
            if (models == null) yield break;

            foreach (var shard in models)
            {
                // 如果目标分表不存在，则不要展开查询
                var dal = !shard.ConnName.IsNullOrEmpty() ? DAL.Create(shard.ConnName) : Session.Dal;
                if (!dal.TableNames.Contains(shard.TableName)) continue;

                var ss = EntitySession<TEntity>.Create(shard.ConnName, shard.TableName);
                yield return callback(ss);
            }
        }
        #endregion

        #region 拦截器
        //private static EntityInterceptors _Interceptors = new(typeof(TEntity));
#pragma warning disable CS0618 // 类型或成员已过时
        private static EntityModules _Interceptors = new(typeof(TEntity));
#pragma warning restore CS0618 // 类型或成员已过时
        /// <summary>实体拦截器集合</summary>
        public static EntityInterceptors Interceptors => _Interceptors;

        /// <summary>实体模块集合。旧版名称，建议使用 Interceptors</summary>
        [Obsolete("请使用 Interceptors")]
        public static EntityModules Modules => _Interceptors;
        #endregion
    }
}