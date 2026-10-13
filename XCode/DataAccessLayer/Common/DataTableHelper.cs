using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace XCode.DataAccessLayer;

/// <summary>数据表辅助工具</summary>
internal static class DataTableHelper
{
    /// <summary>按过滤表达式筛选数据行</summary>
    /// <param name="table">数据表</param>
    /// <param name="filter">过滤表达式，如“Name='abc'”；仅使用字面量等值比较</param>
    /// <returns>符合条件的数据行数组</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "元数据筛选仅使用字面量等值表达式，不涉及动态代码求值，裁剪后行为不变")]
    public static DataRow[] SelectRows(this DataTable table, String filter) => table.Select(filter);

    /// <summary>按过滤表达式筛选数据行，并排序</summary>
    /// <param name="table">数据表</param>
    /// <param name="filter">过滤表达式，如“Name='abc'”；仅使用字面量等值比较</param>
    /// <param name="sort">排序列，如“Id”或“Id Desc”；为空时保持原顺序</param>
    /// <returns>符合条件的数据行数组</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "元数据筛选仅使用字面量等值表达式，不涉及动态代码求值，裁剪后行为不变")]
    public static DataRow[] SelectRows(this DataTable table, String filter, String? sort) => table.Select(filter, sort);
}
