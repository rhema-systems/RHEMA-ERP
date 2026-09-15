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

function Assert-DeriverRefusesCursorMutation([string]$name, [string]$helperText) {
    $mutatedHelper = Join-Path $temporaryDirectory "$name.cs"
    $mutatedOutput = Join-Path $temporaryDirectory "$name.json"
    $mutatedLog = Join-Path $temporaryDirectory "$name.log"
    [IO.File]::WriteAllText($mutatedHelper, $helperText, [Text.UTF8Encoding]::new($false))
    & dotnet run --project $project -c Release --no-build -- $mutatedHelper $financeAuthority $baseline $manifest $mutatedOutput *> $mutatedLog
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

    Write-Host 'PASS: scratch authority deterministically derives 493 triggers and 835 checks.'
    Write-Host 'PASS: mixed static/dynamic PhysicalCount patch cannot apply freeze-cursor text cross-target.'
    Write-Host 'PASS: the rejected legacy cross-target hash cannot be substituted into committed authority.'
    Write-Host 'PASS: changed, added, removed, reordered, duplicated, or disagreeing PhysicalCount cursor targets are refused.'
    Write-Host 'PASS: executable validation/cursor/fetch/loop/definition/transform/exec flow is bound and comment/dummy spoofing is refused.'
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
