using System.Collections;
using System.Data;
using System.Data.Common;
using NewLife.Serialization;

namespace XCode.IoTDB;

/// <summary>IoTDB命令。CommandText 为 IoTDB SQL，原样转发 REST v2</summary>
public class IoTDBCommand : DbCommand
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
    protected override DbParameterCollection DbParameterCollection { get; } = new IoTDBParameterCollection();

    /// <summary>事务</summary>
    protected override DbTransaction? DbTransaction { get; set; }

    /// <summary>连接（强类型）</summary>
    public new IoTDBConnection? Connection
    {
        get => DbConnection as IoTDBConnection;
        set => DbConnection = value;
    }
    #endregion

    #region 方法
    /// <summary>取消命令</summary>
    public override void Cancel() { }

    /// <summary>执行非查询。DDL/DML 走 /rest/v2/nonQuery</summary>
    /// <returns>固定返回 1</returns>
    public override Int32 ExecuteNonQuery()
    {
        var conn = Connection;
        if (conn == null || conn.State != ConnectionState.Open)
            throw new InvalidOperationException(L.T("Connection must be open."));

        var text = conn.NonQuery(CommandText);
        CheckResult(text);

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
    protected override DbParameter CreateDbParameter() => new IoTDBParameter();

    /// <summary>执行读取器</summary>
    /// <param name="behavior">行为</param>
    /// <returns></returns>
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        var conn = Connection;
        if (conn == null || conn.State != ConnectionState.Open)
            throw new InvalidOperationException(L.T("Connection must be open."));

        var text = conn.Query(CommandText);
        var table = IoTDBResultParser.Parse(text);

        return table.CreateDataReader();
    }

    private static void CheckResult(String text)
    {
        if (text.IsNullOrWhiteSpace()) return;

        if (JsonHelper.Parse(text) is IDictionary<String, Object?> dic)
        {
            var code = dic["code"].ToInt();
            if (code != 0 && code != 200) throw new Exception(L.T("{0} request failed: {1}", "IoTDB", dic["message"] + ""));
        }
    }
    #endregion
}

/// <summary>IoTDB参数</summary>
public class IoTDBParameter : DbParameter
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

/// <summary>IoTDB参数集合</summary>
public class IoTDBParameterCollection : DbParameterCollection
{
    private readonly List<IoTDBParameter> _parameters = [];

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
    public new IoTDBParameter this[Int32 index]
    {
        get => _parameters[index];
        set => _parameters[index] = value;
    }

    /// <summary>获取或设置参数</summary>
    /// <param name="parameterName">参数名</param>
    /// <returns></returns>
    public new IoTDBParameter this[String parameterName]
    {
        get => _parameters[IndexOf(parameterName)];
        set => _parameters[IndexOf(parameterName)] = value;
    }

    /// <summary>添加参数</summary>
    /// <param name="value">参数</param>
    /// <returns></returns>
    public override Int32 Add(Object value)
    {
        _parameters.Add((IoTDBParameter)value);
        return _parameters.Count - 1;
    }

    /// <summary>添加参数范围</summary>
    /// <param name="values">参数数组</param>
    public override void AddRange(Array values)
    {
        foreach (IoTDBParameter param in values) _parameters.Add(param);
    }

    /// <summary>清空参数</summary>
    public override void Clear() => _parameters.Clear();

    /// <summary>是否包含参数</summary>
    /// <param name="value">参数</param>
    /// <returns></returns>
    public override Boolean Contains(Object value) => _parameters.Contains((IoTDBParameter)value);

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
    public override Int32 IndexOf(Object value) => _parameters.IndexOf((IoTDBParameter)value);

    /// <summary>插入参数</summary>
    /// <param name="index">索引</param>
    /// <param name="value">参数</param>
    public override void Insert(Int32 index, Object value) => _parameters.Insert(index, (IoTDBParameter)value);

    /// <summary>移除参数</summary>
    /// <param name="value">参数</param>
    public override void Remove(Object value) => _parameters.Remove((IoTDBParameter)value);

    /// <summary>移除参数</summary>
    /// <param name="index">索引</param>
    public override void RemoveAt(Int32 index) => _parameters.RemoveAt(index);

    /// <summary>移除参数</summary>
    /// <param name="parameterName">参数名</param>
    public override void RemoveAt(String parameterName) => RemoveAt(IndexOf(parameterName));

    /// <summary>设置参数</summary>
    /// <param name="parameterName">参数名</param>
    /// <param name="value">参数</param>
    protected override void SetParameter(String parameterName, DbParameter value) => _parameters[IndexOf(parameterName)] = (IoTDBParameter)value;

    /// <summary>设置参数</summary>
    /// <param name="index">索引</param>
    /// <param name="value">参数</param>
    protected override void SetParameter(Int32 index, DbParameter value) => _parameters[index] = (IoTDBParameter)value;
}
