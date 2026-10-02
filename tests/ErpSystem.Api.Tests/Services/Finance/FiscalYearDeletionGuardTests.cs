using ErpSystem.Api.Services.Finance.Fiscal;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FiscalYearDeletionGuardTests
{
    [Fact]
    public async Task CreateFiscalYearAsync_ShouldRejectRangeThatContainsExistingYear()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedFiscalYear(db, tenantId);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var action = () => service.CreateFiscalYearAsync(new ErpSystem.Core.DTOs.Finance.CreateFiscalYearDto
        {
            FiscalYearName = "Containing year",
            FiscalYearCode = "FY25-27",
            Year = 2027,
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2027, 12, 31),
            NumberOfPeriods = 12,
            PeriodType = PeriodType.Monthly
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*overlap*");
    }

    [Fact]
    public async Task CreateFiscalYearAsync_ShouldRejectEndBeforeStart()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var service = CreateService(db, tenantId);

        var action = () => service.CreateFiscalYearAsync(new ErpSystem.Core.DTOs.Finance.CreateFiscalYearDto
        {
            FiscalYearName = "Invalid year",
            FiscalYearCode = "BAD",
            Year = 2026,
            StartDate = new DateTime(2026, 12, 31),
            EndDate = new DateTime(2026, 1, 1),
            NumberOfPeriods = 12,
            PeriodType = PeriodType.Monthly
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*end date*");
    }

    [Fact]
    public async Task CreateFiscalYearAsync_ShouldRejectNonContiguousYear()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedFiscalYear(db, tenantId);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var action = () => service.CreateFiscalYearAsync(new ErpSystem.Core.DTOs.Finance.CreateFiscalYearDto
        {
            FiscalYearName = "Fiscal Year 2028",
            FiscalYearCode = "FY2028",
            Year = 2028,
            StartDate = new DateTime(2028, 1, 1),
            EndDate = new DateTime(2028, 12, 31),
            NumberOfPeriods = 12,
            PeriodType = PeriodType.Monthly
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*FY2027*contiguously*");
    }

    [Fact]
    public async Task CreateFiscalYearAsync_ShouldRejectPeriodTypeChange()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedFiscalYear(db, tenantId);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var action = () => service.CreateFiscalYearAsync(new ErpSystem.Core.DTOs.Finance.CreateFiscalYearDto
        {
            FiscalYearName = "Fiscal Year 2027",
            FiscalYearCode = "FY2027",
            Year = 2027,
            StartDate = new DateTime(2027, 1, 1),
            EndDate = new DateTime(2027, 12, 31),
            NumberOfPeriods = 52,
            PeriodType = PeriodType.Weekly
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*uses Monthly periods*same period type*");
    }

    [Fact]
    public async Task CreateFiscalYearAsync_ShouldCreateContiguousYearWithEstablishedPeriodType()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedFiscalYear(db, tenantId);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var created = await service.CreateFiscalYearAsync(new ErpSystem.Core.DTOs.Finance.CreateFiscalYearDto
        {
            FiscalYearName = "Fiscal Year 2027",
            FiscalYearCode = "FY2027",
            Year = 2027,
            StartDate = new DateTime(2027, 1, 1),
            EndDate = new DateTime(2027, 12, 31),
            NumberOfPeriods = 12,
            PeriodType = PeriodType.Monthly
        });

        created.Year.Should().Be(2027);
        (await db.FiscalPeriods.Where(period => period.FiscalYearId == created.Id).ToListAsync())
            .Should().OnlyContain(period => period.PeriodType == PeriodType.Monthly);
    }

    [Fact]
    public async Task CreateFiscalYearAsync_ShouldRejectMixedActivePeriodTypes()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (year, _) = SeedFiscalYear(db, tenantId);
        db.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = year.Id,
            PeriodName = "Mixed weekly period",
            PeriodCode = "FY2026-W02",
            PeriodNumber = 2,
            PeriodType = PeriodType.Weekly,
            StartDate = new DateTime(2026, 2, 1),
            EndDate = new DateTime(2026, 2, 7),
            PeriodDays = 7,
            PeriodStatus = "Future"
        });
        await db.SaveChangesAsync();

        var action = () => CreateService(db, tenantId).CreateFiscalYearAsync(Create2027Dto());

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*mixed period types*Repair*");
    }

    [Fact]
    public async Task CreateFiscalYearAsync_ShouldIgnoreSoftDeletedPeriodTypeOutlier()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (year, _) = SeedFiscalYear(db, tenantId);
        db.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = year.Id,
            PeriodName = "Retired weekly outlier",
            PeriodCode = "FY2026-W02",
            PeriodNumber = 2,
            PeriodType = PeriodType.Weekly,
            StartDate = new DateTime(2026, 2, 1),
            EndDate = new DateTime(2026, 2, 7),
            PeriodDays = 7,
            PeriodStatus = "Future",
            IsDeleted = true
        });
        await db.SaveChangesAsync();

        var created = await CreateService(db, tenantId).CreateFiscalYearAsync(Create2027Dto());

        created.Year.Should().Be(2027);
    }

    [Fact]
    public async Task CreateFiscalYearAsync_ShouldPermitOnlyOneConcurrentNextYear()
    {
        var tenantId = Guid.NewGuid();
        var connectionString = $"Data Source=fiscal-calendar-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        await using (var setup = keeper.CreateCommand())
        {
            setup.CommandText =
                """
                CREATE TABLE FiscalYears (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TenantId TEXT NOT NULL,
                    Year INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );
                CREATE UNIQUE INDEX IX_FiscalYears_TenantId_Year
                    ON FiscalYears (TenantId, Year)
                    WHERE IsDeleted = 0;
                """;
            await setup.ExecuteNonQueryAsync();
        }

        async Task<bool> TryInsertAsync()
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO FiscalYears (Id, TenantId, Year, IsDeleted) VALUES ($id, $tenantId, 2027, 0);";
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$tenantId", tenantId.ToString());
            try
            {
                await command.ExecuteNonQueryAsync();
                return true;
            }
            catch (SqliteException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(TryInsertAsync(), TryInsertAsync());

        results.Count(succeeded => succeeded).Should().Be(1);
        await using var verification = keeper.CreateCommand();
        verification.CommandText =
            "SELECT COUNT(*) FROM FiscalYears WHERE TenantId = $tenantId AND Year = 2027 AND IsDeleted = 0;";
        verification.Parameters.AddWithValue("$tenantId", tenantId.ToString());
        Convert.ToInt32(await verification.ExecuteScalarAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Category", "FiscalYearProjection")]
    public async Task GetFiscalYearsAsync_ShouldReturnSelectionAndControlFields()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (year, _) = SeedFiscalYear(db, tenantId);
        year.Status = "Open";
        year.IsLocked = false;
        year.ReportingFramework = "IFRS";
        year.BaseCurrency = "GHS";
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var result = await service.GetFiscalYearsAsync();

        var dto = result.Should().ContainSingle().Subject;
        dto.Id.Should().Be(year.Id);
        dto.Year.Should().Be(2026);
        dto.Status.Should().Be("Open");
        dto.IsActive.Should().BeTrue();
        dto.IsClosed.Should().BeFalse();
        dto.IsLocked.Should().BeFalse();
        dto.NumberOfPeriods.Should().Be(1);
        dto.BaseCurrency.Should().Be("GHS");
    }

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
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task DeleteFiscalYearAsync_PreservesBookCloseEvidenceEvenWithoutAJournal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (year, _) = SeedFiscalYear(db, tenantId);
        db.YearEndBookCloseCycles.Add(new YearEndBookCloseCycle
        {
            TenantId = tenantId, FiscalYearId = year.Id, AccountingBookId = Guid.NewGuid(),
            AccountingBookCode = "BASE", FunctionalCurrencyCode = "GHS", CycleNumber = 1,
            IdempotencyKey = "empty-year-close", RetainedEarningsAccountId = Guid.NewGuid(),
            Status = "Closed", ClosedByUserId = Guid.NewGuid(), ClosedAtUtc = DateTime.UtcNow,
            PeriodAuthoritySnapshotJson = "[]"
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        await service.Invoking(x => x.DeleteFiscalYearAsync(year.Id)).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*year-end close cycles*");
        year.IsDeleted.Should().BeFalse();
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
            Mock.Of<ILogger<FiscalPeriodService>>(),
            Mock.Of<ISubledgerSettlementReadModelService>());
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

    private static ErpSystem.Core.DTOs.Finance.CreateFiscalYearDto Create2027Dto() =>
        new()
        {
            FiscalYearName = "Fiscal Year 2027",
            FiscalYearCode = "FY2027",
            Year = 2027,
            StartDate = new DateTime(2027, 1, 1),
            EndDate = new DateTime(2027, 12, 31),
            NumberOfPeriods = 12,
            PeriodType = PeriodType.Monthly
        };
}
