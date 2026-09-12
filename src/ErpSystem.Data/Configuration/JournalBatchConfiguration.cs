using ErpSystem.Core.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class JournalBatchEntryApprovalConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> entity)
        => entity.ToTable("JournalEntries", table => table.HasTrigger("TR_JournalEntries_BatchOptionalApproval"));
}

public sealed class JournalBatchConfiguration : IEntityTypeConfiguration<JournalBatch>
{
    public void Configure(EntityTypeBuilder<JournalBatch> entity)
    {
        entity.ToTable("JournalBatches", table =>
        {
            table.HasTrigger("TR_JournalBatches_OptionalApproval");
            table.HasCheckConstraint("CK_JournalBatches_ExpectedDebitTotal", "[ExpectedDebitTotal] > 0");
            table.HasCheckConstraint("CK_JournalBatches_ExpectedJournalCount", "[ExpectedJournalCount] IS NULL OR [ExpectedJournalCount] > 0");
        });

        entity.Property(x => x.BatchType).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.ApprovalStatus).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.ApprovalRequired).HasDefaultValue(true).ValueGeneratedNever();
        entity.Property(x => x.PostingStatus).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.ReversalStatus).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.RowVersion).IsRowVersion();

        entity.HasIndex(x => new { x.TenantId, x.BatchNumber })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        entity.HasIndex(x => new { x.TenantId, x.ApprovalStatus, x.FiscalPeriodId });
        entity.HasIndex(x => new { x.TenantId, x.PostingStatus, x.FiscalPeriodId });
        entity.HasIndex(x => new { x.TenantId, x.CreatedAt });
        entity.HasIndex(x => new { x.TenantId, x.ReversalOfJournalBatchId })
            .IsUnique()
            .HasFilter("[ReversalOfJournalBatchId] IS NOT NULL AND [IsDeleted] = 0 AND [IsVoided] = 0");

        entity.HasOne(x => x.FiscalPeriod)
            .WithMany()
            .HasForeignKey(x => x.FiscalPeriodId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ReversalOfJournalBatch)
            .WithMany(x => x.ReversalAttempts)
            .HasForeignKey(x => x.ReversalOfJournalBatchId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class JournalBatchItemConfiguration : IEntityTypeConfiguration<JournalBatchItem>
{
    public void Configure(EntityTypeBuilder<JournalBatchItem> entity)
    {
        entity.ToTable("JournalBatchItems", table =>
        {
            table.HasTrigger("TR_JournalBatchItems_OptionalApproval");
            table.HasCheckConstraint("CK_JournalBatchItems_SequenceNumber", "[SequenceNumber] > 0");
        });

        entity.Property(x => x.ReviewStatus).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.PostingStatus).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.RowVersion).IsRowVersion();

        entity.HasIndex(x => new { x.TenantId, x.JournalEntryId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        entity.HasIndex(x => new { x.TenantId, x.JournalBatchId, x.SequenceNumber })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        entity.HasIndex(x => new { x.TenantId, x.JournalBatchId, x.ReviewStatus, x.PostingStatus });
        entity.HasIndex(x => new { x.TenantId, x.PostingClaimRunId });

        entity.HasOne(x => x.JournalBatch)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.JournalBatchId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.JournalEntry)
            .WithOne(x => x.JournalBatchItem)
            .HasForeignKey<JournalBatchItem>(x => x.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.PostedInRun)
            .WithMany()
            .HasForeignKey(x => x.PostedInRunId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.PostingClaimRun)
            .WithMany()
            .HasForeignKey(x => x.PostingClaimRunId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ReversalJournalBatchItem)
            .WithMany()
            .HasForeignKey(x => x.ReversalJournalBatchItemId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class JournalBatchItemReviewConfiguration : IEntityTypeConfiguration<JournalBatchItemReview>
{
    public void Configure(EntityTypeBuilder<JournalBatchItemReview> entity)
    {
        entity.ToTable("JournalBatchItemReviews");
        entity.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20);
        entity.HasIndex(x => new { x.TenantId, x.JournalBatchItemId, x.WorkflowInstanceId, x.WorkflowStageKey })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        entity.HasOne(x => x.JournalBatchItem)
            .WithMany(x => x.Reviews)
            .HasForeignKey(x => x.JournalBatchItemId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class JournalBatchPostingRunConfiguration : IEntityTypeConfiguration<JournalBatchPostingRun>
{
    public void Configure(EntityTypeBuilder<JournalBatchPostingRun> entity)
    {
        entity.ToTable("JournalBatchPostingRuns", table =>
        {
            table.HasCheckConstraint("CK_JournalBatchPostingRuns_RunNumber", "[RunNumber] > 0");
            table.HasCheckConstraint("CK_JournalBatchPostingRuns_SelectedEntryCount", "[SelectedEntryCount] > 0");
        });

        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => new { x.TenantId, x.JournalBatchId, x.RunNumber }).IsUnique();
        entity.HasIndex(x => new { x.TenantId, x.JournalBatchId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        entity.HasOne(x => x.JournalBatch)
            .WithMany(x => x.PostingRuns)
            .HasForeignKey(x => x.JournalBatchId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class JournalBatchPostingRunItemConfiguration : IEntityTypeConfiguration<JournalBatchPostingRunItem>
{
    public void Configure(EntityTypeBuilder<JournalBatchPostingRunItem> entity)
    {
        entity.ToTable("JournalBatchPostingRunItems");
        entity.HasIndex(x => new { x.TenantId, x.JournalBatchPostingRunId, x.JournalBatchItemId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        entity.HasOne(x => x.PostingRun)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.JournalBatchPostingRunId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.JournalBatchItem)
            .WithMany(x => x.PostingRunItems)
            .HasForeignKey(x => x.JournalBatchItemId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.FinancePostingEvent)
            .WithMany()
            .HasForeignKey(x => x.FinancePostingEventId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class JournalBatchAttachmentConfiguration : IEntityTypeConfiguration<JournalBatchAttachment>
{
    public void Configure(EntityTypeBuilder<JournalBatchAttachment> entity)
    {
        entity.ToTable("JournalBatchAttachments");
        entity.HasIndex(x => new { x.TenantId, x.JournalBatchId, x.FileUploadRecordId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        entity.HasOne(x => x.JournalBatch)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.JournalBatchId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.FileUploadRecord)
            .WithMany()
            .HasForeignKey(x => x.FileUploadRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class JournalBatchImportSessionConfiguration : IEntityTypeConfiguration<JournalBatchImportSession>
{
    public void Configure(EntityTypeBuilder<JournalBatchImportSession> entity)
    {
        entity.ToTable("JournalBatchImportSessions");
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        entity.HasIndex(x => new { x.TenantId, x.PreviewTokenHash }).IsUnique();
        entity.HasIndex(x => new { x.TenantId, x.UploadedByUserId, x.ExpiresAt });
        entity.HasIndex(x => new { x.TenantId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL AND [IsDeleted] = 0");
        entity.HasOne(x => x.CommittedJournalBatch)
            .WithMany()
            .HasForeignKey(x => x.CommittedJournalBatchId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
