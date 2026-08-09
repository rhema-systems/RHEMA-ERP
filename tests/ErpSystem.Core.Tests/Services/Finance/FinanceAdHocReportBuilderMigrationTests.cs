using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Finance;

public sealed class FinanceAdHocReportBuilderMigrationTests
{
    [Fact]
    public void OwnerForeignKeyTargetsConfiguredApplicationUserTable()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableMigration().ApplyUp(builder);

        var table = builder.Operations.OfType<CreateTableOperation>()
            .Single(operation => operation.Name == "FinanceAdHocReportDefinitions");
        var ownerForeignKey = table.ForeignKeys.Single(key =>
            key.Columns.SequenceEqual(["OwnerUserId"]));

        ownerForeignKey.Name.Should().Be(
            "FK_FinanceAdHocReportDefinitions_Users_OwnerUserId");
        ownerForeignKey.PrincipalTable.Should().Be("Users");
        ownerForeignKey.PrincipalColumns.Should().Equal("Id");
    }

    private sealed class TestableMigration : AddFinanceAdHocReportBuilder
    {
        public void ApplyUp(MigrationBuilder migrationBuilder) => Up(migrationBuilder);
    }
}
