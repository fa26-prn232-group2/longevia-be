#Requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$localEnv = @{}
foreach ($line in Get-Content (Join-Path $repoRoot '.env')) {
    if ($line -match '^([^#=]+)=(.*)$') { $localEnv[$Matches[1]] = $Matches[2].Trim() }
}
function To-Base64Url([byte[]] $bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}
function Assert-Equal($actual, $expected, [string] $name) {
    if ($actual -ne $expected) { throw "$name expected $expected, received $actual" }
    Write-Output "PASS: $name"
}

# The local Identity key signs an isolated test user; this does not test Identity login.
$now = [DateTimeOffset]::UtcNow
$userId = [Guid]::NewGuid().ToString()
$header = @{ alg = 'ES256'; typ = 'JWT'; kid = $localEnv.JWT__KEY_ID } | ConvertTo-Json -Compress
$payload = @{
    sub = $userId; iss = $localEnv.JWT__ISSUER; aud = $localEnv.JWT__AUDIENCE
    iat = $now.ToUnixTimeSeconds(); exp = $now.AddMinutes(5).ToUnixTimeSeconds()
} | ConvertTo-Json -Compress
$unsigned = (To-Base64Url ([Text.Encoding]::UTF8.GetBytes($header))) + '.' +
    (To-Base64Url ([Text.Encoding]::UTF8.GetBytes($payload)))
$key = [Security.Cryptography.ECDsa]::Create()
try {
    $pem = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($localEnv.JWT__SIGNING_PRIVATE_KEY))
    $key.ImportFromPem($pem)
    $signature = $key.SignData([Text.Encoding]::UTF8.GetBytes($unsigned),
        [Security.Cryptography.HashAlgorithmName]::SHA256,
        [Security.Cryptography.DSASignatureFormat]::IeeeP1363FixedFieldConcatenation)
    $auth = @{ Authorization = 'Bearer ' + $unsigned + '.' + (To-Base64Url $signature) }
} finally { $key.Dispose() }

$gateway = 'http://localhost:' + $localEnv.GATEWAY_PORT
Assert-Equal (Invoke-RestMethod "$gateway/api/diet/health").status 'ok' 'Diet health through Nginx'
$anonymous = Invoke-WebRequest "$gateway/api/diet/foods" -SkipHttpErrorCheck
Assert-Equal $anonymous.StatusCode 401 'Anonymous catalog rejected'
$target = @{ targetCalories = 2000; targetProteinGrams = 125; targetCarbsGrams = 250; targetFatGrams = 55 }
$saved = Invoke-RestMethod "$gateway/api/diet/targets/me" -Method Put -Headers $auth -ContentType 'application/json' -Body ($target | ConvertTo-Json)
Assert-Equal $saved.targetCalories 2000 'Target persisted'
$foods = Invoke-RestMethod "$gateway/api/diet/foods?category=Meat" -Headers $auth
Assert-Equal $foods.totalCount 1 'Shared demo catalog available'
$day = $now.ToString('yyyy-MM-dd')
$meal = @{
    date = $day; mealType = 'Lunch'
    items = @(@{ foodId = '11111111-1111-4111-8111-111111111111'; quantity = 150 })
}
$logged = Invoke-RestMethod "$gateway/api/diet/meals" -Method Post -Headers $auth -ContentType 'application/json' -Body ($meal | ConvertTo-Json -Depth 4)
Assert-Equal $logged.totalCalories 247.5 '150g meal stores 247.5 kcal'
$summary = Invoke-RestMethod "$gateway/api/diet/meals/daily-summary?date=$day" -Headers $auth
Assert-Equal $summary.consumedCalories 247.5 'Daily summary persisted through real Postgres'
Assert-Equal $summary.macros.protein.consumed 46.5 'Daily protein equals 46.5g'
$swagger = Invoke-RestMethod "$gateway/api/diet/swagger/v1/swagger.json"
Assert-Equal $swagger.servers[0].url '/api/diet' 'Swagger gateway base path'
$prometheus = Invoke-RestMethod ('http://localhost:' + $localEnv.PROMETHEUS_PORT + '/api/v1/targets')
$dietTarget = @($prometheus.data.activeTargets | Where-Object { $_.labels.job -eq 'diet' })
Assert-Equal $dietTarget.Count 1 'Diet scrape job exists'
Assert-Equal $dietTarget[0].health 'up' 'Prometheus scrapes Diet metrics'
$grafana = Invoke-RestMethod ('http://localhost:' + $localEnv.GRAFANA_PORT + '/api/health')
Assert-Equal $grafana.database 'ok' 'Grafana health on local port'

# Deliberately send only to the local Mailpit catcher, never a provider relay.
$subject = 'Diet local smoke ' + [Guid]::NewGuid().ToString('N')
$message = [Net.Mail.MailMessage]::new('no-reply@longevia.local', 'diet-smoke@example.invalid',
    $subject, 'Local smoke test. Captured by Mailpit.')
$smtp = [Net.Mail.SmtpClient]::new('localhost', [int]$localEnv.MAILPIT_SMTP_PORT)
try { $smtp.Send($message) } finally { $smtp.Dispose(); $message.Dispose() }
$mailpit = Invoke-RestMethod ('http://localhost:' + $localEnv.MAILPIT_WEB_PORT + '/api/v1/messages')
Assert-Equal @($mailpit.messages | Where-Object { $_.Subject -eq $subject }).Count 1 'Mailpit captures local test email'
Write-Output 'Diet smoke passed. No Identity login or cross-service AI integration is claimed.'
