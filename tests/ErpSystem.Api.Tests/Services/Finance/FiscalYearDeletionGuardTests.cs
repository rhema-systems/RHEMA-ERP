using ErpSystem.Api.Services.Finance.Fiscal;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FiscalYearDeletionGuardTests
{
    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "FiscalYearDeletion")]
    public async Task DeleteFiscalYearAsync_ShouldRejectOrphanedAccountTransactionLine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (year, period) = SeedFiscalYear(db, tenantId);
        db.AccountTransactions.Add(new AccountTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountId = Guid.NewGuid(),
            JournalEntryId = Guid.NewGuid(),
            FiscalPeriodId = period.Id,
            TransactionDate = new DateTime(2026, 7, 1),
            PostingStatus = "Draft",
            FunctionalCurrencyCode = "GHS"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var action = () => service.DeleteFiscalYearAsync(year.Id);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*account transactions*");
        (await db.FiscalYears.SingleAsync(y => y.Id == year.Id)).IsDeleted.Should().BeFalse();
        (await db.FiscalPeriods.SingleAsync(p => p.Id == period.Id)).IsDeleted.Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "FiscalYearDeletion")]
    public async Task DeleteFiscalYearAsync_ShouldRejectNonLedgerFiscalYearDependency()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (year, _) = SeedFiscalYear(db, tenantId);
        db.BudgetScenarios.Add(new BudgetScenario
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = year.Id,
            Name = "FY2026 Original",
            BaseCurrencyCode = "GHS",
            Status = "Draft"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var action = () => service.DeleteFiscalYearAsync(year.Id);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*budget scenarios*");
    }

    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "FiscalYearDeletion")]
    public async Task DeleteFiscalYearAsync_ShouldSoftDeleteYearAndGeneratedPeriodsWhenUnused()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (year, period) = SeedFiscalYear(db, tenantId);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        await service.DeleteFiscalYearAsync(year.Id);

        var deletedYear = await db.FiscalYears.IgnoreQueryFilters().SingleAsync(y => y.Id == year.Id);
        var deletedPeriod = await db.FiscalPeriods.IgnoreQueryFilters().SingleAsync(p => p.Id == period.Id);
        deletedYear.IsDeleted.Should().BeTrue();
        deletedPeriod.IsDeleted.Should().BeTrue();
        (await db.FiscalYears.AnyAsync(y => y.Id == year.Id)).Should().BeFalse();
        (await db.FiscalPeriods.AnyAsync(p => p.Id == period.Id)).Should().BeFalse();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"fiscal-year-delete-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static FiscalPeriodService CreateService(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("fiscal.year.admin");

        return new FiscalPeriodService(
            new UnitOfWork(db),
            currentUser.Object,
            Mock.Of<ILogger<FiscalPeriodService>>());
    }

    private static (FiscalYear Year, FiscalPeriod Period) SeedFiscalYear(
        ApplicationDbContext db,
        Guid tenantId)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Fiscal Year Delete Tenant",
            Code = "FYD",
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });

        var year = new FiscalYear
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearName = "Fiscal Year 2026",
            FiscalYearCode = "FY2026",
            Year = 2026,
            FiscalYearType = "Calendar",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            TotalDays = 365,
            NumberOfPeriods = 1,
            Status = "Future",
            IsActive = true,
            IsClosed = false
        };

        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = year.Id,
            PeriodName = "Fiscal Year 2026",
            PeriodCode = "FY2026-01",
            PeriodNumber = 1,
            PeriodType = PeriodType.Monthly,
            StartDate = year.StartDate,
            EndDate = year.EndDate,
            PeriodDays = 365,
            PeriodStatus = "Future",
            IsOpen = false,
            IsClosed = false
        };

        db.FiscalYears.Add(year);
        db.FiscalPeriods.Add(period);
        return (year, period);
    }
}
