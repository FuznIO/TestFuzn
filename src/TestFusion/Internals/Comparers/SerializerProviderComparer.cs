using TestFusion.Contracts.Providers;

namespace TestFusion.Internals.Comparers;

internal class SerializerProviderComparer : IEqualityComparer<ISerializerProvider>
{
    public bool Equals(ISerializerProvider? x, ISerializerProvider? y)
    {
        return x?.GetType().FullName == y?.GetType().FullName;
    }

    public int GetHashCode(ISerializerProvider obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        return StringComparer.Ordinal.GetHashCode(obj.GetType().FullName ?? string.Empty);
    }
}
