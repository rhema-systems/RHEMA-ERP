using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class SupplierOnboardingCategoryAlignmentMigrationTests
{
    private const string MigrationId =
        "20260827090000_AlignSupplierOnboardingPartnerCategories";
    private const string CurrentBaselineId =
        "20260913162402_DisposableDevelopmentCurrentModelBaseline";

    [Fact]
    public void ArchivedRepairIsRetainedWhileOnlyCurrentBaselineIsDiscoverable()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=SupplierCategoryMigrationDiscovery;Trusted_Connection=True")
            .Options;
        using var context = new ApplicationDbContext(options);

        var discovered = context.GetService<IMigrationsAssembly>().Migrations;
        discovered.Should().ContainSingle();
        discovered.Should().ContainKey(CurrentBaselineId);
        discovered.Should().NotContainKey(MigrationId);

        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableMigration().ApplyUp(builder);
        builder.Operations.OfType<DropIndexOperation>()
            .Should().ContainSingle(item => item.Name == "IX_PartnerCategories_CategoryCode");
        builder.Operations.OfType<CreateIndexOperation>()
            .Should().ContainSingle(item =>
                item.Name == "IX_PartnerCategories_TenantId_CategoryCode" &&
                item.IsUnique &&
                item.Columns.SequenceEqual(new[] { "TenantId", "CategoryCode" }));
        var sql = string.Join(
            Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(item => item.Sql));
        sql.Should().Contain("INSERT INTO PartnerCategories");
        sql.Should().Contain("INSERT INTO BusinessPartnerCategories");
        sql.Should().Contain("BusinessPartnerRegistrations AS registrations");
        sql.Should().Contain("WHEN 0 THEN N'GOODS'");
        sql.Should().Contain("WHEN 1 THEN N'WORKS'");
        sql.Should().Contain("WHEN 2 THEN N'SERVICES'");
    }

    private sealed class TestableMigration : AlignSupplierOnboardingPartnerCategories
    {
        public void ApplyUp(MigrationBuilder builder) => Up(builder);
    }
}
