namespace XCode.DataAccessLayer;

/// <summary>数据库生命周期接口</summary>
public interface IDatabaseCreator
{
    /// <summary>建立数据库</summary>
    /// <param name="databaseName">数据库名</param>
    /// <param name="file">数据文件路径</param>
    /// <returns>是否成功</returns>
    Boolean CreateDatabase(String databaseName, String? file = null);

    /// <summary>删除数据库</summary>
    /// <param name="databaseName">数据库名</param>
    /// <returns>是否成功</returns>
    Boolean DropDatabase(String databaseName);

    /// <summary>数据库是否存在</summary>
    /// <param name="databaseName">数据库名</param>
    /// <returns>是否存在</returns>
    Boolean DatabaseExist(String? databaseName);
}
