using TG.Blazor.IndexedDB;
using System.Text.Json;

namespace LazyMagic.Blazorise.Console;

public class IndexedDbConsoleLogService : IConsoleLogService
{
    private readonly IndexedDBManager _indexedDbManager;
    private const string StoreName = "consoleLogs";

    public IndexedDbConsoleLogService(IndexedDBManager indexedDbManager)
    {
        _indexedDbManager = indexedDbManager;
    }

    public async Task AddLogAsync(ConsoleLogEntry log)
    {
        var entry = new StoreRecord<ConsoleLogEntry>
        {
            Storename = StoreName,
            Data = log
        };

        await _indexedDbManager.AddRecord(entry);
    }

    public async Task<List<ConsoleLogEntry>> GetLogsAsync()
    {
        var records = await _indexedDbManager.GetRecords<ConsoleLogEntry>(StoreName);
        return records.ToList();
    }

    public async Task ClearLogsAsync()
    {
        await _indexedDbManager.ClearStore(StoreName);
    }
}