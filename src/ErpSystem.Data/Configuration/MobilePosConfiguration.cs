using ErpSystem.Core.Entities.MobilePos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class MobilePosStoreConfiguration : IEntityTypeConfiguration<MobilePosStore>
{
    public void Configure(EntityTypeBuilder<MobilePosStore> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.Code }).IsUnique()
            .HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.Status });
        entity.HasIndex(item => new { item.TenantId, item.DefaultWalkInBusinessPartnerId });
        entity.HasOne(item => item.CompanyProfile).WithMany().HasForeignKey(item => item.CompanyProfileId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.Location).WithMany().HasForeignKey(item => item.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.Warehouse).WithMany().HasForeignKey(item => item.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.DefaultWalkInBusinessPartner).WithMany()
            .HasForeignKey(item => item.DefaultWalkInBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.DefaultWalkInBusinessPartnerRole).WithMany()
            .HasForeignKey(item => item.DefaultWalkInBusinessPartnerRoleId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.OfflinePolicy).WithMany().HasForeignKey(item => item.OfflinePolicyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MobilePosStoreDimensionDefaultConfiguration : IEntityTypeConfiguration<MobilePosStoreDimensionDefault>
{
    public void Configure(EntityTypeBuilder<MobilePosStoreDimensionDefault> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.MobilePosStoreId, item.FinanceDimensionDefinitionId })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        entity.HasOne(item => item.MobilePosStore).WithMany(item => item.DimensionDefaults)
            .HasForeignKey(item => item.MobilePosStoreId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.FinanceDimensionDefinition).WithMany()
            .HasForeignKey(item => item.FinanceDimensionDefinitionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.FinanceDimensionValue).WithMany()
            .HasForeignKey(item => item.FinanceDimensionValueId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MobilePosTillConfiguration : IEntityTypeConfiguration<MobilePosTill>
{
    public void Configure(EntityTypeBuilder<MobilePosTill> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.TillNumber }).IsUnique()
            .HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.LiquidityAccountId }).IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [Status] <> 4");
        entity.HasOne(item => item.MobilePosStore).WithMany(item => item.Tills)
            .HasForeignKey(item => item.MobilePosStoreId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.LiquidityAccount).WithMany()
            .HasForeignKey(item => item.LiquidityAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MobilePosTillPaymentMethodConfiguration : IEntityTypeConfiguration<MobilePosTillPaymentMethod>
{
    public void Configure(EntityTypeBuilder<MobilePosTillPaymentMethod> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.MobilePosTillId, item.PaymentMethodId })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        entity.HasOne(item => item.MobilePosTill).WithMany(item => item.PaymentMethods)
            .HasForeignKey(item => item.MobilePosTillId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.PaymentMethod).WithMany()
            .HasForeignKey(item => item.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MobilePosUserStoreAssignmentConfiguration : IEntityTypeConfiguration<MobilePosUserStoreAssignment>
{
    public void Configure(EntityTypeBuilder<MobilePosUserStoreAssignment> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.UserId }).IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [IsActive] = 1");
        entity.HasIndex(item => new { item.TenantId, item.MobilePosStoreId, item.IsActive });
        entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosStore).WithMany().HasForeignKey(item => item.MobilePosStoreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MobilePosOfflinePolicyConfiguration : IEntityTypeConfiguration<MobilePosOfflinePolicy>
{
    public void Configure(EntityTypeBuilder<MobilePosOfflinePolicy> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.Name }).IsUnique()
            .HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.IsActive });
    }
}

public sealed class MobilePosDeviceConfiguration : IEntityTypeConfiguration<MobilePosDevice>
{
    public void Configure(EntityTypeBuilder<MobilePosDevice> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.InstallationIdHash }).IsUnique()
            .HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.Status, item.LastSeenAtUtc });
        entity.HasIndex(item => new { item.TenantId, item.MobilePosTillId }).IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [MobilePosTillId] IS NOT NULL AND [Status] = 2");
        entity.HasOne(item => item.RequestedByUser).WithMany().HasForeignKey(item => item.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.ApprovedByUser).WithMany().HasForeignKey(item => item.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.RevokedByUser).WithMany().HasForeignKey(item => item.RevokedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosStore).WithMany().HasForeignKey(item => item.MobilePosStoreId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosTill).WithMany().HasForeignKey(item => item.MobilePosTillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MobilePosDeviceAssignmentHistoryConfiguration : IEntityTypeConfiguration<MobilePosDeviceAssignmentHistory>
{
    public void Configure(EntityTypeBuilder<MobilePosDeviceAssignmentHistory> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.MobilePosDeviceId, item.ChangedAtUtc });
        entity.HasOne(item => item.MobilePosDevice).WithMany().HasForeignKey(item => item.MobilePosDeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MobilePosOfflineGrantConfiguration : IEntityTypeConfiguration<MobilePosOfflineGrant>
{
    public void Configure(EntityTypeBuilder<MobilePosOfflineGrant> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.MobilePosDeviceId, item.Status, item.ExpiresAtUtc });
        entity.HasIndex(item => new { item.TenantId, item.CashierTillSessionId, item.Status });
        entity.HasOne(item => item.MobilePosDevice).WithMany().HasForeignKey(item => item.MobilePosDeviceId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosOfflinePolicy).WithMany()
            .HasForeignKey(item => item.MobilePosOfflinePolicyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MobilePosSaleConfiguration : IEntityTypeConfiguration<MobilePosSale>
{
    public void Configure(EntityTypeBuilder<MobilePosSale> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.MobilePosDeviceId, item.ClientMutationId })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.MobilePosDeviceId, item.LocalReference })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.MobilePosStoreId, item.BusinessDate, item.Status });
        entity.HasIndex(item => new { item.TenantId, item.InvoiceId })
            .IsUnique().HasFilter("[IsDeleted] = 0 AND [InvoiceId] IS NOT NULL");
        entity.Property(item => item.CurrencyCode).IsUnicode(false);
        entity.Property(item => item.OfflinePolicySnapshotHash).IsUnicode(false);

        entity.HasOne(item => item.MobilePosStore).WithMany()
            .HasForeignKey(item => item.MobilePosStoreId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosTill).WithMany()
            .HasForeignKey(item => item.MobilePosTillId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.CashierTillSession).WithMany()
            .HasForeignKey(item => item.CashierTillSessionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosDevice).WithMany()
            .HasForeignKey(item => item.MobilePosDeviceId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.OperatorUser).WithMany()
            .HasForeignKey(item => item.OperatorUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.BusinessPartner).WithMany()
            .HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.BusinessPartnerRole).WithMany()
            .HasForeignKey(item => item.BusinessPartnerRoleId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosOfflineGrant).WithMany()
            .HasForeignKey(item => item.MobilePosOfflineGrantId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.Invoice).WithMany()
            .HasForeignKey(item => item.InvoiceId).OnDelete(DeleteBehavior.Restrict);

        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_MobilePosSales_Amounts",
                "[SubTotal] >= 0 AND [TaxAmount] >= 0 AND [DiscountAmount] >= 0 AND [TotalAmount] >= 0");
            table.HasCheckConstraint("CK_MobilePosSales_PolicyHash",
                "[OfflinePolicySnapshotHash] IS NULL OR LEN([OfflinePolicySnapshotHash]) = 64");
        });
    }
}

public sealed class MobilePosSaleLineConfiguration : IEntityTypeConfiguration<MobilePosSaleLine>
{
    public void Configure(EntityTypeBuilder<MobilePosSaleLine> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.MobilePosSaleId, item.Sequence })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.MobilePosSaleId, item.ClientLineId })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        entity.HasOne(item => item.MobilePosSale).WithMany(item => item.Lines)
            .HasForeignKey(item => item.MobilePosSaleId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.InventoryItem).WithMany()
            .HasForeignKey(item => item.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.TaxGroup).WithMany()
            .HasForeignKey(item => item.TaxGroupId).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable(table => table.HasCheckConstraint("CK_MobilePosSaleLines_Values",
            "[Sequence] > 0 AND [Quantity] > 0 AND [UnitPrice] >= 0 AND [DiscountAmount] >= 0 AND [TaxAmount] >= 0 AND [LineTotal] >= 0"));
    }
}

public sealed class MobilePosTenderConfiguration : IEntityTypeConfiguration<MobilePosTender>
{
    public void Configure(EntityTypeBuilder<MobilePosTender> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.MobilePosSaleId, item.Sequence })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.CustomerPaymentId })
            .IsUnique().HasFilter("[IsDeleted] = 0 AND [CustomerPaymentId] IS NOT NULL");
        entity.HasOne(item => item.MobilePosSale).WithMany(item => item.Tenders)
            .HasForeignKey(item => item.MobilePosSaleId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.PaymentMethod).WithMany()
            .HasForeignKey(item => item.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.LiquidityAccount).WithMany()
            .HasForeignKey(item => item.LiquidityAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.BankAccount).WithMany()
            .HasForeignKey(item => item.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.CustomerPayment).WithMany()
            .HasForeignKey(item => item.CustomerPaymentId).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable(table => table.HasCheckConstraint("CK_MobilePosTenders_Amount", "[Amount] > 0"));
    }
}

public sealed class MobilePosCollectionConfiguration : IEntityTypeConfiguration<MobilePosCollection>
{
    public void Configure(EntityTypeBuilder<MobilePosCollection> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.MobilePosDeviceId, item.ClientMutationId })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.MobilePosDeviceId, item.LocalReference })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.MobilePosStoreId, item.BusinessDate, item.Status });
        entity.Property(item => item.CurrencyCode).IsUnicode(false);

        entity.HasOne(item => item.MobilePosStore).WithMany()
            .HasForeignKey(item => item.MobilePosStoreId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosTill).WithMany()
            .HasForeignKey(item => item.MobilePosTillId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.CashierTillSession).WithMany()
            .HasForeignKey(item => item.CashierTillSessionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosDevice).WithMany()
            .HasForeignKey(item => item.MobilePosDeviceId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.OperatorUser).WithMany()
            .HasForeignKey(item => item.OperatorUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.BusinessPartner).WithMany()
            .HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.BusinessPartnerRole).WithMany()
            .HasForeignKey(item => item.BusinessPartnerRoleId).OnDelete(DeleteBehavior.Restrict);

        entity.ToTable(table => table.HasCheckConstraint(
            "CK_MobilePosCollections_TotalAmount", "[TotalAmount] > 0"));
    }
}

public sealed class MobilePosCollectionAllocationConfiguration : IEntityTypeConfiguration<MobilePosCollectionAllocation>
{
    public void Configure(EntityTypeBuilder<MobilePosCollectionAllocation> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.MobilePosCollectionId, item.Sequence })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.MobilePosCollectionId, item.InvoiceId })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        entity.HasOne(item => item.MobilePosCollection).WithMany(item => item.Allocations)
            .HasForeignKey(item => item.MobilePosCollectionId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.Invoice).WithMany()
            .HasForeignKey(item => item.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable(table => table.HasCheckConstraint(
            "CK_MobilePosCollectionAllocations_Values", "[Sequence] > 0 AND [Amount] > 0"));
    }
}

public sealed class MobilePosCollectionTenderConfiguration : IEntityTypeConfiguration<MobilePosCollectionTender>
{
    public void Configure(EntityTypeBuilder<MobilePosCollectionTender> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.MobilePosCollectionId, item.Sequence })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.CustomerPaymentId })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        entity.HasOne(item => item.MobilePosCollection).WithMany(item => item.Tenders)
            .HasForeignKey(item => item.MobilePosCollectionId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.PaymentMethod).WithMany()
            .HasForeignKey(item => item.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.LiquidityAccount).WithMany()
            .HasForeignKey(item => item.LiquidityAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.BankAccount).WithMany()
            .HasForeignKey(item => item.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.CustomerPayment).WithMany()
            .HasForeignKey(item => item.CustomerPaymentId).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable(table => table.HasCheckConstraint(
            "CK_MobilePosCollectionTenders_Values", "[Sequence] > 0 AND [Amount] > 0"));
    }
}

public sealed class MobileMutationReceiptConfiguration : IEntityTypeConfiguration<MobileMutationReceipt>
{
    public void Configure(EntityTypeBuilder<MobileMutationReceipt> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.MobilePosDeviceId, item.ClientMutationId })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.Status, item.LastAttemptAtUtc });
        entity.Property(item => item.RequestHash).IsUnicode(false);
        entity.Property(item => item.ResultHash).IsUnicode(false);
        entity.HasOne(item => item.MobilePosDevice).WithMany()
            .HasForeignKey(item => item.MobilePosDeviceId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosSale).WithMany()
            .HasForeignKey(item => item.MobilePosSaleId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosCollection).WithMany()
            .HasForeignKey(item => item.MobilePosCollectionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.CanonicalInvoice).WithMany()
            .HasForeignKey(item => item.CanonicalInvoiceId).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_MobileMutationReceipts_Hashes",
                "LEN([RequestHash]) = 64 AND ([ResultHash] IS NULL OR LEN([ResultHash]) = 64)");
            table.HasCheckConstraint("CK_MobileMutationReceipts_SchemaVersion", "[SchemaVersion] > 0");
        });
    }
}

public sealed class MobilePosTillCloseSubmissionConfiguration : IEntityTypeConfiguration<MobilePosTillCloseSubmission>
{
    public void Configure(EntityTypeBuilder<MobilePosTillCloseSubmission> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.CashierTillSessionId })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.Status, item.SubmittedAtUtc });
        entity.Property(item => item.PendingMutationDigest).IsUnicode(false);
        entity.HasOne(item => item.CashierTillSession).WithMany()
            .HasForeignKey(item => item.CashierTillSessionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosStore).WithMany()
            .HasForeignKey(item => item.MobilePosStoreId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosTill).WithMany()
            .HasForeignKey(item => item.MobilePosTillId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosDevice).WithMany()
            .HasForeignKey(item => item.MobilePosDeviceId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosOfflinePolicy).WithMany()
            .HasForeignKey(item => item.MobilePosOfflinePolicyId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.BankDepositBatch).WithMany()
            .HasForeignKey(item => item.BankDepositBatchId).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_MobilePosTillCloseSubmissions_PendingCount",
                "[PendingMutationCount] >= 0");
            table.HasCheckConstraint("CK_MobilePosTillCloseSubmissions_Digest",
                "LEN([PendingMutationDigest]) = 64");
        });
    }
}
