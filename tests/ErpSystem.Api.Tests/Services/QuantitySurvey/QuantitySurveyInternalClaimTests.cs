using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Api.Services.QuantitySurvey;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyInternalClaimTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Staff_intake_requires_project_access_and_tenant_owned_contract(bool access, bool otherTenant)
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = Guid.NewGuid(); var project = Guid.NewGuid();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns(tenant);
        user.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        var projects = new Mock<IProjectService>();
        projects.Setup(x => x.HasProjectAccessAsync(project)).ReturnsAsync(access);
        var requisition = new PurchaseRequisition { TenantId = tenant, ProjectId = otherTenant ? project : Guid.NewGuid() };
        var tender = new Tender { TenantId = tenant, SourcePurchaseRequisition = requisition, SourcePurchaseRequisitionId = requisition.Id };
        var contract = new Contract { TenantId = otherTenant ? Guid.NewGuid() : tenant,
            ContractType = "Works", Status = "Active", BusinessPartnerId = Guid.NewGuid(), Tender = tender, TenderId = tender.Id };
        db.Add(contract); await db.SaveChangesAsync();
        var service = new QuantitySurveyContractClaimService(db, user.Object, projects.Object,
            Mock.Of<IWorkflowIntegrationService>(), Mock.Of<IWorkflowStatusAdapterRegistry>(),
            Mock.Of<IControlledFileUploadService>(), Mock.Of<ICentralDocumentRepositoryFileService>());
        Func<Task> action = () => service.SaveInternalAsync(project, new SaveQuantitySurveyContractClaimRequest
        { ClientRequestId = Guid.NewGuid(), ContractId = contract.Id, Title = "UAT claim", Basis = "Synthetic recorded claim",
            ClaimType = ErpSystem.Core.Entities.QuantitySurvey.QuantitySurveyContractClaimType.LossAndExpense, ClaimedAmount = 100 }, "uat");
        if (!access) await action.Should().ThrowAsync<UnauthorizedAccessException>();
        else await action.Should().ThrowAsync<QuantitySurveyContractClaimValidationException>().WithMessage("*active Procurement Works contract*");
        (await db.QuantitySurveyContractClaims.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("Save")]
    [InlineData("Submit")]
    public void Staff_intake_endpoints_require_claim_management_permission(string method)
    {
        var action = typeof(QuantitySurveyContractClaimsController).GetMethod(method)!;
        action.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()
            .Should().Contain(x => x.Policy == QuantitySurveyAccessControlRegistry.ClaimsManage);
    }

    [Fact]
    public async Task External_intake_still_requires_a_linked_partner_identity()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        user.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        var service = new QuantitySurveyContractClaimService(db, user.Object, Mock.Of<IProjectService>(),
            Mock.Of<IWorkflowIntegrationService>(), Mock.Of<IWorkflowStatusAdapterRegistry>(),
            Mock.Of<IControlledFileUploadService>(), Mock.Of<ICentralDocumentRepositoryFileService>());
        Func<Task> action = () => service.SaveExternalAsync(Guid.NewGuid(), new SaveQuantitySurveyContractClaimRequest(), "uat");
        await action.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*business-partner identity*");
        (await db.QuantitySurveyContractClaims.CountAsync()).Should().Be(0);
    }
}
