namespace Fuzn.TestFuzn;

public interface IStartup
{
    public TestFuznConfiguration Configuration();
    public Task InitGlobal(Context context);
    public Task CleanupGlobal(Context context);
}
