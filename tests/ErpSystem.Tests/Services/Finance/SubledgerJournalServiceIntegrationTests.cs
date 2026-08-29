using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ErpSystem.Core.Constants;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Tests.Services.Finance
{
    public class SubledgerJournalServiceIntegrationTests : IDisposable
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
        private readonly Guid _tenantId = Guid.NewGuid();

        // System Under Test
        private readonly SubledgerJournalService _sut;
        
        public SubledgerJournalServiceIntegrationTests()
        {
            // IMPORTANT: This test explicitly tests WITH Foreign Keys=True to prove relational integrity
            _connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:;Foreign Keys=True;");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ApplicationDbContext(options, _tenantId);
            ApplicationDbContext.IsTesting = true;
            _dbContext.Database.EnsureCreated();

            // Set up real dependencies for the full posting flow
            var repository = new FinancialRepository(_dbContext);
            
            var currentUserMock = new Mock<ICurrentUserService>();
            currentUserMock.Setup(x => x.TenantId).Returns(_tenantId);
            currentUserMock.Setup(x => x.UserId).Returns(Guid.NewGuid().ToString());

            var tenantSettingsMock = new Mock<ITenantSettingsService>();
            tenantSettingsMock.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");

            var bookValidationMock = new Mock<IBookValidationService>();
            bookValidationMock.Setup(x => x.ResolveTargetBooksAsync(It.IsAny<string>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "Primary" });
            bookValidationMock.Setup(x => x.ValidateAccountsForBookAsync(It.IsAny<List<Guid>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var journalEntryService = new FakeJournalEntryService(_dbContext, _tenantId);

            var postingLoggerMock = new Mock<ILogger<SubledgerPostingService>>();
            var postingService = new SubledgerPostingService(
                repository,
                currentUserMock.Object,
                tenantSettingsMock.Object,
                journalEntryService,
                bookValidationMock.Object,
                postingLoggerMock.Object);

            _sut = new SubledgerJournalService(
                _dbContext,
                repository,
                currentUserMock.Object,
                postingService,
                journalEntryService);
        }

        [Fact]
        public async Task PostAsync_WithForeignKeysEnabled_CreatesRelationallyValidPosting()
        {
            // Arrange
            // 0. Seed Tenant
            var tenant = new Tenant { Id = _tenantId, Name = "Test Tenant", Code = "TT01" };
            _dbContext.Tenants.Add(tenant);

            // 1. Seed Base Currency (GHS)
            var baseCurrency = new Currency { Id = Guid.NewGuid(), TenantId = _tenantId, CurrencyCode = "GHS", NumericCode = "936", CurrencyName = "Ghana Cedi", IsBaseCurrency = true };
            _dbContext.Currencies.Add(baseCurrency);

            // 2. Seed Foreign Currency (USD)
            var foreignCurrency = new Currency { Id = Guid.NewGuid(), TenantId = _tenantId, CurrencyCode = "USD", NumericCode = "840", CurrencyName = "US Dollar", IsBaseCurrency = false };
            _dbContext.Currencies.Add(foreignCurrency);

            // 3. Seed Fiscal Year and Period
            var year = new FiscalYear { Id = Guid.NewGuid(), TenantId = _tenantId, Year = 2026, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), IsActive = true };
            var period = new FiscalPeriod { Id = Guid.NewGuid(), TenantId = _tenantId, FiscalYearId = year.Id, PeriodNumber = 5, StartDate = new DateTime(2026, 5, 1), EndDate = new DateTime(2026, 5, 31), IsOpen = true };
            _dbContext.FiscalYears.Add(year);
            _dbContext.FiscalPeriods.Add(period);

            // 4. Seed Accounts
            var arAccount = new Account { Id = Guid.NewGuid(), TenantId = _tenantId, AccountCode = "1200", AccountName = "AR Control", CurrencyCode = "GHS" };
            var offsetAccount = new Account { Id = Guid.NewGuid(), TenantId = _tenantId, AccountCode = "4000", AccountName = "Sales Revenue", CurrencyCode = "GHS" };
            _dbContext.Accounts.AddRange(arAccount, offsetAccount);

            // 5. Seed Finance Settings
            var settings = new FinanceSettings { Id = Guid.NewGuid(), TenantId = _tenantId, ControlAccountArId = arAccount.Id, SubledgerPostingMode = "Direct" };
            _dbContext.FinanceSettings.Add(settings);

            // 6. Seed Business Partner
            var bp = new BusinessPartner { Id = Guid.NewGuid(), TenantId = _tenantId, PartnerCode = "C001", PartnerName = "Valid Customer", DefaultArAccountId = arAccount.Id, OutstandingBalance = 0 };
            _dbContext.BusinessPartners.Add(bp);

            await _dbContext.SaveChangesAsync();

            // Act
            // Create the subledger batch
            var dto = new CreateSubledgerJournalDto
            {
                SubledgerType = "AR",
                EntryType = SubledgerEntryType.OpeningBalance,
                TransactionDate = new DateTime(2026, 5, 15),
                BaseCurrencyCode = "GHS",
                Description = "FK Integration Test",
                Lines = new List<CreateSubledgerJournalLineDto>
                {
                    new CreateSubledgerJournalLineDto
                    {
                        BusinessPartnerId = bp.Id,
                        AccountId = offsetAccount.Id,
                        Description = "USD Open Item",
                        CurrencyCode = "USD",
                        ExchangeRate = 12.0m,
                        ForeignDebitAmount = 100m,
                        BaseDebitAmount = 1200m,
                        DueDate = new DateTime(2026, 5, 30)
                    }
                }
            };

            var created = await _sut.CreateAsync(dto);
            
            // Post the subledger batch (triggers open items, journal entries, and account transactions)
            var posted = await _sut.PostAsync(created.Id);

            // Assert
            Assert.Equal("Posted", posted.Status);
            
            // Assert relationally valid Open Item (Invoice)
            var invoice = await _dbContext.Invoices.FirstOrDefaultAsync(i => i.BusinessPartnerId == bp.Id);
            Assert.NotNull(invoice);
            Assert.Equal(bp.Id, invoice.BusinessPartnerId); // FK points to existing row
            Assert.Equal("USD", invoice.CurrencyCode);
            Assert.Equal(100m, invoice.TotalAmount); // Foreign amount
            
            // Assert Business Partner balance update
            var updatedBp = await _dbContext.BusinessPartners.FirstAsync(b => b.Id == bp.Id);
            Assert.Equal(100m, updatedBp.OutstandingBalance);

            // Assert relationally valid Journal Entry and Account Transactions
            var jeLinks = await _dbContext.SubledgerJournalGlLinks.Where(l => l.SubledgerJournalEntryId == created.Id).ToListAsync();
            Assert.NotEmpty(jeLinks);
            
            var jeId = jeLinks.First().JournalEntryId;
            var journalEntry = await _dbContext.JournalEntries
                .Include(j => j.Transactions)
                .FirstOrDefaultAsync(j => j.Id == jeId);
                
            Assert.NotNull(journalEntry);
            Assert.Equal(2, journalEntry.Transactions.Count);
            
            // Verify FK constraints on transactions (e.g. valid Account IDs, Currency Codes)
            foreach (var txn in journalEntry.Transactions)
            {
                Assert.NotEqual(Guid.Empty, txn.AccountId);
                var validAccount = await _dbContext.Accounts.AnyAsync(a => a.Id == txn.AccountId);
                Assert.True(validAccount, "AccountTransaction must point to a valid Account.");
                
                Assert.NotNull(txn.TransactionCurrency);
            }
        }

        public void Dispose()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
            _connection.Dispose();
        }

        private class FakeJournalEntryService : IJournalEntryService
        {
            private readonly ApplicationDbContext _dbContext;
            private readonly Guid _tenantId;

            public FakeJournalEntryService(ApplicationDbContext dbContext, Guid tenantId)
            {
                _dbContext = dbContext;
                _tenantId = tenantId;
            }

            public Task<IReadOnlyList<JournalEntryDto>> GetJournalEntriesBySourceAsync(string sourceModule, Guid sourceDocumentId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<JournalEntryDto>>(new List<JournalEntryDto>());
            }

            public async Task<JournalEntryDto> CreateJournalEntryAsync(CreateJournalEntryDto dto, CancellationToken cancellationToken = default)
            {
                var entry = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenantId,
                    JournalEntryNumber = "JE-1000",
                    EntryDate = dto.TransactionDate,
                    Description = dto.Description,
                    PostingStatus = "Draft",
                    BookClassification = dto.BookClassification,
                    FiscalPeriodId = _dbContext.FiscalPeriods.FirstOrDefault()?.Id ?? Guid.Empty
                };

                _dbContext.JournalEntries.Add(entry);

                int lineNum = 1;
                foreach (var txn in dto.Transactions)
                {
                    _dbContext.AccountTransactions.Add(new AccountTransaction
                    {
                        Id = Guid.NewGuid(),
                        TenantId = entry.TenantId,
                        JournalEntryId = entry.Id,
                        AccountId = txn.AccountId,
                        DebitAmount = txn.TransactionType == "Debit" ? txn.Amount : 0,
                        CreditAmount = txn.TransactionType == "Credit" ? txn.Amount : 0,
                        TransactionDate = dto.TransactionDate,
                        TransactionCurrency = txn.CurrencyCode,
                        ExchangeRate = txn.ExchangeRate,
                        ForeignCurrencyAmount = txn.ForeignAmount,
                        BookClassification = dto.BookClassification,
                        FiscalPeriodId = entry.FiscalPeriodId,
                        LineNumber = lineNum++
                    });
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                return new JournalEntryDto { Id = entry.Id };
            }

            public async Task<JournalEntryDto> PostJournalEntryAsync(Guid id, CancellationToken cancellationToken = default)
            {
                var entry = await _dbContext.JournalEntries.FindAsync(new object[] { id }, cancellationToken);
                if (entry != null)
                {
                    entry.PostingStatus = "Posted";
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                return new JournalEntryDto { Id = id };
            }

            public Task<JournalEntryDto> PostJournalEntryForBatchAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => PostJournalEntryAsync(id, cancellationToken);

            public Task NotifyJournalPostedAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            // Unused methods for this test
            public Task<IReadOnlyList<JournalEntryDto>> GetJournalEntriesAsync(
                CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<JournalEntryDto>> GetJournalEntriesAsync(
                string? status,
                DateTime? startDate,
                DateTime? endDate,
                Guid? fiscalPeriodId,
                string? sourceModule,
                CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<JournalEntryDto?> GetJournalEntryByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<JournalEntryDto?> GetJournalEntryByNumberAsync(string journalNumber, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<JournalEntryDto>> GetJournalEntriesByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<JournalEntryDto> UpdateJournalEntryAsync(Guid id, UpdateJournalEntryDto dto, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task DeleteJournalEntryAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<JournalEntryDto> ReverseJournalEntryAsync(Guid id, string reason, DateTime? reversalDate = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> ValidateBalanceAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<string> GenerateJournalEntryNumberAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task UpdateApprovalStatusAsync(Guid id, string postingStatus, ErpSystem.Core.Enums.DocumentApprovalStatus? approvalStatus, Guid? approvedByUserId = null, string? rejectionReason = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task LinkAttachmentAsync(Guid journalEntryId, Guid fileUploadRecordId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task UnlinkAttachmentAsync(Guid journalEntryId, Guid fileUploadRecordId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<Guid>> GetAttachmentIdsAsync(Guid journalEntryId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }
    }
}
