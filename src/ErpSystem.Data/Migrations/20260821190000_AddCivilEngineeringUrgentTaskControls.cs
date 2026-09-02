using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>CIV-0503 controlled urgent-task SLA, notification-escalation and immutable escalation lineage.</summary>
public partial class AddCivilEngineeringUrgentTaskControls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE ProjectCivilDirectTaskControls ADD
              IsUrgentPath bit NOT NULL CONSTRAINT DF_ProjectCivilDirectTaskControls_IsUrgentPath DEFAULT 0,
              UrgencyReason nvarchar(1000) NULL,
              UrgentResponseDueAt datetime2 NULL,
              UrgentEscalatedAt datetime2 NULL,
              UrgentEscalationClientRequestId uniqueidentifier NULL,
              UrgentEscalationRequestHash varchar(64) NULL,
              CONSTRAINT CK_ProjectCivilDirectTaskControls_UrgentPath CHECK (
                ([IsUrgentPath] = 0 AND [UrgencyReason] IS NULL AND [UrgentResponseDueAt] IS NULL AND [UrgentEscalatedAt] IS NULL AND [UrgentEscalationClientRequestId] IS NULL AND [UrgentEscalationRequestHash] IS NULL)
                OR
                ([IsUrgentPath] = 1 AND [Urgency] IN (2,3) AND [UrgencyReason] IS NOT NULL AND LEN(LTRIM(RTRIM([UrgencyReason]))) BETWEEN 5 AND 1000 AND [UrgentResponseDueAt] IS NOT NULL AND [DueDate] <= [UrgentResponseDueAt])
              );
            CREATE INDEX IX_ProjectCivilDirectTaskControls_TenantId_ProjectId_UrgentResponseDueAt ON ProjectCivilDirectTaskControls(TenantId,ProjectId,IsUrgentPath,UrgentResponseDueAt,UrgentEscalatedAt);
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDirectTaskControls_UrgentPath ON ProjectCivilDirectTaskControls AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;

              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN CivilEngineeringConfigurationDecisions decision ON decision.Id=value.ConfigurationDecisionId AND decision.ProfileId=value.ConfigurationProfileId AND decision.TenantId=value.TenantId AND decision.IsDeleted=0 AND decision.ConfigurationKey='CIV-CFG-010' AND decision.Status=2 AND decision.ApprovalStatus=1 AND decision.EvidenceStatus=2
                OUTER APPLY (SELECT TRY_CONVERT(int, JSON_VALUE(decision.ValueJson,'$.urgentResponseHours')) AS UrgentResponseHours) policy
                WHERE (value.IsUrgentPath=1 AND (
                    decision.Id IS NULL
                    OR policy.UrgentResponseHours IS NULL OR policy.UrgentResponseHours < 1
                    OR value.Urgency NOT IN (2,3)
                    OR value.UrgencyReason IS NULL OR LEN(LTRIM(RTRIM(value.UrgencyReason))) NOT BETWEEN 5 AND 1000
                    OR value.UrgentResponseDueAt IS NULL OR value.DueDate > value.UrgentResponseDueAt
                    OR value.UrgentResponseDueAt > DATEADD(hour,policy.UrgentResponseHours,value.CreatedAt)
                    OR NOT EXISTS (SELECT 1 FROM OPENJSON(decision.ValueJson,'$.urgentEscalationRoleIds') configuredRole WHERE TRY_CONVERT(uniqueidentifier,configuredRole.value) IS NOT NULL)
                  ))
                  OR (value.IsUrgentPath=0 AND (value.UrgencyReason IS NOT NULL OR value.UrgentResponseDueAt IS NOT NULL OR value.UrgentEscalatedAt IS NOT NULL OR value.UrgentEscalationClientRequestId IS NOT NULL OR value.UrgentEscalationRequestHash IS NOT NULL))
              ) THROW 52289, 'Civil urgent-task SLA, configuration or escalation lineage is invalid.', 1;

              IF EXISTS (
                SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                WHERE value.IsUrgentPath<>prior.IsUrgentPath
                   OR ISNULL(value.UrgencyReason,'')<>ISNULL(prior.UrgencyReason,'')
                   OR ISNULL(value.UrgentResponseDueAt,CONVERT(datetime2,'19000101',112))<>ISNULL(prior.UrgentResponseDueAt,CONVERT(datetime2,'19000101',112))
                   OR (prior.UrgentEscalatedAt IS NOT NULL AND (
                        ISNULL(value.UrgentEscalatedAt,CONVERT(datetime2,'19000101',112))<>prior.UrgentEscalatedAt
                        OR ISNULL(value.UrgentEscalationClientRequestId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.UrgentEscalationClientRequestId,'00000000-0000-0000-0000-000000000000')
                        OR ISNULL(value.UrgentEscalationRequestHash,'')<>ISNULL(prior.UrgentEscalationRequestHash,'')))
                   OR (prior.UrgentEscalatedAt IS NULL AND value.UrgentEscalatedAt IS NULL AND (
                        value.UrgentEscalationClientRequestId IS NOT NULL OR value.UrgentEscalationRequestHash IS NOT NULL))
                   OR (prior.UrgentEscalatedAt IS NULL AND value.UrgentEscalatedAt IS NOT NULL AND (
                        value.UrgentEscalationClientRequestId IS NULL OR value.UrgentEscalationRequestHash IS NULL OR LEN(value.UrgentEscalationRequestHash)<>64))
              ) THROW 52290, 'Civil urgent-task path and escalation lineage are immutable.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_ProjectCivilDirectTaskControls_UrgentPath;
            DROP INDEX IF EXISTS IX_ProjectCivilDirectTaskControls_TenantId_ProjectId_UrgentResponseDueAt ON ProjectCivilDirectTaskControls;
            ALTER TABLE ProjectCivilDirectTaskControls DROP CONSTRAINT IF EXISTS CK_ProjectCivilDirectTaskControls_UrgentPath;
            ALTER TABLE ProjectCivilDirectTaskControls DROP CONSTRAINT IF EXISTS DF_ProjectCivilDirectTaskControls_IsUrgentPath;
            ALTER TABLE ProjectCivilDirectTaskControls DROP COLUMN IF EXISTS IsUrgentPath, UrgencyReason, UrgentResponseDueAt, UrgentEscalatedAt, UrgentEscalationClientRequestId, UrgentEscalationRequestHash;
            """);
    }
}
