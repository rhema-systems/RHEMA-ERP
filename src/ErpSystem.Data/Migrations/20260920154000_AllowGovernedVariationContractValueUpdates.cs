using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260920154000_AllowGovernedVariationContractValueUpdates")]
public sealed class AllowGovernedVariationContractValueUpdates : Migration
{
    public const string ReconciliationSql = """
        DECLARE @guard nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_Contracts_QS0520CommercialTerms'));
        IF @guard IS NULL THROW 52038, 'The existing Works commercial-terms guard is required.', 1;
        IF CHARINDEX(N'QS_APPROVED_VARIATION_VALUE', @guard) = 0
        BEGIN
            DECLARE @value nvarchar(max) = N'i.ContractValue <> d.ContractValue';
            DECLARE @replacement nvarchar(max) = N'(i.ContractValue <> d.ContractValue AND NOT EXISTS (
                -- QS_APPROVED_VARIATION_VALUE: exact completed workflow and service capability only.
                SELECT 1 FROM dbo.ProjectVariationOrders v
                JOIN dbo.WorkflowInstances w ON w.Id=v.WorkflowInstanceId AND w.TenantId=v.TenantId
                    AND w.WorkflowDefinitionId=v.ApprovalWorkflowDefinitionId AND w.Status=2 AND w.IsDeleted=0
                WHERE v.Id=TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N''qs_variation_application_id''))
                    AND v.TenantId=i.TenantId AND v.ContractId=i.Id AND v.IsDeleted=0 AND v.IsQuantitySurveyGoverned=1
                    AND v.Status IN (''PendingApproval'',''Approved'')
                    AND (v.DownstreamApplicationStatus=''NotApplied'' OR (v.DownstreamApplicationStatus=''AppliedPendingBoqApproval''
                        AND v.ApplicationHash=CONVERT(nvarchar(64), SESSION_CONTEXT(N''qs_variation_application_hash''))
                        AND v.AppliedById=i.LastModifiedById AND v.ApprovedAmount=v.EstimatedAmount))
                    AND v.UpdateContractSumOnApplication=1 AND v.ContractorBusinessPartnerId=i.BusinessPartnerId
                    AND v.Currency=i.Currency AND v.OriginalContractSumSnapshot=d.ContractValue
                    AND v.EstimatedAmount IS NOT NULL AND i.ContractValue=ROUND(d.ContractValue+v.EstimatedAmount,2)
                    AND i.ContractType=''Works'' AND d.ContractType=''Works'' AND i.Status=''Active'' AND d.Status=''Active''
                    AND TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N''qs_variation_application_actor''))=i.LastModifiedById
                    AND i.LastModifiedById<>v.PreparedById AND i.LastModifiedById<>v.SubmittedById
                    AND LEN(CONVERT(nvarchar(64), SESSION_CONTEXT(N''qs_variation_application_hash'')))=64))';
            IF (LEN(@guard)-LEN(REPLACE(@guard,@value,N'')))/LEN(@value) <> 2
                THROW 52039, 'Unexpected Works commercial-terms guard; review before alignment.', 1;
            SET @guard=REPLACE(@guard,@value,@replacement);
            DECLARE @profile nvarchar(max)=N'p.LifecycleStatus = 1 AND p.PublishedAt IS NOT NULL';
            IF CHARINDEX(@profile,@guard)=0 THROW 52039, 'Expected frozen-profile guard was not found.', 1;
            SET @guard=REPLACE(@guard,@profile,N'p.PublishedAt IS NOT NULL AND (p.LifecycleStatus=1 OR (p.LifecycleStatus=2 AND EXISTS (
                SELECT 1 FROM deleted prior WHERE prior.Id=i.Id AND prior.TenantId=i.TenantId
                  AND prior.CommercialTermsConfigurationProfileId=i.CommercialTermsConfigurationProfileId
                  AND prior.RetentionDecisionId=i.RetentionDecisionId AND prior.ContractControlsDecisionId=i.ContractControlsDecisionId
                  AND prior.CommercialTermsPolicyHash=i.CommercialTermsPolicyHash)))');
            SET @guard=REPLACE(@guard,N'CREATE OR ALTER TRIGGER',N'ALTER TRIGGER');
            SET @guard=REPLACE(@guard,N'CREATE TRIGGER',N'ALTER TRIGGER');
            SET @guard=REPLACE(@guard,N'CREATE   TRIGGER',N'ALTER TRIGGER');
            EXEC sys.sp_executesql @guard;
        END;
        """;

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(ReconciliationSql);
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve compatibility with already approved, audited variation applications.
    }
}
