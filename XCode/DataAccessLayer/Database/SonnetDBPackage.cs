using System.Data;
using System.Threading;
using NewLife.Data;

namespace XCode.DataAccessLayer;

/// <summary>涉及SonnetDB的显式数据迁移，保留来源中的SQL NULL；普通DbPackage和DAL读取合同不变。</summary>
public class SonnetDBPackage : DbPackage
{
    /// <summary>创建保留SQL NULL的迁移数据包，使用内置ID、时间或分页抽取算法。</summary>
    public SonnetDBPackage() => QueryCallback = ReadPage;

    private static DbTable ReadPage(DAL source, SelectBuilder builder, Int64 row, Int64 size, CancellationToken cancellationToken)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = source.Db.Factory.CreateConnection()!;
        connection.ConnectionString = source.Db.ConnectionString;
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = source.PageSplit(builder.Clone(), row, size).ToString();
        command.CommandTimeout = source.Db.CommandTimeout > 0 ? source.Db.CommandTimeout : 30;
        command.Parameters.AddRange(builder.Parameters.ToArray());
        using var reader = command.ExecuteReader();
        var result = new DbTable();
        result.ReadHeader(reader);
        for (Int64 i = 0; i < size && reader.Read(); i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Rows.Add(SonnetDBSession.ReadNullableRow(reader));
        }
        cancellationToken.ThrowIfCancellationRequested();
        result.Total = result.Rows.Count;
        return result;
    }
}
