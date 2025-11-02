namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Precomputed boardroom layout data (embedded in SVG defs section)
/// </summary>
public class BoardroomLayoutData
{
    /// <summary>
    /// Timestamp when this data was generated
    /// </summary>
    public string GeneratedAt { get; set; } = string.Empty;

    /// <summary>
    /// Project name
    /// </summary>
    public string Project { get; set; } = string.Empty;

    /// <summary>
    /// Total number of section combinations processed
    /// </summary>
    public int TotalCombinations { get; set; }

    /// <summary>
    /// Number of successful computations
    /// </summary>
    public int SuccessfulComputations { get; set; }

    /// <summary>
    /// Number of failed computations
    /// </summary>
    public int FailedComputations { get; set; }

    /// <summary>
    /// Statistical information about computation performance
    /// </summary>
    public ComputationStatistics? Statistics { get; set; }

    /// <summary>
    /// List of all computed boardroom layouts
    /// </summary>
    public List<BoardroomLayout> BoardroomLayouts { get; set; } = new();
}
