using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Api.Services.Finance.AR;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Tests.Services.Finance
{
    public class InvoicePostingIntegrationTests : IDisposable
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
        private readonly Guid _tenantId = Guid.NewGuid();

        // System Under Test
        private readonly InvoiceService _sut;
        
        // Mocks that we might want to verify
        private readonly Mock<IJournalEntryService> _journalEntryServiceMock;
        private readonly Mock<ITaxCalculationEngine> _taxEngineMock;
        private readonly Mock<IInventoryValuationService> _inventoryValuationMock;
        private readonly Guid _taxGroupId = Guid.NewGuid();
        
        public InvoicePostingIntegrationTests()
        {
            _connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:;Foreign Keys=True;");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ApplicationDbContext(options, _tenantId);
            ApplicationDbContext.IsTesting = true;
            _dbContext.Database.EnsureCreated();

            var unitOfWork = new UnitOfWork(_dbContext);
            var repository = new FinancialRepository(_dbContext);
            
            var currentUserMock = new Mock<ICurrentUserService>();
            currentUserMock.SetupGet(x => x.TenantId).Returns(_tenantId);
            currentUserMock.SetupGet(x => x.UserName).Returns("test_user");

            var tenantSettingsMock = new Mock<ITenantSettingsService>();
            tenantSettingsMock.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");

            var bookValidationMock = new Mock<IBookValidationService>();
            bookValidationMock.Setup(x => x.ResolveTargetBooksAsync(It.IsAny<string>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "Primary" });
            bookValidationMock.Setup(x => x.ValidateAccountsForBookAsync(It.IsAny<List<Guid>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _journalEntryServiceMock = new Mock<IJournalEntryService>();
            _taxEngineMock = new Mock<ITaxCalculationEngine>();
            _inventoryValuationMock = new Mock<IInventoryValuationService>();

            var postingLoggerMock = new Mock<ILogger<SubledgerPostingService>>();
            var postingService = new SubledgerPostingService(
                repository,
                currentUserMock.Object,
                tenantSettingsMock.Object,
                _journalEntryServiceMock.Object,
                bookValidationMock.Object,
                postingLoggerMock.Object);

            var loggerMock = new Mock<ILogger<InvoiceService>>();

            var taxAuditLoggerMock = new Mock<ILogger<ErpSystem.Api.Services.Finance.Taxation.TaxAuditService>>();
            var realTaxAuditService = new ErpSystem.Api.Services.Finance.Taxation.TaxAuditService(
                repository,
                currentUserMock.Object,
                taxAuditLoggerMock.Object
            );

            _sut = new InvoiceService(
                unitOfWork,
                currentUserMock.Object,
                _taxEngineMock.Object,
                realTaxAuditService,
                postingService,
                _inventoryValuationMock.Object,
                tenantSettingsMock.Object,
                loggerMock.Object);
        }

        private async Task SeedDataAsync(
            Guid bpId,
            Guid arAccountId,
            Guid revenueAccountId,
            Guid taxAccountId,
            Guid cogsAccountId,
            Guid inventoryAccountId,
            Guid? taxId = null)
        {
            var tenant = new Tenant
            {
                Id = _tenantId,
                Name = "Test Tenant",
                Code = "TT01"
            };
            _dbContext.Tenants.Add(tenant);

            var currencyBase = new Currency { Id = Guid.NewGuid(), TenantId = _tenantId, CurrencyCode = "GHS", NumericCode = "936", CurrencyName = "Ghana Cedi", IsBaseCurrency = true };
            var currencyForeign = new Currency { Id = Guid.NewGuid(), TenantId = _tenantId, CurrencyCode = "USD", NumericCode = "840", CurrencyName = "US Dollar", IsBaseCurrency = false };
            _dbContext.Currencies.AddRange(currencyBase, currencyForeign);

            var yearId = Guid.NewGuid();
            var fy = new FiscalYear { Id = yearId, TenantId = _tenantId, Year = 2024, StartDate = new DateTime(2024, 1, 1), EndDate = new DateTime(2024, 12, 31), IsActive = true };
            _dbContext.FiscalYears.Add(fy);

            var periodId = Guid.NewGuid();
            var fp = new FiscalPeriod { Id = periodId, TenantId = _tenantId, FiscalYearId = yearId, PeriodNumber = 1, StartDate = new DateTime(2024, 1, 1), EndDate = new DateTime(2024, 1, 31), IsOpen = true };
            _dbContext.FiscalPeriods.Add(fp);

            var settings = new FinanceSettings
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                ControlAccountArId = arAccountId,
                ControlAccountTaxId = taxAccountId,
                ControlAccountCOGSId = cogsAccountId,
                ControlAccountInventoryId = inventoryAccountId,
                SubledgerPostingMode = "Direct"
            };
            _dbContext.FinanceSettings.Add(settings);

            var arAcc = new Account { Id = arAccountId, TenantId = _tenantId, AccountCode = "1200", AccountName = "AR", CurrencyCode = "GHS" };
            var revAcc = new Account { Id = revenueAccountId, TenantId = _tenantId, AccountCode = "4000", AccountName = "Revenue", CurrencyCode = "GHS" };
            var taxAcc = new Account { Id = taxAccountId, TenantId = _tenantId, AccountCode = "2200", AccountName = "VAT", CurrencyCode = "GHS" };
            var cogsAcc = new Account { Id = cogsAccountId, TenantId = _tenantId, AccountCode = "5000", AccountName = "COGS", CurrencyCode = "GHS" };
            var invAcc = new Account { Id = inventoryAccountId, TenantId = _tenantId, AccountCode = "1300", AccountName = "Inventory", CurrencyCode = "GHS" };
            _dbContext.Accounts.AddRange(arAcc, revAcc, taxAcc, cogsAcc, invAcc);

            var bp = new ErpSystem.Core.Entities.Procurement.BusinessPartner
            {
                Id = bpId,
                TenantId = _tenantId,
                PartnerCode = "C001",
                PartnerName = "Test Customer",
                PartnerType = "Customer",
                Currency = "USD",
                DefaultArAccountId = arAccountId,
                OutstandingBalance = 0m,
                CreditLimit = 100000m,
                IsActive = true
            };
            _dbContext.BusinessPartners.Add(bp);

            if (taxId.HasValue)
            {
                var tax = new ErpSystem.Core.Entities.Finance.Tax
                {
                    Id = taxId.Value,
                    TenantId = _tenantId,
                    Code = "VAT10",
                    Name = "VAT 10%",
                    Rate = 10m,
                    Applicability = ErpSystem.Core.Enums.TaxApplicability.Both,
                    Category = ErpSystem.Core.Enums.TaxCategory.Standard,
                    IsActive = true
                };
                _dbContext.Taxes.Add(tax);

                var taxGroup = new TaxGroup
                {
                    Id = _taxGroupId,
                    TenantId = _tenantId,
                    Code = "VAT10_GROUP",
                    Name = "VAT 10% Group",
                    Applicability = TaxApplicability.Both,
                    IsActive = true
                };
                _dbContext.Set<TaxGroup>().Add(taxGroup);
            }

            await _dbContext.SaveChangesAsync();
        }

        [Fact]
        public async Task SendInvoiceAsync_ForeignCurrency_ShouldPostCorrectBaseAmountsToGL()
        {
            // 1. Foreign-currency invoice currently posts wrong base GL amounts.
            // Arrange
            var bpId = Guid.NewGuid();
            var arAccId = Guid.NewGuid();
            var revAccId = Guid.NewGuid();
            var taxAccId = Guid.NewGuid();
            var cogsAccId = Guid.NewGuid();
            var invAccId = Guid.NewGuid();
            var taxId = Guid.NewGuid();

            await SeedDataAsync(bpId, arAccId, revAccId, taxAccId, cogsAccId, invAccId, taxId);

            _taxEngineMock.Setup(x => x.CalculateTaxesAsync(It.IsAny<TaxCalculationRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TaxCalculationResultDto 
                { 
                    TotalTaxAmount = 10m,
                    TaxBreakdowns = new List<TaxBreakdownDto> 
                    { 
                        new TaxBreakdownDto { TaxId = taxId, TaxableAmount = 90m, TaxRate = 0.1m, TaxAmount = 10m } 
                    }
                });

            var createDto = new InvoiceCreateDto
            {
                BusinessPartnerId = bpId,
                InvoiceDate = new DateTime(2024, 1, 15),
                CurrencyCode = "USD",
                ExchangeRate = 12.5m,
                TaxGroupId = _taxGroupId,
                LineItems = new List<InvoiceLineItemCreateDto>
                {
                    new InvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = revAccId,
                        Description = "Test Product",
                        Quantity = 1,
                        UnitPrice = 90m, // 90 USD
                        TaxCode = "VAT10",
                        TaxGroupId = _taxGroupId
                    }
                }
            };

            var invoiceDto = await _sut.CreateAsync(createDto);
            
            // Expected: Total 100 USD (90 + 10). Exchange 12.5. Base = 1250 GHS.
            
            CreateJournalEntryDto? capturedJe = null;
            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .Callback<CreateJournalEntryDto, CancellationToken>((je, ct) => capturedJe = je)
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            // Act
            await _sut.SendInvoiceAsync(invoiceDto.Id);

            // Assert
            Assert.NotNull(capturedJe);
            var arTransaction = capturedJe.Transactions.Single(t => t.TransactionType == "Debit" && t.AccountId == arAccId);
            
            // Currently this test will FAIL because the original code does: Amount = invoice.TotalAmount
            // The user requested that we explicitly write failing tests to prove the defect first.
            // We expect the correct behavior to be: Amount = BaseCurrencyAmount (1250)
            Assert.Equal(1250m, arTransaction.Amount); // Base amount
            Assert.Equal(100m, arTransaction.ForeignAmount); // Transaction currency amount

            // Verify tax audit trail persistence
            var taxRecords = await _dbContext.Set<TaxCalculation>()
                .Where(t => t.DocumentId == invoiceDto.Id && !t.IsReversed)
                .ToListAsync();
            
            Assert.NotEmpty(taxRecords);
            var taxRecord = taxRecords.First();
            Assert.Equal("CustomerInvoice", taxRecord.DocumentType);
            Assert.Equal(10m, taxRecord.TaxAmount);
            Assert.Equal(90m, taxRecord.BaseAmount);
        }

        [Fact]
        public async Task SendInvoiceAsync_GLPostingFailure_ShouldRollbackInvoiceAndCustomerBalance()
        {
            // 2. GL posting failure currently leaves invoice/customer/inventory changes committed.
            // Arrange
            var bpId = Guid.NewGuid();
            var arAccId = Guid.NewGuid();
            var revAccId = Guid.NewGuid();
            
            await SeedDataAsync(bpId, arAccId, revAccId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            var createDto = new InvoiceCreateDto
            {
                BusinessPartnerId = bpId,
                InvoiceDate = new DateTime(2024, 1, 15),
                CurrencyCode = "GHS",
                ExchangeRate = 1m,
                LineItems = new List<InvoiceLineItemCreateDto>
                {
                    new InvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = revAccId,
                        Description = "Test Product",
                        Quantity = 1,
                        UnitPrice = 100m
                    }
                }
            };

            // Setup TaxEngine to simulate tax generation BEFORE CreateAsync
            _taxEngineMock.Setup(x => x.CalculateTaxesAsync(It.IsAny<TaxCalculationRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TaxCalculationResultDto 
                { 
                    TotalTaxAmount = 10m,
                    TaxBreakdowns = new List<TaxBreakdownDto> 
                    { 
                        new TaxBreakdownDto { TaxId = Guid.NewGuid(), TaxableAmount = 90m, TaxRate = 0.1m, TaxAmount = 10m } 
                    }
                });

            var invoiceDto = await _sut.CreateAsync(createDto);

            // Set up a failure in GL posting to fail!
            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Simulated GL Posting Failure"));

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SendInvoiceAsync(invoiceDto.Id));

            // Assert
            // This test will FAIL on the current codebase because SendInvoiceAsync calls _unitOfWork.SaveChangesAsync() BEFORE posting to GL!
            // Thus, the invoice is saved as Sent and the Customer Balance is updated.
            
            // In a properly hardened system, the transaction should roll back everything!
            _dbContext.ChangeTracker.Clear();
            var freshInvoice = await _dbContext.Invoices.FindAsync(invoiceDto.Id);
            Assert.NotNull(freshInvoice);
            Assert.Equal(InvoiceStatus.Draft, freshInvoice.Status); // Should remain draft!

            var freshBp = await _dbContext.BusinessPartners.FindAsync(bpId);
            Assert.NotNull(freshBp);
            Assert.Equal(0m, freshBp.OutstandingBalance); // Should remain 0!

            // Verify Tax Audit Trail also rolled back
            var taxRecords = await _dbContext.Set<TaxCalculation>()
                .Where(t => t.DocumentId == invoiceDto.Id)
                .ToListAsync();
            Assert.Empty(taxRecords);
        }

        [Fact]
        public async Task VoidInvoiceAsync_ShouldReverseGLAndInventory()
        {
            // 3. Voiding a posted invoice currently fails to reverse GL and inventory.
            // Arrange
            var bpId = Guid.NewGuid();
            var arAccId = Guid.NewGuid();
            var revAccId = Guid.NewGuid();
            var taxId = Guid.NewGuid();
            
            await SeedDataAsync(bpId, arAccId, revAccId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), taxId);
            
            var bpCount = _dbContext.BusinessPartners.Count();
            Console.WriteLine($"BP Count after Seed: {bpCount}");
            
            var bpCheck = _dbContext.BusinessPartners.Find(bpId);
            Console.WriteLine($"BP Check: {(bpCheck != null ? "Found" : "Not Found")}");

            var createDto = new InvoiceCreateDto
            {
                BusinessPartnerId = bpId,
                InvoiceDate = new DateTime(2024, 1, 15),
                CurrencyCode = "GHS",
                ExchangeRate = 1m,
                TaxGroupId = _taxGroupId,
                LineItems = new List<InvoiceLineItemCreateDto>
                {
                    new InvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = revAccId,
                        Description = "Test Product",
                        Quantity = 1,
                        UnitPrice = 100m,
                        TaxCode = "VAT10",
                        TaxGroupId = _taxGroupId
                    }
                }
            };

            // Setup TaxEngine to simulate tax generation
            _taxEngineMock.Setup(x => x.CalculateTaxesAsync(It.IsAny<TaxCalculationRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TaxCalculationResultDto 
                { 
                    TotalTaxAmount = 10m,
                    TaxBreakdowns = new List<TaxBreakdownDto> 
                    { 
                        new TaxBreakdownDto { TaxId = taxId, TaxableAmount = 90m, TaxRate = 0.1m, TaxAmount = 10m } 
                    }
                });

            var invoiceDto = await _sut.CreateAsync(createDto);
            
            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            // Setup the entries to return for reversal
            var dummyEntry = new JournalEntryDto { Id = Guid.NewGuid(), PostingStatus = "Posted" };
            _journalEntryServiceMock.Setup(x => x.GetJournalEntriesBySourceAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JournalEntryDto> { dummyEntry });

            await _sut.SendInvoiceAsync(invoiceDto.Id);

            // Act
            await _sut.VoidInvoiceAsync(invoiceDto.Id, "Test Void");

            // Assert
            // This will FAIL currently because VoidInvoiceAsync only touches the Invoice and BP tables.
            _journalEntryServiceMock.Verify(x => x.ReverseJournalEntryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce, "Voiding must trigger a GL reversal.");
            
            // Verify Tax Audit Trail is marked reversed
            var taxRecords = await _dbContext.Set<TaxCalculation>()
                .Where(t => t.DocumentId == invoiceDto.Id)
                .ToListAsync();
                
            Assert.NotEmpty(taxRecords);
            Assert.All(taxRecords, t => Assert.True(t.IsReversed));
            Assert.All(taxRecords, t => Assert.NotNull(t.ReversedAt));
        }

        [Fact]
        public async Task CreateAsync_ShouldNotGenerateFinalTaxLiability_UntilPosted()
        {
            var bpId = Guid.NewGuid();
            var arAccId = Guid.NewGuid();
            var revAccId = Guid.NewGuid();
            var taxId = Guid.NewGuid();
            
            await SeedDataAsync(bpId, arAccId, revAccId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), taxId);

            var createDto = new InvoiceCreateDto
            {
                BusinessPartnerId = bpId,
                InvoiceDate = new DateTime(2024, 1, 15),
                CurrencyCode = "GHS",
                ExchangeRate = 1m,
                TaxGroupId = _taxGroupId,
                LineItems = new List<InvoiceLineItemCreateDto>
                {
                    new InvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = revAccId,
                        Description = "Test Product",
                        Quantity = 1,
                        UnitPrice = 100m,
                        TaxCode = "VAT10",
                        TaxGroupId = _taxGroupId
                    }
                }
            };

            // Setup TaxEngine
            _taxEngineMock.Setup(x => x.CalculateTaxesAsync(It.IsAny<TaxCalculationRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TaxCalculationResultDto 
                { 
                    TotalTaxAmount = 10m,
                    TaxBreakdowns = new List<TaxBreakdownDto> 
                    { 
                        new TaxBreakdownDto { TaxId = taxId, TaxableAmount = 100m, TaxRate = 0.1m, TaxAmount = 10m } 
                    }
                });

            var invoiceDto = await _sut.CreateAsync(createDto);

            // Assert that no TaxCalculation records were created yet (only generated in SendInvoiceAsync)
            var taxRecords = await _dbContext.Set<TaxCalculation>().Where(t => t.DocumentId == invoiceDto.Id).ToListAsync();
            Assert.Empty(taxRecords);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Dispose();
        }
    }
}
