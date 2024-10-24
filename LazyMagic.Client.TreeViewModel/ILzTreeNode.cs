namespace LazyMagic.Client.TreeViewModel;

public interface ILzTreeNode
{
    Task<ILzTreeNodeViewModel> GetTreeNodeAsync();
}
 