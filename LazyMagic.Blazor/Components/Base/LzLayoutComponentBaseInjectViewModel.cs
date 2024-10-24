namespace LazyMagic.Blazor;

/// <summary>
/// A base component for handling property changes and updating the blazer view appropriately.
/// </summary>
/// <typeparam name="T">The type of view model. Must support INotifyPropertyChanged.</typeparam>
public class LzLayoutComponentBaseInjectViewModel<T> : LzLayoutComponentBase<T>
    where T : class, INotifyPropertyChanged
{
    [Inject]
    protected T _myViewModel { set => ViewModel = value; }

}