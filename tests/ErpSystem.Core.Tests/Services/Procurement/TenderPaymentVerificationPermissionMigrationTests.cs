using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderPaymentVerificationPermissionMigrationTests
{
    private const string MigrationId =
        "20260831194500_GrantTenderPaymentVerificationPermission";

    [Fact]
    public void MigrationIsDiscoverableAndRepairsPermissionAndRoleGrants()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=TenderPaymentPermissionMigrationDiscovery;Trusted_Connection=True")
            .Options;
        using var context = new ApplicationDbContext(options);

        context.GetService<IMigrationsAssembly>().Migrations
            .Should().ContainKey(MigrationId);

        var builder = new MigrationBuilder(
            "Microsoft.EntityFrameworkCore.SqlServer");
        new TestableMigration().ApplyUp(builder);

        var sql = builder.Operations.OfType<SqlOperation>().Single().Sql;
        sql.Should()
            .Contain("procurement.tender.payment.verify")
            .And.Contain("TDC_PROCUREMENT_OFFICER")
            .And.Contain("TDC_SENIOR_PROCUREMENT_OFFICER")
            .And.Contain("NOT EXISTS")
            .And.Contain("IsDeleted] = 0");
    }

    private sealed class TestableMigration :
        GrantTenderPaymentVerificationPermission
    {
        public void ApplyUp(MigrationBuilder builder) => Up(builder);
    }
}
