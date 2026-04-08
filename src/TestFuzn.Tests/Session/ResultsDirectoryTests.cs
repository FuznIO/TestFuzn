using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.AppConfiguration;

namespace Fuzn.TestFuzn.Tests.Session;

[TestClass]
public class ResultsDirectoryTests : Test, IStartup
{
    private static string _tempBaseDirectory = null!;
    private static string _assemblyOutputDirectory = null!;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext testContext)
    {
        _tempBaseDirectory = Path.Combine(Path.GetTempPath(), $"TestFuzn_OutputDirTest_{Guid.NewGuid():N}");

        var testSession = new TestSession(nameof(ResultsDirectoryTests));
        await testSession.Init<ResultsDirectoryTests>(
            new EnvironmentWrapper(),
            new FileSystem(),
            new ConfigurationLoader(),
            new ArgumentsParser(new EnvironmentWrapper()),
            new MsTestRunnerAdapter(testContext),
            args: new Dictionary<string, string>
            {
                ["results-directory"] = _tempBaseDirectory,
                ["keep-last-n-runs"] = "2"
            });
        TestSessionRegistry.Add(testSession);

        // The parent directory contains all runs for this assembly
        _assemblyOutputDirectory = Path.GetDirectoryName(testSession.TestsResultsDirectory)!;
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        try
        {
            if (Directory.Exists(_tempBaseDirectory))
                Directory.Delete(_tempBaseDirectory, recursive: true);
        }
        catch { }
    }

    public void Configure(TestFuznConfiguration configuration)
    {
    }

    [Test(TestSessionId = nameof(ResultsDirectoryTests))]
    public async Task Custom_results_directory_is_used_as_base_path()
    {
        await Scenario()
            .Step("Verify results directory uses custom base path", context =>
            {
                Assert.StartsWith(_tempBaseDirectory, TestSession.Current.TestsResultsDirectory);
            })
            .Step("Verify TestFuznResults segment is NOT appended for custom results directory", context =>
            {
                // When the user explicitly sets results-directory, TestFuzn must not nest
                // an extra TestFuznResults folder underneath -- the user already chose the location.
                Assert.DoesNotContain("TestFuznResults", TestSession.Current.TestsResultsDirectory);
            })
            .Step("Verify layout is <customDir>/<assemblyName>/<runId>", context =>
            {
                var assemblyName = typeof(ResultsDirectoryTests).Assembly.GetName().Name!;
                var expectedPrefix = Path.Combine(_tempBaseDirectory, assemblyName);
                Assert.StartsWith(expectedPrefix, TestSession.Current.TestsResultsDirectory);
            })
            .Run();
    }

    [Test(TestSessionId = nameof(ResultsDirectoryTests))]
    public async Task Default_results_directory_uses_test_framework_path_with_TestFuznResults_segment()
    {
        // Build a session WITHOUT a custom results-directory so we exercise the default path
        // (testFramework.TestResultsDirectory + "TestFuznResults" + assemblyName + runId).
        TestSession defaultSession = null!;

        await Scenario()
            .Step("Init session without custom results-directory", async context =>
            {
                defaultSession = new TestSession($"{nameof(ResultsDirectoryTests)}_default");
                await defaultSession.Init<ResultsDirectoryTests>(
                    new EnvironmentWrapper(),
                    new FileSystem(),
                    new ConfigurationLoader(),
                    new ArgumentsParser(new EnvironmentWrapper()),
                    new MsTestRunnerAdapter(TestContext));
            })
            .Step("Verify TestFuznResults segment is present", context =>
            {
                Assert.Contains("TestFuznResults", defaultSession.TestsResultsDirectory);
            })
            .Step("Verify TestFuznResults sits next to MSTest TestResults (not inside it)", context =>
            {
                // The MSTest adapter returns the parent of the TestResults folder, so the
                // resulting path should NOT contain a "TestResults/TestFuznResults" segment.
                var combined = Path.Combine("TestResults", "TestFuznResults");
                Assert.DoesNotContain(combined, defaultSession.TestsResultsDirectory);
            })
            .Run();
    }

    [Test(TestSessionId = nameof(ResultsDirectoryTests))]
    public async Task Marker_file_exists_in_output_directory()
    {
        await Scenario()
            .Step("Verify .testfuzn marker file exists", context =>
            {
                var markerPath = Path.Combine(TestSession.Current.TestsResultsDirectory, ".testfuzn");
                Assert.IsTrue(File.Exists(markerPath), $"Marker file not found at {markerPath}");
            })
            .Run();
    }

    [Test(TestSessionId = nameof(ResultsDirectoryTests))]
    public async Task Cleanup_deletes_old_runs_with_marker_and_preserves_unmarked()
    {
        await Scenario()
            .Step("Seed old run directories", context =>
            {
                // Create 3 old run directories with marker files
                for (var i = 1; i <= 3; i++)
                {
                    var oldRunDir = Path.Combine(_assemblyOutputDirectory, $"old-run-{i}");
                    Directory.CreateDirectory(oldRunDir);
                    File.WriteAllText(Path.Combine(oldRunDir, ".testfuzn"), "");
                    // Stagger creation times so ordering is deterministic
                    Directory.SetCreationTimeUtc(oldRunDir, DateTime.UtcNow.AddHours(-i));
                }

                // Create a directory WITHOUT a marker file (should never be deleted)
                var unmarkedDir = Path.Combine(_assemblyOutputDirectory, "not-a-testfuzn-run");
                Directory.CreateDirectory(unmarkedDir);
            })
            .Step("Trigger cleanup by calling session Cleanup", async context =>
            {
                var session = TestSessionRegistry.Get(nameof(ResultsDirectoryTests));
                var testFramework = new MsTestRunnerAdapter(TestContext);
                await session.Cleanup(testFramework);
            })
            .Step("Verify only oldest marked runs were deleted (keep-last-n-runs=2)", context =>
            {
                // We have 4 marked directories: current run + old-run-1, old-run-2, old-run-3
                // keep-last-n-runs=2, so the 2 oldest (old-run-2, old-run-3) should be deleted
                var currentRunExists = Directory.Exists(TestSession.Current.TestsResultsDirectory);
                Assert.IsTrue(currentRunExists, "Current run directory should still exist");

                var oldRun1Exists = Directory.Exists(Path.Combine(_assemblyOutputDirectory, "old-run-1"));
                Assert.IsTrue(oldRun1Exists, "old-run-1 (2nd newest) should still exist");

                var oldRun2Exists = Directory.Exists(Path.Combine(_assemblyOutputDirectory, "old-run-2"));
                Assert.IsFalse(oldRun2Exists, "old-run-2 (3rd oldest) should have been deleted");

                var oldRun3Exists = Directory.Exists(Path.Combine(_assemblyOutputDirectory, "old-run-3"));
                Assert.IsFalse(oldRun3Exists, "old-run-3 (oldest) should have been deleted");
            })
            .Step("Verify unmarked directory was not deleted", context =>
            {
                var unmarkedExists = Directory.Exists(Path.Combine(_assemblyOutputDirectory, "not-a-testfuzn-run"));
                Assert.IsTrue(unmarkedExists, "Directory without .testfuzn marker should not be deleted");
            })
            .Run();
    }
}
