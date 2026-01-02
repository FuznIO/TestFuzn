namespace Fuzn.TestFuzn;

/// <summary>
/// Interface for executing code after a test suite completes.
/// Implement this interface in your startup class to run cleanup logic after all tests execute.
/// </summary>
public interface IAfterSuite
{
    /// <summary>
    /// Executes cleanup logic after the test suite completes.
    /// </summary>
    /// <param name="context">The context providing access to execution information and logging.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task AfterSuite(Context context);
}