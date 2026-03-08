namespace Fuzn.TestFuzn.Tests.IocContainer;

internal sealed class ScopedMarker
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}
