namespace Fuzn.TestFuzn;

/// <summary>
/// Interface for executing code before a test suite runs.
/// Implement this interface in your startup class to run setup logic before any tests execute.
/// </summary>
public interface IBeforeSuite
{
    /// <summary>
    /// Executes setup logic before the test suite runs.
    /// </summary>
    /// <param name="context">The context providing access to execution information and logging.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task BeforeSuite(Context context);
}
