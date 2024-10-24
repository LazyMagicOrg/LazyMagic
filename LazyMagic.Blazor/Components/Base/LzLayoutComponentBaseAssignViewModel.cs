namespace LazyMagic.Blazor;

public class LzLayoutComponentBaseAssignViewModel<T> : LzLayoutComponentBase<T>
    where T : class, INotifyPropertyChanged
{
    protected override async Task OnInitializedAsync()
    {
        var derivedClassName = this.GetType().Name;
        if (ViewModel == null) throw new Exception($"{derivedClassName}, LzLayoutComponentBaseAssignViewModel: ViewModel is null. Assign it in the OnIntializedAsync method.");
        await base.OnInitializedAsync();
    }
}
