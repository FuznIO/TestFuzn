namespace Fuzn.TestFuzn.Tests.IocContainer;

internal sealed class SingletonMarker
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}
