using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260907033000_AlignPettyPurchaseQuotationLifecycle")]
public sealed class AlignPettyPurchaseQuotationLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(name: "AuthorityRouteId", table: "ProcurementExceptionalSourcingControls",
            type: "uniqueidentifier", nullable: true, oldClrType: typeof(Guid), oldType: "uniqueidentifier");
        migrationBuilder.Sql(GuardSql);
    }

    // Do not invent authority, invitations or negotiations to force existing Petty records
    // back into the legacy schema. A rollback requires a reviewed data-compatible migration.
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Petty Purchase quotation records require a reviewed forward-compatible rollback; retained approvals must not be fabricated or deleted.");

    internal const string GuardSql = """
        ALTER TABLE dbo.ProcurementExceptionalSourcingControls DROP CONSTRAINT CK_ProcurementExceptionalSourcingControls_Lifecycle;
        ALTER TABLE dbo.ProcurementExceptionalSourcingControls WITH CHECK ADD CONSTRAINT CK_ProcurementExceptionalSourcingControls_Lifecycle CHECK (
          ([Status] = 0 OR ([WorkflowInstanceId] IS NOT NULL AND [SubmittedForApprovalAtUtc] IS NOT NULL AND [SubmittedForApprovalById] IS NOT NULL))
          AND ([Status] NOT IN (2,3,4,5,6,7,8) OR ([ApprovedAtUtc] IS NOT NULL AND [ApprovedById] IS NOT NULL
            AND ([Method] = 5 OR [SuppliersInvitedAtUtc] IS NOT NULL)
            AND ([PpaApprovalRequired] = 0 OR NULLIF(LTRIM(RTRIM([PpaApprovalReference])), '') IS NOT NULL)
            AND ([BoardApprovalRequired] = 0 OR NULLIF(LTRIM(RTRIM([BoardApprovalReference])), '') IS NOT NULL)
            AND ([ManagingDirectorApprovalRequired] = 0 OR NULLIF(LTRIM(RTRIM([ManagingDirectorApprovalReference])), '') IS NOT NULL)))
          AND ([Method] = 5 OR [Status] NOT IN (3,4,5,6,7,8) OR ([NegotiationId] IS NOT NULL
            AND NULLIF(LTRIM(RTRIM([NegotiationPlanReference])), '') IS NOT NULL
            AND NULLIF(LTRIM(RTRIM([NegotiationMinutesEvidenceReference])), '') IS NOT NULL
            AND NULLIF(LTRIM(RTRIM([NegotiationOutcomeReference])), '') IS NOT NULL
            AND [NegotiatedAmount] IS NOT NULL AND [NegotiatedAtUtc] IS NOT NULL))
          AND ([Status] NOT IN (4,5,6,7,8) OR ([RecommendedBidId] IS NOT NULL
            AND NULLIF(LTRIM(RTRIM([RecommendationReason])), '') IS NOT NULL
            AND NULLIF(LTRIM(RTRIM([RecommendationEvidenceReference])), '') IS NOT NULL
            AND [RecommendedAtUtc] IS NOT NULL AND [RecommendedById] IS NOT NULL))
          AND ([Status] NOT IN (5,6,7,8) OR ([AwardBidId] IS NOT NULL AND [AwardBidId] = [RecommendedBidId]
            AND NULLIF(LTRIM(RTRIM([AwardReference])), '') IS NOT NULL
            AND NULLIF(LTRIM(RTRIM([AwardEvidenceReference])), '') IS NOT NULL AND [AwardedAtUtc] IS NOT NULL))
          AND ([Status] NOT IN (6,7,8) OR (NULLIF(LTRIM(RTRIM([ContractReference])), '') IS NOT NULL
            AND NULLIF(LTRIM(RTRIM([ContractEvidenceReference])), '') IS NOT NULL AND [ContractedAtUtc] IS NOT NULL))
          AND ([Status] NOT IN (7,8) OR (NULLIF(LTRIM(RTRIM([BidderAcceptanceReference])), '') IS NOT NULL
            AND NULLIF(LTRIM(RTRIM([BidderAcceptanceEvidenceReference])), '') IS NOT NULL AND [AcceptedAtUtc] IS NOT NULL))
          AND ([Status] <> 8 OR (NULLIF(LTRIM(RTRIM([PostAwardFilingReference])), '') IS NOT NULL
            AND NULLIF(LTRIM(RTRIM([PostAwardFilingEvidenceReference])), '') IS NOT NULL
            AND NULLIF(LTRIM(RTRIM([ExceptionReportReference])), '') IS NOT NULL
            AND NULLIF(LTRIM(RTRIM([ExceptionReportEvidenceReference])), '') IS NOT NULL
            AND [FiledAtUtc] IS NOT NULL AND [FiledById] IS NOT NULL))
        );
        ALTER TABLE dbo.ProcurementExceptionalSourcingControls WITH CHECK ADD CONSTRAINT CK_PettyPurchase_Authority CHECK (
          ([Method] = 5 AND [AuthorityRouteId] IS NULL AND NULLIF(LTRIM(RTRIM([AuthorityRouteReference])), '') IS NULL)
          OR ([AuthorityRouteId] IS NOT NULL AND NULLIF(LTRIM(RTRIM([AuthorityRouteReference])), '') IS NOT NULL)
        );
        DECLARE @trigger nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_ProcurementExceptionalSourcingControls_Lifecycle'));
        IF @trigger IS NULL OR CHARINDEX(N'OR a.[Id] IS NULL', @trigger) = 0
          OR CHARINDEX(N'c.[AuthorityRouteId] <> i.[AuthorityRouteId]', @trigger) = 0
          OR CHARINDEX(N'i.[AuthorityRouteId] <> d.[AuthorityRouteId]', @trigger) = 0
          OR CHARINDEX(N'(d.[Status] = 2 AND i.[Status] = 3)', @trigger) = 0
          THROW 51124, 'Unexpected exceptional lifecycle trigger; migration stopped without weakening its controls.', 1;
        SET @trigger = STUFF(@trigger, 1, CHARINDEX(N'TRIGGER', UPPER(@trigger)) - 1, N'ALTER ');
        SET @trigger = REPLACE(@trigger, N'OR a.[Id] IS NULL', N'OR (a.[Id] IS NULL AND NOT (i.[Method] = 5 AND i.[AuthorityRouteId] IS NULL))');
        SET @trigger = REPLACE(@trigger, N'c.[AuthorityRouteId] <> i.[AuthorityRouteId]', N'ISNULL(CONVERT(nvarchar(36),c.[AuthorityRouteId]),'''') <> ISNULL(CONVERT(nvarchar(36),i.[AuthorityRouteId]),'''')');
        SET @trigger = REPLACE(@trigger, N'i.[AuthorityRouteId] <> d.[AuthorityRouteId]', N'ISNULL(CONVERT(nvarchar(36),i.[AuthorityRouteId]),'''') <> ISNULL(CONVERT(nvarchar(36),d.[AuthorityRouteId]),'''')');
        SET @trigger = REPLACE(@trigger, N'(d.[Status] = 2 AND i.[Status] = 3)', N'(d.[Status] = 2 AND (i.[Status] = 3 OR (i.[Method] = 5 AND i.[Status] = 4)))');
        EXEC sys.sp_executesql @trigger;
        """;
}
