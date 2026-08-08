using ErpSystem.Api.Services.Finance.Fiscal;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FiscalPeriodModuleLockTests
{
    [Fact]
    [Trait("Category", "ModuleLocks")]
    public async Task ReopenModule_ShouldConvertGlobalLockToPartial_ThenRestoreGlobalWhenRelocked()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Module lock tenant",
            Code = "LOCK",
            Status = TenantStatus.Active
        });
        db.TenantModules.Add(new TenantModule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleName = "Sales",
            Status = ModuleStatus.Enabled
        });
        var finance = SeedModule(db, tenantId, "FIN", "Finance", 1);
        var sales = SeedModule(db, tenantId, "SALES", "Sales", 4);
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "January 2026",
            PeriodCode = "2026-01",
            PeriodNumber = 1,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 31),
            PeriodDays = 31,
            PeriodStatus = "Locked",
            IsOpen = false,
            IsClosed = true,
            IsLocked = true,
            LockedDate = DateTime.UtcNow.AddDays(-1),
            LockedByUserId = userId,
            LockReason = "January close"
        };
        db.FiscalPeriods.Add(period);
        await db.SaveChangesAsync();

        using var unitOfWork = new UnitOfWork(db);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserId).Returns(userId.ToString());
        currentUser.SetupGet(service => service.UserName).Returns("finance.admin");
        var service = new FiscalPeriodService(
            unitOfWork,
            currentUser.Object,
            Mock.Of<ILogger<FiscalPeriodService>>(),
            Mock.Of<ISubledgerSettlementReadModelService>());
        var expiry = DateTime.UtcNow.AddHours(4);

        var partialResult = await service.UnlockPeriodForModuleAsync(
            period.Id,
            "SALES",
            "Authorized correction",
            expiry);

        partialResult.IsPartiallyLocked.Should().BeTrue();
        var partialPeriod = await db.FiscalPeriods.SingleAsync(item => item.Id == period.Id);
        partialPeriod.IsGlobalLockSuspended.Should().BeTrue();
        partialPeriod.IsLocked.Should().BeFalse();
        partialPeriod.IsOpen.Should().BeTrue();
        partialPeriod.IsClosed.Should().BeFalse();
        var locks = await db.PeriodModuleLocks
            .Where(item => item.FiscalPeriodId == period.Id)
            .ToDictionaryAsync(item => item.ModuleDefinitionId);
        locks[finance.Id].IsLocked.Should().BeTrue();
        locks[sales.Id].IsLocked.Should().BeFalse();
        locks[sales.Id].ReopenExpiresAtUtc.Should().BeCloseTo(expiry, TimeSpan.FromSeconds(1));

        var restoredResult = await service.LockPeriodForModuleAsync(period.Id, "SALES", "Correction completed");

        restoredResult.IsPartiallyLocked.Should().BeFalse();
        var restoredPeriod = await db.FiscalPeriods.SingleAsync(item => item.Id == period.Id);
        restoredPeriod.IsGlobalLockSuspended.Should().BeFalse();
        restoredPeriod.IsLocked.Should().BeTrue();
        restoredPeriod.IsOpen.Should().BeFalse();
        restoredPeriod.IsClosed.Should().BeTrue();
        restoredPeriod.PeriodStatus.Should().Be("Locked");
    }

    [Fact]
    [Trait("Category", "ModuleLocks")]
    public async Task ReopenModule_ShouldAllowAReplacementWindow_WhenPreviousWindowAlreadyExpired()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Module lock tenant",
            Code = "LOCK2",
            Status = TenantStatus.Active
        });
        db.TenantModules.Add(new TenantModule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleName = "Sales",
            Status = ModuleStatus.Enabled
        });
        var sales = SeedModule(db, tenantId, "SALES", "Sales", 4);
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "January 2026",
            PeriodCode = "2026-01",
            PeriodNumber = 1,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 31),
            PeriodDays = 31,
            PeriodStatus = "Open",
            IsOpen = true,
            IsClosed = false,
            IsLocked = false,
            IsGlobalLockSuspended = true,
            LockReason = "January close"
        };
        db.FiscalPeriods.Add(period);
        db.PeriodModuleLocks.Add(new PeriodModuleLock
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalPeriodId = period.Id,
            ModuleDefinitionId = sales.Id,
            IsLocked = false,
            UnlockedDate = DateTime.UtcNow.AddHours(-2),
            UnlockedByUserId = userId,
            UnlockReason = "First correction window",
            ReopenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        using var unitOfWork = new UnitOfWork(db);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserId).Returns(userId.ToString());
        currentUser.SetupGet(service => service.UserName).Returns("finance.admin");
        var service = new FiscalPeriodService(
            unitOfWork,
            currentUser.Object,
            Mock.Of<ILogger<FiscalPeriodService>>(),
            Mock.Of<ISubledgerSettlementReadModelService>());
        var replacementExpiry = DateTime.UtcNow.AddHours(2);

        await service.UnlockPeriodForModuleAsync(
            period.Id,
            "SALES",
            "Additional approved correction",
            replacementExpiry);

        var moduleLock = await db.PeriodModuleLocks.SingleAsync();
        moduleLock.IsLocked.Should().BeFalse();
        moduleLock.UnlockReason.Should().Be("Additional approved correction");
        moduleLock.ReopenExpiresAtUtc.Should().BeCloseTo(replacementExpiry, TimeSpan.FromSeconds(1));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"fiscal-period-module-locks-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ModuleDefinition SeedModule(
        ApplicationDbContext db,
        Guid tenantId,
        string code,
        string name,
        int sortOrder)
    {
        var module = new ModuleDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleCode = code,
            ModuleName = name,
            SortOrder = sortOrder,
            IsActive = true,
            IsSystem = true
        };
        db.ModuleDefinitions.Add(module);
        return module;
    }
}
