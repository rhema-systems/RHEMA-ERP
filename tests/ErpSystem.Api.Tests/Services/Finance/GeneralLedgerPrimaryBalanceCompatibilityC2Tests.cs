using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class GeneralLedgerPrimaryBalanceCompatibilityC2Tests
{
    [Fact]
    public async Task LegacyBalanceInquiry_ReturnsPrimaryCompatibilityOnlyForEligibleMapping()
    {
        await using var fixture = await Fixture.CreateAsync("valid");

        (await fixture.Service.GetAccountBalanceAsync(fixture.Account.Id)).Should().Be(42m);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("disabled")]
    [InlineData("foreign")]
    [InlineData("retired")]
    [InlineData("non-posting")]
    [InlineData("wrong-type")]
    public async Task LegacyBalanceInquiry_FailsClosedWithoutCompatibleEnabledPrimaryMapping(string mode)
    {
        await using var fixture = await Fixture.CreateAsync(mode);

        await fixture.Service.Invoking(service => service.GetAccountBalanceAsync(fixture.Account.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ACCOUNT_BOOK_MAPPING_INVALID*");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; }
        public Account Account { get; }
        public GeneralLedgerService Service { get; }

        private Fixture(ApplicationDbContext db, Account account, GeneralLedgerService service) =>
            (Db, Account, Service) = (db, account, service);

        public static async Task<Fixture> CreateAsync(string mode)
        {
            var tenantId = Guid.NewGuid();
            var otherTenant = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"c2-primary-balance-{Guid.NewGuid():N}").Options;
            var db = new ApplicationDbContext(options);
            var account = new Account
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = "1000", AccountNumber = "1000",
                AccountName = "Cash", AccountType = AccountType.Asset, CurrencyCode = "GHS",
                Status = AccountStatus.Active, Balance = 42m
            };
            var book = new AccountingBook
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "PRIMARY_FULL", Name = "Primary",
                IsDefault = true, IsActive = true, AllowsPosting = true
            };
            var classification = new AccountClassification
            {
                Id = Guid.NewGuid(), TenantId = mode == "foreign" ? otherTenant : tenantId,
                AccountingBookId = book.Id, Code = "CASH", Name = "Cash",
                CoreAccountType = mode == "wrong-type" ? AccountType.Liability : AccountType.Asset,
                Status = mode == "retired" ? AccountClassificationStatus.Retired : AccountClassificationStatus.Active,
                IsPostingClassification = mode != "non-posting"
            };
            db.AddRange(
                new Tenant { Id = tenantId, Code = "C2GL", Name = "C2 GL", BaseCurrency = "GHS" },
                new Tenant { Id = otherTenant, Code = "OTHER", Name = "Other", BaseCurrency = "GHS" },
                account, book, classification);
            if (mode != "missing")
                db.AccountAccountingBooks.Add(new AccountAccountingBook
                {
                    Id = Guid.NewGuid(), TenantId = mode == "foreign" ? otherTenant : tenantId,
                    AccountId = account.Id, AccountingBookId = book.Id,
                    AccountClassificationId = classification.Id, AccountClassification = classification,
                    IsEnabled = mode != "disabled"
                });
            await db.SaveChangesAsync();

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
            currentUser.SetupGet(item => item.Claims).Returns(new Dictionary<string, string>());
            var settings = new Mock<ITenantSettingsService>();
            settings.Setup(item => item.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
            var reporting = new ReportingDbContext(new DbContextOptionsBuilder<ReportingDbContext>()
                .UseInMemoryDatabase($"c2-primary-balance-read-{Guid.NewGuid():N}").Options);
            var service = new GeneralLedgerService(db, reporting, currentUser.Object, settings.Object,
                Mock.Of<IFiscalPeriodService>(), Mock.Of<IDocumentNumberingService>(),
                Mock.Of<IAccountingBookService>(), Mock.Of<IFinancePostingEngine>());
            return new Fixture(db, account, service);
        }

        public async ValueTask DisposeAsync() => await Db.DisposeAsync();
    }
}
