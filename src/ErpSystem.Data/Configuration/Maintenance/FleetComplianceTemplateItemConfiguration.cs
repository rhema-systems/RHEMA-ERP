using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Maintenance;

public sealed class FleetComplianceTemplateItemConfiguration : IEntityTypeConfiguration<FleetComplianceTemplateItem>
{
    public void Configure(EntityTypeBuilder<FleetComplianceTemplateItem> builder)
    {
        builder.ToTable("FleetComplianceTemplateItems");

        builder.HasIndex(x => new { x.TenantId, x.TemplateId });
        builder.HasIndex(x => new { x.TenantId, x.TemplateId, x.ComplianceType })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(x => x.ComplianceType).IsRequired().HasMaxLength(100);
    }
}
