using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementSupplierOnboardingTokenConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierOnboardingToken>
{
    public void Configure(EntityTypeBuilder<ProcurementSupplierOnboardingToken> builder)
    {
        builder.ToTable("ProcurementSupplierOnboardingTokens", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierOnboardingTokens_Protected");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierOnboardingTokens_State",
                "[Generation] >= 1 AND [Status] BETWEEN 0 AND 2 " +
                "AND [PaymentStatus] BETWEEN 0 AND 5 AND [FeeMode] BETWEEN 0 AND 1 " +
                "AND [SourceConfigurationProfileVersion] >= 1 " +
                "AND [FeeAmount] >= 0 AND [TaxPercent] >= 0 AND [TaxAmount] >= 0 " +
                "AND [TotalAmount] = [FeeAmount] + [TaxAmount] " +
                "AND LEN([CurrencyCode]) = 3 AND LEN([TokenHashSha256]) = 64 " +
                "AND LEN([DecisionSnapshotHash]) = 64 AND LEN([IntegrityHash]) = 64 " +
                "AND ISJSON([PaymentChannelsJson]) = 1 AND ISJSON([DecisionSnapshotJson]) = 1 " +
                "AND (([Status] = 2 AND [ExpiredAtUtc] IS NOT NULL AND [ExpiryReason] IS NOT NULL) " +
                "OR ([Status] <> 2 AND [ExpiredAtUtc] IS NULL)) " +
                "AND (([FeeMode] = 0 AND [FeeAmount] = 0 AND [TaxAmount] = 0 " +
                "AND [PaymentStatus] IN (0, 4)) OR [FeeMode] = 1)");
        });

        builder.HasIndex(item => new { item.TenantId, item.RegistrationId })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.TokenReference }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.TokenHashSha256 }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.CreationCorrelationId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.Status, item.PaymentStatus, item.IssuedAtUtc });
        builder.HasIndex(item => new { item.TenantId, item.SourceConfigurationProfileId });

        builder.HasOne(item => item.Registration).WithOne(item => item.OnboardingToken)
            .HasForeignKey<ProcurementSupplierOnboardingToken>(item => item.RegistrationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SourceConfigurationProfile).WithMany()
            .HasForeignKey(item => item.SourceConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SourceConfigurationDecision).WithMany()
            .HasForeignKey(item => item.SourceConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.RevenueAccount).WithMany()
            .HasForeignKey(item => item.RevenueAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.TaxAccount).WithMany()
            .HasForeignKey(item => item.TaxAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ExemptionWorkflowDefinition).WithMany()
            .HasForeignKey(item => item.ExemptionWorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementSupplierOnboardingPaymentConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierOnboardingPayment>
{
    public void Configure(EntityTypeBuilder<ProcurementSupplierOnboardingPayment> builder)
    {
        builder.ToTable("ProcurementSupplierOnboardingPayments", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierOnboardingPayments_Protected");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierOnboardingPayments_State",
                "[Status] BETWEEN 1 AND 5 AND [FeeAmount] >= 0 AND [TaxAmount] >= 0 " +
                "AND [TotalAmount] > 0 AND [TotalAmount] = [FeeAmount] + [TaxAmount] " +
                "AND LEN([CurrencyCode]) = 3 AND LEN([IntegrityHash]) = 64 " +
                "AND (([Status] IN (2, 3) AND [PostedAtUtc] IS NOT NULL " +
                "AND [PostingEventId] IS NOT NULL AND [JournalEntryId] IS NOT NULL " +
                "AND [ReceiptNumber] IS NOT NULL AND [ReceiptIssuedAtUtc] IS NOT NULL) " +
                "OR [Status] NOT IN (2, 3)) " +
                "AND (([Status] = 3 AND [ReconciledAtUtc] IS NOT NULL AND [ReconciledById] IS NOT NULL " +
                "AND [ReconciliationReference] IS NOT NULL) OR [Status] <> 3)");
        });

        builder.HasIndex(item => new { item.TenantId, item.CreationCorrelationId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ReceiptNumber })
            .IsUnique().HasFilter("[ReceiptNumber] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.PostingEventId })
            .IsUnique().HasFilter("[PostingEventId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.TokenId, item.Status });

        builder.HasOne(item => item.Token).WithMany(item => item.Payments)
            .HasForeignKey(item => item.TokenId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PaymentMethod).WithMany()
            .HasForeignKey(item => item.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementSupplierOnboardingExemptionConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierOnboardingExemption>
{
    public void Configure(EntityTypeBuilder<ProcurementSupplierOnboardingExemption> builder)
    {
        builder.ToTable("ProcurementSupplierOnboardingExemptions", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierOnboardingExemptions_Protected");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierOnboardingExemptions_State",
                "[Status] BETWEEN 0 AND 2 AND LEN([EvidenceHash]) = 64 " +
                "AND LEN([IntegrityHash]) = 64 AND ISJSON([EvidenceJson]) = 1 " +
                "AND (([Status] = 0 AND [DecidedById] IS NULL AND [DecidedAtUtc] IS NULL) " +
                "OR ([Status] IN (1, 2) AND [DecidedById] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL))");
        });

        builder.HasIndex(item => new { item.TenantId, item.CreationCorrelationId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.TokenId })
            .IsUnique().HasFilter("[Status] = 0 AND [IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId })
            .IsUnique().HasFilter("[WorkflowInstanceId] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasOne(item => item.Token).WithMany(item => item.Exemptions)
            .HasForeignKey(item => item.TokenId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementSupplierApplicantAccessConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierApplicantAccess>
{
    public void Configure(EntityTypeBuilder<ProcurementSupplierApplicantAccess> builder)
    {
        builder.ToTable("ProcurementSupplierApplicantAccesses", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierApplicantAccesses_Protected");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierApplicantAccesses_State",
                "[VerifiedChannel] BETWEEN 0 AND 1 AND [Status] BETWEEN 0 AND 5 " +
                "AND LEN([VerifiedContactHashSha256]) = 64 " +
                "AND LEN([IntegrityHash]) = 64 AND [NotificationAttemptCount] >= 0 " +
                "AND ISJSON([ApprovedIdentityRolesJson]) = 1 " +
                "AND (([Status] = 0 AND [TerminalOutcome] IS NULL AND [TerminalAtUtc] IS NULL) " +
                "OR ([Status] = 4 AND [TerminalOutcome] = 'Rejected' AND [TerminalAtUtc] IS NOT NULL) " +
                "OR ([Status] IN (1, 2, 3, 5) AND [TerminalOutcome] = 'Approved' AND [TerminalAtUtc] IS NOT NULL)) " +
                "AND (([ApprovedUserId] IS NULL AND [BusinessPartnerId] IS NULL) " +
                "OR ([ApprovedUserId] IS NOT NULL AND [BusinessPartnerId] IS NOT NULL " +
                "AND [LoginIdentifier] IS NOT NULL " +
                "AND [TemporaryCredentialIssuedAtUtc] IS NOT NULL " +
                "AND [TemporaryCredentialExpiresAtUtc] >= [TemporaryCredentialIssuedAtUtc])) " +
                "AND ([Status] NOT IN (2, 3, 5) OR [ApprovedUserId] IS NOT NULL) " +
                "AND (([Status] = 3 AND [CredentialActivatedAtUtc] IS NOT NULL) " +
                "OR ([Status] <> 3 AND [CredentialActivatedAtUtc] IS NULL))");
        });

        builder.HasIndex(item => new { item.TenantId, item.RegistrationId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.TokenId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ApprovedUserId })
            .IsUnique().HasFilter("[ApprovedUserId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.VerifiedContactHashSha256,
            item.Status
        });

        builder.HasOne(item => item.Registration).WithOne(item => item.ApplicantAccess)
            .HasForeignKey<ProcurementSupplierApplicantAccess>(item => item.RegistrationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Token).WithOne(item => item.ApplicantAccess)
            .HasForeignKey<ProcurementSupplierApplicantAccess>(item => item.TokenId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ApprovedUser).WithMany()
            .HasForeignKey(item => item.ApprovedUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.BusinessPartner).WithMany()
            .HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementSupplierApplicantSessionConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierApplicantSession>
{
    public void Configure(EntityTypeBuilder<ProcurementSupplierApplicantSession> builder)
    {
        builder.ToTable("ProcurementSupplierApplicantSessions", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierApplicantSessions_Protected");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierApplicantSessions_State",
                "[Status] BETWEEN 0 AND 1 AND [ExpiresAtUtc] > [IssuedAtUtc] " +
                "AND LEN([IntegrityHash]) = 64 " +
                "AND (([Status] = 0 AND [RevokedAtUtc] IS NULL) " +
                "OR ([Status] = 1 AND [RevokedAtUtc] IS NOT NULL AND [RevocationReason] IS NOT NULL))");
        });

        builder.HasIndex(item => new { item.TenantId, item.SessionReference }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ApplicantAccessId, item.Status });
        builder.HasOne(item => item.ApplicantAccess).WithMany(item => item.Sessions)
            .HasForeignKey(item => item.ApplicantAccessId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
