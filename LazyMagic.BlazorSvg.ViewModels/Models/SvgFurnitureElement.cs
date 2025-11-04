namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Represents a furniture element in a boardroom layout
/// </summary>
public class SvgFurnitureElement
{
    /// <summary>
    /// Type of furniture (e.g., "table", "chair", "podium")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Position of the furniture element
    /// </summary>
    public SvgPoint Position { get; set; } = new();

    /// <summary>
    /// Rotation angle in degrees
    /// </summary>
    public double Rotation { get; set; }

    /// <summary>
    /// Width of the furniture element
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Height (or depth) of the furniture element
    /// </summary>
    public double Height { get; set; }
}
