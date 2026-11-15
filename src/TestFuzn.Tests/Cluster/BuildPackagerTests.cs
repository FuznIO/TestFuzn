using System.IO.Compression;
using Fuzn.TestFuzn.Internals.Cluster.Remote;
using Fuzn.TestFuzn.Internals.Cluster.Protocol;

namespace Fuzn.TestFuzn.Tests.Cluster;

[TestClass]
[Tags("unit")]
public class BuildPackagerTests : Test
{
    [Test]
    public async Task Verify_only_the_workers_os_family_is_uploaded()
    {
        await Scenario()
            .Step("Native folders of other OS families are left out; unix goes to Linux and macOS", context =>
            {
                Assert.IsTrue(BuildPackager.IsIncluded("runtimes/linux-x64/native/libe_sqlite3.so", OsFamilies.Linux));
                Assert.IsTrue(BuildPackager.IsIncluded("runtimes/linux-arm64/native/libe_sqlite3.so", OsFamilies.Linux));
                Assert.IsFalse(BuildPackager.IsIncluded("runtimes/win-x64/native/e_sqlite3.dll", OsFamilies.Linux));
                Assert.IsFalse(BuildPackager.IsIncluded("runtimes/osx-arm64/native/libe_sqlite3.dylib", OsFamilies.Linux));
                Assert.IsTrue(BuildPackager.IsIncluded("runtimes/unix/lib/net8.0/System.Drawing.dll", OsFamilies.MacOs));
                Assert.IsFalse(BuildPackager.IsIncluded("runtimes/unix/lib/net8.0/System.Drawing.dll", OsFamilies.Windows));
                Assert.IsTrue(BuildPackager.IsIncluded("runtimes/browser/lib/x.dll", OsFamilies.Windows));
            })
            .Step("Playwright's node driver keeps only the workers' platform", context =>
            {
                Assert.IsTrue(BuildPackager.IsIncluded(".playwright/node/linux-x64/node", OsFamilies.Linux));
                Assert.IsFalse(BuildPackager.IsIncluded(".playwright/node/win32_x64/node.exe", OsFamilies.Linux));
                Assert.IsTrue(BuildPackager.IsIncluded(".playwright/node/darwin-arm64/node", OsFamilies.MacOs));
                Assert.IsTrue(BuildPackager.IsIncluded(".playwright/package/index.js", OsFamilies.Linux));
            })
            .Step("Results and log folders are never uploaded", context =>
            {
                Assert.IsFalse(BuildPackager.IsIncluded("TestFuznResults/MyTests/run/TestReport.html", OsFamilies.Linux));
                Assert.IsFalse(BuildPackager.IsIncluded("TestResults/x.trx", OsFamilies.Linux));
                Assert.IsTrue(BuildPackager.IsIncluded("MyTests.dll", OsFamilies.Linux));
            });
    }

    [Test]
    public async Task Verify_the_build_hash_depends_on_paths_and_contents_only()
    {
        await Scenario()
            .Step("The same files give the same hash; a changed file gives another", context =>
            {
                var directory = Directory.CreateTempSubdirectory("testfuzn-hash").FullName;
                try
                {
                    File.WriteAllText(Path.Combine(directory, "a.dll"), "one");
                    Directory.CreateDirectory(Path.Combine(directory, "sub"));
                    File.WriteAllText(Path.Combine(directory, "sub", "b.json"), "two");

                    var files = BuildPackager.Files(directory, OsFamilies.Linux);
                    CollectionAssert.AreEqual(new[] { "a.dll", "sub/b.json" }, files);

                    var first = BuildPackager.Hash(directory, files);
                    Assert.AreEqual(first, BuildPackager.Hash(directory, files));

                    File.WriteAllText(Path.Combine(directory, "sub", "b.json"), "changed");
                    Assert.AreNotEqual(first, BuildPackager.Hash(directory, files));
                }
                finally
                {
                    Directory.Delete(directory, recursive: true);
                }
            });
    }

    [Test]
    public async Task Verify_zip_entries_cannot_write_outside_the_target_folder()
    {
        await Scenario()
            .Step("A normal archive unpacks", context =>
            {
                var target = Directory.CreateTempSubdirectory("testfuzn-zip").FullName;
                try
                {
                    SafeZip.Extract(Zip("sub/file.txt"), target, setExecuteBit: false);
                    Assert.IsTrue(File.Exists(Path.Combine(target, "sub", "file.txt")));
                }
                finally
                {
                    Directory.Delete(target, recursive: true);
                }
            })
            .Step("Entries with .. or an absolute path are rejected", context =>
            {
                var target = Directory.CreateTempSubdirectory("testfuzn-zip").FullName;
                try
                {
                    Assert.ThrowsExactly<InvalidDataException>(() => SafeZip.Extract(Zip("../evil.txt"), target, setExecuteBit: false));
                    Assert.ThrowsExactly<InvalidDataException>(() => SafeZip.Extract(Zip("/etc/evil.txt"), target, setExecuteBit: false));
                }
                finally
                {
                    Directory.Delete(target, recursive: true);
                }
            });
    }

    private static MemoryStream Zip(string entryName)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
            writer.Write("content");
        }

        stream.Position = 0;
        return stream;
    }
}
