namespace LazyMagic.Blazor;

public class LzComponentBasePassViewModel<T> : LzComponentBase<T>
    where T : class, INotifyPropertyChanged
{
    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        if (ViewModel == null) throw new Exception($"{this.GetType().Name}, LzComponentBasePassViewModel: ViewModel is null. Pass ViewModel as Parameter");
        await base.OnInitializedAsync();
    }

}
