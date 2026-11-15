# Cockpit

Cockpit shows the results of all your test frameworks in one place. TestFuzn talks to it in three ways:

- **The report:** send it with `cockpit upload --framework testfuzn` after the tests, like every other framework (see [Importing results](https://fuzn.io/docs/cockpit/importing-results) and Cockpit's Send results page). The command runs again safely: Cockpit imports a run once.
- **Live data:** the Cockpit sink sends a load test's live charts while it runs.
- **Cluster runs:** Cockpit is the hub (see [Cluster Runs](cluster-runs.md)); it imports a cluster run's report itself.

The sink is part of TestFuzn: there is no package to add and nothing to call in `Startup`. It turns on by itself when the run has the settings below.

## When the Sink Sends

Only when the run has all three settings. Pipelines set them; laptops do not, so developer runs stay out of Cockpit.

| Setting | Where |
|---------|-------|
| URL | `TESTFUZN_COCKPIT_URL`, or `TestFuzn:Cockpit:Url` in appsettings |
| API key | `TESTFUZN_COCKPIT_API_KEY`, or `TestFuzn:Cockpit:ApiKey` in appsettings (fine for a local Cockpit; keep a real key in the variable). The key belongs to a Cockpit project. |
| Source | `--cockpit-source=<name>` or `TESTFUZN_COCKPIT_SOURCE` (the argument wins), Cockpit's name for the kind of run, e.g. `nightly`. Not in appsettings, so a local run does not go into the project's test runs by mistake. The argument needs the TestFuzn runner (`--runner=testfuzn` or `--cluster`) |

Branch, commit and build number come from `TESTFUZN_COCKPIT_BRANCH`, `TESTFUZN_COCKPIT_COMMIT` and `TESTFUZN_COCKPIT_BUILD`, or from what GitHub Actions, Azure Pipelines and GitLab set. Without those, the branch and commit come from git, when the tests run inside a git repository.

The log file is never sent, because it can be very large. Cockpit shows a link to it instead: set `TESTFUZN_COCKPIT_LOG_URL` to where the pipeline keeps the log, for example a build artifact. Without it, the link goes to the build page of GitHub Actions, Azure Pipelines or GitLab.

In a [cluster run](cluster-runs.md) the sink does not run: the controller sends the live points itself, and the source, branch, commit, build number and log link go with the run as named fields. The API key stays on the machine that started the run.

## What the Sink Sends

| When | What |
|------|------|
| First live data | Creates the run in Cockpit (`POST /api/testfuzn/live`, type `start`) with the test run id, suite, source, branch and environments |
| Every sink write | One point per scenario and step for the interval since the previous write: ok and failed counts, mean, p50, p90, p95, p99. One row per load machine: CPU, memory, start lag |

The report sent later with `cockpit upload` completes the same run (matched by the test run id).

Live points are per interval, because a cumulative p95 flattens as a run goes on and hides spikes; TestFuzn computes them from its histograms. The report's end numbers are for the whole measurement.

A failed send is not retried and never fails the test; the live chart gets a gap and the end result still comes from the report. The API key goes in the `X-Api-Key` header.

## The Cluster Page

[Cluster runs](cluster-runs.md) go through Cockpit: it is the hub of a TestFuzn cluster. Clients, workers and controllers call it over HTTP(S), and it keeps the runs, the builds and the reports. It shows the cluster on its **TestFuzn cluster** page: **TestFuzn cluster** at the top of the left menu.

- Every pool: how many workers are free, and a warning when its workers run different TestFuzn versions.
- Every worker: free or busy, with which run, test and person, and the runs it is the controller of.
- The runs going on and the ones that ended in the last day. Open one for its live charts (iterations per second, durations, CPU per worker); they start at the run's beginning even when you open the page late. When a run ends, its page links to the results in the project's test runs.
- A **Stop** button. The person who started the run sees who stopped it. On a queued run, Stop takes it out of the queue.

The page needs no sink. Turn the integration on in Cockpit's settings; until then nothing about TestFuzn shows in Cockpit. **Cockpit settings → Integrations** (the gear icon at the top right) shows whether it is on:

| Cockpit setting | Or the environment variable | Meaning |
|-----------------|-----------------------------|---------|
| `Cockpit:TestFuzn:WorkerKey` | `COCKPIT_TESTFUZN_WORKER_KEY` | The key workers use (`TESTFUZN_WORKER_KEY`). Setting it turns the integration on. |
| `Cockpit:TestFuzn:StorePath` | `COCKPIT_TESTFUZN_STORE` | Where builds and reports are kept. Default `data/testfuzn` under Cockpit's folder; in Docker, put it on a volume. |

- Test projects and pipelines use a project API key; the run belongs to that project.
- Runs and their reports are kept 7 days, live points a day, and a build a day after it was last used.
- Cockpit, the workers and the test projects must use the same TestFuzn version.

---

[← Back to Table of Contents](README.md)
