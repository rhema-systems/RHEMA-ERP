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
$transportRoot = $null
$transportEvidence = $null
$transportRiskEvidence = $null
$zeroOutputEvidence = $null
$migrationParserEvidence = $null
$priorPath = [Environment]::GetEnvironmentVariable('PATH', 'Process')
try {
    $scriptTextForHelpers = Get-Content -Raw -LiteralPath $script
    $helperTokens = $null
    $helperErrors = $null
    $helperAst = [System.Management.Automation.Language.Parser]::ParseFile($script,[ref]$helperTokens,[ref]$helperErrors)
    $migrationHelper = @($helperAst.FindAll({ param($node)
        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Get-DiscoveredMigrationIds' }, $true))
    if ($helperErrors.Count -ne 0 -or $migrationHelper.Count -ne 1) {
        throw 'Could not load the exact migration-discovery helper for FinalClone regression testing.'
    }
    . ([ScriptBlock]::Create($migrationHelper[0].Extent.Text))
    $migrationParserEvidence = Join-Path ([System.IO.Path]::GetTempPath()) "RHEMA_MIGRATION_PARSE_$([Guid]::NewGuid().ToString('N')).log"
    @('Build started.','20260901000000_FirstExact','20260902000000_SecondExact (Pending)',
        '20260903000000_Invalid-Suffix','RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=SUCCESS|EXIT_CODE=0|COMMAND=dotnet') |
        Set-Content -Encoding ascii -LiteralPath $migrationParserEvidence
    $parsedIds = @(Get-DiscoveredMigrationIds $migrationParserEvidence)
    $expectedParsedIds = @('20260901000000_FirstExact','20260902000000_SecondExact')
    if (($parsedIds -join "`n") -cne ($expectedParsedIds -join "`n")) {
        throw 'Actual migration-discovery helper did not return the exact ordered parsed IDs.'
    }
    Write-Host 'PASS: actual FinalClone migration parser returns exact ordered IDs only'

    foreach ($case in $cases) {
        [Environment]::SetEnvironmentVariable('RHEMA_GL_REHEARSAL_CONNECTION', $case.Value, 'Process')
        $output = & pwsh -NoProfile -File $script -Mode DropRehearsal 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0) { throw "Safety case '$($case.Name)' unexpectedly succeeded." }
        if ($output -notmatch [regex]::Escape($case.Expected)) {
            throw "Safety case '$($case.Name)' did not return the expected refusal '$($case.Expected)'."
        }
        Write-Host "PASS: $($case.Name)"
    }

    [Environment]::SetEnvironmentVariable('RHEMA_GL_REHEARSAL_CONNECTION', $null, 'Process')
    [Environment]::SetEnvironmentVariable('RHEMA_GL_SOURCE_READONLY_CONNECTION', $null, 'Process')
    $nonEmptyEvidence = Join-Path ([System.IO.Path]::GetTempPath()) "RHEMAERP_GL_REHEARSAL_EVIDENCE_$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $nonEmptyEvidence | Out-Null
    Set-Content -LiteralPath (Join-Path $nonEmptyEvidence 'stale.txt') -Value 'stale'
    foreach ($legacyMode in @('RehearseEmpty','RehearseClone')) {
        $output = & pwsh -NoProfile -File $script -Mode $legacyMode 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0 -or $output -notmatch "$legacyMode is permanently disabled") {
            throw "$legacyMode did not refuse before connection lookup with its permanent docs-only message. Output: $output"
        }
        Write-Host "PASS: $legacyMode is hard-disabled before connection parsing"
    }

    $transportRoot = Join-Path ([System.IO.Path]::GetTempPath()) "RHEMAERP_GL_SQLCMD_TRANSPORT_$([guid]::NewGuid().ToString('N'))"
    $transportEvidence = Join-Path $transportRoot 'wide-evidence'
    $transportRiskEvidence = Join-Path $transportRoot 'limit-evidence'
    New-Item -ItemType Directory -Path $transportRoot | Out-Null
    @'
$captured = @($args)
$captured | Set-Content -Encoding ascii -LiteralPath $env:RHEMA_GL_SQLCMD_ARGUMENT_CAPTURE
$outputIndex = [Array]::IndexOf($captured, '-o')
if ($outputIndex -lt 0 -or $outputIndex + 1 -ge $captured.Count) { exit 91 }
$width = [int]$env:RHEMA_GL_SQLCMD_PROBE_WIDTH
if ($width -ge 8000) { $line = 'X' * 8000 }
else { $line = 'CANONICAL|' + ('X' * ($width - 30)) + '|TAIL_AFTER_4000' }

# Model sqlcmd's two independent lossy defaults. The probe succeeds intact only when the actual
# Invoke-Sql transport supplies both compatible, sufficiently wide options.
$variableIndex = [Array]::IndexOf($captured, '-y')
$variableWidth = if ($variableIndex -ge 0) { [int]$captured[$variableIndex + 1] } else { 256 }
if ($line.Length -gt $variableWidth) { $line = $line.Substring(0, $variableWidth) }
$screenIndex = [Array]::IndexOf($captured, '-w')
$screenWidth = if ($screenIndex -ge 0) { [int]$captured[$screenIndex + 1] } else { 80 }
$rendered = for ($offset = 0; $offset -lt $line.Length; $offset += $screenWidth) {
    $line.Substring($offset, [Math]::Min($screenWidth, $line.Length - $offset))
}
$rendered | Set-Content -Encoding utf8 -LiteralPath $captured[$outputIndex + 1]
exit 0
'@ | Set-Content -Encoding utf8 -LiteralPath (Join-Path $transportRoot 'sqlcmd.ps1')
    [Environment]::SetEnvironmentVariable('PATH', "$transportRoot$([System.IO.Path]::PathSeparator)$priorPath", 'Process')
    $capturePath = Join-Path $transportRoot 'arguments.txt'
    [Environment]::SetEnvironmentVariable('RHEMA_GL_SQLCMD_ARGUMENT_CAPTURE', $capturePath, 'Process')
    [Environment]::SetEnvironmentVariable('RHEMA_GL_SOURCE_READONLY_CONNECTION', 'Server=transport-probe;Database=RhemaERP;Integrated Security=true', 'Process')
    [Environment]::SetEnvironmentVariable('RHEMA_GL_SQLCMD_PROBE_WIDTH', '4500', 'Process')
    $output = & pwsh -NoProfile -File $script -Mode InspectSource -EvidenceDirectory $transportEvidence 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Offline sqlcmd transport probe failed: $output" }
    $transportArguments = @(Get-Content -LiteralPath $capturePath)
    $widthIndex = [Array]::IndexOf($transportArguments, '-y')
    $screenWidthIndex = [Array]::IndexOf($transportArguments, '-w')
    if ($widthIndex -lt 0 -or $transportArguments[$widthIndex + 1] -ne '8000' -or
        $screenWidthIndex -lt 0 -or $transportArguments[$screenWidthIndex + 1] -ne '8000' -or
        $transportArguments -ccontains '-W' -or $transportArguments -ccontains '-Y') {
        throw "Actual Invoke-Sql transport did not use compatible '-y 8000 -w 8000': $($transportArguments -join ' ')"
    }
    $wideEvidenceLines = @(Get-Content -LiteralPath (Join-Path $transportEvidence 'source-readiness.txt'))
    if ($wideEvidenceLines.Count -ne 1 -or $wideEvidenceLines[0].Length -le 4000 -or
        -not $wideEvidenceLines[0].EndsWith('TAIL_AFTER_4000', [StringComparison]::Ordinal)) {
        throw 'Actual Invoke-Sql transport truncated at 4,000 characters or screen-wrapped the canonical row.'
    }
    Write-Host 'PASS: actual Invoke-Sql transport retains a single canonical line and tail beyond character 4000'

    [Environment]::SetEnvironmentVariable('RHEMA_GL_SQLCMD_PROBE_WIDTH', '8000', 'Process')
    $output = & pwsh -NoProfile -File $script -Mode InspectSource -EvidenceDirectory $transportRiskEvidence 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0 -or $output -notmatch 'truncation cannot be excluded') {
        throw "Actual Invoke-Sql transport did not fail closed at the 8000-character display limit. Output: $output"
    }
    Write-Host 'PASS: actual Invoke-Sql transport refuses evidence at the display-width truncation boundary'

    @'
exit 0
'@ | Set-Content -Encoding utf8 -LiteralPath (Join-Path $transportRoot 'dotnet.ps1')
    $zeroOutputEvidence = Join-Path $transportRoot 'zero-output-command-evidence'
    $repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    $executedCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $executedTree = (& git -C $repositoryRoot rev-parse 'HEAD^{tree}').Trim()
    [Environment]::SetEnvironmentVariable('RHEMA_GL_REVIEWED_COMMIT', $executedCommit, 'Process')
    [Environment]::SetEnvironmentVariable('RHEMA_GL_REVIEWED_TREE', $executedTree, 'Process')
    foreach ($flagName in $flagNames) { [Environment]::SetEnvironmentVariable($flagName, 'false', 'Process') }
    [Environment]::SetEnvironmentVariable('RHEMA_GL_REHEARSAL_CONNECTION', 'Server=zero-output-host;Database=RHEMAERP_GL_REHEARSAL_ZERO_OUTPUT;Integrated Security=true', 'Process')
    [Environment]::SetEnvironmentVariable('RHEMA_GL_SOURCE_READONLY_CONNECTION', 'Server=zero-output-host;Database=RhemaERP;Integrated Security=true', 'Process')
    $output = & pwsh -NoProfile -File $script -Mode RehearseFinalClone -EvidenceDirectory $zeroOutputEvidence 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0 -or $output -notmatch 'Final clone assembly mismatch') {
        throw "Zero-output native evidence probe did not stop at the offline migration-count gate. Output: $output"
    }
    $gitDiffEvidence = @(Get-Content -LiteralPath (Join-Path $zeroOutputEvidence 'git-diff-check.log'))
    if ($gitDiffEvidence.Count -ne 1 -or
        $gitDiffEvidence[0] -cne 'RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=SUCCESS|EXIT_CODE=0|COMMAND=git') {
        throw 'Successful zero-output git diff --check did not atomically publish its explicit command evidence marker.'
    }
    if (@(Get-ChildItem -LiteralPath $zeroOutputEvidence -Filter '.git-diff-check.log.*.tmp' -Force).Count -ne 0) {
        throw 'Successful zero-output command evidence left an unpublished temporary file.'
    }
    Write-Host 'PASS: actual zero-output git command atomically publishes explicit sanitized success evidence'

    [Environment]::SetEnvironmentVariable('PATH', $priorPath, 'Process')
    [Environment]::SetEnvironmentVariable(
        'RHEMA_GL_REHEARSAL_CONNECTION',
        'Server=target-host;Database=RHEMAERP_GL_REHEARSAL_CLONE_SAFETY;Integrated Security=true',
        'Process')
    foreach ($name in @('RHEMA_GL_SQLCMD_ARGUMENT_CAPTURE','RHEMA_GL_SQLCMD_PROBE_WIDTH')) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
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

    $dirtyProbe = Join-Path $repositoryRoot 'gl-final-clone-dirty-probe.ps1'
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
        '$authoritativeMigrationCount = 1',
        '$sqlcmdMaxVariableWidth = 8000',
        '$sqlcmdScreenWidth = 8000',
        "'20260913162402_DisposableDevelopmentCurrentModelBaseline'",
        'Assert-TargetAbsent $target',
        'COPY_ONLY, CHECKSUM, NOINIT, NOSKIP, MEDIANAME=',
        '[System.IO.FileMode]::CreateNew',
        'BACKUP_PATH_ATOMICALLY_RESERVED',
        'RHEMA_IDEMPOTENT_SCRIPT_GENERATION_V1|STATUS=NOT_REQUIRED|REASON=ZERO_PENDING_MIGRATIONS|PENDING_COUNT=0',
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
    $legacyRefusalIndex = $scriptText.IndexOf("if (`$Mode -in @('RehearseEmpty', 'RehearseClone'))", [StringComparison]::Ordinal)
    $repositoryResolutionIndex = $scriptText.IndexOf('$repositoryRoot =', [StringComparison]::Ordinal)
    if ($legacyRefusalIndex -lt 0 -or $repositoryResolutionIndex -lt 0 -or
        $legacyRefusalIndex -ge $repositoryResolutionIndex -or
        $scriptText -match "if \(`$Mode -eq 'RehearseClone'\)" -or
        $scriptText -match 'INSERT INTO\s+\[dbo\]\.\[__EFMigrationsHistory\]') {
        throw 'Legacy RehearseEmpty/RehearseClone is not hard-disabled before connection-capable setup, or fake history stamping remains.'
    }
    $finalModeText = $scriptText
    if ($finalModeText -match 'COPY_ONLY,\s*CHECKSUM,\s*INIT\b' -or
        $finalModeText -notmatch 'COPY_ONLY,\s*CHECKSUM,\s*NOINIT,\s*NOSKIP,\s*MEDIANAME=') {
        throw 'Final clone backup must use the fresh unpredictable media identity and no-overwrite NOINIT/NOSKIP pattern.'
    }
    $reservationProbe = Join-Path ([System.IO.Path]::GetTempPath()) "RHEMAERP_GL_RESERVATION_$([Guid]::NewGuid().ToString('N')).bak"
    try {
        $firstReservation = [System.IO.File]::Open($reservationProbe, [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        $firstReservation.Dispose()
        $secondCreateRefused = $false
        try {
            $unexpected = [System.IO.File]::Open($reservationProbe, [System.IO.FileMode]::CreateNew,
                [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
            $unexpected.Dispose()
        }
        catch [System.IO.IOException] { $secondCreateRefused = $true }
        if (-not $secondCreateRefused) { throw 'Atomic CreateNew reservation did not refuse an existing backup path.' }
    }
    finally { if (Test-Path -LiteralPath $reservationProbe) { Remove-Item -LiteralPath $reservationProbe -Force } }
    Write-Host 'PASS: atomic backup reservation refuses an already-created exact path'
    $sourceReadinessText = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'sql\gl-source-readiness.sql')
    if ($sourceReadinessText -notmatch "ACTIVE_LEGACY_CURRENCY_LINKS'' AS FindingCode, N''REVIEW'' AS Severity" -or
        -not $scriptText.Contains("`$readinessText -match '(?im)\b(BLOCKER|REVIEW)\b'")) {
        throw 'Active legacy currency links are not guaranteed to stop final clone before backup for Phase 4 review.'
    }
    $syntheticLegacyFinding = 'ACTIVE_LEGACY_CURRENCY_LINKS REVIEW 1'
    if ($syntheticLegacyFinding -notmatch '(?im)\b(BLOCKER|REVIEW)\b') {
        throw 'Synthetic active legacy currency-link finding did not trigger the final pre-backup stop predicate.'
    }
    Write-Host 'PASS: active legacy currency links force REVIEW and the final pre-backup stop predicate'

    $finalInvariantText = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'sql\gl-final-clone-invariants.sql')
    foreach ($requiredText in @('20260913162402_DisposableDevelopmentCurrentModelBaseline', 'ACCOUNT|', 'ACCOUNT_SEGMENT_VALUE|',
        'ACCOUNT_BALANCE|', 'ACCOUNT_CURRENCY_EXPOSURE|', 'ACCOUNTING_BOOK_PERIOD|',
        'ACCOUNTING_BOOK_INITIALIZATION|', 'ACCOUNTING_BOOK_INITIALIZATION_LINE|', 'JOURNAL_ENTRY|',
        'ACCOUNT_TRANSACTION|', 'FINANCE_POSTING_EVENT|', 'APPLICABILITY_POLICY|', 'SELECTION_EVIDENCE|', 'ACCOUNTING_EVENT|', 'ACCOUNTING_EVENT_POSTING|',
        'AccountingEventProducerReceipts', 'PRODUCER_INTENT_GROUP|', 'PRODUCER_INTENT_GROUP_MEMBER|', 'CONTROL_COUNTS')) {
        if ($finalInvariantText -notmatch [regex]::Escape($requiredText)) {
            throw "Final-clone invariant contract is missing: $requiredText"
        }
    }
    if ($finalInvariantText -match 'seed must not create (journal|account transaction|posting event)') {
        throw 'Final-clone invariants incorrectly assume an empty historical business dataset.'
    }
    $canonicalConcatCount = [regex]::Matches($finalInvariantText, '(?i)\bCONCAT\(').Count
    $maxCanonicalConcatCount = [regex]::Matches(
        $finalInvariantText, "(?i)CONCAT\(CAST\(N'' AS nvarchar\(max\)\),").Count
    $sqlSideRowHashCount = [regex]::Matches(
        $finalInvariantText, "(?i)CONVERT\(varchar\(64\),HASHBYTES\('SHA2_256',CONCAT\(CAST\(N'' AS nvarchar\(max\)\),").Count
    $stableKeyedHashCount = [regex]::Matches(
        $finalInvariantText, "(?im)^SELECT\s+N'[^']+\|'\s+\+\s+CONVERT\(nvarchar\((?:36|150)\),[^\r\n]+\+\s+CONVERT\(varchar\(64\),HASHBYTES").Count
    if ($canonicalConcatCount -lt 20 -or $maxCanonicalConcatCount -ne $canonicalConcatCount -or
        $sqlSideRowHashCount -ne $canonicalConcatCount -or $stableKeyedHashCount -ne $canonicalConcatCount) {
        throw "Every canonical CONCAT must begin with nvarchar(max), be SHA2_256-hashed inside SQL, and retain a stable key. CONCAT=$canonicalConcatCount max=$maxCanonicalConcatCount hash=$sqlSideRowHashCount keyed=$stableKeyedHashCount"
    }
    Write-Host 'PASS: every canonical row uses nvarchar(max) CONCAT and a stable-keyed fixed SQL-side SHA2_256 hash'
    Write-Host 'PASS: final baseline, absent-target, no-overwrite, backup/restore/DBCC and preflight NO-GO contracts'
}
finally {
    [Environment]::SetEnvironmentVariable('PATH', $priorPath, 'Process')
    foreach ($name in @('RHEMA_GL_SQLCMD_ARGUMENT_CAPTURE','RHEMA_GL_SQLCMD_PROBE_WIDTH')) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    foreach ($name in @('RHEMA_GL_REHEARSAL_CONNECTION','RHEMA_GL_SOURCE_READONLY_CONNECTION') + $flagNames + $reviewNames) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    if ($nonEmptyEvidence -and (Test-Path -LiteralPath $nonEmptyEvidence)) {
        Remove-Item -LiteralPath $nonEmptyEvidence -Recurse -Force
    }
    if ($emptyEvidence -and (Test-Path -LiteralPath $emptyEvidence)) {
        Remove-Item -LiteralPath $emptyEvidence -Recurse -Force
    }
    if ($transportRoot -and (Test-Path -LiteralPath $transportRoot)) {
        Remove-Item -LiteralPath $transportRoot -Recurse -Force
    }
    if ($migrationParserEvidence -and (Test-Path -LiteralPath $migrationParserEvidence)) {
        Remove-Item -LiteralPath $migrationParserEvidence -Force
    }
}

Write-Host 'All GL cutover rehearsal safety refusals passed.'
