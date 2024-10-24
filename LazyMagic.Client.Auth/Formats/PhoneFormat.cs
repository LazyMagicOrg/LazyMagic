namespace LazyMagic.Client.Auth;

public class PhoneFormat : IPhoneFormat
{
    public IEnumerable<string> CheckPhoneFormat(string phone)
    {
        var ok = false;
        if (ok) // todo - create a ruleset for phone numbers
            yield return "AuthFormatMessages_Phone01";
    }
}
