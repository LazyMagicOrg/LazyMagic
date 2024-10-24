namespace LazyMagic.Client.ViewModels;
public class LzEditContext<T1,T2>
    : ReactiveObject, IDisposable
    where T1 : class, new()
    where T2 : class, T1, IRegisterObservables, new()
{
    public LzEditContext(
        T1 baseItem, 
        Func<T1, Task<(bool, string, T1?)>> createAsync, 
        Func<T1, Task<(bool, string, T1?)>> updateAsync, 
        bool isNew = false)
    {
        editItem = new();
        baseItem.ShallowCloneTo(editItem);

        editItem.RegisterObservables();
        // EditContext contains a private Dictionary<FieldIdentifier,FieldState>
        EditContext = new EditContext(editItem);
        // ValidationMessageStore contains an Item[FieldIdentifer] array containing LzMessages by field
        ValidationMessageStore = new ValidationMessageStore(EditContext);
        // Note that editItem instance properties are uniquely identified by a FieldIdentier and it is this
        // FieldIdentifier that provides "shared context" between the EditContext and ValidationMessageStore
        // EditContext does not have a reference to ValidationMessageStore. 
        

        this.createAsync = createAsync; // Callback that knows how to persist the editItem
        this.updateAsync = updateAsync; // Callback that knows how to persist an update to the editItem
        IsNew = isNew;

        EditContext.OnValidationRequested += (sender, eventArgs) => ValidateModel();
        EditContext.OnFieldChanged += (sender, eventArgs) => ValidateField(eventArgs.FieldIdentifier);


    }
    private Func<T1, Task<(bool,string,T1?)>> createAsync;
    private Func<T1, Task<(bool,string,T1?)>> updateAsync;
    private T2 editItem;
    private bool disposedValue;

    public EditContext EditContext { get; init; }
    [Reactive] public bool CanUpdate { get; private set; }
    [Reactive] public bool CanCreate { get; private set; }
    [Reactive] public bool IsNew { get; private set; }

    ValidationMessageStore ValidationMessageStore { get; init; } 


    private IValidator GetValidatorForModel(object model)
    {
        var abstractValidatorType = typeof(AbstractValidator<>).MakeGenericType(model.GetType());
        var modelValidatorType = Assembly.GetExecutingAssembly().GetTypes().FirstOrDefault(t => t.IsSubclassOf(abstractValidatorType));
        var validatorObject = Activator.CreateInstance(modelValidatorType!);
        var validatorInstance = validatorObject as IValidator;
        return validatorInstance!;
    }

    private void ValidateField( in FieldIdentifier fieldIdentifier)
    {

        var properties = new[] { fieldIdentifier.FieldName };
        var context = new ValidationContext<T1>((T1)fieldIdentifier.Model, new PropertyChain(), new MemberNameValidatorSelector(properties));

        var validator = GetValidatorForModel(fieldIdentifier.Model);
        var validationResults = validator.Validate(context);

        ValidationMessageStore.Clear(fieldIdentifier);

        foreach (var validationResult in validationResults.Errors)
        {
            ValidationMessageStore.Add(EditContext.Field(validationResult.PropertyName), validationResult.ErrorMessage);
        }
        CanUpdate = EditContext.IsModified() && EditContext.Validate() && !IsNew;
        CanCreate = EditContext.IsModified() && EditContext.Validate() && IsNew;
        EditContext.NotifyValidationStateChanged();
    }
    private void ValidateModel()
    {
        var validator = GetValidatorForModel(EditContext.Model);
        var validationResults = validator.Validate((IValidationContext)EditContext.Model);

        ValidationMessageStore.Clear();
        foreach (var validationResult in validationResults.Errors)
        {
            ValidationMessageStore.Add(EditContext.Field(validationResult.PropertyName), validationResult.ErrorMessage);
        }
        CanUpdate = EditContext.IsModified() && EditContext.Validate() && !IsNew;
        CanCreate = EditContext.IsModified() && EditContext.Validate() && IsNew;
        EditContext.NotifyValidationStateChanged();
    }
    public void OnFieldChanged(object? sender, FieldChangedEventArgs args)
    {
        var valid = EditContext.Validate();
        Console.WriteLine($"isvalid: {valid}");
        CanUpdate = EditContext.IsModified() && EditContext.Validate() && !IsNew;
        CanCreate = EditContext.IsModified() && EditContext.Validate() && IsNew;
        foreach(var msg in EditContext.GetValidationMessages())
            Console.WriteLine(msg);

    }
    public async Task<(bool, string)> CreateAsync()
    {
        if(!IsNew)
        {
            return (false, "Can't Create item with IsNew=false status");
        }

        T1 model = (T1)EditContext.Model;
        var(success, msg, baseItem) = await createAsync(model);
        if(success)
        {
            IsNew = false;
            baseItem.ShallowCloneTo(editItem);
            EditContext!.MarkAsUnmodified();
            return (true, msg);  
        }
        return(false, msg);  
    }
    public async Task<(bool,string)> UpdateAsync()
    {
        if (IsNew)
        {
            return (false, "Can't update item with New status");
        }

        T1 model = (T1)EditContext.Model;
        var (success, msg, baseItem) = await updateAsync(model);
        if (success)
        {
            baseItem.ShallowCloneTo(editItem);
            EditContext.MarkAsUnmodified();
            return (true, msg);
        }
        return (false, msg);
    }
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
                EditContext.OnFieldChanged -= OnFieldChanged;
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~LzEditContext()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}




