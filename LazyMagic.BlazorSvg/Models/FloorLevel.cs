namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Represents a complete floor level with embedded layout data
/// </summary>
public class FloorLevel
{
    /// <summary>
    /// Unique identifier for the level
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the level
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Diagram metadata
    /// </summary>
    public DiagramInfo? Diagram { get; set; }

    /// <summary>
    /// Rooms in this level
    /// </summary>
    public List<Room> Rooms { get; set; } = new();

    /// <summary>
    /// All precomputed inscribed rectangle layouts embedded in SVG
    /// (includes max-inscribed, boardroom, and hollow square types)
    /// </summary>
    public InscribedRectangleData? InscribedRectangles { get; set; }
}
