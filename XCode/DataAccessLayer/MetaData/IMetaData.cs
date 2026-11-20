using NewLife;

namespace XCode.DataAccessLayer;

/// <summary>数据库元数据接口</summary>
public interface IMetaData : IDisposable2, ISchemaReader, ISchemaMigrator, IDatabaseCreator, IDatabaseMaintenance
{
}