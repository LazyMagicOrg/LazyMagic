﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LazyMagic.BlazoriseComponents;

public static class ConfigureLazyMagicBlazoriseComponentsAuthMessages
{
    public static ILzMessages AddLazyMagicBlazoriseComponentsAuthMessages(this ILzMessages lzMessages)
    {
        List<string> messages = [
            ];
        lzMessages.MessageFiles.AddRange(messages);
        return lzMessages;
    }
}