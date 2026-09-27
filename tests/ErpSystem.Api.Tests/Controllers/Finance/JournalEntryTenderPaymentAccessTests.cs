using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Finance;

public sealed class JournalEntryTenderPaymentAccessTests
{
    [Fact]
    public async Task Search_DeniesScopedTenderVerifierBeforeReadingTheJournalRegister()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedPermissionAsync(db, userId, ProcurementAccessControlRegistry.TenderPaymentVerifyPermission);
        var service = new Mock<IJournalEntryService>(MockBehavior.Strict);
        var controller = CreateController(db, service.Object, userId, tenantId);
        (await controller.Search("JE", 5)).Result.Should().BeOfType<ForbidResult>();
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Search_UsesBoundedOwnerReadForFinanceReaders()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedPermissionAsync(db, userId, ErpSystem.Shared.FinancePermissions.ViewFinance);
        var service = new Mock<IJournalEntryService>();
        service.Setup(item => item.SearchAsync("JE", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new FinanceRecordSearchDto { Id = Guid.NewGuid(), Number = "JE-001" } });
        var controller = CreateController(db, service.Object, userId, tenantId);
        (await controller.Search("JE", 5)).Result.Should().BeOfType<OkObjectResult>();
        service.Verify(item => item.SearchAsync("JE", 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetJournalEntryById_AllowsTenderPaymentVerifierForExactLinkedJournal()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var journalId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedPermissionAsync(db, userId, ProcurementAccessControlRegistry.TenderPaymentVerifyPermission);
        db.TenderPayments.Add(TenderPaymentFor(tenantId, journalId));
        await db.SaveChangesAsync();

        var journalService = new Mock<IJournalEntryService>();
        journalService
            .Setup(service => service.GetJournalEntryByIdAsync(journalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JournalEntryDto { Id = journalId });

        var controller = CreateController(db, journalService.Object, userId, tenantId);

        var result = await controller.GetJournalEntryById(journalId);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetJournalEntryById_DeniesTenderPaymentVerifierForUnlinkedJournal()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedPermissionAsync(db, userId, ProcurementAccessControlRegistry.TenderPaymentVerifyPermission);

        var controller = CreateController(db, Mock.Of<IJournalEntryService>(), userId, tenantId);

        var result = await controller.GetJournalEntryById(Guid.NewGuid());

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetJournalEntryById_DeniesTenderPaymentVerifierForAnotherTenantLink()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var journalId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedPermissionAsync(db, userId, ProcurementAccessControlRegistry.TenderPaymentVerifyPermission);
        db.TenderPayments.Add(TenderPaymentFor(Guid.NewGuid(), journalId));
        await db.SaveChangesAsync();

        var controller = CreateController(db, Mock.Of<IJournalEntryService>(), userId, tenantId);

        var result = await controller.GetJournalEntryById(journalId);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    private static JournalEntryController CreateController(
        ApplicationDbContext db,
        IJournalEntryService journalEntryService,
        Guid userId,
        Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.UserId).Returns(userId.ToString());
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.Claims).Returns(new Dictionary<string, string>());
        currentUser.Setup(service => service.IsInRole(It.IsAny<string>())).Returns(false);

        return new JournalEntryController(
            journalEntryService,
            Mock.Of<IGeneralLedgerService>(),
            Mock.Of<IWorkflowService>(),
            currentUser.Object,
            Mock.Of<IFinanceAuditService>(),
            db,
            Mock.Of<IFinanceBudgetControlService>());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"journal-tender-payment-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task SeedPermissionAsync(
        ApplicationDbContext db,
        Guid userId,
        string permissionName)
    {
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        db.Roles.Add(new ApplicationRole("Procurement test role")
        {
            Id = roleId,
            NormalizedName = "PROCUREMENT TEST ROLE"
        });
        db.Permissions.Add(new Permission
        {
            Id = permissionId,
            Name = permissionName,
            DisplayName = permissionName,
            Category = "Procurement",
            IsSystemPermission = true
        });
        db.UserRoles.Add(new ApplicationUserRole { UserId = userId, RoleId = roleId });
        db.RolePermissions.Add(new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId,
            GrantedBy = "Tests"
        });
        await db.SaveChangesAsync();
    }

    private static TenderPayment TenderPaymentFor(Guid tenantId, Guid journalId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        TenderFeeId = Guid.NewGuid(),
        BusinessPartnerId = Guid.NewGuid(),
        PaymentReference = $"PAY-{Guid.NewGuid():N}",
        Amount = 500m,
        Currency = "GHS",
        PaymentMethod = "MobileMoney",
        Status = "Verified",
        JournalEntryId = journalId,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = "Tests"
    };
}
