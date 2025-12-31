namespace BlazorUI;

public static class ConfigureBlazorUIMessages
{
    public static ILzMessages AddBlazorUIMessages(this ILzMessages lzMessages)
    {
        lzMessages.AddLazyMagicBlazorMessages();

        // Remember that the message file folders are modified to include the culture name 
        // by the load routine.
        // Example: system/SnapsApp/Messages.json => system/SnapsApp-en-US/Messages.json
        List<string> messages = [
            ];
        lzMessages.MessageFiles.AddRange(messages);
        return lzMessages;
    }
}
