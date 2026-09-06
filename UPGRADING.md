# Upgrading

Breaking changes and the steps to move across them. Newest first.

---

## 0.7.6 — one test project, no separate runner project

Live console output used to need a second project. Your test project now does both jobs, so the
runner project goes away.

**1. Add this to your test project's `.csproj`:**

```xml
<GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
```

**2. Add a `Program.cs` to your test project:**

```csharp
using Fuzn.TestFuzn;

namespace MyProject.Tests;

internal static class Program
{
    public static Task<int> Main(string[] args) => TestFuznHost.Run<Startup>(args);
}
```

**3. Delete the runner project**, and remove it from your solution file.

**4. Run with live console output like this:**

```bash
dotnet run --project MyProject.Tests -- --runner=testfuzn
```

`dotnet test`, Test Explorer and CI do not change.

See [Test Runners](docs/test-runners.md).

---

[← Back to README](README.md)
