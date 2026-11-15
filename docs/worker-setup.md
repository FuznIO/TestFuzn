# Set Up Worker Machines

Workers belong to no test project: every run brings its own build. Set a machine up once with what your tests need (the machine layer) and run the worker app on it. Examples are in [`examples/cluster`](../examples/cluster).

| Layer | Contents | Changes | How it reaches the worker |
|-------|----------|---------|---------------------------|
| Code | Test DLLs, NuGet packages, config, data files | Every run | Uploaded by `--cluster`, downloaded by the workers |
| Machine | .NET runtimes, OS libraries, browsers, CLI tools, certificates | Rarely | Your own image or VM setup |

Many tools download what they need at run time (Playwright browsers with `InstallPlaywright`), so usually only OS libraries need a few Dockerfile lines.

---

## Cockpit

[Cockpit](cockpit.md) is the hub of the cluster. Turn on its TestFuzn integration with a worker key (`Cockpit:TestFuzn:WorkerKey` or `COCKPIT_TESTFUZN_WORKER_KEY`); see [The Cluster Page](cockpit.md#the-cluster-page).

- Workers use the worker key, and so does the controller, which runs on one of them. Test projects and pipelines use the project's API key from its settings.
- Only a test project or pipeline with a project API key can upload builds and start runs. Whoever has such a key may run code on the workers, so keep keys like passwords.
- Workers can download builds but not upload them.
- Use HTTPS when Cockpit is reachable outside a private network.
- Cockpit keeps builds in its store for a day after their last use, and runs with their reports for 7 days.

---

## The Worker App

```bash
dotnet tool install -g Fuzn.TestFuzn.Worker
testfuzn-worker --pool playwright --work-dir /work
```

| Variable | Meaning |
|----------|---------|
| `TESTFUZN_COCKPIT_URL` | Cockpit's address, as this machine reaches it |
| `TESTFUZN_WORKER_KEY` | The worker key; must match Cockpit's `Cockpit:TestFuzn:WorkerKey` |
| `TESTFUZN_EXECUTION_ENVIRONMENT` | This machine's execution environment (your own value) |
| `TESTFUZN_KEEP_LAST_N_RUNS` | How many runs of logs and attachments to keep; use a low value (e.g. 2), logs can be large |

- The worker opens no port; it only calls Cockpit over HTTP(S): every 3 seconds, every second while it runs a load job.
- Before it runs a build, the worker hashes the unpacked files again and refuses a build that does not match its hash.
- Builds land in `<work-dir>/runs/<runId>` and are deleted after the run. Logs land in `<work-dir>/results/load` and `<work-dir>/results/controller`; a run's report shows where.
- On SIGTERM (e.g. a pod deletion) the worker stops its children and kills them after 30 s.
- A load child whose controller goes quiet for the connection timeout stops by itself, so a lost controller never blocks a worker. The same happens when the worker cannot reach Cockpit for that long.

---

## One VM (Small Team)

Workers with Docker Compose ([`examples/cluster/docker-compose.yml`](../examples/cluster/docker-compose.yml)), next to a Cockpit you run with its own compose file. The workers and your laptop only need to reach Cockpit. More workers on other machines only need outbound access to it too.

## Worker Image

[`examples/cluster/Dockerfile`](../examples/cluster/Dockerfile) installs the worker app on the .NET runtime image. Add what your tests need: other .NET runtimes, OS libraries, browsers, tools. Build it once; rebuild only when the machine layer changes. On an Apple Silicon Mac, build for the workers' platform (`docker build --platform linux/amd64`).

## Kubernetes

Workers as a plain Deployment with no Service and no Ingress: [`examples/cluster/kubernetes.yaml`](../examples/cluster/kubernetes.yaml). More workers: change `replicas`. Another pool: another Deployment with another `--pool`. Give workers fixed CPU (requests = limits) for stable results.

The workers and your laptop reach Cockpit at its normal address; the workers need nothing else.

---

[← Back to Table of Contents](README.md)
