using System.Text;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Finance;

public sealed class BankStatementsControllerTests
{
    private const string Csv =
        "Date,Description,Reference,Debit,Credit,Balance\n" +
        "2026-06-01,Opening deposit,DEP-1,,1000.00,1000.00\n" +
        "2026-06-05,Supplier payment,CHQ-88,250.00,,750.00\n" +
        "2026-06-20,\"Customer receipt, June\",RCT-42,,500.00,1250.00\n";

    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "BankStatements")]
    public async Task Import_ShouldParseCsvAndComputeStatementTotals()
    {
        var (db, controller, bankAccountId, tenantId) = await CreateFixtureAsync();

        var result = await controller.ImportStatement(CsvFile(Csv), bankAccountId, "STMT-JUN", "June import");

        var statement = (BankStatementDto)((CreatedAtActionResult)result.Result!).Value!;
        statement.BankAccountId.Should().Be(bankAccountId);
        statement.StatementDate.Should().Be(new DateTime(2026, 6, 20));
        statement.OpeningBalance.Should().Be(0m, "the first line's balance minus its own movement is the opening position");
        statement.ClosingBalance.Should().Be(1250m);
        statement.TotalDebits.Should().Be(250m);
        statement.TotalCredits.Should().Be(1500m);

        var lines = await db.Set<BankStatementLine>().Where(l => l.BankStatementId == statement.Id).ToListAsync();
        lines.Should().HaveCount(3);
        lines.Should().OnlyContain(l => l.TenantId == tenantId && !l.IsMatched);
        lines.Single(l => l.ReferenceNumber == "RCT-42").Description.Should().Be("Customer receipt, June");
    }

    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "BankStatements")]
    public async Task Import_ShouldRejectMalformedRowsWithLineNumber()
    {
        var (_, controller, bankAccountId, _) = await CreateFixtureAsync();
        var malformed = "Date,Description,Reference,Debit,Credit,Balance\nnot-a-date,x,y,1,2,3\n";

        var result = await controller.ImportStatement(CsvFile(malformed), bankAccountId);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        ((BadRequestObjectResult)result.Result!).Value!.ToString().Should().Contain("Line 2");
    }

    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "TenantIsolation")]
    public async Task GetStatements_ShouldNotReturnAnotherTenantsStatements()
    {
        var (db, controller, bankAccountId, _) = await CreateFixtureAsync();
        await controller.ImportStatement(CsvFile(Csv), bankAccountId);

        var otherTenantController = CreateController(db, Guid.NewGuid());
        var result = await otherTenantController.GetStatements();

        var statements = (IReadOnlyList<BankStatementDto>)((OkObjectResult)result.Result!).Value!;
        statements.Should().BeEmpty();
    }

    private static IFormFile CsvFile(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "statement.csv");
    }

    private static async Task<(ApplicationDbContext Db, BankStatementsController Controller, Guid BankAccountId, Guid TenantId)> CreateFixtureAsync()
    {
        var db = CreateContext();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Statement Tenant", Code = "STM", Status = TenantStatus.Active, BaseCurrency = "GHS" });

        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountName = "Main Operating",
            AccountNumber = "0011003300",
            Currency = "GHS"
        };
        db.BankAccounts.Add(bankAccount);
        await db.SaveChangesAsync();

        return (db, CreateController(db, tenantId), bankAccount.Id, tenantId);
    }

    private static BankStatementsController CreateController(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("statement.importer");
        var accessScope = new Mock<IFinanceAccessScopeService>();
        accessScope.Setup(x => x.GetPermittedBankAccountIdsAsync(
                It.IsAny<FinanceAccessLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid>?)null);
        accessScope.Setup(x => x.EnsureBankAccountAccessAsync(
                It.IsAny<Guid?>(),
                It.IsAny<FinanceAccessLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new BankStatementsController(db, currentUser.Object, accessScope.Object);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"bank-statements-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }
}
