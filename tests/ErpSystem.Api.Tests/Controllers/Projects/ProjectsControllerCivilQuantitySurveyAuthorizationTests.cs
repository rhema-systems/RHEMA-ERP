using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public sealed class ProjectsControllerCivilQuantitySurveyAuthorizationTests
{
    [Theory]
    [InlineData(nameof(ProjectsController.GetProjectDrawings), CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.AddProjectDrawing), CivilEngineeringAccessControlRegistry.DesignManage)]
    [InlineData(nameof(ProjectsController.UpdateProjectDrawing), CivilEngineeringAccessControlRegistry.DesignManage)]
    [InlineData(nameof(ProjectsController.DeleteProjectDrawing), CivilEngineeringAccessControlRegistry.DesignManage)]
    [InlineData(nameof(ProjectsController.GetProjectSubmittals), CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.AddProjectSubmittal), CivilEngineeringAccessControlRegistry.SupervisionManage)]
    [InlineData(nameof(ProjectsController.UpdateProjectSubmittal), CivilEngineeringAccessControlRegistry.SupervisionManage)]
    [InlineData(nameof(ProjectsController.DeleteProjectSubmittal), CivilEngineeringAccessControlRegistry.SupervisionManage)]
    [InlineData(nameof(ProjectsController.GetProjectRfis), CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.AddProjectRfi), CivilEngineeringAccessControlRegistry.SupervisionManage)]
    [InlineData(nameof(ProjectsController.UpdateProjectRfi), CivilEngineeringAccessControlRegistry.SupervisionManage)]
    [InlineData(nameof(ProjectsController.DeleteProjectRfi), CivilEngineeringAccessControlRegistry.SupervisionManage)]
    [InlineData(nameof(ProjectsController.GetProjectSiteInstructions), CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.AddProjectSiteInstruction), CivilEngineeringAccessControlRegistry.SupervisionManage)]
    [InlineData(nameof(ProjectsController.UpdateProjectSiteInstruction), CivilEngineeringAccessControlRegistry.SupervisionManage)]
    [InlineData(nameof(ProjectsController.DeleteProjectSiteInstruction), CivilEngineeringAccessControlRegistry.SupervisionManage)]
    [InlineData(nameof(ProjectsController.GetProjectExtensionOfTimeRequests), CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.AddProjectExtensionOfTimeRequest), CivilEngineeringAccessControlRegistry.CommercialManage)]
    [InlineData(nameof(ProjectsController.UpdateProjectExtensionOfTimeRequest), CivilEngineeringAccessControlRegistry.CommercialManage)]
    [InlineData(nameof(ProjectsController.DeleteProjectExtensionOfTimeRequest), CivilEngineeringAccessControlRegistry.CommercialManage)]
    [InlineData(nameof(ProjectsController.GetProjectVariationOrders), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.AddProjectVariationOrder), QuantitySurveyAccessControlRegistry.VariationsManage)]
    [InlineData(nameof(ProjectsController.UpdateProjectVariationOrder), QuantitySurveyAccessControlRegistry.VariationsManage)]
    [InlineData(nameof(ProjectsController.DeleteProjectVariationOrder), QuantitySurveyAccessControlRegistry.VariationsManage)]
    [InlineData(nameof(ProjectsController.GetProjectInterimValuations), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.AddProjectInterimValuation), QuantitySurveyAccessControlRegistry.ValuationsManage)]
    [InlineData(nameof(ProjectsController.UpdateProjectInterimValuation), QuantitySurveyAccessControlRegistry.ValuationsManage)]
    [InlineData(nameof(ProjectsController.DeleteProjectInterimValuation), QuantitySurveyAccessControlRegistry.ValuationsManage)]
    [InlineData(nameof(ProjectsController.GetProjectPaymentCertificates), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.AddProjectPaymentCertificate), QuantitySurveyAccessControlRegistry.CertificatesManage)]
    [InlineData(nameof(ProjectsController.UpdateProjectPaymentCertificate), QuantitySurveyAccessControlRegistry.CertificatesManage)]
    [InlineData(nameof(ProjectsController.DeleteProjectPaymentCertificate), QuantitySurveyAccessControlRegistry.CertificatesManage)]
    [InlineData(nameof(ProjectsController.GetProjectFinalAccount), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.UpsertProjectFinalAccount), QuantitySurveyAccessControlRegistry.FinalAccountsManage)]
    public void LegacyModuleRouteUsesItsRegisteredPermissionInsteadOfGenericRoles(
        string action,
        string expectedPermission)
    {
        var authorization = typeof(ProjectsController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>();

        authorization.Should().NotBeNull();
        authorization!.Policy.Should().Be(expectedPermission);
        authorization.Roles.Should().BeNullOrWhiteSpace();
    }
}
