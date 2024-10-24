namespace LazyMagic.Blazor;

public class LzComponentBaseAssignViewModel<T> : LzComponentBase<T>
    where T : class, INotifyPropertyChanged
{
    protected override async Task OnInitializedAsync()
    {
        var derivedClassName = this.GetType().Name;
        if (ViewModel == null) throw new Exception($"{derivedClassName}, LzComponentBaseAssignViewModel: ViewModel is null. Assign it in the OnIntializedAsync method.");
        await base.OnInitializedAsync();
    }
}
