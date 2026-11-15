# Cluster Runs

A load test can run on a cluster: one or more other machines (workers) instead of your own. Use it when your laptop is too slow, when one machine cannot make enough load, or when a long test must keep running after you close the laptop.

Nothing in your test code changes. The same test runs locally or on the cluster; `--cluster` decides.

---

## How It Works

```
 Laptop / CI / Test Explorer               Cockpit                  Worker machines (one pool)
+---------------------------+       +------------------+       +-------------------------------+
| your test exe --cluster    | ----> | runs, workers    | <---- | testfuzn-worker               |
|  - uploads the build once |       | builds, reports  |       |  - controller child (1 a run) |
|  - prints the live link   |       | live stats       |       |  - load child (1 a worker)    |
|  - prints the result      |       +------------------+       +-------------------------------+
+---------------------------+                                         load ---> system under test
```

- **[Cockpit](cockpit.md)** is the hub: every machine calls it over HTTP(S), and nothing calls back. Workers open no ports. Cockpit keeps the runs, the builds and the reports, and shows the cluster on its [cluster page](cockpit.md#the-cluster-page).
- **The worker app** (`testfuzn-worker`) runs on each worker machine and joins a pool. It asks Cockpit for work every 3 seconds (every second while it runs a load job).
- **Your test exe** with `--cluster` uploads the build (only when it changed), hands the run to the pool, prints the link to the live charts in Cockpit, waits and prints the result.
- **The controller** runs on one of the workers. It starts the load on N workers, merges their stats, runs your asserts and the run-once hooks, writes the report and sends it to Cockpit.
- **One load test per run.** A cluster run runs exactly one load test.
- **The load children** run the iterations, each with its part of the load.

---

## Try It Locally

Start Cockpit with its TestFuzn integration turned on (a worker key set, see [The Cluster Page](cockpit.md#the-cluster-page)), and make a project API key in the project's settings. Then start two workers:

```bash
dotnet tool install -g Fuzn.TestFuzn.Worker
export TESTFUZN_COCKPIT_URL=http://localhost:5170
export TESTFUZN_WORKER_KEY=<the worker key set in Cockpit>
testfuzn-worker --pool local          # terminal 1
testfuzn-worker --pool local          # terminal 2
```

Cluster runs are part of TestFuzn: no package to add, nothing to call in `Startup`. Set the pool and Cockpit in `appsettings.json`:

```json
// appsettings.json
{
  "TestFuzn": {
    "Cluster": { "Pool": "local", "Workers": 2 },
    "Cockpit": { "Url": "http://localhost:5170", "ApiKey": "<your project API key>" }
  }
}
```

```bash
dotnet run --project MyTests -- --cluster --test-name=MyTests.ProductLoadTests.Load_products
```

The workers start your test exe. Its entry point comes from `Fuzn.TestFuzn.Adapters.MSTest`; if you write your own `Main`, it must call `TestFuznHost.Run<Startup>(args)` (see [Test Runners](test-runners.md#your-own-main)).

---

## Configuration

**Run options** (`appsettings*.json`, normal file rules, so they can differ per environment):

```json
{
  "TestFuzn": {
    "Cluster": {
      "Pool": "playwright",
      "Workers": 3,
      "ConnectionTimeout": "00:02:00",
      "PackagePath": null,
      "OpenBrowser": true
    },
    "Cockpit": { "Url": "https://cockpit.example.com", "ApiKey": null }
  }
}
```

| Setting | Default | Meaning |
|---------|---------|---------|
| `Pool` | `default` | Which workers run the test |
| `Workers` | `1` | How many workers share the load |
| `ConnectionTimeout` | 2 min | How long a silence may last before it counts as lost (a worker, the controller, or your attached client) |
| `PackagePath` | – | A folder to upload instead of your `bin` folder, e.g. a `dotnet publish -r linux-x64` output when native packages only copy files for your own OS |
| `OpenBrowser` | `true` | Opens the run's live page in your browser when the run is handed to the pool. Never on a CI server (`CI`, `TF_BUILD`, `GITHUB_ACTIONS` and the like are set). Useful in Visual Studio, whose Test Explorer shows the output only when the test ends |

**Cockpit connection** (`TestFuzn:Cockpit` above, or environment variables; a variable wins over the file):

| Setting | Variable | Meaning |
|---------|----------|---------|
| `Cockpit:Url` | `TESTFUZN_COCKPIT_URL` | Cockpit's address |
| `Cockpit:ApiKey` | `TESTFUZN_COCKPIT_API_KEY` | A project API key, made in the project's settings in Cockpit. The run belongs to that project. |

A key in `appsettings.json` is fine for a local Cockpit, so other developers can run without setting anything up. For a real Cockpit, keep the key in `TESTFUZN_COCKPIT_API_KEY`. Workers, and the controller on one of them, use the worker key (see [Set Up Worker Machines](worker-setup.md)); the API key stays on your machine.

Every cluster run needs a source (`--cockpit-source=<name>` or `TESTFUZN_COCKPIT_SOURCE`, e.g. `nightly-load`): Cockpit's name for the kind of run, so it knows which runs to compare. Without one, the run stops before it starts. While the run goes on, it shows on Cockpit's cluster page. When it ends, Cockpit imports the report itself and the run goes into the project's test runs, with the branch, commit and build number of your machine or pipeline. On your own machine, the branch and commit come from git.

---

## Running

```bash
dotnet run --project MyTests -- --cluster --cockpit-source=soak                        # run, wait, print the result
dotnet run --project MyTests -- --cluster --cockpit-source=soak --run-name="Soak test" # the same, with a name on Cockpit's cluster page
```

- `--cluster` (or `TESTFUZN_CLUSTER=true`) prints the link to the live charts, a `Running…` line every 30 s, then the summary, the error and the link to the run in Cockpit, and exits with pass or fail.
- Outside CI, the live charts also open in your browser. Turn this off with `Cluster:OpenBrowser: false`.
- Ctrl+C or the quit key stops the run on all workers.
- Everything else is in Cockpit: the live charts, the list of runs, their results and a **Stop** button. See [The cluster page](cockpit.md#the-cluster-page).
- If your laptop loses Cockpit during a run, the run continues on the workers and the client prints the link to it.

### The queue

When the pool's workers are busy, the run waits in the pool's queue and starts by itself when enough workers are free. The terminal shows its place, such as `Queued: 2nd in line`.

- Runs start in the order they were sent.
- To take a run out of the queue, press Ctrl+C in the terminal that started it, or click **Stop** on Cockpit's cluster page.
- A run still waiting after 24 hours is removed from the queue.
- A run fails at once when its pool has no connected workers, or fewer workers in total than the run needs.

### Test Explorer and `dotnet test`

Set `TESTFUZN_CLUSTER=true` (e.g. in a `.runsettings` file) and run one load test: it runs on the cluster and passes or fails with its run. Cancelling the test stops the run.

Cluster mode runs one load test per session. Any other test in that session, a second load test or a standard test, fails at once without running. The test runner does not tell TestFuzn which tests you selected, so TestFuzn checks while the tests run: the first load test goes to the cluster. Run standard tests without the cluster.

---

## What Runs Where

| Work | Your machine | Controller | Load workers |
|------|--------------|------------|--------------|
| `IStartup.Configure` | ✔ | ✔ | ✔ |
| `BeforeSuite` / `AfterSuite`, `BeforeClass` / `AfterClass`, context plugins | – | – | ✔ |
| `BeforeTest` / `AfterTest`, `BeforeScenario` / `AfterScenario`, input data, iteration hooks, steps | – | – | ✔ |
| `.Load().BeforeScenarioOnce` / `.Load().AfterScenarioOnce` | – | ✔ (once) | – |
| `AssertWhileWarmingUp`, `AssertWhileRunning`, `AssertWhenDone` | – | ✔ (merged stats) | – |
| Sinks | – | ✔ (merged stats) | – |
| Live charts | – (in Cockpit) | – | – |
| Report | – | written, sent to Cockpit | – |

With `--cluster` under the TestFuzn runner your machine runs no suite or class hooks, so a build agent never starts browsers or other tools. Asserts and run-once hooks run on the controller: set breakpoints in a local run.

---

## Splitting the Load

The test states the **total** load. Each of N workers runs its part; the leftover goes to the lowest worker indexes, so the totals are exact (`OneTimeLoad(1)` runs once in the whole cluster).

| Simulation | Split |
|------------|-------|
| `FixedLoad`, `FixedConcurrentLoad`, `OneTimeLoad` | Rate or count |
| `GradualLoadIncrease` | Start and end rate |
| `RandomLoadPerSecond` | Every worker draws the same rate from the same seed and runs its part |
| `Pause` | Unchanged |

In warmup every worker gets at least one iteration, so each warms its own connections. Measurement starts on all workers when all have finished warmup; that is the only sync point.

`context.Info.WorkerIndex` and `context.Info.WorkerCount` tell a step which part it runs (0 and 1 on one machine).

## Input Data

Each worker loads the full list itself and owns the items at its index, index + N, index + 2N, … so no item is used on two workers at once while looping.

| Behavior | Cluster |
|----------|---------|
| `Loop` | Each worker loops over its own slice |
| `Random` | Random from the full list |
| `LoopThenRepeatLast` | Own slice once, then the last item of the full list |
| `LoopThenRandom` | Own slice once, then random from the full list |

Workers compare the list count and hash; the report warns when they differ (e.g. a database query without `ORDER BY`). For data that must be created once, use `BeforeScenarioOnce`:

## Run-Once Hooks

```csharp
await Scenario()
    .Load().BeforeScenarioOnce(async context =>
    {
        var items = await api.CreateItems(100);
        return items;                                   // must be JSON-serializable
    })
    .InputDataFromList(context => context.OnceData<List<Item>>().Cast<object>().ToList())
    .Step("Use item", async context => { /* ... */ })
    .Load().Simulations((context, simulations) => simulations.OneTimeLoad(100))
    .Load().AfterScenarioOnce(async context => await api.DeleteItems(context.OnceData<List<Item>>()));
```

- Runs once per run: on the controller in a cluster, in the test process on one machine. The data goes to every worker as JSON; on one machine it is round-tripped too, so serialization problems show up locally first.
- `AfterScenarioOnce` also runs after a stop or failure, so created data is cleaned up.
- The controller must reach the API these hooks use.

---

## Environments

| Setting | Belongs to | In a cluster run |
|---------|------------|-----------------|
| Execution environment | The machine | Each worker's own `TESTFUZN_EXECUTION_ENVIRONMENT`, set where the worker app starts |
| Target environment | The run | Chosen on your machine and sent to the controller and every worker |
| Node name | The machine | Each worker loads its own `appsettings.{machine}.json` |

---

## Workers, Logs and Reports

- The live charts and the report show a row per worker: CPU, memory and iteration start lag (how late iterations start because the machine is busy). Above 80 % CPU the row is marked: an overloaded load machine makes durations look slow while the system under test is fine.
- Only report files go to Cockpit. Logs and attachments (e.g. screenshots) stay on the workers, under `TESTFUZN_KEEP_LAST_N_RUNS`, and the report shows the folder on each worker.
- A worker runs one load child at a time. A busy worker is not claimed; the run waits in the queue until enough workers are free. See [The queue](#the-queue).
- [Cockpit](cockpit.md#the-cluster-page) can show the whole cluster: every pool and worker, the runs going on with live charts, and a Stop button.

---

## Builds and Platforms

The build is uploaded to Cockpit once per hash; an unchanged build uploads nothing. Cockpit keeps a build for a day after it was last used. Each worker hashes the unpacked build again and does not run a build that does not match its hash. Only the `runtimes/` and `.playwright/node/` folders of the workers' OS family are uploaded. Workers can run Windows, Linux or macOS on x64 or ARM; one pool can mix them.

- Keep `AnyCPU` (the default); a fixed `PlatformTarget` does not run on ARM workers.
- `Fuzn.TestFuzn.Plugins.Playwright` copies Playwright's node driver for every OS (`PlaywrightPlatform=all`); with `InstallPlaywright`, workers install only the configured browsers.
- Packages that copy native files only for your OS: set `Cluster:PackagePath` to a `dotnet publish -r <rid> --self-contained false` folder for the workers' platform. You get a warning when it is older than your build.
- Use `/` in file paths (`context.Files` turns `\` into `/`) and match the case of file names: Linux file names are case sensitive.
- Workers need the .NET runtime your test project targets.

---

## Known Limitations

- Cluster runs need a Cockpit with its TestFuzn integration turned on. Cockpit, the workers and your test project must use the same TestFuzn version (protocol 3).
- Workers must be running before a run; TestFuzn does not start or stop machines.
- The split assumes equal workers; there are no per-worker weights.
- `AssertWhileRunning` runs once per tick (every 2 s) on the merged stats, not after every iteration.
- A start reaches the workers through Cockpit, so it takes effect about 3 seconds after the controller sends it; all workers then start at the same moment.

See [Set Up Worker Machines](worker-setup.md) and [Run From a Pipeline](pipelines.md).

---

[← Back to Table of Contents](README.md)
