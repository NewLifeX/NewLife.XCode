using System;
using System.IO;
using System.Security.Cryptography;
using NewLife;
using NewLife.Security;
using NewLife.Web;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>驱动下载校验测试</summary>
public class DriverLoaderTests
{
    [Fact(DisplayName = "固定哈希不匹配时拒绝驱动文件")]
    public void VerifyDownloaded_WrongHash_Rejected()
    {
        var file = Path.Combine(Path.GetTempPath(), "driver_" + Rand.NextString(8) + ".dll");
        try
        {
            File.Copy(typeof(DriverLoaderTests).Assembly.Location, file, true);

            var name = Path.GetFileName(file);
            DriverLoader.KnownHashes[name] = new String('0', 64);
            try
            {
                Assert.False(DriverLoader.VerifyDownloaded(file, new WebClientX()));
            }
            finally
            {
                DriverLoader.KnownHashes.Remove(name);
            }
        }
        finally
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }
    }

    [Fact(DisplayName = "固定哈希匹配时接受驱动文件")]
    public void VerifyDownloaded_CorrectHash_Accepted()
    {
        var file = Path.Combine(Path.GetTempPath(), "driver_" + Rand.NextString(8) + ".dll");
        try
        {
            File.Copy(typeof(DriverLoaderTests).Assembly.Location, file, true);

            var name = Path.GetFileName(file);
            var client = new WebClientX();
            var sha256 = System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(file)).ToHex();
            DriverLoader.KnownHashes[name] = sha256;
            try
            {
                Assert.True(DriverLoader.VerifyDownloaded(file, client));
            }
            finally
            {
                DriverLoader.KnownHashes.Remove(name);
            }
        }
        finally
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }
    }

    [Fact(DisplayName = "非托管文件不能作为驱动加载")]
    public void VerifyDownloaded_NotAssembly_Rejected()
    {
        var file = Path.Combine(Path.GetTempPath(), "driver_" + Rand.NextString(8) + ".dll");
        try
        {
            File.WriteAllText(file, "not a managed assembly");

            Assert.False(DriverLoader.VerifyDownloaded(file, new WebClientX()));
        }
        finally
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }
    }

    [Fact(DisplayName = "自定义校验回调可拒绝驱动文件")]
    public void VerifyDownloaded_CustomCallback_Rejected()
    {
        var file = Path.Combine(Path.GetTempPath(), "driver_" + Rand.NextString(8) + ".dll");
        try
        {
            File.Copy(typeof(DriverLoaderTests).Assembly.Location, file, true);

            DriverLoader.VerifyFile = _ => false;
            try
            {
                Assert.False(DriverLoader.VerifyDownloaded(file, new WebClientX()));
            }
            finally
            {
                DriverLoader.VerifyFile = null;
            }
        }
        finally
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }
    }

    [Fact(DisplayName = "下载页哈希对应归档时校验归档而不是解压出的程序集")]
    public void VerifyDownloaded_ChecksArchiveHash()
    {
        var name = "driver_" + Rand.NextString(8);
        var file = Path.Combine(Path.GetTempPath(), name + ".dll");
        var archive = Path.Combine(Path.GetTempPath(), name + ".zip");
        try
        {
            // 程序集与归档内容不同，模拟「压缩包解压出驱动」的官方下载方式
            File.Copy(typeof(DriverLoaderTests).Assembly.Location, file, true);
            File.WriteAllText(archive, "fake archive " + name);

            var client = new WebClientX
            {
                LastLink = new Link { FullName = Path.GetFileName(archive), Url = "https://x.newlifex.com/adonet/" + Path.GetFileName(archive) }
            };

            // 哈希属于归档，不能拿去比对解压出的程序集
            client.LastLink.Hash = MD5.Create().ComputeHash(File.ReadAllBytes(archive)).ToHex();
            Assert.True(DriverLoader.VerifyDownloaded(file, client, archive));

            // 归档被篡改则拒绝
            client.LastLink.Hash = MD5.Create().ComputeHash(File.ReadAllBytes(file)).ToHex();
            Assert.NotEqual(client.LastLink.Hash, MD5.Create().ComputeHash(File.ReadAllBytes(archive)).ToHex());
            Assert.False(DriverLoader.VerifyDownloaded(file, client, archive));
        }
        finally
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
            try { if (File.Exists(archive)) File.Delete(archive); } catch { }
        }
    }

    [Fact(DisplayName = "下载页给出SHA-256时同样校验而不是跳过")]
    public void VerifyDownloaded_Sha256PageHash_Checked()
    {
        var file = Path.Combine(Path.GetTempPath(), "driver_" + Rand.NextString(8) + ".dll");
        try
        {
            File.Copy(typeof(DriverLoaderTests).Assembly.Location, file, true);

            var client = new WebClientX
            {
                LastLink = new Link { FullName = Path.GetFileName(file), Url = "https://x.newlifex.com/adonet/" + Path.GetFileName(file) }
            };
            var sha256 = SHA256.Create().ComputeHash(File.ReadAllBytes(file)).ToHex();

            // 64位哈希属于可识别算法，必须真正比对
            client.LastLink.Hash = sha256;
            Assert.True(DriverLoader.VerifyDownloaded(file, client));

            client.LastLink.Hash = new String('0', 64);
            Assert.NotEqual(sha256, client.LastLink.Hash);
            Assert.False(DriverLoader.VerifyDownloaded(file, client));

            // 带算法前缀的哈希同样生效
            client.LastLink.Hash = "sha256$" + sha256;
            Assert.True(DriverLoader.VerifyDownloaded(file, client));

            // 不支持的算法与不可识别的长度无法比对，保持宽松跳过而不是拒绝下载
            client.LastLink.Hash = "sha384$" + new String('0', 96);
            Assert.True(DriverLoader.VerifyDownloaded(file, client));

            client.LastLink.Hash = new String('0', 96);
            Assert.True(DriverLoader.VerifyDownloaded(file, client));
        }
        finally
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }
    }

    [Fact(DisplayName = "下载页哈希属于压缩包时不误判解压出的程序集")]
    public void VerifyDownloaded_ArchiveHashNotAppliedToAssembly()
    {
        var name = "driver_" + Rand.NextString(8);
        var file = Path.Combine(Path.GetTempPath(), name + ".dll");
        try
        {
            File.Copy(typeof(DriverLoaderTests).Assembly.Location, file, true);

            // 没有归档路径时，不能拿压缩包的哈希比对解压出的驱动文件
            var client = new WebClientX
            {
                LastLink = new Link { FullName = name + ".zip", Url = "https://x.newlifex.com/adonet/" + name + ".zip", Hash = new String('0', 32) }
            };

            Assert.True(DriverLoader.VerifyDownloaded(file, client));
        }
        finally
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }
    }
}
