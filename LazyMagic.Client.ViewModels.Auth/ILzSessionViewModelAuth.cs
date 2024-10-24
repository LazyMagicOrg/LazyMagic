namespace LazyMagic.Client.ViewModels;

public interface ILzSessionViewModelAuth : ILzSessionViewModel
{
    IAuthProcess AuthProcess { get; set; }
    bool IsBusy { get; }
    bool IsSignedIn { get; }
    bool HasChallenge { get; }
    bool IsAdmin { get; }
    Task<bool> IsAdminCheck();
    Task OnSignedInAsync();
    Task OnSignedOutAsync();
}