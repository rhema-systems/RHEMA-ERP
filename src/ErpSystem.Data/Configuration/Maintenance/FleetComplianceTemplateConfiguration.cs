using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Maintenance;

public sealed class FleetComplianceTemplateConfiguration : IEntityTypeConfiguration<FleetComplianceTemplate>
{
    public void Configure(EntityTypeBuilder<FleetComplianceTemplate> builder)
    {
        builder.ToTable("FleetComplianceTemplates");

        builder.HasIndex(x => new { x.TenantId, x.Name })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);

        builder.HasMany(x => x.Items)
            .WithOne(i => i.Template)
            .HasForeignKey(i => i.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
