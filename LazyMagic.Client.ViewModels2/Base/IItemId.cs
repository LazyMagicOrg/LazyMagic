using System;

namespace LazyMagic.Client.ViewModels;

public interface IItemId<TId>
{
    public TId? Id { get; }
}
