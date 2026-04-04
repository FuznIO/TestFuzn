namespace Fuzn.TestFuzn;

/// <summary>
/// Interface for executing code once after all tests in the class complete.
/// Implement this interface in your test class to run cleanup logic once per class.
/// </summary>
public interface IAfterClass
{
    /// <summary>
    /// Executes cleanup logic once after all tests in the class complete.
    /// </summary>
    /// <param name="context">The context providing access to execution information and logging.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AfterClass(Context context);
}
