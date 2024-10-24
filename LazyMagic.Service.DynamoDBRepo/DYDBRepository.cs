namespace LazyMagic.Service.DynamoDBRepo;

/// <summary>
/// Map CRUDL operations onto DynamoDBv2.Model namespace operations (low level access)
/// DynamoDB offers a variety of access libraries. 
/// This class uses the "Low Level" interfaces available in the DynamoDBv2.Model namespace.
/// https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/DynamoDBv2/NDynamoDBv2Model.html
/// </summary>
/// <typeparam name="TEnv"></typeparam>
/// <typeparam name="T"></typeparam>
public abstract
    class DYDBRepository<TEnv, T> : IDYDBRepository<TEnv, T> where TEnv : class, IDataEnvelope<T>, new()
          where T : class, IItem, new()
{
    public DYDBRepository(IAmazonDynamoDB client)
    {
        this.client = client;
        PK = $"{nameof(T)}:";
        ConstructorExtensions();
    }

    protected virtual void ConstructorExtensions() { } 

    #region Fields
    protected string tablename;
    protected IAmazonDynamoDB client;
    protected Dictionary<string, (TEnv envelope, long lastReadTick)> cache = new();
    #endregion

    #region Properties 

    private bool _UpdateReturnOkResults = true;
    public bool UpdateReturnsOkResult
    {
        get { return _UpdateReturnOkResults; }
        set { _UpdateReturnOkResults = value; }
    }
    public bool AlwaysCache { get; set; } = false;
    private long cacheTime = 0;
    public long CacheTimeSeconds
    {
        get { return cacheTime / 10000000; } // 10 million ticks in a second, 600 million ticks in a minute
        set { cacheTime = value * 10000000; }
    }

    public long MaxItems { get; set; }
    /// <summary>
    /// Time To Live in Seconds. Set to 0 to disable. 
    /// Default is 0.
    /// Override GetTTL() for custom behavior.
    /// </summary>
    public long TTL { get; set; } = 0;
    public bool UseIsDeleted { get; set; }
    public bool UseSoftDelete { get; set; }
    public string PK { get; set; }
    public bool UseNotifications { get; set; }  
    #endregion
    
    protected virtual long GetTTL()
    {
        if (TTL == 0)
            return 0;
        // We don't use createdAt in case we are doing time windows for testing. Instead, we always  
        // use the current time + 48 hours for TTL. 
        return (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds + TTL;
    }
    /// <summary>
    /// Topics to insert place in optional Topics attribute. 
    /// Override in derived class to suite your messaging requirements.
    /// </summary>
    /// <returns>Json String</returns>
    public virtual string SetTopics() => $"[\"{nameof(T)}:\"]";
    /// <summary>
    /// Make sure cache has less than MaxItems 
    /// MaxItems == 0 means infinite cache
    /// </summary>
    /// <returns></returns>
    protected void PruneCache(string table = null)
    {
        if (string.IsNullOrEmpty(table))
            table = tablename;

        if (MaxItems == 0) return;
        if (cache.Count > MaxItems)
        {
            var numToFlush = cache.Count - MaxItems;
            // Simple flush the oldest strategy
            var cacheOrderByUpdateTick = cache.Values.OrderBy(item => item.lastReadTick);
            int i = 0;
            foreach (var (envelope, lastReadTick) in cacheOrderByUpdateTick)
            {
                if (i > numToFlush) return;
                cache.Remove($"{envelope.PK}{envelope.SK}");
            }
        }
    }
    public async Task FlushCache(string table = null)
    {
        if (string.IsNullOrEmpty(table))
            table = tablename;

        await Task.Delay(0);
        cache = new Dictionary<string, (TEnv, long)>();
    }
    public virtual async Task<ActionResult<TEnv>> CreateEAsync(string table, T data, bool? useCache = null)
        => await CreateEAsync(new CallerInfo() { Table = table }, data, useCache);
    public virtual async Task<ActionResult<TEnv>> CreateEAsync(ICallerInfo callerInfo, T data, bool? useCache = null)
    {
        callerInfo ??= new CallerInfo();
        var table = callerInfo.Table;

        if (string.IsNullOrEmpty(table))
            table = tablename;

        bool useCache2 = (useCache != null) ? (bool)useCache : AlwaysCache;
        try
        {
            var now = DateTime.UtcNow.Ticks;
            TEnv envelope = new()
            {
                EntityInstance = data,
                CreateUtcTick = now,
                UpdateUtcTick = now
            };

            envelope.SealEnvelope();

            // Wait until just before write to serialize EntityInstance (captures updates to UtcTick fields just assigned)
            var jsonData = JsonConvert.SerializeObject(envelope.EntityInstance);
            envelope.DbRecord.Add("Data", new AttributeValue() { S = jsonData });

            AddOptionalAttributes(callerInfo, envelope); // Adds TTL, Topics when specified
            AddOptionalTTLAttribute(callerInfo, envelope); // Adds TTL attribute when GetTTL() is not 0
            var topics = AddOptionalTopicsAttribute(callerInfo, envelope); // Adds Topics attribute when GetTopics() is not empty

            var request = new PutItemRequest()
            {
                TableName = table,
                Item = envelope.DbRecord,
                ConditionExpression = "attribute_not_exists(PK)" // Technique to avoid replacing an existing record. PK refers to PartionKey + SortKey
            };

            await client.PutItemAsync(request);

            if (useCache2)
            {
                cache[$"{table}:{envelope.PK}{envelope.SK}"] = (envelope, DateTime.UtcNow.Ticks);
                PruneCache();
            }
            
            if(UseNotifications)
                await WriteNotificationAsync(callerInfo, envelope.TypeName, jsonData, topics, envelope.UpdateUtcTick, "Create");

            return envelope;
        }
        catch (ConditionalCheckFailedException) { return new ConflictResult(); }
        catch (AmazonDynamoDBException) { return new StatusCodeResult(400); }
        catch (AmazonServiceException) { return new StatusCodeResult(500); }
        catch { return new StatusCodeResult(500); }
    }
    public virtual Task WriteNotificationAsync(ICallerInfo callerInfo, string dataType, string data, string topics, long updatedUtcTick, string action)
    {
        throw new NotImplementedException();
    }

    public virtual Task WriteDeleteNotificationAsync(ICallerInfo callerInfo, string dataType, string sk, string topics, long updatedUtcTick)
    {
        throw new NotImplementedException();
    }

    public virtual async Task<ActionResult<T>> CreateAsync(string table, T data, bool? useCache = null)
        => await CreateAsync(new CallerInfo() { Table = table }, data, useCache);
    public virtual async Task<ActionResult<T>> CreateAsync(ICallerInfo callerInfo, T data, bool? useCache = null)
    {
        callerInfo ??= new CallerInfo();
        var result = await CreateEAsync(callerInfo, data, useCache);
        if (result.Result is not null)
            return result.Result;
        return result.Value.EntityInstance;
    }
    public virtual async Task<ActionResult<T>> ReadAsync(string table, string id, bool? useCache = null)
        => await ReadAsync(new CallerInfo() { Table = table }, id, useCache);
    public virtual async Task<ActionResult<T>> ReadAsync(ICallerInfo callerInfo, string id, bool? useCache = null)
        => await ReadAsync(callerInfo, this.PK, $"{id}:", useCache);
    public virtual async Task<ActionResult<T>> ReadAsync(string table, string pK, string sK, bool? useCache = null)
        => await ReadAsync(new CallerInfo() { Table = table }, pK, sK, useCache);
    public virtual async Task<ActionResult<T>> ReadAsync(ICallerInfo callerInfo, string pK, string sK, bool? useCache = null)
    {
        callerInfo ??= new CallerInfo();
        var table = callerInfo.Table;
        if (string.IsNullOrEmpty(table))
            table = tablename;

        try
        {
            var response = await ReadEAsync(callerInfo, pK, sK, useCache: useCache);
            if (response.Value == null)
                return response.Result;

            return response.Value.EntityInstance;
        }

        catch (AmazonDynamoDBException) { return new StatusCodeResult(500); }
        catch (AmazonServiceException) { return new StatusCodeResult(503); }
        catch { return new StatusCodeResult(406); }
    }
    public virtual async Task<ActionResult<T>> ReadSkAsync(string table, string indexName, string id, bool? useCache = null)
        => await ReadSkAsync(new CallerInfo() { Table = table }, indexName, id, useCache);
    public virtual async Task<ActionResult<T>> ReadSkAsync(ICallerInfo callerInfo, string indexName, string id, bool? useCache = null)
        => await ReadSkAsync(callerInfo, this.PK, indexName, $"{id}:", useCache);
    public virtual async Task<ActionResult<T>> ReadSKAsync(string table, string pK, string indexName, string sK, bool? useCache = null)
        => await ReadSkAsync(new CallerInfo() { Table = table }, pK, indexName, sK, useCache);
    public virtual async Task<ActionResult<T>> ReadSkAsync(ICallerInfo callerInfo, string pK, string indexName, string sK, bool? useCache = null)
    {
        callerInfo ??= new CallerInfo();
        var table = callerInfo.Table;
        if (string.IsNullOrEmpty(table))
            table = tablename;

        try
        {
            var response = await ReadSkEAsync(callerInfo, pK, indexName, sK, useCache: useCache);
            if (response.Value == null)
                return response.Result;

            return response.Value.EntityInstance;
        }

        catch (AmazonDynamoDBException) { return new StatusCodeResult(500); }
        catch (AmazonServiceException) { return new StatusCodeResult(503); }
        catch { return new StatusCodeResult(406); }
    }
    public virtual async Task<ActionResult<TEnv>> ReadEAsync(string table, string id, bool? useCache = null)
        => await ReadEAsync(new CallerInfo() { Table = table }, id, useCache);
    public virtual async Task<ActionResult<TEnv>> ReadEAsync(ICallerInfo callerInfo, string id, bool? useCache = null)
        => await ReadEAsync(callerInfo, this.PK, $"{id}:", useCache);
    public virtual async Task<ActionResult<TEnv>> ReadEAsync(string table, string pK, string sK, bool? useCache = null)
        => await ReadEAsync(new CallerInfo() { Table = table }, pK, sK, useCache);
    public virtual async Task<ActionResult<TEnv>> ReadEAsync(ICallerInfo callerInfo, string pK, string sK, bool? useCache = null)
    {
        callerInfo ??= new CallerInfo();
        var table = callerInfo.Table;
        if (string.IsNullOrEmpty(table))
            table = tablename;

        bool useCache2 = (useCache != null) ? (bool)useCache : AlwaysCache;
        try
        {
            var key = $"{table}:{pK}{sK}";
            if ((useCache2) && cache.ContainsKey(key))
            {
                TEnv cachedEnvelope;
                long lastReadTicks;
                (cachedEnvelope, lastReadTicks) = cache[key];
                PruneCache(table);
                if (DateTime.UtcNow.Ticks - lastReadTicks < cacheTime)
                    return cachedEnvelope;
            }

            var request = new GetItemRequest()
            {
                TableName = table,
                Key = new Dictionary<string, AttributeValue>()
                {
                    {"PK", new AttributeValue {S = pK}},
                    {"SK", new AttributeValue {S = sK } }
                }
            };
            var response = await client.GetItemAsync(request);

            var item = new TEnv() { DbRecord = response.Item };
            if (useCache2)
            {
                cache[key] = (item, DateTime.UtcNow.Ticks);
                PruneCache();
            }

            return item;
        }
        catch (AmazonDynamoDBException) { return new StatusCodeResult(500); }
        catch (AmazonServiceException) { return new StatusCodeResult(503); }
        catch { return new StatusCodeResult(406); }
    }
    public virtual async Task<ActionResult<TEnv>> ReadSkEAsync(string table, string indexName, string id, bool? useCache = null)
        => await ReadSkEAsync(new CallerInfo() { Table = table }, indexName, id, useCache);
    public virtual async Task<ActionResult<TEnv>> ReadSkEAsync(ICallerInfo callerInfo, string indexName, string id, bool? useCache = null)
        => await ReadSkEAsync(callerInfo, this.PK, indexName, $"{id}:", useCache);
    public virtual async Task<ActionResult<TEnv>> ReadSKEAsync(string table, string pK, string indexName, string sK, bool? useCache = null)
        => await ReadSkEAsync(new CallerInfo() { Table = table }, pK, indexName, sK, useCache);
    public virtual async Task<ActionResult<TEnv>> ReadSkEAsync(ICallerInfo callerInfo, string pK, string indexName, string sK, bool? useCache = null)
    {
        callerInfo ??= new CallerInfo();

        var objResult = await ListAsync(callerInfo, indexName, sK);
        var statusCode = objResult.StatusCode;
        if (statusCode < 200 || statusCode > 299)
            return new StatusCodeResult((int)statusCode);

        var list = objResult.Value as List<TEnv>;
        if (list.Count == 0 || list.Count > 1)
            return new StatusCodeResult(404);

        return list[0];
    }

    /// <summary>
    /// Add optional attributes to envelope prior to create or update. 
    /// This routine currently handles the optional attributes TTL and Topics.
    /// </summary>
    /// <param name="envelope"></param>
    public virtual bool AddOptionalTTLAttribute(ICallerInfo callerInfo, TEnv envelope)
    {
        // Add TTL attribute when GetTTL() is not 0
        var ttl = GetTTL();
        if (ttl == 0)
            return false;
        envelope.DbRecord.Add("TTL", new AttributeValue() { N = ttl.ToString() });
        return true;
    }
    public virtual string AddOptionalTopicsAttribute(ICallerInfo callerInfo, TEnv envelope)
    {
        // Add Topics attribute when GetTopics() is not empty 
        var topics = SetTopics();
        if (string.IsNullOrEmpty(topics))
            return string.Empty;
        envelope.DbRecord.Add("Topics", new AttributeValue() { S = topics });
        return topics;
    }
    public virtual void AddOptionalAttributes(ICallerInfo callerInfo, TEnv envelope)
    {
        return;
    }   

    public virtual async Task<ActionResult<TEnv>> UpdateEAsync(string table, T data)
        => await UpdateEAsync(new CallerInfo() { Table = table }, data);
    public virtual async Task<ActionResult<TEnv>> UpdateEAsync(ICallerInfo callerInfo, T data, bool forceUpdate = false)
    {
        callerInfo ??= new CallerInfo();
        var table = callerInfo.Table;

        if (string.IsNullOrEmpty(table))
            table = tablename;

        if (data.Equals(null))
            return new StatusCodeResult(400);

        try
        {
            TEnv envelope = new() { EntityInstance = data };
            var OldUpdateUtcTick = envelope.UpdateUtcTick;
            var now = DateTime.UtcNow.Ticks;
            envelope.UpdateUtcTick = now; // The UpdateUtcTick Set calls SetUpdateUtcTick where you can update your entity data record 
            if(envelope.CreateUtcTick == 0)
                envelope.CreateUtcTick = now;   

            envelope.SealEnvelope();

            // Waiting until just before write to serialize EntityInstance (captures updates to UtcTick fields)
            var jsonData = JsonConvert.SerializeObject(envelope.EntityInstance);
            envelope.DbRecord.Add("Data", new AttributeValue() { S = jsonData });

            AddOptionalAttributes(callerInfo, envelope); // Adds any user specified attributes
            AddOptionalTTLAttribute(callerInfo, envelope); // Adds TTL attribute when GetTTL() is not 0
            var topics = AddOptionalTopicsAttribute(callerInfo, envelope); // Adds Topics attribute when GetTopics() is defined

            if(forceUpdate)
            {
                // Write data to database - use conditional put to avoid overwriting newer data
                var request = new PutItemRequest()
                {
                    TableName = table,
                    Item = envelope.DbRecord
                };
                await client.PutItemAsync(request);
            }
            else
            {
                // Write data to database - use conditional put to avoid overwriting newer data
                var request2 = new PutItemRequest()
                {
                    TableName = table,
                    Item = envelope.DbRecord,
                    ConditionExpression = "UpdateUtcTick = :OldUpdateUtcTick",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    {":OldUpdateUtcTick", new AttributeValue() {N = OldUpdateUtcTick.ToString()} }
                }
                };

                await client.PutItemAsync(request2);
            }

            var key = $"{table}:{envelope.PK}{envelope.SK}";
            if (cache.ContainsKey(key)) cache[key] = (envelope, DateTime.UtcNow.Ticks);
            PruneCache();

            if (UseNotifications)
                await WriteNotificationAsync(callerInfo, envelope.TypeName, jsonData, topics, now, "Update");

            return (UpdateReturnsOkResult)
                ? new OkObjectResult(envelope.EntityInstance)
                : envelope;
        }
        catch (ConditionalCheckFailedException) { return new ConflictResult(); } // STatusCode 409
        catch (AmazonDynamoDBException) { return new StatusCodeResult(500); }
        catch (AmazonServiceException) { return new StatusCodeResult(503); }
        catch { return new StatusCodeResult(500); }
    }
    public virtual async Task<ActionResult<T>> UpdateAddAsync(ICallerInfo callerInfo, T data)
    {
        callerInfo ??= new CallerInfo();
        var result = await UpdateEAsync(callerInfo, data, forceUpdate: true);
        if (result.Result is not null)
            return result.Result;
        return result.Value.EntityInstance;
    }
    public virtual async Task<ActionResult<T>> UpdateAsync(string table, T data)
        => await UpdateAsync(new CallerInfo() { Table = table }, data);
    public virtual async Task<ActionResult<T>> UpdateAsync(ICallerInfo callerInfo, T data)
    {
        callerInfo ??= new CallerInfo();
        var result = await UpdateEAsync(callerInfo, data);
        if (result.Result is not null)
            return result.Result; 
        return result.Value.EntityInstance;
    }
    public virtual async Task<StatusCodeResult> DeleteAsync(string table, string id)
        => await DeleteAsync(new CallerInfo() { Table = table }, id);
    public virtual async Task<StatusCodeResult> DeleteAsync(ICallerInfo callerInfo, string id) => await DeleteAsync(callerInfo, this.PK, $"{id}:");
    public virtual async Task<StatusCodeResult> DeleteAsync(string table, string pK, string sK = null)
        => await DeleteAsync(new CallerInfo() { Table = table }, pK, sK);
    public virtual async Task<StatusCodeResult> DeleteAsync(ICallerInfo callerInfo, string pK, string sK = null)
    {
        callerInfo ??= new CallerInfo();
        var table = callerInfo.Table;
        if (string.IsNullOrEmpty(table))
            table = tablename;
        try
        {
            if (string.IsNullOrEmpty(pK))
                return new StatusCodeResult(406); // bad key

            if (!UseSoftDelete)
            {
                var request = new DeleteItemRequest()
                {
                    TableName = table,
                    Key = new Dictionary<string, AttributeValue>()
                    {
                        {"PK", new AttributeValue {S= pK} },
                        {"SK", new AttributeValue {S = sK} }
                    }
                };
                await client.DeleteItemAsync(request);
            }

            if (UseSoftDelete || UseNotifications)
            {
                var readResult = await ReadEAsync(callerInfo, pK, sK);
                var envelope = readResult.Value;
                if (envelope is null)
                    return new StatusCodeResult(200);
                if (UseSoftDelete)
                {
                    envelope.IsDeleted = true;
                    envelope.UseTTL = true; // DynamoDB will delete records after TTL reached. Envelope class sets TTL when UseTTL is true.
                    var updateResult = await UpdateEAsync(callerInfo, envelope.EntityInstance);
                    if (updateResult.Result is not null)
                        return (StatusCodeResult)updateResult.Result; // return error code
                }
                if (UseNotifications)
                {
                    var topics = SetTopics();
                    await WriteDeleteNotificationAsync(callerInfo, envelope.TypeName, sK, topics, DateTime.UtcNow.Ticks);
                }
            }

            var key = $"{table}:{pK}{sK}";
            if (cache.ContainsKey(key)) cache.Remove(key);
            PruneCache();

            return new StatusCodeResult(200);
        }
        catch (AmazonDynamoDBException) { return new StatusCodeResult(500); }
        catch (AmazonServiceException) { return new StatusCodeResult(503); }
        catch { return new StatusCodeResult(406); }
    }
    public virtual async Task<(ObjectResult objResult, long responseSize)> ListEAndSizeAsync(QueryRequest queryRequest, bool? useCache = null, int limit = 0)
    {
        var table = queryRequest.TableName;
        bool useCache2 = (useCache != null) ? (bool)useCache : AlwaysCache;
        Dictionary<string, AttributeValue> lastEvaluatedKey = null;
        const int maxResponseSize = 5248000; // 5MB
        try
        {
            var list = new List<TEnv>();
            var responseSize = 0;
            do
            {
                if (lastEvaluatedKey is not null)
                    queryRequest.ExclusiveStartKey = lastEvaluatedKey;
                if (limit != 0)
                    queryRequest.Limit = limit;

                var response = await client.QueryAsync(queryRequest);
                foreach (Dictionary<string, AttributeValue> item in response?.Items)
                {
                    var envelope = new TEnv() { DbRecord = item };
                    responseSize += envelope.JsonSize;
                    if (responseSize > maxResponseSize)
                        break;

                    list.Add(envelope);
                    var key = $"{table}:{envelope.PK}{envelope.SK}";
                    if (useCache2 || cache.ContainsKey(key))
                        cache[key] = (envelope, DateTime.UtcNow.Ticks);
                }
            } while (responseSize <= maxResponseSize && lastEvaluatedKey != null && list.Count < limit);
            var statusCode = lastEvaluatedKey == null ? 200 : 206;
            PruneCache();

            var objResult = new ObjectResult(list) { StatusCode = statusCode };
            return (objResult, responseSize);
        }
        catch (AmazonDynamoDBException) { return (new ObjectResult(null) { StatusCode = 500 }, 0); }
        catch (AmazonServiceException) { return (new ObjectResult(null) { StatusCode = 503 }, 0); }
        catch { return (new ObjectResult(null) { StatusCode = 500 }, 0); }
    }
    public virtual async Task<ObjectResult> ListEAsync(QueryRequest queryRequest, bool? useCache = null, int limit = 0)
    {
        var (actionResult, _) = await ListEAndSizeAsync(queryRequest, useCache, limit);
        return actionResult;
    }
    /// <summary>
    /// ListAndSizeAsync returns up to "roughly" 5MB of data to stay under the 
    /// 6Mb limit imposed on API Gateway Response bodies.
    /// 
    /// Since DynamoDB queries are limited to 1MB of data, we use pagination to do 
    /// multiple reads as necessary up to approximately 5MB of data.
    /// 
    /// If the query exceeds the 5MB data limit, we return only that
    /// data and a StatusCode 206 (partial result).
    /// 
    /// If you want more pagination control, use the limit argument to control 
    /// how many records are returned in the query. When more records than the 
    /// limit are available, a Status 206 will be returned. The other size limits 
    /// still apply so you might get back fewer records than the limit specified 
    /// even when you set a limit. For instance, if you specify a limit of 20
    /// and each record is 500k in size, then only the first 10 records would be 
    /// returned and the status code would be 206.
    /// 
    /// On the client side, use the status code 200, not the number of records
    /// returned, to recognize end of list.
    /// 
    /// </summary>
    /// <param name="queryRequest"></param>
    /// <param name="useCache"></param>
    /// <returns>Task&lt;(ActionResult&lt;ICollection<T>> actionResult,long responseSize)&gt;</returns>
    public virtual async Task<(ObjectResult objResult, long responseSize)> ListAndSizeAsync(QueryRequest queryRequest, bool? useCache = null, int limit = 0)
    {
        try
        {
            var list = new List<T>();
            var (actionResult, size) = await ListEAndSizeAsync(queryRequest, useCache, limit);

            var statusCode = actionResult.StatusCode;
            if (statusCode < 200 || statusCode > 299)
                return (new ObjectResult(null) { StatusCode = statusCode }, size);
            var envList = actionResult.Value as List<TEnv>;
            foreach (var envelope in envList)
                list.Add(envelope.EntityInstance);
            var objResult = new ObjectResult(list) { StatusCode = statusCode };
            return (objResult, size);
        }
        catch (AmazonDynamoDBException) { return (new ObjectResult(null) { StatusCode = 500 }, 0); }
        catch (AmazonServiceException) { return (new ObjectResult(null) { StatusCode = 503 }, 0); }
        catch { return (new ObjectResult(null) { StatusCode = 500 }, 0); }
    }
    public virtual async Task<ObjectResult> ListAsync(QueryRequest queryRequest, bool? useCache = null, int limit = 0)
    {
        var (actionResult, _) = await ListAndSizeAsync(queryRequest, useCache, limit);
        return actionResult;
    }
    public virtual async Task<ObjectResult> ListAsync(string table)
        => await ListAsync(new CallerInfo() { Table = table });
    public virtual async Task<ObjectResult> ListAsync(ICallerInfo callerInfo)
    {
        var queryRequest = QueryEquals(PK, callerInfo: callerInfo);
        var (objResult, _) = await ListAndSizeAsync(queryRequest);
        return objResult;
    }
    public virtual async Task<ObjectResult> ListAsync(string table, string indexName, string indexValue)
        => await ListAsync(new CallerInfo() { Table = table }, indexName, indexValue);
    public virtual async Task<ObjectResult> ListAsync(ICallerInfo callerInfo, string indexName, string indexValue)
    {
        var queryRequest = QueryEquals(PK, indexName, indexValue, callerInfo: callerInfo);
        var (objResult, _) = await ListAndSizeAsync(queryRequest);
        return objResult;
    }
    public virtual async Task<ObjectResult> ListBeginsWithAsync(string table, string indexName, string indexValue)
        => await ListBeginsWithAsync(new CallerInfo() { Table = table }, indexName, indexValue);
    public virtual async Task<ObjectResult> ListBeginsWithAsync(ICallerInfo callerInfo, string indexName, string indexValue)
    {
        var queryRequest = QueryBeginsWith(PK, indexName, indexValue, callerInfo: callerInfo);
        var (objResult, _) = await ListAndSizeAsync(queryRequest);
        return objResult;
    }
    public virtual async Task<ObjectResult> ListLessThanAsync(string table, string indexName, string indexValue)
        => await ListLessThanAsync(new CallerInfo() { Table = table }, indexName, indexValue);
    public virtual async Task<ObjectResult> ListLessThanAsync(ICallerInfo callerInfo, string indexName, string indexValue)
    {
        var queryRequest = QueryLessThan(PK, indexName, indexValue, callerInfo: callerInfo);
        var (objResult, _) = await ListAndSizeAsync(queryRequest);
        return objResult;
    }
    public virtual async Task<ObjectResult> ListLessThanOrEqualAsync(string table, string indexName, string indexValue)
        => await ListLessThanOrEqualAsync(new CallerInfo() { Table = table }, indexName, indexValue);
    public virtual async Task<ObjectResult> ListLessThanOrEqualAsync(ICallerInfo callerInfo, string indexName, string indexValue)
    {
        var queryRequest = QueryLessThanOrEqual(PK, indexName, indexValue, callerInfo: callerInfo);
        var (objResult, _) = await ListAndSizeAsync(queryRequest);
        return objResult;
    }
    public virtual async Task<ObjectResult> ListGreaterThanAsync(string table, string indexName, string indexValue)
        => await ListGreaterThanAsync(new CallerInfo() { Table = table }, indexName, indexValue);
    public virtual async Task<ObjectResult> ListGreaterThanAsync(ICallerInfo callerInfo, string indexName, string indexValue)
    {
        var queryRequest = QueryGreaterThan(PK, indexName, indexValue, callerInfo: callerInfo);
        var (objResult, _) = await ListAndSizeAsync(queryRequest);
        return objResult;
    }
    public virtual async Task<ObjectResult> ListGreaterThanOrEqualAsync(string table, string indexName, string indexValue)
        => await ListGreaterThanOrEqualAsync(new CallerInfo() { Table = table }, indexName, indexValue);
    public virtual async Task<ObjectResult> ListGreaterThanOrEqualAsync(ICallerInfo callerInfo, string indexName, string indexValue)
    {
        var queryRequest = QueryGreaterThanOrEqual(PK, indexName, indexValue, callerInfo: callerInfo);
        var (objResult, _) = await ListAndSizeAsync(queryRequest);
        return objResult;
    }
    public virtual async Task<ObjectResult> ListBetweenAsync(string table, string indexName, string indexValue1, string indexValue2)
        => await ListBetweenAsync(new CallerInfo() { Table = table }, indexName, indexValue1, indexValue2);
    public virtual async Task<ObjectResult> ListBetweenAsync(ICallerInfo callerInfo, string indexName, string indexValue1, string indexValue2)
    {
        var queryRequest = QueryRange(PK, indexName, indexValue1, indexValue2, callerInfo: callerInfo);
        var (objResult, _) = await ListAndSizeAsync(queryRequest);
        return objResult;
    }
    protected Dictionary<string, string> GetExpressionAttributeNames(Dictionary<string, string> value)
    {
        if (value != null)
            return value;

        return new Dictionary<string, string>()
        {
            {"#Data", "Data" },
            {"#Status", "Status" },
            {"#General", "General" }
        };
    }
    protected string GetProjectionExpression(string value)
    {
        value ??= "#Data, TypeName, #Status, UpdateUtcTick, CreateUtcTick, #General";
        return value;
    }

    public virtual QueryRequest QueryEquals(string pK, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = callerInfo.Table;
        if (string.IsNullOrEmpty(table))
            table = tablename;

        expressionAttributeNames = GetExpressionAttributeNames(expressionAttributeNames);
        projectionExpression = GetProjectionExpression(projectionExpression);

        var query = new QueryRequest()
        {
            TableName = table,
            KeyConditionExpression = $"PK = :PKval",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                {":PKval", new AttributeValue() {S = pK} }
            },
            ExpressionAttributeNames = expressionAttributeNames,
            ProjectionExpression = projectionExpression
        };

        if (UseIsDeleted)
        {
            query.ExpressionAttributeValues.Add(":IsDeleted", new AttributeValue() { BOOL = false });
            query.FilterExpression = "IsDeleted = :IsDeleted"; // IsDeleted = False
        }

        return query;

    }
    public virtual QueryRequest QueryEquals(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = callerInfo.Table;
        if (string.IsNullOrEmpty(table))
            table = tablename;

        expressionAttributeNames = GetExpressionAttributeNames(expressionAttributeNames);
        projectionExpression = GetProjectionExpression(projectionExpression);

        var indexName = (string.IsNullOrEmpty(keyField) || keyField.Equals("SK")) ? null : $"PK-{keyField}-Index";

        var query = new QueryRequest()
        {
            TableName = table,
            KeyConditionExpression = $"PK = :PKval and {keyField} = :SKval",
            IndexName = indexName,
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                {":PKval", new AttributeValue() {S = pK} },
                {":SKval", new AttributeValue() {S =  key } }
            },
            ExpressionAttributeNames = expressionAttributeNames,
            ProjectionExpression = projectionExpression
        };
        if (UseIsDeleted)
        {
            query.ExpressionAttributeValues.Add(":IsDeleted", new AttributeValue() { BOOL = false });
            query.FilterExpression = "IsDeleted = :IsDeleted"; // IsDeleted = False
        }
        return query;
    }
    public virtual QueryRequest QueryBeginsWith(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = callerInfo.Table;
        if (string.IsNullOrEmpty(table))
            table = tablename;

        expressionAttributeNames = GetExpressionAttributeNames(expressionAttributeNames);
        projectionExpression = GetProjectionExpression(projectionExpression);

        var indexName = (string.IsNullOrEmpty(keyField) || keyField.Equals("SK")) ? null : $"PK-{keyField}-Index";

        var query = new QueryRequest()
        {
            TableName = table,
            KeyConditionExpression = $"PK = :PKval and begins_with({keyField}, :SKval)",
            IndexName = indexName,
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                {":PKval", new AttributeValue() {S = pK} },
                {":SKval", new AttributeValue() {S =  key } }
            },
            ExpressionAttributeNames = expressionAttributeNames,
            ProjectionExpression = projectionExpression
        };
        if (UseIsDeleted)
        {
            query.ExpressionAttributeValues.Add(":IsDeleted", new AttributeValue() { BOOL = false });
            query.FilterExpression = "IsDeleted = :IsDeleted"; // IsDeleted = False
        }
        return query;
    }
    public virtual QueryRequest QueryLessThan(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = callerInfo.Table;
        if (string.IsNullOrEmpty(table))
            table = tablename;

        expressionAttributeNames = GetExpressionAttributeNames(expressionAttributeNames);
        projectionExpression = GetProjectionExpression(projectionExpression);

        var indexName = (string.IsNullOrEmpty(keyField) || keyField.Equals("SK")) ? null : $"PK-{keyField}-Index";

        var query = new QueryRequest()
        {
            TableName = table,
            KeyConditionExpression = $"PK = :PKval and SK < :SKval",
            IndexName = indexName,
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                {":PKval", new AttributeValue() {S = pK} },
                {":SKval", new AttributeValue() {S =  key } }
            },
            ExpressionAttributeNames = expressionAttributeNames,
            ProjectionExpression = projectionExpression
        };
        if (UseIsDeleted)
        {
            query.ExpressionAttributeValues.Add(":IsDeleted", new AttributeValue() { BOOL = false });
            query.FilterExpression = "IsDeleted = :IsDeleted"; // IsDeleted = False
        }
        return query;
    }
    public virtual QueryRequest QueryLessThanOrEqual(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = callerInfo.Table;
        if (string.IsNullOrEmpty(table))
            table = tablename;

        expressionAttributeNames = GetExpressionAttributeNames(expressionAttributeNames);
        projectionExpression = GetProjectionExpression(projectionExpression);

        var indexName = (string.IsNullOrEmpty(keyField) || keyField.Equals("SK")) ? null : $"PK-{keyField}-Index";

        var query = new QueryRequest()
        {
            TableName = table,
            KeyConditionExpression = $"PK = :PKval and SK <= :SKval",
            IndexName = indexName,
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                {":PKval", new AttributeValue() {S = pK} },
                {":SKval", new AttributeValue() {S =  key } }
            },
            ExpressionAttributeNames = expressionAttributeNames,
            ProjectionExpression = projectionExpression
        };
        if (UseIsDeleted)
        {
            query.ExpressionAttributeValues.Add(":IsDeleted", new AttributeValue() { BOOL = false });
            query.FilterExpression = "IsDeleted = :IsDeleted"; // IsDeleted = False
        }
        return query;
    }
    public virtual QueryRequest QueryGreaterThan(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = callerInfo.Table;
        if (string.IsNullOrEmpty(table))
            table = tablename;

        expressionAttributeNames = GetExpressionAttributeNames(expressionAttributeNames);
        projectionExpression = GetProjectionExpression(projectionExpression);

        var indexName = (string.IsNullOrEmpty(keyField) || keyField.Equals("SK")) ? null : $"PK-{keyField}-Index";

        var query = new QueryRequest()
        {
            TableName = table,
            KeyConditionExpression = $"PK = :PKval and SK > :SKval",
            IndexName = indexName,
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                {":PKval", new AttributeValue() {S = pK} },
                {":SKval", new AttributeValue() {S =  key } }
            },
            ExpressionAttributeNames = expressionAttributeNames,
            ProjectionExpression = projectionExpression
        };
        if (UseIsDeleted)
        {
            query.ExpressionAttributeValues.Add(":IsDeleted", new AttributeValue() { BOOL = false });
            query.FilterExpression = "IsDeleted = :IsDeleted"; // IsDeleted = False
        }
        return query;
    }
    public virtual QueryRequest QueryGreaterThanOrEqual(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = callerInfo.Table;
        if (string.IsNullOrEmpty(table))
            table = tablename;

        expressionAttributeNames = GetExpressionAttributeNames(expressionAttributeNames);
        projectionExpression = GetProjectionExpression(projectionExpression);

        var indexName = (string.IsNullOrEmpty(keyField) || keyField.Equals("SK")) ? null : $"PK-{keyField}-Index";

        var query = new QueryRequest()
        {
            TableName = table,
            KeyConditionExpression = $"PK = :PKval and SK >= :SKval",
            IndexName = indexName,
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                {":PKval", new AttributeValue() {S = pK} },
                {":SKval", new AttributeValue() {S =  key } }
            },
            ExpressionAttributeNames = expressionAttributeNames,
            ProjectionExpression = projectionExpression
        };
        if (UseIsDeleted)
        {
            query.ExpressionAttributeValues.Add(":IsDeleted", new AttributeValue() { BOOL = false });
            query.FilterExpression = "IsDeleted = :IsDeleted"; // IsDeleted = False
        }
        return query;
    }
    public virtual QueryRequest QueryBeginsWith(string pK, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = callerInfo.Table;
        if (string.IsNullOrEmpty(table))
            table = tablename;

        expressionAttributeNames = GetExpressionAttributeNames(expressionAttributeNames);
        projectionExpression = GetProjectionExpression(projectionExpression);

        var query = new QueryRequest()
        {
            TableName = table,
            KeyConditionExpression = $"PK = :PKval",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                {":PKval", new AttributeValue() {S = pK} }
            },
            ExpressionAttributeNames = expressionAttributeNames,
            ProjectionExpression = projectionExpression
        };
        if (UseIsDeleted)
        {
            query.ExpressionAttributeValues.Add(":IsDeleted", new AttributeValue() { BOOL = false });
            query.FilterExpression = "IsDeleted = :IsDeleted"; // IsDeleted = False
        }
        return query;
    }

    public virtual QueryRequest QueryRange(
        string pK,
        string keyField,
        string keyStart,
        string keyEnd,
        Dictionary<string, string> expressionAttributeNames = null,
        string projectionExpression = null,
        string table = null)
        => QueryRange(pK, keyField, keyStart, keyEnd, expressionAttributeNames, projectionExpression, new CallerInfo() { Table = table });

    public virtual QueryRequest QueryRange(
        string pK,
        string keyField,
        string keyStart,
        string keyEnd,
        Dictionary<string, string> expressionAttributeNames = null,
        string projectionExpression = null,
        ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = callerInfo.Table;
        if (string.IsNullOrEmpty(table))
            table = tablename;

        expressionAttributeNames = GetExpressionAttributeNames(expressionAttributeNames);
        projectionExpression = GetProjectionExpression(projectionExpression);

        var indexName = (string.IsNullOrEmpty(keyField) || keyField.Equals("SK")) ? null : $"PK-{keyField}-Index";

        var query = new QueryRequest()
        {
            TableName = table,
            KeyConditionExpression = $"PK = :PKval and {keyField} between :SKStart and :SKEnd",
            IndexName = indexName,
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                {":PKval", new AttributeValue() {S = pK} },
                {":SKStart", new AttributeValue() {S =  keyStart }},
                {":SKEnd", new AttributeValue() {S = keyEnd} }
            },
            ExpressionAttributeNames = expressionAttributeNames,
            ProjectionExpression = projectionExpression
        };
        if (UseIsDeleted)
        {
            query.ExpressionAttributeValues.Add(":IsDeleted", new AttributeValue() { BOOL = false });
            query.FilterExpression = "IsDeleted = :IsDeleted"; // IsDeleted = False
        }
        return query;
    }
    protected bool IsResultOk(IActionResult actionResult)
    {
        if (actionResult is StatusCodeResult statusCodeResult)
        {
            int statusCode = statusCodeResult.StatusCode;
            if (statusCode >= 200 && statusCode < 300)
                return true;
        }
        return true;
    }
    protected int GetStatusCode(IActionResult actionResult)
    {
        if (actionResult is StatusCodeResult statusCodeResult)
            return statusCodeResult.StatusCode;
        return 200;
    }
}
