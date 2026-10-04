using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class GovernedAccountingBookPeriodBackfillMigrationTests
{
    [Fact]
    public void Migration_ShouldBackfillMissingAuthoritiesWithoutOverwritingExistingRows()
    {
        var migration = new ExposedMigration();
        var operations = migration.UpOperations();
        var sql = operations.OfType<SqlOperation>().Single().Sql;

        sql.Should().Contain("INSERT INTO [AccountingBookPeriods]")
            .And.Contain("NOT EXISTS")
            .And.Contain("period.[IsLocked] = 1 THEN 4")
            .And.Contain("period.[IsClosed] = 1 THEN 3")
            .And.Contain("period.[IsOpen] = 1 THEN 2")
            .And.Contain("book.[LifecycleStatus] <> 6");
    }

    private sealed class ExposedMigration : GovernedAccountingBookPeriodBackfill
    {
        public IReadOnlyList<MigrationOperation> UpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            typeof(GovernedAccountingBookPeriodBackfill)
                .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this, new object[] { builder });
            return builder.Operations;
        }
    }
}
