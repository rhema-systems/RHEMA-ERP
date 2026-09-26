using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Configurations;

public static class BusinessPartnerPostingDefaultsConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<SubledgerAdjustmentJournal>().HasOne<Account>().WithMany()
            .HasForeignKey(x => x.ControlAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<VendorInvoice>().Property(invoice => invoice.WithholdingDecisionPending).HasDefaultValue(false);
        builder.Entity<BusinessPartner>(entity =>
        {
            entity.Property(x => x.CashAccountSource).HasMaxLength(20).HasDefaultValue("Chequebook");
            entity.Property(x => x.WithholdingTaxRate).HasPrecision(18, 4);
            entity.HasOne<Tax>().WithMany().HasForeignKey(x => x.DefaultWithholdingTaxId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TaxGroup>().WithMany().HasForeignKey(x => x.DefaultTaxGroupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<BankAccount>().WithMany().HasForeignKey(x => x.DefaultBankAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.DefaultCashAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.DefaultTermsDiscountsAvailableAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.DefaultTermsDiscountsTakenAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.DefaultFinanceChargesAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.DefaultTradeDiscountAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.DefaultMiscellaneousAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.DefaultFreightAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.DefaultTaxAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.DefaultWriteoffAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.DefaultAccruedPurchasesAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.DefaultPurchasePriceVarianceAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.CustomerSalesAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.CustomerCostOfSalesAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.CustomerInventoryAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.CustomerTermsDiscountsTakenAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.CustomerSalesReturnsAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.CustomerFinanceChargesAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.CustomerWriteoffAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.CustomerOverpaymentWriteoffAccountId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
