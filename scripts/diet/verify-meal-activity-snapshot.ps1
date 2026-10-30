#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string] $Path)
$ErrorActionPreference = 'Stop'
$resolved = (Resolve-Path -LiteralPath $Path).Path
$document = [Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($resolved))
$hash = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
try {
    $root = $document.RootElement
    if ($root.GetProperty('schemaVersion').GetInt32() -ne 1) { throw 'Unsupported snapshot schema version' }
    $snapshotId = $root.GetProperty('snapshotId').GetGuid()
    if ($snapshotId -eq [Guid]::Empty -or $root.GetProperty('capturedAtUtc').GetDateTimeOffset().Offset -ne [TimeSpan]::Zero) {
        throw 'Invalid snapshot identity or UTC timestamp'
    }
    $ids = [Collections.Generic.HashSet[Guid]]::new()
    $count = 0L
    foreach ($entry in $root.GetProperty('entries').EnumerateArray()) {
        $mealId = $entry.GetProperty('mealLogId').GetGuid()
        $userId = $entry.GetProperty('userId').GetGuid()
        if ($mealId -eq [Guid]::Empty -or $userId -eq [Guid]::Empty -or -not $ids.Add($mealId)) {
            throw 'Invalid or duplicate meal identity'
        }
        $date = [DateOnly]::MinValue
        if (-not [DateOnly]::TryParseExact($entry.GetProperty('businessDate').GetString(), 'yyyy-MM-dd',
            [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref]$date)) {
            throw 'Invalid business date'
        }
        if ($entry.GetProperty('createdAt').GetDateTimeOffset().Offset -ne [TimeSpan]::Zero) { throw 'Non-UTC creation time' }
        $hash.AppendData([Text.Encoding]::UTF8.GetBytes($entry.GetRawText() + "`n"))
        $count++
    }
    if ($root.GetProperty('recordCount').GetInt64() -ne $count) { throw 'Snapshot record count mismatch' }
    $actual = [Convert]::ToHexString($hash.GetHashAndReset())
    if ($actual -cne $root.GetProperty('entriesSha256').GetString()) { throw 'Snapshot checksum mismatch' }
    Write-Output "PASS: snapshot $snapshotId verified ($count distinct meal records, SHA-256 matches)"
} finally {
    $hash.Dispose()
    $document.Dispose()
}
