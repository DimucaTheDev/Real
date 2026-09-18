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
        var io = Hexa.NET.ImGui.ImGui.GetIO();
        io.AddMousePosEvent(pos.X, pos.Y);
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
            _ => -1
        };

        if (imguiButton >= 0)
        {
            io.AddMouseButtonEvent(imguiButton, pressed);
        }
    }

    private void OnMouseScrolled(Vector2 delta)
    {
        var io = Hexa.NET.ImGui.ImGui.GetIO();
        io.AddMouseWheelEvent(delta.X, delta.Y);
    }

    private void OnKeyChanged(KeyCode key, bool pressed)
    {
        var io = Hexa.NET.ImGui.ImGui.GetIO();

        // Модификаторы — если у вас в KeyCode есть Shift/Ctrl/Alt/Super, лучше
        // трекать их состояние отдельно и слать io.AddKeyEvent(ImGuiKey.ModCtrl, ...)
        var imguiKey = MapKey(key);
        if (imguiKey != ImGuiKey.None)
        {
            io.AddKeyEvent(imguiKey, pressed);
        }
    }

    private void OnCharacterInput(char c)
    {
        var io = Hexa.NET.ImGui.ImGui.GetIO();
        io.AddInputCharacter(c);
    }

    private void OnFocusChanged(bool focused)
    {
        Hexa.NET.ImGui.ImGui.GetIO().AddFocusEvent(focused);
    }

    // ВАЖНО: имена элементов KeyCode здесь — предположение (стандартная
    // раскладка a-la GLFW). Подставьте реальные имена вашего enum.
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

        KeyCode.Space => ImGuiKey.Space,
        KeyCode.Enter => ImGuiKey.Enter,
        KeyCode.Escape => ImGuiKey.Escape,
        KeyCode.Backspace => ImGuiKey.Backspace,
        KeyCode.Tab => ImGuiKey.Tab,
        KeyCode.Left => ImGuiKey.LeftArrow,
        KeyCode.Right => ImGuiKey.RightArrow,
        KeyCode.Up => ImGuiKey.UpArrow,
        KeyCode.Down => ImGuiKey.DownArrow,
        KeyCode.Delete => ImGuiKey.Delete,
        KeyCode.Home => ImGuiKey.Home,
        KeyCode.End => ImGuiKey.End,
        KeyCode.LeftShift => ImGuiKey.LeftShift,
        KeyCode.RightShift => ImGuiKey.RightShift,
        KeyCode.LeftControl => ImGuiKey.LeftCtrl,
        KeyCode.RightControl => ImGuiKey.RightCtrl,
        KeyCode.LeftAlt => ImGuiKey.LeftAlt,
        KeyCode.RightAlt => ImGuiKey.RightAlt,

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