using Amazon.DynamoDBv2.Model;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LazyMagic.Service.Test;

/// <summary>
/// Pure unit tests (mocked IAmazonDynamoDB — no AWS, no DynamoDB Local) pinning the FAIL-CLOSED
/// contract of the single-record paths:
///
/// <list type="bullet">
/// <item><description>An exception the repository does not recognise is a failure to READ, reported
///   as 500. It is never reported as <c>NotFoundResult</c>. This is the regression these tests exist
///   for: all three catch-alls in <c>DYDBRepository</c> — <c>ReadAsync</c>'s and both of
///   <c>DeleteAsync</c>'s — used to return 404, and a refused TCP connection throws
///   <c>HttpRequestException</c>, which is neither of the two named AWS types. So an unreachable
///   store arrived at every caller in every consuming system as "no such row": an outage
///   indistinguishable from absence, and 404 is the one answer a client does not retry.</description></item>
/// <item><description>A row that genuinely is not there is STILL a 404. The fix must not buy
///   honesty about outages by making absence unreportable — which is why the absence cases are
///   pinned here beside the failure cases rather than assumed.</description></item>
/// <item><description>The two recognised AWS shapes keep their existing mapping: 500 for
///   <c>AmazonDynamoDBException</c>, 503 for <c>AmazonServiceException</c>. Order matters, because
///   the first derives from the second.</description></item>
/// </list>
/// </summary>
public class DYDBRepositoryFailClosedTests
{
    private static readonly ICallerInfo Caller = new CallerInfo
    {
        DefaultDB = "unit-test-table",
        LzUserId = "unit-test-user",
        TenantId = "unit-test-tenant",
        SessionId = "unit-test-session",
    };

    /// <summary>A repo whose GetItem always throws the given exception.</summary>
    private static TestItemRepo RepoWhoseReadThrows(Exception ex)
    {
        var client = new Mock<IAmazonDynamoDB>(MockBehavior.Loose);
        client
            .Setup(c => c.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(ex);
        return new TestItemRepo(client.Object);
    }

    /// <summary>A repo whose GetItem answers, and whose DeleteItem always throws.</summary>
    private static TestItemRepo RepoWhoseDeleteThrows(Exception ex)
    {
        var client = new Mock<IAmazonDynamoDB>(MockBehavior.Loose);
        client
            .Setup(c => c.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(ex);
        return new TestItemRepo(client.Object);
    }

    private static int? StatusOf(ActionResult? result) => result switch
    {
        ObjectResult objectResult => objectResult.StatusCode,
        StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
        _ => null
    };

    // ---- THE REGRESSION ------------------------------------------------------------------------

    [Fact]
    public async Task ReadAsync_WhenTheStoreIsUnreachable_Is500_AndNeverNotFound()
    {
        // The measured shape of a refused connection: AmazonDynamoDBClient pointed at a dead port
        // (http://127.0.0.1:1, MaxErrorRetry = 0) surfaces System.Net.Http.HttpRequestException,
        // which is neither AmazonDynamoDBException nor AmazonServiceException.
        var repo = RepoWhoseReadThrows(new HttpRequestException("Connection refused"));

        var result = await repo.ReadAsync(Caller, "any-id");

        Assert.Null(result.Value);
        Assert.IsNotType<NotFoundResult>(result.Result);
        Assert.Equal(500, StatusOf(result.Result));
    }

    [Fact]
    public async Task DeleteAsync_WhenTheStoreIsUnreachable_Is500_AndNeverNotFound()
    {
        // A delete that threw for an unrecognised reason may well have left the record in place, so
        // 404 ("there was nothing there") tells the caller the opposite of what is actually known.
        var repo = RepoWhoseDeleteThrows(new HttpRequestException("Connection refused"));

        var result = await repo.DeleteAsync(Caller, "any-id");

        Assert.IsNotType<NotFoundResult>(result);
        Assert.Equal(500, StatusOf(result));
    }

    [Fact]
    public async Task ReadAsync_WhenDeserializationFails_Is500_AndNeverNotFound()
    {
        // The other way an unrecognised exception reaches the catch-all, and the reason "unreachable
        // store" is too narrow a framing: a row that IS there but cannot be read back is corruption,
        // not absence, and reporting it as "no such row" hides it permanently.
        var client = new Mock<IAmazonDynamoDB>(MockBehavior.Loose);
        client
            .Setup(c => c.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>
                {
                    { "Data", new AttributeValue { S = "{ this is not json" } }
                }
            });

        var result = await new TestItemRepo(client.Object).ReadAsync(Caller, "any-id");

        Assert.Null(result.Value);
        Assert.IsNotType<NotFoundResult>(result.Result);
        Assert.Equal(500, StatusOf(result.Result));
    }

    // ---- ABSENCE IS STILL ABSENCE --------------------------------------------------------------

    [Fact]
    public async Task ReadAsync_WhenTheRowIsGenuinelyMissing_IsStill404()
    {
        // The half the fix must not break. This does NOT go through the catch-all: the method tests
        // the response for a null OR EMPTY Item and returns NotFoundResult from that check, which is
        // untouched. Asserting it here is what separates "fails closed" from "cannot say no".
        var client = new Mock<IAmazonDynamoDB>(MockBehavior.Loose);
        client
            .Setup(c => c.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse());

        var result = await new TestItemRepo(client.Object).ReadAsync(Caller, "missing-id");

        Assert.Null(result.Value);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task ReadAsync_WhenTheRowHasNoData_IsStill404()
    {
        var client = new Mock<IAmazonDynamoDB>(MockBehavior.Loose);
        client
            .Setup(c => c.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>
                {
                    { "Data", new AttributeValue { S = "" } }
                }
            });

        var result = await new TestItemRepo(client.Object).ReadAsync(Caller, "empty-id");

        Assert.Null(result.Value);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---- THE TWO RECOGNISED AWS SHAPES ARE UNCHANGED -------------------------------------------

    [Fact]
    public async Task ReadAsync_OnAmazonDynamoDBException_Is500()
    {
        var repo = RepoWhoseReadThrows(new AmazonDynamoDBException("throttled"));

        Assert.Equal(500, StatusOf((await repo.ReadAsync(Caller, "any-id")).Result));
    }

    [Fact]
    public async Task ReadAsync_OnAmazonServiceException_Is503()
    {
        // Pins the CATCH ORDER as much as the mapping: AmazonDynamoDBException derives from
        // AmazonServiceException, so swapping the two clauses would silently turn every 503 into a
        // 500 without failing anything else.
        var repo = RepoWhoseReadThrows(new AmazonServiceException("service unavailable"));

        Assert.Equal(503, StatusOf((await repo.ReadAsync(Caller, "any-id")).Result));
    }
}
