namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Represents a rectangle definition with corners, dimensions, and orientation
/// </summary>
public class SvgRectangle
{
    /// <summary>
    /// Four corner points of the rectangle
    /// </summary>
    public List<SvgPoint> Corners { get; set; } = new();

    /// <summary>
    /// Width of the rectangle
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Height of the rectangle
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// Area of the rectangle (width * height)
    /// </summary>
    public double Area { get; set; }

    /// <summary>
    /// Rotation angle in degrees
    /// </summary>
    public double Angle { get; set; }

    /// <summary>
    /// Centroid (center point) of the rectangle
    /// </summary>
    public SvgPoint Centroid { get; set; } = new();
}
