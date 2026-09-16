[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $PSScriptRoot 'CheckConstraintDualAuthority\CheckConstraintDualAuthority.csproj'
$dll = Join-Path $PSScriptRoot 'CheckConstraintDualAuthority\bin\Release\net8.0\CheckConstraintDualAuthority.dll'
$baseline = Join-Path $repositoryRoot 'src\ErpSystem.Data\Migrations\20260916132000_DisposableDevelopmentCurrentModelBaseline.cs'
$authority = Join-Path $PSScriptRoot 'sql\gl-check-constraint-dual-authority.json'
$evidence = Join-Path (Split-Path $repositoryRoot -Parent) 'GL-Scratch-Baseline-Evidence-20260916-01'
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('rhema-check-dual-authority-' + [guid]::NewGuid().ToString('N'))

function Invoke-ExpectFailure([string]$name, [string[]]$arguments) {
    $log = Join-Path $temporaryDirectory ($name + '.log')
    & dotnet $dll @arguments *> $log
    if ($LASTEXITCODE -eq 0) { throw "Expected refusal: $name" }
}

try {
    [void][IO.Directory]::CreateDirectory($temporaryDirectory)
    & dotnet build $project -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Dual-authority build failed.' }
    & dotnet $dll --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Dual-authority semantic boundary tests failed.' }
    & dotnet $dll --verify $baseline $authority
    if ($LASTEXITCODE -ne 0) { throw 'Committed dual-authority source verification failed.' }

    $source = Join-Path $evidence 'check-constraint-source-raw.jsonl'
    $actual = Join-Path $evidence 'check-constraint-actual-raw.jsonl'
    $terminal = Join-Path $evidence 'terminal-evidence-manifest.json'
    if (-not ((Test-Path -LiteralPath $source -PathType Leaf) -and
            (Test-Path -LiteralPath $actual -PathType Leaf) -and
            (Test-Path -LiteralPath $terminal -PathType Leaf))) {
        throw 'The exact preserved -01 raw-capture package is required.'
    }
    $generated = Join-Path $temporaryDirectory 'authority.json'
    & dotnet $dll --generate $baseline $source $actual $terminal $generated
    if ($LASTEXITCODE -ne 0) { throw 'Pinned -01 authority regeneration failed.' }
    $expected = [IO.File]::ReadAllBytes($authority)
    $observed = [IO.File]::ReadAllBytes($generated)
    $expectedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($expected))
    $observedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($observed))
    if ($expected.Length -ne $observed.Length -or $expectedHash -cne $observedHash) {
        throw 'Committed authority is not the byte-exact deterministic -01 derivation.'
    }

    $tamperedActual = Join-Path $temporaryDirectory 'actual-tampered.jsonl'
    $actualText = [IO.File]::ReadAllText($actual)
    $actualText = $actualText.Replace('"originalUtf16LeBytes":106', '"originalUtf16LeBytes":108', [StringComparison]::Ordinal)
    [IO.File]::WriteAllText($tamperedActual, $actualText, [Text.UTF8Encoding]::new($false))
    Invoke-ExpectFailure 'tampered-storage-corpus' @('--generate', $baseline, $source, $tamperedActual, $terminal, (Join-Path $temporaryDirectory 'tampered-output.json'))

    $tamperedTerminal = Join-Path $temporaryDirectory 'terminal-tampered.json'
    $terminalText = [IO.File]::ReadAllText($terminal).Replace(
        'RAW_CHECK_CAPTURE_COMPLETE_REVIEW_REQUIRED_DROPPED',
        'RAW_CHECK_CAPTURE_COMPLETE_REVIEW_REQUIRED', [StringComparison]::Ordinal)
    [IO.File]::WriteAllText($tamperedTerminal, $terminalText, [Text.UTF8Encoding]::new($false))
    Invoke-ExpectFailure 'tampered-terminal-provenance' @('--generate', $baseline, $source, $actual, $tamperedTerminal, (Join-Path $temporaryDirectory 'terminal-output.json'))
    Write-Host 'PASS: exact -01 raw corpora deterministically regenerate the committed 835-row authority.'
    Write-Host 'PASS: raw storage and terminal provenance tampering are refused before authority publication.'
    $authorityObject = Get-Content -Raw -LiteralPath $authority | ConvertFrom-Json -Depth 100
    if ([string]$authorityObject.schema -cne 'RHEMA_CHECK_CONSTRAINT_DUAL_AUTHORITY_V1' -or
        @($authorityObject.entries).Count -ne 835 -or
        [int]$authorityObject.semantic.semanticMatchCount -ne 835 -or
        [int]$authorityObject.semantic.semanticDriftCount -ne 0 -or
        [int]$authorityObject.semantic.unsupportedCount -ne 0 -or
        [bool]$authorityObject.capture.storageAuthorityAccepted) {
        throw 'Committed authority summary is not the reviewed fail-closed state.'
    }

    foreach ($field in @('sourceOriginalUtf16LeSha256', 'storageOriginalUtf16LeSha256', 'semanticSha256')) {
        $tampered = Get-Content -Raw -LiteralPath $authority | ConvertFrom-Json -Depth 100
        $tampered.entries[0].$field = '0' * 64
        $path = Join-Path $temporaryDirectory ("authority-$field.json")
        [IO.File]::WriteAllText($path, ($tampered | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
        Invoke-ExpectFailure "authority-$field" @('--verify', $baseline, $path)
    }
    Write-Host 'PASS: source raw, exact SQL Server storage raw, and structural semantic identities are independently bound.'
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
