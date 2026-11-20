using System.Data;
using NewLife;

namespace XCode.DataAccessLayer;

/// <summary>数据库架构读取接口</summary>
public interface ISchemaReader
{
    /// <summary>数据库</summary>
    IDatabase Database { get; }

    /// <summary>所有元数据集合</summary>
    ICollection<String> MetaDataCollections { get; }

    /// <summary>保留关键字</summary>
    ICollection<String> ReservedWords { get; }

    /// <summary>取得表模型，正向工程</summary>
    /// <returns>所有表模型</returns>
    IList<IDataTable> GetTables();

    /// <summary>取得所有表名</summary>
    /// <returns>表名列表</returns>
    IList<String> GetTableNames();

    /// <summary>返回数据源的架构信息</summary>
    /// <param name="collectionName">指定要返回的架构名称</param>
    /// <param name="restrictionValues">为请求的架构指定一组限制值</param>
    /// <returns>架构信息；不支持时返回 null</returns>
    DataTable? GetSchema(String collectionName, String?[]? restrictionValues);
}
