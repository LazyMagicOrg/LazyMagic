using System;
using System.Collections.Generic;
using System.Text;

namespace LazyMagic.Client.Base;

public class MessageDoc
{
	public DocMetaData DocMetaData { get; set; } = new DocMetaData();
	public Dictionary<string, MsgItem> Messages { get; set; } = new Dictionary<string, MsgItem>();
    [JsonIgnore]
    public bool Dirty { get; set; }

    public async Task SaveAsync(string pathName)
	{
		if(!Dirty)
            return;
		await Task.Delay(0);
        var json = JsonConvert.SerializeObject(this, Formatting.Indented);
		// Todo: save file to host

		Dirty = false;
    }
}
 