using ErpSystem.Core.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class AccountCurrencyExposureConfiguration : IEntityTypeConfiguration<AccountCurrencyExposure>
{
    public void Configure(EntityTypeBuilder<AccountCurrencyExposure> builder)
    {
        builder.ToTable("AccountCurrencyExposures", table =>
        {
            table.HasCheckConstraint("CK_AccountCurrencyExposures_DistinctCurrency", "[TransactionCurrencyCode] <> [FunctionalCurrencyCode]");
            table.HasCheckConstraint("CK_AccountCurrencyExposures_TransactionCount", "[TransactionCount] >= 0");
        });
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.AccountId,
            item.AccountingBookId,
            item.TransactionCurrencyCode
        }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.Property(item => item.AccountingBookCode).HasMaxLength(20).IsRequired();
        builder.Property(item => item.FunctionalCurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(item => item.TransactionCurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(item => item.SignedForeignBalance).HasColumnType("decimal(18,2)");
        builder.Property(item => item.SignedFunctionalBalance).HasColumnType("decimal(18,2)");
        builder.Property(item => item.SourceFingerprint).HasMaxLength(64).IsRequired();
        builder.HasOne(item => item.Account)
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.AccountId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.AccountingBook)
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.AccountingBookId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
