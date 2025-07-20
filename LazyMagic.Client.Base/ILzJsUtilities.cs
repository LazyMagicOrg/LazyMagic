
namespace LazyMagic.Client.Base;
public interface ILzJsUtilities : INotifyPropertyChanged
{
    string ModuleFileName { get; }
	bool CheckingAssetData { get; }
	bool UpdatingAssetData { get; }
	bool UpdatingServiceWorker { get; }
	string CacheMiss { get; set; }
    void SetJSRuntime(object jsRuntime);
	ValueTask Initialize();
	ValueTask CheckForNewAssetData();
    ValueTask Reload();
    ValueTask<int> GetMemory();
	ValueTask SetPointerCapture(object elementRef, long pointerId);

	ValueTask<string> GetBase64Image(object elementReferenceImg);
    ValueTask<string> GetBase64ImageDownsized(object elementReferenceImg);
	ValueTask<bool> SharePng(string title, string text, string pngData, string? textData = null);
	ValueTask<bool> ShareText(string title, string text);
	ValueTask SetItem(string key, string value);
	ValueTask<string> GetItem(string key);
	ValueTask RemoveItem(string key);

	// Callbacks. ie. [JSInvokable]
	void AssetDataCheckStarted();
	void AssetDataCheckComplete();
    void AssetDataUpdateStarted();
    void AssetDataUpdateComplete();
    void ServiceWorkerUpdateStarted();
    void ServiceWorkerUpdateComplete();
	void CacheMissAction(string url);
	void MessageSelected(string key, string value);

}
