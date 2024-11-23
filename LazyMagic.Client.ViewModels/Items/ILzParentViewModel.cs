namespace LazyMagic.Client.ViewModels;

public interface ILzParentViewModel
{
    /// <summary>
    ///This method is called by the ItemViewModel when a new item is persisted  
    ///to the service. 
    /// </summary>
    /// <param name="itemViewModel"></param>
    /// <param name="makeCurrentItem"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="Exception"></exception>
    public Task ItemCreated(object itemViewModel, bool makeCurrentItem = true);
    /// This method is called by the ItemViewModel when the item is deleted 
    /// in the service. 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Task ItemDeleted(string id);
    /// <summary>
    /// Implement this to handle any aggregation etc. necessary when 
    /// items are updated. 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Task ItemUpdated(string id);
    /// <summary>
    /// Implement this to handle any state changes you add to the 
    /// derived class releated to updating items. By default, there 
    /// are no states to handle in this class.
    /// Implement this to handle any aggregation etc. necessary when 
    /// items are updated. 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Task ItemUpdateCanceled(string id);
    /// <summary>
    /// Implement this to handle any aggregation etc. necessary when
    /// items are read from server.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Task ItemRead(string id);
}
