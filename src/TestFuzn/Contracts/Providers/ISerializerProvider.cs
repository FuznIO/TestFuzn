namespace FuznLabs.TestFuzn.Contracts.Providers;

public interface ISerializerProvider
{
    string Serialize<T>(T obj) where T : class;
    T Deserialize<T>(string json) where T : class;
    bool IsSerializerSpecific<T>() where T : class;
    int Priority { get; }
}
