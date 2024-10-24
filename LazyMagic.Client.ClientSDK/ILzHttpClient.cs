using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.CompilerServices;

namespace LazyMagic.ClientSDK 
{ 
    // This interface surfaces only those HttpClient members actually
    // required by the generated code.
    public interface ILzHttpClient : IDisposable
    {
        // Note: CallerMember is inserted as a literal by the compiler in the IL so 
        // there is no performance penalty for using it.
        Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage requestMessage,
            HttpCompletionOption httpCompletionOption,
            CancellationToken cancellationToken,
            [CallerMemberName] string callerMemberName = null);
        bool IsServiceAvailable { get; }
    }
}