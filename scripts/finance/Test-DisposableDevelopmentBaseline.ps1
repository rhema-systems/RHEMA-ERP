[CmdletBinding()]
param([string]$GeneratedSqlPath)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$dataProject = Join-Path $repositoryRoot 'src\ErpSystem.Data\ErpSystem.Data.csproj'
$migrationDirectory = Join-Path $repositoryRoot 'src\ErpSystem.Data\Migrations'
$archiveDirectory = Join-Path $repositoryRoot 'src\ErpSystem.Data\LegacyMigrationsArchive'
$supersededDirectory = Join-Path $repositoryRoot 'src\ErpSystem.Data\SupersededMigrationsArchive\PreBaselineLateMerges'
$baselineId = '20260916132000_DisposableDevelopmentCurrentModelBaseline'
$reconciliationId = '20260922210000_ReconcilePreBaselineLateMerges'
$baselinePath = Join-Path $migrationDirectory "$baselineId.cs"
$reconciliationPath = Join-Path $migrationDirectory "$reconciliationId.cs"
$designerPath = Join-Path $migrationDirectory "$baselineId.Designer.cs"
$snapshotPath = Join-Path $migrationDirectory 'ApplicationDbContextModelSnapshot.cs'
$authorityPath = Join-Path $migrationDirectory 'FinanceC1C8BaselineAuthoritySql.cs'
$governancePath = Join-Path $migrationDirectory 'ArchivedGovernanceBaselineSql.cs'
$governanceManifestPath = Join-Path $migrationDirectory 'ArchivedGovernanceBaselineManifest.json'
$archivedCheckModelPath = Join-Path $repositoryRoot 'src\ErpSystem.Data\Configuration\ArchivedCheckConstraintBaselineModel.cs'
$archivedC8Path = Join-Path $archiveDirectory '20260908120000_AddProducerIntentGroupsC8.cs'
$archivedPettyPath = Join-Path $archiveDirectory '20260907033000_AlignPettyPurchaseQuotationLifecycle.cs'
$inspectorProject = Join-Path $repositoryRoot 'scripts\finance\ArchivedMigrationSqlInspector\ArchivedMigrationSqlInspector.csproj'
$inspectorSource = Join-Path $repositoryRoot 'scripts\finance\ArchivedMigrationSqlInspector\Program.cs'

foreach ($requiredPath in @($dataProject,$baselinePath,$reconciliationPath,$designerPath,$snapshotPath,$authorityPath,$governancePath,
    $governanceManifestPath,$archivedCheckModelPath,$inspectorProject,$inspectorSource,$archivedC8Path,$archivedPettyPath,
    (Join-Path $archiveDirectory 'README.md'),(Join-Path $supersededDirectory 'README.md'))) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Disposable-development baseline artifact is missing: $requiredPath"
    }
}

$projectText = Get-Content -Raw -LiteralPath $dataProject
if ($projectText -notmatch '<Compile Remove="LegacyMigrationsArchive\\\*\*\\\*\.cs"\s*/>' -or
    $projectText -notmatch '<Compile Remove="SupersededMigrationsArchive\\\*\*\\\*\.cs"\s*/>' -or
    $projectText -notmatch '<Compile Include="SupersededMigrationsArchive\\PreBaselineLateMerges\\\*\.cs"' -or
    $projectText -notmatch 'Exclude="SupersededMigrationsArchive\\PreBaselineLateMerges\\\*\.Designer\.cs;SupersededMigrationsArchive\\PreBaselineLateMerges\\20260915093000_AddProcedureCaseOrganizationScope\.cs"' -or
    $projectText -match 'TdcFastEfBuild|RejectEfToolingBuild') {
    throw 'The legacy and superseded migration archives are not cleanly excluded from normal EF compilation.'
}

$expectedSupersededIds = @(
    '20260907003611_AddSheChecklistBuilder',
    '20260909004030_AddGuarantorIdTypeAndDocuments',
    '20260909013533_AddOrganizationUnitHistoryNotes',
    '20260909071641_AddCertificationModel',
    '20260909090940_AddProbationSourceAndContractKind',
    '20260909133438_AddEmployeePayBasis',
    '20260909161346_AddSalaryAssignmentWithdrawal',
    '20260909204655_AddSalaryStructurePolicy',
    '20260909225805_AddNamedSets',
    '20260910001423_AddUnitFinanceAccount',
    '20260910013025_AddSubRecordGeographyAndRelationshipTypes',
    '20260910023951_AddTeamTermsObjectivesAndTasks',
    '20260910031733_AddTeamMeetingsAndReviews',
    '20260910043146_AddTeamApprovalRejectionReasons',
    '20260910163909_AddManpowerBudgetLineSalaryScale',
    '20260910201924_AddStaffRequisitionBudgetLink',
    '20260910205043_AddStaffRequisitionCostPayeeAndApproval',
    '20260911091344_AddEmployeeSalaryChangeRequest',
    '20260911111931_AddCandidateIdentityLanguagesAndCertification',
    '20260912211938_AddCriteriaCatalogueValues',
    '20260913161339_AddPreEmploymentCheckProviders',
    '20260913181139_AddUnionContactsDocumentsLogo',
    '20260913214947_AddJobDescriptionProposedGrade',
    '20260913224857_AddDisabilityTypes',
    '20260913234443_DropContractLeaveColumns',
    '20260914063456_AddTalentSegmentOwnership',
    '20260915093000_AddProcedureCaseOrganizationScope'
)
$supersededSources = @(Get-ChildItem -LiteralPath $supersededDirectory -File -Filter '*.cs')
$supersededIds = @($supersededSources | Where-Object Name -notlike '*.Designer.cs' |
    ForEach-Object { $_.BaseName } | Sort-Object)
if ($supersededSources.Count -ne 53 -or $supersededIds.Count -ne 27 -or
    ($supersededIds -join "`n") -cne (($expectedSupersededIds | Sort-Object) -join "`n")) {
    throw 'The exact 27 late-merged pre-baseline migrations are not preserved in the superseded archive.'
}

$reconciliationText = Get-Content -Raw -LiteralPath $reconciliationPath
$expectedReplayClasses = @($expectedSupersededIds | Where-Object {
    $_ -ne '20260915093000_AddProcedureCaseOrganizationScope'
} | ForEach-Object { $_ -replace '^\d{14}_', '' })
foreach ($className in $expectedReplayClasses) {
    if ($reconciliationText -notmatch (':\s*' + [regex]::Escape($className) + '\b')) {
        throw "The post-baseline reconciliation does not replay $className."
    }
}
if ($expectedReplayClasses.Count -ne 26 -or
    [regex]::Matches($reconciliationText, '\.Apply\(migrationBuilder\);').Count -ne 26 -or
    $reconciliationText -notmatch ('\[Migration\("' + [regex]::Escape($reconciliationId) + '"\)\]') -or
    $reconciliationText -match ':\s*AddProcedureCaseOrganizationScope\b') {
    throw 'The post-baseline reconciliation must replay exactly the 26 guarded omissions and leave the baseline-owned ProcedureCases scope alone.'
}

$archivedSources = @(Get-ChildItem -LiteralPath $archiveDirectory -File -Filter '*.cs')
$archivedMigrations = @($archivedSources | Where-Object {
    $_.Name -notlike '*.Designer.cs' -and
    $_.Name -notin @('ApplicationDbContextModelSnapshot.cs','FastBuildMigrationMetadata.cs')
})
if ($archivedSources.Count -ne 889 -or $archivedMigrations.Count -ne 607) {
    throw "The recoverable legacy archive is incomplete. Sources=$($archivedSources.Count); migrations=$($archivedMigrations.Count)."
}
$archiveIds = @($archivedMigrations | ForEach-Object { $_.BaseName } | Sort-Object)
$inspectorIdOutput = @(& dotnet run --project $inspectorProject -c Release -- --migration-ids 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "Archived migration identity inspection failed:`n$($inspectorIdOutput -join "`n")"
}
$inspectorIds = @($inspectorIdOutput | Where-Object { $_ -cmatch '^\d{14}_[A-Za-z0-9_]+$' } | Sort-Object)
if ($archiveIds.Count -ne 607 -or @($archiveIds | Sort-Object -Unique).Count -ne 607 -or
    $inspectorIds.Count -ne 607 -or ($archiveIds -join "`n") -cne ($inspectorIds -join "`n")) {
    throw 'The archived source filenames and executable inspector identities are not the exact same 607-ID set.'
}
$lifecycleMutationOutput = @(& dotnet run --project $inspectorProject -c Release --no-build -- --self-test-lifecycle 2>&1)
if ($LASTEXITCODE -ne 0 -or
    @($lifecycleMutationOutput | Where-Object { $_ -cmatch '^PASS: ' }).Count -ne 3) {
    throw "Archived lifecycle/provenance mutation tests failed:`n$($lifecycleMutationOutput -join "`n")"
}
$d3ArchiveDirectory = Join-Path $archiveDirectory 'D3BaselineArchive'
$expectedD3Hashes = [ordered]@{
    '20260913162402_DisposableDevelopmentCurrentModelBaseline.cs'='58CB4CD0EE9E140041CFDC4CD231BD65CD89F0F15B7316B907F01420C15067D6'
    '20260913162402_DisposableDevelopmentCurrentModelBaseline.Designer.cs'='3B727FE3593C8BD604E49DE25B63C35DFE3CB0EE3A8C868670D5EE3D68000BC9'
    'ApplicationDbContextModelSnapshot.cs'='387899CE97A627B6B0364873C9A55EAA7BE7A19E68DF786F48C9AC2B8A69784F'
}
if (@(Get-ChildItem -LiteralPath $d3ArchiveDirectory -File).Count -ne 3) {
    throw 'The exact reviewed D3 baseline archive must contain only its migration, designer, and snapshot.'
}
foreach ($entry in $expectedD3Hashes.GetEnumerator()) {
    $path = Join-Path $d3ArchiveDirectory $entry.Key
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $entry.Value) {
        throw "The exact reviewed D3 baseline archive changed: $($entry.Key)."
    }
}

$mergedArchiveDirectory = Join-Path $archiveDirectory 'MergedBaseline20260916PreRegeneration'
$expectedMergedArchiveHashes = [ordered]@{
    '20260913162402_DisposableDevelopmentCurrentModelBaseline.cs'='E1A6053AE340CBE10AA7756A48879445F013ADA3D3EAA37148454B4716CF860D'
    '20260913162402_DisposableDevelopmentCurrentModelBaseline.Designer.cs'='BE3F32C1AAE3CC8171E7C018411622A4310A6FE9CCF7B20E98AE9AA64B43D338'
    'ApplicationDbContextModelSnapshot.cs'='E2279643D0C646DE25140ECDCFE720E66E6DF970ED215492BE4247EB222E3B8F'
}
if (@(Get-ChildItem -LiteralPath $mergedArchiveDirectory -File).Count -ne 3) {
    throw 'The pre-regeneration merged baseline archive must contain only its migration, designer, and snapshot.'
}
foreach ($entry in $expectedMergedArchiveHashes.GetEnumerator()) {
    $path = Join-Path $mergedArchiveDirectory $entry.Key
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $entry.Value) {
        throw "The pre-regeneration merged baseline archive changed: $($entry.Key)."
    }
}
$preD3Snapshot = Join-Path $archiveDirectory 'PreD3HistoricalSnapshot\ApplicationDbContextModelSnapshot.cs'
if (-not (Test-Path -LiteralPath $preD3Snapshot -PathType Leaf) -or
    (Get-FileHash -LiteralPath $preD3Snapshot -Algorithm SHA256).Hash -cne
    '1C6A764DDBAF2956F27F78CDE8E9C90EB73CEA0EC3C8CC844FDCE11D79BA8C9F') {
    throw 'The byte-preserved pre-D3 archive snapshot changed.'
}

$compiledMigrationIds = @(Get-ChildItem -LiteralPath $migrationDirectory -File -Filter '*.cs' | ForEach-Object {
    $text = Get-Content -Raw -LiteralPath $_.FullName
    $match = [regex]::Match($text, '\[Migration\("(?<id>\d{14}_[A-Za-z0-9_]+)"\)\]')
    if ($match.Success) { $match.Groups['id'].Value }
} | Sort-Object)
if ($compiledMigrationIds.Count -lt 1 -or $compiledMigrationIds[0] -cne $baselineId -or
    $compiledMigrationIds -cnotcontains $reconciliationId -or
    @($compiledMigrationIds | Sort-Object -Unique).Count -ne $compiledMigrationIds.Count) {
    throw "Compiled migration discovery does not start with the unique disposable-development baseline: $($compiledMigrationIds -join ',')."
}

$baselineText = Get-Content -Raw -LiteralPath $baselinePath
$transactionalSources = @($baselinePath,$authorityPath,$governancePath)
foreach ($transactionalSource in $transactionalSources) {
    if ((Get-Content -Raw -LiteralPath $transactionalSource) -match 'suppressTransaction\s*:\s*true') {
        throw "Disposable-development baseline authority escapes the sole migration transaction: $transactionalSource"
    }
}
$upEnd = $baselineText.IndexOf('protected override void Down', [StringComparison]::Ordinal)
if ($upEnd -lt 0) { throw 'The disposable-development baseline has no Down boundary.' }
$upText = $baselineText.Substring(0, $upEnd)
if ($upText -match '\bDrop(?:Table|Column|ForeignKey|Index|PrimaryKey|UniqueConstraint)\s*\(' -or
    $upText -match '\bUpdateData\s*\(') {
    throw 'The zero-to-current baseline contains predecessor-dependent drop/update operations.'
}
$procedureCaseCreate = $upText.IndexOf('name: "ProcedureCases"', [StringComparison]::Ordinal)
$organizationLevelCreate = $upText.IndexOf('name: "OrganizationLevels"', [StringComparison]::Ordinal)
$organizationUnitCreate = $upText.IndexOf('name: "OrganizationUnits"', [StringComparison]::Ordinal)
$procedureLevelForeignKey = $upText.LastIndexOf('name: "FK_ProcedureCases_OrganizationLevels_OrganizationLevelId"', [StringComparison]::Ordinal)
$procedureUnitForeignKey = $upText.LastIndexOf('name: "FK_ProcedureCases_OrganizationUnits_OrganizationUnitId"', [StringComparison]::Ordinal)
if ($procedureCaseCreate -lt 0 -or $organizationLevelCreate -le $procedureCaseCreate -or
    $organizationUnitCreate -le $organizationLevelCreate -or
    $procedureLevelForeignKey -le $organizationLevelCreate -or
    $procedureUnitForeignKey -le $organizationUnitCreate) {
    throw 'ProcedureCases organization foreign keys must be added only after both principal tables exist.'
}
if ([regex]::Matches($upText, 'ArchivedGovernanceBaselineSql\.Apply\(migrationBuilder\)').Count -ne 1 -or
    [regex]::Matches($upText, 'FinanceC1C8BaselineAuthoritySql\.Apply\(migrationBuilder\)').Count -ne 1 -or
    $upText.IndexOf('ArchivedGovernanceBaselineSql.Apply(migrationBuilder)', [StringComparison]::Ordinal) -ge
        $upText.IndexOf('FinanceC1C8BaselineAuthoritySql.Apply(migrationBuilder)', [StringComparison]::Ordinal)) {
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

$baselineDesignerText = Get-Content -Raw -LiteralPath $designerPath
$baselineModelTriggerNames = @([regex]::Matches($baselineDesignerText, 'HasTrigger\("(?<name>[^"]+)"\)') |
    ForEach-Object { $_.Groups['name'].Value } | Sort-Object -Unique)
$snapshotText = Get-Content -Raw -LiteralPath $snapshotPath
$currentModelTriggerNames = @([regex]::Matches($snapshotText, 'HasTrigger\("(?<name>[^"]+)"\)') |
    ForEach-Object { $_.Groups['name'].Value } | Sort-Object -Unique)
$governanceManifest = Get-Content -Raw -LiteralPath $governanceManifestPath | ConvertFrom-Json
$archivedCheckModelText = Get-Content -Raw -LiteralPath $archivedCheckModelPath
$manifestTriggerNames = @($governanceManifest.triggerDefinitions | ForEach-Object { [string]$_.Name } | Sort-Object -Unique)
$manifestModelTriggerNames=@($governanceManifest.modelTriggerNames|Sort-Object -Unique)
$activeNonModelTriggerNames=@($governanceManifest.activeNonModelTriggerNames|Sort-Object -Unique)
if ($governanceManifest.schema -cne 'RHEMA_DISPOSABLE_BASELINE_GOVERNANCE_V2' -or
    $baselineModelTriggerNames.Count -ne 386 -or $manifestModelTriggerNames.Count -ne 386 -or
    ($baselineModelTriggerNames -join "`n") -cne ($manifestModelTriggerNames -join "`n") -or
    $manifestTriggerNames.Count -ne 478 -or $activeNonModelTriggerNames.Count -ne 92 -or
    [int]$governanceManifest.archiveMigrationCount -ne 607 -or
    [int]$governanceManifest.archiveSqlOperationCount -ne 1818 -or
    [int]$governanceManifest.archivedUniqueTriggerCount -ne 488 -or
    [int]$governanceManifest.finalUniqueTriggerCount -ne 493 -or
    [int]$governanceManifest.baselineTableCount -ne 1630 -or
    [int]$governanceManifest.archivedCheckConstraintCount -ne 675 -or
    [int]$governanceManifest.baselineCheckConstraintCount -ne 835 -or
    [int]$governanceManifest.archivedCheckConstraintLifecycleEventCount -ne 872 -or
    @($governanceManifest.checkConstraintLifecycle).Count -ne 872 -or
    @($governanceManifest.finalArchivedCheckConstraints).Count -ne 675 -or
    @($governanceManifest.finalArchivedCheckConstraints | ForEach-Object { "$($_.Table)|$($_.Name)" } | Sort-Object -Unique).Count -ne 675 -or
    [int]$governanceManifest.staticallyValidatedColumnReferenceCount -ne 16212) {
    throw 'Archived governance manifest lacks exact model and active non-model trigger coverage.'
}
$missingCurrentModelTriggers = @($baselineModelTriggerNames | Where-Object {
    $currentModelTriggerNames -cnotcontains $_
})
if ($missingCurrentModelTriggers.Count -ne 0) {
    throw "The current model snapshot dropped baseline trigger annotations: $($missingCurrentModelTriggers -join ',')."
}
$dispositions=@($governanceManifest.archivedTriggerDisposition)
$expectedDispositions=[ordered]@{ACTIVE_NON_MODEL=92;CURRENT_MODEL=384;SEPARATE_FINANCE_AUTHORITY=10;SUPERSEDED_BY_EXACT_MODEL_ALIAS=2}
foreach($entry in $expectedDispositions.GetEnumerator()){
    if(@($dispositions|Where-Object disposition -ceq $entry.Key).Count -ne $entry.Value){
        throw "Archived trigger disposition is not exhaustive for $($entry.Key)."
    }
}

$governanceText = Get-Content -Raw -LiteralPath $governancePath
$archivedPettyText = Get-Content -Raw -LiteralPath $archivedPettyPath
if ([regex]::Matches($archivedPettyText,
        'ALTER TABLE dbo\.ProcurementExceptionalSourcingControls DROP CONSTRAINT CK_ProcurementExceptionalSourcingControls_Lifecycle').Count -ne 1 -or
    $governanceText -match '(?im)^\s*ALTER\s+TABLE\b' -or
    $governanceText -match 'migration:WarehouseDefaultLocations|ALTER\s+COLUMN\s+UnitCost' -or
    [regex]::Matches($governanceText,
        'CREATE OR ALTER FUNCTION dbo\.InventoryAdjustmentExpectedUnitCost').Count -ne 1) {
    throw 'The empty-schema governance helper retained mixed predecessor DDL/backfill or duplicated its final valuation function.'
}
$lifecycleEvents = @($governanceManifest.checkConstraintLifecycle)
if ((@($lifecycleEvents | ForEach-Object { [int]$_.Sequence }) -join ',') -cne ((0..871) -join ',') -or
    @($lifecycleEvents | Where-Object {
        $_.Action -cnotin @('ADD','DROP','PATCH') -or
        [string]$_.Table -cnotmatch '^[A-Za-z0-9_]+$' -or
        [string]$_.Name -cnotmatch '^CK_[A-Za-z0-9_]+$' -or
        [string]$_.OperationSha256 -cnotmatch '^[0-9A-F]{64}$'
    }).Count -ne 0) {
    throw 'Archived check lifecycle event order, identity, action, or source-operation binding changed.'
}
if ([regex]::Matches($archivedCheckModelText,'(?m)^\s*Apply\(modelBuilder, "').Count -ne 675 -or
    $archivedCheckModelText -cnotmatch 'CK_ProcurementExceptionalSourcingControls_Lifecycle' -or
    $archivedCheckModelText -cnotmatch 'CK_PhysicalCountActions_ActionType' -or
    $archivedCheckModelText -cnotmatch 'ActionType BETWEEN 1 AND 16' -or
    $archivedCheckModelText -cnotmatch 'CK_PettyPurchase_Authority') {
    throw 'The declarative archived-final check-constraint model is incomplete or stale.'
}
$governanceBatches = @([regex]::Matches($governanceText,
    'migrationBuilder\.Sql\(\s*"""(?<sql>.*?)"""\);',[Text.RegularExpressions.RegexOptions]::Singleline))
$triggerBatches = @($governanceBatches | Where-Object {
    $_.Groups['sql'].Value -match '(?im)^\s*CREATE OR ALTER TRIGGER\s+'
})
if ($governanceBatches.Count -ne 592 -or $triggerBatches.Count -ne 478 -or
    @($governanceBatches | Where-Object {
        [regex]::Matches($_.Groups['sql'].Value,'(?im)^\s*CREATE OR ALTER TRIGGER\s+').Count -gt 1
    }).Count -ne 0) {
    throw 'Archived governance SQL is not one isolated operation per active archived trigger.'
}
if ([regex]::Matches($governanceText,'(?im)^\s*CREATE OR ALTER FUNCTION\s+').Count -ne 5 -or
    [regex]::Matches($governanceText,'(?im)^\s*CREATE OR ALTER VIEW\s+').Count -ne 1 -or
    @($governanceManifest.programmableObjects).Count -ne 6 -or
    @($governanceManifest.postDefinitionPatches).Count -ne 108) {
    throw 'Archived raw-SQL governance audit did not retain the five functions, one view, and 108 final trigger patches.'
}
$patchProvenance = @($governanceManifest.postDefinitionPatches)
$mixedPatchProvenance = @($patchProvenance | Where-Object transformation -ceq 'EXACT_SUFFIX_FROM_UNIQUE_MARKER_V1')
$identityPatchProvenance = @($patchProvenance | Where-Object transformation -ceq 'IDENTITY_FULL_SQL_OPERATION')
if ($mixedPatchProvenance.Count -ne 4 -or $identityPatchProvenance.Count -ne 104 -or
    @($patchProvenance | Where-Object {
        [string]$_.sourceOperationSha256 -cnotmatch '^[0-9A-F]{64}$' -or
        [string]$_.retainedFragmentSha256 -cnotmatch '^[0-9A-F]{64}$' -or
        $_.retainedFragmentSha256 -cne $_.sqlSha256 -or
        [int]$_.sourceNormalizedLength -le 0 -or [int]$_.retainedFragmentLength -le 0 -or
        [int]$_.retainedFragmentStart -lt 0 -or
        [int]$_.retainedFragmentStart + [int]$_.retainedFragmentLength -ne [int]$_.sourceNormalizedLength
    }).Count -ne 0 -or
    @($identityPatchProvenance | Where-Object {
        [int]$_.retainedFragmentStart -ne 0 -or $_.sourceMarker -cne 'FULL_OPERATION' -or
        $_.sourceOperationSha256 -cne $_.retainedFragmentSha256
    }).Count -ne 0) {
    throw 'Post-definition patch full-source/fragment provenance is incomplete or ambiguous.'
}
$expectedMixedPatchSources = [ordered]@{
    '20260907033000_AlignPettyPurchaseQuotationLifecycle'='E3566BFDC587FE9C1F8F8C6504F7D66707A4DF6D56CC9409720AE54EB625E42F'
    '20260911210000_PhysicalCountReviewDecisions'='D3872BB4125FB2F23A873A5D127DCA9F39C3DDAB6529D402BE9866D641ADA727'
    '20260912003000_WarehouseDefaultLocations'='9FF54D977BCE5AB2F3F7847E60294EF4FCDC88606998F61BAFEC5F6DF655019B'
    '20260912013000_AlignStockAdjustmentLocationValuation'='A51CD64C67710435EC1845947800DA9E6FEEF34A1855FBCFA6FE7E353CD9D6DA'
}
foreach ($entry in $expectedMixedPatchSources.GetEnumerator()) {
    $match = @($mixedPatchProvenance | Where-Object MigrationId -ceq $entry.Key)
    if ($match.Count -ne 1 -or $match[0].sourceOperationSha256 -cne $entry.Value -or
        $match[0].sourceMarker -cnotmatch '^DECLARE @[A-Za-z]+$') {
        throw "Mixed patch full-source provenance changed: $($entry.Key)."
    }
}
$expectedProgrammableNames = @(
    'InventoryAdjustmentExpectedLineValue','InventoryAdjustmentExpectedUnitCost',
    'WorkflowApprovalEntityKey','WorkflowApprovalRequiredAtSubmission',
    'fn_ProcurementRfqSourceLineIdentity','vw_ProcurementReceiptDocumentReconciliation'
) | Sort-Object
$programmableDisposition = @($governanceManifest.programmableObjectDisposition)
if ($programmableDisposition.Count -ne 6 -or
    @($programmableDisposition | Where-Object disposition -cne 'ACTIVE_FINAL_DEFINITION').Count -ne 0 -or
    ((@($programmableDisposition | ForEach-Object { [string]$_.Name } | Sort-Object)) -join "`n") -cne
        ($expectedProgrammableNames -join "`n")) {
    throw 'The archived programmable-object lifecycle audit is not exhaustive or retains a later-dropped object.'
}
$representativeHashes = [ordered]@{
    TR_AuditLogs_AppendOnly='CA1BD7583EBD5B50B1637A9DA26F691337CC28AC5FB40A8614122BD133CF00AC'
    TR_InventoryTransfers_ControlledLifecycle='5B47F68FB5325AB42C295F0A6CEADD7F2747277A963492B0274C0083EF893532'
    TR_PurchaseOrders_ApprovedSourceProtected='03877199F3932A4119A987C3546BAE1A683B7324CCDD37548C46C2A54BE5AB5E'
    TR_ProjectCivilDirectTaskControls_Lifecycle='DE3E27771997B240E8780125BFFBE9C7F9721BF0885D098161EA6B53EA3FD6B8'
    TR_VendorPaymentAllocation_TDC0505PaymentReadiness='2EC1FE4A63C27772227B05F1C0602417BF54D893932DD10EBAC3292FDBDF700C'
    TR_ProcurementSourcingCaseLots_NoMutation='FC3DCD2EFE219E37776637383A90EAC62CF087F3D87A28711F6BD4F616F2DEBD'
    TR_PurchaseOrders_ApprovedCommercialCapacity='335301A2C8C313209B7835BB181AA45B64F481A7B36E4EA459F62968B8A8DB0D'
    TR_PurchaseOrderItems_ApprovedCommercialCapacity='9038DBBBA6696FCB342ED4B7E1E8BEB9371E6E2041055F033D8340236F02F749'
}
$commercialPatchHashes=[ordered]@{
    TR_PurchaseOrders_ApprovedCommercialCapacity='00F5A1DE8421069AE425C1D5C4FAC13B726D63BD0E3A93758663F6C0AFC8B8F5'
    TR_PurchaseOrderItems_ApprovedCommercialCapacity='7F014663802EE86FF86614BC3D70079935ACAECA2535862430C8C13EAFD0F738'
}
foreach($entry in $commercialPatchHashes.GetEnumerator()){
    $patch=@($governanceManifest.postDefinitionPatches|Where-Object {
        $_.TargetNames -contains $entry.Key -and
        $_.MigrationId -ceq '20260827211500_AlignRfqCommercialIdentityWithReceiptItemMaster'
    })
    if($patch.Count -ne 1 -or $patch[0].MigrationId -cne '20260827211500_AlignRfqCommercialIdentityWithReceiptItemMaster' -or
        $patch[0].sqlSha256 -cne $entry.Value){throw "Commercial-capacity trigger patch provenance changed: $($entry.Key)."}
}
foreach ($entry in $representativeHashes.GetEnumerator()) {
    $definition = @($governanceManifest.triggerDefinitions | Where-Object Name -ceq $entry.Key)
    if ($definition.Count -ne 1 -or [string]$definition[0].bodySha256 -cne $entry.Value) {
        throw "Representative archived trigger body changed: $($entry.Key)."
    }
}

$authorityText = Get-Content -Raw -LiteralPath $authorityPath
$archivedC8Text = Get-Content -Raw -LiteralPath $archivedC8Path
$badC8Boundary = "WHERE m.[TenantId]=g.[TenantId] AND m.[ProducerIntentGroupId]=g.[Id] AND e.[Status]<>N'Posted'))))`n  THROW 51000, 'C8_ATTEMPT_AUTHORITY: one exact terminal attempt must atomically drive an authorized group transition.', 1;"
$correctedC8Boundary = $badC8Boundary.Replace("N'Posted'))))", "N'Posted')))))")
if (([regex]::Matches($archivedC8Text.Replace("`r`n","`n"), [regex]::Escape($badC8Boundary))).Count -ne 1 -or
    ([regex]::Matches($authorityText.Replace("`r`n","`n"), [regex]::Escape($correctedC8Boundary))).Count -ne 1 -or
    $authorityText -cnotmatch 'Baseline-only grammar correction for archived 20260908120000_AddProducerIntentGroupsC8') {
    throw 'The disposable baseline does not bind the exact archived C8 grammar defect to its semantics-preserving correction.'
}
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

if ($GeneratedSqlPath) {
    $resolvedGeneratedSql = (Resolve-Path -LiteralPath $GeneratedSqlPath).Path
    $generatedSql = Get-Content -Raw -LiteralPath $resolvedGeneratedSql
    $generatedTriggerMatches = @([regex]::Matches($generatedSql,
        '(?im)^\s*CREATE\s+(?:OR\s+ALTER\s+)?TRIGGER\s+(?:(?:\[dbo\]|dbo)\.)?(?:\[(?<bracketed>[^\]]+)\]|(?<plain>[A-Za-z0-9_]+))'))
    $generatedTriggerNames = @($generatedTriggerMatches | ForEach-Object {
        if ($_.Groups['bracketed'].Success) { $_.Groups['bracketed'].Value } else { $_.Groups['plain'].Value }
    })
    $expectedGeneratedTriggerNames = @($manifestTriggerNames + $requiredTriggers | Sort-Object -Unique)
    if ($generatedTriggerNames.Count -ne 493 -or
        @($generatedTriggerNames | Sort-Object -Unique).Count -ne 493 -or
        (($generatedTriggerNames | Sort-Object) -join "`n") -cne ($expectedGeneratedTriggerNames -join "`n")) {
        throw 'Generated zero-to-current SQL does not contain the exact active archived + C5-C8 trigger set.'
    }
    $generatedBatches = @([regex]::Split($generatedSql, '(?im)^\s*GO\s*$'))
    if (@($generatedBatches | Where-Object {
        [regex]::Matches($_, '(?im)^\s*CREATE\s+(?:OR\s+ALTER\s+)?TRIGGER\s+').Count -gt 1
    }).Count -ne 0) {
        throw 'Generated zero-to-current SQL combines multiple trigger definitions in one executable batch.'
    }
    $createdTablePositions = @{}
    foreach ($createdTable in [regex]::Matches($generatedSql,
        '(?im)^\s*CREATE\s+TABLE\s+(?:(?:\[dbo\]|dbo)\.)?\[(?<name>[^\]]+)\]')) {
        $createdTablePositions[$createdTable.Groups['name'].Value] = $createdTable.Index
    }
    $lateForeignKeyPrincipals = @([regex]::Matches($generatedSql,
        '(?im)\bREFERENCES\s+(?:(?:\[dbo\]|dbo)\.)?\[(?<name>[^\]]+)\]') | Where-Object {
            $principalName = $_.Groups['name'].Value
            $createdTablePositions.ContainsKey($principalName) -and
            [int]$createdTablePositions[$principalName] -gt $_.Index
        } | ForEach-Object { $_.Groups['name'].Value } | Sort-Object -Unique)
    if ($lateForeignKeyPrincipals.Count -ne 0) {
        throw "Generated zero-to-current SQL references principal tables before creation: $($lateForeignKeyPrincipals -join ',')."
    }
    if ([regex]::Matches($generatedSql,'(?im)^\s*CREATE\s+(?:OR\s+ALTER\s+)?FUNCTION\s+(?:\[dbo\]|dbo)\.(?:\[(?:InventoryAdjustmentExpectedLineValue|InventoryAdjustmentExpectedUnitCost|WorkflowApprovalEntityKey|WorkflowApprovalRequiredAtSubmission|fn_ProcurementRfqSourceLineIdentity)\]|(?:InventoryAdjustmentExpectedLineValue|InventoryAdjustmentExpectedUnitCost|WorkflowApprovalEntityKey|WorkflowApprovalRequiredAtSubmission|fn_ProcurementRfqSourceLineIdentity))').Count -ne 5 -or
        [regex]::Matches($generatedSql,'(?im)^\s*CREATE\s+(?:OR\s+ALTER\s+)?VIEW\s+\[dbo\]\.\[vw_ProcurementReceiptDocumentReconciliation\]').Count -ne 1 -or
        @($generatedBatches | Where-Object { $_ -match 'OBJECT_DEFINITION\s*\(' }).Count -ne 108 -or
        [regex]::Matches($generatedSql,[regex]::Escape($baselineId)).Count -ne 1) {
        throw 'Generated zero-to-current SQL omits or duplicates audited governance objects, patches, or baseline history.'
    }
    if ($generatedSql -match '(?im)^\s*ALTER\s+TABLE\s+.+\s+DROP\s+CONSTRAINT\b' -or
        $generatedSql -match 'migration:WarehouseDefaultLocations|ALTER\s+COLUMN\s+\[?UnitCost\]?\s+decimal\(18,4\)' -or
        [regex]::Matches($generatedSql,'CK_ProcurementExceptionalSourcingControls_Lifecycle').Count -ne 1 -or
        [regex]::Matches($generatedSql,'CK_PhysicalCountActions_ActionType').Count -ne 1) {
        throw 'Generated zero-to-current SQL replays a predecessor constraint/backfill/column mutation or duplicates final checks.'
    }
    $grammarOutput = @(& dotnet run --project $inspectorProject -c Release -- --verify-generated-sql $resolvedGeneratedSql 2>&1)
    if ($LASTEXITCODE -ne 0 -or
        @($grammarOutput | Where-Object { $_ -ceq 'PASS: generated SQL parses with TSql160Parser; THROW_STATEMENTS=1502' }).Count -ne 1 -or
        @($grammarOutput | Where-Object { $_ -ceq 'PASS: archived C8 missing-parenthesis boundary reproduces TSql160Parser grammar error 46005 near THROW' }).Count -ne 1) {
        throw "Generated zero-to-current SQL failed the deterministic T-SQL grammar gate:`n$($grammarOutput -join "`n")"
    }
    Write-Host 'PASS: generated zero-to-current SQL has exact isolated 493-trigger and audited object authority'
    $grammarOutput | Where-Object { $_ -cmatch '^PASS:' } | ForEach-Object { Write-Host $_ }
}

Write-Host "PASS: compiled EF migration chain starts at $baselineId and contains $($compiledMigrationIds.Count) unique ordered migrations"
Write-Host 'PASS: exact 27 late-merged sources are preserved; 26 guarded bodies replay once after the baseline without original EF metadata'
Write-Host 'PASS: complete 607-migration source chain retained as an uncompiled recoverable archive'
Write-Host 'PASS: exact reviewed D3 migration/designer/snapshot hashes are preserved separately from the merged baseline'
Write-Host 'PASS: zero-to-current Up has no predecessor-dependent drops/updates'
Write-Host 'PASS: 872 ordered check lifecycle events fail closed on malformed, removed, duplicate, or reordered authority'
Write-Host 'PASS: 675 archived-final checks are declarative in the 835-check current model with zero missing/drifted definitions'
Write-Host 'PASS: mixed trigger patches bind full source, retained fragment, exact marker and transformation boundary provenance'
Write-Host 'PASS: 386 current-model, 92 active non-model, and 15 distinct C5-C8 triggers are preserved'
Write-Host 'PASS: archived governance audit retains the final five functions, one view, and chronological trigger patches'
Write-Host 'PASS: trigger targets and 16,212 unambiguous inserted/deleted column references match the baseline schema'
