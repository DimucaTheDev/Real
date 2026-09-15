namespace Real.Windowing;

public readonly struct WindowPosition(int x, int y)
{
    public int X { get; } = x;
    public int Y { get; } = y;
    
    public static readonly WindowPosition Zero = new(0, 0);
}