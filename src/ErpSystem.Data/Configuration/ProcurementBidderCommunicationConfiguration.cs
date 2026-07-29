using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementBidderCommunicationRegisterConfiguration :
    IEntityTypeConfiguration<ProcurementBidderCommunicationRegister>
{
    public void Configure(EntityTypeBuilder<ProcurementBidderCommunicationRegister> builder)
    {
        builder.ToTable("ProcurementBidderCommunicationRegisters", table =>
        {
            table.HasTrigger("TR_ProcurementBidderCommunicationRegisters_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementBidderCommunicationRegisters_State",
                "[SourceType] BETWEEN 0 AND 2 AND [AwardFamily] BETWEEN 0 AND 3 " +
                "AND [AwardReadinessDecisionSequence] >= 1 " +
                "AND [StandstillStartsAtUtc] >= [AwardedAtUtc] " +
                "AND [StandstillEndsAtUtc] > [StandstillStartsAtUtc] " +
                "AND [AppealWindowEndsAtUtc] >= [StandstillEndsAtUtc] " +
                "AND LEN([AwardReadinessIntegrityHash]) = 64 " +
                "AND LEN([AwardReadinessSourceIntegrityHash]) = 64 " +
                "AND LEN([RecipientSnapshotHash]) = 64 AND LEN([IntegrityHash]) = 64 " +
                "AND ISJSON([AwardSnapshotJson]) = 1 AND ISJSON([RecipientSnapshotJson]) = 1");
        });

        builder.HasAlternateKey(item => new { item.TenantId, item.Id });
        builder.HasIndex(item => new { item.TenantId, item.SourceType, item.SourceId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.SourceType, item.SourceId, item.IdempotencyKey })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.AwardFamily, item.AwardId });
        builder.HasIndex(item => new { item.TenantId, item.AwardReadinessDecisionId });
        builder.HasIndex(item => new { item.TenantId, item.StandstillEndsAtUtc, item.AppealWindowEndsAtUtc });

        builder.HasOne<ProcurementAwardReadinessDecision>().WithMany()
            .HasForeignKey(item => item.AwardReadinessDecisionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementBidderCommunicationRecipientConfiguration :
    IEntityTypeConfiguration<ProcurementBidderCommunicationRecipient>
{
    public void Configure(EntityTypeBuilder<ProcurementBidderCommunicationRecipient> builder)
    {
        builder.ToTable("ProcurementBidderCommunicationRecipients", table =>
        {
            table.HasTrigger("TR_ProcurementBidderCommunicationRecipients_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementBidderCommunicationRecipients_State",
                "[Outcome] BETWEEN 0 AND 1 AND ISJSON([BidOrQuoteIdsJson]) = 1 " +
                "AND LEN([LineageHash]) = 64 AND LEN([IntegrityHash]) = 64");
        });

        builder.HasAlternateKey(item => new { item.TenantId, item.Id });
        builder.HasIndex(item => new { item.TenantId, item.RegisterId, item.BusinessPartnerId })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.BusinessPartnerId, item.Outcome });

        builder.HasOne(item => item.Register).WithMany(item => item.Recipients)
            .HasForeignKey(item => new { item.TenantId, item.RegisterId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.BusinessPartner).WithMany()
            .HasForeignKey(item => item.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementBidderCommunicationLetterVersionConfiguration :
    IEntityTypeConfiguration<ProcurementBidderCommunicationLetterVersion>
{
    public void Configure(EntityTypeBuilder<ProcurementBidderCommunicationLetterVersion> builder)
    {
        builder.ToTable("ProcurementBidderCommunicationLetterVersions", table =>
        {
            table.HasTrigger("TR_ProcurementBidderCommunicationLetterVersions_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementBidderCommunicationLetterVersions_State",
                "[Version] >= 1 AND LEN([TemplateChecksumSha256]) = 64 " +
                "AND LEN([ContentChecksumSha256]) = 64 AND LEN([IntegrityHash]) = 64 " +
                "AND ([ApprovalWorkflowEvidenceDocumentId] IS NULL OR [ApprovalFileUploadRecordId] IS NULL)");
        });

        builder.HasAlternateKey(item => new { item.TenantId, item.Id });
        builder.HasIndex(item => new { item.TenantId, item.RecipientId, item.Version }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.RecipientId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.TemplateVersionId });
        builder.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId });

        builder.HasOne(item => item.Recipient).WithMany(item => item.LetterVersions)
            .HasForeignKey(item => new { item.TenantId, item.RecipientId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.TemplateVersion).WithMany()
            .HasForeignKey(item => item.TemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowDefinition>().WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowInstance>().WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowEvidenceDocument>().WithMany()
            .HasForeignKey(item => item.ApprovalWorkflowEvidenceDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FileUploadRecord>().WithMany()
            .HasForeignKey(item => item.ApprovalFileUploadRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementBidderCommunicationDispatchConfiguration :
    IEntityTypeConfiguration<ProcurementBidderCommunicationDispatch>
{
    public void Configure(EntityTypeBuilder<ProcurementBidderCommunicationDispatch> builder)
    {
        builder.ToTable("ProcurementBidderCommunicationDispatches", table =>
        {
            table.HasTrigger("TR_ProcurementBidderCommunicationDispatches_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementBidderCommunicationDispatches_State",
                "[Sequence] >= 1 AND [Channel] BETWEEN 0 AND 4 AND LEN([IntegrityHash]) = 64 " +
                "AND ([DispatchWorkflowEvidenceDocumentId] IS NULL OR [DispatchFileUploadRecordId] IS NULL)");
        });

        builder.HasAlternateKey(item => new { item.TenantId, item.Id });
        builder.HasIndex(item => new { item.TenantId, item.LetterVersionId, item.Sequence }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.LetterVersionId, item.IdempotencyKey })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.DispatchedAtUtc });

        builder.HasOne(item => item.LetterVersion).WithMany(item => item.Dispatches)
            .HasForeignKey(item => new { item.TenantId, item.LetterVersionId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowEvidenceDocument>().WithMany()
            .HasForeignKey(item => item.DispatchWorkflowEvidenceDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FileUploadRecord>().WithMany()
            .HasForeignKey(item => item.DispatchFileUploadRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementBidderCommunicationDeliveryConfiguration :
    IEntityTypeConfiguration<ProcurementBidderCommunicationDelivery>
{
    public void Configure(EntityTypeBuilder<ProcurementBidderCommunicationDelivery> builder)
    {
        builder.ToTable("ProcurementBidderCommunicationDeliveries", table =>
        {
            table.HasTrigger("TR_ProcurementBidderCommunicationDeliveries_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementBidderCommunicationDeliveries_State",
                "[Sequence] >= 1 AND [Outcome] BETWEEN 0 AND 3 AND LEN([IntegrityHash]) = 64");
        });

        builder.HasIndex(item => new { item.TenantId, item.DispatchId, item.Sequence }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.DispatchId, item.IdempotencyKey }).IsUnique();

        builder.HasOne(item => item.Dispatch).WithMany(item => item.Deliveries)
            .HasForeignKey(item => new { item.TenantId, item.DispatchId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementBidderCommunicationAcknowledgementConfiguration :
    IEntityTypeConfiguration<ProcurementBidderCommunicationAcknowledgement>
{
    public void Configure(EntityTypeBuilder<ProcurementBidderCommunicationAcknowledgement> builder)
    {
        builder.ToTable("ProcurementBidderCommunicationAcknowledgements", table =>
        {
            table.HasTrigger("TR_ProcurementBidderCommunicationAcknowledgements_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementBidderCommunicationAcknowledgements_State",
                "[Sequence] >= 1 AND [Outcome] BETWEEN 0 AND 2 AND LEN([IntegrityHash]) = 64");
        });

        builder.HasIndex(item => new { item.TenantId, item.DispatchId, item.Sequence }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.DispatchId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.AcknowledgedByBusinessPartnerId });

        builder.HasOne(item => item.Dispatch).WithMany(item => item.Acknowledgements)
            .HasForeignKey(item => new { item.TenantId, item.DispatchId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BusinessPartner>().WithMany()
            .HasForeignKey(item => item.AcknowledgedByBusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementBidderAppealConfiguration :
    IEntityTypeConfiguration<ProcurementBidderAppeal>
{
    public void Configure(EntityTypeBuilder<ProcurementBidderAppeal> builder)
    {
        builder.ToTable("ProcurementBidderAppeals", table =>
        {
            table.HasTrigger("TR_ProcurementBidderAppeals_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementBidderAppeals_State",
                "[Sequence] >= 1 AND LEN([IntegrityHash]) = 64 " +
                "AND ([EvidenceWorkflowDocumentId] IS NULL OR [EvidenceFileUploadRecordId] IS NULL)");
        });

        builder.HasAlternateKey(item => new { item.TenantId, item.Id });
        builder.HasIndex(item => new { item.TenantId, item.RecipientId, item.Sequence }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.RecipientId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.FiledByBusinessPartnerId });

        builder.HasOne(item => item.Recipient).WithMany(item => item.Appeals)
            .HasForeignKey(item => new { item.TenantId, item.RecipientId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BusinessPartner>().WithMany()
            .HasForeignKey(item => item.FiledByBusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowEvidenceDocument>().WithMany()
            .HasForeignKey(item => item.EvidenceWorkflowDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FileUploadRecord>().WithMany()
            .HasForeignKey(item => item.EvidenceFileUploadRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementBidderAppealDecisionConfiguration :
    IEntityTypeConfiguration<ProcurementBidderAppealDecision>
{
    public void Configure(EntityTypeBuilder<ProcurementBidderAppealDecision> builder)
    {
        builder.ToTable("ProcurementBidderAppealDecisions", table =>
        {
            table.HasTrigger("TR_ProcurementBidderAppealDecisions_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementBidderAppealDecisions_State",
                "[Outcome] BETWEEN 0 AND 2 AND LEN([IntegrityHash]) = 64 " +
                "AND ([EvidenceWorkflowDocumentId] IS NULL OR [EvidenceFileUploadRecordId] IS NULL)");
        });

        builder.HasIndex(item => new { item.TenantId, item.AppealId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.AppealId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId });

        builder.HasOne(item => item.Appeal).WithMany(item => item.Decisions)
            .HasForeignKey(item => new { item.TenantId, item.AppealId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowDefinition>().WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowInstance>().WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowEvidenceDocument>().WithMany()
            .HasForeignKey(item => item.EvidenceWorkflowDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FileUploadRecord>().WithMany()
            .HasForeignKey(item => item.EvidenceFileUploadRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementTenderSecurityInstrumentConfiguration :
    IEntityTypeConfiguration<ProcurementTenderSecurityInstrument>
{
    public void Configure(EntityTypeBuilder<ProcurementTenderSecurityInstrument> builder)
    {
        builder.ToTable("ProcurementTenderSecurityInstruments", table =>
        {
            table.HasTrigger("TR_ProcurementTenderSecurityInstruments_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementTenderSecurityInstruments_Subject",
                "([TenderBidId] IS NOT NULL AND [RequestForQuotationQuoteId] IS NULL) OR " +
                "([TenderBidId] IS NULL AND [RequestForQuotationQuoteId] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_ProcurementTenderSecurityInstruments_State",
                "[InstrumentType] BETWEEN 0 AND 4 AND [Amount] > 0 AND LEN([CurrencyCode]) = 3 " +
                "AND [ExpiresAtUtc] > [IssuedAtUtc] AND LEN([IntegrityHash]) = 64 " +
                "AND ([EvidenceWorkflowDocumentId] IS NULL OR [EvidenceFileUploadRecordId] IS NULL)");
        });

        builder.HasAlternateKey(item => new { item.TenantId, item.Id });
        builder.HasIndex(item => new { item.TenantId, item.RecipientId, item.InstrumentReference })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.RecipientId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.TenderBidId });
        builder.HasIndex(item => new { item.TenantId, item.RequestForQuotationQuoteId });
        builder.HasIndex(item => new { item.TenantId, item.ExpiresAtUtc });

        builder.HasOne(item => item.Recipient).WithMany(item => item.SecurityInstruments)
            .HasForeignKey(item => new { item.TenantId, item.RecipientId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TenderBid>().WithMany()
            .HasForeignKey(item => item.TenderBidId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RequestForQuotationQuote>().WithMany()
            .HasForeignKey(item => item.RequestForQuotationQuoteId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowEvidenceDocument>().WithMany()
            .HasForeignKey(item => item.EvidenceWorkflowDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FileUploadRecord>().WithMany()
            .HasForeignKey(item => item.EvidenceFileUploadRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementTenderSecurityActionConfiguration :
    IEntityTypeConfiguration<ProcurementTenderSecurityAction>
{
    public void Configure(EntityTypeBuilder<ProcurementTenderSecurityAction> builder)
    {
        builder.ToTable("ProcurementTenderSecurityActions", table =>
        {
            table.HasTrigger("TR_ProcurementTenderSecurityActions_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementTenderSecurityActions_State",
                "[Sequence] >= 1 AND [ActionType] BETWEEN 0 AND 2 AND LEN([IntegrityHash]) = 64 " +
                "AND ([EvidenceWorkflowDocumentId] IS NULL OR [EvidenceFileUploadRecordId] IS NULL)");
        });

        builder.HasIndex(item => new { item.TenantId, item.SecurityInstrumentId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.SecurityInstrumentId, item.Sequence }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.SecurityInstrumentId, item.IdempotencyKey })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId });

        builder.HasOne(item => item.SecurityInstrument).WithMany(item => item.Actions)
            .HasForeignKey(item => new { item.TenantId, item.SecurityInstrumentId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowDefinition>().WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowInstance>().WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowEvidenceDocument>().WithMany()
            .HasForeignKey(item => item.EvidenceWorkflowDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FileUploadRecord>().WithMany()
            .HasForeignKey(item => item.EvidenceFileUploadRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
