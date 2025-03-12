using LazyMagic.Client.Base;

namespace LazyMagic.MudBlazorComponents.Auth;

public static class ConfigureLazyMagicMudBlazorComponentsMessages
{
public static ILzMessages AddLazyMagicMudBlazorComponentsAuthMessages(this ILzMessages lzMessages)
    {
        List<string> messages = [
            ];
        lzMessages.MessageFiles.AddRange(messages);
        return lzMessages;
    }
}
