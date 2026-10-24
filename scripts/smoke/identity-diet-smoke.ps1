#Requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$smtp = @{}
foreach ($line in Get-Content (Join-Path $repoRoot '.env')) {
    if ($line -match '^(SMTP__[^=]+)=(.*)$') { $smtp[$Matches[1]] = $Matches[2].Trim() }
}
if ($smtp.SMTP__HOST -ne 'mailpit' -or $smtp.SMTP__PORT -ne '1025' -or $smtp.SMTP__USE_SSL -ne 'false') {
    throw 'This smoke test requires local Mailpit SMTP; refusing to register a test email.'
}
$null = Invoke-RestMethod 'http://localhost:8025/api/v1/messages'
$email = 'diet-auth-' + [Guid]::NewGuid().ToString('N') + '@example.invalid'
$password = 'DietSmoke!9a-' + [Guid]::NewGuid().ToString('N')
$base = 'http://localhost:8080'
$ready = $false
$readyDeadline = [DateTimeOffset]::UtcNow.AddSeconds(60)
while ([DateTimeOffset]::UtcNow -lt $readyDeadline -and -not $ready) {
    try {
        $health = Invoke-WebRequest "$base/api/identity/health" -SkipHttpErrorCheck -TimeoutSec 3
        $ready = $health.StatusCode -eq 200
    } catch { $ready = $false }
    if (-not $ready) { Start-Sleep -Milliseconds 500 }
}
if (-not $ready) { throw 'Identity did not become healthy through gateway' }
$registration = Invoke-WebRequest -Method Post -Uri "$base/api/identity/auth/register" -ContentType 'application/json' -Body (
    @{ email = $email; password = $password; fullName = 'Diet Auth Smoke' } | ConvertTo-Json)
if ($registration.StatusCode -ne 201) { throw 'Registration did not return 201' }
Write-Output 'PASS: Identity registers isolated test user'
$messageId = $null
$deadline = [DateTimeOffset]::UtcNow.AddSeconds(90)
while ([DateTimeOffset]::UtcNow -lt $deadline -and -not $messageId) {
    $inbox = Invoke-RestMethod 'http://localhost:8025/api/v1/messages'
    $messageId = $inbox.messages | Where-Object { @($_.To | ForEach-Object { $_.Address }) -contains $email } | Select-Object -First 1 -ExpandProperty ID
    if (-not $messageId) { Start-Sleep -Milliseconds 500 }
}
if (-not $messageId) { throw 'Verification email did not reach local Mailpit' }
$message = Invoke-RestMethod "http://localhost:8025/api/v1/message/$messageId"
$verificationToken = [regex]::Match($message.HTML + $message.Text, 'token=([A-Za-z0-9%_-]+)').Groups[1].Value
if (-not $verificationToken) { throw 'Missing verification token in local email' }
Write-Output 'PASS: RabbitMQ/Worker delivers verification email to Mailpit'
$verified = Invoke-WebRequest "$base/api/identity/auth/verify?token=$verificationToken"
if ($verified.StatusCode -ne 204) { throw 'Verification did not return 204' }
Write-Output 'PASS: Identity verifies account'
$login = Invoke-RestMethod -Method Post -Uri "$base/api/identity/auth/login" -ContentType 'application/json' -Body (
    @{ email = $email; password = $password } | ConvertTo-Json)
if (-not $login.accessToken) { throw 'Identity did not issue an access token' }
Write-Output 'PASS: Identity issues real access token'
$headers = @{ Authorization = 'Bearer ' + $login.accessToken }
$foods = Invoke-RestMethod "$base/api/diet/foods" -Headers $headers
if ($foods.totalCount -lt 3) { throw 'Diet did not return shared catalog with real Identity token' }
Write-Output 'PASS: Diet accepts Identity token through gateway'
$target = Invoke-RestMethod -Method Put -Uri "$base/api/diet/targets/me" -Headers $headers -ContentType 'application/json' -Body (
    @{ targetCalories = 2000; targetProteinGrams = 125; targetCarbsGrams = 250; targetFatGrams = 55 } | ConvertTo-Json)
if ($target.targetCalories -ne 2000) { throw 'Target write with Identity token failed' }
Write-Output 'PASS: Diet writes target owned by Identity user'
Write-Output 'Identity-to-Diet smoke passed. Tokens/passwords were not printed; no external email relay was used.'
