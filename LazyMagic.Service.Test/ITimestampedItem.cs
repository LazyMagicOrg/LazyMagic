using LazyMagic.Shared;

namespace LazyMagic.Service.Test;

public interface ITimestampedItem : IItem
{
    public long CreateUtcTick { get; set; }
    public long UpdateUtcTick { get; set; }
} 