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
        var sql = ArchivedMigrationSource.Read("20260905182403_AddBookAwareBalanceFoundation.cs");
        sql.Should().Contain("C2_ACCOUNT_BALANCE_PREFLIGHT")
            .And.Contain("C2_BOOK_AUTHORITY_PREFLIGHT")
            .And.Contain("C2_FUNCTIONAL_CURRENCY_AUTHORITY_PREFLIGHT")
            .And.Contain("Latin1_General_100_BIN2")
            .And.Contain("DATALENGTH")
            .And.Contain("UPPER(LTRIM(RTRIM")
            .And.Contain("ALL_ACTIVE_BOOKS")
            .And.Contain("ALLCLASSIFIEDBOOKS")
            .And.Contain("j.[IsDeleted] <> 0")
            .And.Contain("duplicate rows collide")
            .And.Contain("C2_PRIMARY_BOOK_PREFLIGHT")
            .And.Contain("FinanceSettings")
            .And.Contain("UPDATE ab")
            .And.Contain("UPDATE a");
        sql.Should().Contain("allEvidence").And.Contain("AccountTransactions")
            .And.Contain("name: \"AccountCurrencyExposures\"")
            .And.Contain("name: \"FinanceBalanceRebuildRuns\"")
            .And.Contain("migrationBuilder.DropTable(\n                name: \"AccountCurrencyExposures\"")
            .And.Contain("migrationBuilder.DropTable(\n                name: \"FinanceBalanceRebuildRuns\"");
    }
}
