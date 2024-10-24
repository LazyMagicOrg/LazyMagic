using System;
using System.Collections.Generic;
using System.Text;

namespace LazyMagic.Client.Base;

/// <summary>
/// MsgItem contains all the information necessary to 
/// create/edit/save a message to a message file in 
/// a message set.
/// </summary>
public class MsgItem : NotifyBase
{
    private string _msg = "";
    public string Msg 
    {   get => _msg; 
        set => SetProperty(ref _msg, value); 
    }
    public bool? Editable { get; set; }
}
