namespace LazyMagic.Service.Authorization;
public interface ILzAuthorization
{
    public Task<ICallerInfo> GetCallerInfoAsync(HttpRequest request, [CallerMemberName] string endpointName = "");
}
