using System.Text.Json;
using Fuzn.TestFuzn.Internals.AppConfiguration;
using Fuzn.TestFuzn.Internals.Cluster;
using Fuzn.TestFuzn.Internals.Cluster.LoadChild;

namespace Fuzn.TestFuzn.Tests.Cluster;

[TestClass]
[Tags("unit")]
public class LoadChildCoordinatorTests : Test
{
    [Test]
    public async Task Verify_the_child_reads_run_json_and_reports_through_status_json()
    {
        var directory = Directory.CreateTempSubdirectory("testfuzn-child").FullName;
        try
        {
            await Scenario()
                .Step("run.json is read and a Preparing status is written at once", context =>
                {
                    var runFile = WriteRunFile(directory, protocol: LoadChildProtocol.Version);
                    using var coordinator = LoadChildCoordinator.FromRunFile(runFile, new StringReader(""));
                    Assert.AreEqual("r1", coordinator.Settings.RunId);
                    Assert.AreEqual(2, coordinator.Settings.WorkerCount);
                    Assert.AreEqual("[1,2]", coordinator.Settings.OnceData!["S"].GetRawText());

                    coordinator.Finish(null);
                    var status = ReadStatus(directory);
                    Assert.AreEqual("r1", status.RunId);
                    Assert.AreEqual(LoadChildStates.Completed, status.State);
                })
                .Step("A closed stdin stops the child: the worker app is gone", async context =>
                {
                    var runFile = WriteRunFile(directory, protocol: LoadChildProtocol.Version);
                    using var coordinator = LoadChildCoordinator.FromRunFile(runFile, new StringReader(""));
                    coordinator.Start();

                    var deadline = DateTime.UtcNow.AddSeconds(5);
                    while (coordinator.Phase != LoadChildStates.Stopping && DateTime.UtcNow < deadline)
                        await Task.Delay(20);

                    Assert.AreEqual(LoadChildStates.Stopping, coordinator.Phase);
                    coordinator.Finish(new OperationCanceledException());
                    Assert.AreEqual(LoadChildStates.Stopped, ReadStatus(directory).State);
                })
                .Step("A failure is reported with its message", context =>
                {
                    var runFile = WriteRunFile(directory, protocol: LoadChildProtocol.Version);
                    using var coordinator = LoadChildCoordinator.FromRunFile(runFile, new StringReader(""));
                    coordinator.Finish(new InvalidOperationException("Init broke"));

                    var status = ReadStatus(directory);
                    Assert.AreEqual(LoadChildStates.Failed, status.State);
                    Assert.Contains("Init broke", status.Error!);
                })
                .Step("A run.json of another protocol version is refused", context =>
                {
                    var runFile = WriteRunFile(directory, protocol: 99);
                    var exception = Assert.ThrowsExactly<InvalidOperationException>(() => LoadChildCoordinator.FromRunFile(runFile, new StringReader("")));
                    Assert.Contains("protocol 99", exception.Message);
                });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Verify_config_files_and_data_paths_work_on_every_os()
    {
        await Scenario()
            .Step("A target environment finds its config file whatever the case of the file name", context =>
            {
                var directory = Directory.CreateTempSubdirectory("testfuzn-config").FullName;
                try
                {
                    File.WriteAllText(Path.Combine(directory, "appsettings.target-staging.json"), "{}");

                    // On Windows and macOS any case already works; on Linux the name must come back as the file is named.
                    var found = ConfigurationLoader.FindFile(directory, "appsettings.target-Staging.json");
                    Assert.IsTrue(File.Exists(Path.Combine(directory, found)));
                    Assert.AreEqual("appsettings.target-staging.json", found, ignoreCase: true);
                    Assert.AreEqual("appsettings.target-prod.json", ConfigurationLoader.FindFile(directory, "appsettings.target-prod.json"));
                }
                finally
                {
                    Directory.Delete(directory, recursive: true);
                }
            })
            .Step("Backslashes in data file paths become forward slashes", context =>
            {
                Assert.AreEqual("InputData/users.csv", FileManager.NormalizePath(@"InputData\users.csv"));
            });
    }

    private static string WriteRunFile(string directory, int protocol)
    {
        var settings = new LoadChildRunSettings
        {
            Protocol = protocol,
            RunId = "r1",
            TestName = "My.Tests.Load",
            WorkerIndex = 1,
            WorkerCount = 2,
            Seed = 7,
            OnceData = new Dictionary<string, JsonElement> { ["S"] = JsonDocument.Parse("[1,2]").RootElement.Clone() }
        };

        var path = Path.Combine(directory, "run.json");
        File.WriteAllText(path, ClusterJson.Serialize(settings));
        return path;
    }

    private static LoadChildStatus ReadStatus(string directory)
    {
        return ClusterJson.Deserialize<LoadChildStatus>(File.ReadAllText(Path.Combine(directory, LoadChildProtocol.StatusFileName)));
    }
}
