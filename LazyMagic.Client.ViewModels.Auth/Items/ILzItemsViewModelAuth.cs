namespace LazyMagic.Client.ViewModels;
public interface ILzItemsViewModelAuth<TVM, TDTO, TModel> : 
        ILzItemsViewModel<TVM, TDTO,TModel>
        where TVM : class, ILzItemViewModelAuth<TModel>
        where TDTO : class, new()
        where TModel : class, IRegisterObservables, TDTO, new()
{ }