namespace Fuzn.TestFuzn.Internals;

/// <summary>
/// Default implementation that delegates to <see cref="System.IO"/> APIs.
/// </summary>
internal sealed class FileSystem : IFileSystem
{
    public void CreateDirectory(string path) =>
        Directory.CreateDirectory(path);

    public bool DirectoryExists(string path) =>
        Directory.Exists(path);

    public bool FileExists(string path) =>
        File.Exists(path);

    public Task WriteAllTextAsync(string path, string content) =>
        File.WriteAllTextAsync(path, content);

    public Task WriteAllBytesAsync(string path, byte[] content) =>
        File.WriteAllBytesAsync(path, content);

    public Task<string> ReadAllTextAsync(string path) =>
        File.ReadAllTextAsync(path);

    public Stream CreateFile(string path) =>
        File.Create(path);

    public FileStream OpenFileStream(string path, FileMode mode, FileAccess access, FileShare share) =>
        new FileStream(path, mode, access, share);

    public StreamReader OpenStreamReader(string path) =>
        new StreamReader(path);

    public string[] GetDirectories(string path) =>
        Directory.GetDirectories(path);

    public void DeleteDirectory(string path) =>
        Directory.Delete(path, recursive: true);

    public DateTime GetDirectoryCreationTimeUtc(string path) =>
        Directory.GetCreationTimeUtc(path);
}
