namespace LazyMagic.Blazor;

public class LzLayoutComponentBasePassViewModel<T> : LzLayoutComponentBase<T>
    where T : class, INotifyPropertyChanged
{
    protected override async Task OnInitializedAsync()
    {
        if (ViewModel == null) throw new Exception($"{this.GetType().Name}, LzLayoutComponentBasePassViewModel: ViewModel is null. Pass ViewModel as Parameter");
        await base.OnInitializedAsync();
    }
}
