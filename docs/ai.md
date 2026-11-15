# Use With AI

Copy a prompt into your coding agent (Claude Code, Copilot, Cursor, ...). Each prompt points the agent to the
docs, so it uses the current API instead of guessing. Replace the text in `<...>` first.

Agents that use [Context7](https://context7.com) can also look up the TestFuzn docs there.

## Set Up TestFuzn

```text
Add TestFuzn to this .NET solution.

Read https://fuzn.io/docs/testfuzn/getting-started first and follow it exactly.

- Create a test project <name>.Tests with MSTest.Sdk and .NET 10, or use <existing test project>.
- Add the packages Fuzn.TestFuzn, Fuzn.TestFuzn.Adapters.MSTest and Fuzn.TestFuzn.Plugins.Http.
- Add one Startup class that implements IStartup and calls configuration.UseHttp().
- Add one test class that inherits from Test, with one [Test] method made of named steps.
- Do not add a Program.cs: the adapter adds the entry point.
- Build, then run the tests with dotnet test. Fix what fails and tell me the result.
```

## Write a Test

```text
Write a TestFuzn test for <what to test, e.g. "creating and reading a product through POST and GET /api/products">.

Read these pages first:
https://fuzn.io/docs/testfuzn/scenarios
https://fuzn.io/docs/testfuzn/steps
https://fuzn.io/docs/testfuzn/shared-data
https://fuzn.io/docs/testfuzn/http-testing

Rules:
- A class that inherits from Test, a [Test] method, and await Scenario() with one .Step("name", ...) per action.
  Step names say what the step does, in plain words.
- Send HTTP calls with context.CreateHttpRequest(...).
- Pass data between steps with Scenario<TModel>() and context.Model, or SetSharedData/GetSharedData.
- Log with context.Logger, not Console.WriteLine. Use TestFuzn's [Test] and Context, not MSTest's TestContext.
- Use MSTest v4 asserts.
- Build, run the test with dotnet test, and fix what fails.
```

## Write a Load Test

```text
Write a TestFuzn load test for <what to load, e.g. "GET /api/products">: <load, e.g. "50 iterations per second for 1 minute">.
It must pass when <goal, e.g. "no iteration fails and p99 is under 500 ms">.

Read these pages first:
https://fuzn.io/docs/testfuzn/scenarios
https://fuzn.io/docs/testfuzn/load-testing
https://fuzn.io/docs/testfuzn/test-runners

Rules:
- A load test is a normal scenario with .Load().Simulations(...). Reuse the steps of an existing test when one fits.
- Pick the simulation from the load-testing page. The rate is iterations started per second.
- Add a short .Load().Warmup(...) before the measured load.
- Check the goal in .Load().AssertWhenDone((context, stats) => ...), for example stats.Failed.Count
  and stats.Ok.DurationPercentile99. Use stats.GetStep("name") for one step.
- No shared mutable state between iterations: use Scenario<TModel>() and context.Model, never a captured local.
- Build, then run it with dotnet run --project <test project> -- --runner=testfuzn --test-name=<full test name>
  and tell me the result.
```

---

[← Back to Table of Contents](README.md)
