using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance
{
    public class FinancePurchaseOrderReceiptServiceTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;

            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task ReceiveAsync_WithValidQuantities_ShouldUpdatePOAndCallInventory()
        {
            await using var context = CreateContext();
            var mockInventory = new Mock<IInventoryReceiptService>();
            var service = new FinancePurchaseOrderReceiptService(context, mockInventory.Object);

            var tenantId = Guid.NewGuid();
            var poId = Guid.NewGuid();
            var poLineId = Guid.NewGuid();
            
            context.FinancePurchaseOrders.Add(new FinancePurchaseOrder
            {
                Id = poId,
                TenantId = tenantId,
                Status = FinancePurchaseOrderStatus.Approved,
                Items = new List<FinancePurchaseOrderItem>
                {
                    new FinancePurchaseOrderItem
                    {
                        Id = poLineId,
                        LineType = FinancePurchaseOrderLineType.Inventory,
                        InventoryItemId = Guid.NewGuid(),
                        WarehouseId = Guid.NewGuid(),
                        OrderedQuantity = 10,
                        ReceivedQuantity = 0,
                        UnitPrice = 100
                    }
                }
            });
            await context.SaveChangesAsync();

            var receipt = new FinancePurchaseOrderReceipt
            {
                TenantId = tenantId,
                FinancePurchaseOrderId = poId,
                ReceiptNumber = "GRV-001",
                Items = new List<FinancePurchaseOrderReceiptItem>
                {
                    new FinancePurchaseOrderReceiptItem
                    {
                        FinancePurchaseOrderItemId = poLineId,
                        QuantityReceived = 5
                    }
                }
            };

            var result = await service.ReceiveAsync(receipt);

            result.Id.Should().NotBeEmpty();
            
            var updatedPo = await context.FinancePurchaseOrders.Include(p => p.Items).FirstAsync(p => p.Id == poId);
            updatedPo.Status.Should().Be(FinancePurchaseOrderStatus.PartiallyReceived);
            updatedPo.Items.First().ReceivedQuantity.Should().Be(5);
            
            mockInventory.Verify(i => i.ProcessFinanceReceiptAsync(tenantId, result.Id, It.IsAny<IEnumerable<FinanceReceiptInventoryLine>>()), Times.Once);
        }

        [Fact]
        public async Task ReceiveAsync_OverReceipt_ShouldThrow()
        {
            await using var context = CreateContext();
            var mockInventory = new Mock<IInventoryReceiptService>();
            var service = new FinancePurchaseOrderReceiptService(context, mockInventory.Object);

            var tenantId = Guid.NewGuid();
            var poId = Guid.NewGuid();
            var poLineId = Guid.NewGuid();
            
            context.FinancePurchaseOrders.Add(new FinancePurchaseOrder
            {
                Id = poId,
                TenantId = tenantId,
                Status = FinancePurchaseOrderStatus.Approved,
                Items = new List<FinancePurchaseOrderItem>
                {
                    new FinancePurchaseOrderItem
                    {
                        Id = poLineId,
                        OrderedQuantity = 10,
                        ReceivedQuantity = 8,
                        UnitPrice = 100
                    }
                }
            });
            await context.SaveChangesAsync();

            var receipt = new FinancePurchaseOrderReceipt
            {
                TenantId = tenantId,
                FinancePurchaseOrderId = poId,
                Items = new List<FinancePurchaseOrderReceiptItem>
                {
                    new FinancePurchaseOrderReceiptItem
                    {
                        FinancePurchaseOrderItemId = poLineId,
                        QuantityReceived = 3 // 8 + 3 = 11 > 10
                    }
                }
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReceiveAsync(receipt));
        }

        [Fact]
        public async Task ConvertToVendorInvoiceAsync_ShouldCreateInvoiceAndTrackQuantities()
        {
            await using var context = CreateContext();
            var mockInventory = new Mock<IInventoryReceiptService>();
            var service = new FinancePurchaseOrderReceiptService(context, mockInventory.Object);

            var tenantId = Guid.NewGuid();
            var vendorId = Guid.NewGuid();
            var poId = Guid.NewGuid();
            var poLineId = Guid.NewGuid();
            var receiptId = Guid.NewGuid();
            var receiptLineId = Guid.NewGuid();

            var poLine = new FinancePurchaseOrderItem
            {
                Id = poLineId,
                OrderedQuantity = 10,
                ReceivedQuantity = 5,
                InvoicedQuantity = 0,
                UnitPrice = 100,
                LineType = FinancePurchaseOrderLineType.GLAccount
            };

            context.FinancePurchaseOrders.Add(new FinancePurchaseOrder
            {
                Id = poId,
                TenantId = tenantId,
                VendorId = vendorId,
                Status = FinancePurchaseOrderStatus.PartiallyReceived,
                Items = new List<FinancePurchaseOrderItem> { poLine }
            });

            var receiptLine = new FinancePurchaseOrderReceiptItem
            {
                Id = receiptLineId,
                FinancePurchaseOrderItemId = poLineId,
                QuantityReceived = 5,
                InvoicedQuantity = 0,
                FinancePurchaseOrderItem = poLine
            };

            context.FinancePurchaseOrderReceipts.Add(new FinancePurchaseOrderReceipt
            {
                Id = receiptId,
                TenantId = tenantId,
                FinancePurchaseOrderId = poId,
                ReceiptNumber = "GRV-123",
                Items = new List<FinancePurchaseOrderReceiptItem> { receiptLine }
            });

            await context.SaveChangesAsync();

            var invoice = await service.ConvertToVendorInvoiceAsync(receiptId, Guid.NewGuid());

            invoice.Should().NotBeNull();
            invoice.BusinessPartnerId.Should().Be(vendorId);
            invoice.Status.Should().Be(VendorInvoiceStatus.Draft);
            invoice.TotalAmount.Should().Be(500); // 5 * 100
            
            // Validate tracking updated
            receiptLine.InvoicedQuantity.Should().Be(5);
            poLine.InvoicedQuantity.Should().Be(5);

            // Po status should be partially invoiced (since received < ordered)
            var updatedPo = await context.FinancePurchaseOrders.FindAsync(poId);
            updatedPo.Status.Should().Be(FinancePurchaseOrderStatus.PartiallyInvoiced);
        }
    }
}
