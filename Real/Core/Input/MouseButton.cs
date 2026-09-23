namespace Real.Core.Input;

/// <summary>
/// Кнопки мыши. Значения совпадают с GLFW_MOUSE_BUTTON_* (0..7), поэтому
/// GLFW-бэкенд окна может кастовать (MouseButton)(int)glfwButton напрямую.
/// Left/Right/Middle - это те же значения 0/1/2, что и Button1/2/3
/// (стандартный для GLFW-биндингов приём "именованный алиас + Button-N").
/// </summary>
public enum MouseButton
{
    Left = 0,
    Right = 1,
    Middle = 2,
    Button4 = 3,
    Button5 = 4,
    Button6 = 5,
    Button7 = 6,
    Button8 = 7
}
