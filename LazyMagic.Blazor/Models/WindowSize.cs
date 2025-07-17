using System.Runtime.InteropServices;

namespace LazyMagic.Blazor;

public class WindowSize
{
    public int Width { get; set; }   
    public int Height { get; set; }  
    public bool IsLandscape => Width > Height;
    public bool IsPortrait => Width < Height;
    public int HeaderHeight { get; set; } = 0;
    public int MaxWidth { get; set; } = 0;  

}
