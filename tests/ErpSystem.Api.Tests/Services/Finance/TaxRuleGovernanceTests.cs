using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using ErpSystem.Shared.DTOs.Finance;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class TaxRuleGovernanceTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-Tax")]
    [Trait("Category", "Tax")]
    public async Task ActiveRuleCriteriaCannotBeDuplicatedAndCreationIsAudited()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var group = SeedGroup(db, tenantId);
        await db.SaveChangesAsync();
        var (controller, audit) = CreateController(db, tenantId);

        var first = await controller.CreateTaxRule(new CreateTaxRuleDto
        {
            Name = " Standard purchase rule ",
            Priority = 10,
            TaxGroupId = group.Id,
            TransactionType = " Purchase ",
            CustomerType = " Corporate ",
            IsActive = true
        });
        var duplicate = await controller.CreateTaxRule(new CreateTaxRuleDto
        {
            Name = "Duplicate rule",
            Priority = 20,
            TaxGroupId = group.Id,
            TransactionType = "Purchase",
            CustomerType = "Corporate",
            IsActive = true
        });

        first.Result.Should().BeOfType<CreatedAtActionResult>();
        duplicate.Result.Should().BeOfType<ConflictObjectResult>();
        (await db.TaxRules.SingleAsync()).Name.Should().Be("Standard purchase rule");
        audit.Verify(service => service.RecordAsync(
            It.Is<FinanceAuditEventDto>(entry => entry.EventType == FinanceAuditEvents.TaxRuleCreated),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-Tax")]
    [Trait("Category", "Tax")]
    public async Task DeleteRetainsSoftDeletedRuleAndRecordsDeactivationEvidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var group = SeedGroup(db, tenantId);
        var rule = new TaxRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Historical tax rule",
            Priority = 10,
            TaxGroupId = group.Id,
            TransactionType = "Sale",
            IsActive = true
        };
        db.TaxRules.Add(rule);
        await db.SaveChangesAsync();
        var (controller, audit) = CreateController(db, tenantId);

        (await controller.DeleteTaxRule(rule.Id)).Should().BeOfType<NoContentResult>();

        var retained = await db.TaxRules.IgnoreQueryFilters().SingleAsync(item => item.Id == rule.Id);
        retained.IsDeleted.Should().BeTrue();
        retained.IsActive.Should().BeFalse();
        retained.DeletedBy.Should().Be("tax.controller");
        audit.Verify(service => service.RecordAsync(
            It.Is<FinanceAuditEventDto>(entry => entry.EventType == FinanceAuditEvents.TaxRuleDeactivated),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tax-rule-governance-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static TaxGroup SeedGroup(ApplicationDbContext db, Guid tenantId)
    {
        var group = new TaxGroup
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "GH-STD",
            Name = "Ghana standard taxes",
            Applicability = TaxApplicability.Both,
            IsActive = true
        };
        db.TaxGroups.Add(group);
        return group;
    }

    private static (TaxRuleController Controller, Mock<IFinanceAuditService> Audit) CreateController(
        ApplicationDbContext db,
        Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserName).Returns("tax.controller");
        currentUser.SetupGet(service => service.Claims).Returns(new Dictionary<string, string>());
        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(service => service.RecordAsync(
                It.IsAny<FinanceAuditEventDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog());
        return (new TaxRuleController(
            db,
            Mock.Of<ITaxConfigurationService>(),
            currentUser.Object,
            audit.Object), audit);
    }
}
