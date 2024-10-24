using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
