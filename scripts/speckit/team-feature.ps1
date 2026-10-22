#!/usr/bin/env pwsh
# Team-mode identity helper for Spec Kit.
#
# Feature directories follow: specs/<owner>-<NNN>-<slug>
#   owner  short lowercase handle, [a-z0-9-] only, 2-20 chars. Machine-local:
#          read from -Owner / SPECIFY_OWNER / .specify/owner.json (gitignored).
#   NNN    zero-padded counter scoped to that owner (never a repo-wide counter).
#   slug   unique across the repository; the branch is feat/<slug>.
#
# Commands:
#   owner     Show or set this machine's handle in .specify/owner.json.
#   resolve   Verify this checkout is bound to the current branch. Use instead
#             of guessing the feature from specs/ or a stale feature.json.
#   new       Create the feature directory, spec.md and meta.json.
#   bind      Re-point .specify/feature.json at the feature for this branch.
#   registry  Print (or -Write) specs/registry.md from every meta.json.
#   check     Validate repository-level feature integrity. Exit 1 on failure.
#
# The helper never edits the Spec Kit managed files under .specify/scripts/.

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('owner', 'resolve', 'new', 'bind', 'registry', 'check')]
    [string]$Command,
    [string]$Slug,
    [string]$Description = '',
    [string]$Owner,
    [string]$Handle,
    [string]$Root,
    [switch]$Write,
    [switch]$Json,
    [switch]$Strict,
    [switch]$Help
)

$ErrorActionPreference = 'Stop'

$usage = @"
Usage: ./team-feature.ps1 <command> [options]

Commands:
  owner      Show this machine's handle, or set it with -Handle
  resolve    Verify the bound feature matches the current branch
  new        Create specs/<owner>-<NNN>-<slug> from the spec template
  bind       Bind .specify/feature.json to the feature of the current branch
  registry   Print specs/registry.md (-Write to update the file)
  check      Validate feature directories, meta.json and the local identity

Options:
  -Handle <handle>     owner: this machine's handle (2-20 chars, [a-z0-9-])
  -Slug <slug>         Feature slug (required for new), e.g. user-auth
  -Description <text>  Short description recorded in meta.json
  -Owner <handle>      Override the configured owner handle
  -Root <path>         Project root to operate on (testing / CI)
  -Write               registry: write specs/registry.md
  -Json                Machine-readable output
  -Strict              check: fail on warnings (missing registry.md, stale bind)
  -Help                Show this message
"@

if ($Help -or -not $Command) {
    Write-Output $usage
    if (-not $Command) { exit 1 }
    exit 0
}

function Get-TeamRoot {
    param([string]$Override)

    if ($Override) {
        $resolved = Resolve-Path -LiteralPath $Override -ErrorAction SilentlyContinue
        if (-not $resolved -or -not (Test-Path -LiteralPath $resolved.Path -PathType Container)) {
            throw "Root '$Override' does not exist."
        }
        if (-not (Test-Path -LiteralPath (Join-Path $resolved.Path '.specify') -PathType Container)) {
            throw "Root '$($resolved.Path)' is not a Spec Kit project (no .specify/ directory)."
        }
        return $resolved.Path
    }

    $current = (Get-Location).Path
    while ($true) {
        if (Test-Path -LiteralPath (Join-Path $current '.specify') -PathType Container) {
            return $current
        }
        $parent = Split-Path $current -Parent
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $current) { break }
        $current = $parent
    }
    throw 'Cannot find the repository root (no .specify/ directory above the current directory).'
}

function Get-GitBranch {
    param([string]$Root)

    # symbolic-ref also resolves unborn branches (a fresh repository has no commit
    # yet, where rev-parse --abbrev-ref HEAD reports the literal "HEAD").
    $symbolic = & git -C $Root symbolic-ref --short HEAD 2>$null
    if ($LASTEXITCODE -eq 0 -and $symbolic) {
        return ([string]($symbolic | Select-Object -First 1)).Trim()
    }

    $output = & git -C $Root rev-parse --abbrev-ref HEAD 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $output) { return '' }
    return ([string]($output | Select-Object -First 1)).Trim()
}

function Assert-FeatureBranch {
    param([string]$Branch)

    if ([string]::IsNullOrWhiteSpace($Branch) -or $Branch -eq 'HEAD') {
        throw 'Detached HEAD. Check out the feature branch (feat/<slug>) before running Spec Kit.'
    }
    if (@('main', 'master', 'develop', 'trunk') -contains $Branch) {
        throw "Refusing to do Spec Kit work on '$Branch'. Create or check out 'feat/<slug>' first."
    }
}

function Get-BranchSlug {
    param([string]$Branch)

    if ($Branch -match '^(?:feat|fix)/(.+)$') {
        $rest = $Matches[1]
        if ($rest.Contains('/')) {
            throw "Branch '$Branch' has more than one path segment. Use 'feat/<slug>' so the feature is unambiguous."
        }
        return $rest
    }
    throw "Branch '$Branch' is not a feature branch. Use 'feat/<slug>'."
}

function Get-OwnerFilePath {
    param([string]$Root)

    return (Join-Path (Join-Path $Root '.specify') 'owner.json')
}

function Assert-OwnerHandle {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw 'An owner handle is required.'
    }
    if ($Value.Length -lt 2 -or $Value.Length -gt 20 -or $Value -cnotmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$') {
        throw "Handle '$Value' is invalid: use 2-20 lowercase letters, digits or single hyphens, not starting or ending with '-' (e.g. duck, minh-anh)."
    }
}

function Get-OwnerConfig {
    param([string]$Root)

    $path = Get-OwnerFilePath -Root $Root
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }

    try {
        $data = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
    } catch {
        throw ".specify/owner.json is not valid JSON: $($_.Exception.Message). Fix it or run: pwsh scripts/speckit/team-feature.ps1 owner -Handle <handle>"
    }

    $handle = ''
    if ($null -ne $data -and ($data.PSObject.Properties.Name -contains 'handle')) {
        $handle = [string]$data.handle
    }
    if (-not $handle) {
        throw '.specify/owner.json has no "handle". Set it with: pwsh scripts/speckit/team-feature.ps1 owner -Handle <handle>'
    }
    Assert-OwnerHandle -Value $handle
    return [PSCustomObject]@{ Handle = $handle; Path = $path }
}

function Resolve-OwnerHandle {
    param([string]$Root, [string]$Explicit)

    if ($Explicit) {
        Assert-OwnerHandle -Value $Explicit
        return [PSCustomObject]@{ Handle = $Explicit; Source = '-Owner' }
    }
    if ($env:SPECIFY_OWNER) {
        Assert-OwnerHandle -Value $env:SPECIFY_OWNER
        return [PSCustomObject]@{ Handle = $env:SPECIFY_OWNER; Source = 'SPECIFY_OWNER' }
    }

    $config = Get-OwnerConfig -Root $Root
    if ($null -eq $config) {
        throw ('No owner handle configured in .specify/owner.json. ' +
            'Ask the developer for a short lowercase handle (2-20 chars, [a-z0-9-]), then run: ' +
            'pwsh scripts/speckit/team-feature.ps1 owner -Handle <handle>')
    }
    return [PSCustomObject]@{ Handle = $config.Handle; Source = '.specify/owner.json' }
}

function Invoke-Owner {
    param([string]$Root, [string]$Value)

    $path = Get-OwnerFilePath -Root $Root
    if ([string]::IsNullOrWhiteSpace($Value)) {
        $config = Get-OwnerConfig -Root $Root
        if ($null -eq $config) {
            throw ('No owner handle configured. Ask the developer for a short lowercase handle ' +
                '(2-20 chars, [a-z0-9-]), then run: ' +
                'pwsh scripts/speckit/team-feature.ps1 owner -Handle <handle>')
        }
        return [PSCustomObject]@{ OWNER = $config.Handle; OWNER_FILE = $path; WRITTEN = $false }
    }

    Assert-OwnerHandle -Value $Value
    $text = ([ordered]@{ handle = $Value } | ConvertTo-Json -Compress)
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, $text, $utf8)
    return [PSCustomObject]@{ OWNER = $Value; OWNER_FILE = $path; WRITTEN = $true }
}

function Assert-Slug {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw 'A feature slug is required. Pass -Slug <slug>, e.g. -Slug user-auth.'
    }
    if ($Value -notmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$') {
        throw "Slug '$Value' must be lowercase words joined by single hyphens (^[a-z0-9]+(?:-[a-z0-9]+)*$)."
    }
}

function Get-FeatureDirectory {
    param([string]$Root)

    return (Join-Path $Root 'specs')
}

function Get-FeatureRecords {
    param([string]$Root)

    $specsDir = Get-FeatureDirectory -Root $Root
    $records = @()
    if (-not (Test-Path -LiteralPath $specsDir -PathType Container)) { return $records }

    foreach ($dir in Get-ChildItem -LiteralPath $specsDir -Directory -ErrorAction SilentlyContinue) {
        if ($dir.Name.StartsWith('.')) { continue }
        $metaPath = Join-Path $dir.FullName 'meta.json'
        $meta = $null
        if (Test-Path -LiteralPath $metaPath -PathType Leaf) {
            try {
                $meta = [System.IO.File]::ReadAllText($metaPath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
            } catch {
                $meta = 'INVALID'
            }
        }
        $records += [PSCustomObject]@{
            Name     = $dir.Name
            Path     = $dir.FullName
            MetaPath = $metaPath
            Meta     = $meta
        }
    }
    return $records
}

function Get-ExpectedDirectoryName {
    param($Meta)

    $number = [int]$Meta.number
    return '{0}-{1:000}-{2}' -f [string]$Meta.owner, $number, [string]$Meta.slug
}

function Read-FeatureState {
    param([string]$Root)

    $path = Join-Path (Join-Path $Root '.specify') 'feature.json'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw 'No feature is bound in this checkout (.specify/feature.json is missing). Run: scripts/speckit/team-feature.ps1 bind'
    }
    try {
        $state = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
    } catch {
        throw "Cannot read .specify/feature.json: $($_.Exception.Message)"
    }
    if (-not $state.feature_directory) {
        throw '.specify/feature.json has no feature_directory. Run: scripts/speckit/team-feature.ps1 bind'
    }
    return $state
}

function Save-FeatureState {
    param([string]$Root, $Meta)

    $relative = 'specs/{0}' -f (Get-ExpectedDirectoryName -Meta $Meta)
    $payload = [ordered]@{
        feature_directory = $relative
        owner             = [string]$Meta.owner
        number            = [int]$Meta.number
        slug              = [string]$Meta.slug
        branch            = [string]$Meta.branch
    }
    $path = Join-Path (Join-Path $Root '.specify') 'feature.json'
    $text = $payload | ConvertTo-Json -Compress
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, $text, $utf8)
    $env:SPECIFY_FEATURE = [string]$Meta.slug
    $env:SPECIFY_FEATURE_DIRECTORY = (Join-Path $Root $relative)
    return $path
}

function Save-FeatureMeta {
    param([string]$Path, $Meta)

    $text = $Meta | ConvertTo-Json -Depth 4
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $text, $utf8)
}

function Find-FeatureBySlug {
    param([string]$Root, [string]$Value)

    # Not $matches: PowerShell variable names are case-insensitive, so that name
    # would collide with the automatic $Matches variable.
    $found = @(Get-FeatureRecords -Root $Root | Where-Object {
        $_.Name -match ('-' + [regex]::Escape($Value) + '$') -or
        ($_.Meta -isnot [string] -and $null -ne $_.Meta -and $_.Meta.slug -eq $Value)
    })
    return $found
}

function Get-TemplateContent {
    param([string]$Root, [string]$Name)

    $common = Join-Path (Join-Path $Root '.specify/scripts/powershell') 'common.ps1'
    if (-not (Test-Path -LiteralPath $common -PathType Leaf)) {
        throw "Missing $common. Cannot resolve the $Name template."
    }
    . $common
    $content = Resolve-TemplateContent -TemplateName $Name -RepoRoot $Root
    if ($null -eq $content) {
        throw "Could not resolve the '$Name' template under .specify/templates (or overrides/presets)."
    }
    return $content
}

function Invoke-Resolve {
    param([string]$Root)

    $branch = Get-GitBranch -Root $Root
    Assert-FeatureBranch -Branch $branch

    $state = Read-FeatureState -Root $Root
    $featureDir = [string]$state.feature_directory
    if (-not [System.IO.Path]::IsPathRooted($featureDir)) {
        $featureDir = Join-Path $Root $featureDir
    }
    if (-not (Test-Path -LiteralPath $featureDir -PathType Container)) {
        throw "Bound feature directory does not exist: $($state.feature_directory). Run: scripts/speckit/team-feature.ps1 bind"
    }

    $metaPath = Join-Path $featureDir 'meta.json'
    if (-not (Test-Path -LiteralPath $metaPath -PathType Leaf)) {
        throw "Feature has no meta.json: $($state.feature_directory). Ask the owner to restore it or run check."
    }
    $meta = [System.IO.File]::ReadAllText($metaPath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json

    if ([string]$meta.branch -ne $branch) {
        throw "This checkout is bound to feature '$($meta.slug)' on branch '$($meta.branch)', but the current branch is '$branch'. Run: scripts/speckit/team-feature.ps1 bind"
    }
    $expected = Get-ExpectedDirectoryName -Meta $meta
    if ((Split-Path $featureDir -Leaf) -ne $expected) {
        throw "Feature directory '$featureDir' does not match its meta.json ('$expected'). Run check."
    }

    $spec = Join-Path $featureDir 'spec.md'
    return [PSCustomObject]@{
        FEATURE_DIR  = $featureDir
        FEATURE_SPEC = $spec
        IMPL_PLAN    = Join-Path $featureDir 'plan.md'
        TASKS        = Join-Path $featureDir 'tasks.md'
        META_FILE    = $metaPath
        OWNER        = [string]$meta.owner
        NUMBER       = [int]$meta.number
        SLUG         = [string]$meta.slug
        BRANCH       = $branch
        STATUS       = [string]$meta.status
    }
}

function Invoke-New {
    param([string]$Root, [string]$Value, [string]$Text, [string]$OwnerOverride)

    Assert-Slug -Value $Value
    $branch = Get-GitBranch -Root $Root
    Assert-FeatureBranch -Branch $branch
    $branchSlug = Get-BranchSlug -Branch $branch
    if ($branchSlug -ne $Value) {
        throw "Branch '$branch' does not match slug '$Value'. Create the branch 'feat/$Value' first, or run with -Slug $branchSlug."
    }

    $existing = @(Find-FeatureBySlug -Root $Root -Value $Value)
    if ($existing.Count -gt 0) {
        throw "Slug '$Value' is already used by '$($existing[0].Name)'. Slugs are unique across the repository; pick another."
    }

    $owner = Resolve-OwnerHandle -Root $Root -Explicit $OwnerOverride
    $records = @(Get-FeatureRecords -Root $Root | Where-Object {
        $_.Meta -isnot [string] -and $null -ne $_.Meta -and [string]$_.Meta.owner -eq $owner.Handle
    })

    $highest = 0
    foreach ($record in $records) {
        $parsedNumber = 0
        if ([int]::TryParse([string]$record.Meta.number, [ref]$parsedNumber) -and $parsedNumber -gt $highest) {
            $highest = $parsedNumber
        }
    }
    $number = [int]$highest + 1

    $meta = [ordered]@{
        owner      = $owner.Handle
        number     = $number
        slug       = $Value
        branch     = $branch
        status     = 'open'
        services   = @()
        description = $Text
        opened_at  = (Get-Date).ToString('yyyy-MM-dd')
    }
    $dirName = Get-ExpectedDirectoryName -Meta $meta
    $featureDir = Join-Path (Get-FeatureDirectory -Root $Root) $dirName
    if (Test-Path -LiteralPath $featureDir) {
        throw "Feature directory already exists: specs/$dirName"
    }

    New-Item -ItemType Directory -Path $featureDir -Force | Out-Null
    $specPath = Join-Path $featureDir 'spec.md'
    $template = Get-TemplateContent -Root $Root -Name 'spec-template'
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($specPath, $template, $utf8)

    $metaPath = Join-Path $featureDir 'meta.json'
    Save-FeatureMeta -Path $metaPath -Meta $meta
    $statePath = Save-FeatureState -Root $Root -Meta $meta

    return [PSCustomObject]@{
        FEATURE_DIR = $featureDir
        SPEC_FILE   = $specPath
        META_FILE   = $metaPath
        STATE_FILE  = $statePath
        OWNER       = $owner.Handle
        OWNER_SOURCE = $owner.Source
        FEATURE_NUM = ('{0:000}' -f $number)
        SLUG        = $Value
        BRANCH      = $branch
    }
}

function Invoke-Bind {
    param([string]$Root)

    $branch = Get-GitBranch -Root $Root
    Assert-FeatureBranch -Branch $branch
    $slug = Get-BranchSlug -Branch $branch

    $found = @(Find-FeatureBySlug -Root $Root -Value $slug)
    if ($found.Count -eq 0) {
        throw "No feature directory matches slug '$slug'. Create it first: scripts/speckit/team-feature.ps1 new -Slug $slug"
    }
    if ($found.Count -gt 1) {
        $names = ($found | ForEach-Object { $_.Name }) -join ', '
        throw "Slug '$slug' matches more than one feature directory ($names). Run check."
    }
    $record = $found[0]
    if ($record.Meta -is [string] -or $null -eq $record.Meta) {
        throw "Feature '$($record.Name)' has an unreadable meta.json. Run check."
    }

    $meta = $record.Meta
    $meta.branch = $branch
    Save-FeatureMeta -Path $record.MetaPath -Meta $meta
    $statePath = Save-FeatureState -Root $Root -Meta $meta

    return [PSCustomObject]@{
        FEATURE_DIR = $record.Path
        META_FILE   = $record.MetaPath
        STATE_FILE  = $statePath
        OWNER       = [string]$meta.owner
        FEATURE_NUM = ('{0:000}' -f [int]$meta.number)
        SLUG        = [string]$meta.slug
        BRANCH      = $branch
    }
}

function Get-RegistryRows {
    param([string]$Root)

    $rows = @()
    foreach ($record in Get-FeatureRecords -Root $Root) {
        if ($record.Meta -is [string] -or $null -eq $record.Meta) { continue }
        $meta = $record.Meta
        $services = '-'
        if ($meta.PSObject.Properties.Name -contains 'services' -and @($meta.services).Count -gt 0) {
            $services = (@($meta.services) -join ', ')
        }
        $rows += [PSCustomObject]@{
            Owner    = [string]$meta.owner
            Number   = [int]$meta.number
            Slug     = [string]$meta.slug
            Branch   = [string]$meta.branch
            Status   = [string]$meta.status
            Services = $services
            Opened   = [string]$meta.opened_at
            Directory = $record.Name
        }
    }
    return ($rows | Sort-Object Owner, Number)
}

function Invoke-Registry {
    param([string]$Root, [switch]$AsWrite)

    $rows = @(Get-RegistryRows -Root $Root)
    $lines = @(
        '# Specs Registry',
        '',
        '<!-- Generated by scripts/speckit/team-feature.ps1 registry -Write. Do not edit by hand. -->',
        '<!-- One row per specs/<owner>-<NNN>-<slug> feature; meta.json is the source of truth. -->',
        '',
        '| Owner | # | Slug | Branch | Status | Services | Opened |',
        '| --- | --- | --- | --- | --- | --- | --- |'
    )
    foreach ($row in $rows) {
        $lines += ('| {0} | {1:000} | {2} | {3} | {4} | {5} | {6} |' -f $row.Owner, $row.Number, $row.Slug, $row.Branch, $row.Status, $row.Services, $row.Opened)
    }
    if ($rows.Count -eq 0) {
        $lines += '| _(none)_ | | | | | | |'
    }
    $lines += ''
    $text = ($lines -join "`n")

    $path = Join-Path (Get-FeatureDirectory -Root $Root) 'registry.md'
    if ($AsWrite) {
        $specsDir = Get-FeatureDirectory -Root $Root
        if (-not (Test-Path -LiteralPath $specsDir -PathType Container)) {
            New-Item -ItemType Directory -Path $specsDir -Force | Out-Null
        }
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($path, $text, $utf8)
    }
    return [PSCustomObject]@{ REGISTRY = $path; ROWS = $rows.Count; CONTENT = $text; WRITTEN = [bool]$AsWrite }
}

function Invoke-Check {
    param([string]$Root, [switch]$IsStrict)

    $errors = @()
    $warnings = @()
    $records = @(Get-FeatureRecords -Root $Root)

    $ownerConfig = $null
    try {
        $ownerConfig = Get-OwnerConfig -Root $Root
    } catch {
        $errors += $_.Exception.Message
    }

    $ownerPath = Get-OwnerFilePath -Root $Root
    if (Test-Path -LiteralPath $ownerPath -PathType Leaf) {
        & git -C $Root check-ignore -q -- .specify/owner.json 2>$null
        if ($LASTEXITCODE -ne 0) {
            $warnings += '.specify/owner.json is not gitignored. Add it to .gitignore so a personal handle is never committed.'
        }
    }
    $legacyIdentity = Join-Path (Get-FeatureDirectory -Root $Root) 'owners.json'
    if (Test-Path -LiteralPath $legacyIdentity -PathType Leaf) {
        $warnings += 'specs/owners.json is obsolete: identity is machine-local at .specify/owner.json. Delete the old file.'
    }

    $bySlug = @{}
    $byOwnerNumber = @{}

    foreach ($record in $records) {
        if ($record.Meta -is [string]) {
            $errors += "specs/$($record.Name)/meta.json is not valid JSON."
            continue
        }
        if ($null -eq $record.Meta) {
            if (Test-Path -LiteralPath (Join-Path $record.Path 'spec.md') -PathType Leaf) {
                $errors += "specs/$($record.Name) looks like a feature but has no meta.json (legacy or manual directory)."
            }
            continue
        }

        $meta = $record.Meta
        $missing = @()
        foreach ($field in @('owner', 'number', 'slug', 'branch')) {
            if (-not ($meta.PSObject.Properties.Name -contains $field)) {
                $errors += "specs/$($record.Name)/meta.json is missing '$field'."
                $missing += $field
            }
        }
        if ($missing.Count -gt 0) { continue }

        try { $numberValue = [int]$meta.number } catch {
            $errors += "specs/$($record.Name)/meta.json: number '$($meta.number)' is not an integer."
            continue
        }

        $expected = Get-ExpectedDirectoryName -Meta $meta
        if ($record.Name -ne $expected) {
            $errors += "specs/$($record.Name) does not match its meta.json ('$expected')."
        }
        try {
            Assert-OwnerHandle -Value ([string]$meta.owner)
        } catch {
            $errors += "specs/$($record.Name): $($_.Exception.Message)"
        }
        if ([string]$meta.status -and @('open', 'closed') -notcontains [string]$meta.status) {
            $errors += "specs/$($record.Name): status '$($meta.status)' must be 'open' or 'closed'."
        }

        $slugKey = [string]$meta.slug
        if ($bySlug.ContainsKey($slugKey)) {
            $errors += "Slug '$slugKey' is used by both '$($bySlug[$slugKey])' and 'specs/$($record.Name)'."
        } else {
            $bySlug[$slugKey] = "specs/$($record.Name)"
        }

        $numberKey = '{0}#{1}' -f [string]$meta.owner, $numberValue
        if ($byOwnerNumber.ContainsKey($numberKey)) {
            $errors += "Owner '$($meta.owner)' reuses number $('{0:000}' -f $numberValue) in '$($byOwnerNumber[$numberKey])' and 'specs/$($record.Name)'."
        } else {
            $byOwnerNumber[$numberKey] = "specs/$($record.Name)"
        }
    }

    $openBranches = @{}
    foreach ($record in $records) {
        if ($record.Meta -is [string] -or $null -eq $record.Meta) { continue }
        if ([string]$record.Meta.status -ne 'open') { continue }
        $branchKey = [string]$record.Meta.branch
        if (-not $branchKey) { continue }
        if ($openBranches.ContainsKey($branchKey)) {
            $errors += "Branch '$branchKey' is claimed by two open features: '$($openBranches[$branchKey])' and 'specs/$($record.Name)'."
        } else {
            $openBranches[$branchKey] = "specs/$($record.Name)"
        }
    }

    $statePath = Join-Path (Join-Path $Root '.specify') 'feature.json'
    if (Test-Path -LiteralPath $statePath -PathType Leaf) {
        try {
            $state = [System.IO.File]::ReadAllText($statePath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
            $branch = Get-GitBranch -Root $Root
            if ($state.branch -and $branch -and [string]$state.branch -ne $branch) {
                $warnings += "Local bind points at branch '$($state.branch)' but this checkout is on '$branch'. Run bind."
            }
        } catch {
            $warnings += ".specify/feature.json is unreadable. Run bind."
        }
    }

    $registryPath = Join-Path (Get-FeatureDirectory -Root $Root) 'registry.md'
    if ($records.Count -gt 0 -and -not (Test-Path -LiteralPath $registryPath -PathType Leaf)) {
        $warnings += 'specs/registry.md is missing. Run: scripts/speckit/team-feature.ps1 registry -Write'
    }

    $result = [PSCustomObject]@{
        OK          = ($errors.Count -eq 0)
        FEATURES    = $records.Count
        OWNER       = $(if ($null -ne $ownerConfig) { $ownerConfig.Handle } else { 'unset' })
        ERRORS      = @($errors)
        WARNINGS    = @($warnings)
    }

    if ($IsStrict -and $warnings.Count -gt 0) {
        $result.OK = $false
    }
    return $result
}

try {
    $repoRoot = Get-TeamRoot -Override $Root

    switch ($Command) {
        'owner' {
            $result = Invoke-Owner -Root $repoRoot -Value $Handle
            if ($Json) {
                $result | ConvertTo-Json -Compress
            } else {
                Write-Output "OWNER: $($result.OWNER)"
                Write-Output "OWNER_FILE: $($result.OWNER_FILE)"
                if ($result.WRITTEN) { Write-Output 'Wrote the handle. This file is machine-local and must stay out of git.' }
            }
        }
        'resolve' {
            $result = Invoke-Resolve -Root $repoRoot
            if ($Json) {
                $result | ConvertTo-Json -Compress
            } else {
                Write-Output "FEATURE_DIR: $($result.FEATURE_DIR)"
                Write-Output "OWNER: $($result.OWNER)"
                Write-Output "FEATURE_NUM: $($result.NUMBER)"
                Write-Output "SLUG: $($result.SLUG)"
                Write-Output "BRANCH: $($result.BRANCH)"
            }
        }
        'new' {
            $result = Invoke-New -Root $repoRoot -Value $Slug -Text $Description -OwnerOverride $Owner
            if ($Json) {
                $result | ConvertTo-Json -Compress
            } else {
                Write-Output "BRANCH_NAME: $($result.BRANCH)"
                Write-Output "FEATURE_DIR: $($result.FEATURE_DIR)"
                Write-Output "SPEC_FILE: $($result.SPEC_FILE)"
                Write-Output "FEATURE_NUM: $($result.FEATURE_NUM)"
                Write-Output "OWNER: $($result.OWNER)"
            }
        }
        'bind' {
            $result = Invoke-Bind -Root $repoRoot
            if ($Json) {
                $result | ConvertTo-Json -Compress
            } else {
                Write-Output "FEATURE_DIR: $($result.FEATURE_DIR)"
                Write-Output "OWNER: $($result.OWNER)"
                Write-Output "FEATURE_NUM: $($result.FEATURE_NUM)"
                Write-Output "BRANCH: $($result.BRANCH)"
            }
        }
        'registry' {
            $result = Invoke-Registry -Root $repoRoot -AsWrite:$Write
            if ($Json) {
                [PSCustomObject]@{ REGISTRY = $result.REGISTRY; ROWS = $result.ROWS; WRITTEN = $result.WRITTEN } | ConvertTo-Json -Compress
            } else {
                Write-Output $result.CONTENT
                if ($result.WRITTEN) { Write-Output "Wrote $($result.REGISTRY)" }
            }
        }
        'check' {
            $result = Invoke-Check -Root $repoRoot -IsStrict:$Strict
            if ($Json) {
                $result | ConvertTo-Json -Compress
            } else {
                Write-Output "Features: $($result.FEATURES) | Owner: $($result.OWNER)"
                foreach ($warning in $result.WARNINGS) { Write-Output "WARN: $warning" }
                foreach ($errorText in $result.ERRORS) { Write-Output "ERROR: $errorText" }
                if ($result.OK) { Write-Output 'OK: feature layout is consistent.' } else { Write-Output 'FAILED: fix the errors above.' }
            }
            if (-not $result.OK) { exit 1 }
        }
    }
    exit 0
} catch {
    [Console]::Error.WriteLine("ERROR: $($_.Exception.Message)")
    exit 1
}
