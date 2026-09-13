[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$dataProject = Join-Path $repositoryRoot 'src\ErpSystem.Data\ErpSystem.Data.csproj'
$migrationDirectory = Join-Path $repositoryRoot 'src\ErpSystem.Data\Migrations'
$archiveDirectory = Join-Path $repositoryRoot 'src\ErpSystem.Data\LegacyMigrationsArchive'
$baselineId = '20260913162402_DisposableDevelopmentCurrentModelBaseline'
$baselinePath = Join-Path $migrationDirectory "$baselineId.cs"
$designerPath = Join-Path $migrationDirectory "$baselineId.Designer.cs"
$snapshotPath = Join-Path $migrationDirectory 'ApplicationDbContextModelSnapshot.cs'
$authorityPath = Join-Path $migrationDirectory 'FinanceC1C8BaselineAuthoritySql.cs'

foreach ($requiredPath in @($dataProject,$baselinePath,$designerPath,$snapshotPath,$authorityPath,
    (Join-Path $archiveDirectory 'README.md'))) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Disposable-development baseline artifact is missing: $requiredPath"
    }
}

$projectText = Get-Content -Raw -LiteralPath $dataProject
if ($projectText -notmatch '<Compile Remove="LegacyMigrationsArchive\\\*\*\\\*\.cs"\s*/>' -or
    $projectText -match 'TdcFastEfBuild|RejectEfToolingBuild') {
    throw 'The legacy migration archive is not cleanly excluded from normal EF compilation.'
}

$archivedSources = @(Get-ChildItem -LiteralPath $archiveDirectory -File -Filter '*.cs')
$archivedMigrations = @($archivedSources | Where-Object {
    $_.Name -notlike '*.Designer.cs' -and
    $_.Name -notin @('ApplicationDbContextModelSnapshot.cs','FastBuildMigrationMetadata.cs')
})
if ($archivedSources.Count -ne 655 -or $archivedMigrations.Count -ne 456) {
    throw "The recoverable legacy archive is incomplete. Sources=$($archivedSources.Count); migrations=$($archivedMigrations.Count)."
}

$compiledDesignerFiles = @(Get-ChildItem -LiteralPath $migrationDirectory -File -Filter '*.Designer.cs')
$compiledMigrationIds = @($compiledDesignerFiles | ForEach-Object {
    $text = Get-Content -Raw -LiteralPath $_.FullName
    $match = [regex]::Match($text, '\[Migration\("(?<id>\d{14}_[A-Za-z0-9_]+)"\)\]')
    if (-not $match.Success) { throw "Compiled migration designer lacks a safe migration identity: $($_.Name)" }
    $match.Groups['id'].Value
})
if ($compiledMigrationIds.Count -ne 1 -or $compiledMigrationIds[0] -cne $baselineId) {
    throw "Compiled migration discovery is not exactly the disposable-development baseline: $($compiledMigrationIds -join ',')."
}

$baselineText = Get-Content -Raw -LiteralPath $baselinePath
$upEnd = $baselineText.IndexOf('protected override void Down', [StringComparison]::Ordinal)
if ($upEnd -lt 0) { throw 'The disposable-development baseline has no Down boundary.' }
$upText = $baselineText.Substring(0, $upEnd)
if ($upText -match '\bDrop(?:Table|Column|ForeignKey|Index|PrimaryKey|UniqueConstraint)\s*\(' -or
    $upText -match '\bUpdateData\s*\(') {
    throw 'The zero-to-current baseline contains predecessor-dependent drop/update operations.'
}
if ([regex]::Matches($upText, 'FinanceC1C8BaselineAuthoritySql\.Apply\(migrationBuilder\)').Count -ne 1) {
    throw 'The disposable-development baseline does not apply C5-C8 database-only authority exactly once.'
}

$requiredTables = @(
    'AccountClassifications','AccountBalances','AccountCurrencyExposures','AccountingBooks',
    'AccountingBookPeriods','AccountingBookInitializations','AccountingBookInitializationLines',
    'AccountingBookApplicabilityPolicies','AccountingBookApplicabilityRules','AccountingBookSelectionEvidence',
    'AccountingEvents','AccountingEventPostings','AccountingEventAttempts','AccountingEventProducerReceipts',
    'ProducerIntentGroups','ProducerIntentGroupMembers','ProducerIntentGroupReceipts','ProducerIntentGroupAttempts'
)
foreach ($table in $requiredTables) {
    if ($upText -notmatch [regex]::Escape("name: `"$table`"")) {
        throw "The disposable-development baseline lacks required C1-C8 table '$table'."
    }
}
foreach ($requiredModelToken in @(
    'IX_AccountClassifications_TenantId_AccountingBookId_SystemRole',
    'CK_Tenants_BaseCurrencyCanonical_C3',
    'CK_FinanceSettings_BaseCurrencyCanonical_C3')) {
    if ($upText -notmatch [regex]::Escape($requiredModelToken)) {
        throw "The disposable-development baseline lacks required Finance authority '$requiredModelToken'."
    }
}

$authorityText = Get-Content -Raw -LiteralPath $authorityPath
$authorityBatches = @([regex]::Matches($authorityText,
    'migrationBuilder\.Sql\(@"(?<sql>.*?)"\);',[Text.RegularExpressions.RegexOptions]::Singleline))
if ($authorityBatches.Count -ne 17 -or @($authorityBatches | Where-Object {
    $_.Groups['sql'].Value -cnotmatch '^(?:CREATE|ALTER) TRIGGER ' -or
    [regex]::Matches($_.Groups['sql'].Value,'(?m)^(?:CREATE|ALTER) TRIGGER ').Count -ne 1
}).Count -ne 0) {
    throw 'Every final C5-C8 trigger definition must be a distinct SQL operation beginning its own batch.'
}
$requiredTriggers = @(
    'TR_AccountingBookApplicabilityPolicies_C5Authority',
    'TR_AccountingBookApplicabilityRules_C5Immutable',
    'TR_AccountingBookApplicabilityRuleBooks_C5Immutable',
    'TR_AccountingBookSelectionEvidence_C5Immutable',
    'TR_AccountingBookSelectionEvidenceBooks_C5Immutable',
    'TR_AccountingEvents_C6Authority','TR_AccountingEventPostings_C6Authority',
    'TR_AccountingEventAttempts_C6AppendOnly','TR_AccountingEventProducerReceipts_C7Immutable',
    'TR_AccountingEvents_C7ProducerDecision','TR_ProducerIntentGroupMembers_C8Immutable',
    'TR_ProducerIntentGroupReceipts_C8Immutable','TR_ProducerIntentGroupAttempts_C8Immutable',
    'TR_ProducerIntentGroupAttempts_C8NoMutation','TR_ProducerIntentGroups_C8Authority'
)
foreach ($trigger in $requiredTriggers) {
    if ([regex]::Matches($authorityText, "(?:CREATE|ALTER) TRIGGER \[$([regex]::Escape($trigger))\]").Count -lt 1) {
        throw "The baseline authority SQL lacks required final trigger '$trigger'."
    }
}
foreach ($c7Trigger in @('TR_AccountingEventProducerReceipts_C7Immutable','TR_AccountingEvents_C7ProducerDecision')) {
    if ([regex]::Matches($authorityText, "ALTER TRIGGER \[$([regex]::Escape($c7Trigger))\]").Count -ne 1) {
        throw "C8 does not finalize C7 trigger '$c7Trigger' exactly once."
    }
}

Write-Host "PASS: exactly one compiled EF migration ($baselineId)"
Write-Host 'PASS: complete 456-migration source chain retained as an uncompiled recoverable archive'
Write-Host 'PASS: zero-to-current Up has no predecessor-dependent drops/updates'
Write-Host 'PASS: C1-C8 relational schema and final C5-C8 database trigger authority are preserved'
