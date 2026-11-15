namespace Fuzn.TestFuzn.Internals;

internal interface ILoadConfiguration
{
    public bool IsWarmup { get; set; }
    public string GetDescription();

    // This worker's part of the total load when the run is spread over several workers.
    public ILoadConfiguration Split(int workerIndex, int workerCount, int seed);
}
