namespace Fuzn.TestFuzn;

/// <summary>
/// Interface for executing code once before any test in the class runs.
/// Implement this interface in your test class to run setup logic once per class.
/// </summary>
public interface IBeforeClass
{
    /// <summary>
    /// Executes setup logic once before any test in the class runs.
    /// </summary>
    /// <param name="context">The context providing access to execution information and logging.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BeforeClass(Context context);
}
