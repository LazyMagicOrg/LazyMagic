namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Configuration settings for boardroom layout generation
/// </summary>
public class SvgBoardroomConfiguration
{
    /// <summary>
    /// Width of the boardroom table
    /// </summary>
    public double TableWidth { get; set; }

    /// <summary>
    /// Length of the boardroom table
    /// </summary>
    public double TableLength { get; set; }

    /// <summary>
    /// Width of chairs
    /// </summary>
    public double ChairWidth { get; set; }

    /// <summary>
    /// Depth of chairs
    /// </summary>
    public double ChairDepth { get; set; }

    /// <summary>
    /// Spacing between chairs
    /// </summary>
    public double ChairSpacing { get; set; }

    /// <summary>
    /// Distance from table edge to chairs
    /// </summary>
    public double TableChairGap { get; set; }
}
