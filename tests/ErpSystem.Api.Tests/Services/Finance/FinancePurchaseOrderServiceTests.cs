using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance
{
    public class FinancePurchaseOrderServiceTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;

            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task CreateAsync_WithValidInventoryLine_ShouldSucceed()
        {
            await using var context = CreateContext();
            var service = new FinancePurchaseOrderService(context);

            var po = new CreateFinancePurchaseOrderDto
            {
                OrderNumber = "FPO-001",
                VendorId = Guid.NewGuid(),
                OrderDate = DateTime.UtcNow,
                Items = new List<CreateFinancePurchaseOrderItemDto>
                {
                    new CreateFinancePurchaseOrderItemDto
                    {
                        LineType = FinancePurchaseOrderLineType.Inventory,
                        InventoryItemId = Guid.NewGuid(),
                        WarehouseId = Guid.NewGuid(),
                        OrderedQuantity = 10,
                        UnitPrice = 100,
                        Description = "Test Inventory Item"
                    }
                }
            };

            var result = await service.CreateAsync(po);

            result.Status.Should().Be(FinancePurchaseOrderStatus.Draft);
            result.TotalAmount.Should().Be(1000);
            result.Items.First().LineTotal.Should().Be(1000);
            
            var saved = await context.FinancePurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == result.Id);
            saved.Should().NotBeNull();
            saved.Items.Should().HaveCount(1);
        }

        [Fact]
        public async Task CreateAsync_WithInventoryLineMissingWarehouse_ShouldThrow()
        {
            await using var context = CreateContext();
            var service = new FinancePurchaseOrderService(context);

            var po = new CreateFinancePurchaseOrderDto
            {
                OrderNumber = "FPO-002",
                VendorId = Guid.NewGuid(),
                OrderDate = DateTime.UtcNow,
                Items = new List<CreateFinancePurchaseOrderItemDto>
                {
                    new CreateFinancePurchaseOrderItemDto
                    {
                        LineType = FinancePurchaseOrderLineType.Inventory,
                        InventoryItemId = Guid.NewGuid(),
                        // WarehouseId is missing
                        OrderedQuantity = 10,
                        UnitPrice = 100,
                        Description = "Test Inventory Item"
                    }
                }
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(po));
        }

        [Fact]
        public async Task CreateAsync_WithGLLineHavingInventoryItem_ShouldThrow()
        {
            await using var context = CreateContext();
            var service = new FinancePurchaseOrderService(context);

            var po = new CreateFinancePurchaseOrderDto
            {
                OrderNumber = "FPO-003",
                VendorId = Guid.NewGuid(),
                OrderDate = DateTime.UtcNow,
                Items = new List<CreateFinancePurchaseOrderItemDto>
                {
                    new CreateFinancePurchaseOrderItemDto
                    {
                        LineType = FinancePurchaseOrderLineType.GLAccount,
                        GlAccountId = Guid.NewGuid(),
                        InventoryItemId = Guid.NewGuid(), // Invalid for GL Account type
                        OrderedQuantity = 10,
                        UnitPrice = 100,
                        Description = "Test GL Item"
                    }
                }
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(po));
        }

        [Fact]
        public async Task ApproveAsync_OnDraftPO_ShouldChangeStatusToApproved()
        {
            await using var context = CreateContext();
            var service = new FinancePurchaseOrderService(context);
            var poId = Guid.NewGuid();

            context.FinancePurchaseOrders.Add(new FinancePurchaseOrder
            {
                Id = poId,
                TenantId = Guid.NewGuid(),
                VendorId = Guid.NewGuid(),
                Status = FinancePurchaseOrderStatus.Draft,
                TotalAmount = 500
            });
            await context.SaveChangesAsync();

            await service.ApproveAsync(poId);

            var saved = await context.FinancePurchaseOrders.FindAsync(poId);
            saved.Status.Should().Be(FinancePurchaseOrderStatus.Approved);
        }

        [Fact]
        public async Task ApproveAsync_OnNonDraftPO_ShouldThrow()
        {
            await using var context = CreateContext();
            var service = new FinancePurchaseOrderService(context);
            var poId = Guid.NewGuid();

            context.FinancePurchaseOrders.Add(new FinancePurchaseOrder
            {
                Id = poId,
                TenantId = Guid.NewGuid(),
                VendorId = Guid.NewGuid(),
                Status = FinancePurchaseOrderStatus.Approved,
                TotalAmount = 500
            });
            await context.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(poId));
        }
    }
}
