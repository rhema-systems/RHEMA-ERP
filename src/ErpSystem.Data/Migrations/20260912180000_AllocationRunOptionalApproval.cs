using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912180000_AllocationRunOptionalApproval")]
public sealed class AllocationRunOptionalApproval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "ApprovalRequired", table: "AllocationRunBatches", type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_AllocationRunBatches_ApprovalPolicy
            ON dbo.AllocationRunBatches AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                    WHERE (i.Status=6 AND i.ApprovalRequired<>0)
                        OR (i.ApprovalRequired=0 AND (
                            d.Id IS NULL OR i.Status NOT IN (4,6) OR i.WorkflowInstanceId IS NOT NULL
                            OR i.ApprovedAt IS NOT NULL OR i.ApprovedBy IS NOT NULL
                            OR NULLIF(i.ApprovedByName,N'') IS NOT NULL
                            OR i.SubmittedAt IS NULL OR i.SubmittedBy IS NULL))
                        OR (d.Id IS NOT NULL AND d.ApprovalRequired<>i.ApprovalRequired AND NOT (
                            d.Status=0 AND i.Status=6 AND i.ApprovalRequired=0
                            AND d.ApprovedAt IS NULL AND d.ApprovedBy IS NULL AND d.WorkflowInstanceId IS NULL
                            AND NULLIF(d.ApprovedByName,N'') IS NULL
                            AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'AllocationRunBatch',i.Id)=0)))
                    THROW 51996, 'ALLOCATION_APPROVAL_STATE_INVALID: direct readiness requires a new validated submission without active approval or fabricated human approval.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS(SELECT 1 FROM dbo.AllocationRunBatches WHERE ApprovalRequired=0 OR Status=6)
                THROW 51997, 'Direct allocation history exists. Roll forward to preserve its decision.', 1;
            DROP TRIGGER IF EXISTS dbo.TR_AllocationRunBatches_ApprovalPolicy;
            """);
        migrationBuilder.DropColumn(name: "ApprovalRequired", table: "AllocationRunBatches");
    }
}
