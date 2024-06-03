using System.Data;
using System.Data.Common;
using System.Text;
using NewLife;
using NewLife.Collections;
using NewLife.Data;

namespace XCode.DataAccessLayer;

/// <summary>插入Sql语句生成器</summary>
public class InsertBuilder
{
    #region 属性
    /// <summary>数据保存模式。默认Insert</summary>
    public SaveModes Mode { get; set; } = SaveModes.Insert;

    /// <summary>
    /// 是否允许插入标识字段。默认false
    /// </summary>
    public Boolean AllowInsertIdentity { get; set; }

    /// <summary>参数化添删改查。默认关闭</summary>
    public Boolean UseParameter { get; set; }

    /// <summary>
    /// 参数集合
    /// </summary>
    public IDataParameter[]? Parameters { get; set; }
    #endregion

    /// <summary>
    /// 获取SQL语句
    /// </summary>
    /// <param name="database"></param>
    /// <param name="table"></param>
    /// <param name="columns"></param>
    /// <param name="entity"></param>
    /// <returns></returns>
    public virtual String? GetSql(IDatabase database, IDataTable table, IDataColumn[]? columns, IModel entity)
    {
        Parameters = null;

        var sbNames = Pool.StringBuilder.Get();
        var sbValues = Pool.StringBuilder.Get();

        columns ??= table.Columns.ToArray();

        var dps = new List<IDataParameter>();
        // 只读列没有插入操作
        foreach (var field in columns)
        {
            var value = entity[field.Name];

            // 标识列不需要插入，别的类型都需要
            if (CheckIdentity(database, field, value, sbNames, sbValues)) continue;

            sbNames.Separate(",").Append(database.FormatName(field));
            sbValues.Separate(",");

            if (UseParameter)
            {
                var dp = CreateParameter(database, field, value);
                dps.Add(dp);

                sbValues.Append(dp.ParameterName);
            }
            else
                sbValues.Append(database.FormatValue(field, value));
        }

        var ns = sbNames.Put(true);
        var vs = sbValues.Put(true);
        if (ns.IsNullOrEmpty()) return null;

        if (dps.Count > 0) Parameters = dps.ToArray();

        var action = Mode switch
        {
            SaveModes.Insert => "Insert",
            SaveModes.Upsert => "Upsert",
            SaveModes.InsertIgnore => "Insert Ignore",
            SaveModes.Replace => "Replace",
            _ => throw new NotSupportedException($"未支持[{Mode}]"),
        };

        return $"{action} Into {database.FormatName(table)}({ns}) Values({vs})";
    }

    Boolean CheckIdentity(IDatabase db, IDataColumn column, Object? value, StringBuilder sbNames, StringBuilder sbValues)
    {
        if (!column.Identity) return false;

        // 有些时候需要向自增字段插入数据，这里特殊处理
        String? idv = null;
        if (AllowInsertIdentity) idv = "" + value;

        // 允许返回String.Empty作为插入空
        if (idv == null) return true;

        sbNames.Separate(", ").Append(db.FormatName(column));
        sbValues.Separate(", ");

        sbValues.Append(idv);

        return true;
    }

    static IDataParameter CreateParameter(IDatabase db, IDataColumn column, Object? value)
    {
        var dp = db.CreateParameter(column.ColumnName ?? column.Name, value, column);

        if (dp is DbParameter dbp) dbp.IsNullable = column.Nullable;

        return dp;
    }
}