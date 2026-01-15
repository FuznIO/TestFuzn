using Fuzn.TestFuzn.Contracts.Results.Standard;

namespace Fuzn.TestFuzn;

public class StepInfo
{
    private readonly IterationContext? _context;
    public string? Name { get; internal set; }
    public string? Id { get; internal set; }
    internal string? ParentName { get; set; }
    internal List<Comment>? Comments { get; set; }
    internal List<Attachment>? Attachments { get; set; }

    public StepInfo(IterationContext? context, string name, string? id, string? parentName)
    {
        _context = context;
        Name = name;
        Id = id;
        ParentName = parentName;
    }
}
