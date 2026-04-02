using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn;

/// <summary>
/// Base context provided to each test iteration, exposing shared data, input data, comments, and attachments.
/// </summary>
public abstract class IterationContext : Context
{
    /// <summary>
    /// Gets information about the current scenario.
    /// </summary>
    public ScenarioInfo ScenarioInfo => new(IterationState.Scenario);

    /// <summary>
    /// Retrieves the iteration input data cast to the specified type.
    /// </summary>
    /// <typeparam name="T">The expected type of the input data.</typeparam>
    /// <returns>The input data cast to <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidCastException">Thrown when the input data cannot be cast to <typeparamref name="T"/>.</exception>
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

    /// <summary>
    /// Retrieves a shared data value by key, cast to the specified type.
    /// </summary>
    /// <typeparam name="T">The expected type of the shared data value.</typeparam>
    /// <param name="key">The key identifying the shared data entry.</param>
    /// <returns>The shared data value cast to <typeparamref name="T"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when <paramref name="key"/> is not found in shared data.</exception>
    public T GetSharedData<T>(string key)
    {
        if (IterationState.SharedData.TryGetValue(key, out var value))
        {
            return (T) value;
        }
        throw new KeyNotFoundException($"Key '{key}' not found in StepContext.Data.");
    }

    /// <summary>
    /// Stores a value in shared data that can be accessed across steps within the same iteration.
    /// </summary>
    /// <param name="key">The key to associate with the value.</param>
    /// <param name="value">The value to store.</param>
    public void SetSharedData(string key, object value)
    {
        IterationState.SharedData[key] = value;
    }

    /// <summary>
    /// Adds a comment to the current step. For load tests, the comment is logged instead.
    /// </summary>
    /// <param name="message">The comment text.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null or whitespace.</exception>
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

    /// <summary>
    /// Attaches a text file to the current step.
    /// </summary>
    /// <param name="fileName">The desired file name for the attachment.</param>
    /// <param name="content">The text content to write.</param>
    public async Task Attach(string fileName, string content)
    {
        var fileSystem = IterationState.ServiceProvider.GetRequiredService<IFileSystem>();

        await Attach(
            fileName,
            fileSystem,
            async (path) => await fileSystem.WriteAllTextAsync(path, content)
        );
    }

    /// <summary>
    /// Attaches a binary file to the current step.
    /// </summary>
    /// <param name="fileName">The desired file name for the attachment.</param>
    /// <param name="content">The byte array content to write.</param>
    public async Task Attach(string fileName, byte[] content)
    {
        var fileSystem = IterationState.ServiceProvider.GetRequiredService<IFileSystem>();
        await Attach(
            fileName,
            fileSystem,
            async (path) => await fileSystem.WriteAllBytesAsync(path, content)
        );
    }

    /// <summary>
    /// Attaches a stream as a file to the current step. If the stream is seekable, it is reset to the beginning before writing.
    /// </summary>
    /// <param name="fileName">The desired file name for the attachment.</param>
    /// <param name="content">The stream content to write.</param>
    public async Task Attach(string fileName, Stream content)
    {
        var fileSystem = IterationState.ServiceProvider.GetRequiredService<IFileSystem>();

        await Attach(
            fileName,
            fileSystem,
            async (path) =>
            {
                if (content.CanSeek)
                    content.Seek(0, SeekOrigin.Begin);

                await using var fileStream = fileSystem.OpenFileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                await content.CopyToAsync(fileStream);
            }
        );
    }

    /// <summary>
    /// Registers a cleanup action to execute at the end of the current iteration, after all steps complete.
    /// Multiple cleanup actions are executed in reverse registration order (LIFO).
    /// </summary>
    /// <param name="action">The asynchronous cleanup action to execute.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    public void Cleanup(Func<Task> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action), "Cleanup action cannot be null.");

        if (IterationState.CleanupActions == null)
            IterationState.CleanupActions = new();

        IterationState.CleanupActions.Add(action);
    }

    /// <summary>
    /// Registers a cleanup action to execute at the end of the current iteration, after all steps complete.
    /// Multiple cleanup actions are executed in reverse registration order (LIFO).
    /// </summary>
    /// <param name="action">The synchronous cleanup action to execute.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    public void Cleanup(Action action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action), "Cleanup action cannot be null.");

        if (IterationState.CleanupActions == null)
            IterationState.CleanupActions = new();

        IterationState.CleanupActions.Add(() =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    private async Task Attach(string fileName, IFileSystem fileSystem, Func<string, Task> writeFileAsync)
    {
        if (StepInfo.Attachments == null)
            StepInfo.Attachments = new();

        var directory = Path.Combine(Info.TestSession.TestsOutputDirectory, "Data", "Attachments");
        if (!fileSystem.DirectoryExists(directory))
            fileSystem.CreateDirectory(directory);

        var attachment = new Attachment();
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        attachment.Name = $"{Info.CorrelationId}_{fileNameWithoutExtension}_{Guid.NewGuid().ToString("N")[..6]}{extension}";
        attachment.Path = Path.Combine(directory, attachment.Name);
        StepInfo.Attachments.Add(attachment);

        await writeFileAsync(attachment.Path);
    }
}
/// <summary>
/// Typed iteration context that provides access to a strongly-typed model and step execution.
/// </summary>
/// <typeparam name="TModel">The type of the iteration model.</typeparam>
public sealed class IterationContext<TModel> : IterationContext
    where TModel : new()
{
    /// <summary>
    /// Gets the strongly-typed model for the current iteration.
    /// </summary>
    public TModel Model => (TModel) IterationState.Model;

    /// <summary>
    /// Defines and executes an asynchronous sub-step with an explicit identifier.
    /// </summary>
    /// <param name="name">The display name of the step.</param>
    /// <param name="id">A unique identifier for the step.</param>
    /// <param name="action">The asynchronous action to execute.</param>
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

    /// <summary>
    /// Defines and executes an asynchronous sub-step.
    /// </summary>
    /// <param name="name">The display name of the step.</param>
    /// <param name="action">The asynchronous action to execute.</param>
    public async Task Step(string name, Func<IterationContext<TModel>, Task> action)
    {
        var step = new Step();
        step.ContextType = typeof(IterationContext<TModel>);
        step.Name = name;
        step.ParentName = StepInfo.Name;
        step.Action = ctx => action((IterationContext<TModel>) ctx);
        await IterationState.ExecuteStepHandler.ExecuteStep(step);
    }

    /// <summary>
    /// Defines and executes a synchronous sub-step with an explicit identifier.
    /// </summary>
    /// <param name="name">The display name of the step.</param>
    /// <param name="id">A unique identifier for the step.</param>
    /// <param name="action">The synchronous action to execute.</param>
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

    /// <summary>
    /// Defines and executes a synchronous sub-step.
    /// </summary>
    /// <param name="name">The display name of the step.</param>
    /// <param name="action">The synchronous action to execute.</param>
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
