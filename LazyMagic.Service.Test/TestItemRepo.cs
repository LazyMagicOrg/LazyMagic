using LazyMagic.Service.Shared;
using LazyMagic.Service.DynamoDBRepo;
using Amazon.DynamoDBv2;
using Newtonsoft.Json.Linq;
using Amazon.DynamoDBv2.Model;

namespace LazyMagic.Service.Test;

public partial interface ITestItemRepo : IDocumentRepo<TestItem> { }
public partial class TestItemRepo : DYDBRepository<TestItem>, ITestItemRepo
{
    public TestItemRepo(IAmazonDynamoDB client) : base(client) { }

    protected override void AssignEntityAttributes(ICallerInfo callerInfo, JObject jobjectData, Dictionary<string, AttributeValue> dbrecord, long now)
    {
        base.AssignEntityAttributes(callerInfo, jobjectData, dbrecord, now);
        // Assign SK1 index
        var name = jobjectData["name"]?.ToString();
        if (name != null)
            dbrecord.Add("SK1", new AttributeValue { S = name });
    }

    protected override void AssignOptionalAttributes(ICallerInfo callerInfo, JObject jobjectData, Dictionary<string, AttributeValue> dbrecord, long now)
    {
        base.AssignOptionalAttributes(callerInfo, jobjectData, dbrecord, now);
    }

    protected override void AssignTTLAttribute(ICallerInfo callerInfo, JObject jobjectData, Dictionary<string, AttributeValue> dbrecord, long now)
    {
        base.AssignTTLAttribute(callerInfo, jobjectData, dbrecord, now);
    }

    protected override void AssignTopicsAttribute(ICallerInfo callerInfo, JObject jobjectData, Dictionary<string, AttributeValue> dbrecord, long now)
    {
        base.AssignTopicsAttribute(callerInfo, jobjectData, dbrecord, now);         
    }

    protected override void AssignDataAttribute(ICallerInfo callerInfo, JObject jobjectData, Dictionary<string, AttributeValue> dbrecord, long now)
    {
        base.AssignDataAttribute(callerInfo, jobjectData, dbrecord, now);
    }

    protected override string GetTableName(ICallerInfo callerInfo)
    {
        return base.GetTableName(callerInfo);   
    }

    protected override void AssignUpdateUtcTickAttribute(ICallerInfo callerInfo, JObject jobjectData, Dictionary<string, AttributeValue> dbrecord, long now)
    {
        base.AssignUpdateUtcTickAttribute(callerInfo, jobjectData, dbrecord, now);
    }

    protected override void AssignCreateUtcTickAttribute(ICallerInfo callerInfo, JObject jobjectData, Dictionary<string, AttributeValue> dbrecord, long now)
    {
        base.AssignCreateUtcTickAttribute(callerInfo, jobjectData, dbrecord, now);
    }

}   