using System.Reflection;
using ErpSystem.Api.Services.Finance.UnitAccounting;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class BookBalanceConsumerAuthorityC2Tests
{
    [Fact]
    public async Task RatioAndAllocation_SelectOnlyExactPrimaryBookFunctionalCurrency()
    {
        await using var fixture = await Fixture.CreateAsync();

        (await fixture.InvokeRatioAsync()).Should().Be(100m);
        (await fixture.InvokeAllocationAsync()).Should().Be(100m);
    }

    [Fact]
    public async Task RatioAndAllocation_FailClosedWhenPrimaryAuthorityIsAmbiguous()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.AccountingBooks.Add(new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, Code = "MANAGEMENT", Name = "Management",
            IsDefault = true, IsActive = true, AllowsPosting = true
        });
        await fixture.Db.SaveChangesAsync();

        await fixture.Invoking(item => item.InvokeRatioAsync()).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*PRIMARY_BOOK_AUTHORITY_AMBIGUOUS*");
        await fixture.Invoking(item => item.InvokeAllocationAsync()).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*PRIMARY_BOOK_AUTHORITY_AMBIGUOUS*");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private static readonly MethodInfo RatioMethod = typeof(RatioDefinitionService)
            .GetMethod("GetComponentValue", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly MethodInfo AllocationMethod = typeof(AllocationService)
            .GetMethod("GetSourceAccountPeriodBalanceAsync", BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null, new[] { typeof(Guid), typeof(Guid), typeof(CancellationToken) }, modifiers: null)!;

        public Guid TenantId { get; }
        public Guid AccountId { get; } = Guid.NewGuid();
        public Guid PeriodId { get; } = Guid.NewGuid();
        public ApplicationDbContext Db { get; }
        private readonly RatioDefinitionService _ratio;
        private readonly AllocationService _allocation;

        private Fixture(Guid tenantId, ApplicationDbContext db, ICurrentUserService user)
        {
            TenantId = tenantId;
            Db = db;
            var unit = new UnitOfWork(db);
            _ratio = new RatioDefinitionService(unit, user, Mock.Of<ILogger<RatioDefinitionService>>());
            _allocation = new AllocationService(unit, user, Mock.Of<IWorkflowService>(),
                Mock.Of<IFinancePostingEngine>(), Mock.Of<ILogger<AllocationService>>());
        }

        public static async Task<Fixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"c2-consumers-{Guid.NewGuid():N}").Options;
            var tenantId = Guid.NewGuid();
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(item => item.TenantId).Returns(tenantId);
            user.SetupGet(item => item.Claims).Returns(new Dictionary<string, string>());
            var fixture = new Fixture(tenantId, new ApplicationDbContext(options), user.Object);

            var primary = new AccountingBook
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "Primary",
                IsDefault = true, IsActive = true, AllowsPosting = true
            };
            var local = new AccountingBook
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "LOCAL_STATUTORY", Name = "Local",
                IsActive = true, AllowsPosting = true
            };
            fixture.Db.AddRange(
                new Tenant { Id = tenantId, Code = "C2C", Name = "C2 consumers", BaseCurrency = "GHS" },
                primary, local,
                new AccountBalance
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, AccountId = fixture.AccountId,
                    AccountingBookId = primary.Id, BookClassification = primary.Code,
                    FiscalPeriodId = fixture.PeriodId, Currency = "GHS", ClosingBalance = 100m
                },
                new AccountBalance
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, AccountId = fixture.AccountId,
                    AccountingBookId = local.Id, BookClassification = local.Code,
                    FiscalPeriodId = fixture.PeriodId, Currency = "GHS", ClosingBalance = 900m
                });
            await fixture.Db.SaveChangesAsync();
            return fixture;
        }

        public async Task<decimal> InvokeRatioAsync()
        {
            var task = (Task<decimal>)RatioMethod.Invoke(_ratio,
                new object?[] { RatioComponentType.FinancialAccount, AccountId, null, PeriodId, CancellationToken.None })!;
            return await task;
        }

        public async Task<decimal> InvokeAllocationAsync()
        {
            var task = (Task<decimal>)AllocationMethod.Invoke(_allocation,
                new object[] { AccountId, PeriodId, CancellationToken.None })!;
            return await task;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
