# Read-only shared readiness checks for the isolated rehearsal transfer runtime.
function Get-TransferDraftLineApplyProof {
    param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)][string]$Sha256)
    if ($Sha256 -notmatch '^[A-Fa-f0-9]{64}$' -or (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Sha256) { throw 'The reviewed draft-line apply proof hash differs.' }
    $proofText=Get-Content -LiteralPath $Path -Raw
    $proofCandidates=@()
    try { $proofCandidates=@($proofText | ConvertFrom-Json -ErrorAction Stop) } catch {
        $proofCandidates=@(foreach($proofLine in ($proofText -split '\r?\n')) {
            if($proofLine.TrimStart().StartsWith('{')) { try { $proofLine | ConvertFrom-Json -ErrorAction Stop } catch {} }
        })
    }
    $proofRows=@($proofCandidates | Where-Object { $_.Applied -eq $true -and $_.Phase -eq 'TransferDraftLineGuard' })
    if($proofRows.Count -ne 1){throw 'Exactly one successful draft-line guard apply result is required.'}
    $proof=$proofRows[0]
    if($proof.Database -ne 'RhemaERP_PO_Rehearsal_20260909' -or [int]$proof.MigrationCount -ne 483 -or
       @($proof.MigrationIds).Count -ne 1 -or $proof.MigrationIds[0] -ne '20260912233000_InventoryTransferDraftLineCompletionGuard' -or
       $proof.SqlSha256 -ne '533D170C854ABA70BB8F083A07DFB00942D5AC798AE0A932D4938574DBEA5E8D' -or
       $proof.OriginalBusinessRowsUnchanged -ne $true -or $proof.HumanApprovalHistoryUnchanged -ne $true -or
       $proof.PostedCountAndUatCountsUnchanged -ne $true -or $proof.ReviewedBaselineSha256 -notmatch '^[A-Fa-f0-9]{64}$') {
        throw 'The apply proof does not confirm the exact rehearsal-only483 guard and preserved business data.'
    }
    $backupDirectory=[IO.Path]::GetFullPath('C:/Program Files/Microsoft SQL Server/MSSQL15.SQL2017/MSSQL/Backup')
    $backupPath=[IO.Path]::GetFullPath([string]$proof.VerifiedBackup)
    if([IO.Path]::GetDirectoryName($backupPath) -ne $backupDirectory -or
       [IO.Path]::GetFileName($backupPath) -notlike 'RhemaERP_PO_Rehearsal_20260909_before_optional_approval_*.bak' -or
       -not(Test-Path -LiteralPath $backupPath -PathType Leaf)) { throw 'The verified rehearsal pre-apply backup is unavailable.' }
    return $proof
}

function Assert-TransferRuntimeAssemblySet {
    param([Parameter(Mandatory)]$Manifest,[Parameter(Mandatory)][string]$ApiRuntimePath)
    if(@($Manifest.assemblies).Count -ne 4 -or (@($Manifest.assemblies.Assembly | Sort-Object) -join '|') -ne 'ErpSystem.Api|ErpSystem.Core|ErpSystem.Data|ErpSystem.Shared') { throw 'The exact four-assembly transfer manifest is required.' }
    foreach($assembly in $Manifest.assemblies) {
        if($assembly.SHA256 -notmatch '^[A-Fa-f0-9]{64}$' -or (Get-FileHash -LiteralPath (Join-Path $ApiRuntimePath ($assembly.Assembly+'.dll')) -Algorithm SHA256).Hash -ne $assembly.SHA256) { throw 'A transfer API binary differs from its tested manifest.' }
    }
}

function Get-LocalTransferRuntimeReadiness {
    param([Parameter(Mandatory)][string]$ApiRuntimePath,[switch]$RequireRunning,[int]$ExpectedProcessId)
    $expectedRuntime=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Temp/tdc-inventory-transfer-auto-complete-20260912/api'))
    if([IO.Path]::GetFullPath($ApiRuntimePath) -ne $expectedRuntime){throw 'Only the isolated rehearsal transfer runtime is supported.'}
    $manifest=Get-Content -LiteralPath (Join-Path $expectedRuntime 'inventory-runtime-ready.json') -Raw | ConvertFrom-Json
    if($manifest.release -ne 'inventory-transfer-auto-complete-20260912'){throw 'The transfer runtime release is not the reviewed rehearsal release.'}
    $schemaCounts=@{'20260912232000_InventoryTransferAutomaticCompletion'=482;'20260912233000_InventoryTransferDraftLineCompletionGuard'=483}
    if(-not $schemaCounts.ContainsKey([string]$manifest.schemaRequired)){throw 'Only explicit transfer schema482 or483 readiness is supported.'}
    $schemaCount=$schemaCounts[[string]$manifest.schemaRequired]
    Assert-TransferRuntimeAssemblySet -Manifest $manifest -ApiRuntimePath $expectedRuntime
    if($schemaCount -eq 483) {
        if(-not $manifest.schemaAdoption -or $manifest.schemaAdoption.migrationId -ne $manifest.schemaRequired){throw 'Schema483 requires its verified adoption proof.'}
        $null=Get-TransferDraftLineApplyProof -Path $manifest.schemaAdoption.applyProofPath -Sha256 $manifest.schemaAdoption.applyProofSha256
        if(-not [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes((Join-Path $expectedRuntime 'ErpSystem.Data.dll'))).Contains('InventoryTransferDraftLineCompletionGuard')) { throw 'The staged Data assembly does not contain the adopted migration.' }
    }
    Add-Type -AssemblyName System.Data
    $connection=[System.Data.SqlClient.SqlConnection]::new('Server=RHEMA-MICHAEL\SQL2017;Database=RhemaERP_PO_Rehearsal_20260909;Integrated Security=True;Application Name=ReadOnlyTransferRuntimeReadiness')
    try {
        $connection.Open();$command=$connection.CreateCommand();$command.CommandTimeout=30
        $command.CommandText="SELECT CASE WHEN DB_NAME()=N'RhemaERP_PO_Rehearsal_20260909' AND CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))=N'RHEMA-MICHAEL\SQL2017' AND (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory)=@count AND (SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory)=@last THEN 1 ELSE 0 END"
        [void]$command.Parameters.AddWithValue('@count',$schemaCount);[void]$command.Parameters.AddWithValue('@last',[string]$manifest.schemaRequired)
        if([int]$command.ExecuteScalar() -ne 1){throw "Rehearsal database does not exactly match manifest schema$schemaCount; no server will be changed."}
        if($schemaCount -eq 483) {
            $command.CommandText="DECLARE @s nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_InventoryTransfers_ControlledLifecycle'));SELECT CASE WHEN @s IS NOT NULL AND (LEN(@s)-LEN(REPLACE(@s,N'AND line.IsDeleted=0 AND (line.RequestedQuantity<=0',N'')))/LEN(N'AND line.IsDeleted=0 AND (line.RequestedQuantity<=0')=2 AND CHARINDEX(N'AND (line.IsDeleted=1 OR line.RequestedQuantity<=0',@s)=0 AND CHARINDEX(N'line.TenantId = i.TenantId AND line.IsDeleted = 0 AND (line.ShippedQuantity <> line.RequestedQuantity',@s)>0 AND OBJECTPROPERTY(OBJECT_ID(N'dbo.TR_InventoryTransfers_ControlledLifecycle'),N'ExecIsTriggerDisabled')=0 THEN 1 ELSE 0 END"
            if([int]$command.ExecuteScalar() -ne 1){throw 'Schema483 receipt/completion guard is not the applied enabled definition.'}
        }
    } finally {$connection.Dispose()}
    $processId=$null
    if($RequireRunning -or $ExpectedProcessId) {
        $listeners=@(Get-NetTCPConnection -LocalAddress '127.0.0.1' -LocalPort 5002 -State Listen -ErrorAction SilentlyContinue)
        if($listeners.Count -ne 1){throw 'Exactly one rehearsal API listener is required.'}
        $processId=[int]$listeners[0].OwningProcess
        $process=Get-CimInstance Win32_Process -Filter "ProcessId=$processId"
        if(-not $process.ExecutablePath -or [IO.Path]::GetFullPath($process.ExecutablePath) -ne (Join-Path $expectedRuntime 'ErpSystem.Api.exe') -or ($ExpectedProcessId -and $ExpectedProcessId -ne $processId)){throw 'The running API is not the verified rehearsal transfer executable.'}
    }
    [pscustomobject]@{Verified=$true;Database='RhemaERP_PO_Rehearsal_20260909';ApiRuntimePath=$expectedRuntime;SchemaCount=$schemaCount;SchemaRequired=$manifest.schemaRequired;ProcessId=$processId}
}
