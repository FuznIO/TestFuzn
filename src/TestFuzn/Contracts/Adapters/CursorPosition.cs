namespace Fuzn.TestFuzn.Contracts.Adapters;

internal class CursorPosition
{
    public int Left { get; set; }
    public int Top { get; set; }

    public CursorPosition(int left, int top)
    {
        Left = left;
        Top = top;
    }
}
