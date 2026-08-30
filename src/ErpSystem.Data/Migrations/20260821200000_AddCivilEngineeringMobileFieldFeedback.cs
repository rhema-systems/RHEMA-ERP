using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>CIV-0504 structured Civil mobile field feedback on the existing append-only direct-task feedback owner.</summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260821200000_AddCivilEngineeringMobileFieldFeedback")]
public partial class AddCivilEngineeringMobileFieldFeedback : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE ProjectCivilDirectTaskFeedbackEntries ADD
              MeasurementValue decimal(18,4) NULL,
              MeasurementUnitId uniqueidentifier NULL,
              CapturedOfflineAtUtc datetime2 NULL,
              CONSTRAINT FK_ProjectCivilDirectTaskFeedbackEntries_MeasurementUnit FOREIGN KEY (MeasurementUnitId) REFERENCES UnitsOfMeasure(Id),
              CONSTRAINT CK_ProjectCivilDirectTaskFeedbackEntries_Measurement CHECK ((MeasurementValue IS NULL AND MeasurementUnitId IS NULL) OR (MeasurementValue IS NOT NULL AND MeasurementValue >= 0 AND MeasurementUnitId IS NOT NULL));
            CREATE INDEX IX_ProjectCivilDirectTaskFeedbackEntries_TenantId_TaskId_CapturedOfflineAtUtc ON ProjectCivilDirectTaskFeedbackEntries(TenantId,DirectTaskControlId,CapturedOfflineAtUtc);
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDirectTaskFeedbackEntries_FieldCapture ON ProjectCivilDirectTaskFeedbackEntries AFTER INSERT AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN UnitsOfMeasure unit ON unit.Id=value.MeasurementUnitId AND unit.TenantId=value.TenantId AND unit.IsActive=1 AND unit.IsDeleted=0
                WHERE (value.MeasurementValue IS NULL AND value.MeasurementUnitId IS NOT NULL)
                   OR (value.MeasurementValue IS NOT NULL AND (value.MeasurementValue < 0 OR unit.Id IS NULL))
                   OR (value.CapturedOfflineAtUtc IS NOT NULL AND (value.CapturedOfflineAtUtc > DATEADD(minute,5,value.CreatedAt) OR value.CapturedOfflineAtUtc < DATEADD(day,-31,value.CreatedAt)))
              ) THROW 52291, 'Civil mobile field feedback measurement, unit or offline-capture lineage is invalid.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_ProjectCivilDirectTaskFeedbackEntries_FieldCapture;
            DROP INDEX IF EXISTS IX_ProjectCivilDirectTaskFeedbackEntries_TenantId_TaskId_CapturedOfflineAtUtc ON ProjectCivilDirectTaskFeedbackEntries;
            ALTER TABLE ProjectCivilDirectTaskFeedbackEntries DROP CONSTRAINT IF EXISTS CK_ProjectCivilDirectTaskFeedbackEntries_Measurement;
            ALTER TABLE ProjectCivilDirectTaskFeedbackEntries DROP CONSTRAINT IF EXISTS FK_ProjectCivilDirectTaskFeedbackEntries_MeasurementUnit;
            ALTER TABLE ProjectCivilDirectTaskFeedbackEntries DROP COLUMN IF EXISTS MeasurementValue, MeasurementUnitId, CapturedOfflineAtUtc;
            """);
    }
}
