namespace XCode.DataAccessLayer;

/// <summary>数据库维护接口</summary>
public interface IDatabaseMaintenance
{
    /// <summary>备份数据库</summary>
    /// <param name="dbName">数据库名</param>
    /// <param name="backupFile">备份文件</param>
    /// <returns>备份文件路径</returns>
    String? BackupDatabase(String? dbName = null, String? backupFile = null);

    /// <summary>还原数据库</summary>
    /// <param name="backupFile">备份文件</param>
    /// <param name="recoverDir">还原目录</param>
    /// <returns>是否成功</returns>
    Boolean RestoreDatabase(String backupFile, String? recoverDir = null);

    /// <summary>收缩数据库</summary>
    /// <returns>是否成功</returns>
    Boolean CompactDatabase();
}
