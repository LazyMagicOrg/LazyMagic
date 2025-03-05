using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using Amazon.DynamoDBv2.DocumentModel;
using ThirdParty.Json.LitJson;
using Amazon.DynamoDBv2.Model;

namespace LazyMagic.Service.DynamoDBRepo;

public enum TableLevel 
{ 
    System,     // Use SystemDB passed in CallerInfo
    Tenant,     // Use TenantDB passed in CallerInfo
    Subtenant,  // Use SubtenantDB passed in CallerInfo
    Default,    // Use DefaultDB passed in CallerInfo
    Local       // Use tablename (usually set in constructor)
}


/// <summary>
/// Map CRUDL operations onto DynamoDBv2.Model namespace operations (low level access)
/// DynamoDB offers a variety of access libraries. 
/// This class uses the "Low Level" interfaces available in the DynamoDBv2.Model namespace.
/// https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/DynamoDBv2/NDynamoDBv2Model.html
/// This library provides Create, Read, Update, Delete, List, and List method implementations 
/// to handle the basic CRUDL operations on a DynamoDB table.
/// 
/// The basic idea is that we use an Envelope record containing those fields that are 
/// common to all record types, and which include a Data attribute containing the entity 
/// type as a JSON string.
/// 
/// Create: new Envelope(data) -> SealEnvelope() -> putItem
/// Read: new Envelope(readItem()) -> OpenEnvelope() -> return 
/// Update: data -> attributes -> record (we use optimistic locking)
/// Delete: record delete
/// List: foreach item: record -> attributes -> data
/// 
/// 
/// </summary>
/// <typeparam name="TEnv"></typeparam>
/// <typeparam name="T"></typeparam>
public abstract class DYDBRepository<T> : IDYDBRepository<T> 
          where T : class, IItem, new()
{
    public DYDBRepository(IAmazonDynamoDB client)
    {
        this.client = client;
        EntityType = $"{typeof(T).Name}:";
        ConstructorExtensions();
    }

    protected virtual void ConstructorExtensions() { } 

    #region Fields
    protected string tablename; // Set this in constructor and set tableLevel to Local to use it
    protected TableLevel tableLevel = TableLevel.Default; // Default to use DefaultDB in CallerInfo
    protected bool debug = false; // Set to true to see debug output in logs
    protected IAmazonDynamoDB client;
    #endregion

    #region Properties 
    private bool _UpdateReturnOkResults = true;
    protected bool UpdateReturnsOkResult
    {
        get { return _UpdateReturnOkResults; }
        set { _UpdateReturnOkResults = value; }
    }
    /// <summary>
    /// Time To Live in Seconds. Set to 0 to disable. 
    /// Default is 0.
    /// Override GetTTL() for custom behavior.
    /// </summary>
    protected long TTL { get; set; } = 0;
    protected bool UseIsDeleted { get; set; }
    protected bool UseSoftDelete { get; set; }
    protected string EntityType { get; set; }
    protected bool UseNotifications { get; set; }
    protected string NotificationsTablename { get; set; } = "";
    protected string NotificationsSqsQueue { get; set; } = "";
    #endregion

    #region Public Methods

    public virtual async Task<ActionResult<T>> CreateAsync(ICallerInfo callerInfo, T data)
    {
        if (debug) Console.WriteLine("CreateAsync() called");
        callerInfo ??= new CallerInfo();
        var table = GetTableName(callerInfo);
        try
        {
            var now = DateTime.UtcNow.Ticks;
            var dbrecord = new Dictionary<string, AttributeValue>(); // create an empty record
            var jobjectData = JObject.FromObject(data); // Create JObject from data

            // You can override each of the Assign attribute methods to customize the attributes
            AssignEntityAttributes(callerInfo, jobjectData, dbrecord, now); // Assigns attributes from JObject data
            AssignOptionalAttrubutes(callerInfo, jobjectData, dbrecord, now); // Adds optional attributes
            AssignTTLAttribute(callerInfo, jobjectData, dbrecord, now); // Adds TTL attribute when GetTTL() is not 0
            AssignTopicsAttribute(callerInfo, jobjectData, dbrecord, now); // Adds Topics attribute
            AssignJsonDataAttribute(callerInfo, jobjectData, dbrecord, now); // Adds Data attribute containing JSON data
            AssignCreateUtcTickAttribute(callerInfo, jobjectData, dbrecord, now); // Adds CreateUtcTickAttribute attribute
            AssignUpdateUtcTickAttribute(callerInfo, jobjectData, dbrecord, now); // Adds UpdateUtcTickAttribute attribute

            var request = new PutItemRequest()
            {
                TableName = table,
                Item = dbrecord,
                ConditionExpression = "attribute_not_exists(PK)" // Technique to avoid replacing an existing record. EntityType refers to PartionKey + SortKey
            };

            if (debug) Console.WriteLine($"CreateEAsync() PutItemAsync called");
            await client.PutItemAsync(request);

            if (UseNotifications)
                await WriteNotificationAsync(callerInfo, dbrecord, "Create");

            return jobjectData.ToObject<T>();
        }
        catch (ConditionalCheckFailedException ex)
        {
            if (debug) Console.WriteLine($"CreateEAsync() ConditionalCheckFailedException. {ex.Message}");
            return new ConflictResult();
        }
        catch (AmazonDynamoDBException ex)
        {
            if (debug) Console.WriteLine($"CreateEAsync() AmazonDynamoDBException. {ex.Message}");
            return new StatusCodeResult(400);
        }
        catch (AmazonServiceException ex)
        {
            if (debug) Console.WriteLine($"CreateEAsync() AmazonServiceException. {ex.Message}");
            return new StatusCodeResult(500);
        }
        catch (Exception ex)
        {
            if (debug) Console.WriteLine($"CreateEAsync() catch all. {ex.Message}");
            return new StatusCodeResult(500);
        }
    }
    public virtual async Task<ActionResult<T>> ReadAsync(ICallerInfo callerInfo, string id)
    {
        if (debug) Console.WriteLine("ReadAsync() called");
        callerInfo ??= new CallerInfo();
        var table = GetTableName(callerInfo);
        //=> await ReadAsync(callerInfo, this.EntityType, $"{id}:");
        var sK = $"{id}:";
        try
        {
            var key = $"{table}:{EntityType}{sK}";
            var request = new GetItemRequest()
            {
                TableName = table,
                Key = new Dictionary<string, AttributeValue>()
                {
                    {"PK", new AttributeValue {S = EntityType}},
                    {"SK", new AttributeValue {S = sK } }
                }
            };
            if (debug) Console.WriteLine($"ReadEAsync() GetItemAsync called");
            var response = await client.GetItemAsync(request);
            var data = response.Item["Data"].S; 
            return DeserializeJsonData(data);
        }

        catch (AmazonDynamoDBException ex)
        {
            if (debug) Console.WriteLine($"ReadAsync() AmazonDynamoDBException. {ex.Message}");
            return new StatusCodeResult(500);
        }
        catch (AmazonServiceException ex)
        {
            if (debug) Console.WriteLine($"ReadAsync() AmazonServiceException. {ex.Message}");
            return new StatusCodeResult(503);
        }
        catch (Exception ex)
        {
            if (debug) Console.WriteLine($"ReadAsync() catch all. {ex.Message}");
            return new StatusCodeResult(406);
        }
    }
    public virtual Task<ActionResult<T>> UpdateCreateAsync(ICallerInfo callerInfo, T data)
    {
        if (debug) Console.WriteLine("UpdateCreateAsync() called but not implemented");
        throw new NotImplementedException();
    }
    public virtual async Task<ActionResult<T>> UpdateAsync(ICallerInfo callerInfo, T data, bool forceUpdate = false)
    {
        if (debug) Console.WriteLine("UpdateAsync() called");
        if (data.Equals(null))
        {
            if (debug) Console.WriteLine("UpdateEAsync() data is null");
            return new StatusCodeResult(400);
        }
        callerInfo ??= new CallerInfo();
        var table = GetTableName(callerInfo);
        try
        {
            var now = DateTime.UtcNow.Ticks;
            var dbrecord = new Dictionary<string, AttributeValue>(); // create an empty record
            var jobjectData = JObject.FromObject(data); // Create JObject from data

            // You can override each of the Assign attribute methods to customize the attributes
            AssignEntityAttributes(callerInfo, jobjectData, dbrecord, now); // Assigns attributes from JObject data
            var OldUpdateUtcTick = dbrecord["UpdateUtcTick"].N;
            AssignOptionalAttrubutes(callerInfo, jobjectData, dbrecord, now); // Adds optional attributes
            AssignTTLAttribute(callerInfo, jobjectData, dbrecord, now); // Adds TTL attribute when GetTTL() is not 0
            AssignTopicsAttribute(callerInfo, jobjectData, dbrecord, now); // Adds Topics attribute
            AssignJsonDataAttribute(callerInfo, jobjectData, dbrecord, now); // Adds Data attribute containing JSON data
            AssignCreateUtcTickAttribute(callerInfo, jobjectData, dbrecord, now); // Adds CreateUtcTickAttribute attribute
            AssignUpdateUtcTickAttribute(callerInfo, jobjectData, dbrecord, now); // Adds UpdateUtcTickAttribute attribute

            if (forceUpdate)
            {
                // Write data to database - do not use conditional put to avoid overwriting newer data
                var request = new PutItemRequest()
                {
                    TableName = table,
                    Item = dbrecord
                };
                await client.PutItemAsync(request);
            }
            else
            {
                // Write data to database - use conditional put to avoid overwriting newer data
                var request2 = new PutItemRequest()
                {
                    TableName = table,
                    Item = dbrecord,
                    ConditionExpression = "UpdateUtcTick = :OldUpdateUtcTick",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    {":OldUpdateUtcTick", new AttributeValue() {N = OldUpdateUtcTick.ToString()} }
                }
                };

                await client.PutItemAsync(request2);
            }

            if (UseNotifications)
                await WriteNotificationAsync(callerInfo, dbrecord, "Update");

            return jobjectData.ToObject<T>();
        }
        catch (ConditionalCheckFailedException ex)
        {
            if (debug) Console.WriteLine($"UpdateEAsync() ConditionalCheckFailedException. {ex.Message}");
            return new ConflictResult();
        } // STatusCode 409
        catch (AmazonDynamoDBException ex)
        {
            if (debug) Console.WriteLine($"UpdateEAsync() AmazonDynamoDBException. {ex.Message}");
            return new StatusCodeResult(500);
        }
        catch (AmazonServiceException ex)
        {
            if (debug) Console.WriteLine($"UpdateEAsync() AmazonServiceException. {ex.Message}");
            return new StatusCodeResult(503);
        }
        catch (Exception ex)
        {
            if (debug) Console.WriteLine($"UpdateEAsync() catch all. {ex.Message}");
            return new StatusCodeResult(500);
        }

    }
    public virtual async Task<StatusCodeResult> DeleteAsync(ICallerInfo callerInfo, string id)
    {
        var sK = $"{id}:";  
        if (debug) Console.WriteLine("DeleteAsync() called");
        callerInfo ??= new CallerInfo();
        var table = GetTableName(callerInfo);

        try
        {
            if (string.IsNullOrEmpty(EntityType))
                return new StatusCodeResult(406); // bad key

            if (UseSoftDelete || UseNotifications)
            {
                // Read existing record
                try
                {
                    var key = $"{table}:{EntityType}{sK}";
                    var request = new GetItemRequest()
                    {
                        TableName = table,
                        Key = new Dictionary<string, AttributeValue>()
                {
                    {"PK", new AttributeValue {S = EntityType}},
                    {"SK", new AttributeValue {S = sK } }
                }
                    };
                    if (debug) Console.WriteLine($"ReadEAsync() GetItemAsync called");
                    var response = await client.GetItemAsync(request); // doesn't throw error if item doesn't exist
                    var item = response.Item;
                    if (UseSoftDelete && item != null)
                    {
                        item["IsDeleted"].BOOL = true;
                        item["UseTTL"].BOOL = true; // DynamoDB will delete records after TTL reached. Envelope class sets TTL when UseTTL is true. 
                        var request2 = new PutItemRequest()
                        {
                            TableName = table,
                            Item = item,
                            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                            {
                            }
                        };

                        await client.PutItemAsync(request2);
                    }
                    if (UseNotifications)
                    {

                        await WriteNotificationAsync(callerInfo, item, "Delete");
                    }
                    if(UseSoftDelete)
                        return new StatusCodeResult(200);
                }
                catch (AmazonDynamoDBException ex)
                {
                    if (debug) Console.WriteLine($"ReadAsync() AmazonDynamoDBException. {ex.Message}");
                    return new StatusCodeResult(500);
                }
                catch (AmazonServiceException ex)
                {
                    if (debug) Console.WriteLine($"ReadAsync() AmazonServiceException. {ex.Message}");
                    return new StatusCodeResult(503);
                }
                catch (Exception ex)
                {
                    if (debug) Console.WriteLine($"ReadAsync() catch all. {ex.Message}");
                    return new StatusCodeResult(406);
                }
            } 
            
            var request3 = new DeleteItemRequest()
            {
                TableName = table,
                Key = new Dictionary<string, AttributeValue>()
            {
                {"PK", new AttributeValue {S= EntityType} },
                {"SK", new AttributeValue {S = sK} }
            }
            };
            await client.DeleteItemAsync(request3); // doesn't throw error if item doesn't exist

            return new StatusCodeResult(200);
        }
        catch (AmazonDynamoDBException ex)
        {
            if (debug) Console.WriteLine($"DeleteAsync() AmazonDynamoDBException. {ex.Message}");
            return new StatusCodeResult(500);
        }
        catch (AmazonServiceException ex)
        {
            if (debug) Console.WriteLine($"DeleteAsync() AmazonServiceException. {ex.Message}");
            return new StatusCodeResult(503);
        }
        catch (Exception ex)
        {
            if (debug) Console.WriteLine($"DeleteAsync() catch all. {ex.Message}");
            return new StatusCodeResult(406);
        }
    }
    public virtual async Task<ObjectResult> ListAsync(ICallerInfo callerInfo, int limit = 0)
    {
        var queryRequest = QueryEquals(EntityType, callerInfo: callerInfo);
        return await ListAndSizeAsync(queryRequest, limit);
    }
    public virtual async Task<ObjectResult> ListAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit=0)
    {
        var queryRequest = QueryEquals(EntityType, indexName, indexValue, callerInfo: callerInfo);
        return await ListAndSizeAsync(queryRequest, limit);
    }
    public virtual async Task<ObjectResult> ListBeginsWithAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit=0)
    {
        var queryRequest = QueryBeginsWith(EntityType, indexName, indexValue, callerInfo: callerInfo);
        return await ListAndSizeAsync(queryRequest, limit);
    }
    public virtual async Task<ObjectResult> ListLessThanAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit=0)
    {
        var queryRequest = QueryLessThan(EntityType, indexName, indexValue, callerInfo: callerInfo);
        return await ListAndSizeAsync(queryRequest, limit);
    }
    public virtual async Task<ObjectResult> ListLessThanOrEqualAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit=0)
    {
        var queryRequest = QueryLessThanOrEqual(EntityType, indexName, indexValue, callerInfo: callerInfo);
        return await ListAndSizeAsync(queryRequest, limit);
    }
    public virtual async Task<ObjectResult> ListGreaterThanAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit=0)
    {
        var queryRequest = QueryGreaterThan(EntityType, indexName, indexValue, callerInfo: callerInfo);
        return await ListAndSizeAsync(queryRequest);
    }
    public virtual async Task<ObjectResult> ListGreaterThanOrEqualAsync(ICallerInfo callerInfo, string indexName, string indexValue, int limit=0)
    {
        var queryRequest = QueryGreaterThanOrEqual(EntityType, indexName, indexValue, callerInfo: callerInfo);
        return await ListAndSizeAsync(queryRequest, limit);
    }
    public virtual async Task<ObjectResult> ListBetweenAsync(ICallerInfo callerInfo, string indexName, string indexValue1, string indexValue2, int limit=0)
    {
        var queryRequest = QueryRange(EntityType, indexName, indexValue1, indexValue2, callerInfo: callerInfo);
        return await ListAndSizeAsync(queryRequest, limit);
    }

    #endregion

    #region Protected Methods
    protected virtual async Task<ObjectResult> ListAndSizeAsync(QueryRequest queryRequest, int limit = 0)
    {
        if(debug) Console.WriteLine("ListEAndSizeAsync() called");
        var table = queryRequest.TableName;
        Dictionary<string, AttributeValue> lastEvaluatedKey = null;
        const int maxResponseSize = 5248000; // 5MB
        try
        {
            var list = new List<T>();
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
                    var jsonData = item["Data"].S;
                    responseSize += jsonData.Length;
                    if (responseSize > maxResponseSize)
                        break;

                    list.Add(DeserializeJsonData(jsonData));
                }
            } while (responseSize <= maxResponseSize && lastEvaluatedKey != null && list.Count < limit);
            var statusCode = lastEvaluatedKey == null ? 200 : 206;

            return new ObjectResult(list) { StatusCode = statusCode };
        }
        catch (AmazonDynamoDBException ex) 
        { 
            if(debug) Console.WriteLine($"ListEAndSizeAsync() AmazonDynamoDBException. {ex.Message}");
            return new ObjectResult(null) { StatusCode = 500 }; 
        }
        catch (AmazonServiceException ex) 
        {
            if (debug) Console.WriteLine($"ListEAndSizeAsync() AmazonServiceException. {ex.Message}");
            return new ObjectResult(null) { StatusCode = 503 }; 
        }
        catch (Exception ex)
        {
            if (debug) Console.WriteLine($"ListEAndSizeAsync() catch all. {ex.Message}");
            return new ObjectResult(null) { StatusCode = 500 }; 
        }
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
    protected virtual QueryRequest QueryEquals(string pK, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = GetTableName(callerInfo);

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
    protected virtual QueryRequest QueryEquals(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = GetTableName(callerInfo);

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
    protected virtual QueryRequest QueryBeginsWith(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = GetTableName(callerInfo);

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
    protected virtual QueryRequest QueryLessThan(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = GetTableName(callerInfo);

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
    protected virtual QueryRequest QueryLessThanOrEqual(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = GetTableName(callerInfo);

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
    protected virtual QueryRequest QueryGreaterThan(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = GetTableName(callerInfo);

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
    protected virtual QueryRequest QueryGreaterThanOrEqual(string pK, string keyField, string key, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = GetTableName(callerInfo);

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
    protected virtual QueryRequest QueryBeginsWith(string pK, Dictionary<string, string> expressionAttributeNames = null, string projectionExpression = null, ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = GetTableName(callerInfo);

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
    protected virtual QueryRequest QueryRange(
        string pK,
        string keyField,
        string keyStart,
        string keyEnd,
        Dictionary<string, string> expressionAttributeNames = null,
        string projectionExpression = null,
        string table = null)
        => QueryRange(pK, keyField, keyStart, keyEnd, expressionAttributeNames, projectionExpression, new CallerInfo() { DefaultDB = table });

    protected virtual QueryRequest QueryRange(
        string pK,
        string keyField,
        string keyStart,
        string keyEnd,
        Dictionary<string, string> expressionAttributeNames = null,
        string projectionExpression = null,
        ICallerInfo callerInfo = null)
    {
        callerInfo ??= new CallerInfo();
        var table = GetTableName(callerInfo);

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
    protected virtual string GetTableName(ICallerInfo callerInfo)
    {
        var table = "";
        switch (tableLevel)
        {
            case TableLevel.System:
                table = callerInfo.SystemDB;
                break;
            case TableLevel.Tenant:
                table = callerInfo.TenantDB;
                break;
            case TableLevel.Subtenant:
                table = callerInfo.SubtenantDB;
                break;
            case TableLevel.Default:
                table = callerInfo.DefaultDB;
                break;
            case TableLevel.Local:
                table = tablename;
                break;
            default:
                table = callerInfo.DefaultDB;
                break;
        }
        if (debug) Console.WriteLine($"GetTableName() table: {table}");
        return table;
    }
    protected virtual void AssignEntityAttributes(ICallerInfo callerInfo, JObject jobjectData, Dictionary<string,AttributeValue> dbrecord, long now )
    {
        if (debug) Console.WriteLine("AddEntityAttributes() called");
        dbrecord.Add("PK", new AttributeValue() { S = EntityType });
        if(jobjectData["Id"] != null)
            dbrecord.Add("SK", new AttributeValue() { S = $"{jobjectData["Id"]}" });
        dbrecord.Add("SessionId", new AttributeValue() { S = callerInfo.SessionId });
        return;
    }
    protected virtual void AssignOptionalAttrubutes(ICallerInfo callerInfo, JObject jobjectData, Dictionary<string, AttributeValue> dbrecord, long now)
    {
        if (debug) Console.WriteLine("AddOptionalAttributes() called");
        return;
    }
    protected virtual void AssignTTLAttribute(ICallerInfo callerInfo, JObject jobjectData, Dictionary<string, AttributeValue> dbrecord, long now)
    {
        if (debug) Console.WriteLine("AddTTLAttribute() called");
        if(TTL == 0) return;
        dbrecord.Add("TTL", new AttributeValue() { N = TTL.ToString() });
        return;
    }
    protected virtual void AssignTopicsAttribute(ICallerInfo callerInfo, JObject jobjectData, Dictionary<string, AttributeValue> dbrecord, long now)
    {
        if (debug) Console.WriteLine("AddTopicsAttribute() called");
        var topics = $"[\"{typeof(T).Name}:\"]";
        dbrecord.Add("Topics", new AttributeValue() { S = topics });
        return;
    }
    protected virtual void AssignJsonDataAttribute(ICallerInfo callerInfo, JObject jobjectData, Dictionary<string, AttributeValue> dbrecord, long now)
    {
        if (debug) Console.WriteLine("AddJsonDataAttribute() called");
        var jsonData = JsonConvert.SerializeObject(jobjectData);
        dbrecord.Add("Data", new AttributeValue() { S = jsonData });
        return;
    }
    /// <summary>
    /// If your entity data doesn't have a CreateUtcTick property, you can override this method
    /// to assign the CreateUtcTick value from a different property.  
    /// </summary>
    /// <param name="callerInfo"></param>
    /// <param name="jobjectData"></param>
    /// <param name="dbrecord"></param>
    /// <param name="now"></param>
    protected virtual void AssignCreateUtcTickAttribute(ICallerInfo callerInfo, JObject jobjectData, Dictionary<string, AttributeValue> dbrecord, long now)
    {
        if (debug) Console.WriteLine("AddCreateTickAttributeAttribute() called");
        long createUtcTick = 0;
        if (jobjectData["CreateUtcTick"] != null) // the type contains a CreateUtcTick property, it may be zero
        {
            createUtcTick = (long)jobjectData["CreateUtcTick"];
            if (createUtcTick == 0) createUtcTick = now;
        }
        dbrecord.Add("CreateUtcTick", new AttributeValue() { N = createUtcTick.ToString() });
        return;
    }
    /// <summary>
    /// If your entity data doesn't have an UpdateUtcTick property, you can override this method
    /// to assign the UpdateUtcTick value from a different property.
    /// </summary>
    /// <param name="callerInfo"></param>
    /// <param name="jobjectData"></param>
    /// <param name="dbrecord"></param>
    /// <param name="now"></param>
    protected virtual void AssignUpdateUtcTickAttribute(ICallerInfo callerInfo, JObject jobjectData, Dictionary<string, AttributeValue> dbrecord, long now)
    {
        if (debug) Console.WriteLine("AddUpdateTickAttributeAttribute() called");
        dbrecord.Add("UpdateUtcTick", new AttributeValue() { N = now.ToString() });
        // Update the object data with the new UpdateUtcTick if it contains one.
        // If your object stores this data in another property, you will need to voerride this method.
        // and assign the class property there.
        if (jobjectData["UpdateUtcTick"] != null) jobjectData["UpdateUtcTick"] = now;
        return;
    }
    /// <summary>
    /// Overrid this method to handle in-line data transformations from 
    /// older type definitions to new ones.
    /// You can perform persitent updates for records by doing a read followed
    /// by a write.
    /// </summary>
    /// <param name="jsonData"></param>
    /// <returns></returns>
    protected virtual T DeserializeJsonData(string jsonData)
    {
        if (debug) Console.WriteLine("DeserializeData() called");
        return JsonConvert.DeserializeObject<T>(jsonData);
    }
    protected virtual Task WriteNotificationAsync(ICallerInfo callerInfo, Dictionary<string, AttributeValue> dbrecord, string action)
    {
        if (debug) Console.WriteLine("WriteNotificationAsync() called but not implemented");
        throw new NotImplementedException();
    }
    #endregion 
}
