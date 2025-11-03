namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Represents an inscribed rectangle layout for a specific section combination.
/// Supports multiple layout types: max-inscribed, boardroom, and hollow square.
/// </summary>
public class SvgInscribedRectangleLayout
{
    /// <summary>
    /// Type of inscribed rectangle layout
    /// </summary>
    public SvgInscribedRectangleType LayoutType { get; set; }

    /// <summary>
    /// Unique key identifying the section combination (e.g., "Ballroom_Room_1")
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// List of section IDs included in this combination
    /// </summary>
    public List<string> Sections { get; set; } = new();

    /// <summary>
    /// The computed inscribed rectangle
    /// </summary>
    public SvgRectangle? Rectangle { get; set; }

    /// <summary>
    /// Area of the original polygon
    /// </summary>
    public double PolygonArea { get; set; }

    /// <summary>
    /// Area of the inscribed rectangle
    /// </summary>
    public double InscribedArea { get; set; }

    /// <summary>
    /// Time taken to compute this rectangle in milliseconds
    /// </summary>
    public int ComputationTimeMs { get; set; }

    // Type-specific properties (optional, populated based on LayoutType)

    /// <summary>
    /// Algorithm used for computation (e.g., "boundary-based", "Dedicated")
    /// </summary>
    public string? Algorithm { get; set; }

    /// <summary>
    /// Boardroom-specific: Configuration used to generate this layout
    /// </summary>
    public SvgBoardroomConfiguration? BoardroomConfig { get; set; }

    /// <summary>
    /// Boardroom-specific: List of furniture elements in the layout
    /// </summary>
    public List<SvgFurnitureElement>? FurnitureElements { get; set; }

    /// <summary>
    /// Boardroom-specific: Number of table sets
    /// </summary>
    public int? Sets { get; set; }

    /// <summary>
    /// Boardroom-specific: Total number of tables
    /// </summary>
    public int? Tables { get; set; }

    /// <summary>
    /// Boardroom-specific: Orientation (e.g., "width×length", "length×width")
    /// </summary>
    public string? Orientation { get; set; }

    /// <summary>
    /// Additional algorithm-specific metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
