namespace LazyMagic.Service.ControllerBase;

public interface IControllerUtils
{
    public Task<ICallerInfo> GetCallerInfoAsync(HttpRequest request, [CallerMemberName] string endpointName = "");
}
