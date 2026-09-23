using System.Numerics;
using Hexa.NET.ImGui;
using Real.Core.Input;
using Real.Windowing;

namespace Real.ImGui;

public sealed class ImGuiInputHandler : IDisposable
{
    private readonly IWindow _window;

    public ImGuiInputHandler(IWindow window)
    {
        _window = window;

        _window.MouseMoved += OnMouseMoved;
        _window.MouseButtonChanged += OnMouseButtonChanged;
        _window.MouseScrolled += OnMouseScrolled;
        _window.KeyChanged += OnKeyChanged;
        _window.CharacterInput += OnCharacterInput;
        _window.FocusChanged += OnFocusChanged;
    }

    private void OnMouseMoved(Vector2 pos)
    {
        Hexa.NET.ImGui.ImGui.GetIO().AddMousePosEvent(pos.X, pos.Y);
    }

    private void OnMouseButtonChanged(MouseButton button, bool pressed)
    {
        var io = Hexa.NET.ImGui.ImGui.GetIO();
        int imguiButton = button switch
        {
            MouseButton.Left => 0,
            MouseButton.Right => 1,
            MouseButton.Middle => 2,
            MouseButton.Button4 => 3,
            MouseButton.Button5 => 4,
            MouseButton.Button6 => 5,
            MouseButton.Button7 => 6,
            MouseButton.Button8 => 7,
            _ => -1
        };

        if (imguiButton >= 0)
        {
            io.AddMouseButtonEvent(imguiButton, pressed);
        }
    }

    private void OnMouseScrolled(Vector2 delta)
    {
        Hexa.NET.ImGui.ImGui.GetIO().AddMouseWheelEvent(delta.X, delta.Y);
    }

    private void OnKeyChanged(KeyCode key, bool pressed)
    {
        var io = Hexa.NET.ImGui.ImGui.GetIO();

        // Модификаторы ImGui хочет получать отдельными "виртуальными" клавишами
        // ModCtrl/ModShift/ModAlt/ModSuper - шлём их дополнительно к обычной клавише.
        switch (key)
        {
            case KeyCode.LeftControl or KeyCode.RightControl:
                io.AddKeyEvent(ImGuiKey.ModCtrl, pressed);
                break;
            case KeyCode.LeftShift or KeyCode.RightShift:
                io.AddKeyEvent(ImGuiKey.ModShift, pressed);
                break;
            case KeyCode.LeftAlt or KeyCode.RightAlt:
                io.AddKeyEvent(ImGuiKey.ModAlt, pressed);
                break;
            case KeyCode.LeftSuper or KeyCode.RightSuper:
                io.AddKeyEvent(ImGuiKey.ModSuper, pressed);
                break;
        }

        var imguiKey = MapKey(key);
        if (imguiKey != ImGuiKey.None)
        {
            io.AddKeyEvent(imguiKey, pressed);
        }
    }

    private void OnCharacterInput(char c)
    {
        Hexa.NET.ImGui.ImGui.GetIO().AddInputCharacter(c);
    }

    private void OnFocusChanged(bool focused)
    {
        Hexa.NET.ImGui.ImGui.GetIO().AddFocusEvent(focused);
    }

    private static ImGuiKey MapKey(KeyCode key) => key switch
    {
        KeyCode.A => ImGuiKey.A, KeyCode.B => ImGuiKey.B, KeyCode.C => ImGuiKey.C,
        KeyCode.D => ImGuiKey.D, KeyCode.E => ImGuiKey.E, KeyCode.F => ImGuiKey.F,
        KeyCode.G => ImGuiKey.G, KeyCode.H => ImGuiKey.H, KeyCode.I => ImGuiKey.I,
        KeyCode.J => ImGuiKey.J, KeyCode.K => ImGuiKey.K, KeyCode.L => ImGuiKey.L,
        KeyCode.M => ImGuiKey.M, KeyCode.N => ImGuiKey.N, KeyCode.O => ImGuiKey.O,
        KeyCode.P => ImGuiKey.P, KeyCode.Q => ImGuiKey.Q, KeyCode.R => ImGuiKey.R,
        KeyCode.S => ImGuiKey.S, KeyCode.T => ImGuiKey.T, KeyCode.U => ImGuiKey.U,
        KeyCode.V => ImGuiKey.V, KeyCode.W => ImGuiKey.W, KeyCode.X => ImGuiKey.X,
        KeyCode.Y => ImGuiKey.Y, KeyCode.Z => ImGuiKey.Z,

        KeyCode.D0 => ImGuiKey.Key0, KeyCode.D1 => ImGuiKey.Key1, KeyCode.D2 => ImGuiKey.Key2,
        KeyCode.D3 => ImGuiKey.Key3, KeyCode.D4 => ImGuiKey.Key4, KeyCode.D5 => ImGuiKey.Key5,
        KeyCode.D6 => ImGuiKey.Key6, KeyCode.D7 => ImGuiKey.Key7, KeyCode.D8 => ImGuiKey.Key8,
        KeyCode.D9 => ImGuiKey.Key9,

        KeyCode.F1 => ImGuiKey.F1, KeyCode.F2 => ImGuiKey.F2, KeyCode.F3 => ImGuiKey.F3,
        KeyCode.F4 => ImGuiKey.F4, KeyCode.F5 => ImGuiKey.F5, KeyCode.F6 => ImGuiKey.F6,
        KeyCode.F7 => ImGuiKey.F7, KeyCode.F8 => ImGuiKey.F8, KeyCode.F9 => ImGuiKey.F9,
        KeyCode.F10 => ImGuiKey.F10, KeyCode.F11 => ImGuiKey.F11, KeyCode.F12 => ImGuiKey.F12,

        KeyCode.Space => ImGuiKey.Space,
        KeyCode.Enter => ImGuiKey.Enter,
        KeyCode.Escape => ImGuiKey.Escape,
        KeyCode.Backspace => ImGuiKey.Backspace,
        KeyCode.Tab => ImGuiKey.Tab,
        KeyCode.Left => ImGuiKey.LeftArrow,
        KeyCode.Right => ImGuiKey.RightArrow,
        KeyCode.Up => ImGuiKey.UpArrow,
        KeyCode.Down => ImGuiKey.DownArrow,
        KeyCode.Insert => ImGuiKey.Insert,
        KeyCode.Delete => ImGuiKey.Delete,
        KeyCode.Home => ImGuiKey.Home,
        KeyCode.End => ImGuiKey.End,
        KeyCode.PageUp => ImGuiKey.PageUp,
        KeyCode.PageDown => ImGuiKey.PageDown,
        KeyCode.CapsLock => ImGuiKey.CapsLock,
        KeyCode.ScrollLock => ImGuiKey.ScrollLock,
        KeyCode.NumLock => ImGuiKey.NumLock,
        KeyCode.PrintScreen => ImGuiKey.PrintScreen,
        KeyCode.Pause => ImGuiKey.Pause,
        KeyCode.Menu => ImGuiKey.Menu,

        KeyCode.Apostrophe => ImGuiKey.Apostrophe,
        KeyCode.Comma => ImGuiKey.Comma,
        KeyCode.Minus => ImGuiKey.Minus,
        KeyCode.Period => ImGuiKey.Period,
        KeyCode.Slash => ImGuiKey.Slash,
        KeyCode.Semicolon => ImGuiKey.Semicolon,
        KeyCode.Equal => ImGuiKey.Equal,
        KeyCode.LeftBracket => ImGuiKey.LeftBracket,
        KeyCode.Backslash => ImGuiKey.Backslash,
        KeyCode.RightBracket => ImGuiKey.RightBracket,
        KeyCode.GraveAccent => ImGuiKey.GraveAccent,

        KeyCode.LeftShift => ImGuiKey.LeftShift,
        KeyCode.RightShift => ImGuiKey.RightShift,
        KeyCode.LeftControl => ImGuiKey.LeftCtrl,
        KeyCode.RightControl => ImGuiKey.RightCtrl,
        KeyCode.LeftAlt => ImGuiKey.LeftAlt,
        KeyCode.RightAlt => ImGuiKey.RightAlt,
        KeyCode.LeftSuper => ImGuiKey.LeftSuper,
        KeyCode.RightSuper => ImGuiKey.RightSuper,

        KeyCode.KeyPad0 => ImGuiKey.Keypad0, KeyCode.KeyPad1 => ImGuiKey.Keypad1,
        KeyCode.KeyPad2 => ImGuiKey.Keypad2, KeyCode.KeyPad3 => ImGuiKey.Keypad3,
        KeyCode.KeyPad4 => ImGuiKey.Keypad4, KeyCode.KeyPad5 => ImGuiKey.Keypad5,
        KeyCode.KeyPad6 => ImGuiKey.Keypad6, KeyCode.KeyPad7 => ImGuiKey.Keypad7,
        KeyCode.KeyPad8 => ImGuiKey.Keypad8, KeyCode.KeyPad9 => ImGuiKey.Keypad9,
        KeyCode.KeyPadDecimal => ImGuiKey.KeypadDecimal,
        KeyCode.KeyPadDivide => ImGuiKey.KeypadDivide,
        KeyCode.KeyPadMultiply => ImGuiKey.KeypadMultiply,
        KeyCode.KeyPadSubtract => ImGuiKey.KeypadSubtract,
        KeyCode.KeyPadAdd => ImGuiKey.KeypadAdd,
        KeyCode.KeyPadEnter => ImGuiKey.KeypadEnter,
        KeyCode.KeyPadEqual => ImGuiKey.KeypadEqual,

        _ => ImGuiKey.None
    };

    public void Dispose()
    {
        _window.MouseMoved -= OnMouseMoved;
        _window.MouseButtonChanged -= OnMouseButtonChanged;
        _window.MouseScrolled -= OnMouseScrolled;
        _window.KeyChanged -= OnKeyChanged;
        _window.CharacterInput -= OnCharacterInput;
        _window.FocusChanged -= OnFocusChanged;
    }
}
