using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Finance;

public sealed class ChequesControllerTests
{
    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "ChequeRegister")]
    public async Task Create_ShouldRejectDuplicateChequeNumberPerBankAccount()
    {
        var (db, controller, bankAccountId, _) = await CreateFixtureAsync();

        var first = await controller.Create(NewCheque(bankAccountId, "000101"));
        first.Result.Should().BeOfType<CreatedAtActionResult>();

        var duplicate = await controller.Create(NewCheque(bankAccountId, "000101"));
        duplicate.Result.Should().BeOfType<BadRequestObjectResult>();

        (await db.Set<Cheque>().CountAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "ChequeRegister")]
    public async Task UpdateStatus_ShouldFollowLegalLifecycleAndStampDates()
    {
        var (_, controller, bankAccountId, _) = await CreateFixtureAsync();
        var created = (ChequeDto)((CreatedAtActionResult)(await controller.Create(NewCheque(bankAccountId, "000201"))).Result!).Value!;

        var presented = await controller.UpdateStatus(created.Id, new UpdateChequeStatusDto { Status = ChequeStatus.Presented });
        var presentedDto = (ChequeDto)((OkObjectResult)presented.Result!).Value!;
        presentedDto.Status.Should().Be(ChequeStatus.Presented);
        presentedDto.PresentedDate.Should().NotBeNull();

        var cleared = await controller.UpdateStatus(created.Id, new UpdateChequeStatusDto { Status = ChequeStatus.Cleared });
        var clearedDto = (ChequeDto)((OkObjectResult)cleared.Result!).Value!;
        clearedDto.Status.Should().Be(ChequeStatus.Cleared);
        clearedDto.ClearedDate.Should().NotBeNull();

        // Cleared is terminal.
        var afterCleared = await controller.UpdateStatus(created.Id, new UpdateChequeStatusDto { Status = ChequeStatus.Cancelled, StatusReason = "too late" });
        afterCleared.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "ChequeRegister")]
    public async Task UpdateStatus_ShouldRequireReasonForBounceAndCancel()
    {
        var (_, controller, bankAccountId, _) = await CreateFixtureAsync();
        var created = (ChequeDto)((CreatedAtActionResult)(await controller.Create(NewCheque(bankAccountId, "000301"))).Result!).Value!;
        await controller.UpdateStatus(created.Id, new UpdateChequeStatusDto { Status = ChequeStatus.Presented });

        var withoutReason = await controller.UpdateStatus(created.Id, new UpdateChequeStatusDto { Status = ChequeStatus.Bounced });
        withoutReason.Result.Should().BeOfType<BadRequestObjectResult>();

        var withReason = await controller.UpdateStatus(created.Id, new UpdateChequeStatusDto { Status = ChequeStatus.Bounced, StatusReason = "Insufficient funds" });
        ((ChequeDto)((OkObjectResult)withReason.Result!).Value!).Status.Should().Be(ChequeStatus.Bounced);
    }

    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "TenantIsolation")]
    public async Task Void_ShouldNotSeeAnotherTenantsCheque()
    {
        var (db, controller, bankAccountId, _) = await CreateFixtureAsync();
        var created = (ChequeDto)((CreatedAtActionResult)(await controller.Create(NewCheque(bankAccountId, "000401"))).Result!).Value!;

        var otherTenantController = CreateController(db, Guid.NewGuid());
        var result = await otherTenantController.Void(created.Id, new VoidChequeDto { Reason = "hijack attempt" });

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    private static CreateChequeDto NewCheque(Guid bankAccountId, string number) => new()
    {
        ChequeNumber = number,
        BankAccountId = bankAccountId,
        IssueDate = new DateTime(2026, 7, 1),
        PayeeName = "Acme Supplies",
        Amount = 1500m,
        Currency = "GHS"
    };

    private static async Task<(ApplicationDbContext Db, ChequesController Controller, Guid BankAccountId, Guid TenantId)> CreateFixtureAsync()
    {
        var db = CreateContext();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Cheque Tenant", Code = "CHQ", Status = TenantStatus.Active, BaseCurrency = "GHS" });

        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountName = "Main Operating",
            AccountNumber = "0011002200",
            Currency = "GHS"
        };
        db.BankAccounts.Add(bankAccount);
        await db.SaveChangesAsync();

        return (db, CreateController(db, tenantId), bankAccount.Id, tenantId);
    }

    private static ChequesController CreateController(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("cheque.register");
        return new ChequesController(db, currentUser.Object);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"cheques-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }
}
