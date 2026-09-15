using System.Runtime.InteropServices;
using Silk.NET.GLFW;

namespace Real.Windowing.Glfw;

public unsafe class GlfwWindow : IWindow
{
    public IntPtr Handle => (IntPtr)_handle;
    public GraphicsApi GraphicsApi { get; }

    public string Title
    {
        get;
        set
        {
            field = value;
            if (_handle != null)
            {
                _glfw.SetWindowTitle(_handle, field);
            }
        }
    } = "GLFW Window";

    public WindowPosition Position
    {
        get => _position;
        set
        {
            _position = value;
            if (_handle != null)
            {
                _glfw.SetWindowPos(_handle, value.X, value.Y);
            }
        }
    }

    public WindowSize Size
    {
        get => _size;
        set
        {
            _size = value;
            if (_handle != null)
            {
                _glfw.SetWindowSize(_handle, value.Width, value.Height);
            }
        }
    }

    public WindowState State
    {
        get => _state;
        set
        {
            _state = value;
            if (_handle != null)
            {
                ApplyState(value);
            }
        }
    }

    public WindowBorder Border
    {
        get;
        set
        {
            field = value;
            if (_handle != null)
            {
                _glfw.SetWindowAttrib(_handle, WindowAttributeSetter.Resizable, value == WindowBorder.Resizable);
                _glfw.SetWindowAttrib(_handle, WindowAttributeSetter.Decorated, value != WindowBorder.Hidden);
            }
        }
    } = WindowBorder.Resizable;

    public bool IsVisible
    {
        get => _handle != null && _glfw.GetWindowAttrib(_handle, WindowAttributeGetter.Visible);
        set
        {
            if (_handle == null) return;
            if (value) Show();
            else Hide();
        }
    }

    public bool IsFocused
    {
        get => _handle != null && _glfw.GetWindowAttrib(_handle, WindowAttributeGetter.Focused);
        set
        {
            if (_handle != null && value)
            {
                _glfw.FocusWindow(_handle);
            }
        }
    }

    public bool IsClosing
    {
        get => _handle != null && _glfw.WindowShouldClose(_handle);
        set
        {
            if (_handle != null)
            {
                _glfw.SetWindowShouldClose(_handle, value);
            }
        }
    }

    public event Action? Load;
    public event Action? Closing;
    public event Action<WindowSize>? Resized;
    public event Action<WindowPosition>? Moved;
    public event Action<WindowState>? StateChanged;
    public event Action<bool>? FocusChanged;
    public event Action<string[]>? FilesDropped;

    private readonly Silk.NET.GLFW.Glfw _glfw = Silk.NET.GLFW.Glfw.GetApi();
    private WindowHandle* _handle;

    private WindowSize _size = WindowSize.Hd;
    private WindowPosition _position;
    private WindowState _state = WindowState.Normal;

    private readonly GlfwCallbacks.WindowSizeCallback _sizeCallback;
    private readonly GlfwCallbacks.WindowPosCallback _posCallback;
    private readonly GlfwCallbacks.WindowFocusCallback _focusCallback;
    private readonly GlfwCallbacks.WindowCloseCallback _closeCallback;
    private readonly GlfwCallbacks.DropCallback _dropCallback;

    public GlfwWindow(GraphicsApi api)
    { 
        if (!_glfw.Init())
        {
            throw new SystemException("Unable to initialize GLFW");
        }

        GraphicsApi = api;

        _sizeCallback = OnWindowResized;
        _posCallback = OnWindowMoved;
        _focusCallback = OnWindowFocusChanged;
        _closeCallback = OnWindowClosing;
        _dropCallback = OnFilesDropped;
    }

    public void Show()
    {
        if (_handle == null)
        {
            CreateWindow();
        }

        _glfw.ShowWindow(_handle);
        Load?.Invoke();
    }

    public void Hide()
    {
        if (_handle != null)
        {
            _glfw.HideWindow(_handle);
        }
    }

    public void Close()
    {
        if (_handle != null)
        {
            _glfw.SetWindowShouldClose(_handle, true);
            OnWindowClosing(_handle);
        }
    }

    public void PollEvents()
    {
        _glfw.PollEvents();
    }

    public void SwapBuffers()
    {
        if (_handle != null)
        {
            _glfw.SwapBuffers(_handle);
        }
    }

    private void CreateWindow()
    {
        _glfw.WindowHint(WindowHintBool.Visible, false);
        _glfw.WindowHint(WindowHintBool.Resizable, Border == WindowBorder.Resizable);
        _glfw.WindowHint(WindowHintBool.Decorated, Border != WindowBorder.Hidden);
        _glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.NoApi);

        _handle = _glfw.CreateWindow(Size.Width, Size.Height, Title, null, null);
        if (_handle == null)
        {
            throw new SystemException("Unable to create GLFW window");
        }


        _glfw.SetWindowSizeCallback(_handle, _sizeCallback);
        _glfw.SetWindowPosCallback(_handle, _posCallback);
        _glfw.SetWindowFocusCallback(_handle, _focusCallback);
        _glfw.SetWindowCloseCallback(_handle, _closeCallback);
        _glfw.SetDropCallback(_handle, _dropCallback);

        ApplyState(_state);
    }

    private void ApplyState(WindowState state)
    {
        _state = state;

        switch (state)
        {
            case WindowState.Normal:
                _glfw.RestoreWindow(_handle);
                break;
            case WindowState.Minimized:
                _glfw.IconifyWindow(_handle);
                break;
            case WindowState.Maximized:
                _glfw.MaximizeWindow(_handle);
                break;
            case WindowState.Fullscreen:
                var monitor = _glfw.GetPrimaryMonitor();
                var mode = _glfw.GetVideoMode(monitor);
                _glfw.SetWindowMonitor(_handle, monitor, 0, 0, mode->Width, mode->Height, mode->RefreshRate);
                break;
        }

        StateChanged?.Invoke(_state);
    }

    private void OnWindowResized(WindowHandle* window, int width, int height)
    {
        _size = new WindowSize(width, height);
        Resized?.Invoke(_size);
    }

    private void OnWindowMoved(WindowHandle* window, int x, int y)
    {
        _position = new WindowPosition(x, y);
        Moved?.Invoke(_position);
    }

    private void OnWindowFocusChanged(WindowHandle* window, bool focused)
    {
        FocusChanged?.Invoke(focused);
    }

    private void OnWindowClosing(WindowHandle* window)
    {
        Closing?.Invoke();
    }

    private void OnFilesDropped(WindowHandle* window, int count, nint paths)
    {
        var files = new string[count];
        var stringPointers = (byte**)paths;

        for (int i = 0; i < count; i++)
        {
            files[i] = Marshal.PtrToStringUTF8((IntPtr)stringPointers[i]) ?? string.Empty;
        }

        FilesDropped?.Invoke(files);
    }

    public void Dispose()
    {
        if (_handle != null)
        {
            _glfw.DestroyWindow(_handle);
            _handle = null;
        }

        _glfw.Terminate();
    }
}