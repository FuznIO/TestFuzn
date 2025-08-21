namespace FuznLabs.TestFuzn.Contracts.Adapters;

public class CursorPosition
{
    public int Left { get; set; }
    public int Top { get; set; }

    public CursorPosition(int left, int top)
    {
        Left = left;
        Top = top;
    }
}
