Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$script = Join-Path $PSScriptRoot 'Invoke-GlCutoverRehearsal.ps1'
$cases = @(
    @{ Name='configured development database'; Value='Server=localhost;Database=RhemaERP;Integrated Security=true'; Expected='Refusing database' },
    @{ Name='missing database'; Value='Server=localhost;Integrated Security=true'; Expected='no Database/Initial Catalog' },
    @{ Name='attach file'; Value='Server=localhost;Database=RHEMAERP_GL_REHEARSAL_SAFE;Integrated Security=true;AttachDbFilename=C:\temp\unsafe.mdf'; Expected='AttachDbFilename is forbidden' },
    @{ Name='user instance'; Value='Server=localhost;Database=RHEMAERP_GL_REHEARSAL_SAFE;Integrated Security=true;User Instance=true'; Expected='User Instance connections are forbidden' },
    @{ Name='invalid suffix'; Value='Server=localhost;Database=RHEMAERP_GL_REHEARSAL_bad-name;Integrated Security=true'; Expected='Refusing database' }
)

$prior = [Environment]::GetEnvironmentVariable('RHEMA_GL_REHEARSAL_CONNECTION', 'Process')
$priorSource = [Environment]::GetEnvironmentVariable('RHEMA_GL_SOURCE_READONLY_CONNECTION', 'Process')
$nonEmptyEvidence = $null
try {
    foreach ($case in $cases) {
        [Environment]::SetEnvironmentVariable('RHEMA_GL_REHEARSAL_CONNECTION', $case.Value, 'Process')
        $output = & pwsh -NoProfile -File $script -Mode RehearseEmpty 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0) { throw "Safety case '$($case.Name)' unexpectedly succeeded." }
        if ($output -notmatch [regex]::Escape($case.Expected)) {
            throw "Safety case '$($case.Name)' did not return the expected refusal '$($case.Expected)'."
        }
        Write-Host "PASS: $($case.Name)"
    }

    [Environment]::SetEnvironmentVariable(
        'RHEMA_GL_REHEARSAL_CONNECTION',
        'Server=target-host;Database=RHEMAERP_GL_REHEARSAL_CLONE_SAFETY;Integrated Security=true',
        'Process')
    $nonEmptyEvidence = Join-Path ([System.IO.Path]::GetTempPath()) "RHEMAERP_GL_REHEARSAL_EVIDENCE_$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $nonEmptyEvidence | Out-Null
    Set-Content -LiteralPath (Join-Path $nonEmptyEvidence 'stale.txt') -Value 'stale'
    $cloneCases = @(
        @{ Name='missing clone source'; Value=$null; Expected="RHEMA_GL_SOURCE_READONLY_CONNECTION' is required" },
        @{ Name='rehearsal clone source'; Value='Server=target-host;Database=RHEMAERP_GL_REHEARSAL_SOURCE;Integrated Security=true'; Expected='Clone source must be the retained development database' },
        @{ Name='wrong clone source catalog'; Value='Server=target-host;Database=master;Integrated Security=true'; Expected='must be the exact configured RhemaERP catalog' },
        @{ Name='cross-server clone'; Value='Server=source-host;Database=RhemaERP;Integrated Security=true'; Expected='must resolve to the same SQL Server instance' },
        @{ Name='nonempty evidence directory'; Value='Server=target-host;Database=RhemaERP;Integrated Security=true'; Expected='Evidence directory must be new or empty'; EvidenceDirectory=$nonEmptyEvidence }
    )
    foreach ($case in $cloneCases) {
        [Environment]::SetEnvironmentVariable('RHEMA_GL_SOURCE_READONLY_CONNECTION', $case.Value, 'Process')
        $arguments = @('-NoProfile', '-File', $script, '-Mode', 'RehearseClone')
        if ($case.ContainsKey('EvidenceDirectory')) { $arguments += @('-EvidenceDirectory', $case.EvidenceDirectory) }
        $output = & pwsh @arguments 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0) { throw "Safety case '$($case.Name)' unexpectedly succeeded." }
        if ($output -notmatch [regex]::Escape($case.Expected)) {
            throw "Safety case '$($case.Name)' did not return the expected refusal '$($case.Expected)'."
        }
        Write-Host "PASS: $($case.Name)"
    }
}
finally {
    [Environment]::SetEnvironmentVariable('RHEMA_GL_REHEARSAL_CONNECTION', $prior, 'Process')
    [Environment]::SetEnvironmentVariable('RHEMA_GL_SOURCE_READONLY_CONNECTION', $priorSource, 'Process')
    if ($nonEmptyEvidence -and (Test-Path -LiteralPath $nonEmptyEvidence)) {
        Remove-Item -LiteralPath $nonEmptyEvidence -Recurse -Force
    }
}

Write-Host 'All GL cutover rehearsal safety refusals passed.'
