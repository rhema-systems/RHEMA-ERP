using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
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
    public class VendorInvoiceServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
        private readonly Guid _tenantId = Guid.NewGuid();

        private readonly VendorInvoiceService _sut;
        
        private readonly Mock<ISubledgerPostingService> _subledgerPostingMock;
        private readonly Mock<IInventoryValuationService> _inventoryValuationMock;
        private readonly Mock<ICurrentUserService> _currentUserMock;
        
        public VendorInvoiceServiceTests()
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
            
            _currentUserMock = new Mock<ICurrentUserService>();
            _currentUserMock.SetupGet(x => x.TenantId).Returns(_tenantId);
            _currentUserMock.SetupGet(x => x.UserName).Returns("test_user");

            _subledgerPostingMock = new Mock<ISubledgerPostingService>();
            _inventoryValuationMock = new Mock<IInventoryValuationService>();
            
            var taxEngineMock = new Mock<ITaxCalculationEngine>();
            var taxAuditServiceMock = new Mock<ITaxAuditService>();
            var tenantSettingsMock = new Mock<ITenantSettingsService>();
            tenantSettingsMock.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
            var loggerMock = new Mock<ILogger<VendorInvoiceService>>();

            _sut = new VendorInvoiceService(
                unitOfWork,
                _currentUserMock.Object,
                _subledgerPostingMock.Object,
                _inventoryValuationMock.Object,
                taxEngineMock.Object,
                taxAuditServiceMock.Object,
                tenantSettingsMock.Object,
                loggerMock.Object);
        }

        private async Task<(Guid bpId, Guid poId, Guid receiptId)> SeedMatchingDataAsync()
        {
            var tenant = new Tenant { Id = _tenantId, Name = "Test", Code = "TT01" };
            if (!await _dbContext.Tenants.AnyAsync()) _dbContext.Tenants.Add(tenant);
            
            var bp = new BusinessPartner
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                PartnerCode = "V001",
                PartnerName = "Test Vendor",
                PartnerType = "Supplier",
                Currency = "GHS",
                IsActive = true
            };
            _dbContext.BusinessPartners.Add(bp);

            var po = new PurchaseOrder
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                OrderNumber = "PO-1001",
                BusinessPartnerId = bp.Id,
                Status = "Approved",
                TotalAmount = 1000m,
                Currency = "GHS",
                ExchangeRate = 1m
            };
            
            var poLine = new PurchaseOrderItem
            {
                Id = Guid.NewGuid(),
                    TenantId = _tenantId,
                    PurchaseOrderId = po.Id,
                    ItemDescription = "Test Item",
                    OrderedQuantity = 10m,
                    LineTotal = 1000m
            };
            po.Items = new List<PurchaseOrderItem> { poLine };
            _dbContext.PurchaseOrders.Add(po);

            var receipt = new PurchaseOrderReceipt
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                PurchaseOrderId = po.Id,
                ReceiptNumber = "GR-1001",
                Status = "Completed"
            };
            var receiptLine = new PurchaseOrderReceiptItem
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                ReceiptId = receipt.Id,
                PurchaseOrderItemId = poLine.Id,
                ReceivedQuantity = 10,
                AcceptedQuantity = 10
            };
            receipt.Items = new List<PurchaseOrderReceiptItem> { receiptLine };
            _dbContext.Set<PurchaseOrderReceipt>().Add(receipt);

            await _dbContext.SaveChangesAsync();
            return (bp.Id, po.Id, receipt.Id);
        }

        [Fact]
        public async Task VendorInvoice_Create_ShouldPassTwoWayMatch_WhenInvoiceMatchesPO()
        {
            // Arrange
            var data = await SeedMatchingDataAsync();
            var poItem = await _dbContext.PurchaseOrderItems.FirstAsync(i => i.PurchaseOrderId == data.poId);

            var createDto = new VendorInvoiceCreateDto
            {
                BusinessPartnerId = data.bpId,
                PurchaseOrderId = data.poId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1m,
                MatchingType = InvoiceMatchingType.TwoWay,
                LineItems = new List<VendorInvoiceLineItemCreateDto>
                {
                    new VendorInvoiceLineItemCreateDto
                    {
                        Description = "Item 1",
                        Quantity = 10,
                        UnitPrice = 100m, // Matches PO
                        PurchaseOrderItemId = poItem.Id
                    }
                }
            };

            var created = await _sut.CreateAsync(createDto);

            // Act
            var matchResult = await _sut.PerformTwoWayMatchAsync(created.Id);

            // Assert
            Assert.True(matchResult.IsMatched);
            Assert.Equal(InvoiceMatchingStatus.TwoWayMatched, matchResult.MatchingStatus);
            Assert.Empty(matchResult.Discrepancies);
        }

        [Fact]
        public async Task VendorInvoice_Create_ShouldFailTwoWayMatch_WhenInvoiceExceedsPOTolerance()
        {
            // Arrange
            var data = await SeedMatchingDataAsync();
            var poItem = await _dbContext.PurchaseOrderItems.FirstAsync(i => i.PurchaseOrderId == data.poId);

            var createDto = new VendorInvoiceCreateDto
            {
                BusinessPartnerId = data.bpId,
                PurchaseOrderId = data.poId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1m,
                MatchingType = InvoiceMatchingType.TwoWay,
                LineItems = new List<VendorInvoiceLineItemCreateDto>
                {
                    new VendorInvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        Description = "Item 1",
                        Quantity = 10,
                        UnitPrice = 115m, // Exceeds PO unit price
                        PurchaseOrderItemId = poItem.Id
                    }
                }
            };

            var created = await _sut.CreateAsync(createDto);

            // Act
            var matchResult = await _sut.PerformTwoWayMatchAsync(created.Id);

            // Assert
            Assert.False(matchResult.IsMatched);
            Assert.Equal(InvoiceMatchingStatus.MatchException, matchResult.MatchingStatus);
            Assert.NotEmpty(matchResult.Discrepancies);
            Assert.Contains(matchResult.Discrepancies, d => d.DiscrepancyType == "Price");
        }

        [Fact]
        public async Task VendorInvoice_Create_ShouldPassThreeWayMatch_WhenInvoiceMatchesPOAndGoodsReceipt()
        {
            // Arrange
            var data = await SeedMatchingDataAsync();
            var poItem = await _dbContext.PurchaseOrderItems.FirstAsync(i => i.PurchaseOrderId == data.poId);

            var createDto = new VendorInvoiceCreateDto
            {
                BusinessPartnerId = data.bpId,
                PurchaseOrderId = data.poId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1m,
                MatchingType = InvoiceMatchingType.ThreeWay,
                LineItems = new List<VendorInvoiceLineItemCreateDto>
                {
                    new VendorInvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        Description = "Item 1",
                        Quantity = 10, // Matches PO and Receipt
                        UnitPrice = 100m,
                        PurchaseOrderItemId = poItem.Id
                    }
                }
            };

            var created = await _sut.CreateAsync(createDto);

            // Act
            var matchResult = await _sut.PerformThreeWayMatchAsync(created.Id);

            // Assert
            Assert.True(matchResult.IsMatched);
            Assert.Equal(InvoiceMatchingStatus.ThreeWayMatched, matchResult.MatchingStatus);
            Assert.Empty(matchResult.Discrepancies);
        }

        [Fact]
        public async Task VendorInvoice_Create_ShouldFailThreeWayMatch_WhenQuantityOrAmountMismatch()
        {
            // Arrange
            var data = await SeedMatchingDataAsync();
            var poItem = await _dbContext.PurchaseOrderItems.FirstAsync(i => i.PurchaseOrderId == data.poId);

            var createDto = new VendorInvoiceCreateDto
            {
                BusinessPartnerId = data.bpId,
                PurchaseOrderId = data.poId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1m,
                MatchingType = InvoiceMatchingType.ThreeWay,
                LineItems = new List<VendorInvoiceLineItemCreateDto>
                {
                    new VendorInvoiceLineItemCreateDto
                    {
                        LineItemType = "Product",
                        Description = "Item 1",
                        Quantity = 12, // Exceeds receipt quantity (10)
                        UnitPrice = 100m,
                        PurchaseOrderItemId = poItem.Id
                    }
                }
            };

            var created = await _sut.CreateAsync(createDto);

            // Act
            var matchResult = await _sut.PerformThreeWayMatchAsync(created.Id);

            // Assert
            Assert.False(matchResult.IsMatched);
            Assert.Equal(InvoiceMatchingStatus.MatchException, matchResult.MatchingStatus);
            Assert.NotEmpty(matchResult.Discrepancies);
            Assert.Contains(matchResult.Discrepancies, d => d.DiscrepancyType == "Quantity");
        }

        [Fact]
        public async Task VendorInvoice_Approve_ShouldCreateAPOpenItem()
        {
            // Arrange
            var data = await SeedMatchingDataAsync();

            var createDto = new VendorInvoiceCreateDto
            {
                BusinessPartnerId = data.bpId,
                InvoiceDate = DateTime.UtcNow,
                CurrencyCode = "GHS",
                ExchangeRate = 1m,
                LineItems = new List<VendorInvoiceLineItemCreateDto>
                {
                    new VendorInvoiceLineItemCreateDto
                    {
                        LineItemType = "Service",
                        Description = "Consulting",
                        Quantity = 1,
                        UnitPrice = 500m
                    }
                }
            };
            var created = await _sut.CreateAsync(createDto);

            // Act
            await _sut.SubmitForApprovalAsync(created.Id);
            await _sut.ApproveAsync(created.Id);

            // Assert
            var approvedInvoice = await _dbContext.VendorInvoices.FindAsync(created.Id);
            Assert.NotNull(approvedInvoice);
            Assert.Equal(VendorInvoiceStatus.Approved, approvedInvoice.Status);
            Assert.Equal(500m, approvedInvoice.TotalAmount);
            Assert.Equal(500m, approvedInvoice.BalanceAmount);
            Assert.Equal(0m, approvedInvoice.PaidAmount);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Dispose();
        }
    }
}
