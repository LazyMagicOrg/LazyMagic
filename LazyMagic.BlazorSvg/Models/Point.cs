namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Represents a 2D point for polygon coordinates and geometric calculations
/// </summary>
public class Point
{
    public double X { get; set; }
    public double Y { get; set; }

    public Point() { }

    public Point(double x, double y)
    {
        X = x;
        Y = y;
    }
}
