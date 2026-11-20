using System.Data;
using System.Data.Common;
using NewLife;

namespace XCode.DataAccessLayer;

/// <summary>数据库方言接口。集中处理名称、值、参数与分页等 SQL 方言差异</summary>
public interface IDbDialect
{
    /// <summary>长文本长度</summary>
    Int32 LongTextLength { get; }

    /// <summary>格式化名称，如果不是关键字，则原样返回</summary>
    /// <param name="name">名称</param>
    /// <returns>格式化后的名称</returns>
    String FormatName(String name);

    /// <summary>格式化表名，考虑表前缀和 Owner</summary>
    /// <param name="table">表</param>
    /// <param name="formatKeyword">是否格式化关键字</param>
    /// <returns>格式化后的表名</returns>
    String FormatName(IDataTable table, Boolean formatKeyword = true);

    /// <summary>格式化字段名，考虑大小写</summary>
    /// <param name="column">字段</param>
    /// <returns>格式化后的字段名</returns>
    String FormatName(IDataColumn column);

    /// <summary>格式化数据为 SQL 数据</summary>
    /// <param name="column">字段</param>
    /// <param name="value">数值</param>
    /// <returns>格式化后的 SQL 字面量</returns>
    String FormatValue(IDataColumn column, Object? value);

    /// <summary>格式化时间为 SQL 字符串</summary>
    /// <param name="dateTime">时间值</param>
    /// <returns>格式化后的 SQL 字面量</returns>
    String FormatDateTime(DateTime dateTime);

    /// <summary>格式化时间为 SQL 字符串</summary>
    /// <param name="column">字段</param>
    /// <param name="dateTime">时间值</param>
    /// <returns>格式化后的 SQL 字面量</returns>
    String FormatDateTime(IDataColumn column, DateTime dateTime);

    /// <summary>格式化模糊搜索的字符串，处理转义字符</summary>
    /// <param name="column">字段</param>
    /// <param name="format">格式化字符串</param>
    /// <param name="value">数值</param>
    /// <returns>格式化后的模糊搜索字面量</returns>
    String FormatLike(IDataColumn column, String format, String value);

    /// <summary>格式化参数化的模糊搜索字符串，处理转义字符</summary>
    /// <param name="column">字段</param>
    /// <param name="format">格式化字符串</param>
    /// <returns>格式化后的模糊搜索模板</returns>
    String FormatLike(IDataColumn column, String format);

    /// <summary>格式化参数名</summary>
    /// <param name="name">名称</param>
    /// <returns>带前缀的参数名</returns>
    String FormatParameterName(String name);

    /// <summary>字符串相加</summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>字符串相加表达式</returns>
    String StringConcat(String left, String right);

    /// <summary>创建参数</summary>
    /// <param name="name">名称</param>
    /// <param name="value">值</param>
    /// <param name="field">字段</param>
    /// <returns>数据参数</returns>
    IDataParameter CreateParameter(String name, Object? value, IDataColumn? field);

    /// <summary>创建参数</summary>
    /// <param name="name">名称</param>
    /// <param name="value">值</param>
    /// <param name="type">类型</param>
    /// <returns>数据参数</returns>
    IDataParameter CreateParameter(String name, Object? value, Type? type = null);

    /// <summary>创建参数数组</summary>
    /// <param name="ps">参数字典</param>
    /// <returns>参数数组</returns>
    IDataParameter[] CreateParameters(IDictionary<String, Object>? ps);

    /// <summary>根据对象成员创建参数数组</summary>
    /// <param name="model">数据对象</param>
    /// <returns>参数数组</returns>
    IDataParameter[] CreateParameters(Object? model);

    /// <summary>构造分页 SQL</summary>
    /// <param name="sql">SQL 语句</param>
    /// <param name="startRowIndex">开始行，0 表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0 表示所有行</param>
    /// <param name="keyColumn">唯一键，用于 not in 分页</param>
    /// <returns>分页 SQL</returns>
    String PageSplit(String sql, Int64 startRowIndex, Int64 maximumRows, String? keyColumn);

    /// <summary>构造分页 SQL</summary>
    /// <param name="builder">查询生成器</param>
    /// <param name="startRowIndex">开始行，0 表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0 表示所有行</param>
    /// <returns>分页查询生成器</returns>
    SelectBuilder PageSplit(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows);

    /// <summary>生成批量删除 SQL。部分数据库支持分批删除</summary>
    /// <param name="tableName">表名</param>
    /// <param name="where">条件</param>
    /// <param name="batchSize">批大小</param>
    /// <returns>批量删除 SQL；不支持分批删除时返回 null</returns>
    String? BuildDeleteSql(String tableName, String where, Int32 batchSize);

    /// <summary>渲染窗口函数表达式</summary>
    /// <param name="expression">窗口表达式</param>
    /// <returns>SQL 片段</returns>
    String RenderWindow(WindowExpression expression);
}
