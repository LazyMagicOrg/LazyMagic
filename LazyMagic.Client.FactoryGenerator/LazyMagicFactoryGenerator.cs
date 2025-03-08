namespace LazyMagic.Client.FactoryGenerator;
[Generator]
public class LazyMagicFactoryGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor _messageRule = new DiagnosticDescriptor(
        id: "LMF0001",
        title: "Factory Generator Error",
        messageFormat: "{0}",
        category: "Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Get all class declarations with the Factory attribute
        var classDeclarations =
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                    transform: (ctx, _) =>
                    {
                        var classNode = (ClassDeclarationSyntax)ctx.Node;
                        var semanticModel = ctx.SemanticModel;
                        if (semanticModel.GetDeclaredSymbol(classNode)?.GetAttributes()
                            .Any(a => a.AttributeClass?.Name == nameof(FactoryAttribute)) == true)
                        {
                            return (classNode, semanticModel);
                        }
                        return (null, null);
                    })
                .Where(tuple => tuple.Item1 != null);

        // Group class declarations by namespace
        var groupedByNamespace = classDeclarations
            .Collect()
            .Select((classNodes, _) =>
            {
                return classNodes
                    .GroupBy(tuple => GetNamespace(tuple.Item2, tuple.Item1))
                    .ToImmutableArray();
            });

        // Register the source output
        context.RegisterSourceOutput(groupedByNamespace,
            (spc, groupedClasses) =>
            {
                foreach (var group in groupedClasses)
                {
                    var namespaceName = group.Key;
                    var classes = group.Select(tuple => tuple.Item1).ToList();
                    GenerateRegistrationsClass(spc, namespaceName, classes);
                }
            });

        // Register the source output for individual factory classes
        context.RegisterSourceOutput(classDeclarations,
            (spc, tuple) => GenerateFactory(spc, tuple.Item1, tuple.Item2));
    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };
    }

    private void GenerateFactory(SourceProductionContext context,
        ClassDeclarationSyntax classNode,
        SemanticModel model)
    {
        try
        {
            context.ReportDiagnostic(Diagnostic.Create(_messageRule, Location.None, $"Generating factory for {classNode.Identifier.Text}"));

            var className = classNode.Identifier.Text;
            var namespaceName = GetNamespace(model, classNode);

            var constructor = classNode.DescendantNodes().OfType<ConstructorDeclarationSyntax>().FirstOrDefault();

            // Grab the parameters that have the FactoryInjectAttribute and prune the attribute from them
            var injectedParameters = constructor?.ParameterList.Parameters
                .Where(param => model.GetDeclaredSymbol(param) is IParameterSymbol paramSymbol &&
                               paramSymbol.GetAttributes().Any(a => a.AttributeClass!.Name == nameof(FactoryInjectAttribute)))
                .ToList();
            injectedParameters = RemoveFactoryInjectAttributeFromParameters(injectedParameters, model);
            var injectedParametersText = SyntaxFactory.SeparatedList(injectedParameters).ToFullString();

            // Generate private variables for the injected parameters
            var privateVariables = "";
            foreach (var param in injectedParameters)
            {
                var paramType = param.Type!.ToString();
                var paramName = param.Identifier.ToString();
                privateVariables += $"\t\tprivate {paramType} {paramName};\n";
            }

            // Generate the constructor argument assignments
            var constructorAssignments = "";
            foreach (var param in injectedParameters)
            {
                var paramName = param.Identifier.ToString();
                constructorAssignments += $"\t\tthis.{paramName} = {paramName};\n";
            }

            // Grab the parameters that are not injected
            var nonInjectedParameters = constructor?.ParameterList.Parameters
                .Where(param => model.GetDeclaredSymbol(param) is IParameterSymbol paramSymbol &&
                               !paramSymbol.GetAttributes().Any(a => a.AttributeClass!.Name == nameof(FactoryInjectAttribute)))
                .ToList();
            var nonInjectedParametersText = SyntaxFactory.SeparatedList(nonInjectedParameters).ToFullString();

            // Grab the arguments list (which includes all the parameters)
            var arguments = constructor?.ParameterList.Parameters.Select(p =>
                SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier)));
            var argumentsText = SyntaxFactory.SeparatedList(arguments).ToFullString();

            var sourceBuilder = new StringBuilder();
            sourceBuilder.Append(@$"
using System.Linq;
namespace {namespaceName}
{{
    public interface I{className}Factory
    {{
        {className} Create({nonInjectedParametersText});
    }} 
    public class {className}Factory : I{className}Factory
    {{
        public {className}Factory({injectedParametersText}) 
        {{ 
{constructorAssignments}
        }}
{privateVariables}
        public {className} Create({nonInjectedParametersText}) 
        {{
            return new {className}({argumentsText});
        }}
    }}
}}");

            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceBuilder.ToString());
            SyntaxNode root = tree.GetRoot();
            SyntaxNode formattedRoot = root.NormalizeWhitespace();
            context.AddSource($"I{className}Factory.g.cs", SourceText.From(formattedRoot.ToString(), Encoding.UTF8));
        }
        catch (Exception ex)
        {
            var diagnostic = Diagnostic.Create(_messageRule, Location.None, ex.Message);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private void GenerateRegistrationsClass(SourceProductionContext context,
        string namespaceName,
        List<ClassDeclarationSyntax> classes)
    {
        var sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine(@$"
// <auto-generated />
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace {namespaceName};

public static class RegisterFactories
{{
    public static void Register(IServiceCollection services)
    {{");

        foreach (var classNode in classes)
        {
            var className = classNode.Identifier.Text;
            sourceBuilder.AppendLine($"        services.TryAddTransient<I{className}Factory,{className}Factory>();");
        }

        sourceBuilder.AppendLine($@"
    }}
}}");

        SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceBuilder.ToString());
        SyntaxNode root = tree.GetRoot();
        SyntaxNode formattedRoot = root.NormalizeWhitespace();
        var source = SourceText.From(formattedRoot.ToString(), Encoding.UTF8);
        context.AddSource($"{namespaceName}.RegisterFactories.g.cs", source);
    }

    private List<ParameterSyntax> RemoveFactoryInjectAttributeFromParameters(List<ParameterSyntax>? parameterList, SemanticModel model)
    {
        if (parameterList == null) return new List<ParameterSyntax>();
        var modifiedParameters = parameterList.Select(p => RemoveFactoryInjectAttributeFromParameter(p, model)).ToList();
        return modifiedParameters;
    }

    private ParameterSyntax RemoveFactoryInjectAttributeFromParameter(ParameterSyntax parameter, SemanticModel model)
    {
        // Get the symbol for the parameter
        if (model.GetDeclaredSymbol(parameter) is IParameterSymbol paramSymbol &&
            paramSymbol.GetAttributes().Any(a => a.AttributeClass!.Name == nameof(FactoryInjectAttribute) ||
                                               a.AttributeClass!.Name == nameof(FactoryInjectAttribute) + "Attribute"))
        {
            // Remove the FactoryInjectAttribute at the syntax level
            var modifiedAttributeLists = parameter.AttributeLists.Select(
                attrList => attrList.WithAttributes(SyntaxFactory.SeparatedList<AttributeSyntax>(
                    attrList.Attributes.Where(attr => attr.Name.ToString() != "FactoryInject"))
                )
            );

            return parameter.WithAttributeLists(SyntaxFactory.List(modifiedAttributeLists.Where(al => al.Attributes.Count > 0)));
        }
        // If the parameter didn't have the FactoryInjectAttribute, return it as is.
        return parameter;
    }

    private static string GetNamespace(SemanticModel model, ClassDeclarationSyntax classNode)
    {
        var symbol = model.GetDeclaredSymbol(classNode);
        if (symbol == null)
            return string.Empty;

        var namespaceName = symbol.ContainingNamespace.ToDisplayString();

        return namespaceName;
    }

    private string LowerFirstChar(string name) => name.Substring(0, 1).ToLower() + name.Substring(1);
}
