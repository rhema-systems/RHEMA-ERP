using System.Security.Claims;
using AutoMapper;
using ErpSystem.Api.Controllers;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class InventoryItemPostingAccountLookupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inventory_editor_or_super_admin_reads_current_tenant_active_posting_identities_without_finance_access(bool superAdmin)
    {
        var tenant = Guid.NewGuid();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"inventory-posting-lookup-{Guid.NewGuid():N}").Options);
        var control = Account(tenant, "CONTROL"); control.IsControlAccount = true; control.AllowDirectPosting = false;
        var direct = Account(tenant, "DIRECT");
        var parent = Account(tenant, "PARENT"); parent.AllowDirectPosting = false;
        var inactive = Account(tenant, "INACTIVE"); inactive.Status = AccountStatus.Inactive;
        var deleted = Account(tenant, "DELETED"); deleted.IsDeleted = true;
        db.Accounts.AddRange(control, direct, parent, inactive, deleted, Account(Guid.NewGuid(), "FOREIGN"));
        await db.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(db);
        var access = Access(false); // The item editor does not require a Stores Manager mutation grant to read identities.
        var controller = Controller(tenant, access.Object, unitOfWork, superAdmin: superAdmin);

        var response = await controller.GetPostingAccounts();

        var rows = ((OkObjectResult)response.Result!).Value.Should().BeAssignableTo<IReadOnlyList<BusinessPartnerPostingAccountOptionDto>>().Subject;
        rows.Select(row => row.Id).Should().BeEquivalentTo(new[] { control.Id, direct.Id });
        access.Verify(value => value.CheckCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        typeof(BusinessPartnerPostingAccountOptionDto).GetProperties().Select(property => property.Name)
            .Should().NotContain(name => name.Contains("Balance") || name.Contains("Transaction") || name.Contains("Budget"));
    }

    [Fact]
    public async Task External_user_cannot_read_account_options_or_query_finance_data()
    {
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var response = await Controller(Guid.NewGuid(), Access(false).Object, unitOfWork.Object, external: true).GetPostingAccounts();
        response.Result.Should().BeOfType<ForbidResult>();
        unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Missing_tenant_cannot_query_account_options()
    {
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var response = await Controller(Guid.Empty, Access(false).Object, unitOfWork.Object).GetPostingAccounts();
        response.Result.Should().BeOfType<ForbidResult>();
        unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Unauthenticated_user_cannot_query_account_options()
    {
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var response = await Controller(Guid.NewGuid(), Access(false).Object, unitOfWork.Object, authenticated: false).GetPostingAccounts();
        response.Result.Should().BeOfType<ForbidResult>();
        unitOfWork.VerifyNoOtherCalls();
    }

    private static Account Account(Guid tenant, string code) => new()
    {
        TenantId = tenant, AccountCode = code, AccountNumber = code, AccountName = code,
        AccountType = AccountType.Asset, Status = AccountStatus.Active, AllowDirectPosting = true
    };

    private static Mock<IProcurementAccessControlService> Access(bool allowed)
    {
        var access = new Mock<IProcurementAccessControlService>();
        access.Setup(value => value.CheckCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = allowed });
        access.Setup(value => value.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = allowed });
        return access;
    }

    private static InventoryItemsController Controller(Guid tenant, IProcurementAccessControlService access, IUnitOfWork unitOfWork,
        bool external = false, bool authenticated = true, bool superAdmin = false)
    {
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(value => value.TenantId).Returns(tenant);
        current.SetupGet(value => value.IsExternalUser).Returns(external);
        current.Setup(value => value.HasRole(ErpSystem.Shared.Constants.Roles.SuperAdmin)).Returns(superAdmin);
        var controller = new InventoryItemsController(current.Object, Mock.Of<IInventoryItemRepository>(),
            Mock.Of<IStockMovementRepository>(), Mock.Of<IInventoryLocationRepository>(), Mock.Of<IInventoryAllocationRepository>(),
            Mock.Of<IWarehouseLocationRepository>(), Mock.Of<IWarehouseRepository>(), Mock.Of<IWarehouseQuantityRepository>(),
            Mock.Of<IInventoryMovementRepository>(), Mock.Of<IInventoryBalanceRepository>(), Mock.Of<IItemUnitOfMeasureRepository>(),
            Mock.Of<IUnitOfMeasureScheduleRepository>(), access, Mock.Of<IMapper>(), NullLogger<InventoryItemsController>.Instance,
            unitOfWork: unitOfWork);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tenant_id", tenant.ToString()),
                new Claim(ClaimTypes.Role, superAdmin ? ErpSystem.Shared.Constants.Roles.SuperAdmin : "TDC_STORES_OFFICER") }, authenticated ? "Test" : null))
        } };
        return controller;
    }
}
