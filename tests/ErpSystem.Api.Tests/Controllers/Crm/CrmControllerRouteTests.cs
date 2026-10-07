using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.IO;
using System.Reflection;
using System.Text;
using ErpSystem.Api.Middleware;
using ErpSystem.Api.Controllers.Crm;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Crm;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Crm;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Crm;

public class CrmControllerRouteTests
{
    [Fact]
    public void OpportunityStageRoutes_ShouldUseDynamicCrmPermissions()
    {
        var controller = typeof(CrmController);
        controller.GetMethod(nameof(CrmController.GetOpportunityStages))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CrmPermissions.Read);
        controller.GetMethod(nameof(CrmController.UpdateOpportunityStages))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CrmPermissions.Manage);
    }

    [Fact]
    public async Task GetOverview_ShouldAllowLocalInternalUsers()
    {
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetOverviewAsync(5))
            .ReturnsAsync(new CrmOverviewDto
            {
                OpenOpportunityCount = 2,
                ActiveAccountCount = 1
            });

        using var factory = CreateFactory(
            crmService,
            CreatePrincipal(
                new Claim("auth_provider", "Local"),
                new Claim(ClaimTypes.Role, "SuperAdmin")));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/crm/overview?take=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExternalUserAccessMiddleware_ShouldBlockExternalPortalUsersFromCrmRoutes()
    {
        var middleware = new ExternalUserAccessMiddleware();
        var context = new DefaultHttpContext
        {
            User = CreatePrincipal(
                new Claim("auth_provider", "Local"),
                new Claim(ClaimTypes.Role, "ExternalUser"))
        };
        context.Request.Path = "/api/crm/overview";
        context.Response.Body = new MemoryStream();

        var nextCalled = false;
        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        body.Should().Contain("External users are not permitted");
    }

    [Fact]
    public async Task GetOverview_ShouldReturnCrmOverview()
    {
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetOverviewAsync(5))
            .ReturnsAsync(new CrmOverviewDto
            {
                OpenOpportunityCount = 4,
                OpenOpportunityValue = 250000m,
                WeightedPipelineValue = 145000m,
                ActiveAccountCount = 3,
                Accounts =
                {
                    new CrmAccountOverviewDto
                    {
                        BusinessPartnerId = Guid.NewGuid(),
                        PartnerCode = "CUST-001",
                        PartnerName = "Northwind Civic Works",
                        PartnerType = "Customer",
                        RegistrationStatus = "Approved"
                    }
                }
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/crm/overview?take=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var overview = await response.Content.ReadFromJsonAsync<CrmOverviewDto>();
        overview.Should().NotBeNull();
        overview!.OpenOpportunityCount.Should().Be(4);
        overview.ActiveAccountCount.Should().Be(3);
        overview.Accounts.Should().ContainSingle(x => x.PartnerCode == "CUST-001");
    }

    [Fact]
    public async Task GetAccountDetail_ShouldReturnCrmAccountDetail()
    {
        var partnerId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetAccountDetailAsync(partnerId, 8))
            .ReturnsAsync(new CrmAccountDetailDto
            {
                BusinessPartnerId = partnerId,
                PartnerCode = "CUST-200",
                PartnerName = "Atlas Infrastructure",
                OpenOpportunityCount = 2,
                ActiveQuoteCount = 1
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/crm/accounts/{partnerId}?take=8");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var account = await response.Content.ReadFromJsonAsync<CrmAccountDetailDto>();
        account.Should().NotBeNull();
        account!.BusinessPartnerId.Should().Be(partnerId);
        account.OpenOpportunityCount.Should().Be(2);
        account.ActiveQuoteCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAccounts_ShouldReturnPagedCrmAccounts()
    {
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetAccountsAsync(1, 20, "atlas", "At Risk", true, "Customer"))
            .ReturnsAsync(new PagedResult<CrmAccountOverviewDto>
            {
                Items = new List<CrmAccountOverviewDto>
                {
                    new()
                    {
                        BusinessPartnerId = Guid.NewGuid(),
                        PartnerCode = "CUST-310",
                        PartnerName = "Atlas Infrastructure",
                        PartnerType = "Customer",
                        RegistrationStatus = "Approved",
                        HealthCategory = "At Risk",
                        HealthScore = 44,
                        OpenOpportunityCount = 2,
                        OpenOpportunityValue = 420000m,
                        ActiveQuoteCount = 1,
                        ActiveQuoteValue = 400000m
                    }
                },
                Page = 1,
                PageSize = 20,
                TotalCount = 1
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/crm/accounts?page=1&pageSize=20&search=atlas&healthCategory=At%20Risk&atRiskOnly=true&partnerType=Customer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var accounts = await response.Content.ReadFromJsonAsync<PagedResult<CrmAccountOverviewDto>>();
        accounts.Should().NotBeNull();
        accounts!.TotalCount.Should().Be(1);
        accounts.Items.Should().ContainSingle(x => x.PartnerName == "Atlas Infrastructure");
    }

    [Fact]
    public async Task GetContacts_ShouldReturnPagedCrmContacts()
    {
        var businessPartnerId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetContactsAsync(1, 20, "irene", businessPartnerId, "Commercial", true, true, "Customer"))
            .ReturnsAsync(new PagedResult<CrmContactListItemDto>
            {
                Items = new List<CrmContactListItemDto>
                {
                    new()
                    {
                        ContactId = Guid.NewGuid(),
                        BusinessPartnerId = businessPartnerId,
                        PartnerCode = "CUST-310",
                        PartnerName = "Atlas Infrastructure",
                        PartnerType = "Customer",
                        RegistrationStatus = "Approved",
                        ContactName = "Irene Mensah",
                        ContactTitle = "Commercial Director",
                        Department = "Commercial",
                        Email = "irene@atlas.test",
                        IsPrimary = true,
                        IsAtRisk = true,
                        HealthScore = 42,
                        HealthCategory = "At Risk",
                        OpenOpportunityCount = 1,
                        ActiveContractCount = 1
                    }
                },
                Page = 1,
                PageSize = 20,
                TotalCount = 1
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/crm/contacts?page=1&pageSize=20&search=irene&businessPartnerId={businessPartnerId}&department=Commercial&primaryOnly=true&atRiskOnly=true&partnerType=Customer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contacts = await response.Content.ReadFromJsonAsync<PagedResult<CrmContactListItemDto>>();
        contacts.Should().NotBeNull();
        contacts!.TotalCount.Should().Be(1);
        contacts.Items.Should().ContainSingle(x => x.ContactName == "Irene Mensah" && x.BusinessPartnerId == businessPartnerId);
    }

    [Fact]
    public async Task GetReadiness_ShouldReturnPagedCrmReadiness()
    {
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetReadinessAsync(1, 20, "atlas", "Critical", true, true, "Customer"))
            .ReturnsAsync(new PagedResult<CrmReadinessListItemDto>
            {
                Items = new List<CrmReadinessListItemDto>
                {
                    new()
                    {
                        BusinessPartnerId = Guid.NewGuid(),
                        PartnerCode = "CUST-READY-1",
                        PartnerName = "Atlas Readiness",
                        PartnerType = "Customer",
                        RegistrationStatus = "Approved",
                        DocumentCount = 2,
                        ExpiredDocumentCount = 1,
                        LicenseCount = 1,
                        ExpiredLicenseCount = 1,
                        FinancialRecordCount = 0,
                        ReadinessScore = 34,
                        ReadinessCategory = "Critical",
                        HasCriticalGap = true
                    }
                },
                Page = 1,
                PageSize = 20,
                TotalCount = 1
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/crm/readiness?page=1&pageSize=20&search=atlas&readinessCategory=Critical&expiringOnly=true&missingFinancialsOnly=true&partnerType=Customer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var readiness = await response.Content.ReadFromJsonAsync<PagedResult<CrmReadinessListItemDto>>();
        readiness.Should().NotBeNull();
        readiness!.TotalCount.Should().Be(1);
        readiness.Items.Should().ContainSingle(x => x.PartnerName == "Atlas Readiness" && x.HasCriticalGap);
    }

    [Fact]
    public async Task GetReadinessDetail_ShouldReturnCrmReadinessDetail()
    {
        var businessPartnerId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetReadinessDetailAsync(businessPartnerId, 8))
            .ReturnsAsync(new CrmReadinessDetailDto
            {
                BusinessPartnerId = businessPartnerId,
                PartnerCode = "CUST-READY-2",
                PartnerName = "Blue Harbor Readiness",
                PartnerType = "Customer",
                RegistrationStatus = "Approved",
                ReadinessScore = 52,
                ReadinessCategory = "Gap",
                HasCriticalGap = false,
                Documents =
                {
                    new CrmReadinessDocumentDto
                    {
                        DocumentId = Guid.NewGuid(),
                        DocumentType = "Insurance",
                        DocumentName = "Insurance Cover",
                        IsVerified = true,
                        UploadedAt = DateTime.UtcNow
                    }
                }
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/crm/readiness/{businessPartnerId}?take=8");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var readiness = await response.Content.ReadFromJsonAsync<CrmReadinessDetailDto>();
        readiness.Should().NotBeNull();
        readiness!.BusinessPartnerId.Should().Be(businessPartnerId);
        readiness.ReadinessCategory.Should().Be("Gap");
        readiness.Documents.Should().ContainSingle(x => x.DocumentType == "Insurance");
    }

    [Fact]
    public async Task GetRisk_ShouldReturnPagedCrmRiskRegister()
    {
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetRiskAsync(1, 20, "atlas", "Critical", true, true, "Customer"))
            .ReturnsAsync(new PagedResult<CrmRiskListItemDto>
            {
                Items = new List<CrmRiskListItemDto>
                {
                    new()
                    {
                        BusinessPartnerId = Guid.NewGuid(),
                        PartnerCode = "CUST-RISK-1",
                        PartnerName = "Atlas Risk Hub",
                        PartnerType = "Customer",
                        RegistrationStatus = "Approved",
                        RiskLevel = "High",
                        OpenIncidentCount = 2,
                        CriticalIncidentCount = 1,
                        PendingAppealCount = 1,
                        RiskScore = 82,
                        RiskCategory = "Critical",
                        RequiresEscalation = true
                    }
                },
                Page = 1,
                PageSize = 20,
                TotalCount = 1
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/crm/risk?page=1&pageSize=20&search=atlas&riskCategory=Critical&escalationOnly=true&openIncidentOnly=true&partnerType=Customer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var risk = await response.Content.ReadFromJsonAsync<PagedResult<CrmRiskListItemDto>>();
        risk.Should().NotBeNull();
        risk!.TotalCount.Should().Be(1);
        risk.Items.Should().ContainSingle(x => x.PartnerName == "Atlas Risk Hub" && x.RequiresEscalation);
    }

    [Fact]
    public async Task GetRiskDetail_ShouldReturnCrmRiskDetail()
    {
        var businessPartnerId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetRiskDetailAsync(businessPartnerId, 8))
            .ReturnsAsync(new CrmRiskDetailDto
            {
                BusinessPartnerId = businessPartnerId,
                PartnerCode = "CUST-RISK-2",
                PartnerName = "Blue Harbor Risk",
                PartnerType = "Customer",
                RegistrationStatus = "Approved",
                RiskScore = 68,
                RiskCategory = "Elevated",
                RequiresEscalation = true,
                Incidents =
                {
                    new CrmRiskIncidentDto
                    {
                        IncidentId = Guid.NewGuid(),
                        IncidentNumber = "QI-200",
                        IncidentDate = DateTime.UtcNow.AddDays(-7),
                        IncidentType = "Delay",
                        Severity = "High",
                        Status = "InProgress",
                        Description = "Escalated delivery issue",
                        RequiresSupplierResponse = true
                    }
                }
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/crm/risk/{businessPartnerId}?take=8");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var risk = await response.Content.ReadFromJsonAsync<CrmRiskDetailDto>();
        risk.Should().NotBeNull();
        risk!.BusinessPartnerId.Should().Be(businessPartnerId);
        risk.RiskCategory.Should().Be("Elevated");
        risk.Incidents.Should().ContainSingle(x => x.IncidentNumber == "QI-200");
    }

    [Fact]
    public async Task GetCollaboration_ShouldReturnPagedCrmCollaboration()
    {
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetCollaborationAsync(1, 20, "atlas", "Blocked", true, true, "Customer"))
            .ReturnsAsync(new PagedResult<CrmCollaborationListItemDto>
            {
                Items = new List<CrmCollaborationListItemDto>
                {
                    new()
                    {
                        BusinessPartnerId = Guid.NewGuid(),
                        PartnerCode = "CUST-COLLAB-1",
                        PartnerName = "Atlas Collaboration Hub",
                        PartnerType = "Customer",
                        RegistrationStatus = "Approved",
                        LatestApplicationNumber = "REG-ATLAS-1",
                        LatestRegistrationLifecycleStatus = "Submitted",
                        PortalProjectCount = 1,
                        CollaborationProjectCount = 1,
                        OpenOpportunityCount = 1,
                        ActiveContractCount = 1,
                        CollaborationScore = 41,
                        CollaborationCategory = "Blocked",
                        RequiresEnablement = true
                    }
                },
                Page = 1,
                PageSize = 20,
                TotalCount = 1
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/crm/collaboration?page=1&pageSize=20&search=atlas&collaborationCategory=Blocked&enablementOnly=true&pendingOnboardingOnly=true&partnerType=Customer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var collaboration = await response.Content.ReadFromJsonAsync<PagedResult<CrmCollaborationListItemDto>>();
        collaboration.Should().NotBeNull();
        collaboration!.TotalCount.Should().Be(1);
        collaboration.Items.Should().ContainSingle(x => x.PartnerName == "Atlas Collaboration Hub" && x.RequiresEnablement);
    }

    [Fact]
    public async Task GetCollaborationDetail_ShouldReturnCrmCollaborationDetail()
    {
        var businessPartnerId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetCollaborationDetailAsync(businessPartnerId, 8))
            .ReturnsAsync(new CrmCollaborationDetailDto
            {
                BusinessPartnerId = businessPartnerId,
                PartnerCode = "CUST-COLLAB-2",
                PartnerName = "Blue Harbor Collaboration",
                PartnerType = "Customer",
                RegistrationStatus = "Approved",
                CollaborationScore = 88,
                CollaborationCategory = "Connected",
                RequiresEnablement = false,
                PrimaryContactName = "Noah Tetteh",
                Registrations =
                {
                    new CrmCollaborationRegistrationDto
                    {
                        RegistrationId = Guid.NewGuid(),
                        ApplicationNumber = "REG-BLUE-1",
                        Status = "Approved",
                        CreatedAt = DateTime.UtcNow.AddDays(-10),
                        ApprovedDate = DateTime.UtcNow.AddDays(-5),
                        DocumentCount = 1,
                        VerifiedDocumentCount = 1
                    }
                },
                PortalUsers =
                {
                    new CrmCollaborationPortalUserDto
                    {
                        PortalUserId = Guid.NewGuid(),
                        UserId = Guid.NewGuid(),
                        UserName = "portal.blueharbor",
                        FullName = "Noah Tetteh",
                        Email = "noah@blueharbor.test",
                        Role = "Admin",
                        IsActive = true,
                        GrantedAt = DateTime.UtcNow.AddDays(-4)
                    }
                }
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/crm/collaboration/{businessPartnerId}?take=8");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var collaboration = await response.Content.ReadFromJsonAsync<CrmCollaborationDetailDto>();
        collaboration.Should().NotBeNull();
        collaboration!.BusinessPartnerId.Should().Be(businessPartnerId);
        collaboration.CollaborationCategory.Should().Be("Connected");
        collaboration.Registrations.Should().ContainSingle(x => x.ApplicationNumber == "REG-BLUE-1");
        collaboration.PortalUsers.Should().ContainSingle(x => x.FullName == "Noah Tetteh");
    }

    [Fact]
    public async Task GetService_ShouldReturnPagedCrmService()
    {
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetServiceAsync(1, 20, "atlas", "Critical", true, true, "Customer"))
            .ReturnsAsync(new PagedResult<CrmServiceListItemDto>
            {
                Items = new List<CrmServiceListItemDto>
                {
                    new()
                    {
                        BusinessPartnerId = Guid.NewGuid(),
                        PartnerCode = "CUST-SVC-1",
                        PartnerName = "Atlas Service Hub",
                        PartnerType = "Customer",
                        RegistrationStatus = "Approved",
                        PortalUserCount = 1,
                        ActivePortalUserCount = 1,
                        TicketCount = 3,
                        OpenTicketCount = 2,
                        OverdueTicketCount = 1,
                        ComplaintTicketCount = 1,
                        LinkedProblemCount = 1,
                        OpenProblemCount = 1,
                        ServiceScore = 42,
                        ServiceCategory = "Critical",
                        RequiresAttention = true,
                        HasSlaBreachRisk = true
                    }
                },
                Page = 1,
                PageSize = 20,
                TotalCount = 1
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/crm/service?page=1&pageSize=20&search=atlas&serviceCategory=Critical&attentionOnly=true&overdueOnly=true&partnerType=Customer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var service = await response.Content.ReadFromJsonAsync<PagedResult<CrmServiceListItemDto>>();
        service.Should().NotBeNull();
        service!.TotalCount.Should().Be(1);
        service.Items.Should().ContainSingle(x => x.PartnerName == "Atlas Service Hub" && x.RequiresAttention);
    }

    [Fact]
    public async Task GetServiceDetail_ShouldReturnCrmServiceDetail()
    {
        var businessPartnerId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetServiceDetailAsync(businessPartnerId, 8))
            .ReturnsAsync(new CrmServiceDetailDto
            {
                BusinessPartnerId = businessPartnerId,
                PartnerCode = "CUST-SVC-2",
                PartnerName = "Blue Harbor Service",
                PartnerType = "Customer",
                RegistrationStatus = "Approved",
                ServiceScore = 61,
                ServiceCategory = "Escalate",
                RequiresAttention = true,
                HasSlaBreachRisk = true,
                Tickets =
                {
                    new CrmServiceTicketDto
                    {
                        TicketId = Guid.NewGuid(),
                        TicketNumber = "TCK-200",
                        TicketType = "Complaint",
                        Priority = "Critical",
                        Source = "PhoneCall",
                        Status = "InProgress",
                        CreatedAt = DateTime.UtcNow.AddDays(-2),
                        IsOpen = true,
                        IsOverdue = true,
                        IsComplaint = true
                    }
                },
                Problems =
                {
                    new CrmServiceProblemDto
                    {
                        ProblemId = Guid.NewGuid(),
                        ProblemNumber = "PRB-200",
                        Title = "Recurring outage pattern",
                        Status = "InProgress",
                        Priority = "High",
                        LinkedTicketCount = 1,
                        CreatedAt = DateTime.UtcNow.AddDays(-1)
                    }
                }
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/crm/service/{businessPartnerId}?take=8");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var service = await response.Content.ReadFromJsonAsync<CrmServiceDetailDto>();
        service.Should().NotBeNull();
        service!.BusinessPartnerId.Should().Be(businessPartnerId);
        service.ServiceCategory.Should().Be("Escalate");
        service.Tickets.Should().ContainSingle(x => x.TicketNumber == "TCK-200" && x.IsOverdue);
        service.Problems.Should().ContainSingle(x => x.ProblemNumber == "PRB-200");
    }

    [Fact]
    public async Task GetCampaigns_ShouldReturnPagedCrmCampaigns()
    {
        var businessPartnerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetCampaignsAsync(1, 20, "renewal", "Active", "Event", true, businessPartnerId, leadId))
            .ReturnsAsync(new PagedResult<CrmCampaignListItemDto>
            {
                Items = new List<CrmCampaignListItemDto>
                {
                    new()
                    {
                        CampaignId = Guid.NewGuid(),
                        Name = "Renewal push",
                        CampaignType = "Event",
                        CampaignStatus = "Active",
                        MemberCount = 12,
                        RespondedMemberCount = 5,
                        ResponseRate = 41.7m,
                        OpenOpportunityCount = 2,
                        WeightedPipelineValue = 185000m,
                        InfluencedAccountCount = 1,
                        IsActive = true
                    }
                },
                Page = 1,
                PageSize = 20,
                TotalCount = 1
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/crm/campaigns?page=1&pageSize=20&search=renewal&status=Active&campaignType=Event&activeOnly=true&businessPartnerId={businessPartnerId}&leadId={leadId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var campaigns = await response.Content.ReadFromJsonAsync<PagedResult<CrmCampaignListItemDto>>();
        campaigns.Should().NotBeNull();
        campaigns!.TotalCount.Should().Be(1);
        campaigns.Items.Should().ContainSingle(x => x.Name == "Renewal push" && x.IsActive);
    }

    [Fact]
    public async Task GetCampaign_ShouldReturnCrmCampaignDetail()
    {
        var campaignId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetCampaignByIdAsync(campaignId))
            .ReturnsAsync(new CrmCampaignDetailDto
            {
                CampaignId = campaignId,
                Name = "Port summit series",
                CampaignType = "Event",
                CampaignStatus = "Active",
                OpenOpportunityCount = 1,
                Members =
                {
                    new CrmCampaignMemberDto
                    {
                        MemberId = Guid.NewGuid(),
                        LeadId = Guid.NewGuid(),
                        LeadName = "Kojo Owusu",
                        MemberStatus = "Active"
                    }
                }
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/crm/campaigns/{campaignId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var campaign = await response.Content.ReadFromJsonAsync<CrmCampaignDetailDto>();
        campaign.Should().NotBeNull();
        campaign!.CampaignId.Should().Be(campaignId);
        campaign.Members.Should().ContainSingle(x => x.LeadName == "Kojo Owusu");
    }

    [Fact]
    public async Task CreateCampaign_ShouldReturnCreatedCampaign()
    {
        var campaignId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.CreateCampaignAsync(It.IsAny<CreateCrmCampaignDto>()))
            .ReturnsAsync(new CrmCampaignDetailDto
            {
                CampaignId = campaignId,
                Name = "Q3 campaign",
                CampaignType = "Email",
                CampaignStatus = "Planning"
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/crm/campaigns", new CreateCrmCampaignDto
        {
            Name = "Q3 campaign",
            CampaignType = "Email",
            StartDate = DateTime.UtcNow.Date,
            CampaignStatus = "Planning"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var campaign = await response.Content.ReadFromJsonAsync<CrmCampaignDetailDto>();
        campaign.Should().NotBeNull();
        campaign!.CampaignId.Should().Be(campaignId);
    }

    [Fact]
    public async Task AddCampaignMember_ShouldReturnBadRequestWhenServiceRejectsMembership()
    {
        var campaignId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetCampaignByIdAsync(campaignId))
            .ReturnsAsync(new CrmCampaignDetailDto
            {
                CampaignId = campaignId,
                Name = "Tender outreach",
                CampaignType = "Tender",
                CampaignStatus = "Active"
            });
        crmService
            .Setup(x => x.AddCampaignMemberAsync(campaignId, It.IsAny<CreateCrmCampaignMemberDto>()))
            .ThrowsAsync(new InvalidOperationException("Lead is already a member of this campaign."));

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/crm/campaigns/{campaignId}/members", new CreateCrmCampaignMemberDto
        {
            LeadId = Guid.NewGuid(),
            MemberStatus = "Active"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var message = await response.Content.ReadAsStringAsync();
        message.Should().Contain("already a member");
    }

    [Fact]
    public async Task GetForecast_ShouldReturnCrmForecast()
    {
        var businessPartnerId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetForecastAsync(6, "Renewal", businessPartnerId))
            .ReturnsAsync(new CrmForecastDto
            {
                HorizonMonths = 6,
                OpportunityCount = 3,
                WeightedPipelineValue = 550000m,
                CommitValue = 300000m,
                RenewalGapValue = 120000m,
                Buckets =
                {
                    new CrmForecastBucketDto
                    {
                        PeriodLabel = "Apr 2026",
                        OpportunityCount = 2,
                        WeightedValue = 250000m
                    }
                }
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/crm/forecast?months=6&opportunityType=Renewal&businessPartnerId={businessPartnerId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var forecast = await response.Content.ReadFromJsonAsync<CrmForecastDto>();
        forecast.Should().NotBeNull();
        forecast!.HorizonMonths.Should().Be(6);
        forecast.WeightedPipelineValue.Should().Be(550000m);
        forecast.Buckets.Should().ContainSingle(x => x.PeriodLabel == "Apr 2026");
    }

    [Fact]
    public async Task GetConversions_ShouldReturnCrmConversions()
    {
        var businessPartnerId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetConversionsAsync(6, "Existing Customer", businessPartnerId))
            .ReturnsAsync(new CrmConversionsDto
            {
                HorizonMonths = 6,
                LeadCount = 8,
                LeadWithOpportunityCount = 5,
                OpportunityCount = 5,
                QuotedOpportunityCount = 3,
                ContractBackedOpportunityCount = 2,
                ProjectBackedOpportunityCount = 1,
                LeadToOpportunityRate = 62.5m,
                OpportunityToQuoteRate = 60m,
                Funnel =
                {
                    new CrmConversionStageMetricDto
                    {
                        Stage = "Quote",
                        EntityCount = 4,
                        RelatedOpportunityCount = 3,
                        ConversionRate = 60m
                    }
                },
                Journeys =
                {
                    new CrmConversionJourneyDto
                    {
                        OpportunityId = Guid.NewGuid(),
                        OpportunityName = "Metro rollout",
                        Stage = "Proposal",
                        OpportunityType = "Existing Customer",
                        CoverageStatus = "Quoted",
                        QuoteCount = 1,
                        Chain = new CrmConversionChainDto
                        {
                            OpportunityName = "Metro rollout"
                        }
                    }
                }
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/crm/conversions?months=6&opportunityType=Existing%20Customer&businessPartnerId={businessPartnerId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var conversions = await response.Content.ReadFromJsonAsync<CrmConversionsDto>();
        conversions.Should().NotBeNull();
        conversions!.LeadCount.Should().Be(8);
        conversions.OpportunityToQuoteRate.Should().Be(60m);
        conversions.Funnel.Should().ContainSingle(x => x.Stage == "Quote");
    }

    [Fact]
    public async Task GetReporting_ShouldReturnCrmReporting()
    {
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetReportingAsync(6))
            .ReturnsAsync(new CrmReportingDto
            {
                TotalLeadCount = 12,
                ConvertedLeadCount = 4,
                LeadConversionRate = 33.3m,
                WeightedPipelineValue = 780000m,
                QuoteAcceptanceRate = 50m,
                AverageAccountHealthScore = 71.2m
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/crm/reports?take=6");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<CrmReportingDto>();
        report.Should().NotBeNull();
        report!.LeadConversionRate.Should().Be(33.3m);
        report.WeightedPipelineValue.Should().Be(780000m);
        report.AverageAccountHealthScore.Should().Be(71.2m);
    }

    [Fact]
    public async Task CreateLead_ShouldReturnCreatedLead()
    {
        var leadId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.CreateLeadAsync(It.IsAny<CreateCrmLeadDto>()))
            .ReturnsAsync(new CrmLeadDetailDto
            {
                LeadId = leadId,
                FirstName = "Naa",
                LastName = "Ofori",
                FullName = "Naa Ofori",
                LeadStatus = "New"
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/crm/leads", new CreateCrmLeadDto
        {
            FirstName = "Naa",
            LastName = "Ofori",
            LeadSource = "Referral",
            LeadStatus = "New"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var lead = await response.Content.ReadFromJsonAsync<CrmLeadDetailDto>();
        lead.Should().NotBeNull();
        lead!.LeadId.Should().Be(leadId);
        lead.FullName.Should().Be("Naa Ofori");
    }

    [Fact]
    public async Task GetOpportunities_ShouldReturnPagedCrmOpportunities()
    {
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetOpportunitiesAsync(
                1, 20, "atlas", "Negotiation", null, null, "Renewal",
                null, null, null, null))
            .ReturnsAsync(new PagedResult<CrmOpportunityListItemDto>
            {
                Items = new List<CrmOpportunityListItemDto>
                {
                    new()
                    {
                        OpportunityId = Guid.NewGuid(),
                        Name = "Atlas corridor expansion",
                        Stage = "Negotiation",
                        Amount = 250000m,
                        Probability = 70,
                        Currency = "USD",
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(12),
                        OpportunityType = "Renewal"
                    }
                },
                Page = 1,
                PageSize = 20,
                TotalCount = 1
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/crm/opportunities?page=1&pageSize=20&search=atlas&stage=Negotiation&opportunityType=Renewal");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var opportunities = await response.Content.ReadFromJsonAsync<PagedResult<CrmOpportunityListItemDto>>();
        opportunities.Should().NotBeNull();
        opportunities!.TotalCount.Should().Be(1);
        opportunities.Items.Should().ContainSingle(x => x.Name == "Atlas corridor expansion" && x.OpportunityType == "Renewal");
    }

    [Fact]
    public async Task GetActivities_ShouldReturnPagedCrmActivities()
    {
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetActivitiesAsync(1, 15, "review", "Planned", "Call", true, null, null, null))
            .ReturnsAsync(new PagedResult<CrmActivityListItemDto>
            {
                Items = new List<CrmActivityListItemDto>
                {
                    new()
                    {
                        ActivityId = Guid.NewGuid(),
                        Subject = "Executive review call",
                        ActivityType = "Call",
                        ActivityStatus = "Planned",
                        ActivityDate = DateTime.UtcNow.AddDays(-1),
                        DueDate = DateTime.UtcNow.AddDays(2),
                        RequiresFollowUp = true,
                        Priority = 1,
                        IsOverdue = false,
                        CreatedAt = DateTime.UtcNow.AddDays(-2)
                    }
                },
                Page = 1,
                PageSize = 15,
                TotalCount = 1
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/crm/activities?page=1&pageSize=15&search=review&status=Planned&activityType=Call&followUpOnly=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var activities = await response.Content.ReadFromJsonAsync<PagedResult<CrmActivityListItemDto>>();
        activities.Should().NotBeNull();
        activities!.TotalCount.Should().Be(1);
        activities.Items.Should().ContainSingle(x => x.Subject == "Executive review call");
    }

    [Fact]
    public async Task GetActivity_ShouldReturnCrmActivityDetail()
    {
        var activityId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetActivityByIdAsync(activityId))
            .ReturnsAsync(new CrmActivityDetailDto
            {
                ActivityId = activityId,
                Subject = "Customer workshop",
                ActivityType = "Meeting",
                ActivityStatus = "In Progress",
                ActivityDate = DateTime.UtcNow,
                Priority = 2,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/crm/activities/{activityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var activity = await response.Content.ReadFromJsonAsync<CrmActivityDetailDto>();
        activity.Should().NotBeNull();
        activity!.ActivityId.Should().Be(activityId);
        activity.Subject.Should().Be("Customer workshop");
    }

    [Fact]
    public async Task CreateActivity_ShouldReturnCreatedActivity()
    {
        var activityId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.CreateActivityAsync(It.IsAny<CreateCrmActivityDto>()))
            .ReturnsAsync(new CrmActivityDetailDto
            {
                ActivityId = activityId,
                Subject = "Pipeline call",
                ActivityType = "Call",
                ActivityStatus = "Planned",
                ActivityDate = DateTime.UtcNow,
                Priority = 1,
                CreatedAt = DateTime.UtcNow
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/crm/activities", new CreateCrmActivityDto
        {
            Subject = "Pipeline call",
            ActivityType = "Call",
            ActivityStatus = "Planned",
            ActivityDate = DateTime.UtcNow,
            Priority = 1
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var activity = await response.Content.ReadFromJsonAsync<CrmActivityDetailDto>();
        activity.Should().NotBeNull();
        activity!.ActivityId.Should().Be(activityId);
        activity.ActivityType.Should().Be("Call");
    }

    [Fact]
    public async Task GetQuotes_ShouldReturnPagedCrmQuotes()
    {
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetQuotesAsync(1, 15, "atlas", "Sent", null, null, null))
            .ReturnsAsync(new PagedResult<CrmQuoteListItemDto>
            {
                Items = new List<CrmQuoteListItemDto>
                {
                    new()
                    {
                        QuoteId = Guid.NewGuid(),
                        OpportunityId = Guid.NewGuid(),
                        QuoteName = "Atlas proposal pack",
                        QuoteStatus = "Sent",
                        Value = 260000m,
                        Currency = "USD",
                        ValidUntil = DateTime.UtcNow.AddDays(12),
                        DocumentNumber = "Q-ATLAS-1",
                        DocumentDate = DateTime.UtcNow.AddDays(-2),
                        CreatedAt = DateTime.UtcNow.AddDays(-3)
                    }
                },
                Page = 1,
                PageSize = 15,
                TotalCount = 1
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/crm/quotes?page=1&pageSize=15&search=atlas&status=Sent");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var quotes = await response.Content.ReadFromJsonAsync<PagedResult<CrmQuoteListItemDto>>();
        quotes.Should().NotBeNull();
        quotes!.TotalCount.Should().Be(1);
        quotes.Items.Should().ContainSingle(x => x.QuoteName == "Atlas proposal pack");
    }

    [Fact]
    public async Task GetQuote_ShouldReturnCrmQuoteDetail()
    {
        var quoteId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetQuoteByIdAsync(quoteId))
            .ReturnsAsync(new CrmQuoteDetailDto
            {
                QuoteId = quoteId,
                OpportunityId = Guid.NewGuid(),
                QuoteName = "Atlas final quote",
                QuoteStatus = "Accepted",
                Value = 275000m,
                Currency = "USD",
                ValidUntil = DateTime.UtcNow.AddDays(8),
                DocumentNumber = "Q-ATLAS-2",
                DocumentDate = DateTime.UtcNow.AddDays(-1),
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/crm/quotes/{quoteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var quote = await response.Content.ReadFromJsonAsync<CrmQuoteDetailDto>();
        quote.Should().NotBeNull();
        quote!.QuoteId.Should().Be(quoteId);
        quote.QuoteStatus.Should().Be("Accepted");
    }

    [Fact]
    public async Task GetProjects_ShouldReturnPagedCrmProjects()
    {
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetProjectsAsync(1, 12, "atlas", "InProgress", null, null))
            .ReturnsAsync(new PagedResult<CrmProjectListItemDto>
            {
                Items = new List<CrmProjectListItemDto>
                {
                    new()
                    {
                        ProjectId = Guid.NewGuid(),
                        ProjectCode = "PRJ-ATLAS-1",
                        Title = "Atlas rollout",
                        Status = "InProgress",
                        Value = 210000m,
                        ProgressPercent = 35m,
                        IsOverdue = false,
                        IsLinkedToActiveContract = true
                    }
                },
                Page = 1,
                PageSize = 12,
                TotalCount = 1
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/crm/projects?page=1&pageSize=12&search=atlas&status=InProgress");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var projects = await response.Content.ReadFromJsonAsync<PagedResult<CrmProjectListItemDto>>();
        projects.Should().NotBeNull();
        projects!.TotalCount.Should().Be(1);
        projects.Items.Should().ContainSingle(x => x.Title == "Atlas rollout");
    }

    [Fact]
    public async Task GetProject_ShouldReturnCrmProjectDetail()
    {
        var projectId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetProjectByIdAsync(projectId))
            .ReturnsAsync(new CrmProjectDetailDto
            {
                ProjectId = projectId,
                ProjectCode = "PRJ-ATLAS-2",
                Title = "Atlas commissioning",
                Status = "Approved",
                Value = 180000m,
                ProgressPercent = 15m,
                Methodology = "Hybrid",
                BudgetStatus = "OnTrack"
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/crm/projects/{projectId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var project = await response.Content.ReadFromJsonAsync<CrmProjectDetailDto>();
        project.Should().NotBeNull();
        project!.ProjectId.Should().Be(projectId);
        project.Methodology.Should().Be("Hybrid");
    }

    [Fact]
    public async Task GetContracts_ShouldReturnPagedCrmContracts()
    {
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetContractsAsync(1, 12, "harbor", "Active", null, true))
            .ReturnsAsync(new PagedResult<CrmContractListItemDto>
            {
                Items = new List<CrmContractListItemDto>
                {
                    new()
                    {
                        ContractId = Guid.NewGuid(),
                        ContractNumber = "CTR-9001",
                        ContractTitle = "Harbor operations",
                        Status = "Active",
                        ContractValue = 480000m,
                        BusinessPartnerId = Guid.NewGuid(),
                        TenderAwardId = Guid.NewGuid(),
                        TenderId = Guid.NewGuid(),
                        RelationshipType = "Account",
                        Currency = "USD",
                        ContractType = "Service",
                        ProjectCount = 1,
                        ActiveProjectCount = 1,
                        IsActive = true,
                        IsExpiringSoon = true
                    }
                },
                Page = 1,
                PageSize = 12,
                TotalCount = 1
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/crm/contracts?page=1&pageSize=12&search=harbor&status=Active&expiringOnly=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contracts = await response.Content.ReadFromJsonAsync<PagedResult<CrmContractListItemDto>>();
        contracts.Should().NotBeNull();
        contracts!.TotalCount.Should().Be(1);
        contracts.Items.Should().ContainSingle(x => x.ContractNumber == "CTR-9001");
    }

    [Fact]
    public async Task GetContract_ShouldReturnCrmContractDetail()
    {
        var contractId = Guid.NewGuid();
        var crmService = new Mock<ICrmService>();
        crmService
            .Setup(x => x.GetContractByIdAsync(contractId))
            .ReturnsAsync(new CrmContractDetailDto
            {
                ContractId = contractId,
                ContractNumber = "CTR-9002",
                ContractTitle = "Harbor renewal",
                Status = "Active",
                ContractValue = 510000m,
                BusinessPartnerId = Guid.NewGuid(),
                TenderAwardId = Guid.NewGuid(),
                TenderId = Guid.NewGuid(),
                RelationshipType = "Account",
                Currency = "USD",
                ContractType = "Service",
                ProjectCount = 2,
                ActiveProjectCount = 1,
                IsActive = true,
                IsExpiringSoon = true
            });

        using var factory = CreateFactory(crmService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/crm/contracts/{contractId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contract = await response.Content.ReadFromJsonAsync<CrmContractDetailDto>();
        contract.Should().NotBeNull();
        contract!.ContractId.Should().Be(contractId);
        contract.ContractTitle.Should().Be("Harbor renewal");
    }

    private static WebApplicationFactory<Program> CreateFactory(
        Mock<ICrmService> crmService,
        ClaimsPrincipal? principal = null)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IAuthorizationHandler>();
                services.RemoveAll<ICrmService>();
                services.RemoveAll<ICurrentUserProvider>();

                services.AddSingleton<IPolicyEvaluator>(new TestPolicyEvaluator(principal ?? CreatePrincipal()));
                services.AddSingleton<IAuthorizationHandler, AllowAnonymousHandler>();
                services.AddSingleton(crmService.Object);
                services.AddSingleton<ICurrentUserProvider>(new FakeCurrentUserProvider(Guid.NewGuid(), Guid.NewGuid()));
            });
        });

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "Test"));
}

internal sealed class TestPolicyEvaluator : IPolicyEvaluator
{
    private readonly ClaimsPrincipal _principal;

    public TestPolicyEvaluator(ClaimsPrincipal principal)
    {
        _principal = principal;
    }

    public Task<AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, HttpContext context)
        => Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(_principal, "Test")));

    public Task<PolicyAuthorizationResult> AuthorizeAsync(AuthorizationPolicy policy, AuthenticateResult authenticationResult, HttpContext context, object? resource)
        => Task.FromResult(PolicyAuthorizationResult.Success());
}

internal sealed class AllowAnonymousHandler : IAuthorizationHandler
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
