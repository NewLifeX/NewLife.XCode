// 本机数据库启动器。.NET 10+ 文件应用：dotnet run tools/localdb/localdb.cs -- <命令>
// 支持 Windows 便携版（下载到空余空间最多的磁盘）与 WSL systemd / Docker 委托启动。
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

const String Distro = "Ubuntu-24.04";
const String KeepAliveVbs = @"C:\X\LocalDatabases\keepalive.vbs";
const String SettingsFile = "settings.ini";

var databases = new DbDef[]
{
    new("prometheus", "Prometheus", "zip", "2.45.3", 9090,
        Url: "https://github.com/prometheus/prometheus/releases/download/v2.45.3/prometheus-2.45.3.windows-amd64.zip",
        Mirrors: ["https://ghfast.top/{url}", "https://gh-proxy.com/{url}", "https://ghproxy.net/{url}"],
        Exe: "prometheus.exe",
        Args: "--config.file=\"{app}\\prometheus.yml\" --storage.tsdb.path=\"{data}\" --web.listen-address=127.0.0.1:{port}",
        ConfigFile: "prometheus.yml",
        ConfigContent: "global:\n  scrape_interval: 15s\nscrape_configs:\n  - job_name: prometheus\n    static_configs:\n      - targets: [\"127.0.0.1:{port}\"]\n",
        ConnStr: "Server=http://127.0.0.1:9090"),
    new("victoriametrics", "VictoriaMetrics", "zip", "1.156.0", 8428,
        Url: "https://github.com/VictoriaMetrics/VictoriaMetrics/releases/download/v1.156.0/victoria-metrics-windows-amd64-v1.156.0.zip",
        Mirrors: ["https://ghfast.top/{url}", "https://gh-proxy.com/{url}", "https://ghproxy.net/{url}"],
        Exe: "victoria-metrics*.exe",
        Args: "-storageDataPath=\"{data}\" -httpListenAddr=127.0.0.1:{port}",
        ConnStr: "Server=http://127.0.0.1:8428"),
    new("cockroachdb", "CockroachDB", "zip", "v24.1.5", 26257, 8089,
        Url: "https://binaries.cockroachdb.com/cockroach-v24.1.5.windows-amd64.zip",
        Exe: "cockroach.exe",
        Args: "start-single-node --insecure --store=\"{data}\" --listen-addr=127.0.0.1:{port} --http-addr=127.0.0.1:{httpPort} --max-offset=5s",
        ConnStr: "Server=127.0.0.1;Port=26257;Database=defaultdb;Uid=root;Pwd="),
    new("mongodb", "MongoDB", "zip", "7.0.14", 27017,
        Url: "https://fastdl.mongodb.org/windows/mongodb-windows-x86_64-7.0.14.zip",
        Exe: "mongod.exe",
        Args: "--dbpath \"{data}\" --port {port} --bind_ip 127.0.0.1",
        ConnStr: "mongodb://127.0.0.1:27017/test"),
    new("tdengine", "TDengine", "wsl-systemd", "3.3.6", 6041,
        Service: "taosd taosadapter",
        ConnStr: "Server=127.0.0.1;Port=6041;Database=db;user=root;password=taosdata"),
    new("clickhouse", "ClickHouse", "wsl-docker", "24.8", 8123,
        Container: "clickhouse",
        Image: "docker.m.daocloud.io/clickhouse/clickhouse-server:24.8",
        RunArgs: "-e CLICKHOUSE_USER=default -e CLICKHOUSE_PASSWORD=clickhouse -v clickhouse_data:/var/lib/clickhouse",
        Ports: "8123:8123 9000:9000",
        ConnStr: "Host=127.0.0.1;Port=8123;Database=default;Username=default;Password=clickhouse"),
    new("cratedb", "CrateDB", "wsl-docker", "5.10.4", 15432,
        Container: "cratedb",
        Image: "docker.m.daocloud.io/crate:5.10.4",
        RunArgs: "-v cratedb_data:/data",
        Ports: "15432:5432 4200:4200",
        ConnStr: "Server=127.0.0.1;Port=15432;Database=doc;Uid=crate;Pwd="),
    new("questdb", "QuestDB", "wsl-docker", "10.2.0", 8812,
        Container: "questdb",
        Image: "docker.1ms.run/questdb/questdb:latest",
        RunArgs: "-v questdb_data:/var/lib/questdb",
        Ports: "8812:8812 9001:9000",
        ConnStr: "Server=127.0.0.1;Port=8812;Database=qdb;Uid=admin;Pwd=quest"),
    new("presto", "PrestoDB", "wsl-cmd", "0.300", 8081,
        StartCmd: "sudo systemctl stop presto-live >/dev/null 2>&1; sudo systemd-run --unit=presto-live --collect --setenv=JAVA_HOME=/usr/lib/jvm/java-17-openjdk-amd64 --working-directory=/opt/dbservers/presto-0.300 /opt/dbservers/presto-0.300/bin/launcher run",
        StopCmd: "sudo systemctl stop presto-live || true",
        ConnStr: "Host=127.0.0.1;Port=8081;Catalog=tpch;Schema=tiny;User=user"),
    new("trino", "Trino", "wsl-cmd", "476", 8080,
        StartCmd: "cd /opt/dbservers/trino-476 && JAVA_HOME=/opt/dbservers/jdk25 bin/launcher start",
        StopCmd: "cd /opt/dbservers/trino-476 && JAVA_HOME=/opt/dbservers/jdk25 bin/launcher stop || true",
        ConnStr: "Host=127.0.0.1;Port=8080;Catalog=tpch;Schema=tiny;User=user"),
    new("firebird", "Firebird", "wsl-systemd", "3.0.11", 3050,
        Service: "firebird3.0",
        ConnStr: "Database=/var/lib/firebird/3.0/data/test.fdb;DataSource=127.0.0.1;Port=3050;User=SYSDBA;Password=masterkey;Dialect=3"),
    new("monetdb", "MonetDB", "wsl-cmd", "11.55", 50000,
        StartCmd: "runuser -u monetdb -- monetdbd start /var/lib/monetdb || true",
        StopCmd: "runuser -u monetdb -- monetdbd stop /var/lib/monetdb || true",
        ConnStr: "Host=127.0.0.1;Port=50000;Database=demo;User=monetdb;Password=monetdb"),
    new("duckdb", "DuckDB", "embedded", "1.5.6", 0,
        Note: "嵌入式数据库，无需启动；NuGet 包 DuckDB.NET.Data.Full", ConnStr: "Data Source=<path>.duckdb"),
};

var settings = LoadSettings();
var root = settings.Root ?? DefaultRoot();
if (settings.Root == null)
{
    settings.Root = root;
    SaveSettings(settings);
}

var cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "list";
var name = args.Length > 1 && !args[1].StartsWith('-') ? args[1].ToLowerInvariant() : null;
var force = args.Any(e => e is "--yes" or "-y");
var extra = args.Skip(1).Where(e => !e.StartsWith('-')).Skip(name == null ? 0 : 1).ToArray();

try
{
    switch (cmd)
    {
        case "list" or "ls":
            ListAll();
            break;
        case "root":
            if (name != null)
            {
                var path = Path.GetFullPath(args[1]);
                settings.Root = path;
                root = path;
                SaveSettings(settings);
                Console.WriteLine($"安装根目录已设为 {root}");
            }
            Console.WriteLine($"安装根目录：{root}（空余 {FreeGb(root):N1} GB）");
            foreach (var d in DriveInfo.GetDrives().Where(e => e.DriveType == DriveType.Fixed && e.IsReady).OrderByDescending(e => e.AvailableFreeSpace))
                Console.WriteLine($"  {d.Name,-4} 空余 {d.AvailableFreeSpace / 1024d / 1024 / 1024:N1} GB");
            break;
        case "start":
            RequireName();
            StartDb(Find(name!));
            break;
        case "stop":
            RequireName();
            StopDb(Find(name!));
            break;
        case "restart":
            RequireName();
            StopDb(Find(name!));
            StartDb(Find(name!));
            break;
        case "status":
            if (name == null) { ListAll(); break; }
            Status(Find(name));
            break;
        case "install":
            RequireName();
            Install(Find(name!));
            break;
        case "clean":
            CleanWindows();
            break;
        case "clean-wsl":
            CleanWsl();
            break;
        case "wsl-move":
            var targetArg = args.Length > 1 && !args[1].StartsWith('-') ? args[1] : null;
            MoveWsl(targetArg ?? @"E:\WSL\Ubuntu-24.04", force);
            break;
        case "help" or "--help" or "-h":
            Help();
            break;
        default:
            Console.WriteLine($"未知命令 {cmd}，使用 help 查看帮助。");
            return 1;
    }

    return 0;
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("失败：" + ex.Message);
    Console.ResetColor();
    return 1;
}

void RequireName()
{
    if (name == null) throw new ArgumentException("请指定数据库名称，例如：start prometheus");
}

DbDef Find(String id) => databases.FirstOrDefault(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
    ?? throw new ArgumentException($"未知数据库 {id}，可用：{String.Join(", ", databases.Select(e => e.Id))}");

void ListAll()
{
    Console.WriteLine($"安装根目录：{root}");
    Console.WriteLine($"{"ID",-16}{"名称",-16}{"方式",-12}{"版本",-10}{"端口",-8}{"已安装",-8}{"运行中",-8}连接串");
    foreach (var db in databases)
    {
        Boolean? installed = db.Kind is "zip" ? Directory.Exists(AppDir(db)) : db.Kind is "embedded" ? true : null;
        var running = IsRunning(db);
        Console.WriteLine($"{db.Id,-16}{db.Name,-16}{db.Kind,-12}{db.Version,-10}{db.Port,-8}{(installed == null ? "-" : installed.Value ? "是" : "否"),-8}{(running ? "是" : "否"),-8}{db.ConnStr}");
    }
}

void Status(DbDef db)
{
    Console.WriteLine($"{db.Name} ({db.Id}) 方式={db.Kind} 版本={db.Version} 端口={db.Port}");
    if (db.Kind == "zip")
    {
        Console.WriteLine($"安装目录：{AppDir(db)}（{(Directory.Exists(AppDir(db)) ? "已安装" : "未安装")}）");
        Console.WriteLine($"数据目录：{DataDir(db)}");
    }
    Console.WriteLine($"运行状态：{(IsRunning(db) ? "运行中" : "未运行")}");

    var pidFile = PidFile(db);
    if (File.Exists(pidFile))
    {
        var pid = File.ReadAllText(pidFile).Trim();
        var p = TryGetProcessByText(pid);
        Console.WriteLine($"进程：{pid}（{(p == null ? "已退出" : p.ProcessName)}）");
    }

    if (db.Note != null) Console.WriteLine("说明：" + db.Note);
    if (db.ConnStr != null) Console.WriteLine("连接串：" + db.ConnStr);
}

void StartDb(DbDef db)
{
    if (db.Kind == "embedded")
    {
        Console.WriteLine($"{db.Name} 是嵌入式数据库，无需启动。{db.Note}");
        return;
    }

    if (db.Kind != "zip")
    {
        StartWsl(db);
        WaitPort(db, 60);
        Report(db);
        return;
    }

    Install(db);
    if (IsRunning(db))
    {
        Console.WriteLine($"{db.Name} 已在运行。");
        Report(db);
        return;
    }

    Directory.CreateDirectory(DataDir(db));
    if (db.ConfigFile != null && db.ConfigContent != null)
    {
        var cfg = Path.Combine(AppDir(db), db.ConfigFile);
        File.WriteAllText(cfg, Expand(db.ConfigContent, db), new UTF8Encoding(false));
    }

    var exe = ResolveExe(db);
    var argLine = Expand(db.Args ?? "", db);
    var log = LogFile(db);
    Directory.CreateDirectory(Path.GetDirectoryName(log)!);

    // 通过启动脚本重定向日志，工具退出后进程继续运行
    var launcher = Path.Combine(StateDir(), db.Id + ".cmd");
    Directory.CreateDirectory(StateDir());
    File.WriteAllText(launcher,
        "@echo off\r\n" +
        $"cd /d \"{AppDir(db)}\"\r\n" +
        $"\"{exe}\" {argLine} >> \"{log}\" 2>&1\r\n", new UTF8Encoding(false));

    // ShellExecute 启动可脱离调用方 stdout，否则调用方的管道会一直挂起
    var psi = new ProcessStartInfo(launcher)
    {
        UseShellExecute = true,
        WindowStyle = ProcessWindowStyle.Hidden,
        WorkingDirectory = AppDir(db),
    };

    var p = Process.Start(psi) ?? throw new InvalidOperationException("无法启动进程");
    File.WriteAllText(PidFile(db), p.Id.ToString());

    Console.WriteLine($"已启动 {db.Name}，PID={p.Id}，日志：{log}");
    try
    {
        WaitPort(db, 90, p);
    }
    catch
    {
        Console.WriteLine("启动后端口未就绪，日志末尾：");
        Tail(log, 25);
        throw;
    }

    Report(db);
}

void StopDb(DbDef db)
{
    if (db.Kind == "embedded") return;

    if (db.Kind != "zip")
    {
        StopWsl(db);
        Console.WriteLine($"{db.Name} 已停止。");
        return;
    }

    var pidFile = PidFile(db);
    if (File.Exists(pidFile))
    {
        var p = TryGetProcessByText(File.ReadAllText(pidFile).Trim());
        if (p != null)
        {
            p.Kill(true);
            p.WaitForExit(10_000);
        }
        File.Delete(pidFile);
    }

    // 按端口兜底清理
    foreach (var p in FindByPort(db.Port))
    {
        try { p.Kill(true); } catch { }
    }

    Console.WriteLine($"{db.Name} 已停止。");
}

void StartWsl(DbDef db)
{
    switch (db.Kind)
    {
        case "wsl-systemd":
            Wsl($"sudo systemctl start {db.Service}", true);
            break;
        case "wsl-cmd":
            Wsl(db.StartCmd!, true);
            break;
        case "wsl-docker":
            var exists = Wsl($"docker inspect -f '{{{{.State.Running}}}}' {db.Container} 2>/dev/null", false).Trim();
            if (exists.Length == 0 || exists.Contains("No such"))
            {
                var ports = String.Join(" ", (db.Ports ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(e => "-p " + e));
                Wsl($"docker run -d --name {db.Container} --restart unless-stopped {ports} {db.RunArgs} {db.Image}", true);
            }
            else if (!exists.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                Wsl($"docker start {db.Container}", true);
            }
            break;
    }
    Console.WriteLine($"已请求启动 {db.Name}（{db.Kind}）。");
}

void StopWsl(DbDef db)
{
    switch (db.Kind)
    {
        case "wsl-systemd":
            Wsl($"sudo systemctl stop {db.Service}", false);
            break;
        case "wsl-cmd":
            Wsl(db.StopCmd!, false);
            break;
        case "wsl-docker":
            Wsl($"docker stop {db.Container}", false);
            break;
    }
}

void Install(DbDef db)
{
    if (db.Kind != "zip") return;

    var app = AppDir(db);
    var marker = Path.Combine(app, ".installed");
    if (File.Exists(marker) && File.ReadAllText(marker).Trim() == db.Version)
    {
        Console.WriteLine($"{db.Name} {db.Version} 已安装：{app}");
        return;
    }

    Directory.CreateDirectory(app);
    var archive = Path.Combine(DownloadDir(), $"{db.Id}-{db.Version}.zip");
    if (!File.Exists(archive) || new FileInfo(archive).Length == 0) Download(db, archive);

    Console.WriteLine($"解压到 {app} ...");
    ZipFile.ExtractToDirectory(archive, app, true);
    File.WriteAllText(marker, db.Version);
    Console.WriteLine($"安装完成：{app}");
}

void Download(DbDef db, String archive)
{
    Directory.CreateDirectory(DownloadDir());
    // GitHub 直连常被限速，镜像优先
    var urls = new List<String>();
    var mirrors = db.Mirrors;
    if (mirrors != null) urls.AddRange(mirrors.Select(e => e.Replace("{url}", db.Url!)));
    urls.Add(db.Url!);

    var part = archive + ".part";
    Exception? last = null;
    foreach (var url in urls)
    {
        try
        {
            Console.WriteLine($"下载 {url}");
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("LocalDbTool/1.0");
            using var resp = http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            resp.EnsureSuccessStatusCode();

            var total = resp.Content.Headers.ContentLength ?? 0;
            using var input = resp.Content.ReadAsStream();
            using var output = File.Create(part);
            var buffer = new Byte[81920];
            Int64 read = 0;
            var lastReport = DateTime.UtcNow;
            while (true)
            {
                var n = input.Read(buffer, 0, buffer.Length);
                if (n <= 0) break;
                output.Write(buffer, 0, n);
                read += n;
                if ((DateTime.UtcNow - lastReport).TotalSeconds >= 2)
                {
                    lastReport = DateTime.UtcNow;
                    Console.WriteLine(total > 0 ? $"  {read / 1024d / 1024:N1}/{total / 1024d / 1024:N1} MB ({read * 100d / total:N0}%)" : $"  {read / 1024d / 1024:N1} MB");
                }
            }
            output.Close();
            if (total > 0 && read != total) throw new IOException($"下载不完整 {read}/{total}");
            File.Move(part, archive, true);
            return;
        }
        catch (Exception ex)
        {
            last = ex;
            Console.WriteLine($"  失败：{ex.Message}");
            try { File.Delete(part); } catch { }
        }
    }

    throw new IOException($"所有下载源均失败：{last?.Message}");
}

void CleanWindows()
{
    Int64 freed = 0;
    if (Directory.Exists(DownloadDir()))
    {
        foreach (var f in Directory.GetFiles(DownloadDir(), "*.part"))
        {
            freed += new FileInfo(f).Length;
            File.Delete(f);
        }
    }

    if (Directory.Exists(Path.Combine(root, "apps")))
    {
        foreach (var dir in Directory.GetDirectories(Path.Combine(root, "apps")))
        {
            var db = databases.FirstOrDefault(e => e.Id.Equals(Path.GetFileName(dir), StringComparison.OrdinalIgnoreCase));
            if (db == null) continue;
            foreach (var ver in Directory.GetDirectories(dir))
            {
                if (Path.GetFileName(ver).Equals(db.Version, StringComparison.OrdinalIgnoreCase)) continue;
                freed += DirSize(ver);
                Directory.Delete(ver, true);
            }
        }
    }

    foreach (var f in Directory.Exists(StateDir()) ? Directory.GetFiles(StateDir(), "*.pid") : [])
    {
        if (TryGetProcessByText(File.ReadAllText(f).Trim()) == null) File.Delete(f);
    }

    Console.WriteLine($"Windows 侧清理完成，释放 {freed / 1024d / 1024:N1} MB。");
}

void CleanWsl()
{
    Console.WriteLine("清理 WSL 缓存与安装包（不删除数据库数据）...");
    var script = """
        set -u
        echo "== 清理前 =="
        df -h / | tail -1
        sudo apt-get clean
        sudo journalctl --vacuum-size=256M >/dev/null 2>&1 || true
        sudo rm -f /opt/pkgs/*.tar.gz /opt/pkgs/*.tgz 2>/dev/null || true
        sudo find /var/log/taos -maxdepth 1 -name 'taos*.log*' -mtime +3 -delete 2>/dev/null || true
        sudo find /var/log -maxdepth 1 -name '*.gz' -mtime +7 -delete 2>/dev/null || true
        docker image prune -f >/dev/null 2>&1 || true
        if docker image inspect crate:5.10.4 >/dev/null 2>&1 && docker inspect -f '{{.Config.Image}}' cratedb 2>/dev/null | grep -q 'crate:5.10.4'; then
          docker rmi docker.m.daocloud.io/crate:5.10.4 >/dev/null 2>&1 || true
        fi
        echo "== 清理后 =="
        df -h / | tail -1
        echo "== 可人工确认的目录 =="
        sudo du -sh /root/models /root/a4build /root/sd /root/rknn-venv /opt/rf '/root/C:UsersStone' 2>/dev/null || true
        """;
    var output = Wsl(script, true);
    Console.WriteLine(output);
}

void MoveWsl(String target, Boolean yes)
{
    if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("仅支持 Windows");
    var source = GetWslVhdx();
    var targetFull = Path.GetFullPath(target);
    var drive = new DriveInfo(Path.GetPathRoot(targetFull)!);
    var vhdxSize = new FileInfo(source).Length;

    Console.WriteLine($"当前 WSL 磁盘：{source}（{vhdxSize / 1024d / 1024 / 1024:N1} GB）");
    Console.WriteLine($"目标位置：{targetFull}（{drive.Name} 空余 {drive.AvailableFreeSpace / 1024d / 1024 / 1024:N1} GB）");
    if (drive.Name.Equals(Path.GetPathRoot(source), StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("目标磁盘与当前位置相同，无需移动");
    if (drive.AvailableFreeSpace < vhdxSize + 5L * 1024 * 1024 * 1024)
        throw new InvalidOperationException("目标磁盘空余不足（需大于 VHDX 大小 + 5GB）");

    if (!yes)
    {
        Console.WriteLine("该操作会关闭所有 WSL 数据库。确认请追加 --yes：");
        Console.WriteLine($"  dotnet run tools/localdb/localdb.cs -- wsl-move \"{targetFull}\" --yes");
        return;
    }

    Console.WriteLine("关闭 WSL ...");
    Run("wsl.exe", ["--shutdown"], null, true);
    Thread.Sleep(3000);

    Console.WriteLine("移动磁盘，请耐心等待 ...");
    var code = Run("wsl.exe", ["--manage", Distro, "--move", targetFull], null, false);
    if (code != 0) throw new InvalidOperationException($"wsl --move 失败，退出码 {code}");

    Run("wsl.exe", ["--manage", Distro, "--set-sparse", "true"], null, false);

    Console.WriteLine("重新启动 WSL 与数据库 ...");
    Run("wsl.exe", ["-d", Distro, "--", "bash", "-lc", "true"], null, true);
    Thread.Sleep(2000);
    if (File.Exists(KeepAliveVbs)) Process.Start(new ProcessStartInfo("wscript.exe", $"\"{KeepAliveVbs}\"") { UseShellExecute = true });

    foreach (var db in databases.Where(e => e.Kind.StartsWith("wsl") && e.Port > 0))
    {
        try { StartWsl(db); } catch (Exception ex) { Console.WriteLine($"{db.Id} 启动失败：{ex.Message}"); }
    }

    Console.WriteLine("完成。WSL 新位置：" + GetWslVhdx());
}

String GetWslVhdx()
{
    using var sub = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Lxss");
    if (sub != null)
    {
        foreach (var name in sub.GetSubKeyNames())
        {
            using var k = sub.OpenSubKey(name);
            if (k?.GetValue("DistributionName") as String != Distro) continue;
            var basePath = k.GetValue("BasePath") as String ?? "";
            if (basePath.StartsWith(@"\\?\")) basePath = basePath[4..];
            return Path.Combine(basePath, "ext4.vhdx");
        }
    }
    throw new InvalidOperationException("未找到 WSL 磁盘位置");
}

void Report(DbDef db)
{
    Console.WriteLine();
    Console.WriteLine($"{db.Name} 运行中，端口 {db.Port}");
    if (db.ConnStr != null) Console.WriteLine("连接串：" + db.ConnStr);
}

Boolean IsRunning(DbDef db)
{
    if (db.Kind == "embedded") return false;
    if (db.Kind == "zip")
    {
        var pidFile = PidFile(db);
        if (File.Exists(pidFile) && TryGetProcessByText(File.ReadAllText(pidFile).Trim()) != null) return true;
    }
    return db.Port > 0 && CanConnect("127.0.0.1", db.Port);
}

void WaitPort(DbDef db, Int32 seconds, Process? process = null)
{
    if (db.Port <= 0) return;
    var end = DateTime.UtcNow.AddSeconds(seconds);
    while (DateTime.UtcNow < end)
    {
        if (process != null && process.HasExited) throw new InvalidOperationException($"进程已退出，退出码 {process.ExitCode}");
        if (CanConnect("127.0.0.1", db.Port)) return;
        Thread.Sleep(500);
    }
    throw new TimeoutException($"{db.Name} 端口 {db.Port} 在 {seconds} 秒内未就绪");
}

static Boolean CanConnect(String host, Int32 port)
{
    try
    {
        using var client = new System.Net.Sockets.TcpClient();
        return client.ConnectAsync(host, port).Wait(800) && client.Connected;
    }
    catch { return false; }
}

String Wsl(String command, Boolean throwOnError)
{
    command = command.Replace("\r\n", "\n");
    var psi = new ProcessStartInfo("wsl.exe")
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
    };
    foreach (var a in new[] { "-d", Distro, "--", "bash", "-lc", command }) psi.ArgumentList.Add(a);

    using var p = Process.Start(psi)!;
    var stdout = p.StandardOutput.ReadToEnd();
    var stderr = p.StandardError.ReadToEnd();
    p.WaitForExit(120_000);
    if (throwOnError && p.ExitCode != 0) throw new InvalidOperationException($"WSL 命令失败（{p.ExitCode}）：{stderr.Trim()}");
    return stdout + stderr;
}

static Int32 Run(String file, String[] arguments, String? workDir, Boolean throwOnError)
{
    var psi = new ProcessStartInfo(file)
    {
        UseShellExecute = false,
        CreateNoWindow = false,
        WorkingDirectory = workDir ?? Environment.CurrentDirectory,
    };
    foreach (var a in arguments) psi.ArgumentList.Add(a);

    using var p = Process.Start(psi)!;
    p.WaitForExit();
    if (throwOnError && p.ExitCode != 0) throw new InvalidOperationException($"{file} 退出码 {p.ExitCode}");
    return p.ExitCode;
}

static Process? TryGetProcessByText(String pid) => Int32.TryParse(pid, out var id) ? TryGetProcessByPid(id) : null;

static Process? TryGetProcessByPid(Int32 pid)
{
    try { return Process.GetProcessById(pid); } catch { return null; }
}

static IEnumerable<Process> FindByPort(Int32 port)
{
    if (port <= 0) yield break;
    foreach (var line in RunCapture("netstat.exe", ["-ano", "-p", "tcp"]).Split('\n'))
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5 || !parts[1].EndsWith(":" + port)) continue;
        if (Int32.TryParse(parts[4], out var pid) && TryGetProcessByPid(pid) is { } p) yield return p;
    }
}

static String RunCapture(String file, String[] arguments)
{
    var psi = new ProcessStartInfo(file)
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        StandardOutputEncoding = Encoding.UTF8,
    };
    foreach (var a in arguments) psi.ArgumentList.Add(a);
    using var p = Process.Start(psi)!;
    var text = p.StandardOutput.ReadToEnd();
    p.WaitForExit();
    return text;
}

String ResolveExe(DbDef db)
{
    var pattern = db.Exe!;
    if (pattern.Contains('\\') || pattern.Contains('/'))
    {
        var direct = Path.Combine(AppDir(db), pattern);
        if (File.Exists(direct)) return direct;
    }

    var files = Directory.GetFiles(AppDir(db), pattern, SearchOption.AllDirectories);
    return files.FirstOrDefault() ?? throw new FileNotFoundException($"安装目录中未找到 {pattern}");
}

String Expand(String template, DbDef db) => template
    .Replace("{app}", AppDir(db))
    .Replace("{data}", DataDir(db))
    .Replace("{root}", root)
    .Replace("{port}", db.Port.ToString())
    .Replace("{httpPort}", db.HttpPort.ToString());

void Tail(String file, Int32 lines)
{
    if (!File.Exists(file)) return;
    foreach (var line in File.ReadLines(file).TakeLast(lines)) Console.WriteLine("  " + line);
}

static Int64 DirSize(String dir) => Directory.Exists(dir) ? Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Sum(e => new FileInfo(e).Length) : 0;
static Double FreeGb(String path) => new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace / 1024d / 1024 / 1024;

static String DefaultRoot()
{
    var drive = DriveInfo.GetDrives()
        .Where(e => e.DriveType == DriveType.Fixed && e.IsReady && e.AvailableFreeSpace > 10L * 1024 * 1024 * 1024)
        .OrderByDescending(e => e.AvailableFreeSpace)
        .First();
    return Path.Combine(drive.RootDirectory.FullName, "LocalDatabases");
}

Settings LoadSettings()
{
    try
    {
        var file = SettingsPath();
        if (File.Exists(file))
        {
            foreach (var line in File.ReadAllLines(file))
            {
                var p = line.IndexOf('=');
                if (p > 0 && line[..p].Trim().Equals("root", StringComparison.OrdinalIgnoreCase))
                    return new Settings { Root = line[(p + 1)..].Trim() };
            }
        }
    }
    catch { }
    return new();
}

void SaveSettings(Settings s)
{
    Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath())!);
    File.WriteAllText(SettingsPath(), "root=" + s.Root);
}

String SettingsPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalDbTool", SettingsFile);
String AppDir(DbDef db) => Path.Combine(root, "apps", db.Id, db.Version);
String DataDir(DbDef db) => Path.Combine(root, "data", db.Id);
String StateDir() => Path.Combine(root, "state");
String DownloadDir() => Path.Combine(root, "downloads");
String LogFile(DbDef db) => Path.Combine(root, "logs", db.Id + ".log");
String PidFile(DbDef db) => Path.Combine(StateDir(), db.Id + ".pid");

void Help()
{
    Console.WriteLine("""
    本机数据库启动器（C# 文件应用）

    用法：dotnet run tools/localdb/localdb.cs -- <命令> [参数]

      list                 列出所有数据库及安装/运行状态
      start <id>           启动数据库；未安装的 Windows 便携版会自动下载安装
      stop <id>            停止数据库
      restart <id>         重启数据库
      status [id]          查看状态
      install <id>         仅下载并安装
      root [path]          查看或设置安装根目录（默认空余空间最多的磁盘）
      clean                清理 Windows 侧下载残留与旧版本
      clean-wsl            清理 WSL 中的 apt/日志/安装包缓存与重复镜像
      wsl-move <path> --yes 把 WSL 虚拟磁盘移动到指定磁盘（需 WSL 3.0+）
      help                 显示帮助

    说明：prometheus/victoriametrics/cockroachdb/mongodb 为 Windows 便携版，
    会安装到根目录（默认空余空间最多的磁盘）；tdengine/clickhouse/cratedb/questdb/
    presto/trino/firebird/monetdb 通过 WSL 的 systemd 或 Docker 启动。
    """);
}

/// <summary>数据库定义</summary>
sealed record DbDef(
    String Id,
    String Name,
    String Kind,
    String Version,
    Int32 Port = 0,
    Int32 HttpPort = 0,
    String? Url = null,
    String[]? Mirrors = null,
    String? Exe = null,
    String? Args = null,
    String? ConfigFile = null,
    String? ConfigContent = null,
    String? Service = null,
    String? Container = null,
    String? Image = null,
    String? RunArgs = null,
    String? Ports = null,
    String? StartCmd = null,
    String? StopCmd = null,
    String? ConnStr = null,
    String? Note = null);

sealed class Settings
{
    public String? Root { get; set; }
}
