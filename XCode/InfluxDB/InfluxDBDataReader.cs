using System.Collections;
using System.Data;
using System.Data.Common;

namespace XCode.InfluxDB;

/// <summary>InfluxDB数据读取器</summary>
public class InfluxDBDataReader : DbDataReader
{
    private readonly String _csv;
    private readonly String[][] _rows;
    private readonly String[] _headers;
    private Int32 _currentRow = -1;
    private Boolean _isClosed;

    /// <summary>实例化</summary>
    /// <param name="csv">CSV数据</param>
    public InfluxDBDataReader(String csv)
    {
        _csv = csv;
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // InfluxDB 返回的 CSV 格式：第一行是注释（以#开头），第二行是列名，第三行开始是数据
        var dataLines = lines.Where(l => !l.StartsWith("#")).ToArray();
        if (dataLines.Length > 0)
        {
            _headers = dataLines[0].Split(',');
            _rows = dataLines.Skip(1).Select(line => line.Split(',')).ToArray();
        }
        else
        {
            _headers = [];
            _rows = [];
        }
    }

    /// <summary>字段数量</summary>
    public override Int32 FieldCount => _headers.Length;

    /// <summary>是否有行</summary>
    public override Boolean HasRows => _rows.Length > 0;

    /// <summary>是否已关闭</summary>
    public override Boolean IsClosed => _isClosed;

    /// <summary>受影响行数</summary>
    public override Int32 RecordsAffected => -1;

    /// <summary>深度</summary>
    public override Int32 Depth => 0;

    /// <summary>索引器</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override Object this[Int32 ordinal] => GetValue(ordinal);

    /// <summary>索引器</summary>
    /// <param name="name">列名</param>
    /// <returns></returns>
    public override Object this[String name] => GetValue(GetOrdinal(name));

    /// <summary>读取下一行</summary>
    /// <returns></returns>
    public override Boolean Read()
    {
        if (_currentRow + 1 < _rows.Length)
        {
            _currentRow++;
            return true;
        }
        return false;
    }

    /// <summary>关闭读取器</summary>
    public override void Close() => _isClosed = true;

    /// <summary>获取列名</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override String GetName(Int32 ordinal) => _headers[ordinal];

    /// <summary>获取列索引</summary>
    /// <param name="name">列名</param>
    /// <returns></returns>
    public override Int32 GetOrdinal(String name)
    {
        for (var i = 0; i < _headers.Length; i++)
        {
            if (_headers[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    /// <summary>获取值</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override Object GetValue(Int32 ordinal)
    {
        if (_currentRow < 0 || _currentRow >= _rows.Length)
            throw new InvalidOperationException("Invalid row position.");

        var value = _rows[_currentRow][ordinal];
        return String.IsNullOrEmpty(value) ? DBNull.Value : value;
    }

    /// <summary>获取所有值</summary>
    /// <param name="values">值数组</param>
    /// <returns></returns>
    public override Int32 GetValues(Object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }
        return count;
    }

    /// <summary>是否为空值</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override Boolean IsDBNull(Int32 ordinal) => GetValue(ordinal) == DBNull.Value;

    /// <summary>获取字段类型</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override Type GetFieldType(Int32 ordinal) => typeof(String);

    /// <summary>获取数据类型名称</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override String GetDataTypeName(Int32 ordinal) => "String";

    /// <summary>获取布尔值</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override Boolean GetBoolean(Int32 ordinal) => Boolean.Parse(GetValue(ordinal).ToString()!);

    /// <summary>获取字节</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override Byte GetByte(Int32 ordinal) => Byte.Parse(GetValue(ordinal).ToString()!);

    /// <summary>获取字节数组</summary>
    /// <param name="ordinal">索引</param>
    /// <param name="dataOffset">数据偏移</param>
    /// <param name="buffer">缓冲区</param>
    /// <param name="bufferOffset">缓冲区偏移</param>
    /// <param name="length">长度</param>
    /// <returns></returns>
    public override Int64 GetBytes(Int32 ordinal, Int64 dataOffset, Byte[]? buffer, Int32 bufferOffset, Int32 length) => 0;

    /// <summary>获取字符</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override Char GetChar(Int32 ordinal) => Char.Parse(GetValue(ordinal).ToString()!);

    /// <summary>获取字符数组</summary>
    /// <param name="ordinal">索引</param>
    /// <param name="dataOffset">数据偏移</param>
    /// <param name="buffer">缓冲区</param>
    /// <param name="bufferOffset">缓冲区偏移</param>
    /// <param name="length">长度</param>
    /// <returns></returns>
    public override Int64 GetChars(Int32 ordinal, Int64 dataOffset, Char[]? buffer, Int32 bufferOffset, Int32 length) => 0;

    /// <summary>获取日期时间</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override DateTime GetDateTime(Int32 ordinal) => DateTime.Parse(GetValue(ordinal).ToString()!);

    /// <summary>获取十进制数</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override Decimal GetDecimal(Int32 ordinal) => Decimal.Parse(GetValue(ordinal).ToString()!);

    /// <summary>获取双精度浮点数</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override Double GetDouble(Int32 ordinal) => Double.Parse(GetValue(ordinal).ToString()!);

    /// <summary>获取单精度浮点数</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override Single GetFloat(Int32 ordinal) => Single.Parse(GetValue(ordinal).ToString()!);

    /// <summary>获取GUID</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override Guid GetGuid(Int32 ordinal) => Guid.Parse(GetValue(ordinal).ToString()!);

    /// <summary>获取16位整数</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override Int16 GetInt16(Int32 ordinal) => Int16.Parse(GetValue(ordinal).ToString()!);

    /// <summary>获取32位整数</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override Int32 GetInt32(Int32 ordinal) => Int32.Parse(GetValue(ordinal).ToString()!);

    /// <summary>获取64位整数</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override Int64 GetInt64(Int32 ordinal) => Int64.Parse(GetValue(ordinal).ToString()!);

    /// <summary>获取字符串</summary>
    /// <param name="ordinal">索引</param>
    /// <returns></returns>
    public override String GetString(Int32 ordinal) => GetValue(ordinal).ToString()!;

    /// <summary>获取枚举器</summary>
    /// <returns></returns>
    public override IEnumerator GetEnumerator() => new DbEnumerator(this);

    /// <summary>获取模式表</summary>
    /// <returns></returns>
    public override DataTable GetSchemaTable()
    {
        var table = new DataTable("SchemaTable");
        table.Columns.Add("ColumnName", typeof(String));
        table.Columns.Add("ColumnOrdinal", typeof(Int32));
        table.Columns.Add("DataType", typeof(Type));

        for (var i = 0; i < _headers.Length; i++)
        {
            var row = table.NewRow();
            row["ColumnName"] = _headers[i];
            row["ColumnOrdinal"] = i;
            row["DataType"] = typeof(String);
            table.Rows.Add(row);
        }

        return table;
    }

    /// <summary>下一个结果集</summary>
    /// <returns></returns>
    public override Boolean NextResult() => false;
}
