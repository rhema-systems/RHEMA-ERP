using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class PartnerReceivablesDefaultsCompatibilityTests
{
    [Fact]
    public void PartialCustomerUpdate_ShouldPreserveOtherCustomerAndSupplierMappings()
    {
        var ar = Guid.NewGuid();
        var sales = Guid.NewGuid();
        var ap = Guid.NewGuid();
        var partner = new BusinessPartner { DefaultArAccountId = ar, CustomerSalesAccountId = sales, DefaultApAccountId = ap };
        var value = JsonSerializer.Deserialize<BusinessPartnerReceivablesDefaultsDto>(
            "{\"salesAccountId\":null}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        BusinessPartnerReceivablesDefaults.Apply(partner, value);

        partner.CustomerSalesAccountId.Should().BeNull();
        partner.DefaultArAccountId.Should().Be(ar);
        partner.DefaultApAccountId.Should().Be(ap);
    }

    [Fact]
    public void LegacyArOnlyUpdate_ShouldPreserveAdditionalCustomerAccounts()
    {
        var sales = Guid.NewGuid();
        var partner = new BusinessPartner { CustomerSalesAccountId = sales };
        var ar = Guid.NewGuid();
        BusinessPartnerReceivablesDefaults.Apply(partner, new() { DefaultArAccountId = ar });
        partner.DefaultArAccountId.Should().Be(ar);
        partner.CustomerSalesAccountId.Should().Be(sales);
    }
}
