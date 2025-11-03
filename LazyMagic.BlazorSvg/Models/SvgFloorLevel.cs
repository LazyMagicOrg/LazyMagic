namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Represents a complete floor level with embedded layout data
/// </summary>
public class SvgFloorLevel
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
    public SvgDiagramInfo? Diagram { get; set; }

    /// <summary>
    /// Rooms in this level
    /// </summary>
    public List<SvgRoom> Rooms { get; set; } = new();

    /// <summary>
    /// All precomputed inscribed rectangle layouts embedded in SVG
    /// (includes max-inscribed, boardroom, and hollow square types)
    /// </summary>
    public SvgInscribedRectangleData? InscribedRectangles { get; set; }
}
