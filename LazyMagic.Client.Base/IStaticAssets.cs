using System;
using System.Collections.Generic;
using System.Text;

namespace LazyMagic.Client.Base;

public interface IStaticAssets
{
    public Task<string> ReadAuthConfigAsync(string filepath);
    public Task<string> ReadTenancyConfigAsync(string filepath);
    public Task<string> ReadContentAsync(string filepath);
}
