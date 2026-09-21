using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260920121000_AllowInternalQuantitySurveyValuations")]
public sealed class AllowInternalQuantitySurveyValuations : Migration
{
    // Preserve every other trigger check, including frozen lineage, evidence, workflow and independent approval.
    public const string ReconciliationSql = """
        DECLARE @policy nvarchar(max) = (SELECT definition FROM sys.check_constraints
            WHERE parent_object_id = OBJECT_ID(N'dbo.QuantitySurveyValuationWorksheets') AND name = N'CK_QsValuationWorksheets_Policy');
        IF @policy IS NULL
            THROW 52035, 'The existing QS valuation policy constraint is required before alignment.', 1;
        IF CHARINDEX(N'ContractorSubmissionRequired', @policy) = 0
        BEGIN
            ALTER TABLE dbo.QuantitySurveyValuationWorksheets DROP CONSTRAINT CK_QsValuationWorksheets_Policy;
            ALTER TABLE dbo.QuantitySurveyValuationWorksheets WITH CHECK ADD CONSTRAINT CK_QsValuationWorksheets_Policy CHECK (
                ([ConfigurationProfileId] IS NULL AND [ValuationDecisionId] IS NULL AND [ExternalSubmissionDecisionId] IS NULL AND [ApprovalWorkflowDefinitionId] IS NULL AND [EvidenceMetadataTemplateId] IS NULL AND [PolicyHash] IS NULL)
                OR ([ConfigurationProfileId] IS NOT NULL AND [ValuationDecisionId] IS NOT NULL AND (([ContractorSubmissionRequired] = 0 AND [ConsultantEndorsementRequired] = 0) OR [ExternalSubmissionDecisionId] IS NOT NULL) AND [ApprovalWorkflowDefinitionId] IS NOT NULL AND [EvidenceMetadataTemplateId] IS NOT NULL AND LEN([PolicyHash]) = 64));
        END;
        DECLARE @guard nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_QsValuationWorksheets_Guard'));
        IF @guard IS NULL
            THROW 52033, 'The existing QS valuation guard is required before internal valuation alignment.', 1;
        DECLARE @previous nvarchar(200) = N'i.ExternalSubmissionDecisionId IS NULL OR';
        DECLARE @replacement nvarchar(300) = N'((i.ContractorSubmissionRequired=1 OR i.ConsultantEndorsementRequired=1) AND i.ExternalSubmissionDecisionId IS NULL) OR';
        DECLARE @previousLineage nvarchar(200) = N'OR ed.Id IS NULL OR mt.Id IS NULL';
        DECLARE @replacementLineage nvarchar(300) = N'OR (i.ExternalSubmissionDecisionId IS NOT NULL AND ed.Id IS NULL) OR mt.Id IS NULL';
        IF CHARINDEX(@replacement, @guard) = 0 OR CHARINDEX(@replacementLineage, @guard) = 0
        BEGIN
            IF (CHARINDEX(@previous, @guard) = 0 AND CHARINDEX(@replacement, @guard) = 0)
                OR (CHARINDEX(@previousLineage, @guard) = 0 AND CHARINDEX(@replacementLineage, @guard) = 0)
                THROW 52034, 'The QS valuation guard differs from the expected version; review before applying.', 1;
            SET @guard = REPLACE(@guard, @previous, @replacement);
            SET @guard = REPLACE(@guard, @previousLineage, @replacementLineage);
            SET @guard = REPLACE(@guard, N'CREATE OR ALTER TRIGGER', N'ALTER TRIGGER');
            SET @guard = REPLACE(@guard, N'CREATE TRIGGER', N'ALTER TRIGGER');
            SET @guard = REPLACE(@guard, N'CREATE   TRIGGER', N'ALTER TRIGGER');
            EXEC sys.sp_executesql @guard;
        END;
        """;

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(ReconciliationSql);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Internal valuations may already have progressed. Keep the compatible guard on rollback.
    }
}
