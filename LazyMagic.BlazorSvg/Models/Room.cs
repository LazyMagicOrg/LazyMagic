namespace LazyMagic.BlazorSvg.Models;

/// <summary>
/// Represents a room containing multiple sections
/// </summary>
public class Room
{
    /// <summary>
    /// Unique identifier for the room
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Sections within this room
    /// </summary>
    public List<RoomSection> RoomSections { get; set; } = new();

    /// <summary>
    /// Joins defining how sections connect
    /// </summary>
    public List<SectionJoin> Joins { get; set; } = new();
}
