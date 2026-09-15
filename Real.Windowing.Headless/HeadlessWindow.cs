namespace Real.Windowing.Headless;

public class HeadlessWindow(GraphicsApi api) : IWindow
{
    public void Dispose() { }

    public string Title { get; set; }
    public WindowPosition Position { get; set; }
    public WindowSize Size { get; set; }
    public WindowState State { get; set; }
    public WindowBorder Border { get; set; }
    public bool IsVisible { get; set; }
    public bool IsFocused { get; set; }
    public bool IsClosing { get; }
    public IntPtr Handle { get; }
    public GraphicsApi GraphicsApi { get; } = api;

    public void Show() { }

    public void Hide() { }

    public void Close() { }

    public void PollEvents() { }

    public void SwapBuffers() { }

    public event Action? Load;
    public event Action? Closing;
    public event Action<WindowSize>? Resized;
    public event Action<WindowPosition>? Moved;
    public event Action<WindowState>? StateChanged;
    public event Action<bool>? FocusChanged;
    public event Action<string[]>? FilesDropped;
}