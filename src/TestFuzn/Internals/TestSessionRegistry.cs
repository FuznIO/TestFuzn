using System.Collections.Concurrent;

namespace Fuzn.TestFuzn.Internals;

/// <summary>
/// A static, thread-safe registry of <see cref="TestSession"/> instances keyed by a string identifier.
/// </summary>
internal static class TestSessionRegistry
{
    internal static ConcurrentDictionary<string, TestSession> TestSessions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a <see cref="TestSession"/> using its <see cref="TestSession.Id"/>. Throws if the identifier is already registered.
    /// </summary>
    internal static void Add(TestSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(session.Id))
            throw new InvalidOperationException("TestSession.Id must be set before registering.");

        if (!TestSessions.TryAdd(session.Id, session))
            throw new InvalidOperationException($"A TestSession with id '{session.Id}' is already registered.");
    }

    /// <summary>
    /// Gets the <see cref="TestSession"/> registered with the specified identifier.
    /// </summary>
    internal static TestSession Get(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!TestSessions.TryGetValue(id, out var session))
            throw new InvalidOperationException($"No TestSession found with id '{id}'.");

        return session;
    }

    /// <summary>
    /// Removes and returns the <see cref="TestSession"/> registered with the specified identifier.
    /// </summary>
    internal static bool Remove(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return TestSessions.TryRemove(id, out _);
    }

    /// <summary>
    /// Removes all registered sessions. Intended for test teardown.
    /// </summary>
    internal static void Clear() => TestSessions.Clear();
}
