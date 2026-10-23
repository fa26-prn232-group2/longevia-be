# Smoke test cho đường deploy "DB rỗng -> kb-bootstrap tự nạp seed" (T093).
#
# Chạy trong một compose project RIÊNG với volume RIÊNG, nên không bao giờ chạm
# database `longevia_*` của máy dev. Kết thúc luôn dọn sạch (down -v).
#
#   pwsh -NoProfile -File scripts/book-kb/smoke-seed.ps1

[CmdletBinding()]
param(
    [string]$ComposeFile = 'deployments/docker-compose.yml',
    [string]$EnvFile = '.env',
    [string]$Project = 'longevia-seedtest',
    [int]$HostPort = 55432,
    [int]$ExpectedChunks = 188
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Push-Location $root
$previousPort = $env:POSTGRES_PORT
$env:POSTGRES_PORT = [string]$HostPort

# --progress plain --ansi never: compose không cần TTY, nên chạy được trong CI
# hoặc khi output bị redirect (nếu không, progress UI sẽ treo).
$composeArgs = @(
    'compose', '--progress', 'plain', '--ansi', 'never',
    '-p', $Project, '--env-file', $EnvFile, '-f', $ComposeFile,
    '--profile', 'bootstrap'
)

function Invoke-Step {
    param([string]$Name, [string[]]$StepArgs)
    Write-Host "[smoke] $Name"
    & docker @composeArgs @StepArgs
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

function Get-ChunkCount {
    $output = & docker @composeArgs exec -T postgres `
        psql -U longevia -d recommendation_db -t -A -c "SELECT count(*) FROM kb.chunk;"
    if ($LASTEXITCODE -ne 0) { throw 'psql failed while counting kb.chunk' }
    return [int](@($output)[-1]).Trim()
}

try {
    Write-Host "[smoke] project=$Project host-port=$HostPort"
    Invoke-Step 'build recommendation image' @('build', 'recommendation')
    Invoke-Step 'start postgres' @('up', '--detach', '--wait', 'postgres')
    Invoke-Step 'first bootstrap (empty database)' @('run', '--rm', '-T', '--no-deps', 'kb-bootstrap')

    $first = Get-ChunkCount
    Write-Host "[smoke] chunks after first bootstrap: $first"

    Write-Host '[smoke] second bootstrap (must skip)'
    $second = & docker @composeArgs run --rm -T --no-deps kb-bootstrap
    if ($LASTEXITCODE -ne 0) { throw "second bootstrap failed with exit code $LASTEXITCODE" }
    $secondText = ($second | Out-String)
    $skipped = $secondText -match 'already active|skipping load|skipped'
    Write-Host "[smoke] second bootstrap skipped=$skipped"

    $afterSecond = Get-ChunkCount
    Write-Host "[smoke] chunks after second bootstrap: $afterSecond"

    if ($first -ne $ExpectedChunks) {
        throw "expected $ExpectedChunks chunks after the first init, got $first"
    }
    if ($afterSecond -ne $first) {
        throw "the rerun changed the corpus: $first -> $afterSecond"
    }
    if (-not $skipped) {
        throw 'the rerun was not skipped; --skip-if-loaded did not take effect'
    }

    Write-Host '[smoke] PASS - empty database loaded from the committed seed, rerun skipped'
}
finally {
    try {
        & docker @composeArgs down --volumes --remove-orphans | Out-Null
    }
    catch {
        Write-Warning "cleanup failed: $($_.Exception.Message)"
    }
    if ($null -eq $previousPort) { Remove-Item Env:POSTGRES_PORT -ErrorAction SilentlyContinue }
    else { $env:POSTGRES_PORT = $previousPort }
    Pop-Location
}
