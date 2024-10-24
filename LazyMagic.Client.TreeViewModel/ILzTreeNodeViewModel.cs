namespace LazyMagic.Client.TreeViewModel
{
    public interface ILzTreeNodeViewModel
    {
        IEnumerable<ILzTreeNodeViewModel> Children { get; set; }
        bool IsFolder { get; set; }
        string Page { get; set; }
        string Text { get; set; }
        object ViewModel { get; set; }
        Type ViewModelType { get; set; }
    }
}