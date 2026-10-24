#Requires -Version 7.0
[CmdletBinding()]
param([string] $DietRef = 'origin/feat/diet-service')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('progress-diet-interop-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
$archive = Join-Path $temporary 'diet.zip'
Push-Location $repo
try {
    $dietSha = git rev-parse --verify $DietRef
    if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve Diet ref; fetch it first.' }
    git archive --format=zip "--output=$archive" $dietSha
    if ($LASTEXITCODE -ne 0) { throw 'Diet archive failed.' }
    $checkout = Join-Path $temporary 'checkout'
    Expand-Archive -LiteralPath $archive -DestinationPath $checkout
    $source = Join-Path $repo 'src/Services/Progress'
    foreach ($file in Get-ChildItem -LiteralPath $source -File -Recurse) {
        $relative = [IO.Path]::GetRelativePath($source, $file.FullName)
        if ($relative -match '(^|[\\/])(bin|obj)([\\/]|$)') { continue }
        $destination = Join-Path $checkout "src/Services/Progress/$relative"
        New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
    $testDirectory = Join-Path $checkout 'src/Services/Diet/tests/Diet.IntegrationTests'
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'interop/ProgressInteropCorrectiveTests.cs') -Destination $testDirectory
    $project = Join-Path $testDirectory 'Diet.IntegrationTests.csproj'
    [xml] $xml = Get-Content -LiteralPath $project
    $group = $xml.CreateElement('ItemGroup')
    $reference = $xml.CreateElement('ProjectReference')
    $reference.SetAttribute('Include', '../../../Progress/Progress.Infrastructure/Progress.Infrastructure.csproj')
    [void] $group.AppendChild($reference)
    [void] $xml.Project.AppendChild($group)
    $xml.Save($project)
    Write-Output "Diet source SHA: $dietSha; isolated evidence directory: $temporary"
    dotnet test $project -m:1 --filter FullyQualifiedName~ProgressInteropCorrectiveTests
    $result = $LASTEXITCODE
} finally { Pop-Location }
# Preserve evidence files. Testcontainers disposes only its own containers.
exit $result
