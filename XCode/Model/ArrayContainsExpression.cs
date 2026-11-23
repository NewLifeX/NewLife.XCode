using System.Text;
using NewLife;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace XCode;

/// <summary>数组包含表达式。按数据库方言生成 contains() 或 @> ARRAY[...]</summary>
public class ArrayContainsExpression(FieldItem field, Object value) : Expression
{
    #region 属性
    /// <summary>字段</summary>
    public FieldItem Field { get; set; } = field;

    /// <summary>要包含的元素值</summary>
    public Object Value { get; set; } = value;

    /// <summary>是否为空</summary>
    public override Boolean IsEmpty => Field == null;
    #endregion

    #region 输出
    /// <summary>输出字段表达式的字符串形式</summary>
    /// <param name="db">数据库</param>
    /// <param name="builder">字符串构建器</param>
    /// <param name="ps">参数字典。数组包含统一使用字面量，避免驱动参数绑定差异</param>
    /// <returns></returns>
    public override void GetString(IDatabase? db, StringBuilder builder, IDictionary<String, Object?>? ps)
    {
        if (Field == null) return;

        // 部分场景外部未能传入数据库，此时内部尽力获取
        db ??= Field.Factory?.Session.Dal.Db ?? throw new ArgumentNullException(nameof(db));

        var column = Field.Field;
        var elementType = Field.Type?.GetElementType();
        if (column == null || elementType == null)
            throw new NotSupportedException(L.T("[{0}] supports only array fields; the current type is {1}.", nameof(ArrayContainsExpression), Field.Type?.Name));

        var columnName = db.FormatName(column);

        // 克隆字段并把类型换成元素类型，避免 PostgreSQL 把标量字符串当 IEnumerable<char>
        var clone = column.Clone(column.Table);
        clone.DataType = elementType;
        var value = db.FormatValue(clone, Value);

        if (db.Type is DatabaseType.Presto or DatabaseType.Trino)
        {
            builder.AppendFormat("contains({0}, {1})", columnName, value);
        }
        else if (db.Type is DatabaseType.PostgreSQL or DatabaseType.VastBase)
        {
            builder.AppendFormat("{0} @> ARRAY[{1}]", columnName, value);

            // 使用原始类型做数组类型转换，避免 bigint[] 与 integer[] 无法比较
            var rawType = column.RawType;
            if (!rawType.IsNullOrEmpty() && rawType.EndsWith("[]")) builder.Append("::").Append(rawType);
        }
        else
        {
            throw new NotSupportedException(L.T("ArrayContains is not supported by {0}.", db.Type));
        }
    }
    #endregion
}
