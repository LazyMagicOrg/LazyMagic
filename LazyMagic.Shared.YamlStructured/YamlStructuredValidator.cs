// Teal Penguin - Start
namespace LazyMagic.Shared.YamlStructured;

using YamlDotNet.Core;
using YamlDotNet.Serialization;

/// <summary>
/// Validates and repairs YAML structured responses.
/// Shared between client and service to ensure deterministic repair behavior.
/// </summary>
public class YamlStructuredValidator
{
    private readonly IDeserializer _deserializer;

    public YamlStructuredValidator()
    {
        _deserializer = new DeserializerBuilder()
            .Build();
    }

    /// <summary>
    /// Validates YAML text against a generated type
    /// </summary>
    /// <param name="yamlText">YAML content to validate</param>
    /// <param name="targetType">Expected type (from PayloadTemplateTypeGenerator)</param>
    /// <returns>Validation result with errors if any</returns>
    public ValidationResult Validate(string yamlText, Type targetType)
    {
        var result = new ValidationResult();

        try
        {
            // Attempt to deserialize
            var instance = _deserializer.Deserialize(yamlText, targetType);

            if (instance == null)
            {
                result.IsValid = false;
                result.Errors.Add("Deserialization resulted in null instance");
                return result;
            }

            // Check for required properties (all properties expected to have values)
            var properties = targetType.GetProperties();
            foreach (var prop in properties)
            {
                var value = prop.GetValue(instance) as string;
                if (string.IsNullOrEmpty(value))
                {
                    result.Warnings.Add($"Property '{prop.Name}' is null or empty");
                }
            }

            result.IsValid = true;
            result.Instance = instance;
        }
        catch (YamlException ex)
        {
            result.IsValid = false;
            result.Errors.Add($"YAML parsing error: {ex.Message}");
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Validation error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Attempts to repair malformed YAML text.
    /// Uses deterministic rules to ensure client and service produce identical results.
    /// </summary>
    /// <param name="yamlText">Potentially malformed YAML</param>
    /// <returns>Repaired YAML text</returns>
    public string Repair(string yamlText)
    {
        if (string.IsNullOrEmpty(yamlText))
            return yamlText;

        // TODO: Implement repair logic
        // For now, just return as-is
        // Future: Add common repairs like:
        // - Fixing indentation
        // - Escaping special characters
        // - Completing truncated block scalars
        // - Removing invalid characters

        return yamlText;
    }
}

/// <summary>
/// Result of YAML validation
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public object? Instance { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
// Teal Penguin - End
