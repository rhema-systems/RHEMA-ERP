using ErpSystem.Core.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

/// <summary>
/// Maps the GL period balance snapshot used by financial reports, ratios, and allocation runs.
/// FinancePostingEngine remains the source of posted movement; this table is the period-balance
/// read model that allocation rules use when calculating an approvable run batch.
/// </summary>
public sealed class AccountBalanceConfiguration : IEntityTypeConfiguration<AccountBalance>
{
    public void Configure(EntityTypeBuilder<AccountBalance> builder)
    {
        builder.ToTable("AccountBalances");

        builder.HasIndex(balance => new
        {
            balance.TenantId,
            balance.AccountId,
            balance.AccountingBookId,
            balance.FiscalPeriodId,
            balance.Currency
        }).IsUnique()
            .HasFilter(null);
        builder.HasIndex(balance => new { balance.TenantId, balance.FiscalPeriodId, balance.BookClassification });
        builder.HasIndex(balance => new { balance.TenantId, balance.AccountId, balance.BookClassification });
        builder.HasIndex(balance => new { balance.TenantId, balance.LastUpdated });
        builder.HasIndex(balance => new { balance.TenantId, balance.IsReconciled });
        builder.HasIndex(balance => new { balance.DepartmentSegment, balance.FiscalPeriodId });
        builder.HasIndex(balance => new { balance.CostCenterSegment, balance.FiscalPeriodId });
        builder.HasIndex(balance => new { balance.ProjectSegment, balance.FiscalPeriodId });

        builder.Property(balance => balance.BookClassification)
            .IsRequired()
            .HasMaxLength(20);
        builder.Property(balance => balance.Currency)
            .HasMaxLength(3)
            .IsRequired();
        builder.Property(balance => balance.OpeningBalance)
            .HasColumnType("decimal(18,2)");
        builder.Property(balance => balance.OpeningBalanceType)
            .IsRequired()
            .HasMaxLength(2);
        builder.Property(balance => balance.PeriodDebits)
            .HasColumnType("decimal(18,2)");
        builder.Property(balance => balance.PeriodCredits)
            .HasColumnType("decimal(18,2)");
        builder.Property(balance => balance.PeriodNetMovement)
            .HasColumnType("decimal(18,2)");
        builder.Property(balance => balance.ClosingBalance)
            .HasColumnType("decimal(18,2)");
        builder.Property(balance => balance.ClosingBalanceType)
            .IsRequired()
            .HasMaxLength(2);
        builder.Property(balance => balance.YearToDateDebits)
            .HasColumnType("decimal(18,2)");
        builder.Property(balance => balance.YearToDateCredits)
            .HasColumnType("decimal(18,2)");
        builder.Property(balance => balance.YearToDateNetMovement)
            .HasColumnType("decimal(18,2)");
        builder.Property(balance => balance.SegmentString)
            .HasMaxLength(200);
        builder.Property(balance => balance.DepartmentSegment)
            .HasMaxLength(20);
        builder.Property(balance => balance.CostCenterSegment)
            .HasMaxLength(20);
        builder.Property(balance => balance.ProjectSegment)
            .HasMaxLength(20);
        builder.Property(balance => balance.LocationSegment)
            .HasMaxLength(20);
        builder.Property(balance => balance.ExchangeRate)
            .HasColumnType("decimal(18,6)");
        builder.Property(balance => balance.BaseCurrencyEquivalent)
            .HasColumnType("decimal(18,2)");
        builder.Property(balance => balance.UnrealizedGainLoss)
            .HasColumnType("decimal(18,2)");
        builder.Property(balance => balance.ReconciliationDiscrepancy)
            .HasColumnType("decimal(18,2)");
        builder.Property(balance => balance.Notes)
            .HasMaxLength(1000);

        builder.HasOne(balance => balance.Account)
            .WithMany()
            .HasForeignKey(balance => new { balance.TenantId, balance.AccountId })
            .HasPrincipalKey(account => new { account.TenantId, account.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(balance => balance.FiscalPeriod)
            .WithMany()
            .HasForeignKey(balance => new { balance.TenantId, balance.FiscalPeriodId })
            .HasPrincipalKey(period => new { period.TenantId, period.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(balance => balance.AccountingBook)
            .WithMany()
            .HasForeignKey(balance => new { balance.TenantId, balance.AccountingBookId })
            .HasPrincipalKey(book => new { book.TenantId, book.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
