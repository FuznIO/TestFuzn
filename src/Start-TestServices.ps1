# Manages the Docker services the test suite runs against: TestWebApp, SampleApp.WebApp and InfluxDB.
#
# Do NOT start the docker-compose project from Visual Studio -- it uses Fast Mode,
# which idles the container on a helper process and only injects the app while a VS
# session is live. A test build tears that session down and kills the app, which shows
# up as net::ERR_CONNECTION_CLOSED on https://localhost:7058.
#
#   .\Start-TestServices.ps1          rebuild and (re)start -- run this after changing either web app
#   .\Start-TestServices.ps1 stop     stop the containers, freeing 7058/7059 and 44316 for Kestrel
#   .\Start-TestServices.ps1 start    start them again
#   .\Start-TestServices.ps1 down     remove the containers entirely
#   .\Start-TestServices.ps1 logs     follow the logs

[CmdletBinding()]
param(
    [ValidateSet('up', 'stop', 'start', 'down', 'logs')]
    [string]$Command = 'up'
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$project = 'testfuzn'
$services = @('testwebapp', 'sampleapp.webapp', 'influxdb')

# Visual Studio's own Fast Mode containers hold the same ports and must go first.
$stale = docker ps -aq --filter 'name=dockercompose[0-9]'
if ($stale) {
    Write-Host 'Removing Visual Studio Fast Mode containers holding the ports...'
    docker rm -f $stale | Out-Null
}

switch ($Command) {
    'up' {
        docker compose -p $project up -d --build @services
        if ($LASTEXITCODE -ne 0) { throw 'docker compose up failed' }

        Write-Host 'Waiting for TestWebApp...' -NoNewline
        foreach ($attempt in 1..30) {
            try {
                Invoke-WebRequest -Uri 'https://localhost:7058/' -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 2 | Out-Null
                Write-Host ' ready.'
                Write-Host 'TestWebApp      https://localhost:7058  (http 7059)'
                Write-Host 'SampleApp       https://localhost:44316'
                Write-Host 'InfluxDB        http://localhost:8086'
                return
            }
            catch {
                Write-Host '.' -NoNewline
                Start-Sleep -Seconds 1
            }
        }
        throw 'TestWebApp did not become reachable on https://localhost:7058'
    }
    'stop' { docker compose -p $project stop @services }
    'start' { docker compose -p $project start @services }
    'down' { docker compose -p $project down }
    'logs' { docker compose -p $project logs -f @services }
}
