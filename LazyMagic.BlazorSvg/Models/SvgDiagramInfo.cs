namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Diagram metadata including measurement units
/// </summary>
public class SvgDiagramInfo
{
    /// <summary>
    /// Path to the SVG image
    /// </summary>
    public string Image { get; set; } = string.Empty;

    /// <summary>
    /// Measurement unit system (e.g., "Imperial", "Metric")
    /// </summary>
    public string MeasureUnits { get; set; } = string.Empty;

    /// <summary>
    /// Specific unit of measurement (e.g., "ft", "m")
    /// </summary>
    public string Unit { get; set; } = string.Empty;
}
