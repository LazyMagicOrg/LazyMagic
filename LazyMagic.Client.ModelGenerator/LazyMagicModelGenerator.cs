﻿using Microsoft.CodeAnalysis;

namespace LazyMagic.Client.ModelGenerator;

// Example1:
// namespace MyNamespace;
// [LzModel("Yada")]
//
// Generated: YadaModel.cs
// namespace MyNamespace;
// public partial class YadaModel : IRegisterObservables
// {
// }
// 

[Generator]
public class LazyMagicModelGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor _messageRule = new(
        id: "LZI0002",
        title: "LazyMagic.Client.LzModelGenerator Source Generator Message",
        messageFormat: "{0}",
        category: "SourceGenerator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the syntax provider to find classes with LzModel attribute
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetClassToGenerate(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!.Value);  // Convert from nullable to non-nullable

        // Register the output
        context.RegisterSourceOutput(classDeclarations,
            static (spc, classInfo) => Execute(spc, classInfo));
    }

    private static (string Namespace, string BaseClassName)? GetClassToGenerate(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var model = context.SemanticModel;

        // Check if the class has the LzModel attribute
        var classSymbol = model.GetDeclaredSymbol(classDecl);
        if (classSymbol == null) return null;

        var hasAttribute = classSymbol.GetAttributes()
            .Any(a => a.AttributeClass?.Name == nameof(LzModelAttribute));
        
        if (!hasAttribute) return null;

        // Get the attribute and its argument
        var modelAttribute = classDecl.AttributeLists
            .SelectMany(a => a.Attributes)
            .FirstOrDefault(a => a.Name.ToString().EndsWith("LzModel"));

        if (modelAttribute?.ArgumentList?.Arguments.Count < 1) return null;

        var baseClassName = modelAttribute.ArgumentList.Arguments[0].Expression.ToString().Trim('"');
        var namespaceName = classSymbol.ContainingNamespace.ToString();

        return (namespaceName, baseClassName);
    }

    private static void Execute(SourceProductionContext context, (string Namespace, string BaseClassName) classInfo)
    {
        var source = $@"
// <auto-generated />
namespace {classInfo.Namespace};
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public partial class {classInfo.BaseClassName}Model : {classInfo.BaseClassName}, IRegisterObservables
{{
}}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
";
        context.AddSource($"{classInfo.BaseClassName}Model.g.cs", source);
    }

    private static void Log(SourceProductionContext context, string? message)
    {
        if (message == null) return;
        string[] lines = message.Split(new[] { '\n' }, StringSplitOptions.None);
        foreach (var line in lines)
        {
            var diagnostic = Diagnostic.Create(_messageRule, Location.None, line);
            context.ReportDiagnostic(diagnostic);
        }
    }
}

