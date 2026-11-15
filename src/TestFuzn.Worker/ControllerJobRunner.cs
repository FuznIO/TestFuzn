using Fuzn.TestFuzn.Internals.Cluster.Protocol;

namespace Fuzn.TestFuzn.Worker;

// Starts the controller child of a run. The controller talks to Cockpit itself, with the worker key it
// inherits from the worker app; the worker app only reports a failure, so the run does not stay "Running" when the child dies. Returns
// that failure, or null when the child exited cleanly.
internal static class ControllerJobRunner
{
    public static async Task<string?> Run(WorkerHost host, ControllerJob job, CancellationToken shutdown)
    {
        var jobDirectory = Path.Combine(host.Options.WorkDirectory, "runs", job.RunId, "controller");
        ChildProcess? child = null;
        string? failure = null;

        try
        {
            var packageDirectory = Path.Combine(jobDirectory, "package");
            await host.Builds.Unpack(WorkerHost.BuildFor(job.Builds), packageDirectory, shutdown);

            var controllerFilePath = Path.Combine(jobDirectory, ClusterProtocol.ControllerFileName);
            await File.WriteAllTextAsync(controllerFilePath, ProtocolJson.Serialize(job), shutdown);

            var resultsDirectory = Path.Combine(host.Options.WorkDirectory, "results", "controller");
            var outputLog = Path.Combine(resultsDirectory, Path.GetFileNameWithoutExtension(job.EntryAssembly), job.RunId, ClusterProtocol.OutputLogFileName);
            var environment = new Dictionary<string, string> { ["TESTFUZN_RESULTS_DIRECTORY"] = resultsDirectory };

            child = ChildProcess.Start(packageDirectory, job.EntryAssembly, "--cluster-controller=" + controllerFilePath, outputLog, environment);
            try
            {
                await child.WaitForExit(shutdown);
            }
            catch (OperationCanceledException)
            {
                // "stop" tells the controller to stop the run; it gets the same grace as a load child.
                child.Send(RunCommands.Stop);
                using var grace = new CancellationTokenSource(ClusterProtocol.ChildKillGrace);
                try
                {
                    await child.WaitForExit(grace.Token);
                }
                catch (OperationCanceledException)
                {
                    child.Kill();
                }
            }

            if (child.ExitCode != 0)
                failure = $"The controller exited with code {child.ExitCode}.{Environment.NewLine}{child.Tail()}";
        }
        catch (Exception ex)
        {
            failure = ex.Message;
        }
        finally
        {
            if (child != null)
                child.Dispose();
        }

        try
        {
            if (Directory.Exists(jobDirectory))
                Directory.Delete(jobDirectory, recursive: true);

            WorkerHost.DeleteIfEmpty(Path.GetDirectoryName(jobDirectory)!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Run {job.RunId}: could not delete {jobDirectory}: {ex.Message}");
        }

        return failure;
    }
}
