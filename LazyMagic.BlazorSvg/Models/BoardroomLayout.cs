namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Boardroom layout for a specific section combination
/// </summary>
public class BoardroomLayout
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
    /// Configuration used to generate this layout
    /// </summary>
    public BoardroomConfiguration? Config { get; set; }

    /// <summary>
    /// List of furniture elements in the layout
    /// </summary>
    public List<FurnitureElement> Elements { get; set; } = new();

    /// <summary>
    /// Area of the original polygon
    /// </summary>
    public double PolygonArea { get; set; }

    /// <summary>
    /// Time taken to compute this layout in milliseconds
    /// </summary>
    public int ComputationTimeMs { get; set; }
}
