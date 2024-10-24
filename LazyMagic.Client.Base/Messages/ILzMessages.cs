namespace LazyMagic.Client.Base;

/// <summary>
/// ILzMessages is an interface for managing messages in a multi-language, multi-tenancy application.
/// 
/// </summary>
public interface ILzMessages : INotifyPropertyChanged
{
    // Properties
    /// <summary>
    /// List of cultures supported with frieldly names.
    /// ex: ("en-US", "English (United States)")
    /// </summary>
    public List<(string culture, string name)> Cultures { get; set; }
    /// <summary>
    /// The List of message files to be loaded. These file names do not include 
    /// the culture. The culture is added to the folder path name before loading.
    /// ex: "Assets/{culture}/MyApp/messages.json" 
    /// </summary>
    public List<string> MessageFiles { get; set; }
    /// <summary>
    /// Set this value to make access to static assets fully rooted.
    /// </summary>
    public string AssetsUrl { get; set; } 
    /// <summary>
    /// The current culture for the current message set.
    /// </summary>
    public string Culture { get; }
    /// <summary>
    /// The current units of measure for the current message set.
    /// </summary>
    public LzMessageUnits Units { get; }
    /// <summary>
    /// The current message set. The message set is a combination of the culture and the units of measure.
    /// </summary>
    public LzMessageSet MessageSet { get; }
    /// <summary>
    /// UseInspect is a flag that indicates that the message should be wrapped in a span tag
    /// which enables the WYSIWYG editor to display the key in the Assets editor.
    /// </summary>
    public bool UseInspect { get; set; }
    /// <summary>
    /// The number of times the message set has been refreshed. This is 
    /// useful for ReactiveUI to trigger a UI refresh. See the 
    /// Refresh() method.
    /// </summary>
    public int RefreshCount { get; set; }
    /// <summary>
    /// Dirty is true when there are unsaved changes to any of the loaded
    /// message sets.
    /// </summary>
    public bool Dirty { get; }

	// Methods
    /// <summary>
    /// Refresh the UI. This is useful for ReactiveUI to trigger a UI refresh.
    /// </summary>
    /// </summary>
	public void Refresh();
    /// <summary>
    /// Set the OSAccess object. This object abstracts the method 
    /// used to load the message files.
    /// </summary>
    /// <param name="staticAssets"></param>
    /// </summary>
    /// <param name="staticAssets"></param>
	public void SetStaticAssets(IStaticAssets staticAssets);
  
    /// <summary>
    /// Loads the specified message files for the specified culture and 
    /// units. Makes this message set the current message set. Affects 
    /// the MessageSet, Culture and Units properties.
    /// </summary>
    /// <param name="culture"></param>
    /// <param name="units"></param>
    /// <returns></returns>
    public Task SetMessageSetAsync(string culture, LzMessageUnits units);
    /// <summary>
    /// Returns the message for the specified key. If the key is not found, the key is returned.
    /// When the UseInspect property is true and ignoreUseInspect is false, the key is returned 
    /// in a span tag with the attributes class="static-content-message" and key="{key}". This
    /// enables the WYSIWYG editor to display the key in the asset editor.
    /// Note that this method uses the current _msgs dictionary where all the merging, variable 
    /// substitution and unit processing has been done. This is the method to use when displaying
    /// messages in the UI.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="ignoreUseInspect"></param>
    /// <returns></returns>
    public string Msg(string key, bool ignoreUseInspect = false, LzMessageUnits? unitsArg = null);

    /// <summary>
    /// Returns the img url for the specified key. 
    /// </summary>
    /// <param name="key"></param>
    /// <param name="ignoreUseInspect"></param>
    /// <returns></returns>
    public string Img(string key, bool ignoreUseInspect = false, LzMessageUnits? unitsArg = null);

    /// <summary>
    /// Returns a MsgItemsModel instance for the specified key where each 
    /// MsgItem is associated with one of the message files in the current message set. 
    /// This method is used by the WYSIWYG editor to present all the MsgItemModels 
    /// for the key. 
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public MsgItemsModel MsgItemsModel(string key);
    /// <summary>
    /// Sets the MsgItem for the specified culture and key. Each MsgItem knows what
    /// message set it it belongs to. Using the culture and key, it is possible to
    /// resolve the specific message file and update the MsgItem.
    /// </summary>
    /// <param name="culture"></param>
    /// <param name="key"></param>
    /// <param name="msgItem"></param>
    public void SetMsgItem(string culture, string key, MsgItem msgItem);

    /// <summary>
    /// Save any dirty message sets.
    /// </summary>
    public Task SaveMessageSetsAsync();


}
