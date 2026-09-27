using System.Net;
using System.Net.Http.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public class BusinessPartnersControllerContactRouteTests
{
    [Fact]
    public async Task SupplierAccountEndpointUsesCurrentUserAndOnlyReturnsPublicIdentity()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<IBusinessPartnerService>();
        service.Setup(value => value.GetByUserIdAsync(userId)).ReturnsAsync(new BusinessPartnerDetailDto
        {
            Id = Guid.NewGuid(), PartnerCode = "SUP260123", PartnerName = "Supplier One",
            PostingDefaults = new() { DefaultApAccountId = Guid.NewGuid() },
            ReceivablesDefaults = new() { DefaultArAccountId = Guid.NewGuid() }
        });
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(value => value.UserId).Returns(userId);
        current.SetupGet(value => value.IsExternalUser).Returns(true);
        var controller = new ErpSystem.Api.Controllers.Procurement.BusinessPartnersController(service.Object,
            current.Object, Mock.Of<IWorkflowService>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ErpSystem.Api.Controllers.Procurement.BusinessPartnersController>.Instance);

        var result = (Microsoft.AspNetCore.Mvc.OkObjectResult)await controller.GetMyAccount();
        using var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(result.Value));
        json.RootElement.GetProperty("PartnerCode").GetString().Should().Be("SUP260123");
        json.RootElement.EnumerateObject().Select(value => value.Name).Should().BeEquivalentTo("Id", "PartnerCode", "PartnerName");
        service.Verify(value => value.GetByUserIdAsync(userId), Times.Once);
    }

    [Fact]
    public async Task SupplierCannotLookUpAnotherUsersBusinessPartner()
    {
        var service = new Mock<IBusinessPartnerService>();
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(value => value.UserId).Returns(Guid.NewGuid());
        current.SetupGet(value => value.IsExternalUser).Returns(true);
        var controller = new ErpSystem.Api.Controllers.Procurement.BusinessPartnersController(service.Object,
            current.Object, Mock.Of<IWorkflowService>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ErpSystem.Api.Controllers.Procurement.BusinessPartnersController>.Instance)
        { ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() } };
        var result = await controller.GetPartnerByUserId(Guid.NewGuid());
        ((Microsoft.AspNetCore.Mvc.ObjectResult)result.Result!).StatusCode.Should().Be(403);
        service.Verify(value => value.GetByUserIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetContacts_ShouldReturnBusinessPartnerContacts()
    {
        var partnerId = Guid.NewGuid();
        var partnerService = new Mock<IBusinessPartnerService>();
        partnerService
            .Setup(x => x.GetContactsAsync(partnerId))
            .ReturnsAsync(new List<BusinessPartnerContactDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    BusinessPartnerId = partnerId,
                    ContactName = "Irene Mensah",
                    ContactTitle = "Commercial Director",
                    Email = "irene@atlas.test",
                    IsPrimary = true,
                    IsActive = true
                }
            });

        using var factory = CreateFactory(partnerService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/procurement/business-partners/{partnerId}/contacts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contacts = await response.Content.ReadFromJsonAsync<List<BusinessPartnerContactDto>>();
        contacts.Should().NotBeNull();
        contacts!.Should().ContainSingle(x => x.ContactName == "Irene Mensah" && x.IsPrimary);
    }

    [Fact]
    public async Task CreateContact_ShouldReturnCreatedContact()
    {
        var partnerId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var partnerService = new Mock<IBusinessPartnerService>();
        partnerService
            .Setup(x => x.AddContactAsync(partnerId, It.IsAny<CreateBusinessPartnerContactDto>()))
            .ReturnsAsync(new BusinessPartnerContactDto
            {
                Id = contactId,
                BusinessPartnerId = partnerId,
                ContactName = "Kojo Asare",
                ContactTitle = "Project Sponsor",
                Email = "kojo@atlas.test",
                IsPrimary = false,
                IsActive = true
            });

        using var factory = CreateFactory(partnerService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/procurement/business-partners/{partnerId}/contacts", new CreateBusinessPartnerContactDto
        {
            ContactName = "Kojo Asare",
            Title = "Project Sponsor",
            Email = "kojo@atlas.test",
            IsPrimary = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var contact = await response.Content.ReadFromJsonAsync<BusinessPartnerContactDto>();
        contact.Should().NotBeNull();
        contact!.Id.Should().Be(contactId);
        contact.ContactName.Should().Be("Kojo Asare");
    }

    [Fact]
    public async Task SetPrimaryContact_ShouldReturnNoContent()
    {
        var partnerId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var partnerService = new Mock<IBusinessPartnerService>();
        partnerService
            .Setup(x => x.SetPrimaryContactAsync(partnerId, contactId))
            .Returns(Task.CompletedTask);

        using var factory = CreateFactory(partnerService);
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/procurement/business-partners/{partnerId}/contacts/{contactId}/set-primary", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static WebApplicationFactory<Program> CreateFactory(Mock<IBusinessPartnerService> partnerService)
    {
        var workflowService = new Mock<IWorkflowService>();

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("CandidatePortal:PortalUrl", "https://candidate.test/");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IAuthorizationHandler>();
                services.RemoveAll<IBusinessPartnerService>();
                services.RemoveAll<IWorkflowService>();
                services.RemoveAll<ICurrentUserProvider>();

                services.AddSingleton<IPolicyEvaluator, ProcurementTestPolicyEvaluator>();
                services.AddSingleton<IAuthorizationHandler, ProcurementAllowAnonymousHandler>();
                services.AddSingleton(partnerService.Object);
                services.AddSingleton(workflowService.Object);
                services.AddSingleton<ICurrentUserProvider>(new ProcurementFakeCurrentUserProvider(Guid.NewGuid(), Guid.NewGuid()));
            });
        });
    }
}

internal sealed class ProcurementTestPolicyEvaluator : IPolicyEvaluator
{
    public Task<AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, Microsoft.AspNetCore.Http.HttpContext context)
        => Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(), "Test")));

    public Task<PolicyAuthorizationResult> AuthorizeAsync(AuthorizationPolicy policy, AuthenticateResult authenticationResult, Microsoft.AspNetCore.Http.HttpContext context, object? resource)
        => Task.FromResult(PolicyAuthorizationResult.Success());
}

internal sealed class ProcurementAllowAnonymousHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        foreach (var requirement in context.Requirements)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

internal sealed class ProcurementFakeCurrentUserProvider : ICurrentUserProvider
{
    public ProcurementFakeCurrentUserProvider(Guid userId, Guid tenantId)
    {
        UserId = userId;
        TenantId = tenantId;
    }

    public Guid UserId { get; }
    public Guid TenantId { get; }
    public string Username => "test.user@erp.local";
    public string FullName => "Test User";
    public bool IsAuthenticated => true;
    public IEnumerable<string> Roles => Array.Empty<string>();
    public IDictionary<string, string> Claims => new Dictionary<string, string>();
    public bool IsExternalUser => false;
    public string AuthenticationProvider => "Test";

    public bool HasRole(string role) => false;
}
