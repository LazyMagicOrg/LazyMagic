namespace LazyMagic.Client.Auth;

public interface IPasswordFormat
{
    IEnumerable<string> CheckPasswordFormat(string password);
}