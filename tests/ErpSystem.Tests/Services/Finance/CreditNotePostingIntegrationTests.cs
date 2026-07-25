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
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Tests.Services.Finance
{
    public class CreditNotePostingIntegrationTests : IDisposable
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
        private readonly Guid _tenantId = Guid.NewGuid();

        // System Under Test
        private readonly CreditNotePostingService _sut;
        
        // Mocks that we might want to verify
        private readonly Mock<IJournalEntryService> _journalEntryServiceMock;
        private readonly Mock<ITaxAuditService> _taxAuditServiceMock;
        
        public CreditNotePostingIntegrationTests()
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
            _taxAuditServiceMock = new Mock<ITaxAuditService>();

            var postingLoggerMock = new Mock<ILogger<SubledgerPostingService>>();
            var postingService = new SubledgerPostingService(
                repository,
                currentUserMock.Object,
                tenantSettingsMock.Object,
                _journalEntryServiceMock.Object,
                bookValidationMock.Object,
                postingLoggerMock.Object);

            var returnOrderMock = new Mock<IReturnOrderService>();
            returnOrderMock.Setup(x => x.GetCreditNoteByIdAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new ErpSystem.Core.DTOs.Sales.CreditNoteDetailDto { Id = Guid.NewGuid() });

            var loggerMock = new Mock<ILogger<CreditNotePostingService>>();

            _sut = new CreditNotePostingService(
                unitOfWork,
                postingService,
                _taxAuditServiceMock.Object,
                currentUserMock.Object,
                loggerMock.Object);
        }

        private async Task SeedDataAsync(
            Guid bpId,
            Guid arAccountId,
            Guid revenueAccountId,
            Guid taxAccountId,
            Guid suspenseAccountId)
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

            var settings = new FinanceSettings
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                ControlAccountArId = arAccountId,
                ControlAccountTaxId = taxAccountId,
                SuspenseAccountId = suspenseAccountId,
                SubledgerPostingMode = "Direct"
            };
            _dbContext.FinanceSettings.Add(settings);

            var arAcc = new Account { Id = arAccountId, TenantId = _tenantId, AccountCode = "1200", AccountName = "AR", CurrencyCode = "GHS" };
            var revAcc = new Account { Id = revenueAccountId, TenantId = _tenantId, AccountCode = "4000", AccountName = "Revenue", CurrencyCode = "GHS" };
            var taxAcc = new Account { Id = taxAccountId, TenantId = _tenantId, AccountCode = "2200", AccountName = "VAT", CurrencyCode = "GHS" };
            var suspAcc = new Account { Id = suspenseAccountId, TenantId = _tenantId, AccountCode = "9999", AccountName = "Suspense", CurrencyCode = "GHS" };
            _dbContext.Accounts.AddRange(arAcc, revAcc, taxAcc, suspAcc);

            var bp = new ErpSystem.Core.Entities.Procurement.BusinessPartner
            {
                Id = bpId,
                TenantId = _tenantId,
                PartnerCode = "C001",
                PartnerName = "Test Customer",
                PartnerType = "Customer",
                Currency = "GHS",
                DefaultArAccountId = arAccountId,
                OutstandingBalance = 1000m,
                CreditLimit = 100000m,
                IsActive = true
            };
            _dbContext.BusinessPartners.Add(bp);

            await _dbContext.SaveChangesAsync();
        }

        private async Task<Invoice> CreateTestInvoiceAsync(Guid bpId, decimal totalAmount)
        {
            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                BusinessPartnerId = bpId,
                InvoiceNumber = $"INV-{DateTime.UtcNow.Ticks}",
                InvoiceDate = DateTime.UtcNow.Date,
                DueDate = DateTime.UtcNow.Date.AddDays(30),
                CurrencyCode = "GHS",
                ExchangeRate = 1m,
                TotalAmount = totalAmount,
                PaidAmount = 0m,
                CreditedAmount = 0m,
                Status = InvoiceStatus.Sent
            };
            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync();
            return invoice;
        }

        private async Task<CreditNote> CreateTestCreditNoteAsync(Guid bpId, decimal totalAmount)
        {
            var cn = new CreditNote
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                BusinessPartnerId = bpId,
                DocumentNumber = $"CN-{DateTime.UtcNow.Ticks}",
                DocumentDate = DateTime.UtcNow.Date,
                Currency = "GHS",
                ExchangeRate = 1m,
                TotalAmount = totalAmount,
                CreditNoteStatus = CreditNoteStatus.Approved,
                Lines = new List<CreditNoteLine>
                {
                    new CreditNoteLine
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantId,
                        Description = "Test Line",
                        Quantity = 1,
                        UnitPrice = totalAmount,
                        TaxAmount = 0
                    }
                }
            };
            _dbContext.CreditNotes.Add(cn);
            await _dbContext.SaveChangesAsync();
            return cn;
        }

        [Fact]
        public async Task ApplyCreditNote_ReducesInvoiceCreditedAmount_And_Balance()
        {
            // T1 & T13
            var bpId = Guid.NewGuid();
            var arId = Guid.NewGuid();
            await SeedDataAsync(bpId, arId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            var invoice = await CreateTestInvoiceAsync(bpId, 500m);
            var cn = await CreateTestCreditNoteAsync(bpId, 200m);

            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            // Act
            await _sut.PostCreditNoteAsync(cn.Id, invoice.Id);

            // Assert
            var freshInvoice = await _dbContext.Invoices.FindAsync(invoice.Id);
            Assert.Equal(200m, freshInvoice.CreditedAmount);
            Assert.Equal(300m, freshInvoice.BalanceAmount); // 500 - 200
            
            var freshCn = await _dbContext.CreditNotes.FindAsync(cn.Id);
            Assert.Equal(CreditNoteStatus.Applied, freshCn.CreditNoteStatus);
            Assert.Equal(200m, freshCn.AppliedAmount);
            Assert.Equal(0m, freshCn.UnappliedAmount);
        }

        [Fact]
        public async Task ApplyCreditNote_ReducesCustomerOutstandingBalance()
        {
            // T2
            var bpId = Guid.NewGuid();
            var arId = Guid.NewGuid();
            await SeedDataAsync(bpId, arId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            var invoice = await CreateTestInvoiceAsync(bpId, 500m);
            var cn = await CreateTestCreditNoteAsync(bpId, 200m);

            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            // Act
            await _sut.PostCreditNoteAsync(cn.Id, invoice.Id);

            // Assert
            var freshBp = await _dbContext.BusinessPartners.FindAsync(bpId);
            // SeedData set it to 1000m. Invoice wasn't added to BP balance in setup, but we applied 200m credit.
            // So BP balance should be 1000 - 200 = 800m
            Assert.Equal(800m, freshBp.OutstandingBalance);
        }

        [Fact]
        public async Task ApplyCreditNote_PostsReversingGLJournal()
        {
            // T3
            var bpId = Guid.NewGuid();
            var arId = Guid.NewGuid();
            var revId = Guid.NewGuid();
            var taxId = Guid.NewGuid();
            var suspenseId = Guid.NewGuid();
            await SeedDataAsync(bpId, arId, revId, taxId, suspenseId);

            var invoice = await CreateTestInvoiceAsync(bpId, 500m);
            var cn = await CreateTestCreditNoteAsync(bpId, 200m);

            CreateJournalEntryDto capturedJe = null;
            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .Callback<CreateJournalEntryDto, CancellationToken>((je, ct) => capturedJe = je)
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            // Act
            await _sut.PostCreditNoteAsync(cn.Id, invoice.Id);

            // Assert
            Assert.NotNull(capturedJe);
            Assert.Equal("AR", capturedJe.SourceModule);
            
            // Expected: Dr Suspense (because we didn't link original invoice line mapping, falls back to suspense) for Revenue, Cr AR
            // Wait, the credit note line has no GLAccountId and no OriginalInvoice. So it uses Suspense.
            var drTrans = capturedJe.Transactions.Single(t => t.TransactionType == "Debit");
            var crTrans = capturedJe.Transactions.Single(t => t.TransactionType == "Credit");

            Assert.Equal(suspenseId, drTrans.AccountId);
            Assert.Equal(200m, drTrans.Amount);
            
            Assert.Equal(arId, crTrans.AccountId);
            Assert.Equal(200m, crTrans.Amount);
        }

        [Fact]
        public async Task ApplyCreditNote_ReversesOutputVat_ViaTaxAudit()
        {
            // T4
            var bpId = Guid.NewGuid();
            var arId = Guid.NewGuid();
            var taxId = Guid.NewGuid();
            await SeedDataAsync(bpId, arId, Guid.NewGuid(), taxId, Guid.NewGuid());

            var invoice = await CreateTestInvoiceAsync(bpId, 500m);
            var cn = await CreateTestCreditNoteAsync(bpId, 220m); // 200 + 20 tax
            var cnLine = cn.Lines.First();
            cnLine.UnitPrice = 200m;
            cnLine.TaxAmount = 20m;
            cnLine.TaxCode = "VAT10";
            await _dbContext.SaveChangesAsync();

            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            // Act
            await _sut.PostCreditNoteAsync(cn.Id, invoice.Id);

            // Assert
            _taxAuditServiceMock.Verify(x => x.RecordTaxCalculationsAsync("CreditNote", cn.Id, It.IsAny<ErpSystem.Core.DTOs.Finance.TaxCalculationResultDto>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ApplyCreditNote_PartialApplication()
        {
            // T5
            var bpId = Guid.NewGuid();
            var arId = Guid.NewGuid();
            await SeedDataAsync(bpId, arId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            var invoice = await CreateTestInvoiceAsync(bpId, 500m);
            var cn = await CreateTestCreditNoteAsync(bpId, 200m);

            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            // Act
            await _sut.PostCreditNoteAsync(cn.Id, invoice.Id);

            // Assert
            var freshInvoice = await _dbContext.Invoices.FindAsync(invoice.Id);
            Assert.Equal(200m, freshInvoice.CreditedAmount);
            Assert.Equal(300m, freshInvoice.BalanceAmount); // Still 300 remaining
        }

        [Fact]
        public async Task ApplyCreditNote_ExcessCredit_CreatesUnappliedCustomerCredit()
        {
            // T6 & T7
            var bpId = Guid.NewGuid();
            var arId = Guid.NewGuid();
            await SeedDataAsync(bpId, arId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            var invoice = await CreateTestInvoiceAsync(bpId, 100m);
            var cn = await CreateTestCreditNoteAsync(bpId, 250m);

            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            // Act
            await _sut.PostCreditNoteAsync(cn.Id, invoice.Id);

            // Assert
            var freshInvoice = await _dbContext.Invoices.FindAsync(invoice.Id);
            Assert.Equal(100m, freshInvoice.CreditedAmount);
            Assert.Equal(0m, freshInvoice.BalanceAmount); 
            
            var freshCn = await _dbContext.CreditNotes.FindAsync(cn.Id);
            Assert.Equal(CreditNoteStatus.Applied, freshCn.CreditNoteStatus); // Status is Applied; UnappliedAmount tracks excess
            Assert.Equal(100m, freshCn.AppliedAmount);
            Assert.Equal(150m, freshCn.UnappliedAmount);
        }

        [Fact]
        public async Task VoidCreditNote_ReversesAllFinancialEffects()
        {
            // T9
            var bpId = Guid.NewGuid();
            var arId = Guid.NewGuid();
            await SeedDataAsync(bpId, arId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            var invoice = await CreateTestInvoiceAsync(bpId, 500m);
            var cn = await CreateTestCreditNoteAsync(bpId, 200m);

            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });
                
            var dummyEntry = new JournalEntryDto { Id = Guid.NewGuid(), PostingStatus = "Posted" };
            _journalEntryServiceMock.Setup(x => x.GetJournalEntriesBySourceAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JournalEntryDto> { dummyEntry });

            await _sut.PostCreditNoteAsync(cn.Id, invoice.Id);

            // Act
            await _sut.VoidCreditNoteAsync(cn.Id, "Voiding test");

            // Assert
            var freshInvoice = await _dbContext.Invoices.FindAsync(invoice.Id);
            Assert.Equal(0m, freshInvoice.CreditedAmount); // Restored!
            Assert.Equal(500m, freshInvoice.BalanceAmount); // Restored!

            var freshCn = await _dbContext.CreditNotes.FindAsync(cn.Id);
            Assert.Equal(CreditNoteStatus.Voided, freshCn.CreditNoteStatus);

            _journalEntryServiceMock.Verify(x => x.ReverseJournalEntryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ApplyCreditNote_MultiCurrency_UsesOriginalInvoiceRate()
        {
            // T8
            var bpId = Guid.NewGuid();
            var arId = Guid.NewGuid();
            await SeedDataAsync(bpId, arId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                BusinessPartnerId = bpId,
                InvoiceNumber = $"INV-{DateTime.UtcNow.Ticks}",
                InvoiceDate = DateTime.UtcNow.Date,
                DueDate = DateTime.UtcNow.Date.AddDays(30),
                CurrencyCode = "USD",
                ExchangeRate = 12.5m, // Historic rate
                TotalAmount = 100m,
                PaidAmount = 0m,
                CreditedAmount = 0m,
                Status = InvoiceStatus.Sent
            };
            _dbContext.Invoices.Add(invoice);

            var cn = new CreditNote
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                BusinessPartnerId = bpId,
                DocumentNumber = $"CN-{DateTime.UtcNow.Ticks}",
                DocumentDate = DateTime.UtcNow.Date,
                Currency = "USD",
                ExchangeRate = 14.0m, // Current rate, but should use 12.5m for applying to this invoice
                TotalAmount = 50m,
                CreditNoteStatus = CreditNoteStatus.Approved,
                Lines = new List<CreditNoteLine>
                {
                    new CreditNoteLine { Id = Guid.NewGuid(), TenantId = _tenantId, Description = "Line", Quantity = 1, UnitPrice = 50m }
                }
            };
            _dbContext.CreditNotes.Add(cn);
            await _dbContext.SaveChangesAsync();

            CreateJournalEntryDto capturedJe = null;
            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .Callback<CreateJournalEntryDto, CancellationToken>((je, ct) => capturedJe = je)
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            // Act
            await _sut.PostCreditNoteAsync(cn.Id, invoice.Id);

            // Assert
            var app = await _dbContext.Set<CreditNoteApplication>().SingleAsync(a => a.CreditNoteId == cn.Id);
            Assert.Equal(50m, app.AppliedForeignAmount); // 50 USD
            Assert.Equal(625m, app.AppliedBaseAmount); // 50 * 12.5 (historic rate) = 625, NOT 50 * 14 = 700

            Assert.NotNull(capturedJe);
            var arTrans = capturedJe.Transactions.Single(t => t.TransactionType == "Credit" && t.AccountId == arId);
            Assert.Equal(625m, arTrans.Amount);
            Assert.Equal(50m, arTrans.ForeignAmount);
        }

        [Fact]
        public async Task T14_CreditNote_TaxAudit_RecordsNegativeAmounts()
        {
            // Arrange — Credit Note with known tax amount
            var bpId = Guid.NewGuid();
            var arId = Guid.NewGuid();
            var revenueId = Guid.NewGuid();
            var taxId = Guid.NewGuid();
            var suspenseId = Guid.NewGuid();
            await SeedDataAsync(bpId, arId, revenueId, taxId, suspenseId);

            var invoice = await CreateTestInvoiceAsync(bpId, 1000m);

            var cn = new CreditNote
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                BusinessPartnerId = bpId,
                DocumentNumber = $"CN-TAX-{DateTime.UtcNow.Ticks}",
                DocumentDate = DateTime.UtcNow.Date,
                Currency = "GHS",
                ExchangeRate = 1m,
                TotalAmount = 230m,  // 200 base + 30 VAT
                TaxAmount = 30m,
                CreditNoteStatus = CreditNoteStatus.Approved,
                Lines = new List<CreditNoteLine>
                {
                    new CreditNoteLine
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantId,
                        Description = "Tax Test Line",
                        Quantity = 1,
                        UnitPrice = 200m,
                        TaxAmount = 30m,
                        TaxCode = "VAT15"
                    }
                }
            };
            _dbContext.CreditNotes.Add(cn);
            await _dbContext.SaveChangesAsync();

            // Capture the TaxCalculationResultDto passed to RecordTaxCalculationsAsync
            Core.DTOs.Finance.TaxCalculationResultDto? capturedTaxResult = null;
            _taxAuditServiceMock
                .Setup(x => x.RecordTaxCalculationsAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Core.DTOs.Finance.TaxCalculationResultDto>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, Guid, Core.DTOs.Finance.TaxCalculationResultDto, CancellationToken>(
                    (docType, docId, result, ct) => capturedTaxResult = result)
                .Returns(Task.CompletedTask);

            _journalEntryServiceMock
                .Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            // Act
            await _sut.PostCreditNoteAsync(cn.Id, invoice.Id);

            // Assert — Tax audit records must have NEGATIVE amounts for credit notes
            _taxAuditServiceMock.Verify(x => x.RecordTaxCalculationsAsync(
                "CreditNote", cn.Id, It.IsAny<Core.DTOs.Finance.TaxCalculationResultDto>(), It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.NotNull(capturedTaxResult);

            // BaseAmount should be -(TotalAmount - TaxAmount) = -(230 - 30) = -200
            Assert.Equal(-200m, capturedTaxResult.BaseAmount);

            // GrandTotal should be -TotalAmount = -230
            Assert.Equal(-230m, capturedTaxResult.GrandTotal);

            // TotalTaxAmount should be -TaxAmount = -30
            Assert.Equal(-30m, capturedTaxResult.TotalTaxAmount);
        }

        [Fact]
        public async Task T20_ApplyUnappliedCredit_ToDifferentInvoice_NoNewGL()
        {
            // Arrange — Post a credit note WITHOUT a target invoice to create fully unapplied credit
            var bpId = Guid.NewGuid();
            var arId = Guid.NewGuid();
            await SeedDataAsync(bpId, arId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            // Credit note with no target invoice → 100% unapplied
            var cn = await CreateTestCreditNoteAsync(bpId, 300m);

            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            // Post credit note with no invoice → all 300 is unapplied
            await _sut.PostCreditNoteAsync(cn.Id, null);

            // Snapshot counts BEFORE reapplication
            var journalCallsBefore = _journalEntryServiceMock.Invocations.Count(i => i.Method.Name == "CreateJournalEntryAsync");
            var taxCallsBefore = _taxAuditServiceMock.Invocations.Count(i => i.Method.Name == "RecordTaxCalculationsAsync");

            var freshCnBefore = await _dbContext.CreditNotes.FindAsync(cn.Id);
            Assert.Equal(0m, freshCnBefore!.AppliedAmount);
            Assert.Equal(300m, freshCnBefore.UnappliedAmount);

            var freshBpBefore = await _dbContext.BusinessPartners.FindAsync(bpId);
            var bpBalanceBefore = freshBpBefore!.OutstandingBalance;

            // Create a target invoice to apply credit against
            var invoice = await CreateTestInvoiceAsync(bpId, 500m);

            // Act — Apply 200 of the 300 unapplied credit to the invoice
            await _sut.ApplyUnappliedCreditAsync(cn.Id, invoice.Id, 200m);

            // Assert — CreditNoteApplication created
            var applications = _dbContext.Set<CreditNoteApplication>()
                .Where(a => a.CreditNoteId == cn.Id && a.InvoiceId == invoice.Id).ToList();
            Assert.Single(applications);
            Assert.Equal(200m, applications[0].AppliedForeignAmount);
            Assert.False(applications[0].IsReversed);

            // Assert — CreditNote.AppliedAmount increased, UnappliedAmount decreased
            var freshCnAfter = await _dbContext.CreditNotes.FindAsync(cn.Id);
            Assert.Equal(200m, freshCnAfter!.AppliedAmount);
            Assert.Equal(100m, freshCnAfter.UnappliedAmount); // 300 - 200

            // Assert — Invoice.CreditedAmount increased, BalanceAmount decreased
            var freshInvoice = await _dbContext.Invoices.FindAsync(invoice.Id);
            Assert.Equal(200m, freshInvoice!.CreditedAmount);
            Assert.Equal(300m, freshInvoice.BalanceAmount); // 500 - 200

            // Assert — BusinessPartner.OutstandingBalance is UNCHANGED (no double-counting)
            var freshBpAfter = await _dbContext.BusinessPartners.FindAsync(bpId);
            Assert.Equal(bpBalanceBefore, freshBpAfter!.OutstandingBalance);

            // Assert — No new GL journal was created
            var journalCallsAfter = _journalEntryServiceMock.Invocations.Count(i => i.Method.Name == "CreateJournalEntryAsync");
            Assert.Equal(journalCallsBefore, journalCallsAfter);

            // Assert — No new tax audit was recorded
            var taxCallsAfter = _taxAuditServiceMock.Invocations.Count(i => i.Method.Name == "RecordTaxCalculationsAsync");
            Assert.Equal(taxCallsBefore, taxCallsAfter);
        }

        [Fact]
        public async Task T21_CreditNoteAgainstFullyPaidInvoice_StoresFullUnappliedAmount()
        {
            // Arrange — Create a fully paid invoice (BalanceAmount = 0)
            var bpId = Guid.NewGuid();
            var arId = Guid.NewGuid();
            await SeedDataAsync(bpId, arId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            var invoice = await CreateTestInvoiceAsync(bpId, 500m);
            invoice.PaidAmount = 500m; // Fully paid
            invoice.Status = InvoiceStatus.Paid;
            await _dbContext.SaveChangesAsync();

            var cn = await CreateTestCreditNoteAsync(bpId, 150m);

            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            // Act — Post credit note against the fully paid invoice
            await _sut.PostCreditNoteAsync(cn.Id, invoice.Id);

            // Assert — No amount was applied to the invoice (it's already settled)
            var freshCn = await _dbContext.CreditNotes.FindAsync(cn.Id);
            Assert.Equal(0m, freshCn!.AppliedAmount); // Nothing applied because invoice was fully paid
            Assert.Equal(150m, freshCn.UnappliedAmount); // Full credit note amount is unapplied

            // Invoice balance should remain 0 (it was already paid in full, credit didn't create negative)
            var freshInvoice = await _dbContext.Invoices.FindAsync(invoice.Id);
            Assert.Equal(0m, freshInvoice!.BalanceAmount);
        }

        [Fact]
        public async Task T22_CustomerNetBalance_CorrectAfterInvoiceCreditRefund_ReapplicationDoesNotDoubleCount()
        {
            // This test proves: Customer Net AR = Invoices - PostedCreditNotes + Refunds
            // AND that ApplyUnappliedCreditAsync does NOT change OutstandingBalance again.
            var bpId = Guid.NewGuid();
            var arId = Guid.NewGuid();
            await SeedDataAsync(bpId, arId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            // ── Step 1: Create Invoice (+AR) ──
            // BP starts at 1000. Invoice TotalAmount = 800.
            var invoice1 = await CreateTestInvoiceAsync(bpId, 800m);
            var invoice2 = await CreateTestInvoiceAsync(bpId, 400m);
            // BP.OutstandingBalance hasn't been increased by invoices in the seed (invoice posting is separate).
            // For this test, we manually set BP balance to reflect posted invoices.
            var bp = await _dbContext.BusinessPartners.FindAsync(bpId);
            bp!.OutstandingBalance = 1200m; // 800 + 400 invoiced
            await _dbContext.SaveChangesAsync();

            // ── Step 2: Post Credit Note of 500 (no target invoice → fully unapplied) ──
            var cn = await CreateTestCreditNoteAsync(bpId, 500m);

            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            await _sut.PostCreditNoteAsync(cn.Id, null);

            // After credit note posting: 1200 - 500 = 700
            bp = await _dbContext.BusinessPartners.FindAsync(bpId);
            Assert.Equal(700m, bp!.OutstandingBalance);
            var balanceAfterCreditNote = bp.OutstandingBalance;

            // ── Step 3: Apply 300 of the 500 unapplied credit to Invoice1 ──
            await _sut.ApplyUnappliedCreditAsync(cn.Id, invoice1.Id, 300m);

            // Assert — BP balance must NOT change from reapplication
            bp = await _dbContext.BusinessPartners.FindAsync(bpId);
            Assert.Equal(balanceAfterCreditNote, bp!.OutstandingBalance); // Still 700

            // Invoice1 now has 300 credited
            var freshInv1 = await _dbContext.Invoices.FindAsync(invoice1.Id);
            Assert.Equal(300m, freshInv1!.CreditedAmount);
            Assert.Equal(500m, freshInv1.BalanceAmount); // 800 - 300

            // ── Step 4: Apply remaining 200 unapplied credit to Invoice2 ──
            await _sut.ApplyUnappliedCreditAsync(cn.Id, invoice2.Id, 200m);

            // Assert — BP balance still unchanged
            bp = await _dbContext.BusinessPartners.FindAsync(bpId);
            Assert.Equal(balanceAfterCreditNote, bp!.OutstandingBalance); // Still 700

            // Invoice2 now has 200 credited
            var freshInv2 = await _dbContext.Invoices.FindAsync(invoice2.Id);
            Assert.Equal(200m, freshInv2!.CreditedAmount);
            Assert.Equal(200m, freshInv2.BalanceAmount); // 400 - 200

            // Credit note fully allocated
            var freshCn = await _dbContext.CreditNotes.FindAsync(cn.Id);
            Assert.Equal(500m, freshCn!.AppliedAmount);
            Assert.Equal(0m, freshCn.UnappliedAmount);

            // ── Final formula verification ──
            // Customer Net AR = Invoices(1200) - PostedCreditNotes(500) = 700
            // The 500 was already subtracted at posting. Reapplications moved credit between invoices
            // but did NOT subtract from BP balance again.
            Assert.Equal(1200m - 500m, bp!.OutstandingBalance);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Dispose();
        }
    }
}
