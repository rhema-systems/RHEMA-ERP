using System.Linq.Expressions;
using ErpSystem.Core.DTOs.Crm;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Crm;
using FluentAssertions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Crm;

public class CrmServiceTests
{
    [Fact]
    public async Task GetLeadByIdAsync_ShouldExposeLinkedPropertyEnquiryCurrency()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.Leads.Add(new Lead
        {
            Id = leadId,
            TenantId = tenantId,
            FirstName = "Akosua",
            LastName = "Mensah",
            LeadStatus = "Qualified",
            LeadSource = "Property Enquiry"
        });
        fixture.EhcTickets.Add(new EhcTicket
        {
            Id = ticketId,
            TenantId = tenantId,
            TicketNumber = "EHC-PE-0007",
            TicketType = EhcTicketType.Enquiry,
            Description = "Published plot enquiry"
        });
        fixture.PropertyEnquiryProspects.Add(new EhcPropertyEnquiryProspect
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TicketId = ticketId,
            LeadId = leadId,
            AgreedAmount = 275000m,
            Currency = "GHS"
        });

        var result = await fixture.CreateService().GetLeadByIdAsync(leadId);

        result.Should().NotBeNull();
        result!.PropertyEnquiryTicketId.Should().Be(ticketId);
        result.PropertyEnquiryTicketNumber.Should().Be("EHC-PE-0007");
        result.PropertyEnquiryCurrency.Should().Be("GHS");
    }

    [Fact]
    public async Task GetOverviewAsync_ShouldAggregatePipelineAndAccountSignals()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-001",
            PartnerName = "Northwind Civic Works",
            PartnerType = "Customer",
            RegistrationStatus = "Approved",
            RiskLevel = "High",
            PerformanceRating = 2.8m,
            SalesTerritory = "Public Sector"
        });
        fixture.Leads.Add(new Lead
        {
            Id = leadId,
            TenantId = tenantId,
            FirstName = "Amina",
            LastName = "Boateng",
            CompanyName = "Northwind Civic Works",
            LeadStatus = "Qualified",
            LeadSource = "Referral",
            NextFollowUpDate = DateTime.UtcNow.AddDays(3)
        });
        fixture.Opportunities.Add(new Opportunity
        {
            Id = opportunityId,
            TenantId = tenantId,
            Name = "Regional rollout",
            Stage = "Proposal",
            Amount = 500000m,
            Probability = 60,
            Currency = "USD",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(21),
            OpportunityType = "New Business",
            LeadSource = "Referral",
            LeadId = leadId
        });
        fixture.Quotes.Add(new Quote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OpportunityId = opportunityId,
            QuoteName = "Northwind proposal",
            QuoteStatus = "Sent",
            SubTotal = 500000m,
            DiscountAmount = 25000m,
            ShippingAmount = 0m,
            TotalAmount = 475000m
        });
        fixture.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Subject = "Executive follow-up call",
            ActivityType = "Call",
            ActivityStatus = "Planned",
            DueDate = DateTime.UtcNow.AddDays(2),
            RequiresFollowUp = true,
            OpportunityId = opportunityId
        });
        fixture.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-CRM-001",
            Title = "Northwind Delivery",
            Status = ProjectStatuses.InProgress,
            BusinessPartnerId = partnerId,
            ApprovedBudget = 220000m,
            TargetEndDate = DateTime.UtcNow.AddDays(-2)
        });
        fixture.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderAwardId = Guid.NewGuid(),
            TenderId = Guid.NewGuid(),
            BusinessPartnerId = partnerId,
            ContractNumber = "CTR-001",
            ContractTitle = "Northwind Master Services",
            Status = "Active",
            ContractValue = 350000m,
            EndDate = DateTime.UtcNow.AddDays(20)
        });
        fixture.TenderBids.Add(new TenderBid
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = Guid.NewGuid(),
            BusinessPartnerId = partnerId,
            BidNumber = "BID-001",
            Status = "Submitted",
            TotalBidAmount = 275000m
        });
        fixture.TenderAwards.Add(new TenderAward
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = Guid.NewGuid(),
            TenderBidId = Guid.NewGuid(),
            BusinessPartnerId = partnerId,
            AwardedAmount = 250000m,
            OriginalBidAmount = 275000m
        });

        var service = fixture.CreateService();

        var result = await service.GetOverviewAsync(10);

        result.TotalLeadCount.Should().Be(1);
        result.QualifiedLeadCount.Should().Be(1);
        result.LeadsNeedingFollowUpCount.Should().Be(1);
        result.OpenOpportunityCount.Should().Be(1);
        result.OpenOpportunityValue.Should().Be(500000m);
        result.WeightedPipelineValue.Should().Be(300000m);
        result.ActiveQuoteCount.Should().Be(1);
        result.ActiveQuoteValue.Should().Be(475000m);
        result.ActiveAccountCount.Should().Be(1);
        result.AtRiskAccountCount.Should().Be(1);
        result.ActiveProjectCount.Should().Be(1);
        result.ActiveContractCount.Should().Be(1);
        result.ExpiringContractCount.Should().Be(1);
        result.FollowUps.Should().HaveCount(2);
        result.Opportunities.Should().ContainSingle(x => x.OpportunityId == opportunityId && x.WeightedValue == 300000m);
        result.Accounts.Should().ContainSingle(x =>
            x.BusinessPartnerId == partnerId
            && x.ActiveProjectCount == 1
            && x.ActiveContractCount == 1
            && x.TenderAwardCount == 1
            && x.IsAtRisk);
        var account = result.Accounts.Single();
        account.ProjectValuesByCurrency.Should().BeEmpty();
        account.ProjectsWithoutCurrencyCount.Should().Be(1);
        account.ContractValuesByCurrency.Should().ContainSingle(x => x.Currency == "USD" && x.Amount == 350000m);
        account.TenderAwardedValuesByCurrency.Should().ContainSingle(x => x.Currency == "USD" && x.Amount == 250000m);
    }

    [Fact]
    public async Task GetOverviewAsync_SeparatesCurrenciesAcrossThePipeline()
    {
        var tenantId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, Guid.NewGuid());
        var usdOpportunityId = Guid.NewGuid();
        var ghsOpportunityId = Guid.NewGuid();
        fixture.Opportunities.AddRange(new[]
        {
            new Opportunity
            {
                Id = usdOpportunityId, TenantId = tenantId, Name = "USD deal", Stage = "Proposal",
                Amount = 100m, Probability = 50, Currency = "USD", ExpectedCloseDate = DateTime.UtcNow.AddDays(7)
            },
            new Opportunity
            {
                Id = ghsOpportunityId, TenantId = tenantId, Name = "GHS deal", Stage = "Proposal",
                Amount = 200m, Probability = 25, Currency = "GHS", ExpectedCloseDate = DateTime.UtcNow.AddDays(8)
            }
        });
        fixture.Quotes.AddRange(new[]
        {
            new Quote
            {
                Id = Guid.NewGuid(), TenantId = tenantId, OpportunityId = usdOpportunityId,
                QuoteStatus = "Sent", Currency = "USD", TotalAmount = 90m
            },
            new Quote
            {
                Id = Guid.NewGuid(), TenantId = tenantId, OpportunityId = ghsOpportunityId,
                QuoteStatus = "Sent", Currency = "GHS", TotalAmount = 180m
            }
        });

        var result = await fixture.CreateService().GetOverviewAsync(1);

        result.Opportunities.Should().HaveCount(2);
        result.OpenOpportunityCount.Should().Be(2);
        result.OpenOpportunityValuesByCurrency.Should().ContainSingle(x => x.Currency == "GHS" && x.Amount == 200m);
        result.OpenOpportunityValuesByCurrency.Should().ContainSingle(x => x.Currency == "USD" && x.Amount == 100m);
        result.WeightedPipelineValuesByCurrency.Should().ContainSingle(x => x.Currency == "GHS" && x.Amount == 50m);
        result.WeightedPipelineValuesByCurrency.Should().ContainSingle(x => x.Currency == "USD" && x.Amount == 50m);
        result.ActiveQuoteValuesByCurrency.Should().ContainSingle(x => x.Currency == "GHS" && x.Amount == 180m);
        result.ActiveQuoteValuesByCurrency.Should().ContainSingle(x => x.Currency == "USD" && x.Amount == 90m);
    }

    [Fact]
    public async Task GetAccountDetailAsync_ShouldReturnLinkedAccountContext()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-200",
            PartnerName = "Atlas Infrastructure",
            PartnerType = "Customer",
            RegistrationStatus = "Approved",
            CustomerType = "Government",
            SalesTerritory = "West Africa",
            RiskLevel = "Medium",
            Currency = Guid.NewGuid().ToString(),
            PrimaryContactName = "Irene Mensah",
            PrimaryEmail = "irene@atlas.test",
            Contacts =
            {
                new BusinessPartnerContact
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BusinessPartnerId = partnerId,
                    ContactName = "Irene Mensah",
                    Email = "irene@atlas.test",
                    IsPrimary = true
                }
            }
        });
        fixture.Leads.Add(new Lead
        {
            Id = leadId,
            TenantId = tenantId,
            FirstName = "Kojo",
            LastName = "Asare",
            CompanyName = "Atlas Infrastructure",
            LeadStatus = "Converted",
            LeadSource = "Tender",
            ConvertedCustomerId = partnerId,
            ConvertedDate = DateTime.UtcNow.AddDays(-7)
        });
        fixture.Opportunities.Add(new Opportunity
        {
            Id = opportunityId,
            TenantId = tenantId,
            Name = "Bridge corridor expansion",
            CustomerId = partnerId,
            LeadId = leadId,
            Stage = "Negotiation",
            Amount = 1200000m,
            Probability = 70,
            Currency = "USD",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(15),
            OpportunityType = "New Business",
            LeadSource = "Tender"
        });
        fixture.Quotes.Add(new Quote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OpportunityId = opportunityId,
            CustomerId = partnerId,
            QuoteName = "Atlas commercial pack",
            QuoteStatus = "Sent",
            TotalAmount = 1180000m,
            ValidUntil = DateTime.UtcNow.AddDays(10),
            Currency = "USD"
        });
        fixture.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Subject = "Commercial review",
            ActivityType = "Meeting",
            ActivityStatus = "Planned",
            DueDate = DateTime.UtcNow.AddDays(5),
            OpportunityId = opportunityId,
            CustomerId = partnerId
        });
        fixture.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Subject = "Lead qualification follow-up",
            ActivityType = "Call",
            ActivityStatus = "Planned",
            DueDate = DateTime.UtcNow.AddDays(3),
            LeadId = leadId,
            RequiresFollowUp = true
        });
        fixture.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-ATLAS-1",
            Title = "Atlas Mobilization",
            Status = ProjectStatuses.InProgress,
            BusinessPartnerId = partnerId,
            ApprovedBudget = 500000m,
            ProgressPercent = 42m,
            TargetEndDate = DateTime.UtcNow.AddDays(90)
        });
        fixture.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderAwardId = Guid.NewGuid(),
            TenderId = Guid.NewGuid(),
            BusinessPartnerId = partnerId,
            ContractNumber = "CTR-ATLAS",
            ContractTitle = "Atlas Frame Contract",
            Status = "Active",
            ContractValue = 850000m,
            EndDate = DateTime.UtcNow.AddDays(25)
        });
        fixture.TenderInvitations.Add(new TenderInvitation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = Guid.NewGuid(),
            BusinessPartnerId = partnerId,
            Status = "Viewed"
        });

        var service = fixture.CreateService();

        var result = await service.GetAccountDetailAsync(partnerId, 10);

        result.Should().NotBeNull();
        result!.PartnerName.Should().Be("Atlas Infrastructure");
        result.OpenOpportunityCount.Should().Be(1);
        result.ActiveQuoteCount.Should().Be(1);
        result.ActiveProjectCount.Should().Be(1);
        result.ActiveContractCount.Should().Be(1);
        result.RelatedLeadCount.Should().Be(1);
        result.Currency.Should().Be("USD");
        result.Contacts.Should().ContainSingle(x => x.ContactName == "Irene Mensah");
        result.Opportunities.Should().ContainSingle(x => x.BusinessPartnerId == partnerId);
        result.Quotes.Should().ContainSingle(x => x.Value == 1180000m);
        result.Activities.Should().Contain(x => x.Subject == "Commercial review");
        result.Activities.Should().Contain(x => x.Subject == "Lead qualification follow-up");
    }

    [Fact]
    public async Task GetContactsAsync_ShouldFlattenBusinessPartnerContactsWithAccountHealthContext()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);
        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-CONTACT-1",
            PartnerName = "Atlas Relationship Hub",
            PartnerType = "Customer",
            RegistrationStatus = "Approved",
            SalesTerritory = "Central",
            Contacts =
            {
                new BusinessPartnerContact
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BusinessPartnerId = partnerId,
                    ContactName = "Irene Mensah",
                    ContactTitle = "Commercial Director",
                    Department = "Commercial",
                    Email = "irene@atlas.test",
                    Phone = "+2335551000",
                    IsPrimary = true
                },
                new BusinessPartnerContact
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BusinessPartnerId = partnerId,
                    ContactName = "Noah Tetteh",
                    ContactTitle = "Delivery Lead",
                    Department = "Operations",
                    Email = "noah@atlas.test",
                    Mobile = "+2335552000",
                    IsPrimary = false
                }
            }
        });
        fixture.Opportunities.Add(new Opportunity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Atlas service expansion",
            CustomerId = partnerId,
            Stage = "Proposal",
            Amount = 220000m,
            Probability = 60,
            Currency = "USD",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(18),
            OpportunityType = "New Business",
            LeadSource = "Referral"
        });
        fixture.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            TenderAwardId = Guid.NewGuid(),
            TenderId = Guid.NewGuid(),
            ContractNumber = "CTR-CONTACT-1",
            ContractTitle = "Atlas support agreement",
            Status = "Active",
            ContractValue = 150000m,
            EndDate = DateTime.UtcNow.AddDays(25)
        });

        var service = fixture.CreateService();

        var result = await service.GetContactsAsync(1, 10, "atlas", partnerId, "Commercial", true, false, "Customer");

        result.TotalCount.Should().Be(1);
        var contact = result.Items.Should().ContainSingle().Subject;
        contact.BusinessPartnerId.Should().Be(partnerId);
        contact.ContactName.Should().Be("Irene Mensah");
        contact.Department.Should().Be("Commercial");
        contact.IsPrimary.Should().BeTrue();
        contact.PartnerName.Should().Be("Atlas Relationship Hub");
        contact.OpenOpportunityCount.Should().Be(1);
        contact.ActiveContractCount.Should().Be(1);
        contact.HealthCategory.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetReadinessAsync_ShouldReturnQualificationCountsAndSignals()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-READY-1",
            PartnerName = "Atlas Qualification Hub",
            PartnerType = "Customer",
            RegistrationStatus = "Approved",
            Documents =
            {
                new BusinessPartnerDocument
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BusinessPartnerId = partnerId,
                    DocumentType = "Insurance",
                    DocumentName = "Insurance Cover",
                    IsVerified = true,
                    ExpiryDate = DateTime.UtcNow.AddDays(20)
                }
            },
            Licenses =
            {
                new BusinessPartnerLicense
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BusinessPartnerId = partnerId,
                    LicenseTypeId = Guid.NewGuid(),
                    LicenseType = new LicenseType
                    {
                        Id = Guid.NewGuid(),
                        LicenseName = "ISO 9001"
                    },
                    LicenseNumber = "ISO-9001-ATLAS",
                    Status = "Active",
                    ExpiryDate = DateTime.UtcNow.AddDays(30)
                }
            },
            Financials =
            {
                new BusinessPartnerFinancial
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BusinessPartnerId = partnerId,
                    FiscalYear = DateTime.UtcNow.Year - 1,
                    AnnualRevenue = 1250000m,
                    NetProfit = 210000m,
                    CreditRating = "A",
                    AuditorName = "KPMG"
                }
            }
        });
        fixture.Opportunities.Add(new Opportunity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Atlas readiness-backed pursuit",
            CustomerId = partnerId,
            Stage = "Proposal",
            Amount = 340000m,
            Probability = 55,
            Currency = "USD",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(21),
            OpportunityType = "New Business",
            LeadSource = "Referral"
        });
        fixture.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            TenderAwardId = Guid.NewGuid(),
            TenderId = Guid.NewGuid(),
            ContractNumber = "CTR-READY-1",
            ContractTitle = "Atlas enablement contract",
            Status = "Active",
            ContractValue = 180000m
        });

        var service = fixture.CreateService();

        var result = await service.GetReadinessAsync(1, 10, "atlas", null, true, false, "Customer");

        result.TotalCount.Should().Be(1);
        var readiness = result.Items.Should().ContainSingle().Subject;
        readiness.BusinessPartnerId.Should().Be(partnerId);
        readiness.DocumentCount.Should().Be(1);
        readiness.VerifiedDocumentCount.Should().Be(1);
        readiness.ExpiringDocumentCount.Should().Be(1);
        readiness.LicenseCount.Should().Be(1);
        readiness.ExpiringLicenseCount.Should().Be(1);
        readiness.FinancialRecordCount.Should().Be(1);
        readiness.LatestFinancialYear.Should().Be(DateTime.UtcNow.Year - 1);
        readiness.OpenOpportunityCount.Should().Be(1);
    }

    [Fact]
    public async Task GetReadinessDetailAsync_ShouldReturnEvidenceListsAndSignals()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-READY-2",
            PartnerName = "Blue Harbor Readiness",
            PartnerType = "Customer",
            RegistrationStatus = "Approved",
            PrimaryContactName = "Irene Mensah",
            PrimaryEmail = "irene@blueharbor.test",
            Documents =
            {
                new BusinessPartnerDocument
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BusinessPartnerId = partnerId,
                    DocumentType = "TaxCertificate",
                    DocumentName = "Tax Clearance",
                    IsVerified = false,
                    ExpiryDate = DateTime.UtcNow.AddDays(-2)
                }
            },
            Licenses =
            {
                new BusinessPartnerLicense
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BusinessPartnerId = partnerId,
                    LicenseTypeId = Guid.NewGuid(),
                    LicenseType = new LicenseType
                    {
                        Id = Guid.NewGuid(),
                        LicenseName = "Service License"
                    },
                    LicenseNumber = "SERV-ATLAS",
                    Status = "Expired",
                    ExpiryDate = DateTime.UtcNow.AddDays(-5)
                }
            },
            Financials =
            {
                new BusinessPartnerFinancial
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BusinessPartnerId = partnerId,
                    FiscalYear = DateTime.UtcNow.Year - 3,
                    AnnualRevenue = 850000m,
                    NetProfit = 60000m,
                    CreditRating = "B"
                }
            }
        });

        var service = fixture.CreateService();

        var result = await service.GetReadinessDetailAsync(partnerId, 10);

        result.Should().NotBeNull();
        result!.PartnerName.Should().Be("Blue Harbor Readiness");
        result.PrimaryContactName.Should().Be("Irene Mensah");
        result.Documents.Should().ContainSingle(x => x.DocumentType == "TaxCertificate" && x.IsExpired);
        result.Licenses.Should().ContainSingle(x => x.Status == "Expired" && x.IsExpired);
        result.Financials.Should().ContainSingle(x => x.FinancialYear == DateTime.UtcNow.Year - 3);
        result.Signals.Should().NotBeEmpty();
        result.HasCriticalGap.Should().BeTrue();
    }

    [Fact]
    public async Task GetRiskAsync_ShouldReturnEscalationCountsAndRiskSignals()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-RISK-1",
            PartnerName = "Atlas Risk Hub",
            PartnerType = "Customer",
            RegistrationStatus = "Approved",
            RiskLevel = "High",
            PerformanceRating = 2.4m,
            IsOnCreditHold = true
        });
        fixture.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            TenderAwardId = Guid.NewGuid(),
            TenderId = Guid.NewGuid(),
            ContractNumber = "CTR-RISK-1",
            ContractTitle = "Atlas support contract",
            Status = "Active",
            ContractValue = 175000m,
            EndDate = DateTime.UtcNow.AddDays(18)
        });
        fixture.QualityIncidents.Add(new QualityIncident
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            IncidentNumber = "QI-001",
            IncidentDate = DateTime.UtcNow.AddDays(-14),
            IncidentType = "ServiceFailure",
            Severity = "Critical",
            Description = "Major service outage",
            Status = "Open",
            RequiresSupplierResponse = true,
            FinancialImpact = 25000m
        });
        fixture.SupplierPerformanceMetrics.Add(new SupplierPerformanceMetric
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            MetricPeriod = "Quarterly",
            Year = DateTime.UtcNow.Year,
            Quarter = 1,
            OverallPerformanceScore = 54m,
            PerformanceGrade = "C",
            ComplianceScore = 62m,
            ComplaintsReceived = 4,
            ComplaintsResolved = 1,
            ContractViolations = 2,
            CalculatedAt = DateTime.UtcNow.AddDays(-5)
        });
        fixture.PerformanceReviews.Add(new PerformanceReview
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            ReviewNumber = "REV-001",
            ReviewDate = DateTime.UtcNow.AddDays(-10),
            ReviewPeriod = "Q1 2026",
            ReviewedById = userId,
            PeriodStartDate = DateTime.UtcNow.AddMonths(-3),
            PeriodEndDate = DateTime.UtcNow,
            OverallScore = 2.5m,
            Status = "Submitted",
            Strengths = "Responsive escalation team",
            AreasForImprovement = "Compliance drift",
            RequiresFollowUp = true,
            FollowUpDate = DateTime.UtcNow.AddDays(7)
        });
        fixture.BlacklistAppeals.Add(new BlacklistAppeal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            AppealNumber = "APL-001",
            AppealDate = DateTime.UtcNow.AddDays(-2),
            Status = "Pending",
            AppealReason = "Requesting reconsideration"
        });

        var service = fixture.CreateService();

        var result = await service.GetRiskAsync(1, 10, "atlas", "Critical", true, true, "Customer");

        result.TotalCount.Should().Be(1);
        var risk = result.Items.Should().ContainSingle().Subject;
        risk.BusinessPartnerId.Should().Be(partnerId);
        risk.OpenIncidentCount.Should().Be(1);
        risk.CriticalIncidentCount.Should().Be(1);
        risk.PendingAppealCount.Should().Be(1);
        risk.OpenReviewFollowUpCount.Should().Be(1);
        risk.IsOnCreditHold.Should().BeTrue();
        risk.RequiresEscalation.Should().BeTrue();
        risk.RiskCategory.Should().Be("Critical");
    }

    [Fact]
    public async Task GetRiskDetailAsync_ShouldReturnOperationalRiskEvidence()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-RISK-2",
            PartnerName = "Blue Harbor Risk",
            PartnerType = "Customer",
            RegistrationStatus = "Approved",
            PrimaryContactName = "Irene Mensah",
            PrimaryEmail = "irene@blueharbor.test",
            RiskLevel = "Medium",
            IsBlacklisted = true
        });
        fixture.SupplierPerformanceMetrics.Add(new SupplierPerformanceMetric
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            MetricPeriod = "Monthly",
            Year = DateTime.UtcNow.Year,
            Month = DateTime.UtcNow.Month,
            OverallPerformanceScore = 71m,
            PerformanceGrade = "B",
            ComplianceScore = 88m,
            QualityAcceptanceRate = 93m,
            OnTimeDeliveryRate = 84m,
            CalculatedAt = DateTime.UtcNow.AddDays(-3)
        });
        fixture.QualityIncidents.Add(new QualityIncident
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            IncidentNumber = "QI-200",
            IncidentDate = DateTime.UtcNow.AddDays(-40),
            IncidentType = "Delay",
            Severity = "High",
            Description = "Escalated delivery delay",
            Status = "InProgress",
            RequiresSupplierResponse = true
        });
        fixture.PerformanceReviews.Add(new PerformanceReview
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            ReviewNumber = "REV-200",
            ReviewDate = DateTime.UtcNow.AddDays(-20),
            ReviewPeriod = "Q4 2025",
            ReviewedById = userId,
            PeriodStartDate = DateTime.UtcNow.AddMonths(-4),
            PeriodEndDate = DateTime.UtcNow.AddMonths(-1),
            OverallScore = 3.4m,
            Status = "Disputed",
            Strengths = "Commercial flexibility",
            AreasForImprovement = "Operational consistency",
            RequiresFollowUp = true,
            FollowUpDate = DateTime.UtcNow.AddDays(10)
        });
        fixture.BlacklistAppeals.Add(new BlacklistAppeal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            AppealNumber = "APL-200",
            AppealDate = DateTime.UtcNow.AddDays(-12),
            Status = "Approved",
            AppealReason = "Corrective action completed",
            ApprovedDate = DateTime.UtcNow.AddDays(-5),
            RemoveBlacklist = true
        });

        var service = fixture.CreateService();

        var result = await service.GetRiskDetailAsync(partnerId, 10);

        result.Should().NotBeNull();
        result!.PartnerName.Should().Be("Blue Harbor Risk");
        result.PrimaryContactName.Should().Be("Irene Mensah");
        result.IsBlacklisted.Should().BeTrue();
        result.PerformanceMetrics.Should().ContainSingle(x => x.PerformanceGrade == "B");
        result.Incidents.Should().ContainSingle(x => x.IncidentNumber == "QI-200" && x.Status == "InProgress");
        result.Reviews.Should().ContainSingle(x => x.ReviewNumber == "REV-200" && x.RequiresFollowUp);
        result.Appeals.Should().ContainSingle(x => x.AppealNumber == "APL-200" && x.Status == "Approved");
        result.Signals.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetCollaborationAsync_ShouldReturnPortalAccessAndOnboardingSignals()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-COLLAB-1",
            PartnerName = "Atlas Collaboration Hub",
            PartnerType = "Customer",
            RegistrationStatus = "Approved",
            SalesTerritory = "Central",
            PrimaryContactName = "Irene Mensah"
        });
        fixture.Opportunities.Add(new Opportunity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Atlas external rollout",
            CustomerId = partnerId,
            Stage = "Proposal",
            Amount = 210000m,
            Probability = 55,
            Currency = "USD",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(18),
            OpportunityType = "New Business",
            LeadSource = "Portal"
        });
        fixture.Contracts.Add(new Contract
        {
            Id = contractId,
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            TenderAwardId = Guid.NewGuid(),
            TenderId = Guid.NewGuid(),
            ContractNumber = "CTR-COLLAB-1",
            ContractTitle = "Atlas shared services",
            Status = "Active",
            ContractValue = 260000m,
            EndDate = DateTime.UtcNow.AddDays(45)
        });
        fixture.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContractId = contractId,
            ProjectCode = "PRJ-COLLAB-1",
            Title = "Atlas external workspace",
            Status = ProjectStatuses.InProgress,
            ExternalPortalAccessEnabled = true,
            ExternalCollaborationEnabled = true,
            TargetEndDate = DateTime.UtcNow.AddDays(60)
        });
        fixture.BusinessPartnerRegistrations.Add(new BusinessPartnerRegistration
        {
            Id = registrationId,
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            RegistrationNumber = "REG-ATLAS-1",
            ApplicantName = "Irene Mensah",
            ApplicantEmail = "irene@atlas.test",
            PartnerType = "Both",
            Status = "Submitted",
            SubmittedDate = DateTime.UtcNow.AddDays(-2),
            Documents =
            {
                new BusinessPartnerRegistrationDocument
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RegistrationId = registrationId,
                    DocumentType = "Insurance",
                    DocumentName = "Insurance Cover",
                    DocumentPath = "/docs/insurance.pdf"
                }
            },
            StatusHistory =
            {
                new BusinessPartnerRegistrationStatusHistory
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = registrationId,
                    ToStatus = "Submitted",
                    ChangedAt = DateTime.UtcNow.AddDays(-2)
                }
            }
        });

        var service = fixture.CreateService();

        var result = await service.GetCollaborationAsync(1, 10, "atlas", null, true, true, "Customer");

        result.TotalCount.Should().Be(1);
        var collaboration = result.Items.Should().ContainSingle().Subject;
        collaboration.BusinessPartnerId.Should().Be(partnerId);
        collaboration.LatestApplicationNumber.Should().Be("REG-ATLAS-1");
        collaboration.LatestRegistrationLifecycleStatus.Should().Be("Submitted");
        collaboration.RequiresEnablement.Should().BeTrue();
        collaboration.PortalUserCount.Should().Be(0);
        collaboration.ActivePortalUserCount.Should().Be(0);
        collaboration.RegistrationDocumentCount.Should().Be(1);
        collaboration.PortalProjectCount.Should().Be(1);
        collaboration.CollaborationProjectCount.Should().Be(1);
        collaboration.OpenOpportunityCount.Should().Be(1);
        collaboration.ActiveContractCount.Should().Be(1);
        collaboration.CollaborationCategory.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetCollaborationDetailAsync_ShouldReturnRegistrationsUsersAssignmentsAndProjects()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var tenderId = Guid.NewGuid();
        var portalUserId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-COLLAB-2",
            PartnerName = "Blue Harbor Collaboration",
            PartnerType = "Customer",
            RegistrationStatus = "Approved",
            PrimaryContactName = "Noah Tetteh",
            PrimaryEmail = "noah@blueharbor.test"
        });
        fixture.Opportunities.Add(new Opportunity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Blue Harbor external program",
            CustomerId = partnerId,
            Stage = "Negotiation",
            Amount = 310000m,
            Probability = 70,
            Currency = "USD",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(22),
            OpportunityType = "Renewal",
            LeadSource = "Relationship"
        });
        fixture.Contracts.Add(new Contract
        {
            Id = contractId,
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            TenderAwardId = Guid.NewGuid(),
            TenderId = tenderId,
            ContractNumber = "CTR-COLLAB-2",
            ContractTitle = "Blue Harbor collaboration contract",
            Status = "Active",
            ContractValue = 480000m,
            EndDate = DateTime.UtcNow.AddDays(120)
        });
        fixture.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContractId = contractId,
            ProjectCode = "PRJ-COLLAB-2",
            Title = "Blue Harbor portal launch",
            Status = ProjectStatuses.InProgress,
            ExternalPortalAccessEnabled = true,
            ExternalCollaborationEnabled = true,
            TargetEndDate = DateTime.UtcNow.AddDays(90)
        });

        var portalUser = new ApplicationUser
        {
            Id = portalUserId,
            UserName = "portal.blueharbor",
            Email = "portal@blueharbor.test",
            FirstName = "Noah",
            LastName = "Tetteh"
        };
        var assignedUser = new ApplicationUser
        {
            Id = assigneeId,
            UserName = "tender.noah",
            Email = "noah@blueharbor.test",
            FirstName = "Noah",
            LastName = "Tetteh"
        };

        fixture.BusinessPartnerRegistrations.Add(new BusinessPartnerRegistration
        {
            Id = registrationId,
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            RegistrationNumber = "REG-BLUE-1",
            ApplicantName = "Noah Tetteh",
            ApplicantEmail = "noah@blueharbor.test",
            PartnerType = "Customer",
            Status = "Approved",
            SubmittedDate = DateTime.UtcNow.AddDays(-10),
            ApprovedDate = DateTime.UtcNow.AddDays(-5),
            Documents =
            {
                new BusinessPartnerRegistrationDocument
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RegistrationId = registrationId,
                    DocumentType = "Profile",
                    DocumentName = "Profile Pack",
                    DocumentPath = "/docs/profile.pdf",
                    IsVerified = true
                }
            },
            StatusHistory =
            {
                new BusinessPartnerRegistrationStatusHistory
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = registrationId,
                    ToStatus = "Approved",
                    ChangedAt = DateTime.UtcNow.AddDays(-5)
                }
            }
        });
        fixture.BusinessPartnerUsers.Add(new BusinessPartnerUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            UserId = portalUserId,
            Role = "Admin",
            IsActive = true,
            GrantedAt = DateTime.UtcNow.AddDays(-4),
            Notes = "Primary external admin",
            User = portalUser
        });
        fixture.Tenders.Add(new Tender
        {
            Id = tenderId,
            TenantId = tenantId,
            TenderNumber = "TEN-200",
            Title = "Blue Harbor Renewal Tender"
        });
        fixture.TenderAssignments.Add(new TenderAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = tenderId,
            BusinessPartnerId = partnerId,
            AssignedToUserId = assigneeId,
            AssignmentType = "SelectedUsers",
            AssignedAt = DateTime.UtcNow.AddDays(-3),
            AssignedById = userId,
            Tender = fixture.Tenders.Single(),
            AssignedToUser = assignedUser,
            AssignedBy = new ApplicationUser
            {
                Id = userId,
                UserName = "crm.tester@erp.local",
                Email = "crm.tester@erp.local",
                FirstName = "CRM",
                LastName = "Tester"
            }
        });

        var service = fixture.CreateService();

        var result = await service.GetCollaborationDetailAsync(partnerId, 10);

        result.Should().NotBeNull();
        result!.PartnerName.Should().Be("Blue Harbor Collaboration");
        result.PrimaryContactName.Should().Be("Noah Tetteh");
        result.ActivePortalUserCount.Should().Be(1);
        result.AdminUserCount.Should().Be(1);
        result.Registrations.Should().ContainSingle(x => x.ApplicationNumber == "REG-BLUE-1" && x.Status == "Approved");
        result.PortalUsers.Should().ContainSingle(x => x.FullName == "Noah Tetteh" && x.Role == "Admin" && x.IsActive);
        result.TenderAssignments.Should().ContainSingle(x => x.TenderNumber == "TEN-200" && x.AssignedToUserName == "Noah Tetteh");
        result.Projects.Should().ContainSingle(x => x.ProjectCode == "PRJ-COLLAB-2" && x.ExternalPortalAccessEnabled && x.ExternalCollaborationEnabled);
        result.Signals.Should().NotBeEmpty();
        result.CollaborationCategory.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetServiceAsync_ShouldReturnTicketLoadAndSlaSignals()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var openTicketId = Guid.NewGuid();
        var resolvedTicketId = Guid.NewGuid();
        var problemId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var fixture = new CrmServiceFixture(tenantId, userId);

        var requester = new ApplicationUser
        {
            Id = requesterId,
            UserName = "portal.atlas",
            Email = "portal@atlas.test",
            FirstName = "Irene",
            LastName = "Mensah"
        };

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-SVC-1",
            PartnerName = "Atlas Service Hub",
            PartnerType = "Customer",
            RegistrationStatus = "Approved"
        });
        fixture.BusinessPartnerUsers.Add(new BusinessPartnerUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            UserId = requesterId,
            Role = "User",
            IsActive = true,
            GrantedAt = now.AddDays(-30),
            User = requester
        });
        fixture.EhcTickets.Add(new EhcTicket
        {
            Id = openTicketId,
            TenantId = tenantId,
            TicketNumber = "TCK-100",
            TicketType = EhcTicketType.Complaint,
            Priority = EhcTicketPriority.High,
            Source = EhcTicketSource.Email,
            Subject = "Billing dispute",
            Description = "Invoice mismatch reported by the customer.",
            RequesterUserId = requesterId,
            RequesterUser = requester,
            Status = EhcTicketStatus.InProgress,
            FirstResponseDueAt = now.AddDays(-2),
            ResolutionDueAt = now.AddDays(-1),
            CreatedAt = now.AddDays(-4)
        });
        fixture.EhcTickets.Add(new EhcTicket
        {
            Id = resolvedTicketId,
            TenantId = tenantId,
            TicketNumber = "TCK-101",
            TicketType = EhcTicketType.Helpdesk,
            Priority = EhcTicketPriority.Medium,
            Source = EhcTicketSource.Web,
            Subject = "Portal access help",
            Description = "User requested portal walkthrough.",
            RequesterUserId = requesterId,
            RequesterUser = requester,
            Status = EhcTicketStatus.Resolved,
            ResolvedAt = now.AddDays(-3),
            CreatedAt = now.AddDays(-5),
            Feedbacks =
            {
                new EhcTicketFeedback
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TicketId = resolvedTicketId,
                    SubmittedByUserId = requesterId,
                    SubmittedByUser = requester,
                    Rating = 5,
                    Comment = "Fast resolution"
                }
            }
        });
        fixture.EhcProblems.Add(new EhcProblem
        {
            Id = problemId,
            TenantId = tenantId,
            ProblemNumber = "PRB-100",
            Title = "Recurring billing mismatch",
            Description = "A recurring commercial dispute remains open.",
            Priority = EhcTicketPriority.High,
            Status = EhcProblemStatus.Open,
            CreatedAt = now.AddDays(-3)
        });
        fixture.EhcProblemTicketLinks.Add(new EhcProblemTicketLink
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProblemId = problemId,
            TicketId = openTicketId,
            Problem = fixture.EhcProblems.Single()
        });

        var service = fixture.CreateService();

        var result = await service.GetServiceAsync(1, 10, "atlas", null, true, true, "Customer");

        result.TotalCount.Should().Be(1);
        var accountService = result.Items.Should().ContainSingle().Subject;
        accountService.BusinessPartnerId.Should().Be(partnerId);
        accountService.PortalUserCount.Should().Be(1);
        accountService.OpenTicketCount.Should().Be(1);
        accountService.OverdueTicketCount.Should().Be(1);
        accountService.ComplaintTicketCount.Should().Be(1);
        accountService.HelpdeskTicketCount.Should().Be(1);
        accountService.LinkedProblemCount.Should().Be(1);
        accountService.OpenProblemCount.Should().Be(1);
        accountService.FeedbackResponseCount.Should().Be(1);
        accountService.RequiresAttention.Should().BeTrue();
        accountService.HasSlaBreachRisk.Should().BeTrue();
        accountService.ServiceCategory.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetServiceDetailAsync_ShouldReturnTicketsProblemsAndSignals()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var openTicketId = Guid.NewGuid();
        var resolvedTicketId = Guid.NewGuid();
        var problemId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var fixture = new CrmServiceFixture(tenantId, userId);

        var requester = new ApplicationUser
        {
            Id = requesterId,
            UserName = "portal.blueharbor",
            Email = "portal@blueharbor.test",
            FirstName = "Noah",
            LastName = "Tetteh"
        };
        var assignee = new ApplicationUser
        {
            Id = assigneeId,
            UserName = "agent.blueharbor",
            Email = "agent@blueharbor.test",
            FirstName = "Martha",
            LastName = "Cole"
        };

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-SVC-2",
            PartnerName = "Blue Harbor Service",
            PartnerType = "Customer",
            RegistrationStatus = "Approved",
            PrimaryContactName = "Noah Tetteh",
            PrimaryEmail = "noah@blueharbor.test"
        });
        fixture.BusinessPartnerUsers.Add(new BusinessPartnerUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            UserId = requesterId,
            Role = "Admin",
            IsActive = true,
            GrantedAt = now.AddDays(-20),
            User = requester
        });
        fixture.EhcTickets.Add(new EhcTicket
        {
            Id = openTicketId,
            TenantId = tenantId,
            TicketNumber = "TCK-200",
            TicketType = EhcTicketType.Complaint,
            Priority = EhcTicketPriority.Critical,
            Source = EhcTicketSource.PhoneCall,
            Subject = "Critical outage",
            Description = "Customer reported an outage affecting service delivery.",
            RequesterUserId = requesterId,
            RequesterUser = requester,
            AssignedToUserId = assigneeId,
            AssignedToUser = assignee,
            Status = EhcTicketStatus.InProgress,
            FirstResponseDueAt = now.AddDays(-1),
            ResolutionDueAt = now.AddHours(-6),
            CreatedAt = now.AddDays(-2)
        });
        fixture.EhcTickets.Add(new EhcTicket
        {
            Id = resolvedTicketId,
            TenantId = tenantId,
            TicketNumber = "TCK-201",
            TicketType = EhcTicketType.Helpdesk,
            Priority = EhcTicketPriority.Medium,
            Source = EhcTicketSource.Web,
            Subject = "Portal navigation support",
            Description = "Customer needed help finding supporting documents.",
            RequesterUserId = requesterId,
            RequesterUser = requester,
            AssignedToUserId = assigneeId,
            AssignedToUser = assignee,
            Status = EhcTicketStatus.Resolved,
            ResolvedAt = now.AddDays(-1),
            CreatedAt = now.AddDays(-3),
            Feedbacks =
            {
                new EhcTicketFeedback
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TicketId = resolvedTicketId,
                    SubmittedByUserId = requesterId,
                    SubmittedByUser = requester,
                    Rating = 4,
                    Comment = "Helpful support"
                }
            }
        });
        fixture.EhcProblems.Add(new EhcProblem
        {
            Id = problemId,
            TenantId = tenantId,
            ProblemNumber = "PRB-200",
            Title = "Recurring outage pattern",
            Description = "A recurring issue links multiple customer outages.",
            Priority = EhcTicketPriority.High,
            Status = EhcProblemStatus.InProgress,
            OwnerUserId = assigneeId,
            OwnerUser = assignee,
            CreatedFromTicketId = openTicketId,
            CreatedAt = now.AddDays(-2)
        });
        fixture.EhcProblemTicketLinks.Add(new EhcProblemTicketLink
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProblemId = problemId,
            TicketId = openTicketId,
            Problem = fixture.EhcProblems.Single()
        });

        var service = fixture.CreateService();

        var result = await service.GetServiceDetailAsync(partnerId, 10);

        result.Should().NotBeNull();
        result!.PartnerName.Should().Be("Blue Harbor Service");
        result.PrimaryContactName.Should().Be("Noah Tetteh");
        result.PortalUserCount.Should().Be(1);
        result.ActivePortalUserCount.Should().Be(1);
        result.Tickets.Should().Contain(x => x.TicketNumber == "TCK-200" && x.IsOverdue && x.IsComplaint);
        result.Tickets.Should().Contain(x => x.TicketNumber == "TCK-201" && x.FeedbackRating == 4);
        result.Problems.Should().ContainSingle(x => x.ProblemNumber == "PRB-200" && x.LinkedTicketCount == 1);
        result.AverageFeedbackRating.Should().Be(4);
        result.Signals.Should().NotBeEmpty();
        result.ServiceCategory.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetAccountsAsync_ShouldReturnPagedAccountsWithCommercialContext()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.AddRange(new[]
        {
            new BusinessPartner
            {
                Id = partnerId,
                TenantId = tenantId,
                PartnerCode = "CUST-310",
                PartnerName = "Atlas Infrastructure",
                PartnerType = "Customer",
                RegistrationStatus = "Approved",
                CustomerType = "Government",
                RiskLevel = "High"
            },
            new BusinessPartner
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PartnerCode = "VEN-500",
                PartnerName = "Supply Hub",
                PartnerType = "Vendor",
                RegistrationStatus = "Approved"
            }
        });
        fixture.Leads.Add(new Lead
        {
            Id = leadId,
            TenantId = tenantId,
            FirstName = "Irene",
            LastName = "Mensah",
            LeadStatus = "Converted",
            LeadSource = "Tender",
            ConvertedCustomerId = partnerId
        });
        fixture.Opportunities.Add(new Opportunity
        {
            Id = opportunityId,
            TenantId = tenantId,
            Name = "Atlas corridor package",
            CustomerId = partnerId,
            LeadId = leadId,
            Stage = "Proposal",
            Amount = 650000m,
            Probability = 60,
            Currency = "USD",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(20),
            OpportunityType = "New Business",
            LeadSource = "Tender"
        });
        fixture.Quotes.Add(new Quote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OpportunityId = opportunityId,
            CustomerId = partnerId,
            QuoteName = "Atlas pricing pack",
            QuoteStatus = "Sent",
            TotalAmount = 625000m,
            Currency = "USD",
            ValidUntil = DateTime.UtcNow.AddDays(12)
        });
        fixture.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-310",
            Title = "Atlas mobilization",
            Status = ProjectStatuses.InProgress,
            BusinessPartnerId = partnerId,
            ApprovedBudget = 280000m
        });
        fixture.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            TenderAwardId = Guid.NewGuid(),
            TenderId = Guid.NewGuid(),
            ContractNumber = "CTR-310",
            ContractTitle = "Atlas framework",
            Status = "Active",
            ContractValue = 540000m,
            EndDate = DateTime.UtcNow.AddDays(40)
        });

        var service = fixture.CreateService();

        var result = await service.GetAccountsAsync(1, 20, "atlas", null, true, "Customer");

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle();
        var account = result.Items.Single();
        account.PartnerName.Should().Be("Atlas Infrastructure");
        account.RelatedLeadCount.Should().Be(1);
        account.OpenOpportunityCount.Should().Be(1);
        account.OpenOpportunityValue.Should().Be(650000m);
        account.ActiveQuoteCount.Should().Be(1);
        account.ActiveQuoteValue.Should().Be(625000m);
        account.ActiveProjectCount.Should().Be(1);
        account.ActiveContractCount.Should().Be(1);
        account.IsAtRisk.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAndUpdateLeadAsync_ShouldPersistLeadManagementChanges()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);
        var service = fixture.CreateService();

        var created = await service.CreateLeadAsync(new CreateCrmLeadDto
        {
            FirstName = "Naa",
            LastName = "Ofori",
            CompanyName = "Beacon Energy",
            LeadSource = "Referral",
            LeadStatus = "New",
            QualificationScore = 45,
            EstimatedValue = 90000m,
            NextFollowUpDate = DateTime.UtcNow.AddDays(4),
            Notes = "Initial discovery"
        });

        var updated = await service.UpdateLeadAsync(created.LeadId, new UpdateCrmLeadDto
        {
            FirstName = "Naa",
            LastName = "Ofori",
            CompanyName = "Beacon Energy",
            LeadSource = "Referral",
            LeadStatus = "Qualified",
            QualificationScore = 80,
            EstimatedValue = 120000m,
            NextFollowUpDate = DateTime.UtcNow.AddDays(2),
            Notes = "Budget confirmed"
        });

        var paged = await service.GetLeadsAsync();

        updated.LeadStatus.Should().Be("Qualified");
        updated.QualificationScore.Should().Be(80);
        updated.EstimatedValue.Should().Be(120000m);
        paged.Items.Should().ContainSingle(x => x.LeadId == created.LeadId && x.LeadStatus == "Qualified");
    }

    [Fact]
    public async Task CreateOpportunityAsync_ShouldUseBusinessPartnerBackedAccountLink()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);
        var proposalStage = new OpportunityStageDefinition
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "OFFER", Name = "Offer Issued",
            SortOrder = 30, IsActive = true, DefaultProbability = 50
        };
        var wonStage = new OpportunityStageDefinition
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "SIGNED", Name = "Agreement Signed",
            SortOrder = 40, IsActive = true, IsClosed = true, IsWon = true, DefaultProbability = 100
        };
        fixture.OpportunityStages.AddRange(new[] { proposalStage, wonStage });

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-500",
            PartnerName = "Harbor Works",
            PartnerType = "Customer",
            RegistrationStatus = "Approved"
        });

        var service = fixture.CreateService();

        var created = await service.CreateOpportunityAsync(new CreateCrmOpportunityDto
        {
            Name = "Harbor expansion",
            BusinessPartnerId = partnerId,
            StageDefinitionId = proposalStage.Id,
            Amount = 450000m,
            Probability = 50,
            Currency = "USD",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
            LeadSource = "Referral",
            OpportunityType = "New Business"
        });

        var paged = await service.GetOpportunitiesAsync();
        var updated = await service.UpdateOpportunityAsync(created.OpportunityId, new UpdateCrmOpportunityDto
        {
            Name = created.Name,
            BusinessPartnerId = partnerId,
            StageDefinitionId = wonStage.Id,
            Amount = created.Amount,
            Probability = 100,
            Currency = created.Currency,
            ExpectedCloseDate = created.ExpectedCloseDate,
            LeadSource = created.LeadSource,
            OpportunityType = created.OpportunityType
        });

        created.BusinessPartnerId.Should().Be(partnerId);
        created.BusinessPartnerName.Should().Be("Harbor Works");
        created.StageDefinitionId.Should().Be(proposalStage.Id);
        paged.Items.Should().ContainSingle(x => x.BusinessPartnerId == partnerId && x.Name == "Harbor expansion");
        updated.StageDefinitionId.Should().Be(wonStage.Id);
        updated.ActualCloseDate.Should().NotBeNull();
        fixture.OpportunityStageHistories.Should().HaveCount(2);
        fixture.OpportunityStageHistories.Select(history => history.StageDefinitionId)
            .Should().ContainInOrder(proposalStage.Id, wonStage.Id);
    }

    [Fact]
    public async Task GetOpportunitiesAsync_ShouldFilterByOpportunityType()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.Opportunities.AddRange(new[]
        {
            new Opportunity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Atlas renewal package",
                Stage = "Negotiation",
                Amount = 300000m,
                Probability = 75,
                Currency = "USD",
                ExpectedCloseDate = DateTime.UtcNow.AddDays(14),
                OpportunityType = "Renewal",
                LeadSource = "Direct"
            },
            new Opportunity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Atlas upsell package",
                Stage = "Proposal",
                Amount = 180000m,
                Probability = 40,
                Currency = "USD",
                ExpectedCloseDate = DateTime.UtcNow.AddDays(25),
                OpportunityType = "Upsell",
                LeadSource = "Direct"
            }
        });

        var service = fixture.CreateService();

        var result = await service.GetOpportunitiesAsync(1, 20, "atlas", null, null, null, "Renewal");

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(x => x.Name == "Atlas renewal package" && x.OpportunityType == "Renewal");
    }

    [Fact]
    public async Task GetOpportunityByIdAsync_ShouldReturnConversionChainAndDeliveryLinks()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var quoteId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-900",
            PartnerName = "Summit Rail",
            PartnerType = "Customer",
            RegistrationStatus = "Approved"
        });
        fixture.Leads.Add(new Lead
        {
            Id = leadId,
            TenantId = tenantId,
            FirstName = "Efua",
            LastName = "Owusu",
            LeadStatus = "Qualified",
            LeadSource = "Tender"
        });
        fixture.Opportunities.Add(new Opportunity
        {
            Id = opportunityId,
            TenantId = tenantId,
            Name = "Summit Rail phase 2",
            CustomerId = partnerId,
            LeadId = leadId,
            Stage = "Proposal",
            Amount = 780000m,
            Probability = 65,
            Currency = "USD",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(20),
            OpportunityType = "New Business",
            LeadSource = "Tender"
        });
        fixture.Quotes.Add(new Quote
        {
            Id = quoteId,
            TenantId = tenantId,
            OpportunityId = opportunityId,
            CustomerId = partnerId,
            QuoteName = "Summit proposal",
            QuoteStatus = "Sent",
            DocumentNumber = "Q-900",
            DocumentDate = DateTime.UtcNow.AddDays(-3),
            ValidUntil = DateTime.UtcNow.AddDays(14),
            TotalAmount = 760000m,
            Currency = "USD"
        });
        fixture.Contracts.Add(new Contract
        {
            Id = contractId,
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            TenderAwardId = Guid.NewGuid(),
            TenderId = Guid.NewGuid(),
            ContractNumber = "CTR-900",
            ContractTitle = "Summit umbrella contract",
            Status = "Active",
            ContractValue = 810000m
        });
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-900",
            Title = "Summit rollout",
            Status = ProjectStatuses.InProgress,
            BusinessPartnerId = partnerId,
            ContractId = contractId,
            ApprovedBudget = 640000m,
            ProgressPercent = 35m
        });

        var service = fixture.CreateService();

        var result = await service.GetOpportunityByIdAsync(opportunityId);

        result.Should().NotBeNull();
        result!.Quotes.Should().ContainSingle(x => x.QuoteId == quoteId);
        result.RelatedContracts.Should().ContainSingle(x => x.ContractId == contractId && x.RelationshipType == "Account");
        result.RelatedProjects.Should().ContainSingle(x => x.ProjectId == projectId && x.RelationshipType == "Contract");
        result.ConversionChain.Nodes.Select(x => x.EntityType).Should().Contain(new[] { "Lead", "Opportunity", "Quote", "Contract", "Project" });
    }

    [Fact]
    public async Task GetActivitiesAsync_ShouldFilterByScopedAccountAndFollowUp()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var otherPartnerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.AddRange(new[]
        {
            new BusinessPartner
            {
                Id = partnerId,
                TenantId = tenantId,
                PartnerCode = "CUST-910",
                PartnerName = "Cobalt Ports",
                PartnerType = "Customer",
                RegistrationStatus = "Approved"
            },
            new BusinessPartner
            {
                Id = otherPartnerId,
                TenantId = tenantId,
                PartnerCode = "CUST-911",
                PartnerName = "Delta Works",
                PartnerType = "Customer",
                RegistrationStatus = "Approved"
            }
        });
        fixture.Leads.Add(new Lead
        {
            Id = leadId,
            TenantId = tenantId,
            FirstName = "Ivy",
            LastName = "Badu",
            LeadStatus = "Qualified",
            LeadSource = "Referral",
            ConvertedCustomerId = partnerId
        });
        fixture.Opportunities.Add(new Opportunity
        {
            Id = opportunityId,
            TenantId = tenantId,
            Name = "Harbor extension",
            CustomerId = partnerId,
            LeadId = leadId,
            Stage = "Proposal",
            Amount = 350000m,
            Probability = 60,
            Currency = "USD",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(18),
            OpportunityType = "New Business",
            LeadSource = "Referral"
        });
        fixture.Activities.AddRange(new[]
        {
            new Activity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Subject = "Account review call",
                ActivityType = "Call",
                ActivityStatus = "Planned",
                CustomerId = partnerId,
                DueDate = DateTime.UtcNow.AddDays(2),
                RequiresFollowUp = true
            },
            new Activity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Subject = "Opportunity workshop",
                ActivityType = "Meeting",
                ActivityStatus = "In Progress",
                OpportunityId = opportunityId,
                DueDate = DateTime.UtcNow.AddDays(3)
            },
            new Activity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Subject = "Other account touchpoint",
                ActivityType = "Call",
                ActivityStatus = "Planned",
                CustomerId = otherPartnerId,
                DueDate = DateTime.UtcNow.AddDays(1),
                RequiresFollowUp = true
            }
        });

        var service = fixture.CreateService();

        var result = await service.GetActivitiesAsync(
            page: 1,
            pageSize: 25,
            businessPartnerId: partnerId,
            followUpOnly: true);

        result.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(x => x.BusinessPartnerId == partnerId);
        result.Items.Should().Contain(x => x.OpportunityId == opportunityId && x.LeadId == leadId);
    }

    [Fact]
    public async Task CreateUpdateAndDeleteActivityAsync_ShouldPersistOpportunityLinkedActivity()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        var firstAttendeeId = Guid.NewGuid();
        var secondAttendeeId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.Employees.AddRange(new[]
        {
            new Employee
            {
                Id = firstAttendeeId,
                TenantId = tenantId,
                EmployeeNumber = "EMP-100",
                FirstName = "Ama",
                LastName = "Mensah"
            },
            new Employee
            {
                Id = secondAttendeeId,
                TenantId = tenantId,
                EmployeeNumber = "EMP-101",
                FirstName = "Kojo",
                LastName = "Asare"
            }
        });

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-912",
            PartnerName = "Summit Terminals",
            PartnerType = "Customer",
            RegistrationStatus = "Approved"
        });
        fixture.Leads.Add(new Lead
        {
            Id = leadId,
            TenantId = tenantId,
            FirstName = "Adjoa",
            LastName = "Lartey",
            LeadStatus = "Qualified",
            LeadSource = "Tender",
            ConvertedCustomerId = partnerId
        });
        fixture.Opportunities.Add(new Opportunity
        {
            Id = opportunityId,
            TenantId = tenantId,
            Name = "Terminal modernization",
            CustomerId = partnerId,
            LeadId = leadId,
            Stage = "Negotiation",
            Amount = 640000m,
            Probability = 70,
            Currency = "USD",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(12),
            OpportunityType = "Existing Customer",
            LeadSource = "Tender"
        });

        var service = fixture.CreateService();

        var created = await service.CreateActivityAsync(new CreateCrmActivityDto
        {
            Subject = "Steering committee call",
            ActivityType = "Call",
            OpportunityId = opportunityId,
            ActivityStatus = "Planned",
            ActivityDate = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date.AddDays(2),
            Priority = 1,
            RequiresFollowUp = true,
            NextFollowUpDate = DateTime.UtcNow.Date.AddDays(5),
            InternalAttendeeEmployeeIds = [firstAttendeeId, secondAttendeeId, firstAttendeeId],
            ExternalAttendees = "  Efua Owusu, client@example.com, Efua Owusu  ",
            Notes = "Confirm commercial approvals"
        });

        var updated = await service.UpdateActivityAsync(created.ActivityId, new UpdateCrmActivityDto
        {
            Subject = "Steering committee call",
            ActivityType = "Call",
            OpportunityId = opportunityId,
            ActivityStatus = "Completed",
            ActivityDate = created.ActivityDate,
            DueDate = created.DueDate,
            Priority = 2,
            RequiresFollowUp = false,
            InternalAttendeeEmployeeIds = [secondAttendeeId],
            ExternalAttendees = "client@example.com",
            Outcome = "Successful",
            Notes = "Approvals confirmed"
        });

        await service.DeleteActivityAsync(created.ActivityId);
        var deleted = await service.GetActivityByIdAsync(created.ActivityId);

        created.BusinessPartnerId.Should().Be(partnerId);
        created.BusinessPartnerName.Should().Be("Summit Terminals");
        created.LeadId.Should().Be(leadId);
        created.OpportunityId.Should().Be(opportunityId);
        created.InternalAttendees.Select(x => x.EmployeeId).Should().Equal(firstAttendeeId, secondAttendeeId);
        created.InternalAttendees.Select(x => x.DisplayName).Should().Equal("Ama Mensah", "Kojo Asare");
        created.ExternalAttendees.Should().Be("Efua Owusu, client@example.com");
        updated.ActivityStatus.Should().Be("Completed");
        updated.InternalAttendees.Should().ContainSingle(x => x.EmployeeId == secondAttendeeId);
        updated.ExternalAttendees.Should().Be("client@example.com");
        updated.Outcome.Should().Be("Successful");
        updated.RequiresFollowUp.Should().BeFalse();
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task CreateActivityAsync_ShouldRejectInternalAttendeeFromAnotherTenant()
    {
        var tenantId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, Guid.NewGuid());
        var partnerId = Guid.NewGuid();
        var otherTenantEmployeeId = Guid.NewGuid();
        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-OUTSIDE-TEST",
            PartnerName = "Tenant Customer",
            PartnerType = "Customer",
            RegistrationStatus = "Approved"
        });
        fixture.Employees.Add(new Employee
        {
            Id = otherTenantEmployeeId,
            TenantId = Guid.NewGuid(),
            EmployeeNumber = "EMP-OUTSIDE",
            FirstName = "Outside",
            LastName = "Employee"
        });

        var action = () => fixture.CreateService().CreateActivityAsync(new CreateCrmActivityDto
        {
            Subject = "Tenant-scoped meeting",
            ActivityType = "Meeting",
            ActivityStatus = "Planned",
            ActivityDate = DateTime.UtcNow,
            Priority = 2,
            BusinessPartnerId = partnerId,
            InternalAttendeeEmployeeIds = [otherTenantEmployeeId]
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not employees in this tenant*");
        fixture.Activities.Should().BeEmpty();
    }

    [Fact]
    public async Task GetQuoteByIdAsync_ShouldReturnLineItemsAndConversionChain()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        var quoteId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-901",
            PartnerName = "Bluewave Ports",
            PartnerType = "Customer",
            RegistrationStatus = "Approved"
        });
        fixture.Leads.Add(new Lead
        {
            Id = leadId,
            TenantId = tenantId,
            FirstName = "Yaw",
            LastName = "Mensimah",
            LeadStatus = "Qualified",
            LeadSource = "Referral"
        });
        fixture.Opportunities.Add(new Opportunity
        {
            Id = opportunityId,
            TenantId = tenantId,
            Name = "Port security upgrade",
            CustomerId = partnerId,
            LeadId = leadId,
            Stage = "Negotiation",
            Amount = 300000m,
            Probability = 75,
            Currency = "USD",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(18),
            OpportunityType = "Existing Customer",
            LeadSource = "Referral"
        });
        fixture.Quotes.Add(new Quote
        {
            Id = quoteId,
            TenantId = tenantId,
            OpportunityId = opportunityId,
            CustomerId = partnerId,
            QuoteName = "Bluewave final offer",
            QuoteStatus = "Sent",
            DocumentNumber = "Q-901",
            DocumentDate = DateTime.UtcNow.AddDays(-1),
            ValidUntil = DateTime.UtcNow.AddDays(9),
            TotalAmount = 298000m,
            Currency = "USD",
            LineItems =
            {
                new QuoteLineItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Description = "Security control pack",
                    Quantity = 2,
                    UnitPrice = 50000m,
                    ProductCode = "CTRL-01",
                    Unit = "Lot",
                    DiscountAmount = 0m,
                    TaxAmount = 0m
                }
            }
        });
        fixture.Contracts.Add(new Contract
        {
            Id = contractId,
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            TenderAwardId = Guid.NewGuid(),
            TenderId = Guid.NewGuid(),
            ContractNumber = "CTR-901",
            ContractTitle = "Bluewave security contract",
            Status = "Active",
            ContractValue = 350000m
        });
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-901",
            Title = "Bluewave deployment",
            Status = ProjectStatuses.InProgress,
            BusinessPartnerId = partnerId,
            ContractId = contractId,
            ApprovedBudget = 210000m,
            ProgressPercent = 20m
        });

        var service = fixture.CreateService();

        var result = await service.GetQuoteByIdAsync(quoteId);

        result.Should().NotBeNull();
        result!.BusinessPartnerId.Should().Be(partnerId);
        result.LineItems.Should().ContainSingle(x => x.Description == "Security control pack" && x.LineTotal == 100000m);
        result.ConversionChain.Nodes.Select(x => x.EntityType).Should().Contain(new[] { "Lead", "Opportunity", "Quote", "Contract", "Project" });
    }

    [Fact]
    public async Task GetProjectsAsync_ShouldReturnResolvedAccountAndContractContext()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-PRJ-1",
            PartnerName = "Atlas Delivery",
            PartnerType = "Customer",
            RegistrationStatus = "Approved"
        });
        fixture.Contracts.Add(new Contract
        {
            Id = contractId,
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            TenderAwardId = Guid.NewGuid(),
            TenderId = Guid.NewGuid(),
            ContractNumber = "CTR-PRJ-1",
            ContractTitle = "Atlas framework contract",
            Status = "Active",
            ContractValue = 650000m,
            EndDate = DateTime.UtcNow.AddDays(40)
        });
        fixture.Projects.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-ATLAS-1",
            Title = "Atlas rollout",
            Status = ProjectStatuses.InProgress,
            ContractId = contractId,
            ApprovedBudget = 210000m,
            ProgressPercent = 35m,
            TargetEndDate = DateTime.UtcNow.AddDays(15)
        });

        var service = fixture.CreateService();

        var result = await service.GetProjectsAsync(1, 10, "atlas", ProjectStatuses.InProgress, partnerId, contractId);

        result.TotalCount.Should().Be(1);
        var project = result.Items.Should().ContainSingle().Subject;
        project.ProjectId.Should().Be(projectId);
        project.ResolvedBusinessPartnerId.Should().Be(partnerId);
        project.BusinessPartnerName.Should().Be("Atlas Delivery");
        project.ContractNumber.Should().Be("CTR-PRJ-1");
        project.IsLinkedToActiveContract.Should().BeTrue();
    }

    [Fact]
    public async Task GetContractByIdAsync_ShouldReturnRenewalAndDeliveryContext()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-CON-1",
            PartnerName = "Blue Harbor",
            PartnerType = "Customer",
            RegistrationStatus = "Approved"
        });
        fixture.Contracts.Add(new Contract
        {
            Id = contractId,
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            TenderAwardId = Guid.NewGuid(),
            TenderId = Guid.NewGuid(),
            ContractNumber = "CTR-9001",
            ContractTitle = "Blue Harbor operations contract",
            Status = "Active",
            ContractType = "Service",
            ContractValue = 480000m,
            Currency = "USD",
            PaymentTerms = "Milestone based",
            EndDate = DateTime.UtcNow.AddDays(20),
            ScopeOfWork = "Operate and maintain harbor access systems.",
            Notes = "Renewal discussion already expected."
        });
        fixture.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-CON-1",
            Title = "Harbor mobilization",
            Status = ProjectStatuses.InProgress,
            BusinessPartnerId = partnerId,
            ContractId = contractId,
            ApprovedBudget = 120000m,
            ProgressPercent = 50m
        });

        var service = fixture.CreateService();

        var result = await service.GetContractByIdAsync(contractId);

        result.Should().NotBeNull();
        result!.BusinessPartnerName.Should().Be("Blue Harbor");
        result.ProjectCount.Should().Be(1);
        result.ActiveProjectCount.Should().Be(1);
        result.IsExpiringSoon.Should().BeTrue();
        result.ScopeOfWork.Should().Contain("harbor access systems");
        result.Notes.Should().Contain("Renewal");
    }

    [Fact]
    public async Task GetTendersAsync_ShouldReturnInvitationBidAndAwardContext()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var tenderId = Guid.NewGuid();
        var bidId = Guid.NewGuid();
        var awardId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-TEN-1",
            PartnerName = "Summit Civil Works",
            PartnerType = "Customer",
            RegistrationStatus = "Approved"
        });
        fixture.Tenders.Add(new Tender
        {
            Id = tenderId,
            TenantId = tenantId,
            TenderNumber = "TEN-2026-001",
            Title = "Highway rehabilitation package",
            TenderType = "RFP",
            Status = "Published",
            SubmissionDeadline = DateTime.UtcNow.AddDays(7),
            EstimatedValue = 900000m,
            Currency = "USD"
        });
        fixture.TenderInvitations.Add(new TenderInvitation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = tenderId,
            BusinessPartnerId = partnerId,
            InvitedDate = DateTime.UtcNow.AddDays(-6),
            Status = "Viewed"
        });
        fixture.TenderBids.Add(new TenderBid
        {
            Id = bidId,
            TenantId = tenantId,
            TenderId = tenderId,
            BusinessPartnerId = partnerId,
            BidNumber = "BID-001",
            SubmittedDate = DateTime.UtcNow.AddDays(-2),
            Status = "Submitted",
            TotalBidAmount = 875000m,
            Currency = "USD"
        });
        fixture.TenderAwards.Add(new TenderAward
        {
            Id = awardId,
            TenantId = tenantId,
            TenderId = tenderId,
            TenderBidId = bidId,
            BusinessPartnerId = partnerId,
            AwardDate = DateTime.UtcNow.AddDays(-1),
            OriginalBidAmount = 875000m,
            AwardedAmount = 860000m,
            Currency = "USD",
            Status = "Awarded"
        });
        fixture.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            TenderAwardId = awardId,
            TenderId = tenderId,
            ContractNumber = "CTR-TEN-1",
            ContractTitle = "Highway works contract",
            Status = "PendingSignature",
            ContractValue = 860000m
        });

        var service = fixture.CreateService();

        var result = await service.GetTendersAsync(1, 20, "highway", null, null, partnerId, tenderId);

        result.TotalCount.Should().Be(3);
        result.Items.Should().Contain(x => x.EntityType == "Invitation" && x.TenderNumber == "TEN-2026-001");
        result.Items.Should().Contain(x => x.EntityType == "Bid" && x.ReferenceNumber == "BID-001");
        result.Items.Should().Contain(x => x.EntityType == "Award" && x.RelatedContractNumber == "CTR-TEN-1");
    }

    [Fact]
    public async Task GetTenderByEntityAsync_ShouldReturnAwardDetailWithContractContext()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var tenderId = Guid.NewGuid();
        var awardId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-TEN-2",
            PartnerName = "Northern Ports",
            PartnerType = "Customer",
            RegistrationStatus = "Approved"
        });
        fixture.Tenders.Add(new Tender
        {
            Id = tenderId,
            TenantId = tenantId,
            TenderNumber = "TEN-2026-100",
            Title = "Port dredging package",
            TenderType = "ITB",
            Status = "Awarded",
            PublishDate = DateTime.UtcNow.AddDays(-20),
            SubmissionDeadline = DateTime.UtcNow.AddDays(-10),
            AwardDate = DateTime.UtcNow.AddDays(-3),
            EstimatedValue = 1500000m,
            Currency = "USD"
        });
        fixture.TenderAwards.Add(new TenderAward
        {
            Id = awardId,
            TenantId = tenantId,
            TenderId = tenderId,
            TenderBidId = Guid.NewGuid(),
            BusinessPartnerId = partnerId,
            AwardDate = DateTime.UtcNow.AddDays(-3),
            OriginalBidAmount = 1480000m,
            AwardedAmount = 1440000m,
            IsNegotiated = true,
            AwardJustification = "Best value after negotiation",
            Currency = "USD",
            Status = "Awarded"
        });
        fixture.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            TenderAwardId = awardId,
            TenderId = tenderId,
            ContractNumber = "CTR-TEN-2",
            ContractTitle = "Port dredging contract",
            Status = "Active",
            ContractValue = 1440000m
        });

        var service = fixture.CreateService();

        var result = await service.GetTenderByEntityAsync("Award", awardId);

        result.Should().NotBeNull();
        result!.EntityType.Should().Be("Award");
        result.TenderTitle.Should().Be("Port dredging package");
        result.BusinessPartnerName.Should().Be("Northern Ports");
        result.RelatedContractNumber.Should().Be("CTR-TEN-2");
        result.IsNegotiated.Should().BeTrue();
        result.AwardJustification.Should().Contain("Best value");
    }

    [Fact]
    public async Task GetCampaignsAsync_ShouldReturnCampaignMetricsAndSupportFilters()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-CAM-1",
            PartnerName = "Atlas Civil Works",
            PartnerType = "Customer",
            RegistrationStatus = "Approved"
        });
        fixture.Leads.Add(new Lead
        {
            Id = leadId,
            TenantId = tenantId,
            FirstName = "Ama",
            LastName = "Mensah",
            CompanyName = "Atlas Civil Works",
            LeadStatus = "Converted",
            LeadSource = "Campaign",
            QualificationScore = 84,
            EstimatedValue = 120000m,
            ConvertedCustomerId = partnerId
        });
        fixture.Opportunities.Add(new Opportunity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Atlas renewal package",
            CustomerId = partnerId,
            LeadId = leadId,
            Stage = "Proposal",
            Amount = 100000m,
            Probability = 50,
            Currency = "USD",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(20),
            OpportunityType = "Renewal",
            LeadSource = "Campaign"
        });
        fixture.Campaigns.AddRange(new[]
        {
            new Campaign
            {
                Id = campaignId,
                TenantId = tenantId,
                Name = "Renewal push",
                CampaignType = "Event",
                CampaignStatus = "Active",
                StartDate = DateTime.UtcNow.AddDays(-2),
                EndDate = DateTime.UtcNow.AddDays(10),
                ActualAudience = 20,
                ResponseCount = 5
            },
            new Campaign
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Dormant campaign",
                CampaignType = "Email",
                CampaignStatus = "Planning",
                StartDate = DateTime.UtcNow.AddDays(5)
            }
        });
        fixture.CampaignMembers.Add(new CampaignMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CampaignId = campaignId,
            LeadId = leadId,
            CustomerId = partnerId,
            MemberStatus = "Responded",
            ResponseDate = DateTime.UtcNow.AddDays(-1)
        });

        var service = fixture.CreateService();

        var result = await service.GetCampaignsAsync(1, 25, "renewal", "Active", "Event", true, partnerId, leadId);
        var item = result.Items.Single();

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle();
        item.CampaignId.Should().Be(campaignId);
        item.MemberCount.Should().Be(1);
        item.RespondedMemberCount.Should().Be(1);
        item.OpenOpportunityCount.Should().Be(1);
        item.WeightedPipelineValue.Should().Be(50000m);
        item.InfluencedAccountCount.Should().Be(1);
        item.ResponseRate.Should().Be(25m);
        item.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetCampaignByIdAsync_ShouldReturnMembersAccountsAndOpportunities()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-CAM-2",
            PartnerName = "Blue Harbor Ports",
            PartnerType = "Customer",
            RegistrationStatus = "Approved"
        });
        fixture.Leads.Add(new Lead
        {
            Id = leadId,
            TenantId = tenantId,
            FirstName = "Kojo",
            LastName = "Owusu",
            CompanyName = "Blue Harbor Ports",
            LeadStatus = "Qualified",
            LeadSource = "Campaign",
            QualificationScore = 70,
            EstimatedValue = 320000m,
            ConvertedCustomerId = partnerId,
            NextFollowUpDate = DateTime.UtcNow.AddDays(5)
        });
        fixture.Opportunities.Add(new Opportunity
        {
            Id = opportunityId,
            TenantId = tenantId,
            Name = "Harbor expansion package",
            CustomerId = partnerId,
            LeadId = leadId,
            Stage = "Negotiation",
            Amount = 300000m,
            Probability = 60,
            Currency = "USD",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(15),
            OpportunityType = "New Business",
            LeadSource = "Campaign"
        });
        fixture.Campaigns.Add(new Campaign
        {
            Id = campaignId,
            TenantId = tenantId,
            Name = "Port summit series",
            CampaignType = "Event",
            CampaignStatus = "Active",
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(14),
            Description = "Executive campaign",
            Notes = "Priority motion"
        });
        fixture.CampaignMembers.Add(new CampaignMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CampaignId = campaignId,
            LeadId = leadId,
            CustomerId = partnerId,
            MemberStatus = "Active"
        });

        var service = fixture.CreateService();

        var result = await service.GetCampaignByIdAsync(campaignId);

        result.Should().NotBeNull();
        result!.CampaignId.Should().Be(campaignId);
        result.Members.Should().ContainSingle(x => x.LeadId == leadId && x.NeedsFollowUp);
        result.InfluencedAccounts.Should().ContainSingle(x => x.BusinessPartnerId == partnerId);
        result.Opportunities.Should().ContainSingle(x => x.OpportunityId == opportunityId);
    }

    [Fact]
    public async Task AddCampaignMemberAsync_ShouldRejectDuplicateLead()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.Campaigns.Add(new Campaign
        {
            Id = campaignId,
            TenantId = tenantId,
            Name = "Tender outreach",
            CampaignType = "Tender",
            CampaignStatus = "Active",
            StartDate = DateTime.UtcNow.AddDays(-1)
        });
        fixture.Leads.Add(new Lead
        {
            Id = leadId,
            TenantId = tenantId,
            FirstName = "Efua",
            LastName = "Coleman",
            LeadStatus = "New",
            LeadSource = "Campaign"
        });
        fixture.CampaignMembers.Add(new CampaignMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CampaignId = campaignId,
            LeadId = leadId,
            MemberStatus = "Active"
        });

        var service = fixture.CreateService();

        var action = async () => await service.AddCampaignMemberAsync(campaignId, new CreateCrmCampaignMemberDto
        {
            LeadId = leadId,
            MemberStatus = "Active"
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already a member*");
    }

    [Fact]
    public async Task GetForecastAsync_ShouldReturnBucketsDealsAndRenewalCoverage()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var renewalOpportunityId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-FRC-1",
            PartnerName = "Metro Build",
            PartnerType = "Customer",
            RegistrationStatus = "Approved"
        });
        fixture.Leads.Add(new Lead
        {
            Id = leadId,
            TenantId = tenantId,
            FirstName = "Ama",
            LastName = "Owusu",
            CompanyName = "Metro Build",
            LeadStatus = "Qualified",
            LeadSource = "Campaign",
            ConvertedCustomerId = partnerId
        });
        fixture.Opportunities.AddRange(new[]
        {
            new Opportunity
            {
                Id = renewalOpportunityId,
                TenantId = tenantId,
                Name = "Metro renewal",
                CustomerId = partnerId,
                LeadId = leadId,
                Stage = "Negotiation",
                Amount = 200000m,
                Probability = 80,
                Currency = "USD",
                ExpectedCloseDate = DateTime.UtcNow.AddDays(25),
                OpportunityType = "Renewal",
                LeadSource = "Campaign"
            },
            new Opportunity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Metro expansion",
                CustomerId = partnerId,
                LeadId = leadId,
                Stage = "Proposal",
                Amount = 100000m,
                Probability = 60,
                Currency = "USD",
                ExpectedCloseDate = DateTime.UtcNow.AddDays(50),
                OpportunityType = "New Business",
                LeadSource = "Campaign"
            }
        });
        fixture.Quotes.Add(new Quote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OpportunityId = renewalOpportunityId,
            CustomerId = partnerId,
            QuoteName = "Metro renewal quote",
            QuoteStatus = "Sent",
            TotalAmount = 195000m,
            ValidUntil = DateTime.UtcNow.AddDays(14),
            Currency = "USD"
        });
        fixture.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            TenderAwardId = Guid.NewGuid(),
            TenderId = Guid.NewGuid(),
            ContractNumber = "CTR-FRC-1",
            ContractTitle = "Metro services contract",
            Status = "Active",
            ContractValue = 250000m,
            EndDate = DateTime.UtcNow.AddDays(40)
        });
        fixture.Campaigns.Add(new Campaign
        {
            Id = campaignId,
            TenantId = tenantId,
            Name = "Renewal blitz",
            CampaignType = "Email",
            CampaignStatus = "Active",
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(30)
        });
        fixture.CampaignMembers.Add(new CampaignMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CampaignId = campaignId,
            LeadId = leadId,
            CustomerId = partnerId,
            MemberStatus = "Responded"
        });

        var service = fixture.CreateService();

        var result = await service.GetForecastAsync(6, null, partnerId);

        result.HorizonMonths.Should().Be(6);
        result.OpportunityCount.Should().Be(2);
        result.WeightedPipelineValue.Should().Be(220000m);
        result.CommitValue.Should().Be(200000m);
        result.CampaignBackedWeightedValue.Should().Be(220000m);
        result.RenewalContractValue.Should().Be(250000m);
        result.RenewalCoverageValue.Should().Be(160000m);
        result.RenewalGapValue.Should().Be(90000m);
        result.Buckets.Sum(x => x.WeightedValue).Should().Be(220000m);
        result.HighConfidenceDeals.Should().Contain(x => x.OpportunityId == renewalOpportunityId && x.ForecastCategory == "Commit" && x.CampaignCount == 1);
        result.RenewalWatchlist.Should().ContainSingle(x => x.BusinessPartnerId == partnerId && x.CoverageCategory == "Watch");
    }

    [Fact]
    public async Task GetConversionsAsync_ShouldReturnFunnelJourneysAndLeakage()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deliveryPartnerId = Guid.NewGuid();
        var stalledPartnerId = Guid.NewGuid();
        var healthyLeadId = Guid.NewGuid();
        var stalledLeadId = Guid.NewGuid();
        var standaloneLeadId = Guid.NewGuid();
        var healthyOpportunityId = Guid.NewGuid();
        var stalledOpportunityId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.AddRange(new[]
        {
            new BusinessPartner
            {
                Id = deliveryPartnerId,
                TenantId = tenantId,
                PartnerCode = "CUST-CNV-1",
                PartnerName = "Metro Delivery",
                PartnerType = "Customer",
                RegistrationStatus = "Approved"
            },
            new BusinessPartner
            {
                Id = stalledPartnerId,
                TenantId = tenantId,
                PartnerCode = "CUST-CNV-2",
                PartnerName = "Atlas Pipeline",
                PartnerType = "Customer",
                RegistrationStatus = "Approved"
            }
        });

        fixture.Leads.AddRange(new[]
        {
            new Lead
            {
                Id = healthyLeadId,
                TenantId = tenantId,
                FirstName = "Ama",
                LastName = "Mensah",
                CompanyName = "Metro Delivery",
                LeadStatus = "Qualified",
                LeadSource = "Referral",
                ConvertedCustomerId = deliveryPartnerId
            },
            new Lead
            {
                Id = stalledLeadId,
                TenantId = tenantId,
                FirstName = "Kojo",
                LastName = "Asante",
                CompanyName = "Atlas Pipeline",
                LeadStatus = "Qualified",
                LeadSource = "Campaign",
                ConvertedCustomerId = stalledPartnerId
            },
            new Lead
            {
                Id = standaloneLeadId,
                TenantId = tenantId,
                FirstName = "Esi",
                LastName = "Boateng",
                CompanyName = "North Ridge",
                LeadStatus = "Qualified",
                LeadSource = "Website",
                EstimatedValue = 55000m,
                NextFollowUpDate = DateTime.UtcNow.AddDays(5)
            }
        });

        fixture.Opportunities.AddRange(new[]
        {
            new Opportunity
            {
                Id = healthyOpportunityId,
                TenantId = tenantId,
                Name = "Metro platform rollout",
                CustomerId = deliveryPartnerId,
                LeadId = healthyLeadId,
                Stage = "Closed Won",
                Amount = 180000m,
                Probability = 100,
                Currency = "USD",
                ExpectedCloseDate = DateTime.UtcNow.AddDays(-4),
                ActualCloseDate = DateTime.UtcNow.AddDays(-1),
                OpportunityType = "Existing Customer",
                LeadSource = "Referral"
            },
            new Opportunity
            {
                Id = stalledOpportunityId,
                TenantId = tenantId,
                Name = "Atlas modernization",
                CustomerId = stalledPartnerId,
                LeadId = stalledLeadId,
                Stage = "Proposal",
                Amount = 90000m,
                Probability = 60,
                Currency = "GHS",
                ExpectedCloseDate = DateTime.UtcNow.AddDays(12),
                OpportunityType = "New Business",
                LeadSource = "Campaign"
            }
        });

        fixture.Quotes.Add(new Quote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OpportunityId = healthyOpportunityId,
            CustomerId = deliveryPartnerId,
            QuoteName = "Metro rollout quote",
            QuoteStatus = "Accepted",
            TotalAmount = 175000m,
            ValidUntil = DateTime.UtcNow.AddDays(20),
            AcceptedDate = DateTime.UtcNow.AddDays(-2),
            Currency = "USD"
        });

        fixture.Contracts.Add(new Contract
        {
            Id = contractId,
            TenantId = tenantId,
            BusinessPartnerId = deliveryPartnerId,
            TenderAwardId = Guid.NewGuid(),
            TenderId = Guid.NewGuid(),
            ContractNumber = "CTR-CNV-1",
            ContractTitle = "Metro rollout contract",
            Status = "Active",
            ContractValue = 185000m,
            StartDate = DateTime.UtcNow.AddDays(-2),
            EndDate = DateTime.UtcNow.AddMonths(8),
            Currency = "USD"
        });

        fixture.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectCode = "PRJ-CNV-1",
            Title = "Metro rollout delivery",
            Status = ProjectStatuses.InProgress,
            BusinessPartnerId = deliveryPartnerId,
            ContractId = contractId,
            ApprovedBudget = 170000m,
            ProgressPercent = 35m,
            StartDate = DateTime.UtcNow.AddDays(-1),
            TargetEndDate = DateTime.UtcNow.AddMonths(4)
        });

        var service = fixture.CreateService();

        var result = await service.GetConversionsAsync(6);

        result.HorizonMonths.Should().Be(6);
        result.LeadCount.Should().Be(3);
        result.LeadWithOpportunityCount.Should().Be(2);
        result.OpportunityCount.Should().Be(2);
        result.QuotedOpportunityCount.Should().Be(1);
        result.ContractBackedOpportunityCount.Should().Be(1);
        result.ProjectBackedOpportunityCount.Should().Be(1);
        result.LeadToOpportunityRate.Should().Be(66.7m);
        result.OpportunityToQuoteRate.Should().Be(50m);
        result.TotalOpportunityValuesByCurrency.Should().ContainSingle(x => x.Currency == "GHS" && x.Amount == 90000m);
        result.TotalOpportunityValuesByCurrency.Should().ContainSingle(x => x.Currency == "USD" && x.Amount == 180000m);
        result.WeightedPipelineValuesByCurrency.Should().ContainSingle(x => x.Currency == "GHS" && x.Amount == 54000m);
        result.Funnel.Should().ContainSingle(x => x.Stage == "Project" && x.UnspecifiedCurrencyCount == 1);
        result.Funnel.Should().ContainSingle(x => x.Stage == "Quote" && x.EntityCount == 1);
        result.Journeys.Should().Contain(x => x.OpportunityId == healthyOpportunityId && x.CoverageStatus == "Project Live");
        result.Journeys.Should().Contain(x =>
            x.OpportunityId == stalledOpportunityId
            && x.CoverageStatus == "Opportunity Only"
            && x.LeakageReason != null
            && x.LeakageReason.Contains("no quote", StringComparison.OrdinalIgnoreCase));
        result.Leakage.Should().Contain(x => x.EntityType == "Lead" && x.EntityId == standaloneLeadId);
        result.Leakage.Should().Contain(x => x.EntityType == "Opportunity" && x.EntityId == stalledOpportunityId);
    }

    [Fact]
    public async Task GetReportingAsync_ShouldReturnPipelineQuoteAndHealthMetrics()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        var fixture = new CrmServiceFixture(tenantId, userId);

        fixture.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUST-902",
            PartnerName = "Metro Build",
            PartnerType = "Customer",
            RegistrationStatus = "Approved",
            RiskLevel = "Low",
            PerformanceRating = 4.5m
        });
        fixture.Leads.AddRange(new[]
        {
            new Lead
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                FirstName = "Ama",
                LastName = "Koranteng",
                LeadStatus = "Qualified",
                LeadSource = "Referral"
            },
            new Lead
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                FirstName = "Kofi",
                LastName = "Adu",
                LeadStatus = "Converted",
                LeadSource = "Website",
                ConvertedCustomerId = partnerId
            }
        });
        fixture.Opportunities.Add(new Opportunity
        {
            Id = opportunityId,
            TenantId = tenantId,
            Name = "Metro renewal",
            CustomerId = partnerId,
            Stage = "Negotiation",
            Amount = 400000m,
            Probability = 50,
            Currency = "USD",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(10),
            OpportunityType = "Renewal",
            LeadSource = "Website"
        });
        fixture.Quotes.AddRange(new[]
        {
            new Quote
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OpportunityId = opportunityId,
                CustomerId = partnerId,
                QuoteName = "Metro sent quote",
                QuoteStatus = "Sent",
                ValidUntil = DateTime.UtcNow.AddDays(7),
                TotalAmount = 390000m,
                Currency = "USD"
            },
            new Quote
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OpportunityId = opportunityId,
                CustomerId = partnerId,
                QuoteName = "Metro accepted quote",
                QuoteStatus = "Accepted",
                ValidUntil = DateTime.UtcNow.AddDays(5),
                TotalAmount = 395000m,
                Currency = "USD"
            }
        });
        fixture.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            TenderAwardId = Guid.NewGuid(),
            TenderId = Guid.NewGuid(),
            ContractNumber = "CTR-902",
            ContractTitle = "Metro active contract",
            Status = "Active",
            ContractValue = 420000m,
            EndDate = DateTime.UtcNow.AddDays(20)
        });

        var service = fixture.CreateService();

        var result = await service.GetReportingAsync(10);

        result.TotalLeadCount.Should().Be(2);
        result.ConvertedLeadCount.Should().Be(1);
        result.LeadConversionRate.Should().Be(50m);
        result.OpenOpportunityCount.Should().Be(1);
        result.WeightedPipelineValue.Should().Be(200000m);
        result.TotalQuoteCount.Should().Be(2);
        result.AcceptedQuoteCount.Should().Be(1);
        result.QuoteAcceptanceRate.Should().Be(50m);
        result.ActiveAccountCount.Should().Be(1);
        result.PipelineByStage.Should().ContainSingle(x => x.Stage == "Negotiation" && x.QuoteCount == 2);
        result.AccountHealth.Should().ContainSingle(x => x.BusinessPartnerId == partnerId);
        result.ClosingOpportunities.Should().ContainSingle(x => x.OpportunityId == opportunityId);
        result.ExpiringContracts.Should().HaveCount(1);
    }

    private sealed class CrmServiceFixture
    {
        public List<Lead> Leads { get; } = new();
        public List<Opportunity> Opportunities { get; } = new();
        public List<OpportunityStageDefinition> OpportunityStages { get; } = new();
        public List<OpportunityStageHistory> OpportunityStageHistories { get; } = new();
        public List<Quote> Quotes { get; } = new();
        public List<SalesOrder> SalesOrders { get; } = new();
        public List<SalesAgreement> SalesAgreements { get; } = new();
        public List<SalesAllocation> SalesAllocations { get; } = new();
        public List<ReturnOrder> ReturnOrders { get; } = new();
        public List<CreditNote> CreditNotes { get; } = new();
        public List<Refund> Refunds { get; } = new();
        public List<Activity> Activities { get; } = new();
        public List<Employee> Employees { get; } = new();
        public List<BusinessPartner> BusinessPartners { get; } = new();
        public List<Project> Projects { get; } = new();
        public List<Contract> Contracts { get; } = new();
        public List<Tender> Tenders { get; } = new();
        public List<TenderInvitation> TenderInvitations { get; } = new();
        public List<TenderBid> TenderBids { get; } = new();
        public List<TenderAward> TenderAwards { get; } = new();
        public List<SupplierPerformanceMetric> SupplierPerformanceMetrics { get; } = new();
        public List<QualityIncident> QualityIncidents { get; } = new();
        public List<PerformanceReview> PerformanceReviews { get; } = new();
        public List<BlacklistAppeal> BlacklistAppeals { get; } = new();
        public List<BusinessPartnerRegistration> BusinessPartnerRegistrations { get; } = new();
        public List<BusinessPartnerUser> BusinessPartnerUsers { get; } = new();
        public List<TenderAssignment> TenderAssignments { get; } = new();
        public List<EhcTicket> EhcTickets { get; } = new();
        public List<EhcPropertyEnquiryProspect> PropertyEnquiryProspects { get; } = new();
        public List<EhcProblem> EhcProblems { get; } = new();
        public List<EhcProblemTicketLink> EhcProblemTicketLinks { get; } = new();
        public List<Campaign> Campaigns { get; } = new();
        public List<CampaignMember> CampaignMembers { get; } = new();

        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<ICurrentUserProvider> _currentUserProvider = new();

        public CrmServiceFixture(Guid tenantId, Guid userId)
        {
            _unitOfWork.Setup(x => x.Repository<Lead>()).Returns(CreateRepository(Leads).Object);
            _unitOfWork.Setup(x => x.Repository<Opportunity>()).Returns(CreateRepository(Opportunities).Object);
            _unitOfWork.Setup(x => x.Repository<OpportunityStageDefinition>()).Returns(CreateRepository(OpportunityStages).Object);
            _unitOfWork.Setup(x => x.Repository<OpportunityStageHistory>()).Returns(CreateRepository(OpportunityStageHistories).Object);
            _unitOfWork.Setup(x => x.Repository<Quote>()).Returns(CreateRepository(Quotes).Object);
            _unitOfWork.Setup(x => x.Repository<SalesOrder>()).Returns(CreateRepository(SalesOrders).Object);
            _unitOfWork.Setup(x => x.Repository<SalesAgreement>()).Returns(CreateRepository(SalesAgreements).Object);
            _unitOfWork.Setup(x => x.Repository<SalesAllocation>()).Returns(CreateRepository(SalesAllocations).Object);
            _unitOfWork.Setup(x => x.Repository<ReturnOrder>()).Returns(CreateRepository(ReturnOrders).Object);
            _unitOfWork.Setup(x => x.Repository<CreditNote>()).Returns(CreateRepository(CreditNotes).Object);
            _unitOfWork.Setup(x => x.Repository<Refund>()).Returns(CreateRepository(Refunds).Object);
            _unitOfWork.Setup(x => x.Repository<Activity>()).Returns(CreateRepository(Activities).Object);
            _unitOfWork.Setup(x => x.Repository<Employee>()).Returns(CreateRepository(Employees).Object);
            _unitOfWork.Setup(x => x.Repository<BusinessPartner>()).Returns(CreateRepository(BusinessPartners).Object);
            _unitOfWork.Setup(x => x.Repository<Project>()).Returns(CreateRepository(Projects).Object);
            _unitOfWork.Setup(x => x.Repository<Contract>()).Returns(CreateRepository(Contracts).Object);
            _unitOfWork.Setup(x => x.Repository<Tender>()).Returns(CreateRepository(Tenders).Object);
            _unitOfWork.Setup(x => x.Repository<TenderInvitation>()).Returns(CreateRepository(TenderInvitations).Object);
            _unitOfWork.Setup(x => x.Repository<TenderBid>()).Returns(CreateRepository(TenderBids).Object);
            _unitOfWork.Setup(x => x.Repository<TenderAward>()).Returns(CreateRepository(TenderAwards).Object);
            _unitOfWork.Setup(x => x.Repository<SupplierPerformanceMetric>()).Returns(CreateRepository(SupplierPerformanceMetrics).Object);
            _unitOfWork.Setup(x => x.Repository<QualityIncident>()).Returns(CreateRepository(QualityIncidents).Object);
            _unitOfWork.Setup(x => x.Repository<PerformanceReview>()).Returns(CreateRepository(PerformanceReviews).Object);
            _unitOfWork.Setup(x => x.Repository<BlacklistAppeal>()).Returns(CreateRepository(BlacklistAppeals).Object);
            _unitOfWork.Setup(x => x.Repository<BusinessPartnerRegistration>()).Returns(CreateRepository(BusinessPartnerRegistrations).Object);
            _unitOfWork.Setup(x => x.Repository<BusinessPartnerUser>()).Returns(CreateRepository(BusinessPartnerUsers).Object);
            _unitOfWork.Setup(x => x.Repository<TenderAssignment>()).Returns(CreateRepository(TenderAssignments).Object);
            _unitOfWork.Setup(x => x.Repository<EhcTicket>()).Returns(CreateRepository(EhcTickets).Object);
            _unitOfWork.Setup(x => x.Repository<EhcPropertyEnquiryProspect>()).Returns(CreateRepository(PropertyEnquiryProspects).Object);
            _unitOfWork.Setup(x => x.Repository<EhcProblem>()).Returns(CreateRepository(EhcProblems).Object);
            _unitOfWork.Setup(x => x.Repository<EhcProblemTicketLink>()).Returns(CreateRepository(EhcProblemTicketLinks).Object);
            _unitOfWork.Setup(x => x.Repository<Campaign>()).Returns(CreateRepository(Campaigns).Object);
            _unitOfWork.Setup(x => x.Repository<CampaignMember>()).Returns(CreateRepository(CampaignMembers).Object);
            _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            _currentUserProvider.SetupGet(x => x.TenantId).Returns(tenantId);
            _currentUserProvider.SetupGet(x => x.UserId).Returns(userId);
            _currentUserProvider.SetupGet(x => x.Username).Returns("crm.tester@erp.local");
        }

        public CrmService CreateService() => new(_unitOfWork.Object, _currentUserProvider.Object);

        private static Mock<IGenericRepository<T>> CreateRepository<T>(List<T> items) where T : BaseEntity
        {
            var repository = new Mock<IGenericRepository<T>>();

            repository
                .Setup(x => x.FindAsync(It.IsAny<Expression<Func<T, bool>>>()))
                .ReturnsAsync((Expression<Func<T, bool>> predicate) => items.Where(x => !x.IsDeleted).Where(predicate.Compile()).ToList());
            repository
                .Setup(x => x.FindAsync(It.IsAny<Expression<Func<T, bool>>>(), It.IsAny<Expression<Func<T, object>>[]>()))
                .ReturnsAsync((Expression<Func<T, bool>> predicate, Expression<Func<T, object>>[] _) => items.Where(x => !x.IsDeleted).Where(predicate.Compile()).ToList());
            repository
                .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));
            repository
                .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Expression<Func<T, object>>[]>()))
                .ReturnsAsync((Guid id, Expression<Func<T, object>>[] _) => items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));
            repository
                .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<T, bool>>>()))
                .ReturnsAsync((Expression<Func<T, bool>> predicate) => items.Where(x => !x.IsDeleted).Any(predicate.Compile()));
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
                .Setup(x => x.UpdateAsync(It.IsAny<T>()))
                .Returns((T entity) =>
                {
                    var index = items.FindIndex(x => x.Id == entity.Id);
                    if (index >= 0)
                    {
                        items[index] = entity;
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
                        entity.IsDeleted = true;
                    }

                    return Task.CompletedTask;
                });

            return repository;
        }
    }
}
