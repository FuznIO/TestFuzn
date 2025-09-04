namespace Fuzn.TestFuzn;

public class BaseStartup : IStartup
{
    public virtual TestFusionConfiguration Configuration()
    {
        return null;
    }

    public virtual Task InitGlobal(Context context)
    {
        return Task.CompletedTask;
    }

    public virtual Task CleanupGlobal(Context context)
    {
        return Task.CompletedTask;
    }
}
