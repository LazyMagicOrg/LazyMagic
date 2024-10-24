namespace LazyMagic.Service.DynamoDBRepo
{
    public interface IDYDBRepository<TEnv, T>
        where TEnv : class, IDataEnvelope<T>, new()
        where T : class, IItem, new()
    {

        bool AlwaysCache { get; set; }
        long CacheTimeSeconds { get; set; }
        long MaxItems { get; set; }
        string PK { get; set; }
        long TTL { get; set; }
        bool UpdateReturnsOkResult { get; set; }
        bool UseIsDeleted { get; set; }
        bool UseSoftDelete { get; set; }
        bool UseNotifications { get; set; }

        void AddOptionalAttributes(ICallerInfo callerInfo, TEnv envelope);
        Task WriteDeleteNotificationAsync(ICallerInfo callerInfo, string dataType, string sk, string topics, long updatedUtcTick);
        Task WriteNotificationAsync(ICallerInfo callerInfo, string dataType, string data, string topics, long updatedUtcTick, string action);
        Task<ActionResult<T>> CreateAsync(ICallerInfo callerInfo, T data, bool? useCache = null);
        Task<ActionResult<T>> CreateAsync(string table, T data, bool? useCache = null);
        Task<ActionResult<TEnv>> CreateEAsync(ICallerInfo callerInfo, T data, bool? useCache = null);
        Task<ActionResult<TEnv>> CreateEAsync(string table, T data, bool? useCache = null);
        Task<StatusCodeResult> DeleteAsync(ICallerInfo callerInfo, string id);
        Task<StatusCodeResult> DeleteAsync(ICallerInfo callerInfo, string pK, string sK = null);
        Task<StatusCodeResult> DeleteAsync(string table, string id);
        Task<StatusCodeResult> DeleteAsync(string table, string pK, string sK = null);
        Task FlushCache(string table = null);
        Task<(ObjectResult objResult, long responseSize)> ListAndSizeAsync(QueryRequest queryRequest, bool? useCache = null, int limit = 0);
        Task<ObjectResult> ListAsync(ICallerInfo callerInfo);
        Task<ObjectResult> ListAsync(ICallerInfo callerInfo, string indexName, string indexValue);
        Task<ObjectResult> ListAsync(QueryRequest queryRequest, bool? useCache = null, int limit = 0);
        Task<ObjectResult> ListAsync(string table);
        Task<ObjectResult> ListAsync(string table, string indexName, string indexValue);
        Task<ObjectResult> ListBeginsWithAsync(ICallerInfo callerInfo, string indexName, string indexValue);
        Task<ObjectResult> ListBeginsWithAsync(string table, string indexName, string indexValue);
        Task<ObjectResult> ListBetweenAsync(ICallerInfo callerInfo, string indexName, string indexValue1, string indexValue2);
        Task<ObjectResult> ListBetweenAsync(string table, string indexName, string indexValue1, string indexValue2);
        Task<(ObjectResult objResult, long responseSize)> ListEAndSizeAsync(QueryRequest queryRequest, bool? useCache = null, int limit = 0);
        Task<ObjectResult> ListEAsync(QueryRequest queryRequest, bool? useCache = null, int limit = 0);
        Task<ObjectResult> ListGreaterThanAsync(ICallerInfo callerInfo, string indexName, string indexValue);
        Task<ObjectResult> ListGreaterThanAsync(string table, string indexName, string indexValue);
        Task<ObjectResult> ListGreaterThanOrEqualAsync(ICallerInfo callerInfo, string indexName, string indexValue);
        Task<ObjectResult> ListGreaterThanOrEqualAsync(string table, string indexName, string indexValue);
        Task<ObjectResult> ListLessThanAsync(ICallerInfo callerInfo, string indexName, string indexValue);
        Task<ObjectResult> ListLessThanAsync(string table, string indexName, string indexValue);
        Task<ObjectResult> ListLessThanOrEqualAsync(ICallerInfo callerInfo, string indexName, string indexValue);
        Task<ObjectResult> ListLessThanOrEqualAsync(string table, string indexName, string indexValue);
        QueryRequest QueryBeginsWith(string pK, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null);
        QueryRequest QueryBeginsWith(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null);
        QueryRequest QueryEquals(string pK, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null);
        QueryRequest QueryEquals(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null);
        QueryRequest QueryGreaterThan(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null);
        QueryRequest QueryGreaterThanOrEqual(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null);
        QueryRequest QueryLessThan(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null);
        QueryRequest QueryLessThanOrEqual(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null);
        QueryRequest QueryRange(string pK, string keyField, string keyStart, string keyEnd, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null);
        QueryRequest QueryRange(string pK, string keyField, string keyStart, string keyEnd, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, string table = null);
        Task<ActionResult<T>> ReadAsync(ICallerInfo callerInfo, string id, bool? useCache = null);
        Task<ActionResult<T>> ReadAsync(ICallerInfo callerInfo, string pK, string sK, bool? useCache = null);
        Task<ActionResult<T>> ReadAsync(string table, string id, bool? useCache = null);
        Task<ActionResult<T>> ReadAsync(string table, string pK, string sK, bool? useCache = null);
        Task<ActionResult<TEnv>> ReadEAsync(ICallerInfo callerInfo, string id, bool? useCache = null);
        Task<ActionResult<TEnv>> ReadEAsync(ICallerInfo callerInfo, string pK, string sK, bool? useCache = null);
        Task<ActionResult<TEnv>> ReadEAsync(string table, string id, bool? useCache = null);
        Task<ActionResult<TEnv>> ReadEAsync(string table, string pK, string sK, bool? useCache = null);
        Task<ActionResult<T>> ReadSkAsync(ICallerInfo callerInfo, string indexName, string id, bool? useCache = null);
        Task<ActionResult<T>> ReadSkAsync(ICallerInfo callerInfo, string pK, string indexName, string sK, bool? useCache = null);
        Task<ActionResult<T>> ReadSkAsync(string table, string indexName, string id, bool? useCache = null);
        Task<ActionResult<T>> ReadSKAsync(string table, string pK, string indexName, string sK, bool? useCache = null);
        Task<ActionResult<TEnv>> ReadSkEAsync(ICallerInfo callerInfo, string indexName, string id, bool? useCache = null);
        Task<ActionResult<TEnv>> ReadSkEAsync(ICallerInfo callerInfo, string pK, string indexName, string sK, bool? useCache = null);
        Task<ActionResult<TEnv>> ReadSkEAsync(string table, string indexName, string id, bool? useCache = null);
        Task<ActionResult<TEnv>> ReadSKEAsync(string table, string pK, string indexName, string sK, bool? useCache = null);
        Task<ActionResult<T>> UpdateAsync(ICallerInfo callerInfo, T data);
        Task<ActionResult<T>> UpdateAddAsync(ICallerInfo callerInfo, T data); 
        Task<ActionResult<T>> UpdateAsync(string table, T data);
        Task<ActionResult<TEnv>> UpdateEAsync(ICallerInfo callerInfo, T data, bool forceUpdate = false);
        Task<ActionResult<TEnv>> UpdateEAsync(string table, T data);
    }
}