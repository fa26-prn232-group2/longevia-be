<#
.SYNOPSIS
    Automated E2E Smoke Test for Longevia Infrastructure & Observability.
.DESCRIPTION
    Verifies that Nginx API Gateway, Prometheus, Grafana, and Mailpit are responding
    with correct HTTP statuses, RFC 7807 ProblemDetails UTF-8 payloads, and health checks.
    Returns exit code 0 on all passes; exit code 1 on any failure.
.PARAMETER GatewayUrl
    Base URL for the Nginx API Gateway (default: http://localhost:8080).
.PARAMETER PrometheusUrl
    Base URL for Prometheus (default: http://localhost:9090).
.PARAMETER GrafanaUrl
    Base URL for Grafana (default: http://localhost:3001).
.PARAMETER MailpitUrl
    Base URL for Mailpit Web API (default: http://localhost:8025).
.PARAMETER ForceFail
    Negative control flag to deliberately fail an assertion for verification.
#>
[CmdletBinding()]
param(
    [string]$GatewayUrl = "http://localhost:8080",
    [string]$PrometheusUrl = "http://localhost:9090",
    [string]$GrafanaUrl = "http://localhost:3001",
    [string]$MailpitUrl = "http://localhost:8025",
    [switch]$ForceFail
)

$ErrorActionPreference = "Continue"
$failedCount = 0
$passedCount = 0

function Get-ResponseText($Response) {
    if ($Response.Content -is [byte[]]) {
        return [Text.Encoding]::UTF8.GetString($Response.Content)
    }
    return [string]$Response.Content
}

function Assert-Test {
    param(
        [string]$Name,
        [scriptblock]$TestBlock
    )
    Write-Host -NoNewline "[TEST] $Name ... "
    try {
        $result = & $TestBlock
        if ($result -eq $true) {
            Write-Host -ForegroundColor Green "PASS"
            $script:passedCount++
        } else {
            Write-Host -ForegroundColor Red "FAIL (Assertion evaluated to false)"
            $script:failedCount++
        }
    } catch {
        Write-Host -ForegroundColor Red "FAIL: $_"
        $script:failedCount++
    }
}

Write-Host "=========================================================="
Write-Host " Longevia Infra & Observability E2E Smoke Test"
Write-Host "=========================================================="
Write-Host "Gateway:    $GatewayUrl"
Write-Host "Prometheus: $PrometheusUrl"
Write-Host "Grafana:    $GrafanaUrl"
Write-Host "Mailpit:    $MailpitUrl"
if ($ForceFail) {
    Write-Host -ForegroundColor Yellow "WARNING: -ForceFail flag active (Negative control test)"
}
Write-Host "----------------------------------------------------------"

# 1. Nginx Gateway Liveness (/healthz)
Assert-Test -Name "Nginx /healthz returns 200 OK and text 'ok'" -TestBlock {
    $resp = Invoke-WebRequest -Uri "$GatewayUrl/healthz" -UseBasicParsing -TimeoutSec 5
    $expectedStatus = if ($ForceFail) { 500 } else { 200 }
    return ($resp.StatusCode -eq $expectedStatus -and $resp.Content.Trim() -eq "ok")
}

# 2. Nginx Gateway 404 ProblemDetails UTF-8 on unknown routes
Assert-Test -Name "Nginx unknown route returns 404 ProblemDetails with Vietnamese UTF-8" -TestBlock {
    $resp = Invoke-WebRequest -Uri "$GatewayUrl/api/unknown-service/test" -SkipHttpErrorCheck -UseBasicParsing -TimeoutSec 5
    $contentType = if ($resp.Headers["Content-Type"]) { $resp.Headers["Content-Type"] } else { "" }
    $hasProblemJson = $contentType -like "*problem+json*"
    $hasVietnamese = (Get-ResponseText $resp) -like "*Không tìm thấy*"
    return ($resp.StatusCode -eq 404 -and $hasProblemJson -and $hasVietnamese)
}

# 3. Progress is not implemented yet; Diet now has its own healthy upstream.
Assert-Test -Name "Nginx unreachable upstream returns 502 ProblemDetails with Vietnamese UTF-8" -TestBlock {
    $resp = Invoke-WebRequest -Uri "$GatewayUrl/api/progress/health" -SkipHttpErrorCheck -UseBasicParsing -TimeoutSec 5
    $contentType = if ($resp.Headers["Content-Type"]) { $resp.Headers["Content-Type"] } else { "" }
    $hasProblemJson = $contentType -like "*problem+json*"
    $hasVietnamese = (Get-ResponseText $resp) -like "*Dịch vụ không khả dụng*"
    return ($resp.StatusCode -eq 502 -and $hasProblemJson -and $hasVietnamese)
}

# 4. Prometheus Health (/api/v1/targets)
Assert-Test -Name "Prometheus API is responding on port 9090" -TestBlock {
    $resp = Invoke-RestMethod -Uri "$PrometheusUrl/api/v1/targets" -TimeoutSec 5
    return ($resp.status -eq "success")
}

# 5. Grafana Health (/api/health) on port 3001
Assert-Test -Name "Grafana is healthy on port 3001" -TestBlock {
    $resp = Invoke-RestMethod -Uri "$GrafanaUrl/api/health" -TimeoutSec 5
    return ($resp.database -eq "ok")
}

# 6. Mailpit Health (/api/v1/info)
Assert-Test -Name "Mailpit API is responding on port 8025" -TestBlock {
    $resp = Invoke-RestMethod -Uri "$MailpitUrl/api/v1/info" -TimeoutSec 5
    return ($resp.Version -ne $null -or $resp.Database -ne $null)
}

Write-Host "----------------------------------------------------------"
Write-Host "Summary: Passed: $passedCount, Failed: $failedCount"

if ($failedCount -gt 0) {
    Write-Host -ForegroundColor Red "[RESULT] Smoke tests FAILED."
    exit 1
} else {
    Write-Host -ForegroundColor Green "[RESULT] All smoke tests PASSED."
    exit 0
}
