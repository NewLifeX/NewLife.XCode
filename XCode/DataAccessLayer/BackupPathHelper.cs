using System;
using System.IO;
using NewLife;
using XCode.Exceptions;

namespace XCode.DataAccessLayer;

/// <summary>备份路径与 SQL 字面量安全助手</summary>
/// <remarks>
/// 备份、恢复与数据包读写都允许外部传入文件名，统一在这里把路径限制到备份目录内，
/// 避免 "../" 或绝对路径把文件写到任意位置、删掉任意文件。
/// </remarks>
internal static class BackupPathHelper
{
    /// <summary>把路径限制在备份目录内，返回绝对路径</summary>
    /// <param name="path">相对或绝对路径</param>
    /// <returns>绝对路径</returns>
    public static String Ensure(String path)
    {
        var root = NewLife.Setting.Current.BackupPath;
        if (root.IsNullOrEmpty()) root = "Backup";
        root = root.GetBasePath();

        var full = Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(root.CombinePath(path));

        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var cmp = StringComparison.OrdinalIgnoreCase;
        if (!full.Equals(rootFull, cmp) && !full.StartsWith(rootFull + Path.DirectorySeparatorChar, cmp))
            throw new XCodeException($"The file path [{path}] is outside the backup directory [{rootFull}] and is rejected.");

        return full;
    }

    /// <summary>转义 SQL 字符串字面量，返回带单引号的形式</summary>
    /// <param name="value">原始值</param>
    /// <returns>可直接拼进 SQL 的字面量</returns>
    public static String Quote(String? value) => "'" + (value ?? "").Replace("'", "''") + "'";

    /// <summary>转义 SQL 标识符，返回带方括号的形式</summary>
    /// <param name="value">原始标识符</param>
    /// <returns>可直接拼进 SQL 的标识符</returns>
    public static String QuoteName(String? value) => "[" + (value ?? "").Replace("]", "]]") + "]";
}
