// XCode NativeAOT 冒烟：16 场景逐项 PASS/FAIL，任一失败以非 0 退出码结束。
// 同一工程双跑对照：JIT（dotnet run -c Release）与 NativeAOT（dotnet publish -c Release -r <rid> 后运行产物）。
// 由 .github/workflows/aot.yml 的 analyzer/aot job 自动执行（win-x64 阻断基线，linux-x64 观察期）。
// 背景与实测结论见 Doc/AOT兼容性分析.md。
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.Common;
using XCode.Configuration;
using XCode.DataAccessLayer;

var failures = new List<String>();

void Check(String name, Boolean ok)
{
    Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}");
    if (!ok) failures.Add(name);
}

void Run(String name, Func<Boolean> func)
{
    try
    {
        Check(name, func());
    }
    catch (Exception ex)
    {
        var e = ex;
        while (e is TypeInitializationException && e.InnerException != null) e = e.InnerException;
        var lines = e.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Console.WriteLine($"[FAIL] {name} -> {String.Join(Environment.NewLine, lines.Take(8))}");
        failures.Add(name);
    }
}

Console.WriteLine($"IsDynamicCodeSupported={RuntimeFeature.IsDynamicCodeSupported}");
Console.WriteLine($"Assembly.Location={GetXCodeLocation()}");

[UnconditionalSuppressMessage("SingleFile", "IL3000", Justification = "AOT 判真探针：以 Location 是否为空区分原生发布件与 JIT 运行，不用于路径判断")]
static String? GetXCodeLocation() => typeof(DAL).Assembly.Location;

[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "冒烟探针：验证 AOT 下 Expression.Compile 的运行时解释器回退，非生产路径")]
static Boolean ProbeDirectCompile() => System.Linq.Expressions.Expression.Lambda(System.Linq.Expressions.Expression.Constant(42)).Compile().DynamicInvoke() is Int32 i42 && i42 == 42;

// 1. Utility 基础类型转换（Core 能力）
Run("1.Utility 基础类型转换", () => "123".ToInt() == 123 && "3.14".ToDouble() == 3.14 && "true".ToBoolean() && "2026-10-13".ToDateTime().Year == 2026);

// 2. DAL.CreateTable 元数据对象创建（非实体表路径）
Run("2.DAL.CreateTable 元数据对象创建", () =>
{
    var table = DAL.CreateTable();
    var col = table.CreateColumn();
    col.DataType = typeof(Int32);
    Console.WriteLine($"    (TableType={table.GetType().Name}, Columns.Count={table.Columns.Count})");
    return col != null && table.Columns != null;
});

// 3. 实体属性/索引器（生成代码静态路径）
Run("3.实体属性/索引器（生成代码静态路径）", () =>
{
    var e = new SmokeUser { ID = 1, Name = "aot" };
    e["Name"] = "aot2";
    return (String?)e["Name"] == "aot2";
});

// 4. 实体元数据 Meta.Table
Run("4.实体元数据 Meta.Table", () =>
{
    var table = SmokeUser.Meta.Table;
    Console.WriteLine($"    (Table={table?.TableName}, Fields.Length={table?.Fields.Length})");
    return table != null && table.Fields.Length >= 2;
});

// 5. 实体工厂 Meta.Factory.Create()
Run("5.实体工厂 Meta.Factory.Create()", () => SmokeUser.Meta.Factory.Create() is SmokeUser);

// 6. XTrace 日志（Core 能力）
Run("6.XTrace 日志", () =>
{
    XTrace.WriteLine("aot smoke");
    return true;
});

// 7. 实体行映射（元数据→DataTable 装载，覆盖元数据扫描与工厂创建回路）
Run("7.实体行映射 LoadData", () =>
{
    var dt = new System.Data.DataTable();
    dt.Columns.Add("ID", typeof(Int32));
    dt.Columns.Add("Name", typeof(String));
    dt.Rows.Add(1, "row1");
    var list = SmokeUser.Meta.Factory.Accessor.LoadData<SmokeUser>(dt);
    Console.WriteLine($"    (list.Count={list.Count}, ID={list.FirstOrDefault()?.ID}, Name={list.FirstOrDefault()?.Name})");
    return list.Count == 1 && list[0].ID == 1 && list[0].Name == "row1";
});

// 8. 实体 CRUD（NovaDb 嵌入库真库操作：建表/插入/查询/更新/表达式查询/删除）
Run("8.实体 CRUD（NovaDb 嵌入库）", () =>
{
    var dir = Path.Combine(Path.GetTempPath(), "aot-smoke-nova-" + Guid.NewGuid().ToString("N")[..6]);
    DAL.AddConnStr("aot_smoke", $"Data Source={dir}", null, "NovaDb");

    // 手工建表，等价反向工程产物
    DAL.Create("aot_smoke").Execute("Create Table smoke_user (ID Integer Primary Key, Name Varchar(50))");

    var e = new SmokeUser { ID = 1, Name = "aot" };
    var ok1 = e.Insert() > 0;

    var q = SmokeUser.FindByKey(1);
    var ok2 = q != null && q.Name == "aot";

    q!.Name = "aot2";
    var ok3 = q.Update() > 0;

    var list = SmokeUser.FindAll(SmokeUser._.Name == "aot2");
    var ok4 = list.Count == 1;

    var ok5 = q.Delete() > 0;

    Console.WriteLine($"    (insert={ok1} find={ok2} update={ok3} query={ok4} delete={ok5})");
    return ok1 && ok2 && ok3 && ok4 && ok5;
});

// 9. 实体缓存（插入后走实体缓存查询）
Run("9.实体缓存 FindAllWithCache", () =>
{
    var e = new SmokeUser { ID = 2, Name = "cache" };
    var n = e.Insert();

    var list = SmokeUser.FindAllWithCache();
    Console.WriteLine($"    (insert={n} cached={list.Count})");
    return list.Count >= 1;
});

// 10. 实体 LINQ（IQueryable：Where/OrderBy/ToList/Count/FirstOrDefault）
Run("10.实体 LINQ 查询", () =>
{
    var e1 = new SmokeUser { Name = "linq1" }; e1.Insert();
    var e2 = new SmokeUser { Name = "linq2" }; e2.Insert();

    var list = SmokeUser.Query.Where(e => e.ID >= e1.ID).OrderBy(e => e.ID).ToList();
    var n1 = list.Any(x => x.Name == "linq1") && list.Any(x => x.Name == "linq2");

    var cnt = SmokeUser.Query.Count(e => e.Name == "linq2");
    var n2 = cnt == 1;

    var first = SmokeUser.Query.Where(e => e.Name == "linq1").FirstOrDefault();
    var n3 = first != null && first.ID == e1.ID && first.Name == "linq1";

    Console.WriteLine($"    (id1={e1.ID} id2={e2.ID} list={list.Count} cnt={cnt} first={first?.ID})");
    return n1 && n2 && n3;
});

// 11. LINQ 闭包变量与表达式求值（GetValue/EvaluateMember 路径）
Run("11.LINQ 闭包变量与表达式求值", () =>
{
    var name = "linq2";
    var list = SmokeUser.Query.Where(e => e.Name == name).ToList();
    var n1 = list.Count == 1 && list[0].Name == "linq2";

    var cut = 0;
    var cnt = SmokeUser.Query.Count(e => e.ID >= cut + 0);
    var total = SmokeUser.FindCount();
    var n2 = cnt == total;

    var id1 = list[0].ID;
    var q = SmokeUser.Query.Where(e => e.ID >= id1).ToList();
    var n3 = q.Count >= 1 && q[0].Name == "linq2";

    Console.WriteLine($"    (closure={list.Count} arith={cnt}/{total} nested={q.Count})");
    return n1 && n2 && n3;
});

// 12. LINQ 表达式求值深水区（直接 Compile/方法调用/常量算术/分页/ToDictionary）
Run("12.LINQ 表达式求值深水区", () =>
{
    // 表达式树运行时编译（AOT 下由运行时解释器回退）
    var n0 = ProbeDirectCompile();

    // 方法调用链谓词 → GetValue 回退路径
    var name = "linq2";
    var a = SmokeUser.Query.Where(e => e.Name == name.ToUpper().ToLower()).ToList();
    var n1 = a.Count == 1 && a[0].Name == "linq2";

    // 常量算术谓词 → 二元表达式求值路径
    var cnt = SmokeUser.Query.Count(e => e.ID >= 1 + 1);
    var n2 = cnt >= 1;

    // Skip/Take 分页（NovaDb 使用 LIMIT n OFFSET m）
    var page = SmokeUser.Query.OrderBy(e => e.ID).Skip(1).Take(1).ToList();
    var n3 = page.Count <= 1;

    // ToDictionary：运行时构造 Dictionary<主键类型, 实体类型>
    var dict = SmokeUser.FindAll().ToDictionary();
    var n4 = dict.Count >= 2;

    Console.WriteLine($"    (directCompile={n0} method={a.Count} arith={cnt} page={page.Count} dict={dict.Count})");
    return n0 && n1 && n2 && n3 && n4;
});

// 13. SQLite 嵌入库（Microsoft.Data.Sqlite/SQLitePCLRaw 路线：驱动加载/建表/CRUD/反向工程）
Run("13.SQLite 嵌入库（Microsoft.Data.Sqlite）", () =>
{
    var file = Path.Combine(Path.GetTempPath(), "aot-sqlite-" + Guid.NewGuid().ToString("N")[..6] + ".db");
    DAL.AddConnStr("aot_sqlite", $"Data Source={file}", null, "SQLite");

    var dal = DAL.Create("aot_sqlite");

    // 手工建表，等价反向工程产物
    dal.Execute("Create Table smoke_sqlite_user (ID Integer Primary Key, Name Varchar(50))");

    var e = new SmokeSqliteUser { Name = "sqlite" };
    var ok1 = e.Insert() > 0 && e.ID > 0;

    var q = SmokeSqliteUser.FindByKey(e.ID);
    var ok2 = q != null && q.Name == "sqlite";

    q!.Name = "sqlite2";
    var ok3 = q.Update() > 0;

    var list = SmokeSqliteUser.FindAll(SmokeSqliteUser._.Name == "sqlite2");
    var ok4 = list.Count == 1;

    // 驱动反向工程：读取 sqlite_master 解析表结构
    var tables = dal.Tables;
    var ok5 = tables.Any(t => t.TableName.EqualIgnoreCase("smoke_sqlite_user"));

    var ok6 = q.Delete() > 0;

    Console.WriteLine($"    (factory={dal.Db.Factory?.GetType().FullName} insert={ok1} find={ok2} update={ok3} query={ok4} tables={tables.Count} delete={ok6})");
    return ok1 && ok2 && ok3 && ok4 && ok5 && ok6;
});

// 14. 泛型属性拷贝（DataConversion.CopyProperty<TSource,TTarget>：源与目标不同类型，AOT 下自动保留公开属性）
Run("14.泛型属性拷贝 CopyProperty<TSource,TTarget>", () =>
{
    var src = new SmokeUser { ID = 7, Name = "copy" };
    var dst = new SmokeSqliteUser();
    DataConversion.CopyProperty(src, dst);

    Console.WriteLine($"    (ID={dst.ID} Name={dst.Name})");
    return dst.ID == 7 && dst.Name == "copy";
});

// 15. MySQL 真库（官方 MySql.Data 连接器；连接串来自环境变量 XCode_mysql，未设置时跳过）
Run("15.MySQL 真库（环境变量 XCode_mysql）", () =>
{
    var connStr = Environment.GetEnvironmentVariable("XCode_mysql");
    if (connStr.IsNullOrEmpty())
    {
        Console.WriteLine("    (skip: 未设置 XCode_mysql)");
        return true;
    }

    DAL.AddConnStr("aot_mysql", connStr, null, "MySql");

    var dal = DAL.Create("aot_mysql");
    dal.Execute("Create Table if not exists smoke_mysql_user (ID Int Auto_Increment Primary Key, Name Varchar(50))");
    dal.Execute("Delete From smoke_mysql_user");

    var e = new SmokeMySqlUser { Name = "mysql" };
    var ok1 = e.Insert() > 0 && e.ID > 0;

    var q = SmokeMySqlUser.FindByKey(e.ID);
    var ok2 = q != null && q.Name == "mysql";

    q!.Name = "mysql2";
    var ok3 = q.Update() > 0;

    var list = SmokeMySqlUser.FindAll(SmokeMySqlUser._.Name == "mysql2");
    var ok4 = list.Count == 1;

    // DAL 层查询（走驱动 OnFill）
    var dt = dal.Query("select * from smoke_mysql_user");
    var ok5 = dt != null && dt.Rows.Count == 1;

    var ok6 = q.Delete() > 0;

    Console.WriteLine($"    (factory={dal.Db.Factory?.GetType().FullName} insert={ok1} find={ok2} update={ok3} query={ok4} dalQuery={ok5} delete={ok6})");
    return ok1 && ok2 && ok3 && ok4 && ok5 && ok6;
});

// 16. SqlServer 真库（官方 System.Data.SqlClient 连接器，与 XCode.SqlServer 适配包同版本；连接串来自环境变量 XCode_sqlserver，未设置时跳过。本机可用 LocalDB：(localdb)\MSSQLLocalDB）
Run("16.SqlServer 真库（环境变量 XCode_sqlserver）", () =>
{
    var connStr = Environment.GetEnvironmentVariable("XCode_sqlserver");
    if (connStr.IsNullOrEmpty())
    {
        Console.WriteLine("    (skip: 未设置 XCode_sqlserver)");
        return true;
    }

    DAL.AddConnStr("aot_sqlserver", connStr, null, "SqlServer");

    var dal = DAL.Create("aot_sqlserver");
    dal.Execute("if object_id('smoke_sqlserver_user') is null Create Table smoke_sqlserver_user (ID Int Identity(1,1) Primary Key, Name Varchar(50))");
    dal.Execute("Delete From smoke_sqlserver_user");

    var e = new SmokeSqlServerUser { Name = "sqlserver" };
    var ok1 = e.Insert() > 0 && e.ID > 0;

    var q = SmokeSqlServerUser.FindByKey(e.ID);
    var ok2 = q != null && q.Name == "sqlserver";

    q!.Name = "sqlserver2";
    var ok3 = q.Update() > 0;

    var list = SmokeSqlServerUser.FindAll(SmokeSqlServerUser._.Name == "sqlserver2");
    var ok4 = list.Count == 1;

    // DAL 层查询（走驱动 OnFill）
    var dt = dal.Query("select * from smoke_sqlserver_user");
    var ok5 = dt != null && dt.Rows.Count == 1;

    var ok6 = q.Delete() > 0;

    Console.WriteLine($"    (factory={dal.Db.Factory?.GetType().FullName} insert={ok1} find={ok2} update={ok3} query={ok4} dalQuery={ok5} delete={ok6})");
    return ok1 && ok2 && ok3 && ok4 && ok5 && ok6;
});

Console.WriteLine(failures.Count == 0 ? "全部通过" : $"失败 {failures.Count}：{String.Join("、", failures)}");
return failures.Count == 0 ? 0 : 1;

/// <summary>冒烟实体（手写最小生成代码形态，字段/索引器/字段快捷方式与 xcode 生成代码一致）</summary>
[Serializable]
[DataObject]
[Description("冒烟用户")]
[BindTable("smoke_user", Description = "冒烟用户", ConnName = "aot_smoke")]
public class SmokeUser : Entity<SmokeUser>
{
    private Int32 _ID;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("ID", "编号", "")]
    public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

    private String? _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object? this[String name]
    {
        get => name switch
        {
            "ID" => _ID,
            "Name" => _Name,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "ID": _ID = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }

    /// <summary>取得冒烟用户字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field ID = FindByName("ID");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }
}

/// <summary>SQLite 冒烟实体（与 SmokeUser 同构，绑定 SQLite 连接）</summary>
[Serializable]
[DataObject]
[Description("SQLite 冒烟用户")]
[BindTable("smoke_sqlite_user", Description = "SQLite 冒烟用户", ConnName = "aot_sqlite")]
public class SmokeSqliteUser : Entity<SmokeSqliteUser>
{
    private Int32 _ID;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("ID", "编号", "")]
    public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

    private String? _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object? this[String name]
    {
        get => name switch
        {
            "ID" => _ID,
            "Name" => _Name,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "ID": _ID = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }

    /// <summary>取得 SQLite 冒烟用户字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field ID = FindByName("ID");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }
}

/// <summary>MySQL 冒烟实体（与 SmokeUser 同构，绑定 MySQL 连接）</summary>
[Serializable]
[DataObject]
[Description("MySQL 冒烟用户")]
[BindTable("smoke_mysql_user", Description = "MySQL 冒烟用户", ConnName = "aot_mysql")]
public class SmokeMySqlUser : Entity<SmokeMySqlUser>
{
    private Int32 _ID;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("ID", "编号", "")]
    public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

    private String? _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object? this[String name]
    {
        get => name switch
        {
            "ID" => _ID,
            "Name" => _Name,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "ID": _ID = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }

    /// <summary>取得 MySQL 冒烟用户字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field ID = FindByName("ID");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }
}

/// <summary>SqlServer 冒烟实体（与 SmokeUser 同构，绑定 SqlServer 连接）</summary>
[Serializable]
[DataObject]
[Description("SqlServer 冒烟用户")]
[BindTable("smoke_sqlserver_user", Description = "SqlServer 冒烟用户", ConnName = "aot_sqlserver")]
public class SmokeSqlServerUser : Entity<SmokeSqlServerUser>
{
    private Int32 _ID;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("ID", "编号", "")]
    public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

    private String? _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object? this[String name]
    {
        get => name switch
        {
            "ID" => _ID,
            "Name" => _Name,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "ID": _ID = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }

    /// <summary>取得 SqlServer 冒烟用户字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field ID = FindByName("ID");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }
}