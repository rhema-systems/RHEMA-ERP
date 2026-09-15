[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $PSScriptRoot 'ScratchSchemaAuthorityDeriver\ScratchSchemaAuthorityDeriver.csproj'
$deriverDll = Join-Path $PSScriptRoot 'ScratchSchemaAuthorityDeriver\bin\Release\net8.0\ScratchSchemaAuthorityDeriver.dll'
$helper = Join-Path $repositoryRoot 'src\ErpSystem.Data\Migrations\ArchivedGovernanceBaselineSql.cs'
$financeAuthority = Join-Path $repositoryRoot 'src\ErpSystem.Data\Migrations\FinanceC1C8BaselineAuthoritySql.cs'
$baseline = Join-Path $repositoryRoot 'src\ErpSystem.Data\Migrations\20260913162402_DisposableDevelopmentCurrentModelBaseline.cs'
$manifest = Join-Path $repositoryRoot 'src\ErpSystem.Data\Migrations\ArchivedGovernanceBaselineManifest.json'
$committedAuthority = Join-Path $PSScriptRoot 'sql\gl-scratch-expected-schema-authority.json'
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('rhema-scratch-authority-test-' + [guid]::NewGuid().ToString('N'))

function Assert-DeriverRefusesCursorMutation([string]$name, [string]$helperText) {
    $mutatedHelper = Join-Path $temporaryDirectory "$name.cs"
    $mutatedOutput = Join-Path $temporaryDirectory "$name.json"
    $mutatedLog = Join-Path $temporaryDirectory "$name.log"
    [IO.File]::WriteAllText($mutatedHelper, $helperText, [Text.UTF8Encoding]::new($false))
    & dotnet $deriverDll $mutatedHelper $financeAuthority $baseline $manifest $mutatedOutput *> $mutatedLog
    if ($LASTEXITCODE -eq 0) { throw "Authority deriver accepted cursor mutation: $name." }
}

function Replace-ExactOnce([string]$text, [string]$old, [string]$new) {
    $first = $text.IndexOf($old, [StringComparison]::Ordinal)
    if ($first -lt 0 -or $text.IndexOf($old, $first + $old.Length, [StringComparison]::Ordinal) -ge 0) {
        throw "Expected exactly one source occurrence for mutation: $old"
    }
    return $text.Substring(0, $first) + $new + $text.Substring($first + $old.Length)
}

function Replace-ExactOccurrence([string]$text, [string]$old, [string]$new, [int]$ordinal, [int]$expectedCount) {
    $indexes = @()
    $searchAt = 0
    while (($found = $text.IndexOf($old, $searchAt, [StringComparison]::Ordinal)) -ge 0) {
        $indexes += $found
        $searchAt = $found + $old.Length
    }
    if ($indexes.Count -ne $expectedCount -or $ordinal -lt 0 -or $ordinal -ge $indexes.Count) {
        throw "Expected $expectedCount source occurrences and ordinal $ordinal for mutation: $old"
    }
    $index = $indexes[$ordinal]
    return $text.Substring(0, $index) + $new + $text.Substring($index + $old.Length)
}

function Replace-InTriggerDefinitionOnce([string]$text, [string]$triggerName, [string]$old, [string]$new) {
    $pattern = '(?ms)^\s*// TRIGGER ' + [regex]::Escape($triggerName) +
        ' .+?\r?\n\s*migrationBuilder\.Sql\("""\r?\n(?<sql>.*?)\r?\n\s*"""\);'
    $match = [regex]::Match($text, $pattern)
    if (-not $match.Success) { throw "Trigger source block was not found: $triggerName" }
    $sql = $match.Groups['sql'].Value
    if (([regex]::Matches($sql, [regex]::Escape($old))).Count -ne 1) {
        throw "Expected one exact trigger marker for mutation: $triggerName / $old"
    }
    $mutatedSql = $sql.Replace($old, $new, [StringComparison]::Ordinal)
    $start = $match.Groups['sql'].Index
    return $text.Substring(0, $start) + $mutatedSql + $text.Substring($start + $sql.Length)
}

function Replace-InPatchOnce([string]$text, [string]$migrationId, [string]$old, [string]$new) {
    $pattern = '(?ms)^\s*// POST-DEFINITION PATCH .+? from ' + [regex]::Escape($migrationId) +
        ':\d+;.+?\r?\n\s*migrationBuilder\.Sql\("""\r?\n(?<sql>.*?)\r?\n\s*"""\);'
    $match = [regex]::Match($text, $pattern)
    if (-not $match.Success) { throw "Patch source block was not found: $migrationId" }
    $sql = $match.Groups['sql'].Value
    if (([regex]::Matches($sql, [regex]::Escape($old))).Count -ne 1) {
        throw "Expected one exact patch marker for mutation: $migrationId / $old"
    }
    $mutatedSql = $sql.Replace($old, $new, [StringComparison]::Ordinal)
    $start = $match.Groups['sql'].Index
    return $text.Substring(0, $start) + $mutatedSql + $text.Substring($start + $sql.Length)
}

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

    $runtimeFinalHashes = [ordered]@{
        'TR_ProcurementFrameworkAgreementExtensions_Lifecycle' = 'F71463C1698F0341AF79419D94F0E9DCBCC8C238BDE8AB242B7689312F8DD377'
        'TR_ProcurementFrameworkCallOffs_PurchaseOrderSource' = 'BA76BA523B9F4DD54A78644A879BDF188A109D8B96381FF0686B5DF7CF08C456'
        'TR_PurchaseOrders_ApprovedSourceProtected' = '2EDC0D810330F957E8B22173A0BDF199AEC6CE6CB868FC4B274C12D41A02C22D'
    }
    $rejectedFlattenedHashes = @(
        'EA17DC774A461377E7582F96FAF0031636DFFB361FDFA38B47899FA996F763ED',
        'F41855117B3738BBEB3A80F8E326AF774AA2EE3C37F6D30012528B1151C7AE91',
        '8ABDBD654E35D4811D0541BD004E510E5EFC773635CFAC08054FF5A54400E6EE')
    foreach ($entry in $runtimeFinalHashes.GetEnumerator()) {
        $definition = @($authority.triggerDefinitions | Where-Object name -ceq $entry.Key)
        if ($definition.Count -ne 1 -or [string]$definition[0].canonicalSha256 -cne $entry.Value) {
            throw "Already-final runtime authority hash drifted: $($entry.Key)."
        }
    }
    $authorityText = [Text.Encoding]::UTF8.GetString($actualBytes)
    foreach ($oldHash in $rejectedFlattenedHashes) {
        if ($authorityText.Contains($oldHash, [StringComparison]::Ordinal)) {
            throw "Flattened guarded-patch hash remains accepted: $oldHash."
        }
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

    $helperText = Get-Content -Raw -LiteralPath $helper
    $cursorList = "N'TR_WarehouseQuantities_PhysicalCountFreeze',N'TR_InventoryItems_PhysicalCountFreeze',N'TR_StockMovements_PhysicalCountFreeze'"
    $cursorListCount = ([regex]::Matches($helperText, [regex]::Escape($cursorList))).Count
    if ($cursorListCount -ne 2) { throw "Expected two exact reviewed PhysicalCount cursor lists, found $cursorListCount." }

    $firstCursorIndex = $helperText.IndexOf($cursorList, [StringComparison]::Ordinal)
    $oneListChanged = $helperText.Substring(0, $firstCursorIndex) +
        $cursorList.Replace('TR_WarehouseQuantities_PhysicalCountFreeze', 'TR_PhysicalCounts_ControlledLifecycle') +
        $helperText.Substring($firstCursorIndex + $cursorList.Length)
    Assert-DeriverRefusesCursorMutation 'cursor-lists-disagree' $oneListChanged
    Assert-DeriverRefusesCursorMutation 'cursor-target-added' ($helperText.Replace($cursorList,
        $cursorList + ",N'TR_PhysicalCounts_ControlledLifecycle'", [StringComparison]::Ordinal))
    Assert-DeriverRefusesCursorMutation 'cursor-target-removed' ($helperText.Replace($cursorList,
        "N'TR_WarehouseQuantities_PhysicalCountFreeze',N'TR_InventoryItems_PhysicalCountFreeze'", [StringComparison]::Ordinal))
    Assert-DeriverRefusesCursorMutation 'cursor-target-reordered' ($helperText.Replace($cursorList,
        "N'TR_InventoryItems_PhysicalCountFreeze',N'TR_WarehouseQuantities_PhysicalCountFreeze',N'TR_StockMovements_PhysicalCountFreeze'", [StringComparison]::Ordinal))
    Assert-DeriverRefusesCursorMutation 'cursor-target-duplicated' ($helperText.Replace($cursorList,
        "N'TR_WarehouseQuantities_PhysicalCountFreeze',N'TR_InventoryItems_PhysicalCountFreeze',N'TR_InventoryItems_PhysicalCountFreeze'", [StringComparison]::Ordinal))

    $mutation = Replace-ExactOnce $helperText 'IF (SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1 AND name IN' 'IF (SELECT COUNT(*) FROM sys.triggers WHERE parent_class=0 AND name IN'
    Assert-DeriverRefusesCursorMutation 'cursor-validation-context' $mutation
    $mutation = Replace-ExactOnce $helperText 'DECLARE freezeGuards CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM sys.triggers' 'DECLARE freezeGuards CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM sys.objects'
    Assert-DeriverRefusesCursorMutation 'cursor-select-source' $mutation
    $mutation = Replace-ExactOnce $helperText 'DECLARE freezeGuards CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM sys.triggers' 'DECLARE freezeGuards CURSOR LOCAL FAST_FORWARD FOR SELECT object_id FROM sys.triggers'
    Assert-DeriverRefusesCursorMutation 'cursor-select-projection' $mutation
    $mutation = Replace-ExactOccurrence $helperText 'FETCH NEXT FROM freezeGuards INTO @freezeName;' 'FETCH NEXT FROM freezeGuards INTO @life;' 0 2
    Assert-DeriverRefusesCursorMutation 'cursor-initial-fetch-destination' $mutation
    $mutation = Replace-ExactOccurrence $helperText 'FETCH NEXT FROM freezeGuards INTO @freezeName;' 'FETCH NEXT FROM freezeGuards INTO @life;' 1 2
    Assert-DeriverRefusesCursorMutation 'cursor-loop-fetch-destination' $mutation
    $mutation = Replace-ExactOnce $helperText "FETCH NEXT FROM freezeGuards INTO @freezeName;`r`n            WHILE @@FETCH_STATUS = 0" "FETCH NEXT FROM freezeGuards INTO @freezeName;`r`n            WHILE @@FETCH_STATUS <> 0"
    Assert-DeriverRefusesCursorMutation 'cursor-loop-predicate' $mutation
    $mutation = Replace-ExactOnce $helperText "SET @freeze = OBJECT_DEFINITION(OBJECT_ID(N'dbo.'+@freezeName));" "SET @freeze = OBJECT_DEFINITION(OBJECT_ID(N'dbo.'+@life));"
    Assert-DeriverRefusesCursorMutation 'cursor-object-definition-variable' $mutation
    $mutation = Replace-ExactOnce $helperText "SET @freeze = REPLACE(@freeze, N'N''InProgress'',N''RecountRequired'''," "SET @life = REPLACE(@freeze, N'N''InProgress'',N''RecountRequired''',"
    Assert-DeriverRefusesCursorMutation 'cursor-transform-destination' $mutation
    $mutation = Replace-ExactOnce $helperText "SET @freeze = STUFF(@freeze, 1, CHARINDEX(N'TRIGGER', UPPER(@freeze)) - 1, N'CREATE OR ALTER ');" "SET @freeze = STUFF(@life, 1, CHARINDEX(N'TRIGGER', UPPER(@freeze)) - 1, N'CREATE OR ALTER ');"
    Assert-DeriverRefusesCursorMutation 'cursor-promotion-source' $mutation
    $mutation = Replace-ExactOnce $helperText "EXEC sys.sp_executesql @freeze;`r`n                FETCH NEXT FROM freezeGuards INTO @freezeName;" "EXEC sys.sp_executesql @life;`r`n                FETCH NEXT FROM freezeGuards INTO @freezeName;"
    Assert-DeriverRefusesCursorMutation 'cursor-exec-variable' $mutation
    $mutation = Replace-ExactOccurrence $helperText 'FETCH NEXT FROM freezeGuards INTO @freezeName;' '-- removed loop progression' 1 2
    Assert-DeriverRefusesCursorMutation 'cursor-loop-progression-missing' $mutation

    $badCursorList = $cursorList.Replace('TR_WarehouseQuantities_PhysicalCountFreeze',
        'TR_PhysicalCounts_ControlledLifecycle', [StringComparison]::Ordinal)
    $commentSpoof = $helperText.Replace($cursorList, $badCursorList, [StringComparison]::Ordinal).Replace(
        'DECLARE @freezeName nvarchar(128), @freeze nvarchar(max);',
        "-- name IN ($cursorList)`r`n            -- name IN ($cursorList)`r`n            DECLARE @freezeName nvarchar(128), @freeze nvarchar(max);",
        [StringComparison]::Ordinal)
    Assert-DeriverRefusesCursorMutation 'cursor-comment-list-spoof' $commentSpoof
    $dummySpoof = $helperText.Replace($cursorList, $badCursorList, [StringComparison]::Ordinal).Replace(
        'DECLARE @freezeName nvarchar(128), @freeze nvarchar(max);',
        "DECLARE @freezeName nvarchar(128), @freeze nvarchar(max);`r`n            IF 1=0 SELECT name FROM sys.triggers WHERE name IN ($cursorList);`r`n            IF 1=0 SELECT name FROM sys.triggers WHERE name IN ($cursorList);",
        [StringComparison]::Ordinal)
    Assert-DeriverRefusesCursorMutation 'cursor-dummy-list-spoof' $dummySpoof

    $mutation = Replace-InPatchOnce $helperText '20260730031000_TDC0403ExceptionalFrameworkLineage' "N'newer.DecisionSequence >',`r`n                    @definition) = 0" "N'newer.DecisionSequence >',`r`n                    @changed) = 0"
    Assert-DeriverRefusesCursorMutation 'already-final-calloff-guard-variable' $mutation
    $mutation = Replace-InPatchOnce $helperText '20260730031000_TDC0403ExceptionalFrameworkLineage' 'OBJECT_ID(N''[TR_ProcurementFrameworkCallOffs_PurchaseOrderSource]'')' 'OBJECT_ID(N''[TR_ProcurementFrameworkAgreementExtensions_Lifecycle]'')'
    Assert-DeriverRefusesCursorMutation 'already-final-calloff-definition-target' $mutation
    $mutation = Replace-InPatchOnce $helperText '20260730031000_TDC0403ExceptionalFrameworkLineage' 'EXEC sys.sp_executesql @definition;' 'EXEC sys.sp_executesql @changed;'
    Assert-DeriverRefusesCursorMutation 'already-final-calloff-exec-variable' $mutation
    $mutation = Replace-InTriggerDefinitionOnce $helperText 'TR_ProcurementFrameworkCallOffs_PurchaseOrderSource' 'newer.DecisionSequence >' 'newer.DecisionSequence <'
    Assert-DeriverRefusesCursorMutation 'already-final-calloff-marker-missing' $mutation
    $mutation = Replace-InTriggerDefinitionOnce $helperText 'TR_ProcurementFrameworkCallOffs_PurchaseOrderSource' 'newer.DecisionSequence >' 'newer.DecisionSequence > newer.DecisionSequence >'
    Assert-DeriverRefusesCursorMutation 'already-final-calloff-marker-ambiguous' $mutation

    $mutation = Replace-InPatchOnce $helperText '20260730150000_TDC0403OrdinarySourceTriggerCorrections' "N'contract.StartDate > SYSUTCDATETIME()',`r`n                    @definition) = 0" "N'contract.StartDate > SYSUTCDATETIME()',`r`n                    @changed) = 0"
    Assert-DeriverRefusesCursorMutation 'already-final-purchase-order-guard-variable' $mutation
    $mutation = Replace-InTriggerDefinitionOnce $helperText 'TR_PurchaseOrders_ApprovedSourceProtected' 'contract.StartDate > SYSUTCDATETIME()' 'contract.StartDate < SYSUTCDATETIME()'
    Assert-DeriverRefusesCursorMutation 'already-final-purchase-order-marker-missing' $mutation
    $mutation = Replace-InTriggerDefinitionOnce $helperText 'TR_PurchaseOrders_ApprovedSourceProtected' 'contract.StartDate > SYSUTCDATETIME()' 'contract.StartDate > SYSUTCDATETIME() OR contract.StartDate > SYSUTCDATETIME()'
    Assert-DeriverRefusesCursorMutation 'already-final-purchase-order-marker-ambiguous' $mutation

    $mutation = Replace-InPatchOnce $helperText '20260730192500_TDC0402TerminalReviewPaths' "OBJECT_ID(N'[TR_ProcurementFrameworkAgreementExtensions_Lifecycle]')" "OBJECT_ID(N'[TR_ProcurementFrameworkCallOffs_Lifecycle]')"
    Assert-DeriverRefusesCursorMutation 'already-final-mixed-target-rebound' $mutation
    $mutation = Replace-InPatchOnce $helperText '20260730192500_TDC0402TerminalReviewPaths' 'EXEC sys.sp_executesql @extensionDefinition;' 'EXEC sys.sp_executesql @callOffDefinition;'
    Assert-DeriverRefusesCursorMutation 'already-final-mixed-exec-variable' $mutation
    $mutation = Replace-InTriggerDefinitionOnce $helperText 'TR_ProcurementFrameworkAgreementExtensions_Lifecycle' 'prior.Id IS NULL OR i.Status = 1' 'prior.Id IS NULL OR i.Status <= 1'
    Assert-DeriverRefusesCursorMutation 'already-final-extension-marker-missing' $mutation
    $mutation = Replace-InTriggerDefinitionOnce $helperText 'TR_ProcurementFrameworkAgreementExtensions_Lifecycle' 'prior.Id IS NULL OR i.Status = 1' 'prior.Id IS NULL OR i.Status = 1 OR prior.Id IS NULL OR i.Status = 1'
    Assert-DeriverRefusesCursorMutation 'already-final-extension-marker-ambiguous' $mutation

    Write-Host 'PASS: scratch authority deterministically derives 493 triggers and 835 checks.'
    Write-Host 'PASS: mixed static/dynamic PhysicalCount patch cannot apply freeze-cursor text cross-target.'
    Write-Host 'PASS: the rejected legacy cross-target hash cannot be substituted into committed authority.'
    Write-Host 'PASS: changed, added, removed, reordered, duplicated, or disagreeing PhysicalCount cursor targets are refused.'
    Write-Host 'PASS: executable validation/cursor/fetch/loop/definition/transform/exec flow is bound and comment/dummy spoofing is refused.'
    Write-Host 'PASS: guarded already-final patches match three preserved runtime hashes and flattened duplicate hashes are rejected.'
    Write-Host 'PASS: already-final source identity, guard/definition/exec variables, mixed targets, and marker multiplicity fail closed.'
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
