using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LazyMagic.Client.Base;

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
