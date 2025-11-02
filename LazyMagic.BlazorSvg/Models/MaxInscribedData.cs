namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Precomputed max-inscribed rectangle data (embedded in SVG defs section)
/// </summary>
public class MaxInscribedData
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
    /// List of all computed max-inscribed rectangles
    /// </summary>
    public List<MaxInscribedRectangle> Rectangles { get; set; } = new();
}

/// <summary>
/// Statistical information about computation performance
/// </summary>
public class ComputationStatistics
{
    /// <summary>
    /// Total number of layouts computed
    /// </summary>
    public int TotalLayouts { get; set; }

    /// <summary>
    /// Total computation time in milliseconds
    /// </summary>
    public int TotalComputationTimeMs { get; set; }

    /// <summary>
    /// Average computation time in milliseconds
    /// </summary>
    public double AverageComputationTimeMs { get; set; }

    /// <summary>
    /// Total computation time in seconds (formatted string)
    /// </summary>
    public string TotalComputationTimeSec { get; set; } = string.Empty;

    /// <summary>
    /// Average computation time in seconds (formatted string)
    /// </summary>
    public string AverageComputationTimeSec { get; set; } = string.Empty;
}
