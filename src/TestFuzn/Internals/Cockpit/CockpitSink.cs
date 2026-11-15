using System.Net.Http.Json;
using System.Text.Json;
using Fuzn.TestFuzn.Contracts.Sinks;
using Fuzn.TestFuzn.Internals;
using Microsoft.Extensions.Logging;

namespace Fuzn.TestFuzn.Internals.Cockpit;

// Live points while a load test runs. The report goes to Cockpit with `cockpit upload`. A failed send is not
// retried: the live chart gets a gap, and the end result still comes from the report.
internal class CockpitSink : ISinkPlugin
{
    internal const string LivePath = "/api/testfuzn/live";
    internal const string ApiKeyHeader = "X-Api-Key";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CockpitSettings _settings;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private bool _isStarted;
    private bool _isLiveDisabled;
    private bool _hasWarned;

    public CockpitSink(CockpitSettings settings)
    {
        _settings = settings;
    }

    public Task InitSuite()
    {
        if (_settings.IsComplete)
            _http.DefaultRequestHeaders.Add(ApiKeyHeader, _settings.ApiKey);

        return Task.CompletedTask;
    }

    public async Task WriteStats(SinkStats stats)
    {
        if (!_settings.IsComplete || _isLiveDisabled)
            return;

        if (!await EnsureStarted(stats.TestRunId))
            return;

        var tick = new
        {
            type = "tick",
            testRunId = stats.TestRunId,
            testName = stats.GroupName + "." + stats.TestName,
            points = stats.Points.Select(point => new
            {
                path = point.Path,
                phase = point.Phase,
                tMs = point.TimeMs,
                okCount = point.OkCount,
                failedCount = point.FailedCount,
                durationMeanMs = point.MeanMs,
                durationP50Ms = point.P50Ms,
                durationP90Ms = point.P90Ms,
                durationP95Ms = point.P95Ms,
                durationP99Ms = point.P99Ms,
                virtualUsers = point.VirtualUsers,
                statusCodes = point.StatusCodes
            }),
            errors = Internals.Cluster.LiveErrors.Of(stats.Scenario).Select(error => new
            {
                path = error.Path,
                message = error.Message,
                count = error.Count,
                isOther = error.IsOther
            }),
            stoppedReason = Internals.Cluster.LiveErrors.StoppedReason(stats.Scenario),
            generators = stats.Generators.Select(generator => new
            {
                generatorName = generator.Id,
                tMs = stats.Points.Count > 0 ? stats.Points[0].TimeMs : 0,
                cpuPercent = generator.CpuPercent,
                memoryMb = generator.MemoryMb,
                startLagMs = generator.IterationStartLagMs
            })
        };

        await Post(LivePath, tick);
    }

    // One Cockpit run per test session: created on the first live data.
    private async Task<bool> EnsureStarted(string testRunId)
    {
        if (_isStarted)
            return true;

        await _startLock.WaitAsync();
        try
        {
            if (_isStarted)
                return true;

            var session = TestSession.Current;
            string? suite = null;
            string? targetEnvironment = null;
            string? executionEnvironment = null;
            string? nodeName = null;
            if (session != null)
            {
                var configuration = session.Configuration;
                if (configuration != null)
                {
                    if (configuration.Suite != null)
                        suite = configuration.Suite.Name;
                    targetEnvironment = configuration.TargetEnvironment;
                    executionEnvironment = configuration.ExecutionEnvironment;
                }
                nodeName = session.NodeName;
            }

            var start = new
            {
                type = "start",
                testRunId,
                framework = "testfuzn",
                suite,
                source = _settings.Source,
                branch = _settings.Branch,
                commit = _settings.Commit,
                buildNumber = _settings.BuildNumber,
                targetEnvironment,
                executionEnvironment,
                nodeName
            };

            if (!await Post(LivePath, start))
            {
                _isLiveDisabled = true;
                return false;
            }

            _isStarted = true;
            return true;
        }
        finally
        {
            _startLock.Release();
        }
    }

    public Task CleanupSuite()
    {
        return Task.CompletedTask;
    }

    private async Task<bool> Post(string path, object body)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(_settings.Url + path, body, JsonOptions);
            if (response.IsSuccessStatusCode)
                return true;

            Warn($"Cockpit answered {(int) response.StatusCode} {response.ReasonPhrase} to {path}.");
            return false;
        }
        catch (Exception ex)
        {
            Warn($"Could not reach Cockpit at {_settings.Url}: {ex.Message}");
            return false;
        }
    }

    // Once per session, in the log only: Cockpit being down must never fail a load test.
    private void Warn(string message)
    {
        if (_hasWarned)
            return;

        _hasWarned = true;
        var session = TestSession.Current;
        if (session == null)
            return;

        var logger = session.Logger;
        if (logger != null)
            logger.LogWarning(message);
    }
}
