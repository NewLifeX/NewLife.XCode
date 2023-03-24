using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NewLife;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Web;

namespace XCode.DataAccessLayer;

internal class SqlServer : RemoteDb
{
    #region 属性
    /// <summary>返回数据库类型。外部DAL数据库类请使用Other</summary>
    public override DatabaseType Type => DatabaseType.SqlServer;

    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory CreateFactory()
    {
        // Microsoft 是最新的跨平台版本，优先使用
        //if (_Factory == null) _Factory = GetProviderFactory("Microsoft.Data.SqlClient.dll", "Microsoft.Data.SqlClient.SqlClientFactory", false, true);

        // 根据提供者加载已有驱动
        if (!Provider.IsNullOrEmpty() && Provider.Contains("Microsoft"))
        {
            var type = PluginHelper.LoadPlugin("Microsoft.Data.SqlClient.SqlClientFactory", null, "Microsoft.Data.SqlClient.dll", null);
            var factory = GetProviderFactory(type);
            if (factory != null) return factory;
        }

        // 找不到驱动时，再到线上下载
        {
            var factory = GetProviderFactory("System.Data.SqlClient.dll", "System.Data.SqlClient.SqlClientFactory");

            return factory;
        }
    }

    /// <summary>是否SQL2012及以上</summary>
    public Boolean IsSQL2012 => Version.Major > 11;

    private Version _Version;
    /// <summary>数据库版本</summary>
    public Version Version
    {
        get
        {
            if (_Version == null)
            {
                //_Version = new Version(ServerVersion);
                if (Version.TryParse(ServerVersion, out var v))
                    _Version = v;
                else
                {
                    var ns = ServerVersion.SplitAsInt(".");
                    if (ns.Length >= 4)
                        _Version = new Version(ns[0], ns[1], ns[2], ns[3]);
                    else if (ns.Length >= 3)
                        _Version = new Version(ns[0], ns[1], ns[2]);
                    else if (ns.Length >= 2)
                        _Version = new Version(ns[0], ns[1]);
                    else
                        _Version = new Version();
                }
            }
            return _Version;
        }
    }

    /// <summary>数据目录</summary>
    public String DataPath { get; set; }

    private const String Application_Name = "Application Name";
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        // 获取数据目录，用于反向工程创建数据库
        if (builder.TryGetAndRemove("DataPath", out var str) && !str.IsNullOrEmpty()) DataPath = str;

        base.OnSetConnectionString(builder);

        if (builder[Application_Name] == null)
        {
            var name = AppDomain.CurrentDomain.FriendlyName;
            builder[Application_Name] = $"XCode_{name}_{ConnName}";
        }
    }
    #endregion

    #region 方法
    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new SqlServerSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new SqlServerMetaData();

    public override Boolean Support(String providerName)
    {
        providerName = providerName.ToLower();
        if (providerName.Contains("system.data.sqlclient")) return true;
        if (providerName.Contains("sql2012")) return true;
        if (providerName.Contains("sql2008")) return true;
        if (providerName.Contains("sql2005")) return true;
        if (providerName.Contains("sql2000")) return true;
        if (providerName == "sqlclient") return true;
        if (providerName.Contains("mssql")) return true;
        if (providerName.Contains("sqlserver")) return true;
        if (providerName.Contains("microsoft.data.sqlclient")) return true;

        return false;
    }
    #endregion

    #region 分页
    /// <summary>构造分页SQL</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <param name="keyColumn">唯一键。用于not in分页</param>
    /// <returns>分页SQL</returns>
    public override String PageSplit(String sql, Int64 startRowIndex, Int64 maximumRows, String keyColumn)
    {
        // 从第一行开始，不需要分页
        if (startRowIndex <= 0 && maximumRows < 1) return sql;

        // 先用字符串判断，命中率高，这样可以提高处理效率
        var hasOrderBy = false;
        if (sql.Contains(" Order ") && sql.ToLower().Contains(" order "))
            hasOrderBy = true;

        // 使用MS SQL 2012特有的分页算法
        if (hasOrderBy && IsSQL2012) return PageSplitFor2012(sql, startRowIndex, maximumRows);

        var builder = new SelectBuilder();
        builder.Parse(sql);

        return PageSplit(builder, startRowIndex, maximumRows).ToString();
    }

    public static String PageSplitFor2012(String sql, Int64 startRowIndex, Int64 maximumRows)
    {
        // 从第一行开始，不需要分页
        if (startRowIndex <= 0)
        {
            if (maximumRows < 1) return sql;

            return $"{sql} offset 1 rows fetch next {maximumRows} rows only";
        }
        if (maximumRows < 1) throw new NotSupportedException("不支持取第几条数据之后的所有数据！");

        return $"{sql} offset {startRowIndex} rows fetch next {maximumRows} rows only";
    }

    /// <summary>
    /// 格式化SQL SERVER 2012分页前半部分SQL语句
    /// </summary>
    /// <param name="sql"></param>
    /// <returns></returns>
    private String FormatSqlserver2012SQL(String sql)
    {
        var builder = new SelectBuilder();
        builder.Parse(sql);

        var sb = Pool.StringBuilder.Get();
        sb.Append("Select ");
        sb.Append(builder.Column.IsNullOrEmpty() ? "*" : builder.Column);
        sb.Append(" From ");
        sb.Append(builder.Table);
        if (!String.IsNullOrEmpty(builder.Where))
        {
            sb.Append(" Where type='p' and " + builder.Where);
        }
        else
        {
            sb.Append(" Where type='p' ");
        }
        if (!String.IsNullOrEmpty(builder.GroupBy)) sb.Append(" Group By " + builder.GroupBy);
        if (!String.IsNullOrEmpty(builder.Having)) sb.Append(" Having " + builder.Having);
        if (!String.IsNullOrEmpty(builder.OrderBy)) sb.Append(" Order By " + builder.OrderBy);

        return sb.Put(true);
    }

    public override SelectBuilder PageSplit(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows)
    {
        // 首页处理
        if (startRowIndex <= 0)
        {
            if (maximumRows < 1) return builder;

            return builder.Clone().Top(maximumRows);
        }

        // 修复无主键分页报错的情况
        if (builder.Key.IsNullOrEmpty() && builder.OrderBy.IsNullOrEmpty()) throw new XCodeException("分页算法要求指定排序列！" + builder.ToString());

        // Sql2012，非首页
        if (IsSQL2012 && !builder.OrderBy.IsNullOrEmpty())
        {
            builder = builder.Clone();
            builder.Limit = $"offset {startRowIndex} rows fetch next {maximumRows} rows only";
            return builder;
        }

        // 如果包含分组，则必须作为子查询
        var builder1 = builder.CloneWithGroupBy("XCode_T0", true);
        // 不必追求极致，把所有列放出来
        builder1.Column = $"*, row_number() over(Order By {builder.OrderBy ?? builder.Key}) as rowNumber";

        var builder2 = builder1.AsChild("XCode_T1", true);
        // 结果列处理
        //builder2.Column = builder.Column;
        //// 如果结果列包含有“.”，即有形如tab1.id、tab2.name之类的列时设为获取子查询的全部列
        //if ((!string.IsNullOrEmpty(builder2.Column)) && builder2.Column.Contains("."))
        //{
        //    builder2.Column = "*";
        //}
        // 不必追求极致，把所有列放出来
        builder2.Column = "*";

        // row_number()直接影响了排序，这里不再需要
        builder2.OrderBy = null;
        if (maximumRows < 1)
            builder2.Where = $"rowNumber>={startRowIndex + 1}";
        else
            builder2.Where = $"rowNumber Between {startRowIndex + 1} And {startRowIndex + maximumRows}";

        return builder2;
    }

    /// <summary>按top not in构造分页SQL</summary>
    /// <remarks>
    /// 两个构造分页SQL的方法，区别就在于查询生成器能够构造出来更好的分页语句，尽可能的避免子查询。
    /// MS体系的分页精髓就在于唯一键，当唯一键带有Asc/Desc/Unkown等排序结尾时，就采用最大最小值分页，否则使用较次的TopNotIn分页。
    /// TopNotIn分页和MaxMin分页的弊端就在于无法完美的支持GroupBy查询分页，只能查到第一页，往后分页就不行了，因为没有主键。
    /// </remarks>
    /// <param name="sql">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <param name="keyColumn">唯一键。用于not in分页</param>
    /// <returns>分页SQL</returns>
    public virtual String PageSplitByTopNotIn(String sql, Int64 startRowIndex, Int64 maximumRows, String keyColumn)
    {
        // 从第一行开始，不需要分页
        if (startRowIndex <= 0 && maximumRows < 1) return sql;

        #region Max/Min分页
        // 如果要使用max/min分页法，首先keyColumn必须有asc或者desc
        if (!String.IsNullOrEmpty(keyColumn))
        {
            var kc = keyColumn.ToLower();
            if (kc.EndsWith(" desc") || kc.EndsWith(" asc") || kc.EndsWith(" unknown"))
            {
                var str = PageSplitMaxMin(sql, startRowIndex, maximumRows, keyColumn);
                if (!String.IsNullOrEmpty(str)) return str;

                // 如果不能使用最大最小值分页，则砍掉排序，为TopNotIn分页做准备
                var p = keyColumn.IndexOf(' ');
                if (p > 0) keyColumn = keyColumn[..p];
            }
        }
        #endregion

        //检查简单SQL。为了让生成分页SQL更短
        var tablename = CheckSimpleSQL(sql);
        if (tablename != sql)
            sql = tablename;
        else
            sql = $"({sql}) XCode_Temp_a";

        // 取第一页也不用分页。把这代码放到这里，主要是数字分页中要自己处理这种情况
        if (startRowIndex <= 0 && maximumRows > 0)
            return $"Select Top {maximumRows} * From {sql}";

        if (String.IsNullOrEmpty(keyColumn)) throw new ArgumentNullException(nameof(keyColumn), "这里用的not in分页算法要求指定主键列！");

        if (maximumRows < 1)
            sql = $"Select * From {sql} Where {keyColumn} Not In(Select Top {startRowIndex} {keyColumn} From {sql})";
        else
            sql = $"Select Top {maximumRows} * From {sql} Where {keyColumn} Not In(Select Top {startRowIndex} {keyColumn} From {sql})";
        return sql;
    }

    private static readonly Regex reg_Order = new(@"\border\s*by\b([^)]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    /// <summary>按唯一数字最大最小分析</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <param name="keyColumn">唯一键。用于not in分页</param>
    /// <returns>分页SQL</returns>
    public static String PageSplitMaxMin(String sql, Int64 startRowIndex, Int64 maximumRows, String keyColumn)
    {
        // 唯一键的顺序。默认为Empty，可以为asc或desc，如果有，则表明主键列是数字唯一列，可以使用max/min分页法
        var isAscOrder = keyColumn.ToLower().EndsWith(" asc");
        // 是否使用max/min分页法
        var canMaxMin = false;

        // 如果sql最外层有排序，且唯一的一个排序字段就是keyColumn时，可用max/min分页法
        // 如果sql最外层没有排序，其排序不是unknown，可用max/min分页法
        var ms = reg_Order.Matches(sql);
        if (ms != null && ms.Count > 0 && ms[0].Index > 0)
        {
            #region 有OrderBy
            // 取第一页也不用分页。把这代码放到这里，主要是数字分页中要自己处理这种情况
            if (startRowIndex <= 0 && maximumRows > 0)
                return $"Select Top {maximumRows} * From {CheckSimpleSQL(sql)}";

            var p = keyColumn.IndexOf(' ');
            if (p > 0) keyColumn = keyColumn[..p];
            sql = sql[..ms[0].Index];

            var strOrderBy = ms[0].Groups[1].Value.Trim();
            // 只有一个排序字段
            if (!String.IsNullOrEmpty(strOrderBy) && !strOrderBy.Contains(","))
            {
                // 有asc或者desc。没有时，默认为asc
                if (strOrderBy.ToLower().EndsWith(" desc"))
                {
                    var str = strOrderBy[..^" desc".Length].Trim();
                    // 排序字段等于keyColumn
                    if (str.ToLower() == keyColumn.ToLower())
                    {
                        isAscOrder = false;
                        canMaxMin = true;
                    }
                }
                else if (strOrderBy.ToLower().EndsWith(" asc"))
                {
                    var str = strOrderBy[..^" asc".Length].Trim();
                    // 排序字段等于keyColumn
                    if (str.ToLower() == keyColumn.ToLower())
                    {
                        isAscOrder = true;
                        canMaxMin = true;
                    }
                }
                else if (!strOrderBy.Contains(" ")) // 不含空格，是唯一排序字段
                {
                    // 排序字段等于keyColumn
                    if (strOrderBy.ToLower() == keyColumn.ToLower())
                    {
                        isAscOrder = true;
                        canMaxMin = true;
                    }
                }
            }
            #endregion
        }
        else
        {
            // 取第一页也不用分页。把这代码放到这里，主要是数字分页中要自己处理这种情况
            if (startRowIndex <= 0 && maximumRows > 0)
            {
                //数字分页中，业务上一般使用降序，Entity类会给keyColumn指定降序的
                //但是，在第一页的时候，没有用到keyColumn，而数据库一般默认是升序
                //这时候就会出现第一页是升序，后面页是降序的情况了。这里改正这个BUG
                if (keyColumn.ToLower().EndsWith(" desc") || keyColumn.ToLower().EndsWith(" asc"))
                    return $"Select Top {maximumRows} * From {CheckSimpleSQL(sql)} Order By {keyColumn}";
                else
                    return $"Select Top {maximumRows} * From {CheckSimpleSQL(sql)}";
            }

            if (!keyColumn.ToLower().EndsWith(" unknown")) canMaxMin = true;

            var p = keyColumn.IndexOf(' ');
            if (p > 0) keyColumn = keyColumn[..p];
        }

        if (canMaxMin)
        {
            if (maximumRows < 1)
                sql = $"Select * From {CheckSimpleSQL(sql)} Where {keyColumn}{(isAscOrder ? ">" : "<")}(Select {(isAscOrder ? "max" : "min")}({keyColumn}) From (Select Top {startRowIndex} {keyColumn} From {CheckSimpleSQL(sql)} Order By {keyColumn} {(isAscOrder ? "Asc" : "Desc")}) XCode_Temp_a) Order By {keyColumn} {(isAscOrder ? "Asc" : "Desc")}";
            else
                sql = $"Select Top {maximumRows} * From {CheckSimpleSQL(sql)} Where {keyColumn}{(isAscOrder ? ">" : "<")}(Select {(isAscOrder ? "max" : "min")}({keyColumn}) From (Select Top {startRowIndex} {keyColumn} From {CheckSimpleSQL(sql)} Order By {keyColumn} {(isAscOrder ? "Asc" : "Desc")}) XCode_Temp_a) Order By {keyColumn} {(isAscOrder ? "Asc" : "Desc")}";
            return sql;
        }
        return null;
    }
    #endregion

    #region 数据库特性
    protected override String ReservedWordsStr => "ADD,EXCEPT,PERCENT,ALL,EXEC,PLAN,ALTER,EXECUTE,PRECISION,AND,EXISTS,PRIMARY,ANY,EXIT,PRINT,AS,FETCH,PROC,ASC,FILE,PROCEDURE,AUTHORIZATION,FILLFACTOR,PUBLIC,BACKUP,FOR,RAISERROR,BEGIN,FOREIGN,READ,BETWEEN,FREETEXT,READTEXT,BREAK,FREETEXTTABLE,RECONFIGURE,BROWSE,FROM,REFERENCES,BULK,FULL,REPLICATION,BY,FUNCTION,RESTORE,CASCADE,GOTO,RESTRICT,CASE,GRANT,RETURN,CHECK,GROUP,REVOKE,CHECKPOINT,HAVING,RIGHT,CLOSE,HOLDLOCK,ROLLBACK,CLUSTERED,IDENTITY,ROWCOUNT,COALESCE,IDENTITY_INSERT,ROWGUIDCOL,COLLATE,IDENTITYCOL,RULE,COLUMN,IF,SAVE,COMMIT,IN,SCHEMA,COMPUTE,INDEX,SELECT,CONSTRAINT,INNER,SESSION_USER,CONTAINS,INSERT,SET,CONTAINSTABLE,INTERSECT,SETUSER,CONTINUE,INTO,SHUTDOWN,CONVERT,IS,SOME,CREATE,JOIN,STATISTICS,CROSS,KEY,SYSTEM_USER,CURRENT,KILL,TABLE,CURRENT_DATE,LEFT,TEXTSIZE,CURRENT_TIME,LIKE,THEN,CURRENT_TIMESTAMP,LINENO,TO,CURRENT_USER,LOAD,TOP,CURSOR,NATIONAL ,TRAN,DATABASE,NOCHECK,TRANSACTION,DBCC,NONCLUSTERED,TRIGGER,DEALLOCATE,NOT,TRUNCATE,DECLARE,NULL,TSEQUAL,DEFAULT,NULLIF,UNION,DELETE,OF,UNIQUE,DENY,OFF,UPDATE,DESC,OFFSETS,UPDATETEXT,DISK,ON,USE,DISTINCT,OPEN,USER,DISTRIBUTED,OPENDATASOURCE,VALUES,DOUBLE,OPENQUERY,VARYING,DROP,OPENROWSET,VIEW,DUMMY,OPENXML,WAITFOR,DUMP,OPTION,WHEN,ELSE,OR,WHERE,END,ORDER,WHILE,ERRLVL,OUTER,WITH,ESCAPE,OVER,WRITETEXT,ABSOLUTE,FOUND,PRESERVE,ACTION,FREE,PRIOR,ADMIN,GENERAL,PRIVILEGES,AFTER,GET,READS,AGGREGATE,GLOBAL,REAL,ALIAS,GO,RECURSIVE,ALLOCATE,GROUPING,REF,ARE,HOST,REFERENCING,ARRAY,HOUR,RELATIVE,ASSERTION,IGNORE,RESULT,AT,IMMEDIATE,RETURNS,BEFORE,INDICATOR,ROLE,BINARY,INITIALIZE,ROLLUP,BIT,INITIALLY,ROUTINE,BLOB,INOUT,ROW,BOOLEAN,INPUT,ROWS,BOTH,INT,SAVEPOINT,BREADTH,INTEGER,SCROLL,CALL,INTERVAL,SCOPE,CASCADED,ISOLATION,SEARCH,CAST,ITERATE,SECOND,CATALOG,LANGUAGE,SECTION,CHAR,LARGE,SEQUENCE,CHARACTER,LAST,SESSION,CLASS,LATERAL,SETS,CLOB,LEADING,SIZE,COLLATION,LESS,SMALLINT,COMPLETION,LEVEL,SPACE,CONNECT,LIMIT,SPECIFIC,CONNECTION,LOCAL,SPECIFICTYPE,CONSTRAINTS,LOCALTIME,SQL,CONSTRUCTOR,LOCALTIMESTAMP,SQLEXCEPTION,CORRESPONDING,LOCATOR,SQLSTATE,CUBE,MAP,SQLWARNING,CURRENT_PATH,MATCH,START,CURRENT_ROLE,MINUTE,STATE,CYCLE,MODIFIES,STATEMENT,DATA,MODIFY,STATIC,DATE,MODULE,STRUCTURE,DAY,MONTH,TEMPORARY,DEC,NAMES,TERMINATE,DECIMAL,NATURAL,THAN,DEFERRABLE,NCHAR,TIME,DEFERRED,NCLOB,TIMESTAMP,DEPTH,NEW,TIMEZONE_HOUR,DEREF,NEXT,TIMEZONE_MINUTE,DESCRIBE,NO,TRAILING,DESCRIPTOR,NONE,TRANSLATION,DESTROY,NUMERIC,TREAT,DESTRUCTOR,OBJECT,TRUE,DETERMINISTIC,OLD,UNDER,DICTIONARY,ONLY,UNKNOWN,DIAGNOSTICS,OPERATION,UNNEST,DISCONNECT,ORDINALITY,USAGE,DOMAIN,OUT,USING,DYNAMIC,OUTPUT,VALUE,EACH,PAD,VARCHAR,END-EXEC,PARAMETER,VARIABLE,EQUALS,PARAMETERS,WHENEVER,EVERY,PARTIAL,WITHOUT,EXCEPTION,PATH,WORK,EXTERNAL,POSTFIX,WRITE,FALSE,PREFIX,YEAR,FIRST,PREORDER,ZONE,FLOAT,PREPARE,ADA,AVG,BIT_LENGTH,CHAR_LENGTH,CHARACTER_LENGTH,COUNT,EXTRACT,FORTRAN,INCLUDE,INSENSITIVE,LOWER,MAX,MIN,OCTET_LENGTH,OVERLAPS,PASCAL,POSITION,SQLCA,SQLCODE,SQLERROR,SUBSTRING,SUM,TRANSLATE,TRIM,UPPER," +
              "Sort,Level,User,Online";

    /// <summary>长文本长度</summary>
    public override Int32 LongTextLength => 4000;

    /// <summary>格式化时间为SQL字符串</summary>
    /// <param name="dateTime">时间值</param>
    /// <returns></returns>
    public override String FormatDateTime(DateTime dateTime) => "{ts'" + dateTime.ToFullString() + "'}";

    /// <summary>格式化名称，如果是关键字，则格式化后返回，否则原样返回</summary>
    /// <param name="name">名称</param>
    /// <returns></returns>
    public override String FormatName(String name)
    {
        if (name.IsNullOrEmpty()) return name;

        // SqlServer数据库名和表名可以用横线。。。
        if (name.Contains("-")) return $"[{name}]";

        return base.FormatName(name);
    }

    /// <summary>格式化关键字</summary>
    /// <param name="keyWord">关键字</param>
    /// <returns></returns>
    public override String FormatKeyWord(String keyWord)
    {
        //if (String.IsNullOrEmpty(keyWord)) throw new ArgumentNullException("keyWord");
        if (String.IsNullOrEmpty(keyWord)) return keyWord;

        if (keyWord.StartsWith("[") && keyWord.EndsWith("]")) return keyWord;

        return $"[{keyWord}]";
    }

    /// <summary>系统数据库名</summary>
    public override String SystemDatabaseName => "master";

    public override String FormatValue(IDataColumn field, Object value)
    {
        var isNullable = true;
        Type type = null;
        if (field != null)
        {
            type = field.DataType;
            isNullable = field.Nullable;
        }
        else if (value != null)
            type = value.GetType();

        if (type == typeof(String))
        {
            // 热心网友 Hannibal 在处理日文网站时发现插入的日文为乱码，这里加上N前缀
            if (value == null) return isNullable ? "null" : "''";

            // 为了兼容旧版本实体类
            if (field.RawType.StartsWithIgnoreCase("n"))
                return "N'" + value.ToString().Replace("'", "''") + "'";
            else
                return "'" + value.ToString().Replace("'", "''") + "'";
        }
        else if (type == typeof(DateTime))
        {
            if (value == null) return isNullable ? "null" : "''";
            var dt = Convert.ToDateTime(value);

            if (dt <= DateTime.MinValue || dt >= DateTime.MaxValue) return isNullable ? "null" : "''";

            if (isNullable && (dt <= DateTime.MinValue || dt >= DateTime.MaxValue)) return "null";

            return FormatDateTime(dt);
        }

        return base.FormatValue(field, value);
    }

    private static readonly Char[] _likeKeys = new[] { '[', ']', '%', '_' };
    /// <summary>格式化模糊搜索的字符串。处理转义字符</summary>
    /// <param name="column">字段</param>
    /// <param name="format">格式化字符串</param>
    /// <param name="value">数值</param>
    /// <returns></returns>
    public override String FormatLike(IDataColumn column, String format, String value)
    {
        if (value.IsNullOrEmpty()) return value;
        // fix 2022.11.17
        // Like 构建SQL语句不参转义   
        //if (value.IndexOfAny(_likeKeys) >= 0)
        //    value = value
        //        .Replace("[", "[[]")
        //        .Replace("]", "[]]")
        //        .Replace("%", "[%]")
        //        .Replace("_", "[_]");
        // fix 2023.03.22
        // LIKE 构建SQL语句 中 [ ] % 会循环转义 ,只转_比较合适
        if (value.IndexOfAny(_likeKeys) >= 0)
              value = value
                   .Replace("_", "[_]");
        return base.FormatLike(column, format, value);
    }
    #endregion
}

/// <summary>SqlServer数据库</summary>
internal class SqlServerSession : RemoteDbSession
{
    #region 构造函数
    public SqlServerSession(IDatabase db) : base(db) { }
    #endregion

    #region 查询
    protected override DbTable OnFill(DbDataReader dr)
    {
        var dt = new DbTable();
        dt.ReadHeader(dr);
        dt.ReadData(dr, GetFields(dt, dr));

        return dt;
    }

    private Int32[] GetFields(DbTable dt, DbDataReader dr)
    {
        // 干掉rowNumber
        var idx = Array.FindIndex(dt.Columns, c => c.EqualIgnoreCase("rowNumber"));
        if (idx >= 0)
        {
            var cs = dt.Columns.ToList();
            var ts = dt.Types.ToList();
            var fs = Enumerable.Range(0, cs.Count).ToList();

            cs.RemoveAt(idx);
            ts.RemoveAt(idx);
            fs.RemoveAt(idx);

            dt.Columns = cs.ToArray();
            dt.Types = ts.ToArray();
            return fs.ToArray();
        }

        return null;
    }

    /// <summary>快速查询单表记录数，稍有偏差</summary>
    /// <param name="tableName"></param>
    /// <returns></returns>
    public override Int64 QueryCountFast(String tableName)
    {
        tableName = tableName.Trim().Trim('[', ']').Trim();

        var sql = $"select rows from sysindexes where id = object_id('{tableName}') and indid in (0,1)";
        return ExecuteScalar<Int64>(sql);
    }

    /// <summary>执行插入语句并返回新增行的自动编号</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[] ps)
    {
        sql = "SET NOCOUNT ON;" + sql + ";Select SCOPE_IDENTITY()";
        return base.InsertAndGetIdentity(sql, type, ps);
    }

    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[] ps)
    {
        sql = "SET NOCOUNT ON;" + sql + ";Select SCOPE_IDENTITY()";
        return base.InsertAndGetIdentityAsync(sql, type, ps);
    }
    #endregion

    #region 批量操作
    public override Int32 Insert(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list)
    {
        var ps = new HashSet<String>();
        var sql = GetInsertSql(table, columns, ps);
        var dpsList = GetParametersList(columns, ps, list);

        return BatchExecute(sql, dpsList);
    }

    private String GetInsertSql(IDataTable table, IDataColumn[] columns, ICollection<String> ps)
    {
        var sb = Pool.StringBuilder.Get();
        var db = Database as DbBase;

        // 字段列表
        sb.AppendFormat("Insert Into {0}(", db.FormatName(table));
        foreach (var dc in columns)
        {
            if (dc.Identity) continue;

            sb.Append(db.FormatName(dc));
            sb.Append(',');
        }
        sb.Length--;
        sb.Append(')');

        // 值列表
        sb.Append(" Values(");
        foreach (var dc in columns)
        {
            if (dc.Identity) continue;

            sb.Append(db.FormatParameterName(dc.Name));
            sb.Append(',');

            if (!ps.Contains(dc.Name)) ps.Add(dc.Name);
        }
        sb.Length--;
        sb.Append(')');

        return sb.Put(true);
    }

    public override Int32 Upsert(IDataTable table, IDataColumn[] columns, ICollection<String> updateColumns, ICollection<String> addColumns, IEnumerable<IModel> list)
    {
        var ps = new HashSet<String>();
        var insert = GetInsertSql(table, columns, ps);
        var update = GetUpdateSql(table, columns, updateColumns, addColumns, ps);

        // 先更新，根据更新结果影响的条目数判断是否需要插入
        var sb = Pool.StringBuilder.Get();
        sb.Append(update);
        sb.AppendLine(";");
        sb.AppendLine("IF(@@ROWCOUNT = 0)");
        sb.AppendLine("BEGIN");
        sb.Append(insert);
        sb.AppendLine(";");
        sb.AppendLine("END;");
        var sql = sb.Put(true);

        var dpsList = GetParametersList(columns, ps, list, true);
        return BatchExecute(sql, dpsList);
    }

    private String GetUpdateSql(IDataTable table, IDataColumn[] columns, ICollection<String> updateColumns, ICollection<String> addColumns, ICollection<String> ps)
    {
        var sb = Pool.StringBuilder.Get();
        var db = Database as DbBase;

        // 字段列表
        sb.AppendFormat("Update {0} Set ", db.FormatName(table));
        foreach (var dc in columns)
        {
            if (dc.Identity || dc.PrimaryKey) continue;

            // 修复当columns看存在updateColumns不存在列时构造出来的Sql语句会出现连续逗号的问题
            if (updateColumns != null && updateColumns.Contains(dc.Name) && (addColumns == null || !addColumns.Contains(dc.Name)))
            {
                sb.AppendFormat("{0}={1},", db.FormatName(dc), db.FormatParameterName(dc.Name));

                if (!ps.Contains(dc.Name)) ps.Add(dc.Name);
            }
            else if (addColumns != null && addColumns.Contains(dc.Name))
            {
                sb.AppendFormat("{0}={0}+{1},", db.FormatName(dc), db.FormatParameterName(dc.Name));

                if (!ps.Contains(dc.Name)) ps.Add(dc.Name);
            }
            //sb.Append(",");
        }
        sb.Length--;
        //sb.Append(")");

        // 条件
        var pks = columns.Where(e => e.PrimaryKey).ToArray();
        if (pks == null || pks.Length == 0) throw new InvalidOperationException("未指定用于更新的主键");

        sb.Append(" Where ");
        foreach (var dc in columns)
        {
            if (!dc.PrimaryKey) continue;

            sb.AppendFormat("{0}={1}", db.FormatName(dc), db.FormatParameterName(dc.Name));
            sb.Append(" And ");

            if (!ps.Contains(dc.Name)) ps.Add(dc.Name);
        }
        sb.Length -= " And ".Length;

        return sb.Put(true);
    }
    #endregion

    #region 修复实现SqlServer批量操作增添方法
    private Int32 BatchExecute(String sql, List<IDataParameter[]> psList)
    {
        return Process(conn =>
        {
            ////获取连接对象
            //var conn = Database.Pool.Get();

            // 准备
            var mBatcher = new SqlBatcher(Database.Factory);
            mBatcher.StartBatch(conn);

            // 创建并添加Command
            foreach (var dps in psList)
            {
                if (dps != null)
                {
                    var cmd = OnCreateCommand(sql, CommandType.Text, dps);
                    mBatcher.AddToBatch(cmd);
                }
            }

            // 执行批量操作
            try
            {
                BeginTrace();
                var ret = mBatcher.ExecuteBatch();
                mBatcher.EndBatch();
                return ret;
            }
            catch (DbException ex)
            {
                throw OnException(ex);
            }
            finally
            {
                //if (conn != null) Database.Pool.Put(conn);
                EndTrace(OnCreateCommand(sql, CommandType.Text));
            }
        });
    }

    private List<IDataParameter[]> GetParametersList(IDataColumn[] columns, ICollection<String> ps, IEnumerable<IModel> list, Boolean isInsertOrUpdate = false)
    {
        var db = Database;
        var dpsList = new List<IDataParameter[]>();

        foreach (var entity in list)
        {
            var dps = new List<IDataParameter>();
            foreach (var dc in columns)
            {
                if (isInsertOrUpdate)
                {
                    if (dc.Identity || dc.PrimaryKey)
                    {
                        //更新时添加主键做为查询条件
                        dps.Add(db.CreateParameter(dc.Name, entity[dc.Name], dc));
                        continue;
                    }
                }
                else
                {
                    if (dc.Identity) continue;
                }
                if (!ps.Contains(dc.Name)) continue;

                // 用于参数化的字符串不能为null
                var val = entity[dc.Name];
                if (dc.DataType == typeof(String))
                    val += "";
                else if (dc.DataType == typeof(DateTime))
                {
                    var dt = val.ToDateTime();
                    if (dt.Year < 1970) val = new DateTime(1970, 1, 1);
                }

                // 逐列创建参数对象
                dps.Add(db.CreateParameter(dc.Name, val, dc));
            }

            dpsList.Add(dps.ToArray());
        }

        return dpsList;
    }

    /// <summary>
    /// 批量操作帮助类
    /// </summary>
    private class SqlBatcher
    {
        private DataAdapter mAdapter;
        private readonly DbProviderFactory _factory;

        /// <summary>获得批处理是否正在批处理状态。</summary>
        public Boolean IsStarted { get; private set; }

        static MethodInfo _init;
        static MethodInfo _add;
        static MethodInfo _exe;
        static MethodInfo _clear;
        Func<IDbCommand, Int32> _addToBatch;
        Func<Int32> _executeBatch;
        Action _clearBatch;

        public SqlBatcher(DbProviderFactory factory)
        {
            _factory = factory;

            if (_init == null)
            {
                using var adapter = factory.CreateDataAdapter();
                var type = adapter.GetType();

                _add = type.GetMethodEx("AddToBatch");
                _exe = type.GetMethodEx("ExecuteBatch");
                _clear = type.GetMethodEx("ClearBatch");
                _init = type.GetMethodEx("InitializeBatching");
            }
        }

        /// <summary>开始批处理</summary>
        /// <param name="connection">连接。</param>
        public void StartBatch(DbConnection connection)
        {
            if (IsStarted) return;

            var cmd = _factory.CreateCommand();
            cmd.Connection = connection;

            var adapter = _factory.CreateDataAdapter();
            adapter.InsertCommand = cmd;
            //adapter.Invoke("InitializeBatching");
            _init.As<Action>(adapter)();

            _addToBatch = _add.As<Func<IDbCommand, Int32>>(adapter);
            _executeBatch = _exe.As<Func<Int32>>(adapter);
            _clearBatch = _clear.As<Action>(adapter);

            mAdapter = adapter;

            IsStarted = true;
        }

        /// <summary>
        /// 添加批命令。
        /// </summary>
        /// <param name="command">命令</param>
        public void AddToBatch(IDbCommand command)
        {
            if (!IsStarted) throw new InvalidOperationException();

            //mAdapter.Invoke("AddToBatch", new Object[] { command });
            _addToBatch(command);
        }

        /// <summary>
        /// 执行批处理。
        /// </summary>
        /// <returns>影响的数据行数。</returns>
        public Int32 ExecuteBatch()
        {
            if (!IsStarted) throw new InvalidOperationException();

            //return (Int32)mAdapter.Invoke("ExecuteBatch");
            return _executeBatch();
        }

        /// <summary>
        /// 结束批处理。
        /// </summary>
        public void EndBatch()
        {
            if (IsStarted)
            {
                ClearBatch();
                mAdapter.Dispose();
                mAdapter = null;
                IsStarted = false;
            }
        }

        /// <summary>
        /// 清空保存的批命令。
        /// </summary>
        public void ClearBatch()
        {
            if (!IsStarted) throw new InvalidOperationException();

            //mAdapter.Invoke("ClearBatch");
            _clearBatch();
        }
    }
    #endregion
}

/// <summary>SqlServer元数据</summary>
internal class SqlServerMetaData : RemoteDbMetaData
{
    public SqlServerMetaData() => Types = _DataTypes;

    #region 属性
    ///// <summary>是否SQL2005</summary>
    //public Boolean IsSQL2005 { get { return (Database as SqlServer).IsSQL2005; } }

    public Version Version => (Database as SqlServer).Version;

    ///// <summary>0级类型</summary>
    //public String Level0type { get { return IsSQL2005 ? "SCHEMA" : "USER"; } }
    #endregion

    #region 构架
    /// <summary>取得所有表构架</summary>
    /// <returns></returns>
    protected override List<IDataTable> OnGetTables(String[] names)
    {
        #region 查表说明、字段信息、索引信息
        var session = Database.CreateSession();

        //一次性把所有的表说明查出来
        DataTable DescriptionTable = null;

        try
        {
            var sql = "select b.name n, a.value v from sys.extended_properties a inner join sysobjects b on a.major_id=b.id and a.minor_id=0 and a.name = 'MS_Description'";
            DescriptionTable = session.Query(sql).Tables[0];
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        var dt = GetSchema(_.Tables, null);
        if (dt == null || dt.Rows == null || dt.Rows.Count <= 0) return null;

        try
        {
            AllFields = session.Query(SchemaSql).Tables[0];
            AllIndexes = session.Query(IndexSql).Tables[0];
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
        #endregion

        // 列出用户表
        var rows = dt.Select($"(TABLE_TYPE='BASE TABLE' Or TABLE_TYPE='VIEW') AND TABLE_NAME<>'Sysdiagrams'");
        if (rows == null || rows.Length <= 0) return null;

        var list = GetTables(rows, names);
        if (list == null || list.Count <= 0) return list;

        // 修正备注
        foreach (var item in list)
        {
            var drs = DescriptionTable?.Select("n='" + item.TableName + "'");
            item.Description = drs == null || drs.Length <= 0 ? "" : drs[0][1].ToString();
        }

        return list;
    }

    /// <summary>
    /// 快速取得所有表名
    /// </summary>
    /// <returns></returns>
    public override IList<String> GetTableNames()
    {
        var list = new List<String>();

        var dt = GetSchema(_.Tables, null);
        if (dt?.Rows == null || dt.Rows.Count <= 0) return list;

        // 默认列出所有字段
        var rows = dt.Select($"(TABLE_TYPE='BASE TABLE' Or TABLE_TYPE='VIEW') AND TABLE_NAME<>'Sysdiagrams'");
        foreach (var dr in rows)
        {
            list.Add(GetDataRowValue<String>(dr, _.TalbeName));
        }

        return list;
    }

    private DataTable AllFields = null;
    private DataTable AllIndexes = null;

    protected override void FixField(IDataColumn field, DataRow dr)
    {
        base.FixField(field, dr);

        var rows = AllFields?.Select("表名='" + field.Table.TableName + "' And 字段名='" + field.ColumnName + "'", null);
        if (rows != null && rows.Length > 0)
        {
            var dr2 = rows[0];

            field.Identity = GetDataRowValue<Boolean>(dr2, "标识");
            field.PrimaryKey = GetDataRowValue<Boolean>(dr2, "主键");
            //field.NumOfByte = GetDataRowValue<Int32>(dr2, "占用字节数");
            field.Description = GetDataRowValue<String>(dr2, "字段说明");
            field.Precision = GetDataRowValue<Int32>(dr2, "精度");
            field.Scale = GetDataRowValue<Int32>(dr2, "小数位数");
        }
    }

    protected override List<IDataIndex> GetIndexes(IDataTable table, DataTable _indexes, DataTable _indexColumns)
    {
        var list = base.GetIndexes(table, _indexes, _indexColumns);
        if (list != null && list.Count > 0)
        {
            foreach (var item in list)
            {
                var drs = AllIndexes?.Select("name='" + item.Name + "'");
                if (drs != null && drs.Length > 0)
                {
                    item.Unique = GetDataRowValue<Boolean>(drs[0], "is_unique");
                    item.PrimaryKey = GetDataRowValue<Boolean>(drs[0], "is_primary_key");
                }
            }
        }
        return list;
    }

    public override String CreateTableSQL(IDataTable table)
    {
        var sql = base.CreateTableSQL(table);

        var pks = table.PrimaryKeys;
        if (String.IsNullOrEmpty(sql) || pks == null || pks.Length < 2) return sql;

        // 处理多主键
        sql += "; " + Environment.NewLine;
        sql += $"Alter Table {FormatName(table)} Add Constraint PK_{table.TableName} Primary Key Clustered({pks.Join(",", FormatName)})";
        return sql;
    }

    public override String FieldClause(IDataColumn field, Boolean onlyDefine)
    {
        if (!String.IsNullOrEmpty(field.RawType) && field.RawType.Contains("char(-1)"))
        {
            //if (IsSQL2005)
            field.RawType = field.RawType.Replace("char(-1)", "char(MAX)");
            //else
            //    field.RawType = field.RawType.Replace("char(-1)", "char(" + (Int32.MaxValue / 2) + ")");
        }

        //chenqi 2017-3-28
        //增加处理decimal类型精度和小数位数处理
        //此处只针对Sql server进行处理
        //严格来说，应该修改的地方是
        if (!field.RawType.IsNullOrEmpty() && field.RawType.StartsWithIgnoreCase("decimal"))
        {
            field.RawType = $"decimal({field.Precision},{field.Scale})";
        }

        return base.FieldClause(field, onlyDefine);
    }

    protected override String GetFieldConstraints(IDataColumn field, Boolean onlyDefine)
    {
        // 非定义时（修改字段），主键字段没有约束
        if (!onlyDefine && field.PrimaryKey) return null;

        var str = base.GetFieldConstraints(field, onlyDefine);

        // 非定义时，自增字段没有约束
        if (onlyDefine && field.Identity) str = " IDENTITY(1,1)" + str;

        return str;
    }

    //protected override String GetFormatParam(IDataColumn field, DataRow dr)
    //{
    //    var str = base.GetFormatParam(field, dr);
    //    if (String.IsNullOrEmpty(str)) return str;

    //    // 这个主要来自于float，因为无法取得其精度
    //    if (str == "(0)") return null;
    //    return str;
    //}

    //protected override String GetFormatParamItem(IDataColumn field, DataRow dr, String item)
    //{
    //    var pi = base.GetFormatParamItem(field, dr, item);
    //    if (field.DataType == typeof(String) && pi == "-1" && IsSQL2005) return "MAX";
    //    return pi;
    //}
    #endregion

    #region 取得字段信息的SQL模版
    private String _SchemaSql = "";
    /// <summary>构架SQL</summary>
    public virtual String SchemaSql
    {
        get
        {
            if (String.IsNullOrEmpty(_SchemaSql))
            {
                var sb = new StringBuilder();
                sb.Append("SELECT ");
                sb.Append("表名=d.name,");
                sb.Append("字段序号=a.colorder,");
                sb.Append("字段名=a.name,");
                sb.Append("标识=case when COLUMNPROPERTY( a.id,a.name,'IsIdentity')=1 then Convert(Bit,1) else Convert(Bit,0) end,");
                sb.Append("主键=case when exists(SELECT 1 FROM sysobjects where xtype='PK' and name in (");
                sb.Append("SELECT name FROM sysindexes WHERE id = a.id AND indid in(");
                sb.Append("SELECT indid FROM sysindexkeys WHERE id = a.id AND colid=a.colid");
                sb.Append("))) then Convert(Bit,1) else Convert(Bit,0) end,");
                sb.Append("类型=b.name,");
                sb.Append("占用字节数=a.length,");
                sb.Append("长度=COLUMNPROPERTY(a.id,a.name,'PRECISION'),");
                sb.Append("小数位数=isnull(COLUMNPROPERTY(a.id,a.name,'Scale'),0),");
                sb.Append("允许空=case when a.isnullable=1 then Convert(Bit,1)else Convert(Bit,0) end,");
                sb.Append("默认值=isnull(e.text,''),");
                sb.Append("字段说明=isnull(g.[value],'')");
                sb.Append("FROM syscolumns a ");
                sb.Append("left join systypes b on a.xtype=b.xusertype ");
                sb.Append("inner join sysobjects d on a.id=d.id  and d.xtype='U' ");
                sb.Append("left join syscomments e on a.cdefault=e.id ");
                //if (IsSQL2005)
                //{
                sb.Append("left join sys.extended_properties g on a.id=g.major_id and a.colid=g.minor_id and g.name = 'MS_Description'  ");
                //}
                //else
                //{
                //    sb.Append("left join sysproperties g on a.id=g.id and a.colid=g.smallid  ");
                //}
                sb.Append("order by a.id,a.colorder");
                _SchemaSql = sb.ToString();
            }
            return _SchemaSql;
        }
    }

    private String _IndexSql;
    public virtual String IndexSql
    {
        get
        {
            if (_IndexSql == null)
            {
                //if (IsSQL2005)
                _IndexSql = "select ind.* from sys.indexes ind inner join sys.objects obj on ind.object_id = obj.object_id where obj.type='u'";
                //else
                //    _IndexSql = "select IndexProperty(obj.id, ind.name,'IsUnique') as is_unique, ObjectProperty(object_id(ind.name),'IsPrimaryKey') as is_primary_key,ind.* from sysindexes ind inner join sysobjects obj on ind.id = obj.id where obj.type='u'";
            }
            return _IndexSql;
        }
    }

    //private readonly String _DescriptionSql2000 = "select b.name n, a.value v from sysproperties a inner join sysobjects b on a.id=b.id where a.smallid=0";
    //private readonly String _DescriptionSql2005 = "select b.name n, a.value v from sys.extended_properties a inner join sysobjects b on a.major_id=b.id and a.minor_id=0 and a.name = 'MS_Description'";
    ///// <summary>取表说明SQL</summary>
    //public virtual String DescriptionSql { get { return IsSQL2005 ? _DescriptionSql2005 : _DescriptionSql2000; } }
    #endregion

    #region 数据定义
    public override Object SetSchema(DDLSchema schema, params Object[] values)
    {
        {
            var db = Database as DbBase;
            var tracer = db.Tracer;
            if (schema is not DDLSchema.BackupDatabase and not DDLSchema.RestoreDatabase) tracer = null;
            using var span = tracer?.NewSpan($"db:{db.ConnName}:SetSchema:{schema}", values);

            var dbname = "";
            var file = "";
            var recoverDir = "";
            switch (schema)
            {
                case DDLSchema.BackupDatabase:
                    if (values != null)
                    {
                        if (values.Length > 0)
                            dbname = values[0] as String;
                        if (values.Length > 1)
                            file = values[1] as String;
                    }
                    return Backup(dbname, file, false);
                case DDLSchema.RestoreDatabase:
                    if (values != null)
                    {
                        if (values.Length > 0)
                            file = values[0] as String;
                        if (values.Length > 1)
                            recoverDir = values[1] as String;
                    }
                    return Restore(file, recoverDir, true);
                default:
                    break;
            }
        }
        return base.SetSchema(schema, values);
    }

    public override String CreateDatabaseSQL(String dbname, String file)
    {
        var dp = (Database as SqlServer).DataPath;

        if (String.IsNullOrEmpty(file))
        {
            if (String.IsNullOrEmpty(dp)) return $"CREATE DATABASE {Database.FormatName(dbname)}";

            file = dbname + ".mdf";
        }

        if (!Path.IsPathRooted(file))
        {
            if (!String.IsNullOrEmpty(dp)) file = Path.Combine(dp, file);

            if (!Path.IsPathRooted(file)) file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
        }
        if (String.IsNullOrEmpty(Path.GetExtension(file))) file += ".mdf";
        file = new FileInfo(file).FullName;

        var logfile = Path.ChangeExtension(file, ".ldf");
        logfile = new FileInfo(logfile).FullName;

        var dir = Path.GetDirectoryName(file);
        if (!String.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var sb = new StringBuilder();

        sb.AppendFormat("CREATE DATABASE {0} ON  PRIMARY", Database.FormatName(dbname));
        sb.AppendLine();
        sb.AppendFormat(@"( NAME = N'{0}', FILENAME = N'{1}', SIZE = 10 , MAXSIZE = UNLIMITED, FILEGROWTH = 10%)", dbname, file);
        sb.AppendLine();
        sb.Append("LOG ON ");
        sb.AppendLine();
        sb.AppendFormat(@"( NAME = N'{0}_Log', FILENAME = N'{1}', SIZE = 10 , MAXSIZE = UNLIMITED, FILEGROWTH = 10%)", dbname, logfile);
        sb.AppendLine();

        return sb.ToString();
    }

    public override String DatabaseExistSQL(String dbname) => $"SELECT * FROM sysdatabases WHERE name = N'{dbname}'";

    /// <summary>使用数据架构确定数据库是否存在，因为使用系统视图可能没有权限</summary>
    /// <param name="dbname"></param>
    /// <returns></returns>
    protected override Boolean DatabaseExist(String dbname)
    {
        var dt = GetSchema(_.Databases, new String[] { dbname });
        return dt != null && dt.Rows != null && dt.Rows.Count > 0;
    }

    //protected override Boolean DropDatabase(String databaseName)
    //{
    //    //return base.DropDatabase(databaseName);

    //    // SQL语句片段，断开该数据库所有链接
    //    var sb = new StringBuilder();
    //    sb.AppendLine("use master");
    //    sb.AppendLine(";");
    //    sb.AppendLine("declare   @spid   varchar(20),@dbname   varchar(20)");
    //    sb.AppendLine("declare   #spid   cursor   for");
    //    sb.AppendFormat("select   spid=cast(spid   as   varchar(20))   from   master..sysprocesses   where   dbid=db_id('{0}')", databaseName);
    //    sb.AppendLine();
    //    sb.AppendLine("open   #spid");
    //    sb.AppendLine("fetch   next   from   #spid   into   @spid");
    //    sb.AppendLine("while   @@fetch_status=0");
    //    sb.AppendLine("begin");
    //    sb.AppendLine("exec('kill   '+@spid)");
    //    sb.AppendLine("fetch   next   from   #spid   into   @spid");
    //    sb.AppendLine("end");
    //    sb.AppendLine("close   #spid");
    //    sb.AppendLine("deallocate   #spid");

    //    var count = 0;
    //    var session = Database.CreateSession();
    //    try
    //    {
    //        count = session.Execute(sb.ToString());
    //    }
    //    catch (Exception ex) { XTrace.WriteException(ex); }
    //    return session.Execute(String.Format("Drop Database {0}", FormatName(databaseName))) > 0;
    //}

    /// <summary>备份文件到目标文件</summary>
    /// <param name="dbname"></param>
    /// <param name="bakfile"></param>
    /// <param name="compressed"></param>
    public override String Backup(String dbname, String bakfile, Boolean compressed)
    {

        var name = dbname;
        if (name.IsNullOrEmpty())
        {
            name = Database.DatabaseName;
        }

        var bf = bakfile;
        if (bf.IsNullOrEmpty())
        {
            var ext = Path.GetExtension(bakfile);
            if (ext.IsNullOrEmpty()) ext = ".bak";

            if (compressed)
                bf = $"{name}{ext}";
            else
                bf = $"{name}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
        }
        if (!Path.IsPathRooted(bf)) bf = NewLife.Setting.Current.BackupPath.CombinePath(bf).GetBasePath();

        bf = bf.EnsureDirectory(true);

        WriteLog("{0}备份SqlServer数据库 {1} 到 {2}", Database.ConnName, name, bf);

        var sw = Stopwatch.StartNew();

        // 删除已有文件
        if (File.Exists(bf)) File.Delete(bf);

        base.Database.CreateSession().Execute($"USE master;BACKUP DATABASE {name} TO disk = '{bf}'");

        // 压缩
        WriteLog("备份文件大小：{0:n0}字节", bf.AsFile().Length);
        if (compressed)
        {
            var zipfile = Path.ChangeExtension(bf, "zip");
            if (bakfile.IsNullOrEmpty()) zipfile = zipfile.TrimEnd(".zip") + $"_{DateTime.Now:yyyyMMddHHmmss}.zip";

            var fi = bf.AsFile();
            fi.Compress(zipfile);
            WriteLog("压缩后大小：{0:n0}字节，{1}", zipfile.AsFile().Length, zipfile);

            // 删除未备份
            File.Delete(bf);

            bf = zipfile;
        }

        sw.Stop();
        WriteLog("备份完成，耗时{0:n0}ms", sw.ElapsedMilliseconds);

        return bf;
    }

    /// <summary>还原备份文件到目标数据库</summary>
    /// <param name="bakfile"></param>
    /// <param name="recoverDir"></param>
    /// <param name="replace"></param>
    /// <param name="compressed"></param>
    public String Restore(String bakfile, String recoverDir, Boolean replace = true, Boolean compressed = false)
    {
        var session = base.Database.CreateSession();
        var result = "";
        if (compressed)
        {
            return result;
        }
        if (bakfile.IsNullOrEmpty())
        {
            return result;
        }

        if (recoverDir.IsNullOrEmpty())
        {
            var sql = "select filename from sysfiles";
            var dt = session.Query(sql).Tables[0];
            if (dt.Rows.Count < 1)
            {
                return result;
            }
            else
            {
                recoverDir = Path.GetDirectoryName(dt.Rows[0][0].ToString());
            }
        }


        WriteLog("{0}还原SqlServer数据库 备份文件为{1}", Database.ConnName, bakfile);

        var sw = Stopwatch.StartNew();


        var headInfo = session.Query($"RESTORE HEADERONLY FROM DISK = '{bakfile}'").Tables[0];
        var fileInfo = session.Query($"RESTORE FILELISTONLY from disk= N'{bakfile}'").Tables[0];
        if (headInfo.Rows.Count < 1)
        {
            return result;
        }
        else
        {
            var databaseName = headInfo.Rows[0]["DatabaseName"];
            var dataName = fileInfo.Rows[0]["LogicalName"];
            var logName = fileInfo.Rows[1]["LogicalName"];
            var stopConnect = $"ALTER DATABASE {databaseName} SET OFFLINE WITH ROLLBACK IMMEDIATE";
            var restorSql = $@"RESTORE DATABASE {databaseName} from disk= N'{bakfile}' 
                WITH NOUNLOAD,
                {(replace ? "REPLACE," : "")}
                    MOVE '{dataName}' TO '{Path.Combine(recoverDir, String.Concat(databaseName, ".mdf"))}',
                    MOVE '{logName}' TO '{Path.Combine(recoverDir, String.Concat(databaseName, ".ldf"))}';";
            session.Execute(stopConnect);
            session.Execute(restorSql);
            result = "ok";
        }

        sw.Stop();
        WriteLog("还原完成，耗时{0:n0}ms", sw.ElapsedMilliseconds);

        return result;
    }

    //public override String TableExistSQL(IDataTable table) => $"select * from sysobjects where xtype='U' and name='{table.TableName}'";

    /// <summary>使用数据架构确定数据表是否存在，因为使用系统视图可能没有权限</summary>
    /// <param name="table"></param>
    /// <returns></returns>
    public Boolean TableExist(IDataTable table)
    {
        var dt = GetSchema(_.Tables, new String[] { null, null, table.TableName, null });
        return dt != null && dt.Rows != null && dt.Rows.Count > 0;
    }

    protected override String RenameTable(String tableName, String tempTableName)
    {
        if (Version.Major >= 8)
            return $"EXECUTE sp_rename N'{tableName}', N'{tempTableName}', 'OBJECT' ";
        else
            return base.RenameTable(tableName, tempTableName);
    }

    protected override String RebuildTable(IDataTable entitytable, IDataTable dbtable)
    {
        var sql = base.RebuildTable(entitytable, dbtable);
        if (String.IsNullOrEmpty(sql)) return sql;

        // 特殊处理带标识列的表，需要增加SET IDENTITY_INSERT
        if (!entitytable.Columns.Any(e => e.Identity)) return sql;

        var tableName = Database.FormatName(entitytable);
        var ss = sql.Split("; " + Environment.NewLine);
        for (var i = 0; i < ss.Length; i++)
        {
            if (ss[i].StartsWithIgnoreCase("Insert Into"))
            {
                ss[i] = $"SET IDENTITY_INSERT {tableName} ON;{ss[i]};SET IDENTITY_INSERT {tableName} OFF";
                break;
            }
        }
        return String.Join("; " + Environment.NewLine, ss);
    }

    public override String AddTableDescriptionSQL(IDataTable table) => $"EXEC dbo.sp_addextendedproperty @name=N'MS_Description', @value=N'{table.Description}' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'{table.TableName}'";

    public override String DropTableDescriptionSQL(IDataTable table) => $"EXEC dbo.sp_dropextendedproperty @name=N'MS_Description', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'{table.TableName}'";

    public override String AddColumnSQL(IDataColumn field) => $"Alter Table {FormatName(field.Table)} Add {FieldClause(field, true)}";

    public override String AlterColumnSQL(IDataColumn field, IDataColumn oldfield)
    {
        // 创建为自增，重建表
        if (field.Identity && !oldfield.Identity)
        {
            //return DropColumnSQL(oldfield) + ";" + Environment.NewLine + AddColumnSQL(field);
            return RebuildTable(field.Table, oldfield.Table);
        }
        // 类型改变，必须重建表
        if (IsColumnTypeChanged(field, oldfield)) return RebuildTable(field.Table, oldfield.Table);

        var sql = $"Alter Table {FormatName(field.Table)} Alter Column {FieldClause(field, false)}";
        var pk = DeletePrimaryKeySQL(field);
        if (field.PrimaryKey)
        {
            // 如果没有主键删除脚本，表明没有主键
            //if (String.IsNullOrEmpty(pk))
            if (!oldfield.PrimaryKey)
            {
                // 增加主键约束
                pk = $"Alter Table {FormatName(field.Table)} ADD CONSTRAINT PK_{FormatName(field.Table)} PRIMARY KEY {(field.Identity ? "CLUSTERED" : "")}({FormatName(field)}) ON [PRIMARY]";
                sql += ";" + Environment.NewLine + pk;
            }
        }
        else
        {
            // 字段声明没有主键，但是主键实际存在，则删除主键
            //if (!String.IsNullOrEmpty(pk))
            if (oldfield.PrimaryKey)
            {
                sql += ";" + Environment.NewLine + pk;
            }
        }

        //// 需要提前删除相关默认值
        //if (oldfield.Default != null)
        //{
        //    var df = DropDefaultSQL(oldfield);
        //    if (!String.IsNullOrEmpty(df))
        //    {
        //        sql = df + ";" + Environment.NewLine + sql;

        //        // 如果还有默认值，加上
        //        if (field.Default != null)
        //        {
        //            df = AddDefaultSQLWithNoCheck(field);
        //            if (!String.IsNullOrEmpty(df)) sql += ";" + Environment.NewLine + df;
        //        }
        //    }
        //}
        // 需要提前删除相关索引
        foreach (var di in oldfield.Table.Indexes)
        {
            // 如果包含该字段
            if (di.Columns.Contains(oldfield.ColumnName, StringComparer.OrdinalIgnoreCase))
            {
                var dis = DropIndexSQL(di);
                if (!String.IsNullOrEmpty(dis)) sql = dis + ";" + Environment.NewLine + sql;
            }
        }
        // 如果还有索引，则加上
        foreach (var di in field.Table.Indexes)
        {
            // 如果包含该字段
            if (di.Columns.Contains(field.ColumnName, StringComparer.OrdinalIgnoreCase))
            {
                var cis = CreateIndexSQL(di);
                if (!String.IsNullOrEmpty(cis)) sql += ";" + Environment.NewLine + cis;
            }
        }

        return sql;
    }

    public override String DropIndexSQL(IDataIndex index) => $"Drop Index {FormatName(index.Table)}.{index.Name}";

    public override String DropColumnSQL(IDataColumn field)
    {
        ////删除默认值
        //String sql = DropDefaultSQL(field);
        //if (!String.IsNullOrEmpty(sql)) sql += ";" + Environment.NewLine;

        //删除主键
        var sql = DeletePrimaryKeySQL(field);
        if (!String.IsNullOrEmpty(sql)) sql += ";" + Environment.NewLine;

        sql += base.DropColumnSQL(field);
        return sql;
    }

    public override String AddColumnDescriptionSQL(IDataColumn field)
    {
        var sql = $"EXEC dbo.sp_addextendedproperty @name=N'MS_Description', @value=N'{field.Description}' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'{field.Table.TableName}', @level2type=N'COLUMN',@level2name=N'{field.ColumnName}'";
        return sql;
    }

    public override String DropColumnDescriptionSQL(IDataColumn field) => $"EXEC dbo.sp_dropextendedproperty @name=N'MS_Description', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'{field.Table.TableName}', @level2type=N'COLUMN',@level2name=N'{field.ColumnName}'";

    private String DeletePrimaryKeySQL(IDataColumn field)
    {
        if (!field.PrimaryKey) return String.Empty;

        var dis = field.Table.Indexes;
        if (dis == null || dis.Count <= 0) return String.Empty;

        var di = dis.FirstOrDefault(e => e.Columns.Any(x => x.EqualIgnoreCase(field.ColumnName, field.Name)));
        if (di == null) return String.Empty;

        return $"Alter Table {FormatName(field.Table)} Drop CONSTRAINT {di.Name}";
    }

    public override String DropDatabaseSQL(String dbname)
    {
        var sb = new StringBuilder();
        sb.AppendLine("use master");
        sb.AppendLine(";");
        sb.AppendLine("declare   @spid   varchar(20),@dbname   varchar(20)");
        sb.AppendLine("declare   #spid   cursor   for");
        sb.AppendFormat("select   spid=cast(spid   as   varchar(20))   from   master..sysprocesses   where   dbid=db_id('{0}')", dbname);
        sb.AppendLine();
        sb.AppendLine("open   #spid");
        sb.AppendLine("fetch   next   from   #spid   into   @spid");
        sb.AppendLine("while   @@fetch_status=0");
        sb.AppendLine("begin");
        sb.AppendLine("exec('kill   '+@spid)");
        sb.AppendLine("fetch   next   from   #spid   into   @spid");
        sb.AppendLine("end");
        sb.AppendLine("close   #spid");
        sb.AppendLine("deallocate   #spid");
        sb.AppendLine(";");
        sb.AppendFormat("Drop Database {0}", Database.FormatName(dbname));
        return sb.ToString();
    }

    #endregion

    /// <summary>数据类型映射</summary>
    private static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte[]), new String[] { "binary({0})", "image", "varbinary({0})", "timestamp" } },
        //{ typeof(DateTimeOffset), new String[] { "datetimeoffset({0})" } },
        { typeof(Guid), new String[] { "uniqueidentifier" } },
        //{ typeof(Object), new String[] { "sql_variant" } },
        //{ typeof(TimeSpan), new String[] { "time({0})" } },
        { typeof(Boolean), new String[] { "bit" } },
        { typeof(Byte), new String[] { "tinyint" } },
        { typeof(Int16), new String[] { "smallint" } },
        { typeof(Int32), new String[] { "int" } },
        { typeof(Int64), new String[] { "bigint" } },
        { typeof(Single), new String[] { "real" } },
        { typeof(Double), new String[] { "float" } },
        { typeof(Decimal), new String[] { "money", "decimal({0}, {1})", "numeric({0}, {1})", "smallmoney" } },
        { typeof(DateTime), new String[] { "datetime", "smalldatetime", "datetime2({0})", "date" } },
        { typeof(String), new String[] { "nvarchar({0})", "ntext", "text", "varchar({0})", "char({0})", "nchar({0})", "xml" } }
    };

    #region 辅助函数
    /// <summary>除去字符串两端成对出现的符号</summary>
    /// <param name="str"></param>
    /// <param name="prefix"></param>
    /// <param name="suffix"></param>
    /// <returns></returns>
    public static String Trim(String str, String prefix, String suffix)
    {
        while (!String.IsNullOrEmpty(str))
        {
            if (!str.StartsWith(prefix)) return str;
            if (!str.EndsWith(suffix)) return str;

            str = str[prefix.Length..^suffix.Length];
        }
        return str;
    }
    #endregion
}
