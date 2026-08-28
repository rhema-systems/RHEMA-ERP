using System.Net;
using System.Net.Http.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public class ProjectsControllerRouteTests
{
    [Fact]
    public async Task CreateProject_ShouldReturnCreatedProjectDetail()
    {
        var projectId = Guid.NewGuid();
        var request = new CreateProjectDto
        {
            Title = "New Delivery Program",
            Methodology = "Hybrid",
            EstimatedBudget = 150000m,
            StartDate = new DateTime(2026, 3, 10),
            TargetEndDate = new DateTime(2026, 8, 31),
            ApprovalRequired = true
        };

        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.CreateProjectAsync(It.Is<CreateProjectDto>(dto => dto.Title == request.Title)))
            .ReturnsAsync(new ProjectDetailDto
            {
                Id = projectId,
                Title = request.Title,
                Methodology = request.Methodology,
                EstimatedBudget = request.EstimatedBudget,
                Status = "Draft",
                ApprovalRequired = true,
                ProjectCode = "PRJ-2026-0200",
                CreatedAt = DateTime.UtcNow
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/projects", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectDetailDto>();
        project.Should().NotBeNull();
        project!.Id.Should().Be(projectId);
        project.Title.Should().Be(request.Title);
        project.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task GetProject_ShouldReturnMockedProjectDetail()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetProjectByIdAsync(projectId))
            .ReturnsAsync(new ProjectDetailDto
            {
                Id = projectId,
                ProjectCode = "PRJ-2026-0001",
                Title = "Route Test Project",
                Status = "Planned",
                ApprovalRequired = true,
                ExternalPortalAccessEnabled = true,
                ExternalCollaborationEnabled = false,
                CreatedAt = DateTime.UtcNow
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var project = await response.Content.ReadFromJsonAsync<ProjectDetailDto>();
        project.Should().NotBeNull();
        project!.Id.Should().Be(projectId);
        project.Title.Should().Be("Route Test Project");
        project.ProjectCode.Should().Be("PRJ-2026-0001");
    }

    [Fact]
    public async Task GetProjectWorkspace_ShouldReturnAggregatedProjectOverview()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetProjectWorkspaceAsync(projectId))
            .ReturnsAsync(new ProjectWorkspaceDto
            {
                Project = new ProjectDetailDto
                {
                    Id = projectId,
                    ProjectCode = "PRJ-2026-0042",
                    Title = "Workspace Route Project",
                    Status = "InProgress",
                    CreatedAt = DateTime.UtcNow
                },
                FinancialSummary = new ProjectFinancialControlSummaryDto
                {
                    ProjectId = projectId,
                    BudgetBaseline = 1000m,
                    ActualCost = 250m,
                    HealthStatus = "Healthy"
                },
                IntegrationSummary = new ProjectIntegrationSummaryDto
                {
                    ProjectId = projectId,
                    HasBusinessPartner = true,
                    HasContract = true,
                    HasPortfolio = true,
                    HasProgram = true
                },
                GovernanceSummary = new ProjectGovernanceSummaryDto
                {
                    ProjectId = projectId,
                    OpenRiskCount = 2,
                    OpenIssueCount = 1
                },
                LinkOptions = new ProjectLinkOptionsDto
                {
                    SalesAgreements = new List<ProjectSalesAgreementLinkOptionDto>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            BusinessPartnerId = Guid.NewGuid(),
                            DocumentNumber = "AGR-2026-004",
                            AgreementTitle = "Apartment Sale Agreement",
                            CustomerName = "Kojo Mensah",
                            AgreementStatus = "Active"
                        }
                    }
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/workspace");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var workspace = await response.Content.ReadFromJsonAsync<ProjectWorkspaceDto>();
        workspace.Should().NotBeNull();
        workspace!.Project.Id.Should().Be(projectId);
        workspace.Project.Title.Should().Be("Workspace Route Project");
        workspace.FinancialSummary.Should().NotBeNull();
        workspace.FinancialSummary!.BudgetBaseline.Should().Be(1000m);
        workspace.IntegrationSummary.Should().NotBeNull();
        workspace.IntegrationSummary!.HasPortfolio.Should().BeTrue();
        workspace.GovernanceSummary.Should().NotBeNull();
        workspace.GovernanceSummary!.OpenRiskCount.Should().Be(2);
        workspace.LinkOptions.Should().NotBeNull();
        workspace.LinkOptions.SalesAgreements.Should().ContainSingle(x => x.DocumentNumber == "AGR-2026-004");
    }

    [Fact]
    public async Task GetProjectLinkOptions_ShouldReturnProjectScopedReferences()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetProjectLinkOptionsAsync(projectId))
            .ReturnsAsync(new ProjectLinkOptionsDto
            {
                SalesAgreements = new List<ProjectSalesAgreementLinkOptionDto>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        BusinessPartnerId = Guid.NewGuid(),
                        DocumentNumber = "AGR-2026-010",
                        AgreementTitle = "Penthouse Agreement",
                        CustomerName = "Ama Owusu",
                        AgreementStatus = "Active"
                    }
                },
                WorkOrders = new List<ProjectWorkOrderLinkOptionDto>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        AssetId = Guid.NewGuid(),
                        WorkOrderNumber = "WO-2026-017",
                        Title = "Post-handover rectification",
                        Status = "Open"
                    }
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/link-options");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var options = await response.Content.ReadFromJsonAsync<ProjectLinkOptionsDto>();
        options.Should().NotBeNull();
        options!.SalesAgreements.Should().ContainSingle(x => x.DocumentNumber == "AGR-2026-010");
        options.WorkOrders.Should().ContainSingle(x => x.WorkOrderNumber == "WO-2026-017");
    }

    [Fact]
    public async Task GetReleasedProjectUnitsForSales_ShouldReturnReleasedUnits()
    {
        var unitId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetReleasedProjectUnitsForSalesAsync("tower", 25))
            .ReturnsAsync(new List<ProjectReleasedUnitSalesLookupDto>
            {
                new()
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-2026-021",
                    ProjectTitle = "Airport View Towers",
                    ProjectUnitId = unitId,
                    ProjectUnitCode = "A-12",
                    ProjectUnitName = "Apartment A-12",
                    ProjectUnitType = "Apartment",
                    ProjectUnitStatus = "Available",
                    CommercialStatus = "Available",
                    HandoverStatus = "NotScheduled",
                    Currency = "GHS",
                    CanCreateSalesAgreement = true,
                    CanCreateLeaseAgreement = true,
                    CanCreateSalesOrder = true
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/units/released-market?search=tower&take=25");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ProjectReleasedUnitSalesLookupDto>>();
        items.Should().NotBeNull();
        items!.Should().ContainSingle();
        items[0].ProjectCode.Should().Be("PRJ-2026-021");
        items[0].ProjectUnitCode.Should().Be("A-12");
        items[0].CanCreateSalesOrder.Should().BeTrue();
    }

    [Fact]
    public async Task ReleaseProjectUnit_ShouldReturnUpdatedUnit()
    {
        var unitId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.ReleaseProjectUnitAsync(unitId))
            .ReturnsAsync(new ProjectUnitDto
            {
                Id = unitId,
                ProjectId = Guid.NewGuid(),
                Name = "Apartment B2",
                Status = "Available",
                CommercialStatus = "Available",
                HandoverStatus = "NotScheduled",
                Currency = "USD",
                SortOrder = 1,
                IsReleasedForMarket = true,
                ReleasedByDisplayName = "Project Sales Admin"
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/projects/units/{unitId}/release", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var unit = await response.Content.ReadFromJsonAsync<ProjectUnitDto>();
        unit.Should().NotBeNull();
        unit!.IsReleasedForMarket.Should().BeTrue();
        unit.ReleasedByDisplayName.Should().Be("Project Sales Admin");
    }

    [Fact]
    public async Task CreateSalesAgreementFromProjectUnit_ShouldReturnUpdatedUnit()
    {
        var unitId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.CreateSalesAgreementFromProjectUnitAsync(unitId))
            .ReturnsAsync(new ProjectUnitDto
            {
                Id = unitId,
                ProjectId = Guid.NewGuid(),
                Name = "Apartment C3",
                Status = "Reserved",
                CommercialStatus = "Reserved",
                HandoverStatus = "Pending",
                Currency = "USD",
                SortOrder = 1,
                IsReleasedForMarket = true,
                SalesAgreementId = Guid.NewGuid(),
                SalesAgreementNumber = "AGR-UNIT-002"
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/projects/units/{unitId}/create-sales-agreement", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var unit = await response.Content.ReadFromJsonAsync<ProjectUnitDto>();
        unit.Should().NotBeNull();
        unit!.Status.Should().Be("Reserved");
        unit.SalesAgreementNumber.Should().Be("AGR-UNIT-002");
    }

    [Fact]
    public async Task CreateLeaseAgreementFromProjectUnit_ShouldReturnUpdatedUnit()
    {
        var unitId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.CreateLeaseAgreementFromProjectUnitAsync(unitId))
            .ReturnsAsync(new ProjectUnitDto
            {
                Id = unitId,
                ProjectId = Guid.NewGuid(),
                Name = "Apartment L3",
                Status = "Reserved",
                CommercialStatus = "Reserved",
                HandoverStatus = "Pending",
                Currency = "USD",
                SortOrder = 1,
                IsReleasedForMarket = true,
                SalesAgreementId = Guid.NewGuid(),
                SalesAgreementNumber = "AGR-LEASE-003",
                SalesAgreementType = "TenancyAgreement"
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/projects/units/{unitId}/create-lease-agreement", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var unit = await response.Content.ReadFromJsonAsync<ProjectUnitDto>();
        unit.Should().NotBeNull();
        unit!.Status.Should().Be("Reserved");
        unit.SalesAgreementNumber.Should().Be("AGR-LEASE-003");
        unit.SalesAgreementType.Should().Be("TenancyAgreement");
    }

    [Fact]
    public async Task CreateSalesOrderFromProjectUnit_ShouldReturnUpdatedUnit()
    {
        var unitId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.CreateSalesOrderFromProjectUnitAsync(unitId))
            .ReturnsAsync(new ProjectUnitDto
            {
                Id = unitId,
                ProjectId = Guid.NewGuid(),
                Name = "Apartment D1",
                Status = "Reserved",
                CommercialStatus = "Reserved",
                HandoverStatus = "Pending",
                Currency = "USD",
                SortOrder = 1,
                IsReleasedForMarket = true,
                SalesOrderId = Guid.NewGuid(),
                SalesOrderNumber = "SO-UNIT-003"
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/projects/units/{unitId}/create-sales-order", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var unit = await response.Content.ReadFromJsonAsync<ProjectUnitDto>();
        unit.Should().NotBeNull();
        unit!.Status.Should().Be("Reserved");
        unit.SalesOrderNumber.Should().Be("SO-UNIT-003");
    }

    [Fact]
    public async Task LinkSalesAgreementToProjectUnit_ShouldReturnUpdatedUnit()
    {
        var unitId = Guid.NewGuid();
        var agreementId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.LinkSalesAgreementToProjectUnitAsync(unitId, agreementId))
            .ReturnsAsync(new ProjectUnitDto
            {
                Id = unitId,
                ProjectId = Guid.NewGuid(),
                Name = "Apartment E2",
                Status = "Reserved",
                CommercialStatus = "Reserved",
                CommercialIntent = "Sale",
                HandoverStatus = "Pending",
                Currency = "USD",
                SortOrder = 1,
                IsReleasedForMarket = true,
                SalesAgreementId = agreementId,
                SalesAgreementNumber = "AGR-UNIT-004"
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/projects/units/{unitId}/link-sales-agreement", new LinkProjectUnitSalesAgreementDto
        {
            SalesAgreementId = agreementId
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var unit = await response.Content.ReadFromJsonAsync<ProjectUnitDto>();
        unit.Should().NotBeNull();
        unit!.SalesAgreementId.Should().Be(agreementId);
        unit.CommercialIntent.Should().Be("Sale");
    }

    [Fact]
    public async Task LinkSalesOrderToProjectUnit_ShouldReturnUpdatedUnit()
    {
        var unitId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.LinkSalesOrderToProjectUnitAsync(unitId, orderId))
            .ReturnsAsync(new ProjectUnitDto
            {
                Id = unitId,
                ProjectId = Guid.NewGuid(),
                Name = "Apartment F1",
                Status = "Reserved",
                CommercialStatus = "Reserved",
                CommercialIntent = "Sale",
                HandoverStatus = "Pending",
                Currency = "USD",
                SortOrder = 1,
                IsReleasedForMarket = true,
                SalesOrderId = orderId,
                SalesOrderNumber = "SO-UNIT-004"
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/projects/units/{unitId}/link-sales-order", new LinkProjectUnitSalesOrderDto
        {
            SalesOrderId = orderId
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var unit = await response.Content.ReadFromJsonAsync<ProjectUnitDto>();
        unit.Should().NotBeNull();
        unit!.SalesOrderId.Should().Be(orderId);
        unit.CommercialIntent.Should().Be("Sale");
    }

    [Fact]
    public async Task GetDevelopmentProfile_ShouldReturnConstructionProfile()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetDevelopmentProfileAsync(projectId))
            .ReturnsAsync(new ProjectDevelopmentProfileDto
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                DeliveryStructure = "MultiUnit",
                DevelopmentType = "Residential",
                SiteName = "Airport Hills"
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/development-profile");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<ProjectDevelopmentProfileDto>();
        profile.Should().NotBeNull();
        profile!.ProjectId.Should().Be(projectId);
        profile.DeliveryStructure.Should().Be("MultiUnit");
    }

    [Fact]
    public async Task GetProjectPhases_ShouldReturnLifecyclePhases()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetProjectPhasesAsync(projectId))
            .ReturnsAsync(new[]
            {
                new ProjectPhaseDto
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Name = "Feasibility",
                    Status = "NotStarted",
                    SortOrder = 0,
                    Children = new List<ProjectPhaseDto>()
                },
                new ProjectPhaseDto
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Name = "Construction",
                    Status = "InProgress",
                    SortOrder = 1,
                    Children = new List<ProjectPhaseDto>()
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/phases");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var phases = await response.Content.ReadFromJsonAsync<List<ProjectPhaseDto>>();
        phases.Should().NotBeNull();
        phases.Should().HaveCount(2);
        phases![1].Name.Should().Be("Construction");
    }

    [Fact]
    public async Task AdvanceProjectPhase_ShouldReturnProgressionResult()
    {
        var phaseId = Guid.NewGuid();
        var nextPhaseId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.AdvanceProjectPhaseAsync(
                phaseId,
                It.Is<AdvanceProjectPhaseDto>(dto => dto.StartNextPhase)))
            .ReturnsAsync(new ProjectPhaseProgressionResultDto
            {
                Action = "Completed",
                Message = "Phase 'Detailed Design' completed and 'Approvals & Permits' started.",
                StageGateEvaluated = true,
                StageGatePassed = true,
                NextPhaseStarted = true,
                Phase = new ProjectPhaseDto
                {
                    Id = phaseId,
                    ProjectId = projectId,
                    Name = "Detailed Design",
                    Status = "Completed",
                    SortOrder = 2,
                    Children = new List<ProjectPhaseDto>()
                },
                NextPhase = new ProjectPhaseDto
                {
                    Id = nextPhaseId,
                    ProjectId = projectId,
                    Name = "Approvals & Permits",
                    Status = "InProgress",
                    SortOrder = 3,
                    Children = new List<ProjectPhaseDto>()
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/projects/phases/{phaseId}/advance", new AdvanceProjectPhaseDto
        {
            StartNextPhase = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProjectPhaseProgressionResultDto>();
        result.Should().NotBeNull();
        result!.Phase.Status.Should().Be("Completed");
        result.NextPhaseStarted.Should().BeTrue();
        result.NextPhase.Should().NotBeNull();
        result.NextPhase!.Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task GetProjectPackages_ShouldReturnPackageRegister()
    {
        var projectId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetProjectPackagesAsync(projectId))
            .ReturnsAsync(new[]
            {
                new ProjectPackageDto
                {
                    Id = packageId,
                    ProjectId = projectId,
                    Name = "Substructure",
                    PackageType = "TradePackage",
                    Status = "Planned",
                    Currency = "USD",
                    BoqItems = new List<ProjectBoqItemDto>()
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/packages");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var packages = await response.Content.ReadFromJsonAsync<List<ProjectPackageDto>>();
        packages.Should().NotBeNull();
        packages.Should().HaveCount(1);
        packages![0].Id.Should().Be(packageId);
        packages[0].Name.Should().Be("Substructure");
    }

    [Fact]
    public async Task GetProjectBoqItems_ShouldReturnBoqLines()
    {
        var projectId = Guid.NewGuid();
        var boqItemId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetProjectBoqItemsAsync(projectId))
            .ReturnsAsync(new[]
            {
                new ProjectBoqItemDto
                {
                    Id = boqItemId,
                    ProjectId = projectId,
                    ProjectPackageId = packageId,
                    SectionCode = "A",
                    TradeCode = "CONC",
                    CostCode = "CC-100",
                    MeasurementStandard = "Cesmm4",
                    MeasurementCode = "E20",
                    Description = "Excavation",
                    ItemType = "Item",
                    Quantity = 120,
                    Currency = "USD",
                    SortOrder = 0
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/boq-items");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ProjectBoqItemDto>>();
        items.Should().NotBeNull();
        items.Should().HaveCount(1);
        items![0].Id.Should().Be(boqItemId);
        items[0].Description.Should().Be("Excavation");
        items[0].SectionCode.Should().Be("A");
        items[0].TradeCode.Should().Be("CONC");
        items[0].MeasurementStandard.Should().Be("Cesmm4");
    }

    [Fact]
    public async Task GetProjectBoqClassifications_ShouldReturnEffectiveControlledOptions()
    {
        var projectId = Guid.NewGuid();
        var tradeId = Guid.NewGuid();
        var effectiveAt = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc);
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetProjectBoqClassificationOptionsAsync(projectId, effectiveAt))
            .ReturnsAsync(new ProjectBoqClassificationOptionsDto
            {
                EffectiveAtUtc = effectiveAt,
                Trades =
                [
                    new ProjectBoqClassificationOptionDto
                    {
                        Id = tradeId,
                        CatalogType = "qs-trades",
                        Code = "CONC",
                        Name = "Concrete work"
                    }
                ]
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/boq-classifications?effectiveAtUtc={Uri.EscapeDataString(effectiveAt.ToString("O"))}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var options = await response.Content.ReadFromJsonAsync<ProjectBoqClassificationOptionsDto>();
        options.Should().NotBeNull();
        options!.EffectiveAtUtc.Should().Be(effectiveAt);
        options.Trades.Should().ContainSingle();
        options.Trades[0].Id.Should().Be(tradeId);
        options.Trades[0].Code.Should().Be("CONC");
    }

    [Fact]
    public async Task GetApprovalRegister_ShouldReturnApprovalItems()
    {
        var projectId = Guid.NewGuid();
        var approvalId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetApprovalRegisterAsync(projectId))
            .ReturnsAsync(new[]
            {
                new ProjectApprovalRegisterItemDto
                {
                    Id = approvalId,
                    ProjectId = projectId,
                    ApprovalType = "BuildingPermit",
                    Title = "Building permit approval",
                    Status = "Submitted"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/approval-register");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var approvals = await response.Content.ReadFromJsonAsync<List<ProjectApprovalRegisterItemDto>>();
        approvals.Should().NotBeNull();
        approvals.Should().HaveCount(1);
        approvals![0].Id.Should().Be(approvalId);
        approvals[0].Title.Should().Be("Building permit approval");
    }

    [Fact]
    public async Task GetCommercialSummary_ShouldReturnPhaseCostRollups()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetCommercialSummaryAsync(projectId))
            .ReturnsAsync(new ProjectCommercialSummaryDto
            {
                ProjectId = projectId,
                Currency = "GHS",
                PackageBudgetAmount = 250000m,
                PackageForecastAmount = 265000m,
                PhaseRollups = new List<ProjectPhaseCommercialRollupDto>
                {
                    new()
                    {
                        ProjectPhaseId = Guid.NewGuid(),
                        PhaseName = "Construction",
                        PackageCount = 2,
                        BudgetAmount = 250000m,
                        ForecastAmount = 265000m
                    }
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/commercial-summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<ProjectCommercialSummaryDto>();
        summary.Should().NotBeNull();
        summary!.ProjectId.Should().Be(projectId);
        summary.Currency.Should().Be("GHS");
        summary.PhaseRollups.Should().ContainSingle(x => x.PhaseName == "Construction");
    }

    [Fact]
    public async Task GetProjectUnits_ShouldReturnUnitSchedule()
    {
        var projectId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetProjectUnitsAsync(projectId))
            .ReturnsAsync(new[]
            {
                new ProjectUnitDto
                {
                    Id = unitId,
                    ProjectId = projectId,
                    Code = "APT-12B",
                    Name = "Apartment 12B",
                    UnitType = "Apartment",
                    Status = "Available",
                    AreaSquareMeters = 142.5m,
                    Currency = "GHS"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/units");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var units = await response.Content.ReadFromJsonAsync<List<ProjectUnitDto>>();
        units.Should().NotBeNull();
        units.Should().HaveCount(1);
        units![0].Id.Should().Be(unitId);
        units[0].Name.Should().Be("Apartment 12B");
    }

    [Fact]
    public async Task GetCustomerVariations_ShouldReturnVariationItems()
    {
        var projectId = Guid.NewGuid();
        var variationId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetCustomerVariationsAsync(projectId))
            .ReturnsAsync(new[]
            {
                new ProjectCustomerVariationDto
                {
                    Id = variationId,
                    ProjectId = projectId,
                    Title = "Kitchen finish upgrade",
                    Timing = "PreHandover",
                    Status = "Quoted",
                    Currency = "GHS"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/customer-variations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var variations = await response.Content.ReadFromJsonAsync<List<ProjectCustomerVariationDto>>();
        variations.Should().NotBeNull();
        variations.Should().HaveCount(1);
        variations![0].Id.Should().Be(variationId);
        variations[0].Title.Should().Be("Kitchen finish upgrade");
    }

    [Fact]
    public async Task CreateCustomerVariationJobCard_ShouldReturnUpdatedVariation()
    {
        var variationId = Guid.NewGuid();
        var jobCardId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.CreateJobCardFromCustomerVariationAsync(
                variationId,
                It.IsAny<CreateProjectMaintenanceFollowThroughDto?>()))
            .ReturnsAsync(new ProjectCustomerVariationDto
            {
                Id = variationId,
                ProjectId = Guid.NewGuid(),
                Title = "Kitchen finish upgrade",
                Timing = "PostHandover",
                Status = "Approved",
                JobCardId = jobCardId,
                JobCardNumber = "JC-2026-0007",
                Currency = "GHS"
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/projects/customer-variations/{variationId}/create-job-card", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var variation = await response.Content.ReadFromJsonAsync<ProjectCustomerVariationDto>();
        variation.Should().NotBeNull();
        variation!.JobCardId.Should().Be(jobCardId);
        variation.JobCardNumber.Should().Be("JC-2026-0007");
    }

    [Fact]
    public async Task GetProjectCommissioningItems_ShouldReturnCommissioningItems()
    {
        var projectId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetProjectCommissioningItemsAsync(projectId))
            .ReturnsAsync(new[]
            {
                new ProjectCommissioningItemDto
                {
                    Id = itemId,
                    ProjectId = projectId,
                    Title = "Electrical final test",
                    Status = "ReadyForInspection"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/commissioning");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ProjectCommissioningItemDto>>();
        items.Should().NotBeNull();
        items.Should().HaveCount(1);
        items![0].Id.Should().Be(itemId);
        items[0].Title.Should().Be("Electrical final test");
    }

    [Fact]
    public async Task GetProjectHandoverItems_ShouldReturnHandoverItems()
    {
        var projectId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetProjectHandoverItemsAsync(projectId))
            .ReturnsAsync(new[]
            {
                new ProjectHandoverItemDto
                {
                    Id = itemId,
                    ProjectId = projectId,
                    HandoverType = "PracticalCompletion",
                    Title = "Practical completion certificate",
                    Status = "Ready"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/handover-items");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ProjectHandoverItemDto>>();
        items.Should().NotBeNull();
        items.Should().HaveCount(1);
        items![0].Id.Should().Be(itemId);
        items[0].Title.Should().Be("Practical completion certificate");
    }

    [Fact]
    public async Task GetProjectSnagItems_ShouldReturnSnagItems()
    {
        var projectId = Guid.NewGuid();
        var snagId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetProjectSnagItemsAsync(projectId))
            .ReturnsAsync(new[]
            {
                new ProjectSnagItemDto
                {
                    Id = snagId,
                    ProjectId = projectId,
                    Title = "Tile crack at lobby",
                    Severity = "High",
                    Status = "Open",
                    ReportedDate = DateTime.UtcNow
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/snag-items");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ProjectSnagItemDto>>();
        items.Should().NotBeNull();
        items.Should().HaveCount(1);
        items![0].Id.Should().Be(snagId);
        items[0].Title.Should().Be("Tile crack at lobby");
    }

    [Fact]
    public async Task GetProjectDefectLiabilityCases_ShouldReturnCases()
    {
        var projectId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetProjectDefectLiabilityCasesAsync(projectId))
            .ReturnsAsync(new[]
            {
                new ProjectDefectLiabilityCaseDto
                {
                    Id = caseId,
                    ProjectId = projectId,
                    Title = "Water ingress on penthouse roof",
                    Status = "Reported",
                    ReportedDate = DateTime.UtcNow,
                    Currency = "GHS"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/defect-liability");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ProjectDefectLiabilityCaseDto>>();
        items.Should().NotBeNull();
        items.Should().HaveCount(1);
        items![0].Id.Should().Be(caseId);
        items[0].Title.Should().Be("Water ingress on penthouse roof");
    }

    [Fact]
    public async Task CreateDefectLiabilityWorkOrder_ShouldReturnUpdatedCase()
    {
        var defectCaseId = Guid.NewGuid();
        var workOrderId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.CreateWorkOrderFromDefectLiabilityCaseAsync(
                defectCaseId,
                It.IsAny<CreateProjectMaintenanceFollowThroughDto?>()))
            .ReturnsAsync(new ProjectDefectLiabilityCaseDto
            {
                Id = defectCaseId,
                ProjectId = Guid.NewGuid(),
                Title = "Water ingress on penthouse roof",
                Status = "InProgress",
                WorkOrderId = workOrderId,
                WorkOrderNumber = "WO-2026-0012",
                ReportedDate = DateTime.UtcNow,
                Currency = "GHS"
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/projects/defect-liability/{defectCaseId}/create-work-order", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<ProjectDefectLiabilityCaseDto>();
        item.Should().NotBeNull();
        item!.WorkOrderId.Should().Be(workOrderId);
        item.WorkOrderNumber.Should().Be("WO-2026-0012");
    }

    [Fact]
    public async Task GetTenderLookup_ShouldReturnTenderOptions()
    {
        var tenderId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetTenderLookupAsync("tower"))
            .ReturnsAsync(new[]
            {
                new ProjectTenderLookupDto
                {
                    Id = tenderId,
                    TenderNumber = "TND-2026-0004",
                    Title = "Tower Works Package",
                    Status = "Published"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/tenders/lookup?search=tower");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tenders = await response.Content.ReadFromJsonAsync<List<ProjectTenderLookupDto>>();
        tenders.Should().NotBeNull();
        tenders.Should().HaveCount(1);
        tenders![0].Id.Should().Be(tenderId);
        tenders[0].Title.Should().Be("Tower Works Package");
    }

    [Fact]
    public async Task GetAiInsights_ShouldReturnMockedInsights()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetAiInsightsAsync(projectId))
            .ReturnsAsync(new[]
            {
                new ProjectAiInsightDto
                {
                    Category = "Risk",
                    Severity = "High",
                    Title = "Schedule compression risk",
                    Recommendation = "Re-sequence critical tasks."
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/ai-insights");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var insights = await response.Content.ReadFromJsonAsync<List<ProjectAiInsightDto>>();
        insights.Should().NotBeNull();
        insights.Should().HaveCount(1);
        insights![0].Severity.Should().Be("High");
        insights[0].Title.Should().Be("Schedule compression risk");
    }

    [Fact]
    public async Task SubmitAndApproveProject_ShouldForwardCurrentUserAndComments()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.SubmitProjectForApprovalAsync(projectId, userId))
            .Returns(Task.CompletedTask);
        projectService
            .Setup(service => service.ApproveProjectAsync(projectId, userId, "Looks good"))
            .Returns(Task.CompletedTask);

        using var factory = CreateFactory(projectService, userId);
        using var client = factory.CreateClient();

        var submitResponse = await client.PostAsync($"/api/projects/{projectId}/submit", content: null);
        var approveResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/approve", new { comments = "Looks good" });

        submitResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        projectService.Verify(service => service.SubmitProjectForApprovalAsync(projectId, userId), Times.Once);
        projectService.Verify(service => service.ApproveProjectAsync(projectId, userId, "Looks good"), Times.Once);
    }

    [Fact]
    public async Task BudgetRevisionWorkflow_ShouldCreateSubmitAndApproveRevision()
    {
        var projectId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new CreateProjectBudgetRevisionDto
        {
            RevisionName = "Q2 Rebaseline",
            RevisionType = "Revision",
            EstimatedBudget = 210000m,
            ApprovedBudget = 200000m,
            CommittedCost = 65000m,
            ForecastCost = 185000m,
            ThresholdWarningPercent = 75m,
            ThresholdCriticalPercent = 90m,
            ChangeReason = "Approved scope increase",
            Notes = "Add two rollout sites"
        };

        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.CreateBudgetRevisionAsync(projectId, It.Is<CreateProjectBudgetRevisionDto>(dto => dto.RevisionName == request.RevisionName)))
            .ReturnsAsync(new ProjectBudgetRevisionDto
            {
                Id = revisionId,
                ProjectId = projectId,
                RevisionName = request.RevisionName,
                RevisionType = request.RevisionType,
                EstimatedBudget = request.EstimatedBudget,
                ApprovedBudget = request.ApprovedBudget,
                Status = "Draft",
                ChangeReason = request.ChangeReason
            });
        projectService
            .Setup(service => service.SubmitBudgetRevisionAsync(revisionId, userId))
            .ReturnsAsync(new ProjectBudgetRevisionDto
            {
                Id = revisionId,
                ProjectId = projectId,
                RevisionName = request.RevisionName,
                Status = "PendingApproval"
            });
        projectService
            .Setup(service => service.ApproveBudgetRevisionAsync(revisionId, userId, "Approved for execution"))
            .ReturnsAsync(new ProjectBudgetRevisionDto
            {
                Id = revisionId,
                ProjectId = projectId,
                RevisionName = request.RevisionName,
                Status = "Approved",
                ApprovedAt = DateTime.UtcNow,
                ApprovedById = userId
            });

        using var factory = CreateFactory(projectService, userId);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/budget-revisions", request);
        var submitResponse = await client.PostAsync($"/api/projects/budget-revisions/{revisionId}/submit", content: null);
        var approveResponse = await client.PostAsJsonAsync($"/api/projects/budget-revisions/{revisionId}/approve", new { comments = "Approved for execution" });

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createdRevision = await createResponse.Content.ReadFromJsonAsync<ProjectBudgetRevisionDto>();
        var submittedRevision = await submitResponse.Content.ReadFromJsonAsync<ProjectBudgetRevisionDto>();
        var approvedRevision = await approveResponse.Content.ReadFromJsonAsync<ProjectBudgetRevisionDto>();

        createdRevision.Should().NotBeNull();
        submittedRevision.Should().NotBeNull();
        approvedRevision.Should().NotBeNull();
        createdRevision!.Status.Should().Be("Draft");
        submittedRevision!.Status.Should().Be("PendingApproval");
        approvedRevision!.Status.Should().Be("Approved");
    }

    [Fact]
    public async Task InvoiceRequestWorkflow_ShouldSubmitAndSendToFinance()
    {
        var invoiceRequestId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.SubmitInvoiceRequestAsync(invoiceRequestId, "Ready for finance"))
            .ReturnsAsync(new ProjectInvoiceRequestDto
            {
                Id = invoiceRequestId,
                ProjectId = projectId,
                RequestNumber = "INVREQ-2026-0200",
                RequestedAmount = 850m,
                Status = "Submitted",
                SubmittedAt = DateTime.UtcNow
            });
        projectService
            .Setup(service => service.MarkInvoiceRequestSentToFinanceAsync(invoiceRequestId, "AR-2026-0091", "Exported"))
            .ReturnsAsync(new ProjectInvoiceRequestDto
            {
                Id = invoiceRequestId,
                ProjectId = projectId,
                RequestNumber = "INVREQ-2026-0200",
                RequestedAmount = 850m,
                Status = "SentToFinance",
                SubmittedAt = DateTime.UtcNow,
                ExternalReference = "AR-2026-0091"
            });

        using var factory = CreateFactory(projectService, userId);
        using var client = factory.CreateClient();

        var submitResponse = await client.PostAsJsonAsync($"/api/projects/invoice-requests/{invoiceRequestId}/submit", new { comments = "Ready for finance" });
        var sendResponse = await client.PostAsJsonAsync($"/api/projects/invoice-requests/{invoiceRequestId}/send-to-finance", new
        {
            externalReference = "AR-2026-0091",
            comments = "Exported"
        });

        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var submitted = await submitResponse.Content.ReadFromJsonAsync<ProjectInvoiceRequestDto>();
        var sent = await sendResponse.Content.ReadFromJsonAsync<ProjectInvoiceRequestDto>();

        submitted.Should().NotBeNull();
        sent.Should().NotBeNull();
        submitted!.Status.Should().Be("Submitted");
        sent!.Status.Should().Be("SentToFinance");
        sent.ExternalReference.Should().Be("AR-2026-0091");
    }

    [Fact]
    public async Task InvoiceRequestFinanceCompletion_ShouldMarkInvoicedAndPaid()
    {
        var invoiceRequestId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.MarkInvoiceRequestInvoicedAsync(invoiceRequestId, "INV-2026-0101", "Invoice raised"))
            .ReturnsAsync(new ProjectInvoiceRequestDto
            {
                Id = invoiceRequestId,
                ProjectId = projectId,
                RequestNumber = "INVREQ-2026-0200",
                RequestedAmount = 850m,
                Status = "Invoiced",
                ExternalReference = "INV-2026-0101"
            });
        projectService
            .Setup(service => service.MarkInvoiceRequestPaidAsync(invoiceRequestId, "Cash received"))
            .ReturnsAsync(new ProjectInvoiceRequestDto
            {
                Id = invoiceRequestId,
                ProjectId = projectId,
                RequestNumber = "INVREQ-2026-0200",
                RequestedAmount = 850m,
                Status = "Paid",
                ExternalReference = "INV-2026-0101"
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var invoicedResponse = await client.PostAsJsonAsync($"/api/projects/invoice-requests/{invoiceRequestId}/mark-invoiced", new
        {
            externalReference = "INV-2026-0101",
            comments = "Invoice raised"
        });
        var paidResponse = await client.PostAsJsonAsync($"/api/projects/invoice-requests/{invoiceRequestId}/mark-paid", new
        {
            comments = "Cash received"
        });

        invoicedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        paidResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invoiced = await invoicedResponse.Content.ReadFromJsonAsync<ProjectInvoiceRequestDto>();
        var paid = await paidResponse.Content.ReadFromJsonAsync<ProjectInvoiceRequestDto>();

        invoiced.Should().NotBeNull();
        paid.Should().NotBeNull();
        invoiced!.Status.Should().Be("Invoiced");
        invoiced.ExternalReference.Should().Be("INV-2026-0101");
        paid!.Status.Should().Be("Paid");
    }

    [Fact]
    public async Task InvoiceRequestQueueReport_ShouldReturnFinanceQueueItems()
    {
        var projectId = Guid.NewGuid();
        var invoiceRequestId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetInvoiceRequestQueueReportAsync(100, "Submitted"))
            .ReturnsAsync(new[]
            {
                new ProjectInvoiceRequestQueueItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-FIN-1",
                    ProjectTitle = "Finance Queue Project",
                    InvoiceRequestId = invoiceRequestId,
                    RequestNumber = "INVREQ-2026-0501",
                    Status = "Submitted",
                    QueueStage = "Approval Complete",
                    RequestedAmount = 1250m,
                    Currency = "USD",
                    RequestedAt = DateTime.UtcNow.AddDays(-2),
                    DaysOutstanding = 2
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/invoice-request-queue?take=100&status=Submitted");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await response.Content.ReadFromJsonAsync<List<ProjectInvoiceRequestQueueItemDto>>();
        queue.Should().NotBeNull();
        queue.Should().ContainSingle();
        queue![0].RequestNumber.Should().Be("INVREQ-2026-0501");
        queue[0].QueueStage.Should().Be("Approval Complete");
    }

    [Fact]
    public async Task WorkflowApprovalQueueReport_ShouldReturnWorkflowItems()
    {
        var projectId = Guid.NewGuid();
        var deliverableId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetWorkflowApprovalQueueReportAsync(100, "ProjectDeliverable"))
            .ReturnsAsync(new[]
            {
                new ProjectWorkflowApprovalQueueItemDto
                {
                    EntityType = "ProjectDeliverable",
                    EntityId = deliverableId,
                    ProjectId = projectId,
                    ProjectCode = "PRJ-WF-1",
                    ProjectTitle = "Workflow Queue Project",
                    ItemTitle = "Acceptance dossier",
                    Status = "PendingApproval",
                    SubmittedAt = DateTime.UtcNow.AddDays(-2),
                    DaysPending = 2,
                    QueueStage = "Awaiting Approval"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/workflow-approval-queue?take=100&entityType=ProjectDeliverable");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await response.Content.ReadFromJsonAsync<List<ProjectWorkflowApprovalQueueItemDto>>();
        queue.Should().NotBeNull();
        queue.Should().ContainSingle();
        queue![0].EntityType.Should().Be("ProjectDeliverable");
        queue[0].QueueStage.Should().Be("Awaiting Approval");
    }

    [Fact]
    public async Task PortfolioPrioritizationReport_ShouldReturnRankedProjects()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetPortfolioPrioritizationReportAsync(null, 50))
            .ReturnsAsync(new[]
            {
                new ProjectPortfolioPrioritizationReportItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-PORT-1",
                    ProjectTitle = "Priority Rollout",
                    Status = "AtRisk",
                    HealthStatus = "Watch",
                    PriorityScore = 38m,
                    PriorityBand = "Stabilize",
                    OpenRiskCount = 2,
                    OpenIssueCount = 1,
                    OverdueMilestoneCount = 1,
                    RecommendedAction = "Escalate governance review, address blockers, and rebaseline near-term commitments."
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/portfolio-prioritization?take=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await response.Content.ReadFromJsonAsync<List<ProjectPortfolioPrioritizationReportItemDto>>();
        queue.Should().NotBeNull();
        queue.Should().ContainSingle();
        queue![0].PriorityBand.Should().Be("Stabilize");
        queue[0].PriorityScore.Should().Be(38m);
    }

    [Fact]
    public async Task StrategicInitiativeReport_ShouldReturnGroupedInitiatives()
    {
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetStrategicInitiativeReportAsync(null, 25))
            .ReturnsAsync(new[]
            {
                new ProjectStrategicInitiativeReportItemDto
                {
                    Initiative = "Operating resilience",
                    ProjectCount = 2,
                    ActiveProjectCount = 1,
                    DelayedProjectCount = 1,
                    HighRiskItemCount = 3,
                    TotalEstimatedBudget = 180000m,
                    PortfolioNames = new List<string> { "Transformation" },
                    ProgramNames = new List<string> { "ERP Delivery" }
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/strategic-initiatives?take=25");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<List<ProjectStrategicInitiativeReportItemDto>>();
        report.Should().NotBeNull();
        report.Should().ContainSingle();
        report![0].Initiative.Should().Be("Operating resilience");
        report[0].ProjectCount.Should().Be(2);
    }

    [Fact]
    public async Task StrategicInitiativeReport_ShouldReturnForbiddenWhenServiceDeniesAccess()
    {
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetStrategicInitiativeReportAsync(null, 25))
            .ThrowsAsync(new UnauthorizedAccessException("Forbidden"));

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/strategic-initiatives?take=25");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DependencyWatchReport_ShouldReturnCrossProjectWatchItems()
    {
        var dependencyId = Guid.NewGuid();
        var sourceProjectId = Guid.NewGuid();
        var targetProjectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetDependencyWatchReportAsync(null, null, 25))
            .ReturnsAsync(new[]
            {
                new ProjectDependencyWatchReportItemDto
                {
                    InterdependencyId = dependencyId,
                    SourceProjectId = sourceProjectId,
                    TargetProjectId = targetProjectId,
                    SourceProjectCode = "PRJ-DEP-1",
                    SourceProjectTitle = "Source Project",
                    TargetProjectCode = "PRJ-DEP-2",
                    TargetProjectTitle = "Target Project",
                    DependencyType = "Schedule",
                    Status = "Open",
                    ImpactLevel = "Critical",
                    Title = "Environment handoff",
                    CoordinationState = "Overdue",
                    DueDate = DateTime.UtcNow.Date.AddDays(-1)
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/dependency-watch?take=25");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<List<ProjectDependencyWatchReportItemDto>>();
        report.Should().NotBeNull();
        report.Should().ContainSingle();
        report![0].Title.Should().Be("Environment handoff");
        report[0].CoordinationState.Should().Be("Overdue");
    }

    [Fact]
    public async Task DependencyWatchReport_ShouldReturnForbiddenWhenServiceDeniesAccess()
    {
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetDependencyWatchReportAsync(null, null, 25))
            .ThrowsAsync(new UnauthorizedAccessException("Forbidden"));

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/dependency-watch?take=25");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MaterialReconciliationReport_ShouldReturnProjectVariance()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetMaterialReconciliationReportAsync(200, "UnderTracked"))
            .ReturnsAsync(new[]
            {
                new ProjectMaterialReconciliationReportItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-MAT-001",
                    ProjectTitle = "Materials Reconciliation",
                    Status = "InProgress",
                    RequisitionCount = 2,
                    PendingRequisitionCount = 1,
                    IssuedRequisitionCount = 1,
                    RequestedValue = 420m,
                    IssuedValue = 300m,
                    ReturnedValue = 20m,
                    NetIssuedValue = 280m,
                    TrackedMaterialCost = 200m,
                    MaterialCostVariance = 80m,
                    ReconciliationStatus = "UnderTracked"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/material-reconciliation?reconciliationStatus=UnderTracked&take=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await response.Content.ReadFromJsonAsync<List<ProjectMaterialReconciliationReportItemDto>>();
        queue.Should().NotBeNull();
        queue.Should().ContainSingle();
        queue![0].MaterialCostVariance.Should().Be(80m);
        queue[0].ReconciliationStatus.Should().Be("UnderTracked");
    }

    [Fact]
    public async Task ProcurementReconciliationReport_ShouldReturnReceiptIssueAndPostingVariance()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetProcurementReconciliationReportAsync(200, "PendingInspection"))
            .ReturnsAsync(new[]
            {
                new ProjectProcurementReconciliationReportItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-PROC-001",
                    ProjectTitle = "Procurement Reconciliation",
                    Status = "InProgress",
                    PurchaseRequisitionCount = 1,
                    OpenPurchaseRequisitionCount = 0,
                    PurchaseRequisitionAmount = 800m,
                    PurchaseOrderCount = 1,
                    OpenPurchaseOrderCount = 1,
                    PurchaseOrderAmount = 800m,
                    PurchaseReceiptCount = 1,
                    ReceivedAmount = 600m,
                    AcceptedReceiptAmount = 500m,
                    PendingInspectionAmount = 100m,
                    IssuedInventoryValue = 450m,
                    NetIssuedInventoryValue = 450m,
                    PostedMaterialCost = 300m,
                    ReceiptToIssueVariance = 50m,
                    IssueToPostingVariance = 150m,
                    ReconciliationStatus = "PendingInspection"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/procurement-reconciliation?reconciliationStatus=PendingInspection&take=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await response.Content.ReadFromJsonAsync<List<ProjectProcurementReconciliationReportItemDto>>();
        queue.Should().NotBeNull();
        queue.Should().ContainSingle();
        queue![0].AcceptedReceiptAmount.Should().Be(500m);
        queue[0].IssueToPostingVariance.Should().Be(150m);
        queue[0].ReconciliationStatus.Should().Be("PendingInspection");
    }

    [Fact]
    public async Task PhaseGateReadinessReport_ShouldReturnConstructionGateRows()
    {
        var projectId = Guid.NewGuid();
        var phaseId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetPhaseGateReadinessReportAsync(projectId, 50))
            .ReturnsAsync(new[]
            {
                new ProjectPhaseGateReadinessReportItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-GATE-001",
                    ProjectTitle = "Phase Gate Project",
                    ProjectStatus = "InProgress",
                    ProjectPhaseId = phaseId,
                    ProjectPhaseCode = "PROCUREMENT",
                    ProjectPhaseName = "Procurement",
                    ProjectPhaseSortOrder = 5,
                    IsStageGateRequired = true,
                    HasConfiguredRules = true,
                    IsReady = false,
                    ConfiguredRuleCount = 3,
                    BlockingRuleCount = 2,
                    BlockingFailureCount = 1,
                    SatisfiedRuleCount = 2,
                    GateStatus = "Blocked",
                    TopBlockingMessage = "Needs attention: actual 0, target min 1."
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/reports/phase-gate-readiness?projectId={projectId}&take=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<List<ProjectPhaseGateReadinessReportItemDto>>();
        report.Should().NotBeNull();
        report.Should().ContainSingle();
        report![0].GateStatus.Should().Be("Blocked");
        report[0].BlockingFailureCount.Should().Be(1);
    }

    [Fact]
    public async Task ApprovalWatchReport_ShouldReturnPermitAlerts()
    {
        var projectId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetApprovalWatchReportAsync(projectId, 50))
            .ReturnsAsync(new[]
            {
                new ProjectApprovalWatchReportItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-APP-001",
                    ProjectTitle = "Approval Watch Project",
                    ProjectStatus = "InProgress",
                    ApprovalRegisterItemId = itemId,
                    ApprovalType = "OccupancyCertificate",
                    Title = "Occupancy certificate",
                    Status = "Approved",
                    WatchState = "ExpiringSoon",
                    Severity = "High",
                    DaysToExpiry = 10
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/reports/approval-watch?projectId={projectId}&take=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<List<ProjectApprovalWatchReportItemDto>>();
        report.Should().NotBeNull();
        report.Should().ContainSingle();
        report![0].WatchState.Should().Be("ExpiringSoon");
        report[0].Severity.Should().Be("High");
    }

    [Fact]
    public async Task CommercialAdministrationReport_ShouldReturnCommercialWatchRows()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetCommercialAdministrationReportAsync(projectId, 50))
            .ReturnsAsync(new[]
            {
                new ProjectCommercialAdministrationReportItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-COM-001",
                    ProjectTitle = "Commercial Watch Project",
                    ProjectStatus = "InProgress",
                    Currency = "GHS",
                    ApprovedBudget = 1000000m,
                    PackageForecastAmount = 1120000m,
                    ForecastVarianceAmount = -120000m,
                    AlertCount = 2,
                    WatchState = "Critical",
                    TopAlertMessage = "Package forecast exceeds the approved project budget."
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/reports/construction-commercial?projectId={projectId}&take=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<List<ProjectCommercialAdministrationReportItemDto>>();
        report.Should().NotBeNull();
        report.Should().ContainSingle();
        report![0].WatchState.Should().Be("Critical");
        report[0].AlertCount.Should().Be(2);
    }

    [Fact]
    public async Task PostHandoverWatchReport_ShouldReturnGovernanceExposure()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetPostHandoverWatchReportAsync(projectId, 50))
            .ReturnsAsync(new[]
            {
                new ProjectPostHandoverWatchReportItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-DLP-001",
                    ProjectTitle = "Post-Handover Watch Project",
                    ProjectStatus = "Completed",
                    OpenHandoverItemCount = 2,
                    ActiveDefectLiabilityCount = 3,
                    ResponseBreachCount = 1,
                    ResolutionBreachCount = 1,
                    WarrantyExpiringSoonCount = 1,
                    AlertCount = 3,
                    HighestSeverity = "Critical",
                    WatchState = "Critical",
                    TotalRectificationExposure = 24500m
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/reports/post-handover-watch?projectId={projectId}&take=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<List<ProjectPostHandoverWatchReportItemDto>>();
        report.Should().NotBeNull();
        report.Should().ContainSingle();
        report![0].HighestSeverity.Should().Be("Critical");
        report[0].TotalRectificationExposure.Should().Be(24500m);
    }

    [Fact]
    public async Task DesignControlWatchReport_ShouldReturnDesignItems()
    {
        var projectId = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetDesignControlWatchReportAsync(projectId, 40))
            .ReturnsAsync(new[]
            {
                new ProjectDesignControlReportItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-DSN-001",
                    ProjectTitle = "Design Watch Project",
                    ProjectStatus = "InProgress",
                    ItemType = "Drawing",
                    RecordId = recordId,
                    ReferenceCode = "A-201",
                    Title = "Typical floor plan",
                    Category = "Architectural",
                    Status = "ForReview",
                    WatchState = "ReviewOverdue",
                    Severity = "High"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/reports/design-control-watch?projectId={projectId}&take=40");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<List<ProjectDesignControlReportItemDto>>();
        report.Should().NotBeNull();
        report.Should().ContainSingle();
        report![0].ItemType.Should().Be("Drawing");
        report[0].WatchState.Should().Be("ReviewOverdue");
    }

    [Fact]
    public async Task SiteControlsWatchReport_ShouldReturnOperationalItems()
    {
        var projectId = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetSiteControlsWatchReportAsync(projectId, 40))
            .ReturnsAsync(new[]
            {
                new ProjectSiteControlReportItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-SITE-001",
                    ProjectTitle = "Site Watch Project",
                    ProjectStatus = "InProgress",
                    ItemType = "RFI",
                    RecordId = recordId,
                    ReferenceCode = "RFI-017",
                    Title = "Beam depth clarification",
                    Category = "Critical",
                    Status = "Submitted",
                    WatchState = "ResponseOverdue",
                    Severity = "Critical"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/reports/site-controls-watch?projectId={projectId}&take=40");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<List<ProjectSiteControlReportItemDto>>();
        report.Should().NotBeNull();
        report.Should().ContainSingle();
        report![0].ItemType.Should().Be("RFI");
        report[0].Severity.Should().Be("Critical");
    }

    [Fact]
    public async Task UnitCommercializationWatchReport_ShouldReturnCommercialUnitRows()
    {
        var projectId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetUnitCommercializationWatchReportAsync(projectId, 40))
            .ReturnsAsync(new[]
            {
                new ProjectUnitCommercializationReportItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-UNIT-001",
                    ProjectTitle = "Unit Watch Project",
                    ProjectStatus = "InProgress",
                    ProjectUnitId = unitId,
                    UnitCode = "B-03",
                    UnitName = "Apartment B-03",
                    UnitType = "Apartment",
                    Status = "Available",
                    CommercialStatus = "Available",
                    HandoverStatus = "NotScheduled",
                    IsReleasedForMarket = false,
                    ReleaseState = "Withheld",
                    Currency = "GHS",
                    WatchState = "NotReleased",
                    Severity = "Warning"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/reports/unit-commercialization-watch?projectId={projectId}&take=40");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<List<ProjectUnitCommercializationReportItemDto>>();
        report.Should().NotBeNull();
        report.Should().ContainSingle();
        report![0].ReleaseState.Should().Be("Withheld");
        report[0].WatchState.Should().Be("NotReleased");
    }

    [Fact]
    public async Task MaterialCostLedgerReport_ShouldReturnLedgerEntries()
    {
        var projectId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetMaterialCostLedgerReportAsync(projectId, 150, "PurchaseReceipt", "Posted", false, true))
            .ReturnsAsync(new[]
            {
                new ProjectMaterialCostEntryDto
                {
                    Id = entryId,
                    ProjectId = projectId,
                    ProjectCode = "PRJ-LED-1",
                    ProjectTitle = "Ledger Project",
                    EntryDate = new DateTime(2026, 3, 10),
                    EntryType = "ReceiptPosting",
                    PostingState = "Posted",
                    AffectsActualCost = true,
                    IsReversed = false,
                    SourceDocumentType = "PurchaseReceipt",
                    SourceDocumentNumber = "PRC-1001",
                    Amount = 420m,
                    Currency = "USD",
                    HasMissingSourceLink = false,
                    HasReversalGap = true
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/reports/material-cost-ledger?projectId={projectId}&take=150&sourceDocumentType=PurchaseReceipt&postingState=Posted&isReversed=false&exceptionsOnly=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<List<ProjectMaterialCostEntryDto>>();
        report.Should().NotBeNull();
        report.Should().ContainSingle();
        report![0].Id.Should().Be(entryId);
        report[0].HasReversalGap.Should().BeTrue();
        report[0].PostingState.Should().Be("Posted");
    }

    [Fact]
    public async Task ResourceCapacityReport_ShouldReturnCapacityRows()
    {
        var userId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        var startDate = new DateTime(2026, 3, 9);
        var endDate = new DateTime(2026, 3, 14);
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetResourceCapacityReportAsync(startDate, endDate, userId))
            .ReturnsAsync(new[]
            {
                new ProjectResourceCapacityReportItemDto
                {
                    UserId = userId,
                    UserDisplayName = "Alex Planner",
                    CapacityUtilizationPercent = 112.5m,
                    EffectiveCapacityHours = 32m,
                    TotalAllocatedHours = 36m,
                    ApprovedLeaveHours = 8m,
                    LeaveRequestCount = 1,
                    ConflictCount = 1,
                    QualificationRisk = "High",
                    AllocationIds = new List<Guid> { allocationId }
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/reports/resource-capacity?startDate={startDate:O}&endDate={endDate:O}&userId={userId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<List<ProjectResourceCapacityReportItemDto>>();
        report.Should().NotBeNull();
        report.Should().ContainSingle();
        report![0].CapacityUtilizationPercent.Should().Be(112.5m);
        report[0].AllocationIds.Should().Contain(allocationId);
    }

    [Fact]
    public async Task ResourceCapacityRecommendations_ShouldReturnRecommendations()
    {
        var userId = Guid.NewGuid();
        var replacementUserId = Guid.NewGuid();
        var startDate = new DateTime(2026, 3, 9);
        var endDate = new DateTime(2026, 3, 14);
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetResourceCapacityRecommendationsAsync(startDate, endDate, userId))
            .ReturnsAsync(new[]
            {
                new ProjectResourceCapacityRecommendationDto
                {
                    UserId = userId,
                    UserDisplayName = "Casey Analyst",
                    CapacityUtilizationPercent = 118m,
                    EffectiveCapacityHours = 30m,
                    SuggestedReductionHours = 6m,
                    Severity = "Critical",
                    Recommendation = "Shift 6h to backup analyst.",
                    SuggestedReplacementUserId = replacementUserId,
                    SuggestedReplacementUserDisplayName = "Jordan Backup",
                    ProjectCodes = new List<string> { "PRJ-CAP-1" },
                    MatchedSkills = new List<string> { "PMP" }
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/reports/resource-capacity-recommendations?startDate={startDate:O}&endDate={endDate:O}&userId={userId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<List<ProjectResourceCapacityRecommendationDto>>();
        report.Should().NotBeNull();
        report.Should().ContainSingle();
        report![0].Severity.Should().Be("Critical");
        report[0].SuggestedReplacementUserId.Should().Be(replacementUserId);
    }

    [Fact]
    public async Task ResourceOptimizationReport_ShouldReturnSuggestions()
    {
        var userId = Guid.NewGuid();
        var replacementUserId = Guid.NewGuid();
        var startDate = new DateTime(2026, 3, 9);
        var endDate = new DateTime(2026, 3, 14);
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetResourceOptimizationSuggestionsAsync(startDate, endDate))
            .ReturnsAsync(new[]
            {
                new ProjectResourceOptimizationSuggestionDto
                {
                    UserId = userId,
                    UserDisplayName = "Primary Engineer",
                    SuggestedReplacementUserId = replacementUserId,
                    SuggestedReplacementUserDisplayName = "Backup Engineer",
                    Severity = "High",
                    MatchedSkills = new List<string> { "Azure", "PowerShell" },
                    MatchedSkillCount = 2,
                    ReplacementVerifiedSkillCount = 4,
                    AffectedAllocationIds = new List<Guid> { Guid.NewGuid() },
                    Recommendation = "Move the allocation to Backup Engineer."
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/reports/resource-optimization?startDate={startDate:O}&endDate={endDate:O}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<List<ProjectResourceOptimizationSuggestionDto>>();
        report.Should().NotBeNull();
        report.Should().ContainSingle();
        report![0].SuggestedReplacementUserId.Should().Be(replacementUserId);
        report[0].MatchedSkills.Should().Contain("Azure");
    }

    [Fact]
    public async Task BillingSummaryReport_ShouldReturnMarginSummary()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetBillingSummaryReportAsync(75))
            .ReturnsAsync(new[]
            {
                new ProjectBillingSummaryReportItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-BILL-1",
                    ProjectTitle = "Billing Project",
                    ReadyBillingScheduleCount = 1,
                    ScheduledBillingAmount = 1500m,
                    InvoiceRequestedAmount = 1200m,
                    CollectedCashAmount = 800m,
                    ActualCost = 500m,
                    MarginAmount = 700m,
                    MarginPercent = 58.33m
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/billing-summary?take=75");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<List<ProjectBillingSummaryReportItemDto>>();
        report.Should().NotBeNull();
        report.Should().ContainSingle();
        report![0].ProjectId.Should().Be(projectId);
        report[0].MarginPercent.Should().Be(58.33m);
    }

    [Fact]
    public async Task MaterialCostLedgerReport_ShouldReturnForbiddenWhenServiceDeniesAccess()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetMaterialCostLedgerReportAsync(projectId, 150, "PurchaseReceipt", "Posted", false, true))
            .ThrowsAsync(new UnauthorizedAccessException("Forbidden"));

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/reports/material-cost-ledger?projectId={projectId}&take=150&sourceDocumentType=PurchaseReceipt&postingState=Posted&isReversed=false&exceptionsOnly=true");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FinancialControlSummary_ShouldReturnForbiddenWhenServiceDeniesAccess()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetFinancialControlSummaryAsync(projectId))
            .ThrowsAsync(new UnauthorizedAccessException("Forbidden"));

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/financial-control-summary");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExternalCollaborationReport_ShouldReturnSharedProjects()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetExternalCollaborationReportAsync(200, "ActionRequired"))
            .ReturnsAsync(new[]
            {
                new ProjectExternalCollaborationReportItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-EXT-2",
                    ProjectTitle = "Portal Oversight",
                    Status = "InProgress",
                    ExternalPortalAccessEnabled = true,
                    ExternalCollaborationEnabled = true,
                    PolicyCount = 2,
                    ExternalVisibleDocumentCount = 3,
                    ExternalVisibleDeliverableCount = 2,
                    PendingExternalSubmissionCount = 1,
                    PendingExternalSignOffCount = 1,
                    ExternalCommentCount = 4,
                    CollaborationState = "ActionRequired"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/external-collaboration?collaborationState=ActionRequired&take=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ProjectExternalCollaborationReportItemDto>>();
        items.Should().NotBeNull();
        items.Should().ContainSingle();
        items![0].ProjectCode.Should().Be("PRJ-EXT-2");
        items[0].PendingExternalSignOffCount.Should().Be(1);
        items[0].CollaborationState.Should().Be("ActionRequired");
    }

    [Fact]
    public async Task TimesheetApprovalQueue_ShouldReturnSubmittedItems()
    {
        var projectId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetTimesheetApprovalQueueAsync(projectId, "Submitted", null, 100))
            .ReturnsAsync(new[]
            {
                new ProjectTimesheetApprovalQueueItemDto
                {
                    EntryId = entryId,
                    ProjectId = projectId,
                    ProjectCode = "PRJ-TS-9",
                    ProjectTitle = "Approval Route Project",
                    UserId = Guid.NewGuid(),
                    EntryDate = new DateTime(2026, 3, 10),
                    Hours = 6m,
                    HourlyRate = 30m,
                    CostAmount = 180m,
                    WorkType = "Implementation",
                    Status = "Submitted",
                    QueueStage = "Pending Approval",
                    DaysOpen = 2
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/timesheets/approval-queue?projectId={projectId}&status=Submitted&take=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await response.Content.ReadFromJsonAsync<List<ProjectTimesheetApprovalQueueItemDto>>();
        queue.Should().NotBeNull();
        queue.Should().ContainSingle();
        queue![0].QueueStage.Should().Be("Pending Approval");
    }

    [Fact]
    public async Task ExpenseApprovalQueue_ShouldReturnSubmittedItems()
    {
        var projectId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetExpenseApprovalQueueAsync(projectId, "Submitted", null, 100))
            .ReturnsAsync(new[]
            {
                new ProjectExpenseApprovalQueueItemDto
                {
                    ExpenseId = expenseId,
                    ProjectId = projectId,
                    ProjectCode = "PRJ-EX-9",
                    ProjectTitle = "Expense Route Project",
                    UserId = Guid.NewGuid(),
                    ExpenseDate = new DateTime(2026, 3, 10),
                    Category = "Travel",
                    Currency = "USD",
                    Amount = 90m,
                    TaxAmount = 9m,
                    TotalAmount = 99m,
                    Status = "Submitted",
                    QueueStage = "Pending Approval",
                    DaysOpen = 3
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/expenses/approval-queue?projectId={projectId}&status=Submitted&take=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await response.Content.ReadFromJsonAsync<List<ProjectExpenseApprovalQueueItemDto>>();
        queue.Should().NotBeNull();
        queue.Should().ContainSingle();
        queue![0].QueueStage.Should().Be("Pending Approval");
    }

    [Fact]
    public async Task AddQualityCheckpoint_ShouldReturnCheckpoint()
    {
        var projectId = Guid.NewGuid();
        var checkpointId = Guid.NewGuid();
        var request = new CreateProjectQualityCheckpointDto
        {
            Title = "Configuration QA gate",
            Status = "Open",
            RequiresQaSignOff = true
        };

        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.AddQualityCheckpointAsync(projectId, It.Is<CreateProjectQualityCheckpointDto>(dto => dto.Title == request.Title)))
            .ReturnsAsync(new ProjectQualityCheckpointDto
            {
                Id = checkpointId,
                ProjectId = projectId,
                Title = request.Title,
                Status = request.Status,
                RequiresQaSignOff = request.RequiresQaSignOff
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/quality-checkpoints", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkpoint = await response.Content.ReadFromJsonAsync<ProjectQualityCheckpointDto>();
        checkpoint.Should().NotBeNull();
        checkpoint!.Id.Should().Be(checkpointId);
        checkpoint.Title.Should().Be("Configuration QA gate");
        checkpoint.RequiresQaSignOff.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveNonConformance_ShouldReturnResolvedItem()
    {
        var nonConformanceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.ResolveNonConformanceAsync(nonConformanceId, "Corrective action completed"))
            .ReturnsAsync(new ProjectNonConformanceDto
            {
                Id = nonConformanceId,
                ProjectId = projectId,
                Title = "Interface mapping mismatch",
                Severity = "High",
                Status = "Resolved",
                ResolutionNotes = "Corrective action completed",
                ResolvedAt = DateTime.UtcNow
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/projects/non-conformances/{nonConformanceId}/resolve", new
        {
            comments = "Corrective action completed"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var nonConformance = await response.Content.ReadFromJsonAsync<ProjectNonConformanceDto>();
        nonConformance.Should().NotBeNull();
        nonConformance!.Status.Should().Be("Resolved");
        nonConformance.ResolutionNotes.Should().Be("Corrective action completed");
    }

    [Fact]
    public async Task ClosureWorkflow_ShouldUpsertSubmitAndApproveClosure()
    {
        var projectId = Guid.NewGuid();
        var closureId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new UpsertProjectClosureDto
        {
            FinalBudget = 200000m,
            FinalCost = 192500m,
            DeliverablesAccepted = true,
            TasksCompletedOrWaived = true,
            AssetsReconciled = true,
            OpenItemsDisposed = true,
            LessonsLearnedSummary = "Early procurement planning reduced delays.",
            PostImplementationReview = "Objectives met."
        };

        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.UpsertClosureAsync(projectId, It.Is<UpsertProjectClosureDto>(dto => dto.FinalCost == request.FinalCost)))
            .ReturnsAsync(new ProjectClosureDto
            {
                Id = closureId,
                ProjectId = projectId,
                Status = "Draft",
                FinalBudget = request.FinalBudget,
                FinalCost = request.FinalCost,
                DeliverablesAccepted = request.DeliverablesAccepted,
                TasksCompletedOrWaived = request.TasksCompletedOrWaived,
                AssetsReconciled = request.AssetsReconciled,
                OpenItemsDisposed = request.OpenItemsDisposed,
                LessonsLearnedSummary = request.LessonsLearnedSummary,
                PostImplementationReview = request.PostImplementationReview
            });
        projectService
            .Setup(service => service.SubmitClosureForApprovalAsync(projectId, userId))
            .Returns(Task.CompletedTask);
        projectService
            .Setup(service => service.ApproveClosureAsync(closureId, userId, "Closure accepted"))
            .Returns(Task.CompletedTask);

        using var factory = CreateFactory(projectService, userId);
        using var client = factory.CreateClient();

        var upsertResponse = await client.PutAsJsonAsync($"/api/projects/{projectId}/closure", request);
        var submitResponse = await client.PostAsync($"/api/projects/{projectId}/closure/submit", content: null);
        var approveResponse = await client.PostAsJsonAsync($"/api/projects/closure/{closureId}/approve", new { comments = "Closure accepted" });

        upsertResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var closure = await upsertResponse.Content.ReadFromJsonAsync<ProjectClosureDto>();
        closure.Should().NotBeNull();
        closure!.Status.Should().Be("Draft");
        closure.FinalCost.Should().Be(request.FinalCost);

        projectService.Verify(service => service.SubmitClosureForApprovalAsync(projectId, userId), Times.Once);
        projectService.Verify(service => service.ApproveClosureAsync(closureId, userId, "Closure accepted"), Times.Once);
    }

    [Fact]
    public async Task SubmitExternalDeliverable_ShouldReturnMockedDeliverable()
    {
        var projectId = Guid.NewGuid();
        var deliverableId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new SubmitProjectDeliverableDto
        {
            SubmittedDocumentId = Guid.NewGuid(),
            Notes = "Portal submission"
        };

        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.SubmitExternalDeliverableAsync(projectId, deliverableId, It.IsAny<SubmitProjectDeliverableDto>(), userId))
            .ReturnsAsync(new ProjectDeliverableDto
            {
                Id = deliverableId,
                ProjectId = projectId,
                Title = "UAT Evidence",
                Status = "In Review",
                ExternalSubmissionAllowed = true,
                ExternalSignOffRequired = true,
                IsExternalVisible = true,
                SubmittedAt = DateTime.UtcNow,
                AcceptanceNotes = request.Notes
            });

        using var factory = CreateFactory(projectService, userId);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/projects/external/my-projects/{projectId}/deliverables/{deliverableId}/submit", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var deliverable = await response.Content.ReadFromJsonAsync<ProjectDeliverableDto>();
        deliverable.Should().NotBeNull();
        deliverable!.Id.Should().Be(deliverableId);
        deliverable.Status.Should().Be("In Review");
        deliverable.AcceptanceNotes.Should().Be("Portal submission");
    }

    [Fact]
    public async Task RejectExternalDeliverable_ShouldForwardCurrentUserAndComments()
    {
        var projectId = Guid.NewGuid();
        var deliverableId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.RejectExternalDeliverableAsync(projectId, deliverableId, "Need updated evidence", userId))
            .ReturnsAsync(new ProjectDeliverableDto
            {
                Id = deliverableId,
                ProjectId = projectId,
                Title = "Customer Sign-off",
                Status = "Rejected",
                ExternalSignOffRequired = true,
                IsExternalVisible = true,
                ExternalApprovalNotes = "Need updated evidence"
            });

        using var factory = CreateFactory(projectService, userId);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/projects/external/my-projects/{projectId}/deliverables/{deliverableId}/reject",
            new { comments = "Need updated evidence" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var deliverable = await response.Content.ReadFromJsonAsync<ProjectDeliverableDto>();
        deliverable.Should().NotBeNull();
        deliverable!.Status.Should().Be("Rejected");
        deliverable.ExternalApprovalNotes.Should().Be("Need updated evidence");
        projectService.Verify(service => service.RejectExternalDeliverableAsync(projectId, deliverableId, "Need updated evidence", userId), Times.Once);
    }

    private static WebApplicationFactory<Program> CreateFactory(Mock<IProjectService> projectService, Guid? userId = null)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("CandidatePortal:PortalUrl", "https://candidate.test/");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IAuthorizationHandler>();
                services.RemoveAll<IProjectService>();
                services.RemoveAll<ICurrentUserProvider>();

                services.AddSingleton<IPolicyEvaluator, TestPolicyEvaluator>();
                services.AddSingleton<IAuthorizationHandler, AllowAnonymousHandler>();
                services.AddSingleton(projectService.Object);
                services.AddSingleton<ICurrentUserProvider>(new FakeCurrentUserProvider(userId ?? Guid.NewGuid(), Guid.NewGuid()));
            });
        });
}

internal sealed class FakeCurrentUserProvider : ICurrentUserProvider
{
    public FakeCurrentUserProvider(Guid userId, Guid tenantId)
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
