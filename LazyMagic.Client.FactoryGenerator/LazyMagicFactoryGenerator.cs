namespace LazyMagic.Client.FactoryGenerator;

//Example: 

// Source Class
// Factory annotation specifies the class needs a DI factory 
// FactoryInject annotation specifies the parameter needs to be injected by the DI factory

//[Factory]
//public class YadaViewModel : LzItemViewModelNotificationsBase<Yada, YadaModel>
//{
//    public YadaViewModel(
//        [FactoryInject] IAuthProcess authProcess,
//        ISessionViewModel sessionViewModel,
//        ILzParentViewModel parentViewModel,
//        Yada Yada,
//        bool? isLoaded = null
//        ) : base(Yada, isLoaded)
//    {
//        ...
//    }
//	...
//}


// Generated Class - implements standard DI factory pattern
//public interface IYadaViewModelFactory
//{
//    YadaViewModel Create(
//        ISessionViewModel sessionViewModel,
//        ILzParentViewModel parentViewModel,
//        Yada item,
//        bool? isLoaded = null);
//}
//public class YadaViewModelFactory : IYadaViewModelFactory, ILzTransient
//{
//    public YadaViewModelFactory(IAuthProcess authProcess)
//    {
//        this.authProcess = authProcess;
//    }

//    private IAuthProcess authProcess;

//    public YadaViewModel Create(ISessionViewModel sessionViewModel, ILzParentViewModel parentViewModel, Yada item, bool? isLoaded = null)
//    {
//        return new YadaViewModel(
//            authProcess,
//            sessionViewModel,
//            parentViewModel,
//            item,
//            isLoaded);
//    }
//}



[Generator]
public class LazyMagicFactoryGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor _messageRule = new DiagnosticDescriptor(
        id: "LMF0001",
        title: "Factory Generator Error",
        messageFormat: "{0}",
        category: "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Get all class declarations with the Factory attribute
        IncrementalValuesProvider<(ClassDeclarationSyntax classNode, SemanticModel semanticModel)> classDeclarations = 
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => s is ClassDeclarationSyntax,
                    transform: (ctx, _) =>
                    {
                        var classNode = (ClassDeclarationSyntax)ctx.Node;
                        var semanticModel = ctx.SemanticModel;
                        if (semanticModel.GetDeclaredSymbol(classNode)?.GetAttributes()
                            .Any(a => a.AttributeClass?.Name == nameof(FactoryAttribute)) == true)
                        {
                            return (classNode, semanticModel);
                        }
                        return default;
                    })
                .Where(tuple => tuple.classNode != null);

        // Register the source output
        context.RegisterSourceOutput(classDeclarations, 
            (spc, tuple) => GenerateFactory(spc, tuple.classNode, tuple.semanticModel));
    }

    private void GenerateFactory(SourceProductionContext context, 
        ClassDeclarationSyntax classNode, 
        SemanticModel model)
    {
        try
        {
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
            context.AddSource($"I{className}Factory.cs", SourceText.From(formattedRoot.ToString(), Encoding.UTF8));

            // Generate registration class
            GenerateRegistrationsClass(context, namespaceName, new List<string> { className });
        }
        catch (Exception ex)
        {
            var diagnostic = Diagnostic.Create(_messageRule, Location.None, ex.Message);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private void GenerateRegistrationsClass(SourceProductionContext context, 
        string namespaceName, 
        List<string> classes)
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

        foreach (var c in classes)
            sourceBuilder.AppendLine($"        services.TryAddTransient<I{c}Factory,{c}Factory>();");

        sourceBuilder.AppendLine($@"
    }}
}}");

        SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceBuilder.ToString());
        SyntaxNode root = tree.GetRoot();
        SyntaxNode formattedRoot = root.NormalizeWhitespace();
        var source = SourceText.From(formattedRoot.ToString(), Encoding.UTF8);
        context.AddSource($"{namespaceName}/RegisterFactories.cs", source);
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

    private string GetNamespace(SemanticModel model, ClassDeclarationSyntax classNode)
    {
        var symbol = model.GetDeclaredSymbol(classNode);
        if (symbol == null)
            return string.Empty;

        var namespaceName = symbol.ContainingNamespace.ToDisplayString();

        return namespaceName;
    }

    private string LowerFirstChar(string name) => name.Substring(0, 1).ToLower() + name.Substring(1);
}
