namespace Fuzn.TestFuzn.Tests.IocContainer;

internal sealed class TransientMarker
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}
