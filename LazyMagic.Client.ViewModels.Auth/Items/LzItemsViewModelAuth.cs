namespace LazyMagic.Client.ViewModels;

public abstract class LzItemsViewModelAuth<TVM, TDTO, TModel> : 
    LzItemsViewModel<TVM, TDTO, TModel>,
    ILzItemsViewModelAuth<TVM, TDTO, TModel> where TDTO : class, new()
    where TModel : class, TDTO, IRegisterObservables, new()
    where TVM : class, ILzItemViewModelAuth<TModel>
{
    public LzItemsViewModelAuth(ILoggerFactory loggerFactory) : base(loggerFactory)
    {
    }
}
