namespace LazyMagic.Client.Auth;

public interface IEmailFormat
{
    IEnumerable<string> CheckEmailFormat(string email);
}