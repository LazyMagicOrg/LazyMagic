namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Hollow square layout for a specific section combination
/// </summary>
public class HollowSquareLayout
{
    /// <summary>
    /// Unique key identifying the section combination
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// List of section IDs included in this combination
    /// </summary>
    public List<string> Sections { get; set; } = new();

    /// <summary>
    /// The computed hollow square rectangle
    /// </summary>
    public Rectangle? HollowSquareRectangle { get; set; }

    /// <summary>
    /// Area of the original polygon
    /// </summary>
    public double PolygonArea { get; set; }

    /// <summary>
    /// Area of the hollow square layout
    /// </summary>
    public double HollowSquareArea { get; set; }

    /// <summary>
    /// Time taken to compute this layout in milliseconds
    /// </summary>
    public int ComputationTimeMs { get; set; }
}
