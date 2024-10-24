using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LazyMagic.Client.Base;

namespace LazyMagic.MudBlazor.Components;


public static class ConfigureMudBlazorComponentsMessages
{
public static ILzMessages AddMudBlazorComponentsMessages(this ILzMessages lzMessages)
    {
        List<string> messages = [
            ];
        lzMessages.MessageFiles.AddRange(messages);
        return lzMessages;
    }

}
