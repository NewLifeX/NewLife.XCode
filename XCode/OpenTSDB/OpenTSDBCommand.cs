using System.Collections;
using System.Data;
using System.Data.Common;
using NewLife.Serialization;

namespace XCode.OpenTSDB;

/// <summary>OpenTSDB命令</summary>
/// <remarks>
/// 写入：CommandText 为 JSON 数组，POST /api/put。
/// 查询：CommandText 以 { 开头时按 JSON POST /api/query，否则按 m=...&amp;start=... 走 GET /api/query。
/// </remarks>
public class OpenTSDBCommand : DbCommand
{
    #region 属性
    /// <summary>命令文本</summary>
    public override String CommandText { get; set; } = String.Empty;

    /// <summary>命令超时时间</summary>
    public override Int32 CommandTimeout { get; set; } = 30;

    /// <summary>命令类型</summary>
    public override CommandType CommandType { get; set; } = CommandType.Text;

    /// <summary>是否已设计时更新</summary>
    public override Boolean DesignTimeVisible { get; set; }

    /// <summary>更新行为来源</summary>
    public override UpdateRowSource UpdatedRowSource { get; set; }

    /// <summary>连接</summary>
    protected override DbConnection? DbConnection { get; set; }

    /// <summary>参数集合</summary>
    protected override DbParameterCollection DbParameterCollection { get; } = new OpenTSDBParameterCollection();

    /// <summary>事务</summary>
    protected override DbTransaction? DbTransaction { get; set; }

    /// <summary>连接（强类型）</summary>
    public new OpenTSDBConnection? Connection
    {
        get => DbConnection as OpenTSDBConnection;
        set => DbConnection = value;
    }
    #endregion

    #region 方法
    /// <summary>取消命令</summary>
    public override void Cancel() { }

    /// <summary>执行非查询。写入 JSON 数据点</summary>
    /// <returns>写入点数</returns>
    public override Int32 ExecuteNonQuery()
    {
        var conn = Connection;
        if (conn == null || conn.State != ConnectionState.Open)
            throw new InvalidOperationException(L.T("Connection must be open."));

        var text = conn.Post("/api/put?details", CommandText, "application/json");
        if (text.IsNullOrWhiteSpace()) return 1;

        if (JsonHelper.Parse(text) is IDictionary<String, Object?> dic)
        {
            var failed = dic["failed"].ToInt();
            if (failed > 0)
            {
                var errors = dic["errors"] is IList<Object> list && list.Count > 0 ? list[0] + "" : text;
                throw new Exception(L.T("{0} request failed: {1}", "OpenTSDB", errors));
            }

            return dic["success"].ToInt();
        }

        return 1;
    }

    /// <summary>执行查询</summary>
    /// <returns>首行首列</returns>
    public override Object? ExecuteScalar()
    {
        using var reader = ExecuteReader();
        if (reader.Read()) return reader.GetValue(0);

        return null;
    }

    /// <summary>准备命令</summary>
    public override void Prepare() { }

    /// <summary>创建参数</summary>
    /// <returns></returns>
    protected override DbParameter CreateDbParameter() => new OpenTSDBParameter();

    /// <summary>执行读取器</summary>
    /// <param name="behavior">行为</param>
    /// <returns></returns>
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        var conn = Connection;
        if (conn == null || conn.State != ConnectionState.Open)
            throw new InvalidOperationException(L.T("Connection must be open."));

        var query = CommandText.Trim();
        String text;
        if (query.StartsWith("{"))
        {
            text = conn.Post("/api/query", query, "application/json");
        }
        else
        {
            // 简化写法：m=sum:metric{tag=value}&start=1h-ago
            if (!query.Contains("start=")) query = query.IsNullOrEmpty() ? "start=1h-ago" : $"{query}&start=1h-ago";

            text = conn.Get("/api/query?" + query);
        }

        var table = OpenTSDBResultParser.Parse(text);
        return table.CreateDataReader();
    }
    #endregion
}

/// <summary>OpenTSDB参数</summary>
public class OpenTSDBParameter : DbParameter
{
    /// <summary>参数名称</summary>
    public override String ParameterName { get; set; } = String.Empty;

    /// <summary>参数值</summary>
    public override Object? Value { get; set; }

    /// <summary>数据库类型</summary>
    public override DbType DbType { get; set; }

    /// <summary>参数方向</summary>
    public override ParameterDirection Direction { get; set; }

    /// <summary>是否可空</summary>
    public override Boolean IsNullable { get; set; }

    /// <summary>参数大小</summary>
    public override Int32 Size { get; set; }

    /// <summary>源列</summary>
    public override String SourceColumn { get; set; } = String.Empty;

    /// <summary>源列是否可空</summary>
    public override Boolean SourceColumnNullMapping { get; set; }

    /// <summary>源版本</summary>
    public override DataRowVersion SourceVersion { get; set; } = DataRowVersion.Current;

    /// <summary>重置数据库类型</summary>
    public override void ResetDbType() => DbType = DbType.String;
}

/// <summary>OpenTSDB参数集合</summary>
public class OpenTSDBParameterCollection : DbParameterCollection
{
    private readonly List<OpenTSDBParameter> _parameters = [];

    /// <summary>参数数量</summary>
    public override Int32 Count => _parameters.Count;

    /// <summary>同步根</summary>
    public override Object SyncRoot => ((ICollection)_parameters).SyncRoot;

    /// <summary>固定大小</summary>
    public override Boolean IsFixedSize => false;

    /// <summary>只读</summary>
    public override Boolean IsReadOnly => false;

    /// <summary>同步</summary>
    public override Boolean IsSynchronized => false;

    /// <summary>获取或设置参数</summary>
    /// <param name="index">索引</param>
    /// <returns></returns>
    public new OpenTSDBParameter this[Int32 index]
    {
        get => _parameters[index];
        set => _parameters[index] = value;
    }

    /// <summary>获取或设置参数</summary>
    /// <param name="parameterName">参数名</param>
    /// <returns></returns>
    public new OpenTSDBParameter this[String parameterName]
    {
        get => _parameters[IndexOf(parameterName)];
        set => _parameters[IndexOf(parameterName)] = value;
    }

    /// <summary>添加参数</summary>
    /// <param name="value">参数</param>
    /// <returns></returns>
    public override Int32 Add(Object value)
    {
        _parameters.Add((OpenTSDBParameter)value);
        return _parameters.Count - 1;
    }

    /// <summary>添加参数范围</summary>
    /// <param name="values">参数数组</param>
    public override void AddRange(Array values)
    {
        foreach (OpenTSDBParameter param in values) _parameters.Add(param);
    }

    /// <summary>清空参数</summary>
    public override void Clear() => _parameters.Clear();

    /// <summary>是否包含参数</summary>
    /// <param name="value">参数</param>
    /// <returns></returns>
    public override Boolean Contains(Object value) => _parameters.Contains((OpenTSDBParameter)value);

    /// <summary>是否包含参数</summary>
    /// <param name="value">参数名</param>
    /// <returns></returns>
    public override Boolean Contains(String value) => IndexOf(value) != -1;

    /// <summary>复制到数组</summary>
    /// <param name="array">目标数组</param>
    /// <param name="index">起始索引</param>
    public override void CopyTo(Array array, Int32 index) => ((ICollection)_parameters).CopyTo(array, index);

    /// <summary>获取枚举器</summary>
    /// <returns></returns>
    public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();

    /// <summary>获取参数</summary>
    /// <param name="parameterName">参数名</param>
    /// <returns></returns>
    protected override DbParameter GetParameter(String parameterName) => _parameters[IndexOf(parameterName)];

    /// <summary>获取参数</summary>
    /// <param name="index">索引</param>
    /// <returns></returns>
    protected override DbParameter GetParameter(Int32 index) => _parameters[index];

    /// <summary>获取参数索引</summary>
    /// <param name="parameterName">参数名</param>
    /// <returns></returns>
    public override Int32 IndexOf(String parameterName)
    {
        for (var i = 0; i < _parameters.Count; i++)
        {
            if (_parameters[i].ParameterName.Equals(parameterName, StringComparison.OrdinalIgnoreCase)) return i;
        }

        return -1;
    }

    /// <summary>获取参数索引</summary>
    /// <param name="value">参数</param>
    /// <returns></returns>
    public override Int32 IndexOf(Object value) => _parameters.IndexOf((OpenTSDBParameter)value);

    /// <summary>插入参数</summary>
    /// <param name="index">索引</param>
    /// <param name="value">参数</param>
    public override void Insert(Int32 index, Object value) => _parameters.Insert(index, (OpenTSDBParameter)value);

    /// <summary>移除参数</summary>
    /// <param name="value">参数</param>
    public override void Remove(Object value) => _parameters.Remove((OpenTSDBParameter)value);

    /// <summary>移除参数</summary>
    /// <param name="index">索引</param>
    public override void RemoveAt(Int32 index) => _parameters.RemoveAt(index);

    /// <summary>移除参数</summary>
    /// <param name="parameterName">参数名</param>
    public override void RemoveAt(String parameterName) => RemoveAt(IndexOf(parameterName));

    /// <summary>设置参数</summary>
    /// <param name="parameterName">参数名</param>
    /// <param name="value">参数</param>
    protected override void SetParameter(String parameterName, DbParameter value) => _parameters[IndexOf(parameterName)] = (OpenTSDBParameter)value;

    /// <summary>设置参数</summary>
    /// <param name="index">索引</param>
    /// <param name="value">参数</param>
    protected override void SetParameter(Int32 index, DbParameter value) => _parameters[index] = (OpenTSDBParameter)value;
}
