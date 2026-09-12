# Audit is read-only. GenerateSql uses the existing compiled EF assemblies, never builds.
# PreviewRollback executes reviewed schema-only SQL in ONE outer transaction and ALWAYS
# rolls it back. There is deliberately no Apply/Commit mode and no operational fixture DML.
param(
    [ValidateSet('RhemaERP', 'RhemaERP_PO_Rehearsal_20260909')]
    [string]$Database = 'RhemaERP_PO_Rehearsal_20260909',
    [ValidateSet('Audit', 'GenerateSql', 'PreviewRollback')]
    [string]$Mode = 'Audit',
    [ValidateSet('Initial', 'LegacyModules', 'InventoryFinal', 'TransferAutoComplete', 'TransferDraftLineGuard')]
    [string]$Phase = 'Initial',
    [string]$SqlScriptPath,
    [string]$ExpectedSqlSha256,
    [switch]$DiagnosePaymentAnchor
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data
$previewRepo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$previewServer = 'RHEMA-MICHAEL\SQL2017'
$previewBaseline = '20260912013000_AlignStockAdjustmentLocationValuation'
$previewBaselineCount = 462
$previewNullableDefinitionTables = @('ProcurementReceiptInspectionCases')
$previewNewTableColumns = [ordered]@{}
$previewMigrations = @(
    '20260912100000_OptionalApprovalPolicyGuard',
    '20260912113000_AllowOptionalProcurementApproval',
    '20260912120000_InventoryOptionalApprovalSnapshots',
    '20260912130000_ReceiptInspectionOptionalWorkflow',
    '20260912140000_VendorInvoiceOptionalApproval',
    '20260912141000_JournalOptionalApprovalSubmission',
    '20260912143000_OptionalTenderPreparationApproval',
    '20260912150000_UnitJournalEntryOptionalWorkflow',
    '20260912153000_OptionalRfqEvaluationApproval',
    '20260912155000_OptionalControlledTenderApproval',
    '20260912160000_FinancePurchasingOptionalApproval',
    '20260912170000_ArInvoiceOptionalWorkflow',
    '20260912180000_AllocationRunOptionalApproval'
)
$previewFlagTables = @('PurchaseRequisitions','PurchaseOrders','ProcurementBudgets','ProcurementPlans',
    'ProcurementBudgetRevisions','PhysicalCounts','StockAdjustments','InventoryReturnVouchers',
    'ProcurementReceiptInspectionCases','VendorInvoice','Tenders','UnitJournalEntries',
    'ProcurementRfqEvaluations','ProcurementTenderControls','FinancePurchaseOrders','FinancePurchaseOrderReceipts',
    'Invoices','AllocationRunBatches')
$previewNewLineageTables = @('UnitJournalEntries','Invoices')
$previewTables = @($previewFlagTables + @('PhysicalCountItems','PhysicalCountActions','StockAdjustmentItems',
    'StockAdjustmentActions','InventoryReturnVoucherActions','InventoryReturnVoucherLines',
    'ProcurementReceiptInspectionActions','ProcurementReceiptInspectionLines','PurchaseOrderReceiptItems',
    'StockMovements','WarehouseQuantities','InventoryItems','JournalEntries','AccountTransactions',
    'FinancePostingEvents','WorkflowEntityTypes','WorkflowDefinitions','WorkflowInstances',
    'ProcurementAwardReadinessDecisions','TenderAwards','FinancePurchaseOrderItems','FinancePurchaseOrderReceiptItems',
    'InvoiceLineItem','VendorInvoiceLineItem','AllocationRunBatchLines') | Sort-Object -Unique)
$previewExpectedChangedModules = @(
    'WorkflowApprovalEntityKey','WorkflowApprovalRequiredAtSubmission',
    'TR_ProcurementRequisitionSourcingReleases_TenantGuard','TR_PurchaseOrders_GovernedCommitment','TR_PurchaseOrders_SodHardStop',
    'TR_StockAdjustments_ControlledLifecycle','TR_StockAdjustmentActions_AppendOnly',
    'TR_InventoryReturnVouchers_ControlledLifecycle','TR_InventoryReturnVoucherActions_AppendOnly','TR_InventoryReturnVoucherLines_AppendOnly',
    'TR_PhysicalCountActions_AppendOnly','TR_PhysicalCounts_ControlledLifecycle','TR_StockMovements_PhysicalCountFreeze',
    'TR_ProcurementReceiptInspectionCases_TDC0502Protected','TR_PurchaseOrderReceiptItems_TDC0502AcceptanceProtected',
    'TR_ProcurementReceiptInspectionActions_TDC0503SodHardStop',
    'TR_VendorInvoices_OptionalApproval','TR_JournalEntries_OptionalApprovalSubmission','TR_Tenders_ApprovalPolicy',
    'TR_UnitJournalEntries_OptionalApproval','TR_ProcurementRfqEvaluations_Lifecycle',
    'TR_ProcurementAwardReadinessDecisions_Immutable','TR_ProcurementRfqEvaluations_ApprovalPolicy',
    'TR_ProcurementAwardReadinessDecisions_RfqApprovalPolicy','TR_ProcurementTenderControls_Lifecycle',
    'TR_ProcurementTenderControls_ApprovalPolicy','TR_ProcurementAwardReadinessDecisions_TenderApprovalPolicy',
    'TR_FinancePurchaseOrders_ApprovalPolicy','TR_FinancePurchaseOrderReceipts_ApprovalPolicy',
    'TR_Invoices_OptionalApproval','TR_AllocationRunBatches_ApprovalPolicy'
) + @('PurchaseRequisitions','PurchaseOrders','ProcurementBudgets','ProcurementPlans','ProcurementBudgetRevisions' |
    ForEach-Object { "TR_${_}_ApprovalPolicy" })
if ($Phase -eq 'LegacyModules') {
    # This is an explicitly separate checkpoint. Never expand the initial thirteen-migration scope.
    $previewBaseline = '20260912180000_AllocationRunOptionalApproval'
    $previewBaselineCount = 475
    $previewMigrations = @(
        '20260912190000_VendorPaymentOptionalApproval',
        '20260912200000_JournalBatchOptionalWorkflow',
        '20260912210000_OptionalExceptionalAndPrequalificationApproval',
        '20260912220000_VendorPaymentDirectEvidence'
    )
    $previewFlagTables = @('VendorPayment','PaymentBatch','JournalBatches',
        'ProcurementExceptionalSourcingControls','ProcurementPrequalificationExercises')
    $previewNewLineageTables = @()
    $previewNullableDefinitionTables = @('ProcurementExceptionalSourcingControls','ProcurementPrequalificationExercises')
    $previewTables = @($previewTables + $previewFlagTables + @(
        'VendorPaymentAllocation','PaymentBatchItem','PaymentBatchInvoice',
        'JournalBatchItems','JournalBatchItemReviews','JournalBatchPostingRuns','JournalBatchPostingRunItems',
        'ProcurementPrequalificationApplications','ProcurementPrequalificationScores','ProcurementQualifiedListEntries',
        'CentralDocumentRecords','CentralDocumentVersions','FileUploadRecords') | Sort-Object -Unique)
    $previewNewTableColumns['VendorPaymentEvidenceLinks'] = @(
        'Id','TenantId','VendorPaymentId','RequirementKey','ClientRequestId','RequestHash',
        'FileUploadRecordId','CentralDocumentRecordId','CentralDocumentVersionId','FileName','ContentType',
        'FileSize','ChecksumSha256','ExpiryDate','CreatedAt','UpdatedAt','CreatedBy','UpdatedBy','CreatedById',
        'LastModifiedById','IsDeleted','DeletedAt','DeletedBy')
    $previewExpectedChangedModules = @(
        'TR_VendorPayment_TDC0506InvoiceProcessorSod','TR_PaymentBatch_TDC0506InvoiceProcessorSod',
        'TR_PaymentBatch_TDC0505Readiness','TR_PaymentBatch_OptionalApproval','TR_VendorPayment_OptionalApproval',
        'TR_JournalBatches_OptionalApproval','TR_JournalBatchItems_OptionalApproval','TR_JournalEntries_BatchOptionalApproval',
        'TR_ProcurementExceptionalSourcingControls_ApprovalPolicy','TR_ProcurementPrequalificationExercises_ApprovalPolicy',
        'TR_ProcurementExceptionalSourcingControls_Lifecycle','TR_ProcurementPrequalificationExercises_Lifecycle',
        'TR_ProcurementAwardReadinessDecisions_Immutable','TR_ProcurementAwardReadinessDecisions_ExceptionalApprovalPolicy',
        'TR_VendorPaymentEvidenceLinks_SourceGuard','TR_VendorPayment_DirectEvidence'
    )
}
if ($Phase -in @('InventoryFinal','TransferAutoComplete','TransferDraftLineGuard')) {
    # Fixed follow-up only. Do not replay or widen the already-applied Phase 1/2 migrations.
    $previewBaseline = '20260912220000_VendorPaymentDirectEvidence'
    $previewBaselineCount = 479
    $previewMigrations = @(
        '20260912230000_InventoryTransferOptionalApproval',
        '20260912231000_PurchaseReturnOptionalApproval'
    )
    $previewFlagTables = @('InventoryTransfers','PurchaseReturns')
    $previewNewLineageTables = @()
    $previewNullableDefinitionTables = @()
    $previewNewTableColumns = [ordered]@{}
    $previewTables = @($previewTables + $previewFlagTables + @(
        'InventoryTransferItems','InventoryTransferActions','InventoryTransferActionLines',
        'InventoryTransferDiscrepancies','InventoryTransferDiscrepancyEvidence','PurchaseReturnItems',
        'InventoryBalances','InventoryLayers','InventoryMovements','InventoryLocations',
        'InventoryValuationReconciliations','InventoryValuationReconciliationActions',
        'Warehouses','WarehouseLocations','AuditLogs','ProcurementControlEvents','ProcurementControlEventEvidenceLinks',
        'VendorPayment','PaymentBatch','VendorPaymentAllocation','PaymentBatchItem','PaymentBatchInvoice',
        'VendorPaymentEvidenceLinks','JournalBatches','JournalBatchItems','JournalBatchItemReviews',
        'JournalBatchPostingRuns','JournalBatchPostingRunItems',
        'ProcurementExceptionalSourcingControls','ProcurementPrequalificationExercises',
        'ProcurementPrequalificationApplications','ProcurementPrequalificationScores','ProcurementQualifiedListEntries',
        'CentralDocumentRecords','CentralDocumentVersions','FileUploadRecords') | Sort-Object -Unique)
    $previewExpectedChangedModules = @(
        'TR_InventoryTransfers_ControlledLifecycle',
        'TR_InventoryTransferDiscrepancies_ControlledLifecycle',
        'TR_PurchaseReturns_OptionalApproval'
    )
    if ($Phase -eq 'TransferAutoComplete') {
        $previewBaseline = '20260912231000_PurchaseReturnOptionalApproval'
        $previewBaselineCount = 481
        $previewMigrations = @('20260912232000_InventoryTransferAutomaticCompletion')
        $previewFlagTables = @()
        $previewExpectedChangedModules = @('TR_InventoryTransfers_ControlledLifecycle')
    }
    if ($Phase -eq 'TransferDraftLineGuard') {
        if ($Database -ne 'RhemaERP_PO_Rehearsal_20260909') { throw 'Draft-line guard follow-up is restricted to rehearsal only.' }
        $previewBaseline = '20260912232000_InventoryTransferAutomaticCompletion'
        $previewBaselineCount = 482
        $previewMigrations = @('20260912233000_InventoryTransferDraftLineCompletionGuard')
        $previewFlagTables = @()
        $previewExpectedChangedModules = @('TR_InventoryTransfers_ControlledLifecycle')
    }
}
$previewConnection = [System.Data.SqlClient.SqlConnection]::new(
    "Server=$previewServer;Database=$Database;Integrated Security=True;ApplicationIntent=ReadOnly;Application Name=OptionalApprovalRollbackPreview")
$previewTransaction = $null
$previewColumns = [ordered]@{}

function Read-PreviewRows([string]$Sql) {
    $previewCommand = $previewConnection.CreateCommand()
    $previewCommand.CommandTimeout = 60
    $previewCommand.CommandText = $Sql
    if ($previewTransaction) { $previewCommand.Transaction = $previewTransaction }
    try {
        $previewReader = $previewCommand.ExecuteReader()
        try {
            while ($previewReader.Read()) {
                $previewValues = [ordered]@{}
                for ($previewIndex=0; $previewIndex -lt $previewReader.FieldCount; $previewIndex++) {
                    $previewValues[$previewReader.GetName($previewIndex)] = if ($previewReader.IsDBNull($previewIndex)) { $null } else { $previewReader.GetValue($previewIndex) }
                }
                [pscustomobject]$previewValues
            }
        } finally { $previewReader.Dispose() }
    } finally { $previewCommand.Dispose() }
}

function Invoke-PreviewBatch([string]$Sql) {
    if (-not $previewTransaction) { throw 'DDL requires the rollback-only owner transaction.' }
    $previewCommand = $previewConnection.CreateCommand()
    $previewCommand.Transaction = $previewTransaction
    $previewCommand.CommandTimeout = 60
    $previewCommand.CommandText = $Sql
    try { [void]$previewCommand.ExecuteNonQuery() } finally { $previewCommand.Dispose() }
}

function Read-PreviewModules {
    @(Read-PreviewRows @'
SELECT s.name AS SchemaName,o.name AS ObjectName,o.type AS ObjectType,
 CONVERT(varchar(64),HASHBYTES('SHA2_256',m.definition),2) AS DefinitionHash,
 t.is_disabled AS IsDisabled
FROM sys.sql_modules m JOIN sys.objects o ON o.object_id=m.object_id
JOIN sys.schemas s ON s.schema_id=o.schema_id LEFT JOIN sys.triggers t ON t.object_id=o.object_id
WHERE s.name=N'dbo' ORDER BY o.name;
'@)
}

function Read-PreviewSchema {
    $previewNames = (@($previewTables) + @($previewNewTableColumns.Keys) | ForEach-Object { "N'$_'" }) -join ','
    @(Read-PreviewRows @"
SELECT t.name AS TableName,c.column_id AS ColumnNumber,c.name AS ColumnName,
 TYPE_NAME(c.user_type_id) AS SqlType,c.max_length AS MaxLength,c.precision AS Precision,c.scale AS Scale,
 c.is_nullable AS Nullable,c.is_computed AS IsComputed,dc.name AS DefaultName,dc.definition AS DefaultDefinition
FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id
LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=t.object_id AND dc.parent_column_id=c.column_id
WHERE SCHEMA_NAME(t.schema_id)=N'dbo' AND t.name IN ($previewNames)
ORDER BY t.name,c.column_id;
"@)
}

function Read-PreviewCheckConstraints {
    @(Read-PreviewRows @'
SELECT OBJECT_SCHEMA_NAME(c.parent_object_id) AS SchemaName,OBJECT_NAME(c.parent_object_id) AS TableName,
 c.name AS ConstraintName,c.definition AS Definition,c.is_disabled AS IsDisabled,c.is_not_trusted AS IsNotTrusted
FROM sys.check_constraints c WHERE OBJECT_SCHEMA_NAME(c.parent_object_id)=N'dbo'
ORDER BY TableName,ConstraintName;
'@)
}

function Read-PreviewTableInventory {
    @(Read-PreviewRows 'SELECT SCHEMA_NAME(schema_id) SchemaName,name TableName FROM sys.tables WHERE is_ms_shipped=0 ORDER BY SchemaName,TableName;')
}

function Assert-PreviewInventoryFinal($OriginalSchema, $OriginalModules, $OriginalChecks, $OriginalTables) {
    if ($Phase -notin @('InventoryFinal','TransferAutoComplete','TransferDraftLineGuard')) { return }
    $inventoryFinalSchema = @(Read-PreviewSchema)
    $inventoryColumnDelta = if ($Phase -in @('TransferAutoComplete','TransferDraftLineGuard')) { 0 } else { 2 }
    if ($inventoryFinalSchema.Count -ne $OriginalSchema.Count + $inventoryColumnDelta) {
        throw "The selected $Phase migration changed an unexpected number of columns."
    }
    foreach ($inventoryFinalColumn in $OriginalSchema) {
        $inventoryFinalNow = @($inventoryFinalSchema | Where-Object {
            $_.TableName -eq $inventoryFinalColumn.TableName -and $_.ColumnName -eq $inventoryFinalColumn.ColumnName
        })
        if ($inventoryFinalNow.Count -ne 1) { throw 'An original inventory schema column disappeared.' }
        Assert-PreviewEqual $inventoryFinalColumn $inventoryFinalNow[0] 'Unchanged inventory column contract'
    }
    Assert-PreviewEqual $OriginalTables @(Read-PreviewTableInventory) 'InventoryFinal table inventory (no new tables)'
    Assert-PreviewEqual $OriginalChecks @(Read-PreviewCheckConstraints) 'InventoryFinal unchanged CHECK contracts'
    $inventoryFinalModules = @(Read-PreviewModules)
    $inventoryFinalChangedNames = @(
        foreach ($inventoryFinalModule in $inventoryFinalModules) {
            $inventoryFinalPrevious = @($OriginalModules | Where-Object ObjectName -eq $inventoryFinalModule.ObjectName)
            if ($inventoryFinalPrevious.Count -eq 0 -or $inventoryFinalModule.DefinitionHash -ne $inventoryFinalPrevious[0].DefinitionHash) {
                if ($inventoryFinalModule.ObjectName -notin $previewExpectedChangedModules -or $inventoryFinalModule.IsDisabled -ne $false) {
                    throw 'InventoryFinal introduced an unreviewed or disabled SQL module.'
                }
                $inventoryFinalModule.ObjectName
            }
        }
    )
    Assert-PreviewEqual @($previewExpectedChangedModules | Sort-Object) @($inventoryFinalChangedNames | Sort-Object) 'Exact InventoryFinal changed module set'
}

function Read-PreviewRecords {
    foreach ($previewTable in $previewColumns.Keys) {
        $previewProjection = $previewColumns[$previewTable]
        Read-PreviewRows @"
SELECT N'$previewTable' AS TableName,COUNT_BIG(*) AS [RowCount],
 CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(nvarchar(max),
    (SELECT $previewProjection FROM dbo.[$previewTable] ORDER BY [Id] FOR JSON PATH,INCLUDE_NULL_VALUES))),2) AS RecordHash
FROM dbo.[$previewTable];
"@
    }
}

function Assert-PreviewEqual($Before, $After, [string]$Label) {
    if (($Before | ConvertTo-Json -Depth 10 -Compress) -cne ($After | ConvertTo-Json -Depth 10 -Compress)) {
        throw "$Label changed. Preview did not verify a clean rollback; inspect concurrent activity before proceeding."
    }
}

function Assert-PreviewNewTables {
    foreach ($previewNewTable in $previewNewTableColumns.Keys) {
        $previewNewColumns = @(Read-PreviewSchema | Where-Object TableName -eq $previewNewTable)
        Assert-PreviewEqual @($previewNewTableColumns[$previewNewTable] | Sort-Object) @($previewNewColumns.ColumnName | Sort-Object) 'Exact new attachment columns'
        if (@(Read-PreviewRows "SELECT COUNT_BIG(*) AS [RowCount] FROM dbo.[$previewNewTable]")[0].RowCount -ne 0) {
            throw 'A schema migration must not create payment documents or change business data.'
        }
        $previewForeignKeys = @(Read-PreviewRows @"
SELECT OBJECT_NAME(f.referenced_object_id) ReferencedTable,f.is_disabled Disabled,f.is_not_trusted NotTrusted,
 f.delete_referential_action DeleteAction,f.update_referential_action UpdateAction
FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.$previewNewTable') ORDER BY ReferencedTable;
"@)
        Assert-PreviewEqual @('CentralDocumentRecords','CentralDocumentVersions','FileUploadRecords','Tenants','VendorPayment') @($previewForeignKeys.ReferencedTable) 'Exact attachment foreign-key owners'
        if (@($previewForeignKeys | Where-Object { $_.Disabled -or $_.NotTrusted -or $_.DeleteAction -ne 0 -or $_.UpdateAction -ne 0 }).Count) {
            throw 'Payment document links require enabled, trusted, restrictive foreign keys.'
        }
        $previewUniqueIndexes = @(Read-PreviewRows "SELECT name IndexName,is_disabled Disabled,has_filter HasFilter,filter_definition FilterDefinition FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.$previewNewTable') AND is_unique=1 ORDER BY name")
        Assert-PreviewEqual @('PK_VendorPaymentEvidenceLinks','UX_VendorPaymentEvidenceLinks_Tenant_Payment_Requirement_Hash','UX_VendorPaymentEvidenceLinks_Tenant_Request') @($previewUniqueIndexes.IndexName) 'Exact payment evidence identity and duplicate guards'
        # EF's legacy SQL generator may emit redundant IS NOT NULL predicates when
        # a hand-authored migration has no target model. Every named key is NOT
        # NULL, so only these exact predicates are equivalent to an unfiltered key.
        $previewAllowedIndexKeys = @{
            PK_VendorPaymentEvidenceLinks = @('Id')
            UX_VendorPaymentEvidenceLinks_Tenant_Request = @('TenantId','ClientRequestId')
            UX_VendorPaymentEvidenceLinks_Tenant_Payment_Requirement_Hash = @('TenantId','VendorPaymentId','RequirementKey','ChecksumSha256')
        }
        foreach ($previewIdentityIndex in $previewUniqueIndexes) {
            if ($previewIdentityIndex.Disabled) { throw 'Payment evidence identity guards must remain enabled.' }
            $previewIdentityKeys = $previewAllowedIndexKeys[$previewIdentityIndex.IndexName]
            $previewActualIdentityKeys = @(Read-PreviewRows @"
SELECT c.name ColumnName
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE i.object_id=OBJECT_ID(N'dbo.$previewNewTable')
  AND i.name=N'$($previewIdentityIndex.IndexName)' AND ic.key_ordinal>0
ORDER BY ic.key_ordinal;
"@)
            Assert-PreviewEqual @($previewIdentityKeys) @($previewActualIdentityKeys.ColumnName) 'Exact ordered payment evidence identity keys'
            foreach ($previewIdentityKey in $previewIdentityKeys) {
                if (@($previewNewColumns | Where-Object { $_.ColumnName -eq $previewIdentityKey -and -not $_.Nullable }).Count -ne 1) {
                    throw 'Payment evidence unique keys must retain non-null columns.'
                }
            }
            if ($previewIdentityIndex.HasFilter) {
                $previewExpectedFilter = ($previewIdentityKeys | ForEach-Object { $_ + 'ISNOTNULL' }) -join 'AND'
                if ($previewIdentityIndex.IndexName -eq 'PK_VendorPaymentEvidenceLinks' -or
                    ($previewIdentityIndex.FilterDefinition -replace '[()\[\]\s]','') -ine $previewExpectedFilter) {
                    throw 'Payment evidence duplicate guards contain an unexpected filter.'
                }
            }
        }
    }
}

try {
    $previewConnection.Open()
    $previewIdentity = @(Read-PreviewRows "SELECT DB_NAME() AS DatabaseName,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName")[0]
    if ($previewIdentity.DatabaseName -ne $Database -or $previewIdentity.ServerName -ne $previewServer) { throw 'Exact local database/server was not verified.' }
    $previewHistory = @(Read-PreviewRows 'SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId')
    if ($previewHistory.Count -ne $previewBaselineCount -or $previewHistory[-1].MigrationId -ne $previewBaseline) { throw "Expected $previewBaselineCount-migration $Phase baseline is not present; no preview DDL is allowed." }
    $previewTenant = @(Read-PreviewRows "SELECT Id FROM dbo.Tenants WHERE Id='00000000-0000-0000-0000-000000000001' AND IsDeleted=0")
    if ($previewTenant.Count -ne 1) { throw 'Expected local UAT tenant is not present.' }
    $previewSchema = @(Read-PreviewSchema)
    if (@($previewSchema | Where-Object { $_.TableName -in $previewNewTableColumns.Keys }).Count) {
        throw 'A new payment evidence table already exists outside the reviewed migration baseline.'
    }
    foreach ($previewTable in $previewTables) {
        $previewTableColumns = @($previewSchema | Where-Object TableName -eq $previewTable)
        if ($previewTableColumns.Count -eq 0 -or 'Id' -notin $previewTableColumns.ColumnName) { throw "Expected table/identity missing: $previewTable" }
        $previewColumns[$previewTable] = ($previewTableColumns | ForEach-Object { '[' + $_.ColumnName.Replace(']',']]') + ']' }) -join ','
    }
    $previewUnexpectedFlags = @($previewSchema | Where-Object { $_.TableName -in $previewFlagTables -and $_.ColumnName -eq 'ApprovalRequired' })
    if ($previewUnexpectedFlags.Count) { throw 'Optional-approval columns already exist outside the expected migration baseline.' }
    $previewModules = @(Read-PreviewModules)
    $previewChecks = @(Read-PreviewCheckConstraints)
    $previewTableInventory = @(Read-PreviewTableInventory)
    $previewDisabled = @($previewModules | Where-Object { $_.ObjectName -in $previewExpectedChangedModules -and $_.IsDisabled -eq $true })
    if ($previewDisabled.Count) { throw 'A protected baseline trigger is disabled; preview refused.' }
    [pscustomobject]@{ Mode=$Mode; Phase=$Phase; Database=$Database; Server=$previewServer; MigrationCount=$previewHistory.Count; Baseline=$previewBaseline; BusinessTables=$previewTables.Count; OptionalApprovalColumns=0; DdlExecuted=$false }

    if ($Mode -eq 'Audit') { return }

    if ($Mode -eq 'GenerateSql') {
        $previewDataDll = Get-Item -LiteralPath (Join-Path $previewRepo 'src/ErpSystem.Api/bin/Debug/net8.0/ErpSystem.Data.dll')
        foreach ($previewMigration in $previewMigrations) {
            $previewSource = Get-Item -LiteralPath (Join-Path $previewRepo "src/ErpSystem.Data/Migrations/$previewMigration.cs")
            if ($previewSource.LastWriteTimeUtc -gt $previewDataDll.LastWriteTimeUtc) { throw "Compiled Data assembly predates $previewMigration; rebuild checkpoint required." }
        }
        if (-not $SqlScriptPath) {
            $previewOutput = Join-Path $previewRepo ('local-artifacts/optional-approval-' + $Phase + '-preview-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
            [void](New-Item -ItemType Directory -Path $previewOutput)
            $SqlScriptPath = Join-Path $previewOutput 'optional-approval.sql'
        }
        if (Test-Path -LiteralPath $SqlScriptPath) { throw 'Refusing to overwrite an existing SQL artifact.' }
        $previewEnvironment = [ordered]@{}
        $previewOverrides = @{
            ConnectionStrings__DefaultConnection="Server=$previewServer;Database=$Database;Integrated Security=True;TrustServerCertificate=True";
            SkipStartupInitialization='true'; BackgroundServices__Enabled='false';
            # Fixed compiled Up-method scripting only: no model authoring, build,
            # migrations add/remove, or database update is performed by this mode.
            TdcAllowFastEfRuntimeMigrationCommands='true'
        }
        try {
            foreach ($previewName in $previewOverrides.Keys) {
                $previewEnvironment[$previewName] = [Environment]::GetEnvironmentVariable($previewName,'Process')
                [Environment]::SetEnvironmentVariable($previewName,$previewOverrides[$previewName],'Process')
            }
            $previewEfOutput = @(& dotnet ef migrations script $previewBaseline $previewMigrations[-1] --no-build --no-transactions --configuration Debug --context ApplicationDbContext --project (Join-Path $previewRepo 'src/ErpSystem.Data/ErpSystem.Data.csproj') --startup-project (Join-Path $previewRepo 'src/ErpSystem.Api/ErpSystem.Api.csproj') --output $SqlScriptPath 2>&1)
            if ($LASTEXITCODE -ne 0) { throw "EF SQL generation failed with exit code $LASTEXITCODE; no database SQL was executed." }
        } finally {
            foreach ($previewName in $previewEnvironment.Keys) { [Environment]::SetEnvironmentVariable($previewName,$previewEnvironment[$previewName],'Process') }
        }
        [pscustomobject]@{ SqlScriptPath=(Resolve-Path -LiteralPath $SqlScriptPath).Path; SqlSha256=(Get-FileHash -LiteralPath $SqlScriptPath -Algorithm SHA256).Hash; DataDllSha256=(Get-FileHash -LiteralPath $previewDataDll.FullName -Algorithm SHA256).Hash; DdlExecuted=$false }
        return
    }

    if (-not $SqlScriptPath -or $ExpectedSqlSha256 -notmatch '^[A-Fa-f0-9]{64}$') { throw 'PreviewRollback requires an explicitly reviewed SQL path and SHA256.' }
    # Hash the exact bytes subsequently executed; do not reopen a replaceable file.
    $previewSqlBytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $SqlScriptPath).Path)
    $previewSqlHasher = [Security.Cryptography.SHA256]::Create()
    try { $previewLoadedSqlHash = [Convert]::ToHexString($previewSqlHasher.ComputeHash($previewSqlBytes)) }
    finally { $previewSqlHasher.Dispose() }
    if ($previewLoadedSqlHash -ne $ExpectedSqlSha256) { throw 'Reviewed SQL hash does not match.' }
    $previewSql = [Text.UTF8Encoding]::new($false, $true).GetString($previewSqlBytes).TrimStart([char]0xFEFF)
    if ($previewSql -match '(?im)^\s*(COMMIT\b|ROLLBACK\b|BEGIN\s+TRAN|USE\s|BACKUP\b|RESTORE\b|DISABLE\s+TRIGGER)') { throw 'SQL contains forbidden transaction/database/guard control. Generate with --no-transactions.' }
    $previewBatches = @([regex]::Split($previewSql,'(?im)^\s*GO\s*\r?$') | Where-Object { $_.Trim() })
    $previewHistoryIds = @([regex]::Matches($previewSql,"(?is)INSERT\s+INTO\s+\[__EFMigrationsHistory\].*?VALUES\s*\(N'([^']+)'") | ForEach-Object { $_.Groups[1].Value })
    Assert-PreviewEqual $previewMigrations $previewHistoryIds 'Generated migration scope'
    $previewRecords = @(Read-PreviewRecords)
    $previewBatchNumber = 0
    $previewSucceeded = $false
    $previewFailure = $null
    try {
        $previewTransaction = $previewConnection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
        Invoke-PreviewBatch 'SET XACT_ABORT ON; SET LOCK_TIMEOUT 5000; SET DEADLOCK_PRIORITY LOW;'
        foreach ($previewBatch in $previewBatches) {
            $previewBatchNumber++
            # Migration history is never inserted, even temporarily. The actual Up DDL runs unchanged.
            if ($previewBatch.Trim() -match "(?is)^INSERT\s+INTO\s+\[__EFMigrationsHistory\]\s*\(\[MigrationId\],\s*\[ProductVersion\]\)\s*VALUES\s*\(N'[^']+',\s*N'[^']+'\);?\s*$") { continue }
            if ($previewBatch -match '(?i)INSERT\s+INTO\s+\[__EFMigrationsHistory\]') { throw 'Unexpected combined history batch; review SQL before execution.' }
            if ($DiagnosePaymentAnchor -and $previewBatch.Contains("OBJECT_ID(N'dbo.TR_VendorPayment_OptionalApproval')")) {
                $anchorMatch=[regex]::Match($previewBatch,"(?s)DECLARE @before nvarchar\(max\)=N'((?:''|[^'])*)';")
                if ($anchorMatch.Success) {
                    $anchorLiteral=$anchorMatch.Groups[1].Value
                    Read-PreviewRows @"
DECLARE @definition nvarchar(max)=REPLACE(OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_VendorPayment_OptionalApproval')),CHAR(13),N'');
DECLARE @before nvarchar(max)=N'$anchorLiteral';
SELECT LEN(@definition) DefinitionLength,LEN(@before) AnchorLength,CHARINDEX(@before,@definition) ExactAnchorPosition,
 (LEN(@definition)-LEN(REPLACE(@definition,@before,N'')))/NULLIF(LEN(@before),0) AnchorOccurrences,
 SUBSTRING(@definition,CHARINDEX(N'OR EXISTS (SELECT 1 FROM OPENJSON(i.ApprovalControlSnapshotJson',@definition),300) DefinitionExcerpt,
 @before AnchorExcerpt;
"@
                }
            }
            Invoke-PreviewBatch $previewBatch
        }
        Assert-PreviewEqual $previewRecords @(Read-PreviewRecords) 'Original operational records inside preview'
        Assert-PreviewNewTables
        foreach ($previewTable in $previewFlagTables) {
            $previewFlag = @(Read-PreviewRows "SELECT COUNT_BIG(*) AS InvalidRows FROM dbo.[$previewTable] WHERE ApprovalRequired<>1 OR ApprovalRequired IS NULL")[0]
            if ($previewFlag.InvalidRows -ne 0) { throw "$previewTable historical approval mode was not preserved." }
        }
        $previewNewSchema = @(Read-PreviewSchema)
        foreach ($previewDefinitionTable in $previewNullableDefinitionTables) {
            $previewDefinition = @($previewNewSchema | Where-Object { $_.TableName -eq $previewDefinitionTable -and $_.ColumnName -eq 'WorkflowDefinitionId' })
            if ($previewDefinition.Count -ne 1 -or $previewDefinition[0].SqlType -ne 'uniqueidentifier' -or
                $previewDefinition[0].Nullable -ne $true -or $previewDefinition[0].DefaultDefinition) {
                throw "$previewDefinitionTable selected workflow must be nullable without an invented default."
            }
        }
        foreach ($previewTable in $previewFlagTables) {
            $previewFlagColumn = @($previewNewSchema | Where-Object { $_.TableName -eq $previewTable -and $_.ColumnName -eq 'ApprovalRequired' })
            $previewRequiredDefault = if ($previewFlagColumn.Count -eq 1) {
                $previewFlagColumn[0].DefaultDefinition -replace '[()\[\]\s]',''
            } else { '' }
            if ($previewFlagColumn.Count -ne 1 -or $previewFlagColumn[0].SqlType -ne 'bit' -or $previewFlagColumn[0].Nullable -ne $false -or
                $previewRequiredDefault -notin @('1','CAST1ASbit','CONVERTbit,1')) { throw "$previewTable approval default/type/nullability differs from the reviewed contract." }
        }
        foreach ($previewLineageTable in $previewNewLineageTables) {
            $previewLineage = @($previewNewSchema | Where-Object { $_.TableName -eq $previewLineageTable -and $_.ColumnName -eq 'WorkflowInstanceId' })
            if ($previewLineage.Count -ne 1 -or $previewLineage[0].SqlType -ne 'uniqueidentifier' -or
                $previewLineage[0].Nullable -ne $true -or $previewLineage[0].DefaultDefinition) {
                throw "$previewLineageTable workflow lineage must be a nullable identifier without an invented default."
            }
            if (@(Read-PreviewRows "SELECT COUNT_BIG(*) AS InvalidRows FROM dbo.[$previewLineageTable] WHERE WorkflowInstanceId IS NOT NULL")[0].InvalidRows -ne 0) {
                throw "The migration invented workflow lineage for historical $previewLineageTable entries."
            }
        }
        $previewChanged = @(Read-PreviewModules)
        foreach ($previewModule in $previewModules) {
            $previewNow = @($previewChanged | Where-Object ObjectName -eq $previewModule.ObjectName)
            if ($previewNow.Count -ne 1) { throw "Existing module disappeared: $($previewModule.ObjectName)" }
            if ($previewModule.IsDisabled -ne $previewNow[0].IsDisabled) { throw "Trigger enabled state changed: $($previewModule.ObjectName)" }
            if ($previewModule.ObjectName -notin $previewExpectedChangedModules -and $previewModule.DefinitionHash -ne $previewNow[0].DefinitionHash) { throw "Unrelated module changed: $($previewModule.ObjectName)" }
        }
        Assert-PreviewInventoryFinal $previewSchema $previewModules $previewChecks $previewTableInventory
        $previewSucceeded = $true
    } catch {
        $previewFailure = $_
    } finally {
        if ($previewTransaction) {
            $previewOwnedTransaction = $previewTransaction
            $previewTransaction = $null
            try { $previewOwnedTransaction.Rollback() }
            catch {
                # XACT_ABORT may already have rolled back the transaction. Verify it;
                # never silently convert an unverified rollback into a success report.
                if ($previewConnection.State -ne [System.Data.ConnectionState]::Open) {
                    $previewConnection.Close(); $previewConnection.Open()
                }
                $previewTransactionState = @(Read-PreviewRows 'SELECT @@TRANCOUNT AS OpenTransactions')[0]
                if ($previewTransactionState.OpenTransactions -ne 0) { throw 'Rollback failed and the SQL session still has an open transaction.' }
            }
            finally { $previewOwnedTransaction.Dispose() }
        }
    }
    Assert-PreviewEqual $previewHistory @(Read-PreviewRows 'SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId') 'Migration history after rollback'
    Assert-PreviewEqual $previewSchema @(Read-PreviewSchema) 'Schema after rollback'
    Assert-PreviewEqual $previewChecks @(Read-PreviewCheckConstraints) 'Check constraint definitions/trusted states after rollback'
    Assert-PreviewEqual $previewModules @(Read-PreviewModules) 'Module definitions/enabled states after rollback'
    if ($Phase -in @('InventoryFinal','TransferAutoComplete','TransferDraftLineGuard')) { Assert-PreviewEqual $previewTableInventory @(Read-PreviewTableInventory) 'Table inventory after rollback' }
    Assert-PreviewEqual $previewRecords @(Read-PreviewRecords) 'Operational records after rollback'
    if ($previewFailure) { throw "Preview SQL batch $previewBatchNumber failed; schema/history/records were verified restored: $($previewFailure.Exception.Message)" }
    [pscustomobject]@{ Database=$Database; PreviewPassed=$previewSucceeded; SqlBatches=$previewBatchNumber; AlwaysRolledBack=$true; OriginalRowsUnchanged=$true; MigrationHistoryUnchanged=$true; SchemaAndGuardsRestored=$true; SqlSha256=$ExpectedSqlSha256 }
} finally {
    if ($previewTransaction) { try { $previewTransaction.Rollback() } finally { $previewTransaction.Dispose() } }
    $previewConnection.Dispose()
}
