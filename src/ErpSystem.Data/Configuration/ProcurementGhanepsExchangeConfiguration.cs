using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementGhanepsExchangeEventConfiguration :
    IEntityTypeConfiguration<ProcurementGhanepsExchangeEvent>
{
    public void Configure(EntityTypeBuilder<ProcurementGhanepsExchangeEvent> builder)
    {
        builder.ToTable("ProcurementGhanepsExchangeEvents", table =>
        {
            table.HasTrigger("TR_ProcurementGhanepsExchangeEvents_Guard");
            table.HasCheckConstraint("CK_ProcurementGhanepsExchangeEvents_State",
                "[SourceType] BETWEEN 0 AND 2 AND [EventFamily] BETWEEN 0 AND 2 " +
                "AND [Direction] BETWEEN 0 AND 1 AND [Status] BETWEEN 0 AND 6 " +
                "AND [ConfigurationProfileVersion] >= 1 " +
                "AND [ConfigurationDecisionSchemaVersion] >= 1 " +
                "AND [MaximumRetryAttempts] BETWEEN 0 AND 100 " +
                "AND LEN([RequestFingerprint]) = 64 " +
                "AND ([ConfigurationEffectiveToUtc] IS NULL OR " +
                "[ConfigurationEffectiveToUtc] >= [ConfigurationEffectiveFromUtc]) " +
                "AND LEN([SourceIntegrityHash]) = 64 " +
                "AND LEN([ConfigurationValueHash]) = 64 " +
                "AND LEN([MappingIntegrityHash]) = 64 AND LEN([IntegrityHash]) = 64 " +
                "AND ISJSON([SourceSnapshotJson]) = 1 " +
                "AND ISJSON([ConfigurationValueJson]) = 1 " +
                "AND ISJSON([MappingSnapshotJson]) = 1");
        });
        builder.HasAlternateKey(item => new { item.TenantId, item.Id });
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.SourceType,
            item.SourceId,
            item.EventFamily,
            item.Direction,
            item.MappingKey,
            item.EventReference,
            item.MappingIntegrityHash
        }).IsUnique().HasDatabaseName("UX_ProcGhanepsEvent_Source_MappingHash");
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.SourceType,
            item.SourceId,
            item.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.ConfigurationProfileId,
            item.ConfigurationDecisionId
        });
        builder.HasIndex(item => new { item.TenantId, item.Status, item.PreparedAtUtc });
        builder.HasOne(item => item.ConfigurationProfile).WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ConfigurationProfileId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ConfigurationDecision).WithMany()
            .HasForeignKey(item => new
            {
                item.TenantId,
                item.ConfigurationProfileId,
                item.ConfigurationDecisionId
            })
            .HasPrincipalKey(item => new { item.TenantId, item.ProfileId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementGhanepsExchangePayloadConfiguration :
    IEntityTypeConfiguration<ProcurementGhanepsExchangePayload>
{
    public void Configure(EntityTypeBuilder<ProcurementGhanepsExchangePayload> builder)
    {
        builder.ToTable("ProcurementGhanepsExchangePayloads", table =>
        {
            table.HasTrigger("TR_ProcurementGhanepsExchangePayloads_Immutable");
            table.HasCheckConstraint("CK_ProcurementGhanepsExchangePayloads_State",
                "[Version] >= 1 AND [Direction] BETWEEN 0 AND 1 " +
                "AND LEN([PayloadChecksumSha256]) = 64 " +
                "AND LEN([IntegrityHash]) = 64");
        });
        builder.HasAlternateKey(item => new { item.TenantId, item.ExchangeEventId, item.Id });
        builder.HasIndex(item => new { item.TenantId, item.ExchangeEventId, item.Version }).IsUnique();
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.ExchangeEventId,
            item.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.PayloadChecksumSha256 });
        builder.HasOne(item => item.ExchangeEvent).WithMany(item => item.Payloads)
            .HasForeignKey(item => new { item.TenantId, item.ExchangeEventId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementGhanepsExchangeAttemptConfiguration :
    IEntityTypeConfiguration<ProcurementGhanepsExchangeAttempt>
{
    public void Configure(EntityTypeBuilder<ProcurementGhanepsExchangeAttempt> builder)
    {
        builder.ToTable("ProcurementGhanepsExchangeAttempts", table =>
        {
            table.HasTrigger("TR_ProcurementGhanepsExchangeAttempts_Immutable");
            table.HasCheckConstraint("CK_ProcurementGhanepsExchangeAttempts_State",
                "[AttemptNumber] >= 1 AND [Outcome] BETWEEN 0 AND 1 " +
                "AND LEN([PayloadChecksumSha256]) = 64 AND LEN([RequestFingerprint]) = 64 " +
                "AND LEN([IntegrityHash]) = 64 " +
                "AND (([Outcome] = 0 AND [TransportReference] IS NOT NULL " +
                "AND [FailureCode] IS NULL AND [FailureMessage] IS NULL) OR " +
                "([Outcome] = 1 AND [FailureCode] IS NOT NULL AND [FailureMessage] IS NOT NULL)) " +
                "AND (([IsRetry] = 0 AND [SupersedesAttemptId] IS NULL) OR " +
                "([IsRetry] = 1 AND [SupersedesAttemptId] IS NOT NULL))");
        });
        builder.HasAlternateKey(item => new { item.TenantId, item.ExchangeEventId, item.Id });
        builder.HasAlternateKey(item => new
        {
            item.TenantId,
            item.ExchangeEventId,
            item.Id,
            item.PayloadId
        });
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.ExchangeEventId,
            item.AttemptNumber
        }).IsUnique();
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.ExchangeEventId,
            item.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.Outcome, item.AttemptedAtUtc });
        builder.HasOne(item => item.ExchangeEvent).WithMany(item => item.Attempts)
            .HasForeignKey(item => new { item.TenantId, item.ExchangeEventId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Payload).WithMany(item => item.Attempts)
            .HasForeignKey(item => new { item.TenantId, item.ExchangeEventId, item.PayloadId })
            .HasPrincipalKey(item => new { item.TenantId, item.ExchangeEventId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SupersedesAttempt).WithMany()
            .HasForeignKey(item => new
            {
                item.TenantId,
                item.ExchangeEventId,
                item.SupersedesAttemptId
            })
            .HasPrincipalKey(item => new { item.TenantId, item.ExchangeEventId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementGhanepsExchangeAcknowledgementConfiguration :
    IEntityTypeConfiguration<ProcurementGhanepsExchangeAcknowledgement>
{
    public void Configure(EntityTypeBuilder<ProcurementGhanepsExchangeAcknowledgement> builder)
    {
        builder.ToTable("ProcurementGhanepsExchangeAcknowledgements", table =>
        {
            table.HasTrigger("TR_ProcurementGhanepsExchangeAcknowledgements_Immutable");
            table.HasCheckConstraint("CK_ProcurementGhanepsExchangeAcknowledgements_State",
                "[Sequence] >= 1 AND [Outcome] BETWEEN 0 AND 1 " +
                "AND LEN([AcknowledgementChecksumSha256]) = 64 " +
                "AND LEN([RequestFingerprint]) = 64 " +
                "AND LEN([IntegrityHash]) = 64");
        });
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.ExchangeEventId,
            item.Sequence
        }).IsUnique();
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.ExchangeEventId,
            item.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.AttemptId }).IsUnique();
        builder.HasOne(item => item.ExchangeEvent).WithMany(item => item.Acknowledgements)
            .HasForeignKey(item => new { item.TenantId, item.ExchangeEventId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Attempt).WithMany()
            .HasForeignKey(item => new
            {
                item.TenantId,
                item.ExchangeEventId,
                item.AttemptId,
                item.PayloadId
            })
            .HasPrincipalKey(item => new
            {
                item.TenantId,
                item.ExchangeEventId,
                item.Id,
                item.PayloadId
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Payload).WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ExchangeEventId, item.PayloadId })
            .HasPrincipalKey(item => new { item.TenantId, item.ExchangeEventId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementGhanepsExchangeReconciliationConfiguration :
    IEntityTypeConfiguration<ProcurementGhanepsExchangeReconciliation>
{
    public void Configure(EntityTypeBuilder<ProcurementGhanepsExchangeReconciliation> builder)
    {
        builder.ToTable("ProcurementGhanepsExchangeReconciliations", table =>
        {
            table.HasTrigger("TR_ProcurementGhanepsExchangeReconciliations_Immutable");
            table.HasCheckConstraint("CK_ProcurementGhanepsExchangeReconciliations_State",
                "[Sequence] >= 1 AND [Outcome] BETWEEN 0 AND 2 " +
                "AND LEN([ExpectedChecksumSha256]) = 64 " +
                "AND ([ActualChecksumSha256] IS NULL OR LEN([ActualChecksumSha256]) = 64) " +
                "AND LEN([RequestFingerprint]) = 64 " +
                "AND LEN([IntegrityHash]) = 64");
        });
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.ExchangeEventId,
            item.Sequence
        }).IsUnique();
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.ExchangeEventId,
            item.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.Outcome, item.ReconciledAtUtc });
        builder.HasOne(item => item.ExchangeEvent).WithMany(item => item.Reconciliations)
            .HasForeignKey(item => new { item.TenantId, item.ExchangeEventId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Attempt).WithMany()
            .HasForeignKey(item => new
            {
                item.TenantId,
                item.ExchangeEventId,
                item.AttemptId,
                item.PayloadId
            })
            .HasPrincipalKey(item => new
            {
                item.TenantId,
                item.ExchangeEventId,
                item.Id,
                item.PayloadId
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Payload).WithMany()
            .HasForeignKey(item => new
            {
                item.TenantId,
                item.ExchangeEventId,
                item.PayloadId
            })
            .HasPrincipalKey(item => new
            {
                item.TenantId,
                item.ExchangeEventId,
                item.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
