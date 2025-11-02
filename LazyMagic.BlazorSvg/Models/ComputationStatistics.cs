namespace LazyMagic.BlazorSvg.Models;

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
