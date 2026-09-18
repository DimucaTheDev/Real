using System.Numerics;
using Real.Core.Input;

namespace Real.Windowing;

public interface IWindow : IDisposable
{
    string Title { get; set; }
    WindowPosition Position { get; set; }
    WindowSize Size { get; set; }
    WindowState State { get; set; }
    WindowBorder Border { get; set; }
    bool IsVisible { get; set; }
    bool IsFocused { get; set; }

    bool IsClosing { get; } // should be true after Close() or X button

    nint Handle { get; }
    GraphicsApi GraphicsApi { get; }

    void Show();
    void Hide();

    void Close(); // only MARKS window for closing

    void PollEvents();

    void SwapBuffers();

    event Action? Load;
    event Action? Closing;
    event Action<WindowSize>? Resized;
    event Action<WindowPosition>? Moved;
    event Action<WindowState>? StateChanged;
    event Action<bool>? FocusChanged;
    event Action<string[]>? FilesDropped;
    
    event Action<Vector2>? MouseMoved;
    event Action<MouseButton, bool>? MouseButtonChanged;
    event Action<Vector2>? MouseScrolled;
    event Action<KeyCode, bool>? KeyChanged;
    event Action<char>? CharacterInput;
}