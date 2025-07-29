namespace LazyMagic.Blazor;

public static class ConfigureLazyMagicBlazorMessages
{

    public static ILzMessages AddLazyMagicBlazorMessages(this ILzMessages lzMessages)
    {
        List<string> messages = [
                        "system/{culture}/System/AuthMessages.json",
                        "system/{culture}/System/BaseMessages.json"
            ];
        lzMessages.MessageFiles.AddRange(messages);
        return lzMessages;
    }
}
