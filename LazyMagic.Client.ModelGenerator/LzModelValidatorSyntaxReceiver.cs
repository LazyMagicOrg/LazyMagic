
namespace LazyMagic.Client.ModelGenerator;
public class LzModelValidatorSyntaxReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> AnnotatedClasses { get; } = new List<ClassDeclarationSyntax>();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        // Check if the node is a class declaration
        if (syntaxNode is ClassDeclarationSyntax classDeclaration)
        {
            // Check each attribute list in the class declaration
            foreach (var attributeList in classDeclaration.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    // Check if the attribute is FeatureAttribute
                    if (attribute.Name.ToString().EndsWith("LzModelValidator"))
                    {
                        AnnotatedClasses.Add(classDeclaration);
                        break; // Break after finding the attribute to avoid adding the same class multiple times
                    }
                }
            }
        }
    }
}