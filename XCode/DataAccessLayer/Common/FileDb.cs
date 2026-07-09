namespace XCode.DataAccessLayer;

/// <summary>文件型数据库</summary>
abstract class FileDbBase : DbBase
{
    #region 属性
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        //if (!builder.TryGetValue(_.DataSource, out file)) return;
        // 允许空，当作内存数据库处理
        //builder.TryGetValue(_.DataSource, out var file);
        var file = builder["Data Source"];
        file = OnResolveFile(file);
        builder["Data Source"] = file;
        DatabaseName = file;
    }

    protected virtual String OnResolveFile(String file) => ResolveFile(file);
    #endregion
}

/// <summary>文件型数据库会话</summary>
abstract class FileDbSession : DbSession
{
    #region 属性
    /// <summary>文件</summary>
    public String? FileName => (Database as FileDbBase)?.DatabaseName;
    #endregion

    #region 构造函数
    protected FileDbSession(IDatabase db) : base(db)
    {
        if (!FileName.IsNullOrEmpty())
        {
            if (!hasChecked.Contains(FileName))
            {
                hasChecked.Add(FileName);
                CreateDatabase();
            }
        }
    }
    #endregion

    #region 方法
    private static readonly List<String> hasChecked = [];

    ///// <summary>已重载。打开数据库连接前创建数据库</summary>
    //public override void Open()
    //{
    //    if (!String.IsNullOrEmpty(FileName))
    //    {
    //        if (!hasChecked.Contains(FileName))
    //        {
    //            hasChecked.Add(FileName);
    //            CreateDatabase();
    //        }
    //    }

    //    base.Open();
    //}

    protected virtual void CreateDatabase()
    {
        if (!File.Exists(FileName)) Database.CreateMetaData().CreateDatabase("");
    }
    #endregion

    #region 高级
    /// <summary>清空数据表，标识归零</summary>
    /// <param name="tableName"></param>
    /// <returns></returns>
    public override Int32 Truncate(String tableName)
    {
        var sql = $"Delete From {Database.FormatName(tableName)}";
        return Execute(sql);
    }
    #endregion
}

/// <summary>文件型数据库元数据</summary>
abstract class FileDbMetaData : DbMetaData
{
    #region 属性
    /// <summary>文件</summary>
    public String? FileName => (Database as FileDbBase)?.DatabaseName;
    #endregion

    #region DDL 执行方法
    /// <summary>建立数据库（文件型数据库创建文件）</summary>
    /// <param name="databaseName">数据库名，忽略</param>
    /// <param name="file">数据文件路径，忽略</param>
    /// <returns>是否成功</returns>
    public override Boolean CreateDatabase(String databaseName, String? file = null)
    {
        if (String.IsNullOrEmpty(FileName)) return false;

        // 提前创建目录
        var dir = Path.GetDirectoryName(FileName);
        if (!String.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        if (!File.Exists(FileName))
        {
            DAL.WriteLog("创建数据库：{0}", FileName);

            File.Create(FileName).Dispose();
        }

        return true;
    }

    /// <summary>删除数据库（文件型数据库删除文件）</summary>
    /// <param name="databaseName">数据库名，忽略</param>
    /// <returns>是否成功</returns>
    public override Boolean DropDatabase(String databaseName)
    {
        //首先关闭数据库
        if (Database is DbBase db)
            db.ReleaseSession();
        else
            Database.CreateSession().Dispose();

        //OleDbConnection.ReleaseObjectPool();
        GC.Collect();

        if (File.Exists(FileName)) File.Delete(FileName);

        return true;
    }

    /// <summary>数据库是否存在</summary>
    /// <param name="databaseName">数据库名，忽略</param>
    /// <returns></returns>
    public override Boolean DatabaseExist(String? databaseName)
    {
        return File.Exists(FileName);
    }
    #endregion

    /// <summary>创建数据库。已被 CreateDatabase(String, String?) 替代</summary>
    protected virtual void CreateDatabase() => CreateDatabase("", null);

    /// <summary>删除数据库。已被 DropDatabase(String) 替代</summary>
    protected virtual void DropDatabase() => DropDatabase("");
}