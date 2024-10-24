using System;
using System.Collections.Generic;
using System.Text;

namespace LazyMagic.Client.Base;

public class DocMetaData
{
    public bool Editable { get; set; } = false;
    public string Description { get; set; } = "";
    // ContentType is Messages or Images
    public string ContentType { get; set; } = "Messages";
    // Default only applies to ContentType == "Images"
    public bool Default { get; set; } = false;
}
