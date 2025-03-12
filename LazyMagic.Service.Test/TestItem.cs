using LazyMagic.Shared;

namespace LazyMagic.Service.Test;

public class TestItem : ITimestampedItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long CreateUtcTick { get; set; }
    public long UpdateUtcTick { get; set; }
}