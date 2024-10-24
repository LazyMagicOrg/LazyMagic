using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LazyMagic.BlazoriseComponents;
public static class ConfigureLazyMagicBlazoriseComponentsMessages
{
    public static ILzMessages AddLazyMagicBlazoriseComponentsMessages(this ILzMessages lzMessages)
    {
        List<string> messages = [
            ];
        lzMessages.MessageFiles.AddRange(messages);
        return lzMessages;
    }
}