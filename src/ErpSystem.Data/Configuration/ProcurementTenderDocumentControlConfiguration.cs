using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementTenderDocumentTemplateVersionConfiguration :
    IEntityTypeConfiguration<ProcurementTenderDocumentTemplateVersion>
{
    public void Configure(EntityTypeBuilder<ProcurementTenderDocumentTemplateVersion> builder)
    {
        builder.ToTable("ProcurementTenderDocumentTemplateVersions", table =>
        {
            table.HasTrigger("TR_ProcurementTenderDocumentTemplateVersions_Lifecycle");
            table.HasCheckConstraint(
                "CK_ProcurementTenderDocumentTemplateVersions_State",
                "[Version] >= 1 AND [PolicySetVersion] >= 1 AND [Status] BETWEEN 0 AND 3 " +
                "AND ([EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]) " +
                "AND (([Status] IN (0, 1) AND LEN([ContentChecksumSha256]) IN (0, 64)) " +
                "OR ([Status] IN (2, 3) AND LEN([ContentChecksumSha256]) = 64)) " +
                "AND LEN([IntegrityHash]) = 64 " +
                "AND ISJSON([LifecycleSnapshotJson]) = 1");
            table.HasCheckConstraint(
                "CK_ProcurementTenderDocumentTemplateVersions_ContentEvidence",
                "([ContentWorkflowEvidenceDocumentId] IS NULL OR [ContentFileUploadRecordId] IS NULL)");
        });

        builder.HasIndex(item => new { item.TenantId, item.TemplateKey, item.Version })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.TemplateCode, item.Version })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.TemplateKey })
            .IsUnique()
            .HasFilter("[Status] IN (0, 1) AND [IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.Status, item.EffectiveFromUtc });
        builder.HasIndex(item => new { item.TenantId, item.PolicySetId });
        builder.HasIndex(item => new { item.TenantId, item.WorkflowDefinitionId });

        builder.HasOne(item => item.PolicySet).WithMany()
            .HasForeignKey(item => item.PolicySetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SourceConfigurationProfile).WithMany()
            .HasForeignKey(item => item.SourceConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ContentWorkflowEvidenceDocument).WithMany()
            .HasForeignKey(item => item.ContentWorkflowEvidenceDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ContentFileUploadRecord).WithMany()
            .HasForeignKey(item => item.ContentFileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SupersedesVersion).WithMany()
            .HasForeignKey(item => item.SupersedesVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementTenderDocumentTemplateMethodConfiguration :
    IEntityTypeConfiguration<ProcurementTenderDocumentTemplateMethod>
{
    public void Configure(EntityTypeBuilder<ProcurementTenderDocumentTemplateMethod> builder)
    {
        builder.ToTable("ProcurementTenderDocumentTemplateMethods", table =>
        {
            table.HasTrigger("TR_ProcurementTenderDocumentTemplateMethods_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementTenderDocumentTemplateMethods_Method",
                "[Method] BETWEEN 0 AND 8 AND LEN([IntegrityHash]) = 64");
        });

        builder.HasIndex(item => new { item.TenantId, item.TemplateVersionId, item.Method })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.Method, item.TemplateVersionId });

        builder.HasOne(item => item.TemplateVersion)
            .WithMany(item => item.ApplicableMethods)
            .HasForeignKey(item => item.TemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementTenderDocumentRegisterConfiguration :
    IEntityTypeConfiguration<ProcurementTenderDocumentRegister>
{
    public void Configure(EntityTypeBuilder<ProcurementTenderDocumentRegister> builder)
    {
        builder.ToTable("ProcurementTenderDocumentRegisters", table =>
        {
            table.HasTrigger("TR_ProcurementTenderDocumentRegisters_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementTenderDocumentRegisters_Source",
                "([SourceType] = 0 AND [TenderId] IS NOT NULL AND [RequestForQuotationId] IS NULL) OR " +
                "([SourceType] = 1 AND [TenderId] IS NULL AND [RequestForQuotationId] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_ProcurementTenderDocumentRegisters_State",
                "[SourceType] BETWEEN 0 AND 1 AND [Method] BETWEEN 0 AND 8 " +
                "AND [PolicySetVersion] >= 1 AND [OriginalBidValidityUntilUtc] > [OriginalSubmissionDeadlineUtc] " +
                "AND ([OpeningScheduledAtUtc] IS NULL OR [OpeningScheduledAtUtc] >= [OriginalSubmissionDeadlineUtc]) " +
                "AND (([FeeMode] = 0 AND [FeeAmount] = 0) OR ([FeeMode] = 1 AND [FeeAmount] > 0)) " +
                "AND LEN([CurrencyCode]) = 3 AND LEN([CorrelationId]) > 0 AND LEN([IntegrityHash]) = 64 " +
                "AND ISJSON([LifecycleSnapshotJson]) = 1");
        });

        builder.HasIndex(item => new { item.TenantId, item.TenderId })
            .IsUnique()
            .HasFilter("[TenderId] IS NOT NULL");
        builder.HasIndex(item => new { item.TenantId, item.RequestForQuotationId })
            .IsUnique()
            .HasFilter("[RequestForQuotationId] IS NOT NULL");
        builder.HasIndex(item => new { item.TenantId, item.SourcingCaseId });
        builder.HasIndex(item => new { item.TenantId, item.InitialTemplateVersionId });
        builder.HasIndex(item => new { item.TenantId, item.MethodRuleId });
        builder.HasIndex(item => new { item.TenantId, item.PolicySetId });
        builder.HasIndex(item => new { item.TenantId, item.CorrelationId });

        builder.HasOne(item => item.Tender).WithMany()
            .HasForeignKey(item => item.TenderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.RequestForQuotation).WithMany()
            .HasForeignKey(item => item.RequestForQuotationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SourcingCase).WithMany()
            .HasForeignKey(item => item.SourcingCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.MethodRule).WithMany()
            .HasForeignKey(item => item.MethodRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PolicySet).WithMany()
            .HasForeignKey(item => item.PolicySetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SourceConfigurationProfile).WithMany()
            .HasForeignKey(item => item.SourceConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.InitialTemplateVersion)
            .WithMany(item => item.Registers)
            .HasForeignKey(item => item.InitialTemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementTenderDocumentIssuanceConfiguration :
    IEntityTypeConfiguration<ProcurementTenderDocumentIssuance>
{
    public void Configure(EntityTypeBuilder<ProcurementTenderDocumentIssuance> builder)
    {
        builder.ToTable("ProcurementTenderDocumentIssuances", table =>
        {
            table.HasTrigger("TR_ProcurementTenderDocumentIssuances_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementTenderDocumentIssuances_Fee",
                "([FeeMode] = 0 AND [FeeAmount] = 0 AND [AmountPaid] = 0 AND [PaymentReference] IS NULL) OR " +
                "([FeeMode] = 1 AND [FeeAmount] > 0 AND [AmountPaid] = [FeeAmount] " +
                "AND LEN(LTRIM(RTRIM(ISNULL([PaymentReference], '')))) > 0)");
            table.HasCheckConstraint(
                "CK_ProcurementTenderDocumentIssuances_State",
                "[FeeMode] BETWEEN 0 AND 1 AND LEN([RecipientKey]) = 64 AND LEN([CurrencyCode]) = 3 " +
                "AND LEN([CorrelationId]) > 0 AND LEN([IntegrityHash]) = 64 AND ISJSON([IssuanceSnapshotJson]) = 1 " +
                "AND ([EvidenceWorkflowDocumentId] IS NULL OR [EvidenceFileUploadRecordId] IS NULL)");
        });

        builder.HasIndex(item => new { item.TenantId, item.RegisterId, item.ReceiptNumber })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.RegisterId, item.RecipientKey })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.RegisterId, item.CorrelationId })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.TemplateVersionId });
        builder.HasIndex(item => new { item.TenantId, item.BusinessPartnerId });

        builder.HasOne(item => item.Register)
            .WithMany(item => item.Issuances)
            .HasForeignKey(item => item.RegisterId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.TemplateVersion).WithMany()
            .HasForeignKey(item => item.TemplateVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.BusinessPartner).WithMany()
            .HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.EvidenceWorkflowDocument).WithMany()
            .HasForeignKey(item => item.EvidenceWorkflowDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.EvidenceFileUploadRecord).WithMany()
            .HasForeignKey(item => item.EvidenceFileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementTenderDocumentChangeConfiguration :
    IEntityTypeConfiguration<ProcurementTenderDocumentChange>
{
    public void Configure(EntityTypeBuilder<ProcurementTenderDocumentChange> builder)
    {
        builder.ToTable("ProcurementTenderDocumentChanges", table =>
        {
            table.HasTrigger("TR_ProcurementTenderDocumentChanges_Lifecycle");
            table.HasCheckConstraint(
                "CK_ProcurementTenderDocumentChanges_Kind",
                "([ChangeType] = 0 AND [PreviousTemplateVersionId] IS NOT NULL AND [NewTemplateVersionId] IS NOT NULL " +
                "AND [PreviousTemplateVersionId] <> [NewTemplateVersionId] " +
                "AND [PreviousValueUtc] IS NULL AND [NewValueUtc] IS NULL " +
                "AND [PreviousOpeningScheduledAtUtc] IS NULL AND [NewOpeningScheduledAtUtc] IS NULL) OR " +
                "([ChangeType] IN (1, 2) AND [PreviousTemplateVersionId] IS NULL AND [NewTemplateVersionId] IS NULL " +
                "AND [PreviousValueUtc] IS NOT NULL AND [NewValueUtc] IS NOT NULL AND [NewValueUtc] > [PreviousValueUtc] " +
                "AND [PreviousOpeningScheduledAtUtc] IS NULL AND [NewOpeningScheduledAtUtc] IS NULL) OR " +
                "([ChangeType] = 3 AND [PreviousTemplateVersionId] IS NULL AND [NewTemplateVersionId] IS NULL " +
                "AND [PreviousValueUtc] IS NOT NULL AND [NewValueUtc] IS NOT NULL AND [NewValueUtc] > [PreviousValueUtc] " +
                "AND [NewOpeningScheduledAtUtc] IS NOT NULL AND [NewOpeningScheduledAtUtc] > [NewValueUtc] " +
                "AND ([PreviousOpeningScheduledAtUtc] IS NULL OR [NewOpeningScheduledAtUtc] > [PreviousOpeningScheduledAtUtc]) " +
                "AND [RequiresAcknowledgement] = 0)");
            table.HasCheckConstraint(
                "CK_ProcurementTenderDocumentChanges_State",
                "[Sequence] >= 1 AND [ChangeType] BETWEEN 0 AND 3 AND [Status] BETWEEN 0 AND 2 " +
                "AND (([Status] = 0 AND [DecidedAtUtc] IS NULL AND [DecidedByUserId] IS NULL " +
                "AND [WorkflowOutcome] IS NULL AND [ApprovalReference] IS NULL " +
                "AND [DispatchedAtUtc] IS NULL AND [DispatchedByUserId] IS NULL AND [DispatchEvidenceReference] IS NULL) OR " +
                "([Status] = 1 AND [DecidedAtUtc] IS NOT NULL AND [DecidedByUserId] IS NOT NULL " +
                "AND LEN(LTRIM(RTRIM(ISNULL([WorkflowOutcome], '')))) > 0 " +
                "AND LEN(LTRIM(RTRIM(ISNULL([ApprovalReference], '')))) > 0 " +
                "AND (([DispatchedAtUtc] IS NULL AND [DispatchedByUserId] IS NULL AND [DispatchEvidenceReference] IS NULL) OR " +
                "([DispatchedAtUtc] IS NOT NULL AND [DispatchedByUserId] IS NOT NULL " +
                "AND LEN(LTRIM(RTRIM(ISNULL([DispatchEvidenceReference], '')))) > 0))) OR " +
                "([Status] = 2 AND [DecidedAtUtc] IS NOT NULL AND [DecidedByUserId] IS NOT NULL " +
                "AND LEN(LTRIM(RTRIM(ISNULL([WorkflowOutcome], '')))) > 0 " +
                "AND LEN(LTRIM(RTRIM(ISNULL([ApprovalReference], '')))) > 0 " +
                "AND [DispatchedAtUtc] IS NULL AND [DispatchedByUserId] IS NULL AND [DispatchEvidenceReference] IS NULL)) " +
                "AND LEN([CorrelationId]) > 0 AND LEN([IntegrityHash]) = 64 AND ISJSON([LifecycleSnapshotJson]) = 1 " +
                "AND ([EvidenceWorkflowDocumentId] IS NULL OR [EvidenceFileUploadRecordId] IS NULL)");
        });

        builder.HasIndex(item => new { item.TenantId, item.RegisterId, item.Sequence })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.RegisterId })
            .IsUnique()
            .HasFilter("[Status] = 0 AND [IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.RegisterId, item.CorrelationId })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.WorkflowDefinitionId });
        builder.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId });

        builder.HasOne(item => item.Register)
            .WithMany(item => item.Changes)
            .HasForeignKey(item => item.RegisterId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PreviousTemplateVersion).WithMany()
            .HasForeignKey(item => item.PreviousTemplateVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.NewTemplateVersion).WithMany()
            .HasForeignKey(item => item.NewTemplateVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.EvidenceWorkflowDocument).WithMany()
            .HasForeignKey(item => item.EvidenceWorkflowDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.EvidenceFileUploadRecord).WithMany()
            .HasForeignKey(item => item.EvidenceFileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementTenderDocumentChangeRecipientConfiguration :
    IEntityTypeConfiguration<ProcurementTenderDocumentChangeRecipient>
{
    public void Configure(EntityTypeBuilder<ProcurementTenderDocumentChangeRecipient> builder)
    {
        builder.ToTable("ProcurementTenderDocumentChangeRecipients", table =>
        {
            table.HasTrigger("TR_ProcurementTenderDocumentChangeRecipients_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementTenderDocumentChangeRecipients_Source",
                "([SourceType] = 0 AND [IssuanceId] IS NOT NULL AND [TenderBidId] IS NULL AND [RequestForQuotationQuoteId] IS NULL) OR " +
                "([SourceType] = 1 AND [IssuanceId] IS NULL AND [TenderBidId] IS NOT NULL AND [RequestForQuotationQuoteId] IS NULL) OR " +
                "([SourceType] = 2 AND [IssuanceId] IS NULL AND [TenderBidId] IS NULL AND [RequestForQuotationQuoteId] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_ProcurementTenderDocumentChangeRecipients_State",
                "[SourceType] BETWEEN 0 AND 2 AND LEN([RecipientKey]) = 64 " +
                "AND LEN([IntegrityHash]) = 64 AND ISJSON([DispatchSnapshotJson]) = 1");
        });

        builder.HasIndex(item => new { item.TenantId, item.ChangeId, item.RecipientKey })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.IssuanceId });
        builder.HasIndex(item => new { item.TenantId, item.TenderBidId });
        builder.HasIndex(item => new { item.TenantId, item.RequestForQuotationQuoteId });
        builder.HasIndex(item => new { item.TenantId, item.BusinessPartnerId });

        builder.HasOne(item => item.Change)
            .WithMany(item => item.Recipients)
            .HasForeignKey(item => item.ChangeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Issuance).WithMany()
            .HasForeignKey(item => item.IssuanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.TenderBid).WithMany()
            .HasForeignKey(item => item.TenderBidId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.RequestForQuotationQuote).WithMany()
            .HasForeignKey(item => item.RequestForQuotationQuoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.BusinessPartner).WithMany()
            .HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementTenderDocumentAcknowledgementConfiguration :
    IEntityTypeConfiguration<ProcurementTenderDocumentAcknowledgement>
{
    public void Configure(EntityTypeBuilder<ProcurementTenderDocumentAcknowledgement> builder)
    {
        builder.ToTable("ProcurementTenderDocumentAcknowledgements", table =>
        {
            table.HasTrigger("TR_ProcurementTenderDocumentAcknowledgements_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementTenderDocumentAcknowledgements_Target",
                "([IssuanceId] IS NOT NULL AND [ChangeRecipientId] IS NULL) OR " +
                "([IssuanceId] IS NULL AND [ChangeRecipientId] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_ProcurementTenderDocumentAcknowledgements_State",
                "[Outcome] BETWEEN 0 AND 1 AND LEN([CorrelationId]) > 0 AND LEN([IntegrityHash]) = 64 " +
                "AND ISJSON([AcknowledgementSnapshotJson]) = 1");
        });

        builder.HasIndex(item => new { item.TenantId, item.IssuanceId })
            .IsUnique()
            .HasFilter("[IssuanceId] IS NOT NULL");
        builder.HasIndex(item => new { item.TenantId, item.ChangeRecipientId })
            .IsUnique()
            .HasFilter("[ChangeRecipientId] IS NOT NULL");
        builder.HasIndex(item => new { item.TenantId, item.CorrelationId })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.BusinessPartnerId });

        builder.HasOne(item => item.Issuance)
            .WithMany(item => item.Acknowledgements)
            .HasForeignKey(item => item.IssuanceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ChangeRecipient)
            .WithMany(item => item.Acknowledgements)
            .HasForeignKey(item => item.ChangeRecipientId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.BusinessPartner).WithMany()
            .HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
