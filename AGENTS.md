# AGENTS.md

Guidance for coding agents working in this repository.

TestFuzn ("testfusion") is a C# testing framework that unifies unit, end-to-end and load tests behind one fluent API. A scenario becomes a load test when `.Load().Simulations(...)` is configured.

## Build & test

```bash
dotnet build src/TestFuzn.slnx

# Microsoft.Testing.Platform — the project must be passed via --project, and -v is rejected
dotnet test --project src/TestFuzn.Tests/TestFuzn.Tests.csproj
dotnet test --project src/TestFuzn.Tests/TestFuzn.Tests.csproj --filter "FullyQualifiedName~TestMethodName"
```

Other suites: `TestFuzn.Tests.Attributes`, `TestFuzn.Tests.DefaultHttpClient`.

**Always run the full `TestFuzn.Tests` suite before reporting a task complete.** A clean build is not enough.

Filtered runs fail `Startup.Cleanup` on `ClassLifecycleTests.BeforeClassCallCount` — that assert needs the whole suite. Ignore it when filtering; verify with an unfiltered run.

## Test services

Tests reach `TestWebApp` and `SampleApp.WebApp` over HTTP; both run in Docker. Start them with `src/Start-TestServices.ps1` and re-run it after changing either app. `.\Start-TestServices.ps1 stop` frees the ports.

**Never start the containers from Visual Studio** — its Fast Mode containers report "Up" while every request fails with `net::ERR_CONNECTION_CLOSED`.

## Conventions

- Thread safety matters in load paths — no shared mutable state between iterations. Use `Scenario<TModel>` or `SetSharedData`/`GetSharedData`, never a captured local.
- Keep tests portable across test framework adapters (MSTest, standalone runner). Use TestFuzn's own `[Test]` and `Context`, not MSTest-specific APIs such as `TestContext`.
- Use `Context.Logger`, not `Console.WriteLine`.
- Use MSTest v4 assertions — specific ones (`Assert.IsGreaterThan`, `Assert.HasCount`, `Assert.ThrowsExactly<T>`) over `Assert.IsTrue` with an expression, and over `StringAssert` / `[ExpectedException]`.
- Prefix tests expected to throw or fail with `ShouldFail_`.
- Use explicit `if` for null checks — not `?.`, `??` or `??=`.
- Reuse existing terminology; search for what the codebase already calls a concept before naming a new one.
- Update `README.md` and `docs/*.md` when a change affects documented behavior, APIs or examples.
- PR reviews focus on the core framework and plugins, not test projects or TestWebApp.
