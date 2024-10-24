namespace LazyMagic.Client.TreeViewModel;
/// <summary>
/// Class supporting Tree node navigation.
/// </summary>
public class LzTreeNodeViewModel : ILzTreeNodeViewModel
{
    public LzTreeNodeViewModel(
        object viewModel,
        Type viewModelType,
        string text,
        bool isFolder,
        string page,
        IList<ILzTreeNodeViewModel>? children = null)
    {
        this.ViewModel = viewModel;
        this.ViewModelType = viewModelType;
        this.Text = text;
        this.IsFolder = isFolder;
        this.Page = page;
        this.Children = children ?? new List<ILzTreeNodeViewModel>();
    }
    public object ViewModel { get; set; }
    public Type ViewModelType { get; set; }
    public string Text { get; set; }
    public bool IsFolder { get; set; }
    public string Page { get; set; }
    public IEnumerable<ILzTreeNodeViewModel> Children { get; set; }
}
