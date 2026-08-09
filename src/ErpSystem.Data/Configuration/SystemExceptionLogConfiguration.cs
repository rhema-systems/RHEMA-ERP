using ErpSystem.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public class SystemExceptionLogConfiguration : IEntityTypeConfiguration<SystemExceptionLog>
{
    public void Configure(EntityTypeBuilder<SystemExceptionLog> builder)
    {
        builder.ToTable("SystemExceptionLogs");

        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OccurrenceCount).IsRequired();
        builder.Property(x => x.FirstOccurredAt).IsRequired();
        builder.Property(x => x.LastOccurredAt).IsRequired();

        builder.Property(x => x.Level).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Logger).HasMaxLength(200);
        builder.Property(x => x.ShortMessage).HasMaxLength(1000).IsRequired();

        builder.Property(x => x.ExceptionType).HasMaxLength(200);
        builder.Property(x => x.TraceId).HasMaxLength(100);
        builder.Property(x => x.RequestMethod).HasMaxLength(20);
        builder.Property(x => x.RequestPath).HasMaxLength(500);
        builder.Property(x => x.QueryString).HasMaxLength(2000);
        builder.Property(x => x.ReferrerUrl).HasMaxLength(500);
        builder.Property(x => x.RemoteIpAddress).HasMaxLength(45);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.Username).HasMaxLength(200);
        builder.Property(x => x.ResolutionNotes).HasMaxLength(2000);

        builder.HasIndex(x => new { x.TenantId, x.Fingerprint });
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.LastOccurredAt });
        builder.HasIndex(x => new { x.TenantId, x.Level, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.IsResolved, x.CreatedAt });
        builder.HasIndex(x => x.TraceId);
        builder.HasIndex(x => x.UserId);
    }
}
