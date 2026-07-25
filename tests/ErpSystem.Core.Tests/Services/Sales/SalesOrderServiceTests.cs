using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Services.Sales;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using System.Linq.Expressions;

namespace ErpSystem.Core.Tests.Services.Sales
{
    public class SalesOrderServiceTests
    {
        private readonly Mock<IGenericRepository<SalesOrder>> _salesOrderRepoMock;
        private readonly Mock<IGenericRepository<SalesOrderLine>> _lineRepoMock;
        private readonly Mock<IGenericRepository<SalesOrderStatusHistory>> _historyRepoMock;
        private readonly Mock<IGenericRepository<BusinessPartner>> _bpRepoMock;
        private readonly Mock<IGenericRepository<Quote>> _quoteRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ICurrentUserProvider> _currentUserProviderMock;
        private readonly Mock<IInventoryManagementService> _inventoryServiceMock;
        private readonly Mock<IInvoiceService> _invoiceServiceMock;
        private readonly Mock<ITaxCalculationEngine> _taxEngineMock;
        private readonly SalesOrderService _sut;

        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly Guid _bpId = Guid.NewGuid();

        public SalesOrderServiceTests()
        {
            _salesOrderRepoMock = new Mock<IGenericRepository<SalesOrder>>();
            _lineRepoMock = new Mock<IGenericRepository<SalesOrderLine>>();
            _historyRepoMock = new Mock<IGenericRepository<SalesOrderStatusHistory>>();
            _bpRepoMock = new Mock<IGenericRepository<BusinessPartner>>();
            _quoteRepoMock = new Mock<IGenericRepository<Quote>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _currentUserProviderMock = new Mock<ICurrentUserProvider>();
            _inventoryServiceMock = new Mock<IInventoryManagementService>();
            _invoiceServiceMock = new Mock<IInvoiceService>();
            _taxEngineMock = new Mock<ITaxCalculationEngine>();

            _currentUserProviderMock.Setup(x => x.UserId).Returns(_userId);
            _currentUserProviderMock.Setup(x => x.TenantId).Returns(_tenantId);

            // Setup basic validations
            _bpRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Expression<Func<BusinessPartner, object>>[]>()))
                .ReturnsAsync(new BusinessPartner { Id = _bpId, CreditLimit = 100000, OutstandingBalance = 0 });
            _bpRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new BusinessPartner { Id = _bpId, CreditLimit = 100000, OutstandingBalance = 0 });

            _sut = new SalesOrderService(
                _salesOrderRepoMock.Object,
                _lineRepoMock.Object,
                _historyRepoMock.Object,
                _bpRepoMock.Object,
                _quoteRepoMock.Object,
                _unitOfWorkMock.Object,
                _currentUserProviderMock.Object,
                new NullLogger<SalesOrderService>(),
                _inventoryServiceMock.Object,
                _invoiceServiceMock.Object,
                _taxEngineMock.Object);
        }

        private SalesOrder CreateTestSalesOrder(SalesOrderStatus status, bool withStockLines = true, bool withServiceLines = false)
        {
            var soId = Guid.NewGuid();
            var so = new SalesOrder
            {
                Id = soId,
                TenantId = _tenantId,
                DocumentNumber = "SO-123",
                BusinessPartnerId = _bpId,
                OrderStatus = status,
                TotalAmount = 500, // Important for credit limit validation
                Lines = new List<SalesOrderLine>()
            };

            if (withStockLines)
            {
                so.Lines.Add(new SalesOrderLine
                {
                    Id = Guid.NewGuid(),
                    SalesOrderId = soId,
                    InventoryItemId = Guid.NewGuid(),
                    Quantity = 5,
                    UnitPrice = 100,
                    DeliveredQuantity = 0,
                    IsStockReserved = status == SalesOrderStatus.Confirmed,
                    ReservedQuantity = status == SalesOrderStatus.Confirmed ? 5 : 0
                });
            }

            if (withServiceLines)
            {
                so.Lines.Add(new SalesOrderLine
                {
                    Id = Guid.NewGuid(),
                    SalesOrderId = soId,
                    InventoryItemId = null,
                    ProductId = Guid.NewGuid(),
                    Quantity = 2,
                    UnitPrice = 50,
                    DeliveredQuantity = 0,
                    IsStockReserved = false,
                    ReservedQuantity = 0
                });
            }

            // GetByIdAsync with includes uses params Expression<Func<SalesOrder, object>>[]
            _salesOrderRepoMock.Setup(r => r.GetByIdAsync(soId, It.IsAny<Expression<Func<SalesOrder, object>>[]>()))
                .ReturnsAsync(so);
                
            // Also setup simple GetByIdAsync
            _salesOrderRepoMock.Setup(r => r.GetByIdAsync(soId))
                .ReturnsAsync(so);

            return so;
        }

        [Fact]
        public async Task ConfirmSalesOrder_ShouldReserveInventory_ForStockControlledLines()
        {
            // Arrange
            var so = CreateTestSalesOrder(SalesOrderStatus.PendingApproval);

            // Setup inventory success
            _inventoryServiceMock.Setup(s => s.AllocateForSalesOrderAsync(It.IsAny<AllocateInventoryDto>()))
                .ReturnsAsync(new InventoryAllocationDto());

            // Act
            await _sut.ConfirmSalesOrderAsync(so.Id);

            // Assert
            so.OrderStatus.Should().Be(SalesOrderStatus.Confirmed);
            so.Lines.First().IsStockReserved.Should().BeTrue();
            so.Lines.First().ReservedQuantity.Should().Be(5);
            
            _inventoryServiceMock.Verify(s => s.AllocateForSalesOrderAsync(It.Is<AllocateInventoryDto>(
                dto => dto.InventoryItemId == so.Lines.First().InventoryItemId && 
                       dto.Quantity == 5 && 
                       dto.ReferenceId == so.Id
            )), Times.Once);

            _salesOrderRepoMock.Verify(r => r.UpdateAsync(so), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ConfirmSalesOrder_ShouldFail_WhenInsufficientAvailableStock()
        {
            // Arrange
            var so = CreateTestSalesOrder(SalesOrderStatus.PendingApproval);

            // Setup inventory failure (exception thrown by strict reservation)
            _inventoryServiceMock.Setup(s => s.AllocateForSalesOrderAsync(It.IsAny<AllocateInventoryDto>()))
                .ThrowsAsync(new InvalidOperationException("Insufficient stock"));

            // Act
            var act = async () => await _sut.ConfirmSalesOrderAsync(so.Id);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Insufficient stock");
            so.Lines.First().IsStockReserved.Should().BeFalse();
            
            _salesOrderRepoMock.Verify(r => r.UpdateAsync(so), Times.Never);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CancelSalesOrder_ShouldReleaseInventoryReservation()
        {
            // Arrange
            var so = CreateTestSalesOrder(SalesOrderStatus.Confirmed);
            var allocationId = Guid.NewGuid();

            _inventoryServiceMock.Setup(s => s.GetAllocationsByReferenceAsync(so.Id))
                .ReturnsAsync(new List<InventoryAllocationDto> 
                { 
                    new InventoryAllocationDto { Id = allocationId, ReferenceNumber = so.Id.ToString(), RemainingQuantity = 5 } 
                });

            // Act
            var dto = new CancelSalesOrderDto { Reason = "Customer request" };
            await _sut.CancelSalesOrderAsync(so.Id, dto);

            // Assert
            so.OrderStatus.Should().Be(SalesOrderStatus.Cancelled);
            _inventoryServiceMock.Verify(s => s.ReleaseAllocationAsync(allocationId, _userId), Times.Once);
            _salesOrderRepoMock.Verify(r => r.UpdateAsync(so), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task DeliverSalesOrder_ShouldConsumeInventoryAndGenerateInvoice_InTransaction()
        {
            // Arrange
            var so = CreateTestSalesOrder(SalesOrderStatus.Confirmed, withStockLines: true, withServiceLines: true);
            var allocationId = Guid.NewGuid();
            var generatedInvoiceId = Guid.NewGuid();

            _inventoryServiceMock.Setup(s => s.GetAllocationsByReferenceAsync(so.Id))
                .ReturnsAsync(new List<InventoryAllocationDto> 
                { 
                    new InventoryAllocationDto { Id = allocationId, ReferenceNumber = so.Id.ToString(), RemainingQuantity = 5 } 
                });

            _invoiceServiceMock.Setup(s => s.CreateAsync(It.IsAny<InvoiceCreateDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InvoiceDto { Id = generatedInvoiceId });

            // Act
            await _sut.DeliverSalesOrderAsync(so.Id);

            // Assert
            // 1. Transaction Boundaries
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);

            // 2. Inventory Consumption
            _inventoryServiceMock.Verify(s => s.ConsumeAllocatedInventoryAsync(allocationId, 5, _userId), Times.Once);

            // 3. Line Updates
            so.Lines.First(l => l.InventoryItemId.HasValue).DeliveredQuantity.Should().Be(5);
            so.Lines.First(l => l.InventoryItemId.HasValue).IsStockReserved.Should().BeFalse();

            // 4. Invoice Generation
            _invoiceServiceMock.Verify(s => s.CreateAsync(It.Is<InvoiceCreateDto>(dto => 
                dto.Reference == so.DocumentNumber &&
                dto.LineItems.Count == 2 && // Stock and service lines included
                dto.LineItems.Any(l => l.LineItemType == "Product") &&
                dto.LineItems.Any(l => l.LineItemType == "Service")
            ), It.IsAny<CancellationToken>()), Times.Once);

            // 5. SO Updates
            so.OrderStatus.Should().Be(SalesOrderStatus.Delivered);
            so.InvoiceId.Should().Be(generatedInvoiceId);
        }

        [Fact]
        public async Task DeliverSalesOrder_ShouldNotGenerateDuplicateInvoice()
        {
            // Arrange
            var so = CreateTestSalesOrder(SalesOrderStatus.Confirmed);
            so.InvoiceId = Guid.NewGuid(); // Already invoiced
            
            var allocationId = Guid.NewGuid();
            _inventoryServiceMock.Setup(s => s.GetAllocationsByReferenceAsync(so.Id))
                .ReturnsAsync(new List<InventoryAllocationDto> 
                { 
                    new InventoryAllocationDto { Id = allocationId, ReferenceNumber = so.Id.ToString(), RemainingQuantity = 5 } 
                });

            // Act
            await _sut.DeliverSalesOrderAsync(so.Id);

            // Assert
            _invoiceServiceMock.Verify(s => s.CreateAsync(It.IsAny<InvoiceCreateDto>(), It.IsAny<CancellationToken>()), Times.Never);
            _inventoryServiceMock.Verify(s => s.ConsumeAllocatedInventoryAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<Guid>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task DeliverSalesOrder_ShouldRollbackInventoryConsumption_IfInvoiceCreationFails()
        {
            // Arrange
            var so = CreateTestSalesOrder(SalesOrderStatus.Confirmed);
            var allocationId = Guid.NewGuid();

            _inventoryServiceMock.Setup(s => s.GetAllocationsByReferenceAsync(so.Id))
                .ReturnsAsync(new List<InventoryAllocationDto> 
                { 
                    new InventoryAllocationDto { Id = allocationId, ReferenceNumber = so.Id.ToString(), RemainingQuantity = 5 } 
                });

            _invoiceServiceMock.Setup(s => s.CreateAsync(It.IsAny<InvoiceCreateDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Finance DB offline"));

            // Act
            var act = async () => await _sut.DeliverSalesOrderAsync(so.Id);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Finance DB offline");

            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DeliverSalesOrder_ShouldSkipInventoryReservation_ForServiceLines()
        {
            // Arrange
            var so = CreateTestSalesOrder(SalesOrderStatus.Confirmed, withStockLines: false, withServiceLines: true);
            
            // Return empty allocations since there are no stock lines
            _inventoryServiceMock.Setup(s => s.GetAllocationsByReferenceAsync(so.Id))
                .ReturnsAsync(new List<InventoryAllocationDto>());
            
            _invoiceServiceMock.Setup(s => s.CreateAsync(It.IsAny<InvoiceCreateDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InvoiceDto { Id = Guid.NewGuid() });

            // Act
            await _sut.DeliverSalesOrderAsync(so.Id);

            // Assert
            _inventoryServiceMock.Verify(s => s.ConsumeAllocatedInventoryAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<Guid>()), Times.Never);
            
            _invoiceServiceMock.Verify(s => s.CreateAsync(It.Is<InvoiceCreateDto>(dto => 
                dto.LineItems.Count == 1 && dto.LineItems.First().LineItemType == "Service"
            ), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeliverSalesOrder_ShouldFail_IfSalesOrderIsNotConfirmed()
        {
            // Arrange
            var so = CreateTestSalesOrder(SalesOrderStatus.Draft);

            // Act
            var act = async () => await _sut.DeliverSalesOrderAsync(so.Id);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Cannot deliver Sales Order in Draft status. Order must be Confirmed.");
            
            _invoiceServiceMock.Verify(s => s.CreateAsync(It.IsAny<InvoiceCreateDto>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once); // Transaction starts before validation
            _unitOfWorkMock.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CancelSalesOrder_ShouldNotReleaseAlreadyConsumedAllocations()
        {
            // Arrange
            var so = CreateTestSalesOrder(SalesOrderStatus.Confirmed);
            var allocationId = Guid.NewGuid();

            _inventoryServiceMock.Setup(s => s.GetAllocationsByReferenceAsync(so.Id))
                .ReturnsAsync(new List<InventoryAllocationDto> 
                { 
                    new InventoryAllocationDto 
                    { 
                        Id = allocationId, 
                        ReferenceNumber = so.Id.ToString(), 
                        AllocatedQuantity = 5,
                        RemainingQuantity = 0, // Fully consumed
                        ConsumedQuantity = 5 
                    } 
                });

            // Act
            var dto = new CancelSalesOrderDto { Reason = "Customer request" };
            await _sut.CancelSalesOrderAsync(so.Id, dto);

            // Assert
            so.OrderStatus.Should().Be(SalesOrderStatus.Cancelled);
            // Since it's fully consumed, we shouldn't try to release it
            _inventoryServiceMock.Verify(s => s.ReleaseAllocationAsync(allocationId, It.IsAny<Guid>()), Times.Never);
            _salesOrderRepoMock.Verify(r => r.UpdateAsync(so), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }
    }
}
