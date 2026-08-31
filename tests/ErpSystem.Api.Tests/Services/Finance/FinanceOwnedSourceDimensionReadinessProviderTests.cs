using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceOwnedSourceDimensionReadinessProviderTests
{
    [Fact]
    public async Task ManualVendorRouteReportsLegacyAndMissingLineEvidenceWithFinanceLink()
    {
        var tenantId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"finance-dimension-readiness-{Guid.NewGuid():N}").Options);
        var supplierId = Guid.NewGuid();
        db.Suppliers.Add(new Supplier
        {
            Id = supplierId,
            TenantId = tenantId,
            SupplierCode = "SUP-READINESS",
            Name = "Readiness Supplier"
        });
        db.VendorInvoices.Add(new VendorInvoice
        {
            Id = invoiceId,
            TenantId = tenantId,
            SupplierId = supplierId,
            InvoiceNumber = "VI-READINESS-001",
            SupplierName = "Readiness Supplier",
            InvoiceDate = new DateTime(2026, 8, 30),
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            Status = VendorInvoiceStatus.Draft,
            ApprovalStatus = "Draft",
            LineItems =
            [
                new VendorInvoiceLineItem
                {
                    Id = lineId,
                    TenantId = tenantId,
                    VendorInvoiceId = invoiceId,
                    LineItemType = "Expense",
                    GLAccountId = accountId,
                    Description = "Readiness expense",
                    Quantity = 1m,
                    UnitPrice = 100m
                }
            ]
        });
        await db.SaveChangesAsync();

        var route = FinanceDimensionRouteCatalog.GetRequired(
            FinanceDimensionRouteId.FinanceApVendorInvoice);
        var provider = new FinanceOwnedSourceDimensionReadinessProvider(db, route.Id);
        var legacy = await provider.EvaluateAsync(tenantId, route);

        legacy.Blockers.Should().ContainSingle(blocker =>
            blocker.Code == "UNCERTIFIED_LEGACY_DOCUMENT"
            && blocker.DocumentLink == $"/finance/ap/invoices/{invoiceId}");

        db.FinanceSourceDimensionAssignments.Add(new FinanceSourceDimensionAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RouteId = route.Id,
            ProducerModule = route.ProducerModule,
            SourceRoute = route.SourceRoute,
            SourceDocumentType = route.DocumentType,
            ContractVersion = route.ContractVersion,
            SourceDocumentId = invoiceId
        });
        await db.SaveChangesAsync();

        var adaptedHeader = await provider.EvaluateAsync(tenantId, route);
        adaptedHeader.Blockers.Should().ContainSingle(blocker =>
            blocker.Code == "SOURCE_LINE_NOT_ADAPTED" && blocker.DocumentId == invoiceId);
    }
}
