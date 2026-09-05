using System.Reflection;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Data.Migrations;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class BookBalanceMigrationC2Tests
{
    [Fact]
    public void InquiryController_RequiresCanonicalFinanceReadPermission()
    {
        typeof(BookBalanceController).GetCustomAttribute<AuthorizeAttribute>()?.Policy
            .Should().Be(FinancePermissions.ViewFinance);
    }

    [Fact]
    public void Migration_UsesExactPreflight_Backfill_AndBoundedDown()
    {
        var migration = new AddBookAwareBalanceFoundation();
        var up = Operations(migration, "Up");
        var sql = string.Join("\n", up.OfType<SqlOperation>().Select(item => item.Sql));
        sql.Should().Contain("C2_ACCOUNT_BALANCE_PREFLIGHT")
            .And.Contain("Latin1_General_100_BIN2")
            .And.Contain("DATALENGTH")
            .And.Contain("duplicate rows collide")
            .And.Contain("C2_PRIMARY_BOOK_PREFLIGHT")
            .And.Contain("FinanceSettings")
            .And.Contain("UPDATE ab")
            .And.Contain("UPDATE a");
        var downSql = string.Join("\n", Operations(migration, "Down")
            .OfType<SqlOperation>().Select(item => item.Sql));
        downSql.Should().Contain("allEvidence").And.Contain("AccountTransactions");
        up.OfType<CreateTableOperation>().Select(item => item.Name)
            .Should().Contain(new[] { "AccountCurrencyExposures", "FinanceBalanceRebuildRuns" });
        Operations(migration, "Down").OfType<DropTableOperation>().Select(item => item.Name)
            .Should().Contain(new[] { "AccountCurrencyExposures", "FinanceBalanceRebuildRuns" });
    }

    private static IReadOnlyList<MigrationOperation> Operations(Migration migration, string method)
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        typeof(AddBookAwareBalanceFoundation).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });
        return builder.Operations;
    }
}
