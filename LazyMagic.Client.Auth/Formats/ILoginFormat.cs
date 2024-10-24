namespace LazyMagic.Client.Auth;

public interface ILoginFormat
{
    public IEnumerable<string> CheckLoginFormat(string password);
}
