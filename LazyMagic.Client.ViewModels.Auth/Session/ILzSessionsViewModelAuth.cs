namespace LazyMagic.Client.ViewModels;

/// <inheritdoc/>
public interface ILzSessionsViewModelAuth<T> : ILzSessionsViewModel<T>
    where T : ILzSessionViewModelAuth
{
    //bool IsSignedIn { get; }
    //bool IsAdmin { get; }
}   
