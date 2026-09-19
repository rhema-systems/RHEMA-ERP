using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class PurchaseRequisitionLinePlanItemLineageMigrationTests
{
    private const string MigrationId =
        "20260829170000_AddPurchaseRequisitionLinePlanItemLineage";
    private const string CurrentBaselineId =
        "20260916132000_DisposableDevelopmentCurrentModelBaseline";

    [Fact]
    public void ArchivedMigrationBackfillIsRetainedWhileOnlyCurrentBaselineIsDiscoverable()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PurchaseRequisitionLineageMigrationDiscovery;Trusted_Connection=True")
            .Options;
        using var context = new ApplicationDbContext(options);

        var discovered = context.GetService<IMigrationsAssembly>().Migrations;
        discovered.Should().ContainSingle();
        discovered.Should().ContainKey(CurrentBaselineId);
        discovered.Should().NotContainKey(MigrationId);

        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableMigration().ApplyUp(builder);

        builder.Operations.OfType<AddColumnOperation>()
            .Should().ContainSingle(operation =>
                operation.Table == "PurchaseRequisitionItems" &&
                operation.Name == "SourcePlanItemId" && operation.IsNullable);
        builder.Operations.OfType<CreateIndexOperation>()
            .Should().Contain(operation =>
                operation.Name == "IX_PurchaseRequisitionItems_TenantId_SourcePlanItemId" &&
                operation.Columns.SequenceEqual(new[] { "TenantId", "SourcePlanItemId" }));
        builder.Operations.OfType<AddForeignKeyOperation>()
            .Should().ContainSingle(operation =>
                operation.Name == "FK_PurchaseRequisitionItems_ProcurementPlanItems_SourcePlanItemId" &&
                operation.PrincipalTable == "ProcurementPlanItems");
        builder.Operations.OfType<SqlOperation>().Single().Sql
            .Should().Contain("SET pri.[SourcePlanItemId] = pr.[SourcePlanItemId]")
            .And.Contain("pr.[TenantId] = pri.[TenantId]");
    }

    private sealed class TestableMigration : AddPurchaseRequisitionLinePlanItemLineage
    {
        public void ApplyUp(MigrationBuilder builder) => Up(builder);
    }
}
