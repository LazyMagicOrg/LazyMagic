namespace LazyMagic.Client.ViewModels;

public abstract class LzItemViewModelAuth<TDTO,TModel> 
    : LzItemViewModel<TDTO,TModel>, 
    ILzItemViewModelAuth<TModel>
    where TDTO : class, new()
    where TModel : class, TDTO, IRegisterObservables, new()
{
    public LzItemViewModelAuth(ILoggerFactory loggerFactory, ILzSessionViewModel sessionViewModel, TDTO? dto = null, TModel? model = null, bool? isLoaded = null) 
        : base(loggerFactory, sessionViewModel, dto, model, isLoaded)
    { }
}
