using ErpSystem.Api.Services;
using ErpSystem.Api.Services.Finance.Settings;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public class TenantSettingsServiceTests
{
    [Fact]
    public async Task GetBaseCurrencyReferenceAsync_ShouldPreferFinanceBaseCurrencyRecord()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();

        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test Tenant",
            Code = "TEST",
            Status = TenantStatus.Active,
            BaseCurrency = "USD",
            BaseCurrencyName = "US Dollar",
            CurrencySymbol = "$",
            CurrencyDecimalPlaces = 2,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        });

        context.Currencies.Add(new Currency
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CurrencyCode = "GHS",
            NumericCode = "936",
            CurrencyName = "Ghana Cedi",
            CurrencySymbol = "₵",
            DecimalPlaces = 2,
            IsBaseCurrency = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        });

        await context.SaveChangesAsync();

        var service = new TenantSettingsService(context, CreateCurrentUserService(tenantId));

        var result = await service.GetBaseCurrencyReferenceAsync();

        result.CurrencyCode.Should().Be("GHS");
        result.CurrencyName.Should().Be("Ghana Cedi");
        result.CurrencySymbol.Should().Be("₵");
        result.DecimalPlaces.Should().Be(2);
    }

    [Fact]
    public async Task GetSettingsAsync_ShouldExposeBaseCurrencyMetadataFromFinanceCurrencySetup()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();

        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test Tenant",
            Code = "TEST",
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        });

        context.Currencies.Add(new Currency
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CurrencyCode = "EUR",
            NumericCode = "978",
            CurrencyName = "Euro",
            CurrencySymbol = "€",
            DecimalPlaces = 2,
            IsBaseCurrency = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        });

        await context.SaveChangesAsync();

        var currentUserService = CreateCurrentUserService(tenantId);
        var tenantSettingsService = new TenantSettingsService(context, currentUserService);
        var service = new FinanceSettingsService(context, currentUserService, tenantSettingsService);

        var result = await service.GetSettingsAsync();

        result.BaseCurrency.Should().Be("EUR");
        result.BaseCurrencyName.Should().Be("Euro");
        result.BaseCurrencySymbol.Should().Be("€");
        result.BaseCurrencyDecimalPlaces.Should().Be(2);
    }

    [Fact]
    public async Task UpdateSettingsAsync_ShouldPromoteRequestedCurrencyToBaseCurrencyAndSyncTenant()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var ghsId = Guid.NewGuid();
        var usdId = Guid.NewGuid();

        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test Tenant",
            Code = "TEST",
            Status = TenantStatus.Active,
            BaseCurrency = "GHS",
            BaseCurrencyName = "Ghana Cedi",
            CurrencySymbol = "₵",
            CurrencyDecimalPlaces = 2,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        });

        context.Currencies.AddRange(
            new Currency
            {
                Id = ghsId,
                TenantId = tenantId,
                CurrencyCode = "GHS",
                NumericCode = "936",
                CurrencyName = "Ghana Cedi",
                CurrencySymbol = "₵",
                DecimalPlaces = 2,
                IsBaseCurrency = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tests"
            },
            new Currency
            {
                Id = usdId,
                TenantId = tenantId,
                CurrencyCode = "USD",
                NumericCode = "840",
                CurrencyName = "US Dollar",
                CurrencySymbol = "$",
                DecimalPlaces = 2,
                IsBaseCurrency = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tests"
            });

        context.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CoaType = "Segmented",
            BaseCurrency = "GHS",
            AccountSeparator = "-",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        });

        await context.SaveChangesAsync();

        var currentUserService = CreateCurrentUserService(tenantId);
        var tenantSettingsService = new TenantSettingsService(context, currentUserService);
        var service = new FinanceSettingsService(context, currentUserService, tenantSettingsService);

        var result = await service.UpdateSettingsAsync(new UpdateFinanceSettingsDto
        {
            BaseCurrency = "usd"
        });

        result.BaseCurrency.Should().Be("USD");
        result.BaseCurrencyName.Should().Be("US Dollar");
        result.BaseCurrencySymbol.Should().Be("$");

        var currencies = await context.Currencies
            .Where(currency => currency.TenantId == tenantId)
            .OrderBy(currency => currency.CurrencyCode)
            .ToListAsync();

        currencies.Single(currency => currency.CurrencyCode == "USD").IsBaseCurrency.Should().BeTrue();
        currencies.Single(currency => currency.CurrencyCode == "GHS").IsBaseCurrency.Should().BeFalse();

        var tenant = await context.Tenants.SingleAsync(t => t.Id == tenantId);
        tenant.BaseCurrency.Should().Be("USD");
        tenant.BaseCurrencyName.Should().Be("US Dollar");
        tenant.CurrencySymbol.Should().Be("$");
        tenant.CurrencyDecimalPlaces.Should().Be(2);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ICurrentUserService CreateCurrentUserService(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserName).Returns("Tests");
        currentUser.SetupGet(service => service.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUser.SetupGet(service => service.Roles).Returns(Array.Empty<string>());
        return currentUser.Object;
    }
}
