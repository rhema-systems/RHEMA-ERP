using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Api.Services.Finance.AR;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Api.Services.Finance.Taxation;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
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
    public class TransactionTaxIntegrationTests : IDisposable
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
        private readonly Guid _tenantId = Guid.NewGuid();

        // Services
        private readonly VendorInvoiceService _vendorInvoiceService;
        private readonly InvoiceService _customerInvoiceService;
        private readonly FinancePurchaseOrderService _financePOService;
        private readonly FinancePurchaseOrderReceiptService _grvService;
        private readonly SubledgerPostingService _postingService;
        private readonly TaxCalculationEngine _taxEngine;
        private readonly TaxAuditService _taxAuditService;

        // Mocks
        private readonly Mock<IJournalEntryService> _journalEntryServiceMock;
        private readonly Mock<IInventoryValuationService> _inventoryValuationMock;
        private readonly Mock<IInventoryReceiptService> _inventoryReceiptMock;

        // Seeding GUIDs
        private readonly Guid _vendorId = Guid.NewGuid();
        private readonly Guid _customerId = Guid.NewGuid();
        private readonly Guid _apAccountId = Guid.NewGuid();
        private readonly Guid _arAccountId = Guid.NewGuid();
        private readonly Guid _expenseAccountId = Guid.NewGuid();
        private readonly Guid _revenueAccountId = Guid.NewGuid();
        
        // Tax Accounts
        private readonly Guid _vatReceivableAccountId = Guid.NewGuid();
        private readonly Guid _vatPayableAccountId = Guid.NewGuid();
        private readonly Guid _levyReceivableAccountId = Guid.NewGuid();
        private readonly Guid _levyPayableAccountId = Guid.NewGuid();
        private readonly Guid _whtPayableAccountId = Guid.NewGuid();
        private readonly Guid _genericTaxControlAccountId = Guid.NewGuid();

        // Tax Group GUIDs
        private readonly Guid _standardTaxGroupId = Guid.NewGuid();
        private readonly Guid _specialTaxGroupId = Guid.NewGuid();
        private readonly Guid _whtGroupId = Guid.NewGuid();

        public TransactionTaxIntegrationTests()
        {
            _connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:;Foreign Keys=False;");
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
            currentUserMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());

            var tenantSettingsMock = new Mock<ITenantSettingsService>();
            tenantSettingsMock.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");

            var bookValidationMock = new Mock<IBookValidationService>();
            bookValidationMock.Setup(x => x.ResolveTargetBooksAsync(It.IsAny<string>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "Primary" });
            bookValidationMock.Setup(x => x.ValidateAccountsForBookAsync(It.IsAny<List<Guid>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _journalEntryServiceMock = new Mock<IJournalEntryService>();
            _inventoryValuationMock = new Mock<IInventoryValuationService>();
            _inventoryReceiptMock = new Mock<IInventoryReceiptService>();

            var postingLoggerMock = new Mock<ILogger<SubledgerPostingService>>();
            _postingService = new SubledgerPostingService(
                repository,
                currentUserMock.Object,
                tenantSettingsMock.Object,
                _journalEntryServiceMock.Object,
                bookValidationMock.Object,
                postingLoggerMock.Object);

            var engineLoggerMock = new Mock<ILogger<TaxCalculationEngine>>();
            _taxEngine = new TaxCalculationEngine(
                repository,
                currentUserMock.Object,
                engineLoggerMock.Object);

            var auditLoggerMock = new Mock<ILogger<TaxAuditService>>();
            _taxAuditService = new TaxAuditService(
                repository,
                currentUserMock.Object,
                auditLoggerMock.Object);

            var apLoggerMock = new Mock<ILogger<VendorInvoiceService>>();
            _vendorInvoiceService = new VendorInvoiceService(
                unitOfWork,
                currentUserMock.Object,
                _postingService,
                _inventoryValuationMock.Object,
                _taxEngine,
                _taxAuditService,
                tenantSettingsMock.Object,
                apLoggerMock.Object);

            var arLoggerMock = new Mock<ILogger<InvoiceService>>();
            _customerInvoiceService = new InvoiceService(
                unitOfWork,
                currentUserMock.Object,
                _taxEngine,
                _taxAuditService,
                _postingService,
                _inventoryValuationMock.Object,
                tenantSettingsMock.Object,
                arLoggerMock.Object);

            _financePOService = new FinancePurchaseOrderService(
                _dbContext,
                _taxEngine,
                tenantSettingsMock.Object,
                currentUserMock.Object);

            _grvService = new FinancePurchaseOrderReceiptService(
                _dbContext,
                _inventoryReceiptMock.Object,
                _taxEngine,
                tenantSettingsMock.Object);

            SeedStaticData();
        }

        private void SeedStaticData()
        {
            var tenant = new Tenant { Id = _tenantId, Name = "Test Tenant", Code = "TT01" };
            _dbContext.Tenants.Add(tenant);

            // Accounts
            _dbContext.Accounts.AddRange(
                new Account { Id = _apAccountId, TenantId = _tenantId, AccountCode = "2100", AccountName = "AP Control", CurrencyCode = "GHS" },
                new Account { Id = _arAccountId, TenantId = _tenantId, AccountCode = "1200", AccountName = "AR Control", CurrencyCode = "GHS" },
                new Account { Id = _expenseAccountId, TenantId = _tenantId, AccountCode = "5100", AccountName = "General Expense", CurrencyCode = "GHS" },
                new Account { Id = _revenueAccountId, TenantId = _tenantId, AccountCode = "4100", AccountName = "Product Sales", CurrencyCode = "GHS" },
                new Account { Id = _vatReceivableAccountId, TenantId = _tenantId, AccountCode = "1310", AccountName = "VAT Input Receivable", CurrencyCode = "GHS" },
                new Account { Id = _vatPayableAccountId, TenantId = _tenantId, AccountCode = "2310", AccountName = "VAT Output Payable", CurrencyCode = "GHS" },
                new Account { Id = _levyReceivableAccountId, TenantId = _tenantId, AccountCode = "1320", AccountName = "Levy Input Receivable", CurrencyCode = "GHS" },
                new Account { Id = _levyPayableAccountId, TenantId = _tenantId, AccountCode = "2320", AccountName = "Levy Output Payable", CurrencyCode = "GHS" },
                new Account { Id = _whtPayableAccountId, TenantId = _tenantId, AccountCode = "2330", AccountName = "WHT Payable", CurrencyCode = "GHS" },
                new Account { Id = _genericTaxControlAccountId, TenantId = _tenantId, AccountCode = "2390", AccountName = "Tax Control Fallback", CurrencyCode = "GHS" }
            );

            // Finance Settings
            _dbContext.FinanceSettings.Add(new FinanceSettings
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                ControlAccountApId = _apAccountId,
                ControlAccountArId = _arAccountId,
                ControlAccountTaxId = _genericTaxControlAccountId,
                SubledgerPostingMode = "Direct"
            });

            // Business Partners
            _dbContext.BusinessPartners.AddRange(
                new BusinessPartner
                {
                    Id = _vendorId,
                    TenantId = _tenantId,
                    PartnerCode = "V001",
                    PartnerName = "Supplier Corp",
                    PartnerType = "Supplier",
                    Currency = "GHS",
                    DefaultApAccountId = _apAccountId,
                    IsActive = true
                },
                new BusinessPartner
                {
                    Id = _customerId,
                    TenantId = _tenantId,
                    PartnerCode = "C001",
                    PartnerName = "Customer LLC",
                    PartnerType = "Customer",
                    Currency = "GHS",
                    DefaultArAccountId = _arAccountId,
                    IsActive = true
                }
            );

            // Taxes
            var vatTax = new Tax
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Code = "VAT15",
                Name = "VAT 15%",
                Rate = 15m,
                Category = TaxCategory.Standard,
                Applicability = TaxApplicability.Both,
                TaxReceivableAccountId = _vatReceivableAccountId,
                TaxPayableAccountId = _vatPayableAccountId,
                IsActive = true
            };

            var nhilTax = new Tax
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Code = "NHIL25",
                Name = "NHIL 2.5%",
                Rate = 2.5m,
                Category = TaxCategory.Levy,
                Applicability = TaxApplicability.Both,
                TaxReceivableAccountId = _levyReceivableAccountId,
                TaxPayableAccountId = _levyPayableAccountId,
                IsActive = true
            };

            var getflTax = new Tax
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Code = "GETFL25",
                Name = "GETFUND Levy 2.5%",
                Rate = 2.5m,
                Category = TaxCategory.Levy,
                Applicability = TaxApplicability.Both,
                TaxReceivableAccountId = _levyReceivableAccountId,
                TaxPayableAccountId = _levyPayableAccountId,
                IsActive = true
            };

            var whtTax = new Tax
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Code = "WHT75",
                Name = "Withholding Tax 7.5%",
                Rate = 7.5m,
                Category = TaxCategory.Withholding,
                Applicability = TaxApplicability.Purchases,
                TaxPayableAccountId = _whtPayableAccountId,
                IsActive = true
            };

            var specialTax = new Tax
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Code = "SPEC5",
                Name = "Special Levy 5%",
                Rate = 5m,
                Category = TaxCategory.Levy,
                Applicability = TaxApplicability.Both,
                IsActive = true // Generic fallback tax account (no custom accounts specified)
            };

            _dbContext.Taxes.AddRange(vatTax, nhilTax, getflTax, whtTax, specialTax);

            // Seed Tax Groups
            var standardGroup = new TaxGroup
            {
                Id = _standardTaxGroupId,
                TenantId = _tenantId,
                Code = "GH_VAT_LEVIES",
                Name = "Ghana VAT + Levies",
                Applicability = TaxApplicability.Both,
                IsActive = true
            };
            standardGroup.Components = new List<TaxGroupComponent>
            {
                new TaxGroupComponent { Id = Guid.NewGuid(), TenantId = _tenantId, TaxGroupId = _standardTaxGroupId, TaxId = nhilTax.Id, Tax = nhilTax, CalculationOrder = 1, CompoundBasis = CompoundBasis.BaseOnly },
                new TaxGroupComponent { Id = Guid.NewGuid(), TenantId = _tenantId, TaxGroupId = _standardTaxGroupId, TaxId = getflTax.Id, Tax = getflTax, CalculationOrder = 2, CompoundBasis = CompoundBasis.BaseOnly },
                new TaxGroupComponent { Id = Guid.NewGuid(), TenantId = _tenantId, TaxGroupId = _standardTaxGroupId, TaxId = vatTax.Id, Tax = vatTax, CalculationOrder = 3, CompoundBasis = CompoundBasis.Cumulative } // VAT on base + levies
            };

            var specialGroup = new TaxGroup
            {
                Id = _specialTaxGroupId,
                TenantId = _tenantId,
                Code = "GH_SPECIAL",
                Name = "Ghana Special Levy",
                Applicability = TaxApplicability.Both,
                IsActive = true
            };
            specialGroup.Components = new List<TaxGroupComponent>
            {
                new TaxGroupComponent { Id = Guid.NewGuid(), TenantId = _tenantId, TaxGroupId = _specialTaxGroupId, TaxId = specialTax.Id, Tax = specialTax, CalculationOrder = 1, CompoundBasis = CompoundBasis.BaseOnly }
            };

            var whtGroup = new TaxGroup
            {
                Id = _whtGroupId,
                TenantId = _tenantId,
                Code = "GH_WHT",
                Name = "Ghana WHT Group",
                Applicability = TaxApplicability.Purchases,
                IsActive = true
            };
            whtGroup.Components = new List<TaxGroupComponent>
            {
                new TaxGroupComponent { Id = Guid.NewGuid(), TenantId = _tenantId, TaxGroupId = _whtGroupId, TaxId = whtTax.Id, Tax = whtTax, CalculationOrder = 1, CompoundBasis = CompoundBasis.BaseOnly }
            };

            _dbContext.TaxGroups.AddRange(standardGroup, specialGroup, whtGroup);

            // Seeding Currencies
            _dbContext.Currencies.Add(new Currency { Id = Guid.NewGuid(), TenantId = _tenantId, CurrencyCode = "GHS", CurrencyName = "Ghana Cedi", IsBaseCurrency = true });
            
            // Seeding Fiscal Calendar
            var fy = new FiscalYear { Id = Guid.NewGuid(), TenantId = _tenantId, Year = DateTime.UtcNow.Year, StartDate = new DateTime(DateTime.UtcNow.Year, 1, 1), EndDate = new DateTime(DateTime.UtcNow.Year, 12, 31), IsActive = true };
            var fp = new FiscalPeriod { Id = Guid.NewGuid(), TenantId = _tenantId, FiscalYearId = fy.Id, PeriodNumber = DateTime.UtcNow.Month, StartDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1), EndDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1).AddDays(-1), IsOpen = true };
            _dbContext.FiscalYears.Add(fy);
            _dbContext.FiscalPeriods.Add(fp);

            _dbContext.SaveChanges();
        }

        [Fact]
        public async Task SupplierInvoice_ShouldCalculateInputTax_FromTaxGroup()
        {
            // Arrange
            var createDto = new VendorInvoiceCreateDto
            {
                BusinessPartnerId = _vendorId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                TaxGroupId = _standardTaxGroupId,
                ExpenseAccountId = _expenseAccountId,
                ApAccountId = _apAccountId,
                LineItems = new List<VendorInvoiceLineItemCreateDto>
                {
                    new VendorInvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = _expenseAccountId,
                        Description = "Test Product Line",
                        Quantity = 2,
                        UnitPrice = 1000m // Base 2000
                    }
                }
            };

            // Act
            var invoiceDto = await _vendorInvoiceService.CreateAsync(createDto);

            // Assert
            // NHIL = 2000 * 2.5% = 50
            // GETFL = 2000 * 2.5% = 50
            // VAT (Cumulative basis) = (2000 + 50 + 50) * 15% = 2100 * 15% = 315
            // Total Tax = 50 + 50 + 315 = 415
            // Subtotal = 2000
            // Grand Total = 2415
            Assert.Equal(2000m, invoiceDto.SubTotal);
            Assert.Equal(415m, invoiceDto.TaxAmount);
            Assert.Equal(2415m, invoiceDto.TotalAmount);
            
            var savedInvoice = await _dbContext.VendorInvoices.Include(i => i.LineItems).FirstAsync(i => i.Id == invoiceDto.Id);
            Assert.Equal(_standardTaxGroupId, savedInvoice.TaxGroupId);
            Assert.Equal(415m, savedInvoice.TaxAmount);
            Assert.Equal(415m, savedInvoice.LineItems.First().TaxAmount);
            Assert.Equal("Ghana VAT + Levies", savedInvoice.LineItems.First().TaxCode);
        }

        [Fact]
        public async Task SupplierInvoice_ShouldAllowLineTaxOverride()
        {
            // Arrange
            var createDto = new VendorInvoiceCreateDto
            {
                BusinessPartnerId = _vendorId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                TaxGroupId = _standardTaxGroupId,
                ExpenseAccountId = _expenseAccountId,
                ApAccountId = _apAccountId,
                LineItems = new List<VendorInvoiceLineItemCreateDto>
                {
                    new VendorInvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = _expenseAccountId,
                        Description = "Standard Line",
                        Quantity = 1,
                        UnitPrice = 1000m // Base 1000. Uses standardGroup: NHIL (25) + GETFL (25) + VAT (165) = 215 Tax
                    },
                    new VendorInvoiceLineItemCreateDto
                    {
                        LineItemType = "Service",
                        GLAccountId = _expenseAccountId,
                        Description = "Special Overridden Line",
                        Quantity = 1,
                        UnitPrice = 1000m,
                        TaxGroupId = _specialTaxGroupId // Overrides standardGroup with specialGroup: SPEC5 (5% = 50)
                    }
                }
            };

            // Act
            var invoiceDto = await _vendorInvoiceService.CreateAsync(createDto);

            // Assert
            // Line 1: SubTotal = 1000, Tax = 207.50
            // Line 2: SubTotal = 1000, Tax = 50
            // Total SubTotal = 2000
            // Total Tax = 257.50
            // Grand Total = 2257.50
            Assert.Equal(2000m, invoiceDto.SubTotal);
            Assert.Equal(257.50m, invoiceDto.TaxAmount);
            Assert.Equal(2257.50m, invoiceDto.TotalAmount);

            var savedInvoice = await _dbContext.VendorInvoices.Include(i => i.LineItems).FirstAsync(i => i.Id == invoiceDto.Id);
            Assert.Equal(2, savedInvoice.LineItems.Count);
            
            var line1 = savedInvoice.LineItems.Single(l => l.Description == "Standard Line");
            Assert.Equal(_standardTaxGroupId, line1.TaxGroupId);
            Assert.Equal(207.50m, line1.TaxAmount);

            var line2 = savedInvoice.LineItems.Single(l => l.Description == "Special Overridden Line");
            Assert.Equal(_specialTaxGroupId, line2.TaxGroupId);
            Assert.Equal(50m, line2.TaxAmount);
        }

        [Fact]
        public async Task SupplierInvoice_Post_ShouldPostInputTaxToCorrectGLAccount()
        {
            // Arrange
            var createDto = new VendorInvoiceCreateDto
            {
                BusinessPartnerId = _vendorId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                TaxGroupId = _standardTaxGroupId,
                ExpenseAccountId = _expenseAccountId,
                ApAccountId = _apAccountId,
                WithholdingTaxRate = 5m, // WHT = 5% of subtotal (100)
                LineItems = new List<VendorInvoiceLineItemCreateDto>
                {
                    new VendorInvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = _expenseAccountId,
                        Description = "Test Post Line",
                        Quantity = 2,
                        UnitPrice = 1000m // Subtotal 2000, Tax 415. Total 2415. WHT 100
                    }
                }
            };

            var invoiceDto = await _vendorInvoiceService.CreateAsync(createDto);
            await _vendorInvoiceService.SubmitForApprovalAsync(invoiceDto.Id);

            CreateJournalEntryDto capturedJe = null;
            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .Callback<CreateJournalEntryDto, CancellationToken>((je, ct) => capturedJe = je)
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            // Act
            await _vendorInvoiceService.ApproveAsync(invoiceDto.Id);

            // Assert
            Assert.NotNull(capturedJe);

            // Ledger distributions check
            // Expense Debit = 2000
            // NHIL Receivable Debit = 50
            // GETFL Receivable Debit = 50
            // VAT Receivable Debit = 315
            // WHT Payable Credit = 100 (separate deduction liability)
            // AP Control Credit = TotalAmount - WithholdingTaxAmount = 2415 - 100 = 2315
            
            var expenseLine = capturedJe.Transactions.Single(t => t.AccountId == _expenseAccountId);
            Assert.Equal("Debit", expenseLine.TransactionType);
            Assert.Equal(2000m, expenseLine.Amount);

            var apLine = capturedJe.Transactions.Single(t => t.AccountId == _apAccountId);
            Assert.Equal("Credit", apLine.TransactionType);
            Assert.Equal(2315m, apLine.Amount); // Reduced by WHT (2415 - 100)

            var whtLine = capturedJe.Transactions.Single(t => t.AccountId == _whtPayableAccountId);
            Assert.Equal("Credit", whtLine.TransactionType);
            Assert.Equal(100m, whtLine.Amount);

            var vatLine = capturedJe.Transactions.Single(t => t.AccountId == _vatReceivableAccountId);
            Assert.Equal("Debit", vatLine.TransactionType);
            Assert.Equal(315m, vatLine.Amount);

            var levyLines = capturedJe.Transactions.Where(t => t.AccountId == _levyReceivableAccountId).ToList();
            Assert.Equal(2, levyLines.Count); // GETFL and NHIL
            Assert.All(levyLines, l => Assert.Equal("Debit", l.TransactionType));
            Assert.All(levyLines, l => Assert.Equal(50m, l.Amount));
        }

        [Fact]
        public async Task CustomerInvoice_ShouldCalculateOutputTax_FromTaxGroup()
        {
            // Arrange
            var createDto = new InvoiceCreateDto
            {
                BusinessPartnerId = _customerId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                TaxGroupId = _standardTaxGroupId,
                LineItems = new List<InvoiceLineItemCreateDto>
                {
                    new InvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = _revenueAccountId,
                        Description = "Customer Sale Line",
                        Quantity = 1,
                        UnitPrice = 2000m
                    }
                }
            };

            // Act
            var invoiceDto = await _customerInvoiceService.CreateAsync(createDto);

            // Assert
            // NHIL = 2000 * 2.5% = 50
            // GETFL = 2000 * 2.5% = 50
            // VAT = (2000 + 50 + 50) * 15% = 315
            // Total Tax = 415
            Assert.Equal(2000m, invoiceDto.SubTotal);
            Assert.Equal(415m, invoiceDto.TaxAmount);
            Assert.Equal(2415m, invoiceDto.TotalAmount);
            
            var savedInvoice = await _dbContext.Invoices.Include(i => i.LineItems).FirstAsync(i => i.Id == invoiceDto.Id);
            Assert.Equal(_standardTaxGroupId, savedInvoice.TaxGroupId);
            Assert.Equal(415m, savedInvoice.TaxAmount);
            Assert.Equal(415m, savedInvoice.LineItems.First().TaxAmount);
        }

        [Fact]
        public async Task CustomerInvoice_ShouldAllowLineTaxOverride()
        {
            // Arrange
            var createDto = new InvoiceCreateDto
            {
                BusinessPartnerId = _customerId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                TaxGroupId = _standardTaxGroupId,
                LineItems = new List<InvoiceLineItemCreateDto>
                {
                    new InvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = _revenueAccountId,
                        Description = "Standard Customer Line",
                        Quantity = 1,
                        UnitPrice = 1000m
                    },
                    new InvoiceLineItemCreateDto
                    {
                        LineItemType = "Service",
                        GLAccountId = _revenueAccountId,
                        Description = "Special Overridden Sale Line",
                        Quantity = 1,
                        UnitPrice = 1000m,
                        TaxGroupId = _specialTaxGroupId
                    }
                }
            };

            // Act
            var invoiceDto = await _customerInvoiceService.CreateAsync(createDto);

            // Assert
            Assert.Equal(2000m, invoiceDto.SubTotal);
            Assert.Equal(257.50m, invoiceDto.TaxAmount); // 207.50 + 50
            Assert.Equal(2257.50m, invoiceDto.TotalAmount);
        }

        [Fact]
        public async Task CustomerInvoice_Post_ShouldPostOutputTaxToCorrectGLAccount()
        {
            // Arrange
            var createDto = new InvoiceCreateDto
            {
                BusinessPartnerId = _customerId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                TaxGroupId = _standardTaxGroupId,
                LineItems = new List<InvoiceLineItemCreateDto>
                {
                    new InvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = _revenueAccountId,
                        Description = "Test Post Output",
                        Quantity = 2,
                        UnitPrice = 1000m // 2000 Subtotal, 415 Tax. Total 2415
                    }
                }
            };

            var invoiceDto = await _customerInvoiceService.CreateAsync(createDto);

            CreateJournalEntryDto capturedJe = null;
            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .Callback<CreateJournalEntryDto, CancellationToken>((je, ct) => capturedJe = je)
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            // Act
            await _customerInvoiceService.SendInvoiceAsync(invoiceDto.Id);

            // Assert
            Assert.NotNull(capturedJe);

            // AR Debit = 2415
            // Revenue Credit = 2000
            // VAT Payable Credit = 315
            // NHIL Payable Credit = 50
            // GETFL Payable Credit = 50
            
            var arLine = capturedJe.Transactions.Single(t => t.AccountId == _arAccountId);
            Assert.Equal("Debit", arLine.TransactionType);
            Assert.Equal(2415m, arLine.Amount);

            var revLine = capturedJe.Transactions.Single(t => t.AccountId == _revenueAccountId);
            Assert.Equal("Credit", revLine.TransactionType);
            Assert.Equal(2000m, revLine.Amount);

            var vatLine = capturedJe.Transactions.Single(t => t.AccountId == _vatPayableAccountId);
            Assert.Equal("Credit", vatLine.TransactionType);
            Assert.Equal(315m, vatLine.Amount);

            var levyLines = capturedJe.Transactions.Where(t => t.AccountId == _levyPayableAccountId).ToList();
            Assert.Equal(2, levyLines.Count);
            Assert.All(levyLines, l => Assert.Equal("Credit", l.TransactionType));
            Assert.All(levyLines, l => Assert.Equal(50m, l.Amount));
        }

        [Fact]
        public async Task FinancePO_ShouldShowEstimatedTax()
        {
            // Arrange
            var po = new CreateFinancePurchaseOrderDto
            {
                OrderNumber = "FPO-1001",
                BusinessPartnerId = _vendorId,
                OrderDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                TaxGroupId = _standardTaxGroupId,
                Items = new List<CreateFinancePurchaseOrderItemDto>
                {
                    new CreateFinancePurchaseOrderItemDto
                    {
                        LineType = FinancePurchaseOrderLineType.GLAccount,
                        GlAccountId = _expenseAccountId,
                        Description = "PO Expense Line",
                        OrderedQuantity = 1,
                        UnitPrice = 2000m,
                        TaxGroupId = _standardTaxGroupId // Explicit override
                    }
                }
            };

            // Act
            var createdPo = await _financePOService.CreateAsync(po);

            // Assert
            Assert.Equal(2000m, createdPo.Items.First().LineTotal);
            Assert.Equal(415m, createdPo.Items.First().TaxAmount);
            Assert.Equal(415m, createdPo.TotalAmount - 2000m); // Estimated tax total
        }

        [Fact]
        public async Task FinanceGRV_ToVendorInvoice_ShouldCarryTaxContext()
        {
            // Arrange
            var po = new FinancePurchaseOrder
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                OrderNumber = "FPO-2002",
                VendorId = _vendorId,
                OrderDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                TaxGroupId = _standardTaxGroupId,
                Status = FinancePurchaseOrderStatus.Approved,
                Items = new List<FinancePurchaseOrderItem>
                {
                    new FinancePurchaseOrderItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantId,
                        LineType = FinancePurchaseOrderLineType.GLAccount,
                        GlAccountId = _expenseAccountId,
                        Description = "Item 1",
                        OrderedQuantity = 10,
                        UnitPrice = 100m,
                        TaxGroupId = _specialTaxGroupId, // Override line tax group
                        TaxRate = 5m,
                        TaxAmount = 50m
                    }
                }
            };
            _dbContext.FinancePurchaseOrders.Add(po);

            var receipt = new FinancePurchaseOrderReceipt
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                FinancePurchaseOrderId = po.Id,
                ReceiptNumber = "RC-2002",
                ReceiptDate = DateTime.UtcNow,
                Items = new List<FinancePurchaseOrderReceiptItem>
                {
                    new FinancePurchaseOrderReceiptItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantId,
                        FinancePurchaseOrderItemId = po.Items.First().Id,
                        QuantityReceived = 10,
                        InvoicedQuantity = 0
                    }
                }
            };
            _dbContext.FinancePurchaseOrderReceipts.Add(receipt);
            await _dbContext.SaveChangesAsync();

            // Act
            var vendorInvoice = await _grvService.ConvertToVendorInvoiceAsync(receipt.Id, Guid.NewGuid());

            // Assert
            Assert.NotNull(vendorInvoice);
            Assert.Equal(_standardTaxGroupId, vendorInvoice.TaxGroupId); // Propagated from PO
            Assert.Single(vendorInvoice.LineItems);
            
            var invoiceLine = vendorInvoice.LineItems.First();
            Assert.Equal(_specialTaxGroupId, invoiceLine.TaxGroupId); // Propagated from PO line item
            Assert.Equal(50m, invoiceLine.TaxAmount); // Recalculated by the engine: 10 * 100 * 5% = 50
        }

        [Fact]
        public async Task VendorInvoice_FromGRV_ShouldRecalculateTaxAuthoritatively()
        {
            // Arrange
            // Seed a PO where estimated tax is different or NHIL rate changes in the DB dynamically (to verify authoritative recalculation)
            var po = new FinancePurchaseOrder
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                OrderNumber = "FPO-3003",
                VendorId = _vendorId,
                OrderDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                TaxGroupId = _standardTaxGroupId,
                Status = FinancePurchaseOrderStatus.Approved,
                Items = new List<FinancePurchaseOrderItem>
                {
                    new FinancePurchaseOrderItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantId,
                        LineType = FinancePurchaseOrderLineType.GLAccount,
                        GlAccountId = _expenseAccountId,
                        Description = "Item 2",
                        OrderedQuantity = 5,
                        UnitPrice = 200m,
                        TaxGroupId = _standardTaxGroupId,
                        TaxRate = 10m, // Fake rate saved in PO
                        TaxAmount = 100m
                    }
                }
            };
            _dbContext.FinancePurchaseOrders.Add(po);

            var receipt = new FinancePurchaseOrderReceipt
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                FinancePurchaseOrderId = po.Id,
                ReceiptNumber = "RC-3003",
                ReceiptDate = DateTime.UtcNow,
                Items = new List<FinancePurchaseOrderReceiptItem>
                {
                    new FinancePurchaseOrderReceiptItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantId,
                        FinancePurchaseOrderItemId = po.Items.First().Id,
                        QuantityReceived = 5,
                        InvoicedQuantity = 0
                    }
                }
            };
            _dbContext.FinancePurchaseOrderReceipts.Add(receipt);
            await _dbContext.SaveChangesAsync();

            // Act
            var vendorInvoice = await _grvService.ConvertToVendorInvoiceAsync(receipt.Id, Guid.NewGuid());

            // Assert
            // Base = 5 * 200 = 1000
            // Actual standardGroup tax = NHIL (25) + GETFL (25) + VAT (157.50) = 207.50
            // It MUST recalculate authoritatively using the engine, overriding the PO's saved "TaxRate = 10%" estimate.
            Assert.Equal(207.50m, vendorInvoice.TaxAmount);
            Assert.Equal(20.75m, vendorInvoice.LineItems.First().TaxRate); // Effective tax rate is 20.75%
        }

        [Fact]
        public async Task Backend_ShouldRejectOrCorrectTamperedFrontendTaxAmounts()
        {
            // Arrange
            var tamperedDto = new VendorInvoiceCreateDto
            {
                BusinessPartnerId = _vendorId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                TaxGroupId = _standardTaxGroupId,
                ExpenseAccountId = _expenseAccountId,
                ApAccountId = _apAccountId,
                LineItems = new List<VendorInvoiceLineItemCreateDto>
                {
                    new VendorInvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = _expenseAccountId,
                        Description = "Tampered Line",
                        Quantity = 10,
                        UnitPrice = 100m,
                        TaxRate = 1m, // Tampered rate (should be 21.5% effective)
                        TaxCode = "FREE_TAX"
                    }
                }
            };

            // Act
            var invoiceDto = await _vendorInvoiceService.CreateAsync(tamperedDto);

            // Assert
            // The service recalculates taxes authoritatively and completely overwrites the tampered values
            Assert.Equal(207.50m, invoiceDto.TaxAmount);
            Assert.Equal(1207.50m, invoiceDto.TotalAmount);
            
            var savedInvoice = await _dbContext.VendorInvoices.Include(i => i.LineItems).FirstAsync(i => i.Id == invoiceDto.Id);
            var savedLine = savedInvoice.LineItems.First();
            Assert.Equal(20.75m, savedLine.TaxRate);
            Assert.Equal(207.50m, savedLine.TaxAmount);
            Assert.Equal("Ghana VAT + Levies", savedLine.TaxCode);
        }

        [Fact]
        public async Task ForeignCurrencyVendorInvoice_ShouldRequireExchangeRate()
        {
            var createDto = new VendorInvoiceCreateDto
            {
                BusinessPartnerId = _vendorId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "USD",
                ExchangeRate = 0.0m, // Invalid rate
                TaxGroupId = _standardTaxGroupId,
                ExpenseAccountId = _expenseAccountId,
                ApAccountId = _apAccountId,
                LineItems = new List<VendorInvoiceLineItemCreateDto>
                {
                    new VendorInvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = _expenseAccountId,
                        Description = "Test Line",
                        Quantity = 1,
                        UnitPrice = 100m
                    }
                }
            };

            await Assert.ThrowsAsync<ArgumentException>(() => _vendorInvoiceService.CreateAsync(createDto));
        }

        [Fact]
        public async Task ForeignCurrencyVendorInvoice_ShouldCalculateBaseAmounts()
        {
            var createDto = new VendorInvoiceCreateDto
            {
                BusinessPartnerId = _vendorId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "USD",
                ExchangeRate = 15.0m,
                TaxGroupId = _standardTaxGroupId,
                ExpenseAccountId = _expenseAccountId,
                ApAccountId = _apAccountId,
                LineItems = new List<VendorInvoiceLineItemCreateDto>
                {
                    new VendorInvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = _expenseAccountId,
                        Description = "Test Line",
                        Quantity = 1,
                        UnitPrice = 100m // Subtotal 100, Tax 20.75. Total = 120.75
                    }
                }
            };

            var invoiceDto = await _vendorInvoiceService.CreateAsync(createDto);
            
            // Expected base currency amount = 120.75 * 15 = 1811.25
            Assert.Equal(1811.25m, invoiceDto.BaseCurrencyAmount);
        }

        [Fact]
        public async Task ForeignCurrencyVendorInvoice_Post_ShouldUseBaseCurrencyInGL()
        {
            var createDto = new VendorInvoiceCreateDto
            {
                BusinessPartnerId = _vendorId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "USD",
                ExchangeRate = 15.0m,
                TaxGroupId = _standardTaxGroupId,
                ExpenseAccountId = _expenseAccountId,
                ApAccountId = _apAccountId,
                LineItems = new List<VendorInvoiceLineItemCreateDto>
                {
                    new VendorInvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = _expenseAccountId,
                        Description = "Test Line",
                        Quantity = 1,
                        UnitPrice = 100m // Subtotal 100, Tax 20.75. Total = 120.75
                    }
                }
            };

            var invoiceDto = await _vendorInvoiceService.CreateAsync(createDto);
            await _vendorInvoiceService.SubmitForApprovalAsync(invoiceDto.Id);

            CreateJournalEntryDto capturedJe = null;
            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .Callback<CreateJournalEntryDto, CancellationToken>((je, ct) => capturedJe = je)
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            await _vendorInvoiceService.ApproveAsync(invoiceDto.Id);

            Assert.NotNull(capturedJe);
            
            // AP Credit should be in base currency: 120.75 * 15 = 1811.25
            var apLine = capturedJe.Transactions.Single(t => t.AccountId == _apAccountId);
            Assert.Equal("Credit", apLine.TransactionType);
            Assert.Equal(1811.25m, apLine.Amount);
            Assert.Equal("USD", apLine.CurrencyCode);
            Assert.Equal(15.0m, apLine.ExchangeRate);
            Assert.Equal(120.75m, apLine.ForeignAmount);
        }

        [Fact]
        public async Task BaseCurrencyVendorInvoice_ShouldForceExchangeRateToOne()
        {
            var createDto = new VendorInvoiceCreateDto
            {
                BusinessPartnerId = _vendorId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "GHS", // Base currency
                ExchangeRate = 15.0m, // Dummy non-1 rate
                TaxGroupId = _standardTaxGroupId,
                ExpenseAccountId = _expenseAccountId,
                ApAccountId = _apAccountId,
                LineItems = new List<VendorInvoiceLineItemCreateDto>
                {
                    new VendorInvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = _expenseAccountId,
                        Description = "Test Line",
                        Quantity = 1,
                        UnitPrice = 100m
                    }
                }
            };

            var invoiceDto = await _vendorInvoiceService.CreateAsync(createDto);
            
            Assert.Equal(1.0m, invoiceDto.ExchangeRate);
        }

        [Fact]
        public async Task ForeignCurrencyCustomerInvoice_ShouldRequireExchangeRate()
        {
            var createDto = new InvoiceCreateDto
            {
                BusinessPartnerId = _customerId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "USD",
                ExchangeRate = 0m,
                TaxGroupId = _standardTaxGroupId,
                LineItems = new List<InvoiceLineItemCreateDto>
                {
                    new InvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = _revenueAccountId,
                        Description = "Test Line",
                        Quantity = 1,
                        UnitPrice = 100m
                    }
                }
            };

            await Assert.ThrowsAsync<ArgumentException>(() => _customerInvoiceService.CreateAsync(createDto));
        }

        [Fact]
        public async Task ForeignCurrencyCustomerInvoice_Post_ShouldUseBaseCurrencyInGL()
        {
            var createDto = new InvoiceCreateDto
            {
                BusinessPartnerId = _customerId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "USD",
                ExchangeRate = 15.0m,
                TaxGroupId = _standardTaxGroupId,
                LineItems = new List<InvoiceLineItemCreateDto>
                {
                    new InvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = _revenueAccountId,
                        Description = "Test Line",
                        Quantity = 1,
                        UnitPrice = 100m // Subtotal 100, Tax 20.75. Total = 120.75
                    }
                }
            };

            CreateJournalEntryDto capturedJe = null;
            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .Callback<CreateJournalEntryDto, CancellationToken>((je, ct) => capturedJe = je)
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            var invoiceDto = await _customerInvoiceService.CreateAsync(createDto);
            await _customerInvoiceService.SendInvoiceAsync(invoiceDto.Id);

            Assert.NotNull(capturedJe);
            
            // AR Debit should be in base currency: 120.75 * 15 = 1811.25
            var arLine = capturedJe.Transactions.Single(t => t.AccountId == _arAccountId);
            Assert.Equal("Debit", arLine.TransactionType);
            Assert.Equal(1811.25m, arLine.Amount);
            Assert.Equal("USD", arLine.CurrencyCode);
            Assert.Equal(15.0m, arLine.ExchangeRate);
            Assert.Equal(120.75m, arLine.ForeignAmount);
        }

        [Fact]
        public async Task PaymentAllocation_ShouldCalculateFxGainLoss_WhenPaymentRateDiffers()
        {
            var fxGainAccountId = Guid.NewGuid();
            var fxLossAccountId = Guid.NewGuid();
            
            _dbContext.Accounts.AddRange(
                new Account { Id = fxGainAccountId, TenantId = _tenantId, AccountCode = "8100", AccountName = "FX Gain", CurrencyCode = "GHS" },
                new Account { Id = fxLossAccountId, TenantId = _tenantId, AccountCode = "8200", AccountName = "FX Loss", CurrencyCode = "GHS" }
            );
            
            var settings = await _dbContext.FinanceSettings.FirstAsync(s => s.TenantId == _tenantId);
            settings.RealizedFxGainAccountId = fxGainAccountId;
            settings.RealizedFxLossAccountId = fxLossAccountId;
            await _dbContext.SaveChangesAsync();

            // Create Invoice in USD at rate 15.0
            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                InvoiceNumber = "AR-USD-01",
                BusinessPartnerId = _customerId,
                CustomerName = "Test Cust",
                InvoiceDate = DateTime.UtcNow,
                SubTotal = 100m,
                TotalAmount = 100m,
                CurrencyCode = "USD",
                ExchangeRate = 15.0m,
                BaseCurrencyAmount = 1500m,
                Status = InvoiceStatus.Sent
            };
            _dbContext.Invoices.Add(invoice);

            // Create customer payment in USD at rate 16.0
            var payment = new CustomerPayment
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                PaymentNumber = "PAY-USD-01",
                BusinessPartnerId = _customerId,
                PaymentDate = DateTime.UtcNow,
                TotalAmount = 100m,
                AllocatedAmount = 100m,
                CurrencyCode = "USD",
                ExchangeRate = 16.0m,
                Status = "Cleared"
            };
            _dbContext.Set<CustomerPayment>().Add(payment);

            // Create PaymentAllocation
            var allocation = new PaymentAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                CustomerPaymentId = payment.Id,
                InvoiceId = invoice.Id,
                AllocatedAmount = 100m,
                AllocationDate = DateTime.UtcNow
            };
            _dbContext.Set<PaymentAllocation>().Add(allocation);
            await _dbContext.SaveChangesAsync();

            // Setup mock to capture the journal entry
            CreateJournalEntryDto capturedJe = null;
            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .Callback<CreateJournalEntryDto, CancellationToken>((je, ct) => capturedJe = je)
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            // Post FX Variance
            await _postingService.PostAllocationFxVarianceAsync(allocation.Id);

            Assert.NotNull(capturedJe);
            
            // Variance = PaymentAllocatedBase - InvoiceAllocatedBase = (100 * 16) - (100 * 15) = 1600 - 1500 = 100 (Gain)
            // Gain: Dr AR Control 100, Cr FX Gain Account 100
            var arLine = capturedJe.Transactions.Single(t => t.AccountId == _arAccountId);
            Assert.Equal("Debit", arLine.TransactionType);
            Assert.Equal(100m, arLine.Amount);

            var gainLine = capturedJe.Transactions.Single(t => t.AccountId == fxGainAccountId);
            Assert.Equal("Credit", gainLine.TransactionType);
            Assert.Equal(100m, gainLine.Amount);
        }

        [Fact]
        public async Task PostedForeignCurrencyInvoice_ShouldNotAllowExchangeRateChange()
        {
            // Create USD Vendor Invoice
            var createDto = new VendorInvoiceCreateDto
            {
                BusinessPartnerId = _vendorId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "USD",
                ExchangeRate = 15.0m,
                TaxGroupId = _standardTaxGroupId,
                ExpenseAccountId = _expenseAccountId,
                ApAccountId = _apAccountId,
                LineItems = new List<VendorInvoiceLineItemCreateDto>
                {
                    new VendorInvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = _expenseAccountId,
                        Description = "Test Line",
                        Quantity = 1,
                        UnitPrice = 100m
                    }
                }
            };

            var invoiceDto = await _vendorInvoiceService.CreateAsync(createDto);
            
            // Submit for Approval
            await _vendorInvoiceService.SubmitForApprovalAsync(invoiceDto.Id);
            
            // Mock Journal Entry for Approve
            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            // Approve / Post Invoice (changes status to Approved)
            await _vendorInvoiceService.ApproveAsync(invoiceDto.Id);

            // Attempt to update the exchange rate of the Approved invoice
            var updateDto = new VendorInvoiceUpdateDto
            {
                Id = invoiceDto.Id,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "USD",
                ExchangeRate = 16.0m, // Attempting rate change
                TaxGroupId = _standardTaxGroupId,
                ExpenseAccountId = _expenseAccountId,
                ApAccountId = _apAccountId,
                LineItems = new List<VendorInvoiceLineItemCreateDto>
                {
                    new VendorInvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = _expenseAccountId,
                        Description = "Test Line",
                        Quantity = 1,
                        UnitPrice = 100m
                    }
                }
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _vendorInvoiceService.UpdateAsync(updateDto));
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Dispose();
        }
    }
}
