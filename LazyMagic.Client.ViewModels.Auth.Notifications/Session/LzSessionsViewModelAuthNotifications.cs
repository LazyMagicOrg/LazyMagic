

namespace LazyMagic.Client.ViewModels;
/// <inheritdoc/>
public abstract class LzSessionsViewModelAuthNotifications<T> 
    : LzSessionsViewModelAuth<T>, 
    ILzSessionsViewModelAuthNotifications<T>
    where T : ILzSessionViewModelAuthNotifications
{

    public LzSessionsViewModelAuthNotifications(ILoggerFactory loggerFactory) : base(loggerFactory)
    {
    }   
}
