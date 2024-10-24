namespace LazyMagic.Client.Auth;

public class CodeFormat : ICodeFormat
{

    public IEnumerable<string> CheckCodeFormat(string code)
    {
        if (code.Length != 6)
            yield return "AuthFormatMessages_Code01";
    }
}
