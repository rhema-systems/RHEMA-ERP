using ErpSystem.Core.Entities.Ehc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Ehc;

public sealed class EhcPublicPropertyEnquiryContactConfiguration : IEntityTypeConfiguration<EhcPublicPropertyEnquiryContact>
{
    public void Configure(EntityTypeBuilder<EhcPublicPropertyEnquiryContact> builder)
    {
        builder.HasIndex(item => new { item.TenantId, item.Channel, item.NormalizedContact })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.BusinessPartnerId });
        builder.HasOne(item => item.BusinessPartner)
            .WithMany()
            .HasForeignKey(item => item.BusinessPartnerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class EhcPublicPropertyEnquiryVerificationConfiguration : IEntityTypeConfiguration<EhcPublicPropertyEnquiryVerification>
{
    public void Configure(EntityTypeBuilder<EhcPublicPropertyEnquiryVerification> builder)
    {
        builder.HasIndex(item => new { item.TenantId, item.ListingId, item.Channel, item.ContactHash, item.RequestedAtUtc });
        builder.HasIndex(item => item.VerificationTokenHash)
            .IsUnique()
            .HasFilter("[VerificationTokenHash] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasOne(item => item.Contact)
            .WithMany()
            .HasForeignKey(item => item.ContactId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
