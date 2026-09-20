// HR → Finance posting (HR finish plan lane 8): the account-role mappings, the per-event rules and
// the posting register. Kept in its own partial so the HR partial's single-call convention holds
// (ConfigureHrModule calls ConfigureHrFinancePostingEntities) and so a Finance reviewer can read
// HR's whole footprint on the boundary in one file.
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data;

public partial class ApplicationDbContext
{
    public DbSet<HrFinanceAccountMapping> HrFinanceAccountMappings { get; set; } = null!;
    public DbSet<HrFinancePostingRule> HrFinancePostingRules { get; set; } = null!;
    public DbSet<HrFinancePostingRecord> HrFinancePostingRecords { get; set; } = null!;

    private void ConfigureHrFinancePostingEntities(ModelBuilder builder)
    {
        builder.Entity<HrFinanceAccountMapping>(entity =>
        {
            entity.ToTable("HrFinanceAccountMappings");

            // One live mapping per role per tenant.
            entity.HasIndex(x => new { x.TenantId, x.Role })
                  .IsUnique()
                  .HasFilter("[IsDeleted] = 0")
                  .HasDatabaseName("UX_HrFinanceAccountMappings_Tenant_Role");

            // Restrict, as on OrganizationUnit.FinanceAccountId: Finance cannot delete an account HR
            // posts to without HR re-mapping the role first. No navigation — the account is read
            // through IHrFinancePostingStore, never through a fixup-prone graph.
            entity.HasOne<Account>()
                  .WithMany()
                  .HasForeignKey(x => x.AccountId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<HrFinancePostingRule>(entity =>
        {
            entity.ToTable("HrFinancePostingRules");
            entity.Property(x => x.EventCode).HasMaxLength(60);

            entity.HasIndex(x => new { x.TenantId, x.EventCode })
                  .IsUnique()
                  .HasFilter("[IsDeleted] = 0")
                  .HasDatabaseName("UX_HrFinancePostingRules_Tenant_Event");
        });

        builder.Entity<HrFinancePostingRecord>(entity =>
        {
            entity.ToTable("HrFinancePostingRecords");
            entity.Property(x => x.EventCode).HasMaxLength(60);
            entity.Property(x => x.SourceDocumentType).HasMaxLength(100);
            entity.Property(x => x.SourceReference).HasMaxLength(100);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(200);
            entity.Property(x => x.PostingAction).HasMaxLength(50);

            // One row per event per source document; a retry updates it.
            entity.HasIndex(x => new { x.TenantId, x.EventCode, x.SourceDocumentId })
                  .IsUnique()
                  .HasFilter("[IsDeleted] = 0")
                  .HasDatabaseName("UX_HrFinancePostingRecords_Tenant_Event_Source");

            // The register's working reads: by source (detail cards), by status (the queue), by
            // journal (drill-through from Finance).
            entity.HasIndex(x => new { x.TenantId, x.SourceDocumentId })
                  .HasDatabaseName("IX_HrFinancePostingRecords_Tenant_Source");
            entity.HasIndex(x => new { x.TenantId, x.Status, x.PostingDate })
                  .HasDatabaseName("IX_HrFinancePostingRecords_Tenant_Status_Date");
            entity.HasIndex(x => x.JournalEntryId)
                  .HasDatabaseName("IX_HrFinancePostingRecords_JournalEntryId");

            // ⚠ No FK to FinancePostingEvents / JournalEntries. The identities are back-references
            // HR keeps for drill-through; Finance's own reversal and archival policy must not be
            // constrained by an HR table, and an HR row must survive whatever Finance does next.
        });
    }
}
