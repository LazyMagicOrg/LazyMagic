namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Represents an individual section within a room (maps to SVG path element)
/// </summary>
public class RoomSection
{
    /// <summary>
    /// Unique identifier for the section (matches SVG path id)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the section
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the section
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Type of section (e.g., "Aisle", "Crossing", null for regular rooms)
    /// </summary>
    public string? SectionType { get; set; }

    /// <summary>
    /// Layout restriction for the section (e.g., "allowed", "prohibited")
    /// </summary>
    public string LayoutRestriction { get; set; } = "allowed";

    /// <summary>
    /// SVG path data (the "d" attribute)
    /// </summary>
    public string? PathData { get; set; }

    /// <summary>
    /// SVG style attribute
    /// </summary>
    public string? Style { get; set; }

    /// <summary>
    /// Inkscape label attribute
    /// </summary>
    public string? InkscapeLabel { get; set; }

    /// <summary>
    /// Computed polygon coordinates
    /// </summary>
    public List<Point>? PolygonCoordinates { get; set; }

    /// <summary>
    /// Computed polygon area (calculated from path coordinates)
    /// </summary>
    public double? PolygonArea { get; set; }

    /// <summary>
    /// FloorMat embedded area (from floormat:area attribute, may differ from PolygonArea)
    /// </summary>
    public double? FloormatArea { get; set; }

    /// <summary>
    /// Width dimension from floormat:width (optional)
    /// </summary>
    public double? Width { get; set; }

    /// <summary>
    /// Depth dimension from floormat:depth (optional)
    /// </summary>
    public double? Depth { get; set; }
}
