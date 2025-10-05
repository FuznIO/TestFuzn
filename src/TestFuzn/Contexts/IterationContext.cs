using Fuzn.TestFuzn.Contracts.Results.Feature;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn;

public abstract class IterationContext : Context
{
    public ScenarioInfo ScenarioInfo => new(IterationState.Scenario);

    public IterationContext()
    {
    }

    public T InputData<T>()
    {
        try
        {
            return (T) IterationState.InputData;
        }
        catch (InvalidCastException)
        {
            throw new InvalidCastException($"Input data is not of type {typeof(T).Name}");
        }
    }

    public T GetSharedData<T>(string key)
    {
        if (IterationState.SharedData.TryGetValue(key, out var value))
        {
            return (T) value;
        }
        throw new KeyNotFoundException($"Key '{key}' not found in StepContext.Data.");
    }

    public void SetSharedData(string key, object value)
    {
        IterationState.SharedData[key] = value;
    }


    public void Comment(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentNullException(nameof(message), "Comment message cannot be null or empty.");

        var created = DateTime.UtcNow;

        if (IterationState.Scenario.TestType == TestType.Load)
        {
            Logger.LogInformation($"Comment {created}: {message}");
            return;
        }

        if (StepInfo.Comments == null)
            StepInfo.Comments = new List<Comment>();
        var comment = new Comment
        {
            Text = message,
            Created = DateTime.UtcNow
        };
        StepInfo.Comments.Add(comment);
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
        if (StepInfo.Attachments == null)
            StepInfo.Attachments = new();

        var directory = Path.Combine(GlobalState.TestsOutputDirectory, "Attachments");
         if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var attachment = new Attachment();
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        attachment.Name = $"{Info.CorrelationId}_{fileNameWithoutExtension}_{Guid.NewGuid().ToString("N")[..6]}{extension}";
        attachment.Path = Path.Combine(directory, attachment.Name);
        StepInfo.Attachments.Add(attachment);

        await writeFileAsync(attachment.Path);
    }
}
public sealed class IterationContext<TModel> : IterationContext
    where TModel : new()
{
    public TModel Model => (TModel) IterationState.Model;

    public async Task Step(string name, string id, Func<IterationContext<TModel>, Task> action)
    {
        var step = new Step();
        step.ContextType = typeof(IterationContext<TModel>);
        step.Name = name;
        step.Id = id;
        step.ParentName = StepInfo.Name;
        step.Action = ctx => action((IterationContext<TModel>) ctx);
        await IterationState.ExecuteStepHandler.ExecuteStep(step);
    }

    public async Task Step(string name, Func<IterationContext<TModel>, Task> action)
    {
        var step = new Step();
        step.ContextType = typeof(IterationContext<TModel>);
        step.Name = name;
        step.ParentName = StepInfo.Name;
        step.Action = ctx => action((IterationContext<TModel>) ctx);
        await IterationState.ExecuteStepHandler.ExecuteStep(step);
    }

    public void Step(string name, string id, Action<IterationContext<TModel>> action)
    {
        var step = new Step();
        step.ContextType = typeof(IterationContext<TModel>);
        step.Name = name;
        step.Id = id;
        step.ParentName = StepInfo.Name;
        step.Action = (ctx) =>
            {
                action((IterationContext<TModel>) ctx);
                return Task.CompletedTask;
            };
        IterationState.ExecuteStepHandler.ExecuteStep(step).GetAwaiter().GetResult();
    }

    public void Step(string name, Action<IterationContext<TModel>> action)
    {
        var step = new Step();
        step.ContextType = typeof(IterationContext<TModel>);
        step.Name = name;
        step.ParentName = StepInfo.Name;
        step.Action = (ctx) =>
            {
                action((IterationContext<TModel>) ctx);
                return Task.CompletedTask;
            };
        IterationState.ExecuteStepHandler.ExecuteStep(step).GetAwaiter().GetResult();
    }
}
