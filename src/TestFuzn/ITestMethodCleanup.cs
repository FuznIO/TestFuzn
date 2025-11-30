namespace Fuzn.TestFuzn;

public interface ITestMethodCleanup
{
    Task CleanupTestMethod(Context context);
}
