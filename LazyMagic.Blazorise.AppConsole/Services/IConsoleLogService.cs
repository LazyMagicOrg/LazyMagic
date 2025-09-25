using System.Collections.Generic;
using System.Threading.Tasks;

namespace LazyMagic.Blazorise.Console;

public interface IConsoleLogService
{
    Task AddLogAsync(ConsoleLogEntry log);
    Task<List<ConsoleLogEntry>> GetLogsAsync();
    Task ClearLogsAsync();
}