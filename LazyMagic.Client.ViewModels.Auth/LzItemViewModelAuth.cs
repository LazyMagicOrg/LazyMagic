
namespace LazyMagic.Client.ViewModels;

// Remove if not used, if needed we will need to implement as derivative of LzItemViewModel 
//public class ViewModelAuth<T> : ReactiveObject
//    where T : class
//{
//    public ViewModelAuth(
//        IAuthProcess authProcess
//        )
//    {
//        this.AuthProcess = authProcess;
//        AuthProcess
//            .WhenAnyValue(x => x.IsSignedIn)
//            .Subscribe(x => IsActive = x);
//    }

//    [Reactive]
//    public IAuthProcess? AuthProcess { get; set; }
//    [Reactive]
//    public bool IsActive { get; set; }
//    [Reactive]
//    public T? Data { get; set; }

//}
