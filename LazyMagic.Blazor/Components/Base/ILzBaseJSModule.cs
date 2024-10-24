
namespace LazyMagic.Blazor;

public interface ILzBaseJSModule
{
    /// <summary>
    /// Gets the module file name.
    /// </summary>
    string ModuleFileName { get; }

    /// <summary>
    /// Gets the awaitable <see cref="IJSObjectReference"/> task.
    /// </summary>
    Task<IJSObjectReference> Module { get; }
    void SetJSRuntime(object jsRuntime);
}
