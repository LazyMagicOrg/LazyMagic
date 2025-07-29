namespace LazyMagic.Client.ViewModels;
/// <inheritdoc/>
public abstract class LzSessionsViewModelAuth<T> : LzSessionsViewModel<T>, ILzSessionsViewModelAuth<T>
    where T : ILzSessionViewModelAuth
{
    public LzSessionsViewModelAuth(ILoggerFactory loggerFactory) : base(loggerFactory)
    {
    }

    public IAuthProviderCreds CurrentSessionAuthProviderCreds => SessionViewModel?.AuthProcess ?? throw new InvalidOperationException("SessionViewModel or AuthProcess is not initialized.");

    public override T? SessionViewModel { get; set; }

    public override async Task<bool> CreateSessionAsync()
    {
        var sessionCreated = await base.CreateSessionAsync();
        return sessionCreated;
    }
}
