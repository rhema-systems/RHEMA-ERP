using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class PhysicalCountCounterConfiguration : IEntityTypeConfiguration<PhysicalCountCounter>
{
    public void Configure(EntityTypeBuilder<PhysicalCountCounter> builder)
    {
        builder.ToTable("PhysicalCountCounters", table => table.HasTrigger("TR_PhysicalCountCounters_ControlledMutation"));
        builder.HasIndex(x => new { x.TenantId, x.PhysicalCountId, x.EmployeeId })
            .IsUnique().HasFilter("[IsDeleted] = 0 AND [IsActive] = 1");
        builder.HasIndex(x => new { x.TenantId, x.PhysicalCountId, x.UserId });
        builder.HasOne(x => x.PhysicalCount).WithMany(x => x.Counters).HasForeignKey(x => x.PhysicalCountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AssignedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RemovedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Notification>().WithMany().HasForeignKey(x => x.InAppNotificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Notification>().WithMany().HasForeignKey(x => x.EmailNotificationId).OnDelete(DeleteBehavior.Restrict);
    }
}
