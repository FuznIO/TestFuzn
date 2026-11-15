# Run From a Pipeline

A pipeline runs a load test on the cluster the same way a developer does: the build agent only needs outbound access to Cockpit. It uploads the build, waits, prints the result and fails the job when the test fails.

```yaml
name: load-test
on: workflow_dispatch
jobs:
  load:
    runs-on: ubuntu-latest
    env:
      TESTFUZN_COCKPIT_URL: https://cockpit.example.com
      TESTFUZN_COCKPIT_API_KEY: ${{ secrets.COCKPIT_API_KEY }}
      TESTFUZN_COCKPIT_SOURCE: nightly
    steps:
      - uses: actions/checkout@v4
      - run: dotnet build MyTests/MyTests.csproj -c Release
      - run: dotnet MyTests/bin/Release/net10.0/MyTests.dll --cluster --test-name=MyTests.ProductLoadTests.Load_products
```

The same file is in [`examples/cluster/github-actions.yml`](../examples/cluster/github-actions.yml). Other CI systems use the same steps.

- The API key is the project's API key from its settings in Cockpit; the run belongs to that project.
- One command runs one load test. For several load tests, run one command per test.

## Results in Cockpit

A cluster run needs `TESTFUZN_COCKPIT_SOURCE`; without it the run stops before it starts. The run goes into the project's test runs when it ends: Cockpit imports the report itself, with the branch, commit and build number of the pipeline. No upload step is needed.

Standard tests, and load tests that run on the build agent itself, send their report with `cockpit upload` (see [Importing results](https://fuzn.io/docs/cockpit/importing-results)).

---

[← Back to Table of Contents](README.md)
