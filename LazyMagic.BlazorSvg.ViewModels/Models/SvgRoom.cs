namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Represents a room containing multiple sections
/// </summary>
public class SvgRoom
{
    /// <summary>
    /// Unique identifier for the room
    /// </summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> SelectedRoomSectionIds { get; set; } = new List<string>();

    /// <summary>
    /// Sections within this room
    /// </summary>
    public List<SvgRoomSection> RoomSections { get; set; } = new();

    /// <summary>
    /// Joins defining how sections connect
    /// </summary>
    public List<SvgSectionJoin> Joins { get; set; } = new();
}
