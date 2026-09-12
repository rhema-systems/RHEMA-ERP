Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$script = Join-Path $PSScriptRoot 'Invoke-GlCutoverRehearsal.ps1'
$cases = @(
    @{ Name='configured development database'; Value='Server=localhost;Database=RhemaERP;Integrated Security=true'; Expected='Refusing database' },
    @{ Name='missing server'; Value='Database=RHEMAERP_GL_REHEARSAL_SAFE;Integrated Security=true'; Expected='no SQL Server target' },
    @{ Name='missing database'; Value='Server=localhost;Integrated Security=true'; Expected='no Database/Initial Catalog' },
    @{ Name='attach file'; Value='Server=localhost;Database=RHEMAERP_GL_REHEARSAL_SAFE;Integrated Security=true;AttachDbFilename=C:\temp\unsafe.mdf'; Expected='AttachDbFilename is forbidden' },
    @{ Name='user instance'; Value='Server=localhost;Database=RHEMAERP_GL_REHEARSAL_SAFE;Integrated Security=true;User Instance=true'; Expected='User Instance connections are forbidden' },
    @{ Name='invalid suffix'; Value='Server=localhost;Database=RHEMAERP_GL_REHEARSAL_bad-name;Integrated Security=true'; Expected='Refusing database' }
)

$flagNames = @('Finance__AccountingEvents__Enabled', 'Finance__ProducerIntents__Enabled', 'Finance__ProducerIntentGroups__Enabled')
$reviewNames = @('RHEMA_GL_REVIEWED_COMMIT','RHEMA_GL_REVIEWED_TREE')
foreach ($name in @('RHEMA_GL_REHEARSAL_CONNECTION','RHEMA_GL_SOURCE_READONLY_CONNECTION') + $flagNames + $reviewNames) {
    [Environment]::SetEnvironmentVariable($name, $null, 'Process')
}
$nonEmptyEvidence = $null
$emptyEvidence = $null
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

    foreach ($flagName in $flagNames) {
        [Environment]::SetEnvironmentVariable($flagName, 'false', 'Process')
    }
    [Environment]::SetEnvironmentVariable('RHEMA_GL_SOURCE_READONLY_CONNECTION', 'Server=target-host;Database=RhemaERP;Integrated Security=true', 'Process')
    $emptyEvidence = Join-Path ([System.IO.Path]::GetTempPath()) "RHEMAERP_GL_REHEARSAL_FINAL_EVIDENCE_$([guid]::NewGuid().ToString('N'))"
    $repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    $executedCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $executedTree = (& git -C $repositoryRoot rev-parse 'HEAD^{tree}').Trim()

    $reviewCases = @(
        @{ Name='missing reviewed commit/tree'; Commit=$null; Tree=$null; Expected='requires exact 40-hex' },
        @{ Name='malformed reviewed commit'; Commit='bad'; Tree=$executedTree; Expected='requires exact 40-hex' },
        @{ Name='descendant or unreviewed HEAD'; Commit=('0' * 40); Tree=$executedTree; Expected='does not exactly match' },
        @{ Name='unreviewed tree'; Commit=$executedCommit; Tree=('0' * 40); Expected='does not exactly match' }
    )
    foreach ($case in $reviewCases) {
        [Environment]::SetEnvironmentVariable('RHEMA_GL_REVIEWED_COMMIT', $case.Commit, 'Process')
        [Environment]::SetEnvironmentVariable('RHEMA_GL_REVIEWED_TREE', $case.Tree, 'Process')
        $output = & pwsh -NoProfile -File $script -Mode RehearseFinalClone -EvidenceDirectory $emptyEvidence 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0 -or $output -notmatch [regex]::Escape($case.Expected)) {
            throw "Safety case '$($case.Name)' did not return '$($case.Expected)'. Output: $output"
        }
        Write-Host "PASS: $($case.Name)"
    }
    [Environment]::SetEnvironmentVariable('RHEMA_GL_REVIEWED_COMMIT', $executedCommit, 'Process')
    [Environment]::SetEnvironmentVariable('RHEMA_GL_REVIEWED_TREE', $executedTree, 'Process')

    $dirtyProbe = Join-Path $repositoryRoot 'gl-final-clone-dirty-probe.tmp'
    try {
        'untracked safety probe' | Set-Content -Encoding ascii -LiteralPath $dirtyProbe
        $output = & pwsh -NoProfile -File $script -Mode RehearseFinalClone -EvidenceDirectory $emptyEvidence 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0 -or $output -notmatch 'completely clean tracked and untracked repository') {
            throw "Safety case 'dirty untracked repository' did not fail before SQL. Output: $output"
        }
        Write-Host 'PASS: dirty untracked repository'
    }
    finally { if (Test-Path -LiteralPath $dirtyProbe) { Remove-Item -LiteralPath $dirtyProbe -Force } }

    $ignoredProbe = Join-Path $repositoryRoot '.env.test'
    if (Test-Path -LiteralPath $ignoredProbe) { throw 'Cannot run ignored-config safety probe because .env.test already exists.' }
    try {
        'ignored configuration safety probe' | Set-Content -Encoding ascii -LiteralPath $ignoredProbe
        $output = & pwsh -NoProfile -File $script -Mode RehearseFinalClone -EvidenceDirectory $emptyEvidence 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0 -or $output -notmatch 'found ignored code, migration, seeder, configuration, or script files') {
            throw "Safety case 'ignored relevant configuration' did not fail before SQL. Output: $output"
        }
        Write-Host 'PASS: ignored relevant configuration'
    }
    finally { if (Test-Path -LiteralPath $ignoredProbe) { Remove-Item -LiteralPath $ignoredProbe -Force } }

    $finalCases = @(
        @{ Name='final clone alternate target variable'; Expected='requires the exact process variables'; Arguments=@('-TargetConnectionEnvironmentVariable','ALTERNATE_TARGET','-EvidenceDirectory',$emptyEvidence) },
        @{ Name='final clone alternate source variable'; Expected='requires the exact process variables'; Arguments=@('-SourceConnectionEnvironmentVariable','ALTERNATE_SOURCE','-EvidenceDirectory',$emptyEvidence) },
        @{ Name='final clone missing explicit evidence'; Expected='requires an explicit new or empty -EvidenceDirectory'; Arguments=@() },
        @{ Name='final clone missing source'; Expected="RHEMA_GL_SOURCE_READONLY_CONNECTION' is required"; Source=$null; Arguments=@('-EvidenceDirectory',$emptyEvidence) },
        @{ Name='final clone source attach file'; Expected='AttachDbFilename is forbidden'; Source='Server=target-host;Database=RhemaERP;Integrated Security=true;AttachDbFilename=C:\temp\unsafe.mdf'; Arguments=@('-EvidenceDirectory',$emptyEvidence) },
        @{ Name='final clone source user instance'; Expected='User Instance connections are forbidden'; Source='Server=target-host;Database=RhemaERP;Integrated Security=true;User Instance=true'; Arguments=@('-EvidenceDirectory',$emptyEvidence) },
        @{ Name='final clone wrong source catalog'; Expected='must be the exact configured RhemaERP catalog'; Source='Server=target-host;Database=master;Integrated Security=true'; Arguments=@('-EvidenceDirectory',$emptyEvidence) },
        @{ Name='final clone cross-server source'; Expected='must resolve to the same SQL Server instance'; Source='Server=source-host;Database=RhemaERP;Integrated Security=true'; Arguments=@('-EvidenceDirectory',$emptyEvidence) },
        @{ Name='final clone unsafe in-repository evidence'; Expected='allowed only below .artifacts/finance-gl-rehearsal'; Source='Server=target-host;Database=RhemaERP;Integrated Security=true'; Arguments=@('-EvidenceDirectory',(Join-Path $repositoryRoot 'scripts\unsafe-final-evidence')) },
        @{ Name='final clone nonempty evidence'; Expected='Evidence directory must be new or empty'; Source='Server=target-host;Database=RhemaERP;Integrated Security=true'; Arguments=@('-EvidenceDirectory',$nonEmptyEvidence) }
    )
    foreach ($case in $finalCases) {
        if ($case.ContainsKey('Source')) {
            [Environment]::SetEnvironmentVariable('RHEMA_GL_SOURCE_READONLY_CONNECTION', $case.Source, 'Process')
        }
        $arguments = @('-NoProfile', '-File', $script, '-Mode', 'RehearseFinalClone') + @($case.Arguments)
        $output = & pwsh @arguments 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0) { throw "Safety case '$($case.Name)' unexpectedly succeeded." }
        if ($output -notmatch [regex]::Escape($case.Expected)) {
            throw "Safety case '$($case.Name)' did not return the expected refusal '$($case.Expected)'."
        }
        Write-Host "PASS: $($case.Name)"
    }

    [Environment]::SetEnvironmentVariable('RHEMA_GL_SOURCE_READONLY_CONNECTION', 'Server=target-host;Database=RhemaERP;Integrated Security=true', 'Process')
    foreach ($flagName in $flagNames) {
        foreach ($unsafeValue in @($null, 'true')) {
            foreach ($resetName in $flagNames) { [Environment]::SetEnvironmentVariable($resetName, 'false', 'Process') }
            [Environment]::SetEnvironmentVariable($flagName, $unsafeValue, 'Process')
            $output = & pwsh -NoProfile -File $script -Mode RehearseFinalClone -EvidenceDirectory $emptyEvidence 2>&1 | Out-String
            if ($LASTEXITCODE -eq 0) { throw "Safety case '$flagName=$unsafeValue' unexpectedly succeeded." }
            if ($output -notmatch [regex]::Escape("'$flagName' must be explicitly set to false")) {
                throw "Safety case '$flagName=$unsafeValue' did not refuse the unsafe feature flag."
            }
            Write-Host "PASS: $flagName refuses '$unsafeValue'"
        }
    }

    $scriptText = Get-Content -Raw -LiteralPath $script
    foreach ($requiredText in @(
        '$authoritativeMigrationCount = 456',
        "'20260908120000_AddProducerIntentGroupsC8'",
        'Assert-TargetAbsent $target',
        'COPY_ONLY, CHECKSUM, INIT',
        'RESTORE VERIFYONLY',
        'DBCC CHECKDB',
        'This harness never overwrites it',
        'Assert-FinalReviewedGitState',
        'Invoke-SqlWithSanitizedEvidence',
        'reviewed-git-state.json',
        "Write-FinalSummary `$evidenceDirectoryResolved 'NO_GO_PREFLIGHT'"
    )) {
        if ($scriptText -notmatch [regex]::Escape($requiredText)) {
            throw "Final clone safety contract is missing: $requiredText"
        }
    }
    $finalInvariantText = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'sql\gl-final-clone-invariants.sql')
    foreach ($requiredText in @('20260908120000_AddProducerIntentGroupsC8', 'ACCOUNT|', 'ACCOUNT_SEGMENT_VALUE|',
        'APPLICABILITY_POLICY|', 'SELECTION_EVIDENCE|', 'ACCOUNTING_EVENT|', 'ACCOUNTING_EVENT_POSTING|',
        'AccountingEventProducerReceipts', 'PRODUCER_INTENT_GROUP|', 'PRODUCER_INTENT_GROUP_MEMBER|', 'CONTROL_COUNTS')) {
        if ($finalInvariantText -notmatch [regex]::Escape($requiredText)) {
            throw "Final-clone invariant contract is missing: $requiredText"
        }
    }
    if ($finalInvariantText -match 'seed must not create (journal|account transaction|posting event)') {
        throw 'Final-clone invariants incorrectly assume an empty historical business dataset.'
    }
    Write-Host 'PASS: final 456/C8, absent-target, no-overwrite, backup/restore/DBCC and preflight NO-GO contracts'
}
finally {
    foreach ($name in @('RHEMA_GL_REHEARSAL_CONNECTION','RHEMA_GL_SOURCE_READONLY_CONNECTION') + $flagNames + $reviewNames) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    if ($nonEmptyEvidence -and (Test-Path -LiteralPath $nonEmptyEvidence)) {
        Remove-Item -LiteralPath $nonEmptyEvidence -Recurse -Force
    }
    if ($emptyEvidence -and (Test-Path -LiteralPath $emptyEvidence)) {
        Remove-Item -LiteralPath $emptyEvidence -Recurse -Force
    }
}

Write-Host 'All GL cutover rehearsal safety refusals passed.'
