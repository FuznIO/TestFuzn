namespace TestFusion;

public interface IStartup
{
    public TestFusionConfiguration Configuration();
    public Task InitGlobal(Context context);
    public Task CleanupGlobal(Context context);
}
