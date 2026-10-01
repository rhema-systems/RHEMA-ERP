using System.Reflection;
using System.Text.Json;
using ErpSystem.Api.Controllers.Ehc;
using ErpSystem.Api.Services.Ehc;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ErpSystem.Shared;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Ehc;

public sealed class PropertyEnquiryProspectLifecycleTests
{
    [Theory]
    [InlineData(ProspectDepositRequirementTypes.Fixed, 100000, 25000, null, 25000)]
    [InlineData(ProspectDepositRequirementTypes.Percentage, 100000, null, 20, 20000)]
    [InlineData(ProspectDepositRequirementTypes.Full, 100000, null, null, 100000)]
    public void Deposit_threshold_uses_the_configured_rule(
        string requirement, int agreedAmount, int? fixedAmount, int? percentage, int expected)
    {
        var prospect = new EhcPropertyEnquiryProspect
        {
            DepositRequirementType = requirement,
            AgreedAmount = agreedAmount,
            FixedDepositAmount = fixedAmount,
            DepositPercentage = percentage
        };

        Assert.Equal(expected, PropertyEnquiryProspectService.RequiredDeposit(prospect));
    }

    [Fact]
    public void Opportunity_creation_is_an_explicit_internal_sales_action()
    {
        var method = typeof(EhcPropertyEnquiriesController).GetMethod(nameof(EhcPropertyEnquiriesController.CreateOpportunity));

        Assert.NotNull(method);
        Assert.Equal("{id:guid}/prospect/opportunity", method!.GetCustomAttribute<HttpPostAttribute>()?.Template);
        var roles = method.GetCustomAttribute<AuthorizeAttribute>()?.Roles ?? string.Empty;
        Assert.Contains("Sales User", roles);
        Assert.DoesNotContain("Anonymous", roles);
    }

    [Fact]
    public async Task Explicit_opportunity_action_reconciles_a_legacy_ticket_opportunity_without_creating_a_duplicate()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        var salesUnit = new OrganizationUnit
        {
            TenantId = tenantId,
            OrganizationLevelId = Guid.NewGuid(),
            Name = "Sales",
            Code = "DEPT-SALES",
            Path = "/SALES",
            IsActive = true
        };
        var lead = new Lead
        {
            TenantId = tenantId,
            ReferenceNumber = "LEAD-LEGACY-001",
            Status = "Active",
            EffectiveDate = DateTime.UtcNow,
            FirstName = "Ama",
            LastName = "Mensah",
            LeadStatus = "Qualified"
        };
        var opportunity = new Opportunity
        {
            TenantId = tenantId,
            Name = "Legacy public enquiry opportunity",
            LeadId = lead.Id,
            Stage = "Qualification",
            Amount = 125000m,
            Currency = "GHS",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30)
        };
        var ticket = new EhcTicket
        {
            TenantId = tenantId,
            TicketNumber = "EHC-LEGACY-001",
            TicketType = EhcTicketType.Enquiry,
            Status = EhcTicketStatus.Acknowledged,
            Description = "Public property enquiry",
            AssignedOrganizationUnitId = salesUnit.Id,
            CrmLeadId = lead.Id,
            CrmOpportunityId = opportunity.Id,
            PropertyListingContextJson = JsonSerializer.Serialize(new EhcPropertyListingContextDto(
                "estate-public-listing", Guid.NewGuid(), "LIST-001", "Public property", "Sale", "GHS",
                "Accra", 125000m, Guid.NewGuid(), null, null, "Ama Mensah", "Ama Mensah",
                "ama@example.test", "+233245550101"))
        };
        var prospect = new EhcPropertyEnquiryProspect
        {
            TenantId = tenantId,
            TicketId = ticket.Id,
            LeadId = lead.Id,
            Status = EhcPropertyProspectStatuses.Qualified,
            AgreedAmount = 125000m,
            Currency = "GHS"
        };
        db.AddRange(salesUnit, lead, opportunity, ticket, prospect);
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.TenantId).Returns(tenantId);
        currentUser.SetupGet(user => user.UserId).Returns(actorId.ToString());
        currentUser.SetupGet(user => user.UserName).Returns("sales.manager");
        var opportunityService = new Mock<IOpportunityService>();
        var service = new PropertyEnquiryProspectService(
            db,
            currentUser.Object,
            Mock.Of<IBusinessPartnerService>(),
            opportunityService.Object,
            Mock.Of<ISalesAllocationService>(),
            Mock.Of<IProspectDepositFinancePostingService>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IEhcTicketService>(),
            NullLogger<PropertyEnquiryProspectService>.Instance);

        var result = await service.CreateOpportunityAsync(ticket.Id, new CreatePropertyEnquiryOpportunityRequest
        {
            Amount = 999999m,
            Currency = "USD",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(14),
            ReserveProperty = false
        });

        Assert.Equal(opportunity.Id, result.OpportunityId);
        Assert.Equal(EhcPropertyProspectStatuses.Opportunity, result.Status);
        Assert.Equal(opportunity.Amount, result.AgreedAmount);
        Assert.Equal(opportunity.Currency, result.Currency);
        opportunityService.Verify(item => item.CreateAsync(It.IsAny<ErpSystem.Core.DTOs.Sales.CreateOpportunityDto>()), Times.Never);
        var savedTicket = await db.EhcTickets.AsNoTracking().SingleAsync(item => item.Id == ticket.Id);
        var savedProspect = await db.Set<EhcPropertyEnquiryProspect>().AsNoTracking().SingleAsync(item => item.Id == prospect.Id);
        Assert.Equal(opportunity.Id, savedTicket.CrmOpportunityId);
        Assert.Equal(opportunity.Id, savedProspect.OpportunityId);
        Assert.Equal(EhcPropertyProspectStatuses.Opportunity, savedProspect.Status);
    }

    [Fact]
    public void Disqualification_is_a_governed_internal_action_with_a_required_reason()
    {
        var method = typeof(EhcPropertyEnquiriesController).GetMethod(nameof(EhcPropertyEnquiriesController.Disqualify));

        Assert.Equal("{id:guid}/prospect/disqualify", method?.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.NotNull(method?.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(typeof(DisqualifyPropertyEnquiryRequest).GetProperty(nameof(DisqualifyPropertyEnquiryRequest.Reason))
            ?.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>());
    }

    [Fact]
    public void Prospect_deposit_requires_an_existing_opportunity_lineage()
    {
        var opportunity = typeof(ProspectDepositReceipt).GetProperty(nameof(ProspectDepositReceipt.OpportunityId));
        var ticket = typeof(ProspectDepositReceipt).GetProperty(nameof(ProspectDepositReceipt.TicketId));
        var lead = typeof(ProspectDepositReceipt).GetProperty(nameof(ProspectDepositReceipt.LeadId));

        Assert.Equal(typeof(Guid), opportunity?.PropertyType);
        Assert.Equal(typeof(Guid), ticket?.PropertyType);
        Assert.Equal(typeof(Guid), lead?.PropertyType);
    }

    [Fact]
    public void Customer_advance_transfer_has_separate_lineage_and_cannot_imply_a_second_cash_posting()
    {
        var receipt = typeof(ProspectDepositReceipt);

        Assert.NotNull(receipt.GetProperty(nameof(ProspectDepositReceipt.PostingEventId)));
        Assert.NotNull(receipt.GetProperty(nameof(ProspectDepositReceipt.CustomerAdvanceTransferPostingEventId)));
        Assert.NotNull(receipt.GetProperty(nameof(ProspectDepositReceipt.CustomerPaymentId)));
        Assert.NotEqual(
            receipt.GetProperty(nameof(ProspectDepositReceipt.PostingEventId))!.Name,
            receipt.GetProperty(nameof(ProspectDepositReceipt.CustomerAdvanceTransferPostingEventId))!.Name);
    }

    [Fact]
    public void Email_request_cannot_supply_recipient_sender_or_internal_note_fields()
    {
        var names = typeof(SendPropertyEnquiryEmailRequest).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains(nameof(SendPropertyEnquiryEmailRequest.Subject), names);
        Assert.Contains(nameof(SendPropertyEnquiryEmailRequest.Body), names);
        Assert.DoesNotContain("Recipient", names);
        Assert.DoesNotContain("Sender", names);
        Assert.DoesNotContain("IsInternal", names);
        Assert.DoesNotContain("InternalNotes", names);
    }

    [Fact]
    public void Business_partner_create_request_cannot_bypass_lifecycle_or_finance_configuration()
    {
        var names = typeof(CreatePropertyEnquiryBusinessPartnerRequest).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("ApprovalStatus", names);
        Assert.DoesNotContain("IsActive", names);
        Assert.DoesNotContain("PostingDefaults", names);
        Assert.DoesNotContain("ReceivablesDefaults", names);
        Assert.DoesNotContain("BusinessPartnerId", names);
    }

    [Fact]
    public void Public_ticket_requester_is_optional_without_weakening_internal_finance_actions()
    {
        Assert.Equal(typeof(Guid?), typeof(EhcTicket).GetProperty(nameof(EhcTicket.RequesterUserId))?.PropertyType);

        var clear = typeof(EhcPropertyEnquiriesController).GetMethod(nameof(EhcPropertyEnquiriesController.ClearDeposit));
        var reverse = typeof(EhcPropertyEnquiriesController).GetMethod(nameof(EhcPropertyEnquiriesController.ReverseDeposit));
        var configure = typeof(EhcPropertyEnquiriesController).GetMethod(nameof(EhcPropertyEnquiriesController.UpsertDepositPolicy));
        var list = typeof(EhcPropertyEnquiriesController).GetMethod(nameof(EhcPropertyEnquiriesController.GetDeposits));

        Assert.Equal(FinancePermissions.ViewFinance,
            list?.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(FinancePermissions.ReceiveCustomerPayments,
            clear?.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(FinancePermissions.ReverseArPayments,
            reverse?.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(FinancePermissions.ManageBankingSettings,
            configure?.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Fact]
    public void Deposit_policy_has_a_governed_read_contract_for_the_finance_setup_screen()
    {
        var read = typeof(EhcPropertyEnquiriesController).GetMethod(nameof(EhcPropertyEnquiriesController.GetDepositPolicy));

        Assert.Equal("prospect-deposit-policy", read?.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal(FinancePermissions.ManageBankingSettings,
            read?.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.NotNull(typeof(PropertyProspectDepositPolicyDto)
            .GetProperty(nameof(PropertyProspectDepositPolicyDto.DepositLiabilityAccountId)));
    }
}
