namespace LazyMagic.Client.Auth;

public class EmailFormat : IEmailFormat
{
    public IEnumerable<string> CheckEmailFormat(string? email)
    {
        string? msg = null;
        email = email ?? string.Empty;
        try
        {
            var result = new MailAddress(email);
        }
        catch
        {
            msg = "AuthFormatMessages_Email01";
        }

        if (msg != null)
            yield return msg;
    }
}
