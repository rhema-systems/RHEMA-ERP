using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Projects;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data;

public partial class ApplicationDbContext
{
    public DbSet<QuantitySurveyConfigurationProfile> QuantitySurveyConfigurationProfiles { get; set; }
    public DbSet<QuantitySurveyConfigurationDecision> QuantitySurveyConfigurationDecisions { get; set; }
    public DbSet<QuantitySurveyConfigurationEvidenceLink> QuantitySurveyConfigurationEvidenceLinks { get; set; }
    public DbSet<QuantitySurveyConfigurationRevision> QuantitySurveyConfigurationRevisions { get; set; }
    public DbSet<QuantitySurveyBoqImportSession> QuantitySurveyBoqImportSessions { get; set; }
    public DbSet<QuantitySurveyTenderBoqSubmission> QuantitySurveyTenderBoqSubmissions { get; set; }
    public DbSet<QuantitySurveyTenderBoqSubmissionLine> QuantitySurveyTenderBoqSubmissionLines { get; set; }
    public DbSet<QuantitySurveyRateLibraryItem> QuantitySurveyRateLibraryItems { get; set; }
    public DbSet<QuantitySurveyRateLibraryRate> QuantitySurveyRateLibraryRates { get; set; }
    public DbSet<QuantitySurveyRateLibraryRevision> QuantitySurveyRateLibraryRevisions { get; set; }
    public DbSet<QuantitySurveyRateBuildUp> QuantitySurveyRateBuildUps { get; set; }
    public DbSet<QuantitySurveyRateBuildUpLine> QuantitySurveyRateBuildUpLines { get; set; }
    public DbSet<QuantitySurveyEstimateVersion> QuantitySurveyEstimateVersions { get; set; }
    public DbSet<QuantitySurveyEstimateLine> QuantitySurveyEstimateLines { get; set; }
    public DbSet<QuantitySurveyEstimateAssumption> QuantitySurveyEstimateAssumptions { get; set; }
    public DbSet<QuantitySurveyEstimateMarkup> QuantitySurveyEstimateMarkups { get; set; }
    public DbSet<QuantitySurveyEstimateRevision> QuantitySurveyEstimateRevisions { get; set; }
    public DbSet<QuantitySurveyPriceIndexFamily> QuantitySurveyPriceIndexFamilies { get; set; }
    public DbSet<QuantitySurveyEscalationFormulaDefinition> QuantitySurveyEscalationFormulas { get; set; }
    public DbSet<QuantitySurveyEscalationFormulaComponent> QuantitySurveyEscalationFormulaComponents { get; set; }
    public DbSet<QuantitySurveyEscalationFormulaRevision> QuantitySurveyEscalationFormulaRevisions { get; set; }
    public DbSet<QuantitySurveyPriceIndexImportBatch> QuantitySurveyPriceIndexImportBatches { get; set; }
    public DbSet<QuantitySurveyPriceIndexValue> QuantitySurveyPriceIndexValues { get; set; }
    public DbSet<QuantitySurveyPriceIndexImportRevision> QuantitySurveyPriceIndexImportRevisions { get; set; }
    public DbSet<QuantitySurveyEscalationCalculationRun> QuantitySurveyEscalationCalculationRuns { get; set; }
    public DbSet<QuantitySurveyEscalationCalculationLine> QuantitySurveyEscalationCalculationLines { get; set; }
    public DbSet<QuantitySurveyEscalationCalculationRevision> QuantitySurveyEscalationCalculationRevisions { get; set; }
    public DbSet<QuantitySurveyEscalationDispute> QuantitySurveyEscalationDisputes { get; set; }
    public DbSet<QuantitySurveyEscalationDisputeAttachment> QuantitySurveyEscalationDisputeAttachments { get; set; }
    public DbSet<QuantitySurveyEscalationDisputeRevision> QuantitySurveyEscalationDisputeRevisions { get; set; }
    public DbSet<QuantitySurveyMeasurementSheet> QuantitySurveyMeasurementSheets { get; set; }
    public DbSet<QuantitySurveyMeasurementLine> QuantitySurveyMeasurementLines { get; set; }
    public DbSet<QuantitySurveyMeasurementAttachment> QuantitySurveyMeasurementAttachments { get; set; }
    public DbSet<QuantitySurveyMeasurementRevision> QuantitySurveyMeasurementRevisions { get; set; }
    public DbSet<QuantitySurveyJointMeasurementRequest> QuantitySurveyJointMeasurementRequests { get; set; }
    public DbSet<QuantitySurveyJointMeasurementParticipant> QuantitySurveyJointMeasurementParticipants { get; set; }
    public DbSet<QuantitySurveyJointMeasurementEndorsement> QuantitySurveyJointMeasurementEndorsements { get; set; }
    public DbSet<QuantitySurveyJointMeasurementEvidence> QuantitySurveyJointMeasurementEvidence { get; set; }
    public DbSet<QuantitySurveyJointMeasurementRevision> QuantitySurveyJointMeasurementRevisions { get; set; }
    public DbSet<QuantitySurveyDesignRevisionImpact> QuantitySurveyDesignRevisionImpacts { get; set; }
    public DbSet<QuantitySurveyDesignRevisionImpactLine> QuantitySurveyDesignRevisionImpactLines { get; set; }
    public DbSet<QuantitySurveyDesignRevisionImpactRevision> QuantitySurveyDesignRevisionImpactRevisions { get; set; }
    public DbSet<QuantitySurveyValuationWorksheet> QuantitySurveyValuationWorksheets { get; set; }
    public DbSet<QuantitySurveyValuationWorksheetLine> QuantitySurveyValuationWorksheetLines { get; set; }
    public DbSet<QuantitySurveyValuationWorksheetRevision> QuantitySurveyValuationWorksheetRevisions { get; set; }
    public DbSet<QuantitySurveyValuationWorksheetEvidence> QuantitySurveyValuationWorksheetEvidence { get; set; }
    public DbSet<QuantitySurveyPaymentCertificateRevision> QuantitySurveyPaymentCertificateRevisions { get; set; }
    public DbSet<QuantitySurveyAdvanceRecoveryAgreement> QuantitySurveyAdvanceRecoveryAgreements { get; set; }
    public DbSet<QuantitySurveyAdvanceRecoveryRevision> QuantitySurveyAdvanceRecoveryRevisions { get; set; }
    public DbSet<QuantitySurveyMaterialReconciliation> QuantitySurveyMaterialReconciliations { get; set; }
    public DbSet<QuantitySurveyMaterialReconciliationLine> QuantitySurveyMaterialReconciliationLines { get; set; }
    public DbSet<QuantitySurveyMaterialReconciliationRevision> QuantitySurveyMaterialReconciliationRevisions { get; set; }
    public DbSet<QuantitySurveyVariationValuationLine> QuantitySurveyVariationValuationLines { get; set; }
    public DbSet<QuantitySurveyVariationEvidence> QuantitySurveyVariationEvidence { get; set; }
    public DbSet<QuantitySurveyVariationRevision> QuantitySurveyVariationRevisions { get; set; }
    public DbSet<QuantitySurveyContractClaim> QuantitySurveyContractClaims { get; set; }
    public DbSet<QuantitySurveyContractClaimEvidence> QuantitySurveyContractClaimEvidence { get; set; }
    public DbSet<QuantitySurveyContractClaimRevision> QuantitySurveyContractClaimRevisions { get; set; }
    public DbSet<QuantitySurveyDayworkSheet> QuantitySurveyDayworkSheets { get; set; }
    public DbSet<QuantitySurveyDayworkLine> QuantitySurveyDayworkLines { get; set; }
    public DbSet<QuantitySurveyDayworkEvidence> QuantitySurveyDayworkEvidence { get; set; }
    public DbSet<QuantitySurveyDayworkRevision> QuantitySurveyDayworkRevisions { get; set; }
    public DbSet<QuantitySurveySubcontract> QuantitySurveySubcontracts { get; set; }
    public DbSet<QuantitySurveySubcontractValuation> QuantitySurveySubcontractValuations { get; set; }
    public DbSet<QuantitySurveySubcontractEvidence> QuantitySurveySubcontractEvidence { get; set; }
    public DbSet<QuantitySurveySubcontractRevision> QuantitySurveySubcontractRevisions { get; set; }
    public DbSet<QuantitySurveySubcontractChargeNotice> QuantitySurveySubcontractChargeNotices { get; set; }
    public DbSet<QuantitySurveySubcontractChargeEvidence> QuantitySurveySubcontractChargeEvidence { get; set; }
    public DbSet<QuantitySurveySubcontractChargeRevision> QuantitySurveySubcontractChargeRevisions { get; set; }
    public DbSet<ProjectBoqVersion> ProjectBoqVersions { get; set; }
    public DbSet<ProjectBoqVersionLine> ProjectBoqVersionLines { get; set; }
    public DbSet<ProjectBoqRemeasurementRevision> ProjectBoqRemeasurementRevisions { get; set; }
    public DbSet<ProjectBoqRemeasurementLine> ProjectBoqRemeasurementLines { get; set; }
    public DbSet<ProjectBoqRemeasurementSource> ProjectBoqRemeasurementSources { get; set; }

    private static void ConfigureQuantitySurveyRateDecimalPrecision(ModelBuilder builder)
    {
        // QS-DEC-004 permits up to six decimal places. The legacy global precision
        // convention runs after entity attributes, so reapply only the governed
        // rate and immutable calculation-snapshot fields here.
        builder.Entity<QuantitySurveyRateLibraryRate>(entity =>
        {
            entity.Property(value => value.UnitRate).HasColumnType("decimal(18,6)");
            entity.Property(value => value.PreviousUnitRate).HasColumnType("decimal(18,6)");
        });
        builder.Entity<QuantitySurveyRateBuildUp>(entity =>
        {
            entity.Property(value => value.MaterialSubtotal).HasColumnType("decimal(18,6)");
            entity.Property(value => value.DirectCost).HasColumnType("decimal(18,6)");
            entity.Property(value => value.AddOnCost).HasColumnType("decimal(18,6)");
            entity.Property(value => value.UnitRate).HasColumnType("decimal(18,6)");
        });
        builder.Entity<QuantitySurveyRateBuildUpLine>(entity =>
        {
            entity.Property(value => value.SourceUnitRate).HasColumnType("decimal(18,6)");
            entity.Property(value => value.InputQuantity).HasColumnType("decimal(18,6)");
            entity.Property(value => value.InputPercentage).HasColumnType("decimal(9,4)");
            entity.Property(value => value.InputFixedAmount).HasColumnType("decimal(18,6)");
            entity.Property(value => value.BasisAmount).HasColumnType("decimal(18,6)");
            entity.Property(value => value.CalculatedAmount).HasColumnType("decimal(18,6)");
        });
        builder.Entity<QuantitySurveyEstimateVersion>(entity =>
        {
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.Property(value => value.CurrencyCodeSnapshot).IsUnicode(false);
            entity.Property(value => value.SnapshotHash).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ProjectId, value.EstimateType, value.VersionNumber }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ProjectId, value.EstimateType, value.Status });
            entity.HasIndex(value => new { value.TenantId, value.ProjectId, value.EstimateType })
                .IsUnique().HasFilter("[Status] = 'Approved' AND [IsDeleted] = 0");
            entity.HasOne(value => value.Project).WithMany().HasForeignKey(value => value.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ProjectBoqVersion).WithMany().HasForeignKey(value => value.ProjectBoqVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.SourceEstimateVersion).WithMany().HasForeignKey(value => value.SourceEstimateVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Currency).WithMany().HasForeignKey(value => value.CurrencyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationProfile).WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationDecision).WithMany().HasForeignKey(value => value.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentRecord).WithMany().HasForeignKey(value => value.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentVersion).WithMany().HasForeignKey(value => value.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsEstimateVersions_Version", "[VersionNumber] > 0");
                table.HasCheckConstraint("CK_QsEstimateVersions_Type", "[EstimateType] BETWEEN 0 AND 2");
                table.HasCheckConstraint("CK_QsEstimateVersions_Status", "[Status] IN ('Draft','PendingApproval','Approved','Rejected','Retired')");
                table.HasCheckConstraint("CK_QsEstimateVersions_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                table.HasCheckConstraint("CK_QsEstimateVersions_Totals", "[DirectCost] >= 0 AND [MarkupTotal] >= 0 AND [TotalAmount] = [DirectCost] + [MarkupTotal]");
                table.HasCheckConstraint("CK_QsEstimateVersions_Counts", "[LineCount] > 0 AND [AssumptionCount] >= 0 AND [MarkupCount] >= 0");
                table.HasCheckConstraint("CK_QsEstimateVersions_EvidencePair", "([CentralDocumentRecordId] IS NULL AND [CentralDocumentVersionId] IS NULL) OR ([CentralDocumentRecordId] IS NOT NULL AND [CentralDocumentVersionId] IS NOT NULL)");
            });
        });
        builder.Entity<QuantitySurveyEstimateLine>(entity =>
        {
            entity.Property(value => value.Quantity).HasColumnType("decimal(18,4)");
            entity.Property(value => value.UnitRate).HasColumnType("decimal(18,6)");
            entity.Property(value => value.LineAmount).HasColumnType("decimal(18,2)");
            entity.HasIndex(value => new { value.TenantId, value.EstimateVersionId, value.Sequence }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.EstimateVersionId, value.ProjectBoqVersionLineId }).IsUnique();
            entity.HasOne(value => value.EstimateVersion).WithMany(value => value.Lines).HasForeignKey(value => value.EstimateVersionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.ProjectBoqVersionLine).WithMany().HasForeignKey(value => value.ProjectBoqVersionLineId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.SourceRate).WithMany().HasForeignKey(value => value.SourceRateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsEstimateLines_Sequence", "[Sequence] > 0");
                table.HasCheckConstraint("CK_QsEstimateLines_Amounts", "[Quantity] >= 0 AND [UnitRate] >= 0 AND [LineAmount] >= 0");
            });
        });
        builder.Entity<QuantitySurveyEstimateAssumption>(entity =>
        {
            entity.Property(value => value.Code).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.EstimateVersionId, value.Sequence }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.EstimateVersionId, value.Code }).IsUnique();
            entity.HasOne(value => value.EstimateVersion).WithMany(value => value.Assumptions).HasForeignKey(value => value.EstimateVersionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("CK_QsEstimateAssumptions_Sequence", "[Sequence] > 0"));
        });
        builder.Entity<QuantitySurveyEstimateMarkup>(entity =>
        {
            entity.Property(value => value.Percentage).HasColumnType("decimal(9,4)");
            entity.Property(value => value.BasisAmount).HasColumnType("decimal(18,2)");
            entity.Property(value => value.Amount).HasColumnType("decimal(18,2)");
            entity.HasIndex(value => new { value.TenantId, value.EstimateVersionId, value.Sequence }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.EstimateVersionId, value.Component }).IsUnique();
            entity.HasOne(value => value.EstimateVersion).WithMany(value => value.Markups).HasForeignKey(value => value.EstimateVersionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsEstimateMarkups_Sequence", "[Sequence] > 0");
                table.HasCheckConstraint("CK_QsEstimateMarkups_Component", "[Component] IN (5,6,8,9)");
                table.HasCheckConstraint("CK_QsEstimateMarkups_Amounts", "[Percentage] >= 0 AND [BasisAmount] >= 0 AND [Amount] >= 0");
            });
        });
        builder.Entity<QuantitySurveyEstimateRevision>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.EstimateVersionId, value.CreatedAt });
            entity.HasIndex(value => new { value.TenantId, value.CorrelationId });
            entity.HasOne(value => value.EstimateVersion).WithMany().HasForeignKey(value => value.EstimateVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<QuantitySurveyPriceIndexFamily>(entity =>
        {
            entity.Property(value => value.Code).IsUnicode(false);
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(value => new { value.TenantId, value.Code }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.Source, value.IsActive });
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("CK_QsPriceIndexFamilies_Source", "[Source] IN (0,1,2)"));
        });
        builder.Entity<QuantitySurveyEscalationFormulaDefinition>(entity =>
        {
            entity.Property(value => value.Code).IsUnicode(false);
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.SnapshotHash).IsUnicode(false);
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(value => new { value.TenantId, value.FormulaKey, value.Version }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ProjectId, value.ContractId, value.Code, value.Version }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.FormulaKey })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [Status] IN ('Draft','PendingApproval')");
            entity.HasIndex(value => new { value.TenantId, value.ProjectId, value.ContractId, value.Status, value.EffectiveFrom });
            entity.HasIndex(value => new { value.TenantId, value.ConfigurationDecisionId, value.Status });
            entity.HasOne(value => value.Project).WithMany().HasForeignKey(value => value.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Contract).WithMany().HasForeignKey(value => value.ContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.AuthorityRole).WithMany().HasForeignKey(value => value.AuthorityRoleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationProfile).WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationDecision).WithMany().HasForeignKey(value => value.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ApprovalWorkflowDefinition).WithMany().HasForeignKey(value => value.ApprovalWorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentRecord).WithMany().HasForeignKey(value => value.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentVersion).WithMany().HasForeignKey(value => value.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.SupersedesFormula).WithMany().HasForeignKey(value => value.SupersedesFormulaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsEscalationFormulas_Version", "[Version] > 0");
                table.HasCheckConstraint("CK_QsEscalationFormulas_Type", "[FormulaType] IN (0,1)");
                table.HasCheckConstraint("CK_QsEscalationFormulas_Period", "[BaseDate] <= [EffectiveFrom] AND ([EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom])");
                table.HasCheckConstraint("CK_QsEscalationFormulas_Status", "[Status] IN ('Draft','PendingApproval','Approved','Rejected','Retired')");
                table.HasCheckConstraint("CK_QsEscalationFormulas_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                table.HasCheckConstraint("CK_QsEscalationFormulas_Approved", "[Status] <> 'Approved' OR ([ApprovalStatus] = 'Approved' AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL)");
            });
        });
        builder.Entity<QuantitySurveyEscalationFormulaComponent>(entity =>
        {
            entity.Property(value => value.Coefficient).HasColumnType("decimal(9,4)");
            entity.Property(value => value.IndexFamilyCodeSnapshot).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.FormulaId, value.Sequence }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.FormulaId, value.Component }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.IndexFamilyId });
            entity.HasOne(value => value.Formula).WithMany(value => value.Components).HasForeignKey(value => value.FormulaId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.IndexFamily).WithMany().HasForeignKey(value => value.IndexFamilyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsEscalationFormulaComponents_Sequence", "[Sequence] BETWEEN 1 AND 4");
                table.HasCheckConstraint("CK_QsEscalationFormulaComponents_Component", "[Component] IN (0,1,2,3)");
                table.HasCheckConstraint("CK_QsEscalationFormulaComponents_Coefficient", "[Coefficient] >= 0 AND [Coefficient] <= 100");
                table.HasCheckConstraint("CK_QsEscalationFormulaComponents_Source", "[IndexSourceSnapshot] IN (0,1,2)");
            });
        });
        builder.Entity<QuantitySurveyEscalationFormulaRevision>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.FormulaId, value.CreatedAt });
            entity.HasIndex(value => new { value.TenantId, value.CorrelationId });
            entity.HasOne(value => value.Formula).WithMany().HasForeignKey(value => value.FormulaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<QuantitySurveyPriceIndexImportBatch>(entity =>
        {
            entity.Property(value => value.FileHash).IsUnicode(false);
            entity.Property(value => value.NormalizedPayloadHash).IsUnicode(false);
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.IndexFamilyId, value.Status, value.PreparedAt });
            entity.HasIndex(value => new { value.TenantId, value.ConfigurationDecisionId, value.Status });
            entity.HasOne(value => value.IndexFamily).WithMany().HasForeignKey(value => value.IndexFamilyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationProfile).WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationDecision).WithMany().HasForeignKey(value => value.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ApprovalWorkflowDefinition).WithMany().HasForeignKey(value => value.ApprovalWorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.AuthorityRole).WithMany().HasForeignKey(value => value.AuthorityRoleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentRecord).WithMany().HasForeignKey(value => value.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentVersion).WithMany().HasForeignKey(value => value.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsPriceIndexImportBatches_Source", "[IndexSource] IN (0,1,2)");
                table.HasCheckConstraint("CK_QsPriceIndexImportBatches_Format", "[ImportFormat] IN ('Controlled Excel','CSV')");
                table.HasCheckConstraint("CK_QsPriceIndexImportBatches_Status", "[Status] IN ('Staged','Invalid','PendingApproval','Approved','Rejected')");
                table.HasCheckConstraint("CK_QsPriceIndexImportBatches_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                table.HasCheckConstraint("CK_QsPriceIndexImportBatches_Counts", "[LineCount] >= 0 AND [ErrorCount] >= 0");
                table.HasCheckConstraint("CK_QsPriceIndexImportBatches_Approved", "[Status] <> 'Approved' OR ([ApprovalStatus] = 'Approved' AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [ErrorCount] = 0 AND [LineCount] > 0)");
            });
        });
        builder.Entity<QuantitySurveyPriceIndexValue>(entity =>
        {
            entity.Property(value => value.IndexValue).HasColumnType("decimal(18,6)");
            entity.HasIndex(value => new { value.TenantId, value.ImportBatchId, value.Sequence }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ImportBatchId, value.IndexPeriod }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ValueKey, value.Version }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.IndexFamilyId, value.IndexPeriod })
                .IsUnique().HasFilter("[IsDeleted] = 0 AND [IsCurrent] = 1 AND [Status] = 'Approved'");
            entity.HasOne(value => value.ImportBatch).WithMany(value => value.Values).HasForeignKey(value => value.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.IndexFamily).WithMany().HasForeignKey(value => value.IndexFamilyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.SupersedesValue).WithMany().HasForeignKey(value => value.SupersedesValueId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsPriceIndexValues_Sequence", "[Sequence] > 0");
                table.HasCheckConstraint("CK_QsPriceIndexValues_Value", "[IndexValue] > 0");
                table.HasCheckConstraint("CK_QsPriceIndexValues_Period", "DAY([IndexPeriod]) = 1");
                table.HasCheckConstraint("CK_QsPriceIndexValues_Publication", "[PublicationDate] >= [IndexPeriod]");
                table.HasCheckConstraint("CK_QsPriceIndexValues_Status", "[Status] IN ('Staged','Approved','Superseded','Rejected')");
                table.HasCheckConstraint("CK_QsPriceIndexValues_Current", "[IsCurrent] = 0 OR [Status] = 'Approved'");
                table.HasCheckConstraint("CK_QsPriceIndexValues_Version", "[Version] > 0");
            });
        });
        builder.Entity<QuantitySurveyPriceIndexImportRevision>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.ImportBatchId, value.CreatedAt });
            entity.HasIndex(value => new { value.TenantId, value.CorrelationId });
            entity.HasOne(value => value.ImportBatch).WithMany().HasForeignKey(value => value.ImportBatchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<QuantitySurveyEscalationCalculationRun>(entity =>
        {
            entity.Property(value => value.BaseRate).HasColumnType("decimal(18,2)");
            entity.Property(value => value.RevisedRate).HasColumnType("decimal(18,2)");
            entity.Property(value => value.AdjustmentFactor).HasColumnType("decimal(18,12)");
            entity.Property(value => value.CalculatedFluctuationAmount).HasColumnType("decimal(18,2)");
            entity.Property(value => value.ReviewerAdjustmentAmount).HasColumnType("decimal(18,2)");
            entity.Property(value => value.ApprovedImpactAmount).HasColumnType("decimal(18,2)");
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.ImpactTargetSnapshotHash).IsUnicode(false);
            entity.Property(value => value.SnapshotHash).IsUnicode(false);
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.RunReference }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ProjectId, value.Status, value.PreparedAt });
            entity.HasIndex(value => new { value.TenantId, value.ContractId, value.Status });
            entity.HasIndex(value => new { value.TenantId, value.FormulaId, value.Status });
            entity.HasOne(value => value.Formula).WithMany().HasForeignKey(value => value.FormulaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Project).WithMany().HasForeignKey(value => value.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Contract).WithMany().HasForeignKey(value => value.ContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.PaymentCertificate).WithMany().HasForeignKey(value => value.PaymentCertificateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.FinalAccount).WithMany().HasForeignKey(value => value.FinalAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.AuthorityRole).WithMany().HasForeignKey(value => value.AuthorityRoleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationProfile).WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationDecision).WithMany().HasForeignKey(value => value.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ApprovalWorkflowDefinition).WithMany().HasForeignKey(value => value.ApprovalWorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsEscalationCalculationRuns_TargetType", "[ImpactTargetType] IN (0,1)");
                table.HasCheckConstraint("CK_QsEscalationCalculationRuns_Target", "([ImpactTargetType] = 0 AND [PaymentCertificateId] IS NOT NULL AND [FinalAccountId] IS NULL) OR ([ImpactTargetType] = 1 AND [PaymentCertificateId] IS NULL AND [FinalAccountId] IS NOT NULL)");
                table.HasCheckConstraint("CK_QsEscalationCalculationRuns_Period", "DAY([BaseIndexPeriod]) = 1 AND DAY([CurrentIndexPeriod]) = 1 AND [CurrentIndexPeriod] >= [BaseIndexPeriod]");
                table.HasCheckConstraint("CK_QsEscalationCalculationRuns_Amounts", "[BaseRate] > 0 AND [RevisedRate] >= 0 AND [AdjustmentFactor] > 0 AND [ApprovedImpactAmount] = [CalculatedFluctuationAmount] + [ReviewerAdjustmentAmount]");
                table.HasCheckConstraint("CK_QsEscalationCalculationRuns_Status", "[Status] IN ('Draft','PendingApproval','ApprovedPendingApplication','Rejected')");
                table.HasCheckConstraint("CK_QsEscalationCalculationRuns_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                table.HasCheckConstraint("CK_QsEscalationCalculationRuns_Application", "[ImpactApplicationStatus] IN ('Projected','PendingApplication','Applied','ApplicationFailed')");
                table.HasCheckConstraint("CK_QsEscalationCalculationRuns_Lifecycle", "([Status] = 'Draft' AND [ApprovalStatus] = 'Draft') OR ([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending' AND [SubmittedById] IS NOT NULL AND [SubmittedAt] IS NOT NULL) OR ([Status] = 'ApprovedPendingApplication' AND [ApprovalStatus] = 'Approved' AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [ImpactApplicationStatus] IN ('PendingApplication','Applied','ApplicationFailed')) OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected' AND [RejectionReason] IS NOT NULL)");
            });
        });
        builder.Entity<QuantitySurveyEscalationCalculationLine>(entity =>
        {
            entity.Property(value => value.Coefficient).HasColumnType("decimal(9,4)");
            entity.Property(value => value.BaseIndexValue).HasColumnType("decimal(18,6)");
            entity.Property(value => value.CurrentIndexValue).HasColumnType("decimal(18,6)");
            entity.Property(value => value.IndexRatio).HasColumnType("decimal(18,12)");
            entity.Property(value => value.WeightedContribution).HasColumnType("decimal(18,12)");
            entity.HasIndex(value => new { value.TenantId, value.CalculationRunId, value.Sequence }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.CalculationRunId, value.Component }).IsUnique();
            entity.HasOne(value => value.CalculationRun).WithMany(value => value.Lines).HasForeignKey(value => value.CalculationRunId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.IndexFamily).WithMany().HasForeignKey(value => value.IndexFamilyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.BaseIndexValueRecord).WithMany().HasForeignKey(value => value.BaseIndexValueId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CurrentIndexValueRecord).WithMany().HasForeignKey(value => value.CurrentIndexValueId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsEscalationCalculationLines_Sequence", "[Sequence] BETWEEN 1 AND 4");
                table.HasCheckConstraint("CK_QsEscalationCalculationLines_Component", "[Component] IN (0,1,2,3)");
                table.HasCheckConstraint("CK_QsEscalationCalculationLines_Coefficient", "[Coefficient] >= 0 AND [Coefficient] <= 100");
                table.HasCheckConstraint("CK_QsEscalationCalculationLines_Indices", "[BaseIndexValue] > 0 AND [CurrentIndexValue] > 0 AND [IndexRatio] > 0 AND [WeightedContribution] >= 0");
            });
        });
        builder.Entity<QuantitySurveyEscalationCalculationRevision>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.CalculationRunId, value.CreatedAt });
            entity.HasIndex(value => new { value.TenantId, value.CorrelationId });
            entity.HasOne(value => value.CalculationRun).WithMany().HasForeignKey(value => value.CalculationRunId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<QuantitySurveyEscalationDispute>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.CalculationSnapshotHash).IsUnicode(false);
            entity.Property(value => value.ContractorResponseHash).IsUnicode(false);
            entity.Property(value => value.ResolutionRequestHash).IsUnicode(false);
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(value => new { value.TenantId, value.CalculationRunId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.DisputeReference }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ContractorResponseClientRequestId })
                .IsUnique().HasFilter("[ContractorResponseClientRequestId] IS NOT NULL");
            entity.HasIndex(value => new { value.TenantId, value.ResolutionClientRequestId })
                .IsUnique().HasFilter("[ResolutionClientRequestId] IS NOT NULL");
            entity.HasIndex(value => new { value.TenantId, value.ProjectId, value.Status, value.OpenedAt });
            entity.HasIndex(value => new { value.TenantId, value.ContractId, value.Status });
            entity.HasOne(value => value.CalculationRun).WithMany().HasForeignKey(value => value.CalculationRunId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Project).WithMany().HasForeignKey(value => value.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Contract).WithMany().HasForeignKey(value => value.ContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ContractorBusinessPartner).WithMany().HasForeignKey(value => value.ContractorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.OpenedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ContractorRespondedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ResolvedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsEscalationDisputes_Status", "[Status] IN ('Open','ContractorResponded','Resolved')");
                table.HasCheckConstraint("CK_QsEscalationDisputes_Outcome", "[Outcome] IS NULL OR [Outcome] IN (0,1,2)");
                table.HasCheckConstraint("CK_QsEscalationDisputes_Lifecycle", "([Status] = 'Open' AND [ContractorResponseClientRequestId] IS NULL AND [ContractorResponse] IS NULL AND [ContractorRespondedById] IS NULL AND [ContractorRespondedAt] IS NULL AND [ResolutionClientRequestId] IS NULL AND [Outcome] IS NULL AND [ResolutionNotes] IS NULL AND [ResolvedById] IS NULL AND [ResolvedAt] IS NULL) OR ([Status] = 'ContractorResponded' AND [ContractorResponseClientRequestId] IS NOT NULL AND [ContractorResponseHash] IS NOT NULL AND [ContractorResponse] IS NOT NULL AND [ContractorRespondedById] IS NOT NULL AND [ContractorRespondedAt] IS NOT NULL AND [ResolutionClientRequestId] IS NULL AND [Outcome] IS NULL AND [ResolutionNotes] IS NULL AND [ResolvedById] IS NULL AND [ResolvedAt] IS NULL) OR ([Status] = 'Resolved' AND [ContractorResponseClientRequestId] IS NOT NULL AND [ContractorResponseHash] IS NOT NULL AND [ContractorResponse] IS NOT NULL AND [ContractorRespondedById] IS NOT NULL AND [ContractorRespondedAt] IS NOT NULL AND [ResolutionClientRequestId] IS NOT NULL AND [ResolutionRequestHash] IS NOT NULL AND [Outcome] IS NOT NULL AND [ResolutionNotes] IS NOT NULL AND [ResolvedById] IS NOT NULL AND [ResolvedAt] IS NOT NULL)");
                table.HasCheckConstraint("CK_QsEscalationDisputes_Sod", "[ResolvedById] IS NULL OR ([ResolvedById] <> [OpenedById] AND [ResolvedById] <> [ContractorRespondedById])");
            });
        });
        builder.Entity<QuantitySurveyEscalationDisputeAttachment>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.ChecksumSha256).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.CentralDocumentVersionId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.DisputeId, value.AttachmentType, value.CreatedAt });
            entity.HasOne(value => value.Dispute).WithMany(value => value.Attachments).HasForeignKey(value => value.DisputeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.FileUploadRecord).WithMany().HasForeignKey(value => value.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentRecord).WithMany().HasForeignKey(value => value.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentVersion).WithMany().HasForeignKey(value => value.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.UploadedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsEscalationDisputeAttachments_Type", "[AttachmentType] IN (0,1,2)");
                table.HasCheckConstraint("CK_QsEscalationDisputeAttachments_File", "[FileSize] > 0 AND LEN([ChecksumSha256]) = 64 AND LEN([RequestHash]) = 64");
            });
        });
        builder.Entity<QuantitySurveyEscalationDisputeRevision>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.DisputeId, value.CreatedAt });
            entity.HasIndex(value => new { value.TenantId, value.CorrelationId });
            entity.HasOne(value => value.Dispute).WithMany().HasForeignKey(value => value.DisputeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<QuantitySurveyMeasurementSheet>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.LastMutationRequestHash).IsUnicode(false);
            entity.Property(value => value.PolicyHash).IsUnicode(false);
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(value => new { value.TenantId, value.SheetReference }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.LastMutationClientRequestId }).IsUnique().HasFilter("[LastMutationClientRequestId] IS NOT NULL");
            entity.HasIndex(value => new { value.TenantId, value.ProjectId, value.Status, value.MeasurementDate });
            entity.HasIndex(value => new { value.TenantId, value.ProjectBoqVersionLineId, value.Status });
            entity.HasOne(value => value.Project).WithMany().HasForeignKey(value => value.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ProjectBoqVersion).WithMany().HasForeignKey(value => value.ProjectBoqVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ProjectBoqVersionLine).WithMany().HasForeignKey(value => value.ProjectBoqVersionLineId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ProjectDrawing).WithMany().HasForeignKey(value => value.ProjectDrawingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationProfile).WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationDecision).WithMany().HasForeignKey(value => value.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.EvidenceMetadataTemplate).WithMany().HasForeignKey(value => value.EvidenceMetadataTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.PreparedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.RecordedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QsMeasurementSheets_Guard");
                table.HasCheckConstraint("CK_QsMeasurementSheets_Status", "[Status] IN ('Draft','Recorded')");
                table.HasCheckConstraint("CK_QsMeasurementSheets_Source", "[SourceType] IN (0,1) AND (([SourceType] = 0 AND [ProjectDrawingId] IS NOT NULL) OR ([SourceType] = 1 AND [SiteLocation] IS NOT NULL))");
                table.HasCheckConstraint("CK_QsMeasurementSheets_Quantity", "[BoqQuantitySnapshot] >= 0 AND [TotalMeasuredQuantity] > 0");
                table.HasCheckConstraint("CK_QsMeasurementSheets_Hashes", "LEN([RequestHash]) = 64 AND LEN([PolicyHash]) = 64 AND ([LastMutationRequestHash] IS NULL OR LEN([LastMutationRequestHash]) = 64)");
                table.HasCheckConstraint("CK_QsMeasurementSheets_Lifecycle", "([Status] = 'Draft' AND [RecordedById] IS NULL AND [RecordedAt] IS NULL) OR ([Status] = 'Recorded' AND [RecordedById] IS NOT NULL AND [RecordedAt] IS NOT NULL)");
            });
        });
        builder.Entity<QuantitySurveyMeasurementLine>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.MeasurementSheetId, value.ClientLineKey }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.MeasurementSheetId, value.Sequence }).IsUnique();
            entity.HasOne(value => value.MeasurementSheet).WithMany(value => value.Lines).HasForeignKey(value => value.MeasurementSheetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QsMeasurementLines_Guard");
                table.HasCheckConstraint("CK_QsMeasurementLines_Formula", "[FormulaType] IN (0,1,2,3) AND [Timesing] > 0 AND (([FormulaType] = 0 AND [Length] IS NULL AND [Width] IS NULL AND [Height] IS NULL) OR ([FormulaType] = 1 AND [Length] > 0 AND [Width] IS NULL AND [Height] IS NULL) OR ([FormulaType] = 2 AND [Length] > 0 AND [Width] > 0 AND [Height] IS NULL) OR ([FormulaType] = 3 AND [Length] > 0 AND [Width] > 0 AND [Height] > 0))");
                table.HasCheckConstraint("CK_QsMeasurementLines_Calculation", "[CalculatedQuantity] <> 0 AND (([IsDeduction] = 1 AND [CalculatedQuantity] < 0) OR ([IsDeduction] = 0 AND [CalculatedQuantity] > 0))");
            });
        });
        builder.Entity<QuantitySurveyMeasurementAttachment>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.ChecksumSha256).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.CentralDocumentVersionId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.MeasurementSheetId, value.EvidenceType, value.CreatedAt });
            entity.HasOne(value => value.MeasurementSheet).WithMany(value => value.Attachments).HasForeignKey(value => value.MeasurementSheetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.FileUploadRecord).WithMany().HasForeignKey(value => value.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentRecord).WithMany().HasForeignKey(value => value.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentVersion).WithMany().HasForeignKey(value => value.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.UploadedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QsMeasurementAttachments_Guard");
                table.HasCheckConstraint("CK_QsMeasurementAttachments_Type", "[EvidenceType] IN (0,1,2,3)");
                table.HasCheckConstraint("CK_QsMeasurementAttachments_File", "[FileSize] > 0 AND LEN([ChecksumSha256]) = 64 AND LEN([RequestHash]) = 64");
            });
        });
        builder.Entity<QuantitySurveyMeasurementRevision>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.MeasurementSheetId, value.CreatedAt });
            entity.HasIndex(value => new { value.TenantId, value.CorrelationId });
            entity.HasOne(value => value.MeasurementSheet).WithMany().HasForeignKey(value => value.MeasurementSheetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasTrigger("TR_QsMeasurementRevisions_Guard"));
        });
        builder.Entity<QuantitySurveyValuationWorksheet>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.LastMutationRequestHash).IsUnicode(false);
            entity.Property(value => value.ApprovalStatus).HasDefaultValue("Draft");
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(value => new { value.TenantId, value.ProjectInterimValuationId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.LastMutationClientRequestId }).IsUnique()
                .HasFilter("[LastMutationClientRequestId] IS NOT NULL");
            entity.HasIndex(value => new { value.TenantId, value.ProjectId, value.Status, value.PreparedAt });
            entity.HasOne(value => value.Project).WithMany().HasForeignKey(value => value.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ProjectInterimValuation).WithMany().HasForeignKey(value => value.ProjectInterimValuationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ProjectBoqVersion).WithMany().HasForeignKey(value => value.ProjectBoqVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ContractorBusinessPartner).WithMany().HasForeignKey(value => value.ContractorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConsultantBusinessPartner).WithMany().HasForeignKey(value => value.ConsultantBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationProfile).WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ValuationDecision).WithMany().HasForeignKey(value => value.ValuationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ExternalSubmissionDecision).WithMany().HasForeignKey(value => value.ExternalSubmissionDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.EvidenceMetadataTemplate).WithMany().HasForeignKey(value => value.EvidenceMetadataTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.PreparedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ContractorSubmittedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.QsVettedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ConsultantEndorsedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QsValuationWorksheets_Guard");
                table.HasCheckConstraint("CK_QsValuationWorksheets_Status", "[Status] IN ('Draft','ContractorSubmitted','UnderQsReview','QsVetted','ConsultantEndorsed','PendingApproval','Approved','Rejected')");
                table.HasCheckConstraint("CK_QsValuationWorksheets_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                table.HasCheckConstraint("CK_QsValuationWorksheets_StateAlignment", "([Status] IN ('Draft','ContractorSubmitted','UnderQsReview','QsVetted','ConsultantEndorsed') AND [ApprovalStatus] = 'Draft') OR ([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending') OR ([Status] = 'Approved' AND [ApprovalStatus] = 'Approved') OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected')");
                table.HasCheckConstraint("CK_QsValuationWorksheets_Policy", "([ConfigurationProfileId] IS NULL AND [ValuationDecisionId] IS NULL AND [ExternalSubmissionDecisionId] IS NULL AND [ApprovalWorkflowDefinitionId] IS NULL AND [EvidenceMetadataTemplateId] IS NULL AND [PolicyHash] IS NULL) OR ([ConfigurationProfileId] IS NOT NULL AND [ValuationDecisionId] IS NOT NULL AND [ExternalSubmissionDecisionId] IS NOT NULL AND [ApprovalWorkflowDefinitionId] IS NOT NULL AND [EvidenceMetadataTemplateId] IS NOT NULL AND LEN([PolicyHash]) = 64)");
                table.HasCheckConstraint("CK_QsValuationWorksheets_Lifecycle", "([Status] = 'Draft' AND [WorkflowInstanceId] IS NULL AND [ApprovedAt] IS NULL AND [CertificateReady] = 0) OR ([Status] IN ('ContractorSubmitted','UnderQsReview','QsVetted','ConsultantEndorsed') AND [WorkflowInstanceId] IS NULL AND [ApprovedAt] IS NULL AND [CertificateReady] = 0) OR ([Status] = 'PendingApproval' AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedAt] IS NULL AND [CertificateReady] = 0) OR ([Status] = 'Approved' AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [CertificateReady] = 1 AND [CertificateReadyAt] IS NOT NULL) OR ([Status] = 'Rejected' AND [WorkflowInstanceId] IS NOT NULL AND [RejectionReason] IS NOT NULL AND [CertificateReady] = 0)");
                table.HasCheckConstraint("CK_QsValuationWorksheets_Retention", "[RetentionPercentage] >= 0 AND [RetentionPercentage] <= 100");
                table.HasCheckConstraint("CK_QsValuationWorksheets_Counts", "[LineCount] > 0");
                table.HasCheckConstraint("CK_QsValuationWorksheets_Amounts", "[MeasuredToDateValue] >= 0 AND [PreviouslyCertifiedValue] >= 0 AND [CurrentClaimedValue] >= 0 AND [CurrentCertifiedValue] >= [PreviouslyCertifiedValue] AND [CurrentCertifiedValue] <= [CurrentClaimedValue] AND [CurrentPeriodCertifiedValue] = [CurrentCertifiedValue] - [PreviouslyCertifiedValue] AND [DisputedValue] = [CurrentClaimedValue] - [CurrentCertifiedValue] AND [RetentionToDateValue] >= [CurrentRetentionValue] AND [CurrentRetentionValue] >= 0 AND [NetCurrentValue] = [CurrentPeriodCertifiedValue] - [CurrentRetentionValue]");
                table.HasCheckConstraint("CK_QsValuationWorksheets_Hashes", "LEN([RequestHash]) = 64 AND ([LastMutationRequestHash] IS NULL OR LEN([LastMutationRequestHash]) = 64)");
            });
        });
        builder.Entity<QuantitySurveyValuationWorksheetLine>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.WorksheetId, value.ProjectBoqVersionLineId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.WorksheetId, value.BoqLineKey }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.WorksheetId, value.Sequence }).IsUnique();
            entity.HasOne(value => value.Worksheet).WithMany(value => value.Lines).HasForeignKey(value => value.WorksheetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ProjectBoqVersionLine).WithMany().HasForeignKey(value => value.ProjectBoqVersionLineId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QsValuationWorksheetLines_Guard");
                table.HasCheckConstraint("CK_QsValuationWorksheetLines_Quantities", "[BoqQuantitySnapshot] >= 0 AND [UnitRateSnapshot] >= 0 AND [MeasuredToDateQuantity] >= 0 AND [PreviouslyCertifiedQuantity] >= 0 AND [CurrentClaimedQuantity] >= [CurrentCertifiedQuantity] AND [CurrentCertifiedQuantity] >= [PreviouslyCertifiedQuantity] AND [CurrentClaimedQuantity] <= [MeasuredToDateQuantity] AND [DisputedQuantity] = [CurrentClaimedQuantity] - [CurrentCertifiedQuantity]");
                table.HasCheckConstraint("CK_QsValuationWorksheetLines_Amounts", "[MeasuredToDateValue] >= 0 AND [PreviouslyCertifiedValue] >= 0 AND [CurrentClaimedValue] >= [CurrentCertifiedValue] AND [CurrentCertifiedValue] >= [PreviouslyCertifiedValue] AND [CurrentPeriodCertifiedValue] = [CurrentCertifiedValue] - [PreviouslyCertifiedValue] AND [DisputedValue] = [CurrentClaimedValue] - [CurrentCertifiedValue] AND [PreviousRetentionValue] >= 0 AND [RetentionToDateValue] >= [PreviousRetentionValue] AND [CurrentRetentionValue] = [RetentionToDateValue] - [PreviousRetentionValue] AND [NetCurrentValue] = [CurrentPeriodCertifiedValue] - [CurrentRetentionValue]");
                table.HasCheckConstraint("CK_QsValuationWorksheetLines_DisputeNote", "[DisputedQuantity] = 0 OR LEN(LTRIM(RTRIM([ReviewNote]))) > 0");
            });
        });
        builder.Entity<QuantitySurveyValuationWorksheetRevision>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.WorksheetId, value.CreatedAt });
            entity.HasIndex(value => new { value.TenantId, value.CorrelationId });
            entity.HasOne(value => value.Worksheet).WithMany().HasForeignKey(value => value.WorksheetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Procurement.BusinessPartner>().WithMany().HasForeignKey(value => value.ActorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasTrigger("TR_QsValuationWorksheetRevisions_AppendOnly"));
        });
        builder.Entity<QuantitySurveyValuationWorksheetEvidence>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.ChecksumSha256).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.CentralDocumentVersionId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.WorksheetId, value.EvidenceType, value.UploadedAt });
            entity.HasOne(value => value.Worksheet).WithMany(value => value.Evidence).HasForeignKey(value => value.WorksheetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.FileUploadRecord).WithMany().HasForeignKey(value => value.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentRecord).WithMany().HasForeignKey(value => value.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentVersion).WithMany().HasForeignKey(value => value.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.UploadedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QsValuationWorksheetEvidence_AppendOnly");
                table.HasCheckConstraint("CK_QsValuationEvidence_Type", "[EvidenceType] IN (0,1,2,3,4,5,6)");
                table.HasCheckConstraint("CK_QsValuationEvidence_File", "[FileSize] > 0 AND LEN([ChecksumSha256]) = 64 AND LEN([RequestHash]) = 64");
            });
        });
        builder.Entity<QuantitySurveyPaymentCertificateRevision>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.PaymentCertificateId, value.CreatedAt });
            entity.HasIndex(value => new { value.TenantId, value.CorrelationId });
            entity.HasOne(value => value.PaymentCertificate).WithMany().HasForeignKey(value => value.PaymentCertificateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasTrigger("TR_QuantitySurveyPaymentCertificateRevisions_AppendOnly"));
        });
        builder.Entity<QuantitySurveyAdvanceRecoveryAgreement>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.LastMutationRequestHash).IsUnicode(false);
            entity.Property(value => value.PolicyHash).IsUnicode(false);
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique()
                .HasDatabaseName("IX_QsAdvanceRecovery_Tenant_ClientRequest");
            entity.HasIndex(value => new { value.TenantId, value.VendorPaymentId }).IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [Status] <> 'Rejected'")
                .HasDatabaseName("IX_QsAdvanceRecovery_Tenant_VendorPayment_Active");
            entity.HasIndex(value => new { value.TenantId, value.RecoveryNumber }).IsUnique()
                .HasDatabaseName("IX_QsAdvanceRecovery_Tenant_Number");
            entity.HasIndex(value => new { value.TenantId, value.ProjectId, value.ContractId, value.Status });
            entity.HasOne(value => value.Project).WithMany().HasForeignKey(value => value.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Contract).WithMany().HasForeignKey(value => value.ContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.VendorPayment).WithMany().HasForeignKey(value => value.VendorPaymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationProfile).WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ValuationDecision).WithMany().HasForeignKey(value => value.ValuationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.PreparedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.SubmittedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsAdvanceRecovery_Status", "[Status] IN ('Draft','PendingApproval','Approved','Rejected','Closed')");
                table.HasCheckConstraint("CK_QsAdvanceRecovery_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                table.HasCheckConstraint("CK_QsAdvanceRecovery_Amounts", "[OriginalAdvanceAmount] > 0 AND [RecoveryPercentage] > 0 AND [RecoveryPercentage] <= 100");
                table.HasCheckConstraint("CK_QsAdvanceRecovery_State", "([Status] = 'Draft' AND [ApprovalStatus] = 'Draft' AND [ApprovedAt] IS NULL) OR ([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending' AND [SubmittedById] IS NOT NULL AND [SubmittedAt] IS NOT NULL AND [ApprovedAt] IS NULL) OR ([Status] IN ('Approved','Closed') AND [ApprovalStatus] = 'Approved' AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL) OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected' AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [RejectionReason] IS NOT NULL)");
            });
        });
        builder.Entity<QuantitySurveyAdvanceRecoveryRevision>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.AgreementId, value.CreatedAt });
            entity.HasIndex(value => new { value.TenantId, value.CorrelationId });
            entity.HasOne(value => value.Agreement).WithMany().HasForeignKey(value => value.AgreementId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ProjectPaymentCertificate>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.QuantitySurveyAdvanceRecoveryAgreementId, value.Status });
            entity.HasOne(value => value.QuantitySurveyAdvanceRecoveryAgreement).WithMany()
                .HasForeignKey(value => value.QuantitySurveyAdvanceRecoveryAgreementId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(value => new { value.TenantId, value.QuantitySurveyMaterialReconciliationId, value.Status });
            entity.HasIndex(value => new { value.TenantId, value.QuantitySurveyMaterialReconciliationId })
                .IsUnique()
                .HasFilter("[QuantitySurveyMaterialReconciliationId] IS NOT NULL AND [Status] <> 'Cancelled' AND [IsDeleted] = 0");
            entity.HasOne(value => value.QuantitySurveyMaterialReconciliation).WithMany()
                .HasForeignKey(value => value.QuantitySurveyMaterialReconciliationId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectPaymentCertificates_QsLifecycle");
                table.HasTrigger("TR_QS0507_PaymentCertificateMaterialLineage");
            });
        });
        builder.Entity<QuantitySurveyJointMeasurementRequest>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.PolicyHash).IsUnicode(false);
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.RequestNumber }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ProjectId, value.Status, value.RequestedAt });
            entity.HasIndex(value => new { value.TenantId, value.ProjectBoqVersionLineId, value.Status });
            entity.HasIndex(value => new { value.TenantId, value.RemeasurementVersionId })
                .IsUnique().HasFilter("[RemeasurementVersionId] IS NOT NULL");
            entity.HasOne(value => value.Project).WithMany().HasForeignKey(value => value.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ProjectBoqVersion).WithMany().HasForeignKey(value => value.ProjectBoqVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ProjectBoqVersionLine).WithMany().HasForeignKey(value => value.ProjectBoqVersionLineId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ContractorBusinessPartner).WithMany().HasForeignKey(value => value.ContractorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConsultantBusinessPartner).WithMany().HasForeignKey(value => value.ConsultantBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationProfile).WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.MeasurementDecision).WithMany().HasForeignKey(value => value.MeasurementDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ExternalSubmissionDecision).WithMany().HasForeignKey(value => value.ExternalSubmissionDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.EvidenceMetadataTemplate).WithMany().HasForeignKey(value => value.EvidenceMetadataTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.MeasurementSheet).WithMany().HasForeignKey(value => value.MeasurementSheetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.RemeasurementVersion).WithMany().HasForeignKey(value => value.RemeasurementVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ScheduledByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ReviewedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsJointMeasurement_Status", "[Status] IN ('Draft','Submitted','Scheduled','AwaitingAttendance','AwaitingEndorsements','ReadyForReview','PendingApproval','ApprovedPendingBoqRevision','BoqWorkflowPending','Applied','Rejected','Cancelled')");
                table.HasCheckConstraint("CK_QsJointMeasurement_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                table.HasCheckConstraint("CK_QsJointMeasurement_Quantities", "[PreviousQuantity] >= 0 AND ([ContractorProposedQuantity] IS NULL OR [ContractorProposedQuantity] > 0)");
                table.HasCheckConstraint("CK_QsJointMeasurement_Schedule", "([ScheduledStartAt] IS NULL AND [ScheduledEndAt] IS NULL) OR ([ScheduledStartAt] IS NOT NULL AND [ScheduledEndAt] > [ScheduledStartAt] AND [ConsultantBusinessPartnerId] IS NOT NULL)");
                table.HasCheckConstraint("CK_QsJointMeasurement_Hashes", "LEN([RequestHash]) = 64 AND LEN([PolicyHash]) = 64");
                table.HasCheckConstraint("CK_QsJointMeasurement_Applied", "[Status] <> 'Applied' OR ([RemeasurementVersionId] IS NOT NULL AND [AppliedAt] IS NOT NULL)");
            });
        });
        builder.Entity<QuantitySurveyJointMeasurementParticipant>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.RequestId, value.ParticipantType, value.BusinessPartnerId, value.RequiredRoleId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.RequestId, value.AttendanceStatus });
            entity.HasOne(value => value.Request).WithMany(value => value.Participants).HasForeignKey(value => value.RequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.BusinessPartner).WithMany().HasForeignKey(value => value.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationRole>().WithMany().HasForeignKey(value => value.RequiredRoleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.AttendedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsJointParticipant_Type", "[ParticipantType] IN ('Contractor','Consultant','InternalRole')");
                table.HasCheckConstraint("CK_QsJointParticipant_Assignment", "([ParticipantType] IN ('Contractor','Consultant') AND [BusinessPartnerId] IS NOT NULL AND [RequiredRoleId] IS NULL) OR ([ParticipantType] = 'InternalRole' AND [BusinessPartnerId] IS NULL AND [RequiredRoleId] IS NOT NULL)");
                table.HasCheckConstraint("CK_QsJointParticipant_Attendance", "[AttendanceStatus] IN ('Invited','Attended','Absent') AND ([AttendanceStatus] <> 'Attended' OR ([AttendedByUserId] IS NOT NULL AND [AttendedAt] IS NOT NULL AND LEN([AttendanceHash]) = 64))");
            });
        });
        builder.Entity<QuantitySurveyJointMeasurementEndorsement>(entity =>
        {
            entity.Property(value => value.SignatureHash).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.RequestId, value.SignerType }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ParticipantId }).IsUnique();
            entity.HasOne(value => value.Request).WithMany(value => value.Endorsements).HasForeignKey(value => value.RequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Participant).WithMany().HasForeignKey(value => value.ParticipantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.BusinessPartner).WithMany().HasForeignKey(value => value.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.SignedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsJointEndorsement_Type", "[SignerType] IN ('Contractor','Consultant')");
                table.HasCheckConstraint("CK_QsJointEndorsement_Method", "[SignatureMethod] IN ('Attestation','DigitalCertificate','ExternalProvider')");
                table.HasCheckConstraint("CK_QsJointEndorsement_Hash", "LEN([SignatureHash]) = 64");
            });
        });
        builder.Entity<QuantitySurveyJointMeasurementEvidence>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.ChecksumSha256).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.CentralDocumentVersionId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.RequestId, value.EvidenceType, value.UploadedAt });
            entity.HasOne(value => value.Request).WithMany(value => value.Evidence).HasForeignKey(value => value.RequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FileUploadRecord>().WithMany().HasForeignKey(value => value.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentRecord).WithMany().HasForeignKey(value => value.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentVersion).WithMany().HasForeignKey(value => value.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsJointEvidence_Type", "[EvidenceType] IN (0,1,2,3,4)");
                table.HasCheckConstraint("CK_QsJointEvidence_File", "[FileSize] > 0 AND LEN([ChecksumSha256]) = 64 AND LEN([RequestHash]) = 64");
            });
        });
        builder.Entity<QuantitySurveyJointMeasurementRevision>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.RequestId, value.CreatedAt });
            entity.HasIndex(value => new { value.TenantId, value.CorrelationId });
            entity.HasOne(value => value.Request).WithMany(value => value.Revisions).HasForeignKey(value => value.RequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Procurement.BusinessPartner>().WithMany().HasForeignKey(value => value.ActorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<QuantitySurveyDesignRevisionImpact>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.LastMutationRequestHash).IsUnicode(false);
            entity.Property(value => value.PolicyHash).IsUnicode(false);
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.LastMutationClientRequestId }).IsUnique().HasFilter("[LastMutationClientRequestId] IS NOT NULL");
            entity.HasIndex(value => new { value.TenantId, value.ImpactNumber }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ProjectId, value.RevisedDrawingId, value.Route }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ProjectId, value.Status, value.CreatedAt });
            entity.HasOne(value => value.Project).WithMany().HasForeignKey(value => value.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.PreviousDrawing).WithMany().HasForeignKey(value => value.PreviousDrawingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.RevisedDrawing).WithMany().HasForeignKey(value => value.RevisedDrawingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationProfile).WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationDecision).WithMany().HasForeignKey(value => value.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.SubmittedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsDesignImpact_Route", "[Route] IN (0,1)");
                table.HasCheckConstraint("CK_QsDesignImpact_Status", "[Status] IN ('Draft','PendingApproval','Approved','Rejected')");
                table.HasCheckConstraint("CK_QsDesignImpact_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                table.HasCheckConstraint("CK_QsDesignImpact_Hashes", "LEN([RequestHash]) = 64 AND LEN([PolicyHash]) = 64 AND ([LastMutationRequestHash] IS NULL OR LEN([LastMutationRequestHash]) = 64)");
                table.HasCheckConstraint("CK_QsDesignImpact_Drawings", "[PreviousDrawingId] <> [RevisedDrawingId] AND LEN([RevisedRevisionSnapshot]) > 0");
                table.HasCheckConstraint("CK_QsDesignImpact_Lifecycle", "([Status] = 'Draft' AND [WorkflowInstanceId] IS NULL AND [ApprovedAt] IS NULL) OR ([Status] = 'PendingApproval' AND [WorkflowInstanceId] IS NOT NULL AND [SubmittedAt] IS NOT NULL) OR ([Status] = 'Approved' AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL) OR ([Status] = 'Rejected' AND [WorkflowInstanceId] IS NOT NULL AND [RejectionReason] IS NOT NULL)");
            });
        });
        builder.Entity<QuantitySurveyDesignRevisionImpactLine>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.ImpactId, value.ProjectBoqVersionLineId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ProjectBoqVersionLineId, value.CreatedAt });
            entity.HasOne(value => value.Impact).WithMany(value => value.Lines).HasForeignKey(value => value.ImpactId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ProjectBoqVersion).WithMany().HasForeignKey(value => value.ProjectBoqVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ProjectBoqVersionLine).WithMany().HasForeignKey(value => value.ProjectBoqVersionLineId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsDesignImpactLine_Type", "[ImpactType] BETWEEN 0 AND 5");
                table.HasCheckConstraint("CK_QsDesignImpactLine_Quantity", "[PreviousQuantity] >= 0 AND ([IndicativeQuantity] IS NULL OR [IndicativeQuantity] >= 0)");
            });
        });
        builder.Entity<QuantitySurveyDesignRevisionImpactRevision>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.ImpactId, value.CreatedAt });
            entity.HasIndex(value => new { value.TenantId, value.CorrelationId });
            entity.HasOne(value => value.Impact).WithMany(value => value.Revisions).HasForeignKey(value => value.ImpactId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureQuantitySurveyConfiguration(ModelBuilder builder)
    {
        builder.Entity<QuantitySurveyConfigurationProfile>(entity =>
        {
            entity.Property(x => x.ProfileCode).IsUnicode(false);
            entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(x => new { x.TenantId, x.ProfileKey, x.Version }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ProfileKey, x.LifecycleStatus });
            entity.HasIndex(x => new { x.TenantId, x.ProfileCode, x.Version }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.LifecycleStatus, x.EffectiveFrom });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsConfigurationProfiles_Version", "[Version] > 0");
                table.HasCheckConstraint("CK_QsConfigurationProfiles_Period", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.HasCheckConstraint("CK_QsConfigurationProfiles_Lifecycle", "[LifecycleStatus] IN (0, 1, 2)");
            });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<QuantitySurveyConfigurationProfile>().WithMany().HasForeignKey(x => x.SupersedesProfileId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<QuantitySurveyConfigurationDecision>(entity =>
        {
            entity.Property(x => x.DecisionKey).IsUnicode(false);
            entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(x => new { x.TenantId, x.ProfileId, x.DecisionKey }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.DecisionKey, x.Status });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsConfigurationDecisions_Key", "[DecisionKey] LIKE 'QS-DEC-[0-9][0-9][0-9]'");
                table.HasCheckConstraint("CK_QsConfigurationDecisions_SchemaVersion", "[SchemaVersion] > 0");
                table.HasCheckConstraint("CK_QsConfigurationDecisions_Period", "[EffectiveTo] IS NULL OR [EffectiveFrom] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.HasCheckConstraint("CK_QsConfigurationDecisions_Status", "[Status] IN (0, 1, 2, 3)");
            });
            entity.HasOne(x => x.Profile).WithMany(x => x.Decisions).HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<QuantitySurveyConfigurationDecision>().WithMany().HasForeignKey(x => x.SourceDecisionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<QuantitySurveyConfigurationEvidenceLink>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.DecisionId });
            entity.HasIndex(x => new { x.TenantId, x.DecisionId, x.CentralDocumentVersionId }).IsUnique().HasFilter("[IsDeleted] = 0 AND [CentralDocumentVersionId] IS NOT NULL");
            entity.HasOne(x => x.Profile).WithMany(x => x.EvidenceLinks).HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Decision).WithMany(x => x.EvidenceLinks).HasForeignKey(x => x.DecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CentralDocumentRecord).WithMany().HasForeignKey(x => x.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CentralDocumentVersion).WithMany().HasForeignKey(x => x.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<QuantitySurveyConfigurationRevision>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.ProfileId, x.CreatedAt });
            entity.HasIndex(x => new { x.TenantId, x.CorrelationId });
            entity.HasOne<QuantitySurveyConfigurationProfile>().WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<QuantitySurveyConfigurationDecision>().WithMany().HasForeignKey(x => x.DecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<QuantitySurveyBoqImportSession>(entity =>
        {
            entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.Property(x => x.PreviewTokenHash).IsUnicode(false);
            entity.Property(x => x.FileHash).IsUnicode(false);
            entity.Property(x => x.NormalizedPayloadHash).IsUnicode(false);
            entity.HasIndex(x => new { x.TenantId, x.ProjectId, x.CreatedAt });
            entity.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [IdempotencyKey] IS NOT NULL");
            entity.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FileUploadRecord>().WithMany().HasForeignKey(x => x.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CentralDocumentRecord).WithMany().HasForeignKey(x => x.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CentralDocumentVersion).WithMany().HasForeignKey(x => x.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsBoqImportSessions_Status", "[Status] IN (0, 1, 2, 3, 4)");
                table.HasCheckConstraint("CK_QsBoqImportSessions_Counts", "[LineCount] >= 0 AND [ErrorCount] >= 0 AND [CommittedLineCount] >= 0");
            });
        });

        builder.Entity<QuantitySurveyTenderBoqSubmission>(entity =>
        {
            entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.Property(x => x.FileHash).IsUnicode(false);
            entity.Property(x => x.NormalizedPayloadHash).IsUnicode(false);
            entity.Property(x => x.PreviewTokenHash).IsUnicode(false);
            entity.Property(x => x.TenderBoqSnapshotHash).IsUnicode(false);
            entity.HasIndex(x => new { x.TenantId, x.TenderBidId, x.CreatedAt });
            entity.HasIndex(x => new { x.TenantId, x.TenderBidId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.TenderBoqVersionId });
            entity.HasIndex(x => new { x.TenantId, x.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [IdempotencyKey] IS NOT NULL");
            entity.HasOne(x => x.TenderBid).WithMany().HasForeignKey(x => x.TenderBidId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Tender).WithMany().HasForeignKey(x => x.TenderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.TenderBoqVersion).WithMany().HasForeignKey(x => x.TenderBoqVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.BusinessPartner).WithMany().HasForeignKey(x => x.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.FileUploadRecord).WithMany().HasForeignKey(x => x.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CentralDocumentRecord).WithMany().HasForeignKey(x => x.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CentralDocumentVersion).WithMany().HasForeignKey(x => x.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsTenderBoqSubmissions_Channel", "[Channel] IN (0, 1, 2, 3)");
                table.HasCheckConstraint("CK_QsTenderBoqSubmissions_Status", "[Status] IN (0, 1, 2, 3, 4)");
                table.HasCheckConstraint("CK_QsTenderBoqSubmissions_VettingStatus", "[VettingStatus] IN (0, 1, 2)");
                table.HasCheckConstraint("CK_QsTenderBoqSubmissions_Counts", "[LineCount] >= 0 AND [ErrorCount] >= 0 AND [WarningCount] >= 0 AND [CommittedLineCount] >= 0");
                table.HasCheckConstraint("CK_QsTenderBoqSubmissions_Totals", "[TenderBoqTotal] >= 0 AND [SubmittedTotal] >= 0");
            });
        });

        builder.Entity<QuantitySurveyTenderBoqSubmissionLine>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.SubmissionId, x.TenderItemId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.SubmissionId, x.LineKey }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.TenderBidId });
            entity.HasOne(x => x.Submission).WithMany(x => x.Lines).HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.TenderBid).WithMany().HasForeignKey(x => x.TenderBidId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.TenderItem).WithMany().HasForeignKey(x => x.TenderItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ProjectBoqVersionLine).WithMany().HasForeignKey(x => x.ProjectBoqVersionLineId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsTenderBoqSubmissionLines_RowNumber", "[RowNumber] > 0");
                table.HasCheckConstraint("CK_QsTenderBoqSubmissionLines_Amounts", "[TenderQuantity] >= 0 AND [OfferedQuantity] >= 0 AND [UnitPrice] >= 0 AND [SubmittedLineTotal] >= 0 AND [CalculatedLineTotal] >= 0");
            });
        });

        builder.Entity<QuantitySurveyRateLibraryItem>(entity =>
        {
            entity.Property(value => value.Code).IsUnicode(false);
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(value => new { value.TenantId, value.Code }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.Category, value.IsActive });
            entity.HasIndex(value => new { value.TenantId, value.InventoryItemId });
            entity.HasOne(value => value.UnitOfMeasure).WithMany().HasForeignKey(value => value.UnitOfMeasureId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ProjectCatalogEntry).WithMany().HasForeignKey(value => value.ProjectCatalogEntryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.InventoryItem).WithMany().HasForeignKey(value => value.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_QsRateLibraryItems_Category",
                "[Category] IN (0, 1, 2, 3, 4, 5)"));
        });

        builder.Entity<QuantitySurveyRateLibraryRate>(entity =>
        {
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.Property(value => value.CurrencyCodeSnapshot).IsUnicode(false);
            entity.Property(value => value.MarketAnalysisCodeSnapshot).IsUnicode(false);
            entity.Property(value => value.PreviousCurrencyCodeSnapshot).IsUnicode(false);
            entity.Property(value => value.HistoricalProjectCodeSnapshot).IsUnicode(false);
            entity.Property(value => value.HistoricalUnitOfMeasureSnapshot).IsUnicode(false);
            entity.Property(value => value.HistoricalSourceHash).IsUnicode(false);
            entity.HasIndex(value => value.RateBuildUpId).IsUnique().HasFilter("[RateBuildUpId] IS NOT NULL");
            entity.HasIndex(value => new { value.TenantId, value.RateLibraryItemId, value.Version }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.RateLibraryItemId, value.LifecycleStatus, value.EffectiveFrom });
            entity.HasIndex(value => new { value.TenantId, value.ProjectTypeId, value.LocationId, value.BusinessPartnerId });
            entity.HasIndex(value => new { value.TenantId, value.CentralDocumentVersionId });
            entity.HasIndex(value => new { value.TenantId, value.MarketAnalysisId });
            entity.HasIndex(value => new { value.TenantId, value.NextReviewDueAt });
            entity.HasIndex(value => new { value.TenantId, value.HistoricalProjectId, value.HistoricalSourceType });
            entity.HasIndex(value => new { value.TenantId, value.HistoricalSourceType, value.HistoricalSourceId })
                .IsUnique()
                .HasFilter("[HistoricalSourceType] IS NOT NULL AND [HistoricalSourceId] IS NOT NULL");
            entity.HasOne(value => value.RateLibraryItem).WithMany(value => value.Rates).HasForeignKey(value => value.RateLibraryItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Currency).WithMany().HasForeignKey(value => value.CurrencyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ProjectType).WithMany().HasForeignKey(value => value.ProjectTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Location).WithMany().HasForeignKey(value => value.LocationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.BusinessPartner).WithMany().HasForeignKey(value => value.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentRecord).WithMany().HasForeignKey(value => value.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentVersion).WithMany().HasForeignKey(value => value.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.MarketAnalysis).WithMany().HasForeignKey(value => value.MarketAnalysisId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.PreviousRate).WithMany().HasForeignKey(value => value.PreviousRateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.HistoricalProject).WithMany().HasForeignKey(value => value.HistoricalProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.RateBuildUp).WithOne(value => value.GeneratedRate).HasForeignKey<QuantitySurveyRateLibraryRate>(value => value.RateBuildUpId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsRateLibraryRates_Version", "[Version] > 0");
                table.HasCheckConstraint("CK_QsRateLibraryRates_UnitRate", "[UnitRate] >= 0");
                table.HasCheckConstraint("CK_QsRateLibraryRates_Period", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.HasCheckConstraint("CK_QsRateLibraryRates_Status", "[LifecycleStatus] IN (0, 1, 2)");
                table.HasCheckConstraint("CK_QsRateLibraryRates_Source", "[SourceType] IN (0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12)");
                table.HasCheckConstraint("CK_QsRateLibraryRates_EvidencePair", "([CentralDocumentRecordId] IS NULL AND [CentralDocumentVersionId] IS NULL) OR ([CentralDocumentRecordId] IS NOT NULL AND [CentralDocumentVersionId] IS NOT NULL)");
                table.HasCheckConstraint("CK_QsRateLibraryRates_MarketSurveyLineage", "([SourceType] <> 11 AND [MarketAnalysisId] IS NULL AND [MarketAnalysisCodeSnapshot] IS NULL AND [MarketSurveyQuoteCount] IS NULL AND [NextReviewDueAt] IS NULL) OR ([SourceType] = 11 AND (([MarketAnalysisId] IS NULL AND [MarketAnalysisCodeSnapshot] IS NULL AND [MarketSurveyQuoteCount] IS NULL AND [NextReviewDueAt] IS NULL) OR ([MarketAnalysisId] IS NOT NULL AND [MarketAnalysisCodeSnapshot] IS NOT NULL AND [MarketSurveyQuoteCount] > 0 AND [NextReviewDueAt] IS NOT NULL)))");
                table.HasCheckConstraint("CK_QsRateLibraryRates_PreviousRateSnapshot", "([PreviousRateId] IS NULL AND [PreviousUnitRate] IS NULL AND [PreviousCurrencyCodeSnapshot] IS NULL) OR ([PreviousRateId] IS NOT NULL AND [PreviousUnitRate] IS NOT NULL AND [PreviousCurrencyCodeSnapshot] IS NOT NULL)");
                table.HasCheckConstraint("CK_QsRateLibraryRates_HistoricalLineage", "([SourceType] <> 8 AND [HistoricalSourceType] IS NULL AND [HistoricalSourceId] IS NULL AND [HistoricalProjectId] IS NULL AND [HistoricalProjectCodeSnapshot] IS NULL AND [HistoricalSourceLabelSnapshot] IS NULL AND [HistoricalUnitOfMeasureSnapshot] IS NULL AND [HistoricalQuantity] IS NULL AND [HistoricalTotalAmount] IS NULL AND [HistoricalSourceHash] IS NULL) OR ([SourceType] = 8 AND (([HistoricalSourceType] IS NULL AND [HistoricalSourceId] IS NULL AND [HistoricalProjectId] IS NULL AND [HistoricalProjectCodeSnapshot] IS NULL AND [HistoricalSourceLabelSnapshot] IS NULL AND [HistoricalUnitOfMeasureSnapshot] IS NULL AND [HistoricalQuantity] IS NULL AND [HistoricalTotalAmount] IS NULL AND [HistoricalSourceHash] IS NULL) OR ([HistoricalSourceType] IN (0, 1, 2, 3) AND [HistoricalSourceId] IS NOT NULL AND [HistoricalProjectId] IS NOT NULL AND [HistoricalProjectCodeSnapshot] IS NOT NULL AND [HistoricalSourceLabelSnapshot] IS NOT NULL AND [HistoricalUnitOfMeasureSnapshot] IS NOT NULL AND [HistoricalQuantity] > 0 AND [HistoricalTotalAmount] > 0 AND [HistoricalSourceHash] IS NOT NULL)))");
                table.HasCheckConstraint("CK_QsRateLibraryRates_BuildUpLineage", "([SourceType] <> 12 AND [RateBuildUpId] IS NULL) OR ([SourceType] = 12 AND [RateBuildUpId] IS NOT NULL)");
            });
        });

        builder.Entity<QuantitySurveyRateBuildUp>(entity =>
        {
            entity.Property(value => value.BuildUpNumber).IsUnicode(false);
            entity.Property(value => value.CurrencyCodeSnapshot).IsUnicode(false);
            entity.Property(value => value.CalculationHash).IsUnicode(false);
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(value => new { value.TenantId, value.RateLibraryItemId, value.Version }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ConfigurationDecisionId, value.PreparedAt });
            entity.HasIndex(value => new { value.TenantId, value.CalculationHash });
            entity.HasOne(value => value.RateLibraryItem).WithMany().HasForeignKey(value => value.RateLibraryItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Currency).WithMany().HasForeignKey(value => value.CurrencyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationProfile).WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationDecision).WithMany().HasForeignKey(value => value.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentRecord).WithMany().HasForeignKey(value => value.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentVersion).WithMany().HasForeignKey(value => value.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsRateBuildUps_Version", "[Version] > 0");
                table.HasCheckConstraint("CK_QsRateBuildUps_DecimalPlaces", "[DecimalPlaces] BETWEEN 0 AND 6");
                table.HasCheckConstraint("CK_QsRateBuildUps_Amounts", "[MaterialSubtotal] >= 0 AND [DirectCost] > 0 AND [AddOnCost] >= 0 AND [UnitRate] > 0 AND [MaterialSubtotal] <= [DirectCost]");
                table.HasCheckConstraint("CK_QsRateBuildUps_EvidencePair", "([CentralDocumentRecordId] IS NULL AND [CentralDocumentVersionId] IS NULL) OR ([CentralDocumentRecordId] IS NOT NULL AND [CentralDocumentVersionId] IS NOT NULL)");
            });
        });

        builder.Entity<QuantitySurveyRateBuildUpLine>(entity =>
        {
            entity.Property(value => value.SourceItemCodeSnapshot).IsUnicode(false);
            entity.Property(value => value.SourceUnitOfMeasureSnapshot).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.RateBuildUpId, value.Sequence }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.SourceRateId });
            entity.HasOne(value => value.RateBuildUp).WithMany(value => value.Lines).HasForeignKey(value => value.RateBuildUpId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.SourceRateLibraryItem).WithMany().HasForeignKey(value => value.SourceRateLibraryItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.SourceRate).WithMany().HasForeignKey(value => value.SourceRateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsRateBuildUpLines_Sequence", "[Sequence] > 0");
                table.HasCheckConstraint("CK_QsRateBuildUpLines_Component", "[Component] BETWEEN 0 AND 11");
                table.HasCheckConstraint("CK_QsRateBuildUpLines_Method", "[CalculationMethod] BETWEEN 0 AND 2");
                table.HasCheckConstraint("CK_QsRateBuildUpLines_PercentageBasis", "[PercentageBasis] IS NULL OR [PercentageBasis] BETWEEN 0 AND 2");
                table.HasCheckConstraint("CK_QsRateBuildUpLines_Amounts", "[BasisAmount] >= 0 AND [CalculatedAmount] > 0");
                table.HasCheckConstraint("CK_QsRateBuildUpLines_InputShape", "([CalculationMethod] = 0 AND [SourceRateLibraryItemId] IS NOT NULL AND [SourceRateId] IS NOT NULL AND [SourceItemCodeSnapshot] IS NOT NULL AND [SourceItemNameSnapshot] IS NOT NULL AND [SourceUnitOfMeasureSnapshot] IS NOT NULL AND [SourceRateVersion] > 0 AND [SourceUnitRate] > 0 AND [InputQuantity] > 0 AND [InputPercentage] IS NULL AND [InputFixedAmount] IS NULL AND [PercentageBasis] IS NULL) OR ([CalculationMethod] = 1 AND [SourceRateLibraryItemId] IS NULL AND [SourceRateId] IS NULL AND [SourceItemCodeSnapshot] IS NULL AND [SourceItemNameSnapshot] IS NULL AND [SourceUnitOfMeasureSnapshot] IS NULL AND [SourceRateVersion] IS NULL AND [SourceUnitRate] IS NULL AND [InputQuantity] IS NULL AND [InputPercentage] > 0 AND [InputFixedAmount] IS NULL AND [PercentageBasis] IS NOT NULL) OR ([CalculationMethod] = 2 AND [SourceRateLibraryItemId] IS NULL AND [SourceRateId] IS NULL AND [SourceItemCodeSnapshot] IS NULL AND [SourceItemNameSnapshot] IS NULL AND [SourceUnitOfMeasureSnapshot] IS NULL AND [SourceRateVersion] IS NULL AND [SourceUnitRate] IS NULL AND [InputQuantity] IS NULL AND [InputPercentage] IS NULL AND [InputFixedAmount] > 0 AND [PercentageBasis] IS NULL)");
            });
        });

        builder.Entity<QuantitySurveyRateLibraryRevision>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.RateLibraryItemId, value.CreatedAt });
            entity.HasIndex(value => new { value.TenantId, value.CorrelationId });
            entity.HasOne(value => value.RateLibraryItem).WithMany().HasForeignKey(value => value.RateLibraryItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Rate).WithMany().HasForeignKey(value => value.RateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<QuantitySurveyMaterialReconciliation>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.PolicyHash).IsUnicode(false);
            entity.Property(value => value.ContractorConfirmationHash).IsUnicode(false);
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(value => new { value.TenantId, value.ValuationWorksheetId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ProjectId, value.ContractId, value.Status });
            entity.HasOne(value => value.Project).WithMany().HasForeignKey(value => value.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Contract).WithMany().HasForeignKey(value => value.ContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ContractorBusinessPartner).WithMany().HasForeignKey(value => value.ContractorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ValuationWorksheet).WithMany().HasForeignKey(value => value.ValuationWorksheetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationProfile).WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.MaterialDecision).WithMany().HasForeignKey(value => value.MaterialDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Workflow.WorkflowDefinition>().WithMany().HasForeignKey(value => value.ApprovalWorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.PreparedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ContractorConfirmedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.SubmittedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0507_MaterialReconciliation_Governance");
                table.HasCheckConstraint("CK_QsMaterialReconciliations_Status", "[Status] IN ('Draft','ContractorConfirmed','PendingApproval','Approved','Rejected')");
                table.HasCheckConstraint("CK_QsMaterialReconciliations_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                table.HasCheckConstraint("CK_QsMaterialReconciliations_Amounts", "[MaterialOnSiteAmount] >= 0 AND [MaterialOffSiteAmount] >= 0 AND [TdcSuppliedDeductionAmount] >= 0");
                table.HasCheckConstraint("CK_QsMaterialReconciliations_Lifecycle", "([Status] = 'Draft' AND [ApprovalStatus] = 'Draft' AND [ContractorConfirmedAt] IS NULL AND [WorkflowInstanceId] IS NULL) OR ([Status] = 'ContractorConfirmed' AND [ApprovalStatus] = 'Draft' AND [ContractorConfirmedById] IS NOT NULL AND [ContractorConfirmedAt] IS NOT NULL AND LEN([ContractorConfirmationHash]) = 64 AND [WorkflowInstanceId] IS NULL) OR ([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending' AND [ContractorConfirmedAt] IS NOT NULL AND [SubmittedById] IS NOT NULL AND [SubmittedAt] IS NOT NULL AND [WorkflowInstanceId] IS NOT NULL) OR ([Status] = 'Approved' AND [ApprovalStatus] = 'Approved' AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [WorkflowInstanceId] IS NOT NULL) OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected' AND [RejectionReason] IS NOT NULL)");
            });
        });

        builder.Entity<QuantitySurveyMaterialReconciliationLine>(entity =>
        {
            entity.Property(value => value.SourceHash).IsUnicode(false);
            entity.Property(value => value.IssueVoucherIntegrityHashSnapshot).IsUnicode(false);
            entity.Property(value => value.EvidenceChecksumSnapshot).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ReconciliationId, value.Sequence }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.InventoryIssueVoucherLineId }).IsUnique()
                .HasFilter("[InventoryIssueVoucherLineId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(value => new { value.TenantId, value.ValuationEvidenceId }).IsUnique()
                .HasFilter("[ValuationEvidenceId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasOne(value => value.Reconciliation).WithMany(value => value.Lines).HasForeignKey(value => value.ReconciliationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.InventoryItem).WithMany().HasForeignKey(value => value.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ApprovedRate).WithMany().HasForeignKey(value => value.ApprovedRateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.InventoryIssueVoucherLine).WithMany().HasForeignKey(value => value.InventoryIssueVoucherLineId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ValuationEvidence).WithMany().HasForeignKey(value => value.ValuationEvidenceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0507_MaterialReconciliationLines_Governance");
                table.HasCheckConstraint("CK_QsMaterialReconciliationLines_Type", "[LineType] BETWEEN 0 AND 2");
                table.HasCheckConstraint("CK_QsMaterialReconciliationLines_Amounts", "[Sequence] > 0 AND [Quantity] > 0 AND [AppliedUnitRate] > 0 AND [TotalValue] > 0 AND LEN([SourceHash]) = 64");
                table.HasCheckConstraint("CK_QsMaterialReconciliationLines_Source", "([LineType] IN (0,1) AND [InventoryIssueVoucherLineId] IS NULL AND [ValuationEvidenceId] IS NOT NULL AND [CentralDocumentRecordIdSnapshot] IS NOT NULL AND [CentralDocumentVersionIdSnapshot] IS NOT NULL AND LEN([EvidenceChecksumSnapshot]) = 64) OR ([LineType] = 2 AND [InventoryIssueVoucherLineId] IS NOT NULL AND [ValuationEvidenceId] IS NULL AND [CentralDocumentRecordIdSnapshot] IS NULL AND [CentralDocumentVersionIdSnapshot] IS NULL AND [EvidenceChecksumSnapshot] IS NULL AND [IssueVoucherNumberSnapshot] IS NOT NULL AND LEN([IssueVoucherIntegrityHashSnapshot]) = 64)");
            });
        });

        builder.Entity<QuantitySurveyMaterialReconciliationRevision>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ReconciliationId, value.CreatedAt });
            entity.HasOne(value => value.Reconciliation).WithMany(value => value.Revisions).HasForeignKey(value => value.ReconciliationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0507_MaterialReconciliationRevisions_AppendOnly");
                table.HasCheckConstraint("CK_QsMaterialReconciliationRevisions_Request", "[ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND LEN([RequestHash]) = 64");
            });
        });

        builder.Entity<ProjectVariationOrder>(entity =>
        {
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.LastMutationRequestHash).IsUnicode(false);
            entity.Property(value => value.PolicyHash).IsUnicode(false);
            entity.Property(value => value.ApplicationRequestHash).IsUnicode(false);
            entity.Property(value => value.ApplicationHash).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique()
                .HasFilter("[IsQuantitySurveyGoverned] = 1 AND [ClientRequestId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(value => new { value.TenantId, value.ReferenceNumber }).IsUnique()
                .HasFilter("[IsQuantitySurveyGoverned] = 1 AND [ReferenceNumber] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(value => new { value.TenantId, value.ApplicationClientRequestId }).IsUnique()
                .HasFilter("[IsQuantitySurveyGoverned] = 1 AND [ApplicationClientRequestId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(value => new { value.TenantId, value.ContractAmendmentId }).IsUnique()
                .HasFilter("[ContractAmendmentId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(value => new { value.TenantId, value.RevisedBoqVersionId }).IsUnique()
                .HasFilter("[RevisedBoqVersionId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(value => new { value.TenantId, value.BudgetRevisionId }).IsUnique()
                .HasFilter("[BudgetRevisionId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(value => new { value.TenantId, value.ForecastVersionId }).IsUnique()
                .HasFilter("[ForecastVersionId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasOne(value => value.SiteInstruction).WithMany().HasForeignKey(value => value.SiteInstructionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ChangeRequest).WithMany().HasForeignKey(value => value.ChangeRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ContractorBusinessPartner).WithMany().HasForeignKey(value => value.ContractorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ApprovedBoqVersion).WithMany().HasForeignKey(value => value.ApprovedBoqVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ConfigurationProfile).WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.VariationDecision).WithMany().HasForeignKey(value => value.VariationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ContractAmendment).WithMany().HasForeignKey(value => value.ContractAmendmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.RevisedBoqVersion).WithMany().HasForeignKey(value => value.RevisedBoqVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.BudgetRevision).WithMany().HasForeignKey(value => value.BudgetRevisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ForecastVersion).WithMany().HasForeignKey(value => value.ForecastVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0508_ProjectVariation_Governance");
                table.HasTrigger("TR_QS0511_VariationApplication_Governance");
                table.HasCheckConstraint("CK_QsVariation_Source", "[IsQuantitySurveyGoverned] = 0 OR (([SiteInstructionId] IS NOT NULL AND [ChangeRequestId] IS NULL AND [VariationSourceType] = 0) OR ([SiteInstructionId] IS NULL AND [ChangeRequestId] IS NOT NULL AND [VariationSourceType] = 1) OR ([SiteInstructionId] IS NULL AND [ChangeRequestId] IS NULL AND [VariationSourceType] IN (2,3)))");
                table.HasCheckConstraint("CK_QsVariation_Governance", "[IsQuantitySurveyGoverned] = 0 OR ([ContractId] IS NOT NULL AND [ContractorBusinessPartnerId] IS NOT NULL AND [ApprovedBoqVersionId] IS NOT NULL AND [ConfigurationProfileId] IS NOT NULL AND [VariationDecisionId] IS NOT NULL AND [ApprovalWorkflowDefinitionId] IS NOT NULL AND [EvidenceMetadataTemplateId] IS NOT NULL AND [PreparedById] IS NOT NULL AND LEN([PolicyHash]) = 64 AND LEN([RequestHash]) = 64)");
                table.HasCheckConstraint("CK_QsVariation_Application", "[IsQuantitySurveyGoverned] = 0 OR (([DownstreamApplicationStatus] = 'NotApplied' AND [ApplicationClientRequestId] IS NULL AND [ApplicationRequestHash] IS NULL AND [ApplicationHash] IS NULL AND [AppliedById] IS NULL AND [AppliedAt] IS NULL AND [ContractAmendmentId] IS NULL AND [RevisedBoqVersionId] IS NULL AND [BudgetRevisionId] IS NULL AND [ForecastVersionId] IS NULL) OR ([DownstreamApplicationStatus] IN ('AppliedPendingBoqApproval','Applied') AND [Status] IN ('Approved','Implemented','Closed') AND [ApplicationClientRequestId] IS NOT NULL AND LEN([ApplicationRequestHash]) = 64 AND LEN([ApplicationHash]) = 64 AND [AppliedById] IS NOT NULL AND [AppliedAt] IS NOT NULL AND [ContractAmendmentId] IS NOT NULL AND [RevisedBoqVersionId] IS NOT NULL AND (([UpdateBudgetOnApplication] = 1 AND [BudgetRevisionId] IS NOT NULL) OR ([UpdateBudgetOnApplication] = 0 AND [BudgetRevisionId] IS NULL)) AND (([UpdateForecastOnApplication] = 1 AND [ForecastVersionId] IS NOT NULL) OR ([UpdateForecastOnApplication] = 0 AND [ForecastVersionId] IS NULL))))");
            });
        });

        builder.Entity<QuantitySurveyVariationValuationLine>(entity =>
        {
            entity.Property(value => value.SourceHash).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.VariationOrderId, value.Sequence }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.VariationOrderId, value.ProjectBoqVersionLineId }).IsUnique();
            entity.HasOne(value => value.VariationOrder).WithMany(value => value.ValuationLines).HasForeignKey(value => value.VariationOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ProjectBoqVersionLine).WithMany().HasForeignKey(value => value.ProjectBoqVersionLineId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0508_VariationLines_Governance");
                table.HasCheckConstraint("CK_QsVariationLine_Value", "[Sequence] > 0 AND [QuantityChange] <> 0 AND [UnitRate] >= 0 AND [Amount] = ROUND([QuantityChange] * [UnitRate], 2) AND LEN([SourceHash]) = 64");
            });
        });

        builder.Entity<QuantitySurveyVariationEvidence>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.ChecksumSha256).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.VariationOrderId, value.CreatedAt });
            entity.HasOne(value => value.VariationOrder).WithMany(value => value.VariationEvidence).HasForeignKey(value => value.VariationOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasTrigger("TR_QS0508_VariationEvidence_AppendOnly"));
        });

        builder.Entity<QuantitySurveyVariationRevision>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.VariationOrderId, value.CreatedAt });
            entity.HasOne(value => value.VariationOrder).WithMany(value => value.VariationRevisions).HasForeignKey(value => value.VariationOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasTrigger("TR_QS0508_VariationRevisions_AppendOnly"));
        });

        builder.Entity<QuantitySurveyContractClaim>(entity =>
        {
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.LastMutationRequestHash).IsUnicode(false);
            entity.Property(value => value.PolicyHash).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.LastMutationClientRequestId }).IsUnique()
                .HasFilter("[LastMutationClientRequestId] IS NOT NULL");
            entity.HasIndex(value => new { value.TenantId, value.ClaimNumber }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ProjectId, value.Status, value.CreatedAt });
            entity.HasIndex(value => new { value.TenantId, value.ContractId, value.ContractorBusinessPartnerId });
            entity.HasOne(value => value.Project).WithMany().HasForeignKey(value => value.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Contract).WithMany().HasForeignKey(value => value.ContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ContractorBusinessPartner).WithMany().HasForeignKey(value => value.ContractorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Procurement.BusinessPartner>().WithMany().HasForeignKey(value => value.SubmittedBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ApprovedBoqVersion).WithMany().HasForeignKey(value => value.ApprovedBoqVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.VariationOrder).WithMany().HasForeignKey(value => value.VariationOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ExtensionOfTime).WithMany().HasForeignKey(value => value.ExtensionOfTimeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<QuantitySurveyConfigurationProfile>().WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<QuantitySurveyConfigurationDecision>().WithMany().HasForeignKey(value => value.VariationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Workflow.WorkflowDefinition>().WithMany().HasForeignKey(value => value.ApprovalWorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.DocumentManagement.CentralDocumentMetadataTemplate>().WithMany().HasForeignKey(value => value.EvidenceMetadataTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.SubmittedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.QsVettedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0509_ContractClaims_Governance");
                table.HasCheckConstraint("CK_QsContractClaims_Type", "[ClaimType] BETWEEN 0 AND 5");
                table.HasCheckConstraint("CK_QsContractClaims_Status", "[Status] IN ('Draft','Submitted','Vetted','PendingApproval','Approved','Rejected')");
                table.HasCheckConstraint("CK_QsContractClaims_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                table.HasCheckConstraint("CK_QsContractClaims_StateAlignment", "([Status] IN ('Draft','Submitted','Vetted') AND [ApprovalStatus] = 'Draft') OR ([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending') OR ([Status] = 'Approved' AND [ApprovalStatus] = 'Approved') OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected')");
                table.HasCheckConstraint("CK_QsContractClaims_Source", "([ClaimType] = 0 AND [ExtensionOfTimeId] IS NOT NULL AND [VariationOrderId] IS NULL) OR ([ClaimType] IN (2,3,4) AND [VariationOrderId] IS NOT NULL AND [ExtensionOfTimeId] IS NULL) OR ([ClaimType] IN (1,5) AND [VariationOrderId] IS NULL AND [ExtensionOfTimeId] IS NULL)");
                table.HasCheckConstraint("CK_QsContractClaims_Amounts", "[ClaimedAmount] > 0 AND ([QsAssessedAmount] IS NULL OR ([QsAssessedAmount] >= 0 AND [QsAssessedAmount] <= [ClaimedAmount])) AND ([ApprovedAmount] IS NULL OR ([ApprovedAmount] >= 0 AND [ApprovedAmount] <= [ClaimedAmount])) AND ([RejectedAmount] IS NULL OR ([RejectedAmount] >= 0 AND [RejectedAmount] <= [ClaimedAmount])) AND [SettledAmount] >= 0 AND ([ApprovedAmount] IS NULL OR [SettledAmount] <= [ApprovedAmount])");
                table.HasCheckConstraint("CK_QsContractClaims_Hashes", "LEN([RequestHash]) = 64 AND LEN([PolicyHash]) = 64 AND ([LastMutationRequestHash] IS NULL OR LEN([LastMutationRequestHash]) = 64)");
                table.HasCheckConstraint("CK_QsContractClaims_Lifecycle", "([Status] = 'Draft' AND [WorkflowInstanceId] IS NULL AND [SubmittedAt] IS NULL AND [QsVettedAt] IS NULL AND [ApprovedAt] IS NULL) OR ([Status] = 'Submitted' AND [WorkflowInstanceId] IS NULL AND [SubmittedById] IS NOT NULL AND [SubmittedAt] IS NOT NULL AND [QsVettedAt] IS NULL AND [ApprovedAt] IS NULL) OR ([Status] = 'Vetted' AND [WorkflowInstanceId] IS NULL AND [SubmittedById] IS NOT NULL AND [SubmittedAt] IS NOT NULL AND [QsVettedById] IS NOT NULL AND [QsVettedAt] IS NOT NULL AND [QsAssessedAmount] IS NOT NULL AND [ApprovedAt] IS NULL) OR ([Status] = 'PendingApproval' AND [WorkflowInstanceId] IS NOT NULL AND [QsVettedAt] IS NOT NULL AND [ApprovedAt] IS NULL) OR ([Status] = 'Approved' AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [ApprovedAmount] IS NOT NULL) OR ([Status] = 'Rejected' AND [WorkflowInstanceId] IS NOT NULL AND [RejectionReason] IS NOT NULL)");
                table.HasCheckConstraint("CK_QsContractClaims_Dispute", "[DisputeStatus] BETWEEN 0 AND 3 AND ([DisputeStatus] = 0 OR [Status] IN ('Approved','Rejected')) AND ([DisputeStatus] = 0 OR LEN(LTRIM(RTRIM([DisputeReason]))) > 0) AND ([DisputeStatus] IN (0,1) OR LEN(LTRIM(RTRIM([DisputeResolution]))) > 0)");
                table.HasCheckConstraint("CK_QsContractClaims_Settlement", "[SettlementStatus] BETWEEN 0 AND 3 AND ([SettlementStatus] = 0 OR [Status] = 'Approved') AND ([SettlementStatus] IN (0,1) OR ([SettledAmount] > 0 AND [SettlementReference] IS NOT NULL AND [SettlementDate] IS NOT NULL)) AND ([SettlementStatus] <> 3 OR [SettledAmount] = [ApprovedAmount])");
            });
        });

        builder.Entity<QuantitySurveyContractClaimEvidence>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.ChecksumSha256).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.CentralDocumentVersionId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ContractClaimId, value.CreatedAt });
            entity.HasOne(value => value.ContractClaim).WithMany(value => value.Evidence).HasForeignKey(value => value.ContractClaimId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.FileUploadRecord>().WithMany().HasForeignKey(value => value.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.DocumentManagement.CentralDocumentRecord>().WithMany().HasForeignKey(value => value.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.DocumentManagement.CentralDocumentVersion>().WithMany().HasForeignKey(value => value.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0509_ContractClaimEvidence_AppendOnly");
                table.HasCheckConstraint("CK_QsContractClaimEvidence_File", "[FileSize] > 0 AND LEN([ChecksumSha256]) = 64 AND LEN([RequestHash]) = 64");
            });
        });

        builder.Entity<QuantitySurveyContractClaimRevision>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ContractClaimId, value.CreatedAt });
            entity.HasIndex(value => new { value.TenantId, value.CorrelationId });
            entity.HasOne(value => value.ContractClaim).WithMany(value => value.Revisions).HasForeignKey(value => value.ContractClaimId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Procurement.BusinessPartner>().WithMany().HasForeignKey(value => value.ActorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0509_ContractClaimRevisions_AppendOnly");
                table.HasCheckConstraint("CK_QsContractClaimRevisions_Request", "[ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND LEN([RequestHash]) = 64");
            });
        });

        builder.Entity<QuantitySurveyDayworkSheet>(entity =>
        {
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.LastMutationRequestHash).IsUnicode(false);
            entity.Property(value => value.PolicyHash).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.LastMutationClientRequestId }).IsUnique().HasFilter("[LastMutationClientRequestId] IS NOT NULL");
            entity.HasIndex(value => new { value.TenantId, value.SheetNumber }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.VariationOrderId, value.WorkDate });
            entity.HasOne(value => value.Project).WithMany().HasForeignKey(value => value.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.VariationOrder).WithMany().HasForeignKey(value => value.VariationOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Contract).WithMany().HasForeignKey(value => value.ContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ContractorBusinessPartner).WithMany().HasForeignKey(value => value.ContractorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<QuantitySurveyConfigurationProfile>().WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<QuantitySurveyConfigurationDecision>().WithMany().HasForeignKey(value => value.VariationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.DocumentManagement.CentralDocumentMetadataTemplate>().WithMany().HasForeignKey(value => value.EvidenceMetadataTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ContractorSignedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.VerifiedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0510_DayworkSheets_Governance");
                table.HasCheckConstraint("CK_QsDayworkSheets_Status", "[Status] BETWEEN 0 AND 3");
                table.HasCheckConstraint("CK_QsDayworkSheets_Amounts", "[TotalAmount] >= 0");
                table.HasCheckConstraint("CK_QsDayworkSheets_Hashes", "LEN([RequestHash]) = 64 AND LEN([PolicyHash]) = 64 AND ([LastMutationRequestHash] IS NULL OR LEN([LastMutationRequestHash]) = 64)");
                table.HasCheckConstraint("CK_QsDayworkSheets_Lifecycle", "([Status] = 0 AND [ContractorSignedById] IS NULL AND [ContractorSignedAt] IS NULL AND [ContractorSignatureHash] IS NULL AND [VerifiedById] IS NULL AND [VerifiedAt] IS NULL AND [VerifierSignatureHash] IS NULL AND [RejectionReason] IS NULL) OR ([Status] = 1 AND [ContractorSignedById] IS NOT NULL AND [ContractorSignedAt] IS NOT NULL AND LEN([ContractorSignatureHash]) = 64 AND [VerifiedById] IS NULL AND [VerifiedAt] IS NULL AND [VerifierSignatureHash] IS NULL AND [RejectionReason] IS NULL) OR ([Status] = 2 AND [ContractorSignedById] IS NOT NULL AND [ContractorSignedAt] IS NOT NULL AND LEN([ContractorSignatureHash]) = 64 AND [VerifiedById] IS NOT NULL AND [VerifiedAt] IS NOT NULL AND LEN([VerifierSignatureHash]) = 64 AND [RejectionReason] IS NULL) OR ([Status] = 3 AND [ContractorSignedById] IS NOT NULL AND [ContractorSignedAt] IS NOT NULL AND LEN([ContractorSignatureHash]) = 64 AND [VerifiedById] IS NOT NULL AND [VerifiedAt] IS NOT NULL AND LEN([VerifierSignatureHash]) = 64 AND LEN(LTRIM(RTRIM([RejectionReason]))) >= 5)");
            });
        });

        builder.Entity<QuantitySurveyDayworkLine>(entity =>
        {
            entity.Property(value => value.SourceHash).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.DayworkSheetId, value.Sequence }).IsUnique();
            entity.HasOne(value => value.DayworkSheet).WithMany(value => value.Lines).HasForeignKey(value => value.DayworkSheetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.RateLibraryRate).WithMany().HasForeignKey(value => value.RateLibraryRateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Inventory.UnitOfMeasure>().WithMany().HasForeignKey(value => value.UnitOfMeasureId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0510_DayworkLines_Governance");
                table.HasCheckConstraint("CK_QsDayworkLines_Type", "[LineType] BETWEEN 0 AND 2");
                table.HasCheckConstraint("CK_QsDayworkLines_Amounts", "[Quantity] > 0 AND [UnitRate] >= 0 AND [Amount] = ROUND([Quantity] * [UnitRate], 2) AND LEN([SourceHash]) = 64");
            });
        });

        builder.Entity<QuantitySurveyDayworkEvidence>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.ChecksumSha256).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.CentralDocumentVersionId }).IsUnique();
            entity.HasOne(value => value.DayworkSheet).WithMany(value => value.Evidence).HasForeignKey(value => value.DayworkSheetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.FileUploadRecord>().WithMany().HasForeignKey(value => value.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.DocumentManagement.CentralDocumentRecord>().WithMany().HasForeignKey(value => value.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.DocumentManagement.CentralDocumentVersion>().WithMany().HasForeignKey(value => value.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0510_DayworkEvidence_AppendOnly");
                table.HasCheckConstraint("CK_QsDayworkEvidence_File", "[FileSize] > 0 AND LEN([ChecksumSha256]) = 64 AND LEN([RequestHash]) = 64");
            });
        });

        builder.Entity<QuantitySurveyDayworkRevision>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.DayworkSheetId, value.CreatedAt });
            entity.HasOne(value => value.DayworkSheet).WithMany(value => value.Revisions).HasForeignKey(value => value.DayworkSheetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Procurement.BusinessPartner>().WithMany().HasForeignKey(value => value.ActorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0510_DayworkRevisions_AppendOnly");
                table.HasCheckConstraint("CK_QsDayworkRevisions_Request", "[ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND LEN([RequestHash]) = 64");
            });
        });

        builder.Entity<QuantitySurveySubcontract>(entity =>
        {
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.LastMutationRequestHash).IsUnicode(false);
            entity.Property(value => value.PolicyHash).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.LastMutationClientRequestId }).IsUnique().HasFilter("[LastMutationClientRequestId] IS NOT NULL");
            entity.HasIndex(value => new { value.TenantId, value.SubcontractNumber }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ProjectId, value.ContractId, value.SubcontractorBusinessPartnerId });
            entity.HasOne(value => value.Project).WithMany().HasForeignKey(value => value.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Contract).WithMany().HasForeignKey(value => value.ContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.SubcontractorBusinessPartner).WithMany().HasForeignKey(value => value.SubcontractorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Finance.PaymentTerm>().WithMany().HasForeignKey(value => value.PaymentTermId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<QuantitySurveyConfigurationProfile>().WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<QuantitySurveyConfigurationDecision>().WithMany().HasForeignKey(value => value.ContractControlsDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Workflow.WorkflowDefinition>().WithMany().HasForeignKey(value => value.ApprovalWorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.PreparedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.SubmittedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ClosedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0521_Subcontracts_Governance");
                table.HasCheckConstraint("CK_QsSubcontracts_Status", "[Status] IN ('Draft','PendingApproval','Approved','Rejected','Closed')");
                table.HasCheckConstraint("CK_QsSubcontracts_Amounts", "[SubcontractValue] > 0 AND [RetentionPercentage] BETWEEN 0 AND 100");
                table.HasCheckConstraint("CK_QsSubcontracts_Dates", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
                table.HasCheckConstraint("CK_QsSubcontracts_Hashes", "LEN([RequestHash]) = 64 AND LEN([PolicyHash]) = 64 AND ([LastMutationRequestHash] IS NULL OR LEN([LastMutationRequestHash]) = 64)");
                table.HasCheckConstraint("CK_QsSubcontracts_Lifecycle", "([Status] = 'Draft' AND [ApprovalStatus] = 'Draft' AND [WorkflowInstanceId] IS NULL AND [ApprovedAt] IS NULL) OR ([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending' AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedAt] IS NULL) OR ([Status] IN ('Approved','Closed') AND [ApprovalStatus] = 'Approved' AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL) OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected' AND [WorkflowInstanceId] IS NOT NULL AND [RejectionReason] IS NOT NULL)");
                table.HasCheckConstraint("CK_QsSubcontracts_Closure", "[Status] <> 'Closed' OR ([ClosedById] IS NOT NULL AND [ClosedAt] IS NOT NULL AND LEN(LTRIM(RTRIM([ClosureNote]))) >= 5)");
            });
        });

        builder.Entity<QuantitySurveySubcontractValuation>(entity =>
        {
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.LastMutationRequestHash).IsUnicode(false);
            entity.Property(value => value.PolicyHash).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.LastMutationClientRequestId }).IsUnique().HasFilter("[LastMutationClientRequestId] IS NOT NULL");
            entity.HasIndex(value => new { value.TenantId, value.ValuationNumber }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.SubcontractId, value.ValuationDate });
            entity.HasIndex(value => new { value.TenantId, value.PaymentCertificateId }).IsUnique().HasFilter("[PaymentCertificateId] IS NOT NULL");
            entity.HasOne(value => value.Subcontract).WithMany(value => value.Valuations).HasForeignKey(value => value.SubcontractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.PaymentCertificate).WithMany().HasForeignKey(value => value.PaymentCertificateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<QuantitySurveyConfigurationProfile>().WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<QuantitySurveyConfigurationDecision>().WithMany().HasForeignKey(value => value.ValuationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Workflow.WorkflowDefinition>().WithMany().HasForeignKey(value => value.ApprovalWorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.DocumentManagement.CentralDocumentMetadataTemplate>().WithMany().HasForeignKey(value => value.EvidenceMetadataTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.SubmittedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Procurement.BusinessPartner>().WithMany().HasForeignKey(value => value.SubmittedBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.AssessedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0521_SubcontractValuations_Governance");
                table.HasCheckConstraint("CK_QsSubcontractValuations_Status", "[Status] IN ('Draft','Submitted','PendingApproval','Approved','Rejected','Paid')");
                table.HasCheckConstraint("CK_QsSubcontractValuations_Amounts", "[ClaimedToDateAmount] > 0 AND ([AssessedToDateAmount] IS NULL OR [AssessedToDateAmount] BETWEEN [PreviouslyCertifiedAmount] AND [ClaimedToDateAmount]) AND [CurrentCertifiedAmount] >= 0 AND [RetentionHeldAmount] >= 0 AND [RetentionReleasedAmount] >= 0 AND [ApprovedBackChargeAmount] >= 0 AND [ApprovedContraChargeAmount] >= 0 AND [TaxAmount] >= 0 AND [NetCertifiedAmount] >= 0");
                table.HasCheckConstraint("CK_QsSubcontractValuations_Hashes", "LEN([RequestHash]) = 64 AND LEN([PolicyHash]) = 64 AND ([LastMutationRequestHash] IS NULL OR LEN([LastMutationRequestHash]) = 64)");
                table.HasCheckConstraint("CK_QsSubcontractValuations_Lifecycle", "([Status] = 'Draft' AND [ApprovalStatus] = 'Draft' AND [WorkflowInstanceId] IS NULL) OR ([Status] = 'Submitted' AND [ApprovalStatus] = 'Draft' AND [SubmittedAt] IS NOT NULL AND [WorkflowInstanceId] IS NULL) OR ([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending' AND [AssessedAt] IS NOT NULL AND [WorkflowInstanceId] IS NOT NULL) OR ([Status] IN ('Approved','Paid') AND [ApprovalStatus] = 'Approved' AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [PaymentCertificateId] IS NOT NULL) OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected' AND [WorkflowInstanceId] IS NOT NULL AND [RejectionReason] IS NOT NULL)");
            });
        });

        builder.Entity<QuantitySurveySubcontractEvidence>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.ChecksumSha256).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.CentralDocumentVersionId }).IsUnique();
            entity.HasOne(value => value.Subcontract).WithMany(value => value.Evidence).HasForeignKey(value => value.SubcontractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Valuation).WithMany(value => value.Evidence).HasForeignKey(value => value.ValuationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.FileUploadRecord>().WithMany().HasForeignKey(value => value.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.DocumentManagement.CentralDocumentRecord>().WithMany().HasForeignKey(value => value.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.DocumentManagement.CentralDocumentVersion>().WithMany().HasForeignKey(value => value.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0521_SubcontractEvidence_AppendOnly");
                table.HasCheckConstraint("CK_QsSubcontractEvidence_File", "[FileSize] > 0 AND LEN([ChecksumSha256]) = 64 AND LEN([RequestHash]) = 64 AND [EvidenceType] IN ('Agreement','Valuation') AND (([EvidenceType] = 'Agreement' AND [ValuationId] IS NULL) OR ([EvidenceType] = 'Valuation' AND [ValuationId] IS NOT NULL))");
            });
        });

        builder.Entity<QuantitySurveySubcontractRevision>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.SubcontractId, value.CreatedAt });
            entity.HasOne(value => value.Subcontract).WithMany().HasForeignKey(value => value.SubcontractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Valuation).WithMany().HasForeignKey(value => value.ValuationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Procurement.BusinessPartner>().WithMany().HasForeignKey(value => value.ActorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0521_SubcontractRevisions_AppendOnly");
                table.HasCheckConstraint("CK_QsSubcontractRevisions_Request", "[ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND LEN([RequestHash]) = 64");
            });
        });

        builder.Entity<QuantitySurveySubcontractChargeNotice>(entity =>
        {
            entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.LastMutationRequestHash).IsUnicode(false);
            entity.Property(value => value.PolicyHash).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.LastMutationClientRequestId }).IsUnique().HasFilter("[LastMutationClientRequestId] IS NOT NULL");
            entity.HasIndex(value => new { value.TenantId, value.NoticeNumber }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.SubcontractId, value.Status, value.NoticeDate });
            entity.HasIndex(value => new { value.TenantId, value.AppliedValuationId, value.Status });
            entity.HasOne(value => value.Subcontract).WithMany(value => value.ChargeNotices).HasForeignKey(value => value.SubcontractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.AppliedValuation).WithMany(value => value.ChargeNotices).HasForeignKey(value => value.AppliedValuationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.RespondedByBusinessPartner).WithMany().HasForeignKey(value => value.RespondedByBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<QuantitySurveyConfigurationProfile>().WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<QuantitySurveyConfigurationDecision>().WithMany().HasForeignKey(value => value.ContractControlsDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Workflow.WorkflowDefinition>().WithMany().HasForeignKey(value => value.ApprovalWorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.DocumentManagement.CentralDocumentMetadataTemplate>().WithMany().HasForeignKey(value => value.EvidenceMetadataTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.PreparedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.IssuedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.SubmittedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0522_SubcontractCharges_Governance");
                table.HasCheckConstraint("CK_QsSubcontractCharges_Type", "[ChargeType] IN ('BackCharge','ContraCharge')");
                table.HasCheckConstraint("CK_QsSubcontractCharges_Status", "[Status] IN ('Draft','Issued','Responded','PendingApproval','Approved','Rejected','Allocated','Applied') AND [ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                table.HasCheckConstraint("CK_QsSubcontractCharges_Response", "[ResponseStatus] IN ('Pending','Accepted','Disputed','NoResponse')");
                table.HasCheckConstraint("CK_QsSubcontractCharges_Amounts", "[ProposedAmount] > 0 AND ([ApprovedAmount] IS NULL OR ([ApprovedAmount] > 0 AND [ApprovedAmount] <= [ProposedAmount]))");
                table.HasCheckConstraint("CK_QsSubcontractCharges_Dates", "[ResponseDueDate] >= [NoticeDate] AND [ResponseDueDate] <= DATEADD(day, 90, [NoticeDate])");
                table.HasCheckConstraint("CK_QsSubcontractCharges_Hashes", "LEN([RequestHash]) = 64 AND LEN([PolicyHash]) = 64 AND ([LastMutationRequestHash] IS NULL OR LEN([LastMutationRequestHash]) = 64)");
                table.HasCheckConstraint("CK_QsSubcontractCharges_Lifecycle", "([Status] = 'Draft' AND [ApprovalStatus] = 'Draft' AND [IssuedAt] IS NULL AND [WorkflowInstanceId] IS NULL) OR ([Status] IN ('Issued','Responded') AND [ApprovalStatus] = 'Draft' AND [IssuedById] IS NOT NULL AND [IssuedAt] IS NOT NULL AND [WorkflowInstanceId] IS NULL) OR ([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending' AND [IssuedAt] IS NOT NULL AND [SubmittedById] IS NOT NULL AND [SubmittedAt] IS NOT NULL AND [WorkflowInstanceId] IS NOT NULL) OR ([Status] IN ('Approved','Allocated','Applied') AND [ApprovalStatus] = 'Approved' AND [ApprovedAmount] IS NOT NULL AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [WorkflowInstanceId] IS NOT NULL) OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected' AND [RejectionReason] IS NOT NULL AND [WorkflowInstanceId] IS NOT NULL)");
                table.HasCheckConstraint("CK_QsSubcontractCharges_Application", "([Status] NOT IN ('Allocated','Applied') AND [AppliedValuationId] IS NULL AND [AllocatedAt] IS NULL AND [AppliedAt] IS NULL) OR ([Status] = 'Allocated' AND [AppliedValuationId] IS NOT NULL AND [AllocatedAt] IS NOT NULL AND [AppliedAt] IS NULL) OR ([Status] = 'Applied' AND [AppliedValuationId] IS NOT NULL AND [AllocatedAt] IS NOT NULL AND [AppliedAt] IS NOT NULL)");
                table.HasCheckConstraint("CK_QsSubcontractCharges_Communication", "[CommunicationStatus] IN ('NotRequested','Requested') AND [CommunicationRequestCount] >= 0 AND (([CommunicationRequestCount] = 0 AND [CommunicationRequestedAt] IS NULL) OR ([CommunicationRequestCount] > 0 AND [CommunicationRequestedAt] IS NOT NULL))");
            });
        });

        builder.Entity<QuantitySurveySubcontractChargeEvidence>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.ChecksumSha256).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.CentralDocumentVersionId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ChargeNoticeId, value.CreatedAt });
            entity.HasOne(value => value.ChargeNotice).WithMany(value => value.Evidence).HasForeignKey(value => value.ChargeNoticeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.FileUploadRecord>().WithMany().HasForeignKey(value => value.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentRecord).WithMany().HasForeignKey(value => value.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentVersion).WithMany().HasForeignKey(value => value.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0522_SubcontractChargeEvidence_AppendOnly");
                table.HasCheckConstraint("CK_QsSubcontractChargeEvidence_File", "[FileSize] > 0 AND LEN([ChecksumSha256]) = 64 AND LEN([RequestHash]) = 64");
            });
        });

        builder.Entity<QuantitySurveySubcontractChargeRevision>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ChargeNoticeId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ChargeNoticeId, value.CreatedAt });
            entity.HasOne(value => value.ChargeNotice).WithMany(value => value.Revisions).HasForeignKey(value => value.ChargeNoticeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Procurement.BusinessPartner>().WithMany().HasForeignKey(value => value.ActorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_QS0522_SubcontractChargeRevisions_AppendOnly");
                table.HasCheckConstraint("CK_QsSubcontractChargeRevisions_Request", "[ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND LEN([RequestHash]) = 64");
            });
        });

        builder.Entity<ProjectBoqItem>(entity =>
        {
            entity.Property(x => x.VersionLineKey).HasDefaultValueSql("NEWID()");
            entity.HasIndex(x => new { x.TenantId, x.ProjectId, x.VersionLineKey }).IsUnique();
        });

        builder.Entity<ProjectBoqVersion>(entity =>
        {
            entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.Property(x => x.SnapshotHash).IsUnicode(false);
            entity.HasIndex(x => new { x.TenantId, x.ProjectId, x.VersionNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ProjectId, x.VersionType, x.SnapshotAt });
            entity.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.ProjectId })
                .IsUnique()
                .HasFilter("[VersionType] = 2 AND [Status] = 'Approved' AND [PublishedAt] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.SourceVersion).WithMany().HasForeignKey(x => x.SourceVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ProjectBoqVersions_VersionNumber", "[VersionNumber] > 0");
                table.HasCheckConstraint("CK_ProjectBoqVersions_VersionType", "[VersionType] BETWEEN 0 AND 6");
                table.HasCheckConstraint("CK_ProjectBoqVersions_LineCount", "[LineCount] >= 0");
                table.HasCheckConstraint("CK_ProjectBoqVersions_Status", "[Status] IN ('Draft', 'PendingApproval', 'Approved', 'Rejected', 'Retired')");
                table.HasCheckConstraint("CK_ProjectBoqVersions_ApprovalStatus", "[ApprovalStatus] IN ('Draft', 'Pending', 'Approved', 'Rejected')");
                table.HasCheckConstraint("CK_ProjectBoqVersions_PublishedLifecycle", "[VersionType] <> 2 OR [Status] <> 'Approved' OR ([PublishedAt] IS NOT NULL AND [PublishedById] IS NOT NULL)");
            });
        });

        builder.Entity<ProjectBoqVersionLine>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.ProjectBoqVersionId, x.LineKey }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ProjectId, x.LineKey });
            entity.HasIndex(x => new { x.TenantId, x.ProjectBoqVersionId, x.SortOrder });
            entity.HasOne(x => x.Version).WithMany(x => x.Lines).HasForeignKey(x => x.ProjectBoqVersionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ProjectBoqVersionLines_Quantity", "[Quantity] >= 0");
                table.HasCheckConstraint("CK_ProjectBoqVersionLines_UnitRate", "[UnitRate] IS NULL OR [UnitRate] >= 0");
                table.HasCheckConstraint("CK_ProjectBoqVersionLines_LineAmount", "[LineAmount] IS NULL OR [LineAmount] >= 0");
            });
        });

        builder.Entity<ProjectBoqRemeasurementRevision>(entity =>
        {
            entity.Property(value => value.RequestHash).IsUnicode(false);
            entity.Property(value => value.MeasurementSetHash).IsUnicode(false);
            entity.HasIndex(value => new { value.TenantId, value.ProjectBoqVersionId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ClientRequestId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ProjectId, value.SourceApprovedBoqVersionId });
            entity.HasOne(value => value.Version).WithOne().HasForeignKey<ProjectBoqRemeasurementRevision>(value => value.ProjectBoqVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.SourceApprovedVersion).WithMany().HasForeignKey(value => value.SourceApprovedBoqVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ProjectBoqRemeasurementRevisions_Counts", "[SelectedMeasurementCount] > 0 AND [ChangedLineCount] > 0");
                table.HasCheckConstraint("CK_ProjectBoqRemeasurementRevisions_Delta", "[TotalAbsoluteQuantityDelta] > 0");
            });
        });

        builder.Entity<ProjectBoqRemeasurementLine>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.RemeasurementRevisionId, value.BoqLineKey }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.ProjectBoqVersionLineId }).IsUnique();
            entity.HasOne(value => value.Revision).WithMany(value => value.Lines).HasForeignKey(value => value.RemeasurementRevisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.VersionLine).WithMany().HasForeignKey(value => value.ProjectBoqVersionLineId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.SourceApprovedLine).WithMany().HasForeignKey(value => value.SourceApprovedBoqVersionLineId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_ProjectBoqRemeasurementLines_Quantities",
                "[PreviousQuantity] >= 0 AND [RevisedQuantity] >= 0 AND [QuantityDelta] = [RevisedQuantity] - [PreviousQuantity] AND [QuantityDelta] <> 0"));
        });

        builder.Entity<ProjectBoqRemeasurementSource>(entity =>
        {
            entity.HasIndex(value => new { value.TenantId, value.MeasurementSheetId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.RemeasurementLineId, value.MeasurementSheetId }).IsUnique();
            entity.HasOne(value => value.Revision).WithMany().HasForeignKey(value => value.RemeasurementRevisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Line).WithMany(value => value.Sources).HasForeignKey(value => value.RemeasurementLineId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.MeasurementSheet).WithMany().HasForeignKey(value => value.MeasurementSheetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_ProjectBoqRemeasurementSources_Quantity", "[MeasuredQuantitySnapshot] > 0"));
        });
    }
}
