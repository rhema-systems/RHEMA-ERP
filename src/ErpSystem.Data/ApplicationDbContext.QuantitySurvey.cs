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
    public DbSet<ProjectBoqVersion> ProjectBoqVersions { get; set; }
    public DbSet<ProjectBoqVersionLine> ProjectBoqVersionLines { get; set; }

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
    }
}
