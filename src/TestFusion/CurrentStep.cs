using TestFusion;
using TestFusion.Internals.State;
using TestFusion.Contracts.Results.Feature;

public class CurrentStep
{
    private readonly StepContext _context;
    public string? Name { get; internal set; }
    internal List<Attachment> Attachments { get; set; }

    public CurrentStep(StepContext context, string name)
    {
        _context = context;
        Name = name;
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
        attachment.Name = $"{_context.CorrelationId}_{fileNameWithoutExtension}_{Guid.NewGuid().ToString("N")[..6]}{extension}";
        attachment.Path = Path.Combine(directory, attachment.Name);
        Attachments.Add(attachment);

        await writeFileAsync(attachment.Path);
    }
}
