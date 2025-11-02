namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Represents how two sections connect
/// </summary>
public class SectionJoin
{
    /// <summary>
    /// Orientation of the join (e.g., "Vertical", "Horizontal", "Intersection")
    /// </summary>
    public string Orientation { get; set; } = string.Empty;

    /// <summary>
    /// ID of the first section
    /// </summary>
    public string Section1Id { get; set; } = string.Empty;

    /// <summary>
    /// ID of the second section
    /// </summary>
    public string Section2Id { get; set; } = string.Empty;
}
