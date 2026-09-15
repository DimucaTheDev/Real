namespace Real.Windowing;

public readonly struct WindowSize(int width, int height)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    
    public static readonly WindowSize Zero = new(0, 0);
    public static readonly WindowSize Hd = new(1080, 720);
    public static readonly WindowSize FullHd = new(1920, 1080);
}