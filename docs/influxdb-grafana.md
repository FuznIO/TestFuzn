# Real-Time Statistics with InfluxDB and Grafana

TestFuzn supports streaming load test statistics to InfluxDB for real-time visualization with Grafana.

---

## Configuration

```csharp
public void Configure(TestFuznConfiguration configuration)
{
    configuration.UseInfluxDB(config =>
    {
        config.Url = "http://localhost:8086";
        config.Token = "your-influxdb-token";
        config.Org = "your-org";
        config.Bucket = "testfuzn";
    });
}
```

---

## Using appsettings.json

```csharp
// Startup.cs
configuration.UseInfluxDB(); // Reads from appsettings.json
```

```json
// appsettings.json
{
  "TestFuzn": {
    "InfluxDB": {
        "Url": "http://localhost:8086",
        "Token": "your-influxdb-token",
        "Org": "your-org",
        "Bucket": "testfuzn"
    }
  }
}
```

---

## Available Metrics

When using InfluxDB, the following metrics are streamed in real-time:

**Scenario Metrics** (measurement: `scenario_metrics`)
- `request_count`, `requests_per_second`, `total_execution_duration_ms`
- OK metrics: `ok_request_count`, `ok_response_time_mean_ms`, `ok_response_time_percentile_99_ms`, etc.
- Failed metrics: `failed_request_count`, `failed_response_time_mean_ms`, etc.

**Step Metrics** (measurement: `step_metrics`)
- Same metrics as scenario, but per individual step

---

## Grafana Dashboard

A pre-configured Docker Compose setup is available in the [examples/influxdb-grafana](../examples/influxdb-grafana) folder that includes both InfluxDB and Grafana with a ready-to-use dashboard.

To get started quickly:

```bash
cd examples/influxdb-grafana
docker-compose up -d
```

This will start:
- **InfluxDB** on `http://localhost:8086`
- **Grafana** on `http://localhost:3000` (default credentials: admin/admin)

The Grafana dashboard includes visualizations for:
- Requests per second over time
- Response time percentiles
- Error rate trends
- Concurrent user counts

You can also create custom Grafana dashboards to visualize additional metrics as needed.

---

```markdown
[← Back to Table of Contents](README.md)
