using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Data;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ErpSystem.Api.Controllers.Finance;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous] // FOR TESTING ONLY
public class GLIntegrationTestController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IVendorInvoiceService _vendorInvoiceService;
    private readonly IInvoiceService _invoiceService;
    private readonly ILogger<GLIntegrationTestController> _logger;

    public GLIntegrationTestController(
        ApplicationDbContext context,
        IVendorInvoiceService vendorInvoiceService,
        IInvoiceService invoiceService,
        ILogger<GLIntegrationTestController> logger)
    {
        _context = context;
        _vendorInvoiceService = vendorInvoiceService;
        _invoiceService = invoiceService;
        _logger = logger;
    }

    [HttpPost("run-test")]
    public async Task<IActionResult> RunTest(CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT", cancellationToken);
            if (tenant == null) return BadRequest("DEFAULT tenant not found.");
            
            var user = await _context.Users.FirstOrDefaultAsync(cancellationToken);
            var userId = user?.Id ?? Guid.NewGuid();

            var settings = await _context.FinanceSettings
                .FirstOrDefaultAsync(s => s.TenantId == tenant.Id, cancellationToken);
            if (settings == null) return BadRequest("Finance Settings not found.");

            // 1. Setup Master Data
            var supplier = new Supplier 
            { 
                Id = Guid.NewGuid(),
                Name = "Test AP Supplier", 
                SupplierCode = "SUP-TEST-01",
                TenantId = tenant.Id
            };
            var customer = new Customer 
            { 
                Id = Guid.NewGuid(),
                CustomerName = "Test AR Customer", 
                CustomerCode = "CUS-TEST-01",
                TenantId = tenant.Id,
                CurrencyCode = "GHS"
            };
            
            var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.TenantId == tenant.Id && w.IsActive, cancellationToken);
            if (warehouse == null) 
            {
                warehouse = new Warehouse { 
                    Id = Guid.NewGuid(),
                    Name = "Main Warehouse", Code = "WH-01", TenantId = tenant.Id };
                _context.Warehouses.Add(warehouse);
            }

            var category = new InventoryCategory
            {
                Id = Guid.NewGuid(),
                Name = "Test Category",
                Code = "TEST-CAT-01",
                TenantId = tenant.Id
            };

            var inventoryItem = new InventoryItem 
            { 
                Id = Guid.NewGuid(),
                ItemCode = "TEST-ITEM-01", 
                Name = "Test Inventory Item",
                CategoryId = category.Id,
                ItemType = ItemType.StockItem,
                TenantId = tenant.Id,
                AverageCost = 50.0m
            };

            await _context.Suppliers.AddAsync(supplier, cancellationToken);
            await _context.Customers.AddAsync(customer, cancellationToken);
            await _context.InventoryCategories.AddAsync(category, cancellationToken);
            await _context.InventoryItems.AddAsync(inventoryItem, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            // Give it some stock so we can sell it
            _context.InventoryBalances.Add(new InventoryBalance
            {
                InventoryItemId = inventoryItem.Id,
                WarehouseId = warehouse.Id,
                QuantityOnHand = 100,
                AverageUnitCost = 50.0m,
                TenantId = tenant.Id
            });
            await _context.SaveChangesAsync(cancellationToken);

            // 2. AP Invoice (Inventory)
            var vendorInvoice = new VendorInvoice
            {
                SupplierId = supplier.Id,
                InvoiceNumber = $"VINV-{DateTime.Now.Ticks}",
                InvoiceDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(30),
                CurrencyCode = "GHS",
                SubTotal = 1000m,
                TaxAmount = 0,
                TotalAmount = 1000m,
                Status = VendorInvoiceStatus.Draft,
                TenantId = tenant.Id,
                LineItems = new List<VendorInvoiceLineItem>
                {
                    new VendorInvoiceLineItem
                    {
                        LineItemType = "Inventory",
                        Description = "Test Item Restock",
                        InventoryItemId = inventoryItem.Id,
                        WarehouseId = warehouse.Id,
                        Quantity = 10,
                        UnitPrice = 100m,
                        TenantId = tenant.Id
                    }
                }
            };
            await _context.VendorInvoices.AddAsync(vendorInvoice, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            // AP Invoice Approval -> Posts to GL
            await _vendorInvoiceService.ApproveAsync(vendorInvoice.Id, userId.ToString());

            // 3. AR Invoice (Inventory)
            var arInvoice = new Invoice
            {
                CustomerId = customer.Id,
                InvoiceNumber = $"INV-{DateTime.Now.Ticks}",
                InvoiceDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(30),
                CurrencyCode = "GHS",
                SubTotal = 1500m,
                TaxAmount = 0,
                TotalAmount = 1500m,
                Status = InvoiceStatus.Draft,
                TenantId = tenant.Id,
                LineItems = new List<InvoiceLineItem>
                {
                    new InvoiceLineItem
                    {
                        LineItemType = LineItemType.Inventory,
                        Description = "Test item sale",
                        InventoryItemId = inventoryItem.Id,
                        WarehouseId = warehouse.Id,
                        Quantity = 5,
                        UnitPrice = 300m,
                        TenantId = tenant.Id
                    }
                }
            };
            await _context.Invoices.AddAsync(arInvoice, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            // AR Invoice Send -> Posts to GL
            await _invoiceService.SendInvoiceAsync(arInvoice.Id, cancellationToken);


            // Re-fetch to see Journal Entries
            var apJe = await _context.JournalEntries
                .Include(j => j.Transactions).ThenInclude(t => t.Account)
                .OrderByDescending(j => j.CreatedAt)
                .FirstOrDefaultAsync(j => j.SourceDocumentId == vendorInvoice.Id, cancellationToken);
                
            var arJe = await _context.JournalEntries
                .Include(j => j.Transactions).ThenInclude(t => t.Account)
                .OrderByDescending(j => j.CreatedAt)
                .FirstOrDefaultAsync(j => j.SourceDocumentId == arInvoice.Id, cancellationToken);

            return Ok(new
            {
                message = "Test completed successfully.",
                apInvoiceJournalEntry = apJe,
                arInvoiceJournalEntry = arJe
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running GL integration test.");
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
    }
}
