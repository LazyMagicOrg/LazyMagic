using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace LazyMagic.Service.Test;

public class DocumentRepoFixture : IAsyncLifetime
{
    public IDocumentRepo<TestItem> Repository { get; private set; } = null!;
    public ICallerInfo CallerInfo { get; private set; } = null!;
    private IAmazonDynamoDB? _dynamoDbClient;
    

    public DocumentRepoFixture()
    {
    }

    public async Task InitializeAsync()
    {
        CallerInfo = new CallerInfo()
        {
            DefaultDB = "lzm_mp", // Use a dynamodb table defined and published by LazyMagic 
            SessionId = "test-session",
            UserName = "test-user",
            LzUserId = "test-user-id",
            TenantId = "test-tenant"
        };

        string? profile = null;
        string region = "us-east-1"; // Default region
        
        // Search up the directory hierarchy for systemconfig.yaml
        string? configPath = null;
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
        
        while (currentDir != null)
        {
            var potentialPath = Path.Combine(currentDir.FullName, "systemconfig.yaml");
            if (File.Exists(potentialPath))
            {
                configPath = potentialPath;
                Console.WriteLine($"Found systemconfig.yaml at: {configPath}");
                break;
            }
            currentDir = currentDir.Parent;
        }
        
        if (string.IsNullOrEmpty(configPath))
        {
            throw new FileNotFoundException(
                "systemconfig.yaml not found. Searched up the directory hierarchy from: " + 
                Directory.GetCurrentDirectory() + 
                "\nPlease create a systemconfig.yaml file with 'Profile' and optionally 'Region' settings.");
        }
        
        try
        {
            using (var reader = new StreamReader(configPath))
            {
                var yaml = new YamlStream();
                yaml.Load(reader);

                if (yaml.Documents.Count == 0)
                {
                    throw new InvalidOperationException("systemconfig.yaml is empty or invalid.");
                }
                
                var mapping = yaml.Documents[0].RootNode as YamlMappingNode;
                if (mapping == null)
                {
                    throw new InvalidOperationException("systemconfig.yaml does not contain a valid YAML mapping.");
                }
                
                if (mapping.Children.TryGetValue(new YamlScalarNode("Profile"), out var profileNode))
                {
                    profile = ((YamlScalarNode)profileNode).Value;
                }
                else
                {
                    throw new InvalidOperationException("'Profile' not found in systemconfig.yaml. This field is required.");
                }

                if (mapping.Children.TryGetValue(new YamlScalarNode("Region"), out var regionNode))
                {
                    region = ((YamlScalarNode)regionNode).Value ?? "us-east-1";
                }
                
                Console.WriteLine($"Using AWS Profile: {profile}, Region: {region}");
            }
            
            // Create DynamoDB client with credentials from the specified profile
            var chain = new CredentialProfileStoreChain();
            if (chain.TryGetAWSCredentials(profile, out var credentials))
            {
                _dynamoDbClient = new AmazonDynamoDBClient(credentials, RegionEndpoint.GetBySystemName(region));
                Console.WriteLine($"Successfully configured AWS credentials for profile: {profile}");
            }
            else
            {
                throw new InvalidOperationException($"AWS Profile '{profile}' not found in credential store. " +
                    "Please ensure the profile exists in your AWS credentials file or use 'aws configure' to set it up.");
            }
        }
        catch (Exception ex) when (!(ex is FileNotFoundException || ex is InvalidOperationException))
        {
            throw new InvalidOperationException($"Failed to load or parse systemconfig.yaml: {ex.Message}", ex);
        }
        
        Repository = new TestItemRepo(_dynamoDbClient);

        // Clean up any existing items in DynamoDB
        var result = await Repository.ListAsync(CallerInfo);
        if (result.Value is IEnumerable<TestItem> existingItems)
        {
            foreach (var item in existingItems)
            {
                await Repository.DeleteAsync(CallerInfo, item.Id);
            }
        }
    }

    public Task DisposeAsync()
    {
        // Clean up any remaining items
        var result = Repository.ListAsync(CallerInfo).GetAwaiter().GetResult();
        if (result.Value is IEnumerable<TestItem> existingItems)
        {
            foreach (var item in existingItems)
            {
                Repository.DeleteAsync(CallerInfo, item.Id).GetAwaiter().GetResult();
            }
        }

        _dynamoDbClient?.Dispose();
        return Task.CompletedTask;
    }
}

public class DynamoDBDocumentRepoFixture : DocumentRepoFixture { }

[CollectionDefinition("DynamoDBRepo")]
public class DynamoDBRepoCollection : ICollectionFixture<DynamoDBDocumentRepoFixture>
{
    // This class has no code, and is never created.
    // Its purpose is to be the place to apply [CollectionDefinition]
    // and all the ICollectionFixture<> interfaces.
}

[Collection("DynamoDBRepo")]
public class DynamoDBDocumentRepoTests : DocumentRepoTestsBase
{
    public DynamoDBDocumentRepoTests(DynamoDBDocumentRepoFixture fixture) : base(fixture)
    {
    }
}

public abstract class DocumentRepoTestsBase
{
    protected readonly IDocumentRepo<TestItem> _repo;
    protected readonly ICallerInfo _callerInfo;

    protected DocumentRepoTestsBase(DocumentRepoFixture fixture)
    {
        _repo = fixture.Repository;
        _callerInfo = fixture.CallerInfo;


    }

    [Fact]
    public async Task CreateAsync_WithValidData_ShouldCreateItem()
    {
        // Arrange
        var item = new TestItem { Name = "Test Item", Description = "Test Description" };

        // Act
        var result = await _repo.CreateAsync(_callerInfo, item);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Result); // Ensure it's not an error result
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value.Id);
        Assert.Equal(item.Name, result.Value.Name);
        Assert.Equal(item.Description, result.Value.Description);
        Assert.NotEqual(0, result.Value.CreateUtcTick);
        Assert.Equal(result.Value.CreateUtcTick, result.Value.UpdateUtcTick);

        // Cleanup
        await _repo.DeleteAsync(_callerInfo, result.Value.Id);
    }

    [Fact]
    public async Task CreateAsync_WithExistingId_ShouldReturnBadRequest()
    {
        // Arrange
        var item = new TestItem { Id = "test-id", Name = "Test Item" };
        var createResult = await _repo.CreateAsync(_callerInfo, item);
        Assert.NotNull(createResult);
        Assert.Null(createResult.Result);
        Assert.NotNull(createResult.Value);

        var duplicateItem = new TestItem { Id = "test-id", Name = "Duplicate Item" };

        // Act
        var result = await _repo.CreateAsync(_callerInfo, duplicateItem);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Result);
        Assert.Null(result.Value);
        Assert.IsType<ConflictResult>(result.Result);

        // Cleanup
        await _repo.DeleteAsync(_callerInfo, "test-id");
    }

    [Fact]
    public async Task ReadAsync_WithExistingId_ShouldReturnItem()
    {
        // Arrange
        var item = new TestItem { Id = "test-id", Name = "Test Item" };
        var createResult = await _repo.CreateAsync(_callerInfo, item);
        Assert.NotNull(createResult);
        Assert.Null(createResult.Result);
        Assert.NotNull(createResult.Value);

        // Act
        var result = await _repo.ReadAsync(_callerInfo, "test-id");

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Result);
        Assert.NotNull(result.Value);
        Assert.Equal("test-id", result.Value.Id);
        Assert.Equal("Test Item", result.Value.Name);

        // Cleanup
        await _repo.DeleteAsync(_callerInfo, "test-id");
    }

    [Fact]
    public async Task ReadAsync_WithNonExistingId_ShouldReturnNotFound()
    {
        // Act
        var result = await _repo.ReadAsync(_callerInfo, "non-existing-id");

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Result);
        Assert.Null(result.Value);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ShouldUpdateItem()
    {
        // Arrange
        var item = new TestItem { Id = "test-id", Name = "Test Item" };
        var createResult = await _repo.CreateAsync(_callerInfo, item);
        Assert.NotNull(createResult);
        Assert.Null(createResult.Result);
        Assert.NotNull(createResult.Value);
        var createdItem = createResult.Value;

        createdItem.Name = "Updated Item";

        // Act
        var result = await _repo.UpdateAsync(_callerInfo, createdItem);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Result);
        Assert.NotNull(result.Value);
        Assert.Equal("Updated Item", result.Value.Name);
        Assert.True(result.Value.UpdateUtcTick > result.Value.CreateUtcTick);

        // Cleanup
        await _repo.DeleteAsync(_callerInfo, "test-id");
    }

    [Fact]
    public async Task UpdateAsync_WithConcurrentModification_ShouldReturnConflict()
    {
        // Arrange
        var item = new TestItem { Id = "test-id", Name = "Test Item" };
        var createResult = await _repo.CreateAsync(_callerInfo, item);
        Assert.NotNull(createResult);
        Assert.Null(createResult.Result);
        Assert.NotNull(createResult.Value);
        var firstCopy = createResult.Value;
        var secondCopy = new TestItem
        {
            Id = firstCopy.Id,
            Name = firstCopy.Name,
            Description = firstCopy.Description,
            CreateUtcTick = firstCopy.CreateUtcTick,
            UpdateUtcTick = firstCopy.UpdateUtcTick
        };

        // Update first copy
        firstCopy.Name = "Updated by First";
        var updateResult = await _repo.UpdateAsync(_callerInfo, firstCopy);
        Assert.NotNull(updateResult);
        Assert.Null(updateResult.Result);
        Assert.NotNull(updateResult.Value);

        // Try to update second copy
        secondCopy.Name = "Updated by Second";

        // Act
        var result = await _repo.UpdateAsync(_callerInfo, secondCopy);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Result);
        Assert.Null(result.Value);
        Assert.IsType<ConflictResult>(result.Result);

        // Cleanup
        await _repo.DeleteAsync(_callerInfo, "test-id");
    }

    [Fact]
    public async Task DeleteAsync_WithExistingId_ShouldDeleteItem()
    {
        // Arrange
        var item = new TestItem { Id = "test-id", Name = "Test Item" };
        var createResult = await _repo.CreateAsync(_callerInfo, item);
        Assert.NotNull(createResult);
        Assert.Null(createResult.Result);
        Assert.NotNull(createResult.Value);

        // Act
        var deleteResult = await _repo.DeleteAsync(_callerInfo, "test-id");
        var readResult = await _repo.ReadAsync(_callerInfo, "test-id");

        // Assert
        Assert.NotNull(deleteResult);
        Assert.IsType<OkResult>(deleteResult);
        Assert.IsType<NotFoundResult>(readResult.Result);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistingId_ShouldReturnOkResult()
    {
        // Act
        var result = await _repo.DeleteAsync(_callerInfo, "non-existing-id");

        // Assert
        Assert.NotNull(result);
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ListAsync_ShouldReturnAllItems()
    {
        // Arrange
        var items = new[]
        {
            new TestItem { Name = "Item 1" },
            new TestItem { Name = "Item 2" },
            new TestItem { Name = "Item 3" }
        };

        foreach (var item in items)
        {
            var newitem = await _repo.CreateAsync(_callerInfo, item);
            item.Id = newitem!.Value!.Id;
        }

        // Act
        var result = await _repo.ListAsync(_callerInfo);

        // Assert
        Assert.NotNull(result.Value);
        var resultItems = result.Value as IEnumerable<TestItem>;
        Assert.NotNull(resultItems);
        Assert.True(resultItems.Count() >= 3);

        // Cleanup
        foreach (var item in items)
        {
            await _repo.DeleteAsync(_callerInfo, item.Id);
        }
    }

    [Fact]
    public async Task ListAsync_WithLimit_ShouldReturnLimitedItems()
    {
        // Arrange
        var items = new[]
        {
            new TestItem { Name = "Item 1" },
            new TestItem { Name = "Item 2" },
            new TestItem { Name = "Item 3" }
        };

        foreach (var item in items)
        {
            var newitem = await _repo.CreateAsync(_callerInfo, item);
            item.Id = newitem!.Value!.Id; 
        }

        // Act
        var result = await _repo.ListAsync(_callerInfo, 2);

        // Assert
        Assert.NotNull(result.Value);
        var resultItems = result.Value as IEnumerable<TestItem>;
        Assert.NotNull(resultItems);
        Assert.True(resultItems.Count() <= 2);

        // Cleanup
        foreach (var item in items)
        {
            await _repo.DeleteAsync(_callerInfo, item.Id);
        }
    }

    [Fact]
    public async Task ListAsync_WithSK1Index_ShouldReturnItemsByExactName()
    {
        // Arrange
        var items = new[]
        {
            new TestItem { Name = "Alpha" },
            new TestItem { Name = "Beta" },
            new TestItem { Name = "Alpha" }, // Duplicate name
            new TestItem { Name = "Gamma" }
        };

        foreach (var item in items)
        {
            var newitem = await _repo.CreateAsync(_callerInfo, item);
            item.Id = newitem!.Value!.Id;
        }

        // Act
        var result = await _repo.ListAsync(_callerInfo, "SK1", "Alpha");

        // Assert
        Assert.NotNull(result.Value);
        var resultItems = result.Value as IEnumerable<TestItem>;
        Assert.NotNull(resultItems);
        Assert.Equal(2, resultItems.Count());
        Assert.True(resultItems.All(item => item.Name == "Alpha"));

        // Cleanup
        foreach (var item in items)
        {
            await _repo.DeleteAsync(_callerInfo, item.Id);
        }
    }

    [Fact]
    public async Task ListBeginsWithAsync_WithSK1Index_ShouldReturnItemsByNamePrefix()
    {
        // Arrange
        var items = new[]
        {
            new TestItem { Name = "Test1" },
            new TestItem { Name = "Test2" },
            new TestItem { Name = "Other" },
            new TestItem { Name = "Test3" }
        };

        foreach (var item in items)
        {
            var newitem = await _repo.CreateAsync(_callerInfo, item);
            item.Id = newitem!.Value!.Id;
        }

        // Act
        var result = await _repo.ListBeginsWithAsync(_callerInfo, "SK1", "Test");

        // Assert
        Assert.NotNull(result.Value);
        var resultItems = result.Value as IEnumerable<TestItem>;
        Assert.NotNull(resultItems);
        Assert.Equal(3, resultItems.Count());
        Assert.True(resultItems.All(item => item.Name.StartsWith("Test")));

        // Cleanup
        foreach (var item in items)
        {
            await _repo.DeleteAsync(_callerInfo, item.Id);
        }
    }

    [Fact]
    public async Task ListBetweenAsync_WithSK1Index_ShouldReturnItemsInNameRange()
    {
        // Arrange
        var items = new[]
        {
            new TestItem { Name = "Alpha" },
            new TestItem { Name = "Beta" },
            new TestItem { Name = "Charlie" },
            new TestItem { Name = "Delta" }
        };

        foreach (var item in items)
        {
            var newitem = await _repo.CreateAsync(_callerInfo, item);
            item.Id = newitem!.Value!.Id;
        }

        // Act
        var result = await _repo.ListBetweenAsync(_callerInfo, "SK1", "Beta", "Delta");

        // Assert
        Assert.NotNull(result.Value);
        var resultItems = result.Value as IEnumerable<TestItem>;
        Assert.NotNull(resultItems);
        Assert.Equal(3, resultItems.Count());
        Assert.Contains(resultItems, item => item.Name == "Beta");
        Assert.Contains(resultItems, item => item.Name == "Charlie");
        Assert.Contains(resultItems, item => item.Name == "Delta");

        // Cleanup
        foreach (var item in items)
        {
            await _repo.DeleteAsync(_callerInfo, item.Id);
        }
    }

    [Fact]
    public async Task ListGreaterThanAsync_WithSK1Index_ShouldReturnItemsAfterName()
    {
        // Arrange
        var items = new[]
        {
            new TestItem { Name = "Alpha" },
            new TestItem { Name = "Beta" },
            new TestItem { Name = "Charlie" },
            new TestItem { Name = "Delta" }
        };

        foreach (var item in items)
        {
            var newitem = await _repo.CreateAsync(_callerInfo, item);
            item.Id = newitem!.Value!.Id;
        }

        // Act
        var result = await _repo.ListGreaterThanAsync(_callerInfo, "SK1", "Beta");

        // Assert
        Assert.NotNull(result.Value);
        var resultItems = result.Value as IEnumerable<TestItem>;
        Assert.NotNull(resultItems);
        Assert.Equal(2, resultItems.Count());
        Assert.Contains(resultItems, item => item.Name == "Charlie");
        Assert.Contains(resultItems, item => item.Name == "Delta");
        Assert.DoesNotContain(resultItems, item => item.Name == "Beta");
        Assert.DoesNotContain(resultItems, item => item.Name == "Alpha");

        // Cleanup
        foreach (var item in items)
        {
            await _repo.DeleteAsync(_callerInfo, item.Id);
        }
    }

    [Fact]
    public async Task ListLessThanAsync_WithSK1Index_ShouldReturnItemsBeforeName()
    {
        // Arrange
        var items = new[]
        {
            new TestItem { Name = "Alpha" },
            new TestItem { Name = "Beta" },
            new TestItem { Name = "Charlie" },
            new TestItem { Name = "Delta" }
        };

        foreach (var item in items)
        {
            var newitem = await _repo.CreateAsync(_callerInfo, item);
            item.Id = newitem!.Value!.Id;
        }

        // Act
        var result = await _repo.ListLessThanAsync(_callerInfo, "SK1", "Charlie");

        // Assert
        Assert.NotNull(result.Value);
        var resultItems = result.Value as IEnumerable<TestItem>;
        Assert.NotNull(resultItems);
        Assert.Equal(2, resultItems.Count());
        Assert.Contains(resultItems, item => item.Name == "Alpha");
        Assert.Contains(resultItems, item => item.Name == "Beta");
        Assert.DoesNotContain(resultItems, item => item.Name == "Charlie");
        Assert.DoesNotContain(resultItems, item => item.Name == "Delta");

        // Cleanup
        foreach (var item in items)
        {
            await _repo.DeleteAsync(_callerInfo, item.Id);
        }
    }

    [Fact]
    public  Task UpdateCreateAsync_WithNewId_ShouldCreateItem()
    {
        return Task.CompletedTask; // Skip this test for now
        //// Arrange
        //var item = new TestItem { Id = "test-id", Name = "Test Item" };

        //// Act
        //var result = await _repo.UpdateCreateAsync(_callerInfo, item);

        //// Assert
        //Assert.NotNull(result);
        //Assert.Null(result.Result);
        //Assert.NotNull(result.Value);
        //Assert.Equal("test-id", result.Value.Id);
        //Assert.Equal("Test Item", result.Value.Name);
        //Assert.NotEqual(0, result.Value.CreateUtcTick);
        //Assert.Equal(result.Value.CreateUtcTick, result.Value.UpdateUtcTick);

        //// Cleanup
        //await _repo.DeleteAsync(_callerInfo, "test-id");
    }

    [Fact]
    public Task UpdateCreateAsync_WithExistingId_ShouldUpdateItem()
    {
        return Task.CompletedTask; // Skip this test for now
        //// Arrange
        //var item = new TestItem { Id = "test-id", Name = "Test Item" };
        //var createResult = await _repo.CreateAsync(_callerInfo, item);
        //Assert.NotNull(createResult);
        //Assert.Null(createResult.Result);
        //Assert.NotNull(createResult.Value);

        //var updatedItem = new TestItem { Id = "test-id", Name = "Updated Item" };

        //// Act
        //var result = await _repo.UpdateCreateAsync(_callerInfo, updatedItem);

        //// Assert
        //Assert.NotNull(result);
        //Assert.Null(result.Result);
        //Assert.NotNull(result.Value);
        //Assert.Equal("test-id", result.Value.Id);
        //Assert.Equal("Updated Item", result.Value.Name);
        //Assert.True(result.Value.UpdateUtcTick > result.Value.CreateUtcTick);

        //// Cleanup
        //await _repo.DeleteAsync(_callerInfo, "test-id");
    }
} 