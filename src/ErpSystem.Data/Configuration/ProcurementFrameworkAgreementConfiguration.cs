using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementFrameworkAgreementConfiguration :
    IEntityTypeConfiguration<ProcurementFrameworkAgreement>
{
    public void Configure(EntityTypeBuilder<ProcurementFrameworkAgreement> builder)
    {
        builder.ToTable("ProcurementFrameworkAgreements", table =>
        {
            table.HasTrigger("TR_ProcurementFrameworkAgreements_Lifecycle");
            table.HasCheckConstraint(
                "CK_ProcurementFrameworkAgreements_State",
                "[Version] >= 1 AND [Status] BETWEEN 0 AND 6 " +
                "AND [SourceType] BETWEEN 0 AND 2 " +
                "AND [PriceListVersion] >= 1 AND [CeilingAmount] > 0 " +
                "AND LEN([CurrencyCode]) = 3 " +
                "AND [EffectiveToUtc] > [EffectiveFromUtc] " +
                "AND LEN([SourceIntegrityHash]) = 64 " +
                "AND LEN([SupplierEligibilityDecisionHash]) = 64 " +
                "AND LEN([IntegrityHash]) = 64 " +
                "AND LEN([CreationCorrelationId]) > 0 " +
                "AND LEN([LastOperationCorrelationId]) > 0 " +
                "AND ISJSON([SnapshotJson]) = 1 " +
                "AND (([Status] = 1 AND [SubmittedById] IS NOT NULL AND [SubmittedAtUtc] IS NOT NULL) " +
                "OR [Status] <> 1) " +
                "AND (([Status] = 2 AND [PublishedById] IS NOT NULL AND [PublishedAtUtc] IS NOT NULL) " +
                "OR [Status] <> 2) " +
                "AND (([Status] = 3 AND [RejectedById] IS NOT NULL AND [RejectedAtUtc] IS NOT NULL) " +
                "OR [Status] <> 3) " +
                "AND (([Status] = 4 AND [SupersededByAgreementId] IS NOT NULL) OR [Status] <> 4) " +
                "AND (([Status] = 6 AND [TerminatedById] IS NOT NULL AND [TerminatedAtUtc] IS NOT NULL " +
                "AND LEN([TerminationReason]) > 0) OR [Status] <> 6)");
        });

        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => new { item.TenantId, item.AgreementKey, item.Version })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.AgreementNumber, item.Version })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.CreationCorrelationId })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.AgreementKey })
            .IsUnique()
            .HasFilter("[Status] IN (0, 1) AND [IsDeleted] = 0")
            .HasDatabaseName("UX_ProcurementFrameworkAgreements_OpenRevision");
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.Status,
            item.EffectiveFromUtc,
            item.EffectiveToUtc
        });
        builder.HasIndex(item => new { item.TenantId, item.BusinessPartnerId, item.Status });
        builder.HasIndex(item => new { item.TenantId, item.SourceType, item.SourceId });
        builder.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId });

        builder.HasOne(item => item.BusinessPartner).WithMany()
            .HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.AwardReadinessDecision).WithMany()
            .HasForeignKey(item => item.AwardReadinessDecisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SupersedesAgreement).WithMany()
            .HasForeignKey(item => item.SupersedesAgreementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcurementFrameworkAgreement>().WithMany()
            .HasForeignKey(item => item.SupersededByAgreementId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementFrameworkAgreementCategoryConfiguration :
    IEntityTypeConfiguration<ProcurementFrameworkAgreementCategory>
{
    public void Configure(EntityTypeBuilder<ProcurementFrameworkAgreementCategory> builder)
    {
        builder.ToTable("ProcurementFrameworkAgreementCategories", table =>
        {
            table.HasTrigger("TR_ProcurementFrameworkAgreementCategories_Protected");
            table.HasCheckConstraint(
                "CK_ProcurementFrameworkAgreementCategories_State",
                "LEN([CategoryCode]) > 0 AND LEN([CategoryName]) > 0 " +
                "AND LEN([IntegrityHash]) = 64");
        });

        builder.HasIndex(item => new { item.TenantId, item.AgreementId, item.PartnerCategoryId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasOne(item => item.Agreement).WithMany(item => item.Categories)
            .HasForeignKey(item => item.AgreementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PartnerCategory).WithMany()
            .HasForeignKey(item => item.PartnerCategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementFrameworkPriceListLineConfiguration :
    IEntityTypeConfiguration<ProcurementFrameworkPriceListLine>
{
    public void Configure(EntityTypeBuilder<ProcurementFrameworkPriceListLine> builder)
    {
        builder.ToTable("ProcurementFrameworkPriceListLines", table =>
        {
            table.HasTrigger("TR_ProcurementFrameworkPriceListLines_Protected");
            table.HasCheckConstraint(
                "CK_ProcurementFrameworkPriceListLines_State",
                "[UnitPrice] > 0 AND [MinimumQuantity] > 0 " +
                "AND ([MaximumQuantity] IS NULL OR [MaximumQuantity] >= [MinimumQuantity]) " +
                "AND [LeadTimeDays] >= 0 AND LEN([ItemCode]) > 0 " +
                "AND LEN([UnitOfMeasure]) > 0 AND LEN([IntegrityHash]) = 64");
        });

        builder.HasIndex(item => new { item.TenantId, item.AgreementId, item.InventoryItemId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasOne(item => item.Agreement).WithMany(item => item.PriceLines)
            .HasForeignKey(item => item.AgreementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.InventoryItem).WithMany()
            .HasForeignKey(item => item.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementFrameworkCallOffAuthorityConfiguration :
    IEntityTypeConfiguration<ProcurementFrameworkCallOffAuthority>
{
    public void Configure(EntityTypeBuilder<ProcurementFrameworkCallOffAuthority> builder)
    {
        builder.ToTable("ProcurementFrameworkCallOffAuthorities", table =>
        {
            table.HasTrigger("TR_ProcurementFrameworkCallOffAuthorities_Protected");
            table.HasCheckConstraint(
                "CK_ProcurementFrameworkCallOffAuthorities_State",
                "[AuthorityKind] BETWEEN 0 AND 3 " +
                "AND LEN([AuthorityValue]) > 0 AND LEN([DisplayName]) > 0 " +
                "AND ([MaximumCallOffAmount] IS NULL OR [MaximumCallOffAmount] > 0) " +
                "AND [ValidToUtc] > [ValidFromUtc] AND LEN([IntegrityHash]) = 64 " +
                "AND (([AuthorityKind] = 0 AND [AuthorityUserId] IS NOT NULL) " +
                "OR ([AuthorityKind] <> 0 AND [AuthorityUserId] IS NULL))");
        });

        builder.HasIndex(item => new
        {
            item.TenantId,
            item.AgreementId,
            item.AuthorityKind,
            item.AuthorityValue
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.AgreementId, item.IsActive });
        builder.HasOne(item => item.Agreement).WithMany(item => item.CallOffAuthorities)
            .HasForeignKey(item => item.AgreementId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementFrameworkAgreementDocumentConfiguration :
    IEntityTypeConfiguration<ProcurementFrameworkAgreementDocument>
{
    public void Configure(EntityTypeBuilder<ProcurementFrameworkAgreementDocument> builder)
    {
        builder.ToTable("ProcurementFrameworkAgreementDocuments", table =>
        {
            table.HasTrigger("TR_ProcurementFrameworkAgreementDocuments_Protected");
            table.HasCheckConstraint(
                "CK_ProcurementFrameworkAgreementDocuments_State",
                "LEN([DocumentType]) > 0 AND LEN([Title]) > 0 " +
                "AND LEN([DmsReference]) > 0 AND LEN([IntegrityHash]) = 64 " +
                "AND (([IsCurrent] = 1 AND [RetiredAtUtc] IS NULL AND [RetiredById] IS NULL) " +
                "OR ([IsCurrent] = 0 AND [RetiredAtUtc] IS NOT NULL AND [RetiredById] IS NOT NULL))");
        });

        builder.HasIndex(item => new { item.TenantId, item.AgreementId, item.FileUploadRecordId })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.CentralDocumentRecordId });
        builder.HasOne(item => item.Agreement).WithMany(item => item.Documents)
            .HasForeignKey(item => item.AgreementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.FileUploadRecord).WithMany()
            .HasForeignKey(item => item.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.CentralDocumentRecord).WithMany()
            .HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.CentralDocumentVersion).WithMany()
            .HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementFrameworkAgreementExtensionConfiguration :
    IEntityTypeConfiguration<ProcurementFrameworkAgreementExtension>
{
    public void Configure(EntityTypeBuilder<ProcurementFrameworkAgreementExtension> builder)
    {
        builder.ToTable("ProcurementFrameworkAgreementExtensions", table =>
        {
            table.HasTrigger("TR_ProcurementFrameworkAgreementExtensions_Lifecycle");
            table.HasCheckConstraint(
                "CK_ProcurementFrameworkAgreementExtensions_State",
                "[SequenceNumber] >= 1 AND [Status] BETWEEN 0 AND 2 " +
                "AND [ProposedEndUtc] > [PreviousEndUtc] " +
                "AND LEN([Reason]) > 0 AND LEN([CorrelationId]) > 0 " +
                "AND LEN([IntegrityHash]) = 64 AND ISJSON([SnapshotJson]) = 1 " +
                "AND (([Status] = 0 AND [DecidedById] IS NULL AND [DecidedAtUtc] IS NULL) " +
                "OR ([Status] IN (1, 2) AND [DecidedById] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL))");
        });

        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => new { item.TenantId, item.AgreementId, item.SequenceNumber })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.CorrelationId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.AgreementId })
            .IsUnique()
            .HasFilter("[Status] = 0 AND [IsDeleted] = 0")
            .HasDatabaseName("UX_ProcurementFrameworkAgreementExtensions_Pending");
        builder.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId });

        builder.HasOne(item => item.Agreement).WithMany(item => item.Extensions)
            .HasForeignKey(item => item.AgreementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
    }
}
