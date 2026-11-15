# TestFuzn

C# test framework: unit, end-to-end and load tests behind one fluent API. A scenario becomes a load test
with `.Load().Simulations(...)`. User docs: `docs/` (copied to fuzn.io).

## Build & test

```bash
dotnet build src/TestFuzn.slnx
dotnet test --project src/TestFuzn.Tests/TestFuzn.Tests.csproj
dotnet test --project src/TestFuzn.Tests/TestFuzn.Tests.csproj --filter "FullyQualifiedName~TestMethodName"
.\Start-Dev.ps1        # Docker: TestWebApp, SampleApp.WebApp, 3 workers (pool docker); "stop" frees the ports
```

- Microsoft.Testing.Platform: pass the project with `--project`; `-v` is rejected.
- **Run the full `TestFuzn.Tests` suite before reporting a task done.** A clean build is not enough.
- Other suites: `TestFuzn.Tests.Attributes`, `TestFuzn.Tests.DefaultHttpClient`, `TestFuzn.Tests.Cluster`.
  The cluster suite needs the Docker workers and Cockpit on http://localhost:5170 (`start-dev.ps1` in the
  Cockpit repo; dev API key `ck_local_testfuzn_cluster`, worker key `testfuzn-local-worker-key`). Without
  Cockpit its end-to-end tests are inconclusive.
- Re-run `Start-Dev.ps1` after changing `TestWebApp`, `SampleApp.WebApp` or `TestFuzn.Worker`.
- **Never start the containers from Visual Studio:** Fast Mode containers show "Up" while every request
  fails with `net::ERR_CONNECTION_CLOSED`.

## Cluster rules the code does not show

- Every machine only calls Cockpit over HTTP(S). Nothing calls back; workers open no ports.
- One load test per cluster session. Cockpit, workers and test projects use the same protocol version.
- The test states the total load. Workers split it; the leftover goes to the lowest indexes, the same
  rule as input data slicing. In warmup every worker gets at least 1.
- The controller merges histograms; percentiles are never averaged. A tick counts only when every worker
  sent a new status. Live points are per interval, from histogram differences.
- Load children never write sinks or reports. Logs never go to Cockpit.

## Conventions

- No shared mutable state between iterations: use `Scenario<TModel>` or `SetSharedData`/`GetSharedData`,
  never a captured local.
- Tests run on both adapters (MSTest and the TestFuzn runner): use TestFuzn's `[Test]` and `Context`, not
  `TestContext`. Log with `Context.Logger`.
- Console output goes through `ITestFrameworkAdapter` or `ITerminalWriter`/`ILiveViewHost`
  (`src/TestFuzn/Internals/Terminal/`), never `System.Console`.
- MSTest v4 asserts: specific ones (`Assert.IsGreaterThan`, `Assert.HasCount`, `Assert.ThrowsExactly<T>`),
  not `Assert.IsTrue(expr)`, `StringAssert` or `[ExpectedException]`.
- Tests that expect a failure start with `ShouldFail_`.
- Null checks use explicit `if`, not `?.`, `??` or `??=`.
- Comments only for a non-obvious why, in one short line. XML docs on public API only, a sentence or two.
- Update `README.md` and `docs/*.md` with every documented change. The website copies `docs/*.md` and
  `UPGRADING.md` with `src/Fuzn.Website/scripts/Sync-TestFuznDocs.ps1` in the `fuzn` repo; a new page
  also needs an entry in `src/Fuzn.Website/Content/docs/testfuzn/nav.json`.
- `<Version>` in `src/Directory.Build.props` is the only version. On a bump, update the package versions
  in the `docs/getting-started.md` csproj, and put each breaking change in `UPGRADING.md` under it.
- PR reviews cover the framework and plugins, not test projects or TestWebApp.
