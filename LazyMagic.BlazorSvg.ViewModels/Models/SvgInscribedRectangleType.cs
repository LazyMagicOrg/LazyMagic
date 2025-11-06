namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Type of inscribed rectangle layout
/// </summary>
public enum SvgInscribedRectangleType
{
    /// <summary>
    /// Maximum inscribed rectangle - largest rectangle fitting in polygon
    /// </summary>
    MaxInscribed,

    /// <summary>
    /// Boardroom layout - fixed-width table arrangement (13 ft × variable)
    /// </summary>
    Boardroom,

    /// <summary>
    /// Hollow square layout - perimeter table arrangement with open center
    /// </summary>
    HollowSquare,

    /// <summary>
    /// U-shape layout - three-sided table arrangement with one open side
    /// </summary>
    UShape,

    None
}
