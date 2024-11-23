namespace LazyMagic.Blazor;

public static class ConfigureLazyMagicBlazorMessages
{

    public static ILzMessages AddLazyMagicBlazorMessages(this ILzMessages lzMessages)
    {
        List<string> messages = [
            ];
        lzMessages.MessageFiles.AddRange(messages);
        return lzMessages;
    }
}
