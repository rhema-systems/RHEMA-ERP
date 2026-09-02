using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Extends the append-only inspection revision action allowlist after the initial
/// inspection migration was applied. No owner data or lifecycle state is changed.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260822003100_HardenCivilEngineeringInspectionAuditActions")]
public partial class HardenCivilEngineeringInspectionAuditActions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilInspectionRevisions_Lineage ON dbo.ProjectCivilInspectionRevisions AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN dbo.ProjectCivilInspectionControls control ON control.Id=value.InspectionControlId AND control.TenantId=value.TenantId AND control.IsDeleted=0
                LEFT JOIN dbo.Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId AND actor.IsActive=1
                WHERE control.Id IS NULL OR actor.Id IS NULL OR LEN(value.RequestHash)<>64 OR value.Action NOT IN ('CreateCivilInspection','ApproveCivilInspection','RejectCivilInspection','SubmitCivilDefectReinspection','ApproveCivilDefectClosure','RejectCivilDefectClosure','CloseCivilInspection')
              ) THROW 52083, 'Civil inspection revision lineage is invalid.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilInspectionRevisions_Lineage ON dbo.ProjectCivilInspectionRevisions AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN dbo.ProjectCivilInspectionControls control ON control.Id=value.InspectionControlId AND control.TenantId=value.TenantId AND control.IsDeleted=0
                LEFT JOIN dbo.Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId AND actor.IsActive=1
                WHERE control.Id IS NULL OR actor.Id IS NULL OR LEN(value.RequestHash)<>64 OR value.Action NOT IN ('CreateCivilInspection','ApproveCivilInspection','RejectCivilInspection','UpdateCivilDefectCorrectiveAction','ApproveCivilDefectClosure','RejectCivilDefectClosure','ApproveCivilWorkClosure')
              ) THROW 52083, 'Civil inspection revision lineage is invalid.', 1;
            END;
            """);
    }
}
