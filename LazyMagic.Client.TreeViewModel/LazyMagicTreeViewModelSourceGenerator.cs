namespace LazyMagic.Client.TreeViewModel;

[Generator]
public class LazyMagicTreeViewModelSourceGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context) { }

    public void Execute(GeneratorExecutionContext context)
    {

        foreach (var syntaxTree in context.Compilation.SyntaxTrees)
        {
            var model = context.Compilation.GetSemanticModel(syntaxTree);
            var classesWithTreeNodeAttribute = syntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(x => model.GetDeclaredSymbol(x)!.GetAttributes().Any(a => a.AttributeClass!.Name == nameof(TreeNodeAttribute)));

            foreach (var classNode in classesWithTreeNodeAttribute)
            {
                var sourceBuilder = new StringBuilder();
                var ViewModelsPropetyExists = PropertyExists(classNode, model, "ViewModels");
                var className = classNode.Identifier.Text;
                var namespaceName = GetNamespace(context, model, classNode);

                var treeNodeNamePropertyName = classNode.DescendantNodes()
                    .OfType<PropertyDeclarationSyntax>()
                    .Where(x => model.GetDeclaredSymbol(x)!.GetAttributes().Any(a => a.AttributeClass!.Name == nameof(TreeNodeNameAttribute)))
                    .FirstOrDefault()?.Identifier.Text; 

                var treeNodeParallellMaxAttribute = classNode.DescendantNodes()
                    .OfType<PropertyDeclarationSyntax>()
                    .Where(x => model.GetDeclaredSymbol(x)!.GetAttributes().Any(a => a.AttributeClass!.Name == nameof(TreeNodeParallellMaxAttribute)))
                    .FirstOrDefault()?.Identifier.Text;

                var treeNodeIsFolderAttribute = classNode.DescendantNodes()
                    .OfType<PropertyDeclarationSyntax>()
                    .Where(x => model.GetDeclaredSymbol(x)!.GetAttributes().Any(a => a.AttributeClass!.Name == nameof(TreeNodeIsFolderAttribute)))
                    .FirstOrDefault()?.Identifier.Text;

				var treeNodePageAttribute = classNode.DescendantNodes()
					.OfType<PropertyDeclarationSyntax>()
					.Where(x => model.GetDeclaredSymbol(x)!.GetAttributes().Any(a => a.AttributeClass!.Name == nameof(TreeNodePageAttribute)))
					.FirstOrDefault()?.Identifier.Text;

				sourceBuilder.Append(@$"
using System.Linq;
using LazyMagic.Client.TreeViewModel;
namespace {namespaceName}
{{
    public partial class {className} : ILzTreeNode
    {{
        public async Task<ILzTreeNodeViewModel> GetTreeNodeAsync()
        {{
            var nodeList = new List<ILzTreeNode>();
");
                if (ViewModelsPropetyExists)
                    sourceBuilder.Append(@$"
            nodeList.AddRange(ViewModels.Values.Cast<ILzTreeNode>());");

                var propertiesWithTreeNodeChildAttribute = classNode.DescendantNodes()
                    .OfType<PropertyDeclarationSyntax>()
                    .Where(x => model.GetDeclaredSymbol(x)!.GetAttributes().Any(a => a.AttributeClass!.Name == nameof(TreeNodeChildAttribute)));

                foreach (var propNode in propertiesWithTreeNodeChildAttribute)
                {
                    var propName = propNode.Identifier.Text;
                    sourceBuilder.Append(@$"
            nodeList.Add({propName}! as ILzTreeNode);");
                }
                var hasChildren = propertiesWithTreeNodeChildAttribute.Count() > 0; // || ViewModelsPropetyExists;
                if (hasChildren)
                {
                    if (treeNodeParallellMaxAttribute is not null)
                        sourceBuilder.Append($@"
            var semaphore = new SemaphoreSlim({treeNodeParallellMaxAttribute}); // limit parallel execution 
");
                    else
                        sourceBuilder.Append($@"
            var semaphore = new SemaphoreSlim(100); // limit parallel execution 
");
                    sourceBuilder.Append($@"
            var tasks = nodeList.Select(async x =>
            {{
                await semaphore.WaitAsync();
                try 
                {{
                    return await x.GetTreeNodeAsync();
                }}
                finally
                {{
                    semaphore.Release();
                }}
            }}).ToList();
            var childrenList = (await Task.WhenAll(tasks)).ToList();
");
                }
                else
                    sourceBuilder.Append(@"
            await Task.Delay(0);
");
                sourceBuilder.Append(@"
            var node = new LzTreeNodeViewModel(
                viewModel: this,
                viewModelType: this.GetType(),");
                if (treeNodeNamePropertyName is not null)
                    sourceBuilder.Append($@"
                text: {treeNodeNamePropertyName},");
                else
                    sourceBuilder.Append($@"
                text: ""{className}"",");
                if (treeNodeIsFolderAttribute is not null)
                    sourceBuilder.Append($@"
                isFolder: {treeNodeIsFolderAttribute},");
                else
                    sourceBuilder.Append($@"
                isFolder: false,");
                if (treeNodePageAttribute is not null)
					sourceBuilder.Append($@"
                page: {treeNodePageAttribute}");
				else
					sourceBuilder.Append($@"
                page: """"");
				if (hasChildren)
                    sourceBuilder.Append($@",
                children: childrenList");
                sourceBuilder.Append(@"
                );
            return node;
        }
    }
}");
                context.AddSource($"{className}.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
            }
        }
    }
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
        id: "LZSG0001",
        title: "LazyMagic.Client.TreeViewModel Source Generator Message",
        messageFormat: "{0}",
        category: "SourceGenerator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

}
