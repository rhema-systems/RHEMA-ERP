using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementSupplierInvoiceBoundaryTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task Procurement_route_requires_same_tenant_source_lineage(bool hasPo, bool foreignTenant, bool allowed)
    {
        await using var f = new Fixture();
        var invoice = new VendorInvoice { Id=Guid.NewGuid(), TenantId=foreignTenant ? Guid.NewGuid() : f.Tenant,
            InvoiceNumber="VI-TEST", SupplierId=Guid.NewGuid(), SupplierName="Supplier", PurchaseOrderId=hasPo ? Guid.NewGuid() : null };
        f.Db.Add(invoice); await f.Db.SaveChangesAsync();
        var action = f.Action(new() { ["id"] = invoice.Id });
        var called = false;
        await f.Filter.OnActionExecutionAsync(action, () => { called = true; return Task.FromResult(new ActionExecutedContext(action, [], new object())); });
        Assert.Equal(allowed, called);
        if (!allowed) Assert.IsType<NotFoundResult>(action.Result);
    }

    [Fact]
    public async Task Register_enforces_source_filter_and_does_not_change_finance_route()
    {
        await using var f = new Fixture();
        var query = new VendorInvoiceQueryDto { ProcurementOnly=false, IsOpeningBalance=true };
        var action = f.Action(new() { ["query"] = query });
        await f.Filter.OnActionExecutionAsync(action, () => Task.FromResult(new ActionExecutedContext(action, [], new object())));
        Assert.True(query.ProcurementOnly); Assert.False(query.IsOpeningBalance);
        var sharedQuery = new VendorInvoiceQueryDto { ProcurementOnly=false, IsOpeningBalance=true };
        var shared = f.Action(new() { ["query"] = sharedQuery }); shared.HttpContext.Request.Path="/api/ap/invoices";
        await f.Filter.OnActionExecutionAsync(shared, () => Task.FromResult(new ActionExecutedContext(shared, [], new object())));
        Assert.False(sharedQuery.ProcurementOnly); Assert.True(sharedQuery.IsOpeningBalance);
    }

    [Fact]
    public async Task Procurement_cannot_create_manual_or_opening_invoice()
    {
        await using var f = new Fixture();
        foreach (var dto in new[] { new VendorInvoiceCreateDto(), new VendorInvoiceCreateDto { PurchaseOrderId=Guid.NewGuid(), IsOpeningBalance=true } })
        {
            var action = f.Action(new() { ["dto"] = dto });
            await f.Filter.OnActionExecutionAsync(action, () => throw new Exception("Should not reach AP mutation"));
            Assert.Equal(422, Assert.IsType<ObjectResult>(action.Result).StatusCode);
        }
    }

    [Fact]
    public async Task Update_uses_route_identity_even_when_body_names_another_procurement_invoice()
    {
        await using var f = new Fixture();
        var original = new VendorInvoice { Id=Guid.NewGuid(), TenantId=f.Tenant, InvoiceNumber="VI-ORIGINAL", SupplierId=Guid.NewGuid(), SupplierName="Supplier", PurchaseOrderId=Guid.NewGuid() };
        var other = new VendorInvoice { Id=Guid.NewGuid(), TenantId=f.Tenant, InvoiceNumber="VI-OTHER", SupplierId=Guid.NewGuid(), SupplierName="Supplier", PurchaseOrderId=Guid.NewGuid() };
        f.Db.AddRange(original, other); await f.Db.SaveChangesAsync();
        var action=f.Action(new() { ["id"]=original.Id, ["dto"]=new VendorInvoiceUpdateDto { Id=other.Id, PurchaseOrderId=other.PurchaseOrderId } });
        await f.Filter.OnActionExecutionAsync(action, () => throw new Exception("Should not change routed source"));
        Assert.Equal(422, Assert.IsType<ObjectResult>(action.Result).StatusCode);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Tenant { get; }=Guid.NewGuid();
        public ApplicationDbContext Db { get; }=new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public ProcurementSupplierInvoiceBoundaryFilter Filter { get; }
        public Fixture()
        {
            var user=new Mock<ICurrentUserService>(); user.SetupGet(u=>u.TenantId).Returns(Tenant);
            user.SetupGet(u=>u.Claims).Returns(new Dictionary<string,string>());
            Filter=new(Db,user.Object);
        }
        public ActionExecutingContext Action(Dictionary<string,object?> args)
        {
            var http=new DefaultHttpContext();http.Request.Path="/api/procurement/supplier-invoices";
            var context=new ActionContext(http,new RouteData(),new ControllerActionDescriptor());
            return new(context,[],args,new object());
        }
        public ValueTask DisposeAsync()=>Db.DisposeAsync();
    }
}
