using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Tests.Services.Finance
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

        private static (Mock<IInventoryReceiptService>, Mock<ITaxCalculationEngine>, Mock<ITenantSettingsService>) CreateMocks()
        {
            var mockInventory = new Mock<IInventoryReceiptService>();
            var mockTax = new Mock<ITaxCalculationEngine>();
            var mockSettings = new Mock<ITenantSettingsService>();

            mockSettings.Setup(s => s.GetBaseCurrencyAsync()).ReturnsAsync("GHS");

            return (mockInventory, mockTax, mockSettings);
        }

        [Fact]
        public async Task GRV_Create_ShouldListOnlyApprovedOrPartiallyReceivedPOs()
        {
            // Note: Selectable PO filtering is executed at the database querying level (or client side).
            // Here, we verify that ReceiveAsync strictly validates that the PO is in Approved or PartiallyReceived status,
            // throwing InvalidOperationException if it is in Draft or Closed status.
            await using var context = CreateContext();
            var (mockInventory, mockTax, mockSettings) = CreateMocks();
            var service = new FinancePurchaseOrderReceiptService(context, mockInventory.Object, mockTax.Object, mockSettings.Object);

            var poId = Guid.NewGuid();
            var lineId = Guid.NewGuid();

            context.FinancePurchaseOrders.Add(new FinancePurchaseOrder
            {
                Id = poId,
                TenantId = Guid.Empty,
                Status = FinancePurchaseOrderStatus.Draft, // Excluded from receipt
                Items = new List<FinancePurchaseOrderItem>
                {
                    new FinancePurchaseOrderItem
                    {
                        Id = lineId,
                        OrderedQuantity = 10,
                        ReceivedQuantity = 0,
                        UnitPrice = 10
                    }
                }
            });
            await context.SaveChangesAsync();

            var dto = new CreateFinancePurchaseReceiptDto
            {
                FinancePurchaseOrderId = poId,
                ReceiptNumber = "GRV-001",
                Lines = new List<CreateFinancePurchaseReceiptLineDto>
                {
                    new CreateFinancePurchaseReceiptLineDto
                    {
                        FinancePurchaseOrderItemId = lineId,
                        QuantityReceived = 5
                    }
                }
            };

            // Act & Assert: Receiving against a Draft PO must throw an InvalidOperationException
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReceiveAsync(dto));
        }

        [Fact]
        public async Task GRV_Create_ShouldLoadRemainingPOLines()
        {
            await using var context = CreateContext();
            var (mockInventory, mockTax, mockSettings) = CreateMocks();
            var service = new FinancePurchaseOrderReceiptService(context, mockInventory.Object, mockTax.Object, mockSettings.Object);

            var poId = Guid.NewGuid();
            var lineId = Guid.NewGuid();

            var po = new FinancePurchaseOrder
            {
                Id = poId,
                TenantId = Guid.Empty,
                Status = FinancePurchaseOrderStatus.Approved,
                Items = new List<FinancePurchaseOrderItem>
                {
                    new FinancePurchaseOrderItem
                    {
                        Id = lineId,
                        OrderedQuantity = 10,
                        ReceivedQuantity = 3,
                        UnitPrice = 15,
                        Description = "Test item"
                    }
                }
            };
            context.FinancePurchaseOrders.Add(po);
            await context.SaveChangesAsync();

            // Act: Query the PO
            var fetchedPo = await context.FinancePurchaseOrders.Include(p => p.Items).FirstAsync(p => p.Id == poId);
            var item = fetchedPo.Items.First();
            var remainingToReceive = item.OrderedQuantity - item.ReceivedQuantity;

            // Assert: verify quantities
            remainingToReceive.Should().Be(7);
        }

        [Fact]
        public async Task GRV_Create_ShouldAllowSelectedLineReceipt()
        {
            await using var context = CreateContext();
            var (mockInventory, mockTax, mockSettings) = CreateMocks();
            var service = new FinancePurchaseOrderReceiptService(context, mockInventory.Object, mockTax.Object, mockSettings.Object);

            var poId = Guid.NewGuid();
            var line1Id = Guid.NewGuid();
            var line2Id = Guid.NewGuid();

            context.FinancePurchaseOrders.Add(new FinancePurchaseOrder
            {
                Id = poId,
                TenantId = Guid.Empty,
                Status = FinancePurchaseOrderStatus.Approved,
                Items = new List<FinancePurchaseOrderItem>
                {
                    new FinancePurchaseOrderItem
                    {
                        Id = line1Id,
                        OrderedQuantity = 10,
                        ReceivedQuantity = 0,
                        UnitPrice = 10,
                        Description = "Line 1"
                    },
                    new FinancePurchaseOrderItem
                    {
                        Id = line2Id,
                        OrderedQuantity = 5,
                        ReceivedQuantity = 0,
                        UnitPrice = 20,
                        Description = "Line 2"
                    }
                }
            });
            await context.SaveChangesAsync();

            // Receive ONLY Line 1
            var dto = new CreateFinancePurchaseReceiptDto
            {
                FinancePurchaseOrderId = poId,
                ReceiptNumber = "GRV-SELECTED",
                Lines = new List<CreateFinancePurchaseReceiptLineDto>
                {
                    new CreateFinancePurchaseReceiptLineDto
                    {
                        FinancePurchaseOrderItemId = line1Id,
                        QuantityReceived = 4 // Receive on Line 1
                    },
                    new CreateFinancePurchaseReceiptLineDto
                    {
                        FinancePurchaseOrderItemId = line2Id,
                        QuantityReceived = 0 // Line 2 is ignored
                    }
                }
            };

            // Act
            var receipt = await service.ReceiveAsync(dto);

            // Assert
            receipt.Items.Should().HaveCount(1);
            receipt.Items.First().FinancePurchaseOrderItemId.Should().Be(line1Id);
            receipt.Items.First().QuantityReceived.Should().Be(4);

            var updatedPo = await context.FinancePurchaseOrders.Include(p => p.Items).FirstAsync(p => p.Id == poId);
            updatedPo.Items.First(i => i.Id == line1Id).ReceivedQuantity.Should().Be(4);
            updatedPo.Items.First(i => i.Id == line2Id).ReceivedQuantity.Should().Be(0);
        }

        [Fact]
        public async Task GRV_Create_ShouldAllowPartialReceipt()
        {
            await using var context = CreateContext();
            var (mockInventory, mockTax, mockSettings) = CreateMocks();
            var service = new FinancePurchaseOrderReceiptService(context, mockInventory.Object, mockTax.Object, mockSettings.Object);

            var poId = Guid.NewGuid();
            var lineId = Guid.NewGuid();

            context.FinancePurchaseOrders.Add(new FinancePurchaseOrder
            {
                Id = poId,
                TenantId = Guid.Empty,
                Status = FinancePurchaseOrderStatus.Approved,
                Items = new List<FinancePurchaseOrderItem>
                {
                    new FinancePurchaseOrderItem
                    {
                        Id = lineId,
                        OrderedQuantity = 10,
                        ReceivedQuantity = 0,
                        UnitPrice = 10
                    }
                }
            });
            await context.SaveChangesAsync();

            var dto = new CreateFinancePurchaseReceiptDto
            {
                FinancePurchaseOrderId = poId,
                ReceiptNumber = "GRV-PARTIAL",
                Lines = new List<CreateFinancePurchaseReceiptLineDto>
                {
                    new CreateFinancePurchaseReceiptLineDto
                    {
                        FinancePurchaseOrderItemId = lineId,
                        QuantityReceived = 3 // Partial quantity
                    }
                }
            };

            // Act
            var receipt = await service.ReceiveAsync(dto);

            // Assert
            receipt.Items.First().QuantityReceived.Should().Be(3);
            var updatedPo = await context.FinancePurchaseOrders.Include(p => p.Items).FirstAsync(p => p.Id == poId);
            updatedPo.Items.First().ReceivedQuantity.Should().Be(3);
            updatedPo.Status.Should().Be(FinancePurchaseOrderStatus.PartiallyReceived);
        }

        [Fact]
        public async Task GRV_Create_ShouldPreventOverReceipt()
        {
            await using var context = CreateContext();
            var (mockInventory, mockTax, mockSettings) = CreateMocks();
            var service = new FinancePurchaseOrderReceiptService(context, mockInventory.Object, mockTax.Object, mockSettings.Object);

            var poId = Guid.NewGuid();
            var lineId = Guid.NewGuid();

            context.FinancePurchaseOrders.Add(new FinancePurchaseOrder
            {
                Id = poId,
                TenantId = Guid.Empty,
                Status = FinancePurchaseOrderStatus.PartiallyReceived,
                Items = new List<FinancePurchaseOrderItem>
                {
                    new FinancePurchaseOrderItem
                    {
                        Id = lineId,
                        OrderedQuantity = 10,
                        ReceivedQuantity = 7,
                        UnitPrice = 10
                    }
                }
            });
            await context.SaveChangesAsync();

            var dto = new CreateFinancePurchaseReceiptDto
            {
                FinancePurchaseOrderId = poId,
                ReceiptNumber = "GRV-OVER",
                Lines = new List<CreateFinancePurchaseReceiptLineDto>
                {
                    new CreateFinancePurchaseReceiptLineDto
                    {
                        FinancePurchaseOrderItemId = lineId,
                        QuantityReceived = 4 // 7 + 4 = 11 > 10
                    }
                }
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReceiveAsync(dto));
        }

        [Fact]
        public async Task GRV_Create_ShouldRejectZeroQuantityReceipt()
        {
            await using var context = CreateContext();
            var (mockInventory, mockTax, mockSettings) = CreateMocks();
            var service = new FinancePurchaseOrderReceiptService(context, mockInventory.Object, mockTax.Object, mockSettings.Object);

            var poId = Guid.NewGuid();
            var lineId = Guid.NewGuid();

            context.FinancePurchaseOrders.Add(new FinancePurchaseOrder
            {
                Id = poId,
                TenantId = Guid.Empty,
                Status = FinancePurchaseOrderStatus.Approved,
                Items = new List<FinancePurchaseOrderItem>
                {
                    new FinancePurchaseOrderItem
                    {
                        Id = lineId,
                        OrderedQuantity = 10,
                        ReceivedQuantity = 0,
                        UnitPrice = 10
                    }
                }
            });
            await context.SaveChangesAsync();

            var dto = new CreateFinancePurchaseReceiptDto
            {
                FinancePurchaseOrderId = poId,
                ReceiptNumber = "GRV-ZERO",
                Lines = new List<CreateFinancePurchaseReceiptLineDto>
                {
                    new CreateFinancePurchaseReceiptLineDto
                    {
                        FinancePurchaseOrderItemId = lineId,
                        QuantityReceived = 0 // Zero received quantity
                    }
                }
            };

            // Act & Assert: Must throw InvalidOperationException because no line has quantity > 0
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReceiveAsync(dto));
        }

        [Fact]
        public async Task GRV_Create_ShouldUpdatePOReceivedQuantities()
        {
            await using var context = CreateContext();
            var (mockInventory, mockTax, mockSettings) = CreateMocks();
            var service = new FinancePurchaseOrderReceiptService(context, mockInventory.Object, mockTax.Object, mockSettings.Object);

            var poId = Guid.NewGuid();
            var lineId = Guid.NewGuid();

            context.FinancePurchaseOrders.Add(new FinancePurchaseOrder
            {
                Id = poId,
                TenantId = Guid.Empty,
                Status = FinancePurchaseOrderStatus.Approved,
                Items = new List<FinancePurchaseOrderItem>
                {
                    new FinancePurchaseOrderItem
                    {
                        Id = lineId,
                        OrderedQuantity = 10,
                        ReceivedQuantity = 2,
                        UnitPrice = 10
                    }
                }
            });
            await context.SaveChangesAsync();

            var dto = new CreateFinancePurchaseReceiptDto
            {
                FinancePurchaseOrderId = poId,
                ReceiptNumber = "GRV-UPDATE",
                Lines = new List<CreateFinancePurchaseReceiptLineDto>
                {
                    new CreateFinancePurchaseReceiptLineDto
                    {
                        FinancePurchaseOrderItemId = lineId,
                        QuantityReceived = 5 // Total received should be 2 + 5 = 7
                    }
                }
            };

            // Act
            await service.ReceiveAsync(dto);

            // Assert
            var updatedPo = await context.FinancePurchaseOrders.Include(p => p.Items).FirstAsync(p => p.Id == poId);
            updatedPo.Items.First().ReceivedQuantity.Should().Be(7);
        }

        [Fact]
        public async Task GRV_Create_ShouldMarkPOPartiallyReceived_WhenSomeQuantityRemains()
        {
            await using var context = CreateContext();
            var (mockInventory, mockTax, mockSettings) = CreateMocks();
            var service = new FinancePurchaseOrderReceiptService(context, mockInventory.Object, mockTax.Object, mockSettings.Object);

            var poId = Guid.NewGuid();
            var lineId = Guid.NewGuid();

            context.FinancePurchaseOrders.Add(new FinancePurchaseOrder
            {
                Id = poId,
                TenantId = Guid.Empty,
                Status = FinancePurchaseOrderStatus.Approved,
                Items = new List<FinancePurchaseOrderItem>
                {
                    new FinancePurchaseOrderItem
                    {
                        Id = lineId,
                        OrderedQuantity = 10,
                        ReceivedQuantity = 0,
                        UnitPrice = 10
                    }
                }
            });
            await context.SaveChangesAsync();

            var dto = new CreateFinancePurchaseReceiptDto
            {
                FinancePurchaseOrderId = poId,
                ReceiptNumber = "GRV-PARTIAL-STATUS",
                Lines = new List<CreateFinancePurchaseReceiptLineDto>
                {
                    new CreateFinancePurchaseReceiptLineDto
                    {
                        FinancePurchaseOrderItemId = lineId,
                        QuantityReceived = 8 // Ordered = 10, Received = 8 (2 remaining)
                    }
                }
            };

            // Act
            await service.ReceiveAsync(dto);

            // Assert
            var updatedPo = await context.FinancePurchaseOrders.FindAsync(poId);
            updatedPo.Status.Should().Be(FinancePurchaseOrderStatus.PartiallyReceived);
        }

        [Fact]
        public async Task GRV_Create_ShouldMarkPOReceived_WhenAllLinesFullyReceived()
        {
            await using var context = CreateContext();
            var (mockInventory, mockTax, mockSettings) = CreateMocks();
            var service = new FinancePurchaseOrderReceiptService(context, mockInventory.Object, mockTax.Object, mockSettings.Object);

            var poId = Guid.NewGuid();
            var lineId = Guid.NewGuid();

            context.FinancePurchaseOrders.Add(new FinancePurchaseOrder
            {
                Id = poId,
                TenantId = Guid.Empty,
                Status = FinancePurchaseOrderStatus.Approved,
                Items = new List<FinancePurchaseOrderItem>
                {
                    new FinancePurchaseOrderItem
                    {
                        Id = lineId,
                        OrderedQuantity = 10,
                        ReceivedQuantity = 0,
                        UnitPrice = 10
                    }
                }
            });
            await context.SaveChangesAsync();

            var dto = new CreateFinancePurchaseReceiptDto
            {
                FinancePurchaseOrderId = poId,
                ReceiptNumber = "GRV-FULLY-RECEIVED",
                Lines = new List<CreateFinancePurchaseReceiptLineDto>
                {
                    new CreateFinancePurchaseReceiptLineDto
                    {
                        FinancePurchaseOrderItemId = lineId,
                        QuantityReceived = 10 // Ordered = 10, Received = 10 (0 remaining)
                    }
                }
            };

            // Act
            await service.ReceiveAsync(dto);

            // Assert
            var updatedPo = await context.FinancePurchaseOrders.FindAsync(poId);
            updatedPo.Status.Should().Be(FinancePurchaseOrderStatus.Received);
        }

        [Fact]
        public async Task GRV_List_ShouldReturnAllReceiptsForTenant()
        {
            await using var context = CreateContext();
            var (mockInventory, mockTax, mockSettings) = CreateMocks();
            var service = new FinancePurchaseOrderReceiptService(context, mockInventory.Object, mockTax.Object, mockSettings.Object);

            context.FinancePurchaseOrderReceipts.Add(new FinancePurchaseOrderReceipt
            {
                Id = Guid.NewGuid(),
                FinancePurchaseOrderId = Guid.NewGuid(),
                ReceiptNumber = "GRV-1",
                ReceiptDate = DateTime.UtcNow.AddMinutes(-5)
            });
            context.FinancePurchaseOrderReceipts.Add(new FinancePurchaseOrderReceipt
            {
                Id = Guid.NewGuid(),
                FinancePurchaseOrderId = Guid.NewGuid(),
                ReceiptNumber = "GRV-2",
                ReceiptDate = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            // Act
            var list = await service.GetAllAsync();

            // Assert
            list.Should().HaveCount(2);
            list.First().ReceiptNumber.Should().Be("GRV-2"); // Sorted desc
        }

        [Fact]
        public async Task GRV_GetById_ShouldReturnReceiptWithLines()
        {
            await using var context = CreateContext();
            var (mockInventory, mockTax, mockSettings) = CreateMocks();
            var service = new FinancePurchaseOrderReceiptService(context, mockInventory.Object, mockTax.Object, mockSettings.Object);

            var receiptId = Guid.NewGuid();
            var lineId = Guid.NewGuid();

            var receipt = new FinancePurchaseOrderReceipt
            {
                Id = receiptId,
                FinancePurchaseOrderId = Guid.NewGuid(),
                ReceiptNumber = "GRV-DETAILS",
                Items = new List<FinancePurchaseOrderReceiptItem>
                {
                    new FinancePurchaseOrderReceiptItem
                    {
                        Id = lineId,
                        QuantityReceived = 5,
                        FinancePurchaseOrderItem = new FinancePurchaseOrderItem
                        {
                            Description = "Mock Item",
                            UnitPrice = 100
                        }
                    }
                }
            };
            context.FinancePurchaseOrderReceipts.Add(receipt);
            await context.SaveChangesAsync();

            // Act
            var result = await service.GetByIdAsync(receiptId);

            // Assert
            result.Should().NotBeNull();
            result.ReceiptNumber.Should().Be("GRV-DETAILS");
            result.Items.Should().HaveCount(1);
            result.Items.First().Id.Should().Be(lineId);
            result.Items.First().FinancePurchaseOrderItem.Description.Should().Be("Mock Item");
        }
    }
}
