[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $PSScriptRoot 'ScratchSchemaAuthorityDeriver\ScratchSchemaAuthorityDeriver.csproj'
$helper = Join-Path $repositoryRoot 'src\ErpSystem.Data\Migrations\ArchivedGovernanceBaselineSql.cs'
$financeAuthority = Join-Path $repositoryRoot 'src\ErpSystem.Data\Migrations\FinanceC1C8BaselineAuthoritySql.cs'
$baseline = Join-Path $repositoryRoot 'src\ErpSystem.Data\Migrations\20260913162402_DisposableDevelopmentCurrentModelBaseline.cs'
$manifest = Join-Path $repositoryRoot 'src\ErpSystem.Data\Migrations\ArchivedGovernanceBaselineManifest.json'
$committedAuthority = Join-Path $PSScriptRoot 'sql\gl-scratch-expected-schema-authority.json'
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('rhema-scratch-authority-test-' + [guid]::NewGuid().ToString('N'))

try {
    [void][IO.Directory]::CreateDirectory($temporaryDirectory)
    $actualAuthority = Join-Path $temporaryDirectory 'authority.json'
    & dotnet run --project $project -c Release -- $helper $financeAuthority $baseline $manifest $actualAuthority
    if ($LASTEXITCODE -ne 0) { throw "Authority derivation failed with exit code $LASTEXITCODE." }

    $expectedBytes = [IO.File]::ReadAllBytes($committedAuthority)
    $actualBytes = [IO.File]::ReadAllBytes($actualAuthority)
    $expectedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($expectedBytes))
    $actualHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($actualBytes))
    if ($expectedBytes.Length -ne $actualBytes.Length -or $expectedHash -cne $actualHash) {
        throw 'Committed scratch authority is not the byte-exact deterministic derivation.'
    }

    $authority = Get-Content -Raw -LiteralPath $actualAuthority | ConvertFrom-Json -Depth 100
    $physicalCount = @($authority.triggerDefinitions | Where-Object name -ceq 'TR_PhysicalCounts_ControlledLifecycle')
    if ([string]$authority.schema -cne 'RHEMA_SCRATCH_EXPECTED_SCHEMA_AUTHORITY_V3' -or
        @($authority.triggerDefinitions).Count -ne 493 -or
        @($authority.checkConstraintDefinitions).Count -ne 835 -or
        $physicalCount.Count -ne 1 -or
        [string]$physicalCount[0].canonicalSha256 -cne 'F778FAD506C8AD0DA20605051395073B8E0BDCE6D37A0461DFCC0308E29A2B8D' -or
        [string]$authority.physicalCountMixedPatchClassification.rejectedCrossTargetFreezeCanonicalSha256 -cne
            'B06E6EEEEDC5546F1B8E6F57D80F6EE1C5BD3ACE55F9373D245EFB374398D95F') {
        throw 'Physical-count mixed static/dynamic patch regression authority drifted.'
    }

    $tampered = [Text.Encoding]::UTF8.GetString($actualBytes).Replace(
        'F778FAD506C8AD0DA20605051395073B8E0BDCE6D37A0461DFCC0308E29A2B8D',
        'B06E6EEEEDC5546F1B8E6F57D80F6EE1C5BD3ACE55F9373D245EFB374398D95F',
        [StringComparison]::Ordinal)
    $tamperedBytes = [Text.Encoding]::UTF8.GetBytes($tampered)
    $tamperedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($tamperedBytes))
    if ($tamperedBytes.Length -eq $actualBytes.Length -and $tamperedHash -ceq $actualHash) {
        throw 'Authority tamper fixture did not change the reviewed PhysicalCounts hash.'
    }

    Write-Host 'PASS: scratch authority deterministically derives 493 triggers and 835 checks.'
    Write-Host 'PASS: mixed static/dynamic PhysicalCount patch cannot apply freeze-cursor text cross-target.'
    Write-Host 'PASS: the rejected legacy cross-target hash cannot be substituted into committed authority.'
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
