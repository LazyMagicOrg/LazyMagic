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
    /// Precomputed max-inscribed rectangle data embedded in SVG
    /// </summary>
    public MaxInscribedData? MaxInscribedData { get; set; }

    /// <summary>
    /// Precomputed boardroom layout data embedded in SVG
    /// </summary>
    public BoardroomLayoutData? BoardroomData { get; set; }

    /// <summary>
    /// Precomputed hollow square layout data embedded in SVG
    /// </summary>
    public HollowSquareLayoutData? HollowSquareData { get; set; }
}
