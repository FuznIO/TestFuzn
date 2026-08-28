namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// A column definition for <see cref="TableWidget"/>: a markup header, the horizontal
/// alignment applied to the header and every cell in the column, and an optional cap on the
/// column's auto-sized width. The header — like the column's cells — is markup text, never
/// pre-rendered widget output (see <see cref="RenderedLine"/>).
/// </summary>
internal readonly struct TableColumn
{
    /// <summary>Header markup shown in the table's first row.</summary>
    public string Header { get; }

    /// <summary>Alignment of the header and cells; left unless set.</summary>
    public TextAlignment Alignment { get; init; }

    /// <summary>Maximum auto-sized width, or null for content-sized with no cap.</summary>
    public int? MaxWidth { get; init; }

    public TableColumn(string header)
    {
        if (header == null)
            throw new ArgumentNullException(nameof(header), "Header cannot be null.");

        Header = header;
    }
}
