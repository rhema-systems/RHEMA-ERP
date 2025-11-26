using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErpSystem.Api.Services;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Tests.Services
{
    public class GeneralLedgerServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly GeneralLedgerService _service;
        private readonly Guid _tenantId = Guid.NewGuid();

        public GeneralLedgerServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockCurrentUserService.Setup(s => s.TenantId).Returns(_tenantId);

            _service = new GeneralLedgerService(_context, _mockCurrentUserService.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Fact]
        public async Task ValidateAccountStructureAsync_ShouldReturnTrue_WhenStructureIsValid()
        {
            // Arrange
            var structure = new AccountSegmentStructure
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                SegmentName = "Segment 1",
                SegmentPosition = 1,
                SegmentLength = 3,
                DataType = "Alphanumeric",
                SeparatorCharacter = "-"
            };
            _context.AccountSegmentStructures.Add(structure);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.ValidateAccountStructureAsync("123");

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateAccountStructureAsync_ShouldThrowException_WhenLengthIsInvalid()
        {
            // Arrange
            var structure = new AccountSegmentStructure
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                SegmentName = "Segment 1",
                SegmentPosition = 1,
                SegmentLength = 3,
                DataType = "Alphanumeric",
                SeparatorCharacter = "-",
                Status = "Active"
            };
            _context.AccountSegmentStructures.Add(structure);
            await _context.SaveChangesAsync();

            // Act
            Func<Task> act = async () => await _service.ValidateAccountStructureAsync("1234");

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*length invalid*");
        }

        [Fact]
        public async Task RunCurrencyRevaluationAsync_ShouldCreateJournalEntry_WhenExchangeRateChanged()
        {
            // Arrange
            var revaluationDate = DateTime.UtcNow.Date;
            var accountId = Guid.NewGuid();
            var unrealizedGainLossAccountId = Guid.NewGuid();

            var account = new Account
            {
                Id = accountId,
                TenantId = _tenantId,
                AccountNumber = "1000",
                AccountName = "Foreign Bank",
                AccountType = AccountType.Asset,
                CurrencyCode = "USD",
                IsMultiCurrency = true,
                Status = AccountStatus.Active
            };

            var fiscalPeriod = new FiscalPeriod
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                StartDate = revaluationDate.AddDays(-10),
                EndDate = revaluationDate.AddDays(10),
                PeriodStatus = "Open",
                IsOpen = true,
                PeriodName = "Test Period"
            };

            var transaction = new AccountTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                AccountId = accountId,
                TransactionDate = revaluationDate.AddDays(-1),
                TransactionCurrency = "USD",
                ForeignCurrencyAmount = 100, // 100 USD
                DebitAmount = 1500, // 100 USD * 15.0 GHS
                CreditAmount = 0,
                ExchangeRate = 15.0m,
                FiscalPeriodId = fiscalPeriod.Id,
                BookClassification = "IFRS"
            };

            var exchangeRate = new ExchangeRate
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                BaseCurrencyCode = "GHS",
                TargetCurrencyCode = "USD",
                Rate = 16.0m, // Rate increased to 16.0
                EffectiveDate = revaluationDate,
                RateType = ExchangeRateType.MonthEnd,
                CreatedByUserId = Guid.NewGuid()
            };

            _context.Accounts.Add(account);
            _context.FiscalPeriods.Add(fiscalPeriod);
            _context.AccountTransactions.Add(transaction);
            _context.ExchangeRates.Add(exchangeRate);
            await _context.SaveChangesAsync();

            var request = new RevaluationRequestDto
            {
                RevaluationDate = revaluationDate,
                RevaluationType = "Month-End",
                UnrealizedGainLossAccountId = unrealizedGainLossAccountId
            };

            // Act
            var journalEntry = await _service.RunCurrencyRevaluationAsync(request);

            // Assert
            journalEntry.Should().NotBeNull();
            journalEntry.Transactions.Should().HaveCount(2); // Adjustment + Balancing

            // Expected Adjustment:
            // Foreign Balance = 100 USD
            // Old Base Balance = 1500 GHS
            // New Base Balance = 100 * 16.0 = 1600 GHS
            // Adjustment = 1600 - 1500 = 100 GHS (Debit to Asset)

            var adjustmentTxn = journalEntry.Transactions.First(t => t.AccountId == accountId);
            adjustmentTxn.DebitAmount.Should().Be(100);
            adjustmentTxn.CreditAmount.Should().Be(0);

            var balancingTxn = journalEntry.Transactions.First(t => t.AccountId == unrealizedGainLossAccountId);
            balancingTxn.CreditAmount.Should().Be(100); // Credit to Gain/Loss (Gain)
            balancingTxn.DebitAmount.Should().Be(0);
        }
    }
}
