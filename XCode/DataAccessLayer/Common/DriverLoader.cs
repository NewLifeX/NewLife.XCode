using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography;
using NewLife.Log;
using NewLife.Web;

namespace XCode.DataAccessLayer;

/// <summary>驱动加载器</summary>
public static class DriverLoader
{
    /// <summary>创建客户端。便于外部自定义</summary>
    public static Func<String, WebClientX> CreateClient { get; set; } = linkName => new WebClientX { AuthKey = "NewLife", Log = XTrace.Log };

    /// <summary>驱动文件SHA-256固定值。Key为文件名（不含路径），Value为十六进制哈希</summary>
    /// <remarks>命中时下载后强制校验；未登记时仅校验下载页给出的MD5/SHA512与托管程序集有效性</remarks>
    public static IDictionary<String, String> KnownHashes { get; } = new ConcurrentDictionary<String, String>(StringComparer.OrdinalIgnoreCase);

    /// <summary>驱动文件校验回调。参数为下载后的文件路径，返回false表示拒绝加载</summary>
    public static Func<String, Boolean>? VerifyFile { get; set; }

    /// <summary>加载插件</summary>
    /// <param name="typeName">插件类型</param>
    /// <param name="disname">显示名</param>
    /// <param name="dll">DLL文件</param>
    /// <param name="linkName">链接名。要在下载页面中搜索的链接名</param>
    /// <param name="urls">提供下载地址的多个目标页面</param>
    /// <param name="minVersion">最低版本。不匹配低于该版本的链接</param>
    /// <returns></returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "驱动加载器按运行时类型名加载外置驱动程序集，裁剪/AOT 下不支持该动态加载路径")]
    [UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "驱动类型名来自各驱动注册的固定字符串，运行时解析加载")]
    [UnconditionalSuppressMessage("SingleFile", "IL3000", Justification = "单文件发布下不支持外置驱动插件，探测路径为空即回退")]
    public static Type? Load(String typeName, String? disname, String? dll, String? linkName, String? urls = null, Version? minVersion = null)
    {
        if (typeName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(typeName));

        //var type = typeName.GetTypeEx(true);
        var type = Type.GetType(typeName);
        if (type != null) return type;

        if (dll.IsNullOrEmpty()) return null;

        // 静态引用降级：驱动已随应用发布（工程引用/deps 声明）时，按程序集限定名直接解析。
        // 动态加载（Assembly.LoadFrom）在 NativeAOT/单文件下不可用，此路径可命中已随应用发布的驱动
        var asmName = Path.GetFileNameWithoutExtension(dll);
        if (!asmName.IsNullOrEmpty())
        {
            try { type = Type.GetType($"{typeName}, {asmName}"); } catch { }
            if (type != null) return type;
        }

        var set = NewLife.Setting.Current;

        var file = "";
        if (!dll.IsNullOrEmpty())
        {
            // 先检查程序集所在目录，再检查当前目录、基准目录和插件目录。在应用发布时，插件很可能跟常规应用程序集放在同一目录下
            var exe = Assembly.GetEntryAssembly()?.Location;
            if (exe.IsNullOrEmpty())
            {
                // GetCallingAssembly 在 NativeAOT 下不受支持（PlatformNotSupportedException），探测失败即跳过
                try { exe = Assembly.GetCallingAssembly()?.Location; } catch (PlatformNotSupportedException) { }
            }
            if (exe.IsNullOrEmpty()) exe = Assembly.GetExecutingAssembly()?.Location;
            if (!exe.IsNullOrEmpty()) file = Path.GetDirectoryName(exe).CombinePath(dll).GetFullPath();
            if (!File.Exists(file)) file = dll.GetCurrentPath();
            if (!File.Exists(file)) file = dll.GetFullPath();
            if (!File.Exists(file)) file = dll.GetBasePath();
            if (!File.Exists(file)) file = set.PluginPath.CombinePath(dll).GetFullPath();
            if (!File.Exists(file)) file = set.PluginPath.CombinePath(dll).GetBasePath();
        }

        // 尝试直接加载DLL
        if (File.Exists(file))
        {
            try
            {
                var asm = Assembly.LoadFrom(file);
                type = asm.GetType(typeName);
                if (type != null) return type;
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }

        if (linkName.IsNullOrEmpty()) return null;

        // 按类型名锁定，超时取不到锁，则放弃
        if (!Monitor.TryEnter(typeName, 15_000)) return null;

        try
        {
            type = Type.GetType(typeName);
            if (type != null) return type;

            if (urls.IsNullOrEmpty()) urls = set.PluginServer;

            // 如果本地没有数据库，则从网络下载
            if (!File.Exists(file))
            {
                // 不限制传输协议。部分嵌入式工控机没有 OpenSSL，无法建立 TLS 连接，只能走 http；
                // 完整性由下载页哈希、固定 SHA-256 与托管程序集校验保证，不依赖传输层
                XTrace.WriteLine("{0} does not exist or has the wrong platform version; fetching {1} from the network.", !disname.IsNullOrEmpty() ? disname : dll, urls);
                if (minVersion != null) XTrace.WriteLine("Minimum version required: {0}", minVersion);

                using var client = CreateClient(linkName);
                client.MinVersion = minVersion;
                var dir = Path.GetDirectoryName(file);
                var file2 = client.DownloadLinkAndExtract(urls, linkName, dir!, true);
                //client.TryDispose();

                // 下载得到的文件必须通过校验，避免镜像投毒或中间人替换。
                // 下载页哈希对应的是链接文件本身，链接为压缩包时校验归档，解压出来的程序集另按固定哈希校验
                if (File.Exists(file) && !VerifyDownloaded(file, client, file2))
                {
                    XTrace.WriteLine(L.T("The downloaded driver file [{0}] failed verification and was deleted."), file);
                    if (!file2.IsNullOrEmpty() && !file2.EqualIgnoreCase(file)) TryDelete(file2);
                    TryDelete(file);
                    return null;
                }
            }
            if (!File.Exists(file))
            {
                XTrace.WriteLine("{0} {1} was not found.", disname, dll);
                return null;
            }

            //return Assembly.LoadFrom(file).GetType(typeName);

            type = Type.GetType(typeName);
            if (type != null) return type;

            // 尝试直接加载DLL
            if (File.Exists(file))
            {
                try
                {
                    var asm = Assembly.LoadFrom(file);
                    type = asm.GetType(typeName);
                    if (type != null) return type;
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }
            }

            return null;
        }
        finally
        {
            Monitor.Exit(typeName);
        }
    }

    /// <summary>校验下载得到的驱动文件，依次检查自定义回调、下载页哈希、固定哈希与托管程序集有效性</summary>
    /// <param name="file">驱动文件路径</param>
    /// <param name="client">下载客户端，提供下载页给出的哈希</param>
    /// <param name="hashFile">下载页哈希所对应的文件。链接为压缩包时传归档路径，为空时按链接名与驱动文件是否同名判断</param>
    /// <returns></returns>
    internal static Boolean VerifyDownloaded(String file, WebClientX client, String? hashFile = null)
    {
        try
        {
            if (VerifyFile != null && !VerifyFile(file)) return false;

            // 下载页给出的哈希对应链接文件本身。链接是压缩包时哈希不属于解压出的程序集，不能拿程序集的哈希去比对
            var link = client.LastLink;
            var hash = link?.Hash;
            var checkFile = file;
            if (!hashFile.IsNullOrEmpty() && File.Exists(hashFile))
                checkFile = hashFile;
            else if (link != null && !link.FullName.IsNullOrEmpty() && !Path.GetFileName(file).EqualIgnoreCase(Path.GetFileName(link.FullName)))
                checkFile = null;

            // 下载页给出的哈希优先比对，沿用 NewLife.Core 的算法识别规则（支持算法前缀与 MD5/SHA1/SHA256/SHA512）
            if (checkFile != null && IsHashVerifiable(hash) && !checkFile.AsFile().VerifyHash(hash!)) return false;

            // 显式固定的 SHA-256 必须匹配
            var pinned = TryGetKnownHash(file, out var sha256);
            if (pinned && !file.AsFile().VerifyHash(sha256)) return false;

            // 必须是可加载的托管程序集
            AssemblyName.GetAssemblyName(file);

            // 未固定时输出哈希，便于运维登记后下次强制校验
            if (!pinned)
            {
                using var fs = File.OpenRead(file);
                var actual = SHA256.Create().ComputeHash(fs).ToHex();
                XTrace.WriteLine("Driver file [{0}] SHA-256 is [{1}]; pin it in DriverLoader.KnownHashes to enforce integrity.", Path.GetFileName(file), actual);
            }

            return true;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return false;
        }
    }

    /// <summary>下载页哈希是否可校验。带算法前缀或长度可识别时校验，认不出的哈希保持宽松跳过</summary>
    /// <param name="hash">下载页给出的哈希字符串</param>
    /// <returns>是否可以交给 PathHelper.VerifyHash 校验</returns>
    private static Boolean IsHashVerifiable(String? hash)
    {
        if (hash.IsNullOrEmpty()) return false;

        var value = hash.Trim();

        // 带算法前缀时只认 VerifyHash 支持的算法，其它算法（如 sha384）无法比对，保持宽松跳过
        var p = value.IndexOf('$');
        if (p > 0)
        {
            var alg = value[..p];
            return alg.EqualIgnoreCase("crc32", "crc", "md5", "sha1", "sha-1", "sha256", "sha-256", "sha512", "sha-512");
        }

        // 无前缀时按长度识别：CRC32(8)/MD5(16,32)/SHA1(40)/SHA256(64)/SHA512(128)
        return value.Length is 8 or 16 or 32 or 40 or 64 or 128;
    }

    private static Boolean TryGetKnownHash(String file, out String hash)
    {
        hash = "";
        if (KnownHashes.TryGetValue(Path.GetFileName(file), out var value) && !value.IsNullOrEmpty())
        {
            hash = value;
            return true;
        }

        return false;
    }

    private static void TryDelete(String file)
    {
        try
        {
            if (File.Exists(file)) File.Delete(file);
        }
        catch { }
    }
}
