using LazyMagic.Service.Shared;
using LazyMagic.Shared;
using Microsoft.AspNetCore.Mvc;

namespace LazyMagic.Service.Test;

public class MockDocumentRepo<T> : IDocumentRepo<T> where T : class, ITimestampedItem, new()
{
    private readonly Dictionary<string, T> _items = new();

    public Task<ActionResult<T>> CreateAsync(ICallerInfo callerInfo, T data)
    {
        if (string.IsNullOrEmpty(data.Id))
        {
            data.Id = Guid.NewGuid().ToString();
        }

        if (_items.ContainsKey(data.Id))
        {
            return Task.FromResult(new ActionResult<T>(new BadRequestResult()));
        }

        data.CreateUtcTick = DateTime.UtcNow.Ticks;
        data.UpdateUtcTick = data.CreateUtcTick;
        _items[data.Id] = data;
        return Task.FromResult(new ActionResult<T>(data));
    }

    public Task<ActionResult<T>> ReadAsync(ICallerInfo callerInfo, string id)
    {
        if (_items.TryGetValue(id, out var item))
        {
            return Task.FromResult(new ActionResult<T>(item));
        }
        return Task.FromResult(new ActionResult<T>(new NotFoundResult()));
    }

    public Task<ActionResult<T>> UpdateAsync(ICallerInfo callerInfo, T data, bool forceupdate = false)
    {
        if (!_items.ContainsKey(data.Id))
        {
            return Task.FromResult(new ActionResult<T>(new NotFoundResult()));
        }

        var existingItem = _items[data.Id];
        if (!forceupdate && existingItem.UpdateUtcTick != data.UpdateUtcTick)
        {
            return Task.FromResult(new ActionResult<T>(new ConflictResult()));
        }

        data.UpdateUtcTick = DateTime.UtcNow.Ticks;
        _items[data.Id] = data;
        return Task.FromResult(new ActionResult<T>(data));
    }

    public Task<ActionResult<T>> UpdateCreateAsync(ICallerInfo callerInfo, T data)
    {
        if (string.IsNullOrEmpty(data.Id))
        {
            data.Id = Guid.NewGuid().ToString();
        }

        data.UpdateUtcTick = DateTime.UtcNow.Ticks;
        if (!_items.ContainsKey(data.Id))
        {
            data.CreateUtcTick = data.UpdateUtcTick;
        }

        _items[data.Id] = data;
        return Task.FromResult(new ActionResult<T>(data));
    }

    public Task<StatusCodeResult> DeleteAsync(ICallerInfo callerInfo, string id)
    {
        if (!_items.ContainsKey(id))
        {
            return Task.FromResult(new StatusCodeResult(404));
        }

        _items.Remove(id);
        return Task.FromResult(new StatusCodeResult(200));
    }

    public Task<ObjectResult> ListAsync(ICallerInfo callerInfo, int limit = 0)
    {
        var items = limit > 0 ? _items.Values.Take(limit) : _items.Values;
        return Task.FromResult(new ObjectResult(items));
    }

    public Task<ObjectResult> ListAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit = 0)
    {
        // For mock purposes, we'll just return all items
        var items = limit > 0 ? _items.Values.Take(limit) : _items.Values;
        return Task.FromResult(new ObjectResult(items));
    }

    public Task<ObjectResult> ListBeginsWithAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit = 0)
    {
        // For mock purposes, we'll just return all items
        var items = limit > 0 ? _items.Values.Take(limit) : _items.Values;
        return Task.FromResult(new ObjectResult(items));
    }

    public Task<ObjectResult> ListBetweenAsync(ICallerInfo callerInfo, string indexName, string indexValue1, string indexValue2, int limit = 0)
    {
        // For mock purposes, we'll just return all items
        var items = limit > 0 ? _items.Values.Take(limit) : _items.Values;
        return Task.FromResult(new ObjectResult(items));
    }

    public Task<ObjectResult> ListGreaterThanAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit = 0)
    {
        // For mock purposes, we'll just return all items
        var items = limit > 0 ? _items.Values.Take(limit) : _items.Values;
        return Task.FromResult(new ObjectResult(items));
    }

    public Task<ObjectResult> ListGreaterThanOrEqualAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit = 0)
    {
        // For mock purposes, we'll just return all items
        var items = limit > 0 ? _items.Values.Take(limit) : _items.Values;
        return Task.FromResult(new ObjectResult(items));
    }

    public Task<ObjectResult> ListLessThanAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit = 0)
    {
        // For mock purposes, we'll just return all items
        var items = limit > 0 ? _items.Values.Take(limit) : _items.Values;
        return Task.FromResult(new ObjectResult(items));
    }

    public Task<ObjectResult> ListLessThanOrEqualAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit = 0)
    {
        // For mock purposes, we'll just return all items
        var items = limit > 0 ? _items.Values.Take(limit) : _items.Values;
        return Task.FromResult(new ObjectResult(items));
    }
} 