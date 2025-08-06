namespace LazyMagic.Blazor;

public class WindowSize
{
    public BrowserWindowSize BrowserWindowSize { get; set; } = new BrowserWindowSize();
    public int Width => BrowserWindowSize.Width;
    public int Height => BrowserWindowSize.Height;
    public bool IsLandscape => Width >= Height;
    public bool IsPortrait => Width < Height;
    public int HeaderHeight { get; set; } = 0;
    /// <summary>
    /// MaxWidth for layout 
    /// </summary>
    public int MaxWidth { get; set; } = 0;  
    /// <summary>
    /// MinWidth for layout
    /// </summary>
    public int MinWidth { get; set; } = 0;

}
