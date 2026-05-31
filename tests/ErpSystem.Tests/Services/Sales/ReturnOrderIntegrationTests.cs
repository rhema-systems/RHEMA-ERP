using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Services.Sales;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Tests.Services.Sales
{
    public class ReturnOrderIntegrationTests : IDisposable
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
        private readonly Guid _tenantId = Guid.NewGuid();

        // System Under Test
        private readonly ReturnOrderService _sut;
        
        // Mocks
        private readonly Mock<IInventoryValuationService> _inventoryValuationMock;
        private readonly Mock<ISubledgerPostingService> _subledgerPostingMock;
        private readonly Mock<IInventoryReturnService> _inventoryReturnMock;
        
        public ReturnOrderIntegrationTests()
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
            var roRepo = new GenericRepository<ReturnOrder>(_dbContext);
            var roLineRepo = new GenericRepository<ReturnOrderLine>(_dbContext);
            var cnRepo = new GenericRepository<CreditNote>(_dbContext);
            var cnLineRepo = new GenericRepository<CreditNoteLine>(_dbContext);
            var refundRepo = new GenericRepository<Refund>(_dbContext);
            var soLineRepo = new GenericRepository<SalesOrderLine>(_dbContext);
            
            // Added dependencies
            var invoiceRepo = new GenericRepository<Invoice>(_dbContext);
            var invoiceLineRepo = new GenericRepository<InvoiceLineItem>(_dbContext);
            var deliveryNoteRepo = new GenericRepository<DeliveryNote>(_dbContext);
            var salesOrderRepo = new GenericRepository<SalesOrder>(_dbContext);
            var creditNotePostingMock = new Mock<ICreditNotePostingService>();
            
            var currentUserMock = new Mock<ICurrentUserProvider>();
            currentUserMock.SetupGet(x => x.TenantId).Returns(_tenantId);
            currentUserMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());

            _inventoryValuationMock = new Mock<IInventoryValuationService>();
            _subledgerPostingMock = new Mock<ISubledgerPostingService>();
            _inventoryReturnMock = new Mock<IInventoryReturnService>();
            var refundPostingMock = new Mock<IRefundPostingService>();

            var loggerMock = new Mock<ILogger<ReturnOrderService>>();

            _sut = new ReturnOrderService(
                roRepo, roLineRepo, cnRepo, cnLineRepo, refundRepo, soLineRepo,
                _inventoryValuationMock.Object,
                _subledgerPostingMock.Object,
                refundPostingMock.Object,
                unitOfWork,
                currentUserMock.Object,
                invoiceRepo,
                invoiceLineRepo,
                deliveryNoteRepo,
                salesOrderRepo,
                creditNotePostingMock.Object,
                _inventoryReturnMock.Object,
                loggerMock.Object);

        }

        [Fact]
        public async Task ReceiveReturnOrder_RestoresRestockableInventory_And_ReversesCOGS()
        {
            // T10 and T11
            var bpId = Guid.NewGuid();
            var soId = Guid.NewGuid();
            var soLineId = Guid.NewGuid();
            var invItemId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();

            var bp = new ErpSystem.Core.Entities.Procurement.BusinessPartner
            {
                Id = bpId,
                TenantId = _tenantId,
                PartnerCode = "C001",
                PartnerName = "Test Customer",
                PartnerType = "Customer"
            };
            _dbContext.Set<ErpSystem.Core.Entities.Procurement.BusinessPartner>().Add(bp);

            var so = new SalesOrder
            {
                Id = soId,
                TenantId = _tenantId,
                BusinessPartnerId = bpId,
                DocumentNumber = $"SO-{DateTime.UtcNow.Ticks}"
            };
            _dbContext.Set<SalesOrder>().Add(so);

            var soLine = new SalesOrderLine
            {
                Id = soLineId,
                TenantId = _tenantId,
                SalesOrderId = soId,
                InventoryItemId = invItemId,
                WarehouseId = warehouseId,
                Quantity = 5,
                UnitPrice = 100m,
                UnitCost = 80m,
                Description = "Item"
            };
            _dbContext.Set<SalesOrderLine>().Add(soLine);

            var roId = Guid.NewGuid();
            var ro = new ReturnOrder
            {
                Id = roId,
                TenantId = _tenantId,
                BusinessPartnerId = bpId,
                DocumentNumber = $"RO-{DateTime.UtcNow.Ticks}",
                SalesOrderId = soId,
                InvoiceId = Guid.NewGuid(),
                ReturnStatus = ReturnOrderStatus.Approved,
                Lines = new List<ReturnOrderLine>
                {
                    new ReturnOrderLine
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantId,
                        SalesOrderLineId = soLineId,
                        QuantityReturned = 2,
                        UnitPrice = 100m,
                        IsRestockable = true,
                        Description = "Item"
                    }
                }
            };
            _dbContext.Set<ReturnOrder>().Add(ro);
            await _dbContext.SaveChangesAsync();

            // Act
            await _sut.ReceiveReturnOrderAsync(roId);

            // Assert
            _inventoryReturnMock.Verify(x => x.ProcessCustomerReturnAsync(
                _tenantId,
                roId,
                It.Is<IEnumerable<FinanceReceiptInventoryLine>>(lines => lines.Count() == 1 && lines.First().QuantityReceived == 2m)),
                Times.Once);

            _subledgerPostingMock.Verify(x => x.PostReturnOrderCOGSGLAsync(roId, It.IsAny<CancellationToken>()), Times.Once);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Dispose();
        }
    }
}
