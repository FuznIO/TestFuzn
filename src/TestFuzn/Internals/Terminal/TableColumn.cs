namespace Fuzn.TestFuzn.Internals.Terminal;

internal readonly struct TableColumn
{
    public string Header { get; }

    public TextAlignment Alignment { get; init; }

    public int? MaxWidth { get; init; }

    public TableColumn(string header)
    {
        if (header == null)
            throw new ArgumentNullException(nameof(header), "Header cannot be null.");

        Header = header;
    }
}
