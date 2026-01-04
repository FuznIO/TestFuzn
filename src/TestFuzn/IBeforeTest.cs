namespace Fuzn.TestFuzn;

/// <summary>
/// Interface for executing code before each test runs.
/// Implement this interface in your test class to run setup logic before each individual test.
/// </summary>
public interface IBeforeTest
{
    /// <summary>
    /// Executes setup logic before each test runs.
    /// </summary>
    /// <param name="context">The context providing access to execution information and logging.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BeforeTest(Context context);
}
