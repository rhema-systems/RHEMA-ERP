using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260811043000_ExtendWorksContractCommercialTerms")]
public partial class ExtendWorksContractCommercialTerms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("AllowSectionalTakeover", "Contracts", "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("AllowSubcontracting", "Contracts", "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<int>("ClaimNoticePeriodDays", "Contracts", "int", nullable: true);
        migrationBuilder.AddColumn<string>("ClaimClause", "Contracts", "nvarchar(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<Guid>("CommercialTermsClientRequestId", "Contracts", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<DateTime>("CommercialTermsConfiguredAt", "Contracts", "datetime2", nullable: true);
        migrationBuilder.AddColumn<Guid>("CommercialTermsConfiguredById", "Contracts", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("CommercialTermsConfigurationProfileId", "Contracts", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("CommercialTermsContractDocumentId", "Contracts", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>("CommercialTermsPolicyHash", "Contracts", "nvarchar(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<string>("CommercialTermsRequestHash", "Contracts", "nvarchar(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<decimal>("ContingencyAmount", "Contracts", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<Guid>("ContractControlsDecisionId", "Contracts", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<int>("DefectsLiabilityDays", "Contracts", "int", nullable: true);
        migrationBuilder.AddColumn<Guid>("PaymentTermId", "Contracts", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<decimal>("ProvisionalSumAmount", "Contracts", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<Guid>("RetentionDecisionId", "Contracts", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>("RetentionClause", "Contracts", "nvarchar(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<string>("SectionalTakeoverClause", "Contracts", "nvarchar(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<Guid>("SubcontractPaymentTermId", "Contracts", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>("SubcontractTerms", "Contracts", "nvarchar(2000)", maxLength: 2000, nullable: true);

        migrationBuilder.AddCheckConstraint("CK_Contracts_QS0520_Amounts", "Contracts",
            "[ProvisionalSumAmount] >= 0 AND [ContingencyAmount] >= 0 AND [ProvisionalSumAmount] + [ContingencyAmount] <= [ContractValue] AND [RetentionPercentage] >= 0 AND [RetentionPercentage] <= 100 AND ([DefectsLiabilityDays] IS NULL OR [DefectsLiabilityDays] BETWEEN 0 AND 3650) AND ([ClaimNoticePeriodDays] IS NULL OR [ClaimNoticePeriodDays] BETWEEN 0 AND 3650)");
        migrationBuilder.AddCheckConstraint("CK_Contracts_QS0520_Lineage", "Contracts",
            "([CommercialTermsPolicyHash] IS NULL AND [CommercialTermsConfigurationProfileId] IS NULL AND [ContractControlsDecisionId] IS NULL AND [RetentionDecisionId] IS NULL AND [CommercialTermsClientRequestId] IS NULL AND [CommercialTermsRequestHash] IS NULL AND [CommercialTermsConfiguredAt] IS NULL AND [CommercialTermsConfiguredById] IS NULL) OR ([CommercialTermsPolicyHash] IS NOT NULL AND LEN([CommercialTermsPolicyHash]) = 64 AND [CommercialTermsConfigurationProfileId] IS NOT NULL AND [ContractControlsDecisionId] IS NOT NULL AND [RetentionDecisionId] IS NOT NULL AND [CommercialTermsClientRequestId] IS NOT NULL AND LEN([CommercialTermsRequestHash]) = 64 AND [CommercialTermsConfiguredAt] IS NOT NULL AND [CommercialTermsConfiguredById] IS NOT NULL AND [PaymentTermId] IS NOT NULL)");
        migrationBuilder.AddCheckConstraint("CK_Contracts_QS0520_Clauses", "Contracts",
            "([RetentionPercentage] = 0 OR NULLIF(LTRIM(RTRIM([RetentionClause])), '') IS NOT NULL) AND ([AllowSectionalTakeover] = 0 OR NULLIF(LTRIM(RTRIM([SectionalTakeoverClause])), '') IS NOT NULL) AND ([AllowSubcontracting] = 0 OR ([SubcontractPaymentTermId] IS NOT NULL AND NULLIF(LTRIM(RTRIM([SubcontractTerms])), '') IS NOT NULL))");

        migrationBuilder.CreateIndex("IX_Contracts_PaymentTermId", "Contracts", "PaymentTermId");
        migrationBuilder.CreateIndex("IX_Contracts_SubcontractPaymentTermId", "Contracts", "SubcontractPaymentTermId");
        migrationBuilder.CreateIndex("IX_Contracts_CommercialTermsContractDocumentId", "Contracts", "CommercialTermsContractDocumentId");
        migrationBuilder.CreateIndex("IX_Contracts_CommercialTermsConfigurationProfileId", "Contracts", "CommercialTermsConfigurationProfileId");
        migrationBuilder.CreateIndex("IX_Contracts_ContractControlsDecisionId", "Contracts", "ContractControlsDecisionId");
        migrationBuilder.CreateIndex("IX_Contracts_RetentionDecisionId", "Contracts", "RetentionDecisionId");
        migrationBuilder.CreateIndex("IX_Contracts_CommercialTermsConfiguredById", "Contracts", "CommercialTermsConfiguredById");
        migrationBuilder.CreateIndex("IX_Contracts_TenantId_CommercialTermsClientRequestId", "Contracts",
            new[] { "TenantId", "CommercialTermsClientRequestId" }, unique: true,
            filter: "[CommercialTermsClientRequestId] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.AddForeignKey("FK_Contracts_PaymentTerms_PaymentTermId", "Contracts", "PaymentTermId", "PaymentTerms", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_Contracts_PaymentTerms_SubcontractPaymentTermId", "Contracts", "SubcontractPaymentTermId", "PaymentTerms", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_Contracts_ContractDocuments_CommercialTermsContractDocumentId", "Contracts", "CommercialTermsContractDocumentId", "ContractDocuments", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_Contracts_QuantitySurveyConfigurationProfiles_CommercialTermsConfigurationProfileId", "Contracts", "CommercialTermsConfigurationProfileId", "QuantitySurveyConfigurationProfiles", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_Contracts_QuantitySurveyConfigurationDecisions_ContractControlsDecisionId", "Contracts", "ContractControlsDecisionId", "QuantitySurveyConfigurationDecisions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_Contracts_QuantitySurveyConfigurationDecisions_RetentionDecisionId", "Contracts", "RetentionDecisionId", "QuantitySurveyConfigurationDecisions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_Contracts_Users_CommercialTermsConfiguredById", "Contracts", "CommercialTermsConfiguredById", "Users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [dbo].[TR_Contracts_QS0520CommercialTerms]
            ON [dbo].[Contracts]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE d.Status <> 'Draft' AND (
                        i.ContractValue <> d.ContractValue OR ISNULL(i.PaymentTerms, '') <> ISNULL(d.PaymentTerms, '') OR
                        ISNULL(i.PaymentTermId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.PaymentTermId, '00000000-0000-0000-0000-000000000000') OR
                        i.ProvisionalSumAmount <> d.ProvisionalSumAmount OR i.ContingencyAmount <> d.ContingencyAmount OR
                        i.RetentionPercentage <> d.RetentionPercentage OR ISNULL(i.DefectsLiabilityDays, -1) <> ISNULL(d.DefectsLiabilityDays, -1) OR ISNULL(i.WarrantyPeriodDays, -1) <> ISNULL(d.WarrantyPeriodDays, -1) OR
                        ISNULL(i.RetentionClause, '') <> ISNULL(d.RetentionClause, '') OR i.AllowSectionalTakeover <> d.AllowSectionalTakeover OR
                        ISNULL(i.SectionalTakeoverClause, '') <> ISNULL(d.SectionalTakeoverClause, '') OR i.AllowSubcontracting <> d.AllowSubcontracting OR
                        ISNULL(i.SubcontractPaymentTermId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.SubcontractPaymentTermId, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(i.SubcontractTerms, '') <> ISNULL(d.SubcontractTerms, '') OR ISNULL(i.ClaimNoticePeriodDays, -1) <> ISNULL(d.ClaimNoticePeriodDays, -1) OR
                        ISNULL(i.ClaimClause, '') <> ISNULL(d.ClaimClause, '') OR
                        ISNULL(i.CommercialTermsContractDocumentId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.CommercialTermsContractDocumentId, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(i.CommercialTermsPolicyHash, '') <> ISNULL(d.CommercialTermsPolicyHash, '')))
                    THROW 51941, 'Works-contract commercial terms are immutable after Draft status.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE i.ContractType <> d.ContractType AND
                          (d.ContractType = 'Works' OR i.ContractType = 'Works'))
                    THROW 51943, 'A contract cannot be reclassified to or from Works to bypass QS controls.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE (d.CommercialTermsPolicyHash IS NOT NULL OR i.CommercialTermsPolicyHash IS NOT NULL)
                      AND (
                        i.ContractValue <> d.ContractValue OR ISNULL(i.PaymentTerms, '') <> ISNULL(d.PaymentTerms, '') OR
                        ISNULL(i.PaymentTermId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.PaymentTermId, '00000000-0000-0000-0000-000000000000') OR
                        i.ProvisionalSumAmount <> d.ProvisionalSumAmount OR i.ContingencyAmount <> d.ContingencyAmount OR
                        i.RetentionPercentage <> d.RetentionPercentage OR ISNULL(i.DefectsLiabilityDays, -1) <> ISNULL(d.DefectsLiabilityDays, -1) OR ISNULL(i.WarrantyPeriodDays, -1) <> ISNULL(d.WarrantyPeriodDays, -1) OR
                        ISNULL(i.RetentionClause, '') <> ISNULL(d.RetentionClause, '') OR i.AllowSectionalTakeover <> d.AllowSectionalTakeover OR
                        ISNULL(i.SectionalTakeoverClause, '') <> ISNULL(d.SectionalTakeoverClause, '') OR i.AllowSubcontracting <> d.AllowSubcontracting OR
                        ISNULL(i.SubcontractPaymentTermId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.SubcontractPaymentTermId, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(i.SubcontractTerms, '') <> ISNULL(d.SubcontractTerms, '') OR ISNULL(i.ClaimNoticePeriodDays, -1) <> ISNULL(d.ClaimNoticePeriodDays, -1) OR
                        ISNULL(i.ClaimClause, '') <> ISNULL(d.ClaimClause, '') OR
                        ISNULL(i.CommercialTermsContractDocumentId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.CommercialTermsContractDocumentId, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(i.CommercialTermsConfigurationProfileId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.CommercialTermsConfigurationProfileId, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(i.ContractControlsDecisionId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ContractControlsDecisionId, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(i.RetentionDecisionId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.RetentionDecisionId, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(i.CommercialTermsClientRequestId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.CommercialTermsClientRequestId, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(i.CommercialTermsRequestHash, '') <> ISNULL(d.CommercialTermsRequestHash, '') OR
                        ISNULL(i.CommercialTermsPolicyHash, '') <> ISNULL(d.CommercialTermsPolicyHash, '') OR
                        ISNULL(i.CommercialTermsConfiguredAt, '19000101') <> ISNULL(d.CommercialTermsConfiguredAt, '19000101') OR
                        ISNULL(i.CommercialTermsConfiguredById, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.CommercialTermsConfiguredById, '00000000-0000-0000-0000-000000000000'))
                      AND (
                        ISNULL(TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'qs_contract_terms_id')), '00000000-0000-0000-0000-000000000000') <> i.Id OR
                        ISNULL(TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'qs_contract_terms_actor')), '00000000-0000-0000-0000-000000000000') <> ISNULL(i.CommercialTermsConfiguredById, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(CONVERT(nvarchar(64), SESSION_CONTEXT(N'qs_contract_terms_hash')), '') <> ISNULL(i.CommercialTermsRequestHash, '')))
                    THROW 51944, 'Works-contract commercial terms must be changed through the governed QS service.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN dbo.PaymentTerms pt ON pt.Id = i.PaymentTermId AND pt.TenantId = i.TenantId AND pt.IsDeleted = 0 AND pt.IsActive = 1 AND pt.ApplicableTo IN ('All','Supplier','Contractor')
                    LEFT JOIN dbo.PaymentTerms spt ON spt.Id = i.SubcontractPaymentTermId AND spt.TenantId = i.TenantId AND spt.IsDeleted = 0 AND spt.IsActive = 1 AND spt.ApplicableTo IN ('All','Supplier','Contractor')
                    LEFT JOIN dbo.QuantitySurveyConfigurationProfiles p ON p.Id = i.CommercialTermsConfigurationProfileId AND p.TenantId = i.TenantId AND p.IsDeleted = 0 AND p.LifecycleStatus = 1 AND p.PublishedAt IS NOT NULL
                    LEFT JOIN dbo.QuantitySurveyConfigurationDecisions r ON r.Id = i.RetentionDecisionId AND r.TenantId = i.TenantId AND r.ProfileId = p.Id AND r.DecisionKey = 'QS-DEC-009' AND r.Status = 2 AND r.ApprovalStatus = 1 AND r.EvidenceStatus = 2 AND r.IsDeleted = 0
                    LEFT JOIN dbo.QuantitySurveyConfigurationDecisions c ON c.Id = i.ContractControlsDecisionId AND c.TenantId = i.TenantId AND c.ProfileId = p.Id AND c.DecisionKey = 'QS-DEC-012' AND c.Status = 2 AND c.ApprovalStatus = 1 AND c.EvidenceStatus = 2 AND c.IsDeleted = 0
                    LEFT JOIN dbo.Users u ON u.Id = i.CommercialTermsConfiguredById AND u.TenantId = i.TenantId AND u.IsActive = 1
                    LEFT JOIN dbo.ContractDocuments cd ON cd.Id = i.CommercialTermsContractDocumentId AND cd.TenantId = i.TenantId AND cd.ContractId = i.Id AND cd.IsDeleted = 0
                    LEFT JOIN dbo.FileUploadRecords fu ON fu.Id = cd.FileUploadRecordId AND fu.TenantId = i.TenantId AND fu.IsDeleted = 0 AND fu.VirusScanStatus = 2
                    LEFT JOIN dbo.CentralDocumentRecords cr ON cr.Id = cd.CentralDocumentRecordId AND cr.TenantId = i.TenantId AND cr.IsDeleted = 0 AND cr.LifecycleStatus = 'Active'
                    LEFT JOIN dbo.CentralDocumentVersions cv ON cv.Id = cd.CentralDocumentVersionId AND cv.TenantId = i.TenantId AND cv.DocumentRecordId = cr.Id AND cv.FileUploadRecordId = fu.Id AND cv.IsDeleted = 0 AND cv.Status = 'Published' AND cv.VersionNumber = cr.CurrentVersion
                    WHERE i.ContractType = 'Works' AND i.CommercialTermsPolicyHash IS NOT NULL AND (
                        pt.Id IS NULL OR p.Id IS NULL OR r.Id IS NULL OR c.Id IS NULL OR u.Id IS NULL OR
                        i.RetentionPercentage > TRY_CONVERT(decimal(5,2), JSON_VALUE(r.ValueJson, '$.maximumRetentionPercent')) OR
                        (TRY_CONVERT(bit, JSON_VALUE(c.ValueJson, '$.controlProvisionalSums')) = 0 AND i.ProvisionalSumAmount <> 0) OR
                        (TRY_CONVERT(bit, JSON_VALUE(c.ValueJson, '$.controlContingencies')) = 0 AND i.ContingencyAmount <> 0) OR
                        (TRY_CONVERT(bit, JSON_VALUE(c.ValueJson, '$.controlDefectsLiability')) = 1 AND (i.DefectsLiabilityDays IS NULL OR i.DefectsLiabilityDays <= 0 OR i.DefectsLiabilityDays > TRY_CONVERT(int, JSON_VALUE(r.ValueJson, '$.defectsLiabilityDays')))) OR
                        (i.AllowSectionalTakeover = 1 AND (TRY_CONVERT(bit, JSON_VALUE(c.ValueJson, '$.controlSectionalTakeover')) <> 1 OR TRY_CONVERT(decimal(5,2), JSON_VALUE(r.ValueJson, '$.sectionalTakeoverReleasePercent')) <= 0)) OR
                        (i.AllowSubcontracting = 1 AND (TRY_CONVERT(bit, JSON_VALUE(c.ValueJson, '$.controlSubcontracts')) <> 1 OR spt.Id IS NULL)) OR
                        (TRY_CONVERT(bit, JSON_VALUE(c.ValueJson, '$.controlClaimClauses')) = 1 AND (i.ClaimNoticePeriodDays IS NULL OR i.ClaimNoticePeriodDays <= 0 OR NULLIF(LTRIM(RTRIM(i.ClaimClause)), '') IS NULL)) OR
                        (COALESCE(TRY_CONVERT(bit, JSON_VALUE(c.ValueJson, '$.requireCommercialTermsDocument')), 1) = 1 AND (cd.Id IS NULL OR fu.Id IS NULL OR cr.Id IS NULL OR cv.Id IS NULL)) OR
                        (i.CommercialTermsContractDocumentId IS NOT NULL AND (cd.Id IS NULL OR fu.Id IS NULL OR cr.Id IS NULL OR cv.Id IS NULL))))
                    THROW 51942, 'Works-contract commercial terms violate tenant, policy, controlled-master, or central-DMS governance.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_Contracts_QS0520CommercialTerms];");
        migrationBuilder.DropCheckConstraint("CK_Contracts_QS0520_Amounts", "Contracts");
        migrationBuilder.DropCheckConstraint("CK_Contracts_QS0520_Lineage", "Contracts");
        migrationBuilder.DropCheckConstraint("CK_Contracts_QS0520_Clauses", "Contracts");
        migrationBuilder.DropForeignKey("FK_Contracts_PaymentTerms_PaymentTermId", "Contracts");
        migrationBuilder.DropForeignKey("FK_Contracts_PaymentTerms_SubcontractPaymentTermId", "Contracts");
        migrationBuilder.DropForeignKey("FK_Contracts_ContractDocuments_CommercialTermsContractDocumentId", "Contracts");
        migrationBuilder.DropForeignKey("FK_Contracts_QuantitySurveyConfigurationProfiles_CommercialTermsConfigurationProfileId", "Contracts");
        migrationBuilder.DropForeignKey("FK_Contracts_QuantitySurveyConfigurationDecisions_ContractControlsDecisionId", "Contracts");
        migrationBuilder.DropForeignKey("FK_Contracts_QuantitySurveyConfigurationDecisions_RetentionDecisionId", "Contracts");
        migrationBuilder.DropForeignKey("FK_Contracts_Users_CommercialTermsConfiguredById", "Contracts");
        foreach (var name in new[] { "IX_Contracts_PaymentTermId", "IX_Contracts_SubcontractPaymentTermId", "IX_Contracts_CommercialTermsContractDocumentId", "IX_Contracts_CommercialTermsConfigurationProfileId", "IX_Contracts_ContractControlsDecisionId", "IX_Contracts_RetentionDecisionId", "IX_Contracts_CommercialTermsConfiguredById", "IX_Contracts_TenantId_CommercialTermsClientRequestId" })
            migrationBuilder.DropIndex(name, "Contracts");
        foreach (var name in new[] { "AllowSectionalTakeover", "AllowSubcontracting", "ClaimNoticePeriodDays", "ClaimClause", "CommercialTermsClientRequestId", "CommercialTermsConfiguredAt", "CommercialTermsConfiguredById", "CommercialTermsConfigurationProfileId", "CommercialTermsContractDocumentId", "CommercialTermsPolicyHash", "CommercialTermsRequestHash", "ContingencyAmount", "ContractControlsDecisionId", "DefectsLiabilityDays", "PaymentTermId", "ProvisionalSumAmount", "RetentionDecisionId", "RetentionClause", "SectionalTakeoverClause", "SubcontractPaymentTermId", "SubcontractTerms" })
            migrationBuilder.DropColumn(name, "Contracts");
    }
}
