namespace Fuzn.TestFuzn;

/// <summary>
/// Interface for executing code after each test completes.
/// Implement this interface in your test class to run cleanup logic after each individual test.
/// </summary>
public interface IAfterTest
{
    /// <summary>
    /// Executes cleanup logic after each test completes.
    /// </summary>
    /// <param name="context">The context providing access to execution information and logging.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AfterTest(Context context);
}
