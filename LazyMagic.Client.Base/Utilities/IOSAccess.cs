namespace LazyMagic.Client.Base;

public interface IOSAccess
{
    public void SetJSRuntime(object jSRuntime); 
    public Task LocalCreateAsync(string filepath, string content);
    public Task<string> LocalReadAsync(string filepath); 
    public Task LocalUpdateAsync(string filepath, string content);
    public Task LocalDeleteAsync(string filepath);
    public Task S3CreateAsync(string path, string content);
    public Task<string> S3ReadAsync(string path);
    public Task S3UpdateAsync(string path, string content);
    public Task S3DeleteAsync(string path);
    public Task<string> HttpReadAsync(string path);


}
