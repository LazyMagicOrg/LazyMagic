using Microsoft.Extensions.Logging;

namespace LazyMagic.Client.ViewModels;

public abstract class LzViewModel: ReactiveObject, IDisposable
{
    protected readonly ILogger _logger;

    public LzViewModel(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(GetType());
    }   

    public virtual void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Subscriptions?.Dispose();
        }
    }
    protected readonly CompositeDisposable Subscriptions = new CompositeDisposable();

    protected virtual string Log(MethodBase m, string msg)
    {
        var msgLoc = $"{m!.ReflectedType!.Name}.{m.Name}";
        msg = $"{msgLoc} failed {msg}";
        //Console.WriteLine(msg);
        _logger.LogError(msg);  
        return msg;
    }

    protected virtual string Log(string userMsg, string detailedMsg)
    {
        //Console.WriteLine(userMsg + " | " + detailedMsg);
        _logger.LogError(userMsg + " | " + detailedMsg);
        return userMsg;
    }
}
