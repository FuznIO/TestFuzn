namespace Fuzn.TestFuzn.Internals.Cluster;

// Carries a failure that happened in another process (controller or load worker); only the message crosses over.
internal class RemoteRunException : Exception
{
    public RemoteRunException(string message)
        : base(message)
    {
    }
}
