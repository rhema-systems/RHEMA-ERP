using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Allows an existing Draft inspection created before the dedicated receipt-inspection
/// workflow was introduced to bind that workflow exactly once during submission. All
/// other source and governance lineage remains immutable.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260814143000_INVREQFU004AllowDraftInspectionWorkflowRebind")]
public partial class INVREQFU004AllowDraftInspectionWorkflowRebind : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @definition nvarchar(max) =
                OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[TR_ProcurementReceiptInspectionCases_TDC0502Protected]'));
            IF @definition IS NULL
                THROW 51970, 'The TDC-0502 receipt-inspection protection trigger is required.', 1;

            DECLARE @old nvarchar(max) =
                N'OR i.WorkflowDefinitionId <> d.WorkflowDefinitionId';
            DECLARE @new nvarchar(max) = N'OR (
                               i.WorkflowDefinitionId <> d.WorkflowDefinitionId
                               AND NOT (
                                   d.Status = 0
                                   AND i.Status = 1
                                   AND d.WorkflowInstanceId IS NULL
                                   AND i.WorkflowInstanceId IS NULL
                                   AND TRY_CONVERT(uniqueidentifier,
                                       SESSION_CONTEXT(N''TDC0502_RECEIPT_INSPECTION_CASE_ID'')) = i.Id
                                   AND TRY_CONVERT(uniqueidentifier,
                                       SESSION_CONTEXT(N''TDC0502_RECEIPT_INSPECTION_WORKFLOW_ID'')) = i.WorkflowDefinitionId
                                   AND EXISTS (
                                       SELECT 1
                                       FROM WorkflowDefinitions workflow
                                       JOIN WorkflowEntityTypes entityType
                                         ON entityType.Id = workflow.EntityTypeId
                                        AND entityType.TenantId = i.TenantId
                                        AND entityType.IsActive = 1
                                        AND entityType.IsDeleted = 0
                                       WHERE workflow.Id = i.WorkflowDefinitionId
                                         AND workflow.TenantId = i.TenantId
                                         AND workflow.IsActive = 1
                                         AND workflow.IsDeleted = 0
                                         AND workflow.LifecycleStatus = 1
                                         AND entityType.Code = N''PROCUREMENT_RECEIPT_INSPECTION''
                                   )
                               )
                           )';

            IF CHARINDEX(N'TDC0502_RECEIPT_INSPECTION_WORKFLOW_ID', @definition) = 0
            BEGIN
                IF CHARINDEX(@old, @definition) = 0
                    THROW 51971, 'The receipt-inspection lineage trigger is not the verified TDC-0502 baseline.', 1;
                SET @definition = REPLACE(@definition, @old, @new);
                DECLARE @triggerKeywordPosition int = CHARINDEX(N'TRIGGER', UPPER(@definition));
                IF @triggerKeywordPosition = 0
                    THROW 51972, 'The receipt-inspection trigger declaration could not be altered safely.', 1;
                SET @definition = N'ALTER ' + SUBSTRING(
                    @definition, @triggerKeywordPosition, LEN(@definition));
                EXEC sys.sp_executesql @definition;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @definition nvarchar(max) =
                OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[TR_ProcurementReceiptInspectionCases_TDC0502Protected]'));
            IF @definition IS NULL
                THROW 51970, 'The TDC-0502 receipt-inspection protection trigger is required.', 1;

            DECLARE @old nvarchar(max) = N'OR (
                               i.WorkflowDefinitionId <> d.WorkflowDefinitionId
                               AND NOT (
                                   d.Status = 0
                                   AND i.Status = 1
                                   AND d.WorkflowInstanceId IS NULL
                                   AND i.WorkflowInstanceId IS NULL
                                   AND TRY_CONVERT(uniqueidentifier,
                                       SESSION_CONTEXT(N''TDC0502_RECEIPT_INSPECTION_CASE_ID'')) = i.Id
                                   AND TRY_CONVERT(uniqueidentifier,
                                       SESSION_CONTEXT(N''TDC0502_RECEIPT_INSPECTION_WORKFLOW_ID'')) = i.WorkflowDefinitionId
                                   AND EXISTS (
                                       SELECT 1
                                       FROM WorkflowDefinitions workflow
                                       JOIN WorkflowEntityTypes entityType
                                         ON entityType.Id = workflow.EntityTypeId
                                        AND entityType.TenantId = i.TenantId
                                        AND entityType.IsActive = 1
                                        AND entityType.IsDeleted = 0
                                       WHERE workflow.Id = i.WorkflowDefinitionId
                                         AND workflow.TenantId = i.TenantId
                                         AND workflow.IsActive = 1
                                         AND workflow.IsDeleted = 0
                                         AND workflow.LifecycleStatus = 1
                                         AND entityType.Code = N''PROCUREMENT_RECEIPT_INSPECTION''
                                   )
                               )
                           )';
            DECLARE @new nvarchar(max) =
                N'OR i.WorkflowDefinitionId <> d.WorkflowDefinitionId';

            IF CHARINDEX(N'TDC0502_RECEIPT_INSPECTION_WORKFLOW_ID', @definition) > 0
            BEGIN
                IF CHARINDEX(@old, @definition) = 0
                    THROW 51973, 'The governed workflow-rebind trigger fragment was not found.', 1;
                SET @definition = REPLACE(@definition, @old, @new);
                DECLARE @triggerKeywordPosition int = CHARINDEX(N'TRIGGER', UPPER(@definition));
                IF @triggerKeywordPosition = 0
                    THROW 51972, 'The receipt-inspection trigger declaration could not be altered safely.', 1;
                SET @definition = N'ALTER ' + SUBSTRING(
                    @definition, @triggerKeywordPosition, LEN(@definition));
                EXEC sys.sp_executesql @definition;
            END;
            """);
    }
}
