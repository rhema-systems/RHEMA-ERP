using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Maintenance;

public class PreInspectionChecklistTemplateConfiguration : IEntityTypeConfiguration<PreInspectionChecklistTemplate>
{
    public void Configure(EntityTypeBuilder<PreInspectionChecklistTemplate> builder)
    {
        builder.ToTable("PreInspectionChecklistTemplates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.Property(t => t.Category)
            .HasMaxLength(50)
            .HasDefaultValue("General");

        builder.Property(t => t.IsActive)
            .HasDefaultValue(true);

        builder.Property(t => t.IsDefault)
            .HasDefaultValue(false);

        builder.Property(t => t.Version)
            .HasDefaultValue(1);

        builder.Property(t => t.VersionNotes)
            .HasMaxLength(1000);

        builder.HasOne(t => t.AssetCategory)
            .WithMany()
            .HasForeignKey(t => t.AssetCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.ChecklistItems)
            .WithOne(i => i.Template)
            .HasForeignKey(i => i.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.TenantId, t.AssetCategoryId });
        builder.HasIndex(t => new { t.TenantId, t.IsActive });
    }
}

public class PreInspectionChecklistItemConfiguration : IEntityTypeConfiguration<PreInspectionChecklistItem>
{
    public void Configure(EntityTypeBuilder<PreInspectionChecklistItem> builder)
    {
        builder.ToTable("PreInspectionChecklistItems");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.ItemName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.Description)
            .HasMaxLength(1000);

        builder.Property(i => i.Category)
            .HasMaxLength(50)
            .HasDefaultValue("General");

        builder.Property(i => i.ItemType)
            .HasMaxLength(20)
            .HasDefaultValue("Boolean");

        builder.Property(i => i.IsRequired)
            .HasDefaultValue(true);

        builder.Property(i => i.ChoiceOptions)
            .HasColumnType("nvarchar(max)");

        builder.Property(i => i.Unit)
            .HasMaxLength(20);

        builder.Property(i => i.MinValue)
            .HasColumnType("decimal(18,4)");

        builder.Property(i => i.MaxValue)
            .HasColumnType("decimal(18,4)");

        builder.Property(i => i.DefaultValue)
            .HasMaxLength(500);

        builder.Property(i => i.HelpText)
            .HasMaxLength(1000);

        builder.Property(i => i.RequiresPhoto)
            .HasDefaultValue(false);

        builder.HasIndex(i => new { i.TenantId, i.TemplateId });
    }
}

public class AssetConditionRecordConfiguration : IEntityTypeConfiguration<AssetConditionRecord>
{
    public void Configure(EntityTypeBuilder<AssetConditionRecord> builder)
    {
        builder.ToTable("AssetConditionRecords");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.InspectionNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.InspectionType)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Admission");

        builder.Property(r => r.Status)
            .HasMaxLength(20)
            .HasDefaultValue("InProgress");

        builder.Property(r => r.GeneralNotes)
            .HasMaxLength(2000);

        builder.Property(r => r.PhotoPaths)
            .HasColumnType("nvarchar(max)");

        builder.HasOne(r => r.Asset)
            .WithMany()
            .HasForeignKey(r => r.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Template)
            .WithMany(t => t.AssetConditionRecords)
            .HasForeignKey(r => r.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        // InspectorId is NOT a foreign key to Employees - it stores the user ID directly
        // The inspector name is looked up from the Users table when needed

        builder.HasOne(r => r.Admission)
            .WithMany()
            .HasForeignKey(r => r.AdmissionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.Discharge)
            .WithMany()
            .HasForeignKey(r => r.DischargeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(r => r.ItemResults)
            .WithOne(ir => ir.ConditionRecord)
            .HasForeignKey(ir => ir.ConditionRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.TenantId, r.AssetId });
        builder.HasIndex(r => new { r.TenantId, r.InspectionDate });
        builder.HasIndex(r => new { r.TenantId, r.InspectionType });
        builder.HasIndex(r => r.InspectionNumber).IsUnique();
    }
}

public class AssetConditionItemResultConfiguration : IEntityTypeConfiguration<AssetConditionItemResult>
{
    public void Configure(EntityTypeBuilder<AssetConditionItemResult> builder)
    {
        builder.ToTable("AssetConditionItemResults");

        builder.HasKey(ir => ir.Id);

        builder.Property(ir => ir.TextValue)
            .HasMaxLength(1000);

        builder.Property(ir => ir.NumericValue)
            .HasColumnType("decimal(18,4)");

        builder.Property(ir => ir.SelectedOption)
            .HasMaxLength(100);

        builder.Property(ir => ir.Comment)
            .HasMaxLength(2000);

        builder.Property(ir => ir.PhotoPaths)
            .HasColumnType("nvarchar(max)");

        builder.HasOne(ir => ir.ChecklistItem)
            .WithMany()
            .HasForeignKey(ir => ir.ChecklistItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ir => new { ir.TenantId, ir.ConditionRecordId });
        builder.HasIndex(ir => new { ir.ConditionRecordId, ir.ChecklistItemId }).IsUnique();
    }
}
