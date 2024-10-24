
namespace LazyMagic.Client.Auth;

public interface IPhoneFormat
{
    IEnumerable<string> CheckPhoneFormat(string phone);
}