namespace Fuzn.TestFuzn.Internals;

/// <summary>
/// Abstracts file system operations for testability and portability.
/// </summary>
internal interface IFileSystem
{
    /// <summary>
    /// Creates all directories and subdirectories in the specified path unless they already exist.
    /// </summary>
    void CreateDirectory(string path);

    /// <summary>
    /// Determines whether the given directory exists.
    /// </summary>
    bool DirectoryExists(string path);

    /// <summary>
    /// Determines whether the given file exists.
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    /// Asynchronously writes the specified string to a file, creating the file if it does not already exist or overwriting it.
    /// </summary>
    Task WriteAllTextAsync(string path, string content);

    /// <summary>
    /// Asynchronously writes the specified byte array to a file, creating the file if it does not already exist or overwriting it.
    /// </summary>
    Task WriteAllBytesAsync(string path, byte[] content);

    /// <summary>
    /// Asynchronously reads all text from a file.
    /// </summary>
    Task<string> ReadAllTextAsync(string path);

    /// <summary>
    /// Creates or overwrites a file at the specified path and returns a writable stream.
    /// </summary>
    Stream CreateFile(string path);

    /// <summary>
    /// Opens a <see cref="FileStream"/> with the specified mode, access, and share settings.
    /// </summary>
    FileStream OpenFileStream(string path, FileMode mode, FileAccess access, FileShare share);

    /// <summary>
    /// Opens a <see cref="StreamReader"/> for the file at the specified path.
    /// </summary>
    StreamReader OpenStreamReader(string path);
}
