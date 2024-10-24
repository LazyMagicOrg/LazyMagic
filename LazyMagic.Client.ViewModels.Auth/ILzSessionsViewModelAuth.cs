namespace LazyMagic.Client.ViewModels;

public interface ILzSessionsViewModelAuth<T> : ILzSessionsViewModel<T>
    where T : ILzSessionViewModelAuth
{
    //bool IsSignedIn { get; }
    //bool IsAdmin { get; }
}   
