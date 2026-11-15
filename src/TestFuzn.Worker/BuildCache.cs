using Fuzn.TestFuzn.Internals.Cluster.Protocol;

namespace Fuzn.TestFuzn.Worker;

// Builds are downloaded from Cockpit by hash once and unpacked per job; a controller and a load child of
// the same run on one machine share the download. Every unpacked build is hashed again, and one that does
// not match its name is refused.
internal class BuildCache
{
    private static readonly TimeSpan KeepFor = TimeSpan.FromDays(1);

    private readonly CockpitConnection _cockpit;
    private readonly string _directory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public BuildCache(CockpitConnection cockpit, string workDirectory)
    {
        _cockpit = cockpit;
        _directory = Path.Combine(workDirectory, "builds");
    }

    public async Task Unpack(string buildHash, string packageDirectory, CancellationToken cancellationToken)
    {
        if (!BuildHash.IsHash(buildHash))
            throw new InvalidOperationException($"'{buildHash}' is not a build hash.");

        var zipPath = await Download(buildHash, cancellationToken);

        if (Directory.Exists(packageDirectory))
            Directory.Delete(packageDirectory, recursive: true);

        await using (var zip = File.OpenRead(zipPath))
            SafeZip.Extract(zip, packageDirectory, setExecuteBit: true);

        var actual = BuildHash.Of(packageDirectory, BuildHash.AllFiles(packageDirectory));
        if (actual != buildHash)
        {
            Directory.Delete(packageDirectory, recursive: true);
            File.Delete(zipPath);
            throw new InvalidOperationException($"The build downloaded as {buildHash} has the contents of {actual}. The worker does not run it.");
        }
    }

    private async Task<string> Download(string buildHash, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_directory);
            var zipPath = Path.Combine(_directory, buildHash + ".zip");
            if (File.Exists(zipPath))
            {
                File.SetLastWriteTimeUtc(zipPath, DateTime.UtcNow);
                return zipPath;
            }

            var partialPath = zipPath + ".part";
            bool found;
            await using (var file = File.Create(partialPath))
                found = await _cockpit.Download(CockpitPaths.WorkerBuild(buildHash), file, cancellationToken);

            if (!found)
            {
                File.Delete(partialPath);
                throw new InvalidOperationException($"Build {buildHash} is not in Cockpit (it may have expired). Start the run again to upload it.");
            }

            File.Move(partialPath, zipPath, overwrite: true);
            RemoveOldBuilds();
            return zipPath;
        }
        finally
        {
            _lock.Release();
        }
    }

    private void RemoveOldBuilds()
    {
        foreach (var file in Directory.GetFiles(_directory, "*.zip"))
        {
            try
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > KeepFor)
                    File.Delete(file);
            }
            catch (IOException)
            {
            }
        }
    }
}
