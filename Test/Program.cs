using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Caching;
using NewLife.Configuration;
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;
using NewLife.Security;
using NewLife.Serialization;
using Stardust;
using XCode;
using XCode.Cache;
using XCode.Code;
using XCode.DataAccessLayer;
using XCode.Membership;

namespace Test;

public class Program
{
    private static void Main(String[] args)
    {
        //Environment.SetEnvironmentVariable("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "1");

        XTrace.UseConsole();

        var star = new StarFactory(null, null, null);
        DefaultTracer.Instance = star?.Tracer;
        //(star.Tracer as StarTracer).AttachGlobal();

#if DEBUG
        XTrace.Debug = true;
        XTrace.Log.Level = LogLevel.All;

        var set = NewLife.Setting.Current;
        set.Debug = true;
        set.LogLevel = LogLevel.All;

        //new LogEventListener(new[] {
        //    "System.Runtime",
        //    "System.Diagnostics.Eventing.FrameworkEventSource",
        //    "System.Transactions.TransactionsEventSource",
        //    "Microsoft-Windows-DotNETRuntime",
        //    //"Private.InternalDiagnostics.System.Net.Sockets",
        //    "System.Net.NameResolution",
        //    //"Private.InternalDiagnostics.System.Net.NameResolution",
        //    "System.Net.Sockets",
        //    //"Private.InternalDiagnostics.System.Net.Http",
        //    "System.Net.Http",
        //    //"System.Data.DataCommonEventSource",
        //    //"Microsoft-Diagnostics-DiagnosticSource",
        //});

        var set2 = XCode.Setting.Current;
        set2.Debug = true;
#endif
        while (true)
        {
            var sw = Stopwatch.StartNew();
#if !DEBUG
            try
            {
#endif
                Test7();
#if !DEBUG
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex?.GetTrue());
            }
#endif

            sw.Stop();
            Console.WriteLine("OK! 耗时 {0}", sw.Elapsed);
            //Thread.Sleep(5000);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var key = Console.ReadKey(true);
            if (key.Key != ConsoleKey.C) break;
        }
    }

    private static void Test1()
    {
        //var td = DAL.Create("tdengine");
        //var tables = td.Tables;
        //XTrace.WriteLine(tables.ToJson(true));

        //var dt = td.Query("select * from t;");
        //XTrace.WriteLine(dt.Total + "");

        var guid = new Guid("00ac7f06-4612-4791-9c84-e221a2d963ad");
        var buf = guid.ToByteArray();
        XTrace.WriteLine(buf.ToHex());

        var dal = DAL.Create("test");

        var rs = dal.RestoreAll($"../dbbak.zip", null);

        //var tables = DAL.Import(File.ReadAllText("../data/lawyer.xml".GetFullPath()));
        //var table = tables.FirstOrDefault(e => e.Name == "SpringSession");
        //var dc1 = table.Columns[0];
        //var dc2 = table.Columns[1];
        //dc1.DataType = typeof(Guid);
        //dc2.DataType = typeof(Guid);
        //dal.Restore($"../data/SpringSession.table", table);

        //var dt = dal.Query("select * from spring_session");
        //XTrace.WriteLine("字段[{0}]：{1}", dt.Columns.Length, dt.Columns.Join());
        //XTrace.WriteLine("类型[{0}]：{1}", dt.Types.Length, dt.Types.Join(",", e => e?.Name));
    }

    private static void Test2()
    {
        //var file = "http://x.newlifex.com/Area.csv.gz";

        //Area.Meta.Session.Truncate();
        //var rs = Area.Import(file, true, 3);

        var ar = Area.FindByID(710000);
        var list = ar.Childs;
    }

    private static void Test7()
    {
        var config = new HttpConfigProvider
        {
            Server = "http://star.newlifex.com:6600",
            AppId = "Test",
            Period = 5,
        };
        //config.LoadAll();
        DAL.SetConfig(config);
        //DAL.GetConfig = config.GetConfig;

        XCode.XCodeSetting.Current.Migration = Migration.Full;
        //Role.Meta.Session.Dal.Db.Migration = Migration.Full;
        //DAL.AddConnStr("membership", "Server=10.0.0.3;Port=3306;Database=Membership;Uid=root;Pwd=Pass@word;", null, "mysql");

        var dal = Role.Meta.Session.Dal;
        XTrace.WriteLine("dal={0}", dal.DbType);
        XTrace.WriteLine("db={0}", dal.Db.ServerVersion);

        Role.Meta.Session.Dal.Db.ShowSQL = true;
        Role.Meta.Session.Dal.Expire = 10;
        //Role.Meta.Session.Dal.Db.Readonly = true;

        var list = Role.FindAll();
        Console.WriteLine(list.Count);

        list = Role.FindAll(Role._.Name.NotContains("abc"));
        Console.WriteLine(list.Count);

        Thread.Sleep(1000);

        list = Role.FindAll();
        Console.WriteLine(list.Count);

        Thread.Sleep(1000);

        var r = list.Last();
        r.IsSystem = !r.IsSystem;
        r.Update();

        Thread.Sleep(5000);

        list = Role.FindAll();
        Console.WriteLine(list.Count);
    }

    private static async void Test8()
    {
        var di = "Plugins".AsDirectory();
        if (di.Exists) di.Delete(true);

        //var db = DbFactory.Create(DatabaseType.MySql);
        //var db = DbFactory.Create(DatabaseType.PostgreSQL);
        var db = DbFactory.Create(DatabaseType.SQLite);
        var factory = db.Factory;
    }

    private static void Test9()
    {
        var cache = new SingleEntityCache<Int32, User> { Expire = 1 };

        // 首次访问
        var user = cache[1];
        XTrace.WriteLine("cache.Success={0}", cache.Success);

        user = cache[1];
        XTrace.WriteLine("cache.Success={0}", cache.Success);

        user = cache[1];
        XTrace.WriteLine("cache.Success={0}", cache.Success);

        EntityFactory.InitAll();

        XTrace.WriteLine("TestRole");
        var r0 = Role.FindByName("Stone");
        r0?.Delete();

        var r = new Role
        {
            Name = "Stone"
        };
        r.Insert();

        var r2 = Role.FindByName("Stone");
        XTrace.WriteLine("FindByName: {0}", r2.ToJson());

        r.Enable = true;
        r.Update();

        var r3 = Role.Find(Role._.Name == "STONE");
        XTrace.WriteLine("Find: {0}", r3.ToJson());

        r.Delete();

        var n = Role.FindCount();
        XTrace.WriteLine("count={0}", n);
    }

    /// <summary>测试序列化</summary>
    private static void Test12()
    {
        var option = new EntityBuilderOption();
        var tables = ClassBuilder.LoadModels("../../NewLife.Cube/CubeDemoNC/Areas/School/Models/Model.xml", option, out var atts);
        EntityBuilder.BuildTables(tables, option);
    }

    private static void Test16()
    {
        var f = "财务数据库.zip";
        var f2 = "财务数据库/凭证库.table";
        var f3 = "cw.zip";
        var dal = DAL.Create("caiwu");

        //var tables = dal.RestoreAll(f, null, true, false);

        //dal.Db.BatchSize = 100;

        var dpk = new DbPackage
        {
            Dal = dal,
            IgnoreError = false,
            Log = XTrace.Log
        };
        //var ts = DAL.ImportFrom("财务数据库/xxgk2.xml");
        //var tables = dpk.Restore(f2, ts[0], true);
        var tables = dpk.RestoreAll(f3, null, true);

        //dal.BackupAll(tables, "cw.zip");
    }
}