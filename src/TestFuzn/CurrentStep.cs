using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Contracts.Results.Feature;

namespace Fuzn.TestFuzn;

public class CurrentStep
{
    private readonly BaseStepContext _context;
    public string? Name { get; internal set; }
    public string? Id { get; set; }
    internal Dictionary<string, string> MetadataInternal { get; set; }
    internal string? ParentName { get; set; }
    internal List<StepComment> Comments { get; set; }
    internal List<Attachment> Attachments { get; set; }

    public CurrentStep(BaseStepContext context, string name, string? parentName = null)
    {
        _context = context;
        Name = name;
        ParentName = parentName;
    }

    public void Metadata(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key), "Metadata key cannot be null or empty.");
        if (MetadataInternal == null)
            MetadataInternal = new Dictionary<string, string>();
        if (MetadataInternal.ContainsKey(key))
            throw new ArgumentException($"Meta key '{key}' already exists in the step. Meta keys must be unique.", nameof(key));
        MetadataInternal.Add(key, value);
    }

    public void Comment(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentNullException(nameof(message), "Comment message cannot be null or empty.");
        if (Comments == null)
            Comments = new List<StepComment>();
        var comment = new StepComment
        {
            Message = message,
            Created = DateTime.UtcNow
        };
        Comments.Add(comment);
    }

    public async Task Attach(string fileName, string content)
    {
        await Attach(
            fileName,
            async (path) => await File.WriteAllTextAsync(path, content)
        );
    }

    public async Task Attach(string fileName, byte[] content)
    {
        await Attach(
            fileName,
            async (path) => await File.WriteAllBytesAsync(path, content)
        );
    }

    public async Task Attach(string fileName, Stream content)
    {
        await Attach(
            fileName,
            async (path) =>
            {
                if (content.CanSeek)
                    content.Seek(0, SeekOrigin.Begin);

                using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await content.CopyToAsync(fileStream);
                }
            }
        );
    }

    private async Task Attach(string fileName, Func<string, Task> writeFileAsync)
    {
        if (Attachments == null)
            Attachments = new();

        var directory = Path.Combine(GlobalState.TestsOutputDirectory, "Attachments");
         if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var attachment = new Attachment();
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        attachment.Name = $"{_context.Info.CorrelationId}_{fileNameWithoutExtension}_{Guid.NewGuid().ToString("N")[..6]}{extension}";
        attachment.Path = Path.Combine(directory, attachment.Name);
        Attachments.Add(attachment);

        await writeFileAsync(attachment.Path);
    }
}
