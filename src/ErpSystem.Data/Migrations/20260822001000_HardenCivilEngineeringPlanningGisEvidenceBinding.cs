using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Binds each Planning/GIS validation evidence reference to the selected
/// development-approval file. Central DMS and the approval-file evidence
/// register remain the authoritative document owners.
/// </summary>
public partial class HardenCivilEngineeringPlanningGisEvidenceBinding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilPlanningGisValidations_EvidenceBinding
            ON dbo.ProjectCivilPlanningGisValidations
            AFTER INSERT, UPDATE
            AS
            BEGIN
              SET NOCOUNT ON;

              IF EXISTS (
                SELECT 1
                FROM inserted i
                LEFT JOIN dbo.ProjectCivilDevelopmentApprovalEvidence approvalEvidence
                  ON approvalEvidence.TenantId = i.TenantId
                    AND approvalEvidence.DevelopmentApprovalFileId = i.DevelopmentApprovalFileId
                    AND approvalEvidence.CentralDocumentRecordId = i.CentralDocumentRecordId
                    AND approvalEvidence.CentralDocumentVersionId = i.CentralDocumentVersionId
                    AND approvalEvidence.IsDeleted = 0
                WHERE approvalEvidence.Id IS NULL
              )
                THROW 51938, 'Planning/GIS evidence must be current central-DMS evidence linked to the selected development-approval file.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilPlanningGisValidations_EvidenceBinding;
            """);
    }
}
