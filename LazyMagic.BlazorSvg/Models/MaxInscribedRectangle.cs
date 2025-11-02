namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Max-inscribed rectangle for a specific section combination
/// </summary>
public class MaxInscribedRectangle
{
    /// <summary>
    /// Unique key identifying the section combination (e.g., "Ballroom_Room_1")
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// List of section IDs included in this combination
    /// </summary>
    public List<string> Sections { get; set; } = new();

    /// <summary>
    /// The computed max-inscribed rectangle
    /// </summary>
    public Rectangle? MaxRectangle { get; set; }

    /// <summary>
    /// Area of the original polygon
    /// </summary>
    public double PolygonArea { get; set; }

    /// <summary>
    /// Area of the max-inscribed rectangle
    /// </summary>
    public double RectangleArea { get; set; }

    /// <summary>
    /// Time taken to compute this rectangle in milliseconds
    /// </summary>
    public int ComputationTimeMs { get; set; }
}
