namespace Fuzn.TestFuzn.ConsoleOutput;

internal class AdvancedTable
{
    public int ColumnCount { get; set; }
    public List<int> ColumnWidths { get; set; } = new();
    public List<AdvancedTableRow> Rows { get; set; } = new();
}

internal class AdvancedTableRow
{
    public List<IAdvancedTableCell> Cells { get; set; } = new();
    public bool IsDivider { get; set; } = false;
}

internal interface IAdvancedTableCell
{
    int ColSpan { get; }
    int GetContentWidth();
    string Render(int width);
}

internal class AdvancedTableCell : IAdvancedTableCell
{
    public string Text { get; set; }
    public int ColSpan { get; set; } = 1;
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;
    public AdvancedTableCell(string text, int colSpan = 1, TextAlignment alignment = TextAlignment.Left)
    {
        Text = text;
        ColSpan = colSpan;
        Alignment = alignment;
    }
    public int GetContentWidth()
    {
        return MarkupHelper.StripMarkup(Text).Length;
    }
    public string Render(int width)
    {
        var text = MarkupHelper.StripMarkup(Text);
        if (text.Length > width) text = text.Substring(0, width);
        var pad = width - text.Length;
        switch (Alignment)
        {
            case TextAlignment.Left:
                return " " + text + new string(' ', pad - 1);
            case TextAlignment.Center:
                int left = pad / 2, right = pad - left;
                return new string(' ', left) + text + new string(' ', right);
            case TextAlignment.Right:
                return new string(' ', pad - 1) + text + " ";
            default:
                return " " + text + new string(' ', pad - 1);
        }
    }
}

internal class KeyValueCell : IAdvancedTableCell
{
    public string Key { get; set; }
    public string Value { get; set; }
    public int ColSpan { get; set; } = 1;
    public KeyValueCell(string key, string value, int colSpan = 1)
    {
        Key = key;
        Value = value;
        ColSpan = colSpan;
    }
    // space + key + space + value + space
    public int GetContentWidth() => (Key?.Length ?? 0) + (Value?.Length ?? 0) + 2;
    public string Render(int width)
    {
        var key = MarkupHelper.StripMarkup(Key);
        var value = MarkupHelper.StripMarkup(Value);
        // 1 space before, key left, value right, 1 space between, 1 space after value
        var total = (key?.Length ?? 0) + (value?.Length ?? 0) + 2;
        var pad = width - total;
        if (pad < 0) pad = 0;
        return $" {key}{new string(' ', pad)}{value} ";
    }
}

internal enum TextAlignment
{
    Left,
    Center,
    Right
}
