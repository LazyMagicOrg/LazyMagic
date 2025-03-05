namespace LazyMagic.Service.Shared;

public interface IDocumentRepo<T>
    where T : class, IItem, new()
{
    Task<ActionResult<T>> CreateAsync(ICallerInfo callerInfo, T data);
    Task<ActionResult<T>> ReadAsync(ICallerInfo callerInfo, string id);
    Task<ActionResult<T>> UpdateAsync(ICallerInfo callerInfo, T data, bool forceupdate = false);
    Task<ActionResult<T>> UpdateCreateAsync(ICallerInfo callerInfo, T data);
    Task<StatusCodeResult> DeleteAsync(ICallerInfo callerInfo, string id);
    Task<ObjectResult> ListAsync(ICallerInfo callerInfo, int limit = 0);
    Task<ObjectResult> ListAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit = 0);
    Task<ObjectResult> ListBeginsWithAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit = 0);
    Task<ObjectResult> ListBetweenAsync(ICallerInfo callerInfo, string indexName, string indexValue1, string indexValue2, int limit = 0);
    Task<ObjectResult> ListGreaterThanAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit = 0);
    Task<ObjectResult> ListGreaterThanOrEqualAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit = 0);
    Task<ObjectResult> ListLessThanAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit = 0);
    Task<ObjectResult> ListLessThanOrEqualAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit = 0);
}
