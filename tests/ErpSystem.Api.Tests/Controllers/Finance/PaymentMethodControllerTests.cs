using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
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

public sealed class PaymentMethodControllerTests
{
    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "TenantIsolation")]
    public async Task GetAll_ShouldSeedDefaultsPerTenantAndNeverLeakAcrossTenants()
    {
        await using var db = CreateContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        SeedTenant(db, tenantA, "TENA");
        SeedTenant(db, tenantB, "TENB");
        await db.SaveChangesAsync();

        var controllerA = CreateController(db, tenantA);
        var controllerB = CreateController(db, tenantB);

        var resultA = await controllerA.GetAll();
        var methodsA = ExtractList(resultA);
        methodsA.Should().HaveCount(5, "each tenant seeds its own defaults on first read");

        var resultB = await controllerB.GetAll();
        var methodsB = ExtractList(resultB);
        methodsB.Should().HaveCount(5);

        methodsA.Select(m => m.Id).Should().NotIntersectWith(methodsB.Select(m => m.Id));
        (await db.PaymentMethods.CountAsync()).Should().Be(10);
    }

    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "TenantIsolation")]
    public async Task Create_ShouldRejectDuplicateCodeWithinTenantButAllowSameCodeInOtherTenant()
    {
        await using var db = CreateContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        SeedTenant(db, tenantA, "TENA");
        SeedTenant(db, tenantB, "TENB");
        await db.SaveChangesAsync();

        var controllerA = CreateController(db, tenantA);
        var controllerB = CreateController(db, tenantB);
        var dto = new CreatePaymentMethodDto { Name = "Corporate Card", Code = "CARD", Type = PaymentMethodType.EFT };

        var created = await controllerA.Create(dto);
        created.Result.Should().BeOfType<CreatedAtActionResult>();

        var duplicate = await controllerA.Create(dto);
        duplicate.Result.Should().BeOfType<BadRequestObjectResult>("codes must be unique within a tenant");

        var otherTenant = await controllerB.Create(dto);
        otherTenant.Result.Should().BeOfType<CreatedAtActionResult>("the same code is fine in a different tenant");
    }

    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "TenantIsolation")]
    public async Task Update_ShouldNotFindAnotherTenantsPaymentMethod()
    {
        await using var db = CreateContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        SeedTenant(db, tenantA, "TENA");
        SeedTenant(db, tenantB, "TENB");
        await db.SaveChangesAsync();

        var controllerA = CreateController(db, tenantA);
        var createdResult = await controllerA.Create(new CreatePaymentMethodDto { Name = "Standing Order", Code = "SO", Type = PaymentMethodType.BankTransfer });
        var created = (PaymentMethodDto)((CreatedAtActionResult)createdResult.Result!).Value!;

        var controllerB = CreateController(db, tenantB);
        var update = await controllerB.Update(created.Id, new CreatePaymentMethodDto { Name = "Hijacked", Type = PaymentMethodType.BankTransfer });

        update.Result.Should().BeOfType<NotFoundResult>("tenant B must not see or edit tenant A's payment methods");
    }

    private static IReadOnlyList<PaymentMethodDto> ExtractList(ActionResult<IReadOnlyList<PaymentMethodDto>> result)
    {
        result.Result.Should().BeOfType<OkObjectResult>();
        return (IReadOnlyList<PaymentMethodDto>)((OkObjectResult)result.Result!).Value!;
    }

    private static PaymentMethodController CreateController(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("payment.methods");
        return new PaymentMethodController(db, currentUser.Object);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"payment-methods-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string code)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {code}",
            Code = code,
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
    }
}
