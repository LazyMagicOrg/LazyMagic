namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Represents a 2D point for polygon coordinates and geometric calculations
/// </summary>
public class SvgPoint
{
    public double X { get; set; }
    public double Y { get; set; }

    public SvgPoint() { }

    public SvgPoint(double x, double y)
    {
        X = x;
        Y = y;
    }
}
