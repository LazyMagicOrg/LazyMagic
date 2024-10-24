using System.Diagnostics;
using System.Runtime.Serialization;

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
public class LazyMagicFactoryGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context) { }

    public void Execute(GeneratorExecutionContext context)
    {
        try
        {
            // Collect the classes and namespaces so we can create a registration class for generated factories
            List<string> classes = new(); // class
            HashSet<string> namespaces = new();
            Dictionary<string, List<string>> registrations = new();

            foreach (var syntaxTree in context.Compilation.SyntaxTrees)
            {
                // Find the classes that have the Factory attribute
                var model = context.Compilation.GetSemanticModel(syntaxTree);
                var classesWithFactoryAttribute = syntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Where(x => model.GetDeclaredSymbol(x)!.GetAttributes().Any(a => a.AttributeClass!.Name == nameof(FactoryAttribute)));

                // Generate the factory class for each class that has the Factory attribute
                foreach (var classNode in classesWithFactoryAttribute)
                {

                    var className = classNode.Identifier.Text;
                    classes.Add(className);
                    var namespaceName = GetNamespace(context, model, classNode);
                    namespaces.Add(namespaceName);

                    if (registrations.TryGetValue(namespaceName, out var existingClasses ))
                        existingClasses.Add(className);
                    else
                        registrations.Add(namespaceName, new List<string> { className });

                    var constructor = classNode.DescendantNodes().OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
                    var constructorText = constructor?.ToFullString();

                    // Grab the parameters that have the FactoryInjectAttribute and prune the attribute from them
                    var injectedParameters = constructor?.ParameterList.Parameters
                     .Where(param => model.GetDeclaredSymbol(param) is IParameterSymbol paramSymbol &&
                                     paramSymbol.GetAttributes().Any(a => a.AttributeClass!.Name == nameof(FactoryInjectAttribute)))
                     .ToList();
                    injectedParameters = RemoveFactoryInjectAttributeFromParameters(injectedParameters, model);
                    var injectedParametersText = SyntaxFactory.SeparatedList(injectedParameters).ToFullString();

                    // Generate private variables for the injected parameters
                    var privateVariables = "";
                    foreach(var param in injectedParameters)
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

                    // Grad the parameters that are not injected
                    var nonInjectedParameters = constructor?.ParameterList.Parameters
                        .Where(param => model.GetDeclaredSymbol(param) is IParameterSymbol paramSymbol &&
                                                               !paramSymbol.GetAttributes().Any(a => a.AttributeClass!.Name == nameof(FactoryInjectAttribute)))
                        .ToList();
                    var nonInjectedParametersText = SyntaxFactory.SeparatedList(nonInjectedParameters).ToFullString();

                    // Grab the arguments list (which includes all the parameters)
                    var arguments = constructor?.ParameterList.Parameters.Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier))).ToList();
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
    public class {className}Factory : I{className}Factory, ILzTransient
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
}}
");
                    // Log(context, $"source: {sourceBuilder.ToString()}");
                    SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceBuilder.ToString());
                    SyntaxNode root = tree.GetRoot();
                    SyntaxNode formattedRoot = root.NormalizeWhitespace();
                    context.AddSource($"I{className}Factory.cs", SourceText.From(formattedRoot.ToString(), Encoding.UTF8));

                }

            }

            GenerateRegistrationsClass(context, registrations);

        }
        catch (Exception ex)
        {
            var diagnostic = Diagnostic.Create(_messageRule, Location.None, ex.Message + "01");
            context.ReportDiagnostic(diagnostic);
        }   
    }

    private void GenerateRegistrationsClass(GeneratorExecutionContext context, Dictionary<string, List<string>> registraitons)
    {
        foreach (var reg in registraitons)
        {
            var namespaceName = reg.Key;
            var classes = reg.Value;

            var sourceBuilder = new StringBuilder();
            sourceBuilder.AppendLine(@$"
// <auto-generated />
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
");

            sourceBuilder.AppendLine(@$"
namespace {namespaceName};
");

            //        foreach (var c in classes)
            //            sourceBuilder.AppendLine(@$"
            // public partial interface I{c}Factory {{}}
            //");

            sourceBuilder.AppendLine(@$"
public static class RegisterFactories
{{
    public static void Register(IServiceCollection services)
    {{
");

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
            paramSymbol.GetAttributes().Any(a => a.AttributeClass!.Name == nameof(FactoryInjectAttribute) || a.AttributeClass!.Name == nameof(FactoryInjectAttribute) + "Attribute"))
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

    private ParameterListSyntax CreateParameterList(List<(string Type, string Name)> parameters)
    {
        var syntaxParameters = new SeparatedSyntaxList<ParameterSyntax>();

        foreach (var param in parameters)
        {
            var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(param.Name))
                                          .WithType(SyntaxFactory.ParseTypeName(param.Type));
            syntaxParameters = syntaxParameters.Add(parameter);
        }

        return SyntaxFactory.ParameterList(syntaxParameters);
    }


    private void Log(GeneratorExecutionContext context, string? message)
    {
        if (message == null) return;
        string[] lines = message.Split(new[] { '\n' }, StringSplitOptions.None); 
        foreach(var line in lines)
        {
            var diagnostic = Diagnostic.Create(_messageRule, Location.None, line);
            context.ReportDiagnostic(diagnostic);
        }
    }
    private  MethodDeclarationSyntax GenerateCreateMethod(GeneratorExecutionContext context, SemanticModel model, ConstructorDeclarationSyntax constructor)
    {
        // 1. Get the class name from the constructor's parent node.
        var className = ((ClassDeclarationSyntax)constructor.Parent).Identifier;


        // 2. Get the parameters from the constructor.
        var parameters = constructor.ParameterList.Parameters;
        var prunedParameters = RemoveFactoryInjectAttributeFromParameters(parameters.ToList(), model);
        parameters = SyntaxFactory.SeparatedList(prunedParameters);
        foreach(var param in parameters)
        {
            var parameterText = param.ToFullString();
            //Log(context, parameterText);
        }

        // 3. Generate the Create method.
        // Construct the argument list from the parameters
        var arguments = parameters.Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier)));
        ArgumentListSyntax argumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments));

        // Create the method body using the arguments to call the class constructor.
        ObjectCreationExpressionSyntax creationExpression = SyntaxFactory.ObjectCreationExpression(
            SyntaxFactory.IdentifierName(className),
            argumentList,
            null);

        ReturnStatementSyntax returnStatement = SyntaxFactory.ReturnStatement(creationExpression);

        // 4. The Create method will return an instance of the class.
        MethodDeclarationSyntax createMethod = SyntaxFactory.MethodDeclaration(SyntaxFactory.IdentifierName(className), "Create")
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
            .WithParameterList(SyntaxFactory.ParameterList(parameters))
            .WithBody(SyntaxFactory.Block(returnStatement));

        return createMethod;
    }


    //private MethodDeclarationSyntax GenerateCreateMethod(ConstructorDeclarationSyntax constructor, ParameterListSyntax parameters)
    //{
    //    var className = ((ClassDeclarationSyntax)constructor.Parent).Identifier;


    //    // Update the ObjectCreationExpression to use the merged parameters
    //    var creationExpression = SyntaxFactory.ObjectCreationExpression(
    //        SyntaxFactory.IdentifierName(className),
    //        SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(parameters),
    //        null);

    //    var returnStatement = SyntaxFactory.ReturnStatement(creationExpression);

    //    var createMethod = SyntaxFactory.MethodDeclaration(SyntaxFactory.IdentifierName(className), "Create")
    //        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
    //        .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(mergedParameters)))
    //        .WithBody(SyntaxFactory.Block(returnStatement));

    //    return createMethod;
    //}
    private bool PropertyExists(ClassDeclarationSyntax classNode, SemanticModel model, string propertyName)
    {
        var classSymbol = model.GetDeclaredSymbol(classNode) as INamedTypeSymbol;
        if (classSymbol == null) return false;

        if (HasProperty(classSymbol, propertyName))
            return true;

        var baseType = classSymbol.BaseType;
        while (baseType != null)
        {
            if (HasProperty(baseType, propertyName))
                return true;

            baseType = baseType.BaseType;
        }

        return false;  // The property was not found on the derived class or any of its base classes.
    }

    private bool HasProperty(INamedTypeSymbol typeSymbol, string propertyName)
    {
        return typeSymbol.GetMembers(propertyName).Any(m => m.Kind == SymbolKind.Property);
    }


    private string GetNamespace(GeneratorExecutionContext context, SemanticModel model, ClassDeclarationSyntax classNode)
    {
        var namespaceName = string.Empty;


        var classSymbol = model.GetDeclaredSymbol(classNode) as INamedTypeSymbol;

        if (classSymbol != null)
            namespaceName = classSymbol.ContainingNamespace.ToString();
        else
        {
            var diagnostic = Diagnostic.Create(_messageRule, Location.None, "Namespace not found.");
            context.ReportDiagnostic(diagnostic);
        }
        return namespaceName;
    }

    private static readonly DiagnosticDescriptor _messageRule = new DiagnosticDescriptor(
        id: "LZSG0002",
        title: "LazyMagic.Client.FactoryGenerator Source Generator Message",
        messageFormat: "{0}",
        category: "SourceGenerator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private string LowerFirstChar(string name) => name.Substring(0, 1).ToLower() + name.Substring(1);
}
