using System.Reflection;
using System.Text.Json;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
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

public sealed class EstateSupplierInvoiceLineageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_governed_payee_returns_actionable_problem_without_creating_partner_or_invoice(bool otherCosts)
    {
        var tenant = Guid.NewGuid();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var acquisition = new LandAcquisition { TenantId = tenant, ProjectReference = "ESTATE-SETUP",
            StageOrder = (int)AcquisitionProcedure.StampDutyPayment,
            WorkspaceDataJson = JsonSerializer.Serialize(new Dictionary<string, object> {
                ["14"] = new { otherAcquisitionServicesJson = "[{\"serviceName\":\"Legal costs\",\"amount\":200}]" }
            }),
            StampDutyAssessment = new StampDutyAssessment { TenantId = tenant, IsApproved = true, DutyAmount = 200m }
        };
        db.LandAcquisitions.Add(acquisition); await db.SaveChangesAsync();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(value => value.TenantId).Returns(tenant);
        user.SetupGet(value => value.UserId).Returns(Guid.NewGuid().ToString());
        user.SetupGet(value => value.Roles).Returns(new[] { "SystemAdmin" });
        var controller = new ErpSystem.Api.Controllers.Estate.LandAcquisitionsController(
            db, user.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var result = otherCosts
            ? await controller.EnsureOtherAcquisitionCostsAccountsPayableRequest(acquisition.Id, CancellationToken.None)
            : await controller.EnsureAccountsPayableRequest(acquisition.Id, CancellationToken.None);
        var response = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Contains(otherCosts ? "LAND-ACQ-OTHER-COSTS" : "GRA-STAMP-DUTY", problem.Detail);
        Assert.Contains("approved AP profile", problem.Detail);
        Assert.Equal(otherCosts ? "ESTATE_AP_VALIDATION" : "ESTATE_AP_PAYEE_SETUP_REQUIRED", problem.Extensions["code"]);
        Assert.Empty(await db.BusinessPartners.ToListAsync());
        Assert.Empty(await db.VendorInvoices.ToListAsync());
        Assert.Empty(await db.Set<ErpSystem.Core.Entities.Procurement.BusinessPartnerApProfileVersion>().ToListAsync());
    }

    [Theory]
    [InlineData(true, 200, true)]
    [InlineData(true, 230, false)]
    [InlineData(false, 200, false)]
    [InlineData(false, 230, true)]
    public void Estate_source_net_comparison_allows_reviewed_tax_without_altering_legacy_totals(bool estate, int sourceAmount, bool expected)
    {
        var invoice = new VendorInvoice { EstateAcquisitionId = estate ? Guid.NewGuid() : null, SubTotal = 200, DiscountAmount = 20, TaxAmount = 30, TotalAmount = 230 };
        var guard = typeof(ErpSystem.Api.Controllers.Estate.LandAcquisitionsController)
            .GetMethod("EstateSourceAmountMatches", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(expected, guard.Invoke(null, new object[] { invoice, (decimal)sourceAmount }));
    }

    [Fact]
    public void Api_clients_cannot_claim_estate_source_identity()
    {
        var dto = JsonSerializer.Deserialize<VendorInvoiceCreateDto>("{\"EstateAcquisitionId\":\"" + Guid.NewGuid() + "\",\"EstatePayableKind\":1}");
        Assert.Null(dto!.EstateAcquisitionId);
        Assert.Null(dto.EstatePayableKind);
    }

    [Theory]
    [InlineData(EstatePayableKind.SurveyorFee)]
    [InlineData(EstatePayableKind.VendorConsideration)]
    [InlineData(EstatePayableKind.StampDuty)]
    [InlineData(EstatePayableKind.OtherAcquisitionCosts)]
    public async Task Typed_estate_source_is_visible_but_reference_only_and_other_tenants_are_not(EstatePayableKind kind)
    {
        var tenant = Guid.NewGuid();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.TenantId).Returns(tenant);
        user.SetupGet(u => u.Claims).Returns(new Dictionary<string, string>());
        var filter = new ProcurementSupplierInvoiceBoundaryFilter(db, user.Object);
        foreach (var scenario in new[] { (Typed: true, Foreign: false), (Typed: false, Foreign: false), (Typed: true, Foreign: true) })
        {
            var invoice = new VendorInvoice { Id = Guid.NewGuid(), TenantId = scenario.Foreign ? Guid.NewGuid() : tenant,
                InvoiceNumber = Guid.NewGuid().ToString(), BusinessPartnerId = Guid.NewGuid(), SupplierName = "Canonical supplier",
                Reference = "LAND-STAMP-DUTY:" + Guid.NewGuid().ToString("N"),
                EstateAcquisitionId = scenario.Typed ? Guid.NewGuid() : null, EstatePayableKind = scenario.Typed ? kind : null };
            db.Add(invoice); await db.SaveChangesAsync();
            var http = new DefaultHttpContext(); http.Request.Path = "/api/procurement/supplier-invoices";
            var action = new ActionExecutingContext(new ActionContext(http, new RouteData(), new ControllerActionDescriptor()), [],
                new Dictionary<string, object?> { ["id"] = invoice.Id }, new object());
            var called = false;
            await filter.OnActionExecutionAsync(action, () => { called = true; return Task.FromResult(new ActionExecutedContext(action, [], new object())); });
            Assert.Equal(scenario.Typed && !scenario.Foreign, called);
        }
    }

    [Fact]
    public void Estate_source_model_uses_tenant_fk_and_unique_non_reusable_source_key()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var entity = db.Model.FindEntityType(typeof(VendorInvoice))!;
        var sourceKey = Assert.Single(entity.GetIndexes().Where(index => index.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { "TenantId", "EstateAcquisitionId", "EstatePayableKind" })));
        Assert.True(sourceKey.IsUnique);
        var fk = Assert.Single(entity.GetForeignKeys().Where(f => f.PrincipalEntityType.ClrType == typeof(LandAcquisition)));
        Assert.Equal(new[] { "TenantId", "EstateAcquisitionId" }, fk.Properties.Select(p => p.Name));
        Assert.Equal(new[] { "TenantId", "Id" }, fk.PrincipalKey.Properties.Select(p => p.Name));
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }

    [Fact]
    public void Tax_review_can_change_without_changing_estate_source_net_amount()
    {
        var (invoice, dto) = Pair();
        dto.LineItems[0].TaxTreatment = TaxTreatment.Standard;
        dto.LineItems[0].TaxGroupId = Guid.NewGuid();
        dto.LineItems[0].TaxRate = 15;
        Validate(invoice, dto);
    }

    [Fact]
    public void Pending_estate_tax_cannot_pass_the_submit_approve_post_guard()
    {
        var (invoice, _) = Pair();
        var guard = typeof(VendorInvoiceService).GetMethod("EnsureLandedCostTaxReviewed", BindingFlags.Static | BindingFlags.NonPublic)!;
        var error = Assert.Throws<TargetInvocationException>(() => guard.Invoke(null, new object[] { invoice }));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        invoice.LineItems.Single().TaxTreatment = TaxTreatment.OutOfScope;
        guard.Invoke(null, new object[] { invoice });
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("price")]
    [InlineData("discount")]
    [InlineData("account")]
    [InlineData("currency")]
    [InlineData("source")]
    [InlineData("remove")]
    public void Estate_source_commercial_values_cannot_be_rewritten(string mutation)
    {
        var (invoice, dto) = Pair();
        switch (mutation)
        {
            case "quantity": dto.LineItems[0].Quantity++; break;
            case "price": dto.LineItems[0].UnitPrice++; break;
            case "discount": dto.LineItems[0].DiscountPercentage++; break;
            case "account": dto.LineItems[0].GLAccountId = Guid.NewGuid(); break;
            case "currency": dto.CurrencyCode = "USD"; break;
            case "source": dto.Reference = "other"; break;
            case "remove": dto.LineItems.Clear(); break;
        }
        var error = Assert.Throws<TargetInvocationException>(() => Validate(invoice, dto));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    private static void Validate(VendorInvoice invoice, VendorInvoiceUpdateDto dto) => typeof(VendorInvoiceService)
        .GetMethod("ValidateEstateInvoiceUpdate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { invoice, dto });

    private static (VendorInvoice, VendorInvoiceUpdateDto) Pair()
    {
        var line = new VendorInvoiceLineItem { Id = Guid.NewGuid(), Description = "Survey", LineItemType = "Service",
            Quantity = 1, UnitPrice = 200, GLAccountId = Guid.NewGuid(), TaxTreatment = TaxTreatment.PendingReview };
        var invoice = new VendorInvoice { EstateAcquisitionId = Guid.NewGuid(), EstatePayableKind = EstatePayableKind.SurveyorFee,
            CurrencyCode = "GHS", ExchangeRate = 1, Reference = "Estate source", LineItems = new List<VendorInvoiceLineItem> { line } };
        var dto = new VendorInvoiceUpdateDto { CurrencyCode = "GHS", ExchangeRate = 1, Reference = invoice.Reference,
            LineItems = new List<VendorInvoiceLineItemCreateDto> { new() { Id = line.Id, Description = line.Description, LineItemType = line.LineItemType,
                Quantity = line.Quantity, UnitPrice = line.UnitPrice, GLAccountId = line.GLAccountId, TaxTreatment = line.TaxTreatment } } };
        return (invoice, dto);
    }
}
