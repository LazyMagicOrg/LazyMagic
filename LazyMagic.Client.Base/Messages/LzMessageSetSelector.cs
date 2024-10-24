using System;
using System.Collections.Generic;
using System.Text;

namespace LazyMagic.Client.Base;

public class LzMessageSetSelector
{
    public string Culture { get; set; } = "en-US";
    public LzMessageUnits Units { get; set; } = LzMessageUnits.Imperial;
    public LzMessageSetSelector(string culture, LzMessageUnits units)
    {
        Culture = culture;
        Units = units;
    }
}
