using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.Code;

namespace XCodeTool;

class Program
{
    static void Main(string[] args)
    {
        XTrace.UseConsole();

        //if (args.Length == 0)
        {
            Console.WriteLine("NewLife.XCode v{0}", Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine("Usage: xcode model.xml");
            Console.WriteLine("Install: dotnet tool install -g xcodetool");
            Console.WriteLine("Upgrade: http://x.newlifex.com/xcodetool.exe");
            Console.WriteLine();
        }

        var log = XTrace.Log;
        log.Level = LogLevel.All;
        if (log is CompositeLog clog)
        {
            // 使用绝对路径目录，避免在子目录中生成日志文件
            var textFileLog = clog.Get<TextFileLog>();
            if (textFileLog != null)
                textFileLog.LogPath = textFileLog.LogPath.GetBasePath();
        }

        // 检查工具
        var task = Task.Run(CheckTool);

        // 在当前目录查找模型文件
        var file = "";
        if (args.Length > 0) file = args.LastOrDefault();
        if (!file.IsNullOrEmpty())
        {
            if (!Path.IsPathRooted(file))
            {
                var file2 = Environment.CurrentDirectory.CombinePath(file);
                if (File.Exists(file2)) file = file2;
            }
            if (!File.Exists(file))
            {
                Console.WriteLine("文件不存在：{0}", file);
                return;
            }

            Build(file, log);
        }
        else
        {
            // 遍历当前目录及子目录
            var files = new List<String>();
            var di = Environment.CurrentDirectory.AsDirectory();
            foreach (var fi in di.GetFiles("*.xml", SearchOption.AllDirectories))
            {
                if (fi.Name.EqualIgnoreCase("XCode.xml")) continue;

                var txt = File.ReadAllText(fi.FullName);
                if (txt.Contains("<Tables") || txt.Contains("<EntityModel")) files.Add(fi.FullName);
            }

            if (files.Count > 0)
            {
                // 循环处理
                foreach (var item in files)
                {
                    Build(item, log);
                }
            }
            else
            {
                // 实在没有，释放一个出来
                var ms = Assembly.GetExecutingAssembly().GetManifestResourceStream("XCode.Model.xml");
                var xml = ms.ToStr();

                file = Environment.CurrentDirectory.CombinePath("Model.xml");
                File.WriteAllText(file, xml, Encoding.UTF8);
            }
        }

        task.Wait(5_000);
    }

    /// <summary>生成实体类。用户可以调整该方法可以改变生成实体类代码的逻辑，从而得到自己的代码生成器</summary>
    /// <param name="modelFile"></param>
    static void Build(String modelFile, ILog log)
    {
        XTrace.WriteLine("正在处理：{0}", modelFile);

        //EntityBuilder.Debug = true;

        // 设置当前工作目录
        PathHelper.BasePath = Path.GetDirectoryName(modelFile);

        // 设置如何格式化字段名，默认去掉下划线并转驼峰命名
        //ModelResolver.Current = new ModelResolver { TrimUnderline = false, Camel = false };

        // 加载模型文件，得到数据表
        var option = new CubeBuilderOption();
        var tables = ClassBuilder.LoadModels(modelFile, option, out var atts, log);

        try
        {
            XTrace.WriteLine("修正模型：{0}", modelFile);
            EntityBuilder.FixModelFile(modelFile, option, atts, tables, log);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        XTrace.WriteLine("共有模型：{0}", tables.Count);

        // 是否使用中文名
        option.ChineseFileName = true;

        // 简易模型类名称，如{name}Model。指定后将生成简易模型类和接口，可用于数据传输
        //var modelClass = atts["ModelClass"];
        //var modelInterface = atts["ModelInterface"];
        var modelClass = option.ModelClass;
        var modelInterface = option.ModelInterface;

        // 生成实体类
        {
            var opt = option.Clone() as EntityBuilderOption;
            //opt.BaseClass = null;
            //opt.ClassNameTemplate = null;
            //opt.ModelNameForCopy = null;
            //opt.ModelNameForToModel = modelClass;
            if (!modelInterface.IsNullOrEmpty())
            {
                opt.BaseClass += "," + modelInterface;
                opt.ModelNameForCopy ??= modelInterface;
            }
            else if (!modelClass.IsNullOrEmpty())
            {
                opt.ModelNameForCopy ??= modelClass;
            }
            EntityBuilder.BuildTables(tables, opt, log);
        }

        // 生成简易模型类
        {
            var opt = option.Clone() as EntityBuilderOption;
            //opt.Items.TryGetValue("ModelsOutput", out var output);
            var output = option.ModelsOutput ?? @".\Models\";
            opt.Output = opt.Output.CombinePath(output);
            //opt.BaseClass = modelInterface;
            opt.BaseClass = null;
            opt.ClassNameTemplate = modelClass;
            // 模型接口存在，且相同于拷贝类型，才能作为基类
            if (!modelInterface.IsNullOrEmpty() && modelInterface == opt.ModelNameForCopy)
                opt.BaseClass += "," + modelInterface;
            else
                opt.BaseClass = null;
            opt.ModelNameForCopy = !modelInterface.IsNullOrEmpty() ? modelInterface : modelClass;
            //opt.HasIModel = true;
            //opt.Partial = true;
            if (!modelClass.IsNullOrEmpty())
            {
                ModelBuilder.BuildModels(tables, opt, log);
            }
            else
            {
                var ts = tables.Where(e => !e.Properties["ModelClass"].IsNullOrEmpty()).ToList();
                if (ts.Count > 0)
                {
                    ModelBuilder.BuildModels(ts, opt, log);
                }
            }
        }

        // 生成简易接口
        {
            var opt = option.Clone() as EntityBuilderOption;
            //opt.Items.TryGetValue("InterfacesOutput", out var output);
            //output ??= @".\Interfaces\";
            var output = option.InterfacesOutput ?? @".\Interfaces\";
            opt.Output = opt.Output.CombinePath(output);
            opt.BaseClass = null;
            opt.ClassNameTemplate = modelInterface;
            opt.ModelNameForCopy = null;
            opt.HasIModel = false;
            //opt.Partial = true;
            if (!modelInterface.IsNullOrEmpty())
            {
                InterfaceBuilder.BuildInterfaces(tables, opt, log);
            }
            else
            {
                var ts = tables.Where(e => !e.Properties["ModelInterface"].IsNullOrEmpty()).ToList();
                if (ts.Count > 0)
                {
                    InterfaceBuilder.BuildInterfaces(ts, opt, log);
                }
            }
        }

        // 生成数据字典
        {
            var opt = option.Clone();
            HtmlBuilder.BuildDataDictionary(tables, opt, log);
        }

        // 生成魔方区域和控制器
        {
            var opt = option.Clone() as CubeBuilderOption;
            if (opt.Items != null && !opt.CubeOutput.IsNullOrEmpty())
            {
                opt.BaseClass = null;
                //opt.Namespace = null;

                opt.Output = opt.CubeOutput;
                CubeBuilder.BuildArea(opt, log);

                CubeBuilder.BuildControllers(tables, opt, log);
            }
        }
    }

    static void CheckTool()
    {
        var rs = Execute("dotnet", "tool list -g", 3_000);

        var ss = rs?.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var line = ss?.FirstOrDefault(e => e.StartsWith("xcodetool"));
        if (line.IsNullOrEmpty())
        {
            rs = Execute("dotnet", "tool install xcodetool -g");
            XTrace.WriteLine(rs);
            //XTrace.WriteLine("已安装工具xcodetool，可在模型目录使用xcode命令来生成代码");
        }
        else
        {
            ss = line.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries);
            if (ss != null && ss.Length >= 2 && Version.TryParse(ss[1], out var ver))
            {
                var dt = new DateTime(ver.Build, ver.Revision / 100, ver.Revision % 100);
                if (dt.AddMonths(1) < DateTime.Now)
                {
                    rs = Execute("dotnet", "tool update xcodetool -g --prerelease");
                    XTrace.WriteLine(rs);
                }
                else if (dt.AddDays(7) < DateTime.Now)
                {
                    //rs = Execute("dotnet", "tool install -g --prerelease xcodetool");
                    rs = Execute("dotnet", "tool update xcodetool -g");
                    XTrace.WriteLine(rs);
                }
            }
        }
    }

    /// <summary>执行命令并等待返回</summary>
    /// <param name="cmd"></param>
    /// <param name="arguments"></param>
    /// <param name="msWait"></param>
    /// <returns></returns>
    public static String? Execute(String cmd, String? arguments = null, Int32 msWait = 0)
    {
        try
        {
            if (XTrace.Log.Level <= LogLevel.Debug) XTrace.WriteLine("Execute({0} {1})", cmd, arguments);

            var psi = new ProcessStartInfo(cmd, arguments ?? String.Empty)
            {
                // UseShellExecute 必须 false，以便于后续重定向输出流
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            var process = Process.Start(psi);
            if (process == null) return null;

            if (msWait > 0 && !process.WaitForExit(msWait))
            {
                process.Kill();
                return null;
            }

            var rs = process.StandardOutput.ReadToEnd();
            if (rs.IsNullOrEmpty()) rs = process.StandardError.ReadToEnd();

            return rs;
        }
        catch { return null; }
    }
}