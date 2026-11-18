using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace XCode.Analyzers.Tests;

public class FieldHandleAccessAnalyzerTests
{
    private const String EntitySource = """
using System;
using XCode;

namespace XCode
{
    public interface IEntity { }
    public class Entity<TEntity> : IEntity { }
}

namespace Demo
{
    public class User : Entity<User>
    {
        public partial class _
        {
            public static readonly Int32 Id = 1;
        }

        public partial class __
        {
            public const String Id = "Id";
        }
    }
}
""";

    [Fact(DisplayName = "同程序集使用 Entity._ 不报诊断")]
    public void SameAssembly_NoDiagnostic()
    {
        var diagnostics = GetDiagnostics(EntitySource + """

namespace App
{
    public static class Biz
    {
        public static Int32 GetId() => Demo.User._.Id;
    }
}
""");

        Assert.Empty(diagnostics);
    }

    [Fact(DisplayName = "跨程序集使用 Entity._ 报 XC0001")]
    public void CrossAssembly_ReportsDiagnostic()
    {
        var reference = CompileToReference(EntitySource, "EntityAssembly");
        var diagnostics = GetDiagnostics("""
using System;

namespace App
{
    public static class Controller
    {
        public static Int32 GetId() => Demo.User._.Id;
    }
}
""", reference);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(FieldHandleAccessAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Fact(DisplayName = "跨程序集使用 Entity.__ 字段名不报诊断")]
    public void FieldNameConstant_NoDiagnostic()
    {
        var reference = CompileToReference(EntitySource, "EntityAssembly");
        var diagnostics = GetDiagnostics("""
using System;

namespace App
{
    public static class Controller
    {
        public static String GetName() => Demo.User.__.Id;
    }
}
""", reference);

        Assert.Empty(diagnostics);
    }

    private static ImmutableArray<Diagnostic> GetDiagnostics(String source, params MetadataReference[] references)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var allReferences = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(Object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        };
        allReferences.AddRange(references);

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            allReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var withAnalyzers = compilation.WithAnalyzers([new FieldHandleAccessAnalyzer()]);
        return withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    private static MetadataReference CompileToReference(String source, String assemblyName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(Object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, String.Join(Environment.NewLine, result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}
