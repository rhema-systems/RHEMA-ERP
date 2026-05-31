using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Api.Services.Finance.AR;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Services.Sales;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using ErpSystem.Data.Repositories.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Tests.Services.Sales
{
    public class CustomerReturnServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
        private readonly Guid _tenantId = Guid.NewGuid();

        // System Under Test
        private readonly ReturnOrderService _sut;
        
        // Real dependencies for integration testing
        private readonly CreditNotePostingService _creditNotePostingService;

        // Mocked dependencies
        private readonly Mock<IInventoryValuationService> _inventoryValuationMock;
        private readonly Mock<ISubledgerPostingService> _subledgerPostingMock;
        private readonly Mock<IRefundPostingService> _refundPostingMock;
        private readonly Mock<ICurrentUserProvider> _currentUserProviderMock;
        private readonly Mock<IInventoryReturnService> _inventoryReturnMock;
        private readonly Mock<IJournalEntryService> _journalEntryServiceMock;
        private readonly Mock<ITaxAuditService> _taxAuditServiceMock;

        public CustomerReturnServiceTests()
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
            var financialRepo = new FinancialRepository(_dbContext);
            
            var roRepo = new GenericRepository<ReturnOrder>(_dbContext);
            var roLineRepo = new GenericRepository<ReturnOrderLine>(_dbContext);
            var cnRepo = new GenericRepository<CreditNote>(_dbContext);
            var cnLineRepo = new GenericRepository<CreditNoteLine>(_dbContext);
            var refundRepo = new GenericRepository<Refund>(_dbContext);
            var soLineRepo = new GenericRepository<SalesOrderLine>(_dbContext);
            var invoiceRepo = new GenericRepository<Invoice>(_dbContext);
            var invoiceLineRepo = new GenericRepository<InvoiceLineItem>(_dbContext);
            var deliveryNoteRepo = new GenericRepository<DeliveryNote>(_dbContext);
            var salesOrderRepo = new GenericRepository<SalesOrder>(_dbContext);

            _currentUserProviderMock = new Mock<ICurrentUserProvider>();
            _currentUserProviderMock.SetupGet(x => x.TenantId).Returns(_tenantId);
            _currentUserProviderMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());

            var currentUserMock = new Mock<ICurrentUserService>();
            currentUserMock.SetupGet(x => x.TenantId).Returns(_tenantId);
            currentUserMock.SetupGet(x => x.UserName).Returns("test_user");

            var tenantSettingsMock = new Mock<ITenantSettingsService>();
            tenantSettingsMock.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");

            var bookValidationMock = new Mock<IBookValidationService>();
            bookValidationMock.Setup(x => x.ResolveTargetBooksAsync(It.IsAny<string>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "Primary" });

            _journalEntryServiceMock = new Mock<IJournalEntryService>();
            _journalEntryServiceMock.Setup(x => x.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });
            _journalEntryServiceMock.Setup(x => x.PostJournalEntryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JournalEntryDto { Id = Guid.NewGuid() });

            _taxAuditServiceMock = new Mock<ITaxAuditService>();

            var postingLoggerMock = new Mock<ILogger<SubledgerPostingService>>();
            var postingService = new SubledgerPostingService(
                financialRepo,
                currentUserMock.Object,
                tenantSettingsMock.Object,
                _journalEntryServiceMock.Object,
                bookValidationMock.Object,
                postingLoggerMock.Object);

            _inventoryValuationMock = new Mock<IInventoryValuationService>();
            _subledgerPostingMock = new Mock<ISubledgerPostingService>();
            _refundPostingMock = new Mock<IRefundPostingService>();
            _inventoryReturnMock = new Mock<IInventoryReturnService>();

            var mockReturnLogger = new Mock<ILogger<ReturnOrderService>>();
            var mockPostingLogger = new Mock<ILogger<CreditNotePostingService>>();

            // Wire them together using Lazy/Mutual reference or mock reference
            // Here, we can create ReturnOrderService and then instantiate CreditNotePostingService
            // and pass the real ReturnOrderService to CreditNotePostingService!
            
            // To pass the real CreditNotePostingService to ReturnOrderService, we need it constructed.
            // We can construct ReturnOrderService first with a mocked ICreditNotePostingService,
            // or construct CreditNotePostingService first with a mocked IReturnOrderService.
            // Let's use a Mock of ICreditNotePostingService that forwards calls to the real one, 
            // or just use a proxy that delegates.
            var creditNotePostingMock = new Mock<ICreditNotePostingService>();

            _sut = new ReturnOrderService(
                roRepo, roLineRepo, cnRepo, cnLineRepo, refundRepo, soLineRepo,
                _inventoryValuationMock.Object,
                _subledgerPostingMock.Object,
                _refundPostingMock.Object,
                unitOfWork,
                _currentUserProviderMock.Object,
                invoiceRepo,
                invoiceLineRepo,
                deliveryNoteRepo,
                salesOrderRepo,
                creditNotePostingMock.Object,
                _inventoryReturnMock.Object,
                mockReturnLogger.Object);

            _creditNotePostingService = new CreditNotePostingService(
                unitOfWork,
                postingService,
                _taxAuditServiceMock.Object,
                currentUserMock.Object,
                mockPostingLogger.Object);

            // Set up mock delegation: when mock post is called, delegate to the real CreditNotePostingService!
            creditNotePostingMock.Setup(x => x.PostCreditNoteAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Returns<Guid, Guid?, CancellationToken>((cnId, invId, ct) => _creditNotePostingService.PostCreditNoteAsync(cnId, invId, ct));
        }

        private async Task SeedDataAsync(Guid bpId)
        {
            var tenant = new Tenant { Id = _tenantId, Name = "Test Tenant", Code = "TT01" };
            _dbContext.Tenants.Add(tenant);

            var currencyBase = new Currency { Id = Guid.NewGuid(), TenantId = _tenantId, CurrencyCode = "GHS", NumericCode = "936", CurrencyName = "Ghana Cedi", IsBaseCurrency = true };
            var currencyForeign = new Currency { Id = Guid.NewGuid(), TenantId = _tenantId, CurrencyCode = "USD", NumericCode = "840", CurrencyName = "US Dollar", IsBaseCurrency = false };
            _dbContext.Currencies.AddRange(currencyBase, currencyForeign);

            var arId = Guid.NewGuid();
            var taxId = Guid.NewGuid();
            var suspenseId = Guid.NewGuid();
            var revId = Guid.NewGuid();

            var settings = new FinanceSettings
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                ControlAccountArId = arId,
                ControlAccountTaxId = taxId,
                SuspenseAccountId = suspenseId,
                SubledgerPostingMode = "Direct"
            };
            _dbContext.FinanceSettings.Add(settings);

            var arAcc = new Account { Id = arId, TenantId = _tenantId, AccountCode = "1200", AccountName = "AR", CurrencyCode = "GHS" };
            var revAcc = new Account { Id = revId, TenantId = _tenantId, AccountCode = "4000", AccountName = "Revenue", CurrencyCode = "GHS" };
            var taxAcc = new Account { Id = taxId, TenantId = _tenantId, AccountCode = "2200", AccountName = "VAT", CurrencyCode = "GHS" };
            var suspAcc = new Account { Id = suspenseId, TenantId = _tenantId, AccountCode = "9999", AccountName = "Suspense", CurrencyCode = "GHS" };
            _dbContext.Accounts.AddRange(arAcc, revAcc, taxAcc, suspAcc);

            var bp = new ErpSystem.Core.Entities.Procurement.BusinessPartner
            {
                Id = bpId,
                TenantId = _tenantId,
                PartnerCode = "C001",
                PartnerName = "Test Customer",
                PartnerType = "Customer",
                Currency = "GHS",
                DefaultArAccountId = arId,
                OutstandingBalance = 1000m,
                CreditLimit = 100000m,
                IsActive = true
            };
            _dbContext.BusinessPartners.Add(bp);
            await _dbContext.SaveChangesAsync();
        }

        [Fact]
        public async Task Return_FromPostedInvoice_ShouldCreateCustomerCreditNote()
        {
            // Arrange
            var bpId = Guid.NewGuid();
            await SeedDataAsync(bpId);

            var soId = Guid.NewGuid();
            var soLineId = Guid.NewGuid();
            var so = new SalesOrder { Id = soId, TenantId = _tenantId, BusinessPartnerId = bpId, DocumentNumber = "SO-001" };
            var soLine = new SalesOrderLine { Id = soLineId, TenantId = _tenantId, SalesOrderId = soId, Quantity = 5, UnitPrice = 100m, Description = "Test Item" };
            _dbContext.Set<SalesOrder>().Add(so);
            _dbContext.Set<SalesOrderLine>().Add(soLine);

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                BusinessPartnerId = bpId,
                InvoiceNumber = "INV-001",
                InvoiceDate = DateTime.UtcNow,
                TotalAmount = 575m, // 500 base + 75 tax
                TaxAmount = 75m,
                PaidAmount = 0m,
                CreditedAmount = 0m,
                CurrencyCode = "GHS",
                ExchangeRate = 1.0m,
                Status = InvoiceStatus.Sent,
                LineItems = new List<InvoiceLineItem>
                {
                    new InvoiceLineItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantId,
                        Description = "Test Item",
                        Quantity = 5,
                        UnitPrice = 100m,
                        TaxAmount = 75m,
                        TaxRate = 15m,
                        TaxCode = "VAT15"
                    }
                }
            };
            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync();

            var dto = new CreateReturnOrderDto
            {
                SalesOrderId = soId,
                InvoiceId = invoice.Id,
                BusinessPartnerId = bpId,
                ReasonCode = ReturnReasonCode.Defective,
                ReasonDescription = "Faulty item",
                Lines = new List<CreateReturnOrderLineDto>
                {
                    new CreateReturnOrderLineDto
                    {
                        SalesOrderLineId = soLineId,
                        InvoiceLineItemId = invoice.LineItems.First().Id,
                        Description = "Test Item",
                        QuantityReturned = 2,
                        UnitPrice = 100m,
                        ReasonCode = ReturnReasonCode.Defective,
                        IsRestockable = true
                    }
                }
            };

            // Act
            var result = await _sut.CreateReturnOrderAsync(dto);

            // Assert Return Order is created in Requested status
            Assert.Equal(ReturnOrderStatus.Requested, result.ReturnStatus);
            Assert.Null(result.CreditNoteId);

            // Act 2: Approve the Return Order
            var approved = await _sut.ApproveReturnOrderAsync(result.Id);

            // Assert Credit Note is generated, marked Applied (posted)
            Assert.Equal(ReturnOrderStatus.CreditIssued, approved.ReturnStatus);
            Assert.NotNull(approved.CreditNoteId);

            var cn = await _dbContext.CreditNotes.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == approved.CreditNoteId);
            Assert.NotNull(cn);
            Assert.Equal(CreditNoteStatus.Applied, cn.CreditNoteStatus);
            Assert.Equal(230m, cn.TotalAmount); // 200 base + 30 proportional tax

            // Assert Invoice balance reduction
            var freshInv = await _dbContext.Invoices.FindAsync(invoice.Id);
            Assert.Equal(230m, freshInv.CreditedAmount);
            Assert.Equal(345m, freshInv.BalanceAmount); // 575 - 230
        }

        [Fact]
        public async Task Return_FromDeliveryWithoutInvoice_ShouldNotReverseARLiability()
        {
            // Arrange
            var bpId = Guid.NewGuid();
            await SeedDataAsync(bpId);

            var soId = Guid.NewGuid();
            var soLineId = Guid.NewGuid();
            var so = new SalesOrder { Id = soId, TenantId = _tenantId, BusinessPartnerId = bpId, DocumentNumber = "SO-002" };
            var soLine = new SalesOrderLine { Id = soLineId, TenantId = _tenantId, SalesOrderId = soId, Quantity = 5, UnitPrice = 100m, Description = "Item 2" };
            _dbContext.Set<SalesOrder>().Add(so);
            _dbContext.Set<SalesOrderLine>().Add(soLine);

            var dn = new DeliveryNote
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                SalesOrderId = soId,
                DocumentNumber = "DN-002",
                Status = "Delivered"
            };
            _dbContext.Set<DeliveryNote>().Add(dn);
            await _dbContext.SaveChangesAsync();

            var dto = new CreateReturnOrderDto
            {
                SalesOrderId = soId,
                DeliveryNoteId = dn.Id,
                BusinessPartnerId = bpId,
                ReasonCode = ReturnReasonCode.CustomerChanged,
                ReasonDescription = "No longer needed",
                Lines = new List<CreateReturnOrderLineDto>
                {
                    new CreateReturnOrderLineDto
                    {
                        SalesOrderLineId = soLineId,
                        Description = "Item 2",
                        QuantityReturned = 3,
                        UnitPrice = 100m,
                        IsRestockable = true
                    }
                }
            };

            // Act
            var result = await _sut.CreateReturnOrderAsync(dto);
            var approved = await _sut.ApproveReturnOrderAsync(result.Id);

            // Assert: Delivery-only return transitioned to Approved, NOT CreditIssued, and NO credit note created
            Assert.Equal(ReturnOrderStatus.Approved, approved.ReturnStatus);
            Assert.Null(approved.CreditNoteId);

            // Assert: No change to BP OutstandingBalance
            var bp = await _dbContext.BusinessPartners.FindAsync(bpId);
            Assert.Equal(1000m, bp.OutstandingBalance); // Seed was 1000m
        }

        [Fact]
        public async Task Return_ShouldReverseOriginalTaxProportionally()
        {
            // Arrange
            var bpId = Guid.NewGuid();
            await SeedDataAsync(bpId);

            var soId = Guid.NewGuid();
            var soLineId = Guid.NewGuid();
            var so = new SalesOrder { Id = soId, TenantId = _tenantId, BusinessPartnerId = bpId, DocumentNumber = "SO-003" };
            var soLine = new SalesOrderLine { Id = soLineId, TenantId = _tenantId, SalesOrderId = soId, Quantity = 10, UnitPrice = 50m, Description = "Tax Item" };
            _dbContext.Set<SalesOrder>().Add(so);
            _dbContext.Set<SalesOrderLine>().Add(soLine);

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                BusinessPartnerId = bpId,
                InvoiceNumber = "INV-003",
                InvoiceDate = DateTime.UtcNow,
                TotalAmount = 550m, // 500 base + 50 tax (10%)
                TaxAmount = 50m,
                CurrencyCode = "GHS",
                Status = InvoiceStatus.Sent,
                LineItems = new List<InvoiceLineItem>
                {
                    new InvoiceLineItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantId,
                        Description = "Tax Item",
                        Quantity = 10,
                        UnitPrice = 50m,
                        TaxAmount = 50m,
                        TaxRate = 10m,
                        TaxCode = "VAT10"
                    }
                }
            };
            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync();

            var dto = new CreateReturnOrderDto
            {
                SalesOrderId = soId,
                InvoiceId = invoice.Id,
                BusinessPartnerId = bpId,
                Lines = new List<CreateReturnOrderLineDto>
                {
                    new CreateReturnOrderLineDto
                    {
                        SalesOrderLineId = soLineId,
                        InvoiceLineItemId = invoice.LineItems.First().Id,
                        Description = "Tax Item",
                        QuantityReturned = 4, // 40% of the quantity
                        UnitPrice = 50m
                    }
                }
            };

            // Act
            var result = await _sut.CreateReturnOrderAsync(dto);

            // Assert: Tax amount is exactly 40% of 50m = 20m
            Assert.Equal(220m, result.TotalAmount); // 200 base + 20 tax
        }

        [Fact]
        public async Task Return_ShouldPreventDoubleReturn()
        {
            // Arrange
            var bpId = Guid.NewGuid();
            await SeedDataAsync(bpId);

            var soId = Guid.NewGuid();
            var soLineId = Guid.NewGuid();
            var so = new SalesOrder { Id = soId, TenantId = _tenantId, BusinessPartnerId = bpId, DocumentNumber = "SO-004" };
            var soLine = new SalesOrderLine { Id = soLineId, TenantId = _tenantId, SalesOrderId = soId, Quantity = 5, UnitPrice = 100m, Description = "Limited Item" };
            _dbContext.Set<SalesOrder>().Add(so);
            _dbContext.Set<SalesOrderLine>().Add(soLine);

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                BusinessPartnerId = bpId,
                InvoiceNumber = "INV-004",
                InvoiceDate = DateTime.UtcNow,
                TotalAmount = 500m,
                CurrencyCode = "GHS",
                Status = InvoiceStatus.Sent,
                LineItems = new List<InvoiceLineItem>
                {
                    new InvoiceLineItem { Id = Guid.NewGuid(), TenantId = _tenantId, Description = "Limited Item", Quantity = 5, UnitPrice = 100m }
                }
            };
            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync();

            // First Return Order of 3 items
            var dto1 = new CreateReturnOrderDto
            {
                SalesOrderId = soId,
                InvoiceId = invoice.Id,
                BusinessPartnerId = bpId,
                Lines = new List<CreateReturnOrderLineDto>
                {
                    new CreateReturnOrderLineDto
                    {
                        SalesOrderLineId = soLineId,
                        InvoiceLineItemId = invoice.LineItems.First().Id,
                        Description = "Limited Item",
                        QuantityReturned = 3,
                        UnitPrice = 100m
                    }
                }
            };
            var result1 = await _sut.CreateReturnOrderAsync(dto1);
            await _sut.ApproveReturnOrderAsync(result1.Id); // Counts towards PreviouslyReturnedQuantity

            // Second Return Order of 3 items (Total = 6 > 5) -> Should throw ArgumentException
            var dto2 = new CreateReturnOrderDto
            {
                SalesOrderId = soId,
                InvoiceId = invoice.Id,
                BusinessPartnerId = bpId,
                Lines = new List<CreateReturnOrderLineDto>
                {
                    new CreateReturnOrderLineDto
                    {
                        SalesOrderLineId = soLineId,
                        InvoiceLineItemId = invoice.LineItems.First().Id,
                        Description = "Limited Item",
                        QuantityReturned = 3,
                        UnitPrice = 100m
                    }
                }
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateReturnOrderAsync(dto2));
        }

        [Fact]
        public async Task Return_ShouldUseOriginalInvoiceExchangeRate()
        {
            // Arrange
            var bpId = Guid.NewGuid();
            await SeedDataAsync(bpId);

            var soId = Guid.NewGuid();
            var so = new SalesOrder { Id = soId, TenantId = _tenantId, BusinessPartnerId = bpId, DocumentNumber = "SO-005" };
            _dbContext.Set<SalesOrder>().Add(so);

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                BusinessPartnerId = bpId,
                InvoiceNumber = "INV-005",
                InvoiceDate = DateTime.UtcNow,
                TotalAmount = 100m,
                CurrencyCode = "USD",
                ExchangeRate = 12.5m, // Locked historic exchange rate
                Status = InvoiceStatus.Sent,
                LineItems = new List<InvoiceLineItem>
                {
                    new InvoiceLineItem { Id = Guid.NewGuid(), TenantId = _tenantId, Description = "USD Item", Quantity = 1, UnitPrice = 100m }
                }
            };
            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync();

            var dto = new CreateReturnOrderDto
            {
                SalesOrderId = soId,
                InvoiceId = invoice.Id,
                BusinessPartnerId = bpId,
                Lines = new List<CreateReturnOrderLineDto>
                {
                    new CreateReturnOrderLineDto
                    {
                        InvoiceLineItemId = invoice.LineItems.First().Id,
                        Description = "USD Item",
                        QuantityReturned = 1,
                        UnitPrice = 100m
                    }
                }
            };

            // Act
            var result = await _sut.CreateReturnOrderAsync(dto);

            // Assert exchange rate is locked to original invoice
            Assert.Equal("USD", result.Currency);
            Assert.Equal(12.5m, result.ExchangeRate);
        }

        [Fact]
        public async Task ApprovedCustomerReturn_ShouldRejectFurtherLineChanges()
        {
            // Arrange
            var bpId = Guid.NewGuid();
            await SeedDataAsync(bpId);

            var roId = Guid.NewGuid();
            var ro = new ReturnOrder
            {
                Id = roId,
                TenantId = _tenantId,
                BusinessPartnerId = bpId,
                SalesOrderId = Guid.NewGuid(),
                ReturnStatus = ReturnOrderStatus.CreditIssued, // Non-draft/Requested status
                DocumentNumber = "RO-IMMUTABLE"
            };
            _dbContext.ReturnOrders.Add(ro);
            await _dbContext.SaveChangesAsync();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ApproveReturnOrderAsync(roId));
            await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RejectReturnOrderAsync(roId, "Frozen"));
        }

        [Fact]
        public async Task Return_FromDeliveryWithoutInvoice_ShouldCallInventoryReturnBoundary()
        {
            // Arrange
            var bpId = Guid.NewGuid();
            await SeedDataAsync(bpId);

            var soId = Guid.NewGuid();
            var soLineId = Guid.NewGuid();
            var invItemId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();
            var so = new SalesOrder { Id = soId, TenantId = _tenantId, BusinessPartnerId = bpId, DocumentNumber = "SO-006" };
            var soLine = new SalesOrderLine 
            { 
                Id = soLineId, 
                TenantId = _tenantId, 
                SalesOrderId = soId, 
                Quantity = 5, 
                UnitPrice = 100m, 
                Description = "Stock Item",
                InventoryItemId = invItemId,
                WarehouseId = warehouseId
            };
            _dbContext.Set<SalesOrder>().Add(so);
            _dbContext.Set<SalesOrderLine>().Add(soLine);

            var dn = new DeliveryNote
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                SalesOrderId = soId,
                DocumentNumber = "DN-006",
                Status = "Delivered"
            };
            _dbContext.Set<DeliveryNote>().Add(dn);
            await _dbContext.SaveChangesAsync();

            var dto = new CreateReturnOrderDto
            {
                SalesOrderId = soId,
                DeliveryNoteId = dn.Id,
                BusinessPartnerId = bpId,
                Lines = new List<CreateReturnOrderLineDto>
                {
                    new CreateReturnOrderLineDto
                    {
                        SalesOrderLineId = soLineId,
                        Description = "Stock Item",
                        QuantityReturned = 2,
                        UnitPrice = 100m,
                        IsRestockable = true
                    }
                }
            };

            // Act
            var result = await _sut.CreateReturnOrderAsync(dto);
            await _sut.ApproveReturnOrderAsync(result.Id);
            
            // Act 2: Receive the return order
            await _sut.ReceiveReturnOrderAsync(result.Id);

            // Assert: process stock returns called the boundary service rather than directly modifying DB
            _inventoryReturnMock.Verify(x => x.ProcessCustomerReturnAsync(
                _tenantId,
                result.Id,
                It.Is<IEnumerable<FinanceReceiptInventoryLine>>(lines => lines.Count() == 1 && lines.First().QuantityReceived == 2m)),
                Times.Once);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Dispose();
        }
    }
}
