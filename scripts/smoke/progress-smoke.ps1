#Requires -Version 7.0
[CmdletBinding()]
param(
    [string] $Gateway = 'http://localhost:8080',
    [string] $Prometheus = 'http://localhost:9090',
    [string] $Grafana = 'http://localhost:3001'
)
$ErrorActionPreference = 'Stop'

function Assert-Equal($actual, $expected, [string] $name) {
    if ($actual -ne $expected) { throw "$name expected $expected, received $actual" }
    Write-Output "PASS: $name"
}

function Query-Prometheus([string] $query) {
    $response = Invoke-RestMethod "$Prometheus/api/v1/query?query=$([Uri]::EscapeDataString($query))"
    if ($response.status -ne 'success' -or $response.data.result.Count -ne 1) {
        throw 'Expected exactly one successful Prometheus query result'
    }
    return $response.data.result
}

foreach ($route in @('alive', 'health')) {
    Assert-Equal (Invoke-RestMethod "$Gateway/api/progress/$route").status 'ok' "Progress $route through Nginx"
}
$prefixed = Invoke-WebRequest "$Gateway/api/progress/api/progress/alive" -SkipHttpErrorCheck
Assert-Equal $prefixed.StatusCode 404 'Gateway strips exactly one prefix'
$metrics = Invoke-WebRequest "$Gateway/api/progress/metrics"
Assert-Equal $metrics.StatusCode 200 'Prometheus endpoint through gateway'
if ($metrics.Content -notmatch 'service_name="ProgressService"') { throw 'Missing Progress resource metric' }
Write-Output 'PASS: ProgressService resource metric'

$swagger = Invoke-RestMethod "$Gateway/api/progress/swagger/v1/swagger.json"
Assert-Equal $swagger.servers[0].url '/api/progress' 'Swagger gateway base URL'
Assert-Equal $swagger.components.securitySchemes.Bearer.scheme 'bearer' 'Swagger JWT bearer definition'
$ui = Invoke-WebRequest "$Gateway/api/progress/swagger/index.html"
Assert-Equal $ui.StatusCode 200 'Swagger UI through gateway'

$deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
do {
    $targets = Invoke-RestMethod "$Prometheus/api/v1/targets"
    $target = @($targets.data.activeTargets | Where-Object { $_.labels.job -eq 'progress' })
    if ($target.Count -eq 1 -and $target[0].health -eq 'up') { break }
    Start-Sleep -Seconds 1
} while ([DateTimeOffset]::UtcNow -lt $deadline)
Assert-Equal $target.Count 1 'Exactly one Progress scrape target'
Assert-Equal $target[0].health 'up' 'Progress scrape target UP'
Assert-Equal $target[0].scrapeUrl 'http://progress:8080/metrics' 'Internal scrape address'

$query = 'sum(http_server_request_duration_seconds_count{job="progress"})'
$before = Query-Prometheus $query
$initial = [double]$before.value[1]
1..5 | ForEach-Object { Invoke-RestMethod "$Gateway/api/progress/alive" | Out-Null }
$deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
do {
    Start-Sleep -Seconds 1
    $after = Query-Prometheus $query
    $current = [double]$after.value[1]
} while ($current -lt $initial + 5 -and [DateTimeOffset]::UtcNow -lt $deadline)
if ($current -lt $initial + 5) { throw "Request metric did not increase: $initial -> $current" }
Write-Output "PASS: Progress request count increased $initial -> $current"

Assert-Equal (Invoke-RestMethod "$Grafana/api/health").database 'ok' 'Grafana health'
$dashboard = Invoke-RestMethod "$Grafana/api/dashboards/uid/aspnetcore-overview"
Assert-Equal $dashboard.dashboard.uid 'aspnetcore-overview' 'Provisioned ASP.NET dashboard'
$source = @((Invoke-RestMethod "$Grafana/api/datasources") | Where-Object { $_.type -eq 'prometheus' })
Assert-Equal $source.Count 1 'Grafana Prometheus data source'
$deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
$grafanaQuery = $null
do {
    try {
        $grafanaQuery = Invoke-RestMethod "$Grafana/api/datasources/proxy/uid/$($source[0].uid)/api/v1/query?query=$([Uri]::EscapeDataString('up{job="progress"}'))"
        break
    } catch {
        # Grafana can report healthy while the bundled datasource plugin is still starting/updating.
        if ([DateTimeOffset]::UtcNow -ge $deadline) { throw }
        Start-Sleep -Seconds 1
    }
} while ([DateTimeOffset]::UtcNow -lt $deadline)
Assert-Equal $grafanaQuery.data.result[0].value[1] '1' 'Grafana data source sees Progress UP'
Write-Output 'Progress foundation smoke passed. No business, Identity login or inter-service integration claim.'
