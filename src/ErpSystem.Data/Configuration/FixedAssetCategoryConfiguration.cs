using ErpSystem.Core.Entities.Finance.FixedAssets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class FixedAssetCategoryConfiguration : IEntityTypeConfiguration<FixedAssetCategory>
{
    public void Configure(EntityTypeBuilder<FixedAssetCategory> builder)
    {
        builder.Property(category => category.RequiresMaintenance)
            .HasDefaultValue(false);
    }
}
