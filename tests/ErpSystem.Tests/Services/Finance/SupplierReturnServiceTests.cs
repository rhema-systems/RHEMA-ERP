using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Api.Services.Finance.Taxation;
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
    public class SupplierReturnServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
        private readonly Guid _tenantId = Guid.NewGuid();

        // Services under test
        private readonly SupplierReturnService _returnService;
        private readonly VendorInvoiceService _vendorInvoiceService;
        private readonly SubledgerPostingService _postingService;

        // Mocks
        private readonly Mock<IInventoryReturnService> _inventoryReturnMock;
        private readonly Mock<IInventoryValuationService> _inventoryValuationMock;
        private readonly Mock<IJournalEntryService> _journalEntryServiceMock;

        // Seeding GUIDs
        private readonly Guid _vendorId = Guid.NewGuid();
        private readonly Guid _apAccountId = Guid.NewGuid();
        private readonly Guid _expenseAccountId = Guid.NewGuid();
        private readonly Guid _vatReceivableAccountId = Guid.NewGuid();
        private readonly Guid _vatPayableAccountId = Guid.NewGuid();
        private readonly Guid _genericTaxControlAccountId = Guid.NewGuid();
        private readonly Guid _standardTaxGroupId = Guid.NewGuid();

        public SupplierReturnServiceTests()
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
            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });
            _journalEntryServiceMock.Setup(x => x.PostJournalEntryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            _inventoryValuationMock = new Mock<IInventoryValuationService>();
            _inventoryReturnMock = new Mock<IInventoryReturnService>();

            var postingLoggerMock = new Mock<ILogger<SubledgerPostingService>>();
            _postingService = new SubledgerPostingService(
                repository,
                currentUserMock.Object,
                tenantSettingsMock.Object,
                _journalEntryServiceMock.Object,
                bookValidationMock.Object,
                postingLoggerMock.Object);

            var engineLoggerMock = new Mock<ILogger<TaxCalculationEngine>>();
            var taxEngine = new TaxCalculationEngine(
                repository,
                currentUserMock.Object,
                engineLoggerMock.Object);

            var auditLoggerMock = new Mock<ILogger<TaxAuditService>>();
            var taxAuditService = new TaxAuditService(
                repository,
                currentUserMock.Object,
                auditLoggerMock.Object);

            var apLoggerMock = new Mock<ILogger<VendorInvoiceService>>();
            _vendorInvoiceService = new VendorInvoiceService(
                unitOfWork,
                currentUserMock.Object,
                _postingService,
                _inventoryValuationMock.Object,
                taxEngine,
                taxAuditService,
                tenantSettingsMock.Object,
                apLoggerMock.Object);

            var returnLoggerMock = new Mock<ILogger<SupplierReturnService>>();
            _returnService = new SupplierReturnService(
                unitOfWork,
                currentUserMock.Object,
                _postingService,
                _inventoryReturnMock.Object,
                tenantSettingsMock.Object,
                returnLoggerMock.Object);

            SeedStaticData();
        }

        private void SeedStaticData()
        {
            var tenant = new Tenant { Id = _tenantId, Name = "Test Tenant", Code = "TT01" };
            _dbContext.Tenants.Add(tenant);

            _dbContext.Accounts.AddRange(
                new Account { Id = _apAccountId, TenantId = _tenantId, AccountCode = "2100", AccountName = "AP Control", CurrencyCode = "GHS" },
                new Account { Id = _expenseAccountId, TenantId = _tenantId, AccountCode = "5100", AccountName = "General Expense", CurrencyCode = "GHS" },
                new Account { Id = _vatReceivableAccountId, TenantId = _tenantId, AccountCode = "1310", AccountName = "VAT Input Receivable", CurrencyCode = "GHS" },
                new Account { Id = _vatPayableAccountId, TenantId = _tenantId, AccountCode = "2310", AccountName = "VAT Output Payable", CurrencyCode = "GHS" },
                new Account { Id = _genericTaxControlAccountId, TenantId = _tenantId, AccountCode = "2390", AccountName = "Tax Control Fallback", CurrencyCode = "GHS" }
            );

            _dbContext.FinanceSettings.Add(new FinanceSettings
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                ControlAccountApId = _apAccountId,
                ControlAccountTaxId = _genericTaxControlAccountId,
                SubledgerPostingMode = "Direct"
            });

            _dbContext.BusinessPartners.Add(new BusinessPartner
            {
                Id = _vendorId,
                TenantId = _tenantId,
                PartnerCode = "V001",
                PartnerName = "Supplier Corp",
                PartnerType = "Supplier",
                Currency = "GHS",
                DefaultApAccountId = _apAccountId,
                IsActive = true
            });

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
            _dbContext.Taxes.Add(vatTax);

            var standardGroup = new TaxGroup
            {
                Id = _standardTaxGroupId,
                TenantId = _tenantId,
                Code = "GH_VAT",
                Name = "Ghana VAT",
                Applicability = TaxApplicability.Both,
                IsActive = true
            };
            standardGroup.Components = new List<TaxGroupComponent>
            {
                new TaxGroupComponent { Id = Guid.NewGuid(), TenantId = _tenantId, TaxGroupId = _standardTaxGroupId, TaxId = vatTax.Id, Tax = vatTax, CalculationOrder = 1, CompoundBasis = CompoundBasis.BaseOnly }
            };
            _dbContext.TaxGroups.Add(standardGroup);

            // Seeding Currencies
            _dbContext.Currencies.Add(new Currency { Id = Guid.NewGuid(), TenantId = _tenantId, CurrencyCode = "GHS", CurrencyName = "Ghana Cedi", IsBaseCurrency = true });
            _dbContext.Currencies.Add(new Currency { Id = Guid.NewGuid(), TenantId = _tenantId, CurrencyCode = "USD", CurrencyName = "US Dollar", IsBaseCurrency = false });

            // Seeding Fiscal Calendar
            var fy = new FiscalYear { Id = Guid.NewGuid(), TenantId = _tenantId, Year = DateTime.UtcNow.Year, StartDate = new DateTime(DateTime.UtcNow.Year, 1, 1), EndDate = new DateTime(DateTime.UtcNow.Year, 12, 31), IsActive = true };
            var fp = new FiscalPeriod { Id = Guid.NewGuid(), TenantId = _tenantId, FiscalYearId = fy.Id, PeriodNumber = DateTime.UtcNow.Month, StartDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1), EndDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1).AddDays(-1), IsOpen = true };
            _dbContext.FiscalYears.Add(fy);
            _dbContext.FiscalPeriods.Add(fp);

            _dbContext.SaveChanges();
        }

        private async Task<VendorInvoice> CreateAndApproveInvoiceAsync(decimal quantity = 10m, decimal unitPrice = 100m, string currency = "GHS", decimal exchangeRate = 1.0m)
        {
            var createDto = new VendorInvoiceCreateDto
            {
                BusinessPartnerId = _vendorId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = currency,
                ExchangeRate = exchangeRate,
                TaxGroupId = _standardTaxGroupId,
                ExpenseAccountId = _expenseAccountId,
                ApAccountId = _apAccountId,
                LineItems = new List<VendorInvoiceLineItemCreateDto>
                {
                    new VendorInvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        GLAccountId = _expenseAccountId,
                        Description = "Test Item",
                        Quantity = quantity,
                        UnitPrice = unitPrice
                    }
                }
            };

            var invoiceDto = await _vendorInvoiceService.CreateAsync(createDto);
            await _vendorInvoiceService.SubmitForApprovalAsync(invoiceDto.Id);

            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });
            _journalEntryServiceMock.Setup(x => x.PostJournalEntryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            await _vendorInvoiceService.ApproveAsync(invoiceDto.Id);

            return await _dbContext.VendorInvoices.Include(i => i.LineItems).FirstAsync(i => i.Id == invoiceDto.Id);
        }

        [Fact]
        public async Task Return_FromPostedInvoice_ShouldCreateSupplierDebitNote()
        {
            // Arrange
            var invoice = await CreateAndApproveInvoiceAsync();
            var line = invoice.LineItems.First();

            var returnDto = new CreateSupplierReturnDto
            {
                VendorId = _vendorId,
                OriginalVendorInvoiceId = invoice.Id,
                ReturnDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                Lines = new List<CreateSupplierReturnLineDto>
                {
                    new CreateSupplierReturnLineDto
                    {
                        OriginalVendorInvoiceLineItemId = line.Id,
                        QuantityReturned = 5m,
                        UnitPrice = 100m,
                        Description = line.Description
                    }
                }
            };

            // Act
            var ret = await _returnService.CreateReturnAsync(returnDto);
            var approvedRet = await _returnService.ApproveReturnAsync(ret.Id);

            // Assert
            Assert.Equal(SupplierReturnStatus.Approved, approvedRet.Status);
            
            var debitNote = await _dbContext.SupplierDebitNotes.Include(dn => dn.LineItems).FirstOrDefaultAsync(dn => dn.SupplierReturnId == ret.Id);
            Assert.NotNull(debitNote);
            Assert.Equal(SupplierDebitNoteStatus.Approved, debitNote.Status);
            Assert.Equal(invoice.Id, debitNote.OriginalVendorInvoiceId);
            Assert.Equal(5m * 100m, debitNote.SubTotal);
            Assert.Equal(5m * 100m * 0.15m, debitNote.TaxAmount); // 75m
            Assert.Equal(575m, debitNote.TotalAmount);
            Assert.Single(debitNote.LineItems);
        }

        [Fact]
        public async Task Return_FromGRVWithoutInvoice_ShouldNotReverseAPLiability()
        {
            // Arrange
            var po = new FinancePurchaseOrder
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                OrderNumber = "FPO-888",
                VendorId = _vendorId,
                OrderDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                Status = FinancePurchaseOrderStatus.Approved,
                Items = new List<FinancePurchaseOrderItem>
                {
                    new FinancePurchaseOrderItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantId,
                        LineType = FinancePurchaseOrderLineType.GLAccount,
                        GlAccountId = _expenseAccountId,
                        Description = "PO GL Line",
                        OrderedQuantity = 10m,
                        UnitPrice = 100m,
                        ReceivedQuantity = 10m
                    }
                }
            };
            _dbContext.FinancePurchaseOrders.Add(po);

            var grv = new FinancePurchaseOrderReceipt
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                FinancePurchaseOrderId = po.Id,
                ReceiptNumber = "GRV-888",
                ReceiptDate = DateTime.UtcNow,
                Items = new List<FinancePurchaseOrderReceiptItem>
                {
                    new FinancePurchaseOrderReceiptItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantId,
                        FinancePurchaseOrderItemId = po.Items.First().Id,
                        QuantityReceived = 10m,
                        InvoicedQuantity = 0m
                    }
                }
            };
            _dbContext.FinancePurchaseOrderReceipts.Add(grv);
            await _dbContext.SaveChangesAsync();

            var returnDto = new CreateSupplierReturnDto
            {
                VendorId = _vendorId,
                OriginalFinancePurchaseOrderReceiptId = grv.Id,
                ReturnDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                Lines = new List<CreateSupplierReturnLineDto>
                {
                    new CreateSupplierReturnLineDto
                    {
                        OriginalFinancePurchaseOrderItemId = po.Items.First().Id,
                        QuantityReturned = 4m,
                        UnitPrice = 100m,
                        Description = "PO GL Line"
                    }
                }
            };

            // Act
            var ret = await _returnService.CreateReturnAsync(returnDto);
            var approvedRet = await _returnService.ApproveReturnAsync(ret.Id);

            // Assert
            Assert.Equal(SupplierReturnStatus.Approved, approvedRet.Status);
            
            // Should not create debit note
            var debitNote = await _dbContext.SupplierDebitNotes.FirstOrDefaultAsync(dn => dn.SupplierReturnId == ret.Id);
            Assert.Null(debitNote);

            // Quantities should be reversed
            var poItem = await _dbContext.FinancePurchaseOrderItems.FirstAsync(i => i.Id == po.Items.First().Id);
            Assert.Equal(6m, poItem.ReceivedQuantity); // 10 - 4
        }

        [Fact]
        public async Task Return_ShouldReverseOriginalTaxProportionally_NotCurrentTaxRate()
        {
            // Arrange
            // Create an invoice with custom tax amount override (simulating historical tax rates)
            var invoice = await CreateAndApproveInvoiceAsync(quantity: 10m, unitPrice: 100m);
            var line = invoice.LineItems.First();

            // Override tax amount on invoice to simulate historical tax rates (e.g. 20% standard tax recorded historically instead of 15% rate)
            line.TaxAmount = 200m; // 200 tax on 1000 subtotal
            invoice.TaxAmount = 200m;
            invoice.TotalAmount = 1200m;
            invoice.BaseCurrencyAmount = 1200m;
            await _dbContext.SaveChangesAsync();

            var returnDto = new CreateSupplierReturnDto
            {
                VendorId = _vendorId,
                OriginalVendorInvoiceId = invoice.Id,
                ReturnDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                Lines = new List<CreateSupplierReturnLineDto>
                {
                    new CreateSupplierReturnLineDto
                    {
                        OriginalVendorInvoiceLineItemId = line.Id,
                        QuantityReturned = 5m, // 50% return
                        UnitPrice = 100m,
                        Description = line.Description
                    }
                }
            };

            // Act
            var ret = await _returnService.CreateReturnAsync(returnDto);
            var approvedRet = await _returnService.ApproveReturnAsync(ret.Id);

            // Assert
            // Proportional tax should be 50% of 200m = 100m
            Assert.Equal(100m, approvedRet.TaxAmount);
            Assert.Equal(600m, approvedRet.TotalAmount); // 500 subtotal + 100 tax
        }

        [Fact]
        public async Task Return_ShouldPreventDoubleReturnAgainstSameQuantity()
        {
            // Arrange
            var invoice = await CreateAndApproveInvoiceAsync();
            var line = invoice.LineItems.First();

            var returnDto1 = new CreateSupplierReturnDto
            {
                VendorId = _vendorId,
                OriginalVendorInvoiceId = invoice.Id,
                ReturnDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                Lines = new List<CreateSupplierReturnLineDto>
                {
                    new CreateSupplierReturnLineDto
                    {
                        OriginalVendorInvoiceLineItemId = line.Id,
                        QuantityReturned = 6m,
                        UnitPrice = 100m,
                        Description = line.Description
                    }
                }
            };

            var returnDto2 = new CreateSupplierReturnDto
            {
                VendorId = _vendorId,
                OriginalVendorInvoiceId = invoice.Id,
                ReturnDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                Lines = new List<CreateSupplierReturnLineDto>
                {
                    new CreateSupplierReturnLineDto
                    {
                        OriginalVendorInvoiceLineItemId = line.Id,
                        QuantityReturned = 5m, // 6 + 5 = 11 > 10
                        UnitPrice = 100m,
                        Description = line.Description
                    }
                }
            };

            // Act & Assert
            var ret1 = await _returnService.CreateReturnAsync(returnDto1);
            await _returnService.ApproveReturnAsync(ret1.Id);

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _returnService.CreateReturnAsync(returnDto2);
            });
        }

        [Fact]
        public async Task Return_ShouldUseOriginalInvoiceExchangeRate()
        {
            // Arrange
            // Foreign currency invoice with exchange rate = 15.5
            var invoice = await CreateAndApproveInvoiceAsync(currency: "USD", exchangeRate: 15.5m);
            var line = invoice.LineItems.First();

            var returnDto = new CreateSupplierReturnDto
            {
                VendorId = _vendorId,
                OriginalVendorInvoiceId = invoice.Id,
                ReturnDate = DateTime.UtcNow,
                CurrencyCode = "USD",
                ExchangeRate = 15.5m,
                Lines = new List<CreateSupplierReturnLineDto>
                {
                    new CreateSupplierReturnLineDto
                    {
                        OriginalVendorInvoiceLineItemId = line.Id,
                        QuantityReturned = 2m,
                        UnitPrice = 100m,
                        Description = line.Description
                    }
                }
            };

            // Act
            var ret = await _returnService.CreateReturnAsync(returnDto);

            // Assert
            Assert.Equal(15.5m, ret.ExchangeRate);
            Assert.Equal("USD", ret.CurrencyCode);
            Assert.Equal(230m * 15.5m, ret.BaseCurrencyAmount); // 200 subtotal + 30 tax = 230 * 15.5
        }

        [Fact]
        public async Task Return_ShouldRejectManualExchangeRateOverride()
        {
            // Arrange
            var invoice = await CreateAndApproveInvoiceAsync(currency: "USD", exchangeRate: 15.5m);
            var line = invoice.LineItems.First();

            var returnDto = new CreateSupplierReturnDto
            {
                VendorId = _vendorId,
                OriginalVendorInvoiceId = invoice.Id,
                ReturnDate = DateTime.UtcNow,
                CurrencyCode = "USD",
                ExchangeRate = 16.0m, // Override attempt!
                Lines = new List<CreateSupplierReturnLineDto>
                {
                    new CreateSupplierReturnLineDto
                    {
                        OriginalVendorInvoiceLineItemId = line.Id,
                        QuantityReturned = 2m,
                        UnitPrice = 100m,
                        Description = line.Description
                    }
                }
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _returnService.CreateReturnAsync(returnDto);
            });
        }

        [Fact]
        public async Task Return_InventoryLine_ShouldCallInventoryReturnBoundary()
        {
            // Arrange
            var po = new FinancePurchaseOrder
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                OrderNumber = "FPO-INV",
                VendorId = _vendorId,
                OrderDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                Status = FinancePurchaseOrderStatus.Approved,
                Items = new List<FinancePurchaseOrderItem>
                {
                    new FinancePurchaseOrderItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantId,
                        LineType = FinancePurchaseOrderLineType.Inventory,
                        InventoryItemId = Guid.NewGuid(),
                        WarehouseId = Guid.NewGuid(),
                        Description = "Inventory Item",
                        OrderedQuantity = 10m,
                        UnitPrice = 100m,
                        ReceivedQuantity = 10m
                    }
                }
            };
            _dbContext.FinancePurchaseOrders.Add(po);

            var grv = new FinancePurchaseOrderReceipt
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                FinancePurchaseOrderId = po.Id,
                ReceiptNumber = "GRV-INV",
                ReceiptDate = DateTime.UtcNow,
                Items = new List<FinancePurchaseOrderReceiptItem>
                {
                    new FinancePurchaseOrderReceiptItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantId,
                        FinancePurchaseOrderItemId = po.Items.First().Id,
                        QuantityReceived = 10m,
                        InvoicedQuantity = 0m
                    }
                }
            };
            _dbContext.FinancePurchaseOrderReceipts.Add(grv);
            await _dbContext.SaveChangesAsync();

            var returnDto = new CreateSupplierReturnDto
            {
                VendorId = _vendorId,
                OriginalFinancePurchaseOrderReceiptId = grv.Id,
                ReturnDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                Lines = new List<CreateSupplierReturnLineDto>
                {
                    new CreateSupplierReturnLineDto
                    {
                        OriginalFinancePurchaseOrderItemId = po.Items.First().Id,
                        QuantityReturned = 5m,
                        UnitPrice = 100m,
                        Description = "Inventory Item"
                    }
                }
            };

            // Act
            var ret = await _returnService.CreateReturnAsync(returnDto);
            await _returnService.ApproveReturnAsync(ret.Id);

            // Assert
            _inventoryReturnMock.Verify(x => x.ProcessSupplierReturnAsync(
                It.Is<Guid>(t => t == _tenantId),
                It.Is<Guid>(r => r == ret.Id),
                It.Is<IEnumerable<FinanceReceiptInventoryLine>>(lines => lines.Count() == 1 && lines.First().QuantityReceived == 5m)),
                Times.Once);
        }

        [Fact]
        public async Task Return_GLAccountLine_ShouldNotCallInventoryReturnBoundary()
        {
            // Arrange
            var po = new FinancePurchaseOrder
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                OrderNumber = "FPO-GL",
                VendorId = _vendorId,
                OrderDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                Status = FinancePurchaseOrderStatus.Approved,
                Items = new List<FinancePurchaseOrderItem>
                {
                    new FinancePurchaseOrderItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantId,
                        LineType = FinancePurchaseOrderLineType.GLAccount,
                        GlAccountId = _expenseAccountId,
                        Description = "GL Line",
                        OrderedQuantity = 10m,
                        UnitPrice = 100m,
                        ReceivedQuantity = 10m
                    }
                }
            };
            _dbContext.FinancePurchaseOrders.Add(po);

            var grv = new FinancePurchaseOrderReceipt
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                FinancePurchaseOrderId = po.Id,
                ReceiptNumber = "GRV-GL",
                ReceiptDate = DateTime.UtcNow,
                Items = new List<FinancePurchaseOrderReceiptItem>
                {
                    new FinancePurchaseOrderReceiptItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantId,
                        FinancePurchaseOrderItemId = po.Items.First().Id,
                        QuantityReceived = 10m,
                        InvoicedQuantity = 0m
                    }
                }
            };
            _dbContext.FinancePurchaseOrderReceipts.Add(grv);
            await _dbContext.SaveChangesAsync();

            var returnDto = new CreateSupplierReturnDto
            {
                VendorId = _vendorId,
                OriginalFinancePurchaseOrderReceiptId = grv.Id,
                ReturnDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                Lines = new List<CreateSupplierReturnLineDto>
                {
                    new CreateSupplierReturnLineDto
                    {
                        OriginalFinancePurchaseOrderItemId = po.Items.First().Id,
                        QuantityReturned = 5m,
                        UnitPrice = 100m,
                        Description = "GL Line"
                    }
                }
            };

            // Act
            var ret = await _returnService.CreateReturnAsync(returnDto);
            await _returnService.ApproveReturnAsync(ret.Id);

            // Assert
            _inventoryReturnMock.Verify(x => x.ProcessSupplierReturnAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IEnumerable<FinanceReceiptInventoryLine>>()),
                Times.Never);
        }

        [Fact]
        public async Task SupplierDebitNote_ShouldAppearInVendorStatementOrOpenItems()
        {
            // Arrange
            var invoice = await CreateAndApproveInvoiceAsync();
            var line = invoice.LineItems.First();

            var returnDto = new CreateSupplierReturnDto
            {
                VendorId = _vendorId,
                OriginalVendorInvoiceId = invoice.Id,
                ReturnDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                Lines = new List<CreateSupplierReturnLineDto>
                {
                    new CreateSupplierReturnLineDto
                    {
                        OriginalVendorInvoiceLineItemId = line.Id,
                        QuantityReturned = 2m, // Return 2 units = 200 + 30 tax = 230 total
                        UnitPrice = 100m,
                        Description = line.Description
                    }
                }
            };

            // Act
            var ret = await _returnService.CreateReturnAsync(returnDto);
            await _returnService.ApproveReturnAsync(ret.Id);

            // Assert
            var savedInvoice = await _dbContext.VendorInvoices.FirstAsync(i => i.Id == invoice.Id);
            // Invoice paid amount should increase by debit note amount
            Assert.Equal(230m, savedInvoice.PaidAmount);
            Assert.Equal(VendorInvoiceStatus.PartiallyPaid, savedInvoice.Status);
            Assert.Equal(920m, savedInvoice.BalanceAmount); // 1150 - 230
        }

        [Fact]
        public async Task ApprovedSupplierReturn_ShouldRejectFurtherLineChanges()
        {
            // Arrange
            var invoice = await CreateAndApproveInvoiceAsync();
            var line = invoice.LineItems.First();

            var returnDto = new CreateSupplierReturnDto
            {
                VendorId = _vendorId,
                OriginalVendorInvoiceId = invoice.Id,
                ReturnDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                Lines = new List<CreateSupplierReturnLineDto>
                {
                    new CreateSupplierReturnLineDto
                    {
                        OriginalVendorInvoiceLineItemId = line.Id,
                        QuantityReturned = 2m,
                        UnitPrice = 100m,
                        Description = line.Description
                    }
                }
            };

            var ret = await _returnService.CreateReturnAsync(returnDto);
            await _returnService.ApproveReturnAsync(ret.Id);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _returnService.ApproveReturnAsync(ret.Id);
            });
        }

        [Fact]
        public async Task Return_ShouldReverseOriginalDiscountValue()
        {
            // Arrange
            // Seed a manual VendorInvoice with a discount
            var invoiceId = Guid.NewGuid();
            var invoiceLineId = Guid.NewGuid();
            var invoice = new VendorInvoice
            {
                Id = invoiceId,
                TenantId = _tenantId,
                InvoiceNumber = "VI-DISC-001",
                BusinessPartnerId = _vendorId,
                SupplierName = "Supplier Corp",
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                SubTotal = 900m, // 1000 gross - 100 discount
                DiscountAmount = 100m,
                TaxAmount = 135m, // 900 * 0.15
                TotalAmount = 1035m,
                BaseCurrencyAmount = 1035m,
                Status = VendorInvoiceStatus.Approved,
                LineItems = new List<VendorInvoiceLineItem>
                {
                    new VendorInvoiceLineItem
                    {
                        Id = invoiceLineId,
                        TenantId = _tenantId,
                        VendorInvoiceId = invoiceId,
                        LineItemType = "Product",
                        GLAccountId = _expenseAccountId,
                        Description = "Discounted Item",
                        Quantity = 10m,
                        UnitPrice = 100m,
                        DiscountPercentage = 10m,
                        DiscountAmount = 100m,
                        TaxGroupId = _standardTaxGroupId,
                        TaxRate = 15m,
                        TaxAmount = 135m
                    }
                }
            };
            
            _dbContext.VendorInvoices.Add(invoice);
            await _dbContext.SaveChangesAsync();

            var returnDto = new CreateSupplierReturnDto
            {
                VendorId = _vendorId,
                OriginalVendorInvoiceId = invoiceId,
                ReturnDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                Lines = new List<CreateSupplierReturnLineDto>
                {
                    new CreateSupplierReturnLineDto
                    {
                        OriginalVendorInvoiceLineItemId = invoiceLineId,
                        QuantityReturned = 5m, // 50% return
                        UnitPrice = 100m,
                        Description = "Discounted Item"
                    }
                }
            };

            // Act
            var ret = await _returnService.CreateReturnAsync(returnDto);
            var approvedRet = await _returnService.ApproveReturnAsync(ret.Id);

            // Assert
            Assert.Equal(SupplierReturnStatus.Approved, approvedRet.Status);
            Assert.Equal(50m, approvedRet.DiscountAmount); // 50% of 100m discount = 50m
            Assert.Equal(450m, approvedRet.SubTotal); // 500 gross - 50 discount = 450m
            Assert.Equal(67.5m, approvedRet.TaxAmount); // 450 * 0.15 = 67.5m
            Assert.Equal(517.5m, approvedRet.TotalAmount); // 450 + 67.5 = 517.5m

            var debitNote = await _dbContext.SupplierDebitNotes.Include(dn => dn.LineItems).FirstOrDefaultAsync(dn => dn.SupplierReturnId == ret.Id);
            Assert.NotNull(debitNote);
            Assert.Equal(50m, debitNote.DiscountAmount);
            Assert.Equal(450m, debitNote.SubTotal);
            Assert.Equal(67.5m, debitNote.TaxAmount);
            Assert.Equal(517.5m, debitNote.TotalAmount);
            
            var dbnLine = debitNote.LineItems.First();
            Assert.Equal(10m, dbnLine.DiscountPercentage);
            Assert.Equal(50m, dbnLine.DiscountAmount);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Dispose();
        }
    }
}
