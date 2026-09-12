# This exact local rollout is read-only unless -Apply and the reviewed preview hash are supplied.
# Source SQL is used because a matching source/build fingerprint for EF generation is not available.
param(
    [switch]$Apply,
    [string]$ExpectedPreviewHash,
    [string]$BackupDirectory
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data
$mainRepo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$mainExpected = [ordered]@{
    '20260909213000_LinkLandedCostsToSupplierInvoiceLines' = 'D640EF241434DAD57D7D8887A0BA7B5C79EC2F00EED89996FFE2D41C7CAE9C07'
    '20260911210000_PhysicalCountReviewDecisions' = '7995F8AA680E7F3E629BA1481C16C0E9DB6F4F6914AD7800C70B9654657CFFA9'
    '20260912003000_WarehouseDefaultLocations' = '43A2979D3B5EC0DDD15ECC39CF35931092B4A31CB3278CC29B1198537F603119'
    '20260912013000_AlignStockAdjustmentLocationValuation' = 'F01839624F813F1D8DB9A29409150AD41999B17A6EB480AB1AF6DAB298D1D148'
}
$mainSql = [ordered]@{}
$mainTransaction = $null
$mainCommandTimeout = 120
$mainBackupPath = $null
$mainConnection = [System.Data.SqlClient.SqlConnection]::new('Server=RHEMA-MICHAEL\SQL2017;Database=RhemaERP;Integrated Security=True;Application Name=MainUatCountMigrationParity')

function Read-MainRows([string]$Sql, [hashtable]$Parameters = @{}) {
    $mainCommand = $mainConnection.CreateCommand()
    $mainCommand.CommandText = $Sql
    $mainCommand.CommandTimeout = $mainCommandTimeout
    if ($mainTransaction) { $mainCommand.Transaction = $mainTransaction }
    foreach ($mainParameter in $Parameters.GetEnumerator()) { [void]$mainCommand.Parameters.AddWithValue($mainParameter.Key, $mainParameter.Value) }
    try {
        $mainReader = $mainCommand.ExecuteReader()
        try {
            while ($mainReader.Read()) {
                $mainValues = [ordered]@{}
                for ($mainIndex = 0; $mainIndex -lt $mainReader.FieldCount; $mainIndex++) {
                    $mainValues[$mainReader.GetName($mainIndex)] = if ($mainReader.IsDBNull($mainIndex)) { $null } else { $mainReader.GetValue($mainIndex) }
                }
                [pscustomobject]$mainValues
            }
        } finally { $mainReader.Dispose() }
    } finally { $mainCommand.Dispose() }
}

function Invoke-MainSql([string]$Sql, [hashtable]$Parameters = @{}) {
    $mainCommand = $mainConnection.CreateCommand()
    $mainCommand.CommandText = $Sql
    $mainCommand.CommandTimeout = $mainCommandTimeout
    if ($mainTransaction) { $mainCommand.Transaction = $mainTransaction }
    foreach ($mainParameter in $Parameters.GetEnumerator()) { [void]$mainCommand.Parameters.AddWithValue($mainParameter.Key, $mainParameter.Value) }
    try { [void]$mainCommand.ExecuteNonQuery() } finally { $mainCommand.Dispose() }
}

function Assert-MainStopped {
    if (Get-NetTCPConnection -LocalPort 3000,5000 -State Listen -ErrorAction SilentlyContinue) {
        throw 'Main UAT ports 3000/5000 must remain stopped; this helper never stops a process.'
    }
}

function Get-MainPending {
    $mainApplied = @(Read-MainRows 'SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId' | ForEach-Object { $_.MigrationId })
    $mainSourceIds = @(Get-ChildItem -LiteralPath (Join-Path $mainRepo 'src/ErpSystem.Data/Migrations') -File |
        Where-Object { $_.Name -match '^\d{14}_.+\.cs$' -and $_.Name -notlike '*.Designer.cs' } |
        ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_.Name) } | Sort-Object)
    # This retained initial history record predates the current migration filename set.
    # It is never deleted/rewritten, and existing history is hashed and compared again before commit.
    $mainRetainedHistory = @('20260402003233_InitialCreate')
    if (@($mainApplied | Where-Object { $_ -notin $mainSourceIds -and $_ -notin $mainRetainedHistory }).Count -gt 0) { throw 'Database has unreviewed migrations absent from this source tree.' }
    @($mainSourceIds | Where-Object { $_ -notin $mainApplied })
}

function Get-MainBaseline {
    foreach ($mainTable in $mainProtectedTables) {
        $mainProjection = $mainProjections[$mainTable]
        # Serializable HOLDLOCK retains protection until commit. Preview uses SELECT only, without table locks.
        $mainLock = if ($mainTransaction) { ' WITH (TABLOCK,HOLDLOCK)' } else { '' }
        $mainBaselineSql = "SELECT N'$mainTable' AS TableName, COUNT_BIG(*) AS [RowCount], " +
            "CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max)," +
            "(SELECT $mainProjection FROM dbo.[$mainTable]$mainLock ORDER BY [Id] FOR JSON PATH,INCLUDE_NULL_VALUES))),2) AS DataHash " +
            "FROM dbo.[$mainTable]$mainLock;"
        Read-MainRows $mainBaselineSql
    }
}

function Get-MainPreviewHash([object[]]$Baseline, [object[]]$Guards) {
    $mainMaterial = [ordered]@{ Database='RhemaERP'; Migrations=$mainExpected; Baseline=$Baseline; Guards=$Guards }
    $mainBytes = [Text.Encoding]::UTF8.GetBytes(($mainMaterial | ConvertTo-Json -Depth 8 -Compress))
    $mainHasher = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($mainHasher.ComputeHash($mainBytes))).Replace('-','') } finally { $mainHasher.Dispose() }
}

try {
    if ($Apply -and $ExpectedPreviewHash -notmatch '^[A-Fa-f0-9]{64}$') { throw 'Apply requires the reviewed ExpectedPreviewHash from a read-only run.' }
    foreach ($mainEntry in $mainExpected.GetEnumerator()) {
        $mainSourcePath = Join-Path $mainRepo "src/ErpSystem.Data/Migrations/$($mainEntry.Key).cs"
        if ((Get-FileHash -LiteralPath $mainSourcePath -Algorithm SHA256).Hash -ne $mainEntry.Value) {
            throw "Migration source changed: $($mainEntry.Key). Review and update the helper before applying."
        }
        $mainSource = Get-Content -LiteralPath $mainSourcePath -Raw
        if ($mainEntry.Key -in @('20260911210000_PhysicalCountReviewDecisions','20260912013000_AlignStockAdjustmentLocationValuation')) {
            $mainMatch = [regex]::Match($mainSource, '(?s)public const string UpgradeSql = """\r?\n(.*?)\r?\n\s*""";')
            if (-not $mainMatch.Success) { throw 'The exact pinned UpgradeSql migration was not found.' }
            $mainSql[$mainEntry.Key] = @($mainMatch.Groups[1].Value)
        } elseif ($mainEntry.Key -eq '20260912003000_WarehouseDefaultLocations') {
            # The pinned source has exactly these two EF schema operations followed by this first SQL block.
            $mainMatch = [regex]::Match($mainSource, '(?s)migrationBuilder\.Sql\("""\r?\n(.*?)\r?\n\s*"""\);')
            if (-not $mainMatch.Success) { throw 'The exact pinned default-bin SQL was not found.' }
            # Separate EF operation batches: later SQL is compiled only after its new column exists.
            $mainSql[$mainEntry.Key] = @(
                'ALTER TABLE dbo.WarehouseLocations ADD IsDefault bit NOT NULL DEFAULT CAST(0 AS bit);',
                'CREATE UNIQUE INDEX UX_WarehouseLocations_Default ON dbo.WarehouseLocations(TenantId,WarehouseId) WHERE IsDefault=1 AND IsActive=1 AND IsDeleted=0;',
                $mainMatch.Groups[1].Value
            )
        } else {
            # Exact SQL equivalent of AddColumn/CreateIndex/AddForeignKey in the pinned EF migration.
            $mainSql[$mainEntry.Key] = @(
                'ALTER TABLE dbo.VendorInvoiceLineItem ADD LandedCostItemId uniqueidentifier NULL;',
                'CREATE UNIQUE INDEX IX_VendorInvoiceLineItem_LandedCostItemId ON dbo.VendorInvoiceLineItem(LandedCostItemId) WHERE LandedCostItemId IS NOT NULL AND IsDeleted=0;',
                'ALTER TABLE dbo.VendorInvoiceLineItem WITH CHECK ADD CONSTRAINT FK_VendorInvoiceLineItem_LandedCostItems_LandedCostItemId FOREIGN KEY(LandedCostItemId) REFERENCES dbo.LandedCostItems(Id);'
            )
        }
    }
    $mainConnection.Open()
    $mainIdentity = @(Read-MainRows "SELECT DB_NAME() AS DatabaseName,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName")[0]
    if ($mainIdentity.DatabaseName -ne 'RhemaERP' -or $mainIdentity.ServerName -ne 'RHEMA-MICHAEL\SQL2017') { throw 'Exact local main server/database verification failed.' }
    $mainPending = @(Get-MainPending)
    if (($mainPending -join '|') -ne (@($mainExpected.Keys) -join '|')) { throw 'Pending migrations are not exactly the four reviewed migrations. No changes made.' }
    $mainSchema = @(Read-MainRows "SELECT COL_LENGTH(N'dbo.VendorInvoiceLineItem',N'LandedCostItemId') AS LinkBytes,COL_LENGTH(N'dbo.WarehouseLocations',N'IsDefault') AS DefaultBytes,COLUMNPROPERTY(OBJECT_ID(N'dbo.StockAdjustmentItems'),N'UnitCost',N'Scale') AS CostScale")[0]
    if ($null -ne $mainSchema.LinkBytes -or $null -ne $mainSchema.DefaultBytes -or $mainSchema.CostScale -ne 2) { throw 'Unexpected schema without migration history; investigate before applying.' }
    $mainHistoryBefore = @(Read-MainRows 'SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId')
    $mainVersion = [string]$mainHistoryBefore[-1].ProductVersion
    if ($mainVersion -notmatch '^8\.0\.\d+') { throw 'Unexpected EF migration product version.' }
    $mainGuardSql = @'
SELECT name,is_disabled,CONVERT(varchar(64),HASHBYTES('SHA2_256',OBJECT_DEFINITION(object_id)),2) AS DefinitionHash
FROM sys.triggers WHERE name IN(N'TR_StockAdjustmentItems_ControlledMutation',N'TR_PhysicalCountItems_ControlledMutation',
 N'TR_PhysicalCounts_ControlledLifecycle',N'TR_PhysicalCountActions_AppendOnly',N'TR_WarehouseQuantities_PhysicalCountFreeze',
 N'TR_InventoryItems_PhysicalCountFreeze',N'TR_StockMovements_PhysicalCountFreeze') ORDER BY name;
'@
    $mainGuards = @(Read-MainRows $mainGuardSql)
    if ($mainGuards.Count -ne 7 -or @($mainGuards | Where-Object { $_.is_disabled -or -not $_.DefinitionHash }).Count) { throw 'An expected count/adjustment guard is absent or disabled.' }
    $mainProtectedTables = @(Read-MainRows @'
SELECT t.name AS TableName FROM sys.tables t WHERE SCHEMA_NAME(t.schema_id)=N'dbo'
 AND (t.name LIKE N'PhysicalCount%' OR t.name LIKE N'StockAdjustment%' OR t.name LIKE N'StockMovement%'
 OR t.name LIKE N'Workflow%' OR t.name LIKE N'JournalEntr%' OR t.name LIKE N'JournalBatch%'
 OR t.name LIKE N'VendorInvoice%' OR t.name LIKE N'LandedCost%'
 OR t.name IN(N'InventoryItems',N'InventoryBalances',N'InventoryLayers',N'InventoryLocations',N'WarehouseQuantities',N'Warehouses'))
 AND EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=t.object_id AND c.name=N'Id') ORDER BY t.name;
'@ | ForEach-Object { $_.TableName })
    foreach ($mainRequiredTable in @('PhysicalCounts','PhysicalCountItems','PhysicalCountActions','InventoryItems','InventoryBalances','InventoryLocations','WarehouseQuantities','StockMovements','StockAdjustments','StockAdjustmentItems','WorkflowInstances','WorkflowApprovals')) {
        if ($mainRequiredTable -notin $mainProtectedTables) { throw "Missing protected table: $mainRequiredTable" }
    }
    $mainProjections = @{}
    foreach ($mainTable in $mainProtectedTables) {
        $mainColumns = @(Read-MainRows @'
SELECT c.name AS ColumnName,TYPE_NAME(c.system_type_id) AS TypeName,c.scale AS Scale
FROM sys.columns c WHERE c.object_id=OBJECT_ID(@table) ORDER BY c.column_id;
'@ @{ '@table'="dbo.$mainTable" })
        $mainProjections[$mainTable] = ($mainColumns | ForEach-Object {
            $mainColumnName = '[' + $_.ColumnName.Replace(']',']]') + ']'
            # Canonical decimal scale prevents a schema-only 18,2 -> 18,4 widening from changing the hash.
            if ($_.TypeName -in @('decimal','numeric','money','smallmoney')) { "CONVERT(decimal(38,10),$mainColumnName) AS $mainColumnName" }
            else { $mainColumnName }
        }) -join ','
    }
    $mainBaseline = @(Get-MainBaseline)
    $mainPreviewHash = Get-MainPreviewHash $mainBaseline $mainGuards
    if (-not $Apply) {
        [pscustomobject]@{ Database='RhemaERP'; ReadOnly=$true; PreviewHash=$mainPreviewHash; PendingMigrations=$mainPending;
            PreservedTables=$mainBaseline; Scope='Four pinned migrations only; default-bin metadata allowed. No approvals, stock/GL posting, seeding or legacy-total repair.' } | ConvertTo-Json -Depth 6
        return
    }
    if ($mainPreviewHash -ne $ExpectedPreviewHash) { throw 'The reviewed preview changed. Run the read-only preview again.' }
    Assert-MainStopped
    if ([string]::IsNullOrWhiteSpace($BackupDirectory)) {
        $BackupDirectory = [string](@(Read-MainRows "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath')) AS BackupDirectory")[0].BackupDirectory)
    }
    if ([string]::IsNullOrWhiteSpace($BackupDirectory) -or -not [IO.Path]::IsPathRooted($BackupDirectory)) { throw 'An absolute verified SQL backup directory is required.' }
    $mainBackupPath = Join-Path $BackupDirectory ('RhemaERP_before_count_parity_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '_' + [Guid]::NewGuid().ToString('N') + '.bak')
    if (Test-Path -LiteralPath $mainBackupPath) { throw 'Refusing to reuse an existing backup file.' }
    $mainCommandTimeout = 600
    Invoke-MainSql 'BACKUP DATABASE [RhemaERP] TO DISK=@path WITH COPY_ONLY,CHECKSUM; RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM;' @{ '@path'=$mainBackupPath }
    $mainCommandTimeout = 120
    Write-Output "Verified main UAT backup: $mainBackupPath"
    Assert-MainStopped
    $mainTransaction = $mainConnection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    try {
        Invoke-MainSql @'
SET XACT_ABORT ON;
DECLARE @lock int; EXEC @lock=sys.sp_getapplock @Resource=N'MainUatCountMigrationParity20260912',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=0;
IF @lock<0 THROW 51990,'Another count migration rollout is active.',1;
'@
        $mainPending = @(Get-MainPending)
        if (($mainPending -join '|') -ne (@($mainExpected.Keys) -join '|')) { throw 'Pending migrations changed after backup.' }
        $mainLockedBaseline = @(Get-MainBaseline)
        $mainLockedGuards = @(Read-MainRows $mainGuardSql)
        if ((Get-MainPreviewHash $mainLockedBaseline $mainLockedGuards) -ne $ExpectedPreviewHash) { throw 'Preserved transaction data or guards changed after backup.' }
        foreach ($mainEntry in $mainExpected.GetEnumerator()) {
            foreach ($mainBatch in $mainSql[$mainEntry.Key]) { Invoke-MainSql $mainBatch }
            # History is recorded only after its exact migration operations succeed, in the same atomic transaction.
            Invoke-MainSql 'INSERT dbo.__EFMigrationsHistory(MigrationId,ProductVersion) VALUES(@migration,@version);' @{ '@migration'=$mainEntry.Key; '@version'=$mainVersion }
        }
        if (@(Get-MainPending).Count) { throw 'Pending migrations remain after the reviewed rollout.' }
        $mainAfterBaseline = @(Get-MainBaseline)
        if (($mainLockedBaseline | ConvertTo-Json -Depth 5 -Compress) -ne ($mainAfterBaseline | ConvertTo-Json -Depth 5 -Compress)) { throw 'Protected count, stock, invoice, GL or approval data changed; rolling back all four migrations.' }
        $mainAfterHistory = @(Read-MainRows 'SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId')
        $mainOldHistoryAfter = @($mainAfterHistory | Where-Object { $_.MigrationId -notin @($mainExpected.Keys) })
        if ($mainAfterHistory.Count -ne $mainHistoryBefore.Count + 4 -or ($mainOldHistoryAfter | ConvertTo-Json -Compress) -ne ($mainHistoryBefore | ConvertTo-Json -Compress)) { throw 'Unexpected migration-history changes; rolling back.' }
        Invoke-MainSql @'
IF COL_LENGTH(N'dbo.VendorInvoiceLineItem',N'LandedCostItemId')<>16 OR COL_LENGTH(N'dbo.WarehouseLocations',N'IsDefault')<>1
 OR COLUMNPROPERTY(OBJECT_ID(N'dbo.StockAdjustmentItems'),N'UnitCost',N'Scale')<>4
 THROW 51990,'Expected migration columns or precision were not verified.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.VendorInvoiceLineItem') AND name=N'IX_VendorInvoiceLineItem_LandedCostItemId' AND is_unique=1 AND is_disabled=0)
 OR NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_VendorInvoiceLineItem_LandedCostItems_LandedCostItemId' AND is_disabled=0 AND is_not_trusted=0)
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.WarehouseLocations') AND name=N'UX_WarehouseLocations_Default' AND is_unique=1 AND is_disabled=0)
 THROW 51990,'Expected trusted or unique constraints were not verified.',1;
IF EXISTS(SELECT 1 FROM dbo.Warehouses w WHERE w.IsDeleted=0 AND
 (SELECT COUNT(*) FROM dbo.WarehouseLocations l WHERE l.TenantId=w.TenantId AND l.WarehouseId=w.Id AND l.IsDefault=1 AND l.IsActive=1 AND l.IsDeleted=0
  AND l.LocationType=N'Bin' AND l.IsConsignmentBin=0 AND l.ConsignmentWarehouseId IS NULL
  AND l.IsQuarantineLocation=0 AND l.IsInspectionLocation=0 AND l.IsInTransitLocation=0 AND l.IsShippingLocation=0
  AND l.IsStagingLocation=0 AND l.IsReturnLocation=0 AND l.IsDamageLocation=0)<>1)
 THROW 51990,'A warehouse does not have exactly one eligible active default bin.',1;
'@
        $mainAfterGuards = @(Read-MainRows $mainGuardSql)
        if ($mainAfterGuards.Count -ne 7 -or @($mainAfterGuards | Where-Object { $_.is_disabled -or -not $_.DefinitionHash }).Count) { throw 'Expected guards are missing or disabled after migration.' }
        $mainTransaction.Commit()
        [pscustomobject]@{ Database='RhemaERP'; Applied=$true; BackupVerified=$true; BackupPath=$mainBackupPath;
            AppliedMigrations=@($mainExpected.Keys); PreservedTables=$mainAfterBaseline; Guards=$mainAfterGuards;
            Scope='Schema/control migration parity only. No count approvals, inventory/GL posting, broad seeding or legacy-total repair.' } | ConvertTo-Json -Depth 6
    } catch {
        try { $mainTransaction.Rollback() } catch { }
        throw
    } finally { $mainTransaction.Dispose(); $mainTransaction=$null }
} finally { $mainConnection.Dispose() }
