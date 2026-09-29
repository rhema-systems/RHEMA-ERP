using ErpSystem.Api.Services.Sales;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class SalesOrderInvoiceGuardTests
{
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"sales-invoice-guard-{Guid.NewGuid():N}")
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generation_replay_reuses_retained_invoice_and_rejects_changed_payload(bool changed)
    {
        await using var db = Context();
        var tenant = Guid.NewGuid();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "sales-admin", IsActive = true };
        db.Users.Add(user);
        db.UserTenants.Add(new UserTenant { UserId = user.Id, User = user, TenantId = tenant, Status = UserTenantStatus.Active });
        var actor = new Mock<ICurrentUserProvider>();
        actor.SetupGet(value => value.UserId).Returns(user.Id);
        actor.SetupGet(value => value.TenantId).Returns(tenant);
        actor.SetupGet(value => value.IsAuthenticated).Returns(true);
        actor.Setup(value => value.HasRole("TenantAdmin")).Returns(true);
        var (order, account, _) = Source(tenant);
        var request = new GenerateSalesOrderInvoiceRequest { InvoiceDate = new(2026, 9, 27), IdempotencyKey = "same-generation" };
        order.InvoiceId = Guid.NewGuid();
        order.InvoiceGeneratedById = user.Id;
        order.InvoiceGenerationKey = request.IdempotencyKey;
        order.InvoiceGenerationHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
        db.AddRange(order, account);
        await db.SaveChangesAsync();
        var invoices = new Mock<IInvoiceService>(MockBehavior.Strict);
        invoices.Setup(value => value.GetByIdAsync(order.InvoiceId.Value, SalesOrderInvoiceGuard.Producer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceDto { Id = order.InvoiceId.Value, InvoiceNumber = "SI-RETAINED", Status = "Sent" });
        var service = new SalesOrderInvoiceService(db, actor.Object, invoices.Object);
        if (changed)
        {
            request.InvoiceDate = request.InvoiceDate.AddDays(1);
            var act = () => service.GenerateAsync(order.Id, request);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already has an invoice*");
            invoices.VerifyNoOtherCalls();
        }
        else
        {
            var result = await service.GenerateAsync(order.Id, request);
            result.Invoice.Id.Should().Be(order.InvoiceId.Value);
            invoices.Verify(value => value.GetByIdAsync(order.InvoiceId.Value, SalesOrderInvoiceGuard.Producer, It.IsAny<CancellationToken>()), Times.Once);
            invoices.VerifyNoOtherCalls();
        }
        (await db.Invoices.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Creation_requires_exact_source_tenant_and_approved_commercial_lines()
    {
        await using var db = Context();
        var tenant = Guid.NewGuid();
        var (order, account, dto) = Source(tenant);
        db.AddRange(order, account);
        await db.SaveChangesAsync();
        var uow = new UnitOfWork(db);
        await SalesOrderInvoiceGuard.ValidateCreationAsync(uow, tenant, dto, SalesOrderInvoiceGuard.Producer, default);
        var foreign = () => SalesOrderInvoiceGuard.ValidateCreationAsync(uow, Guid.NewGuid(), dto, SalesOrderInvoiceGuard.Producer, default);
        await foreign.Should().ThrowAsync<InvalidOperationException>();
        dto.LineItems[0].Quantity++;
        var tampered = () => SalesOrderInvoiceGuard.ValidateCreationAsync(uow, tenant, dto, SalesOrderInvoiceGuard.Producer, default);
        await tampered.Should().ThrowAsync<InvalidOperationException>().WithMessage("*retained Sales*");
        dto.LineItems[0].Quantity--;
        order.OrderStatus = SalesOrderStatus.Draft;
        await db.SaveChangesAsync();
        await tampered.Should().ThrowAsync<InvalidOperationException>().WithMessage("*confirmed standard*");
    }

    [Fact]
    public async Task Manual_AR_cannot_use_the_new_Sales_stock_identity_fields()
    {
        await using var db = Context();
        var (_, _, dto) = Source(Guid.NewGuid());
        dto.LineItems[0].InventoryItemId = Guid.NewGuid();
        var act = () => SalesOrderInvoiceGuard.ValidateCreationAsync(new UnitOfWork(db), Guid.NewGuid(), dto,
            new(FinanceDimensionRouteId.FinanceArCustomerInvoice), default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Creation_preserves_quote_economics_without_forcing_the_invoice_date_exchange_rate_to_match()
    {
        await using var db = Context();
        var tenant = Guid.NewGuid();
        var (order, account, dto) = Source(tenant);
        order.Currency = dto.CurrencyCode = "USD";
        order.ExchangeRate = 8m; dto.ExchangeRate = 10m; dto.ExchangeRateId = Guid.NewGuid();
        db.AddRange(order, account); await db.SaveChangesAsync();
        await SalesOrderInvoiceGuard.ValidateCreationAsync(new UnitOfWork(db), tenant, dto, SalesOrderInvoiceGuard.Producer, default);
        order.ExchangeRate.Should().Be(8m);
        dto.ExchangeRate.Should().Be(10m);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generation_resolves_the_selected_invoice_rate_in_the_current_tenant(bool otherTenantRate)
    {
        await using var db = Context();
        var tenant = Guid.NewGuid();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "sales-fx", IsActive = true };
        db.Users.Add(user);
        db.UserTenants.Add(new UserTenant { UserId = user.Id, User = user, TenantId = tenant, Status = UserTenantStatus.Active });
        var actor = new Mock<ICurrentUserProvider>();
        actor.SetupGet(value => value.UserId).Returns(user.Id);
        actor.SetupGet(value => value.TenantId).Returns(tenant);
        actor.SetupGet(value => value.IsAuthenticated).Returns(true);
        actor.Setup(value => value.HasRole("TenantAdmin")).Returns(true);
        var (order, account, _) = Source(tenant);
        order.Currency = "USD"; order.ExchangeRate = 8m;
        order.SubTotal = order.TotalAmount = 20m;
        var rate = new ExchangeRate { TenantId = otherTenantRate ? Guid.NewGuid() : tenant,
            BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD", Rate = .1m, InverseRate = 10m };
        db.AddRange(order, account, rate); await db.SaveChangesAsync();
        var request = new GenerateSalesOrderInvoiceRequest
        {
            InvoiceDate = new(2026, 9, 27), IdempotencyKey = "invoice-date-rate", RowVersion = Convert.ToBase64String(order.RowVersion),
            ExchangeRateId = rate.Id, Lines = [new() { SalesOrderLineId = order.Lines.Single().Id, TaxTreatment = TaxTreatment.Exempt }]
        };
        var invoices = new Mock<IInvoiceService>(MockBehavior.Strict);
        InvoiceCreateDto? captured = null;
        invoices.Setup(value => value.CreateAsync(It.IsAny<InvoiceCreateDto>(), SalesOrderInvoiceGuard.Producer, It.IsAny<CancellationToken>()))
            .Returns(async (InvoiceCreateDto input, FinancePostingProducerContext _, CancellationToken ct) =>
            {
                captured = input;
                var invoice = new Invoice { TenantId = tenant, InvoiceNumber = "FX-INVOICE", CurrencyCode = input.CurrencyCode,
                    ExchangeRate = input.ExchangeRate, ExchangeRateId = input.ExchangeRateId, SubTotal = 20m, TotalAmount = 20m };
                db.Invoices.Add(invoice); await db.SaveChangesAsync(ct);
                return new InvoiceDto { Id = invoice.Id, SubTotal = 20m, TotalAmount = 20m };
            });
        invoices.Setup(value => value.GetByIdAsync(It.IsAny<Guid>(), SalesOrderInvoiceGuard.Producer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceDto { Status = "Draft" });
        var service = new SalesOrderInvoiceService(db, actor.Object, invoices.Object);
        if (otherTenantRate)
        {
            var act = () => service.GenerateAsync(order.Id, request);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*exchange rate*current tenant*");
            invoices.VerifyNoOtherCalls();
        }
        else
        {
            await service.GenerateAsync(order.Id, request);
            captured.Should().NotBeNull();
            captured!.ExchangeRate.Should().Be(10m);
            captured.ExchangeRateId.Should().Be(rate.Id);
            (await db.SalesOrders.SingleAsync()).ExchangeRate.Should().Be(8m);
        }
    }

    [Theory]
    [InlineData("price")]
    [InlineData("description")]
    [InlineData("bin")]
    [InlineData("source")]
    [InlineData("producer")]
    [InlineData("invoice-fx")]
    [InlineData("source-fx")]
    public async Task Linked_invoice_rejects_tampering_or_wrong_producer(string change)
    {
        await using var db = Context();
        var tenant = Guid.NewGuid();
        var (order, account, _) = Source(tenant);
        var invoice = new Invoice { TenantId = tenant, BusinessPartnerId = order.BusinessPartnerId,
            InvoiceNumber = "SI-TEST", CurrencyCode = "GHS", Reference = order.DocumentNumber,
            LineItems = [new InvoiceLineItem { Id = order.Lines.Single().Id, TenantId = tenant,
                Description = "Survey service", Quantity = 2, UnitPrice = 10, GLAccountId = account.Id }] };
        invoice.LineItems.Single().InvoiceId = invoice.Id;
        order.InvoiceId = invoice.Id;
        order.InvoiceSourceJson = SalesOrderInvoiceGuard.SourceSnapshot(order);
        order.InvoiceEconomicsJson = SalesOrderInvoiceGuard.Snapshot(invoice);
        db.AddRange(order, account, invoice);
        await db.SaveChangesAsync();
        var uow = new UnitOfWork(db);
        await SalesOrderInvoiceGuard.ValidateAsync(uow, tenant, invoice, SalesOrderInvoiceGuard.Producer, default);
        var producer = SalesOrderInvoiceGuard.Producer;
        switch (change)
        {
            case "price": invoice.LineItems.Single().UnitPrice = 11; break;
            case "description": invoice.LineItems.Single().Description += " altered"; break;
            case "bin": invoice.LineItems.Single().LocationId = Guid.NewGuid(); break;
            case "source": order.Lines.Single().Quantity = 3; await db.SaveChangesAsync(); break;
            case "producer": producer = new(FinanceDimensionRouteId.FinanceArCustomerInvoice); break;
            case "invoice-fx": invoice.ExchangeRate += 1; break;
            case "source-fx": order.ExchangeRate += 1; await db.SaveChangesAsync(); break;
        }
        var act = () => SalesOrderInvoiceGuard.ValidateAsync(uow, tenant, invoice, producer, default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*retained source economics*");
        var edit = () => SalesOrderInvoiceGuard.RequireNotGeneratedAsync(uow, tenant, invoice.Id, default);
        await edit.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Unauthenticated_generation_is_denied_before_creating_an_invoice()
    {
        await using var db = Context();
        var invoices = new Mock<IInvoiceService>(MockBehavior.Strict);
        var service = new SalesOrderInvoiceService(db, Mock.Of<ICurrentUserProvider>(), invoices.Object);
        var act = () => service.GenerateAsync(Guid.NewGuid(), new GenerateSalesOrderInvoiceRequest());
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        invoices.VerifyNoOtherCalls();
        (await db.Invoices.CountAsync()).Should().Be(0);
    }

    private static (SalesOrder Order, Account Account, InvoiceCreateDto Dto) Source(Guid tenant)
    {
        var account = new Account { TenantId = tenant, AccountCode = "4000", AccountName = "Sales",
            AccountType = AccountType.Revenue, Status = AccountStatus.Active, AllowDirectPosting = true };
        var order = new SalesOrder { TenantId = tenant, BusinessPartnerId = Guid.NewGuid(), DocumentNumber = "SO-TEST",
            Currency = "GHS", OrderStatus = SalesOrderStatus.Confirmed, OrderType = SalesOrderType.Standard };
        var line = new SalesOrderLine { TenantId = tenant, SalesOrderId = order.Id, Quantity = 2, UnitPrice = 10,
            Description = "Survey service", Unit = "Each", GLAccountId = account.Id };
        order.Lines.Add(line);
        return (order, account, new InvoiceCreateDto { BusinessPartnerId = order.BusinessPartnerId, Reference = order.DocumentNumber,
            CurrencyCode = order.Currency, LineItems = [new InvoiceLineItemCreateDto { Id = line.Id, Quantity = line.Quantity,
                UnitPrice = line.UnitPrice, Description = line.Description, Unit = line.Unit, GLAccountId = account.Id,
                LineItemType = "GLAccount", TaxTreatment = TaxTreatment.Exempt }] });
    }
}
