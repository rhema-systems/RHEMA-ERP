using ErpSystem.Api.Controllers.Procurement;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public class BusinessPartnerProvisioningAuthorizationTests
{
    [Theory]
    [InlineData(typeof(BusinessPartnerUsersController), nameof(BusinessPartnerUsersController.LinkExisting))]
    [InlineData(typeof(BusinessPartnersController), nameof(BusinessPartnersController.AddLicense))]
    public void SetupEndpointsRequireTenantAdministration(Type controller, string method)
    {
        var attributes = controller.GetMethod(method)!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().ToList();
        Assert.Contains(attributes, a => a.Roles == "SuperAdmin,TenantAdmin");
        Assert.Empty(controller.GetMethod(method)!.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
    }
}
