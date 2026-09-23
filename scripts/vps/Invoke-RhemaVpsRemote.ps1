[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Preflight', 'Backup', 'Apply', 'ResumeFrontend', 'Verify')]
    [string]$Action,

    [string]$DeploymentId,
    [string]$ExpectedCommit,
    [string]$ExpectedBuildId,
    [string]$ExpectedCacheVersion,
    [string]$ApiPackageName,
    [string]$FrontendPackageName,
    [string]$ApiSha256,
    [string]$FrontendSha256,
    [string]$ExpectedPublicOrigin = 'https://63.141.230.56',
    [int]$ApiReadyTimeoutSeconds = 1800
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$RhemaRoot = 'C:\RhemaERP'
$ApiRoot = Join-Path $RhemaRoot 'api'
$FrontendRoot = Join-Path $RhemaRoot 'frontend'
$PackagesRoot = Join-Path $RhemaRoot 'packages'
$BackupsRoot = Join-Path $RhemaRoot 'backups'
$LogsRoot = Join-Path $RhemaRoot 'logs'
$ApiServiceXml = Join-Path $RhemaRoot 'services\api\RhemaERPAPI.xml'
$NssmParametersPath = 'HKLM:\SYSTEM\CurrentControlSet\Services\RhemaERPAPI\Parameters'
$FrontendNssmParametersPath = `
    'HKLM:\SYSTEM\CurrentControlSet\Services\RhemaERPFrontend\Parameters'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

$UsesNssmApiConfiguration = -not (Test-Path -LiteralPath $ApiServiceXml)
if ($UsesNssmApiConfiguration) {
    Assert-True (Test-Path -LiteralPath $NssmParametersPath) `
        "API service configuration is missing: $ApiServiceXml or $NssmParametersPath"
}

function Assert-DeploymentId {
    Assert-True (-not [string]::IsNullOrWhiteSpace($DeploymentId)) `
        'DeploymentId is required for this action.'
    Assert-True ($DeploymentId -match '^[a-zA-Z0-9-]+$') `
        'DeploymentId contains unsupported characters.'
}

function Get-ApiConfigurationXml {
    Assert-True (Test-Path -LiteralPath $ApiServiceXml) `
        "API service configuration is missing: $ApiServiceXml"
    return [xml](Get-Content -LiteralPath $ApiServiceXml)
}

function Get-NssmEnvironmentSnapshot {
    $properties = Get-ItemProperty -LiteralPath $NssmParametersPath
    $snapshot = [ordered]@{}
    foreach ($name in @('AppEnvironment', 'AppEnvironmentExtra')) {
        $exists = $null -ne $properties.PSObject.Properties[$name]
        $snapshot[$name] = [ordered]@{
            exists = $exists
            values = if ($exists) { @($properties.$name | ForEach-Object { [string]$_ }) } else { @() }
        }
    }
    return [pscustomobject]$snapshot
}

function Restore-NssmEnvironmentSnapshot {
    param([Parameter(Mandatory = $true)]$Snapshot)

    foreach ($name in @('AppEnvironment', 'AppEnvironmentExtra')) {
        $entry = $Snapshot.$name
        if ($entry.exists) {
            Set-ItemProperty -LiteralPath $NssmParametersPath -Name $name `
                -Value ([string[]]@($entry.values))
        }
        else {
            Remove-ItemProperty -LiteralPath $NssmParametersPath -Name $name `
                -ErrorAction SilentlyContinue
        }
    }
}

function Get-ApiServiceEnvironment {
    $environment = [ordered]@{}
    if (-not $UsesNssmApiConfiguration) {
        $xml = Get-ApiConfigurationXml
        foreach ($node in @($xml.service.env)) {
            $environment[[string]$node.name] = [string]$node.value
        }
        return $environment
    }

    $snapshot = Get-NssmEnvironmentSnapshot
    foreach ($source in @($snapshot.AppEnvironment.values, $snapshot.AppEnvironmentExtra.values)) {
        foreach ($entry in @($source)) {
            $separator = $entry.IndexOf('=')
            if ($separator -gt 0) {
                $environment[$entry.Substring(0, $separator)] = $entry.Substring($separator + 1)
            }
        }
    }
    return $environment
}

function Set-ApiServiceEnvironmentValues {
    param([Parameter(Mandatory = $true)][hashtable]$Values)

    if (-not $UsesNssmApiConfiguration) {
        $xml = Get-ApiConfigurationXml
        foreach ($entry in $Values.GetEnumerator()) {
            $node = @($xml.service.env | Where-Object { $_.name -eq $entry.Key })[0]
            if ($null -eq $node) {
                $node = $xml.CreateElement('env')
                $node.SetAttribute('name', [string]$entry.Key)
                [void]$xml.service.AppendChild($node)
            }
            $node.SetAttribute('value', [string]$entry.Value)
        }
        $settings = New-Object System.Xml.XmlWriterSettings
        $settings.Indent = $true
        $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
        $writer = [System.Xml.XmlWriter]::Create($ApiServiceXml, $settings)
        try { $xml.Save($writer) }
        finally { $writer.Close() }
        return
    }

    $environment = Get-ApiServiceEnvironment
    foreach ($entry in $Values.GetEnumerator()) {
        $environment[[string]$entry.Key] = [string]$entry.Value
    }
    $nssmValues = @($environment.GetEnumerator() | Sort-Object Key | ForEach-Object {
        "{0}={1}" -f $_.Key, $_.Value
    })
    Set-ItemProperty -LiteralPath $NssmParametersPath -Name AppEnvironmentExtra `
        -Value ([string[]]$nssmValues)
}

function Remove-ApiServiceEnvironmentValues {
    param([Parameter(Mandatory = $true)][string[]]$Names)

    if (-not $UsesNssmApiConfiguration) {
        $xml = Get-ApiConfigurationXml
        foreach ($name in $Names) {
            @($xml.service.env | Where-Object { $_.name -eq $name }) |
                ForEach-Object { [void]$xml.service.RemoveChild($_) }
        }
        $settings = New-Object System.Xml.XmlWriterSettings
        $settings.Indent = $true
        $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
        $writer = [System.Xml.XmlWriter]::Create($ApiServiceXml, $settings)
        try { $xml.Save($writer) }
        finally { $writer.Close() }
        return
    }

    $environment = Get-ApiServiceEnvironment
    foreach ($name in $Names) { [void]$environment.Remove($name) }
    $nssmValues = @($environment.GetEnumerator() | Sort-Object Key | ForEach-Object {
        "{0}={1}" -f $_.Key, $_.Value
    })
    Set-ItemProperty -LiteralPath $NssmParametersPath -Name AppEnvironmentExtra `
        -Value ([string[]]$nssmValues)
}

function Assert-SyncfusionLicenseConfigured {
    $environment = Get-ApiServiceEnvironment
    $configured = @(@('Syncfusion__LicenseKey', 'SyncfusionLicenseKey') | Where-Object {
        -not [string]::IsNullOrWhiteSpace([string]$environment[$_])
    })
    Assert-True ($configured.Count -gt 0) `
        'The protected Syncfusion license is missing from the API service environment.'
    Write-Output 'SYNCFUSION_LICENSE|CONFIGURED'
}

function Get-DatabaseConnectionString {
    $environment = Get-ApiServiceEnvironment
    $value = [string]$environment['ConnectionStrings__DefaultConnection']
    Assert-True (-not [string]::IsNullOrWhiteSpace($value)) `
        'Database connection setting is missing from the API service configuration.'
    return $value
}

function Invoke-DatabaseTable {
    param([string]$Query, [int]$TimeoutSeconds = 120)

    $connection = New-Object System.Data.SqlClient.SqlConnection `
        (Get-DatabaseConnectionString)
    $command = $connection.CreateCommand()
    $command.CommandText = $Query
    $command.CommandTimeout = $TimeoutSeconds
    $connection.Open()
    try {
        $reader = $command.ExecuteReader()
        $table = New-Object System.Data.DataTable
        $table.Load($reader)
        return $table
    }
    finally {
        $connection.Close()
    }
}

function Invoke-DatabaseNonQuery {
    param([string]$Query, [int]$TimeoutSeconds = 1800)

    $connection = New-Object System.Data.SqlClient.SqlConnection `
        (Get-DatabaseConnectionString)
    $command = $connection.CreateCommand()
    $command.CommandText = $Query
    $command.CommandTimeout = $TimeoutSeconds
    $connection.Open()
    try {
        [void]$command.ExecuteNonQuery()
    }
    finally {
        $connection.Close()
    }
}

function Invoke-RobocopyChecked {
    param([string[]]$Arguments)

    & robocopy.exe @Arguments | Out-Null
    $code = $LASTEXITCODE
    if ($code -gt 7) {
        throw "Robocopy failed with exit code $code."
    }
}

function Set-TestServerConfiguration {
    $existingCorsNames = @((Get-ApiServiceEnvironment).Keys | Where-Object {
        $_ -like 'CorsSettings__AllowedOrigins__*'
    })
    if ($existingCorsNames.Count -gt 0) {
        Remove-ApiServiceEnvironmentValues $existingCorsNames
    }
    Set-ApiServiceEnvironmentValues @{
        'CorsSettings__AllowedOrigins__0' = $ExpectedPublicOrigin
        'StartupInitialization__SeedDevelopmentData' = 'true'
        'StartupInitialization__AllowDevelopmentDataSeedingOutsideDevelopment' = 'true'
        'CandidatePortal__PortalUrl' = $ExpectedPublicOrigin
        'FrontendUrl' = $ExpectedPublicOrigin
    }
}

function Get-MigrationHistory {
    return Invoke-DatabaseTable @"
SELECT MigrationId
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId;
"@
}

function Get-MigrationGuardResults {
    return Invoke-DatabaseTable @"
SET NOCOUNT ON;
DECLARE @R TABLE (CheckName nvarchar(160), AffectedRows bigint);
IF OBJECT_ID(N'EmployeeShiftPreferences',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''EmployeeShiftPreferences'',COUNT_BIG(*) FROM EmployeeShiftPreferences');
IF OBJECT_ID(N'Shifts',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''Shifts'',COUNT_BIG(*) FROM Shifts');
IF OBJECT_ID(N'WorkStations',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''WorkStations'',COUNT_BIG(*) FROM WorkStations');
IF COL_LENGTH('ShiftAssignments','StartDate') IS NOT NULL INSERT @R EXEC(N'SELECT ''ShiftAssignments.StartDate'',COUNT_BIG(*) FROM ShiftAssignments WHERE StartDate IS NOT NULL');
IF COL_LENGTH('LeaveRequests','ApprovalNotes') IS NOT NULL INSERT @R EXEC(N'SELECT ''LeaveRequests.ApprovalNotes'',COUNT_BIG(*) FROM LeaveRequests WHERE ApprovalNotes IS NOT NULL');
IF COL_LENGTH('LeaveRequests','RejectionDate') IS NOT NULL INSERT @R EXEC(N'SELECT ''LeaveRequests.RejectionDate'',COUNT_BIG(*) FROM LeaveRequests WHERE RejectionDate IS NOT NULL');
IF OBJECT_ID(N'ShiftAssignments',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''ShiftAssignments rows'',COUNT_BIG(*) FROM ShiftAssignments');
IF COL_LENGTH('LeavePlans','DepartmentId') IS NOT NULL INSERT @R EXEC(N'SELECT ''LeavePlans.DepartmentId'',COUNT_BIG(*) FROM LeavePlans WHERE DepartmentId IS NOT NULL');
IF COL_LENGTH('LeaveBalances','AdjustmentReason') IS NOT NULL INSERT @R EXEC(N'SELECT ''LeaveBalances.AdjustmentReason'',COUNT_BIG(*) FROM LeaveBalances WHERE AdjustmentReason IS NOT NULL');
IF COL_LENGTH('EmployeePositions','MinSalary') IS NOT NULL INSERT @R EXEC(N'SELECT ''EmployeePositions legacy fields'',COUNT_BIG(*) FROM EmployeePositions WHERE MinSalary IS NOT NULL OR MaxSalary IS NOT NULL OR Requirements IS NOT NULL OR Responsibilities IS NOT NULL');
IF COL_LENGTH('EmployeeIdentificationCards','DocumentType') IS NOT NULL INSERT @R EXEC(N'SELECT ''EmployeeIdentificationCards legacy fields'',COUNT_BIG(*) FROM EmployeeIdentificationCards WHERE DocumentType IS NOT NULL OR IssuingAuthority IS NOT NULL');
IF COL_LENGTH('EmployeeDependents','IsEmergencyContact') IS NOT NULL INSERT @R EXEC(N'SELECT ''EmployeeDependents legacy fields'',COUNT_BIG(*) FROM EmployeeDependents WHERE IsEmergencyContact=1 OR IsStudentDependent=1');
IF OBJECT_ID(N'PublicHolidays',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''PublicHolidays'',COUNT_BIG(*) FROM PublicHolidays');
IF OBJECT_ID(N'KpiEvaluationRecords',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''KpiEvaluationRecords'',COUNT_BIG(*) FROM KpiEvaluationRecords');
IF OBJECT_ID(N'EmployeeKpiTargets',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''EmployeeKpiTargets'',COUNT_BIG(*) FROM EmployeeKpiTargets');
IF OBJECT_ID(N'AppraisalCriterias',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''AppraisalCriterias'',COUNT_BIG(*) FROM AppraisalCriterias');
IF OBJECT_ID(N'PositionCriteriaMappings',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''PositionCriteriaMappings'',COUNT_BIG(*) FROM PositionCriteriaMappings');
IF OBJECT_ID(N'MappingGradeRanges',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''MappingGradeRanges'',COUNT_BIG(*) FROM MappingGradeRanges');
IF OBJECT_ID(N'PerformanceAppraisals',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''PerformanceAppraisals'',COUNT_BIG(*) FROM PerformanceAppraisals');
IF COL_LENGTH('EvaluatorEvaluations','EvaluationDate') IS NOT NULL INSERT @R EXEC(N'SELECT ''EvaluatorEvaluations.EvaluationDate'',COUNT_BIG(*) FROM EvaluatorEvaluations WHERE EvaluationDate IS NOT NULL');
IF OBJECT_ID(N'CriterionScores',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''CriterionScores'',COUNT_BIG(*) FROM CriterionScores');
IF OBJECT_ID(N'AppraisalEmployeeResponses',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''AppraisalEmployeeResponses'',COUNT_BIG(*) FROM AppraisalEmployeeResponses');
IF OBJECT_ID(N'AppraisalAttachments',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''AppraisalAttachments'',COUNT_BIG(*) FROM AppraisalAttachments');
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'dbo.SystemExceptionLogs')
      AND [name] = N'IX_SystemExceptionLogs_TenantId_Fingerprint')
    INSERT @R VALUES(N'SystemExceptionLogs fingerprint index prerequisite', 1);
IF OBJECT_ID(N'dbo.CentralDocumentGenerationTemplates', N'U') IS NULL
    INSERT @R VALUES(N'Central document generation template prerequisite', 1);
IF OBJECT_ID(N'dbo.BusinessPartnerRegistrations',N'U') IS NULL
   OR OBJECT_ID(N'dbo.ProcurementSupplierEvidencePackVersions',N'U') IS NULL
   OR OBJECT_ID(N'dbo.ProcurementSupplierRegistrationEvidencePackBindings',N'U') IS NULL
    INSERT @R VALUES(N'Supplier evidence binding trigger prerequisites', 1);
IF OBJECT_ID(N'dbo.ProcurementSupplierRegistrationEvidencePackBindings',N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.BusinessPartnerRegistrations',N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.ProcurementSupplierEvidencePackVersions',N'U') IS NOT NULL
    INSERT @R EXEC(N'
        SELECT ''Supplier evidence binding stored lineage'', COUNT_BIG(*)
        FROM dbo.ProcurementSupplierRegistrationEvidencePackBindings i
        JOIN dbo.BusinessPartnerRegistrations r ON r.Id = i.RegistrationId
        JOIN dbo.ProcurementSupplierEvidencePackVersions p ON p.Id = i.PackVersionId
        WHERE r.TenantId <> i.TenantId
           OR p.TenantId <> i.TenantId
           OR r.RegistrationCategory <> i.RegistrationCategory
           OR p.Category <> i.RegistrationCategory
           OR p.PackCode <> i.PackCode
           OR p.Version <> i.PackVersion
           OR TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.PackSnapshotJson, ''$.id'')) <> p.Id
           OR TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.PackSnapshotJson, ''$.tenantId'')) <> i.TenantId
           OR JSON_VALUE(i.PackSnapshotJson, ''$.packCode'') <> p.PackCode
           OR TRY_CONVERT(int, JSON_VALUE(i.PackSnapshotJson, ''$.category'')) <> i.RegistrationCategory
           OR TRY_CONVERT(int, JSON_VALUE(i.PackSnapshotJson, ''$.version'')) <> p.Version');
IF NOT EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260812113000_AddCategoryAwareAcceptedSupply')
   AND OBJECT_ID(N'dbo.PurchaseOrders',N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.PurchaseRequisitions',N'U') IS NOT NULL
    INSERT @R EXEC(N'
        SELECT ''Category-aware accepted supply missing PO category source'', COUNT_BIG(*)
        FROM dbo.PurchaseOrders po
        LEFT JOIN dbo.PurchaseRequisitions pr
          ON pr.Id = po.SourceRequisitionId
         AND pr.TenantId = po.TenantId
         AND pr.IsDeleted = 0
        WHERE po.ProcurementSourceType IS NOT NULL
          AND po.ProcurementSourceType <> 5
          AND (pr.Id IS NULL OR pr.ProcurementCategory IS NULL)');
IF NOT EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260812170000_ExtendControlledSourcingMethods')
BEGIN
    IF OBJECT_ID(N'dbo.ProcurementTenderControls',N'U') IS NULL
       OR OBJECT_ID(N'dbo.ProcurementExceptionalSourcingControls',N'U') IS NULL
       OR OBJECT_ID(N'dbo.CK_ProcurementTenderControls_State',N'C') IS NULL
       OR OBJECT_ID(N'dbo.CK_ProcurementExceptionalSourcingControls_Core',N'C') IS NULL
        INSERT @R VALUES(N'Controlled sourcing method constraint prerequisites', 1);
    DECLARE @tenderControlTrigger nvarchar(max) =
        OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_ProcurementTenderControls_Lifecycle', N'TR'));
    IF @tenderControlTrigger IS NULL
       OR (CHARINDEX(N'sc.[SelectedMethod] NOT IN (1, 2)', @tenderControlTrigger) = 0
           AND CHARINDEX(N'sc.[SelectedMethod] NOT IN (1, 2, 7, 8)', @tenderControlTrigger) = 0)
        INSERT @R VALUES(N'Controlled sourcing method trigger baseline', 1);
END;
IF NOT EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260813103000_AddFixedAssetDisposalSettlement')
   AND EXISTS (
       SELECT required.name
       FROM (VALUES
           (N'AssetDisposals'), (N'BusinessPartners'), (N'TaxGroups'),
           (N'PaymentTerms'), (N'PaymentMethod'), (N'BankAccounts'),
           (N'LiquidityAccounts'), (N'Invoices'), (N'CustomerPayment')
       ) required(name)
       WHERE OBJECT_ID(N'dbo.' + required.name, N'U') IS NULL)
    INSERT @R VALUES(N'Fixed-asset disposal settlement table prerequisites', 1);
IF NOT EXISTS (
    SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = N'20260813063001_INVREQFU002MaintenanceReservationLifecycle')
BEGIN
    IF OBJECT_ID(N'dbo.WorkOrderParts',N'U') IS NULL
       OR OBJECT_ID(N'dbo.InventoryAllocations',N'U') IS NULL
       OR NOT EXISTS (
            SELECT 1 FROM sys.foreign_keys
            WHERE name = N'FK_WorkOrderParts_InventoryAllocations_AllocationId')
       OR NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'dbo.WorkOrderParts')
              AND name = N'IX_WorkOrderParts_TenantId')
        INSERT @R VALUES(N'INV-FU-002 reservation migration prerequisites', 1);
    IF OBJECT_ID(N'dbo.WorkOrderParts',N'U') IS NOT NULL
        INSERT @R EXEC(N'
            SELECT ''INV-FU-002 duplicate work-order allocation bindings'', COUNT_BIG(*)
            FROM (
                SELECT TenantId, AllocationId
                FROM dbo.WorkOrderParts
                WHERE AllocationId IS NOT NULL AND IsDeleted = 0
                GROUP BY TenantId, AllocationId
                HAVING COUNT_BIG(*) > 1
            ) duplicate');
    IF OBJECT_ID(N'dbo.InventoryAllocations',N'U') IS NOT NULL
    BEGIN
        INSERT @R EXEC(N'
            SELECT ''INV-FU-002 duplicate reservation replay keys'', COUNT_BIG(*)
            FROM (
                SELECT TenantId, IdempotencyKey
                FROM dbo.InventoryAllocations
                WHERE AllocationType = N''WorkOrder'' AND IdempotencyKey IS NOT NULL
                GROUP BY TenantId, IdempotencyKey
                HAVING COUNT_BIG(*) > 1
            ) duplicate');
        INSERT @R EXEC(N'
            SELECT ''INV-FU-002 invalid legacy reservation quantities'', COUNT_BIG(*)
            FROM dbo.InventoryAllocations
            WHERE AllocationType = N''WorkOrder''
              AND (AllocatedQuantity <= 0 OR ConsumedQuantity < 0 OR RemainingQuantity < 0
                   OR ConsumedQuantity + RemainingQuantity > AllocatedQuantity)');
    END;
END;
IF EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260813103157_INVREQFU003IssueFinanceAssetLifecycle')
   AND NOT EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260813143000_INVREQFU003ReturnReversalAllocationGate')
   AND OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_InventoryIssueReturnAllocations_Immutable', N'TR')) IS NULL
    INSERT @R VALUES(N'INV-FU-003 return allocation trigger prerequisite', 1);
IF NOT EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260813150000_INVREQFU003AllowPostedFullReturnReversal')
BEGIN
    DECLARE @returnTrigger nvarchar(max) =
        OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_InventoryReturnVouchers_ControlledLifecycle', N'TR'));
    IF @returnTrigger IS NULL
       OR (CHARINDEX(N'r.Status NOT IN (5,6,7)', @returnTrigger) = 0
           AND CHARINDEX(N'(r.Status NOT IN (5,6,7) AND NOT (r.Status = 3 AND i.Status IN (4,5)))', @returnTrigger) = 0)
        INSERT @R VALUES(N'INV-FU-003 Store Return Voucher trigger baseline', 1);
END;
IF NOT EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260813171000_INVREQFU004RequireCleanTransferEvidence')
BEGIN
    DECLARE @transferTrigger nvarchar(max) =
        OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_InventoryTransferDiscrepancyEvidence_AppendOnly', N'TR'));
    IF @transferTrigger IS NULL
       OR (CHARINDEX(N'OR dv.FileUploadRecordId <> i.FileUploadRecordId OR dv.Status <> N''Published'' OR dv.PublishedAt IS NULL', @transferTrigger) = 0
           AND CHARINDEX(N'OR dv.FileUploadRecordId <> i.FileUploadRecordId OR f.VirusScanStatus <> 2 OR dv.Status <> N''Published'' OR dv.PublishedAt IS NULL', @transferTrigger) = 0)
        INSERT @R VALUES(N'INV-FU-004 transfer evidence trigger baseline', 1);
END;
IF NOT EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260814123000_INVREQFU004PolicySupersessionAndGhanepsMappingReuse')
BEGIN
    DECLARE @dueDiligenceTrigger nvarchar(max) =
        OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_ProcurementSupplierDueDiligenceReviews_Lifecycle', N'TR'));
    IF @dueDiligenceTrigger IS NULL
       OR (CHARINDEX(N'(d.Status = 1 AND i.Status IN (2, 3))', @dueDiligenceTrigger) = 0
           AND CHARINDEX(N'(d.Status = 1 AND i.Status IN (2, 3, 5))', @dueDiligenceTrigger) = 0)
        INSERT @R VALUES(N'INV-FU-004 supplier due-diligence trigger baseline', 1);
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.ProcurementGhanepsExchangeEvents')
          AND name IN (
              N'IX_ProcurementGhanepsExchangeEvents_TenantId_SourceType_SourceId_EventFamily_Direction_MappingKey_EventReference',
              N'UX_ProcGhanepsEvent_Source_MappingHash'))
        INSERT @R VALUES(N'INV-FU-004 GHANEPS mapping index prerequisite', 1);
END;
IF NOT EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260814143000_INVREQFU004AllowDraftInspectionWorkflowRebind')
BEGIN
    DECLARE @inspectionTrigger nvarchar(max) =
        OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_ProcurementReceiptInspectionCases_TDC0502Protected', N'TR'));
    IF @inspectionTrigger IS NULL
       OR (CHARINDEX(N'OR i.WorkflowDefinitionId <> d.WorkflowDefinitionId', @inspectionTrigger) = 0
           AND CHARINDEX(N'TDC0502_RECEIPT_INSPECTION_WORKFLOW_ID', @inspectionTrigger) = 0)
        INSERT @R VALUES(N'INV-FU-004 receipt-inspection trigger baseline', 1);
END;
IF NOT EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260820100000_AddInventoryOpeningStockBook')
BEGIN
    IF OBJECT_ID(N'dbo.StockAdjustments', N'U') IS NULL
       OR OBJECT_ID(N'dbo.StockAdjustmentItems', N'U') IS NULL
        INSERT @R VALUES(N'Inventory opening-stock book table prerequisites', 1);
    IF OBJECT_ID(N'dbo.StockAdjustments', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.StockAdjustments', N'BookClassification') IS NOT NULL
        INSERT @R VALUES(N'Inventory opening-stock book partial migration state', 1);
END;
IF NOT EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260826170000_AllowReleaseOnlyRfqAwardTransition')
BEGIN
    IF OBJECT_ID(N'dbo.RequestForQuotations', N'U') IS NULL
       OR OBJECT_ID(N'dbo.RequestForQuotationAwardLines', N'U') IS NULL
       OR COL_LENGTH(N'dbo.RequestForQuotations', N'SourcePurchaseRequisitionId') IS NULL
       OR COL_LENGTH(N'dbo.RequestForQuotations', N'SourcingReleaseId') IS NULL
       OR COL_LENGTH(N'dbo.RequestForQuotations', N'SourcingCaseId') IS NULL
       OR COL_LENGTH(N'dbo.RequestForQuotations', N'SubmissionDeadline') IS NULL
       OR COL_LENGTH(N'dbo.RequestForQuotations', N'AwardedAt') IS NULL
        INSERT @R VALUES(N'Release-only RFQ award transition prerequisites', 1);
END;
IF NOT EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260826210000_AlignPurchaseOrderSourceTriggerWithSupportedRoutes')
BEGIN
    DECLARE @purchaseOrderSourceTrigger nvarchar(max) =
        OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_PurchaseOrders_ApprovedSourceProtected', N'TR'));
    IF @purchaseOrderSourceTrigger IS NULL
       OR CHARINDEX(N'TDC0406_PO_AMENDMENT_ID', @purchaseOrderSourceTrigger) = 0
       OR CHARINDEX(N'THROW 51202', @purchaseOrderSourceTrigger) = 0
       OR CHARINDEX(N'THROW 51205', @purchaseOrderSourceTrigger) = 0
        INSERT @R VALUES(N'Purchase-order source trigger alignment prerequisites', 1);
END;
IF NOT EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260827090000_AlignSupplierOnboardingPartnerCategories')
BEGIN
    IF OBJECT_ID(N'dbo.PartnerCategories', N'U') IS NULL
       OR OBJECT_ID(N'dbo.BusinessPartnerCategories', N'U') IS NULL
       OR OBJECT_ID(N'dbo.BusinessPartnerRegistrations', N'U') IS NULL
       OR OBJECT_ID(N'dbo.BusinessPartners', N'U') IS NULL
       OR OBJECT_ID(N'dbo.Tenants', N'U') IS NULL
       OR COL_LENGTH(N'dbo.PartnerCategories', N'TenantId') IS NULL
       OR COL_LENGTH(N'dbo.PartnerCategories', N'CategoryCode') IS NULL
       OR COL_LENGTH(N'dbo.BusinessPartnerRegistrations', N'RegistrationCategory') IS NULL
       OR COL_LENGTH(N'dbo.BusinessPartnerRegistrations', N'BusinessPartnerId') IS NULL
       OR NOT EXISTS
          (
              SELECT 1
              FROM sys.indexes
              WHERE object_id = OBJECT_ID(N'dbo.PartnerCategories')
                AND name = N'IX_PartnerCategories_CategoryCode'
          )
       OR EXISTS
          (
              SELECT 1
              FROM sys.indexes
              WHERE object_id = OBJECT_ID(N'dbo.PartnerCategories')
                AND name = N'IX_PartnerCategories_TenantId_CategoryCode'
          )
        INSERT @R VALUES(N'Supplier onboarding category alignment prerequisites', 1);
END;
IF NOT EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260828190000_AlignProcurementReservationAndFormalCommitmentLifecycle')
BEGIN
    IF OBJECT_ID(N'dbo.ProcurementBudgetCommitments', N'U') IS NULL
       OR OBJECT_ID(N'dbo.Tenders', N'U') IS NULL
       OR OBJECT_ID(N'dbo.Contracts', N'U') IS NULL
       OR OBJECT_ID(N'dbo.PurchaseOrders', N'U') IS NULL
        INSERT @R VALUES(N'FR-PR-005 commitment lifecycle table prerequisites', 1);
    ELSE
    BEGIN
        INSERT @R EXEC(N'
            SELECT ''FR-PR-005 active contract exposure above reservation'', COUNT_BIG(*)
            FROM (
                SELECT c.Id
                FROM dbo.ProcurementBudgetCommitments c
                JOIN dbo.Tenders tender
                  ON tender.SourcePurchaseRequisitionId = c.PurchaseRequisitionId
                 AND tender.TenantId = c.TenantId AND tender.IsDeleted = 0
                JOIN dbo.Contracts contract
                  ON contract.TenderId = tender.Id
                 AND contract.TenantId = c.TenantId AND contract.IsDeleted = 0
                WHERE c.Status = 1 AND contract.Status = N''Active'' AND contract.ContractValue > 0
                GROUP BY c.Id, c.ReservedAmount
                HAVING SUM(contract.ContractValue) > c.ReservedAmount
            ) violation');
        INSERT @R EXEC(N'
            SELECT ''FR-PR-005 child PO exposure above active contract'', COUNT_BIG(*)
            FROM (
                SELECT contract.Id
                FROM dbo.Contracts contract
                JOIN dbo.PurchaseOrders po
                  ON po.ContractId = contract.Id
                 AND po.TenantId = contract.TenantId AND po.IsDeleted = 0 AND po.TotalAmount > 0
                 AND po.Status IN (N''Approved'', N''Open'', N''Sent'', N''Acknowledged'', N''Partially Received'', N''Received'')
                WHERE contract.IsDeleted = 0 AND contract.Status = N''Active'' AND contract.ContractValue > 0
                GROUP BY contract.Id, contract.ContractValue
                HAVING SUM(po.TotalAmount) > contract.ContractValue
            ) violation');
        INSERT @R EXEC(N'
            SELECT ''FR-PR-005 combined formal exposure above reservation'', COUNT_BIG(*)
            FROM dbo.ProcurementBudgetCommitments c
            OUTER APPLY (
                SELECT COALESCE(SUM(contract.ContractValue), 0) ContractAmount
                FROM dbo.Tenders tender
                JOIN dbo.Contracts contract
                  ON contract.TenderId = tender.Id
                 AND contract.TenantId = tender.TenantId AND contract.IsDeleted = 0
                WHERE tender.SourcePurchaseRequisitionId = c.PurchaseRequisitionId
                  AND tender.TenantId = c.TenantId AND tender.IsDeleted = 0
                  AND contract.Status = N''Active'' AND contract.ContractValue > 0
            ) formal
            OUTER APPLY (
                SELECT COALESCE(SUM(po.TotalAmount), 0) DirectPurchaseOrderAmount
                FROM dbo.PurchaseOrders po
                WHERE po.TenantId = c.TenantId
                  AND po.SourceRequisitionId = c.PurchaseRequisitionId
                  AND po.IsDeleted = 0 AND po.TotalAmount > 0 AND po.ContractId IS NULL
                  AND po.Status IN (N''Approved'', N''Open'', N''Sent'', N''Acknowledged'', N''Partially Received'', N''Received'')
            ) purchaseOrders
            WHERE c.Status = 1
              AND formal.ContractAmount + purchaseOrders.DirectPurchaseOrderAmount > c.ReservedAmount');
    END;
END;
IF EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260828190000_AlignProcurementReservationAndFormalCommitmentLifecycle')
   AND NOT EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260829210000_EnforceAtomicPurchaseOrderBudgetCommitment')
BEGIN
    IF OBJECT_ID(N'dbo.PurchaseOrders', N'U') IS NULL
       OR OBJECT_ID(N'dbo.ProcurementRequisitionSourcingReleases', N'U') IS NULL
       OR OBJECT_ID(N'dbo.PurchaseRequisitions', N'U') IS NULL
       OR OBJECT_ID(N'dbo.ProcurementBudgetCommitments', N'U') IS NULL
       OR OBJECT_ID(N'dbo.ProcurementBudgets', N'U') IS NULL
       OR OBJECT_ID(N'dbo.ProcurementBudgetCommitmentLedgerEntries', N'U') IS NULL
       OR OBJECT_ID(N'dbo.ProcurementPurchaseOrderCommitmentAdjustments', N'U') IS NULL
       OR COL_LENGTH(N'dbo.ProcurementBudgetCommitmentLedgerEntries', N'EntryType') IS NULL
       OR COL_LENGTH(N'dbo.ProcurementBudgetCommitmentLedgerEntries', N'SourceType') IS NULL
       OR COL_LENGTH(N'dbo.ProcurementBudgetCommitmentLedgerEntries', N'SourceId') IS NULL
       OR OBJECT_ID(N'dbo.TR_PurchaseOrders_GovernedCommitment', N'TR') IS NULL
        INSERT @R VALUES(N'Atomic PO budget commitment trigger prerequisites', 1);
END;
IF NOT EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260901033000_AllowGovernedTenderLineageRecovery')
BEGIN
    IF OBJECT_ID(N'dbo.Tenders', N'U') IS NULL
       OR OBJECT_ID(N'dbo.ProcurementRequisitionSourcingReleases', N'U') IS NULL
       OR OBJECT_ID(N'dbo.PurchaseRequisitions', N'U') IS NULL
       OR OBJECT_ID(N'dbo.ProcurementSourcingCases', N'U') IS NULL
       OR COL_LENGTH(N'dbo.Tenders', N'SourcePurchaseRequisitionId') IS NULL
       OR COL_LENGTH(N'dbo.Tenders', N'SourcingReleaseId') IS NULL
       OR COL_LENGTH(N'dbo.Tenders', N'SourcingCaseId') IS NULL
       OR OBJECT_DEFINITION(
            OBJECT_ID(N'dbo.TR_Tenders_SourcingReleaseGuard', N'TR')) IS NULL
        INSERT @R VALUES(N'Governed tender lineage recovery prerequisites', 1);
END;
IF NOT EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260902110000_AllowHistoricalEvaluationCommitteeSourceLineage')
BEGIN
    IF OBJECT_ID(N'dbo.ProcurementEvaluationCommitteeControls', N'U') IS NULL
       OR OBJECT_ID(N'dbo.ProcurementEvaluationCommitteeAppointments', N'U') IS NULL
       OR OBJECT_ID(N'dbo.ProcurementEvaluationCommitteeRoleRequirements', N'U') IS NULL
       OR OBJECT_ID(N'dbo.Tenders', N'U') IS NULL
       OR OBJECT_ID(N'dbo.RequestForQuotations', N'U') IS NULL
       OR OBJECT_ID(N'dbo.ProcurementSourcingCases', N'U') IS NULL
       OR OBJECT_ID(N'dbo.ProcurementCommittees', N'U') IS NULL
       OR OBJECT_ID(N'dbo.ProcurementPolicySets', N'U') IS NULL
       OR OBJECT_ID(N'dbo.ProcurementConfigurationProfiles', N'U') IS NULL
       OR OBJECT_ID(N'dbo.ProcurementPolicyMethodRules', N'U') IS NULL
       OR OBJECT_ID(N'dbo.WorkflowDefinitions', N'U') IS NULL
       OR OBJECT_ID(N'dbo.WorkflowInstances', N'U') IS NULL
       OR COL_LENGTH(N'dbo.Tenders', N'SourcingCaseId') IS NULL
       OR COL_LENGTH(N'dbo.RequestForQuotations', N'SourcingCaseId') IS NULL
       OR COL_LENGTH(N'dbo.ProcurementSourcingCases', N'PolicySetId') IS NULL
       OR COL_LENGTH(N'dbo.ProcurementSourcingCases', N'MethodRuleId') IS NULL
       OR COL_LENGTH(N'dbo.ProcurementConfigurationProfiles', N'PublishedAt') IS NULL
       OR COL_LENGTH(N'dbo.ProcurementPolicySets', N'PublishedAt') IS NULL
       OR OBJECT_DEFINITION(
            OBJECT_ID(N'dbo.TR_ProcurementEvaluationCommitteeControls_Lifecycle', N'TR')) IS NULL
        INSERT @R VALUES(N'Historical evaluation committee lineage prerequisites', 1);
END;
SELECT CheckName,AffectedRows FROM @R WHERE AffectedRows > 0 ORDER BY CheckName;
"@
}

function Get-DatabaseControlSummary {
    return Invoke-DatabaseTable @"
SELECT
    (SELECT COUNT_BIG(*) FROM dbo.__EFMigrationsHistory) AS MigrationCount,
    (SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory) AS LatestMigration,
    (SELECT COUNT_BIG(*) FROM sys.foreign_keys WHERE is_not_trusted = 1) AS UntrustedForeignKeys,
    (SELECT COUNT_BIG(*) FROM sys.foreign_keys WHERE is_disabled = 1) AS DisabledForeignKeys,
    CASE WHEN OBJECT_ID(N'FileUploadPolicies',N'U') IS NULL THEN 0 ELSE
        (SELECT COUNT_BIG(*) FROM FileUploadPolicies
         WHERE ISNULL(AllowedExtensionsCsv,'') LIKE '%.svg%'
            OR ISNULL(AllowedMimeTypesCsv,'') LIKE '%image/svg+xml%') END AS SvgFileUploadPolicies,
    CASE WHEN OBJECT_ID(N'ProcurementSupplierEvidenceRequirements',N'U') IS NULL THEN 0 ELSE
        (SELECT COUNT_BIG(*) FROM ProcurementSupplierEvidenceRequirements
         WHERE AllowedMimeTypesJson LIKE '%image/svg+xml%') END AS SvgSupplierRequirements;
"@
}

function Get-TenderPaymentPermissionSummary {
    return Invoke-DatabaseTable @"
SELECT
    (SELECT COUNT_BIG(*)
     FROM dbo.Permissions
     WHERE [Name] = N'procurement.tender.payment.verify'
       AND [IsDeleted] = 0
       AND [IsSystemPermission] = 1) AS PermissionCount,
    (SELECT COUNT_BIG(*)
     FROM dbo.RolePermissions AS rolePermission
     INNER JOIN dbo.Permissions AS permission
         ON permission.Id = rolePermission.PermissionId
     INNER JOIN dbo.AspNetRoles AS role
         ON role.Id = rolePermission.RoleId
     WHERE permission.[Name] = N'procurement.tender.payment.verify'
       AND permission.[IsDeleted] = 0
       AND role.[Name] IN
           (N'TDC_PROCUREMENT_OFFICER', N'TDC_SENIOR_PROCUREMENT_OFFICER')) AS RoleGrantCount;
"@
}

function Write-ServiceState {
    Get-ManagedServices | ForEach-Object {
        Write-Output "SERVICE|$($_.Name)|$($_.Status)"
    }
}

function Get-ManagedServices {
    $caddy = Get-Service RhemaERPCaddy -ErrorAction SilentlyContinue
    if ($null -eq $caddy) {
        throw 'Required HTTPS gateway service RhemaERPCaddy is missing.'
    }
    return @(Get-Service RhemaERPAPI,RhemaERPFrontend) + @($caddy)
}

function Invoke-Preflight {
    foreach ($path in @($RhemaRoot, $ApiRoot, $FrontendRoot, $PackagesRoot,
            $BackupsRoot, $LogsRoot)) {
        Assert-True (Test-Path -LiteralPath $path) "Required VPS path is missing: $path"
    }
    if (-not $UsesNssmApiConfiguration) {
        Assert-True (Test-Path -LiteralPath $ApiServiceXml) `
            "Required VPS path is missing: $ApiServiceXml"
    }
    Assert-True (Test-Path -LiteralPath $FrontendNssmParametersPath) `
        "Frontend NSSM configuration is missing: $FrontendNssmParametersPath"
    $frontendService = Get-ItemProperty -LiteralPath $FrontendNssmParametersPath
    $frontendApplication = [string]$frontendService.Application
    $frontendDirectory = [string]$frontendService.AppDirectory
    $frontendParameters = [string]$frontendService.AppParameters
    Write-Output "FRONTEND_RUNTIME|$frontendApplication|$frontendDirectory|$frontendParameters"
    Assert-True ($frontendApplication -match '(?i)(^|\\)npm\.cmd$') `
        'RhemaERPFrontend must run npm.cmd for the regular Next.js release package.'
    Assert-True ($frontendDirectory.TrimEnd('\\') -eq $FrontendRoot.TrimEnd('\\')) `
        "RhemaERPFrontend AppDirectory must be $FrontendRoot."
    Assert-True ($frontendParameters -match '(?i)^\s*run\s+start\s+--\s+-p\s+3001\s*$') `
        'RhemaERPFrontend must use: npm run start -- -p 3001.'

    $services = Get-ManagedServices
    $notRunning = @($services | Where-Object { $_.Status -ne 'Running' })
    Assert-True ($notRunning.Count -eq 0) `
        "Preflight requires all deployed services running. Not running: $($notRunning.Name -join ', ')"

    $drive = Get-PSDrive -Name ([System.IO.Path]::GetPathRoot($RhemaRoot).TrimEnd(':\'))
    $freeGb = [Math]::Round($drive.Free / 1GB, 2)
    Write-Output "DISK_FREE_GB|$freeGb"
    Assert-True ($freeGb -ge 5) 'Less than 5 GB of free disk space remains on the VPS.'

    $environment = Get-ApiServiceEnvironment
    Assert-SyncfusionLicenseConfigured
    $requiredSettings = @{
        'StartupInitialization__SeedDevelopmentData' = 'true'
        'StartupInitialization__AllowDevelopmentDataSeedingOutsideDevelopment' = 'true'
    }
    foreach ($entry in $requiredSettings.GetEnumerator()) {
        $actual = [string]$environment[$entry.Key]
        Write-Output "CONFIG|$($entry.Key)|$actual"
        Assert-True ($actual -eq $entry.Value) `
            "Test VPS configuration is invalid for $($entry.Key)."
    }
    foreach ($name in @(
            'CorsSettings__AllowedOrigins__0',
            'CandidatePortal__PortalUrl',
            'FrontendUrl')) {
        $actual = [string]$environment[$name]
        Write-Output "CONFIG|$name|$actual"
        if ($actual -ne $ExpectedPublicOrigin) {
            # Apply reconciles public-origin settings before the API restarts.
            # Existing servers may legitimately carry the previous origin into
            # preflight, so record this as repairable drift instead of blocking
            # the release before its backed-up apply phase.
            Write-Output "CONFIG_DRIFT|$name|EXPECTED=$ExpectedPublicOrigin|ACTUAL=$actual"
        }
    }
    $corsNodes = @($environment.Keys | Where-Object {
        $_ -like 'CorsSettings__AllowedOrigins__*'
    })
    Assert-True ($corsNodes.Count -eq 1) `
        'The test VPS must expose exactly one HTTPS CORS origin.'

    $history = @(Get-MigrationHistory)
    foreach ($row in $history) { Write-Output "MIGRATION_ID|$($row.MigrationId)" }
    Write-Output 'GUARD_COVERAGE|20260720181131_AddHRModule'
    Write-Output 'GUARD_COVERAGE|20260720193903_AddHRPerformanceModule'
    # These migrations only define immutable-row trigger bodies. Their THROW statements
    # are not executed while applying the migrations and have no legacy-data precondition.
    Write-Output 'GUARD_COVERAGE|20260807202000_AddQuantitySurveyConfigurationRegister'
    Write-Output 'GUARD_COVERAGE|20260807233500_FixSupplierEvidencePackBindingLineageTrigger'
    Write-Output 'GUARD_COVERAGE|20260807234500_RecordEverySystemExceptionOccurrence'
    Write-Output 'GUARD_COVERAGE|20260808124500_AddProjectBoqApprovalPublication'
    # These QS migrations either create empty governed registers, replace a
    # trigger, or add nullable/defaulted lineage columns whose unique indexes
    # are filtered to newly-governed rows. Their THROW statements live only in
    # trigger bodies and cannot execute against legacy rows during migration.
    Write-Output 'GUARD_COVERAGE|20260808231322_AddQuantitySurveyRateBuildUps'
    Write-Output 'GUARD_COVERAGE|20260809002001_AddQuantitySurveyEstimateVersions'
    Write-Output 'GUARD_COVERAGE|20260809152905_AddQuantitySurveyEscalationFormulaRegister'
    Write-Output 'GUARD_COVERAGE|20260809165157_AddQuantitySurveyPriceIndexImportWorkflow'
    Write-Output 'GUARD_COVERAGE|20260809221500_AddQuantitySurveyEscalationCalculationRuns'
    Write-Output 'GUARD_COVERAGE|20260810005756_AddQuantitySurveyEscalationDisputes'
    Write-Output 'GUARD_COVERAGE|20260810023000_AddQuantitySurveyMeasurementSheets'
    Write-Output 'GUARD_COVERAGE|20260810040000_AddProjectBoqRemeasurementWorkflow'
    Write-Output 'GUARD_COVERAGE|20260810041753_AddQuantitySurveyJointMeasurements'
    Write-Output 'GUARD_COVERAGE|20260810103259_AddQuantitySurveyDesignRevisionImpacts'
    Write-Output 'GUARD_COVERAGE|20260810121810_AddQuantitySurveyValuationWorksheets'
    Write-Output 'GUARD_COVERAGE|20260810134646_AddQuantitySurveyInterimValuationWorkflow'
    Write-Output 'GUARD_COVERAGE|20260810154236_AddQuantitySurveyPaymentCertificateLifecycle'
    Write-Output 'GUARD_COVERAGE|20260810165005_AddQuantitySurveyRetentionReleaseLifecycle'
    Write-Output 'GUARD_COVERAGE|20260810185711_AddQuantitySurveyAdvanceRecoveryLifecycle'
    Write-Output 'GUARD_COVERAGE|20260810202208_AddQuantitySurveyFinalAccountLifecycle'
    Write-Output 'GUARD_COVERAGE|20260810223350_AddQuantitySurveyMaterialReconciliationLifecycle'
    Write-Output 'GUARD_COVERAGE|20260810234912_AddQuantitySurveyVariationLifecycle'
    Write-Output 'GUARD_COVERAGE|20260811010013_AddQuantitySurveyContractClaimLifecycle'
    Write-Output 'GUARD_COVERAGE|20260811013838_AddQuantitySurveyDayworkLifecycle'
    Write-Output 'GUARD_COVERAGE|20260811022852_AddQuantitySurveyVariationApplications'
    Write-Output 'GUARD_COVERAGE|20260811043000_ExtendWorksContractCommercialTerms'
    Write-Output 'GUARD_COVERAGE|20260811050412_AddQuantitySurveySubcontractLifecycle'
    Write-Output 'GUARD_COVERAGE|20260811061000_HardenQuantitySurveySubcontractCertificates'
    Write-Output 'GUARD_COVERAGE|20260811063100_AddQuantitySurveySubcontractChargeLifecycle'
    # Trigger replacement only; all existing rows remain untouched.
    Write-Output 'GUARD_COVERAGE|20260809173500_AllowControlledSupplierApplicantContactCorrection'
    Write-Output 'GUARD_COVERAGE|20260809204500_AllowPreProvisioningSupplierContactCorrection'
    # Existing plan codes are already globally unique; the new tenant indexes
    # cannot collide. Added governance lineage is nullable/defaulted for Draft
    # rows, and all THROW statements execute only in the post-migration trigger.
    Write-Output 'GUARD_COVERAGE|20260812140000_GovernEmergencyPurchaseExceptions'
    # Creates an empty governed receipt-evidence register and trigger-only hard stops;
    # no existing receipt row is updated while applying this migration.
    Write-Output 'GUARD_COVERAGE|20260812195409_INVREQFU001GovernedReceiptSourceEvidence'
    # The commitment migration only installs trigger bodies. Category-aware supply
    # backfills from the authoritative requisition and preflight rejects any governed
    # PO whose category cannot be derived before constraints are installed. The
    # sourcing-method migration has explicit old-or-new constraint/trigger probes.
    Write-Output 'GUARD_COVERAGE|20260812100000_HardenProcurementCommitmentLifecycle'
    Write-Output 'GUARD_COVERAGE|20260812113000_AddCategoryAwareAcceptedSupply'
    Write-Output 'GUARD_COVERAGE|20260812170000_ExtendControlledSourcingMethods'
    # INV-FU-002 creates an empty action register and otherwise installs constraints,
    # indexes and trigger hard stops. Preflight above validates its only legacy-row
    # quantity and uniqueness prerequisites before the migration is allowed to run.
    Write-Output 'GUARD_COVERAGE|20260813063001_INVREQFU002MaintenanceReservationLifecycle'
    # INV-FU-003 adds nullable/defaulted voucher lineage plus empty governed registers.
    # Its later migrations only replace or validate trigger bodies; preflight validates
    # the required installed baselines whenever the predecessor migration already exists.
    Write-Output 'GUARD_COVERAGE|20260813103157_INVREQFU003IssueFinanceAssetLifecycle'
    Write-Output 'GUARD_COVERAGE|20260813143000_INVREQFU003ReturnReversalAllocationGate'
    Write-Output 'GUARD_COVERAGE|20260813150000_INVREQFU003AllowPostedFullReturnReversal'
    # INV-FU-004 trigger/index replacements are guarded by explicit old-or-new
    # definition probes above, making fresh apply and idempotent revalidation safe.
    Write-Output 'GUARD_COVERAGE|20260813171000_INVREQFU004RequireCleanTransferEvidence'
    Write-Output 'GUARD_COVERAGE|20260814123000_INVREQFU004PolicySupersessionAndGhanepsMappingReuse'
    Write-Output 'GUARD_COVERAGE|20260814143000_INVREQFU004AllowDraftInspectionWorkflowRebind'
    # Opening-stock book governance adds one defaulted column and replaces two
    # trigger bodies without mutating legacy rows. The preflight probe above
    # rejects missing source tables and any partial column apply before startup.
    Write-Output 'GUARD_COVERAGE|20260820100000_AddInventoryOpeningStockBook'
    Write-Output 'GUARD_COVERAGE|20260820120000_AddDmsGenerationTemplateWordSource'
    # The simplified PR control migration only relaxes existing columns to nullable
    # and replaces the insert-time tenant/approval-lineage trigger. Its THROW is in
    # the new trigger body and is not evaluated against stored rows during apply.
    Write-Output 'GUARD_COVERAGE|20260824183000_SimplifyPurchaseRequisitionControls'
    # This migration only relaxes advanced sourcing-case lineage columns. Its
    # Up THROW statements are contained in the replacement lifecycle trigger;
    # the stored-row guard belongs to Down and is not executed during deploy.
    Write-Output 'GUARD_COVERAGE|20260825120000_SimplifyProcurementSourcingCaseLineage'
    # This migration replaces the RFQ and tender lineage triggers. Its THROW
    # statements protect future writes and do not evaluate stored rows in Up.
    Write-Output 'GUARD_COVERAGE|20260825170000_AllowReleaseOnlyProcurementSourceEntry'
    # This migration replaces only the RFQ lifecycle trigger. The read-only
    # prerequisite probe above verifies the tables and columns used by the new
    # release-only transition; existing RFQ rows are not updated during Up.
    Write-Output 'GUARD_COVERAGE|20260826170000_AllowReleaseOnlyRfqAwardTransition'
    # This migration only replaces the purchase-order commitment trigger. Its
    # THROW protects future writes and existing rows are not updated in Up.
    Write-Output 'GUARD_COVERAGE|20260826190000_AlignPurchaseOrderCommitmentWithRequisition'
    # This migration patches two guarded blocks in the installed PO source
    # trigger without updating stored rows. The prerequisite probe above checks
    # the exact baseline markers before startup is allowed to apply it.
    Write-Output 'GUARD_COVERAGE|20260826210000_AlignPurchaseOrderSourceTriggerWithSupportedRoutes'
    # This migration replaces the global category-code index with a tenant-safe
    # composite index, provisions canonical supplier categories, and repairs
    # approved supplier assignments. The probe above rejects missing tables,
    # columns, and partially applied index state before any data is changed.
    Write-Output 'GUARD_COVERAGE|20260827090000_AlignSupplierOnboardingPartnerCategories'
    # FR-PR-005 backfills formal contract and direct-PO exposure into the new
    # immutable ledger. The probes above mirror every legacy-data THROW in the
    # migration so over-exposed reservations fail before any schema change.
    Write-Output 'GUARD_COVERAGE|20260828190000_AlignProcurementReservationAndFormalCommitmentLifecycle'
    # This forward correction replaces only the PO exposure trigger. The
    # prerequisite probe prevents a partially applied FR-PR-005 baseline from
    # reaching migration startup without the immutable ledger it must enforce.
    Write-Output 'GUARD_COVERAGE|20260829210000_EnforceAtomicPurchaseOrderBudgetCommitment'
    # This migration only replaces the tender sourcing-release guard. It does
    # not update stored tenders; the schema probe above verifies every shared
    # lineage owner and the installed trigger baseline before replacement.
    Write-Output 'GUARD_COVERAGE|20260901033000_AllowGovernedTenderLineageRecovery'
    # This migration only replaces the committee-control trigger. The exact
    # source, tenant, policy, method, workflow, and temporal lineage remain
    # fail-closed; no stored committee or sourcing rows are rewritten.
    Write-Output 'GUARD_COVERAGE|20260902110000_AllowHistoricalEvaluationCommitteeSourceLineage'
    # Civil Engineering migrations create new governed tables. The document-register migration
    # idempotently inserts only a missing tenant metadata template; existing Project, Workflow
    # and central-DMS records are not rewritten. All THROW statements live in trigger bodies.
    Write-Output 'GUARD_COVERAGE|20260814205757_AddCivilEngineeringConfigurationLifecycle'
    Write-Output 'GUARD_COVERAGE|20260814234022_AddCivilEngineeringDesignWorkflow'
    Write-Output 'GUARD_COVERAGE|20260815005453_AddCivilEngineeringReconnaissance'
    Write-Output 'GUARD_COVERAGE|20260815031044_AddCivilEngineeringDesignInputRequests'
    Write-Output 'GUARD_COVERAGE|20260815135156_AddCivilEngineeringDocumentRegister'
    # CIV-0201 creates empty appointment and append-only revision registers. Its
    # tenant/policy/lifecycle hard stops live exclusively in triggers.
    Write-Output 'GUARD_COVERAGE|20260820150000_AddCivilEngineeringProjectEngineerAssignments'
    # CIV-0202 adds only Civil routing/envelope, DMS-reference, response, and revision
    # tables around the existing Projects instruction.
    Write-Output 'GUARD_COVERAGE|20260820170000_AddCivilEngineeringSiteInstructionRouting'
    # CIV-0203 through CIV-0205 create empty Projects-owned governed registers with
    # foreign keys and post-migration trigger hard stops only.
    Write-Output 'GUARD_COVERAGE|20260820190000_AddCivilEngineeringRfiRouting'
    Write-Output 'GUARD_COVERAGE|20260820193000_AddCivilEngineeringQualityTestRegister'
    Write-Output 'GUARD_COVERAGE|20260820213000_AddCivilEngineeringWeeklySupervisionReports'
    Write-Output 'GUARD_COVERAGE|20260820233000_AddCivilEngineeringIpcEndorsements'
    # The maintenance overlays create new governed registers and do not rewrite source records.
    Write-Output 'GUARD_COVERAGE|20260821033000_AddCivilEngineeringMaintenanceIntakes'
    Write-Output 'GUARD_COVERAGE|20260821050000_AddCivilEngineeringMaintenanceAssessments'
    Write-Output 'GUARD_COVERAGE|20260821053000_AddCivilEngineeringMaintenanceCostingHandoffs'
    Write-Output 'GUARD_COVERAGE|20260821060000_AddCivilEngineeringMaintenanceExecutionLinks'
    Write-Output 'GUARD_COVERAGE|20260821063000_AddCivilEngineeringMaintenanceCompletionControls'
    # Development-approval and direct-task migrations create Civil-owned envelopes;
    # their THROW statements protect future writes and do not mutate legacy owner records.
    Write-Output 'GUARD_COVERAGE|20260821110000_AddCivilEngineeringDevelopmentApprovalFiles'
    Write-Output 'GUARD_COVERAGE|20260821123000_AddCivilEngineeringDevelopmentApprovalFileHandoffs'
    Write-Output 'GUARD_COVERAGE|20260821143000_AddCivilEngineeringPermittingEngineeringReviews'
    Write-Output 'GUARD_COVERAGE|20260821153000_AddCivilEngineeringPermittingHodDecisions'
    Write-Output 'GUARD_COVERAGE|20260821170000_AddCivilEngineeringDirectTaskControls'
    Write-Output 'GUARD_COVERAGE|20260821180000_AddCivilEngineeringDirectTaskFeedbackWorkflow'
    Write-Output 'GUARD_COVERAGE|20260821190000_AddCivilEngineeringUrgentTaskControls'
    Write-Output 'GUARD_COVERAGE|20260821200000_AddCivilEngineeringMobileFieldFeedback'
    # The migration workbench is an empty Projects-owned staging and validation register.
    Write-Output 'GUARD_COVERAGE|20260821210000_AddCivilEngineeringMigrationWorkbench'
    # Works initiation adds nullable, legacy-safe provenance fields; its hard stops are trigger-only.
    Write-Output 'GUARD_COVERAGE|20260821230000_AddCivilEngineeringWorksCaseInitiation'
    # Planning/GIS and its evidence hardening add governed children and trigger controls only.
    Write-Output 'GUARD_COVERAGE|20260822000000_AddCivilEngineeringPlanningGisValidation'
    Write-Output 'GUARD_COVERAGE|20260822001000_HardenCivilEngineeringPlanningGisEvidenceBinding'
    # These lifecycle hardening migrations preserve existing Projects and central-owner records.
    Write-Output 'GUARD_COVERAGE|20260822002000_HardenCivilEngineeringSiteInstructionLifecycle'
    Write-Output 'GUARD_COVERAGE|20260822003000_AddCivilEngineeringInspectionControls'
    Write-Output 'GUARD_COVERAGE|20260822003100_HardenCivilEngineeringInspectionAuditActions'
    Write-Output 'GUARD_COVERAGE|20260822003200_GovernCivilWeeklyProgressControls'
    Write-Output 'GUARD_COVERAGE|20260822003300_SnapshotCivilWeeklyMilestoneSchedule'
    Write-Output 'GUARD_COVERAGE|20260822003400_AddCivilEngineeringExtensionOfTimeControls'
    # Project-asset reconciliation adds nullable Finance lineage without creating assets.
    Write-Output 'GUARD_COVERAGE|20260822003500_AddProjectAssetLinkFixedAssetReconciliation'
    # Configuration reconciliation affects only an editable missing decision; published profiles remain immutable.
    Write-Output 'GUARD_COVERAGE|20260822003600_ReconcileCivilEngineeringConfigurationDecisionCatalogue'
    # Inspection-plan governance binds future plans to existing shared workflow owners.
    Write-Output 'GUARD_COVERAGE|20260822003700_GovernCivilInspectionPlanWorkflow'
    $currentBaselineId = '20260916132000_DisposableDevelopmentCurrentModelBaseline'
    $baselineApplied = @($history | Where-Object {
        [string]$_.MigrationId -eq $currentBaselineId
    }).Count -eq 1
    if ($baselineApplied) {
        # The baseline is the complete zero-to-current schema. Its archived predecessor
        # migrations are neither compiled nor pending, so their destructive-data guards
        # must not evaluate newly seeded current-model rows as legacy blockers.
        Write-Output "MIGRATION_GUARDS|SKIPPED|$currentBaselineId"
        $guards = @()
    }
    else {
        $guards = @(Get-MigrationGuardResults)
    }
    foreach ($guard in $guards) {
        Write-Output "MIGRATION_GUARD|$($guard.CheckName)|$($guard.AffectedRows)"
    }
    Assert-True ($guards.Count -eq 0) `
        'One or more guarded pre-baseline migrations would halt. Resolve and archive the reported data before deployment.'

    $summary = @(Get-DatabaseControlSummary)[0]
    Write-Output "MIGRATION_COUNT|$($summary.MigrationCount)"
    Write-Output "LATEST_MIGRATION|$($summary.LatestMigration)"
    Write-Output "UNTRUSTED_FKS|$($summary.UntrustedForeignKeys)"
    Write-Output "DISABLED_FKS|$($summary.DisabledForeignKeys)"
    Write-Output "SVG_FILE_UPLOAD_POLICIES|$($summary.SvgFileUploadPolicies)"
    Write-Output "SVG_SUPPLIER_REQUIREMENTS|$($summary.SvgSupplierRequirements)"
    Assert-True ([long]$summary.UntrustedForeignKeys -eq 0) `
        'One or more database foreign keys are untrusted.'
    Assert-True ([long]$summary.DisabledForeignKeys -eq 0) `
        'One or more database foreign keys are disabled.'
    Assert-True ([long]$summary.SvgFileUploadPolicies -eq 0) `
        'Active SVG entries exist in a shared file-upload policy.'
    Assert-True ([long]$summary.SvgSupplierRequirements -eq 0) `
        'Active SVG entries exist in a supplier evidence requirement.'

    $configSvgMatches = @(Get-ChildItem $ApiRoot -File -Filter 'appsettings*.json' |
        Select-String -Pattern 'image/svg\+xml|\.svg' -AllMatches)
    Write-Output "SVG_RUNTIME_CONFIG_MATCHES|$($configSvgMatches.Count)"
    Assert-True ($configSvgMatches.Count -eq 0) `
        'Active SVG entries exist in deployed runtime configuration.'

    Write-ServiceState
    Write-Output 'PREFLIGHT|PASS'
}

function Invoke-Backup {
    Assert-DeploymentId
    $backupRoot = Join-Path $BackupsRoot "deploy-$DeploymentId"
    Assert-True (-not (Test-Path -LiteralPath $backupRoot)) `
        "Backup path already exists: $backupRoot"

    $apiBackup = Join-Path $backupRoot 'api'
    $frontendBackup = Join-Path $backupRoot 'frontend'
    $serviceBackup = Join-Path $backupRoot 'services\api'
    New-Item -ItemType Directory -Path $apiBackup,$frontendBackup,$serviceBackup |
        Out-Null

    Invoke-RobocopyChecked @(
        $ApiRoot, $apiBackup, '/E', '/R:2', '/W:2', '/NFL', '/NDL',
        '/NJH', '/NJS', '/NP',
        '/XD', (Join-Path $ApiRoot 'logs'),
        (Join-Path $ApiRoot 'secure-file-storage'),
        (Join-Path $ApiRoot 'wwwroot\uploads')
    )
    Invoke-RobocopyChecked @(
        $FrontendRoot, $frontendBackup, '/E', '/R:2', '/W:2', '/NFL', '/NDL',
        '/NJH', '/NJS', '/NP', '/XJ',
        '/XD', (Join-Path $FrontendRoot 'node_modules'),
        (Join-Path $FrontendRoot 'logs'),
        '/XF', (Join-Path $FrontendRoot 'let')
    )
    if ($UsesNssmApiConfiguration) {
        [System.IO.File]::WriteAllText(
            (Join-Path $serviceBackup 'RhemaERPAPI.nssm-environment.json'),
            ((Get-NssmEnvironmentSnapshot) | ConvertTo-Json -Depth 6),
            (New-Object System.Text.UTF8Encoding($false)))
    }
    else {
        Copy-Item -LiteralPath $ApiServiceXml -Destination `
            (Join-Path $serviceBackup 'RhemaERPAPI.xml') -Force
    }

    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder `
        (Get-DatabaseConnectionString)
    $databaseName = $builder.InitialCatalog
    Assert-True (-not [string]::IsNullOrWhiteSpace($databaseName)) `
        'Database name is missing from the configured connection string.'
    $defaultBackup = @(Invoke-DatabaseTable `
        "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultBackupPath')) AS BackupPath;")[0].BackupPath
    Assert-True (-not [string]::IsNullOrWhiteSpace($defaultBackup)) `
        'SQL Server did not report its default backup path.'
    $sqlBackupPath = Join-Path $defaultBackup `
        ("{0}_pre_{1}.bak" -f $databaseName, $DeploymentId)
    Assert-True (-not (Test-Path -LiteralPath $sqlBackupPath)) `
        "SQL backup already exists: $sqlBackupPath"

    $safeDatabase = $databaseName.Replace(']', ']]')
    $safeBackupPath = $sqlBackupPath.Replace("'", "''")
    Invoke-DatabaseNonQuery @"
BACKUP DATABASE [$safeDatabase]
TO DISK = N'$safeBackupPath'
WITH COPY_ONLY, COMPRESSION, CHECKSUM, INIT, STATS = 10;
RESTORE VERIFYONLY FROM DISK = N'$safeBackupPath' WITH CHECKSUM;
"@ 3600
    Assert-True (Test-Path -LiteralPath $sqlBackupPath) `
        'SQL backup did not appear at the expected path.'

    $manifest = [ordered]@{
        deploymentId = $DeploymentId
        commit = $ExpectedCommit
        createdUtc = [DateTime]::UtcNow.ToString('o')
        applicationBackup = $backupRoot
        databaseBackup = $sqlBackupPath
        databaseBackupVerified = $true
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $backupRoot 'backup-manifest.json'),
        ($manifest | ConvertTo-Json -Depth 4),
        (New-Object System.Text.UTF8Encoding($false)))

    Write-Output "APPLICATION_BACKUP|$backupRoot"
    Write-Output "DATABASE_BACKUP|$sqlBackupPath"
    Write-Output 'BACKUP|PASS'
}

function Get-NewFatalStartupLine {
    param([DateTime]$StartedAt)

    $log = Get-ChildItem (Join-Path $ApiRoot 'logs\erp-api-*.log') -File `
        -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc | Select-Object -Last 1
    if ($null -eq $log -or $log.LastWriteTime -lt $StartedAt.AddSeconds(-2)) {
        return $null
    }
    $cutoff = $StartedAt.TimeOfDay
    $fatal = Get-Content -LiteralPath $log.FullName | Where-Object {
        if ($_ -notmatch '^\[(?<time>\d{2}:\d{2}:\d{2}) (ERR|FTL)\]') {
            return $false
        }
        $time = [TimeSpan]::Parse($Matches.time)
        return $time -ge $cutoff -and $_ -match `
            'Hosting failed|upgrade halted|OptionsValidationException|failed to start'
    } | Select-Object -Last 1
    return $fatal
}

function Wait-ApiReady {
    param([DateTime]$StartedAt)

    $deadline = (Get-Date).AddSeconds($ApiReadyTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $fatal = Get-NewFatalStartupLine $StartedAt
        if ($null -ne $fatal) {
            throw "API startup reported a fatal condition: $($fatal.Substring(0, [Math]::Min(400, $fatal.Length)))"
        }
        try {
            $response = Invoke-WebRequest 'http://127.0.0.1:5000/health/live' `
                -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) {
                Write-Output "API_READY|200|$($response.Content.Trim())"
                return
            }
        }
        catch {
            # Migrations and seed initialization run before the HTTP listener binds.
        }
        Start-Sleep -Seconds 5
    }
    throw "API readiness exceeded $ApiReadyTimeoutSeconds seconds."
}

function Wait-FrontendReady {
    param([int]$TimeoutSeconds = 600)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest 'http://127.0.0.1:3001/login' `
                -UseBasicParsing -TimeoutSec 30
            if ($response.StatusCode -eq 200) {
                Write-Output 'FRONTEND_READY|200'
                return
            }
        }
        catch { }
        Start-Sleep -Seconds 3
    }
    throw "Frontend readiness exceeded $TimeoutSeconds seconds."
}

function Start-ApiWithControlledMigrations {
    param([DateTime]$StartedAt)

    if ($UsesNssmApiConfiguration) {
        $originalEnvironment = Get-NssmEnvironmentSnapshot
        try {
            Set-ApiServiceEnvironmentValues @{
                'SkipStartupInitialization' = 'false'
                'StartupInitialization__SeedDevelopmentData' = 'false'
                'StartupInitialization__SeedWorkflowDefinitions' = 'false'
            }
            Start-Service RhemaERPAPI
            Wait-ApiReady $StartedAt
        }
        finally {
            Restore-NssmEnvironmentSnapshot $originalEnvironment
        }
        return
    }

    $originalXml = Get-ApiConfigurationXml
    $migrationXml = Get-ApiConfigurationXml
    Set-ServiceEnvironmentValue $migrationXml 'SkipStartupInitialization' 'false'
    Set-ServiceEnvironmentValue $migrationXml `
        'StartupInitialization__SeedDevelopmentData' 'false'
    Set-ServiceEnvironmentValue $migrationXml `
        'StartupInitialization__SeedWorkflowDefinitions' 'false'
    $migrationXml.Save($ApiServiceXml)

    try {
        Start-Service RhemaERPAPI
        Wait-ApiReady $StartedAt
    }
    finally {
        # The test VPS normally skips the expensive startup initializer. Enable it
        # only for the controlled deployment restart, then restore the exact service
        # configuration regardless of migration or readiness success.
        $originalXml.Save($ApiServiceXml)
    }
}

function Stop-ManagedService {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('RhemaERPAPI', 'RhemaERPFrontend')]
        [string]$Name,
        [int]$GracefulTimeoutSeconds = 45
    )

    $service = Get-Service $Name
    if ($service.Status -eq 'Stopped') { return }

    # Submit the graceful stop without allowing a stuck wrapper process to
    # block the whole release indefinitely.
    & sc.exe stop $Name | Out-Null
    $deadline = (Get-Date).AddSeconds($GracefulTimeoutSeconds)
    do {
        Start-Sleep -Seconds 2
        $service.Refresh()
    } while ($service.Status -ne 'Stopped' -and (Get-Date) -lt $deadline)

    if ($service.Status -ne 'Stopped') {
        $serviceProcess = Get-CimInstance Win32_Service |
            Where-Object Name -eq $Name
        if ($null -ne $serviceProcess -and $serviceProcess.ProcessId -gt 0) {
            Stop-Process -Id $serviceProcess.ProcessId -Force
        }
        (Get-Service $Name).WaitForStatus(
            'Stopped', [TimeSpan]::FromSeconds(30))
    }

    Assert-True ((Get-Service $Name).Status -eq 'Stopped') `
        "Service '$Name' did not stop within the controlled deployment window."
}

function Invoke-Apply {
    Assert-DeploymentId
    foreach ($value in @($ApiPackageName, $FrontendPackageName, $ApiSha256,
            $FrontendSha256, $ExpectedBuildId, $ExpectedCacheVersion)) {
        Assert-True (-not [string]::IsNullOrWhiteSpace($value)) `
            'Apply requires package names, hashes, build ID, and cache version.'
    }
    Assert-True ($ApiPackageName -eq [System.IO.Path]::GetFileName($ApiPackageName)) `
        'ApiPackageName must be a file name, not a path.'
    Assert-True ($FrontendPackageName -eq [System.IO.Path]::GetFileName($FrontendPackageName)) `
        'FrontendPackageName must be a file name, not a path.'

    $apiZip = Join-Path $PackagesRoot $ApiPackageName
    $frontendZip = Join-Path $PackagesRoot $FrontendPackageName
    Assert-True (Test-Path -LiteralPath $apiZip) "API package is missing: $apiZip"
    Assert-True (Test-Path -LiteralPath $frontendZip) `
        "Frontend package is missing: $frontendZip"
    Assert-True ((Get-FileHash $apiZip -Algorithm SHA256).Hash -eq $ApiSha256) `
        'Uploaded API package hash does not match the release manifest.'
    Assert-True ((Get-FileHash $frontendZip -Algorithm SHA256).Hash -eq $FrontendSha256) `
        'Uploaded frontend package hash does not match the release manifest.'

    $stage = Join-Path $PackagesRoot "stage-$DeploymentId"
    $retired = Join-Path $PackagesRoot "retired-$DeploymentId"
    $failed = Join-Path $PackagesRoot "failed-$DeploymentId"
    $backup = Join-Path $BackupsRoot "deploy-$DeploymentId"
    Assert-True (Test-Path -LiteralPath $backup) `
        'The matching verified backup must exist before apply.'
    Assert-True (-not (Test-Path -LiteralPath $stage)) `
        "Deployment stage already exists: $stage"
    New-Item -ItemType Directory -Path (Join-Path $stage 'api'), `
        (Join-Path $stage 'frontend'), $retired | Out-Null
    Expand-Archive -LiteralPath $apiZip -DestinationPath (Join-Path $stage 'api') -Force
    Expand-Archive -LiteralPath $frontendZip `
        -DestinationPath (Join-Path $stage 'frontend') -Force

    $stageApi = Join-Path $stage 'api'
    $stageFrontend = Join-Path $stage 'frontend'
    Assert-True (Test-Path -LiteralPath (Join-Path $stageApi 'ErpSystem.Api.exe')) `
        'Staged API executable is missing.'
    Assert-True (Test-Path -LiteralPath (Join-Path $stageFrontend 'package-lock.json')) `
        'Staged frontend dependency lock is missing.'
    Assert-True (Test-Path -LiteralPath (Join-Path $stageFrontend 'next.config.js')) `
        'Staged frontend Next.js configuration is missing.'
    Assert-True (Test-Path -LiteralPath `
            (Join-Path $stageFrontend 'node_modules\next\package.json')) `
        'Staged frontend Next.js runtime is missing.'
    $stagedFrontendPackage = Get-Content `
        (Join-Path $stageFrontend 'package.json') -Raw | ConvertFrom-Json
    $stagedNextPackage = Get-Content `
        (Join-Path $stageFrontend 'node_modules\next\package.json') -Raw |
        ConvertFrom-Json
    Assert-True ($stagedNextPackage.version -eq `
            $stagedFrontendPackage.dependencies.next) `
        'Staged frontend Next.js runtime differs from package.json.'
    $forbiddenApiConfig = @(Get-ChildItem $stageApi -File -Force | Where-Object {
        $_.Name -like 'appsettings*.json' -or $_.Name -like '.env*'
    })
    Assert-True ($forbiddenApiConfig.Count -eq 0) `
        'API package contains protected runtime configuration.'
    $stagedBuildId = (Get-Content `
        (Join-Path $stageFrontend '.next\BUILD_ID') -Raw).Trim()
    Assert-True ($stagedBuildId -eq $ExpectedBuildId) `
        'Staged frontend build ID differs from the release manifest.'
    $serviceWorker = Get-Content (Join-Path $stageFrontend 'public\sw.js') -Raw
    Assert-True ($serviceWorker -match [regex]::Escape($ExpectedCacheVersion)) `
        'Staged service-worker cache version differs from the release manifest.'

    Set-TestServerConfiguration
    $apiStartedAt = Get-Date
    try {
        Stop-ManagedService RhemaERPAPI
        Invoke-RobocopyChecked @(
            $stageApi, $ApiRoot, '/E', '/R:2', '/W:2', '/NFL', '/NDL',
            '/NJH', '/NJS', '/NP',
            '/XF', 'appsettings.json', 'appsettings.Production.json',
            'appsettings.Development.json', 'appsettings.AntiSpam.json',
            '.env', '.env.production',
            '/XD', (Join-Path $ApiRoot 'wwwroot\uploads'),
            (Join-Path $ApiRoot 'logs'), (Join-Path $ApiRoot 'secure-file-storage')
        )
        Start-ApiWithControlledMigrations $apiStartedAt
    }
    catch {
        Stop-Service RhemaERPAPI -Force -ErrorAction SilentlyContinue
        Invoke-RobocopyChecked @(
            (Join-Path $backup 'api'), $ApiRoot, '/E', '/R:2', '/W:2',
            '/NFL', '/NDL', '/NJH', '/NJS', '/NP',
            '/XD', (Join-Path $ApiRoot 'wwwroot\uploads'),
            (Join-Path $ApiRoot 'logs'), (Join-Path $ApiRoot 'secure-file-storage')
        )
        if ($UsesNssmApiConfiguration) {
            $serviceBackup = Join-Path $backup 'services\api\RhemaERPAPI.nssm-environment.json'
            if (Test-Path -LiteralPath $serviceBackup) {
                Restore-NssmEnvironmentSnapshot `
                    (Get-Content -LiteralPath $serviceBackup -Raw | ConvertFrom-Json)
            }
        }
        else {
            $serviceBackup = Join-Path $backup 'services\api\RhemaERPAPI.xml'
            if (Test-Path -LiteralPath $serviceBackup) {
                Copy-Item -LiteralPath $serviceBackup -Destination $ApiServiceXml -Force
            }
        }
        Start-Service RhemaERPAPI -ErrorAction SilentlyContinue
        throw "API apply failed and application files were rolled back: $($_.Exception.Message)"
    }

    try {
        Stop-ManagedService RhemaERPFrontend
        if (Test-Path (Join-Path $FrontendRoot '.next')) {
            Move-Item (Join-Path $FrontendRoot '.next') (Join-Path $retired '.next')
        }
        if (Test-Path (Join-Path $FrontendRoot 'public')) {
            Move-Item (Join-Path $FrontendRoot 'public') (Join-Path $retired 'public')
        }
        if (Test-Path (Join-Path $FrontendRoot 'node_modules')) {
            Move-Item (Join-Path $FrontendRoot 'node_modules') `
                (Join-Path $retired 'node_modules')
        }
        foreach ($name in @('package.json', 'package-lock.json', 'next.config.js')) {
            $liveFile = Join-Path $FrontendRoot $name
            if (Test-Path $liveFile) {
                Copy-Item $liveFile (Join-Path $retired $name) -Force
            }
        }
        Move-Item (Join-Path $stageFrontend '.next') (Join-Path $FrontendRoot '.next')
        Move-Item (Join-Path $stageFrontend 'public') (Join-Path $FrontendRoot 'public')
        Move-Item (Join-Path $stageFrontend 'node_modules') `
            (Join-Path $FrontendRoot 'node_modules')
        Copy-Item (Join-Path $stageFrontend 'package.json'), `
            (Join-Path $stageFrontend 'package-lock.json'), `
            (Join-Path $stageFrontend 'next.config.js') `
            -Destination $FrontendRoot -Force
        Start-Service RhemaERPFrontend
        Wait-FrontendReady
    }
    catch {
        Stop-Service RhemaERPFrontend -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Path $failed -Force | Out-Null
        foreach ($name in @('.next', 'public', 'node_modules')) {
            $livePath = Join-Path $FrontendRoot $name
            if (Test-Path $livePath) { Move-Item $livePath (Join-Path $failed $name) -Force }
            $oldPath = Join-Path $retired $name
            if (Test-Path $oldPath) { Move-Item $oldPath $livePath }
        }
        foreach ($name in @('package.json', 'package-lock.json', 'next.config.js')) {
            $liveFile = Join-Path $FrontendRoot $name
            if (Test-Path $liveFile) { Remove-Item $liveFile -Force }
            $oldFile = Join-Path $retired $name
            if (Test-Path $oldFile) { Copy-Item $oldFile $liveFile -Force }
        }
        Start-Service RhemaERPFrontend -ErrorAction SilentlyContinue
        throw "Frontend apply failed and was rolled back: $($_.Exception.Message)"
    }

    $release = [ordered]@{
        deploymentId = $DeploymentId
        commit = $ExpectedCommit
        buildId = $ExpectedBuildId
        cacheVersion = $ExpectedCacheVersion
        deployedUtc = [DateTime]::UtcNow.ToString('o')
        apiSha256 = $ApiSha256
        frontendSha256 = $FrontendSha256
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $LogsRoot 'current-release.json'),
        ($release | ConvertTo-Json -Depth 4),
        (New-Object System.Text.UTF8Encoding($false)))
    Write-Output 'APPLY|PASS'
}

function Invoke-ResumeFrontend {
    Assert-DeploymentId
    foreach ($value in @($ExpectedCommit, $ExpectedBuildId, $ExpectedCacheVersion,
            $ApiSha256, $FrontendSha256)) {
        Assert-True (-not [string]::IsNullOrWhiteSpace($value)) `
            'ResumeFrontend requires commit, build, cache, and package hashes.'
    }

    $stageFrontend = Join-Path $PackagesRoot "stage-$DeploymentId\frontend"
    $failedFrontend = Join-Path $PackagesRoot "failed-$DeploymentId"
    $rollbackFrontend = Join-Path $PackagesRoot "resume-old-$DeploymentId"
    $retryFailed = Join-Path $PackagesRoot "resume-failed-$DeploymentId"
    Assert-True (Test-Path (Join-Path $stageFrontend 'package-lock.json')) `
        'The staged frontend dependency lock is missing.'
    Assert-True (Test-Path (Join-Path $stageFrontend 'next.config.js')) `
        'The staged frontend Next.js configuration is missing.'
    Assert-True (Test-Path (Join-Path $failedFrontend '.next\BUILD_ID')) `
        'The failed frontend build is unavailable for retry.'
    Assert-True (Test-Path `
            (Join-Path $failedFrontend 'node_modules\next\package.json')) `
        'The failed frontend runtime dependencies are unavailable for retry.'
    Assert-True (-not (Test-Path $rollbackFrontend)) `
        "Frontend retry rollback path already exists: $rollbackFrontend"

    $candidateBuildId = (Get-Content `
        (Join-Path $failedFrontend '.next\BUILD_ID') -Raw).Trim()
    Assert-True ($candidateBuildId -eq $ExpectedBuildId) `
        'The retry candidate build differs from the expected release.'
    $candidateWorker = Get-Content `
        (Join-Path $failedFrontend 'public\sw.js') -Raw
    Assert-True ($candidateWorker -match [regex]::Escape($ExpectedCacheVersion)) `
        'The retry candidate service-worker version differs from the release.'

    $currentResponse = Invoke-WebRequest 'http://127.0.0.1:3001/login' `
        -UseBasicParsing -TimeoutSec 30
    Assert-True ($currentResponse.StatusCode -eq 200) `
        'The current frontend is not healthy enough for a controlled retry.'

    New-Item -ItemType Directory -Path $rollbackFrontend | Out-Null
    $swapped = $false
    try {
        Stop-ManagedService RhemaERPFrontend
        foreach ($name in @('.next', 'public', 'node_modules')) {
            Move-Item (Join-Path $FrontendRoot $name) `
                (Join-Path $rollbackFrontend $name)
        }
        foreach ($name in @('package.json', 'package-lock.json', 'next.config.js')) {
            $liveFile = Join-Path $FrontendRoot $name
            if (Test-Path $liveFile) {
                Copy-Item $liveFile (Join-Path $rollbackFrontend $name) -Force
            }
        }
        foreach ($name in @('.next', 'public', 'node_modules')) {
            Move-Item (Join-Path $failedFrontend $name) `
                (Join-Path $FrontendRoot $name)
        }
        Copy-Item (Join-Path $stageFrontend 'package.json'), `
            (Join-Path $stageFrontend 'package-lock.json'), `
            (Join-Path $stageFrontend 'next.config.js') `
            -Destination $FrontendRoot -Force
        $swapped = $true
        Start-Service RhemaERPFrontend
        Wait-FrontendReady -TimeoutSeconds 600

        $liveBuildId = (Get-Content `
            (Join-Path $FrontendRoot '.next\BUILD_ID') -Raw).Trim()
        Assert-True ($liveBuildId -eq $ExpectedBuildId) `
            'The live frontend build differs from the expected release.'
        $release = [ordered]@{
            deploymentId = $DeploymentId
            commit = $ExpectedCommit
            buildId = $ExpectedBuildId
            cacheVersion = $ExpectedCacheVersion
            deployedUtc = [DateTime]::UtcNow.ToString('o')
            apiSha256 = $ApiSha256
            frontendSha256 = $FrontendSha256
        }
        [System.IO.File]::WriteAllText(
            (Join-Path $LogsRoot 'current-release.json'),
            ($release | ConvertTo-Json -Depth 4),
            (New-Object System.Text.UTF8Encoding($false)))
    }
    catch {
        $failure = $_.Exception.Message
        if ($swapped) {
            Stop-Service RhemaERPFrontend -Force -ErrorAction SilentlyContinue
            New-Item -ItemType Directory -Path $retryFailed -Force | Out-Null
            foreach ($name in @('.next', 'public', 'node_modules')) {
                $livePath = Join-Path $FrontendRoot $name
                if (Test-Path $livePath) {
                    Move-Item $livePath (Join-Path $retryFailed $name) -Force
                }
                $oldPath = Join-Path $rollbackFrontend $name
                if (Test-Path $oldPath) { Move-Item $oldPath $livePath }
            }
            foreach ($name in @('package.json', 'package-lock.json', 'next.config.js')) {
                $liveFile = Join-Path $FrontendRoot $name
                if (Test-Path $liveFile) { Remove-Item $liveFile -Force }
                $oldFile = Join-Path $rollbackFrontend $name
                if (Test-Path $oldFile) { Copy-Item $oldFile $liveFile -Force }
            }
            Start-Service RhemaERPFrontend -ErrorAction SilentlyContinue
        }
        throw "Frontend retry failed and rollback was attempted: $failure"
    }

    Write-Output 'RESUME_FRONTEND|PASS'
}

function Invoke-LocalRouteWithRetry {
    param(
        [Parameter(Mandatory = $true)][string]$Uri,
        [Parameter(Mandatory = $true)][string]$Name,
        [int]$DeadlineSeconds = 180,
        [int]$AttemptTimeoutSeconds = 30
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($DeadlineSeconds)
    $attempts = 0
    $lastFailure = 'No request was attempted.'
    do {
        $attempts++
        try {
            $response = Invoke-WebRequest $Uri -UseBasicParsing `
                -TimeoutSec $AttemptTimeoutSeconds
            if ($response.StatusCode -eq 200) {
                Write-Output "LOCAL_ROUTE|$Name|200|ATTEMPTS=$attempts"
                return
            }
            $lastFailure = "HTTP $($response.StatusCode)"
        }
        catch {
            $lastFailure = $_.Exception.Message
        }

        if ([DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Seconds 3
        }
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Local route '$Name' ($Uri) did not return HTTP 200 within $DeadlineSeconds seconds after $attempts attempt(s). Last failure: $lastFailure"
}

function Invoke-Verify {
    Assert-SyncfusionLicenseConfigured
    Write-ServiceState
    $notRunning = @(Get-ManagedServices | Where-Object { $_.Status -ne 'Running' })
    Assert-True ($notRunning.Count -eq 0) 'One or more VPS services are not running.'

    # Prove the process first, then dependencies and aggregate health. A freshly started
    # NSSM service can report Running before ASP.NET or Next.js has completed initialization,
    # so bounded retries distinguish normal warm-up from a genuinely unhealthy endpoint.
    foreach ($item in @(
            @{ Uri = 'http://127.0.0.1:5000/health/live'; Name = 'api-live'; Deadline = 60; AttemptTimeout = 10 },
            @{ Uri = 'http://127.0.0.1:5000/health/ready'; Name = 'api-ready'; Deadline = 180; AttemptTimeout = 30 },
            @{ Uri = 'http://127.0.0.1:5000/health'; Name = 'api-health'; Deadline = 180; AttemptTimeout = 30 },
            @{ Uri = 'http://127.0.0.1:3001/login'; Name = 'frontend-login'; Deadline = 180; AttemptTimeout = 30 })) {
        Invoke-LocalRouteWithRetry -Uri $item.Uri -Name $item.Name `
            -DeadlineSeconds $item.Deadline -AttemptTimeoutSeconds $item.AttemptTimeout
    }

    $history = @(Get-MigrationHistory)
    foreach ($row in $history) { Write-Output "MIGRATION_ID|$($row.MigrationId)" }
    $summary = @(Get-DatabaseControlSummary)[0]
    Write-Output "MIGRATION_COUNT|$($summary.MigrationCount)"
    Write-Output "LATEST_MIGRATION|$($summary.LatestMigration)"
    Write-Output "UNTRUSTED_FKS|$($summary.UntrustedForeignKeys)"
    Write-Output "DISABLED_FKS|$($summary.DisabledForeignKeys)"
    Write-Output "SVG_FILE_UPLOAD_POLICIES|$($summary.SvgFileUploadPolicies)"
    Write-Output "SVG_SUPPLIER_REQUIREMENTS|$($summary.SvgSupplierRequirements)"
    Assert-True ([long]$summary.UntrustedForeignKeys -eq 0) `
        'One or more database foreign keys are untrusted.'
    Assert-True ([long]$summary.DisabledForeignKeys -eq 0) `
        'One or more database foreign keys are disabled.'
    Assert-True ([long]$summary.SvgFileUploadPolicies -eq 0) `
        'Active SVG entries exist in a shared file-upload policy.'
    Assert-True ([long]$summary.SvgSupplierRequirements -eq 0) `
        'Active SVG entries exist in a supplier evidence requirement.'

    $tenderPaymentPermission = @(Get-TenderPaymentPermissionSummary)[0]
    Write-Output "TENDER_PAYMENT_VERIFY_PERMISSIONS|$($tenderPaymentPermission.PermissionCount)"
    Write-Output "TENDER_PAYMENT_VERIFY_ROLE_GRANTS|$($tenderPaymentPermission.RoleGrantCount)"
    Assert-True ([long]$tenderPaymentPermission.PermissionCount -eq 1) `
        'The tender-payment verification permission catalogue row is missing or duplicated.'
    Assert-True ([long]$tenderPaymentPermission.RoleGrantCount -eq 2) `
        'The tender-payment verification permission is not granted to both operational TDC roles.'

    $buildId = (Get-Content (Join-Path $FrontendRoot '.next\BUILD_ID') -Raw).Trim()
    Write-Output "DEPLOYED_BUILD_ID|$buildId"
    if (-not [string]::IsNullOrWhiteSpace($ExpectedBuildId)) {
        Assert-True ($buildId -eq $ExpectedBuildId) `
            'Deployed frontend build ID differs from the expected release.'
    }
    $serviceWorker = Get-Content (Join-Path $FrontendRoot 'public\sw.js') -Raw
    if (-not [string]::IsNullOrWhiteSpace($ExpectedCacheVersion)) {
        Assert-True ($serviceWorker -match [regex]::Escape($ExpectedCacheVersion)) `
            'Deployed service-worker version differs from the expected release.'
    }
    Assert-True (Test-Path (Join-Path $ApiRoot 'wwwroot\uploads')) `
        'Runtime uploads directory is missing.'
    Assert-True (Test-Path (Join-Path $ApiRoot 'secure-file-storage')) `
        'Secure DMS storage directory is missing.'

    Write-Output 'VERIFY|PASS'
}

switch ($Action) {
    'Preflight' { Invoke-Preflight }
    'Backup' { Invoke-Backup }
    'Apply' { Invoke-Apply }
    'ResumeFrontend' { Invoke-ResumeFrontend }
    'Verify' { Invoke-Verify }
}
