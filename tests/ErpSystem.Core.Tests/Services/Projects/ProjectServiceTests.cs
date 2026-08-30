using System.Linq.Expressions;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Assets;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Estate;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Services;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public class ProjectServiceTests
{
    [Fact]
    public async Task CreateProjectAsync_ShouldGenerateCode_ApplyTemplate_AndCreateInitiationSnapshot()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingProject = new Project
        {
            TenantId = tenantId,
            ProjectCode = $"PRJ-{DateTime.UtcNow.Year}-0001",
            Title = "Existing Project",
            CreatedAt = DateTime.UtcNow
        };
        var template = new ProjectTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "ERP",
            Name = "ERP Template",
            TemplateDefinitionJson = """
            {
              "workItems": [
                {
                  "nodeType": "Phase",
                  "title": "Discovery",
                  "status": "New",
                  "priority": "High",
                  "sortOrder": 0,
                  "percentComplete": 0
                }
              ],
              "milestones": [
                {
                  "title": "Kickoff",
                  "status": "Draft",
                  "targetDate": "2026-01-15T00:00:00Z"
                }
              ]
            }
            """
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(existingProject);
        fixture.SettingsRepository
            .Setup(x => x.GetOrCreateDefaultAsync(tenantId, userId))
            .ReturnsAsync(new ProjectManagementSettings
            {
                TenantId = tenantId,
                ProjectNumberFormat = "PRJ-{YYYY}-{####}",
                RequireSponsor = false,
                DefaultApprovalRequired = true
            });
        fixture.TemplateRepository.Setup(x => x.GetByIdAsync(template.Id)).ReturnsAsync(template);

        var service = fixture.CreateService();

        var result = await service.CreateProjectAsync(new CreateProjectDto
        {
            Title = "ERP Rollout",
            TemplateId = template.Id,
            EstimatedBudget = 250000m
        });

        result.ProjectCode.Should().Be($"PRJ-{DateTime.UtcNow.Year}-0002");
        result.Title.Should().Be("ERP Rollout");
        result.WorkItems.Should().ContainSingle(x => x.Title == "Discovery" && x.NodeType == ProjectWorkItemNodeTypes.Phase);
        result.Milestones.Should().ContainSingle(x => x.Title == "Kickoff");
        result.InitiationVersions.Should().ContainSingle(x => x.ChangeType == "Created");

        fixture.InitiationVersions.Should().ContainSingle(x => x.ProjectId == result.Id && x.VersionNumber == 1);
        fixture.WorkItems.Should().ContainSingle(x => x.ProjectId == result.Id && x.Title == "Discovery");
        fixture.Milestones.Should().ContainSingle(x => x.ProjectId == result.Id && x.Title == "Kickoff");
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldSeedConstructionProfile_AndDefaultLifecyclePhases()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SettingsRepository
            .Setup(x => x.GetOrCreateDefaultAsync(tenantId, userId))
            .ReturnsAsync(new ProjectManagementSettings
            {
                TenantId = tenantId,
                ProjectNumberFormat = "PRJ-{YYYY}-{####}",
                RequireSponsor = false,
                DefaultApprovalRequired = true
            });

        var service = fixture.CreateService();

        var result = await service.CreateProjectAsync(new CreateProjectDto
        {
            Title = "Tower Development",
            DevelopmentProfile = new UpsertProjectDevelopmentProfileDto
            {
                DeliveryStructure = ProjectDeliveryStructures.MultiUnit,
                DevelopmentType = "Residential",
                SiteName = "Airport Hills Plot 10",
                ProcurementRoute = "Traditional",
                ContractStrategy = "MeasuredWorks",
                HandoverStrategy = "UnitByUnitHandover"
            }
        });

        result.DevelopmentProfile.Should().NotBeNull();
        result.DevelopmentProfile!.DeliveryStructure.Should().Be(ProjectDeliveryStructures.MultiUnit);
        result.DevelopmentProfile.DevelopmentType.Should().Be("Residential");
        result.Phases.Should().NotBeEmpty();
        result.Phases.Select(x => x.Name).Should().Contain("Feasibility");
        result.Phases.Select(x => x.Name).Should().Contain("Construction");
        fixture.DevelopmentProfiles.Should().ContainSingle(x => x.ProjectId == result.Id);
        fixture.ProjectPhases.Should().HaveCount(9);
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldSupportSeqTokenInNumberFormat()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SettingsRepository
            .Setup(x => x.GetOrCreateDefaultAsync(tenantId, userId))
            .ReturnsAsync(new ProjectManagementSettings
            {
                TenantId = tenantId,
                ProjectNumberFormat = "IT-{YY}-{SEQ:000}",
                RequireSponsor = false,
                DefaultApprovalRequired = true
            });

        var service = fixture.CreateService();

        var result = await service.CreateProjectAsync(new CreateProjectDto
        {
            Title = "Service Desk Upgrade"
        });

        result.ProjectCode.Should().Be($"IT-{DateTime.UtcNow:yy}-001");
    }

    [Fact]
    public async Task ReleaseProjectUnitAsync_ShouldMarkUnitReleased_AndMovePlannedUnitToAvailable()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-UNIT-1",
            Title = "Unit Release Project"
        };
        var unit = new ProjectUnit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = "Apartment A1",
            Status = ProjectUnitStatuses.Planned,
            Currency = "USD"
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.ProjectUnits.Add(unit);
        fixture.Users.Add(new ApplicationUser
        {
            Id = userId,
            FirstName = "Release",
            LastName = "Manager",
            UserName = "release.manager"
        });

        var service = fixture.CreateService();

        var result = await service.ReleaseProjectUnitAsync(unit.Id);

        result.IsReleasedForMarket.Should().BeTrue();
        result.Status.Should().Be(ProjectUnitStatuses.Available);
        result.ReleasedByDisplayName.Should().Be("Release Manager");
        unit.IsReleasedForMarket.Should().BeTrue();
        unit.ReleasedAt.Should().NotBeNull();
        unit.Status.Should().Be(ProjectUnitStatuses.Available);
    }

    [Fact]
    public async Task CreateSalesAgreementFromProjectUnitAsync_ShouldLinkAgreement_AndReserveUnit()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-UNIT-2",
            Title = "Sales Handoff Project"
        };
        var unit = new ProjectUnit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = "Apartment B4",
            Code = "B4",
            Status = ProjectUnitStatuses.Available,
            Currency = "USD",
            BasePrice = 250000m,
            CustomerBusinessPartnerId = customerId,
            IsReleasedForMarket = true
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.ProjectUnits.Add(unit);
        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = customerId,
            TenantId = tenantId,
            LegalName = "Unit Buyer Ltd"
        });
        fixture.Users.Add(new ApplicationUser
        {
            Id = userId,
            FirstName = "Sales",
            LastName = "Coordinator",
            UserName = "sales.coordinator"
        });
        fixture.SalesAgreementService
            .Setup(x => x.CreateAsync(It.IsAny<CreateSalesAgreementDto>()))
            .ReturnsAsync((CreateSalesAgreementDto dto) =>
            {
                var agreementId = Guid.NewGuid();
                fixture.SalesAgreements.Add(new SalesAgreement
                {
                    Id = agreementId,
                    TenantId = tenantId,
                    BusinessPartnerId = dto.BusinessPartnerId,
                    CustomerName = "Unit Buyer",
                    AgreementTitle = dto.AgreementTitle,
                    AgreementStatus = SalesAgreementStatus.Draft,
                    AgreementType = SalesAgreementType.General,
                    StartDate = dto.StartDate,
                    AgreedValue = dto.AgreedValue,
                    MinimumCommitment = dto.MinimumCommitment,
                    MaximumCommitment = dto.MaximumCommitment,
                    Currency = dto.Currency,
                    PropertyReference = dto.PropertyReference,
                    DocumentNumber = "AGR-UNIT-001"
                });

                return new SalesAgreementDetailDto
                {
                    Id = agreementId,
                    DocumentNumber = "AGR-UNIT-001",
                    BusinessPartnerId = dto.BusinessPartnerId,
                    CustomerName = "Unit Buyer",
                    AgreementTitle = dto.AgreementTitle,
                    AgreementStatus = "Draft",
                    StartDate = dto.StartDate,
                    AgreedValue = dto.AgreedValue,
                    Currency = dto.Currency,
                    PropertyReference = dto.PropertyReference
                };
            });

        var service = fixture.CreateService();

        var result = await service.CreateSalesAgreementFromProjectUnitAsync(unit.Id);

        result.SalesAgreementId.Should().NotBeNull();
        result.SalesAgreementNumber.Should().Be("AGR-UNIT-001");
        result.Status.Should().Be(ProjectUnitStatuses.Reserved);
        result.CommercialStatus.Should().Be(ProjectUnitStatuses.Reserved);
        unit.SalesAgreementId.Should().Be(result.SalesAgreementId);
        unit.Status.Should().Be(ProjectUnitStatuses.Reserved);
    }

    [Fact]
    public async Task CreateLeaseAgreementFromProjectUnitAsync_ShouldLinkAgreement_AndReserveUnit()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-UNIT-LEASE",
            Title = "Lease Handoff Project"
        };
        var unit = new ProjectUnit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = "Apartment L2",
            Code = "L2",
            UnitType = ProjectUnitTypes.Apartment,
            Status = ProjectUnitStatuses.Available,
            Currency = "USD",
            CustomerBusinessPartnerId = customerId,
            IsReleasedForMarket = true
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.ProjectUnits.Add(unit);
        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = customerId,
            TenantId = tenantId,
            LegalName = "Tenant Customer Ltd"
        });
        fixture.SalesAgreementService
            .Setup(x => x.CreateAsync(It.IsAny<CreateSalesAgreementDto>()))
            .ReturnsAsync((CreateSalesAgreementDto dto) =>
            {
                var agreementId = Guid.NewGuid();
                fixture.SalesAgreements.Add(new SalesAgreement
                {
                    Id = agreementId,
                    TenantId = tenantId,
                    BusinessPartnerId = dto.BusinessPartnerId,
                    CustomerName = "Tenant Customer",
                    AgreementTitle = dto.AgreementTitle,
                    AgreementStatus = SalesAgreementStatus.Draft,
                    AgreementType = SalesAgreementType.TenancyAgreement,
                    StartDate = dto.StartDate,
                    Currency = dto.Currency,
                    PropertyReference = dto.PropertyReference,
                    DocumentNumber = "AGR-LEASE-001"
                });

                return new SalesAgreementDetailDto
                {
                    Id = agreementId,
                    DocumentNumber = "AGR-LEASE-001",
                    BusinessPartnerId = dto.BusinessPartnerId,
                    CustomerName = "Tenant Customer",
                    AgreementTitle = dto.AgreementTitle,
                    AgreementType = SalesAgreementType.TenancyAgreement.ToString(),
                    AgreementStatus = SalesAgreementStatus.Draft.ToString(),
                    StartDate = dto.StartDate,
                    Currency = dto.Currency,
                    PropertyReference = dto.PropertyReference
                };
            });

        var service = fixture.CreateService();

        var result = await service.CreateLeaseAgreementFromProjectUnitAsync(unit.Id);

        result.SalesAgreementId.Should().NotBeNull();
        result.SalesAgreementNumber.Should().Be("AGR-LEASE-001");
        result.SalesAgreementType.Should().Be(SalesAgreementType.TenancyAgreement.ToString());
        result.Status.Should().Be(ProjectUnitStatuses.Reserved);
        result.CommercialStatus.Should().Be(ProjectUnitStatuses.Reserved);
        unit.SalesAgreementId.Should().Be(result.SalesAgreementId);
        unit.Status.Should().Be(ProjectUnitStatuses.Reserved);
    }

    [Fact]
    public async Task GetProjectUnitsAsync_ShouldMarkActiveLeaseAgreementAsLeased()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var agreementId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-LEASE-SYNC",
            Title = "Lease Sync Project"
        };
        var unit = new ProjectUnit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = "Suite 4A",
            UnitType = ProjectUnitTypes.OfficeSuite,
            Status = ProjectUnitStatuses.Reserved,
            CustomerBusinessPartnerId = customerId,
            SalesAgreementId = agreementId,
            Currency = "USD",
            IsReleasedForMarket = true
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.ProjectUnits.Add(unit);
        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = customerId,
            TenantId = tenantId,
            LegalName = "Tenant Customer Ltd"
        });
        fixture.SalesAgreements.Add(new SalesAgreement
        {
            Id = agreementId,
            TenantId = tenantId,
            BusinessPartnerId = customerId,
            CustomerName = "Tenant Customer",
            AgreementTitle = "Suite 4A Lease",
            AgreementType = SalesAgreementType.LeaseAgreement,
            AgreementStatus = SalesAgreementStatus.Active,
            StartDate = DateTime.UtcNow.Date,
            Currency = "USD",
            DocumentNumber = "AGR-LEASE-002"
        });

        var service = fixture.CreateService();

        var result = (await service.GetProjectUnitsAsync(project.Id)).Single();

        result.SalesAgreementType.Should().Be(SalesAgreementType.LeaseAgreement.ToString());
        result.CommercialStatus.Should().Be(ProjectUnitStatuses.Leased);
        result.HandoverStatus.Should().Be(ProjectUnitHandoverStatuses.Pending);
    }

    [Fact]
    public async Task CreateSalesOrderFromProjectUnitAsync_ShouldLinkOrder_AndReserveUnit()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-UNIT-3",
            Title = "Sales Order Handoff Project"
        };
        var unit = new ProjectUnit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = "Apartment C5",
            Code = "C5",
            Status = ProjectUnitStatuses.Available,
            Currency = "USD",
            BasePrice = 315000m,
            AreaSquareMeters = 96.5m,
            CustomerBusinessPartnerId = customerId,
            IsReleasedForMarket = true
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.ProjectUnits.Add(unit);
        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = customerId,
            TenantId = tenantId,
            LegalName = "Unit Buyer Ltd"
        });
        fixture.SalesOrderService
            .Setup(x => x.CreateSalesOrderAsync(It.IsAny<CreateSalesOrderDto>()))
            .ReturnsAsync((CreateSalesOrderDto dto) =>
            {
                var orderId = Guid.NewGuid();
                fixture.SalesOrders.Add(new SalesOrder
                {
                    Id = orderId,
                    TenantId = tenantId,
                    BusinessPartnerId = dto.BusinessPartnerId,
                    CustomerName = "Unit Buyer",
                    DocumentNumber = "SO-UNIT-001",
                    OrderType = dto.OrderType,
                    OrderStatus = SalesOrderStatus.Draft,
                    Currency = dto.Currency ?? "USD",
                    PropertyReference = dto.PropertyReference,
                    PropertyType = dto.PropertyType
                });

                return new SalesOrderDetailDto
                {
                    Id = orderId,
                    DocumentNumber = "SO-UNIT-001",
                    OrderType = dto.OrderType,
                    OrderStatus = SalesOrderStatus.Draft,
                    BusinessPartnerId = dto.BusinessPartnerId,
                    CustomerName = "Unit Buyer",
                    Currency = dto.Currency ?? "USD",
                    PropertyReference = dto.PropertyReference,
                    PropertyType = dto.PropertyType
                };
            });

        var service = fixture.CreateService();

        var result = await service.CreateSalesOrderFromProjectUnitAsync(unit.Id);

        result.SalesOrderId.Should().NotBeNull();
        result.SalesOrderNumber.Should().Be("SO-UNIT-001");
        result.Status.Should().Be(ProjectUnitStatuses.Reserved);
        result.CommercialStatus.Should().Be(ProjectUnitStatuses.Reserved);
        unit.SalesOrderId.Should().Be(result.SalesOrderId);
        unit.Status.Should().Be(ProjectUnitStatuses.Reserved);
    }

    [Fact]
    public async Task SubmitProjectForApprovalAsync_ShouldMoveDraftProjectToPendingApproval_AndSnapshotState()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-2026-0007",
            Title = "Approval Flow",
            Status = ProjectStatuses.Draft
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.WorkflowIntegrationService
            .Setup(x => x.SubmitAsync("Project", project.Id))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult
                {
                    Success = true,
                    Status = WorkflowInstanceStatus.InProgress
                },
                WorkflowOutcome.Pending));

        var service = fixture.CreateService();

        await service.SubmitProjectForApprovalAsync(project.Id, userId);

        project.Status.Should().Be(ProjectStatuses.PendingApproval);
        project.SubmittedAt.Should().NotBeNull();
        fixture.InitiationVersions.Should().ContainSingle(x => x.ProjectId == project.Id && x.ChangeType == "Submitted");
    }

    [Fact]
    public async Task AddWorkItemAsync_ShouldRecalculateProjectProgressUsingRollupChildren()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-2026-0010",
            Title = "WBS Progress"
        };
        var phase = new ProjectWorkItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            NodeType = ProjectWorkItemNodeTypes.Phase,
            Title = "Phase 1",
            SortOrder = 0,
            IsRollupEnabled = true
        };
        var existingTask = new ProjectWorkItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            ParentId = phase.Id,
            NodeType = ProjectWorkItemNodeTypes.Task,
            Title = "Task 1",
            SortOrder = 0,
            PercentComplete = 100m
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.WorkItems.Add(phase);
        fixture.WorkItems.Add(existingTask);

        var service = fixture.CreateService();

        var result = await service.AddWorkItemAsync(project.Id, new CreateProjectWorkItemDto
        {
            ParentId = phase.Id,
            NodeType = ProjectWorkItemNodeTypes.Task,
            Title = "Task 2",
            PercentComplete = 0m
        });

        result.SortOrder.Should().Be(1);
        project.ProgressPercent.Should().Be(50m);
    }

    [Fact]
    public async Task GetResourceAllocationsAsync_ShouldPopulateRequirementRoutingInsights()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-2026-0201",
            Title = "Routing",
            Status = ProjectStatuses.Planned
        };
        var assignedUserId = Guid.NewGuid();
        var backupUserId = Guid.NewGuid();
        var scheduleSkillId = Guid.NewGuid();
        var certificationSkillId = Guid.NewGuid();

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.Employees.Add(new Employee { Id = assignedUserId, TenantId = tenantId, FirstName = "Alex", LastName = "Assigned", EmployeeNumber = "EMP-001" });
        fixture.Employees.Add(new Employee { Id = backupUserId, TenantId = tenantId, FirstName = "Bailey", LastName = "Backup", EmployeeNumber = "EMP-002" });
        fixture.Skills.Add(new Skill { Id = scheduleSkillId, TenantId = tenantId, Name = "Primavera P6" });
        fixture.Skills.Add(new Skill { Id = certificationSkillId, TenantId = tenantId, Name = "PMP" });
        fixture.EmployeeSkills.Add(new EmployeeSkill
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeId = assignedUserId,
            SkillId = scheduleSkillId,
            IsVerified = true
        });
        fixture.EmployeeSkills.Add(new EmployeeSkill
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeId = backupUserId,
            SkillId = scheduleSkillId,
            IsVerified = true
        });
        fixture.EmployeeSkills.Add(new EmployeeSkill
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeId = backupUserId,
            SkillId = certificationSkillId,
            IsVerified = true,
            IsCertified = true,
            CertificationDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-30)),
            CertificationExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(180))
        });
        fixture.ResourceAllocations.Add(new ProjectResourceAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            UserId = assignedUserId,
            AllocationRole = "Planner",
            AllocationType = "Hours",
            AllocationValue = 40m,
            PlannedHours = 40m,
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(10),
            BookingType = "Soft",
            Status = "Requested",
            RequiredSkillsJson = "[\"Primavera P6\"]",
            RequiredCertificationsJson = "[\"PMP\"]",
            RoutingPolicy = "CertifiedFirst"
        });

        var service = fixture.CreateService();

        var result = (await service.GetResourceAllocationsAsync(project.Id)).ToList();

        result.Should().ContainSingle();
        result[0].RequiredSkills.Should().ContainSingle("Primavera P6");
        result[0].RequiredCertifications.Should().ContainSingle("PMP");
        result[0].MissingCertifications.Should().ContainSingle("PMP");
        result[0].QualificationRisk.Should().Be("Critical");
        result[0].QualificationMatchPercent.Should().Be(50m);
        result[0].RecommendedUserId.Should().Be(backupUserId);
        result[0].RecommendedUserDisplayName.Should().Contain("Bailey");
        result[0].RoutingRecommendation.Should().Contain("CertifiedFirst");
    }

    [Fact]
    public async Task GetDashboardAsync_ShouldReturnExpandedMvpMetrics()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var plannedProject = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-001",
            Title = "Planned",
            Status = ProjectStatuses.Planned,
            EstimatedBudget = 100m,
            ApprovedBudget = 80m,
            ActualCost = 25m
        };
        var draftProject = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-002",
            Title = "Draft",
            Status = ProjectStatuses.Draft
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(plannedProject);
        fixture.Projects.Add(draftProject);
        fixture.WorkItems.Add(new ProjectWorkItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = plannedProject.Id,
            NodeType = ProjectWorkItemNodeTypes.Task,
            Title = "Late Task",
            PlannedEndDate = now.AddDays(-2),
            Status = "In Progress"
        });
        fixture.Milestones.Add(new ProjectMilestone
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = plannedProject.Id,
            Title = "Go Live",
            TargetDate = now.AddDays(-1),
            Status = "Draft"
        });
        fixture.Risks.Add(new ProjectRisk
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = plannedProject.Id,
            Title = "High Risk",
            Status = "Open",
            Exposure = 16
        });
        fixture.Issues.Add(new ProjectIssue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = plannedProject.Id,
            Title = "Open Issue",
            Status = "Open"
        });

        var service = fixture.CreateService();

        var result = await service.GetDashboardAsync();

        result.TotalProjects.Should().Be(2);
        result.DraftProjects.Should().Be(1);
        result.ActiveProjects.Should().Be(1);
        result.OverdueTasks.Should().Be(1);
        result.OverdueMilestones.Should().Be(1);
        result.TotalApprovedBudget.Should().Be(80m);
        result.AtRiskProjects.Should().ContainSingle(x => x.Id == plannedProject.Id);
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldRejectProgramThatDoesNotBelongToPortfolio()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var otherPortfolioId = Guid.NewGuid();
        var programId = Guid.NewGuid();

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Portfolios.Add(new ProjectPortfolio { Id = portfolioId, TenantId = tenantId, Code = "PF-1", Name = "Portfolio 1" });
        fixture.Portfolios.Add(new ProjectPortfolio { Id = otherPortfolioId, TenantId = tenantId, Code = "PF-2", Name = "Portfolio 2" });
        fixture.Programs.Add(new ProjectProgram { Id = programId, TenantId = tenantId, Code = "PG-1", Name = "Program 1", PortfolioId = otherPortfolioId });
        fixture.SettingsRepository
            .Setup(x => x.GetOrCreateDefaultAsync(tenantId, userId))
            .ReturnsAsync(new ProjectManagementSettings
            {
                TenantId = tenantId,
                ProjectNumberFormat = "PRJ-{YYYY}-{####}",
                RequireSponsor = false,
                DefaultApprovalRequired = true
            });

        var service = fixture.CreateService();

        var action = () => service.CreateProjectAsync(new CreateProjectDto
        {
            Title = "Misaligned Project",
            PortfolioId = portfolioId,
            ProgramId = programId
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not belong*");
    }

    [Fact]
    public async Task GetProgramSummaryReportAsync_ShouldAggregateProjectsByProgram()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var program = new ProjectProgram
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "ERP",
            Name = "ERP Rollout",
            PortfolioId = portfolioId,
            Portfolio = new ProjectPortfolio { Id = portfolioId, TenantId = tenantId, Code = "TRANS", Name = "Transformation" }
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Programs.Add(program);
        fixture.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-1",
            Title = "Wave 1",
            ProgramId = program.Id,
            PortfolioId = portfolioId,
            Status = ProjectStatuses.InProgress,
            EstimatedBudget = 100m,
            ActualCost = 60m,
            ProgressPercent = 80m
        });
        fixture.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-2",
            Title = "Wave 2",
            ProgramId = program.Id,
            PortfolioId = portfolioId,
            Status = ProjectStatuses.Planned,
            EstimatedBudget = 50m,
            ActualCost = 10m,
            ProgressPercent = 20m
        });

        var service = fixture.CreateService();

        var result = (await service.GetProgramSummaryReportAsync()).Single();

        result.ProgramCode.Should().Be("ERP");
        result.ProjectCount.Should().Be(2);
        result.ActiveProjectCount.Should().Be(2);
        result.TotalEstimatedBudget.Should().Be(150m);
        result.TotalActualCost.Should().Be(70m);
        result.AverageProgressPercent.Should().Be(50m);
    }

    [Fact]
    public async Task GetPerformanceAnalyticsReportAsync_ShouldCalculateForecastMetrics()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-900",
            Title = "Analytics",
            Status = ProjectStatuses.InProgress,
            ApprovedBudget = 200m,
            ActualCost = 120m,
            ProgressPercent = 50m
        });

        var service = fixture.CreateService();

        var result = (await service.GetPerformanceAnalyticsReportAsync()).Single();

        result.BudgetBaseline.Should().Be(200m);
        result.EarnedValue.Should().Be(100m);
        result.CostPerformanceIndex.Should().Be(0.83m);
        result.EstimateAtCompletion.Should().Be(240m);
        result.ProjectedVariance.Should().Be(-40m);
        result.HealthStatus.Should().Be("Watch");
    }

    [Fact]
    public async Task GetResourceCapacityReportAsync_ShouldFlagConflictsAndOverAllocation()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var firstProjectId = Guid.NewGuid();
        var secondProjectId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles(Constants.Roles.Manager);
        fixture.Projects.Add(new Project
        {
            Id = firstProjectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-001",
            Title = "Capacity A",
            Status = ProjectStatuses.InProgress
        });
        fixture.Projects.Add(new Project
        {
            Id = secondProjectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-002",
            Title = "Capacity B",
            Status = ProjectStatuses.InProgress
        });

        fixture.ResourceAllocations.Add(new ProjectResourceAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = firstProjectId,
            UserId = resourceId,
            AllocationType = "Hours",
            AllocationValue = 40m,
            StartDate = new DateTime(2026, 3, 9),
            EndDate = new DateTime(2026, 3, 13)
        });
        fixture.ResourceAllocations.Add(new ProjectResourceAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = secondProjectId,
            UserId = resourceId,
            AllocationType = "Hours",
            AllocationValue = 40m,
            StartDate = new DateTime(2026, 3, 11),
            EndDate = new DateTime(2026, 3, 17)
        });

        var service = fixture.CreateService();

        var result = (await service.GetResourceCapacityReportAsync(new DateTime(2026, 3, 9), new DateTime(2026, 3, 17))).Single();

        result.UserId.Should().Be(resourceId);
        result.AllocationCount.Should().Be(2);
        result.ConflictCount.Should().Be(2);
        result.TotalAllocatedHours.Should().Be(80m);
        result.CapacityUtilizationPercent.Should().BeGreaterThan(100m);
    }

    [Fact]
    public async Task GetResourceCapacityRecommendationsAsync_ShouldReturnCriticalRecommendationForOverAllocatedResource()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-CAP-1",
            Title = "Capacity Review"
        });
        fixture.ResourceAllocations.Add(new ProjectResourceAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = resourceId,
            AllocationType = "Hours",
            AllocationValue = 48m,
            StartDate = new DateTime(2026, 3, 9),
            EndDate = new DateTime(2026, 3, 13)
        });
        fixture.ResourceAllocations.Add(new ProjectResourceAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = resourceId,
            AllocationType = "Hours",
            AllocationValue = 40m,
            StartDate = new DateTime(2026, 3, 10),
            EndDate = new DateTime(2026, 3, 14)
        });

        var service = fixture.CreateService();

        var result = (await service.GetResourceCapacityRecommendationsAsync(new DateTime(2026, 3, 9), new DateTime(2026, 3, 14))).Single();

        result.UserId.Should().Be(resourceId);
        result.Severity.Should().Be("Critical");
        result.ProjectCodes.Should().Contain("PRJ-CAP-1");
        result.SuggestedReductionHours.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task GetResourceCapacityReportAsync_ShouldReduceEffectiveCapacityForApprovedLeave()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles(Constants.Roles.Manager);
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-LEAVE-1",
            Title = "Leave Capacity Project",
            Status = ProjectStatuses.InProgress
        });
        fixture.Employees.Add(new Employee
        {
            Id = resourceId,
            TenantId = tenantId,
            EmployeeNumber = "EMP-001",
            FirstName = "Amina",
            LastName = "Mensah"
        });
        fixture.ResourceAllocations.Add(new ProjectResourceAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = resourceId,
            AllocationType = "Hours",
            AllocationValue = 32m,
            PlannedHours = 32m,
            StartDate = new DateTime(2026, 3, 9),
            EndDate = new DateTime(2026, 3, 13),
            Status = "Approved"
        });
        fixture.LeaveRequests.Add(new LeaveRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeId = resourceId,
            LeaveTypeId = Guid.NewGuid(),
            RequestNumber = "LV-001",
            StartDate = new DateOnly(2026, 3, 11),
            EndDate = new DateOnly(2026, 3, 12),
            TotalDays = 2m,
            RequestDate = new DateTime(2026, 3, 1),
            Reason = "Annual leave",
            Status = LeaveStatus.Approved
        });

        var service = fixture.CreateService();

        var result = (await service.GetResourceCapacityReportAsync(new DateTime(2026, 3, 9), new DateTime(2026, 3, 13))).Single();

        result.UserId.Should().Be(resourceId);
        result.UserDisplayName.Should().Be("Amina Mensah (EMP-001)");
        result.StandardCapacityHours.Should().BeGreaterThan(result.EffectiveCapacityHours);
        result.ApprovedLeaveHours.Should().BeGreaterThan(0m);
        result.ApprovedLeaveDays.Should().BeGreaterThan(0m);
        result.LeaveRequestCount.Should().Be(1);
        result.CapacityUtilizationPercent.Should().BeGreaterThan(100m);
    }

    [Fact]
    public async Task GetResourceCapacityRecommendationsAsync_ShouldMentionLeaveWhenCapacityIsReduced()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles(Constants.Roles.Manager);
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-LEAVE-2",
            Title = "Leave Recommendation Project",
            Status = ProjectStatuses.InProgress
        });
        fixture.Employees.Add(new Employee
        {
            Id = resourceId,
            TenantId = tenantId,
            EmployeeNumber = "EMP-002",
            FirstName = "Kojo",
            LastName = "Owusu"
        });
        fixture.ResourceAllocations.Add(new ProjectResourceAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = resourceId,
            AllocationType = "Hours",
            AllocationValue = 40m,
            PlannedHours = 40m,
            StartDate = new DateTime(2026, 3, 9),
            EndDate = new DateTime(2026, 3, 13),
            Status = "Approved"
        });
        fixture.LeaveRequests.Add(new LeaveRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeId = resourceId,
            LeaveTypeId = Guid.NewGuid(),
            RequestNumber = "LV-002",
            StartDate = new DateOnly(2026, 3, 11),
            EndDate = new DateOnly(2026, 3, 13),
            TotalDays = 3m,
            RequestDate = new DateTime(2026, 3, 1),
            Reason = "Approved travel",
            Status = LeaveStatus.Approved
        });

        var service = fixture.CreateService();

        var result = (await service.GetResourceCapacityRecommendationsAsync(new DateTime(2026, 3, 9), new DateTime(2026, 3, 13))).Single();

        result.UserId.Should().Be(resourceId);
        result.UserDisplayName.Should().Be("Kojo Owusu (EMP-002)");
        result.LeaveRequestCount.Should().Be(1);
        result.ApprovedLeaveHours.Should().BeGreaterThan(0m);
        result.Recommendation.ToLowerInvariant().Should().Contain("leave");
        result.Severity.Should().Be("Critical");
        result.SuggestedReductionHours.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task GetResourceCapacityReportAsync_ShouldIncludeQualificationRiskFromEmployeeSkills()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles(Constants.Roles.Manager);
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-QUAL-1",
            Title = "Qualification Watch",
            Status = ProjectStatuses.InProgress
        });
        fixture.Employees.Add(new Employee
        {
            Id = resourceId,
            TenantId = tenantId,
            EmployeeNumber = "EMP-003",
            FirstName = "Esi",
            LastName = "Adjei"
        });
        fixture.ResourceAllocations.Add(new ProjectResourceAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = resourceId,
            AllocationType = "Hours",
            AllocationValue = 24m,
            StartDate = new DateTime(2026, 3, 9),
            EndDate = new DateTime(2026, 3, 13),
            Status = "Approved"
        });
        fixture.EmployeeSkills.Add(new EmployeeSkill
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeId = resourceId,
            SkillId = skillId,
            Skill = new Skill { Id = skillId, TenantId = tenantId, Name = "Project Control" },
            IsVerified = true,
            IsCertified = true,
            CertificationExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-2))
        });

        var service = fixture.CreateService();

        var result = (await service.GetResourceCapacityReportAsync(new DateTime(2026, 3, 9), new DateTime(2026, 3, 13))).Single();

        result.UserId.Should().Be(resourceId);
        result.VerifiedSkillCount.Should().Be(1);
        result.CertifiedSkillCount.Should().Be(1);
        result.ExpiredCertificationCount.Should().Be(1);
        result.ExpiringCertificationCount.Should().Be(0);
        result.QualificationRisk.Should().Be("Critical");
    }

    [Fact]
    public async Task GetResourceOptimizationSuggestionsAsync_ShouldPreferReplacementWithValidCertifiedSharedSkills()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sourceUserId = Guid.NewGuid();
        var weakCandidateId = Guid.NewGuid();
        var strongCandidateId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var skillAId = Guid.NewGuid();
        var skillBId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles(Constants.Roles.Manager);
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-QUAL-2",
            Title = "Optimization Coverage",
            Status = ProjectStatuses.InProgress
        });
        fixture.Employees.AddRange(
        [
            new Employee { Id = sourceUserId, TenantId = tenantId, EmployeeNumber = "EMP-010", FirstName = "Naa", LastName = "Tetteh" },
            new Employee { Id = weakCandidateId, TenantId = tenantId, EmployeeNumber = "EMP-011", FirstName = "Kojo", LastName = "Lamptey" },
            new Employee { Id = strongCandidateId, TenantId = tenantId, EmployeeNumber = "EMP-012", FirstName = "Ama", LastName = "Nyarko" }
        ]);
        fixture.ResourceAllocations.AddRange(
        [
            new ProjectResourceAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                UserId = sourceUserId,
                AllocationType = "Hours",
                AllocationValue = 40m,
                StartDate = new DateTime(2026, 3, 9),
                EndDate = new DateTime(2026, 3, 13),
                Status = "Approved"
            },
            new ProjectResourceAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                UserId = sourceUserId,
                AllocationType = "Hours",
                AllocationValue = 36m,
                StartDate = new DateTime(2026, 3, 11),
                EndDate = new DateTime(2026, 3, 15),
                Status = "Approved"
            }
        ]);
        fixture.EmployeeSkills.AddRange(
        [
            new EmployeeSkill
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EmployeeId = sourceUserId,
                SkillId = skillAId,
                Skill = new Skill { Id = skillAId, TenantId = tenantId, Name = "Scheduling" },
                IsVerified = true,
                IsCertified = true,
                CertificationExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(90))
            },
            new EmployeeSkill
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EmployeeId = sourceUserId,
                SkillId = skillBId,
                Skill = new Skill { Id = skillBId, TenantId = tenantId, Name = "Cost Control" },
                IsVerified = true,
                IsCertified = true,
                CertificationExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(90))
            },
            new EmployeeSkill
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EmployeeId = weakCandidateId,
                SkillId = skillAId,
                Skill = new Skill { Id = skillAId, TenantId = tenantId, Name = "Scheduling" },
                IsVerified = true,
                IsCertified = true,
                CertificationExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-5))
            },
            new EmployeeSkill
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EmployeeId = strongCandidateId,
                SkillId = skillAId,
                Skill = new Skill { Id = skillAId, TenantId = tenantId, Name = "Scheduling" },
                IsVerified = true,
                IsCertified = true,
                CertificationExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(60))
            },
            new EmployeeSkill
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EmployeeId = strongCandidateId,
                SkillId = skillBId,
                Skill = new Skill { Id = skillBId, TenantId = tenantId, Name = "Cost Control" },
                IsVerified = true,
                IsCertified = true,
                CertificationExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(60))
            }
        ]);

        var service = fixture.CreateService();

        var result = (await service.GetResourceOptimizationSuggestionsAsync(new DateTime(2026, 3, 9), new DateTime(2026, 3, 15))).Single();

        result.UserId.Should().Be(sourceUserId);
        result.SuggestedReplacementUserId.Should().Be(strongCandidateId);
        result.MatchedSkillCount.Should().Be(2);
        result.MatchedCertifiedSkillCount.Should().Be(2);
        result.ReplacementQualificationRisk.Should().Be("Healthy");
        result.MatchedSkills.Should().Contain(new[] { "Scheduling", "Cost Control" });
    }

    [Fact]
    public async Task LookupResourcesAsync_ShouldReturnEmployeeDisplayNames()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles(Constants.Roles.Manager);
        fixture.Employees.Add(new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeNumber = "EMP-010",
            FirstName = "Abena",
            LastName = "Boateng"
        });
        fixture.Employees.Add(new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeNumber = "EMP-011",
            FirstName = "Yaw",
            LastName = "Ansah"
        });

        var service = fixture.CreateService();

        var result = (await service.LookupResourcesAsync("EMP-0", 10)).ToList();

        result.Should().HaveCount(2);
        result.Select(x => x.DisplayName).Should().Contain(new[]
        {
            "Abena Boateng (EMP-010)",
            "Yaw Ansah (EMP-011)"
        });
    }

    [Fact]
    public async Task CreateAndApproveBudgetRevisionAsync_ShouldUpdateProjectApprovedBudget()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-BUD-001",
            Title = "Budget Revision Project",
            Status = ProjectStatuses.InProgress,
            ProjectManagerId = userId,
            EstimatedBudget = 100m,
            ApprovedBudget = 100m
        });
        fixture.WorkflowStatusAdapterRegistry
            .Setup(x => x.GetAdapter("ProjectBudgetRevision"))
            .Returns(new ProjectBudgetRevisionWorkflowStatusAdapter());

        var service = fixture.CreateService();
        var created = await service.CreateBudgetRevisionAsync(projectId, new CreateProjectBudgetRevisionDto
        {
            RevisionName = "Budget Increase",
            RevisionType = "Increase",
            EstimatedBudget = 120m,
            ApprovedBudget = 150m,
            CommittedCost = 80m,
            ForecastCost = 140m,
            ThresholdWarningPercent = 70m,
            ThresholdCriticalPercent = 90m
        });

        fixture.WorkflowIntegrationService
            .Setup(x => x.SubmitAsync("ProjectBudgetRevision", created.Id))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult
                {
                    Success = true,
                    Status = WorkflowInstanceStatus.InProgress
                },
                WorkflowOutcome.Pending));
        fixture.WorkflowIntegrationService
            .Setup(x => x.CanUserApproveAsync("ProjectBudgetRevision", created.Id, userId))
            .ReturnsAsync(true);
        fixture.WorkflowIntegrationService
            .Setup(x => x.ProcessApprovalAsync("ProjectBudgetRevision", created.Id, userId, "Approve", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult
                {
                    Success = true,
                    Status = WorkflowInstanceStatus.Completed
                },
                WorkflowOutcome.Approved));

        var submitted = await service.SubmitBudgetRevisionAsync(created.Id, userId);
        var approved = await service.ApproveBudgetRevisionAsync(created.Id, userId);

        submitted.Status.Should().Be("PendingApproval");
        approved.Status.Should().Be("Approved");
        fixture.Projects.Single(x => x.Id == projectId).ApprovedBudget.Should().Be(150m);
        fixture.Projects.Single(x => x.Id == projectId).BudgetStatus.Should().Be("Approved");
    }

    [Fact]
    public async Task GetFinancialControlSummaryAsync_ShouldUseApprovedBudgetRevisionAndActiveForecastVersion()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var pendingRequisitionId = Guid.NewGuid();
        var openPurchaseOrderId = Guid.NewGuid();
        var partiallyReceivedPurchaseOrderId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var fullyOrderedRequisitionId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-FIN-001",
            Title = "Financial Summary Project",
            Status = ProjectStatuses.InProgress,
            ProjectManagerId = userId,
            EstimatedBudget = 500m,
            ApprovedBudget = 500m,
            ActualCost = 400m,
            ProgressPercent = 50m,
            StartDate = DateTime.UtcNow.Date.AddDays(-5),
            TargetEndDate = DateTime.UtcNow.Date.AddDays(5)
        });
        fixture.BudgetRevisions.Add(new ProjectBudgetRevision
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            VersionNumber = 2,
            RevisionName = "Approved Baseline",
            EstimatedBudget = 900m,
            ApprovedBudget = 1000m,
            CommittedCost = 300m,
            ForecastCost = 1100m,
            ThresholdWarningPercent = 60m,
            ThresholdCriticalPercent = 80m,
            Status = "Approved"
        });
        fixture.ForecastVersions.Add(new ProjectForecastVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            VersionNumber = 1,
            VersionName = "April Forecast",
            ForecastCost = 1150m,
            EstimateAtCompletion = 1250m,
            ForecastRevenue = 1600m,
            ForecastMargin = 350m,
            IsActive = true
        });
        fixture.RevenueRecognitions.Add(new ProjectRevenueRecognition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            RecognitionPeriod = "2026-03",
            RecognizedRevenue = 1400m,
            RecognizedCost = 400m,
            GrossMargin = 1000m
        });
        fixture.PurchaseRequisitions.Add(new PurchaseRequisition
        {
            Id = pendingRequisitionId,
            TenantId = tenantId,
            ProjectId = projectId,
            RequisitionNumber = "PR-1001",
            RequestedById = userId,
            Status = "Submitted",
            Currency = "USD",
            TotalAmount = 200m
        });
        fixture.PurchaseRequisitions.Add(new PurchaseRequisition
        {
            Id = fullyOrderedRequisitionId,
            TenantId = tenantId,
            ProjectId = projectId,
            RequisitionNumber = "PR-1002",
            RequestedById = userId,
            Status = "Ordered",
            Currency = "USD",
            TotalAmount = 250m
        });
        fixture.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = openPurchaseOrderId,
            TenantId = tenantId,
            BusinessPartnerId = Guid.NewGuid(),
            OrderNumber = "PO-2001",
            SourceRequisitionId = pendingRequisitionId,
            Status = "Approved",
            TotalAmount = 300m
        });
        fixture.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = partiallyReceivedPurchaseOrderId,
            TenantId = tenantId,
            BusinessPartnerId = Guid.NewGuid(),
            OrderNumber = "PO-2002",
            SourceRequisitionId = fullyOrderedRequisitionId,
            Status = "PartiallyReceived",
            TotalAmount = 250m
        });
        fixture.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = Guid.NewGuid(),
            OrderNumber = "PO-2003",
            SourceRequisitionId = fullyOrderedRequisitionId,
            Status = "Cancelled",
            TotalAmount = 100m
        });
        fixture.PurchaseOrderItems.Add(new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseOrderId = partiallyReceivedPurchaseOrderId,
            OrderedQuantity = 10m,
            UnitPrice = 25m,
            LineTotal = 250m
        });
        fixture.PurchaseOrderReceipts.Add(new PurchaseOrderReceipt
        {
            Id = receiptId,
            TenantId = tenantId,
            PurchaseOrderId = partiallyReceivedPurchaseOrderId,
            ReceiptNumber = "RCV-3001",
            Status = "Received",
            RequiresInspection = true
        });
        fixture.PurchaseOrderReceiptItems.Add(new PurchaseOrderReceiptItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReceiptId = receiptId,
            PurchaseOrderItemId = fixture.PurchaseOrderItems[0].Id,
            ReceivedQuantity = 6m,
            AcceptedQuantity = 6m
        });

        var service = fixture.CreateService();

        var result = await service.GetFinancialControlSummaryAsync(projectId);

        result.BudgetBaseline.Should().Be(1000m);
        result.CommittedCost.Should().Be(550m);
        result.PendingCost.Should().Be(200m);
        result.ForecastCost.Should().Be(1150m);
        result.EstimateAtCompletion.Should().Be(1250m);
        result.ThresholdWarningPercent.Should().Be(60m);
        result.ThresholdCriticalPercent.Should().Be(80m);
        result.ProcurementRequestedAmount.Should().Be(200m);
        result.ProcurementCommittedAmount.Should().Be(550m);
        result.ProcurementOpenCommitmentAmount.Should().Be(550m);
        result.ProcurementReceivedAmount.Should().Be(150m);
        result.ProcurementPendingInspectionAmount.Should().Be(150m);
        result.TotalExposureAmount.Should().Be(1150m);
        result.PlannedValue.Should().Be(500m);
        result.EarnedValue.Should().Be(500m);
        result.ScheduleVariance.Should().Be(0m);
        result.CostVariance.Should().Be(100m);
        result.CostPerformanceIndex.Should().Be(1.25m);
        result.SchedulePerformanceIndex.Should().Be(1m);
        result.ToCompletePerformanceIndex.Should().Be(0.59m);
        result.ProfitabilityPercent.Should().Be(71.43m);
        result.HealthStatus.Should().Be("Watch");
        result.CurrentBudgetRevisionName.Should().Be("Approved Baseline");
        result.ActiveForecastVersionName.Should().Be("April Forecast");
        result.ThresholdExceeded.Should().BeTrue();
        result.Alerts.Should().Contain(x => x.Message.Contains("procurement exposure", StringComparison.OrdinalIgnoreCase));
        result.Alerts.Should().Contain(x => x.Message.Contains("pending inspection", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetFinancialControlSummaryAsync_ShouldAllowFinanceOfficerProjectMember()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles();
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-FIN-ACCESS",
            Title = "Finance Access Project",
            EstimatedBudget = 100m,
            ActualCost = 25m
        });
        fixture.Members.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = userId,
            Role = "Finance Officer",
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });

        var service = fixture.CreateService();

        var result = await service.GetFinancialControlSummaryAsync(projectId);

        result.ProjectId.Should().Be(projectId);
        result.BudgetBaseline.Should().Be(100m);
    }

    [Fact]
    public async Task GetFinancialControlSummaryAsync_ShouldDenyUserWithoutProjectFinancialAccess()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles();
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-FIN-DENY",
            Title = "Restricted Financial Project",
            CreatedById = Guid.NewGuid(),
            ProjectManagerId = Guid.NewGuid(),
            SponsorId = Guid.NewGuid(),
            EstimatedBudget = 120m
        });

        var service = fixture.CreateService();

        await FluentActions.Invoking(() => service.GetFinancialControlSummaryAsync(projectId))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AddResourceAllocationAsync_ShouldAllowExecutionProjectMember()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var assignedUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles();
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-EXEC-ALLOW",
            Title = "Execution Access Project",
            CreatedById = Guid.NewGuid(),
            ProjectManagerId = Guid.NewGuid(),
            SponsorId = Guid.NewGuid()
        });
        fixture.Members.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = userId,
            Role = "TeamMember",
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });
        fixture.Employees.Add(new Employee
        {
            Id = assignedUserId,
            TenantId = tenantId,
            FirstName = "Taylor",
            LastName = "Resource",
            EmployeeNumber = "EMP-EXEC-1"
        });

        var service = fixture.CreateService();

        var result = await service.AddResourceAllocationAsync(projectId, new CreateProjectResourceAllocationDto
        {
            UserId = assignedUserId,
            AllocationRole = "Engineer",
            AllocationType = "Hours",
            AllocationValue = 24m,
            PlannedHours = 24m,
            StartDate = new DateTime(2026, 3, 16),
            EndDate = new DateTime(2026, 3, 20),
            BookingType = "Soft",
            Status = "Requested"
        });

        result.ProjectId.Should().Be(projectId);
        result.UserId.Should().Be(assignedUserId);
        fixture.ResourceAllocations.Should().ContainSingle(x =>
            x.ProjectId == projectId
            && x.UserId == assignedUserId
            && x.AllocationRole == "Engineer");
    }

    [Fact]
    public async Task GetFinancialControlSummaryAsync_ShouldDenyExecutionOnlyProjectMember()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles();
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-FIN-SPLIT",
            Title = "Finance Split Project",
            CreatedById = Guid.NewGuid(),
            ProjectManagerId = Guid.NewGuid(),
            SponsorId = Guid.NewGuid(),
            ApprovedBudget = 1000m
        });
        fixture.Members.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = userId,
            Role = "TeamMember",
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });

        var service = fixture.CreateService();

        await FluentActions.Invoking(() => service.GetFinancialControlSummaryAsync(projectId))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AddRiskAsync_ShouldAllowGovernanceProjectMember()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles();
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-GOV-ALLOW",
            Title = "Governance Access Project",
            CreatedById = Guid.NewGuid(),
            ProjectManagerId = Guid.NewGuid(),
            SponsorId = Guid.NewGuid()
        });
        fixture.Members.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = userId,
            Role = "RiskOfficer",
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });

        var service = fixture.CreateService();

        var result = await service.AddRiskAsync(projectId, new CreateProjectRiskDto
        {
            Title = "Regulatory control gap",
            Status = "Open",
            Category = "Compliance",
            Probability = 4,
            Impact = 5,
            ResponseStrategy = "Mitigate"
        });

        result.Id.Should().NotBeEmpty();
        result.Title.Should().Be("Regulatory control gap");
        result.Exposure.Should().Be(20);
        fixture.Risks.Should().ContainSingle(x =>
            x.ProjectId == projectId
            && x.Title == "Regulatory control gap"
            && x.Exposure == 20);
    }

    [Fact]
    public async Task AddRiskAsync_ShouldDenyFinanceOnlyProjectMember()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles();
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-GOV-DENY",
            Title = "Governance Restricted Project",
            CreatedById = Guid.NewGuid(),
            ProjectManagerId = Guid.NewGuid(),
            SponsorId = Guid.NewGuid()
        });
        fixture.Members.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = userId,
            Role = "Finance Officer",
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });

        var service = fixture.CreateService();

        await FluentActions.Invoking(() => service.AddRiskAsync(projectId, new CreateProjectRiskDto
        {
            Title = "Unauthorized governance edit",
            Probability = 2,
            Impact = 3
        })).Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetIntegrationSummaryAsync_ShouldIncludeProcurementAndInventoryLinks()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-INT-1",
            Title = "Integrated Project",
            CreatedById = userId,
            PortfolioId = portfolioId,
            ProgramId = programId
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.Portfolios.Add(new ProjectPortfolio
        {
            Id = portfolioId,
            TenantId = tenantId,
            Code = "PORT-001",
            Name = "Delivery Portfolio",
            Status = "Active"
        });
        fixture.Programs.Add(new ProjectProgram
        {
            Id = programId,
            TenantId = tenantId,
            PortfolioId = portfolioId,
            Code = "PROG-001",
            Name = "Growth Program",
            Status = "Active"
        });
        fixture.PurchaseRequisitions.Add(new PurchaseRequisition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            ProjectCode = project.ProjectCode,
            RequisitionNumber = "PR-001",
            Status = "Pending Approval",
            Currency = "USD",
            TotalAmount = 500m
        });
        var purchaseOrderId = Guid.NewGuid();
        fixture.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = purchaseOrderId,
            TenantId = tenantId,
            SourceRequisitionId = fixture.PurchaseRequisitions[0].Id,
            SourceRequisitionNumber = "PR-001",
            OrderNumber = "PO-001",
            BusinessPartnerId = Guid.NewGuid(),
            Status = "PartiallyReceived",
            Currency = "USD",
            TotalAmount = 650m
        });
        fixture.PurchaseOrderReceipts.Add(new PurchaseOrderReceipt
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseOrderId = purchaseOrderId,
            ReceiptNumber = "POR-001",
            Status = "Received",
            RequiresInspection = true
        });
        fixture.InventoryRequisitions.Add(new InventoryRequisition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            ProjectCode = project.ProjectCode,
            RequisitionNumber = "IR-001",
            Status = RequisitionStatus.PartiallyIssued,
            TotalValue = 120m,
            Items =
            {
                new InventoryRequisitionItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    InventoryItemId = Guid.NewGuid(),
                    RequestedQuantity = 5m,
                    ApprovedQuantity = 5m,
                    IssuedQuantity = 3m,
                    UnitCost = 40m,
                    LineValue = 120m
                }
            }
        });

        var service = fixture.CreateService();

        var result = await service.GetIntegrationSummaryAsync(project.Id);

        result.PurchaseRequisitionCount.Should().Be(1);
        result.PendingPurchaseRequisitionCount.Should().Be(1);
        result.PurchaseRequisitionAmount.Should().Be(500m);
        result.PurchaseOrderCount.Should().Be(1);
        result.OpenPurchaseOrderCount.Should().Be(1);
        result.PurchaseOrderAmount.Should().Be(650m);
        result.PurchaseReceiptCount.Should().Be(1);
        result.PendingPurchaseReceiptInspectionCount.Should().Be(1);
        result.InventoryRequisitionCount.Should().Be(1);
        result.PendingInventoryRequisitionCount.Should().Be(0);
        result.InventoryRequisitionValue.Should().Be(120m);
        result.IssuedInventoryRequisitionCount.Should().Be(1);
        result.IssuedInventoryValue.Should().Be(120m);
        result.ReturnedInventoryValue.Should().Be(0m);
        result.NetIssuedInventoryValue.Should().Be(120m);
        result.Links.Should().Contain(x => x.LinkType == "PurchaseRequisition" && x.Reference.Contains("PR-001"));
        result.Links.Should().Contain(x => x.LinkType == "PurchaseOrder" && x.Reference.Contains("PO-001"));
        result.Links.Should().Contain(x => x.LinkType == "PurchaseReceipt" && x.Reference.Contains("POR-001"));
        result.Links.Should().Contain(x => x.LinkType == "InventoryRequisition" && x.Reference.Contains("IR-001"));
        result.Links.Should().Contain(x => x.LinkType == "Portfolio" && x.Reference == "Delivery Portfolio");
        result.Links.Should().Contain(x => x.LinkType == "Program" && x.Reference == "Growth Program");
        result.Warnings.Should().Contain(x => x.Contains("Procurement requisitions"));
        result.Warnings.Should().Contain(x => x.Contains("Purchase orders"));
        result.Warnings.Should().Contain(x => x.Contains("Purchase receipts"));
        result.Warnings.Should().NotContain(x => x.Contains("Inventory has been issued", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetMaterialReconciliationReportAsync_ShouldHighlightProjectsWithMaterialVariance()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var issuedItemId = Guid.NewGuid();
        var issueRequisitionId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-MAT-001",
            Title = "Materials Reconciliation",
            Status = ProjectStatuses.InProgress
        });
        fixture.InventoryItems.Add(new InventoryItem
        {
            Id = issuedItemId,
            TenantId = tenantId,
            ItemCode = "MAT-001",
            Name = "Concrete Mix",
            UnitOfMeasure = "Bag"
        });
        fixture.InventoryRequisitions.Add(new InventoryRequisition
        {
            Id = issueRequisitionId,
            TenantId = tenantId,
            ProjectId = projectId,
            ProjectCode = "PRJ-MAT-001",
            RequisitionNumber = "IR-MAT-001",
            Status = RequisitionStatus.Issued,
            TotalValue = 300m,
            Items =
            {
                new InventoryRequisitionItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    InventoryItemId = issuedItemId,
                    RequestedQuantity = 10m,
                    ApprovedQuantity = 10m,
                    IssuedQuantity = 10m,
                    UnitCost = 30m,
                    LineValue = 300m
                }
            }
        });
        fixture.InventoryRequisitions.Add(new InventoryRequisition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            ProjectCode = "PRJ-MAT-001",
            RequisitionNumber = "IR-MAT-002",
            Status = RequisitionStatus.Approved,
            TotalValue = 120m
        });
        fixture.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InventoryItemId = issuedItemId,
            MovementType = "Issue",
            Quantity = 10m,
            UnitCost = 30m,
            TotalValue = 300m,
            MovementDate = DateTime.UtcNow.Date,
            ReferenceType = ReferenceType.Requisition,
            ReferenceId = issueRequisitionId,
            ReferenceNumber = "IR-MAT-001"
        });
        fixture.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InventoryItemId = issuedItemId,
            MovementType = "Return",
            Quantity = 1m,
            UnitCost = 20m,
            TotalValue = 20m,
            MovementDate = DateTime.UtcNow.Date.AddDays(1),
            ReferenceType = ReferenceType.Requisition,
            ReferenceId = issueRequisitionId,
            ReferenceNumber = "IR-MAT-001"
        });

        var service = fixture.CreateService();

        var result = (await service.GetMaterialReconciliationReportAsync()).Single();

        result.ProjectCode.Should().Be("PRJ-MAT-001");
        result.RequisitionCount.Should().Be(2);
        result.PendingRequisitionCount.Should().Be(1);
        result.IssuedRequisitionCount.Should().Be(1);
        result.RequestedValue.Should().Be(420m);
        result.IssuedValue.Should().Be(300m);
        result.ReturnedValue.Should().Be(20m);
        result.NetIssuedValue.Should().Be(280m);
        result.TrackedMaterialCost.Should().Be(280m);
        result.MaterialCostVariance.Should().Be(0m);
        result.ReconciliationStatus.Should().Be("Balanced");
    }

    [Fact]
    public async Task GetProcurementReconciliationReportAsync_ShouldTrackReceiptIssueAndPostingVariance()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var requisitionId = Guid.NewGuid();
        var purchaseOrderId = Guid.NewGuid();
        var purchaseOrderItemId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var inventoryRequisitionId = Guid.NewGuid();
        var inventoryItemId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-PROC-001",
            Title = "Procurement Reconciliation",
            Status = ProjectStatuses.InProgress
        });
        fixture.InventoryItems.Add(new InventoryItem
        {
            Id = inventoryItemId,
            TenantId = tenantId,
            ItemCode = "PIPE-001",
            Name = "Process Pipe",
            UnitOfMeasure = "EA"
        });
        fixture.PurchaseRequisitions.Add(new PurchaseRequisition
        {
            Id = requisitionId,
            TenantId = tenantId,
            ProjectId = projectId,
            RequestedById = userId,
            RequisitionNumber = "PR-4001",
            Status = "Ordered",
            Currency = "USD",
            TotalAmount = 800m
        });
        fixture.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = purchaseOrderId,
            TenantId = tenantId,
            BusinessPartnerId = Guid.NewGuid(),
            OrderNumber = "PO-4001",
            SourceRequisitionId = requisitionId,
            Status = "PartiallyReceived",
            TotalAmount = 800m
        });
        fixture.PurchaseOrderItems.Add(new PurchaseOrderItem
        {
            Id = purchaseOrderItemId,
            TenantId = tenantId,
            PurchaseOrderId = purchaseOrderId,
            InventoryItemId = inventoryItemId,
            OrderedQuantity = 8m,
            UnitPrice = 100m,
            LineTotal = 800m
        });
        fixture.PurchaseOrderReceipts.Add(new PurchaseOrderReceipt
        {
            Id = receiptId,
            TenantId = tenantId,
            PurchaseOrderId = purchaseOrderId,
            ReceiptNumber = "RCV-4001",
            Status = "Accepted",
            RequiresInspection = true
        });
        fixture.PurchaseOrderReceiptItems.Add(new PurchaseOrderReceiptItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReceiptId = receiptId,
            PurchaseOrderItemId = purchaseOrderItemId,
            ReceivedQuantity = 6m,
            AcceptedQuantity = 5m,
            QualityStatus = "Passed"
        });
        fixture.InventoryRequisitions.Add(new InventoryRequisition
        {
            Id = inventoryRequisitionId,
            TenantId = tenantId,
            ProjectId = projectId,
            ProjectCode = "PRJ-PROC-001",
            RequisitionNumber = "IR-4001",
            Status = RequisitionStatus.Issued,
            TotalValue = 450m,
            Items =
            {
                new InventoryRequisitionItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    InventoryItemId = inventoryItemId,
                    RequestedQuantity = 5m,
                    ApprovedQuantity = 5m,
                    IssuedQuantity = 5m,
                    UnitCost = 90m,
                    LineValue = 450m
                }
            }
        });
        fixture.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InventoryItemId = inventoryItemId,
            MovementType = "Issue",
            Quantity = 5m,
            UnitCost = 90m,
            TotalValue = 450m,
            MovementDate = DateTime.UtcNow.Date,
            ReferenceType = ReferenceType.Requisition,
            ReferenceId = inventoryRequisitionId,
            ReferenceNumber = "IR-4001"
        });

        var service = fixture.CreateService();

        var result = (await service.GetProcurementReconciliationReportAsync(20)).Single();

        result.ProjectCode.Should().Be("PRJ-PROC-001");
        result.PurchaseRequisitionCount.Should().Be(1);
        result.PurchaseOrderCount.Should().Be(1);
        result.PurchaseReceiptCount.Should().Be(1);
        result.ReceivedAmount.Should().Be(600m);
        result.AcceptedReceiptAmount.Should().Be(500m);
        result.PendingInspectionAmount.Should().Be(100m);
        result.IssuedInventoryValue.Should().Be(450m);
        result.NetIssuedInventoryValue.Should().Be(450m);
        result.PostedMaterialCost.Should().Be(450m);
        result.ReceiptToIssueVariance.Should().Be(50m);
        result.IssueToPostingVariance.Should().Be(0m);
        result.ReconciliationStatus.Should().Be("PendingInspection");
    }

    [Fact]
    public async Task GetIntegrationSummaryAsync_ShouldNotWarnWhenIssuedInventoryIsTrackedAsMaterialCost()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-INT-2",
            Title = "Tracked Inventory Project",
            CreatedById = userId
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.InventoryRequisitions.Add(new InventoryRequisition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            ProjectCode = project.ProjectCode,
            RequisitionNumber = "IR-TRACK-001",
            Status = RequisitionStatus.Issued,
            TotalValue = 120m,
            Items =
            {
                new InventoryRequisitionItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    InventoryItemId = Guid.NewGuid(),
                    RequestedQuantity = 3m,
                    ApprovedQuantity = 3m,
                    IssuedQuantity = 3m,
                    UnitCost = 40m,
                    LineValue = 120m
                }
            }
        });
        fixture.Expenses.Add(new ProjectExpense
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            UserId = userId,
            ExpenseDate = DateTime.UtcNow.Date,
            Category = "Materials",
            Currency = "USD",
            Amount = 120m,
            TaxAmount = 0m,
            Status = "Approved",
            Notes = "Material issue from requisition IR-TRACK-001"
        });

        var service = fixture.CreateService();

        var result = await service.GetIntegrationSummaryAsync(project.Id);

        result.IssuedInventoryValue.Should().Be(120m);
        result.ReturnedInventoryValue.Should().Be(0m);
        result.NetIssuedInventoryValue.Should().Be(120m);
        result.Warnings.Should().NotContain(x => x.Contains("material consumption", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SubstituteResourceAllocationAsync_ShouldCreateReplacementAndMarkSourceSubstituted()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sourceUserId = Guid.NewGuid();
        var replacementUserId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-RES-1",
            Title = "Resource Substitution"
        };
        var allocation = new ProjectResourceAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            UserId = sourceUserId,
            AllocationRole = "Engineer",
            AllocationType = "Hours",
            AllocationValue = 40m,
            PlannedHours = 40m,
            StartDate = new DateTime(2026, 3, 9),
            EndDate = new DateTime(2026, 3, 13),
            Status = "Approved"
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.ResourceAllocations.Add(allocation);

        var service = fixture.CreateService();

        var result = await service.SubstituteResourceAllocationAsync(allocation.Id, new SubstituteProjectResourceAllocationDto
        {
            ReplacementUserId = replacementUserId,
            FullReplacement = true,
            ApproveReplacement = true,
            Reason = "Primary engineer unavailable"
        });

        allocation.Status.Should().Be("Substituted");
        allocation.ReplacementAllocationId.Should().Be(result.ReplacementAllocation.Id);
        result.SourceAllocation.Id.Should().Be(allocation.Id);
        result.ReplacementAllocation.UserId.Should().Be(replacementUserId);
        result.ReplacementAllocation.SourceAllocationId.Should().Be(allocation.Id);
        fixture.ResourceAllocations.Should().ContainSingle(x => x.UserId == replacementUserId);
    }

    [Fact]
    public async Task GenerateInvoiceRequestFromScheduleAsync_ShouldCreateInvoiceRequestAndMarkScheduleInvoiced()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-INV-1",
            Title = "Billing Project"
        };
        var schedule = new ProjectBillingSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = "Kickoff Invoice",
            Amount = 1250m,
            BillingDate = new DateTime(2026, 4, 1),
            Status = "Draft"
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.BillingSchedules.Add(schedule);
        fixture.TenantSettingsService
            .Setup(x => x.GetBaseCurrencyAsync())
            .ReturnsAsync("EUR");

        var service = fixture.CreateService();

        var result = await service.GenerateInvoiceRequestFromScheduleAsync(schedule.Id, "Release milestone reached");

        result.ProjectId.Should().Be(project.Id);
        result.BillingScheduleId.Should().Be(schedule.Id);
        result.RequestNumber.Should().Be($"INVREQ-{DateTime.UtcNow.Year}-0001");
        result.RequestedAmount.Should().Be(1250m);
        result.Currency.Should().Be("EUR");
        result.Notes.Should().Be("Release milestone reached");
        fixture.InvoiceRequests.Should().ContainSingle(x => x.BillingScheduleId == schedule.Id);
        schedule.Status.Should().Be("Invoiced");
    }

    [Fact]
    public async Task GenerateInvoiceRequestFromScheduleAsync_ShouldSyncLinkedContractMilestone()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var contractMilestoneId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-INV-2",
            Title = "Contract Billing Project"
        };
        var schedule = new ProjectBillingSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            ContractId = contractId,
            ContractMilestoneId = contractMilestoneId,
            Name = "Stage Billing",
            Amount = 500m,
            BillingDate = new DateTime(2026, 4, 15),
            Status = "Draft"
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.BillingSchedules.Add(schedule);

        var service = fixture.CreateService();

        var result = await service.GenerateInvoiceRequestFromScheduleAsync(schedule.Id, "Contract milestone invoiced");

        fixture.ContractService.Verify(x => x.UpdateMilestoneStatusAsync(
            contractMilestoneId,
            It.Is<ErpSystem.Core.DTOs.Procurement.UpdateMilestoneStatusDto>(dto => dto.Status == "Invoiced" && dto.InvoiceNumber == result.RequestNumber)),
            Times.Once);
    }

    [Fact]
    public async Task SubmitInvoiceRequestAsync_ShouldMoveDraftToSubmitted()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-INV-SUB",
            Title = "Invoice Request Submission",
            ProjectManagerId = userId
        };

        var request = new ProjectInvoiceRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            RequestNumber = "INVREQ-2026-0101",
            RequestedAmount = 250m,
            Status = "Draft"
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.InvoiceRequests.Add(request);

        var service = fixture.CreateService();

        var result = await service.SubmitInvoiceRequestAsync(request.Id, "Ready for finance review");

        result.Status.Should().Be("Submitted");
        result.SubmittedAt.Should().NotBeNull();
        fixture.InvoiceRequests.Single(x => x.Id == request.Id).Status.Should().Be("Submitted");
        fixture.InvoiceRequests.Single(x => x.Id == request.Id).Notes.Should().Contain("Ready for finance review");
    }

    [Fact]
    public async Task MarkInvoiceRequestSentToFinanceAsync_ShouldStampExternalReference()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-INV-SYNC",
            Title = "Invoice Sync Project",
            ProjectManagerId = userId
        };

        var request = new ProjectInvoiceRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            RequestNumber = "INVREQ-2026-0102",
            RequestedAmount = 300m,
            Status = "Submitted",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.InvoiceRequests.Add(request);

        var service = fixture.CreateService();

        var result = await service.MarkInvoiceRequestSentToFinanceAsync(request.Id, "AR-00045", "Exported to finance");

        result.Status.Should().Be("SentToFinance");
        result.ExternalReference.Should().Be("AR-00045");
        fixture.InvoiceRequests.Single(x => x.Id == request.Id).Status.Should().Be("SentToFinance");
        fixture.InvoiceRequests.Single(x => x.Id == request.Id).ExternalReference.Should().Be("AR-00045");
        fixture.InvoiceRequests.Single(x => x.Id == request.Id).Notes.Should().Contain("Exported to finance");
    }

    [Fact]
    public async Task MarkInvoiceRequestInvoicedAsync_ShouldUpdateStatusAndSchedule()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-INV-1",
            Title = "Invoice Project",
            CreatedById = userId
        };
        var schedule = new ProjectBillingSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = "Billing checkpoint",
            BillingDate = DateTime.UtcNow.Date,
            Amount = 500m,
            Status = "Approved",
            IsBillable = true
        };
        var request = new ProjectInvoiceRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            BillingScheduleId = schedule.Id,
            RequestNumber = "INVREQ-INV-1",
            RequestedAmount = 500m,
            Status = "SentToFinance"
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.BillingSchedules.Add(schedule);
        fixture.InvoiceRequests.Add(request);

        var service = fixture.CreateService();

        var result = await service.MarkInvoiceRequestInvoicedAsync(request.Id, "INV-9001", "Raised in finance");

        result.Status.Should().Be("Invoiced");
        fixture.InvoiceRequests.Single(x => x.Id == request.Id).ExternalReference.Should().Be("INV-9001");
        fixture.BillingSchedules.Single(x => x.Id == schedule.Id).Status.Should().Be("Invoiced");
    }

    [Fact]
    public async Task MarkInvoiceRequestPaidAsync_ShouldUpdateStatusAndCollectedCash()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-INV-2",
            Title = "Cash Collection Project",
            CreatedById = userId
        };
        var request = new ProjectInvoiceRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            RequestNumber = "INVREQ-INV-2",
            RequestedAmount = 900m,
            Status = "Invoiced"
        };
        var recognition = new ProjectRevenueRecognition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            InvoiceRequestId = request.Id,
            RecognitionPeriod = "2026-03",
            RecognizedRevenue = 900m,
            RecognizedCost = 300m,
            GrossMargin = 600m,
            CashCollected = 0m,
            Status = "Recognized"
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.InvoiceRequests.Add(request);
        fixture.RevenueRecognitions.Add(recognition);

        var service = fixture.CreateService();

        var result = await service.MarkInvoiceRequestPaidAsync(request.Id, "Cash received");

        result.Status.Should().Be("Paid");
        fixture.InvoiceRequests.Single(x => x.Id == request.Id).Notes.Should().Contain("Cash received");
        fixture.RevenueRecognitions.Single(x => x.Id == recognition.Id).CashCollected.Should().Be(900m);
        fixture.RevenueRecognitions.Single(x => x.Id == recognition.Id).Status.Should().Be("Collected");
    }

    [Fact]
    public async Task GenerateRevenueRecognitionAsync_ShouldRefreshCurrentPeriodRecordInsteadOfCreatingDuplicate()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-REV-1",
            Title = "Revenue Refresh",
            ActualCost = 120m,
            ProjectManagerId = userId
        };

        var existingRecognition = new ProjectRevenueRecognition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            RecognitionPeriod = DateTime.UtcNow.ToString("yyyy-MM"),
            RecognizedRevenue = 10m,
            RecognizedCost = 5m,
            GrossMargin = 5m,
            CashCollected = 0m,
            Status = "Draft"
        };

        var financeInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            Reference = project.ProjectCode,
            TotalAmount = 450m
        };

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            InvoiceId = financeInvoice.Id,
            Amount = 300m,
            Status = PaymentStatus.Completed
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.RevenueRecognitions.Add(existingRecognition);
        fixture.Invoices.Add(financeInvoice);
        fixture.Payments.Add(payment);

        var service = fixture.CreateService();

        var result = (await service.GenerateRevenueRecognitionAsync(project.Id)).ToList();

        fixture.RevenueRecognitions.Should().HaveCount(1);
        fixture.RevenueRecognitions[0].RecognizedRevenue.Should().Be(450m);
        fixture.RevenueRecognitions[0].RecognizedCost.Should().Be(120m);
        fixture.RevenueRecognitions[0].GrossMargin.Should().Be(330m);
        fixture.RevenueRecognitions[0].CashCollected.Should().Be(300m);
        fixture.RevenueRecognitions[0].Status.Should().Be("Recognized");
        result.Should().ContainSingle();
    }

    [Fact]
    public async Task GetBillingSummaryReportAsync_ShouldAggregateBillingAndMargin()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-BILL-1",
            Title = "Commercial Project",
            ActualCost = 70m
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.BillingSchedules.Add(new ProjectBillingSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = "Stage 1",
            Amount = 150m,
            BillingDate = DateTime.UtcNow.Date.AddDays(-2),
            IsBillable = true
        });
        fixture.InvoiceRequests.Add(new ProjectInvoiceRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            RequestNumber = "INVREQ-2026-0001",
            RequestedAmount = 120m,
            RequestedAt = DateTime.UtcNow.Date.AddDays(-1),
            Status = "Submitted"
        });
        fixture.InvoiceRequests.Add(new ProjectInvoiceRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            RequestNumber = "INVREQ-2026-0002",
            RequestedAmount = 10m,
            RequestedAt = DateTime.UtcNow.Date,
            Status = "Draft"
        });
        fixture.RevenueRecognitions.Add(new ProjectRevenueRecognition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            RecognitionPeriod = "2026-03",
            RecognizedRevenue = 90m,
            RecognizedCost = 70m,
            GrossMargin = 20m,
            Status = "Posted"
        });

        var service = fixture.CreateService();

        var result = (await service.GetBillingSummaryReportAsync()).Single();

        result.ProjectCode.Should().Be("PRJ-BILL-1");
        result.ScheduledBillingAmount.Should().Be(150m);
        result.InvoiceRequestedAmount.Should().Be(130m);
        result.UnbilledAmount.Should().Be(20m);
        result.ReadyBillingScheduleCount.Should().Be(1);
        result.OverdueBillingScheduleCount.Should().Be(1);
        result.DraftInvoiceRequestCount.Should().Be(1);
        result.SubmittedInvoiceRequestCount.Should().Be(1);
        result.SentToFinanceInvoiceRequestCount.Should().Be(0);
        result.InvoicedInvoiceRequestCount.Should().Be(0);
        result.PaidInvoiceRequestCount.Should().Be(0);
        result.CollectedCashAmount.Should().Be(0m);
        result.BillingCoveragePercent.Should().Be(86.67m);
        result.RecognizedRevenue.Should().Be(90m);
        result.RevenueCoveragePercent.Should().Be(69.23m);
        result.RevenueGapAmount.Should().Be(40m);
        result.ActualCost.Should().Be(70m);
        result.MarginAmount.Should().Be(60m);
        result.MarginPercent.Should().Be(46.15m);
    }

    [Fact]
    public async Task GetInvoiceRequestQueueReportAsync_ShouldPrioritizeAndAnnotateQueueItems()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-BILL-Q",
            Title = "Billing Queue Project",
            CreatedById = userId
        };
        var schedule = new ProjectBillingSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = "Go-live milestone",
            BillingDate = DateTime.UtcNow.Date.AddDays(-5),
            Amount = 250m,
            IsBillable = true
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.BillingSchedules.Add(schedule);
        fixture.InvoiceRequests.Add(new ProjectInvoiceRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            BillingScheduleId = schedule.Id,
            RequestNumber = "INVQ-001",
            RequestedAmount = 250m,
            Currency = "USD",
            Status = "Draft",
            RequestedAt = DateTime.UtcNow.AddDays(-4)
        });
        fixture.InvoiceRequests.Add(new ProjectInvoiceRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            RequestNumber = "INVQ-002",
            RequestedAmount = 100m,
            Currency = "USD",
            Status = "Submitted",
            RequestedAt = DateTime.UtcNow.AddDays(-2),
            SubmittedAt = DateTime.UtcNow.AddDays(-1)
        });

        var service = fixture.CreateService();

        var result = (await service.GetInvoiceRequestQueueReportAsync()).ToList();

        result.Should().HaveCount(2);
        result[0].RequestNumber.Should().Be("INVQ-001");
        result[0].QueueStage.Should().Be("Drafting");
        result[0].BillingScheduleName.Should().Be("Go-live milestone");
        result[0].DaysOutstanding.Should().BeGreaterThanOrEqualTo(3);
        result[1].RequestNumber.Should().Be("INVQ-002");
        result[1].QueueStage.Should().Be("Approval Complete");
    }

    [Fact]
    public async Task GetAssetLinksAsync_ShouldResolveLinkedAssetNamesWithoutQueryableTranslation()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-ASSET-1",
            Title = "Asset Link Project",
            CreatedById = userId
        };
        var maintenanceAssetId = Guid.NewGuid();
        var jobCardId = Guid.NewGuid();

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.AssetLinks.Add(new ProjectAssetLink
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            MaintenanceAssetId = maintenanceAssetId,
            JobCardId = jobCardId,
            LinkType = "Asset",
            Status = "Linked"
        });
        fixture.MaintenanceAssets.Add(new MaintenanceAsset
        {
            Id = maintenanceAssetId,
            TenantId = tenantId,
            Name = "Warehouse Conveyor"
        });
        fixture.JobCards.Add(new JobCard
        {
            Id = jobCardId,
            TenantId = tenantId,
            JobCardNumber = "JC-2026-0042"
        });

        var service = fixture.CreateService();

        var result = (await service.GetAssetLinksAsync(project.Id)).Single();

        result.AssetName.Should().Be("Warehouse Conveyor");
        result.JobCardNumber.Should().Be("JC-2026-0042");
    }

    [Fact]
    public async Task GetTimesheetApprovalQueueAsync_ShouldReturnFilteredApprovalItems()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-TS-1",
            Title = "Timesheet Queue Project",
            CreatedById = userId
        };
        var workItemId = Guid.NewGuid();

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.WorkItems.Add(new ProjectWorkItem
        {
            Id = workItemId,
            TenantId = tenantId,
            ProjectId = project.Id,
            NodeType = ProjectWorkItemNodeTypes.Task,
            Title = "Field setup"
        });
        fixture.TimesheetEntries.Add(new ProjectTimesheetEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            WorkItemId = workItemId,
            UserId = userId,
            EntryDate = DateTime.UtcNow.Date.AddDays(-2),
            Hours = 8m,
            HourlyRate = 25m,
            CostAmount = 200m,
            WorkType = "Implementation",
            Status = "Submitted",
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        });
        fixture.TimesheetEntries.Add(new ProjectTimesheetEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            UserId = userId,
            EntryDate = DateTime.UtcNow.Date,
            Hours = 4m,
            HourlyRate = 25m,
            CostAmount = 100m,
            WorkType = "Support",
            Status = "Draft"
        });

        var service = fixture.CreateService();

        var result = (await service.GetTimesheetApprovalQueueAsync(status: "Submitted")).Single();

        result.ProjectCode.Should().Be("PRJ-TS-1");
        result.WorkItemTitle.Should().Be("Field setup");
        result.QueueStage.Should().Be("Pending Approval");
        result.CostAmount.Should().Be(200m);
    }

    [Fact]
    public async Task GetExpenseApprovalQueueAsync_ShouldReturnFilteredApprovalItems()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-EX-1",
            Title = "Expense Queue Project",
            CreatedById = userId
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.Expenses.Add(new ProjectExpense
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            UserId = userId,
            ExpenseDate = DateTime.UtcNow.Date.AddDays(-4),
            Category = "Travel",
            Currency = "USD",
            Amount = 120m,
            TaxAmount = 12m,
            IsBillable = true,
            Status = "Submitted",
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        });
        fixture.Expenses.Add(new ProjectExpense
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            UserId = userId,
            ExpenseDate = DateTime.UtcNow.Date,
            Category = "Meals",
            Currency = "USD",
            Amount = 40m,
            TaxAmount = 0m,
            Status = "Draft"
        });

        var service = fixture.CreateService();

        var result = (await service.GetExpenseApprovalQueueAsync(status: "Submitted")).Single();

        result.ProjectCode.Should().Be("PRJ-EX-1");
        result.Category.Should().Be("Travel");
        result.QueueStage.Should().Be("Pending Approval");
        result.TotalAmount.Should().Be(132m);
    }

    [Fact]
    public async Task AddQualityCheckpointAsync_ShouldCreateCheckpointForProject()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-QA-1",
            Title = "Quality Project",
            CreatedById = userId
        };
        var workItem = new ProjectWorkItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            NodeType = ProjectWorkItemNodeTypes.Task,
            Title = "Inspect installation"
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.WorkItems.Add(workItem);

        var service = fixture.CreateService();

        var result = await service.AddQualityCheckpointAsync(project.Id, new CreateProjectQualityCheckpointDto
        {
            Title = "QA gate",
            WorkItemId = workItem.Id,
            Status = "Open",
            RequiresQaSignOff = true
        });

        result.Title.Should().Be("QA gate");
        result.WorkItemId.Should().Be(workItem.Id);
        result.RequiresQaSignOff.Should().BeTrue();
        fixture.QualityCheckpoints.Should().ContainSingle(x => x.ProjectId == project.Id && x.Title == "QA gate");
    }

    [Fact]
    public async Task ResolveNonConformanceAsync_ShouldMarkRecordResolved()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-NC-1",
            Title = "Non Conformance Project",
            CreatedById = userId
        };
        var nonConformance = new ProjectNonConformance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Title = "Incorrect labeling",
            Severity = "High",
            Status = "Open"
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.NonConformances.Add(nonConformance);

        var service = fixture.CreateService();

        var result = await service.ResolveNonConformanceAsync(nonConformance.Id, "Corrected and verified");

        result.Status.Should().Be("Resolved");
        result.ResolutionNotes.Should().Be("Corrected and verified");
        fixture.NonConformances.Single().ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetWorkflowApprovalQueueReportAsync_ShouldAggregateWorkflowEntities()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-APP-1",
            Title = "Approval Reporting Project",
            Status = "PendingApproval",
            SubmittedAt = DateTime.UtcNow.AddDays(-3),
            CreatedById = userId
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.BudgetRevisions.Add(new ProjectBudgetRevision
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            RevisionName = "Scope uplift",
            Status = "Approved",
            SubmittedAt = DateTime.UtcNow.AddDays(-5),
            ApprovedAt = DateTime.UtcNow.AddDays(-2)
        });
        fixture.Deliverables.Add(new ProjectDeliverable
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Title = "Deployment package",
            Status = "PendingApproval",
            SubmittedAt = DateTime.UtcNow.AddDays(-1)
        });
        fixture.Closures.Add(new ProjectClosure
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Status = "Rejected",
            SubmittedAt = DateTime.UtcNow.AddDays(-6)
        });

        var service = fixture.CreateService();

        var result = (await service.GetWorkflowApprovalQueueReportAsync()).ToList();

        result.Should().HaveCount(4);
        result[0].EntityType.Should().Be("Project");
        result[0].QueueStage.Should().Be("Awaiting Approval");
        result.Should().ContainSingle(x => x.EntityType == "ProjectDeliverable" && x.ItemTitle == "Deployment package" && x.Status == "PendingApproval");
        result.Should().ContainSingle(x => x.EntityType == "ProjectBudgetRevision" && x.QueueStage == "Approved");
        result.Should().ContainSingle(x => x.EntityType == "ProjectClosure" && x.QueueStage == "Rejected");
    }

    [Fact]
    public async Task GetExternalCollaborationReportAsync_ShouldSummarizeSharedProjects()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-EXT-1",
            Title = "External Collaboration Project",
            Status = ProjectStatuses.InProgress,
            ExternalPortalAccessEnabled = true,
            ExternalCollaborationEnabled = true,
            CreatedById = userId
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.ExternalAccessPolicies.Add(new ProjectExternalAccessPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            BusinessPartnerId = Guid.NewGuid(),
            ArtifactType = "Project",
            AccessLevel = "Collaborate"
        });
        fixture.Documents.Add(new ProjectDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            DocumentName = "Shared Plan",
            Category = "Plan",
            DocumentType = "PortalAttachment",
            FilePath = "/docs/shared-plan.pdf",
            FileType = "application/pdf",
            FileSize = 1024,
            IsExternalVisible = true
        });
        fixture.Deliverables.Add(new ProjectDeliverable
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Title = "Customer sign-off package",
            Status = "PendingExternalSignOff",
            ExternalSubmissionAllowed = true,
            ExternalSignOffRequired = true,
            IsExternalVisible = true
        });
        fixture.Comments.Add(new ProjectComment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            CommentType = "ExternalUpdate",
            Body = "Awaiting customer confirmation",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CreatedBy = "portal.user@example.com",
            CreatedById = Guid.NewGuid()
        });

        var service = fixture.CreateService();

        var result = (await service.GetExternalCollaborationReportAsync()).Single();

        result.ProjectCode.Should().Be("PRJ-EXT-1");
        result.PolicyCount.Should().Be(1);
        result.ExternalVisibleDocumentCount.Should().Be(1);
        result.ExternalVisibleDeliverableCount.Should().Be(1);
        result.PendingExternalSignOffCount.Should().Be(1);
        result.ExternalCommentCount.Should().Be(1);
        result.CollaborationState.Should().Be("ActionRequired");
    }

    [Fact]
    public async Task GetPortfolioPrioritizationReportAsync_ShouldRankProjectsByDeliveryPressure()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-PORT-1",
            Title = "Portfolio Priority Project",
            Status = "AtRisk",
            ApprovedBudget = 120000m,
            ActualCost = 90000m,
            ProgressPercent = 45m,
            CreatedById = userId
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.Risks.Add(new ProjectRisk
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Title = "Vendor delay",
            Status = "Open",
            Exposure = 16
        });
        fixture.Issues.Add(new ProjectIssue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Title = "Blocked test script",
            Status = "Open",
            Severity = "High"
        });
        fixture.Milestones.Add(new ProjectMilestone
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Title = "Go-live readiness",
            TargetDate = DateTime.UtcNow.Date.AddDays(-2),
            Status = "InReview"
        });

        var service = fixture.CreateService();

        var result = (await service.GetPortfolioPrioritizationReportAsync(null, 20)).ToList();

        result.Should().ContainSingle();
        result[0].ProjectId.Should().Be(project.Id);
        result[0].PriorityBand.Should().Be("Stabilize");
        result[0].OpenRiskCount.Should().Be(1);
        result[0].OpenIssueCount.Should().Be(1);
        result[0].OverdueMilestoneCount.Should().Be(1);
        result[0].PriorityScore.Should().BeLessThan(70m);
    }

    [Fact]
    public async Task GetStrategicInitiativeReportAsync_ShouldGroupProjectsByStrategicAlignment()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Portfolios.Add(new ProjectPortfolio
        {
            Id = portfolioId,
            TenantId = tenantId,
            Code = "PORT-1",
            Name = "Transformation"
        });
        fixture.Programs.Add(new ProjectProgram
        {
            Id = programId,
            TenantId = tenantId,
            Code = "PROG-1",
            Name = "ERP Program",
            PortfolioId = portfolioId
        });
        fixture.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-SI-1",
            Title = "Finance Rollout",
            Status = ProjectStatuses.InProgress,
            StrategicAlignment = "Operating resilience",
            PortfolioId = portfolioId,
            ProgramId = programId,
            EstimatedBudget = 100000m,
            ActualCost = 25000m,
            ProgressPercent = 35m
        });
        fixture.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-SI-2",
            Title = "Inventory Rollout",
            Status = "Delayed",
            StrategicAlignment = "Operating resilience",
            PortfolioId = portfolioId,
            ProgramId = programId,
            EstimatedBudget = 80000m,
            ActualCost = 40000m,
            ProgressPercent = 55m
        });

        var service = fixture.CreateService();

        var result = (await service.GetStrategicInitiativeReportAsync(portfolioId, 10)).Single();

        result.Initiative.Should().Be("Operating resilience");
        result.ProjectCount.Should().Be(2);
        result.DelayedProjectCount.Should().Be(1);
        result.TotalEstimatedBudget.Should().Be(180000m);
        result.PortfolioNames.Should().Contain("Transformation");
        result.ProgramNames.Should().Contain("ERP Program");
    }

    [Fact]
    public async Task GetStrategicInitiativeReportAsync_ShouldThrowForExternalUsers()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.CurrentUserProvider.SetupGet(x => x.IsExternalUser).Returns(true);

        var service = fixture.CreateService();

        await FluentActions.Invoking(() => service.GetStrategicInitiativeReportAsync())
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetDependencyWatchReportAsync_ShouldSurfaceOverdueCrossProjectDependencies()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var sourceProjectId = Guid.NewGuid();
        var targetProjectId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Portfolios.Add(new ProjectPortfolio
        {
            Id = portfolioId,
            TenantId = tenantId,
            Code = "PORT-2",
            Name = "Delivery"
        });
        fixture.Programs.Add(new ProjectProgram
        {
            Id = programId,
            TenantId = tenantId,
            Code = "PROG-2",
            Name = "Shared Services",
            PortfolioId = portfolioId
        });
        fixture.Projects.Add(new Project
        {
            Id = sourceProjectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-DEP-1",
            Title = "Source Project",
            Status = ProjectStatuses.InProgress,
            PortfolioId = portfolioId,
            ProgramId = programId
        });
        fixture.Projects.Add(new Project
        {
            Id = targetProjectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-DEP-2",
            Title = "Target Project",
            Status = ProjectStatuses.Planned,
            PortfolioId = portfolioId,
            ProgramId = programId
        });
        fixture.Interdependencies.Add(new ProjectInterdependency
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceProjectId = sourceProjectId,
            TargetProjectId = targetProjectId,
            DependencyType = "Schedule",
            Status = "Open",
            ImpactLevel = "Critical",
            Title = "Environment handoff",
            DueDate = DateTime.UtcNow.Date.AddDays(-1)
        });

        var service = fixture.CreateService();

        var result = (await service.GetDependencyWatchReportAsync(portfolioId, programId, 10)).Single();

        result.SourceProjectCode.Should().Be("PRJ-DEP-1");
        result.TargetProjectCode.Should().Be("PRJ-DEP-2");
        result.CoordinationState.Should().Be("Overdue");
        result.PortfolioName.Should().Be("Delivery");
        result.ProgramName.Should().Be("Shared Services");
    }

    [Fact]
    public async Task LookupProjectsAsync_ShouldThrowWhenUnauthenticated()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.CurrentUserProvider.SetupGet(x => x.IsAuthenticated).Returns(false);

        var service = fixture.CreateService();

        await FluentActions.Invoking(() => service.LookupProjectsAsync())
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task LookupResourcesAsync_ShouldThrowForExternalUsers()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.CurrentUserProvider.SetupGet(x => x.IsExternalUser).Returns(true);

        var service = fixture.CreateService();

        await FluentActions.Invoking(() => service.LookupResourcesAsync())
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SubmitMobileTimesheetAsync_ShouldRequireAssignedWorkItemAndForceSubmittedStatus()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-MOB-1",
            Title = "Mobile Project"
        };
        var workItem = new ProjectWorkItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            NodeType = ProjectWorkItemNodeTypes.Task,
            Title = "Field Inspection",
            Status = "Assigned",
            AssignedToUserId = userId
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles();
        fixture.Projects.Add(project);
        fixture.WorkItems.Add(workItem);

        var service = fixture.CreateService();

        var result = await service.SubmitMobileTimesheetAsync(project.Id, workItem.Id, new CreateProjectTimesheetEntryDto
        {
            UserId = Guid.NewGuid(),
            Hours = 6m,
            HourlyRate = 25m,
            WorkType = "Field",
            Status = "Draft"
        }, userId);

        result.ProjectId.Should().Be(project.Id);
        result.WorkItemId.Should().Be(workItem.Id);
        result.UserId.Should().Be(userId);
        result.Status.Should().Be("Submitted");
        fixture.TimesheetEntries.Should().ContainSingle(x =>
            x.ProjectId == project.Id
            && x.WorkItemId == workItem.Id
            && x.UserId == userId
            && x.Status == "Submitted");
    }

    [Fact]
    public async Task UpdateMobileWorkItemProgressAsync_ShouldRejectUnassignedWorkItems()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-MOB-2",
            Title = "Restricted Mobile Project"
        };
        var workItem = new ProjectWorkItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            NodeType = ProjectWorkItemNodeTypes.Task,
            Title = "Assigned Elsewhere",
            Status = "Assigned",
            AssignedToUserId = otherUserId
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles();
        fixture.Projects.Add(project);
        fixture.WorkItems.Add(workItem);

        var service = fixture.CreateService();

        await FluentActions.Invoking(() => service.UpdateMobileWorkItemProgressAsync(project.Id, workItem.Id, new UpdateProjectWorkItemProgressDto
        {
            Status = "In Progress",
            PercentComplete = 20m
        }, userId)).Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetExternalProjectsAsync_ShouldReturnOnlyProjectsSharedWithLinkedBusinessPartner()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var linkedPartnerId = Guid.NewGuid();
        var otherPartnerId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.BusinessPartnerService
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new BusinessPartnerDetailDto
            {
                Id = linkedPartnerId,
                PartnerCode = "BP-001",
                PartnerName = "Linked Partner"
            });

        fixture.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-EXT-1",
            Title = "Shared Project",
            BusinessPartnerId = linkedPartnerId,
            ExternalPortalAccessEnabled = true,
            ExternalCollaborationEnabled = true
        });
        fixture.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-EXT-2",
            Title = "Internal Project",
            BusinessPartnerId = linkedPartnerId,
            ExternalPortalAccessEnabled = false
        });
        fixture.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-EXT-3",
            Title = "Other Partner Project",
            BusinessPartnerId = otherPartnerId,
            ExternalPortalAccessEnabled = true
        });

        var service = fixture.CreateService();

        var result = (await service.GetExternalProjectsAsync(userId)).ToList();

        result.Should().ContainSingle();
        result[0].ProjectCode.Should().Be("PRJ-EXT-1");
        result[0].ExternalCollaborationEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetExternalProjectByIdAsync_ShouldPopulatePortalActionCounts()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var businessPartnerId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-EXT-COUNT",
            Title = "Portal Action Queue",
            BusinessPartnerId = businessPartnerId,
            ExternalPortalAccessEnabled = true,
            ExternalCollaborationEnabled = true
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.BusinessPartnerService
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new BusinessPartnerDetailDto
            {
                Id = businessPartnerId,
                PartnerCode = "BP-COUNT",
                PartnerName = "Portal Partner"
            });
        fixture.Projects.Add(project);
        fixture.WorkItems.Add(new ProjectWorkItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            NodeType = ProjectWorkItemNodeTypes.Task,
            Title = "Shared Task",
            Status = "Blocked",
            PercentComplete = 15m
        });
        fixture.Deliverables.Add(new ProjectDeliverable
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Title = "Submit Evidence",
            Status = "Draft",
            IsExternalVisible = true,
            ExternalSubmissionAllowed = true,
            ExternalSignOffRequired = false
        });
        fixture.Deliverables.Add(new ProjectDeliverable
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Title = "Accept Installation",
            Status = "PendingExternalSignOff",
            IsExternalVisible = true,
            ExternalSubmissionAllowed = false,
            ExternalSignOffRequired = true
        });

        var service = fixture.CreateService();

        var result = await service.GetExternalProjectByIdAsync(project.Id, userId);

        result.Should().NotBeNull();
        result!.ActionableWorkItemCount.Should().Be(1);
        result.BlockedWorkItemCount.Should().Be(1);
        result.PendingExternalSubmissionCount.Should().Be(1);
        result.PendingExternalSignOffCount.Should().Be(1);
        result.Deliverables.Should().HaveCount(2);
        result.Deliverables.Should().Contain(x => x.Title == "Submit Evidence" && x.CanExternalSubmit);
        result.Deliverables.Should().Contain(x => x.Title == "Accept Installation" && x.CanExternalApprove);
    }

    [Fact]
    public async Task UpdateExternalWorkItemProgressAsync_ShouldLimitChanges_AddComment_AndPublishExternalActivity()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var businessPartnerId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-EXT-4",
            Title = "Portal Collaboration",
            BusinessPartnerId = businessPartnerId,
            ExternalPortalAccessEnabled = true,
            ExternalCollaborationEnabled = true
        };
        var workItem = new ProjectWorkItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            NodeType = ProjectWorkItemNodeTypes.Task,
            Title = "Customer Review",
            Status = "Assigned",
            PercentComplete = 10m
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.BusinessPartnerService
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new BusinessPartnerDetailDto
            {
                Id = businessPartnerId,
                PartnerCode = "BP-EXT",
                PartnerName = "External Partner"
            });
        fixture.Projects.Add(project);
        fixture.WorkItems.Add(workItem);

        var service = fixture.CreateService();

        var result = await service.UpdateExternalWorkItemProgressAsync(project.Id, workItem.Id, new UpdateProjectWorkItemProgressDto
        {
            Status = "In Progress",
            PercentComplete = 75m,
            Notes = "Review is underway."
        }, userId);

        result.Status.Should().Be("In Progress");
        result.PercentComplete.Should().Be(75m);
        project.ProgressPercent.Should().Be(75m);
        fixture.Comments.Should().ContainSingle(x =>
            x.ProjectId == project.Id
            && x.WorkItemId == workItem.Id
            && x.CommentType == "ExternalUpdate"
            && x.Body == "Review is underway.");
        fixture.AppEventBus.Verify(x => x.PublishAsync(It.Is<EntityActivityEvent>(evt =>
            evt.EntityType == "Project"
            && evt.Activity == "ExternalWorkItemProgressUpdated"
            && evt.Audience == "External"
            && evt.Data.ContainsKey("BusinessPartnerId")
            && Guid.Parse(evt.Data["BusinessPartnerId"].ToString()!) == businessPartnerId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitDeliverableAsync_ShouldPersistSubmissionState()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-DEL-1",
            Title = "Deliverable Project"
        };
        var deliverable = new ProjectDeliverable
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Title = "Solution Design",
            Status = "Draft"
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.Deliverables.Add(deliverable);

        var service = fixture.CreateService();

        var result = await service.SubmitDeliverableAsync(deliverable.Id, new SubmitProjectDeliverableDto
        {
            Notes = "Ready for review."
        });

        result.Status.Should().Be("PendingApproval");
        deliverable.SubmittedAt.Should().NotBeNull();
        fixture.Comments.Should().ContainSingle(x => x.CommentType == "DeliverableSubmission" && x.Body == "Ready for review.");
    }

    [Fact]
    public async Task CompareBaselineAsync_ShouldReportChangedWorkItems()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-BL-1",
            Title = "Baseline Project",
            ApprovedBudget = 120m,
            ProgressPercent = 35m
        };
        var taskId = Guid.NewGuid();
        var baseline = new ProjectBaseline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = "Baseline 1",
            SnapshotJson = $$"""
            {
              "ProgressPercent": 20,
              "ApprovedBudget": 100,
              "WorkItems": [
                {
                  "Id": "{{taskId}}",
                  "Title": "Task A",
                  "PlannedStartDate": "2026-03-01T00:00:00Z",
                  "PlannedEndDate": "2026-03-05T00:00:00Z",
                  "PercentComplete": 0
                }
              ],
              "Milestones": []
            }
            """
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.Baselines.Add(baseline);
        fixture.WorkItems.Add(new ProjectWorkItem
        {
            Id = taskId,
            TenantId = tenantId,
            ProjectId = project.Id,
            NodeType = ProjectWorkItemNodeTypes.Task,
            Title = "Task A",
            PlannedStartDate = new DateTime(2026, 3, 2),
            PlannedEndDate = new DateTime(2026, 3, 6),
            PercentComplete = 50m
        });

        var service = fixture.CreateService();

        var result = await service.CompareBaselineAsync(baseline.Id);

        result.ChangedWorkItemCount.Should().Be(1);
        result.BudgetVariance.Should().Be(20m);
        result.CurrentProgressPercent.Should().Be(35m);
    }

    [Fact]
    public async Task ApproveTimesheetEntryAsync_ShouldUpdateProjectCostAndWorkItemEffort()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-TIME-1",
            Title = "Timesheet Project",
            ActualCost = 10m
        };
        var workItem = new ProjectWorkItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            NodeType = ProjectWorkItemNodeTypes.Task,
            Title = "Implementation"
        };
        var entry = new ProjectTimesheetEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            WorkItemId = workItem.Id,
            UserId = userId,
            Hours = 8m,
            HourlyRate = 25m,
            CostAmount = 200m,
            Status = "Submitted"
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.WorkItems.Add(workItem);
        fixture.TimesheetEntries.Add(entry);

        var service = fixture.CreateService();

        var result = await service.ApproveTimesheetEntryAsync(entry.Id);

        result.Status.Should().Be("Approved");
        project.ActualCost.Should().Be(210m);
        workItem.ActualEffortHours.Should().Be(8m);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ShouldRecalculateActualCostForApprovedEntriesRegardlessOfStatusCasing()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-CASE-1",
            Title = "Status Casing Project",
            ActualCost = 0m,
            CreatedById = userId
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.TimesheetEntries.Add(new ProjectTimesheetEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            UserId = userId,
            Hours = 6m,
            HourlyRate = 20m,
            CostAmount = 120m,
            Status = "approved"
        });
        fixture.Expenses.Add(new ProjectExpense
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            UserId = userId,
            Category = "Travel",
            Amount = 80m,
            TaxAmount = 20m,
            ExpenseDate = DateTime.UtcNow.Date,
            Status = "APPROVED",
            Notes = "Site visit"
        });

        var service = fixture.CreateService();

        var result = await service.GetProjectByIdAsync(project.Id);

        result.Should().NotBeNull();
        result!.ActualCost.Should().Be(220m);
        fixture.Projects.Single(x => x.Id == project.Id).ActualCost.Should().Be(220m);
    }

    [Fact]
    public async Task GetMobileSummaryAsync_ShouldExcludeApprovedEntriesRegardlessOfStatusCasing()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-MOB-1",
            Title = "Mobile Summary Project"
        });
        fixture.WorkItems.Add(new ProjectWorkItem
        {
            Id = workItemId,
            TenantId = tenantId,
            ProjectId = projectId,
            Title = "Assigned Task",
            NodeType = ProjectWorkItemNodeTypes.Task,
            AssignedToUserId = userId,
            PlannedEndDate = DateTime.UtcNow.Date.AddDays(1)
        });
        fixture.TimesheetEntries.Add(new ProjectTimesheetEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            WorkItemId = workItemId,
            UserId = userId,
            Hours = 8m,
            HourlyRate = 25m,
            CostAmount = 200m,
            Status = "approved"
        });
        fixture.TimesheetEntries.Add(new ProjectTimesheetEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            WorkItemId = workItemId,
            UserId = userId,
            Hours = 3m,
            HourlyRate = 25m,
            CostAmount = 75m,
            Status = "Submitted"
        });
        fixture.Expenses.Add(new ProjectExpense
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = userId,
            Category = "Travel",
            Amount = 50m,
            TaxAmount = 5m,
            ExpenseDate = DateTime.UtcNow.Date,
            Status = "APPROVED",
            Notes = "Approved expense"
        });
        fixture.Expenses.Add(new ProjectExpense
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = userId,
            Category = "Meals",
            Amount = 40m,
            TaxAmount = 4m,
            ExpenseDate = DateTime.UtcNow.Date,
            Status = "Draft",
            Notes = "Pending expense"
        });

        var service = fixture.CreateService();

        var result = await service.GetMobileSummaryAsync(userId);

        result.PendingHours.Should().Be(3m);
        result.PendingExpenses.Should().Be(44m);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ShouldRejectUserWithoutProjectAccess()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-SEC-1",
            Title = "Restricted Project",
            CreatedById = Guid.NewGuid(),
            ProjectManagerId = Guid.NewGuid(),
            SponsorId = Guid.NewGuid()
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles();
        fixture.Projects.Add(project);

        var service = fixture.CreateService();

        await FluentActions.Invoking(() => service.GetProjectByIdAsync(project.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task HasProjectAccessAsync_ShouldAuthorizeThroughTheLightweightProjectBoundary()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-QS-ACCESS-1",
            Title = "QS access boundary"
        };
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles();
        fixture.Projects.Add(project);
        fixture.Members.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            UserId = userId,
            Role = "QuantitySurveyor",
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });

        var result = await fixture.CreateService().HasProjectAccessAsync(project.Id);

        result.Should().BeTrue();
        fixture.ProjectRepository.Verify(repository => repository.GetByIdAsync(project.Id), Times.Once);
        fixture.ProjectRepository.Verify(repository => repository.GetDetailByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task HasProjectAccessAsync_ShouldReturnFalseForDeniedOrCrossTenantProjects()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deniedProject = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-QS-DENIED",
            Title = "Denied project"
        };
        var crossTenantProject = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ProjectCode = "PRJ-QS-CROSS-TENANT",
            Title = "Cross tenant project"
        };
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles();
        fixture.Projects.AddRange([deniedProject, crossTenantProject]);
        fixture.Members.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            TenantId = crossTenantProject.TenantId,
            ProjectId = crossTenantProject.Id,
            UserId = userId,
            Role = "QuantitySurveyor",
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });

        var service = fixture.CreateService();

        (await service.HasProjectAccessAsync(deniedProject.Id)).Should().BeFalse();
        (await service.HasProjectAccessAsync(crossTenantProject.Id)).Should().BeFalse();
        (await service.HasProjectAccessAsync(Guid.Empty)).Should().BeFalse();
        fixture.ProjectRepository.Verify(repository => repository.GetDetailByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ShouldPopulateUserDisplayNamesForProjectWorkspace()
    {
        var tenantId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var projectManagerId = Guid.NewGuid();
        var sponsorId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var assigneeUserId = Guid.NewGuid();
        var riskOwnerId = Guid.NewGuid();
        var approverUserId = Guid.NewGuid();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-DISPLAY-1",
            Title = "Display Names",
            ProjectManagerId = projectManagerId,
            SponsorId = sponsorId
        };

        var fixture = new ProjectServiceFixture(tenantId, currentUserId);
        fixture.Projects.Add(project);
        fixture.Users.AddRange(
        [
            new ApplicationUser { Id = projectManagerId, TenantId = tenantId, UserName = "pm.user", FirstName = "Project", LastName = "Manager", Email = "pm@example.com" },
            new ApplicationUser { Id = sponsorId, TenantId = tenantId, UserName = "sponsor.user", FirstName = "Program", LastName = "Sponsor", Email = "sponsor@example.com" },
            new ApplicationUser { Id = memberUserId, TenantId = tenantId, UserName = "member.user", FirstName = "Delivery", LastName = "Lead", Email = "member@example.com" },
            new ApplicationUser { Id = assigneeUserId, TenantId = tenantId, UserName = "task.user", FirstName = "Task", LastName = "Owner", Email = "task@example.com" },
            new ApplicationUser { Id = riskOwnerId, TenantId = tenantId, UserName = "risk.user", FirstName = "Risk", LastName = "Owner", Email = "risk@example.com" },
            new ApplicationUser { Id = approverUserId, TenantId = tenantId, UserName = "approver.user", FirstName = "Approval", LastName = "User", Email = "approver@example.com" },
        ]);
        fixture.Members.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            UserId = memberUserId,
            Role = "Team Member",
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });
        fixture.WorkItems.Add(new ProjectWorkItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            NodeType = ProjectWorkItemNodeTypes.Task,
            Title = "Assigned Task",
            Status = "Assigned",
            SortOrder = 0,
            AssignedToUserId = assigneeUserId
        });
        fixture.ResourceAllocations.Add(new ProjectResourceAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            UserId = assigneeUserId,
            AllocationRole = "Engineer",
            AllocationType = "Hours",
            AllocationValue = 40m,
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(5),
            RoutingPolicy = "Balanced",
            Status = "Requested",
            BookingType = "Soft"
        });
        fixture.Risks.Add(new ProjectRisk
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Title = "Named risk",
            Status = "Open",
            Category = "Schedule",
            Probability = 3,
            Impact = 4,
            Exposure = 12,
            OwnerId = riskOwnerId
        });
        fixture.QualityCheckpoints.Add(new ProjectQualityCheckpoint
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Title = "QA sign-off",
            Status = "SignedOff",
            RequiresQaSignOff = true,
            QaOwnerId = assigneeUserId,
            SignedOffById = approverUserId,
            SignedOffAt = DateTime.UtcNow
        });
        fixture.TimesheetEntries.Add(new ProjectTimesheetEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            UserId = assigneeUserId,
            EntryDate = DateTime.UtcNow.Date,
            Hours = 8m,
            HourlyRate = 100m,
            CostAmount = 800m,
            WorkType = "Standard",
            Status = "Approved",
            ApprovedById = approverUserId,
            ApprovedAt = DateTime.UtcNow
        });
        fixture.Decisions.Add(new ProjectDecision
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Title = "Go live",
            DecisionDate = DateTime.UtcNow.Date,
            ApproverId = approverUserId,
            Status = "Approved"
        });
        fixture.Meetings.Add(new ProjectMeetingMinute
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Title = "Weekly sync",
            MeetingDate = DateTime.UtcNow.Date,
            FacilitatorId = assigneeUserId,
            MeetingType = "Status"
        });
        fixture.ActionItems.Add(new ProjectActionItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Title = "Follow up",
            OwnerId = assigneeUserId,
            Status = "Open",
            Priority = "High"
        });

        var service = fixture.CreateService();

        var result = await service.GetProjectByIdAsync(project.Id);

        result.Should().NotBeNull();
        fixture.UserService.Verify(
            x => x.GetUsersByIdsAsync(It.Is<IEnumerable<Guid>>(ids =>
                ids.Contains(projectManagerId)
                && ids.Contains(sponsorId)
                && ids.Contains(memberUserId)
                && ids.Contains(assigneeUserId)
                && ids.Contains(riskOwnerId)
                && ids.Contains(approverUserId))),
            Times.Once);
        fixture.UserService.Verify(x => x.GetUserByIdAsync(It.IsAny<Guid>()), Times.Never);
        result!.ProjectManagerDisplayName.Should().Be("Project Manager (pm.user)");
        result.SponsorDisplayName.Should().Be("Program Sponsor (sponsor.user)");
        result.Members.Single().UserDisplayName.Should().Be("Delivery Lead (member.user)");
        result.WorkItems.Single().AssignedToUserDisplayName.Should().Be("Task Owner (task.user)");
        result.ResourceAllocations.Single().UserDisplayName.Should().Be("Task Owner (task.user)");
        result.Risks.Single().OwnerDisplayName.Should().Be("Risk Owner (risk.user)");
        result.QualityCheckpoints.Single().QaOwnerDisplayName.Should().Be("Task Owner (task.user)");
        result.QualityCheckpoints.Single().SignedOffByDisplayName.Should().Be("Approval User (approver.user)");
        result.TimesheetEntries.Single().UserDisplayName.Should().Be("Task Owner (task.user)");
        result.TimesheetEntries.Single().ApprovedByDisplayName.Should().Be("Approval User (approver.user)");
        result.Decisions.Single().ApproverDisplayName.Should().Be("Approval User (approver.user)");
        result.Meetings.Single().FacilitatorDisplayName.Should().Be("Task Owner (task.user)");
        result.ActionItems.Single().OwnerDisplayName.Should().Be("Task Owner (task.user)");
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldSeedOwnerMembership()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SettingsRepository
            .Setup(x => x.GetOrCreateDefaultAsync(tenantId, userId))
            .ReturnsAsync(new ProjectManagementSettings
            {
                TenantId = tenantId,
                ProjectNumberFormat = "PRJ-{YYYY}-{####}",
                RequireSponsor = false,
                DefaultApprovalRequired = true
            });

        var service = fixture.CreateService();

        var result = await service.CreateProjectAsync(new CreateProjectDto
        {
            Title = "Membership Seed"
        });

        fixture.Members.Should().ContainSingle(x =>
            x.ProjectId == result.Id
            && x.UserId == userId
            && x.IsActive
            && x.Role == "Owner");
    }

    [Fact]
    public async Task GetDashboardAsync_ShouldFilterToAccessibleProjectsForRestrictedUser()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accessibleProject = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-ACC-1",
            Title = "Accessible Project",
            Status = ProjectStatuses.InProgress,
            CreatedById = userId
        };
        var restrictedProject = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-ACC-2",
            Title = "Restricted Project",
            Status = ProjectStatuses.InProgress,
            CreatedById = Guid.NewGuid()
        };

        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.SetRoles();
        fixture.Projects.Add(accessibleProject);
        fixture.Projects.Add(restrictedProject);
        fixture.Risks.Add(new ProjectRisk
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = accessibleProject.Id,
            Title = "Accessible Risk",
            Status = "Open",
            Exposure = 16
        });
        fixture.Risks.Add(new ProjectRisk
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = restrictedProject.Id,
            Title = "Restricted Risk",
            Status = "Open",
            Exposure = 16
        });

        var service = fixture.CreateService();

        var result = await service.GetDashboardAsync();

        result.TotalProjects.Should().Be(1);
        result.ActiveProjects.Should().Be(1);
        result.AtRiskProjects.Should().ContainSingle(x => x.Id == accessibleProject.Id);
    }

    [Fact]
    public async Task AddProjectBoqItemAsync_ShouldSnapshotEffectiveTenantClassifications()
    {
        var tenantId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-QS-001",
            Title = "Controlled BoQ"
        };
        var package = new ProjectPackage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Code = "WP-001",
            Name = "Substructure",
            Currency = "GHS"
        };
        var section = CreateCatalogEntry(tenantId, ProjectCatalogDefaults.QuantitySurveySections, "A", "Preliminaries");
        var trade = CreateCatalogEntry(tenantId, ProjectCatalogDefaults.QuantitySurveyTrades, "CONC", "Concrete work");
        var costCode = CreateCatalogEntry(tenantId, ProjectCatalogDefaults.QuantitySurveyCostCodes, "CC-100", "Structural works");
        var measurementCode = CreateCatalogEntry(tenantId, ProjectCatalogDefaults.QuantitySurveyMeasurementCodes, "E20", "In-situ concrete");
        measurementCode.StandardCode = nameof(QuantitySurveyBoqStandard.Cesmm4);
        measurementCode.MeasurementRule = "Measure net volume in cubic metres.";
        measurementCode.DefaultUnitOfMeasure = "m3";

        var fixture = new ProjectServiceFixture(tenantId, Guid.NewGuid());
        fixture.Projects.Add(project);
        fixture.ProjectPackages.Add(package);
        fixture.ProjectCatalogEntries.AddRange([section, trade, costCode, measurementCode]);

        var result = await fixture.CreateService().AddProjectBoqItemAsync(project.Id, new CreateProjectBoqItemDto
        {
            ProjectPackageId = package.Id,
            SectionCatalogEntryId = section.Id,
            TradeCatalogEntryId = trade.Id,
            CostCodeCatalogEntryId = costCode.Id,
            MeasurementCodeCatalogEntryId = measurementCode.Id,
            Description = "25 MPa concrete",
            Quantity = 12.5m,
            UnitRate = 900m
        });

        result.SectionCode.Should().Be("A");
        result.TradeCode.Should().Be("CONC");
        result.CostCode.Should().Be("CC-100");
        result.MeasurementStandard.Should().Be(nameof(QuantitySurveyBoqStandard.Cesmm4));
        result.MeasurementCode.Should().Be("E20");
        result.MeasurementRule.Should().Be("Measure net volume in cubic metres.");
        result.UnitOfMeasure.Should().Be("m3");
        result.ItemCode.Should().Be("E20");
        result.BudgetAmount.Should().Be(11250m);
    }

    [Fact]
    public async Task AddProjectBoqItemAsync_ShouldRejectClassificationOwnedByAnotherTenant()
    {
        var tenantId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-QS-002",
            Title = "Tenant boundary"
        };
        var package = new ProjectPackage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = "Works",
            Currency = "GHS"
        };
        var foreignTrade = CreateCatalogEntry(Guid.NewGuid(), ProjectCatalogDefaults.QuantitySurveyTrades, "ELEC", "Electrical");
        var fixture = new ProjectServiceFixture(tenantId, Guid.NewGuid());
        fixture.Projects.Add(project);
        fixture.ProjectPackages.Add(package);
        fixture.ProjectCatalogEntries.Add(foreignTrade);

        var action = () => fixture.CreateService().AddProjectBoqItemAsync(project.Id, new CreateProjectBoqItemDto
        {
            ProjectPackageId = package.Id,
            TradeCatalogEntryId = foreignTrade.Id,
            Description = "Cable installation",
            Quantity = 1m
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*could not be found for this tenant*");
        fixture.ProjectBoqItems.Should().BeEmpty();
    }

    [Fact]
    public async Task AddProjectBoqItemAsync_ShouldEnforceEffectiveBoqStandardsDecision()
    {
        var tenantId = Guid.NewGuid();
        var projectTypeId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectTypeId = projectTypeId,
            ProjectCode = "PRJ-QS-003",
            Title = "Policy enforcement"
        };
        var package = new ProjectPackage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = "Works",
            Currency = "GHS"
        };
        var profile = new QuantitySurveyConfigurationProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProfileCode = "QS-POLICY",
            Name = "QS Policy",
            LifecycleStatus = QuantitySurveyConfigurationProfileStatus.Published,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1),
            IsDefault = true
        };
        var decision = new QuantitySurveyConfigurationDecision
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProfileId = profile.Id,
            DecisionKey = "QS-DEC-002",
            OwnerGroup = "Quantity Survey",
            Status = QuantitySurveyConfigurationDecisionStatus.Approved,
            ApprovalStatus = QuantitySurveyConfigurationApprovalStatus.Approved,
            ValueJson = $$"""
            {
              "allowedStandards": ["Cesmm4"],
              "projectTypeIds": ["{{projectTypeId}}"],
              "requireTrade": true,
              "requireCostCode": true
            }
            """,
            DecisionDate = DateTime.UtcNow
        };
        var fixture = new ProjectServiceFixture(tenantId, Guid.NewGuid());
        fixture.Projects.Add(project);
        fixture.ProjectPackages.Add(package);
        fixture.QuantitySurveyProfiles.Add(profile);
        fixture.QuantitySurveyDecisions.Add(decision);

        var action = () => fixture.CreateService().AddProjectBoqItemAsync(project.Id, new CreateProjectBoqItemDto
        {
            ProjectPackageId = package.Id,
            Description = "Unclassified work",
            Quantity = 1m
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires a controlled trade*");
        fixture.ProjectBoqItems.Should().BeEmpty();
    }

    [Fact]
    public async Task BoqVersioning_ShouldPreserveLineageAndCompareQuantityChanges()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-QS-VERSION",
            Title = "BoQ version comparison",
            CreatedById = userId
        };
        var package = new ProjectPackage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Code = "WP-001",
            Name = "Substructure",
            Currency = "GHS"
        };
        var lineKey = Guid.NewGuid();
        var line = new ProjectBoqItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            ProjectPackageId = package.Id,
            VersionLineKey = lineKey,
            LineNumber = "1.1",
            ItemType = ProjectBoqItemTypes.Item,
            Description = "Excavate foundation trenches",
            Quantity = 10m,
            UnitOfMeasure = "m3",
            UnitRate = 25m,
            Currency = "GHS"
        };
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.ProjectPackages.Add(package);
        fixture.ProjectBoqItems.Add(line);
        var service = fixture.CreateService();

        var initialWorkspace = await service.GetProjectBoqVersionWorkspaceAsync(project.Id);
        var original = await service.CreateProjectBoqVersionAsync(project.Id, new CreateProjectBoqVersionDto
        {
            VersionType = QuantitySurveyBoqVersionType.Original,
            ExpectedWorkingSetHash = initialWorkspace.WorkingSetHash,
            ChangeSummary = "Original measured quantities"
        }, "qs-version-test-original");
        var publishedOriginal = fixture.ProjectBoqVersions.Single(item => item.Id == original.Id);
        publishedOriginal.VersionType = QuantitySurveyBoqVersionType.Approved;
        publishedOriginal.Status = ProjectBoqVersionStatuses.Approved;
        publishedOriginal.ApprovalStatus = ProjectBoqVersionStatuses.Approved;
        publishedOriginal.PublishedAt = DateTime.UtcNow;
        publishedOriginal.PublishedById = userId;

        line.Quantity = 14m;
        var revisedWorkspace = await service.GetProjectBoqVersionWorkspaceAsync(project.Id);
        var revised = await service.CreateProjectBoqVersionAsync(project.Id, new CreateProjectBoqVersionDto
        {
            VersionType = QuantitySurveyBoqVersionType.Revised,
            SourceVersionId = original.Id,
            ExpectedWorkingSetHash = revisedWorkspace.WorkingSetHash,
            ChangeSummary = "Revised measured foundation quantity"
        }, "qs-version-test-revised");
        var comparison = await service.CompareProjectBoqVersionsAsync(project.Id, original.Id, revised.Id);

        fixture.ProjectBoqVersions.Should().HaveCount(2);
        fixture.ProjectBoqVersionLines.Should().HaveCount(2);
        fixture.ProjectBoqVersionLines.Should().OnlyContain(item => item.LineKey == lineKey);
        revised.SourceVersionId.Should().Be(original.Id);
        comparison.ChangedLineCount.Should().Be(1);
        comparison.AddedLineCount.Should().Be(0);
        comparison.RemovedLineCount.Should().Be(0);
        comparison.Lines.Should().ContainSingle(item =>
            item.LineKey == lineKey
            && item.QuantityDelta == 4m
            && item.AmountDelta == 100m
            && item.ChangedFields.Contains("Quantity"));
    }

    [Fact]
    public async Task SubmitBoqVersionAsync_ShouldUseConfiguredWorkflowAndCreateImmutableApprovedPublication()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var workflowDefinitionId = Guid.NewGuid();
        var workflowInstanceId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProjectCode = "PRJ-QS-APPROVAL",
            Title = "BoQ approval", CreatedById = userId
        };
        var package = new ProjectPackage
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = project.Id,
            Code = "WP-APP", Name = "Approved works", Currency = "GHS"
        };
        var line = new ProjectBoqItem
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = project.Id,
            ProjectPackageId = package.Id, VersionLineKey = Guid.NewGuid(),
            LineNumber = "1", ItemType = ProjectBoqItemTypes.Item,
            Description = "Controlled approved work", Quantity = 2m,
            UnitOfMeasure = "item", UnitRate = 50m, Currency = "GHS"
        };
        var profile = new QuantitySurveyConfigurationProfile
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProfileCode = "QS-APPROVAL",
            LifecycleStatus = QuantitySurveyConfigurationProfileStatus.Published,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1), IsDefault = true
        };
        var decision = new QuantitySurveyConfigurationDecision
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProfileId = profile.Id,
            DecisionKey = "QS-DEC-003",
            Status = QuantitySurveyConfigurationDecisionStatus.Approved,
            ApprovalStatus = QuantitySurveyConfigurationApprovalStatus.Approved,
            DecisionDate = DateTime.UtcNow,
            ValueJson = $$"""
            {
              "effectiveFrom": "2026-08-01T00:00:00Z",
              "effectiveTo": null,
              "requiredVersionTypes": ["original", "approved", "revised"],
              "boqWorkflowDefinitionId": "{{workflowDefinitionId}}",
              "estimateWorkflowDefinitionId": "{{Guid.NewGuid()}}",
              "approvedVersionsImmutable": true,
              "requireWorkflowBeforeUse": true,
              "requireLineLevelComparison": true
            }
            """
        };
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.ProjectPackages.Add(package);
        fixture.ProjectBoqItems.Add(line);
        fixture.QuantitySurveyProfiles.Add(profile);
        fixture.QuantitySurveyDecisions.Add(decision);
        fixture.WorkflowIntegrationService
            .Setup(service => service.SubmitAsync(
                QuantitySurveyWorkflowBindingRegistry.Boq,
                It.IsAny<Guid>(),
                workflowDefinitionId))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult
                {
                    Success = true,
                    Status = WorkflowInstanceStatus.Completed,
                    WorkflowInstanceId = workflowInstanceId
                },
                WorkflowOutcome.Approved));
        var service = fixture.CreateService();
        var workspace = await service.GetProjectBoqVersionWorkspaceAsync(project.Id);
        var candidate = await service.CreateProjectBoqVersionAsync(project.Id, new CreateProjectBoqVersionDto
        {
            VersionType = QuantitySurveyBoqVersionType.Original,
            ExpectedWorkingSetHash = workspace.WorkingSetHash,
            ChangeSummary = "Original BoQ for governed approval"
        }, "qs-approval-create");

        var submitted = await service.SubmitProjectBoqVersionAsync(
            project.Id, candidate.Id, userId, "qs-approval-submit");

        submitted.Status.Should().Be(ProjectBoqVersionStatuses.Approved);
        fixture.ProjectBoqVersions.Should().HaveCount(2);
        var publication = fixture.ProjectBoqVersions.Single(item => item.VersionType == QuantitySurveyBoqVersionType.Approved);
        publication.SourceVersionId.Should().Be(candidate.Id);
        publication.Status.Should().Be(ProjectBoqVersionStatuses.Approved);
        publication.PublishedAt.Should().NotBeNull();
        publication.PublishedById.Should().Be(userId);
        publication.WorkflowDefinitionId.Should().Be(workflowDefinitionId);
        publication.WorkflowInstanceId.Should().Be(workflowInstanceId);
        fixture.ProjectBoqVersionLines.Should().HaveCount(2);
        fixture.ProjectBoqVersionLines.Where(item => item.ProjectBoqVersionId == publication.Id)
            .Should().ContainSingle().Which.LineKey.Should().Be(line.VersionLineKey);
    }

    [Fact]
    public async Task CreateBoqVersionAsync_ShouldRejectStaleWorkingSetAndDirectApprovedSnapshot()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-QS-GUARD",
            Title = "BoQ version guards",
            CreatedById = userId
        };
        var package = new ProjectPackage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = "Works",
            Currency = "GHS"
        };
        var line = new ProjectBoqItem
        {
            TenantId = tenantId,
            ProjectId = project.Id,
            ProjectPackageId = package.Id,
            Description = "Controlled work",
            Quantity = 1m,
            UnitRate = 10m,
            Currency = "GHS"
        };
        var fixture = new ProjectServiceFixture(tenantId, userId);
        fixture.Projects.Add(project);
        fixture.ProjectPackages.Add(package);
        fixture.ProjectBoqItems.Add(line);
        var service = fixture.CreateService();
        var workspace = await service.GetProjectBoqVersionWorkspaceAsync(project.Id);

        var approvedAttempt = () => service.CreateProjectBoqVersionAsync(project.Id, new CreateProjectBoqVersionDto
        {
            VersionType = QuantitySurveyBoqVersionType.Approved,
            ExpectedWorkingSetHash = workspace.WorkingSetHash,
            ChangeSummary = "Attempted direct approval"
        }, "qs-version-approved-guard");
        await approvedAttempt.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only be created by the configured BoQ approval workflow*");

        line.Quantity = 2m;
        var staleAttempt = () => service.CreateProjectBoqVersionAsync(project.Id, new CreateProjectBoqVersionDto
        {
            VersionType = QuantitySurveyBoqVersionType.Original,
            ExpectedWorkingSetHash = workspace.WorkingSetHash,
            ChangeSummary = "Stale snapshot attempt"
        }, "qs-version-stale-guard");
        await staleAttempt.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*working BoQ changed*");
        fixture.ProjectBoqVersions.Should().BeEmpty();
    }

    private static ProjectCatalogEntry CreateCatalogEntry(Guid tenantId, string catalogType, string code, string name)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CatalogType = catalogType,
            Code = code,
            Name = name,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1)
        };

    [Fact]
    public async Task AddMemberAsync_ShouldRequireAnActiveUserInTheCurrentTenant()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-MEMBER-001",
            Title = "Member validation"
        };
        var fixture = new ProjectServiceFixture(tenantId, actorId);
        fixture.Projects.Add(project);
        var foreignUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserName = "foreign.user",
            IsActive = true
        };
        fixture.Users.Add(foreignUser);

        var action = () => fixture.CreateService().AddMemberAsync(project.Id,
            new AddProjectMemberDto { UserId = foreignUser.Id, Role = "QuantitySurveyor" });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not an active member of the current tenant*");
        fixture.Members.Should().BeEmpty();
    }

    [Fact]
    public async Task AddMemberAsync_ShouldPersistAControlledCurrentTenantUser()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-MEMBER-002",
            Title = "Member validation"
        };
        var selectedUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = "qs.user",
            IsActive = true
        };
        var fixture = new ProjectServiceFixture(tenantId, actorId);
        fixture.Projects.Add(project);
        fixture.Users.Add(selectedUser);

        var result = await fixture.CreateService().AddMemberAsync(project.Id,
            new AddProjectMemberDto { UserId = selectedUser.Id, Role = "QuantitySurveyor" });

        result.UserId.Should().Be(selectedUser.Id);
        fixture.Members.Should().ContainSingle(item => item.ProjectId == project.Id &&
            item.UserId == selectedUser.Id && item.TenantId == tenantId);
    }

    private sealed class ProjectServiceFixture
    {
        public List<Project> Projects { get; } = new();
        public List<ProjectMember> Members { get; } = new();
        public List<ProjectPortfolio> Portfolios { get; } = new();
        public List<ProjectProgram> Programs { get; } = new();
        public List<ProjectDevelopmentProfile> DevelopmentProfiles { get; } = new();
        public List<ProjectPhase> ProjectPhases { get; } = new();
        public List<ProjectWorkItem> WorkItems { get; } = new();
        public List<ProjectMilestone> Milestones { get; } = new();
        public List<ProjectInitiationVersion> InitiationVersions { get; } = new();
        public List<ProjectResourceAllocation> ResourceAllocations { get; } = new();
        public List<ProjectRisk> Risks { get; } = new();
        public List<ProjectIssue> Issues { get; } = new();
        public List<ProjectQualityCheckpoint> QualityCheckpoints { get; } = new();
        public List<ProjectNonConformance> NonConformances { get; } = new();
        public List<ProjectBillingSchedule> BillingSchedules { get; } = new();
        public List<ProjectInvoiceRequest> InvoiceRequests { get; } = new();
        public List<ProjectDeliverable> Deliverables { get; } = new();
        public List<ProjectDocument> Documents { get; } = new();
        public List<ProjectTaskDependency> TaskDependencies { get; } = new();
        public List<ProjectInterdependency> Interdependencies { get; } = new();
        public List<ProjectBaseline> Baselines { get; } = new();
        public List<ProjectTimesheetEntry> TimesheetEntries { get; } = new();
        public List<ProjectExpense> Expenses { get; } = new();
        public List<ProjectRevenueRecognition> RevenueRecognitions { get; } = new();
        public List<ProjectBudgetRevision> BudgetRevisions { get; } = new();
        public List<ProjectForecastVersion> ForecastVersions { get; } = new();
        public List<ProjectAssetLink> AssetLinks { get; } = new();
        public List<ProjectExternalAccessPolicy> ExternalAccessPolicies { get; } = new();
        public List<ProjectDecision> Decisions { get; } = new();
        public List<ProjectMeetingMinute> Meetings { get; } = new();
        public List<ProjectActionItem> ActionItems { get; } = new();
        public List<ProjectLessonLearned> LessonsLearned { get; } = new();
        public List<ProjectClosure> Closures { get; } = new();
        public List<ProjectUnit> ProjectUnits { get; } = new();
        public List<PurchaseRequisition> PurchaseRequisitions { get; } = new();
        public List<PurchaseOrder> PurchaseOrders { get; } = new();
        public List<PurchaseOrderItem> PurchaseOrderItems { get; } = new();
        public List<PurchaseOrderReceipt> PurchaseOrderReceipts { get; } = new();
        public List<PurchaseOrderReceiptItem> PurchaseOrderReceiptItems { get; } = new();
        public List<PurchaseReturn> PurchaseReturns { get; } = new();
        public List<InventoryRequisition> InventoryRequisitions { get; } = new();
        public List<StockMovement> StockMovements { get; } = new();
        public List<InventoryItem> InventoryItems { get; } = new();
        public List<MaintenanceAsset> MaintenanceAssets { get; } = new();
        public List<WorkOrder> WorkOrders { get; } = new();
        public List<MaintenanceType> MaintenanceTypes { get; } = new();
        public List<PriorityLevel> PriorityLevels { get; } = new();
        public List<WorkOrderType> WorkOrderTypes { get; } = new();
        public List<CompanyAsset> CompanyAssets { get; } = new();
        public List<JobCard> JobCards { get; } = new();
        public List<Employee> Employees { get; } = new();
        public List<Skill> Skills { get; } = new();
        public List<EmployeeSkill> EmployeeSkills { get; } = new();
        public List<LeaveRequest> LeaveRequests { get; } = new();
        public List<Invoice> Invoices { get; } = new();
        public List<Payment> Payments { get; } = new();
        public List<ProjectComment> Comments { get; } = new();
        public List<ProjectMaterialCostEntry> MaterialCostEntries { get; } = new();
        public List<ProjectDeliverableExternalReview> DeliverableExternalReviews { get; } = new();
        public List<Warehouse> Warehouses { get; } = new();
        public List<WarehouseLocation> WarehouseLocations { get; } = new();
        public List<Tender> Tenders { get; } = new();
        public List<BusinessPartner> BusinessPartners { get; } = new();
        public List<SalesAgreement> SalesAgreements { get; } = new();
        public List<SalesOrder> SalesOrders { get; } = new();
        public List<ApplicationUser> Users { get; } = new();
        public List<ProjectPackage> ProjectPackages { get; } = new();
        public List<ProjectBoqItem> ProjectBoqItems { get; } = new();
        public List<ProjectCatalogEntry> ProjectCatalogEntries { get; } = new();
        public List<QuantitySurveyConfigurationProfile> QuantitySurveyProfiles { get; } = new();
        public List<QuantitySurveyConfigurationDecision> QuantitySurveyDecisions { get; } = new();
        public List<ProjectBoqVersion> ProjectBoqVersions { get; } = new();
        public List<ProjectBoqVersionLine> ProjectBoqVersionLines { get; } = new();

        public Mock<IProjectRepository> ProjectRepository { get; } = new();
        public Mock<IProjectManagementSettingsRepository> SettingsRepository { get; } = new();
        public Mock<IProjectTemplateRepository> TemplateRepository { get; } = new();
        public Mock<IProjectTypeRepository> ProjectTypeRepository { get; } = new();
        public Mock<IProjectPortfolioRepository> ProjectPortfolioRepository { get; } = new();
        public Mock<IProjectProgramRepository> ProjectProgramRepository { get; } = new();
        public Mock<IContractService> ContractService { get; } = new();
        public Mock<IBusinessPartnerService> BusinessPartnerService { get; } = new();
        public Mock<IWorkflowIntegrationService> WorkflowIntegrationService { get; } = new();
        public Mock<IWorkflowStatusAdapterRegistry> WorkflowStatusAdapterRegistry { get; } = new();
        public Mock<IWorkflowService> WorkflowService { get; } = new();
        public Mock<IUserService> UserService { get; } = new();
        public Mock<IJobCardService> JobCardService { get; } = new();
        public Mock<IWorkOrderService> WorkOrderService { get; } = new();
        public Mock<ISalesAgreementService> SalesAgreementService { get; } = new();
        public Mock<ISalesOrderService> SalesOrderService { get; } = new();
        public Mock<IEstateManagedAssetService> EstateManagedAssetService { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<ITenantSettingsService> TenantSettingsService { get; } = new();
        public Mock<ICurrentUserProvider> CurrentUserProvider { get; } = new();
        public Mock<IAppEventBus> AppEventBus { get; } = new();

        private readonly HashSet<string> _roles = new(StringComparer.OrdinalIgnoreCase)
        {
            Constants.Roles.SuperAdmin
        };
        private readonly Mock<IGenericRepository<Project>> _projectEntityRepository;
        private readonly Mock<IGenericRepository<ProjectMember>> _memberRepository;
        private readonly Mock<IGenericRepository<ProjectDevelopmentProfile>> _developmentProfileRepository;
        private readonly Mock<IGenericRepository<ProjectPhase>> _projectPhaseRepository;
        private readonly Mock<IGenericRepository<ProjectWorkItem>> _workItemRepository;
        private readonly Mock<IGenericRepository<ProjectMilestone>> _milestoneRepository;
        private readonly Mock<IGenericRepository<ProjectInitiationVersion>> _initiationVersionRepository;
        private readonly Mock<IGenericRepository<ProjectResourceAllocation>> _resourceAllocationRepository;
        private readonly Mock<IGenericRepository<ProjectRisk>> _riskRepository;
        private readonly Mock<IGenericRepository<ProjectIssue>> _issueRepository;
        private readonly Mock<IGenericRepository<ProjectQualityCheckpoint>> _qualityCheckpointRepository;
        private readonly Mock<IGenericRepository<ProjectNonConformance>> _nonConformanceRepository;
        private readonly Mock<IGenericRepository<ProjectBillingSchedule>> _billingScheduleRepository;
        private readonly Mock<IGenericRepository<ProjectInvoiceRequest>> _invoiceRequestRepository;
        private readonly Mock<IGenericRepository<ProjectDeliverable>> _deliverableRepository;
        private readonly Mock<IGenericRepository<ProjectDocument>> _documentRepository;
        private readonly Mock<IGenericRepository<ProjectTaskDependency>> _taskDependencyRepository;
        private readonly Mock<IGenericRepository<ProjectInterdependency>> _interdependencyRepository;
        private readonly Mock<IGenericRepository<ProjectBaseline>> _baselineRepository;
        private readonly Mock<IGenericRepository<ProjectTimesheetEntry>> _timesheetRepository;
        private readonly Mock<IGenericRepository<ProjectExpense>> _expenseRepository;
        private readonly Mock<IGenericRepository<ProjectRevenueRecognition>> _revenueRecognitionRepository;
        private readonly Mock<IGenericRepository<ProjectBudgetRevision>> _budgetRevisionRepository;
        private readonly Mock<IGenericRepository<ProjectForecastVersion>> _forecastVersionRepository;
        private readonly Mock<IGenericRepository<ProjectAssetLink>> _assetLinkRepository;
        private readonly Mock<IGenericRepository<ProjectExternalAccessPolicy>> _externalAccessPolicyRepository;
        private readonly Mock<IGenericRepository<ProjectDecision>> _decisionRepository;
        private readonly Mock<IGenericRepository<ProjectMeetingMinute>> _meetingRepository;
        private readonly Mock<IGenericRepository<ProjectActionItem>> _actionItemRepository;
        private readonly Mock<IGenericRepository<ProjectLessonLearned>> _lessonLearnedRepository;
        private readonly Mock<IGenericRepository<ProjectClosure>> _closureRepository;
        private readonly Mock<IGenericRepository<ProjectUnit>> _projectUnitRepository;
        private readonly Mock<IGenericRepository<PurchaseRequisition>> _purchaseRequisitionRepository;
        private readonly Mock<IGenericRepository<PurchaseOrder>> _purchaseOrderRepository;
        private readonly Mock<IGenericRepository<PurchaseOrderItem>> _purchaseOrderItemRepository;
        private readonly Mock<IGenericRepository<PurchaseOrderReceipt>> _purchaseOrderReceiptRepository;
        private readonly Mock<IGenericRepository<PurchaseOrderReceiptItem>> _purchaseOrderReceiptItemRepository;
        private readonly Mock<IGenericRepository<PurchaseReturn>> _purchaseReturnRepository;
        private readonly Mock<IGenericRepository<InventoryRequisition>> _inventoryRequisitionRepository;
        private readonly Mock<IGenericRepository<StockMovement>> _stockMovementRepository;
        private readonly Mock<IGenericRepository<InventoryItem>> _inventoryItemRepository;
        private readonly Mock<IGenericRepository<MaintenanceAsset>> _maintenanceAssetRepository;
        private readonly Mock<IGenericRepository<WorkOrder>> _workOrderRepository;
        private readonly Mock<IGenericRepository<MaintenanceType>> _maintenanceTypeRepository;
        private readonly Mock<IGenericRepository<PriorityLevel>> _priorityLevelRepository;
        private readonly Mock<IGenericRepository<WorkOrderType>> _workOrderTypeRepository;
        private readonly Mock<IGenericRepository<CompanyAsset>> _companyAssetRepository;
        private readonly Mock<IGenericRepository<JobCard>> _jobCardRepository;
        private readonly Mock<IGenericRepository<Employee>> _employeeRepository;
        private readonly Mock<IGenericRepository<Skill>> _skillRepository;
        private readonly Mock<IGenericRepository<EmployeeSkill>> _employeeSkillRepository;
        private readonly Mock<IGenericRepository<LeaveRequest>> _leaveRequestRepository;
        private readonly Mock<IGenericRepository<Invoice>> _invoiceRepository;
        private readonly Mock<IGenericRepository<Payment>> _paymentRepository;
        private readonly Mock<IGenericRepository<ProjectComment>> _commentRepository;
        private readonly Mock<IGenericRepository<ProjectMaterialCostEntry>> _materialCostEntryRepository;
        private readonly Mock<IGenericRepository<ProjectDeliverableExternalReview>> _deliverableExternalReviewRepository;
        private readonly Mock<IGenericRepository<Warehouse>> _warehouseRepository;
        private readonly Mock<IGenericRepository<WarehouseLocation>> _warehouseLocationRepository;
        private readonly Mock<IGenericRepository<Tender>> _tenderRepository;
        private readonly Mock<IGenericRepository<BusinessPartner>> _businessPartnerRepository;
        private readonly Mock<IGenericRepository<SalesAgreement>> _salesAgreementRepository;
        private readonly Mock<IGenericRepository<SalesOrder>> _salesOrderRepository;
        private readonly Mock<IGenericRepository<ProjectPackage>> _projectPackageRepository;
        private readonly Mock<IGenericRepository<ProjectBoqItem>> _projectBoqItemRepository;
        private readonly Mock<IGenericRepository<ProjectCatalogEntry>> _projectCatalogEntryRepository;
        private readonly Mock<IGenericRepository<QuantitySurveyConfigurationProfile>> _quantitySurveyProfileRepository;
        private readonly Mock<IGenericRepository<QuantitySurveyConfigurationDecision>> _quantitySurveyDecisionRepository;
        private readonly Mock<IGenericRepository<ProjectBoqVersion>> _projectBoqVersionRepository;
        private readonly Mock<IGenericRepository<ProjectBoqVersionLine>> _projectBoqVersionLineRepository;

        public ProjectServiceFixture(Guid tenantId, Guid userId)
        {
            _projectEntityRepository = CreateRepository(Projects);
            _memberRepository = CreateRepository(Members);
            _developmentProfileRepository = CreateRepository(DevelopmentProfiles);
            _projectPhaseRepository = CreateRepository(ProjectPhases);
            _workItemRepository = CreateRepository(WorkItems);
            _milestoneRepository = CreateRepository(Milestones);
            _initiationVersionRepository = CreateRepository(InitiationVersions);
            _resourceAllocationRepository = CreateRepository(ResourceAllocations);
            _riskRepository = CreateRepository(Risks);
            _issueRepository = CreateRepository(Issues);
            _qualityCheckpointRepository = CreateRepository(QualityCheckpoints);
            _nonConformanceRepository = CreateRepository(NonConformances);
            _billingScheduleRepository = CreateRepository(BillingSchedules);
            _invoiceRequestRepository = CreateRepository(InvoiceRequests);
            _deliverableRepository = CreateRepository(Deliverables);
            _documentRepository = CreateRepository(Documents);
            _taskDependencyRepository = CreateRepository(TaskDependencies);
            _interdependencyRepository = CreateRepository(Interdependencies);
            _baselineRepository = CreateRepository(Baselines);
            _timesheetRepository = CreateRepository(TimesheetEntries);
            _expenseRepository = CreateRepository(Expenses);
            _revenueRecognitionRepository = CreateRepository(RevenueRecognitions);
            _budgetRevisionRepository = CreateRepository(BudgetRevisions);
            _forecastVersionRepository = CreateRepository(ForecastVersions);
            _assetLinkRepository = CreateRepository(AssetLinks);
            _externalAccessPolicyRepository = CreateRepository(ExternalAccessPolicies);
            _decisionRepository = CreateRepository(Decisions);
            _meetingRepository = CreateRepository(Meetings);
            _actionItemRepository = CreateRepository(ActionItems);
            _lessonLearnedRepository = CreateRepository(LessonsLearned);
            _closureRepository = CreateRepository(Closures);
            _projectUnitRepository = CreateRepository(ProjectUnits);
            _purchaseRequisitionRepository = CreateRepository(PurchaseRequisitions);
            _purchaseOrderRepository = CreateRepository(PurchaseOrders);
            _purchaseOrderItemRepository = CreateRepository(PurchaseOrderItems);
            _purchaseOrderReceiptRepository = CreateRepository(PurchaseOrderReceipts);
            _purchaseOrderReceiptItemRepository = CreateRepository(PurchaseOrderReceiptItems);
            _purchaseReturnRepository = CreateRepository(PurchaseReturns);
            _inventoryRequisitionRepository = CreateRepository(InventoryRequisitions);
            _stockMovementRepository = CreateRepository(StockMovements);
            _inventoryItemRepository = CreateRepository(InventoryItems);
            _maintenanceAssetRepository = CreateRepository(MaintenanceAssets);
            _workOrderRepository = CreateRepository(WorkOrders);
            _maintenanceTypeRepository = CreateRepository(MaintenanceTypes);
            _priorityLevelRepository = CreateRepository(PriorityLevels);
            _workOrderTypeRepository = CreateRepository(WorkOrderTypes);
            _companyAssetRepository = CreateRepository(CompanyAssets);
            _jobCardRepository = CreateRepository(JobCards);
            _employeeRepository = CreateRepository(Employees);
            _skillRepository = CreateRepository(Skills);
            _employeeSkillRepository = CreateRepository(EmployeeSkills);
            _leaveRequestRepository = CreateRepository(LeaveRequests);
            _invoiceRepository = CreateRepository(Invoices);
            _paymentRepository = CreateRepository(Payments);
            _commentRepository = CreateRepository(Comments);
            _materialCostEntryRepository = CreateRepository(MaterialCostEntries);
            _deliverableExternalReviewRepository = CreateRepository(DeliverableExternalReviews);
            _warehouseRepository = CreateRepository(Warehouses);
            _warehouseLocationRepository = CreateRepository(WarehouseLocations);
            _tenderRepository = CreateRepository(Tenders);
            _businessPartnerRepository = CreateRepository(BusinessPartners);
            _salesAgreementRepository = CreateRepository(SalesAgreements);
            _salesOrderRepository = CreateRepository(SalesOrders);
            _projectPackageRepository = CreateRepository(ProjectPackages);
            _projectBoqItemRepository = CreateRepository(ProjectBoqItems);
            _projectCatalogEntryRepository = CreateRepository(ProjectCatalogEntries);
            _quantitySurveyProfileRepository = CreateRepository(QuantitySurveyProfiles);
            _quantitySurveyDecisionRepository = CreateRepository(QuantitySurveyDecisions);
            _projectBoqVersionRepository = CreateRepository(ProjectBoqVersions);
            _projectBoqVersionLineRepository = CreateRepository(ProjectBoqVersionLines);

            ProjectRepository
                .Setup(x => x.LookupAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<int>()))
                .ReturnsAsync((string? _, string? _, Guid? _, Guid? portfolioId, Guid? programId, int _) =>
                    Projects.Where(x => (!portfolioId.HasValue || x.PortfolioId == portfolioId) && (!programId.HasValue || x.ProgramId == programId)).ToList());
            ProjectRepository
                .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => Projects.SingleOrDefault(x => x.Id == id));
            ProjectRepository
                .Setup(x => x.GetDetailByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) =>
                {
                    var project = Projects.SingleOrDefault(x => x.Id == id);
                    if (project == null)
                    {
                        return null;
                    }

                    project.Members = Members.Where(x => x.ProjectId == id && x.IsActive).OrderBy(x => x.JoinedAt).ToList();
                    project.DevelopmentProfile = DevelopmentProfiles.SingleOrDefault(x => x.ProjectId == id);
                    project.Phases = ProjectPhases.Where(x => x.ProjectId == id).OrderBy(x => x.SortOrder).ToList();
                    project.WorkItems = WorkItems.Where(x => x.ProjectId == id).OrderBy(x => x.SortOrder).ToList();
                    project.Milestones = Milestones.Where(x => x.ProjectId == id).OrderBy(x => x.TargetDate).ToList();
                    project.InitiationVersions = InitiationVersions.Where(x => x.ProjectId == id).OrderByDescending(x => x.VersionNumber).ToList();
                    project.ResourceAllocations = ResourceAllocations.Where(x => x.ProjectId == id).OrderBy(x => x.StartDate).ToList();
                    project.Risks = Risks.Where(x => x.ProjectId == id).OrderByDescending(x => x.Exposure).ToList();
                    project.Issues = Issues.Where(x => x.ProjectId == id).OrderBy(x => x.TargetResolutionDate).ToList();
                    project.QualityCheckpoints = QualityCheckpoints.Where(x => x.ProjectId == id).OrderBy(x => x.DueDate).ToList();
                    project.NonConformances = NonConformances.Where(x => x.ProjectId == id).OrderByDescending(x => x.ReportedAt).ToList();
                    project.BillingSchedules = BillingSchedules.Where(x => x.ProjectId == id).OrderBy(x => x.BillingDate).ToList();
                    project.InvoiceRequests = InvoiceRequests.Where(x => x.ProjectId == id).OrderByDescending(x => x.RequestedAt).ToList();
                    project.Deliverables = Deliverables.Where(x => x.ProjectId == id).OrderBy(x => x.TargetDate).ToList();
                    project.Baselines = Baselines.Where(x => x.ProjectId == id).OrderByDescending(x => x.CreatedOn).ToList();
                    project.TimesheetEntries = TimesheetEntries.Where(x => x.ProjectId == id).OrderByDescending(x => x.EntryDate).ToList();
                    project.Expenses = Expenses.Where(x => x.ProjectId == id).OrderByDescending(x => x.ExpenseDate).ToList();
                    project.Decisions = Decisions.Where(x => x.ProjectId == id).OrderByDescending(x => x.DecisionDate).ToList();
                    project.Meetings = Meetings.Where(x => x.ProjectId == id).OrderByDescending(x => x.MeetingDate).ToList();
                    project.ActionItems = ActionItems.Where(x => x.ProjectId == id).OrderBy(x => x.DueDate).ToList();
                    project.Units = ProjectUnits.Where(x => x.ProjectId == id).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToList();
                    project.RevenueRecognitions = RevenueRecognitions.Where(x => x.ProjectId == id).OrderByDescending(x => x.RecognitionPeriod).ToList();
                    project.AssetLinks = AssetLinks.Where(x => x.ProjectId == id).ToList();
                    project.ExternalAccessPolicies = ExternalAccessPolicies.Where(x => x.ProjectId == id).ToList();
                    project.Comments = Comments.Where(x => x.ProjectId == id).OrderByDescending(x => x.CreatedAt).ToList();
                    return project;
                });
            ProjectRepository
                .Setup(x => x.CreateAsync(It.IsAny<Project>()))
                .ReturnsAsync((Project project) =>
                {
                    Projects.Add(project);
                    return project;
                });
            ProjectRepository
                .Setup(x => x.UpdateAsync(It.IsAny<Project>()))
                .ReturnsAsync((Project project) => project);

            ProjectPortfolioRepository
                .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => Portfolios.SingleOrDefault(x => x.Id == id));
            ProjectPortfolioRepository
                .Setup(x => x.GetAllAsync())
                .ReturnsAsync(() => Portfolios.ToList());

            ProjectProgramRepository
                .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => Programs.SingleOrDefault(x => x.Id == id));
            ProjectProgramRepository
                .Setup(x => x.GetAllAsync())
                .ReturnsAsync(() => Programs.ToList());
            ProjectProgramRepository
                .Setup(x => x.GetByPortfolioIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => Programs.Where(x => x.PortfolioId == id).ToList());

            WorkflowStatusAdapterRegistry
                .Setup(x => x.GetAdapter("Project"))
                .Returns(new ProjectWorkflowStatusAdapter());
            WorkflowStatusAdapterRegistry
                .Setup(x => x.GetAdapter("ProjectBudgetRevision"))
                .Returns(new ProjectBudgetRevisionWorkflowStatusAdapter());
            WorkflowStatusAdapterRegistry
                .Setup(x => x.GetAdapter("ProjectDeliverable"))
                .Returns(new ProjectDeliverableWorkflowStatusAdapter());
            WorkflowStatusAdapterRegistry
                .Setup(x => x.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Boq))
                .Returns(new QuantitySurveyWorkflowStatusAdapter());
            WorkflowIntegrationService
                .Setup(x => x.SubmitAsync("ProjectDeliverable", It.IsAny<Guid>()))
                .ReturnsAsync(new WorkflowIntegrationResult(
                    new WorkflowExecutionResult
                    {
                        Success = true,
                        Status = WorkflowInstanceStatus.InProgress
                    },
                    WorkflowOutcome.Pending));
            UserService
                .Setup(x => x.GetUserByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => Users.SingleOrDefault(x => x.Id == id));
            UserService
                .Setup(x => x.GetUsersByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync((IEnumerable<Guid> ids) =>
                {
                    var lookup = ids
                        .Where(id => id != Guid.Empty)
                        .Distinct()
                        .ToHashSet();
                    return Users.Where(user => lookup.Contains(user.Id)).ToList();
                });
            TenantSettingsService
                .Setup(x => x.GetBaseCurrencyAsync())
                .ReturnsAsync("USD");

            UnitOfWork.Setup(x => x.Repository<Project>()).Returns(_projectEntityRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectMember>()).Returns(_memberRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectDevelopmentProfile>()).Returns(_developmentProfileRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectPhase>()).Returns(_projectPhaseRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectWorkItem>()).Returns(_workItemRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectMilestone>()).Returns(_milestoneRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectInitiationVersion>()).Returns(_initiationVersionRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectResourceAllocation>()).Returns(_resourceAllocationRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectRisk>()).Returns(_riskRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectIssue>()).Returns(_issueRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectQualityCheckpoint>()).Returns(_qualityCheckpointRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectNonConformance>()).Returns(_nonConformanceRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectBillingSchedule>()).Returns(_billingScheduleRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectInvoiceRequest>()).Returns(_invoiceRequestRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectDeliverable>()).Returns(_deliverableRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectDocument>()).Returns(_documentRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectTaskDependency>()).Returns(_taskDependencyRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectInterdependency>()).Returns(_interdependencyRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectBaseline>()).Returns(_baselineRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectTimesheetEntry>()).Returns(_timesheetRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectExpense>()).Returns(_expenseRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectRevenueRecognition>()).Returns(_revenueRecognitionRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectBudgetRevision>()).Returns(_budgetRevisionRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectForecastVersion>()).Returns(_forecastVersionRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectAssetLink>()).Returns(_assetLinkRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectExternalAccessPolicy>()).Returns(_externalAccessPolicyRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectDecision>()).Returns(_decisionRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectMeetingMinute>()).Returns(_meetingRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectActionItem>()).Returns(_actionItemRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectLessonLearned>()).Returns(_lessonLearnedRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectClosure>()).Returns(_closureRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectUnit>()).Returns(_projectUnitRepository.Object);
            UnitOfWork.Setup(x => x.Repository<PurchaseRequisition>()).Returns(_purchaseRequisitionRepository.Object);
            UnitOfWork.Setup(x => x.Repository<PurchaseOrder>()).Returns(_purchaseOrderRepository.Object);
            UnitOfWork.Setup(x => x.Repository<PurchaseOrderItem>()).Returns(_purchaseOrderItemRepository.Object);
            UnitOfWork.Setup(x => x.Repository<PurchaseOrderReceipt>()).Returns(_purchaseOrderReceiptRepository.Object);
            UnitOfWork.Setup(x => x.Repository<PurchaseOrderReceiptItem>()).Returns(_purchaseOrderReceiptItemRepository.Object);
            UnitOfWork.Setup(x => x.Repository<PurchaseReturn>()).Returns(_purchaseReturnRepository.Object);
            UnitOfWork.Setup(x => x.Repository<InventoryRequisition>()).Returns(_inventoryRequisitionRepository.Object);
            UnitOfWork.Setup(x => x.Repository<StockMovement>()).Returns(_stockMovementRepository.Object);
            UnitOfWork.Setup(x => x.Repository<InventoryItem>()).Returns(_inventoryItemRepository.Object);
            UnitOfWork.Setup(x => x.Repository<MaintenanceAsset>()).Returns(_maintenanceAssetRepository.Object);
            UnitOfWork.Setup(x => x.Repository<WorkOrder>()).Returns(_workOrderRepository.Object);
            UnitOfWork.Setup(x => x.Repository<MaintenanceType>()).Returns(_maintenanceTypeRepository.Object);
            UnitOfWork.Setup(x => x.Repository<PriorityLevel>()).Returns(_priorityLevelRepository.Object);
            UnitOfWork.Setup(x => x.Repository<WorkOrderType>()).Returns(_workOrderTypeRepository.Object);
            UnitOfWork.Setup(x => x.Repository<CompanyAsset>()).Returns(_companyAssetRepository.Object);
            UnitOfWork.Setup(x => x.Repository<JobCard>()).Returns(_jobCardRepository.Object);
            UnitOfWork.Setup(x => x.Repository<Employee>()).Returns(_employeeRepository.Object);
            UnitOfWork.Setup(x => x.Repository<Skill>()).Returns(_skillRepository.Object);
            UnitOfWork.Setup(x => x.Repository<EmployeeSkill>()).Returns(_employeeSkillRepository.Object);
            UnitOfWork.Setup(x => x.Repository<LeaveRequest>()).Returns(_leaveRequestRepository.Object);
            UnitOfWork.Setup(x => x.Repository<Invoice>()).Returns(_invoiceRepository.Object);
            UnitOfWork.Setup(x => x.Repository<Payment>()).Returns(_paymentRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectComment>()).Returns(_commentRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectMaterialCostEntry>()).Returns(_materialCostEntryRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectDeliverableExternalReview>()).Returns(_deliverableExternalReviewRepository.Object);
            UnitOfWork.Setup(x => x.Repository<Warehouse>()).Returns(_warehouseRepository.Object);
            UnitOfWork.Setup(x => x.Repository<WarehouseLocation>()).Returns(_warehouseLocationRepository.Object);
            UnitOfWork.Setup(x => x.Repository<Tender>()).Returns(_tenderRepository.Object);
            UnitOfWork.Setup(x => x.Repository<BusinessPartner>()).Returns(_businessPartnerRepository.Object);
            UnitOfWork.Setup(x => x.Repository<SalesAgreement>()).Returns(_salesAgreementRepository.Object);
            UnitOfWork.Setup(x => x.Repository<SalesOrder>()).Returns(_salesOrderRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectPackage>()).Returns(_projectPackageRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectBoqItem>()).Returns(_projectBoqItemRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectCatalogEntry>()).Returns(_projectCatalogEntryRepository.Object);
            UnitOfWork.Setup(x => x.Repository<QuantitySurveyConfigurationProfile>()).Returns(_quantitySurveyProfileRepository.Object);
            UnitOfWork.Setup(x => x.Repository<QuantitySurveyConfigurationDecision>()).Returns(_quantitySurveyDecisionRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectBoqVersion>()).Returns(_projectBoqVersionRepository.Object);
            UnitOfWork.Setup(x => x.Repository<ProjectBoqVersionLine>()).Returns(_projectBoqVersionLineRepository.Object);
            UnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            UnitOfWork.SetupGet(x => x.HasActiveTransaction).Returns(true);
            UnitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            UnitOfWork.Setup(x => x.AcquireTransactionLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            UnitOfWork.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            UnitOfWork.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            UnitOfWork
                .Setup(x => x.ExecuteInStrategyAsync(It.IsAny<Func<Task<Guid>>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<Task<Guid>> operation, CancellationToken _) => operation());
            UnitOfWork
                .Setup(x => x.ExecuteInStrategyAsync(It.IsAny<Func<Task<bool>>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<Task<bool>> operation, CancellationToken _) => operation());
            AppEventBus
                .Setup(x => x.PublishAsync(It.IsAny<EntityActivityEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            CurrentUserProvider.SetupGet(x => x.TenantId).Returns(tenantId);
            CurrentUserProvider.SetupGet(x => x.UserId).Returns(userId);
            CurrentUserProvider.SetupGet(x => x.Username).Returns("tester@example.com");
            CurrentUserProvider.SetupGet(x => x.IsAuthenticated).Returns(true);
            CurrentUserProvider.SetupGet(x => x.IsExternalUser).Returns(false);
            CurrentUserProvider.SetupGet(x => x.Roles).Returns(() => _roles.ToArray());
            CurrentUserProvider.Setup(x => x.HasRole(It.IsAny<string>())).Returns((string role) => _roles.Contains(role));
        }

        public ProjectService CreateService()
            => new(
                ProjectRepository.Object,
                SettingsRepository.Object,
                TemplateRepository.Object,
                ProjectTypeRepository.Object,
                ProjectPortfolioRepository.Object,
                ProjectProgramRepository.Object,
                ContractService.Object,
                BusinessPartnerService.Object,
                WorkflowIntegrationService.Object,
                WorkflowStatusAdapterRegistry.Object,
                WorkflowService.Object,
                UserService.Object,
                JobCardService.Object,
                WorkOrderService.Object,
                SalesAgreementService.Object,
                SalesOrderService.Object,
                EstateManagedAssetService.Object,
                UnitOfWork.Object,
                TenantSettingsService.Object,
                CurrentUserProvider.Object,
                AppEventBus.Object,
                NullLogger<ProjectService>.Instance);

        public void SetRoles(params string[] roles)
        {
            _roles.Clear();
            foreach (var role in roles)
            {
                _roles.Add(role);
            }
        }

        private static Mock<IGenericRepository<T>> CreateRepository<T>(List<T> items) where T : BaseEntity
        {
            var repository = new Mock<IGenericRepository<T>>();

            repository
                .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => items.FirstOrDefault(entity => entity.Id == id));
            repository
                .Setup(x => x.FindAsync(It.IsAny<Expression<Func<T, bool>>>()))
                .ReturnsAsync((Expression<Func<T, bool>> predicate) => items.Where(predicate.Compile()).ToList());
            repository
                .Setup(x => x.FindAsync(It.IsAny<Expression<Func<T, bool>>>(), It.IsAny<Expression<Func<T, object>>[]>()))
                .ReturnsAsync((Expression<Func<T, bool>> predicate, Expression<Func<T, object>>[] _) => items.Where(predicate.Compile()).ToList());
            repository
                .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<T, bool>>>()))
                .ReturnsAsync((Expression<Func<T, bool>> predicate) => items.FirstOrDefault(predicate.Compile()));
            repository
                .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<T, bool>>>(), It.IsAny<Expression<Func<T, object>>[]>()))
                .ReturnsAsync((Expression<Func<T, bool>> predicate, Expression<Func<T, object>>[] _) => items.FirstOrDefault(predicate.Compile()));
            repository
                .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<T, bool>>>()))
                .ReturnsAsync((Expression<Func<T, bool>> predicate) => items.Any(predicate.Compile()));
            repository
                .Setup(x => x.AddAsync(It.IsAny<T>()))
                .ReturnsAsync((T entity) =>
                {
                    if (entity.Id == Guid.Empty)
                    {
                        entity.Id = Guid.NewGuid();
                    }

                    if (entity.CreatedAt == default)
                    {
                        entity.CreatedAt = DateTime.UtcNow;
                    }

                    items.Add(entity);
                    return entity;
                });
            repository
                .Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<T>>()))
                .ReturnsAsync((IEnumerable<T> entities) =>
                {
                    var entityList = entities.ToList();
                    foreach (var entity in entityList)
                    {
                        if (entity.Id == Guid.Empty)
                        {
                            entity.Id = Guid.NewGuid();
                        }

                        if (entity.CreatedAt == default)
                        {
                            entity.CreatedAt = DateTime.UtcNow;
                        }
                    }

                    items.AddRange(entityList);
                    return entityList.AsEnumerable();
                });
            repository
                .Setup(x => x.UpdateAsync(It.IsAny<T>()))
                .Returns(Task.CompletedTask);
            repository
                .Setup(x => x.UpdateRangeAsync(It.IsAny<IEnumerable<T>>()))
                .Returns(Task.CompletedTask);
            repository
                .Setup(x => x.DeleteAsync(It.IsAny<T>()))
                .Returns((T entity) =>
                {
                    items.Remove(entity);
                    return Task.CompletedTask;
                });
            repository
                .Setup(x => x.DeleteRangeAsync(It.IsAny<IEnumerable<T>>()))
                .Returns((IEnumerable<T> entities) =>
                {
                    foreach (var entity in entities.ToList())
                    {
                        items.Remove(entity);
                    }

                    return Task.CompletedTask;
                });
            repository
                .Setup(x => x.DeleteAsync(It.IsAny<Guid>()))
                .Returns((Guid id) =>
                {
                    var entity = items.FirstOrDefault(x => x.Id == id);
                    if (entity != null)
                    {
                        items.Remove(entity);
                    }

                    return Task.CompletedTask;
                });
            repository
                .Setup(x => x.HardDeleteRangeAsync(It.IsAny<IEnumerable<T>>()))
                .Returns((IEnumerable<T> entities) =>
                {
                    foreach (var entity in entities.ToList())
                    {
                        items.Remove(entity);
                    }

                    return Task.CompletedTask;
                });

            return repository;
        }
    }
}
