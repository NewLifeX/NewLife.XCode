using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace XCode.Analyzers;

/// <summary>禁止在实体程序集之外使用实体字段句柄 Entity._</summary>
/// <remarks>
/// <c>Entity._</c> 返回 Field 对象，用于在数据层拼装 WhereExpression。
/// 跨程序集使用会让查询逻辑散落到控制器、服务、任务等应用层，绕过实体 Biz 的查询封装。
/// 应用层应调用实体查询方法（Search/FindAllByXxx），字段名或排序使用字符串（如 <c>"Name"</c>）。
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FieldHandleAccessAnalyzer : DiagnosticAnalyzer
{
    /// <summary>诊断编号</summary>
    public const String DiagnosticId = "XC0001";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "禁止在实体程序集之外使用实体字段句柄",
        "“{0}”属于数据层字段句柄，只能在实体程序集内使用；应用层请调用实体查询方法（Search/FindAllByXxx），字段名或排序请使用字符串",
        "XCode",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Entity._ 返回 Field 对象，用于在数据层拼装 WhereExpression；跨程序集使用会让查询逻辑散落到控制器、服务、任务等应用层。");

    /// <summary>支持的诊断</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

    /// <summary>初始化分析器</summary>
    /// <param name="context">分析上下文</param>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var node = (MemberAccessExpressionSyntax)context.Node;
        var symbol = context.SemanticModel.GetSymbolInfo(node, context.CancellationToken).Symbol;
        if (symbol is not IFieldSymbol and not IPropertySymbol) return;

        // 只处理实体嵌套类 _ 的成员，例如 User._.Id
        var fieldType = symbol.ContainingType;
        if (fieldType == null || fieldType.Name != "_") return;

        var entityType = fieldType.ContainingType;
        if (entityType == null || !IsEntity(entityType)) return;

        // 同程序集（实体 Biz/生成代码）放行
        if (SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, context.Compilation.Assembly)) return;

        var diagnostic = Diagnostic.Create(_rule, node.Name.GetLocation(), $"{entityType.Name}._.{symbol.Name}");
        context.ReportDiagnostic(diagnostic);
    }

    private static Boolean IsEntity(INamedTypeSymbol type)
    {
        foreach (var item in type.AllInterfaces)
        {
            if (item.Name == "IEntity" && item.ContainingNamespace.ToDisplayString() == "XCode") return true;
        }

        return false;
    }
}
