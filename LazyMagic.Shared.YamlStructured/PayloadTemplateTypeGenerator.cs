// Teal Penguin - Start
namespace LazyMagic.Shared.YamlStructured;

using System.Reflection;
using System.Reflection.Emit;
using YamlDotNet.Serialization;

/// <summary>
/// Generates runtime types from payload template YAML definitions.
/// Each template becomes a strongly-typed class with properties matching the YAML structure.
/// </summary>
public class PayloadTemplateTypeGenerator
{
    private readonly ModuleBuilder _moduleBuilder;
    private readonly Dictionary<string, Type> _generatedTypes = new();

    public PayloadTemplateTypeGenerator(string assemblyName = "DynamicPayloadTemplates")
    {
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(assemblyName),
            AssemblyBuilderAccess.Run);

        _moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
    }

    /// <summary>
    /// Generates a type from a payload template YAML string.
    /// The YAML's top-level keys become properties on the generated type.
    /// </summary>
    /// <param name="templateId">Unique identifier for this template (used as type name)</param>
    /// <param name="templateYaml">YAML template text</param>
    /// <returns>Generated type with properties matching YAML structure</returns>
    public Type GenerateType(string templateId, string templateYaml)
    {
        if (_generatedTypes.TryGetValue(templateId, out var existingType))
            return existingType;

        // Parse YAML to discover property structure
        var deserializer = new DeserializerBuilder().Build();
        var templateStructure = deserializer.Deserialize<Dictionary<object, object>>(templateYaml);

        if (templateStructure == null)
            throw new ArgumentException($"Failed to parse template YAML for: {templateId}");

        // Sanitize template ID for valid C# type name
        var typeName = SanitizeTypeName(templateId);

        // Define the type
        var typeBuilder = _moduleBuilder.DefineType(
            $"PayloadTemplate_{typeName}",
            TypeAttributes.Public | TypeAttributes.Class);

        // Add properties for each top-level YAML key
        foreach (var key in templateStructure.Keys)
        {
            var propertyName = key.ToString() ?? throw new ArgumentException($"Null property name in template: {templateId}");

            // All properties are strings (YAML content stored as text)
            AddProperty(typeBuilder, propertyName, typeof(string));
        }

        // Create the type
        var generatedType = typeBuilder.CreateType()
            ?? throw new InvalidOperationException($"Failed to create type for: {templateId}");

        _generatedTypes[templateId] = generatedType;
        return generatedType;
    }

    /// <summary>
    /// Gets a previously generated type by template ID
    /// </summary>
    public Type? GetGeneratedType(string templateId)
    {
        return _generatedTypes.TryGetValue(templateId, out var type) ? type : null;
    }

    /// <summary>
    /// Gets all generated types
    /// </summary>
    public IReadOnlyDictionary<string, Type> GetAllGeneratedTypes() => _generatedTypes;

    private void AddProperty(TypeBuilder typeBuilder, string propertyName, Type propertyType)
    {
        // Define backing field
        var fieldBuilder = typeBuilder.DefineField(
            $"_{propertyName}",
            propertyType,
            FieldAttributes.Private);

        // Define property
        var propertyBuilder = typeBuilder.DefineProperty(
            propertyName,
            PropertyAttributes.HasDefault,
            propertyType,
            null);

        // Define getter
        var getterBuilder = typeBuilder.DefineMethod(
            $"get_{propertyName}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            propertyType,
            Type.EmptyTypes);

        var getterIL = getterBuilder.GetILGenerator();
        getterIL.Emit(OpCodes.Ldarg_0);
        getterIL.Emit(OpCodes.Ldfld, fieldBuilder);
        getterIL.Emit(OpCodes.Ret);

        // Define setter
        var setterBuilder = typeBuilder.DefineMethod(
            $"set_{propertyName}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            null,
            new[] { propertyType });

        var setterIL = setterBuilder.GetILGenerator();
        setterIL.Emit(OpCodes.Ldarg_0);
        setterIL.Emit(OpCodes.Ldarg_1);
        setterIL.Emit(OpCodes.Stfld, fieldBuilder);
        setterIL.Emit(OpCodes.Ret);

        // Attach getter and setter to property
        propertyBuilder.SetGetMethod(getterBuilder);
        propertyBuilder.SetSetMethod(setterBuilder);
    }

    private string SanitizeTypeName(string templateId)
    {
        // Replace invalid characters with underscores
        var sanitized = new string(templateId.Select(c =>
            char.IsLetterOrDigit(c) ? c : '_').ToArray());

        // Ensure it starts with a letter
        if (!char.IsLetter(sanitized[0]))
            sanitized = "T_" + sanitized;

        return sanitized;
    }
}
// Teal Penguin - End
