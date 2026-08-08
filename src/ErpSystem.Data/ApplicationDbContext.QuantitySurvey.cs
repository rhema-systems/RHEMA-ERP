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
    public DbSet<ProjectBoqVersion> ProjectBoqVersions { get; set; }
    public DbSet<ProjectBoqVersionLine> ProjectBoqVersionLines { get; set; }

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
            entity.HasIndex(value => new { value.TenantId, value.RateLibraryItemId, value.Version }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.RateLibraryItemId, value.LifecycleStatus, value.EffectiveFrom });
            entity.HasIndex(value => new { value.TenantId, value.ProjectTypeId, value.LocationId, value.BusinessPartnerId });
            entity.HasIndex(value => new { value.TenantId, value.CentralDocumentVersionId });
            entity.HasIndex(value => new { value.TenantId, value.MarketAnalysisId });
            entity.HasIndex(value => new { value.TenantId, value.NextReviewDueAt });
            entity.HasOne(value => value.RateLibraryItem).WithMany(value => value.Rates).HasForeignKey(value => value.RateLibraryItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Currency).WithMany().HasForeignKey(value => value.CurrencyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.ProjectType).WithMany().HasForeignKey(value => value.ProjectTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Location).WithMany().HasForeignKey(value => value.LocationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.BusinessPartner).WithMany().HasForeignKey(value => value.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentRecord).WithMany().HasForeignKey(value => value.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CentralDocumentVersion).WithMany().HasForeignKey(value => value.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.MarketAnalysis).WithMany().HasForeignKey(value => value.MarketAnalysisId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.PreviousRate).WithMany().HasForeignKey(value => value.PreviousRateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QsRateLibraryRates_Version", "[Version] > 0");
                table.HasCheckConstraint("CK_QsRateLibraryRates_UnitRate", "[UnitRate] >= 0");
                table.HasCheckConstraint("CK_QsRateLibraryRates_Period", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.HasCheckConstraint("CK_QsRateLibraryRates_Status", "[LifecycleStatus] IN (0, 1, 2)");
                table.HasCheckConstraint("CK_QsRateLibraryRates_Source", "[SourceType] IN (0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11)");
                table.HasCheckConstraint("CK_QsRateLibraryRates_EvidencePair", "([CentralDocumentRecordId] IS NULL AND [CentralDocumentVersionId] IS NULL) OR ([CentralDocumentRecordId] IS NOT NULL AND [CentralDocumentVersionId] IS NOT NULL)");
                table.HasCheckConstraint("CK_QsRateLibraryRates_MarketSurveyLineage", "([SourceType] <> 11 AND [MarketAnalysisId] IS NULL AND [MarketAnalysisCodeSnapshot] IS NULL AND [MarketSurveyQuoteCount] IS NULL AND [NextReviewDueAt] IS NULL) OR ([SourceType] = 11 AND (([MarketAnalysisId] IS NULL AND [MarketAnalysisCodeSnapshot] IS NULL AND [MarketSurveyQuoteCount] IS NULL AND [NextReviewDueAt] IS NULL) OR ([MarketAnalysisId] IS NOT NULL AND [MarketAnalysisCodeSnapshot] IS NOT NULL AND [MarketSurveyQuoteCount] > 0 AND [NextReviewDueAt] IS NOT NULL)))");
                table.HasCheckConstraint("CK_QsRateLibraryRates_PreviousRateSnapshot", "([PreviousRateId] IS NULL AND [PreviousUnitRate] IS NULL AND [PreviousCurrencyCodeSnapshot] IS NULL) OR ([PreviousRateId] IS NOT NULL AND [PreviousUnitRate] IS NOT NULL AND [PreviousCurrencyCodeSnapshot] IS NOT NULL)");
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
