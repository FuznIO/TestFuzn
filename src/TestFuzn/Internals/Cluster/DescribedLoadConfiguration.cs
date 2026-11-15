namespace Fuzn.TestFuzn.Internals.Cluster;

// Stands in for a simulation that runs on another machine: the controller and the remote client only
// need its description for the live view and the report.
internal class DescribedLoadConfiguration : ILoadConfiguration
{
    private readonly string _description;

    public DescribedLoadConfiguration(string description, bool isWarmup)
    {
        _description = description;
        IsWarmup = isWarmup;
    }

    public bool IsWarmup { get; set; }

    public string GetDescription() => _description;

    public ILoadConfiguration Split(int workerIndex, int workerCount, int seed)
    {
        throw new InvalidOperationException("A described simulation runs on another machine and cannot be split.");
    }
}
