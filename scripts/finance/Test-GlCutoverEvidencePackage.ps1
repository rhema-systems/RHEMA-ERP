[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$EvidenceDirectory,
    [switch]$WriteManifest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
$required = @(
    'commit-ancestry.txt',
    'tree-equivalence.txt',
    'empty-a5\summary.json',
    'empty-a5\migration-apply.log',
    'empty-a5\migration-discovery.log',
    'empty-a5\ef-no-pending-model.log',
    'empty-a5\invariants-pass-1.txt',
    'empty-a5\invariants-pass-2.txt',
    'empty-a5\checksums.sha256',
    'empty-a5\cleanup.json',
    'clone-a2\summary.json',
    'clone-a2\source-fingerprint-before.txt',
    'clone-a2\source-fingerprint-after.txt',
    'clone-a2\backup-restore-checkdb.txt',
    'clone-a2\clone-build.log',
    'clone-a2\clone-apply-migrations.log',
    'clone-a2\clone-state-at-stop.txt',
    'clone-a2\cleanup.json',
    'sql-full-chain\test-result.log'
)

foreach ($relative in $required) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required rehearsal evidence is missing: $relative"
    }
}

$textFiles = Get-ChildItem -LiteralPath $root -File -Recurse |
    Where-Object Extension -in @('.json', '.txt', '.log', '.sha256')
$forbidden = @(
    '(?i)(Password|Pwd|User ID|UID|Data Source|Server|Integrated Security|Trusted_Connection)\s*=\s*(?!<REDACTED>)[^;\r\n]+',
    '(?i)C:\\Users\\',
    '(?i)RHEMA-AKWASI',
    '(?i)ClientConnectionId:[0-9a-f-]{36}'
)
foreach ($file in $textFiles) {
    $text = Get-Content -Raw -LiteralPath $file.FullName
    foreach ($pattern in $forbidden) {
        if ($text -match $pattern) {
            throw "Evidence contains a forbidden machine/connection marker: $($file.FullName)"
        }
    }
}

$manifestPath = Join-Path $root 'manifest.sha256'
if ($WriteManifest) {
    Get-ChildItem -LiteralPath $root -File -Recurse |
        Where-Object FullName -ne $manifestPath |
        Sort-Object FullName |
        ForEach-Object {
            $relative = [System.IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
            "$((Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash)  $relative"
        } | Set-Content -Encoding ascii -LiteralPath $manifestPath
}

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'Evidence manifest is missing. Run with -WriteManifest after assembling the package.'
}

foreach ($line in Get-Content -LiteralPath $manifestPath) {
    if ($line -notmatch '^(?<hash>[0-9A-F]{64})  (?<path>.+)$') {
        throw "Malformed evidence manifest line: $line"
    }
    $path = Join-Path $root $Matches.path
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Manifest target is missing: $($Matches.path)"
    }
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash
    if ($actual -ne $Matches.hash) {
        throw "Evidence hash mismatch: $($Matches.path)"
    }
}

Write-Host "GL cutover evidence package is complete, sanitized and hash-valid: $root"
