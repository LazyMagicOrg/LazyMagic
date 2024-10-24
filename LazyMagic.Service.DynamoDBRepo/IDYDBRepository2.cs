namespace LazyMagic.Service.DynamoDBRepo;

/// <summary>
/// Use DynamoDB via the DynamoDBv2.Model namespace (low level inteface)
/// https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/DynamoDBv2/NDynamoDBv2Model.html
/// CRUDL operations map onto the low level access operations available in the namespace.
/// </summary>
/// <typeparam name="TEnv"></typeparam>
/// <typeparam name="T"></typeparam>
public interface IDYDBRepository2<TEnv,T>
    where TEnv : class, IDataEnvelope<T>, new()
    where T : class, IItem, new()
{
    bool UpdateReturnsOkResult { get; set; }
    bool AlwaysCache { get; set; }
    long CacheTimeSeconds { get; set; }
    long MaxItems { get; set; }
    long TTL { get; set; }
    bool UseIsDeleted { get; set; }
    bool UseSoftDelete { get; set; }

    string PK { get; set; }

    /// <summary>
    /// Flush the cache
    /// </summary>
    /// <returns></returns>
    Task FlushCache(string table = null);

    /// <summary>
    /// Create record
    /// </summary>
    /// <param name="callerInfo">ICallerInfo</param>
    /// <param name="data"></param>
    /// <param name="useCache"></param>
    /// <returns>ActionResult<typeparamref name="T"/></returns>
    Task<ActionResult<T>> CreateAsync(ICallerInfo callerInfo, T data, bool? useCache = null);
    Task<ActionResult<T>> CreateAsync(string table, T data, bool? useCache = null);

    /// <summary>
    ///  Create Envelope record
    /// </summary>
    /// <param name="callerInfo">ICallerInfo</param>
    /// <param name="data"></param>
    /// <param name="useCache"></param>
    /// <returns>ActionResult<typeparamref name="T"/></returns>
    Task<ActionResult<TEnv>> CreateEAsync(ICallerInfo callerInfo, T data,  bool? useCache = null);
    Task<ActionResult<TEnv>> CreateEAsync(string table, T data, bool? useCache = null);

    /// <summary>
    /// Read record
    /// </summary>
    /// <param name="callerInfo">ICallerInfo</param>
    /// <param name="pK"></param>
    /// <param name="sK"></param>
    /// <param name="useCache"></param>
    /// <returns>ActionResult<typeparamref name="T"/></returns>
    Task<ActionResult<T>> ReadAsync(ICallerInfo callerInfo, string pK, string sK,  bool? useCache = null);
    Task<ActionResult<T>> ReadAsync(string table, string pK, string sK, bool? useCache = null);

    /// <summary>
    /// Read record
    /// </summary>
    /// <param name="callerInfo">ICallerInfo</param>
    /// <param name="id"></param>
    /// <param name="useCache"></param>
    /// <returns>ActionResult<typeparamref name="T"/></returns>
    Task<ActionResult<T>> ReadAsync(ICallerInfo callerInfo, string id, bool? useCache = null);
    Task<ActionResult<T>> ReadAsync(string table, string id, bool? useCache = null);

    Task<ActionResult<T>> ReadSkAsync(ICallerInfo callerInfo, string indexName, string id, bool? useCache = null);
    Task<ActionResult<T>> ReadSkAsync(string table, string indexName, string id, bool? useCache = null);

    /// <summary>
    /// Read record Envelope
    /// </summary>
    /// <param name="callerInfo">ICallerInfo</param>
    /// <param name="pK"></param>
    /// <param name="sK"></param>
    /// <param name="useCache"></param>
    /// <returns>AcitonResult<typeparamref name="TEnv"/></returns>
    Task<ActionResult<TEnv>> ReadEAsync(ICallerInfo callerInfo, string pK, string sK, bool? useCache = null);
    Task<ActionResult<TEnv>> ReadEAsync(string table, string pK, string sK, bool? useCache = null);
    Task<ActionResult<TEnv>> ReadSkEAsync(ICallerInfo callerInfo, string indexName, string sK, bool? useCache = null);
    Task<ActionResult< TEnv>> ReadSkEAsync(string table, string pK, string indexName, string sK, bool? useCache = null);
    /// <summary>
    /// Read record Envelope
    /// </summary>
    /// <param name="callerInfo">ICallerInfo</param>
    /// <param name="id"></param>
    /// <param name="useCache"></param>
    /// <returns>AcitonResult<typeparamref name="TEnv"/></returns>
    Task<ActionResult<TEnv>> ReadEAsync(ICallerInfo callerInfo , string id, bool? useCache = null);
    Task<ActionResult<TEnv>> ReadEAsync(string table, string id, bool? useCache = null);
    /// <summary>
    /// Update record
    /// </summary>
    /// <param name="callerInfo">ICallerInfo</param>
    /// <param name="data"></param>
    /// <returns>ActionResult<typeparamref name="T"/></returns>
    Task<ActionResult<T>> UpdateAsync(ICallerInfo callerInfo, T data);
    Task<ActionResult<T>> UpdateAsync(string table, T data);
    /// <summary>
    /// Update record Envelope
    /// </summary>
    /// <param name="callerInfo">ICallerInfo</param>
    /// <param name="data"></param>
    /// <returns>ActionResult<typeparamref name="TEnv"/></returns>
    Task<ActionResult<TEnv>> UpdateEAsync(ICallerInfo callerInfo, T data);
    Task<ActionResult<TEnv>> UpdateEAsync(string table, T data);
    /// <summary>
    /// Delete record. 
    /// </summary>
    /// <param name="callerInfo">ICallerInfo</param>
    /// <param name="pK"></param>
    /// <param name="sK"></param>
    /// <returns>StatusCodeResult</returns>
    Task<StatusCodeResult> DeleteAsync(ICallerInfo callerInfo, string pK, string sK = null);
    Task<StatusCodeResult> DeleteAsync(string table, string pK, string sK = null);
    /// <summary>
    /// Delete record. 
    /// </summary>
    /// <param name="callerInfo">ICallerInfo</param>
    /// <param name="id"></param>
    /// <returns>StatusCodeResult</returns>
    Task<StatusCodeResult> DeleteAsync(ICallerInfo callerInfo, string id);
    Task<StatusCodeResult> DeleteAsync(string table, string id);

    /// <summary>
    /// Call QueryAsync and return list of envelopes
    /// </summary>
    /// <param name="queryRequest"></param>
    /// <param name="useCache"></param>
    /// <param name="limit"></param>
    /// <returns>ObjectResult</returns>
    Task<ObjectResult> ListEAsync(QueryRequest queryRequest, bool? useCache = null, int limit = 0);
    /// <summary>
    /// Call QueryAsync and return list of data objects of type T
    /// </summary>
    /// <param name="queryRequest"></param>
    /// <param name="useCache"></param>
    /// <param name="limit"></param>    
    /// <returns>ObjectResult</returns>
    Task<ObjectResult> ListAsync(QueryRequest queryRequest, bool? useCache = null, int limit = 0);
    Task<ObjectResult> ListAsync(ICallerInfo callerInfo);
    Task<ObjectResult> ListAsync(string table);
    Task<ObjectResult> ListAsync(ICallerInfo callerInfo, string indexName, string indexValue);
    Task<ObjectResult> ListAsync(string table, string indexName, string indexValue);
    Task<ObjectResult> ListLessThanAsync(ICallerInfo callerInfo, string indexName, string indexValue);
    Task<ObjectResult> ListLessThanAsync(string table, string indexName, string indexValue);
    Task<ObjectResult> ListLessThanOrEqualAsync(ICallerInfo callerInfo, string indexName, string indexValue);
    Task<ObjectResult> ListLessThanOrEqualAsync(string table, string indexName, string indexValue);
    Task<ObjectResult> ListGreaterThanAsync(ICallerInfo callerInfo, string indexName, string indexValue);
    Task<ObjectResult> ListGreaterThanAsync(string table, string indexName, string indexValue);
    Task<ObjectResult> ListGreaterThanOrEqualAsync(ICallerInfo callerInfo, string indexName, string indexValue);
    Task<ObjectResult> ListGreaterThanOrEqualAsync(string table, string indexName, string indexValue);
    Task<ObjectResult> ListBetweenAsync(ICallerInfo callerInfo, string indexName, string indexValue1, string indexValue2);
    Task<ObjectResult> ListBetweenAsync(string table, string indexName, string indexValue1, string indexValue2);
    Task<ObjectResult> ListBeginsWithAsync(ICallerInfo callerInfo, string indexName, string indexValue);
    Task<ObjectResult> ListBeginsWithAsync(string table, string indexName, string indexValue);

    /// <summary>
    /// Return a simple query request for records matching query arguments
    /// </summary>
    /// <param name="pK"></param>
    /// <param name="expressionAttributeNames"></param>
    /// <param name="projectionExpression"></param>
    /// <param name="callerInfo">ICallerInfo</param>
    /// <returns>QueryRequest</returns>
    QueryRequest QueryEquals(string pK, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null);
    /// <summary>
    /// Return a simple query request using {keyField} = SKval on index PK-{keyField}-Index
    /// </summary>
    /// <param name="pK"></param>
    /// <param name="keyField"></param>
    /// <param name="key"></param>
    /// <param name="expressionAttributeNames"></param>
    /// <param name="callerInfo">ICallerInfo</param>
    /// <returns>QueryRequest</returns>
    QueryRequest QueryEquals(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null);
    /// <summary>
    /// Return a simple query request using begins_with({keyField}, SKval) on index PK-{keyField}-Index
    /// </summary>
    /// <param name="pK"></param>
    /// <param name="keyField"></param>
    /// <param name="key"></param>
    /// <param name="expressionAttributeNames"></param>
    /// <param name="callerInfo">ICallerInfo</param>
    /// <returns>QueryRequest</returns>
    QueryRequest QueryBeginsWith(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null);
}
