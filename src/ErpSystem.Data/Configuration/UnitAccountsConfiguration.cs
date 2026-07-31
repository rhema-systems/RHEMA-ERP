using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Data.Configuration;

/// <summary>
/// Entity Framework configuration for UnitType entity.
/// </summary>
public class UnitTypeConfiguration : IEntityTypeConfiguration<UnitType>
{
    public void Configure(EntityTypeBuilder<UnitType> builder)
    {
        builder.ToTable("UnitTypes");

        builder.HasIndex(ut => new { ut.TenantId, ut.Code }).IsUnique();
        builder.HasIndex(ut => ut.IsActive);

        builder.Property(ut => ut.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(ut => ut.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ut => ut.Description)
            .HasMaxLength(500);

        // Relationship to UnitAccounts
        builder.HasMany(ut => ut.UnitAccounts)
            .WithOne(ua => ua.UnitType)
            .HasForeignKey(ua => ua.UnitTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Entity Framework configuration for UnitAccount entity.
/// </summary>
public class UnitAccountConfiguration : IEntityTypeConfiguration<UnitAccount>
{
    public void Configure(EntityTypeBuilder<UnitAccount> builder)
    {
        builder.ToTable("UnitAccounts");

        builder.HasIndex(ua => new { ua.TenantId, ua.AccountNumber }).IsUnique();
        builder.HasIndex(ua => ua.UnitTypeId);
        builder.HasIndex(ua => ua.ParentAccountId);
        builder.HasIndex(ua => ua.IsActive);
        builder.HasIndex(ua => ua.IsPostingAccount);

        builder.Property(ua => ua.AccountNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(ua => ua.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ua => ua.Description)
            .HasMaxLength(500);

        builder.Property(ua => ua.CurrentBalance)
            .HasColumnType("decimal(18,6)");

        // Self-referencing hierarchy
        builder.HasOne(ua => ua.ParentAccount)
            .WithMany(ua => ua.ChildAccounts)
            .HasForeignKey(ua => ua.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship to JournalEntryLines
        builder.HasMany(ua => ua.JournalEntryLines)
            .WithOne(jel => jel.UnitAccount)
            .HasForeignKey(jel => jel.UnitAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship to Balances
        builder.HasMany(ua => ua.Balances)
            .WithOne(b => b.UnitAccount)
            .HasForeignKey(b => b.UnitAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Entity Framework configuration for UnitJournalEntry entity.
/// </summary>
public class UnitJournalEntryConfiguration : IEntityTypeConfiguration<UnitJournalEntry>
{
    public void Configure(EntityTypeBuilder<UnitJournalEntry> builder)
    {
        builder.ToTable("UnitJournalEntries");

        builder.HasIndex(uje => new { uje.TenantId, uje.EntryNumber }).IsUnique();
        builder.HasIndex(uje => uje.EntryDate);
        builder.HasIndex(uje => uje.Status);
        builder.HasIndex(uje => uje.FiscalYearId);
        builder.HasIndex(uje => uje.FiscalPeriodId);

        builder.Property(uje => uje.EntryNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(uje => uje.Description)
            .HasMaxLength(500);

        builder.Property(uje => uje.SourceDocument)
            .HasMaxLength(200);

        builder.Property(uje => uje.ApprovedByName)
            .HasMaxLength(100);

        builder.Property(uje => uje.PostedByName)
            .HasMaxLength(100);

        builder.Property(uje => uje.RejectionReason)
            .HasMaxLength(500);

        builder.Property(uje => uje.ReversalReason)
            .HasMaxLength(500);

        // Convert enum to int
        builder.Property(uje => uje.Status)
            .HasConversion<int>();

        // Relationship to FiscalYear
        builder.HasOne(uje => uje.FiscalYear)
            .WithMany()
            .HasForeignKey(uje => uje.FiscalYearId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship to FiscalPeriod
        builder.HasOne(uje => uje.FiscalPeriod)
            .WithMany()
            .HasForeignKey(uje => uje.FiscalPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship to Lines
        builder.HasMany(uje => uje.Lines)
            .WithOne(l => l.UnitJournalEntry)
            .HasForeignKey(l => l.UnitJournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Entity Framework configuration for UnitJournalEntryLine entity.
/// </summary>
public class UnitJournalEntryLineConfiguration : IEntityTypeConfiguration<UnitJournalEntryLine>
{
    public void Configure(EntityTypeBuilder<UnitJournalEntryLine> builder)
    {
        builder.ToTable("UnitJournalEntryLines");

        builder.HasIndex(l => l.UnitJournalEntryId);
        builder.HasIndex(l => l.UnitAccountId);

        builder.Property(l => l.Quantity)
            .HasColumnType("decimal(18,6)");

        builder.Property(l => l.Description)
            .HasMaxLength(300);
    }
}

/// <summary>
/// Entity Framework configuration for UnitAccountBalance entity.
/// </summary>
public class UnitAccountBalanceConfiguration : IEntityTypeConfiguration<UnitAccountBalance>
{
    public void Configure(EntityTypeBuilder<UnitAccountBalance> builder)
    {
        builder.ToTable("UnitAccountBalances");

        builder.HasIndex(b => new { b.UnitAccountId, b.FiscalPeriodId }).IsUnique();
        builder.HasIndex(b => b.FiscalYearId);
        builder.HasIndex(b => b.FiscalPeriodId);

        builder.Property(b => b.OpeningBalance)
            .HasColumnType("decimal(18,6)");

        builder.Property(b => b.PeriodActivity)
            .HasColumnType("decimal(18,6)");

        builder.Property(b => b.ClosingBalance)
            .HasColumnType("decimal(18,6)");

        // Relationship to FiscalYear
        builder.HasOne(b => b.FiscalYear)
            .WithMany()
            .HasForeignKey(b => b.FiscalYearId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship to FiscalPeriod
        builder.HasOne(b => b.FiscalPeriod)
            .WithMany()
            .HasForeignKey(b => b.FiscalPeriodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Entity Framework configuration for RatioDefinition entity.
/// </summary>
public class RatioDefinitionConfiguration : IEntityTypeConfiguration<RatioDefinition>
{
    public void Configure(EntityTypeBuilder<RatioDefinition> builder)
    {
        builder.ToTable("RatioDefinitions");

        builder.HasIndex(rd => new { rd.TenantId, rd.Code }).IsUnique();
        builder.HasIndex(rd => rd.IsActive);

        builder.Property(rd => rd.Code)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(rd => rd.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(rd => rd.Description)
            .HasMaxLength(500);

        builder.Property(rd => rd.NumeratorConstant)
            .HasColumnType("decimal(18,6)");

        builder.Property(rd => rd.DenominatorConstant)
            .HasColumnType("decimal(18,6)");

        // Convert enums to int
        builder.Property(rd => rd.NumeratorType)
            .HasConversion<int>();

        builder.Property(rd => rd.DenominatorType)
            .HasConversion<int>();

        builder.Property(rd => rd.ResultFormat)
            .HasConversion<int>();
    }
}

/// <summary>
/// Entity Framework configuration for UnitAccountBudget entity.
/// </summary>
public class UnitAccountBudgetConfiguration : IEntityTypeConfiguration<UnitAccountBudget>
{
    public void Configure(EntityTypeBuilder<UnitAccountBudget> builder)
    {
        builder.ToTable("UnitAccountBudgets");

        builder.HasIndex(b => new { b.UnitAccountId, b.FiscalPeriodId, b.BudgetVersion }).IsUnique();
        builder.HasIndex(b => b.FiscalYearId);
        builder.HasIndex(b => b.FiscalPeriodId);
        builder.HasIndex(b => b.IsActive);

        builder.Property(b => b.BudgetQuantity)
            .HasColumnType("decimal(18,6)");

        builder.Property(b => b.Notes)
            .HasMaxLength(500);

        builder.Property(b => b.BudgetVersion)
            .HasMaxLength(50);

        // Relationship to UnitAccount
        builder.HasOne(b => b.UnitAccount)
            .WithMany()
            .HasForeignKey(b => b.UnitAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship to FiscalYear
        builder.HasOne(b => b.FiscalYear)
            .WithMany()
            .HasForeignKey(b => b.FiscalYearId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship to FiscalPeriod
        builder.HasOne(b => b.FiscalPeriod)
            .WithMany()
            .HasForeignKey(b => b.FiscalPeriodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Entity Framework configuration for AllocationRule entity.
/// </summary>
public class AllocationRuleConfiguration : IEntityTypeConfiguration<AllocationRule>
{
    public void Configure(EntityTypeBuilder<AllocationRule> builder)
    {
        builder.ToTable("AllocationRules");

        builder.HasIndex(ar => new { ar.TenantId, ar.Code }).IsUnique();
        builder.HasIndex(ar => ar.IsActive);

        builder.Property(ar => ar.Code)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(ar => ar.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ar => ar.Description)
            .HasMaxLength(500);

        // Convert enum to int
        builder.Property(ar => ar.AllocationType)
            .HasConversion<int>();

        // Relationship to Source GL Account
        builder.HasOne(ar => ar.SourceAccount)
            .WithMany()
            .HasForeignKey(ar => ar.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship to Driver Unit Account
        builder.HasOne(ar => ar.DriverUnitAccount)
            .WithMany()
            .HasForeignKey(ar => ar.DriverUnitAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship to Targets
        builder.HasMany(ar => ar.Targets)
            .WithOne(t => t.AllocationRule)
            .HasForeignKey(t => t.AllocationRuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Entity Framework configuration for AllocationTarget entity.
/// </summary>
public class AllocationTargetConfiguration : IEntityTypeConfiguration<AllocationTarget>
{
    public void Configure(EntityTypeBuilder<AllocationTarget> builder)
    {
        builder.ToTable("AllocationTargets");

        builder.HasIndex(at => at.AllocationRuleId);
        builder.HasIndex(at => at.TargetAccountId);

        builder.Property(at => at.FixedPercentage)
            .HasColumnType("decimal(5,2)");

        builder.Property(at => at.CostCenterCode)
            .HasMaxLength(50);

        // Relationship to Target GL Account
        builder.HasOne(at => at.TargetAccount)
            .WithMany()
            .HasForeignKey(at => at.TargetAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship to Target Driver Unit Account
        builder.HasOne(at => at.TargetDriverUnitAccount)
            .WithMany()
            .HasForeignKey(at => at.TargetDriverUnitAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Entity Framework configuration for AllocationRunBatch entity.
/// </summary>
public class AllocationRunBatchConfiguration : IEntityTypeConfiguration<AllocationRunBatch>
{
    public void Configure(EntityTypeBuilder<AllocationRunBatch> builder)
    {
        builder.ToTable("AllocationRunBatches");

        builder.HasIndex(b => new { b.TenantId, b.BatchNumber }).IsUnique();
        builder.HasIndex(b => new { b.TenantId, b.AllocationRuleId, b.FiscalPeriodId, b.Status });
        builder.HasIndex(b => b.WorkflowInstanceId);
        builder.HasIndex(b => b.JournalEntryId);
        builder.HasIndex(b => b.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");

        builder.Property(b => b.BatchNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(b => b.Description)
            .HasMaxLength(500);

        builder.Property(b => b.Status)
            .HasConversion<int>();

        builder.Property(b => b.AllocationType)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(b => b.BookClassification)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(b => b.FunctionalCurrencyCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(b => b.SourcePeriodBalance)
            .HasColumnType("decimal(18,2)");

        builder.Property(b => b.TotalAllocated)
            .HasColumnType("decimal(18,2)");

        builder.Property(b => b.JournalEntryNumber)
            .HasMaxLength(50);

        builder.Property(b => b.SubmittedByName)
            .HasMaxLength(100);

        builder.Property(b => b.ApprovedByName)
            .HasMaxLength(100);

        builder.Property(b => b.PostedByName)
            .HasMaxLength(100);

        builder.Property(b => b.RejectionReason)
            .HasMaxLength(500);

        builder.Property(b => b.IdempotencyKey)
            .HasMaxLength(200);

        builder.HasOne(b => b.AllocationRule)
            .WithMany()
            .HasForeignKey(b => b.AllocationRuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.FiscalPeriod)
            .WithMany()
            .HasForeignKey(b => b.FiscalPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.SourceAccount)
            .WithMany()
            .HasForeignKey(b => b.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(b => b.Lines)
            .WithOne(l => l.AllocationRunBatch)
            .HasForeignKey(l => l.AllocationRunBatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Entity Framework configuration for AllocationRunBatchLine entity.
/// </summary>
public class AllocationRunBatchLineConfiguration : IEntityTypeConfiguration<AllocationRunBatchLine>
{
    public void Configure(EntityTypeBuilder<AllocationRunBatchLine> builder)
    {
        builder.ToTable("AllocationRunBatchLines");

        builder.HasIndex(l => l.AllocationRunBatchId);
        builder.HasIndex(l => l.TargetAccountId);

        builder.Property(l => l.AllocationBasis)
            .HasColumnType("decimal(18,6)");

        builder.Property(l => l.AllocationPercent)
            .HasColumnType("decimal(9,4)");

        builder.Property(l => l.AllocatedAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.CostCenterCode)
            .HasMaxLength(50);

        builder.HasOne(l => l.TargetAccount)
            .WithMany()
            .HasForeignKey(l => l.TargetAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.TargetDriverUnitAccount)
            .WithMany()
            .HasForeignKey(l => l.TargetDriverUnitAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
