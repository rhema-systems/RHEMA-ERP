using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Crm;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Crm;

namespace ErpSystem.Core.Services.Crm;

public class CrmService : ICrmService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public CrmService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<CrmOverviewDto> GetOverviewAsync(int take = 10)
    {
        take = ClampTake(take);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var followUpWindow = now.AddDays(14);
        var contractWindow = now.AddDays(90);

        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var activityRepository = _unitOfWork.Repository<Activity>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var tenderInvitationRepository = _unitOfWork.Repository<TenderInvitation>();
        var tenderBidRepository = _unitOfWork.Repository<TenderBid>();
        var tenderAwardRepository = _unitOfWork.Repository<TenderAward>();

        var leads = (await leadRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var opportunities = (await opportunityRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var quotes = (await quoteRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var activities = (await activityRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var businessPartners = (await businessPartnerRepository.FindAsync(x => x.TenantId == tenantId && x.IsActive)).ToList();

        var partnerIds = businessPartners.Select(x => x.Id).ToHashSet();
        var projects = partnerIds.Count == 0
            ? new List<Project>()
            : (await projectRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.BusinessPartnerId.HasValue
                    && partnerIds.Contains(x.BusinessPartnerId.Value)))
                .ToList();
        var contracts = partnerIds.Count == 0
            ? new List<Contract>()
            : (await contractRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderInvitations = partnerIds.Count == 0
            ? new List<TenderInvitation>()
            : (await tenderInvitationRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderBids = partnerIds.Count == 0
            ? new List<TenderBid>()
            : (await tenderBidRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderAwards = partnerIds.Count == 0
            ? new List<TenderAward>()
            : (await tenderAwardRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();

        var openOpportunities = opportunities
            .Where(x => !IsClosedOpportunityStage(x.Stage))
            .ToList();
        var opportunityLookup = opportunities.ToDictionary(x => x.Id);
        var leadLookup = leads.ToDictionary(x => x.Id);
        var businessPartnerLookup = businessPartners.ToDictionary(x => x.Id, x => x.PartnerName);
        var activeQuotes = quotes
            .Where(x => !IsClosedQuoteStatus(x.QuoteStatus))
            .ToList();
        var convertedLeadIdsByPartnerId = leads
            .Where(x => x.ConvertedCustomerId.HasValue)
            .GroupBy(x => x.ConvertedCustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToHashSet());
        var opportunityLeadIdsByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue && x.LeadId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.LeadId!.Value).ToHashSet());
        var leadFollowUps = leads
            .Where(x => NeedsLeadFollowUp(x, followUpWindow))
            .ToList();
        var activityFollowUps = activities
            .Where(x => NeedsActivityFollowUp(x, followUpWindow))
            .ToList();

        var projectsByPartnerId = projects
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var contractsByPartnerId = contracts
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var invitationsByPartnerId = tenderInvitations
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var bidsByPartnerId = tenderBids
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var awardsByPartnerId = tenderAwards
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var openOpportunitiesByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var activeQuotesByPartnerId = activeQuotes
            .Select(x => new
            {
                Quote = x,
                BusinessPartnerId = ResolveQuoteBusinessPartnerId(x, opportunityLookup)
            })
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Quote).ToList());

        var accounts = businessPartners
            .Select(partner =>
            {
                var partnerProjects = projectsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Project>();
                var partnerContracts = contractsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Contract>();
                var partnerInvitations = invitationsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderInvitation>();
                var partnerBids = bidsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderBid>();
                var partnerAwards = awardsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderAward>();
                var partnerOpportunities = openOpportunitiesByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Opportunity>();
                var partnerQuotes = activeQuotesByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Quote>();

                return BuildAccountOverview(
                    partner,
                    partnerProjects,
                    partnerContracts,
                    partnerInvitations,
                    partnerBids,
                    partnerAwards,
                    partnerOpportunities,
                    partnerQuotes,
                    GetRelatedLeadCount(partner.Id, convertedLeadIdsByPartnerId, opportunityLeadIdsByPartnerId),
                    now,
                    contractWindow);
            })
            .Where(IsCrmRelevantAccount)
            .OrderByDescending(x => x.ContractValue + x.ProjectValue + x.TenderAwardedValue)
            .ThenByDescending(x => x.ActiveProjectCount + x.ActiveContractCount)
            .ThenBy(x => x.PartnerName)
            .Take(take)
            .ToList();

        var followUps = leadFollowUps
            .Select(x => new CrmFollowUpOverviewDto
            {
                EntityType = "Lead",
                EntityId = x.Id,
                Title = string.IsNullOrWhiteSpace(x.CompanyName) ? GetLeadFullName(x) : $"{GetLeadFullName(x)} | {x.CompanyName}",
                Status = x.LeadStatus,
                DueDate = x.NextFollowUpDate ?? x.LastContactDate ?? now,
                Context = x.LeadSource
            })
            .Concat(activityFollowUps.Select(x => new CrmFollowUpOverviewDto
            {
                EntityType = "Activity",
                EntityId = x.Id,
                Title = x.Subject,
                Status = x.ActivityStatus,
                DueDate = x.DueDate ?? x.NextFollowUpDate ?? x.ActivityDate,
                Context = x.ActivityType
            }))
            .OrderBy(x => x.DueDate)
            .Take(take)
            .ToList();

        return new CrmOverviewDto
        {
            TotalLeadCount = leads.Count,
            QualifiedLeadCount = leads.Count(x => string.Equals(x.LeadStatus, "Qualified", StringComparison.OrdinalIgnoreCase)),
            LeadsNeedingFollowUpCount = leadFollowUps.Count,
            OpenOpportunityCount = openOpportunities.Count,
            OpenOpportunityValue = decimal.Round(openOpportunities.Sum(x => x.Amount), 2),
            WeightedPipelineValue = decimal.Round(openOpportunities.Sum(x => x.Amount * x.Probability / 100m), 2),
            ActiveQuoteCount = activeQuotes.Count,
            ActiveQuoteValue = decimal.Round(activeQuotes.Sum(ResolveQuoteValue), 2),
            ActiveAccountCount = accounts.Count,
            AtRiskAccountCount = accounts.Count(x => x.IsAtRisk),
            AverageAccountHealthScore = accounts.Count == 0
                ? 0m
                : decimal.Round((decimal)accounts.Average(x => x.HealthScore), 1),
            ActiveProjectCount = projects.Count(IsActiveProject),
            ActiveContractCount = contracts.Count(IsActiveContract),
            ExpiringContractCount = contracts.Count(x => IsActiveContract(x) && x.EndDate.HasValue && x.EndDate.Value <= contractWindow),
            Accounts = accounts,
            Opportunities = openOpportunities
                .OrderByDescending(x => x.Amount * x.Probability / 100m)
                .ThenBy(x => x.ExpectedCloseDate)
                .Take(take)
                .Select(x => MapOpportunityOverview(x, businessPartnerLookup, leadLookup))
                .ToList(),
            FollowUps = followUps
        };
    }

    public async Task<CrmAccountDetailDto?> GetAccountDetailAsync(Guid businessPartnerId, int take = 10)
    {
        take = ClampTake(take);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var followUpWindow = now.AddDays(14);
        var contractWindow = now.AddDays(90);

        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var activityRepository = _unitOfWork.Repository<Activity>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var tenderRepository = _unitOfWork.Repository<Tender>();
        var tenderInvitationRepository = _unitOfWork.Repository<TenderInvitation>();
        var tenderBidRepository = _unitOfWork.Repository<TenderBid>();
        var tenderAwardRepository = _unitOfWork.Repository<TenderAward>();
        var salesOrderRepository = _unitOfWork.Repository<SalesOrder>();
        var salesAgreementRepository = _unitOfWork.Repository<SalesAgreement>();
        var salesAllocationRepository = _unitOfWork.Repository<SalesAllocation>();
        var returnOrderRepository = _unitOfWork.Repository<ReturnOrder>();
        var creditNoteRepository = _unitOfWork.Repository<CreditNote>();
        var refundRepository = _unitOfWork.Repository<Refund>();

        var account = await businessPartnerRepository.GetByIdAsync(businessPartnerId, x => x.Contacts);
        if (account == null || account.TenantId != tenantId)
        {
            return null;
        }

        var convertedLeads = (await leadRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.ConvertedCustomerId.HasValue
                && x.ConvertedCustomerId.Value == businessPartnerId))
            .ToList();
        var directLeadIds = convertedLeads.Select(x => x.Id).ToHashSet();

        var relatedOpportunities = (await opportunityRepository.FindAsync(x =>
                x.TenantId == tenantId
                && ((x.CustomerId.HasValue && x.CustomerId.Value == businessPartnerId)
                    || (x.LeadId.HasValue && directLeadIds.Contains(x.LeadId.Value)))))
            .ToList();

        var relatedLeadIds = relatedOpportunities
            .Where(x => x.LeadId.HasValue)
            .Select(x => x.LeadId!.Value)
            .ToHashSet();
        var additionalLeadIds = relatedLeadIds.Except(directLeadIds).ToHashSet();
        if (additionalLeadIds.Count > 0)
        {
            convertedLeads.AddRange(await leadRepository.FindAsync(x =>
                x.TenantId == tenantId
                && additionalLeadIds.Contains(x.Id)));
        }

        var leadLookup = convertedLeads
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToDictionary(x => x.Id);
        var allRelatedLeadIds = leadLookup.Keys.ToHashSet();
        var opportunityIds = relatedOpportunities.Select(x => x.Id).ToHashSet();

        var relatedQuotes = (await quoteRepository.FindAsync(x =>
                x.TenantId == tenantId
                && ((x.CustomerId.HasValue && x.CustomerId.Value == businessPartnerId)
                    || opportunityIds.Contains(x.OpportunityId))))
            .ToList();
        var relatedActivities = (await activityRepository.FindAsync(x =>
                x.TenantId == tenantId
                && ((x.CustomerId.HasValue && x.CustomerId.Value == businessPartnerId)
                    || (x.OpportunityId.HasValue && opportunityIds.Contains(x.OpportunityId.Value))
                    || (x.LeadId.HasValue && allRelatedLeadIds.Contains(x.LeadId.Value)))))
            .ToList();
        var contracts = (await contractRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var relatedContractIds = contracts.Select(x => x.Id).ToHashSet();
        var projects = (await projectRepository.FindAsync(x =>
                x.TenantId == tenantId
                && ((x.BusinessPartnerId.HasValue && x.BusinessPartnerId.Value == businessPartnerId)
                    || (x.ContractId.HasValue && relatedContractIds.Contains(x.ContractId.Value)))))
            .ToList();
        var tenderInvitations = (await tenderInvitationRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var tenderBids = (await tenderBidRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var tenderAwards = (await tenderAwardRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var salesOrders = (await salesOrderRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var salesAgreements = (await salesAgreementRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var salesAllocations = (await salesAllocationRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId.HasValue
                && x.BusinessPartnerId.Value == businessPartnerId))
            .ToList();
        var salesOrderIds = salesOrders.Select(x => x.Id).ToHashSet();
        var returnOrders = salesOrderIds.Count == 0
            ? new List<ReturnOrder>()
            : (await returnOrderRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && salesOrderIds.Contains(x.SalesOrderId)))
                .ToList();
        var returnOrderIds = returnOrders.Select(x => x.Id).ToHashSet();
        var creditNotes = returnOrderIds.Count == 0
            ? new List<CreditNote>()
            : (await creditNoteRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.ReturnOrderId.HasValue
                    && returnOrderIds.Contains(x.ReturnOrderId.Value)))
                .ToList();
        var creditNoteIds = creditNotes.Select(x => x.Id).ToHashSet();
        var refunds = (returnOrderIds.Count == 0 && creditNoteIds.Count == 0)
            ? new List<Refund>()
            : (await refundRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && ((x.ReturnOrderId.HasValue && returnOrderIds.Contains(x.ReturnOrderId.Value))
                        || (x.CreditNoteId.HasValue && creditNoteIds.Contains(x.CreditNoteId.Value)))))
                .ToList();
        var relatedTenderIds = tenderInvitations.Select(x => x.TenderId)
            .Concat(tenderBids.Select(x => x.TenderId))
            .Concat(tenderAwards.Select(x => x.TenderId))
            .Distinct()
            .ToHashSet();
        var tenderLookup = relatedTenderIds.Count == 0
            ? new Dictionary<Guid, Tender>()
            : (await tenderRepository.FindAsync(x => x.TenantId == tenantId && relatedTenderIds.Contains(x.Id)))
                .ToDictionary(x => x.Id);

        var activeQuotes = relatedQuotes.Where(x => !IsClosedQuoteStatus(x.QuoteStatus)).ToList();
        var openOpportunities = relatedOpportunities.Where(x => !IsClosedOpportunityStage(x.Stage)).ToList();
        var relatedLeads = leadLookup.Values
            .OrderBy(x => x.NextFollowUpDate ?? DateTime.MaxValue)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();
        var accountOverview = BuildAccountOverview(
            account,
            projects,
            contracts,
            tenderInvitations,
            tenderBids,
            tenderAwards,
            openOpportunities,
            activeQuotes,
            relatedLeads.Count,
            now,
            contractWindow);
        var healthSignals = BuildAccountHealthSignals(
            account,
            projects,
            contracts,
            tenderInvitations,
            tenderBids,
            tenderAwards,
            openOpportunities,
            activeQuotes,
            relatedLeads.Any(x => NeedsLeadFollowUp(x, followUpWindow)),
            relatedActivities.Any(x => NeedsActivityFollowUp(x, followUpWindow)),
            now,
            contractWindow);

        return new CrmAccountDetailDto
        {
            BusinessPartnerId = account.Id,
            PartnerCode = account.PartnerCode,
            PartnerName = account.PartnerName,
            PartnerType = account.PartnerType,
            RegistrationStatus = account.RegistrationStatus,
            CustomerType = account.CustomerType,
            SalesTerritory = account.SalesTerritory,
            RiskLevel = account.RiskLevel,
            PerformanceRating = account.PerformanceRating,
            PrimaryContactName = account.PrimaryContactName,
            PrimaryContactTitle = account.PrimaryContactTitle,
            PrimaryEmail = account.PrimaryEmail,
            PrimaryPhone = account.PrimaryPhone,
            Website = account.Website,
            PhysicalAddress = account.PhysicalAddress,
            PhysicalCity = account.PhysicalCity,
            PhysicalCountry = account.PhysicalCountry,
            PaymentTerms = account.PaymentTerms,
            Currency = ResolveCurrencyCode(
                account.Currency,
                activeQuotes.Select(x => x.Currency).Concat(openOpportunities.Select(x => x.Currency))),
            CreditLimit = account.CreditLimit,
            OutstandingBalance = account.OutstandingBalance,
            IsOnCreditHold = account.IsOnCreditHold,
            CreditHoldReason = account.CreditHoldReason,
            CustomerSince = account.CustomerSince,
            Notes = account.Notes,
            HasOpenFollowUp = accountOverview.HasOpenFollowUp
                || relatedLeads.Any(x => NeedsLeadFollowUp(x, followUpWindow))
                || relatedActivities.Any(x => NeedsActivityFollowUp(x, followUpWindow)),
            IsAtRisk = accountOverview.IsAtRisk,
            HealthScore = accountOverview.HealthScore,
            HealthCategory = accountOverview.HealthCategory,
            NextMilestoneDate = accountOverview.NextMilestoneDate,
            RelatedLeadCount = relatedLeads.Count,
            OpenOpportunityCount = openOpportunities.Count,
            OpenOpportunityValue = decimal.Round(openOpportunities.Sum(x => x.Amount), 2),
            WeightedPipelineValue = decimal.Round(openOpportunities.Sum(x => x.Amount * x.Probability / 100m), 2),
            ActiveQuoteCount = activeQuotes.Count,
            ActiveQuoteValue = decimal.Round(activeQuotes.Sum(ResolveQuoteValue), 2),
            ActiveProjectCount = projects.Count(IsActiveProject),
            ProjectValue = decimal.Round(projects.Sum(x => x.ApprovedBudget ?? x.EstimatedBudget ?? 0m), 2),
            ActiveContractCount = contracts.Count(IsActiveContract),
            ContractValue = decimal.Round(contracts.Sum(x => x.ContractValue), 2),
            ExpiringContractCount = contracts.Count(x => IsActiveContract(x) && x.EndDate.HasValue && x.EndDate.Value <= contractWindow),
            TenderInvitationCount = tenderInvitations.Count,
            TenderBidCount = tenderBids.Count,
            TenderAwardCount = tenderAwards.Count,
            TenderAwardedValue = decimal.Round(tenderAwards.Sum(x => x.AwardedAmount), 2),
            Contacts = account.Contacts
                .OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.ContactName)
                .Take(take)
                .Select(MapAccountContact)
                .ToList(),
            Leads = relatedLeads
                .Take(take)
                .Select(x => MapLeadListItem(x, relatedOpportunities))
                .ToList(),
            Opportunities = openOpportunities
                .OrderByDescending(x => x.Amount * x.Probability / 100m)
                .ThenBy(x => x.ExpectedCloseDate)
                .Take(take)
                .Select(x => MapOpportunityOverview(x, new Dictionary<Guid, string> { [businessPartnerId] = account.PartnerName }, leadLookup))
                .ToList(),
            Quotes = relatedQuotes
                .OrderByDescending(x => x.ValidUntil)
                .Take(take)
                .Select(x => MapQuoteSummary(
                    x,
                    new Dictionary<Guid, Opportunity>(relatedOpportunities.ToDictionary(y => y.Id)),
                    new Dictionary<Guid, string> { [businessPartnerId] = account.PartnerName },
                    leadLookup))
                .ToList(),
            Activities = relatedActivities
                .Select(x => MapActivitySummary(
                    x,
                    new Dictionary<Guid, Opportunity>(relatedOpportunities.ToDictionary(y => y.Id)),
                    new Dictionary<Guid, string> { [businessPartnerId] = account.PartnerName },
                    leadLookup))
                .Concat(BuildSalesMilestoneActivities(
                    salesOrders,
                    salesAgreements,
                    salesAllocations,
                    returnOrders,
                    creditNotes,
                    refunds,
                    account))
                .OrderByDescending(x => x.ActivityDate)
                .ThenByDescending(x => x.DueDate ?? DateTime.MinValue)
                .ThenBy(x => x.Subject)
                .Take(take)
                .ToList(),
            Projects = projects
                .OrderByDescending(x => x.TargetEndDate)
                .Take(take)
                .Select(x => MapProjectSummary(x))
                .ToList(),
            Contracts = contracts
                .OrderByDescending(x => x.EndDate)
                .Take(take)
                .Select(x => MapContractSummary(x))
                .ToList(),
            Tenders = BuildTenderSummaries(
                    tenderInvitations,
                    tenderBids,
                    tenderAwards,
                    tenderLookup,
                    account,
                    contracts)
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .ToList(),
            HealthSignals = healthSignals
        };
    }

    public async Task<PagedResult<CrmAccountOverviewDto>> GetAccountsAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? healthCategory = null,
        bool atRiskOnly = false,
        string? partnerType = null)
    {
        page = Math.Max(page, 1);
        pageSize = ClampPageSize(pageSize);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var contractWindow = now.AddDays(90);

        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var tenderInvitationRepository = _unitOfWork.Repository<TenderInvitation>();
        var tenderBidRepository = _unitOfWork.Repository<TenderBid>();
        var tenderAwardRepository = _unitOfWork.Repository<TenderAward>();

        var leads = (await leadRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var businessPartners = (await businessPartnerRepository.FindAsync(x => x.TenantId == tenantId && x.IsActive)).ToList();
        var partnerIds = businessPartners.Select(x => x.Id).ToHashSet();

        var projects = partnerIds.Count == 0
            ? new List<Project>()
            : (await projectRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.BusinessPartnerId.HasValue
                    && partnerIds.Contains(x.BusinessPartnerId.Value)))
                .ToList();
        var contracts = partnerIds.Count == 0
            ? new List<Contract>()
            : (await contractRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderInvitations = partnerIds.Count == 0
            ? new List<TenderInvitation>()
            : (await tenderInvitationRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderBids = partnerIds.Count == 0
            ? new List<TenderBid>()
            : (await tenderBidRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderAwards = partnerIds.Count == 0
            ? new List<TenderAward>()
            : (await tenderAwardRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var opportunities = partnerIds.Count == 0
            ? new List<Opportunity>()
            : (await opportunityRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.CustomerId.HasValue
                    && partnerIds.Contains(x.CustomerId.Value)))
                .ToList();
        var quotes = (await quoteRepository.FindAsync(x => x.TenantId == tenantId)).ToList();

        var openOpportunities = opportunities
            .Where(x => !IsClosedOpportunityStage(x.Stage))
            .ToList();
        var opportunityLookup = opportunities.ToDictionary(x => x.Id);
        var activeQuotes = quotes
            .Where(x => !IsClosedQuoteStatus(x.QuoteStatus))
            .ToList();
        var convertedLeadIdsByPartnerId = leads
            .Where(x => x.ConvertedCustomerId.HasValue)
            .GroupBy(x => x.ConvertedCustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToHashSet());
        var opportunityLeadIdsByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue && x.LeadId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.LeadId!.Value).ToHashSet());

        var projectsByPartnerId = projects
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var contractsByPartnerId = contracts
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var invitationsByPartnerId = tenderInvitations
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var bidsByPartnerId = tenderBids
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var awardsByPartnerId = tenderAwards
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var openOpportunitiesByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var activeQuotesByPartnerId = activeQuotes
            .Select(x => new
            {
                Quote = x,
                BusinessPartnerId = ResolveQuoteBusinessPartnerId(x, opportunityLookup)
            })
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Quote).ToList());

        var filtered = businessPartners
            .Select(partner => BuildAccountOverview(
                partner,
                projectsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Project>(),
                contractsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Contract>(),
                invitationsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderInvitation>(),
                bidsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderBid>(),
                awardsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderAward>(),
                openOpportunitiesByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Opportunity>(),
                activeQuotesByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Quote>(),
                GetRelatedLeadCount(partner.Id, convertedLeadIdsByPartnerId, opportunityLeadIdsByPartnerId),
                now,
                contractWindow))
            .Where(IsCrmRelevantAccount);

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
                ContainsText(x.PartnerCode, search)
                || ContainsText(x.PartnerName, search)
                || ContainsText(x.PartnerType, search)
                || ContainsText(x.CustomerType, search)
                || ContainsText(x.SalesTerritory, search)
                || ContainsText(x.RiskLevel, search));
        }

        if (!string.IsNullOrWhiteSpace(healthCategory))
        {
            filtered = filtered.Where(x => string.Equals(x.HealthCategory, healthCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (atRiskOnly)
        {
            filtered = filtered.Where(x => x.IsAtRisk);
        }

        if (!string.IsNullOrWhiteSpace(partnerType))
        {
            filtered = filtered.Where(x => string.Equals(x.PartnerType, partnerType, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = filtered
            .OrderByDescending(x => x.IsAtRisk)
            .ThenBy(x => x.NextMilestoneDate ?? DateTime.MaxValue)
            .ThenByDescending(x => x.OpenOpportunityValue + x.ContractValue + x.ProjectValue + x.TenderAwardedValue)
            .ThenBy(x => x.PartnerName)
            .ToList();
        var totalCount = ordered.Count;
        var pagedItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<CrmAccountOverviewDto>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<CrmContactListItemDto>> GetContactsAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        Guid? businessPartnerId = null,
        string? department = null,
        bool primaryOnly = false,
        bool atRiskOnly = false,
        string? partnerType = null)
    {
        page = Math.Max(page, 1);
        pageSize = ClampPageSize(pageSize);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var contractWindow = now.AddDays(90);

        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var tenderInvitationRepository = _unitOfWork.Repository<TenderInvitation>();
        var tenderBidRepository = _unitOfWork.Repository<TenderBid>();
        var tenderAwardRepository = _unitOfWork.Repository<TenderAward>();

        var leads = (await leadRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var businessPartners = (await businessPartnerRepository.FindAsync(
                x => x.TenantId == tenantId
                    && x.IsActive
                    && (!businessPartnerId.HasValue || x.Id == businessPartnerId.Value),
                x => x.Contacts))
            .ToList();
        var partnerIds = businessPartners.Select(x => x.Id).ToHashSet();

        var projects = partnerIds.Count == 0
            ? new List<Project>()
            : (await projectRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.BusinessPartnerId.HasValue
                    && partnerIds.Contains(x.BusinessPartnerId.Value)))
                .ToList();
        var contracts = partnerIds.Count == 0
            ? new List<Contract>()
            : (await contractRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderInvitations = partnerIds.Count == 0
            ? new List<TenderInvitation>()
            : (await tenderInvitationRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderBids = partnerIds.Count == 0
            ? new List<TenderBid>()
            : (await tenderBidRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderAwards = partnerIds.Count == 0
            ? new List<TenderAward>()
            : (await tenderAwardRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var opportunities = partnerIds.Count == 0
            ? new List<Opportunity>()
            : (await opportunityRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.CustomerId.HasValue
                    && partnerIds.Contains(x.CustomerId.Value)))
                .ToList();
        var quotes = (await quoteRepository.FindAsync(x => x.TenantId == tenantId)).ToList();

        var openOpportunities = opportunities
            .Where(x => !IsClosedOpportunityStage(x.Stage))
            .ToList();
        var opportunityLookup = opportunities.ToDictionary(x => x.Id);
        var activeQuotes = quotes
            .Where(x => !IsClosedQuoteStatus(x.QuoteStatus))
            .ToList();
        var convertedLeadIdsByPartnerId = leads
            .Where(x => x.ConvertedCustomerId.HasValue)
            .GroupBy(x => x.ConvertedCustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToHashSet());
        var opportunityLeadIdsByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue && x.LeadId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.LeadId!.Value).ToHashSet());

        var projectsByPartnerId = projects
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var contractsByPartnerId = contracts
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var invitationsByPartnerId = tenderInvitations
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var bidsByPartnerId = tenderBids
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var awardsByPartnerId = tenderAwards
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var openOpportunitiesByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var activeQuotesByPartnerId = activeQuotes
            .Select(x => new
            {
                Quote = x,
                BusinessPartnerId = ResolveQuoteBusinessPartnerId(x, opportunityLookup)
            })
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Quote).ToList());

        var filtered = businessPartners
            .Select(partner => new
            {
                Partner = partner,
                Account = BuildAccountOverview(
                    partner,
                    projectsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Project>(),
                    contractsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Contract>(),
                    invitationsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderInvitation>(),
                    bidsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderBid>(),
                    awardsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderAward>(),
                    openOpportunitiesByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Opportunity>(),
                    activeQuotesByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Quote>(),
                    GetRelatedLeadCount(partner.Id, convertedLeadIdsByPartnerId, opportunityLeadIdsByPartnerId),
                    now,
                    contractWindow)
            })
            .Where(x => IsCrmRelevantAccount(x.Account))
            .SelectMany(x => x.Partner.Contacts
                .Where(contact => !string.IsNullOrWhiteSpace(contact.ContactName))
                .Select(contact => MapContactListItem(contact, x.Account)));

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
                ContainsText(x.ContactName, search)
                || ContainsText(x.ContactTitle, search)
                || ContainsText(x.Department, search)
                || ContainsText(x.Email, search)
                || ContainsText(x.Phone, search)
                || ContainsText(x.Mobile, search)
                || ContainsText(x.PartnerCode, search)
                || ContainsText(x.PartnerName, search)
                || ContainsText(x.PartnerType, search)
                || ContainsText(x.SalesTerritory, search)
                || ContainsText(x.CustomerType, search));
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            filtered = filtered.Where(x => ContainsText(x.Department, department));
        }

        if (primaryOnly)
        {
            filtered = filtered.Where(x => x.IsPrimary);
        }

        if (atRiskOnly)
        {
            filtered = filtered.Where(x => x.IsAtRisk);
        }

        if (!string.IsNullOrWhiteSpace(partnerType))
        {
            filtered = filtered.Where(x => string.Equals(x.PartnerType, partnerType, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = filtered
            .OrderByDescending(x => x.IsAtRisk)
            .ThenByDescending(x => x.IsPrimary)
            .ThenBy(x => x.NextMilestoneDate ?? DateTime.MaxValue)
            .ThenBy(x => x.PartnerName)
            .ThenBy(x => x.ContactName)
            .ToList();
        var totalCount = ordered.Count;
        var pagedItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<CrmContactListItemDto>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<CrmReadinessListItemDto>> GetReadinessAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? readinessCategory = null,
        bool expiringOnly = false,
        bool missingFinancialsOnly = false,
        string? partnerType = null)
    {
        page = Math.Max(page, 1);
        pageSize = ClampPageSize(pageSize);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var contractWindow = now.AddDays(90);
        var readinessWindow = now.AddDays(60);

        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var tenderInvitationRepository = _unitOfWork.Repository<TenderInvitation>();
        var tenderBidRepository = _unitOfWork.Repository<TenderBid>();
        var tenderAwardRepository = _unitOfWork.Repository<TenderAward>();

        var leads = (await leadRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var businessPartners = (await businessPartnerRepository.FindAsync(
                x => x.TenantId == tenantId && x.IsActive,
                x => x.Documents,
                x => x.Licenses,
                x => x.Financials))
            .ToList();
        var partnerIds = businessPartners.Select(x => x.Id).ToHashSet();

        var projects = partnerIds.Count == 0
            ? new List<Project>()
            : (await projectRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.BusinessPartnerId.HasValue
                    && partnerIds.Contains(x.BusinessPartnerId.Value)))
                .ToList();
        var contracts = partnerIds.Count == 0
            ? new List<Contract>()
            : (await contractRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderInvitations = partnerIds.Count == 0
            ? new List<TenderInvitation>()
            : (await tenderInvitationRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderBids = partnerIds.Count == 0
            ? new List<TenderBid>()
            : (await tenderBidRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderAwards = partnerIds.Count == 0
            ? new List<TenderAward>()
            : (await tenderAwardRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var opportunities = partnerIds.Count == 0
            ? new List<Opportunity>()
            : (await opportunityRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.CustomerId.HasValue
                    && partnerIds.Contains(x.CustomerId.Value)))
                .ToList();
        var quotes = (await quoteRepository.FindAsync(x => x.TenantId == tenantId)).ToList();

        var openOpportunities = opportunities
            .Where(x => !IsClosedOpportunityStage(x.Stage))
            .ToList();
        var opportunityLookup = opportunities.ToDictionary(x => x.Id);
        var activeQuotes = quotes
            .Where(x => !IsClosedQuoteStatus(x.QuoteStatus))
            .ToList();
        var convertedLeadIdsByPartnerId = leads
            .Where(x => x.ConvertedCustomerId.HasValue)
            .GroupBy(x => x.ConvertedCustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToHashSet());
        var opportunityLeadIdsByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue && x.LeadId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.LeadId!.Value).ToHashSet());

        var projectsByPartnerId = projects
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var contractsByPartnerId = contracts
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var invitationsByPartnerId = tenderInvitations
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var bidsByPartnerId = tenderBids
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var awardsByPartnerId = tenderAwards
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var openOpportunitiesByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var activeQuotesByPartnerId = activeQuotes
            .Select(x => new
            {
                Quote = x,
                BusinessPartnerId = ResolveQuoteBusinessPartnerId(x, opportunityLookup)
            })
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Quote).ToList());

        var filtered = businessPartners
            .Select(partner => new
            {
                Partner = partner,
                Account = BuildAccountOverview(
                    partner,
                    projectsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Project>(),
                    contractsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Contract>(),
                    invitationsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderInvitation>(),
                    bidsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderBid>(),
                    awardsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderAward>(),
                    openOpportunitiesByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Opportunity>(),
                    activeQuotesByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Quote>(),
                    GetRelatedLeadCount(partner.Id, convertedLeadIdsByPartnerId, opportunityLeadIdsByPartnerId),
                    now,
                    contractWindow),
                Readiness = AssessAccountReadiness(
                    partner,
                    partner.Documents.ToList(),
                    partner.Licenses.ToList(),
                    partner.Financials.ToList(),
                    now,
                    readinessWindow)
            })
            .Where(x => IsCrmRelevantAccount(x.Account))
            .Select(x => BuildReadinessListItem(x.Partner, x.Account, x.Readiness));

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
                ContainsText(x.PartnerCode, search)
                || ContainsText(x.PartnerName, search)
                || ContainsText(x.PartnerType, search)
                || ContainsText(x.CustomerType, search)
                || ContainsText(x.SalesTerritory, search)
                || ContainsText(x.CreditRating, search));
        }

        if (!string.IsNullOrWhiteSpace(readinessCategory))
        {
            filtered = filtered.Where(x => string.Equals(x.ReadinessCategory, readinessCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (expiringOnly)
        {
            filtered = filtered.Where(x =>
                x.ExpiringDocumentCount > 0
                || x.ExpiredDocumentCount > 0
                || x.ExpiringLicenseCount > 0
                || x.ExpiredLicenseCount > 0);
        }

        if (missingFinancialsOnly)
        {
            filtered = filtered.Where(x => x.FinancialRecordCount == 0);
        }

        if (!string.IsNullOrWhiteSpace(partnerType))
        {
            filtered = filtered.Where(x => string.Equals(x.PartnerType, partnerType, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = filtered
            .OrderByDescending(x => x.HasCriticalGap)
            .ThenBy(x => x.NextComplianceDate ?? DateTime.MaxValue)
            .ThenBy(x => x.ReadinessScore)
            .ThenBy(x => x.PartnerName)
            .ToList();
        var totalCount = ordered.Count;
        var pagedItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<CrmReadinessListItemDto>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CrmReadinessDetailDto?> GetReadinessDetailAsync(Guid businessPartnerId, int take = 10)
    {
        take = ClampTake(take);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var contractWindow = now.AddDays(90);
        var readinessWindow = now.AddDays(60);

        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var tenderInvitationRepository = _unitOfWork.Repository<TenderInvitation>();
        var tenderBidRepository = _unitOfWork.Repository<TenderBid>();
        var tenderAwardRepository = _unitOfWork.Repository<TenderAward>();

        var partner = await businessPartnerRepository.GetByIdAsync(
            businessPartnerId,
            x => x.Documents,
            x => x.Licenses,
            x => x.Financials);
        if (partner == null || partner.TenantId != tenantId || !partner.IsActive)
        {
            return null;
        }

        var leads = (await leadRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var projects = (await projectRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId.HasValue
                && x.BusinessPartnerId.Value == businessPartnerId))
            .ToList();
        var contracts = (await contractRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var tenderInvitations = (await tenderInvitationRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var tenderBids = (await tenderBidRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var tenderAwards = (await tenderAwardRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var opportunities = (await opportunityRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.CustomerId.HasValue
                && x.CustomerId.Value == businessPartnerId))
            .ToList();
        var opportunityIds = opportunities.Select(x => x.Id).ToHashSet();
        var quotes = (await quoteRepository.FindAsync(x =>
                x.TenantId == tenantId
                && ((x.CustomerId.HasValue && x.CustomerId.Value == businessPartnerId)
                    || opportunityIds.Contains(x.OpportunityId))))
            .ToList();

        var openOpportunities = opportunities.Where(x => !IsClosedOpportunityStage(x.Stage)).ToList();
        var activeQuotes = quotes.Where(x => !IsClosedQuoteStatus(x.QuoteStatus)).ToList();
        var convertedLeadIdsByPartnerId = leads
            .Where(x => x.ConvertedCustomerId.HasValue)
            .GroupBy(x => x.ConvertedCustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToHashSet());
        var opportunityLeadIdsByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue && x.LeadId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.LeadId!.Value).ToHashSet());

        var account = BuildAccountOverview(
            partner,
            projects,
            contracts,
            tenderInvitations,
            tenderBids,
            tenderAwards,
            openOpportunities,
            activeQuotes,
            GetRelatedLeadCount(partner.Id, convertedLeadIdsByPartnerId, opportunityLeadIdsByPartnerId),
            now,
            contractWindow);
        var readiness = AssessAccountReadiness(
            partner,
            partner.Documents.ToList(),
            partner.Licenses.ToList(),
            partner.Financials.ToList(),
            now,
            readinessWindow);
        var summary = BuildReadinessListItem(partner, account, readiness);

        return new CrmReadinessDetailDto
        {
            BusinessPartnerId = summary.BusinessPartnerId,
            PartnerCode = summary.PartnerCode,
            PartnerName = summary.PartnerName,
            PartnerType = summary.PartnerType,
            RegistrationStatus = summary.RegistrationStatus,
            CustomerType = summary.CustomerType,
            SalesTerritory = summary.SalesTerritory,
            DocumentCount = summary.DocumentCount,
            VerifiedDocumentCount = summary.VerifiedDocumentCount,
            ExpiringDocumentCount = summary.ExpiringDocumentCount,
            ExpiredDocumentCount = summary.ExpiredDocumentCount,
            LicenseCount = summary.LicenseCount,
            ExpiringLicenseCount = summary.ExpiringLicenseCount,
            ExpiredLicenseCount = summary.ExpiredLicenseCount,
            FinancialRecordCount = summary.FinancialRecordCount,
            LatestFinancialYear = summary.LatestFinancialYear,
            LatestAnnualRevenue = summary.LatestAnnualRevenue,
            CreditRating = summary.CreditRating,
            OpenOpportunityCount = summary.OpenOpportunityCount,
            ActiveProjectCount = summary.ActiveProjectCount,
            ActiveContractCount = summary.ActiveContractCount,
            IsAtRisk = summary.IsAtRisk,
            HealthScore = summary.HealthScore,
            HealthCategory = summary.HealthCategory,
            ReadinessScore = summary.ReadinessScore,
            ReadinessCategory = summary.ReadinessCategory,
            HasCriticalGap = summary.HasCriticalGap,
            NextComplianceDate = summary.NextComplianceDate,
            PrimaryContactName = partner.PrimaryContactName,
            PrimaryEmail = partner.PrimaryEmail,
            PrimaryPhone = partner.PrimaryPhone,
            Documents = partner.Documents
                .OrderBy(x => x.ExpiryDate ?? DateTime.MaxValue)
                .ThenByDescending(x => x.CreatedAt)
                .Take(take)
                .Select(x => MapReadinessDocument(x, now, readinessWindow))
                .ToList(),
            Licenses = partner.Licenses
                .OrderBy(x => x.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(x => x.LicenseNumber)
                .Take(take)
                .Select(x => MapReadinessLicense(x, now, readinessWindow))
                .ToList(),
            Financials = partner.Financials
                .OrderByDescending(x => x.FiscalYear)
                .ThenByDescending(x => x.AuditDate ?? DateTime.MinValue)
                .Take(take)
                .Select(MapReadinessFinancial)
                .ToList(),
            Signals = readiness.Signals
        };
    }

    public async Task<PagedResult<CrmRiskListItemDto>> GetRiskAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? riskCategory = null,
        bool escalationOnly = false,
        bool openIncidentOnly = false,
        string? partnerType = null)
    {
        page = Math.Max(page, 1);
        pageSize = ClampPageSize(pageSize);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var contractWindow = now.AddDays(90);

        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var tenderInvitationRepository = _unitOfWork.Repository<TenderInvitation>();
        var tenderBidRepository = _unitOfWork.Repository<TenderBid>();
        var tenderAwardRepository = _unitOfWork.Repository<TenderAward>();
        var performanceMetricRepository = _unitOfWork.Repository<SupplierPerformanceMetric>();
        var qualityIncidentRepository = _unitOfWork.Repository<QualityIncident>();
        var performanceReviewRepository = _unitOfWork.Repository<PerformanceReview>();
        var blacklistAppealRepository = _unitOfWork.Repository<BlacklistAppeal>();

        var leads = (await leadRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var businessPartners = (await businessPartnerRepository.FindAsync(x => x.TenantId == tenantId && x.IsActive)).ToList();
        var partnerIds = businessPartners.Select(x => x.Id).ToHashSet();

        var projects = partnerIds.Count == 0
            ? new List<Project>()
            : (await projectRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.BusinessPartnerId.HasValue
                    && partnerIds.Contains(x.BusinessPartnerId.Value)))
                .ToList();
        var contracts = partnerIds.Count == 0
            ? new List<Contract>()
            : (await contractRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderInvitations = partnerIds.Count == 0
            ? new List<TenderInvitation>()
            : (await tenderInvitationRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderBids = partnerIds.Count == 0
            ? new List<TenderBid>()
            : (await tenderBidRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderAwards = partnerIds.Count == 0
            ? new List<TenderAward>()
            : (await tenderAwardRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var opportunities = partnerIds.Count == 0
            ? new List<Opportunity>()
            : (await opportunityRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.CustomerId.HasValue
                    && partnerIds.Contains(x.CustomerId.Value)))
                .ToList();
        var quotes = (await quoteRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var performanceMetrics = partnerIds.Count == 0
            ? new List<SupplierPerformanceMetric>()
            : (await performanceMetricRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var qualityIncidents = partnerIds.Count == 0
            ? new List<QualityIncident>()
            : (await qualityIncidentRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var performanceReviews = partnerIds.Count == 0
            ? new List<PerformanceReview>()
            : (await performanceReviewRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var blacklistAppeals = partnerIds.Count == 0
            ? new List<BlacklistAppeal>()
            : (await blacklistAppealRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();

        var openOpportunities = opportunities
            .Where(x => !IsClosedOpportunityStage(x.Stage))
            .ToList();
        var opportunityLookup = opportunities.ToDictionary(x => x.Id);
        var activeQuotes = quotes
            .Where(x => !IsClosedQuoteStatus(x.QuoteStatus))
            .ToList();
        var convertedLeadIdsByPartnerId = leads
            .Where(x => x.ConvertedCustomerId.HasValue)
            .GroupBy(x => x.ConvertedCustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToHashSet());
        var opportunityLeadIdsByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue && x.LeadId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.LeadId!.Value).ToHashSet());

        var projectsByPartnerId = projects
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var contractsByPartnerId = contracts
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var invitationsByPartnerId = tenderInvitations
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var bidsByPartnerId = tenderBids
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var awardsByPartnerId = tenderAwards
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var openOpportunitiesByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var activeQuotesByPartnerId = activeQuotes
            .Select(x => new
            {
                Quote = x,
                BusinessPartnerId = ResolveQuoteBusinessPartnerId(x, opportunityLookup)
            })
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Quote).ToList());
        var performanceMetricsByPartnerId = performanceMetrics
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var qualityIncidentsByPartnerId = qualityIncidents
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var performanceReviewsByPartnerId = performanceReviews
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var blacklistAppealsByPartnerId = blacklistAppeals
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var filtered = businessPartners
            .Select(partner =>
            {
                var account = BuildAccountOverview(
                    partner,
                    projectsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Project>(),
                    contractsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Contract>(),
                    invitationsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderInvitation>(),
                    bidsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderBid>(),
                    awardsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderAward>(),
                    openOpportunitiesByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Opportunity>(),
                    activeQuotesByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Quote>(),
                    GetRelatedLeadCount(partner.Id, convertedLeadIdsByPartnerId, opportunityLeadIdsByPartnerId),
                    now,
                    contractWindow);

                return new
                {
                    Partner = partner,
                    Account = account,
                    Risk = AssessAccountRisk(
                        partner,
                        account,
                        performanceMetricsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<SupplierPerformanceMetric>(),
                        qualityIncidentsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<QualityIncident>(),
                        performanceReviewsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<PerformanceReview>(),
                        blacklistAppealsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<BlacklistAppeal>(),
                        now)
                };
            })
            .Where(x => IsCrmRelevantAccount(x.Account))
            .Select(x => BuildRiskListItem(x.Partner, x.Account, x.Risk));

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
                ContainsText(x.PartnerCode, search)
                || ContainsText(x.PartnerName, search)
                || ContainsText(x.PartnerType, search)
                || ContainsText(x.CustomerType, search)
                || ContainsText(x.SalesTerritory, search)
                || ContainsText(x.RiskLevel, search)
                || ContainsText(x.LatestMetricGrade, search)
                || ContainsText(x.LatestMetricPeriod, search));
        }

        if (!string.IsNullOrWhiteSpace(riskCategory))
        {
            filtered = filtered.Where(x => string.Equals(x.RiskCategory, riskCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (escalationOnly)
        {
            filtered = filtered.Where(x => x.RequiresEscalation);
        }

        if (openIncidentOnly)
        {
            filtered = filtered.Where(x => x.OpenIncidentCount > 0);
        }

        if (!string.IsNullOrWhiteSpace(partnerType))
        {
            filtered = filtered.Where(x => string.Equals(x.PartnerType, partnerType, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = filtered
            .OrderByDescending(x => x.RequiresEscalation)
            .ThenByDescending(x => x.RiskScore)
            .ThenByDescending(x => x.CriticalIncidentCount)
            .ThenByDescending(x => x.OpenIncidentCount)
            .ThenBy(x => x.NextMilestoneDate ?? DateTime.MaxValue)
            .ThenBy(x => x.PartnerName)
            .ToList();
        var totalCount = ordered.Count;
        var pagedItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<CrmRiskListItemDto>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CrmRiskDetailDto?> GetRiskDetailAsync(Guid businessPartnerId, int take = 10)
    {
        take = ClampTake(take);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var contractWindow = now.AddDays(90);

        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var tenderInvitationRepository = _unitOfWork.Repository<TenderInvitation>();
        var tenderBidRepository = _unitOfWork.Repository<TenderBid>();
        var tenderAwardRepository = _unitOfWork.Repository<TenderAward>();
        var performanceMetricRepository = _unitOfWork.Repository<SupplierPerformanceMetric>();
        var qualityIncidentRepository = _unitOfWork.Repository<QualityIncident>();
        var performanceReviewRepository = _unitOfWork.Repository<PerformanceReview>();
        var blacklistAppealRepository = _unitOfWork.Repository<BlacklistAppeal>();

        var partner = await businessPartnerRepository.GetByIdAsync(businessPartnerId);
        if (partner == null || partner.TenantId != tenantId || !partner.IsActive)
        {
            return null;
        }

        var leads = (await leadRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var projects = (await projectRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId.HasValue
                && x.BusinessPartnerId.Value == businessPartnerId))
            .ToList();
        var contracts = (await contractRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var tenderInvitations = (await tenderInvitationRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var tenderBids = (await tenderBidRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var tenderAwards = (await tenderAwardRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var opportunities = (await opportunityRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.CustomerId.HasValue
                && x.CustomerId.Value == businessPartnerId))
            .ToList();
        var opportunityIds = opportunities.Select(x => x.Id).ToHashSet();
        var quotes = (await quoteRepository.FindAsync(x =>
                x.TenantId == tenantId
                && ((x.CustomerId.HasValue && x.CustomerId.Value == businessPartnerId)
                    || opportunityIds.Contains(x.OpportunityId))))
            .ToList();
        var performanceMetrics = (await performanceMetricRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var qualityIncidents = (await qualityIncidentRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var performanceReviews = (await performanceReviewRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var blacklistAppeals = (await blacklistAppealRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();

        var openOpportunities = opportunities.Where(x => !IsClosedOpportunityStage(x.Stage)).ToList();
        var activeQuotes = quotes.Where(x => !IsClosedQuoteStatus(x.QuoteStatus)).ToList();
        var convertedLeadIdsByPartnerId = leads
            .Where(x => x.ConvertedCustomerId.HasValue)
            .GroupBy(x => x.ConvertedCustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToHashSet());
        var opportunityLeadIdsByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue && x.LeadId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.LeadId!.Value).ToHashSet());

        var account = BuildAccountOverview(
            partner,
            projects,
            contracts,
            tenderInvitations,
            tenderBids,
            tenderAwards,
            openOpportunities,
            activeQuotes,
            GetRelatedLeadCount(partner.Id, convertedLeadIdsByPartnerId, opportunityLeadIdsByPartnerId),
            now,
            contractWindow);
        var risk = AssessAccountRisk(
            partner,
            account,
            performanceMetrics,
            qualityIncidents,
            performanceReviews,
            blacklistAppeals,
            now);
        var summary = BuildRiskListItem(partner, account, risk);

        return new CrmRiskDetailDto
        {
            BusinessPartnerId = summary.BusinessPartnerId,
            PartnerCode = summary.PartnerCode,
            PartnerName = summary.PartnerName,
            PartnerType = summary.PartnerType,
            RegistrationStatus = summary.RegistrationStatus,
            CustomerType = summary.CustomerType,
            SalesTerritory = summary.SalesTerritory,
            RiskLevel = summary.RiskLevel,
            PerformanceRating = summary.PerformanceRating,
            IsBlacklisted = summary.IsBlacklisted,
            IsOnCreditHold = summary.IsOnCreditHold,
            OpenIncidentCount = summary.OpenIncidentCount,
            CriticalIncidentCount = summary.CriticalIncidentCount,
            PendingAppealCount = summary.PendingAppealCount,
            OpenReviewFollowUpCount = summary.OpenReviewFollowUpCount,
            LatestMetricScore = summary.LatestMetricScore,
            LatestMetricGrade = summary.LatestMetricGrade,
            LatestMetricPeriod = summary.LatestMetricPeriod,
            LatestMetricCalculatedAt = summary.LatestMetricCalculatedAt,
            LatestReviewDate = summary.LatestReviewDate,
            OpenOpportunityCount = summary.OpenOpportunityCount,
            ActiveProjectCount = summary.ActiveProjectCount,
            ActiveContractCount = summary.ActiveContractCount,
            IsAtRisk = summary.IsAtRisk,
            HealthScore = summary.HealthScore,
            HealthCategory = summary.HealthCategory,
            RiskScore = summary.RiskScore,
            RiskCategory = summary.RiskCategory,
            RequiresEscalation = summary.RequiresEscalation,
            NextMilestoneDate = summary.NextMilestoneDate,
            PrimaryContactName = partner.PrimaryContactName,
            PrimaryEmail = partner.PrimaryEmail,
            PrimaryPhone = partner.PrimaryPhone,
            CreditLimit = partner.CreditLimit,
            OutstandingBalance = partner.OutstandingBalance,
            PerformanceMetrics = performanceMetrics
                .OrderByDescending(x => x.CalculatedAt)
                .ThenByDescending(x => x.Year)
                .ThenByDescending(x => x.Quarter ?? 0)
                .ThenByDescending(x => x.Month ?? 0)
                .Take(take)
                .Select(MapRiskPerformanceMetric)
                .ToList(),
            Incidents = qualityIncidents
                .OrderBy(x => IsOpenQualityIncident(x) ? 0 : 1)
                .ThenByDescending(x => IsCriticalQualityIncident(x))
                .ThenByDescending(x => x.IncidentDate)
                .Take(take)
                .Select(MapRiskIncident)
                .ToList(),
            Reviews = performanceReviews
                .OrderByDescending(x => x.RequiresFollowUp)
                .ThenByDescending(x => x.ReviewDate)
                .Take(take)
                .Select(MapRiskReview)
                .ToList(),
            Appeals = blacklistAppeals
                .OrderBy(x => IsPendingBlacklistAppeal(x) ? 0 : 1)
                .ThenByDescending(x => x.AppealDate)
                .Take(take)
                .Select(MapRiskAppeal)
                .ToList(),
            Signals = risk.Signals
        };
    }

    public async Task<PagedResult<CrmCollaborationListItemDto>> GetCollaborationAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? collaborationCategory = null,
        bool enablementOnly = false,
        bool pendingOnboardingOnly = false,
        string? partnerType = null)
    {
        page = Math.Max(page, 1);
        pageSize = ClampPageSize(pageSize);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var contractWindow = now.AddDays(90);

        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var tenderInvitationRepository = _unitOfWork.Repository<TenderInvitation>();
        var tenderBidRepository = _unitOfWork.Repository<TenderBid>();
        var tenderAwardRepository = _unitOfWork.Repository<TenderAward>();
        var registrationRepository = _unitOfWork.Repository<BusinessPartnerRegistration>();
        var businessPartnerUserRepository = _unitOfWork.Repository<BusinessPartnerUser>();
        var tenderAssignmentRepository = _unitOfWork.Repository<TenderAssignment>();

        var leads = (await leadRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var businessPartners = (await businessPartnerRepository.FindAsync(x => x.TenantId == tenantId && x.IsActive)).ToList();
        var partnerIds = businessPartners.Select(x => x.Id).ToHashSet();

        var contracts = partnerIds.Count == 0
            ? new List<Contract>()
            : (await contractRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var contractLookup = contracts.ToDictionary(x => x.Id);
        var contractIds = contracts.Select(x => x.Id).ToHashSet();
        var projects = (partnerIds.Count == 0 && contractIds.Count == 0)
            ? new List<Project>()
            : (await projectRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && ((x.BusinessPartnerId.HasValue && partnerIds.Contains(x.BusinessPartnerId.Value))
                        || (x.ContractId.HasValue && contractIds.Contains(x.ContractId.Value)))))
                .ToList();
        var tenderInvitations = partnerIds.Count == 0
            ? new List<TenderInvitation>()
            : (await tenderInvitationRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderBids = partnerIds.Count == 0
            ? new List<TenderBid>()
            : (await tenderBidRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderAwards = partnerIds.Count == 0
            ? new List<TenderAward>()
            : (await tenderAwardRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var opportunities = partnerIds.Count == 0
            ? new List<Opportunity>()
            : (await opportunityRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.CustomerId.HasValue
                    && partnerIds.Contains(x.CustomerId.Value)))
                .ToList();
        var quotes = (await quoteRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var registrations = partnerIds.Count == 0
            ? new List<BusinessPartnerRegistration>()
            : (await registrationRepository.FindAsync(
                    x => x.TenantId == tenantId
                        && x.BusinessPartnerId.HasValue
                        && partnerIds.Contains(x.BusinessPartnerId.Value),
                    x => x.Documents,
                    x => x.StatusHistory))
                .ToList();
        var portalUsers = partnerIds.Count == 0
            ? new List<BusinessPartnerUser>()
            : (await businessPartnerUserRepository.FindAsync(
                    x => x.TenantId == tenantId
                        && partnerIds.Contains(x.BusinessPartnerId),
                    x => x.User))
                .ToList();
        var tenderAssignments = partnerIds.Count == 0
            ? new List<TenderAssignment>()
            : (await tenderAssignmentRepository.FindAsync(
                    x => x.TenantId == tenantId
                        && partnerIds.Contains(x.BusinessPartnerId),
                    x => x.Tender,
                    x => x.AssignedToUser))
                .ToList();

        var openOpportunities = opportunities
            .Where(x => !IsClosedOpportunityStage(x.Stage))
            .ToList();
        var opportunityLookup = opportunities.ToDictionary(x => x.Id);
        var activeQuotes = quotes
            .Where(x => !IsClosedQuoteStatus(x.QuoteStatus))
            .ToList();
        var convertedLeadIdsByPartnerId = leads
            .Where(x => x.ConvertedCustomerId.HasValue)
            .GroupBy(x => x.ConvertedCustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToHashSet());
        var opportunityLeadIdsByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue && x.LeadId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.LeadId!.Value).ToHashSet());

        var projectsByPartnerId = projects
            .Select(x => new
            {
                Project = x,
                BusinessPartnerId = ResolveProjectBusinessPartnerId(x, contractLookup)
            })
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Project).ToList());
        var contractsByPartnerId = contracts
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var invitationsByPartnerId = tenderInvitations
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var bidsByPartnerId = tenderBids
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var awardsByPartnerId = tenderAwards
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var openOpportunitiesByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var activeQuotesByPartnerId = activeQuotes
            .Select(x => new
            {
                Quote = x,
                BusinessPartnerId = ResolveQuoteBusinessPartnerId(x, opportunityLookup)
            })
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Quote).ToList());
        var registrationsByPartnerId = registrations
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var portalUsersByPartnerId = portalUsers
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var tenderAssignmentsByPartnerId = tenderAssignments
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var filtered = businessPartners
            .Select(partner =>
            {
                var partnerProjects = projectsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Project>();
                var account = BuildAccountOverview(
                    partner,
                    partnerProjects,
                    contractsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Contract>(),
                    invitationsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderInvitation>(),
                    bidsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderBid>(),
                    awardsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderAward>(),
                    openOpportunitiesByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Opportunity>(),
                    activeQuotesByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Quote>(),
                    GetRelatedLeadCount(partner.Id, convertedLeadIdsByPartnerId, opportunityLeadIdsByPartnerId),
                    now,
                    contractWindow);

                return new
                {
                    Partner = partner,
                    Account = account,
                    Collaboration = AssessAccountCollaboration(
                        partner,
                        account,
                        registrationsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<BusinessPartnerRegistration>(),
                        portalUsersByPartnerId.GetValueOrDefault(partner.Id) ?? new List<BusinessPartnerUser>(),
                        tenderAssignmentsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderAssignment>(),
                        partnerProjects,
                        now)
                };
            })
            .Where(x => IsCrmRelevantAccount(x.Account))
            .Select(x => BuildCollaborationListItem(x.Partner, x.Account, x.Collaboration));

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
                ContainsText(x.PartnerCode, search)
                || ContainsText(x.PartnerName, search)
                || ContainsText(x.PartnerType, search)
                || ContainsText(x.CustomerType, search)
                || ContainsText(x.SalesTerritory, search)
                || ContainsText(x.LatestApplicationNumber, search)
                || ContainsText(x.LatestRegistrationLifecycleStatus, search));
        }

        if (!string.IsNullOrWhiteSpace(collaborationCategory))
        {
            filtered = filtered.Where(x => string.Equals(x.CollaborationCategory, collaborationCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (enablementOnly)
        {
            filtered = filtered.Where(x => x.RequiresEnablement);
        }

        if (pendingOnboardingOnly)
        {
            filtered = filtered.Where(x => NeedsCollaborationOnboardingAttention(x.LatestRegistrationLifecycleStatus));
        }

        if (!string.IsNullOrWhiteSpace(partnerType))
        {
            filtered = filtered.Where(x => string.Equals(x.PartnerType, partnerType, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = filtered
            .OrderByDescending(x => x.RequiresEnablement)
            .ThenBy(x => x.CollaborationScore)
            .ThenByDescending(x => x.PortalProjectCount + x.CollaborationProjectCount)
            .ThenBy(x => x.NextMilestoneDate ?? DateTime.MaxValue)
            .ThenBy(x => x.PartnerName)
            .ToList();
        var totalCount = ordered.Count;
        var pagedItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<CrmCollaborationListItemDto>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CrmCollaborationDetailDto?> GetCollaborationDetailAsync(Guid businessPartnerId, int take = 10)
    {
        take = ClampTake(take);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var contractWindow = now.AddDays(90);

        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var tenderInvitationRepository = _unitOfWork.Repository<TenderInvitation>();
        var tenderBidRepository = _unitOfWork.Repository<TenderBid>();
        var tenderAwardRepository = _unitOfWork.Repository<TenderAward>();
        var registrationRepository = _unitOfWork.Repository<BusinessPartnerRegistration>();
        var businessPartnerUserRepository = _unitOfWork.Repository<BusinessPartnerUser>();
        var tenderAssignmentRepository = _unitOfWork.Repository<TenderAssignment>();

        var partner = await businessPartnerRepository.GetByIdAsync(businessPartnerId);
        if (partner == null || partner.TenantId != tenantId || !partner.IsActive)
        {
            return null;
        }

        var leads = (await leadRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var contracts = (await contractRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var contractLookup = contracts.ToDictionary(x => x.Id);
        var contractIds = contracts.Select(x => x.Id).ToHashSet();
        var projects = (await projectRepository.FindAsync(x =>
                x.TenantId == tenantId
                && ((x.BusinessPartnerId.HasValue && x.BusinessPartnerId.Value == businessPartnerId)
                    || (x.ContractId.HasValue && contractIds.Contains(x.ContractId.Value)))))
            .ToList();
        var tenderInvitations = (await tenderInvitationRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var tenderBids = (await tenderBidRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var tenderAwards = (await tenderAwardRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var opportunities = (await opportunityRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.CustomerId.HasValue
                && x.CustomerId.Value == businessPartnerId))
            .ToList();
        var opportunityIds = opportunities.Select(x => x.Id).ToHashSet();
        var quotes = (await quoteRepository.FindAsync(x =>
                x.TenantId == tenantId
                && ((x.CustomerId.HasValue && x.CustomerId.Value == businessPartnerId)
                    || opportunityIds.Contains(x.OpportunityId))))
            .ToList();
        var registrations = (await registrationRepository.FindAsync(
                x => x.TenantId == tenantId
                    && x.BusinessPartnerId.HasValue
                    && x.BusinessPartnerId.Value == businessPartnerId,
                x => x.Documents,
                x => x.StatusHistory))
            .ToList();
        var portalUsers = (await businessPartnerUserRepository.FindAsync(
                x => x.TenantId == tenantId
                    && x.BusinessPartnerId == businessPartnerId,
                x => x.User))
            .ToList();
        var tenderAssignments = (await tenderAssignmentRepository.FindAsync(
                x => x.TenantId == tenantId
                    && x.BusinessPartnerId == businessPartnerId,
                x => x.Tender,
                x => x.AssignedToUser))
            .ToList();

        var resolvedProjects = projects
            .Where(x => ResolveProjectBusinessPartnerId(x, contractLookup) == businessPartnerId)
            .ToList();
        var openOpportunities = opportunities.Where(x => !IsClosedOpportunityStage(x.Stage)).ToList();
        var activeQuotes = quotes.Where(x => !IsClosedQuoteStatus(x.QuoteStatus)).ToList();
        var convertedLeadIdsByPartnerId = leads
            .Where(x => x.ConvertedCustomerId.HasValue)
            .GroupBy(x => x.ConvertedCustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToHashSet());
        var opportunityLeadIdsByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue && x.LeadId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.LeadId!.Value).ToHashSet());

        var account = BuildAccountOverview(
            partner,
            resolvedProjects,
            contracts,
            tenderInvitations,
            tenderBids,
            tenderAwards,
            openOpportunities,
            activeQuotes,
            GetRelatedLeadCount(partner.Id, convertedLeadIdsByPartnerId, opportunityLeadIdsByPartnerId),
            now,
            contractWindow);
        var collaboration = AssessAccountCollaboration(
            partner,
            account,
            registrations,
            portalUsers,
            tenderAssignments,
            resolvedProjects,
            now);
        var summary = BuildCollaborationListItem(partner, account, collaboration);

        return new CrmCollaborationDetailDto
        {
            BusinessPartnerId = summary.BusinessPartnerId,
            PartnerCode = summary.PartnerCode,
            PartnerName = summary.PartnerName,
            PartnerType = summary.PartnerType,
            RegistrationStatus = summary.RegistrationStatus,
            CustomerType = summary.CustomerType,
            SalesTerritory = summary.SalesTerritory,
            LatestApplicationNumber = summary.LatestApplicationNumber,
            LatestRegistrationLifecycleStatus = summary.LatestRegistrationLifecycleStatus,
            LatestRegistrationSubmittedDate = summary.LatestRegistrationSubmittedDate,
            LatestRegistrationApprovedDate = summary.LatestRegistrationApprovedDate,
            RegistrationDocumentCount = summary.RegistrationDocumentCount,
            PortalUserCount = summary.PortalUserCount,
            ActivePortalUserCount = summary.ActivePortalUserCount,
            AdminUserCount = summary.AdminUserCount,
            TenderAssignmentCount = summary.TenderAssignmentCount,
            AssignedTenderCount = summary.AssignedTenderCount,
            PortalProjectCount = summary.PortalProjectCount,
            CollaborationProjectCount = summary.CollaborationProjectCount,
            OpenOpportunityCount = summary.OpenOpportunityCount,
            ActiveProjectCount = summary.ActiveProjectCount,
            ActiveContractCount = summary.ActiveContractCount,
            IsAtRisk = summary.IsAtRisk,
            HealthScore = summary.HealthScore,
            HealthCategory = summary.HealthCategory,
            CollaborationScore = summary.CollaborationScore,
            CollaborationCategory = summary.CollaborationCategory,
            RequiresEnablement = summary.RequiresEnablement,
            NextMilestoneDate = summary.NextMilestoneDate,
            PrimaryContactName = partner.PrimaryContactName,
            PrimaryEmail = partner.PrimaryEmail,
            PrimaryPhone = partner.PrimaryPhone,
            Registrations = registrations
                .OrderByDescending(x => x.SubmittedDate ?? x.CreatedAt)
                .ThenByDescending(x => x.CreatedAt)
                .Take(take)
                .Select(MapCollaborationRegistration)
                .ToList(),
            PortalUsers = portalUsers
                .OrderByDescending(x => x.IsActive)
                .ThenByDescending(x => string.Equals(x.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(x => x.GrantedAt)
                .Take(take)
                .Select(MapCollaborationPortalUser)
                .ToList(),
            TenderAssignments = tenderAssignments
                .OrderByDescending(x => x.AssignedAt)
                .Take(take)
                .Select(MapCollaborationTenderAssignment)
                .ToList(),
            Projects = resolvedProjects
                .Where(x => x.ExternalPortalAccessEnabled || x.ExternalCollaborationEnabled)
                .OrderBy(x => x.TargetEndDate ?? DateTime.MaxValue)
                .ThenBy(x => x.Title)
                .Take(take)
                .Select(MapCollaborationProject)
                .ToList(),
            Signals = collaboration.Signals
        };
    }

    public async Task<PagedResult<CrmServiceListItemDto>> GetServiceAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? serviceCategory = null,
        bool attentionOnly = false,
        bool overdueOnly = false,
        string? partnerType = null)
    {
        page = Math.Max(page, 1);
        pageSize = ClampPageSize(pageSize);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var contractWindow = now.AddDays(90);

        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var tenderInvitationRepository = _unitOfWork.Repository<TenderInvitation>();
        var tenderBidRepository = _unitOfWork.Repository<TenderBid>();
        var tenderAwardRepository = _unitOfWork.Repository<TenderAward>();
        var businessPartnerUserRepository = _unitOfWork.Repository<BusinessPartnerUser>();
        var ticketRepository = _unitOfWork.Repository<EhcTicket>();
        var problemRepository = _unitOfWork.Repository<EhcProblem>();
        var problemLinkRepository = _unitOfWork.Repository<EhcProblemTicketLink>();

        var leads = (await leadRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var businessPartners = (await businessPartnerRepository.FindAsync(x => x.TenantId == tenantId && x.IsActive)).ToList();
        var partnerIds = businessPartners.Select(x => x.Id).ToHashSet();

        var contracts = partnerIds.Count == 0
            ? new List<Contract>()
            : (await contractRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var contractLookup = contracts.ToDictionary(x => x.Id);
        var contractIds = contracts.Select(x => x.Id).ToHashSet();
        var projects = (partnerIds.Count == 0 && contractIds.Count == 0)
            ? new List<Project>()
            : (await projectRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && ((x.BusinessPartnerId.HasValue && partnerIds.Contains(x.BusinessPartnerId.Value))
                        || (x.ContractId.HasValue && contractIds.Contains(x.ContractId.Value)))))
                .ToList();
        var tenderInvitations = partnerIds.Count == 0
            ? new List<TenderInvitation>()
            : (await tenderInvitationRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderBids = partnerIds.Count == 0
            ? new List<TenderBid>()
            : (await tenderBidRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var tenderAwards = partnerIds.Count == 0
            ? new List<TenderAward>()
            : (await tenderAwardRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && partnerIds.Contains(x.BusinessPartnerId)))
                .ToList();
        var opportunities = partnerIds.Count == 0
            ? new List<Opportunity>()
            : (await opportunityRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.CustomerId.HasValue
                    && partnerIds.Contains(x.CustomerId.Value)))
                .ToList();
        var quotes = (await quoteRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var portalUsers = partnerIds.Count == 0
            ? new List<BusinessPartnerUser>()
            : (await businessPartnerUserRepository.FindAsync(
                    x => x.TenantId == tenantId
                        && partnerIds.Contains(x.BusinessPartnerId),
                    x => x.User))
                .ToList();
        var portalUserIds = portalUsers.Select(x => x.UserId).Distinct().ToHashSet();
        var tickets = portalUserIds.Count == 0
            ? new List<EhcTicket>()
            : (await ticketRepository.FindAsync(
                    x => x.TenantId == tenantId
                        && x.RequesterUserId.HasValue
                        && portalUserIds.Contains(x.RequesterUserId.Value),
                    x => x.Feedbacks,
                    x => x.Category,
                    x => x.AssignedToUser,
                    x => x.RequesterUser))
                .ToList();
        var ticketIds = tickets.Select(x => x.Id).ToHashSet();
        var problemLinks = ticketIds.Count == 0
            ? new List<EhcProblemTicketLink>()
            : (await problemLinkRepository.FindAsync(
                    x => x.TenantId == tenantId
                        && ticketIds.Contains(x.TicketId),
                    x => x.Problem))
                .ToList();
        var problemIds = problemLinks.Select(x => x.ProblemId).Distinct().ToHashSet();
        var problems = problemIds.Count == 0
            ? new List<EhcProblem>()
            : (await problemRepository.FindAsync(
                    x => x.TenantId == tenantId
                        && problemIds.Contains(x.Id),
                    x => x.OwnerUser))
                .ToList();

        var openOpportunities = opportunities
            .Where(x => !IsClosedOpportunityStage(x.Stage))
            .ToList();
        var opportunityLookup = opportunities.ToDictionary(x => x.Id);
        var activeQuotes = quotes
            .Where(x => !IsClosedQuoteStatus(x.QuoteStatus))
            .ToList();
        var convertedLeadIdsByPartnerId = leads
            .Where(x => x.ConvertedCustomerId.HasValue)
            .GroupBy(x => x.ConvertedCustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToHashSet());
        var opportunityLeadIdsByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue && x.LeadId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.LeadId!.Value).ToHashSet());

        var projectsByPartnerId = projects
            .Select(x => new
            {
                Project = x,
                BusinessPartnerId = ResolveProjectBusinessPartnerId(x, contractLookup)
            })
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Project).ToList());
        var contractsByPartnerId = contracts
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var invitationsByPartnerId = tenderInvitations
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var bidsByPartnerId = tenderBids
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var awardsByPartnerId = tenderAwards
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var openOpportunitiesByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var activeQuotesByPartnerId = activeQuotes
            .Select(x => new
            {
                Quote = x,
                BusinessPartnerId = ResolveQuoteBusinessPartnerId(x, opportunityLookup)
            })
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Quote).ToList());
        var portalUsersByPartnerId = portalUsers
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var partnerIdByRequesterId = portalUsers
            .GroupBy(x => x.UserId)
            .ToDictionary(x => x.Key, x => x.First().BusinessPartnerId);
        var ticketsByPartnerId = tickets
            .Where(x => x.RequesterUserId.HasValue && partnerIdByRequesterId.ContainsKey(x.RequesterUserId.Value))
            .GroupBy(x => partnerIdByRequesterId[x.RequesterUserId!.Value])
            .ToDictionary(x => x.Key, x => x.ToList());
        var ticketPartnerIds = tickets
            .Where(x => x.RequesterUserId.HasValue && partnerIdByRequesterId.ContainsKey(x.RequesterUserId.Value))
            .ToDictionary(x => x.Id, x => partnerIdByRequesterId[x.RequesterUserId!.Value]);
        var problemLookup = problems.ToDictionary(x => x.Id);
        var problemsByPartnerId = problemLinks
            .Where(x => ticketPartnerIds.ContainsKey(x.TicketId))
            .GroupBy(x => ticketPartnerIds[x.TicketId])
            .ToDictionary(
                x => x.Key,
                x => x
                    .Select(link => problemLookup.TryGetValue(link.ProblemId, out var problem) ? problem : link.Problem)
                    .Where(problem => problem != null)
                    .GroupBy(problem => problem!.Id)
                    .Select(group => group.First()!)
                    .ToList());

        var filtered = businessPartners
            .Select(partner =>
            {
                var partnerProjects = projectsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Project>();
                var account = BuildAccountOverview(
                    partner,
                    partnerProjects,
                    contractsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Contract>(),
                    invitationsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderInvitation>(),
                    bidsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderBid>(),
                    awardsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderAward>(),
                    openOpportunitiesByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Opportunity>(),
                    activeQuotesByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Quote>(),
                    GetRelatedLeadCount(partner.Id, convertedLeadIdsByPartnerId, opportunityLeadIdsByPartnerId),
                    now,
                    contractWindow);
                var partnerPortalUsers = portalUsersByPartnerId.GetValueOrDefault(partner.Id) ?? new List<BusinessPartnerUser>();
                var partnerTickets = ticketsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<EhcTicket>();
                var partnerProblems = problemsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<EhcProblem>();

                return new
                {
                    Partner = partner,
                    Account = account,
                    Service = AssessAccountService(account, partnerPortalUsers, partnerTickets, partnerProblems, now)
                };
            })
            .Where(x => IsCrmRelevantAccount(x.Account) || x.Service.PortalUserCount > 0 || x.Service.TicketCount > 0 || x.Service.LinkedProblemCount > 0)
            .Where(x => x.Service.TicketCount > 0 || x.Service.PortalUserCount > 0 || x.Service.LinkedProblemCount > 0)
            .Select(x => BuildServiceListItem(x.Partner, x.Account, x.Service));

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
                ContainsText(x.PartnerCode, search)
                || ContainsText(x.PartnerName, search)
                || ContainsText(x.PartnerType, search)
                || ContainsText(x.CustomerType, search)
                || ContainsText(x.SalesTerritory, search)
                || ContainsText(x.ServiceCategory, search));
        }

        if (!string.IsNullOrWhiteSpace(serviceCategory))
        {
            filtered = filtered.Where(x => string.Equals(x.ServiceCategory, serviceCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (attentionOnly)
        {
            filtered = filtered.Where(x => x.RequiresAttention);
        }

        if (overdueOnly)
        {
            filtered = filtered.Where(x => x.OverdueTicketCount > 0 || x.HasSlaBreachRisk);
        }

        if (!string.IsNullOrWhiteSpace(partnerType))
        {
            filtered = filtered.Where(x => string.Equals(x.PartnerType, partnerType, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = filtered
            .OrderByDescending(x => x.RequiresAttention)
            .ThenByDescending(x => x.OverdueTicketCount)
            .ThenByDescending(x => x.OpenProblemCount)
            .ThenBy(x => x.ServiceScore)
            .ThenByDescending(x => x.LastTicketCreatedAt)
            .ThenBy(x => x.PartnerName)
            .ToList();
        var totalCount = ordered.Count;
        var pagedItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<CrmServiceListItemDto>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CrmServiceDetailDto?> GetServiceDetailAsync(Guid businessPartnerId, int take = 10)
    {
        take = ClampTake(take);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var contractWindow = now.AddDays(90);

        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var tenderInvitationRepository = _unitOfWork.Repository<TenderInvitation>();
        var tenderBidRepository = _unitOfWork.Repository<TenderBid>();
        var tenderAwardRepository = _unitOfWork.Repository<TenderAward>();
        var businessPartnerUserRepository = _unitOfWork.Repository<BusinessPartnerUser>();
        var ticketRepository = _unitOfWork.Repository<EhcTicket>();
        var problemRepository = _unitOfWork.Repository<EhcProblem>();
        var problemLinkRepository = _unitOfWork.Repository<EhcProblemTicketLink>();

        var partner = await businessPartnerRepository.GetByIdAsync(businessPartnerId);
        if (partner == null || partner.TenantId != tenantId || !partner.IsActive)
        {
            return null;
        }

        var leads = (await leadRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var contracts = (await contractRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var contractLookup = contracts.ToDictionary(x => x.Id);
        var contractIds = contracts.Select(x => x.Id).ToHashSet();
        var projects = (await projectRepository.FindAsync(x =>
                x.TenantId == tenantId
                && ((x.BusinessPartnerId.HasValue && x.BusinessPartnerId.Value == businessPartnerId)
                    || (x.ContractId.HasValue && contractIds.Contains(x.ContractId.Value)))))
            .ToList();
        var tenderInvitations = (await tenderInvitationRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var tenderBids = (await tenderBidRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var tenderAwards = (await tenderAwardRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.BusinessPartnerId == businessPartnerId))
            .ToList();
        var opportunities = (await opportunityRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.CustomerId.HasValue
                && x.CustomerId.Value == businessPartnerId))
            .ToList();
        var opportunityIds = opportunities.Select(x => x.Id).ToHashSet();
        var quotes = (await quoteRepository.FindAsync(x =>
                x.TenantId == tenantId
                && ((x.CustomerId.HasValue && x.CustomerId.Value == businessPartnerId)
                    || opportunityIds.Contains(x.OpportunityId))))
            .ToList();
        var portalUsers = (await businessPartnerUserRepository.FindAsync(
                x => x.TenantId == tenantId
                    && x.BusinessPartnerId == businessPartnerId,
                x => x.User))
            .ToList();
        var portalUserIds = portalUsers.Select(x => x.UserId).Distinct().ToHashSet();
        var tickets = portalUserIds.Count == 0
            ? new List<EhcTicket>()
            : (await ticketRepository.FindAsync(
                    x => x.TenantId == tenantId
                        && x.RequesterUserId.HasValue
                        && portalUserIds.Contains(x.RequesterUserId.Value),
                    x => x.Feedbacks,
                    x => x.Category,
                    x => x.AssignedToUser,
                    x => x.RequesterUser))
                .ToList();
        var ticketIds = tickets.Select(x => x.Id).ToHashSet();
        var problemLinks = ticketIds.Count == 0
            ? new List<EhcProblemTicketLink>()
            : (await problemLinkRepository.FindAsync(
                    x => x.TenantId == tenantId
                        && ticketIds.Contains(x.TicketId),
                    x => x.Problem))
                .ToList();
        var problemIds = problemLinks.Select(x => x.ProblemId).Distinct().ToHashSet();
        var problems = problemIds.Count == 0
            ? new List<EhcProblem>()
            : (await problemRepository.FindAsync(
                    x => x.TenantId == tenantId
                        && problemIds.Contains(x.Id),
                    x => x.OwnerUser))
                .ToList();

        var resolvedProjects = projects
            .Where(x => ResolveProjectBusinessPartnerId(x, contractLookup) == businessPartnerId)
            .ToList();
        var openOpportunities = opportunities.Where(x => !IsClosedOpportunityStage(x.Stage)).ToList();
        var activeQuotes = quotes.Where(x => !IsClosedQuoteStatus(x.QuoteStatus)).ToList();
        var convertedLeadIdsByPartnerId = leads
            .Where(x => x.ConvertedCustomerId.HasValue)
            .GroupBy(x => x.ConvertedCustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToHashSet());
        var opportunityLeadIdsByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue && x.LeadId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.LeadId!.Value).ToHashSet());
        var problemTicketCounts = problemLinks
            .GroupBy(x => x.ProblemId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.TicketId).Distinct().Count());

        var account = BuildAccountOverview(
            partner,
            resolvedProjects,
            contracts,
            tenderInvitations,
            tenderBids,
            tenderAwards,
            openOpportunities,
            activeQuotes,
            GetRelatedLeadCount(partner.Id, convertedLeadIdsByPartnerId, opportunityLeadIdsByPartnerId),
            now,
            contractWindow);
        var service = AssessAccountService(account, portalUsers, tickets, problems, now);
        var summary = BuildServiceListItem(partner, account, service);

        return new CrmServiceDetailDto
        {
            BusinessPartnerId = summary.BusinessPartnerId,
            PartnerCode = summary.PartnerCode,
            PartnerName = summary.PartnerName,
            PartnerType = summary.PartnerType,
            RegistrationStatus = summary.RegistrationStatus,
            CustomerType = summary.CustomerType,
            SalesTerritory = summary.SalesTerritory,
            PortalUserCount = summary.PortalUserCount,
            ActivePortalUserCount = summary.ActivePortalUserCount,
            TicketCount = summary.TicketCount,
            OpenTicketCount = summary.OpenTicketCount,
            OverdueTicketCount = summary.OverdueTicketCount,
            ComplaintTicketCount = summary.ComplaintTicketCount,
            HelpdeskTicketCount = summary.HelpdeskTicketCount,
            EnquiryTicketCount = summary.EnquiryTicketCount,
            LinkedProblemCount = summary.LinkedProblemCount,
            OpenProblemCount = summary.OpenProblemCount,
            ResolvedTicketCount30Days = summary.ResolvedTicketCount30Days,
            AverageFeedbackRating = summary.AverageFeedbackRating,
            FeedbackResponseCount = summary.FeedbackResponseCount,
            OpenOpportunityCount = summary.OpenOpportunityCount,
            ActiveContractCount = summary.ActiveContractCount,
            IsAtRisk = summary.IsAtRisk,
            HealthScore = summary.HealthScore,
            HealthCategory = summary.HealthCategory,
            ServiceScore = summary.ServiceScore,
            ServiceCategory = summary.ServiceCategory,
            RequiresAttention = summary.RequiresAttention,
            HasSlaBreachRisk = summary.HasSlaBreachRisk,
            LastTicketCreatedAt = summary.LastTicketCreatedAt,
            LastResolvedAt = summary.LastResolvedAt,
            NextMilestoneDate = summary.NextMilestoneDate,
            PrimaryContactName = partner.PrimaryContactName,
            PrimaryEmail = partner.PrimaryEmail,
            PrimaryPhone = partner.PrimaryPhone,
            Tickets = tickets
                .OrderByDescending(x => IsTicketOverdue(x, now))
                .ThenByDescending(x => IsOpenTicketStatus(x.Status))
                .ThenByDescending(x => x.CreatedAt)
                .Take(take)
                .Select(x => MapServiceTicket(x, now))
                .ToList(),
            Problems = problems
                .OrderByDescending(x => IsOpenProblemStatus(x.Status))
                .ThenByDescending(x => x.CreatedAt)
                .Take(take)
                .Select(x => MapServiceProblem(x, problemTicketCounts.GetValueOrDefault(x.Id)))
                .ToList(),
            Signals = service.Signals
        };
    }

    public async Task<PagedResult<CrmLeadListItemDto>> GetLeadsAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? status = null,
        bool followUpOnly = false)
    {
        page = Math.Max(page, 1);
        pageSize = ClampPageSize(pageSize);

        var tenantId = _currentUserProvider.TenantId;
        var followUpWindow = DateTime.UtcNow.AddDays(14);
        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();

        var leads = (await leadRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var opportunities = (await opportunityRepository.FindAsync(x => x.TenantId == tenantId && x.LeadId.HasValue)).ToList();

        var filtered = leads.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
                ContainsText(x.FirstName, search)
                || ContainsText(x.LastName, search)
                || ContainsText(GetLeadFullName(x), search)
                || ContainsText(x.CompanyName, search)
                || ContainsText(x.Email, search)
                || ContainsText(x.Phone, search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(x => string.Equals(x.LeadStatus, status, StringComparison.OrdinalIgnoreCase));
        }

        if (followUpOnly)
        {
            filtered = filtered.Where(x => NeedsLeadFollowUp(x, followUpWindow));
        }

        var ordered = filtered
            .OrderBy(x => x.NextFollowUpDate ?? DateTime.MaxValue)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();
        var totalCount = ordered.Count;
        var pagedItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => MapLeadListItem(x, opportunities))
            .ToList();

        return new PagedResult<CrmLeadListItemDto>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CrmLeadDetailDto?> GetLeadByIdAsync(Guid leadId)
    {
        var tenantId = _currentUserProvider.TenantId;
        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();

        var lead = await leadRepository.GetByIdAsync(leadId);
        if (lead == null || lead.TenantId != tenantId)
        {
            return null;
        }

        var opportunities = (await opportunityRepository.FindAsync(x => x.TenantId == tenantId && x.LeadId.HasValue && x.LeadId.Value == leadId)).ToList();
        var businessPartnerIds = opportunities
            .Where(x => x.CustomerId.HasValue)
            .Select(x => x.CustomerId!.Value)
            .ToHashSet();
        var businessPartnerLookup = businessPartnerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await businessPartnerRepository.FindAsync(x => x.TenantId == tenantId && businessPartnerIds.Contains(x.Id)))
                .ToDictionary(x => x.Id, x => x.PartnerName);

        return new CrmLeadDetailDto
        {
            LeadId = lead.Id,
            FirstName = lead.FirstName,
            LastName = lead.LastName,
            FullName = GetLeadFullName(lead),
            CompanyName = lead.CompanyName,
            JobTitle = lead.JobTitle,
            Email = lead.Email,
            Phone = lead.Phone,
            Mobile = lead.Mobile,
            AddressLine1 = lead.AddressLine1,
            AddressLine2 = lead.AddressLine2,
            City = lead.City,
            State = lead.State,
            PostalCode = lead.PostalCode,
            Country = lead.Country,
            LeadSource = lead.LeadSource,
            LeadStatus = lead.LeadStatus,
            QualificationScore = lead.QualificationScore,
            EstimatedValue = lead.EstimatedValue,
            LastContactDate = lead.LastContactDate,
            NextFollowUpDate = lead.NextFollowUpDate,
            AssignedToId = lead.AssignedToId,
            OpportunityCount = opportunities.Count,
            NeedsFollowUp = NeedsLeadFollowUp(lead, DateTime.UtcNow.AddDays(14)),
            CreatedAt = lead.CreatedAt,
            Notes = lead.Notes,
            ConvertedBusinessPartnerId = lead.ConvertedCustomerId,
            ConvertedDate = lead.ConvertedDate,
            Opportunities = opportunities
                .OrderByDescending(x => x.Amount * x.Probability / 100m)
                .ThenBy(x => x.ExpectedCloseDate)
                .Select(x => MapOpportunityOverview(x, businessPartnerLookup, new Dictionary<Guid, Lead> { [lead.Id] = lead }))
                .ToList()
        };
    }

    public async Task<CrmLeadDetailDto> CreateLeadAsync(CreateCrmLeadDto dto)
    {
        var leadRepository = _unitOfWork.Repository<Lead>();

        var lead = new Lead
        {
            TenantId = _currentUserProvider.TenantId,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            CompanyName = CleanNullable(dto.CompanyName),
            JobTitle = CleanNullable(dto.JobTitle),
            Email = CleanNullable(dto.Email),
            Phone = CleanNullable(dto.Phone),
            Mobile = CleanNullable(dto.Mobile),
            AddressLine1 = CleanNullable(dto.AddressLine1),
            AddressLine2 = CleanNullable(dto.AddressLine2),
            City = CleanNullable(dto.City),
            State = CleanNullable(dto.State),
            PostalCode = CleanNullable(dto.PostalCode),
            Country = CleanNullable(dto.Country),
            LeadSource = CleanRequiredText(dto.LeadSource, "Unknown"),
            LeadStatus = CleanRequiredText(dto.LeadStatus, "New"),
            QualificationScore = Math.Clamp(dto.QualificationScore, 0, 100),
            EstimatedValue = Math.Max(dto.EstimatedValue, 0m),
            LastContactDate = dto.LastContactDate,
            NextFollowUpDate = dto.NextFollowUpDate,
            AssignedToId = dto.AssignedToId,
            Notes = CleanNullable(dto.Notes),
            CreatedById = _currentUserProvider.UserId,
            CreatedBy = _currentUserProvider.Username
        };

        await leadRepository.AddAsync(lead);
        await _unitOfWork.SaveChangesAsync();

        return (await GetLeadByIdAsync(lead.Id))!;
    }

    public async Task<CrmLeadDetailDto> UpdateLeadAsync(Guid leadId, UpdateCrmLeadDto dto)
    {
        var tenantId = _currentUserProvider.TenantId;
        var leadRepository = _unitOfWork.Repository<Lead>();

        var lead = await leadRepository.GetByIdAsync(leadId);
        if (lead == null || lead.TenantId != tenantId)
        {
            throw new InvalidOperationException($"Lead {leadId} was not found.");
        }

        lead.FirstName = dto.FirstName.Trim();
        lead.LastName = dto.LastName.Trim();
        lead.CompanyName = CleanNullable(dto.CompanyName);
        lead.JobTitle = CleanNullable(dto.JobTitle);
        lead.Email = CleanNullable(dto.Email);
        lead.Phone = CleanNullable(dto.Phone);
        lead.Mobile = CleanNullable(dto.Mobile);
        lead.AddressLine1 = CleanNullable(dto.AddressLine1);
        lead.AddressLine2 = CleanNullable(dto.AddressLine2);
        lead.City = CleanNullable(dto.City);
        lead.State = CleanNullable(dto.State);
        lead.PostalCode = CleanNullable(dto.PostalCode);
        lead.Country = CleanNullable(dto.Country);
        lead.LeadSource = CleanRequiredText(dto.LeadSource, "Unknown");
        lead.LeadStatus = CleanRequiredText(dto.LeadStatus, lead.LeadStatus);
        lead.QualificationScore = Math.Clamp(dto.QualificationScore, 0, 100);
        lead.EstimatedValue = Math.Max(dto.EstimatedValue, 0m);
        lead.LastContactDate = dto.LastContactDate;
        lead.NextFollowUpDate = dto.NextFollowUpDate;
        lead.AssignedToId = dto.AssignedToId;
        lead.Notes = CleanNullable(dto.Notes);
        lead.LastModifiedById = _currentUserProvider.UserId;
        lead.UpdatedBy = _currentUserProvider.Username;

        if (string.Equals(lead.LeadStatus, "Converted", StringComparison.OrdinalIgnoreCase)
            && !lead.ConvertedDate.HasValue)
        {
            lead.ConvertedDate = DateTime.UtcNow;
        }

        await leadRepository.UpdateAsync(lead);
        await _unitOfWork.SaveChangesAsync();

        return (await GetLeadByIdAsync(lead.Id))!;
    }

    public async Task DeleteLeadAsync(Guid leadId)
    {
        var tenantId = _currentUserProvider.TenantId;
        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();

        var lead = await leadRepository.GetByIdAsync(leadId);
        if (lead == null || lead.TenantId != tenantId)
        {
            throw new InvalidOperationException($"Lead {leadId} was not found.");
        }

        var hasOpportunities = await opportunityRepository.ExistsAsync(x =>
            x.TenantId == tenantId
            && x.LeadId.HasValue
            && x.LeadId.Value == leadId);
        if (hasOpportunities)
        {
            throw new InvalidOperationException("Lead cannot be deleted while opportunities are linked to it.");
        }

        await leadRepository.DeleteAsync(lead.Id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<PagedResult<CrmOpportunityListItemDto>> GetOpportunitiesAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? stage = null,
        Guid? businessPartnerId = null,
        Guid? leadId = null,
        string? opportunityType = null)
    {
        page = Math.Max(page, 1);
        pageSize = ClampPageSize(pageSize);

        var tenantId = _currentUserProvider.TenantId;
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var leadRepository = _unitOfWork.Repository<Lead>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();

        var opportunities = (await opportunityRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var filtered = opportunities.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
                ContainsText(x.Name, search)
                || ContainsText(x.Description, search)
                || ContainsText(x.OpportunityType, search)
                || ContainsText(x.LeadSource, search));
        }

        if (!string.IsNullOrWhiteSpace(stage))
        {
            filtered = filtered.Where(x => string.Equals(x.Stage, stage, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(opportunityType))
        {
            filtered = filtered.Where(x => string.Equals(x.OpportunityType, opportunityType, StringComparison.OrdinalIgnoreCase));
        }

        if (businessPartnerId.HasValue)
        {
            filtered = filtered.Where(x => x.CustomerId.HasValue && x.CustomerId.Value == businessPartnerId.Value);
        }

        if (leadId.HasValue)
        {
            filtered = filtered.Where(x => x.LeadId.HasValue && x.LeadId.Value == leadId.Value);
        }

        var ordered = filtered
            .OrderByDescending(x => x.Amount * x.Probability / 100m)
            .ThenBy(x => x.ExpectedCloseDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();
        var totalCount = ordered.Count;
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var businessPartnerIds = pageItems
            .Where(x => x.CustomerId.HasValue)
            .Select(x => x.CustomerId!.Value)
            .ToHashSet();
        var leadIds = pageItems
            .Where(x => x.LeadId.HasValue)
            .Select(x => x.LeadId!.Value)
            .ToHashSet();

        var businessPartnerLookup = businessPartnerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await businessPartnerRepository.FindAsync(x => x.TenantId == tenantId && businessPartnerIds.Contains(x.Id)))
                .ToDictionary(x => x.Id, x => x.PartnerName);
        var leadLookup = leadIds.Count == 0
            ? new Dictionary<Guid, Lead>()
            : (await leadRepository.FindAsync(x => x.TenantId == tenantId && leadIds.Contains(x.Id)))
                .ToDictionary(x => x.Id);

        return new PagedResult<CrmOpportunityListItemDto>
        {
            Items = pageItems.Select(x => MapOpportunityListItem(x, businessPartnerLookup, leadLookup)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CrmOpportunityDetailDto?> GetOpportunityByIdAsync(Guid opportunityId)
    {
        var tenantId = _currentUserProvider.TenantId;
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var leadRepository = _unitOfWork.Repository<Lead>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var salesOrderRepository = _unitOfWork.Repository<SalesOrder>();

        var opportunity = await opportunityRepository.GetByIdAsync(opportunityId);
        if (opportunity == null || opportunity.TenantId != tenantId)
        {
            return null;
        }

        var leadLookup = new Dictionary<Guid, Lead>();
        if (opportunity.LeadId.HasValue)
        {
            var lead = await leadRepository.GetByIdAsync(opportunity.LeadId.Value);
            if (lead != null && lead.TenantId == tenantId)
            {
                leadLookup[lead.Id] = lead;
            }
        }

        var businessPartnerLookup = new Dictionary<Guid, string>();
        var relatedBusinessPartnerId = opportunity.CustomerId
            ?? leadLookup.Values.Select(x => x.ConvertedCustomerId).FirstOrDefault(x => x.HasValue);
        if (relatedBusinessPartnerId.HasValue)
        {
            var businessPartner = await businessPartnerRepository.GetByIdAsync(relatedBusinessPartnerId.Value);
            if (businessPartner != null && businessPartner.TenantId == tenantId)
            {
                businessPartnerLookup[businessPartner.Id] = businessPartner.PartnerName;
            }
        }

        var listItem = MapOpportunityListItem(opportunity, businessPartnerLookup, leadLookup);
        var quotes = (await quoteRepository.FindAsync(x => x.TenantId == tenantId && x.OpportunityId == opportunityId)).ToList();
        var quoteIds = quotes.Select(x => x.Id).ToHashSet();
        var relatedSalesOrders = (await salesOrderRepository.FindAsync(x =>
                x.TenantId == tenantId
                && ((x.OpportunityId.HasValue && x.OpportunityId.Value == opportunityId)
                    || (x.QuoteId.HasValue && quoteIds.Contains(x.QuoteId.Value)))))
            .ToList();
        var relatedContracts = relatedBusinessPartnerId.HasValue
            ? (await contractRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.BusinessPartnerId == relatedBusinessPartnerId.Value))
                .ToList()
            : new List<Contract>();
        var relatedContractIds = relatedContracts.Select(x => x.Id).ToHashSet();
        var relatedProjects = (relatedBusinessPartnerId.HasValue || relatedContractIds.Count > 0)
            ? (await projectRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && ((relatedBusinessPartnerId.HasValue
                            && x.BusinessPartnerId.HasValue
                            && x.BusinessPartnerId.Value == relatedBusinessPartnerId.Value)
                        || (x.ContractId.HasValue && relatedContractIds.Contains(x.ContractId.Value)))))
                .ToList()
            : new List<Project>();
        var opportunityLookup = new Dictionary<Guid, Opportunity> { [opportunity.Id] = opportunity };

        return new CrmOpportunityDetailDto
        {
            OpportunityId = listItem.OpportunityId,
            Name = listItem.Name,
            Stage = listItem.Stage,
            Amount = listItem.Amount,
            Currency = listItem.Currency,
            Probability = listItem.Probability,
            WeightedValue = listItem.WeightedValue,
            ExpectedCloseDate = listItem.ExpectedCloseDate,
            OpportunityType = listItem.OpportunityType,
            LeadSource = listItem.LeadSource,
            CustomerId = listItem.CustomerId,
            BusinessPartnerId = listItem.BusinessPartnerId,
            BusinessPartnerName = listItem.BusinessPartnerName,
            LeadId = listItem.LeadId,
            LeadName = listItem.LeadName,
            ActualCloseDate = listItem.ActualCloseDate,
            IsClosingSoon = listItem.IsClosingSoon,
            CreatedAt = listItem.CreatedAt,
            Description = opportunity.Description,
            AssignedToId = opportunity.AssignedToId,
            Competitors = opportunity.Competitors,
            Notes = opportunity.Notes,
            LossReason = opportunity.LossReason,
            Quotes = quotes
                .OrderByDescending(x => x.ValidUntil)
                .ThenByDescending(x => x.CreatedAt)
                .Select(x => MapQuoteListItem(x, opportunityLookup, businessPartnerLookup, leadLookup))
                .ToList(),
            RelatedContracts = relatedContracts
                .OrderByDescending(x => x.EndDate)
                .ThenByDescending(x => x.CreatedAt)
                .Select(x => MapContractSummary(x, "Account"))
                .ToList(),
            RelatedProjects = relatedProjects
                .OrderByDescending(x => x.TargetEndDate)
                .ThenByDescending(x => x.CreatedAt)
                .Select(x => MapProjectSummary(
                    x,
                    x.ContractId.HasValue && relatedContractIds.Contains(x.ContractId.Value)
                        ? "Contract"
                        : "Account"))
                .ToList(),
            ConversionChain = BuildConversionChain(
                opportunity,
                quotes,
                relatedContracts,
                relatedProjects,
                relatedSalesOrders,
                businessPartnerLookup,
                leadLookup)
        };
    }

    public async Task<CrmOpportunityDetailDto> CreateOpportunityAsync(CreateCrmOpportunityDto dto)
    {
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;

        var linkedLead = await ResolveLeadAsync(dto.LeadId, tenantId);
        var resolvedBusinessPartnerId = await ResolveBusinessPartnerIdAsync(dto.BusinessPartnerId, linkedLead, tenantId);

        var opportunity = new Opportunity
        {
            TenantId = tenantId,
            Name = dto.Name.Trim(),
            Description = CleanNullable(dto.Description),
            // CRM accounts currently use the existing customer foreign key slot.
            CustomerId = resolvedBusinessPartnerId,
            LeadId = linkedLead?.Id,
            Stage = CleanRequiredText(dto.Stage, "Prospecting"),
            Probability = Math.Clamp(dto.Probability, 0, 100),
            Amount = Math.Max(dto.Amount, 0m),
            Currency = NormalizeCurrencyCode(dto.Currency, "USD"),
            ExpectedCloseDate = dto.ExpectedCloseDate,
            ActualCloseDate = ResolveActualCloseDate(dto.Stage, dto.ActualCloseDate, now),
            LeadSource = ResolveLeadSource(dto.LeadSource, linkedLead),
            OpportunityType = CleanRequiredText(dto.OpportunityType, "New Business"),
            AssignedToId = dto.AssignedToId,
            Competitors = CleanNullable(dto.Competitors),
            Notes = CleanNullable(dto.Notes),
            LossReason = CleanNullable(dto.LossReason),
            CreatedById = _currentUserProvider.UserId,
            CreatedBy = _currentUserProvider.Username
        };

        await opportunityRepository.AddAsync(opportunity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetOpportunityByIdAsync(opportunity.Id))!;
    }

    public async Task<CrmOpportunityDetailDto> UpdateOpportunityAsync(Guid opportunityId, UpdateCrmOpportunityDto dto)
    {
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;

        var opportunity = await opportunityRepository.GetByIdAsync(opportunityId);
        if (opportunity == null || opportunity.TenantId != tenantId)
        {
            throw new InvalidOperationException($"Opportunity {opportunityId} was not found.");
        }

        var linkedLead = await ResolveLeadAsync(dto.LeadId, tenantId);
        var resolvedBusinessPartnerId = await ResolveBusinessPartnerIdAsync(dto.BusinessPartnerId, linkedLead, tenantId);

        opportunity.Name = dto.Name.Trim();
        opportunity.Description = CleanNullable(dto.Description);
        opportunity.CustomerId = resolvedBusinessPartnerId;
        opportunity.LeadId = linkedLead?.Id;
        opportunity.Stage = CleanRequiredText(dto.Stage, opportunity.Stage);
        opportunity.Probability = Math.Clamp(dto.Probability, 0, 100);
        opportunity.Amount = Math.Max(dto.Amount, 0m);
        opportunity.Currency = NormalizeCurrencyCode(dto.Currency, opportunity.Currency);
        opportunity.ExpectedCloseDate = dto.ExpectedCloseDate;
        opportunity.ActualCloseDate = ResolveActualCloseDate(dto.Stage, dto.ActualCloseDate, now);
        opportunity.LeadSource = ResolveLeadSource(dto.LeadSource, linkedLead);
        opportunity.OpportunityType = CleanRequiredText(dto.OpportunityType, opportunity.OpportunityType);
        opportunity.AssignedToId = dto.AssignedToId;
        opportunity.Competitors = CleanNullable(dto.Competitors);
        opportunity.Notes = CleanNullable(dto.Notes);
        opportunity.LossReason = CleanNullable(dto.LossReason);
        opportunity.LastModifiedById = _currentUserProvider.UserId;
        opportunity.UpdatedBy = _currentUserProvider.Username;

        await opportunityRepository.UpdateAsync(opportunity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetOpportunityByIdAsync(opportunity.Id))!;
    }

    public async Task DeleteOpportunityAsync(Guid opportunityId)
    {
        var tenantId = _currentUserProvider.TenantId;
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var quoteRepository = _unitOfWork.Repository<Quote>();

        var opportunity = await opportunityRepository.GetByIdAsync(opportunityId);
        if (opportunity == null || opportunity.TenantId != tenantId)
        {
            throw new InvalidOperationException($"Opportunity {opportunityId} was not found.");
        }

        var hasQuotes = await quoteRepository.ExistsAsync(x =>
            x.TenantId == tenantId
            && x.OpportunityId == opportunityId);
        if (hasQuotes)
        {
            throw new InvalidOperationException("Opportunity cannot be deleted while quotes are linked to it.");
        }

        await opportunityRepository.DeleteAsync(opportunityId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<PagedResult<CrmActivityListItemDto>> GetActivitiesAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? status = null,
        string? activityType = null,
        bool followUpOnly = false,
        Guid? businessPartnerId = null,
        Guid? opportunityId = null,
        Guid? leadId = null)
    {
        page = Math.Max(page, 1);
        pageSize = ClampPageSize(pageSize);

        var tenantId = _currentUserProvider.TenantId;
        var followUpWindow = DateTime.UtcNow.AddDays(14);
        var activityRepository = _unitOfWork.Repository<Activity>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var leadRepository = _unitOfWork.Repository<Lead>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();

        var activities = (await activityRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var opportunities = (await opportunityRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var leads = (await leadRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var businessPartners = (await businessPartnerRepository.FindAsync(x => x.TenantId == tenantId)).ToList();

        var opportunityLookup = opportunities.ToDictionary(x => x.Id);
        var leadLookup = leads.ToDictionary(x => x.Id);
        var businessPartnerLookup = businessPartners.ToDictionary(x => x.Id, x => x.PartnerName);

        var filtered = activities.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
            {
                opportunityLookup.TryGetValue(x.OpportunityId ?? Guid.Empty, out var linkedOpportunity);
                leadLookup.TryGetValue((x.LeadId ?? linkedOpportunity?.LeadId) ?? Guid.Empty, out var linkedLead);
                var linkedBusinessPartnerId = ResolveActivityBusinessPartnerId(x, opportunityLookup, leadLookup);
                var linkedBusinessPartnerName = linkedBusinessPartnerId.HasValue
                    ? businessPartnerLookup.GetValueOrDefault(linkedBusinessPartnerId.Value)
                    : null;

                return ContainsText(x.Subject, search)
                    || ContainsText(x.Description, search)
                    || ContainsText(x.ActivityType, search)
                    || ContainsText(x.ActivityStatus, search)
                    || ContainsText(x.Location, search)
                    || ContainsText(x.Outcome, search)
                    || ContainsText(x.Notes, search)
                    || ContainsText(linkedOpportunity?.Name, search)
                    || ContainsText(linkedBusinessPartnerName, search)
                    || ContainsText(linkedLead == null ? null : GetLeadFullName(linkedLead), search);
            });
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(x => string.Equals(x.ActivityStatus, status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(activityType))
        {
            filtered = filtered.Where(x => string.Equals(x.ActivityType, activityType, StringComparison.OrdinalIgnoreCase));
        }

        if (followUpOnly)
        {
            filtered = filtered.Where(x => NeedsActivityFollowUp(x, followUpWindow));
        }

        if (businessPartnerId.HasValue)
        {
            filtered = filtered.Where(x => ResolveActivityBusinessPartnerId(x, opportunityLookup, leadLookup) == businessPartnerId.Value);
        }

        if (opportunityId.HasValue)
        {
            filtered = filtered.Where(x => x.OpportunityId == opportunityId.Value);
        }

        if (leadId.HasValue)
        {
            filtered = filtered.Where(x =>
                x.LeadId == leadId.Value
                || (x.OpportunityId.HasValue
                    && opportunityLookup.TryGetValue(x.OpportunityId.Value, out var linkedOpportunity)
                    && linkedOpportunity.LeadId == leadId.Value));
        }

        var ordered = filtered
            .OrderBy(x => x.DueDate ?? x.NextFollowUpDate ?? DateTime.MaxValue)
            .ThenByDescending(x => x.ActivityDate)
            .ToList();
        var totalCount = ordered.Count;
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<CrmActivityListItemDto>
        {
            Items = pageItems
                .Select(x => MapActivityListItem(x, opportunityLookup, businessPartnerLookup, leadLookup))
                .ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CrmActivityDetailDto?> GetActivityByIdAsync(Guid activityId)
    {
        var tenantId = _currentUserProvider.TenantId;
        var activityRepository = _unitOfWork.Repository<Activity>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var leadRepository = _unitOfWork.Repository<Lead>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();

        var activity = await activityRepository.GetByIdAsync(activityId);
        if (activity == null || activity.TenantId != tenantId)
        {
            return null;
        }

        var opportunityLookup = new Dictionary<Guid, Opportunity>();
        if (activity.OpportunityId.HasValue)
        {
            var opportunity = await opportunityRepository.GetByIdAsync(activity.OpportunityId.Value);
            if (opportunity != null && opportunity.TenantId == tenantId)
            {
                opportunityLookup[opportunity.Id] = opportunity;
            }
        }

        var resolvedLeadId = activity.LeadId
            ?? opportunityLookup.Values.Select(x => x.LeadId).FirstOrDefault(x => x.HasValue);
        var leadLookup = new Dictionary<Guid, Lead>();
        if (resolvedLeadId.HasValue)
        {
            var lead = await leadRepository.GetByIdAsync(resolvedLeadId.Value);
            if (lead != null && lead.TenantId == tenantId)
            {
                leadLookup[lead.Id] = lead;
            }
        }

        var businessPartnerLookup = new Dictionary<Guid, string>();
        var resolvedBusinessPartnerId = ResolveActivityBusinessPartnerId(activity, opportunityLookup, leadLookup);
        if (resolvedBusinessPartnerId.HasValue)
        {
            var businessPartner = await businessPartnerRepository.GetByIdAsync(resolvedBusinessPartnerId.Value);
            if (businessPartner != null && businessPartner.TenantId == tenantId)
            {
                businessPartnerLookup[businessPartner.Id] = businessPartner.PartnerName;
            }
        }

        return MapActivityDetail(activity, opportunityLookup, businessPartnerLookup, leadLookup);
    }

    public async Task<CrmActivityDetailDto> CreateActivityAsync(CreateCrmActivityDto dto)
    {
        var activityRepository = _unitOfWork.Repository<Activity>();
        var tenantId = _currentUserProvider.TenantId;

        var linkedOpportunity = await ResolveOpportunityAsync(dto.OpportunityId, tenantId);
        var linkedLead = await ResolveActivityLeadAsync(dto.LeadId, linkedOpportunity, tenantId);
        var resolvedBusinessPartnerId = await ResolveActivityBusinessPartnerIdAsync(
            dto.BusinessPartnerId,
            linkedLead,
            linkedOpportunity,
            tenantId);

        ValidateActivityAssociations(resolvedBusinessPartnerId, linkedLead, linkedOpportunity);

        var activity = new Activity
        {
            TenantId = tenantId,
            Subject = dto.Subject.Trim(),
            ActivityType = CleanRequiredText(dto.ActivityType, "Call"),
            Description = CleanNullable(dto.Description),
            ActivityDate = dto.ActivityDate == default ? DateTime.UtcNow : dto.ActivityDate,
            DueDate = dto.DueDate,
            ActivityStatus = CleanRequiredText(dto.ActivityStatus, "Planned"),
            Priority = Math.Clamp(dto.Priority, 1, 4),
            Duration = dto.Duration,
            AssignedToId = dto.AssignedToId,
            LeadId = linkedLead?.Id,
            CustomerId = resolvedBusinessPartnerId,
            OpportunityId = linkedOpportunity?.Id,
            Location = CleanNullable(dto.Location),
            Attendees = CleanNullable(dto.Attendees),
            Outcome = CleanNullable(dto.Outcome),
            Notes = CleanNullable(dto.Notes),
            RequiresFollowUp = dto.RequiresFollowUp,
            NextFollowUpDate = dto.NextFollowUpDate,
            CreatedById = _currentUserProvider.UserId,
            CreatedBy = _currentUserProvider.Username
        };

        await activityRepository.AddAsync(activity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetActivityByIdAsync(activity.Id))!;
    }

    public async Task<CrmActivityDetailDto> UpdateActivityAsync(Guid activityId, UpdateCrmActivityDto dto)
    {
        var activityRepository = _unitOfWork.Repository<Activity>();
        var tenantId = _currentUserProvider.TenantId;

        var activity = await activityRepository.GetByIdAsync(activityId);
        if (activity == null || activity.TenantId != tenantId)
        {
            throw new InvalidOperationException($"Activity {activityId} was not found.");
        }

        var linkedOpportunity = await ResolveOpportunityAsync(dto.OpportunityId, tenantId);
        var linkedLead = await ResolveActivityLeadAsync(dto.LeadId, linkedOpportunity, tenantId);
        var resolvedBusinessPartnerId = await ResolveActivityBusinessPartnerIdAsync(
            dto.BusinessPartnerId,
            linkedLead,
            linkedOpportunity,
            tenantId);

        ValidateActivityAssociations(resolvedBusinessPartnerId, linkedLead, linkedOpportunity);

        activity.Subject = dto.Subject.Trim();
        activity.ActivityType = CleanRequiredText(dto.ActivityType, activity.ActivityType);
        activity.Description = CleanNullable(dto.Description);
        activity.ActivityDate = dto.ActivityDate == default ? activity.ActivityDate : dto.ActivityDate;
        activity.DueDate = dto.DueDate;
        activity.ActivityStatus = CleanRequiredText(dto.ActivityStatus, activity.ActivityStatus);
        activity.Priority = Math.Clamp(dto.Priority, 1, 4);
        activity.Duration = dto.Duration;
        activity.AssignedToId = dto.AssignedToId;
        activity.LeadId = linkedLead?.Id;
        activity.CustomerId = resolvedBusinessPartnerId;
        activity.OpportunityId = linkedOpportunity?.Id;
        activity.Location = CleanNullable(dto.Location);
        activity.Attendees = CleanNullable(dto.Attendees);
        activity.Outcome = CleanNullable(dto.Outcome);
        activity.Notes = CleanNullable(dto.Notes);
        activity.RequiresFollowUp = dto.RequiresFollowUp;
        activity.NextFollowUpDate = dto.NextFollowUpDate;
        activity.LastModifiedById = _currentUserProvider.UserId;
        activity.UpdatedBy = _currentUserProvider.Username;

        await activityRepository.UpdateAsync(activity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetActivityByIdAsync(activity.Id))!;
    }

    public async Task DeleteActivityAsync(Guid activityId)
    {
        var tenantId = _currentUserProvider.TenantId;
        var activityRepository = _unitOfWork.Repository<Activity>();

        var activity = await activityRepository.GetByIdAsync(activityId);
        if (activity == null || activity.TenantId != tenantId)
        {
            throw new InvalidOperationException($"Activity {activityId} was not found.");
        }

        await activityRepository.DeleteAsync(activityId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<PagedResult<CrmQuoteListItemDto>> GetQuotesAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? status = null,
        Guid? businessPartnerId = null,
        Guid? opportunityId = null,
        Guid? leadId = null)
    {
        page = Math.Max(page, 1);
        pageSize = ClampPageSize(pageSize);

        var tenantId = _currentUserProvider.TenantId;
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var leadRepository = _unitOfWork.Repository<Lead>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();

        var quotes = (await quoteRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var opportunities = (await opportunityRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var leads = (await leadRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var businessPartners = (await businessPartnerRepository.FindAsync(x => x.TenantId == tenantId)).ToList();

        var opportunityLookup = opportunities.ToDictionary(x => x.Id);
        var leadLookup = leads.ToDictionary(x => x.Id);
        var businessPartnerLookup = businessPartners.ToDictionary(x => x.Id, x => x.PartnerName);

        var filtered = quotes.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
            {
                opportunityLookup.TryGetValue(x.OpportunityId, out var linkedOpportunity);
                leadLookup.TryGetValue(linkedOpportunity?.LeadId ?? Guid.Empty, out var linkedLead);
                var linkedBusinessPartnerId = ResolveQuoteBusinessPartnerId(x, opportunityLookup);
                var linkedBusinessPartnerName = linkedBusinessPartnerId.HasValue
                    ? businessPartnerLookup.GetValueOrDefault(linkedBusinessPartnerId.Value)
                    : null;

                return ContainsText(x.QuoteName, search)
                    || ContainsText(x.DocumentNumber, search)
                    || ContainsText(x.Proposal, search)
                    || ContainsText(linkedOpportunity?.Name, search)
                    || ContainsText(linkedOpportunity?.OpportunityType, search)
                    || ContainsText(linkedBusinessPartnerName, search)
                    || ContainsText(linkedLead == null ? null : GetLeadFullName(linkedLead), search);
            });
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(x => string.Equals(x.QuoteStatus, status, StringComparison.OrdinalIgnoreCase));
        }

        if (businessPartnerId.HasValue)
        {
            filtered = filtered.Where(x => ResolveQuoteBusinessPartnerId(x, opportunityLookup) == businessPartnerId.Value);
        }

        if (opportunityId.HasValue)
        {
            filtered = filtered.Where(x => x.OpportunityId == opportunityId.Value);
        }

        if (leadId.HasValue)
        {
            filtered = filtered.Where(x =>
                opportunityLookup.TryGetValue(x.OpportunityId, out var linkedOpportunity)
                && linkedOpportunity.LeadId.HasValue
                && linkedOpportunity.LeadId.Value == leadId.Value);
        }

        var ordered = filtered
            .OrderBy(x => x.ValidUntil)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();
        var totalCount = ordered.Count;
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<CrmQuoteListItemDto>
        {
            Items = pageItems
                .Select(x => MapQuoteListItem(x, opportunityLookup, businessPartnerLookup, leadLookup))
                .ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CrmQuoteDetailDto?> GetQuoteByIdAsync(Guid quoteId)
    {
        var tenantId = _currentUserProvider.TenantId;
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var leadRepository = _unitOfWork.Repository<Lead>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var salesOrderRepository = _unitOfWork.Repository<SalesOrder>();

        var quote = await quoteRepository.GetByIdAsync(quoteId, x => x.LineItems);
        if (quote == null || quote.TenantId != tenantId)
        {
            return null;
        }

        var opportunity = await opportunityRepository.GetByIdAsync(quote.OpportunityId);
        if (opportunity == null || opportunity.TenantId != tenantId)
        {
            return null;
        }

        var leadLookup = new Dictionary<Guid, Lead>();
        if (opportunity.LeadId.HasValue)
        {
            var lead = await leadRepository.GetByIdAsync(opportunity.LeadId.Value);
            if (lead != null && lead.TenantId == tenantId)
            {
                leadLookup[lead.Id] = lead;
            }
        }

        var businessPartnerLookup = new Dictionary<Guid, string>();
        var relatedBusinessPartnerId = quote.CustomerId ?? opportunity.CustomerId ?? leadLookup.Values.Select(x => x.ConvertedCustomerId).FirstOrDefault(x => x.HasValue);
        if (relatedBusinessPartnerId.HasValue)
        {
            var businessPartner = await businessPartnerRepository.GetByIdAsync(relatedBusinessPartnerId.Value);
            if (businessPartner != null && businessPartner.TenantId == tenantId)
            {
                businessPartnerLookup[businessPartner.Id] = businessPartner.PartnerName;
            }
        }

        var relatedContracts = relatedBusinessPartnerId.HasValue
            ? (await contractRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.BusinessPartnerId == relatedBusinessPartnerId.Value))
                .ToList()
            : new List<Contract>();
        var relatedContractIds = relatedContracts.Select(x => x.Id).ToHashSet();
        var relatedProjects = (relatedBusinessPartnerId.HasValue || relatedContractIds.Count > 0)
            ? (await projectRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && ((relatedBusinessPartnerId.HasValue
                            && x.BusinessPartnerId.HasValue
                            && x.BusinessPartnerId.Value == relatedBusinessPartnerId.Value)
                        || (x.ContractId.HasValue && relatedContractIds.Contains(x.ContractId.Value)))))
                .ToList()
            : new List<Project>();
        var opportunityQuotes = (await quoteRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.OpportunityId == quote.OpportunityId))
            .ToList();
        var opportunityQuoteIds = opportunityQuotes.Select(x => x.Id).ToHashSet();
        var relatedSalesOrders = (await salesOrderRepository.FindAsync(x =>
                x.TenantId == tenantId
                && ((x.OpportunityId.HasValue && x.OpportunityId.Value == opportunity.Id)
                    || (x.QuoteId.HasValue && opportunityQuoteIds.Contains(x.QuoteId.Value)))))
            .ToList();
        var opportunityLookup = new Dictionary<Guid, Opportunity> { [opportunity.Id] = opportunity };
        var listItem = MapQuoteListItem(quote, opportunityLookup, businessPartnerLookup, leadLookup);

        return new CrmQuoteDetailDto
        {
            QuoteId = listItem.QuoteId,
            OpportunityId = listItem.OpportunityId,
            QuoteName = listItem.QuoteName,
            QuoteStatus = listItem.QuoteStatus,
            Value = listItem.Value,
            Currency = listItem.Currency,
            ValidUntil = listItem.ValidUntil,
            OpportunityName = listItem.OpportunityName,
            BusinessPartnerId = listItem.BusinessPartnerId,
            BusinessPartnerName = listItem.BusinessPartnerName,
            IsExpiringSoon = listItem.IsExpiringSoon,
            DocumentNumber = listItem.DocumentNumber,
            DocumentDate = listItem.DocumentDate,
            CreatedAt = listItem.CreatedAt,
            SentDate = listItem.SentDate,
            AcceptedDate = listItem.AcceptedDate,
            LeadId = listItem.LeadId,
            LeadName = listItem.LeadName,
            IsAccepted = listItem.IsAccepted,
            Proposal = quote.Proposal,
            ConvertedInvoiceId = quote.ConvertedInvoiceId,
            LineItems = quote.LineItems
                .OrderBy(x => x.Description)
                .Select(x => new CrmQuoteLineItemDto
                {
                    Description = x.Description,
                    Quantity = x.Quantity,
                    UnitPrice = x.UnitPrice,
                    LineTotal = x.LineTotal,
                    ProductCode = x.ProductCode,
                    Unit = x.Unit,
                    DiscountAmount = x.DiscountAmount,
                    TaxAmount = x.TaxAmount
                })
                .ToList(),
            ConversionChain = BuildConversionChain(
                opportunity,
                opportunityQuotes,
                relatedContracts,
                relatedProjects,
                relatedSalesOrders,
                businessPartnerLookup,
                leadLookup)
        };
    }

    public async Task<PagedResult<CrmProjectListItemDto>> GetProjectsAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? status = null,
        Guid? businessPartnerId = null,
        Guid? contractId = null)
    {
        page = Math.Max(page, 1);
        pageSize = ClampPageSize(pageSize);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var projectRepository = _unitOfWork.Repository<Project>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();

        var projects = (await projectRepository.FindAsync(x =>
                x.TenantId == tenantId
                && (x.BusinessPartnerId.HasValue || x.ContractId.HasValue)))
            .ToList();

        var contractIds = projects
            .Where(x => x.ContractId.HasValue)
            .Select(x => x.ContractId!.Value)
            .ToHashSet();
        var contractLookup = contractIds.Count == 0
            ? new Dictionary<Guid, Contract>()
            : (await contractRepository.FindAsync(x => x.TenantId == tenantId && contractIds.Contains(x.Id)))
                .ToDictionary(x => x.Id);

        var businessPartnerIds = projects
            .Select(x => ResolveProjectBusinessPartnerId(x, contractLookup))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToHashSet();
        var businessPartnerLookup = businessPartnerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await businessPartnerRepository.FindAsync(x => x.TenantId == tenantId && businessPartnerIds.Contains(x.Id)))
                .ToDictionary(x => x.Id, x => x.PartnerName);

        var filtered = projects.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
            {
                contractLookup.TryGetValue(x.ContractId ?? Guid.Empty, out var linkedContract);
                var resolvedBusinessPartnerId = ResolveProjectBusinessPartnerId(x, contractLookup);
                var linkedBusinessPartnerName = resolvedBusinessPartnerId.HasValue
                    ? businessPartnerLookup.GetValueOrDefault(resolvedBusinessPartnerId.Value)
                    : null;

                return ContainsText(x.ProjectCode, search)
                    || ContainsText(x.Title, search)
                    || ContainsText(x.Summary, search)
                    || ContainsText(x.StatusRemarks, search)
                    || ContainsText(linkedContract?.ContractNumber, search)
                    || ContainsText(linkedContract?.ContractTitle, search)
                    || ContainsText(linkedBusinessPartnerName, search);
            });
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        if (businessPartnerId.HasValue)
        {
            filtered = filtered.Where(x => ResolveProjectBusinessPartnerId(x, contractLookup) == businessPartnerId.Value);
        }

        if (contractId.HasValue)
        {
            filtered = filtered.Where(x => x.ContractId.HasValue && x.ContractId.Value == contractId.Value);
        }

        var ordered = filtered
            .OrderByDescending(IsActiveProject)
            .ThenByDescending(x => x.TargetEndDate.HasValue && x.TargetEndDate.Value < now)
            .ThenBy(x => x.TargetEndDate ?? DateTime.MaxValue)
            .ThenBy(x => x.Title)
            .ToList();
        var totalCount = ordered.Count;
        var pageItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => MapProjectListItem(x, contractLookup, businessPartnerLookup, now))
            .ToList();

        return new PagedResult<CrmProjectListItemDto>
        {
            Items = pageItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CrmProjectDetailDto?> GetProjectByIdAsync(Guid projectId)
    {
        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var projectRepository = _unitOfWork.Repository<Project>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();

        var project = await projectRepository.GetByIdAsync(projectId);
        if (project == null || project.TenantId != tenantId)
        {
            return null;
        }

        Contract? linkedContract = null;
        if (project.ContractId.HasValue)
        {
            linkedContract = await contractRepository.GetByIdAsync(project.ContractId.Value);
            if (linkedContract?.TenantId != tenantId)
            {
                linkedContract = null;
            }
        }

        var resolvedBusinessPartnerId = project.BusinessPartnerId ?? linkedContract?.BusinessPartnerId;
        string? businessPartnerName = null;
        if (resolvedBusinessPartnerId.HasValue)
        {
            var linkedBusinessPartner = await businessPartnerRepository.GetByIdAsync(resolvedBusinessPartnerId.Value);
            if (linkedBusinessPartner?.TenantId == tenantId)
            {
                businessPartnerName = linkedBusinessPartner.PartnerName;
            }
        }

        return MapProjectDetail(project, linkedContract, businessPartnerName, now);
    }

    public async Task<PagedResult<CrmContractListItemDto>> GetContractsAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? status = null,
        Guid? businessPartnerId = null,
        bool expiringOnly = false)
    {
        page = Math.Max(page, 1);
        pageSize = ClampPageSize(pageSize);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var contractWindow = now.AddDays(90);
        var contractRepository = _unitOfWork.Repository<Contract>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();

        var contracts = (await contractRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var contractIds = contracts.Select(x => x.Id).ToHashSet();
        var projectsByContractId = contractIds.Count == 0
            ? new Dictionary<Guid, List<Project>>()
            : (await projectRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.ContractId.HasValue
                    && contractIds.Contains(x.ContractId.Value)))
                .GroupBy(x => x.ContractId!.Value)
                .ToDictionary(x => x.Key, x => x.ToList());

        var businessPartnerIds = contracts.Select(x => x.BusinessPartnerId).ToHashSet();
        var businessPartnerLookup = businessPartnerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await businessPartnerRepository.FindAsync(x => x.TenantId == tenantId && businessPartnerIds.Contains(x.Id)))
                .ToDictionary(x => x.Id, x => x.PartnerName);

        var filtered = contracts.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
                ContainsText(x.ContractNumber, search)
                || ContainsText(x.ContractTitle, search)
                || ContainsText(x.ContractType, search)
                || ContainsText(x.PaymentTerms, search)
                || ContainsText(businessPartnerLookup.GetValueOrDefault(x.BusinessPartnerId), search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        if (businessPartnerId.HasValue)
        {
            filtered = filtered.Where(x => x.BusinessPartnerId == businessPartnerId.Value);
        }

        if (expiringOnly)
        {
            filtered = filtered.Where(x =>
                IsActiveContract(x)
                && x.EndDate.HasValue
                && x.EndDate.Value <= contractWindow);
        }

        var ordered = filtered
            .OrderByDescending(x => IsActiveContract(x) && x.EndDate.HasValue && x.EndDate.Value <= contractWindow)
            .ThenBy(x => x.EndDate ?? DateTime.MaxValue)
            .ThenByDescending(x => x.ContractValue)
            .ThenBy(x => x.ContractNumber)
            .ToList();
        var totalCount = ordered.Count;
        var pageItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => MapContractListItem(
                x,
                businessPartnerLookup.GetValueOrDefault(x.BusinessPartnerId),
                projectsByContractId.GetValueOrDefault(x.Id) ?? new List<Project>(),
                contractWindow))
            .ToList();

        return new PagedResult<CrmContractListItemDto>
        {
            Items = pageItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CrmContractDetailDto?> GetContractByIdAsync(Guid contractId)
    {
        var tenantId = _currentUserProvider.TenantId;
        var contractWindow = DateTime.UtcNow.AddDays(90);
        var contractRepository = _unitOfWork.Repository<Contract>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();

        var contract = await contractRepository.GetByIdAsync(contractId);
        if (contract == null || contract.TenantId != tenantId)
        {
            return null;
        }

        var linkedBusinessPartner = await businessPartnerRepository.GetByIdAsync(contract.BusinessPartnerId);
        var businessPartnerName = linkedBusinessPartner?.TenantId == tenantId
            ? linkedBusinessPartner.PartnerName
            : null;

        var relatedProjects = (await projectRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.ContractId.HasValue
                && x.ContractId.Value == contractId))
            .ToList();

        return MapContractDetail(contract, businessPartnerName, relatedProjects, contractWindow);
    }

    public async Task<PagedResult<CrmTenderListItemDto>> GetTendersAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? entityType = null,
        string? status = null,
        Guid? businessPartnerId = null,
        Guid? tenderId = null)
    {
        page = Math.Max(page, 1);
        pageSize = ClampPageSize(pageSize);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var tenderRepository = _unitOfWork.Repository<Tender>();
        var invitationRepository = _unitOfWork.Repository<TenderInvitation>();
        var bidRepository = _unitOfWork.Repository<TenderBid>();
        var awardRepository = _unitOfWork.Repository<TenderAward>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var contractRepository = _unitOfWork.Repository<Contract>();

        var tenders = (await tenderRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var invitations = (await invitationRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var bids = (await bidRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var awards = (await awardRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var businessPartnerIds = invitations.Select(x => x.BusinessPartnerId)
            .Concat(bids.Select(x => x.BusinessPartnerId))
            .Concat(awards.Select(x => x.BusinessPartnerId))
            .Distinct()
            .ToHashSet();
        var businessPartnerLookup = businessPartnerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await businessPartnerRepository.FindAsync(x => x.TenantId == tenantId && businessPartnerIds.Contains(x.Id)))
                .ToDictionary(x => x.Id, x => x.PartnerName);
        var contracts = (await contractRepository.FindAsync(x => x.TenantId == tenantId)).ToList();

        var tenderLookup = tenders.ToDictionary(x => x.Id);
        var awardLookupByBidId = awards
            .GroupBy(x => x.TenderBidId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.AwardDate).First());
        var awardLookupByPartnerTender = awards
            .GroupBy(x => (x.BusinessPartnerId, x.TenderId))
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.AwardDate).First());
        var contractLookupByAwardId = contracts
            .GroupBy(x => x.TenderAwardId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.CreatedAt).First());

        var items = invitations
            .Select(x =>
            {
                tenderLookup.TryGetValue(x.TenderId, out var relatedTender);
                awardLookupByPartnerTender.TryGetValue((x.BusinessPartnerId, x.TenderId), out var relatedAward);
                var relatedContract = relatedAward != null ? contractLookupByAwardId.GetValueOrDefault(relatedAward.Id) : null;
                return MapTenderInvitationListItem(
                    x,
                    relatedTender,
                    businessPartnerLookup.GetValueOrDefault(x.BusinessPartnerId),
                    relatedContract,
                    now);
            })
            .Concat(bids.Select(x =>
            {
                tenderLookup.TryGetValue(x.TenderId, out var relatedTender);
                awardLookupByBidId.TryGetValue(x.Id, out var relatedAward);
                var relatedContract = relatedAward != null ? contractLookupByAwardId.GetValueOrDefault(relatedAward.Id) : null;
                return MapTenderBidListItem(
                    x,
                    relatedTender,
                    businessPartnerLookup.GetValueOrDefault(x.BusinessPartnerId),
                    relatedAward,
                    relatedContract,
                    now);
            }))
            .Concat(awards.Select(x =>
            {
                tenderLookup.TryGetValue(x.TenderId, out var relatedTender);
                var relatedContract = contractLookupByAwardId.GetValueOrDefault(x.Id);
                return MapTenderAwardListItem(
                    x,
                    relatedTender,
                    businessPartnerLookup.GetValueOrDefault(x.BusinessPartnerId),
                    relatedContract,
                    now);
            }));

        var filtered = items.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
                ContainsText(x.TenderNumber, search)
                || ContainsText(x.TenderTitle, search)
                || ContainsText(x.TenderType, search)
                || ContainsText(x.TenderStatus, search)
                || ContainsText(x.BusinessPartnerName, search)
                || ContainsText(x.ReferenceNumber, search)
                || ContainsText(x.Status, search)
                || ContainsText(x.RelatedContractNumber, search));
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            filtered = filtered.Where(x => string.Equals(x.EntityType, entityType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        if (businessPartnerId.HasValue)
        {
            filtered = filtered.Where(x => x.BusinessPartnerId == businessPartnerId.Value);
        }

        if (tenderId.HasValue)
        {
            filtered = filtered.Where(x => x.TenderId == tenderId.Value);
        }

        var ordered = filtered
            .OrderByDescending(x => string.Equals(x.EntityType, "Award", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.CreatedAt)
            .ThenBy(x => x.TenderNumber)
            .ToList();
        var totalCount = ordered.Count;
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<CrmTenderListItemDto>
        {
            Items = pageItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CrmTenderDetailDto?> GetTenderByEntityAsync(string entityType, Guid entityId)
    {
        var normalizedEntityType = NormalizeTenderEntityType(entityType);
        if (normalizedEntityType == null)
        {
            return null;
        }

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var tenderRepository = _unitOfWork.Repository<Tender>();
        var invitationRepository = _unitOfWork.Repository<TenderInvitation>();
        var bidRepository = _unitOfWork.Repository<TenderBid>();
        var awardRepository = _unitOfWork.Repository<TenderAward>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var contractRepository = _unitOfWork.Repository<Contract>();

        switch (normalizedEntityType)
        {
            case "Invitation":
            {
                var invitation = await invitationRepository.GetByIdAsync(entityId);
                if (invitation == null || invitation.TenantId != tenantId)
                {
                    return null;
                }

                var relatedTender = await tenderRepository.GetByIdAsync(invitation.TenderId);
                var businessPartner = await businessPartnerRepository.GetByIdAsync(invitation.BusinessPartnerId);
                var relatedAward = (await awardRepository.FindAsync(x =>
                        x.TenantId == tenantId
                        && x.TenderId == invitation.TenderId
                        && x.BusinessPartnerId == invitation.BusinessPartnerId))
                    .OrderByDescending(x => x.AwardDate)
                    .FirstOrDefault();
                var relatedContract = relatedAward == null
                    ? null
                    : (await contractRepository.FindAsync(x => x.TenantId == tenantId && x.TenderAwardId == relatedAward.Id))
                        .OrderByDescending(x => x.CreatedAt)
                        .FirstOrDefault();

                return MapTenderInvitationDetail(
                    invitation,
                    relatedTender?.TenantId == tenantId ? relatedTender : null,
                    businessPartner?.TenantId == tenantId ? businessPartner.PartnerName : null,
                    relatedAward,
                    relatedContract,
                    now);
            }
            case "Bid":
            {
                var bid = await bidRepository.GetByIdAsync(entityId);
                if (bid == null || bid.TenantId != tenantId)
                {
                    return null;
                }

                var relatedTender = await tenderRepository.GetByIdAsync(bid.TenderId);
                var businessPartner = await businessPartnerRepository.GetByIdAsync(bid.BusinessPartnerId);
                var relatedAward = (await awardRepository.FindAsync(x => x.TenantId == tenantId && x.TenderBidId == bid.Id))
                    .OrderByDescending(x => x.AwardDate)
                    .FirstOrDefault();
                var relatedContract = relatedAward == null
                    ? null
                    : (await contractRepository.FindAsync(x => x.TenantId == tenantId && x.TenderAwardId == relatedAward.Id))
                        .OrderByDescending(x => x.CreatedAt)
                        .FirstOrDefault();

                return MapTenderBidDetail(
                    bid,
                    relatedTender?.TenantId == tenantId ? relatedTender : null,
                    businessPartner?.TenantId == tenantId ? businessPartner.PartnerName : null,
                    relatedAward,
                    relatedContract,
                    now);
            }
            case "Award":
            {
                var award = await awardRepository.GetByIdAsync(entityId);
                if (award == null || award.TenantId != tenantId)
                {
                    return null;
                }

                var relatedTender = await tenderRepository.GetByIdAsync(award.TenderId);
                var businessPartner = await businessPartnerRepository.GetByIdAsync(award.BusinessPartnerId);
                var relatedContract = (await contractRepository.FindAsync(x => x.TenantId == tenantId && x.TenderAwardId == award.Id))
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefault();

                return MapTenderAwardDetail(
                    award,
                    relatedTender?.TenantId == tenantId ? relatedTender : null,
                    businessPartner?.TenantId == tenantId ? businessPartner.PartnerName : null,
                    relatedContract,
                    now);
            }
            default:
                return null;
        }
    }

    public async Task<PagedResult<CrmCampaignListItemDto>> GetCampaignsAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? status = null,
        string? campaignType = null,
        bool activeOnly = false,
        Guid? businessPartnerId = null,
        Guid? leadId = null)
    {
        page = Math.Max(page, 1);
        pageSize = ClampPageSize(pageSize);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var endingWindow = now.AddDays(14);

        var campaignRepository = _unitOfWork.Repository<Campaign>();
        var campaignMemberRepository = _unitOfWork.Repository<CampaignMember>();
        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();

        var campaigns = (await campaignRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var campaignIds = campaigns.Select(x => x.Id).ToHashSet();
        var campaignMembers = campaignIds.Count == 0
            ? new List<CampaignMember>()
            : (await campaignMemberRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && campaignIds.Contains(x.CampaignId)))
                .ToList();
        var membersByCampaignId = campaignMembers
            .GroupBy(x => x.CampaignId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var leadIds = campaignMembers
            .Where(x => x.LeadId.HasValue)
            .Select(x => x.LeadId!.Value)
            .ToHashSet();
        var leads = leadIds.Count == 0
            ? new List<Lead>()
            : (await leadRepository.FindAsync(x => x.TenantId == tenantId && leadIds.Contains(x.Id)))
                .ToList();
        var leadLookup = leads.ToDictionary(x => x.Id);

        var opportunities = leadIds.Count == 0
            ? new List<Opportunity>()
            : (await opportunityRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.LeadId.HasValue
                    && leadIds.Contains(x.LeadId.Value)))
                .ToList();
        var opportunitiesByLeadId = opportunities
            .Where(x => x.LeadId.HasValue)
            .GroupBy(x => x.LeadId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());

        var businessPartnerIds = leads
            .Where(x => x.ConvertedCustomerId.HasValue)
            .Select(x => x.ConvertedCustomerId!.Value)
            .Concat(opportunities.Where(x => x.CustomerId.HasValue).Select(x => x.CustomerId!.Value))
            .Distinct()
            .ToHashSet();
        var businessPartnerLookup = businessPartnerIds.Count == 0
            ? new Dictionary<Guid, BusinessPartner>()
            : (await businessPartnerRepository.FindAsync(x => x.TenantId == tenantId && businessPartnerIds.Contains(x.Id)))
                .ToDictionary(x => x.Id);

        var filtered = campaigns
            .Select(campaign =>
            {
                var members = membersByCampaignId.GetValueOrDefault(campaign.Id) ?? new List<CampaignMember>();
                var memberLeadIds = members
                    .Where(x => x.LeadId.HasValue)
                    .Select(x => x.LeadId!.Value)
                    .ToHashSet();
                var memberOpportunities = memberLeadIds
                    .SelectMany(id => opportunitiesByLeadId.GetValueOrDefault(id) ?? new List<Opportunity>())
                    .GroupBy(x => x.Id)
                    .Select(x => x.First())
                    .ToList();
                var influencedAccountIds = memberOpportunities
                    .Where(x => x.CustomerId.HasValue)
                    .Select(x => x.CustomerId!.Value)
                    .Concat(memberLeadIds
                        .Select(id => leadLookup.GetValueOrDefault(id)?.ConvertedCustomerId)
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value))
                    .Distinct()
                    .ToHashSet();

                return new
                {
                    Campaign = campaign,
                    Item = BuildCampaignListItem(
                        campaign,
                        members,
                        leadLookup,
                        opportunitiesByLeadId,
                        influencedAccountIds,
                        now,
                        endingWindow),
                    InfluencedAccountIds = influencedAccountIds,
                    MemberLeadIds = memberLeadIds
                };
            })
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
                ContainsText(x.Campaign.Name, search)
                || ContainsText(x.Campaign.Description, search)
                || ContainsText(x.Campaign.Notes, search)
                || ContainsText(x.Campaign.CampaignType, search)
                || ContainsText(x.Campaign.CampaignStatus, search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(x => string.Equals(x.Campaign.CampaignStatus, status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(campaignType))
        {
            filtered = filtered.Where(x => string.Equals(x.Campaign.CampaignType, campaignType, StringComparison.OrdinalIgnoreCase));
        }

        if (activeOnly)
        {
            filtered = filtered.Where(x => x.Item.IsActive);
        }

        if (businessPartnerId.HasValue)
        {
            filtered = filtered.Where(x => x.InfluencedAccountIds.Contains(businessPartnerId.Value));
        }

        if (leadId.HasValue)
        {
            filtered = filtered.Where(x => x.MemberLeadIds.Contains(leadId.Value));
        }

        var ordered = filtered
            .OrderByDescending(x => x.Item.IsActive)
            .ThenBy(x => x.Item.EndDate ?? DateTime.MaxValue)
            .ThenByDescending(x => x.Item.WeightedPipelineValue)
            .ThenBy(x => x.Item.Name)
            .Select(x => x.Item)
            .ToList();
        var totalCount = ordered.Count;
        var pageItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<CrmCampaignListItemDto>
        {
            Items = pageItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CrmCampaignDetailDto?> GetCampaignByIdAsync(Guid campaignId)
    {
        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var endingWindow = now.AddDays(14);

        var campaignRepository = _unitOfWork.Repository<Campaign>();
        var campaignMemberRepository = _unitOfWork.Repository<CampaignMember>();
        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();

        var campaign = await campaignRepository.GetByIdAsync(campaignId);
        if (campaign == null || campaign.TenantId != tenantId)
        {
            return null;
        }

        var members = (await campaignMemberRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.CampaignId == campaignId))
            .ToList();
        var memberLeadIds = members
            .Where(x => x.LeadId.HasValue)
            .Select(x => x.LeadId!.Value)
            .ToHashSet();
        var leads = memberLeadIds.Count == 0
            ? new List<Lead>()
            : (await leadRepository.FindAsync(x => x.TenantId == tenantId && memberLeadIds.Contains(x.Id)))
                .ToList();
        var leadLookup = leads.ToDictionary(x => x.Id);

        var opportunities = memberLeadIds.Count == 0
            ? new List<Opportunity>()
            : (await opportunityRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.LeadId.HasValue
                    && memberLeadIds.Contains(x.LeadId.Value)))
                .ToList();
        var opportunitiesByLeadId = opportunities
            .Where(x => x.LeadId.HasValue)
            .GroupBy(x => x.LeadId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());

        var businessPartnerIds = leads
            .Where(x => x.ConvertedCustomerId.HasValue)
            .Select(x => x.ConvertedCustomerId!.Value)
            .Concat(opportunities.Where(x => x.CustomerId.HasValue).Select(x => x.CustomerId!.Value))
            .Distinct()
            .ToHashSet();
        var businessPartners = businessPartnerIds.Count == 0
            ? new List<BusinessPartner>()
            : (await businessPartnerRepository.FindAsync(x => x.TenantId == tenantId && businessPartnerIds.Contains(x.Id)))
                .ToList();
        var businessPartnerLookup = businessPartners.ToDictionary(x => x.Id);
        var businessPartnerNameLookup = businessPartners.ToDictionary(x => x.Id, x => x.PartnerName);

        var influencedAccountIds = opportunities
            .Where(x => x.CustomerId.HasValue)
            .Select(x => x.CustomerId!.Value)
            .Concat(leads.Where(x => x.ConvertedCustomerId.HasValue).Select(x => x.ConvertedCustomerId!.Value))
            .Distinct()
            .ToHashSet();
        var summary = BuildCampaignListItem(
            campaign,
            members,
            leadLookup,
            opportunitiesByLeadId,
            influencedAccountIds,
            now,
            endingWindow);
        var openOpportunities = opportunities
            .Where(x => !IsClosedOpportunityStage(x.Stage))
            .OrderByDescending(x => x.Amount * x.Probability / 100m)
            .ThenBy(x => x.ExpectedCloseDate)
            .ToList();

        return new CrmCampaignDetailDto
        {
            CampaignId = summary.CampaignId,
            Name = summary.Name,
            CampaignType = summary.CampaignType,
            CampaignStatus = summary.CampaignStatus,
            StartDate = summary.StartDate,
            EndDate = summary.EndDate,
            Budget = summary.Budget,
            ActualCost = summary.ActualCost,
            ExpectedRevenue = summary.ExpectedRevenue,
            ActualRevenue = summary.ActualRevenue,
            RoiPercent = summary.RoiPercent,
            TargetAudience = summary.TargetAudience,
            ActualAudience = summary.ActualAudience,
            ResponseCount = summary.ResponseCount,
            ResponseRate = summary.ResponseRate,
            LeadsGenerated = summary.LeadsGenerated,
            OpportunitiesGenerated = summary.OpportunitiesGenerated,
            MemberCount = summary.MemberCount,
            ActiveMemberCount = summary.ActiveMemberCount,
            RespondedMemberCount = summary.RespondedMemberCount,
            QualifiedLeadCount = summary.QualifiedLeadCount,
            ConvertedLeadCount = summary.ConvertedLeadCount,
            OpenOpportunityCount = summary.OpenOpportunityCount,
            WeightedPipelineValue = summary.WeightedPipelineValue,
            InfluencedAccountCount = summary.InfluencedAccountCount,
            IsActive = summary.IsActive,
            IsEndingSoon = summary.IsEndingSoon,
            CreatedAt = summary.CreatedAt,
            Description = campaign.Description,
            Notes = campaign.Notes,
            Members = members
                .OrderByDescending(IsRespondedCampaignMember)
                .ThenByDescending(x => x.ResponseDate ?? DateTime.MinValue)
                .ThenByDescending(x => x.DateAdded)
                .Select(x => MapCampaignMember(
                    x,
                    x.LeadId.HasValue ? leadLookup.GetValueOrDefault(x.LeadId.Value) : null,
                    businessPartnerNameLookup,
                    opportunitiesByLeadId))
                .ToList(),
            InfluencedAccounts = influencedAccountIds
                .Select(id => businessPartnerLookup.GetValueOrDefault(id))
                .Where(x => x != null)
                .Select(x =>
                {
                    var relatedLeads = leads.Where(y => y.ConvertedCustomerId == x!.Id).ToList();
                    var relatedOpenOpportunities = openOpportunities
                        .Where(y => y.CustomerId.HasValue && y.CustomerId.Value == x.Id)
                        .ToList();
                    return MapCampaignInfluenceAccount(x, relatedLeads, relatedOpenOpportunities);
                })
                .OrderByDescending(x => x.WeightedPipelineValue)
                .ThenByDescending(x => x.OpenOpportunityCount)
                .ThenBy(x => x.PartnerName)
                .ToList(),
            Opportunities = openOpportunities
                .Select(x => MapOpportunityOverview(x, businessPartnerNameLookup, leadLookup))
                .ToList()
        };
    }

    public async Task<CrmCampaignDetailDto> CreateCampaignAsync(CreateCrmCampaignDto dto)
    {
        var campaignRepository = _unitOfWork.Repository<Campaign>();

        var campaign = new Campaign
        {
            TenantId = _currentUserProvider.TenantId,
            Name = dto.Name.Trim(),
            CampaignType = CleanRequiredText(dto.CampaignType, "Email"),
            Description = CleanNullable(dto.Description),
            StartDate = dto.StartDate == default ? DateTime.UtcNow.Date : dto.StartDate,
            EndDate = dto.EndDate,
            CampaignStatus = CleanRequiredText(dto.CampaignStatus, "Planning"),
            Budget = Math.Max(dto.Budget, 0m),
            ActualCost = Math.Max(dto.ActualCost, 0m),
            ExpectedRevenue = Math.Max(dto.ExpectedRevenue, 0m),
            ActualRevenue = Math.Max(dto.ActualRevenue, 0m),
            TargetAudience = Math.Max(dto.TargetAudience, 0),
            ActualAudience = Math.Max(dto.ActualAudience, 0),
            ResponseCount = Math.Max(dto.ResponseCount, 0),
            LeadsGenerated = Math.Max(dto.LeadsGenerated, 0),
            OpportunitiesGenerated = Math.Max(dto.OpportunitiesGenerated, 0),
            Notes = CleanNullable(dto.Notes),
            CreatedById = _currentUserProvider.UserId,
            CreatedBy = _currentUserProvider.Username
        };

        await campaignRepository.AddAsync(campaign);
        await _unitOfWork.SaveChangesAsync();

        return (await GetCampaignByIdAsync(campaign.Id))!;
    }

    public async Task<CrmCampaignDetailDto> UpdateCampaignAsync(Guid campaignId, UpdateCrmCampaignDto dto)
    {
        var tenantId = _currentUserProvider.TenantId;
        var campaignRepository = _unitOfWork.Repository<Campaign>();

        var campaign = await campaignRepository.GetByIdAsync(campaignId);
        if (campaign == null || campaign.TenantId != tenantId)
        {
            throw new InvalidOperationException($"Campaign {campaignId} was not found.");
        }

        campaign.Name = dto.Name.Trim();
        campaign.CampaignType = CleanRequiredText(dto.CampaignType, campaign.CampaignType);
        campaign.Description = CleanNullable(dto.Description);
        campaign.StartDate = dto.StartDate == default ? campaign.StartDate : dto.StartDate;
        campaign.EndDate = dto.EndDate;
        campaign.CampaignStatus = CleanRequiredText(dto.CampaignStatus, campaign.CampaignStatus);
        campaign.Budget = Math.Max(dto.Budget, 0m);
        campaign.ActualCost = Math.Max(dto.ActualCost, 0m);
        campaign.ExpectedRevenue = Math.Max(dto.ExpectedRevenue, 0m);
        campaign.ActualRevenue = Math.Max(dto.ActualRevenue, 0m);
        campaign.TargetAudience = Math.Max(dto.TargetAudience, 0);
        campaign.ActualAudience = Math.Max(dto.ActualAudience, 0);
        campaign.ResponseCount = Math.Max(dto.ResponseCount, 0);
        campaign.LeadsGenerated = Math.Max(dto.LeadsGenerated, 0);
        campaign.OpportunitiesGenerated = Math.Max(dto.OpportunitiesGenerated, 0);
        campaign.Notes = CleanNullable(dto.Notes);
        campaign.LastModifiedById = _currentUserProvider.UserId;
        campaign.UpdatedBy = _currentUserProvider.Username;

        await campaignRepository.UpdateAsync(campaign);
        await _unitOfWork.SaveChangesAsync();

        return (await GetCampaignByIdAsync(campaign.Id))!;
    }

    public async Task DeleteCampaignAsync(Guid campaignId)
    {
        var tenantId = _currentUserProvider.TenantId;
        var campaignRepository = _unitOfWork.Repository<Campaign>();
        var campaignMemberRepository = _unitOfWork.Repository<CampaignMember>();

        var campaign = await campaignRepository.GetByIdAsync(campaignId);
        if (campaign == null || campaign.TenantId != tenantId)
        {
            throw new InvalidOperationException($"Campaign {campaignId} was not found.");
        }

        var members = (await campaignMemberRepository.FindAsync(x =>
                x.TenantId == tenantId
                && x.CampaignId == campaignId))
            .ToList();
        foreach (var member in members)
        {
            await campaignMemberRepository.DeleteAsync(member.Id);
        }

        await campaignRepository.DeleteAsync(campaignId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<CrmCampaignMemberDto> AddCampaignMemberAsync(Guid campaignId, CreateCrmCampaignMemberDto dto)
    {
        var tenantId = _currentUserProvider.TenantId;
        var campaignRepository = _unitOfWork.Repository<Campaign>();
        var campaignMemberRepository = _unitOfWork.Repository<CampaignMember>();

        var campaign = await campaignRepository.GetByIdAsync(campaignId);
        if (campaign == null || campaign.TenantId != tenantId)
        {
            throw new InvalidOperationException($"Campaign {campaignId} was not found.");
        }

        var lead = await ResolveLeadAsync(dto.LeadId, tenantId)
            ?? throw new InvalidOperationException($"Lead {dto.LeadId} was not found.");
        var existingMember = await campaignMemberRepository.ExistsAsync(x =>
            x.TenantId == tenantId
            && x.CampaignId == campaignId
            && x.LeadId.HasValue
            && x.LeadId.Value == lead.Id);
        if (existingMember)
        {
            throw new InvalidOperationException("Lead is already a member of this campaign.");
        }

        var member = new CampaignMember
        {
            TenantId = tenantId,
            CampaignId = campaignId,
            LeadId = lead.Id,
            CustomerId = lead.ConvertedCustomerId,
            MemberStatus = CleanRequiredText(dto.MemberStatus, "Active"),
            DateAdded = DateTime.UtcNow,
            ResponseDate = dto.ResponseDate,
            ResponseType = CleanNullable(dto.ResponseType),
            Notes = CleanNullable(dto.Notes),
            CreatedById = _currentUserProvider.UserId,
            CreatedBy = _currentUserProvider.Username
        };

        await campaignMemberRepository.AddAsync(member);
        await _unitOfWork.SaveChangesAsync();

        var opportunitiesByLeadId = new Dictionary<Guid, List<Opportunity>>();
        var opportunities = (await _unitOfWork.Repository<Opportunity>().FindAsync(x =>
                x.TenantId == tenantId
                && x.LeadId.HasValue
                && x.LeadId.Value == lead.Id))
            .ToList();
        opportunitiesByLeadId[lead.Id] = opportunities;

        var businessPartnerNameLookup = lead.ConvertedCustomerId.HasValue
            ? (await _unitOfWork.Repository<BusinessPartner>().FindAsync(x =>
                    x.TenantId == tenantId
                    && x.Id == lead.ConvertedCustomerId.Value))
                .ToDictionary(x => x.Id, x => x.PartnerName)
            : new Dictionary<Guid, string>();

        return MapCampaignMember(member, lead, businessPartnerNameLookup, opportunitiesByLeadId);
    }

    public async Task<CrmCampaignMemberDto> UpdateCampaignMemberAsync(Guid campaignId, Guid memberId, UpdateCrmCampaignMemberDto dto)
    {
        var tenantId = _currentUserProvider.TenantId;
        var campaignMemberRepository = _unitOfWork.Repository<CampaignMember>();

        var member = await campaignMemberRepository.GetByIdAsync(memberId);
        if (member == null || member.TenantId != tenantId || member.CampaignId != campaignId)
        {
            throw new InvalidOperationException($"Campaign member {memberId} was not found.");
        }

        member.MemberStatus = CleanRequiredText(dto.MemberStatus, member.MemberStatus);
        member.ResponseDate = dto.ResponseDate;
        member.ResponseType = CleanNullable(dto.ResponseType);
        member.Notes = CleanNullable(dto.Notes);
        member.LastModifiedById = _currentUserProvider.UserId;
        member.UpdatedBy = _currentUserProvider.Username;

        await campaignMemberRepository.UpdateAsync(member);
        await _unitOfWork.SaveChangesAsync();

        Lead? lead = null;
        if (member.LeadId.HasValue)
        {
            lead = await ResolveLeadAsync(member.LeadId.Value, tenantId);
        }

        var opportunitiesByLeadId = new Dictionary<Guid, List<Opportunity>>();
        var businessPartnerNameLookup = new Dictionary<Guid, string>();
        if (lead != null)
        {
            var opportunities = (await _unitOfWork.Repository<Opportunity>().FindAsync(x =>
                    x.TenantId == tenantId
                    && x.LeadId.HasValue
                    && x.LeadId.Value == lead.Id))
                .ToList();
            opportunitiesByLeadId[lead.Id] = opportunities;

            if (lead.ConvertedCustomerId.HasValue)
            {
                businessPartnerNameLookup = (await _unitOfWork.Repository<BusinessPartner>().FindAsync(x =>
                        x.TenantId == tenantId
                        && x.Id == lead.ConvertedCustomerId.Value))
                    .ToDictionary(x => x.Id, x => x.PartnerName);
            }
        }

        return MapCampaignMember(member, lead, businessPartnerNameLookup, opportunitiesByLeadId);
    }

    public async Task DeleteCampaignMemberAsync(Guid campaignId, Guid memberId)
    {
        var tenantId = _currentUserProvider.TenantId;
        var campaignMemberRepository = _unitOfWork.Repository<CampaignMember>();

        var member = await campaignMemberRepository.GetByIdAsync(memberId);
        if (member == null || member.TenantId != tenantId || member.CampaignId != campaignId)
        {
            throw new InvalidOperationException($"Campaign member {memberId} was not found.");
        }

        await campaignMemberRepository.DeleteAsync(memberId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<CrmForecastDto> GetForecastAsync(
        int months = 6,
        string? opportunityType = null,
        Guid? businessPartnerId = null)
    {
        months = Math.Clamp(months, 1, 12);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var horizonStart = new DateTime(now.Year, now.Month, 1);
        var horizonEnd = horizonStart.AddMonths(months);

        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var leadRepository = _unitOfWork.Repository<Lead>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var campaignRepository = _unitOfWork.Repository<Campaign>();
        var campaignMemberRepository = _unitOfWork.Repository<CampaignMember>();

        var openOpportunities = (await opportunityRepository.FindAsync(x => x.TenantId == tenantId))
            .Where(x => !IsClosedOpportunityStage(x.Stage))
            .ToList();
        var leadIds = openOpportunities
            .Where(x => x.LeadId.HasValue)
            .Select(x => x.LeadId!.Value)
            .Distinct()
            .ToHashSet();
        var leads = leadIds.Count == 0
            ? new List<Lead>()
            : (await leadRepository.FindAsync(x => x.TenantId == tenantId && leadIds.Contains(x.Id)))
                .ToList();
        var leadLookup = leads.ToDictionary(x => x.Id);

        if (!string.IsNullOrWhiteSpace(opportunityType))
        {
            openOpportunities = openOpportunities
                .Where(x => string.Equals(x.OpportunityType, opportunityType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (businessPartnerId.HasValue)
        {
            openOpportunities = openOpportunities
                .Where(x =>
                    (x.CustomerId.HasValue && x.CustomerId.Value == businessPartnerId.Value)
                    || (x.LeadId.HasValue
                        && leadLookup.TryGetValue(x.LeadId.Value, out var lead)
                        && lead.ConvertedCustomerId == businessPartnerId.Value))
                .ToList();
        }

        var horizonOpportunities = openOpportunities
            .Where(x => x.ExpectedCloseDate >= horizonStart && x.ExpectedCloseDate < horizonEnd)
            .OrderBy(x => x.ExpectedCloseDate)
            .ToList();
        var horizonOpportunityIds = horizonOpportunities.Select(x => x.Id).ToHashSet();
        var quotes = horizonOpportunityIds.Count == 0
            ? new List<Quote>()
            : (await quoteRepository.FindAsync(x => x.TenantId == tenantId && horizonOpportunityIds.Contains(x.OpportunityId)))
                .ToList();
        var latestQuotesByOpportunityId = quotes
            .GroupBy(x => x.OpportunityId)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderByDescending(y => y.DocumentDate == default ? y.CreatedAt : y.DocumentDate)
                    .First());

        var horizonLeadIds = horizonOpportunities
            .Where(x => x.LeadId.HasValue)
            .Select(x => x.LeadId!.Value)
            .Distinct()
            .ToHashSet();
        var activeCampaigns = (await campaignRepository.FindAsync(x => x.TenantId == tenantId))
            .Where(x => IsActiveCampaign(x, now))
            .ToList();
        var activeCampaignIds = activeCampaigns.Select(x => x.Id).ToHashSet();
        var campaignMembers = activeCampaignIds.Count == 0 || horizonLeadIds.Count == 0
            ? new List<CampaignMember>()
            : (await campaignMemberRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && x.LeadId.HasValue
                    && horizonLeadIds.Contains(x.LeadId.Value)
                    && activeCampaignIds.Contains(x.CampaignId)))
                .ToList();
        var campaignLookup = activeCampaigns.ToDictionary(x => x.Id);
        var activeCampaignNamesByLeadId = campaignMembers
            .Where(x => x.LeadId.HasValue)
            .GroupBy(x => x.LeadId!.Value)
            .ToDictionary(
                x => x.Key,
                x => x
                    .Select(y => campaignLookup.GetValueOrDefault(y.CampaignId)?.Name)
                    .Where(y => !string.IsNullOrWhiteSpace(y))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Cast<string>()
                    .ToList());

        var renewalContracts = (await contractRepository.FindAsync(x => x.TenantId == tenantId))
            .Where(x =>
                IsActiveContract(x)
                && x.EndDate.HasValue
                && x.EndDate.Value >= horizonStart
                && x.EndDate.Value < horizonEnd
                && (!businessPartnerId.HasValue || x.BusinessPartnerId == businessPartnerId.Value))
            .OrderBy(x => x.EndDate)
            .ToList();
        var renewalOpportunitiesByPartnerId = openOpportunities
            .Where(x =>
                string.Equals(x.OpportunityType, "Renewal", StringComparison.OrdinalIgnoreCase)
                && x.CustomerId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());

        var businessPartnerIds = horizonOpportunities
            .Where(x => x.CustomerId.HasValue)
            .Select(x => x.CustomerId!.Value)
            .Concat(renewalContracts.Select(x => x.BusinessPartnerId))
            .Concat(leads.Where(x => x.ConvertedCustomerId.HasValue).Select(x => x.ConvertedCustomerId!.Value))
            .Distinct()
            .ToHashSet();
        var businessPartnerLookup = businessPartnerIds.Count == 0
            ? new Dictionary<Guid, BusinessPartner>()
            : (await businessPartnerRepository.FindAsync(x => x.TenantId == tenantId && businessPartnerIds.Contains(x.Id)))
                .ToDictionary(x => x.Id);
        var businessPartnerNameLookup = businessPartnerLookup.ToDictionary(x => x.Key, x => x.Value.PartnerName);

        var buckets = Enumerable.Range(0, months)
            .Select(offset =>
            {
                var periodStart = horizonStart.AddMonths(offset);
                var periodEnd = periodStart.AddMonths(1);
                var bucketOpportunities = horizonOpportunities
                    .Where(x => x.ExpectedCloseDate >= periodStart && x.ExpectedCloseDate < periodEnd)
                    .ToList();
                var bucketContracts = renewalContracts
                    .Where(x => x.EndDate.HasValue && x.EndDate.Value >= periodStart && x.EndDate.Value < periodEnd)
                    .ToList();
                var contractPartnerIds = bucketContracts.Select(x => x.BusinessPartnerId).Distinct().ToHashSet();
                var renewalCoverageOpportunities = contractPartnerIds
                    .SelectMany(id => renewalOpportunitiesByPartnerId.GetValueOrDefault(id) ?? new List<Opportunity>())
                    .GroupBy(x => x.Id)
                    .Select(x => x.First())
                    .ToList();

                return new CrmForecastBucketDto
                {
                    PeriodStart = periodStart,
                    PeriodLabel = periodStart.ToString("MMM yyyy"),
                    OpportunityCount = bucketOpportunities.Count,
                    RenewalOpportunityCount = bucketOpportunities.Count(x => string.Equals(x.OpportunityType, "Renewal", StringComparison.OrdinalIgnoreCase)),
                    CampaignBackedOpportunityCount = bucketOpportunities.Count(x =>
                        x.LeadId.HasValue && activeCampaignNamesByLeadId.ContainsKey(x.LeadId.Value)),
                    BestCaseValue = decimal.Round(bucketOpportunities.Sum(x => x.Amount), 2),
                    WeightedValue = decimal.Round(bucketOpportunities.Sum(x => x.Amount * x.Probability / 100m), 2),
                    CommitValue = decimal.Round(bucketOpportunities.Where(x => x.Probability >= 80).Sum(x => x.Amount), 2),
                    QuoteCoverageValue = decimal.Round(bucketOpportunities.Sum(x =>
                        latestQuotesByOpportunityId.TryGetValue(x.Id, out var quote) ? ResolveQuoteValue(quote) : 0m), 2),
                    RenewalContractValue = decimal.Round(bucketContracts.Sum(x => x.ContractValue), 2),
                    RenewalCoverageValue = decimal.Round(renewalCoverageOpportunities.Sum(x => x.Amount * x.Probability / 100m), 2)
                };
            })
            .ToList();

        var highConfidenceDeals = horizonOpportunities
            .Where(x => x.Probability >= 60)
            .OrderByDescending(x => x.Amount * x.Probability / 100m)
            .ThenBy(x => x.ExpectedCloseDate)
            .Take(10)
            .Select(x => MapForecastDeal(
                x,
                businessPartnerNameLookup,
                leadLookup,
                quotes.Count(y => y.OpportunityId == x.Id),
                x.LeadId.HasValue ? activeCampaignNamesByLeadId.GetValueOrDefault(x.LeadId.Value) : null))
            .ToList();

        var renewalPartnerIds = renewalContracts.Select(x => x.BusinessPartnerId).Distinct().ToHashSet();
        var renewalCoverageOpportunitiesAll = renewalPartnerIds
            .SelectMany(id => renewalOpportunitiesByPartnerId.GetValueOrDefault(id) ?? new List<Opportunity>())
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();
        var renewalWatchlist = renewalContracts
            .Select(contract =>
            {
                var partnerRenewalOpportunities = renewalOpportunitiesByPartnerId.GetValueOrDefault(contract.BusinessPartnerId) ?? new List<Opportunity>();
                var partnerLeadIds = partnerRenewalOpportunities
                    .Where(x => x.LeadId.HasValue)
                    .Select(x => x.LeadId!.Value)
                    .Distinct()
                    .ToHashSet();
                var activeCampaignCount = partnerLeadIds
                    .SelectMany(id => activeCampaignNamesByLeadId.GetValueOrDefault(id) ?? new List<string>())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                var renewalWeightedValue = decimal.Round(partnerRenewalOpportunities.Sum(x => x.Amount * x.Probability / 100m), 2);
                var renewalCommitValue = decimal.Round(partnerRenewalOpportunities.Where(x => x.Probability >= 80).Sum(x => x.Amount), 2);
                var coverageGapValue = decimal.Round(Math.Max(contract.ContractValue - renewalWeightedValue, 0m), 2);

                return new CrmForecastRenewalDto
                {
                    ContractId = contract.Id,
                    ContractNumber = contract.ContractNumber,
                    ContractTitle = contract.ContractTitle,
                    BusinessPartnerId = contract.BusinessPartnerId,
                    BusinessPartnerName = businessPartnerNameLookup.GetValueOrDefault(contract.BusinessPartnerId) ?? contract.ContractTitle,
                    EndDate = contract.EndDate,
                    ContractValue = contract.ContractValue,
                    RenewalOpportunityCount = partnerRenewalOpportunities.Count,
                    RenewalWeightedValue = renewalWeightedValue,
                    RenewalCommitValue = renewalCommitValue,
                    CoverageGapValue = coverageGapValue,
                    ActiveCampaignCount = activeCampaignCount,
                    CoverageCategory = ResolveForecastCoverageCategory(contract.EndDate, contract.ContractValue, renewalWeightedValue, now)
                };
            })
            .OrderBy(x => x.EndDate)
            .ThenByDescending(x => x.CoverageGapValue)
            .ToList();

        return new CrmForecastDto
        {
            HorizonMonths = months,
            HorizonStart = horizonStart,
            HorizonEnd = horizonEnd.AddDays(-1),
            OpportunityCount = horizonOpportunities.Count,
            BestCaseValue = decimal.Round(horizonOpportunities.Sum(x => x.Amount), 2),
            WeightedPipelineValue = decimal.Round(horizonOpportunities.Sum(x => x.Amount * x.Probability / 100m), 2),
            CommitValue = decimal.Round(horizonOpportunities.Where(x => x.Probability >= 80).Sum(x => x.Amount), 2),
            CampaignBackedWeightedValue = decimal.Round(horizonOpportunities
                .Where(x => x.LeadId.HasValue && activeCampaignNamesByLeadId.ContainsKey(x.LeadId.Value))
                .Sum(x => x.Amount * x.Probability / 100m), 2),
            RenewalContractValue = decimal.Round(renewalContracts.Sum(x => x.ContractValue), 2),
            RenewalCoverageValue = decimal.Round(renewalCoverageOpportunitiesAll.Sum(x => x.Amount * x.Probability / 100m), 2),
            RenewalGapValue = decimal.Round(Math.Max(
                renewalContracts.Sum(x => x.ContractValue)
                - renewalCoverageOpportunitiesAll.Sum(x => x.Amount * x.Probability / 100m),
                0m), 2),
            AverageProbability = horizonOpportunities.Count == 0
                ? 0m
                : decimal.Round((decimal)horizonOpportunities.Average(x => x.Probability), 1),
            Buckets = buckets,
            HighConfidenceDeals = highConfidenceDeals,
            RenewalWatchlist = renewalWatchlist
        };
    }

    public async Task<CrmConversionsDto> GetConversionsAsync(
        int months = 6,
        string? opportunityType = null,
        Guid? businessPartnerId = null)
    {
        months = Math.Clamp(months, 1, 12);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var horizonStart = new DateTime(now.Year, now.Month, 1).AddMonths(-(months - 1));
        var horizonEnd = new DateTime(now.Year, now.Month, 1).AddMonths(1);
        var followUpWindow = now.AddDays(14);

        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var salesOrderRepository = _unitOfWork.Repository<SalesOrder>();

        var allLeads = (await leadRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var leadLookup = allLeads.ToDictionary(x => x.Id);

        var allOpportunities = (await opportunityRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        if (!string.IsNullOrWhiteSpace(opportunityType))
        {
            allOpportunities = allOpportunities
                .Where(x => string.Equals(x.OpportunityType, opportunityType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (businessPartnerId.HasValue)
        {
            allOpportunities = allOpportunities
                .Where(x =>
                    (x.CustomerId.HasValue && x.CustomerId.Value == businessPartnerId.Value)
                    || (x.LeadId.HasValue
                        && leadLookup.TryGetValue(x.LeadId.Value, out var lead)
                        && lead.ConvertedCustomerId == businessPartnerId.Value))
                .ToList();
        }

        var horizonOpportunities = allOpportunities
            .Where(x =>
            {
                var referenceDate = GetOpportunityConversionReferenceDate(x);
                return (referenceDate >= horizonStart && referenceDate < horizonEnd)
                    || (x.CreatedAt >= horizonStart && x.CreatedAt < horizonEnd);
            })
            .OrderBy(x => x.ExpectedCloseDate)
            .ThenByDescending(x => x.Amount * x.Probability / 100m)
            .ToList();

        var horizonOpportunityIds = horizonOpportunities.Select(x => x.Id).ToHashSet();
        var scopedLeadIds = horizonOpportunities
            .Where(x => x.LeadId.HasValue)
            .Select(x => x.LeadId!.Value)
            .ToHashSet();

        var relevantLeads = allLeads
            .Where(x =>
                scopedLeadIds.Contains(x.Id)
                || ((x.CreatedAt >= horizonStart && x.CreatedAt < horizonEnd)
                    || (x.NextFollowUpDate.HasValue && x.NextFollowUpDate.Value >= horizonStart && x.NextFollowUpDate.Value < horizonEnd)
                    || (x.LastContactDate.HasValue && x.LastContactDate.Value >= horizonStart && x.LastContactDate.Value < horizonEnd)))
            .ToList();

        if (businessPartnerId.HasValue)
        {
            relevantLeads = relevantLeads
                .Where(x => scopedLeadIds.Contains(x.Id) || x.ConvertedCustomerId == businessPartnerId.Value)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(opportunityType))
        {
            relevantLeads = relevantLeads
                .Where(x => scopedLeadIds.Contains(x.Id))
                .ToList();
        }

        var quotes = horizonOpportunityIds.Count == 0
            ? new List<Quote>()
            : (await quoteRepository.FindAsync(x => x.TenantId == tenantId && horizonOpportunityIds.Contains(x.OpportunityId)))
                .ToList();
        var quoteLookup = quotes.ToDictionary(x => x.Id);
        var quoteIds = quoteLookup.Keys.ToHashSet();
        var quotesByOpportunityId = quotes
            .GroupBy(x => x.OpportunityId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var salesOrders = horizonOpportunityIds.Count == 0 && quoteIds.Count == 0
            ? new List<SalesOrder>()
            : (await salesOrderRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && ((x.OpportunityId.HasValue && horizonOpportunityIds.Contains(x.OpportunityId.Value))
                        || (x.QuoteId.HasValue && quoteIds.Contains(x.QuoteId.Value)))))
                .ToList();
        var salesOrdersByOpportunityId = salesOrders
            .Select(order => new
            {
                Order = order,
                OpportunityId = order.OpportunityId
                    ?? (order.QuoteId.HasValue && quoteLookup.TryGetValue(order.QuoteId.Value, out var linkedQuote)
                        ? linkedQuote.OpportunityId
                        : (Guid?)null)
            })
            .Where(x => x.OpportunityId.HasValue)
            .GroupBy(x => x.OpportunityId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Order).ToList());

        var relevantBusinessPartnerIds = horizonOpportunities
            .Select(x => ResolveOpportunityBusinessPartnerId(x, quotesByOpportunityId.GetValueOrDefault(x.Id), leadLookup))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Concat(relevantLeads.Where(x => x.ConvertedCustomerId.HasValue).Select(x => x.ConvertedCustomerId!.Value))
            .Distinct()
            .ToHashSet();

        var contracts = relevantBusinessPartnerIds.Count == 0
            ? new List<Contract>()
            : (await contractRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && relevantBusinessPartnerIds.Contains(x.BusinessPartnerId)))
                .Where(x =>
                    x.CreatedAt >= horizonStart
                    || IsActiveContract(x)
                    || (x.EndDate.HasValue && x.EndDate.Value >= horizonStart))
                .ToList();
        var contractLookup = contracts.ToDictionary(x => x.Id);
        var relevantContractIds = contractLookup.Keys.ToHashSet();
        var contractsByPartnerId = contracts
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var projects = (relevantBusinessPartnerIds.Count == 0 && contractLookup.Count == 0)
            ? new List<Project>()
            : (await projectRepository.FindAsync(x =>
                    x.TenantId == tenantId
                    && ((x.BusinessPartnerId.HasValue && relevantBusinessPartnerIds.Contains(x.BusinessPartnerId.Value))
                        || (x.ContractId.HasValue && relevantContractIds.Contains(x.ContractId.Value)))))
                .Where(x =>
                    x.CreatedAt >= horizonStart
                    || IsActiveProject(x)
                    || (x.TargetEndDate.HasValue && x.TargetEndDate.Value >= horizonStart)
                    || (x.ActualEndDate.HasValue && x.ActualEndDate.Value >= horizonStart))
                .ToList();
        var projectsByResolvedPartnerId = projects
            .Select(x => new
            {
                Project = x,
                BusinessPartnerId = ResolveProjectBusinessPartnerId(x, contractLookup)
            })
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Project).ToList());

        var businessPartners = relevantBusinessPartnerIds.Count == 0
            ? new List<BusinessPartner>()
            : (await businessPartnerRepository.FindAsync(x => x.TenantId == tenantId && relevantBusinessPartnerIds.Contains(x.Id)))
                .ToList();
        var businessPartnerNameLookup = businessPartners.ToDictionary(x => x.Id, x => x.PartnerName);

        var journeyRecords = horizonOpportunities
            .Select(opportunity =>
            {
                leadLookup.TryGetValue(opportunity.LeadId ?? Guid.Empty, out var lead);
                var relatedQuotes = quotesByOpportunityId.GetValueOrDefault(opportunity.Id) ?? new List<Quote>();
                var resolvedBusinessPartnerId = ResolveOpportunityBusinessPartnerId(opportunity, relatedQuotes, leadLookup);
                var relatedContracts = resolvedBusinessPartnerId.HasValue
                    ? contractsByPartnerId.GetValueOrDefault(resolvedBusinessPartnerId.Value) ?? new List<Contract>()
                    : new List<Contract>();
                var relatedProjects = resolvedBusinessPartnerId.HasValue
                    ? projectsByResolvedPartnerId.GetValueOrDefault(resolvedBusinessPartnerId.Value) ?? new List<Project>()
                    : new List<Project>();
                var relatedSalesOrders = salesOrdersByOpportunityId.GetValueOrDefault(opportunity.Id) ?? new List<SalesOrder>();
                var leakageReason = ResolveConversionJourneyLeakageReason(
                    opportunity,
                    relatedQuotes.Count,
                    relatedContracts.Count,
                    relatedProjects.Count,
                    now);

                return new
                {
                    Opportunity = opportunity,
                    Lead = lead,
                    RelatedQuotes = relatedQuotes,
                    RelatedContracts = relatedContracts,
                    RelatedProjects = relatedProjects,
                    LeakageReason = leakageReason,
                    Journey = new CrmConversionJourneyDto
                    {
                        OpportunityId = opportunity.Id,
                        OpportunityName = opportunity.Name,
                        Stage = opportunity.Stage,
                        OpportunityType = opportunity.OpportunityType,
                        BusinessPartnerId = resolvedBusinessPartnerId,
                        BusinessPartnerName = resolvedBusinessPartnerId.HasValue
                            ? businessPartnerNameLookup.GetValueOrDefault(resolvedBusinessPartnerId.Value)
                            : lead?.CompanyName,
                        LeadId = opportunity.LeadId,
                        LeadName = lead == null ? null : GetLeadFullName(lead),
                        Amount = opportunity.Amount,
                        Currency = NormalizeCurrencyCode(opportunity.Currency, "USD"),
                        WeightedValue = decimal.Round(opportunity.Amount * opportunity.Probability / 100m, 2),
                        ExpectedCloseDate = opportunity.ExpectedCloseDate,
                        QuoteCount = relatedQuotes.Count,
                        ContractCount = relatedContracts.Count,
                        ProjectCount = relatedProjects.Count,
                        CoverageStatus = ResolveConversionCoverageStatus(
                            opportunity.Stage,
                            relatedQuotes.Count,
                            relatedContracts.Count,
                            relatedProjects.Count),
                        LeakageReason = leakageReason,
                        Chain = BuildConversionChain(
                            opportunity,
                            relatedQuotes,
                            relatedContracts,
                            relatedProjects,
                            relatedSalesOrders,
                            businessPartnerNameLookup,
                            leadLookup)
                    }
                };
            })
            .ToList();

        var leadWithOpportunityCount = relevantLeads.Count(x => scopedLeadIds.Contains(x.Id));
        var quotedOpportunityCount = journeyRecords.Count(x => x.RelatedQuotes.Count > 0);
        var contractBackedOpportunityCount = journeyRecords.Count(x => x.RelatedContracts.Count > 0);
        var projectBackedOpportunityCount = journeyRecords.Count(x => x.RelatedProjects.Count > 0);

        var journeys = journeyRecords
            .Select(x => x.Journey)
            .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.LeakageReason))
            .ThenBy(x => x.ExpectedCloseDate)
            .ThenByDescending(x => x.WeightedValue)
            .ToList();

        var leadLeakage = relevantLeads
            .Where(x => !IsClosedLeadStatus(x.LeadStatus) && !scopedLeadIds.Contains(x.Id))
            .Select(x => new CrmConversionLeakDto
            {
                EntityType = "Lead",
                EntityId = x.Id,
                Name = GetLeadFullName(x),
                Status = x.LeadStatus,
                LeakageStage = "Lead",
                LeakageReason = NeedsLeadFollowUp(x, followUpWindow)
                    ? "Lead follow-up is overdue and no opportunity exists yet."
                    : "Lead has not progressed into an opportunity yet.",
                BusinessPartnerId = x.ConvertedCustomerId,
                BusinessPartnerName = x.ConvertedCustomerId.HasValue
                    ? businessPartnerNameLookup.GetValueOrDefault(x.ConvertedCustomerId.Value)
                    : x.CompanyName,
                Amount = x.EstimatedValue,
                ReferenceDate = x.NextFollowUpDate ?? x.LastContactDate ?? x.CreatedAt
            });

        var opportunityLeakage = journeyRecords
            .Where(x => !string.IsNullOrWhiteSpace(x.LeakageReason))
            .Select(x => new CrmConversionLeakDto
            {
                EntityType = "Opportunity",
                EntityId = x.Opportunity.Id,
                Name = x.Opportunity.Name,
                Status = x.Opportunity.Stage,
                LeakageStage = x.Journey.CoverageStatus,
                LeakageReason = x.LeakageReason!,
                BusinessPartnerId = x.Journey.BusinessPartnerId,
                BusinessPartnerName = x.Journey.BusinessPartnerName,
                Amount = x.Opportunity.Amount,
                Currency = NormalizeCurrencyCode(x.Opportunity.Currency, "USD"),
                ReferenceDate = x.Opportunity.ExpectedCloseDate
            });

        var leakage = leadLeakage
            .Concat(opportunityLeakage)
            .OrderBy(x => x.ReferenceDate ?? DateTime.MaxValue)
            .ThenBy(x => x.Name)
            .ToList();

        return new CrmConversionsDto
        {
            HorizonMonths = months,
            HorizonStart = horizonStart,
            HorizonEnd = horizonEnd.AddDays(-1),
            LeadCount = relevantLeads.Count,
            LeadWithOpportunityCount = leadWithOpportunityCount,
            OpportunityCount = horizonOpportunities.Count,
            QuotedOpportunityCount = quotedOpportunityCount,
            ContractBackedOpportunityCount = contractBackedOpportunityCount,
            ProjectBackedOpportunityCount = projectBackedOpportunityCount,
            LeadToOpportunityRate = relevantLeads.Count == 0
                ? 0m
                : decimal.Round(leadWithOpportunityCount * 100m / relevantLeads.Count, 1),
            OpportunityToQuoteRate = horizonOpportunities.Count == 0
                ? 0m
                : decimal.Round(quotedOpportunityCount * 100m / horizonOpportunities.Count, 1),
            OpportunityToContractRate = horizonOpportunities.Count == 0
                ? 0m
                : decimal.Round(contractBackedOpportunityCount * 100m / horizonOpportunities.Count, 1),
            OpportunityToProjectRate = horizonOpportunities.Count == 0
                ? 0m
                : decimal.Round(projectBackedOpportunityCount * 100m / horizonOpportunities.Count, 1),
            TotalOpportunityValue = decimal.Round(horizonOpportunities.Sum(x => x.Amount), 2),
            WeightedPipelineValue = decimal.Round(horizonOpportunities
                .Where(x => !IsClosedOpportunityStage(x.Stage))
                .Sum(x => x.Amount * x.Probability / 100m), 2),
            Funnel =
            {
                new CrmConversionStageMetricDto
                {
                    Stage = "Lead",
                    EntityCount = relevantLeads.Count,
                    RelatedOpportunityCount = leadWithOpportunityCount,
                    TotalValue = decimal.Round(relevantLeads.Sum(x => x.EstimatedValue), 2),
                    ConversionRate = relevantLeads.Count == 0
                        ? 0m
                        : decimal.Round(leadWithOpportunityCount * 100m / relevantLeads.Count, 1)
                },
                new CrmConversionStageMetricDto
                {
                    Stage = "Opportunity",
                    EntityCount = horizonOpportunities.Count,
                    RelatedOpportunityCount = horizonOpportunities.Count,
                    TotalValue = decimal.Round(horizonOpportunities.Sum(x => x.Amount), 2),
                    ConversionRate = horizonOpportunities.Count == 0 ? 0m : 100m
                },
                new CrmConversionStageMetricDto
                {
                    Stage = "Quote",
                    EntityCount = quotes.Count,
                    RelatedOpportunityCount = quotedOpportunityCount,
                    TotalValue = decimal.Round(quotes.Sum(ResolveQuoteValue), 2),
                    ConversionRate = horizonOpportunities.Count == 0
                        ? 0m
                        : decimal.Round(quotedOpportunityCount * 100m / horizonOpportunities.Count, 1)
                },
                new CrmConversionStageMetricDto
                {
                    Stage = "Contract",
                    EntityCount = contracts.Count,
                    RelatedOpportunityCount = contractBackedOpportunityCount,
                    TotalValue = decimal.Round(contracts.Sum(x => x.ContractValue), 2),
                    ConversionRate = horizonOpportunities.Count == 0
                        ? 0m
                        : decimal.Round(contractBackedOpportunityCount * 100m / horizonOpportunities.Count, 1)
                },
                new CrmConversionStageMetricDto
                {
                    Stage = "Project",
                    EntityCount = projects.Count,
                    RelatedOpportunityCount = projectBackedOpportunityCount,
                    TotalValue = decimal.Round(projects.Sum(x => x.ApprovedBudget ?? x.EstimatedBudget ?? 0m), 2),
                    ConversionRate = horizonOpportunities.Count == 0
                        ? 0m
                        : decimal.Round(projectBackedOpportunityCount * 100m / horizonOpportunities.Count, 1)
                }
            },
            Journeys = journeys,
            Leakage = leakage
        };
    }

    public async Task<CrmReportingDto> GetReportingAsync(int take = 10)
    {
        take = ClampTake(take);

        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;
        var closingWindow = now.AddDays(30);
        var contractWindow = now.AddDays(90);

        var leadRepository = _unitOfWork.Repository<Lead>();
        var opportunityRepository = _unitOfWork.Repository<Opportunity>();
        var quoteRepository = _unitOfWork.Repository<Quote>();
        var businessPartnerRepository = _unitOfWork.Repository<BusinessPartner>();
        var projectRepository = _unitOfWork.Repository<Project>();
        var contractRepository = _unitOfWork.Repository<Contract>();
        var tenderInvitationRepository = _unitOfWork.Repository<TenderInvitation>();
        var tenderBidRepository = _unitOfWork.Repository<TenderBid>();
        var tenderAwardRepository = _unitOfWork.Repository<TenderAward>();

        var leads = (await leadRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var opportunities = (await opportunityRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var quotes = (await quoteRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var businessPartners = (await businessPartnerRepository.FindAsync(x => x.TenantId == tenantId && x.IsActive)).ToList();
        var projects = (await projectRepository.FindAsync(x => x.TenantId == tenantId && x.BusinessPartnerId.HasValue)).ToList();
        var contracts = (await contractRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var tenderInvitations = (await tenderInvitationRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var tenderBids = (await tenderBidRepository.FindAsync(x => x.TenantId == tenantId)).ToList();
        var tenderAwards = (await tenderAwardRepository.FindAsync(x => x.TenantId == tenantId)).ToList();

        var leadLookup = leads.ToDictionary(x => x.Id);
        var opportunityLookup = opportunities.ToDictionary(x => x.Id);
        var businessPartnerLookup = businessPartners.ToDictionary(x => x.Id, x => x.PartnerName);
        var openOpportunities = opportunities.Where(x => !IsClosedOpportunityStage(x.Stage)).ToList();
        var activeQuotes = quotes.Where(x => !IsClosedQuoteStatus(x.QuoteStatus)).ToList();
        var acceptedQuotes = quotes.Where(x => string.Equals(x.QuoteStatus, "Accepted", StringComparison.OrdinalIgnoreCase)).ToList();
        var convertedLeadIdsByPartnerId = leads
            .Where(x => x.ConvertedCustomerId.HasValue)
            .GroupBy(x => x.ConvertedCustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToHashSet());
        var opportunityLeadIdsByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue && x.LeadId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.LeadId!.Value).ToHashSet());

        var projectsByPartnerId = projects
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var contractsByPartnerId = contracts
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var invitationsByPartnerId = tenderInvitations
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var bidsByPartnerId = tenderBids
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var awardsByPartnerId = tenderAwards
            .GroupBy(x => x.BusinessPartnerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var openOpportunitiesByPartnerId = openOpportunities
            .Where(x => x.CustomerId.HasValue)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var activeQuotesByPartnerId = activeQuotes
            .Select(x => new
            {
                Quote = x,
                BusinessPartnerId = ResolveQuoteBusinessPartnerId(x, opportunityLookup)
            })
            .Where(x => x.BusinessPartnerId.HasValue)
            .GroupBy(x => x.BusinessPartnerId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Quote).ToList());

        var accounts = businessPartners
            .Select(partner => BuildAccountOverview(
                partner,
                projectsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Project>(),
                contractsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Contract>(),
                invitationsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderInvitation>(),
                bidsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderBid>(),
                awardsByPartnerId.GetValueOrDefault(partner.Id) ?? new List<TenderAward>(),
                openOpportunitiesByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Opportunity>(),
                activeQuotesByPartnerId.GetValueOrDefault(partner.Id) ?? new List<Quote>(),
                GetRelatedLeadCount(partner.Id, convertedLeadIdsByPartnerId, opportunityLeadIdsByPartnerId),
                now,
                contractWindow))
            .ToList();

        var pipelineByStage = openOpportunities
            .GroupBy(x => x.Stage)
            .Select(x =>
            {
                var stageOpportunityIds = x.Select(y => y.Id).ToHashSet();
                return new CrmPipelineStageReportDto
                {
                    Stage = x.Key,
                    OpportunityCount = x.Count(),
                    TotalValue = decimal.Round(x.Sum(y => y.Amount), 2),
                    WeightedValue = decimal.Round(x.Sum(y => y.Amount * y.Probability / 100m), 2),
                    QuoteCount = quotes.Count(y => stageOpportunityIds.Contains(y.OpportunityId))
                };
            })
            .OrderByDescending(x => x.WeightedValue)
            .ThenBy(x => x.Stage)
            .ToList();

        return new CrmReportingDto
        {
            TotalLeadCount = leads.Count,
            QualifiedLeadCount = leads.Count(x => string.Equals(x.LeadStatus, "Qualified", StringComparison.OrdinalIgnoreCase)),
            ConvertedLeadCount = leads.Count(x => string.Equals(x.LeadStatus, "Converted", StringComparison.OrdinalIgnoreCase)),
            LeadConversionRate = CalculateRate(
                leads.Count(x => string.Equals(x.LeadStatus, "Converted", StringComparison.OrdinalIgnoreCase)),
                leads.Count),
            OpenOpportunityCount = openOpportunities.Count,
            OpenOpportunityValue = decimal.Round(openOpportunities.Sum(x => x.Amount), 2),
            WeightedPipelineValue = decimal.Round(openOpportunities.Sum(x => x.Amount * x.Probability / 100m), 2),
            TotalQuoteCount = quotes.Count,
            AcceptedQuoteCount = acceptedQuotes.Count,
            AcceptedQuoteValue = decimal.Round(acceptedQuotes.Sum(ResolveQuoteValue), 2),
            QuoteAcceptanceRate = CalculateRate(acceptedQuotes.Count, quotes.Count),
            ActiveAccountCount = accounts.Count,
            AverageAccountHealthScore = accounts.Count == 0
                ? 0m
                : decimal.Round((decimal)accounts.Average(x => x.HealthScore), 1),
            AtRiskAccountCount = accounts.Count(x => x.IsAtRisk),
            PipelineByStage = pipelineByStage,
            AccountHealth = accounts
                .OrderByDescending(x => x.HealthScore)
                .ThenByDescending(x => openOpportunitiesByPartnerId.GetValueOrDefault(x.BusinessPartnerId)?.Sum(y => y.Amount) ?? 0m)
                .Take(take)
                .Select(x => new CrmAccountHealthReportDto
                {
                    BusinessPartnerId = x.BusinessPartnerId,
                    PartnerCode = x.PartnerCode,
                    PartnerName = x.PartnerName,
                    HealthScore = x.HealthScore,
                    HealthCategory = x.HealthCategory,
                    RiskLevel = x.RiskLevel,
                    PerformanceRating = x.PerformanceRating,
                    OpenOpportunityCount = openOpportunitiesByPartnerId.GetValueOrDefault(x.BusinessPartnerId)?.Count ?? 0,
                    OpenOpportunityValue = decimal.Round(openOpportunitiesByPartnerId.GetValueOrDefault(x.BusinessPartnerId)?.Sum(y => y.Amount) ?? 0m, 2),
                    ActiveProjectCount = x.ActiveProjectCount,
                    ActiveContractCount = x.ActiveContractCount,
                    HasOpenFollowUp = x.HasOpenFollowUp,
                    IsAtRisk = x.IsAtRisk,
                    NextMilestoneDate = x.NextMilestoneDate
                })
                .ToList(),
            ClosingOpportunities = openOpportunities
                .Where(x => x.ExpectedCloseDate <= closingWindow)
                .OrderBy(x => x.ExpectedCloseDate)
                .ThenByDescending(x => x.Amount * x.Probability / 100m)
                .Take(take)
                .Select(x => MapOpportunityListItem(x, businessPartnerLookup, leadLookup))
                .ToList(),
            ExpiringContracts = contracts
                .Where(x => IsActiveContract(x) && x.EndDate.HasValue && x.EndDate.Value <= contractWindow)
                .OrderBy(x => x.EndDate)
                .ThenByDescending(x => x.ContractValue)
                .Take(take)
                .Select(x => MapContractSummary(x))
                .ToList()
        };
    }

    private async Task<Lead?> ResolveLeadAsync(Guid? leadId, Guid tenantId)
    {
        if (!leadId.HasValue)
        {
            return null;
        }

        var lead = await _unitOfWork.Repository<Lead>().GetByIdAsync(leadId.Value);
        if (lead == null || lead.TenantId != tenantId)
        {
            throw new InvalidOperationException($"Lead {leadId.Value} was not found.");
        }

        return lead;
    }

    private async Task<Guid?> ResolveBusinessPartnerIdAsync(Guid? businessPartnerId, Lead? lead, Guid tenantId)
    {
        var resolvedBusinessPartnerId = businessPartnerId ?? lead?.ConvertedCustomerId;
        if (!resolvedBusinessPartnerId.HasValue)
        {
            return null;
        }

        var businessPartner = await _unitOfWork.Repository<BusinessPartner>().GetByIdAsync(resolvedBusinessPartnerId.Value);
        if (businessPartner == null || businessPartner.TenantId != tenantId)
        {
            throw new InvalidOperationException($"CRM account {resolvedBusinessPartnerId.Value} was not found.");
        }

        return resolvedBusinessPartnerId.Value;
    }

    private async Task<Opportunity?> ResolveOpportunityAsync(Guid? opportunityId, Guid tenantId)
    {
        if (!opportunityId.HasValue)
        {
            return null;
        }

        var opportunity = await _unitOfWork.Repository<Opportunity>().GetByIdAsync(opportunityId.Value);
        if (opportunity == null || opportunity.TenantId != tenantId)
        {
            throw new InvalidOperationException($"Opportunity {opportunityId.Value} was not found.");
        }

        return opportunity;
    }

    private async Task<Lead?> ResolveActivityLeadAsync(Guid? leadId, Opportunity? opportunity, Guid tenantId)
    {
        if (leadId.HasValue && opportunity?.LeadId.HasValue == true && opportunity.LeadId.Value != leadId.Value)
        {
            throw new InvalidOperationException("Activity lead must match the lead already linked to the selected opportunity.");
        }

        var resolvedLeadId = leadId ?? opportunity?.LeadId;
        return await ResolveLeadAsync(resolvedLeadId, tenantId);
    }

    private async Task<Guid?> ResolveActivityBusinessPartnerIdAsync(
        Guid? businessPartnerId,
        Lead? lead,
        Opportunity? opportunity,
        Guid tenantId)
    {
        if (businessPartnerId.HasValue && opportunity?.CustomerId.HasValue == true && opportunity.CustomerId.Value != businessPartnerId.Value)
        {
            throw new InvalidOperationException("Activity account must match the CRM account already linked to the selected opportunity.");
        }

        if (businessPartnerId.HasValue && lead?.ConvertedCustomerId.HasValue == true && lead.ConvertedCustomerId.Value != businessPartnerId.Value)
        {
            throw new InvalidOperationException("Activity account must match the CRM account already linked to the selected lead.");
        }

        var resolvedBusinessPartnerId = businessPartnerId ?? opportunity?.CustomerId ?? lead?.ConvertedCustomerId;
        return await ResolveBusinessPartnerIdAsync(resolvedBusinessPartnerId, lead, tenantId);
    }

    private static void ValidateActivityAssociations(Guid? businessPartnerId, Lead? lead, Opportunity? opportunity)
    {
        if (!businessPartnerId.HasValue && lead == null && opportunity == null)
        {
            throw new InvalidOperationException("Activity must be linked to a CRM account, lead, or opportunity.");
        }

        if (opportunity?.CustomerId.HasValue == true && businessPartnerId.HasValue && opportunity.CustomerId.Value != businessPartnerId.Value)
        {
            throw new InvalidOperationException("Activity account must stay aligned with the selected opportunity.");
        }

        if (opportunity?.LeadId.HasValue == true && lead != null && opportunity.LeadId.Value != lead.Id)
        {
            throw new InvalidOperationException("Activity lead must stay aligned with the selected opportunity.");
        }
    }

    private static CrmAccountOverviewDto BuildAccountOverview(
        BusinessPartner partner,
        IReadOnlyCollection<Project> projects,
        IReadOnlyCollection<Contract> contracts,
        IReadOnlyCollection<TenderInvitation> tenderInvitations,
        IReadOnlyCollection<TenderBid> tenderBids,
        IReadOnlyCollection<TenderAward> tenderAwards,
        IReadOnlyCollection<Opportunity> opportunities,
        IReadOnlyCollection<Quote> quotes,
        int relatedLeadCount,
        DateTime now,
        DateTime contractWindow)
    {
        var activeProjectCount = projects.Count(IsActiveProject);
        var activeContractCount = contracts.Count(IsActiveContract);
        var projectValue = decimal.Round(projects.Sum(x => x.ApprovedBudget ?? x.EstimatedBudget ?? 0m), 2);
        var contractValue = decimal.Round(contracts.Sum(x => x.ContractValue), 2);
        var awardedValue = decimal.Round(tenderAwards.Sum(x => x.AwardedAmount), 2);
        var health = AssessAccountHealth(
            partner,
            projects,
            contracts,
            tenderInvitations,
            tenderBids,
            tenderAwards,
            opportunities,
            quotes,
            false,
            false,
            now,
            contractWindow);

        return new CrmAccountOverviewDto
        {
            BusinessPartnerId = partner.Id,
            PartnerCode = partner.PartnerCode,
            PartnerName = partner.PartnerName,
            PartnerType = partner.PartnerType,
            RegistrationStatus = partner.RegistrationStatus,
            CustomerType = partner.CustomerType,
            SalesTerritory = partner.SalesTerritory,
            RiskLevel = partner.RiskLevel,
            PerformanceRating = partner.PerformanceRating,
            RelatedLeadCount = relatedLeadCount,
            OpenOpportunityCount = opportunities.Count,
            OpenOpportunityValue = decimal.Round(opportunities.Sum(x => x.Amount), 2),
            WeightedPipelineValue = decimal.Round(opportunities.Sum(x => x.Amount * x.Probability / 100m), 2),
            ActiveQuoteCount = quotes.Count,
            ActiveQuoteValue = decimal.Round(quotes.Sum(ResolveQuoteValue), 2),
            TotalProjectCount = projects.Count,
            ActiveProjectCount = activeProjectCount,
            ProjectValue = projectValue,
            TotalContractCount = contracts.Count,
            ActiveContractCount = activeContractCount,
            ContractValue = contractValue,
            TenderInvitationCount = tenderInvitations.Count,
            TenderBidCount = tenderBids.Count,
            TenderAwardCount = tenderAwards.Count,
            TenderAwardedValue = awardedValue,
            HasOpenFollowUp = health.HasOpenFollowUp,
            IsAtRisk = health.IsAtRisk,
            HealthScore = health.Score,
            HealthCategory = health.Category,
            NextMilestoneDate = health.NextMilestoneDate
        };
    }

    private static bool IsCrmRelevantAccount(CrmAccountOverviewDto account) =>
        account.TotalProjectCount > 0
        || account.TotalContractCount > 0
        || account.TenderInvitationCount > 0
        || account.TenderBidCount > 0
        || account.TenderAwardCount > 0
        || account.OpenOpportunityCount > 0
        || account.ActiveQuoteCount > 0
        || BusinessPartnerRoles.HasCustomer(account.PartnerType)
        || !string.IsNullOrWhiteSpace(account.CustomerType);

    private static int GetRelatedLeadCount(
        Guid businessPartnerId,
        IReadOnlyDictionary<Guid, HashSet<Guid>> convertedLeadIdsByPartnerId,
        IReadOnlyDictionary<Guid, HashSet<Guid>> opportunityLeadIdsByPartnerId)
    {
        var relatedLeadIds = new HashSet<Guid>();

        if (convertedLeadIdsByPartnerId.TryGetValue(businessPartnerId, out var convertedLeadIds))
        {
            relatedLeadIds.UnionWith(convertedLeadIds);
        }

        if (opportunityLeadIdsByPartnerId.TryGetValue(businessPartnerId, out var opportunityLeadIds))
        {
            relatedLeadIds.UnionWith(opportunityLeadIds);
        }

        return relatedLeadIds.Count;
    }

    private static CrmAccountContactDto MapAccountContact(BusinessPartnerContact contact)
        => new()
        {
            ContactId = contact.Id,
            ContactName = contact.ContactName,
            ContactTitle = contact.ContactTitle,
            Department = contact.Department,
            Email = contact.Email,
            Phone = contact.Phone,
            Mobile = contact.Mobile,
            IsPrimary = contact.IsPrimary
        };

    private static CrmContactListItemDto MapContactListItem(
        BusinessPartnerContact contact,
        CrmAccountOverviewDto account)
        => new()
        {
            ContactId = contact.Id,
            BusinessPartnerId = account.BusinessPartnerId,
            PartnerCode = account.PartnerCode,
            PartnerName = account.PartnerName,
            PartnerType = account.PartnerType,
            RegistrationStatus = account.RegistrationStatus,
            ContactName = contact.ContactName,
            ContactTitle = contact.ContactTitle,
            Department = contact.Department,
            Email = contact.Email,
            Phone = contact.Phone,
            Mobile = contact.Mobile,
            IsPrimary = contact.IsPrimary,
            SalesTerritory = account.SalesTerritory,
            CustomerType = account.CustomerType,
            HasOpenFollowUp = account.HasOpenFollowUp,
            IsAtRisk = account.IsAtRisk,
            HealthScore = account.HealthScore,
            HealthCategory = account.HealthCategory,
            OpenOpportunityCount = account.OpenOpportunityCount,
            OpenOpportunityValue = account.OpenOpportunityValue,
            ActiveProjectCount = account.ActiveProjectCount,
            ActiveContractCount = account.ActiveContractCount,
            NextMilestoneDate = account.NextMilestoneDate
        };

    private static CrmReadinessListItemDto BuildReadinessListItem(
        BusinessPartner partner,
        CrmAccountOverviewDto account,
        AccountReadinessAssessment readiness)
        => new()
        {
            BusinessPartnerId = partner.Id,
            PartnerCode = partner.PartnerCode,
            PartnerName = partner.PartnerName,
            PartnerType = partner.PartnerType,
            RegistrationStatus = partner.RegistrationStatus,
            CustomerType = partner.CustomerType,
            SalesTerritory = partner.SalesTerritory,
            DocumentCount = readiness.DocumentCount,
            VerifiedDocumentCount = readiness.VerifiedDocumentCount,
            ExpiringDocumentCount = readiness.ExpiringDocumentCount,
            ExpiredDocumentCount = readiness.ExpiredDocumentCount,
            LicenseCount = readiness.LicenseCount,
            ExpiringLicenseCount = readiness.ExpiringLicenseCount,
            ExpiredLicenseCount = readiness.ExpiredLicenseCount,
            FinancialRecordCount = readiness.FinancialRecordCount,
            LatestFinancialYear = readiness.LatestFinancialYear,
            LatestAnnualRevenue = readiness.LatestAnnualRevenue,
            CreditRating = readiness.CreditRating,
            OpenOpportunityCount = account.OpenOpportunityCount,
            ActiveProjectCount = account.ActiveProjectCount,
            ActiveContractCount = account.ActiveContractCount,
            IsAtRisk = account.IsAtRisk,
            HealthScore = account.HealthScore,
            HealthCategory = account.HealthCategory,
            ReadinessScore = readiness.Score,
            ReadinessCategory = readiness.Category,
            HasCriticalGap = readiness.HasCriticalGap,
            NextComplianceDate = readiness.NextComplianceDate
        };

    private static CrmReadinessDocumentDto MapReadinessDocument(
        BusinessPartnerDocument document,
        DateTime now,
        DateTime readinessWindow)
        => new()
        {
            DocumentId = document.Id,
            DocumentType = document.DocumentType,
            DocumentName = document.DocumentName,
            IsVerified = document.IsVerified,
            IssueDate = document.IssueDate,
            ExpiryDate = document.ExpiryDate,
            UploadedAt = document.CreatedAt,
            IsExpired = document.ExpiryDate.HasValue && document.ExpiryDate.Value < now,
            IsExpiringSoon = document.ExpiryDate.HasValue
                && document.ExpiryDate.Value >= now
                && document.ExpiryDate.Value <= readinessWindow
        };

    private static CrmReadinessLicenseDto MapReadinessLicense(
        BusinessPartnerLicense license,
        DateTime now,
        DateTime readinessWindow)
    {
        var isExpired = string.Equals(license.Status, "Expired", StringComparison.OrdinalIgnoreCase)
            || license.ExpiryDate.HasValue && license.ExpiryDate.Value < now;
        var isExpiringSoon = !isExpired
            && license.ExpiryDate.HasValue
            && license.ExpiryDate.Value >= now
            && license.ExpiryDate.Value <= readinessWindow;

        return new CrmReadinessLicenseDto
        {
            LicenseId = license.Id,
            LicenseTypeName = license.LicenseType?.LicenseName ?? "License",
            LicenseNumber = license.LicenseNumber,
            Status = license.Status,
            IssuingAuthority = license.IssuingAuthority,
            IssueDate = license.IssueDate,
            ExpiryDate = license.ExpiryDate,
            IsExpired = isExpired,
            IsExpiringSoon = isExpiringSoon
        };
    }

    private static CrmReadinessFinancialDto MapReadinessFinancial(BusinessPartnerFinancial financial)
        => new()
        {
            FinancialId = financial.Id,
            FinancialYear = financial.FiscalYear,
            AnnualRevenue = financial.AnnualRevenue,
            NetProfit = financial.NetProfit,
            TotalAssets = financial.TotalAssets,
            TotalLiabilities = financial.TotalLiabilities,
            CreditRating = financial.CreditRating,
            IsAudited = !string.IsNullOrWhiteSpace(financial.AuditorName),
            AuditorName = financial.AuditorName,
            AuditDate = financial.AuditDate
        };

    private static AccountReadinessAssessment AssessAccountReadiness(
        BusinessPartner partner,
        IReadOnlyCollection<BusinessPartnerDocument> documents,
        IReadOnlyCollection<BusinessPartnerLicense> licenses,
        IReadOnlyCollection<BusinessPartnerFinancial> financials,
        DateTime now,
        DateTime readinessWindow)
    {
        var verifiedDocumentCount = documents.Count(x => x.IsVerified);
        var expiredDocumentCount = documents.Count(x => x.ExpiryDate.HasValue && x.ExpiryDate.Value < now);
        var expiringDocumentCount = documents.Count(x =>
            x.ExpiryDate.HasValue
            && x.ExpiryDate.Value >= now
            && x.ExpiryDate.Value <= readinessWindow);
        var expiredLicenseCount = licenses.Count(x =>
            string.Equals(x.Status, "Expired", StringComparison.OrdinalIgnoreCase)
            || x.ExpiryDate.HasValue && x.ExpiryDate.Value < now);
        var expiringLicenseCount = licenses.Count(x =>
            !string.Equals(x.Status, "Expired", StringComparison.OrdinalIgnoreCase)
            && x.ExpiryDate.HasValue
            && x.ExpiryDate.Value >= now
            && x.ExpiryDate.Value <= readinessWindow);
        var latestFinancial = financials
            .OrderByDescending(x => x.FiscalYear)
            .ThenByDescending(x => x.AuditDate ?? DateTime.MinValue)
            .FirstOrDefault();
        var nextComplianceDate = documents
            .Where(x => x.ExpiryDate.HasValue)
            .Select(x => x.ExpiryDate!.Value)
            .Concat(licenses.Where(x => x.ExpiryDate.HasValue).Select(x => x.ExpiryDate!.Value))
            .OrderBy(x => x)
            .FirstOrDefault();

        var score = 50;
        var signals = new List<CrmReadinessSignalDto>();

        if (documents.Count > 0)
        {
            score += 8;
            AddReadinessSignal(signals, "Account documents are on file", "positive", 8);
        }
        else
        {
            score -= 12;
            AddReadinessSignal(signals, "No account documents are on file", "watch", -12);
        }

        if (verifiedDocumentCount > 0)
        {
            score += 8;
            AddReadinessSignal(signals, "Verified documents strengthen readiness", "positive", 8);
        }
        else if (documents.Count > 0)
        {
            score -= 6;
            AddReadinessSignal(signals, "Documents are present but not verified", "watch", -6);
        }

        if (expiredDocumentCount > 0)
        {
            score -= 18;
            AddReadinessSignal(signals, "Expired documents need renewal", "critical", -18);
        }
        else if (expiringDocumentCount > 0)
        {
            score -= 8;
            AddReadinessSignal(signals, "Some documents are approaching expiry", "watch", -8);
        }
        else if (documents.Count > 0)
        {
            score += 5;
            AddReadinessSignal(signals, "Documents are currently in date", "positive", 5);
        }

        if (licenses.Count > 0)
        {
            score += 8;
            AddReadinessSignal(signals, "Licenses or certifications are available", "positive", 8);
        }
        else if (string.Equals(partner.PartnerType, "Vendor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(partner.PartnerType, "Both", StringComparison.OrdinalIgnoreCase))
        {
            score -= 6;
            AddReadinessSignal(signals, "No licenses are on file for this account", "watch", -6);
        }

        if (expiredLicenseCount > 0)
        {
            score -= 18;
            AddReadinessSignal(signals, "Expired licenses need urgent action", "critical", -18);
        }
        else if (expiringLicenseCount > 0)
        {
            score -= 8;
            AddReadinessSignal(signals, "Licenses are nearing expiry", "watch", -8);
        }

        if (financials.Count > 0)
        {
            score += 10;
            AddReadinessSignal(signals, "Financial statements are on file", "positive", 10);
        }
        else
        {
            score -= 15;
            AddReadinessSignal(signals, "No financial statements are on file", "critical", -15);
        }

        if (latestFinancial != null)
        {
            if (latestFinancial.FiscalYear >= now.Year - 1)
            {
                score += 8;
                AddReadinessSignal(signals, $"Financials are current through {latestFinancial.FiscalYear}", "positive", 8);
            }
            else if (latestFinancial.FiscalYear == now.Year - 2)
            {
                score -= 4;
                AddReadinessSignal(signals, $"Financials stop at {latestFinancial.FiscalYear}", "watch", -4);
            }
            else
            {
                score -= 10;
                AddReadinessSignal(signals, $"Financials are stale at {latestFinancial.FiscalYear}", "critical", -10);
            }

            if (!string.IsNullOrWhiteSpace(latestFinancial.CreditRating))
            {
                score += 4;
                AddReadinessSignal(signals, $"Credit rating {latestFinancial.CreditRating}", "positive", 4);
            }

            if (!string.IsNullOrWhiteSpace(latestFinancial.AuditorName))
            {
                score += 4;
                AddReadinessSignal(signals, "Audited financial history is available", "positive", 4);
            }
        }

        score = Math.Clamp(score, 0, 100);
        var category = score switch
        {
            >= 80 => "Ready",
            >= 65 => "Watch",
            >= 45 => "Gap",
            _ => "Critical"
        };

        return new AccountReadinessAssessment
        {
            Score = score,
            Category = category,
            Signals = signals.OrderBy(x => x.Severity).ThenBy(x => x.ScoreImpact).ToList(),
            HasCriticalGap = expiredDocumentCount > 0 || expiredLicenseCount > 0 || financials.Count == 0,
            DocumentCount = documents.Count,
            VerifiedDocumentCount = verifiedDocumentCount,
            ExpiringDocumentCount = expiringDocumentCount,
            ExpiredDocumentCount = expiredDocumentCount,
            LicenseCount = licenses.Count,
            ExpiringLicenseCount = expiringLicenseCount,
            ExpiredLicenseCount = expiredLicenseCount,
            FinancialRecordCount = financials.Count,
            LatestFinancialYear = latestFinancial?.FiscalYear,
            LatestAnnualRevenue = latestFinancial?.AnnualRevenue,
            CreditRating = latestFinancial?.CreditRating,
            NextComplianceDate = nextComplianceDate == default ? null : nextComplianceDate
        };
    }

    private static void AddReadinessSignal(List<CrmReadinessSignalDto> signals, string label, string severity, int scoreImpact)
    {
        if (signals.Any(x => x.Label == label))
        {
            return;
        }

        signals.Add(new CrmReadinessSignalDto
        {
            Label = label,
            Severity = severity,
            ScoreImpact = scoreImpact
        });
    }

    private static CrmRiskListItemDto BuildRiskListItem(
        BusinessPartner partner,
        CrmAccountOverviewDto account,
        AccountRiskAssessment risk)
        => new()
        {
            BusinessPartnerId = partner.Id,
            PartnerCode = partner.PartnerCode,
            PartnerName = partner.PartnerName,
            PartnerType = partner.PartnerType,
            RegistrationStatus = partner.RegistrationStatus,
            CustomerType = partner.CustomerType,
            SalesTerritory = partner.SalesTerritory,
            RiskLevel = partner.RiskLevel,
            PerformanceRating = partner.PerformanceRating,
            IsBlacklisted = partner.IsBlacklisted,
            IsOnCreditHold = partner.IsOnCreditHold,
            OpenIncidentCount = risk.OpenIncidentCount,
            CriticalIncidentCount = risk.CriticalIncidentCount,
            PendingAppealCount = risk.PendingAppealCount,
            OpenReviewFollowUpCount = risk.OpenReviewFollowUpCount,
            LatestMetricScore = risk.LatestMetricScore,
            LatestMetricGrade = risk.LatestMetricGrade,
            LatestMetricPeriod = risk.LatestMetricPeriod,
            LatestMetricCalculatedAt = risk.LatestMetricCalculatedAt,
            LatestReviewDate = risk.LatestReviewDate,
            OpenOpportunityCount = account.OpenOpportunityCount,
            ActiveProjectCount = account.ActiveProjectCount,
            ActiveContractCount = account.ActiveContractCount,
            IsAtRisk = account.IsAtRisk,
            HealthScore = account.HealthScore,
            HealthCategory = account.HealthCategory,
            RiskScore = risk.Score,
            RiskCategory = risk.Category,
            RequiresEscalation = risk.RequiresEscalation,
            NextMilestoneDate = account.NextMilestoneDate
        };

    private static CrmRiskPerformanceMetricDto MapRiskPerformanceMetric(SupplierPerformanceMetric metric)
        => new()
        {
            MetricId = metric.Id,
            MetricPeriod = BuildMetricPeriodLabel(metric),
            Year = metric.Year,
            Month = metric.Month,
            Quarter = metric.Quarter,
            OverallPerformanceScore = metric.OverallPerformanceScore,
            PerformanceGrade = metric.PerformanceGrade,
            OnTimeDeliveryRate = metric.OnTimeDeliveryRate,
            QualityAcceptanceRate = metric.QualityAcceptanceRate,
            ComplianceScore = metric.ComplianceScore,
            ComplaintsReceived = metric.ComplaintsReceived,
            ComplaintsResolved = metric.ComplaintsResolved,
            ContractViolations = metric.ContractViolations,
            CalculatedAt = metric.CalculatedAt
        };

    private static CrmRiskIncidentDto MapRiskIncident(QualityIncident incident)
        => new()
        {
            IncidentId = incident.Id,
            IncidentNumber = incident.IncidentNumber,
            IncidentDate = incident.IncidentDate,
            IncidentType = incident.IncidentType,
            Severity = incident.Severity,
            Status = incident.Status,
            Description = incident.Description,
            FinancialImpact = incident.FinancialImpact,
            RequiresSupplierResponse = incident.RequiresSupplierResponse,
            SupplierResponseDate = incident.SupplierResponseDate,
            ResolvedDate = incident.ResolvedDate
        };

    private static CrmRiskReviewDto MapRiskReview(PerformanceReview review)
        => new()
        {
            ReviewId = review.Id,
            ReviewNumber = review.ReviewNumber,
            ReviewDate = review.ReviewDate,
            ReviewPeriod = review.ReviewPeriod,
            OverallScore = review.OverallScore,
            OverallGrade = review.OverallGrade,
            Status = review.Status,
            RequiresFollowUp = review.RequiresFollowUp,
            FollowUpDate = review.FollowUpDate
        };

    private static CrmRiskAppealDto MapRiskAppeal(BlacklistAppeal appeal)
        => new()
        {
            AppealId = appeal.Id,
            AppealNumber = appeal.AppealNumber,
            AppealDate = appeal.AppealDate,
            Status = appeal.Status,
            RemoveBlacklist = appeal.RemoveBlacklist,
            ReviewedDate = appeal.ReviewedDate,
            ApprovedDate = appeal.ApprovedDate,
            NewBlacklistExpiryDate = appeal.NewBlacklistExpiryDate
        };

    private static AccountRiskAssessment AssessAccountRisk(
        BusinessPartner partner,
        CrmAccountOverviewDto account,
        IReadOnlyCollection<SupplierPerformanceMetric> metrics,
        IReadOnlyCollection<QualityIncident> incidents,
        IReadOnlyCollection<PerformanceReview> reviews,
        IReadOnlyCollection<BlacklistAppeal> appeals,
        DateTime now)
    {
        var openIncidents = incidents.Where(IsOpenQualityIncident).ToList();
        var criticalOpenIncidentCount = openIncidents.Count(IsCriticalQualityIncident);
        var pendingAppealCount = appeals.Count(IsPendingBlacklistAppeal);
        var openReviewFollowUpCount = reviews.Count(x =>
            x.RequiresFollowUp
            && !string.Equals(x.Status, "Finalized", StringComparison.OrdinalIgnoreCase));
        var latestMetric = metrics
            .OrderByDescending(x => x.CalculatedAt)
            .ThenByDescending(x => x.Year)
            .ThenByDescending(x => x.Quarter ?? 0)
            .ThenByDescending(x => x.Month ?? 0)
            .FirstOrDefault();
        var latestReview = reviews
            .OrderByDescending(x => x.ReviewDate)
            .FirstOrDefault();
        var score = 20;
        var signals = new List<CrmRiskSignalDto>();

        switch ((partner.RiskLevel ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "medium":
                score += 12;
                AddRiskSignal(signals, "Partner carries a medium master risk rating", "watch", 12);
                break;
            case "high":
                score += 24;
                AddRiskSignal(signals, "Partner carries a high master risk rating", "critical", 24);
                break;
            case "critical":
                score += 36;
                AddRiskSignal(signals, "Partner carries a critical master risk rating", "critical", 36);
                break;
            case "low":
                score -= 6;
                AddRiskSignal(signals, "Master risk rating is low", "positive", -6);
                break;
        }

        if (partner.IsBlacklisted)
        {
            score += 35;
            AddRiskSignal(signals, "Account is currently blacklisted", "critical", 35);
        }

        if (partner.IsOnCreditHold)
        {
            score += 22;
            AddRiskSignal(signals, "Credit hold is active on the account", "critical", 22);
        }

        if (partner.PerformanceRating.HasValue)
        {
            var performanceImpact = partner.PerformanceRating.Value switch
            {
                >= 4m => -8,
                >= 3m => -3,
                _ => 12
            };

            score += performanceImpact;
            AddRiskSignal(signals, $"BusinessPartner performance rating {partner.PerformanceRating.Value:0.0}", performanceImpact > 0 ? "watch" : "positive", performanceImpact);
        }

        if (account.IsAtRisk)
        {
            score += 8;
            AddRiskSignal(signals, "CRM account health is already in a watch or risk band", "watch", 8);
        }
        else if (account.HealthScore >= 80)
        {
            score -= 4;
            AddRiskSignal(signals, "CRM account health is currently strong", "positive", -4);
        }

        if (account.NextMilestoneDate.HasValue && account.NextMilestoneDate.Value <= now.AddDays(30))
        {
            score += 6;
            AddRiskSignal(signals, "A delivery or contract milestone is approaching within 30 days", "watch", 6);
        }

        if (openIncidents.Count > 0)
        {
            var impact = Math.Min(18, openIncidents.Count * 5);
            score += impact;
            AddRiskSignal(signals, $"{openIncidents.Count} quality incidents remain open", "watch", impact);
        }
        else if (incidents.Count > 0)
        {
            score -= 4;
            AddRiskSignal(signals, "Recent quality incidents are closed", "positive", -4);
        }

        if (criticalOpenIncidentCount > 0)
        {
            var impact = Math.Min(20, criticalOpenIncidentCount * 8);
            score += impact;
            AddRiskSignal(signals, $"{criticalOpenIncidentCount} high-severity incidents need escalation", "critical", impact);
        }

        if (openIncidents.Any(x => x.IncidentDate <= now.AddDays(-30)))
        {
            score += 10;
            AddRiskSignal(signals, "Some open incidents have aged beyond 30 days", "critical", 10);
        }

        if (pendingAppealCount > 0)
        {
            var impact = Math.Min(18, pendingAppealCount * 9);
            score += impact;
            AddRiskSignal(signals, $"{pendingAppealCount} blacklist appeals are still pending", "critical", impact);
        }

        if (latestMetric == null)
        {
            score += 8;
            AddRiskSignal(signals, "No formal partner performance metric is on file", "watch", 8);
        }
        else
        {
            if (latestMetric.OverallPerformanceScore >= 85m)
            {
                score -= 12;
                AddRiskSignal(signals, $"Latest performance metric is strong at {latestMetric.OverallPerformanceScore:0.#}", "positive", -12);
            }
            else if (latestMetric.OverallPerformanceScore >= 75m)
            {
                score -= 6;
                AddRiskSignal(signals, $"Latest performance metric is healthy at {latestMetric.OverallPerformanceScore:0.#}", "positive", -6);
            }
            else if (latestMetric.OverallPerformanceScore < 60m)
            {
                score += 15;
                AddRiskSignal(signals, $"Latest performance metric dropped to {latestMetric.OverallPerformanceScore:0.#}", "critical", 15);
            }
            else
            {
                score += 6;
                AddRiskSignal(signals, $"Latest performance metric is watch-level at {latestMetric.OverallPerformanceScore:0.#}", "watch", 6);
            }

            if (latestMetric.ComplianceScore < 70m)
            {
                score += 10;
                AddRiskSignal(signals, "Compliance score is below 70%", "critical", 10);
            }
            else if (latestMetric.ComplianceScore >= 90m)
            {
                score -= 5;
                AddRiskSignal(signals, "Compliance score is currently strong", "positive", -5);
            }

            if (latestMetric.ContractViolations > 0)
            {
                var impact = Math.Min(12, latestMetric.ContractViolations * 4);
                score += impact;
                AddRiskSignal(signals, $"{latestMetric.ContractViolations} contract violations were recorded", "critical", impact);
            }

            if (latestMetric.ComplaintsReceived > latestMetric.ComplaintsResolved)
            {
                score += 8;
                AddRiskSignal(signals, "Open complaints exceed resolved complaints in the latest metric", "watch", 8);
            }

            if (latestMetric.CalculatedAt <= now.AddMonths(-9))
            {
                score += 6;
                AddRiskSignal(signals, "The latest partner metric snapshot is stale", "watch", 6);
            }
        }

        if (latestReview == null)
        {
            score += 4;
            AddRiskSignal(signals, "No formal performance review is on file", "watch", 4);
        }
        else
        {
            if (latestReview.OverallScore >= 4m)
            {
                score -= 8;
                AddRiskSignal(signals, $"Latest review score is strong at {latestReview.OverallScore:0.0}/5", "positive", -8);
            }
            else if (latestReview.OverallScore < 3m)
            {
                score += 10;
                AddRiskSignal(signals, $"Latest review score is weak at {latestReview.OverallScore:0.0}/5", "critical", 10);
            }

            if (string.Equals(latestReview.Status, "Disputed", StringComparison.OrdinalIgnoreCase))
            {
                score += 6;
                AddRiskSignal(signals, "Latest performance review is disputed", "watch", 6);
            }
        }

        if (openReviewFollowUpCount > 0)
        {
            var impact = Math.Min(10, openReviewFollowUpCount * 5);
            score += impact;
            AddRiskSignal(signals, $"{openReviewFollowUpCount} review follow-ups are still open", "watch", impact);
        }

        score = Math.Clamp(score, 0, 100);
        var category = score switch
        {
            >= 75 => "Critical",
            >= 55 => "Elevated",
            >= 30 => "Guarded",
            _ => "Low"
        };

        return new AccountRiskAssessment
        {
            Score = score,
            Category = category,
            Signals = signals
                .OrderByDescending(x => x.ScoreImpact)
                .ThenBy(x => x.Label)
                .ToList(),
            OpenIncidentCount = openIncidents.Count,
            CriticalIncidentCount = criticalOpenIncidentCount,
            PendingAppealCount = pendingAppealCount,
            OpenReviewFollowUpCount = openReviewFollowUpCount,
            LatestMetricScore = latestMetric?.OverallPerformanceScore,
            LatestMetricGrade = latestMetric?.PerformanceGrade,
            LatestMetricPeriod = latestMetric == null ? null : BuildMetricPeriodLabel(latestMetric),
            LatestMetricCalculatedAt = latestMetric?.CalculatedAt,
            LatestReviewDate = latestReview?.ReviewDate,
            RequiresEscalation = score >= 60
                || partner.IsBlacklisted
                || partner.IsOnCreditHold
                || criticalOpenIncidentCount > 0
                || pendingAppealCount > 0
        };
    }

    private static void AddRiskSignal(List<CrmRiskSignalDto> signals, string label, string severity, int scoreImpact)
    {
        if (signals.Any(x => x.Label == label))
        {
            return;
        }

        signals.Add(new CrmRiskSignalDto
        {
            Label = label,
            Severity = severity,
            ScoreImpact = scoreImpact
        });
    }

    private static bool IsOpenQualityIncident(QualityIncident incident)
        => !string.Equals(incident.Status, "Resolved", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(incident.Status, "Closed", StringComparison.OrdinalIgnoreCase);

    private static bool IsCriticalQualityIncident(QualityIncident incident)
        => string.Equals(incident.Severity, "Critical", StringComparison.OrdinalIgnoreCase)
            || string.Equals(incident.Severity, "High", StringComparison.OrdinalIgnoreCase);

    private static bool IsPendingBlacklistAppeal(BlacklistAppeal appeal)
        => string.Equals(appeal.Status, "Pending", StringComparison.OrdinalIgnoreCase)
            || string.Equals(appeal.Status, "UnderReview", StringComparison.OrdinalIgnoreCase);

    private static string BuildMetricPeriodLabel(SupplierPerformanceMetric metric)
        => metric.MetricPeriod.Trim().ToLowerInvariant() switch
        {
            "monthly" when metric.Month.HasValue => $"Monthly {metric.Year}-{metric.Month.Value:00}",
            "quarterly" when metric.Quarter.HasValue => $"Q{metric.Quarter.Value} {metric.Year}",
            "yearly" => $"Yearly {metric.Year}",
            _ => $"{metric.MetricPeriod} {metric.Year}".Trim()
        };

    private static CrmCollaborationListItemDto BuildCollaborationListItem(
        BusinessPartner partner,
        CrmAccountOverviewDto account,
        AccountCollaborationAssessment collaboration)
        => new()
        {
            BusinessPartnerId = partner.Id,
            PartnerCode = partner.PartnerCode,
            PartnerName = partner.PartnerName,
            PartnerType = partner.PartnerType,
            RegistrationStatus = partner.RegistrationStatus,
            CustomerType = partner.CustomerType,
            SalesTerritory = partner.SalesTerritory,
            LatestApplicationNumber = collaboration.LatestApplicationNumber,
            LatestRegistrationLifecycleStatus = collaboration.LatestRegistrationLifecycleStatus,
            LatestRegistrationSubmittedDate = collaboration.LatestRegistrationSubmittedDate,
            LatestRegistrationApprovedDate = collaboration.LatestRegistrationApprovedDate,
            RegistrationDocumentCount = collaboration.RegistrationDocumentCount,
            PortalUserCount = collaboration.PortalUserCount,
            ActivePortalUserCount = collaboration.ActivePortalUserCount,
            AdminUserCount = collaboration.AdminUserCount,
            TenderAssignmentCount = collaboration.TenderAssignmentCount,
            AssignedTenderCount = collaboration.AssignedTenderCount,
            PortalProjectCount = collaboration.PortalProjectCount,
            CollaborationProjectCount = collaboration.CollaborationProjectCount,
            OpenOpportunityCount = account.OpenOpportunityCount,
            ActiveProjectCount = account.ActiveProjectCount,
            ActiveContractCount = account.ActiveContractCount,
            IsAtRisk = account.IsAtRisk,
            HealthScore = account.HealthScore,
            HealthCategory = account.HealthCategory,
            CollaborationScore = collaboration.Score,
            CollaborationCategory = collaboration.Category,
            RequiresEnablement = collaboration.RequiresEnablement,
            NextMilestoneDate = account.NextMilestoneDate
        };

    private static CrmCollaborationRegistrationDto MapCollaborationRegistration(BusinessPartnerRegistration registration)
        => new()
        {
            RegistrationId = registration.Id,
            ApplicationNumber = registration.RegistrationNumber,
            Status = registration.Status,
            CreatedAt = registration.CreatedAt,
            SubmittedDate = registration.SubmittedDate,
            ReviewedDate = registration.ReviewedDate,
            ApprovedDate = registration.ApprovedDate,
            DocumentCount = registration.Documents.Count,
            VerifiedDocumentCount = registration.Documents.Count(x => x.IsVerified),
            RejectedDocumentCount = registration.Documents.Count(x => x.IsRejected),
            LastStatusChangeDate = registration.StatusHistory
                .OrderByDescending(x => x.ChangedAt)
                .Select(x => (DateTime?)x.ChangedAt)
                .FirstOrDefault()
        };

    private static CrmCollaborationPortalUserDto MapCollaborationPortalUser(BusinessPartnerUser portalUser)
    {
        var fullName = portalUser.User == null
            ? portalUser.UserId.ToString()
            : string.IsNullOrWhiteSpace(portalUser.User.FullName)
                ? portalUser.User.UserName ?? portalUser.User.Email ?? portalUser.UserId.ToString()
                : portalUser.User.FullName;

        return new CrmCollaborationPortalUserDto
        {
            PortalUserId = portalUser.Id,
            UserId = portalUser.UserId,
            UserName = portalUser.User?.UserName ?? string.Empty,
            FullName = fullName,
            Email = portalUser.User?.Email,
            Role = portalUser.Role,
            IsActive = portalUser.IsActive,
            GrantedAt = portalUser.GrantedAt,
            Notes = portalUser.Notes
        };
    }

    private static CrmCollaborationTenderAssignmentDto MapCollaborationTenderAssignment(TenderAssignment assignment)
        => new()
        {
            AssignmentId = assignment.Id,
            TenderId = assignment.TenderId,
            TenderNumber = assignment.Tender?.TenderNumber ?? string.Empty,
            TenderTitle = assignment.Tender?.Title ?? string.Empty,
            AssignmentType = assignment.AssignmentType,
            AssignedToUserName = assignment.AssignedToUser?.FullName,
            AssignedAt = assignment.AssignedAt,
            Notes = assignment.Notes
        };

    private static CrmCollaborationProjectDto MapCollaborationProject(Project project)
        => new()
        {
            ProjectId = project.Id,
            ProjectCode = project.ProjectCode,
            Title = project.Title,
            Status = project.Status,
            ExternalPortalAccessEnabled = project.ExternalPortalAccessEnabled,
            ExternalCollaborationEnabled = project.ExternalCollaborationEnabled,
            TargetEndDate = project.TargetEndDate
        };

    private static AccountCollaborationAssessment AssessAccountCollaboration(
        BusinessPartner partner,
        CrmAccountOverviewDto account,
        IReadOnlyCollection<BusinessPartnerRegistration> registrations,
        IReadOnlyCollection<BusinessPartnerUser> portalUsers,
        IReadOnlyCollection<TenderAssignment> tenderAssignments,
        IReadOnlyCollection<Project> projects,
        DateTime now)
    {
        var latestRegistration = registrations
            .OrderByDescending(x => x.SubmittedDate ?? x.CreatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        var portalUserCount = portalUsers.Count;
        var activePortalUserCount = portalUsers.Count(x => x.IsActive);
        var adminUserCount = portalUsers.Count(x => x.IsActive && string.Equals(x.Role, "Admin", StringComparison.OrdinalIgnoreCase));
        var assignedTenderCount = tenderAssignments
            .Select(x => x.TenderId)
            .Distinct()
            .Count();
        var directedTenderAssignments = tenderAssignments.Count(x => x.AssignedToUserId.HasValue);
        var portalProjectCount = projects.Count(x => x.ExternalPortalAccessEnabled);
        var collaborationProjectCount = projects.Count(x => x.ExternalCollaborationEnabled);
        var score = 40;
        var signals = new List<CrmCollaborationSignalDto>();

        if (latestRegistration != null)
        {
            switch ((latestRegistration.Status ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "approved":
                    score += 12;
                    AddCollaborationSignal(signals, "External onboarding has been approved", "positive", 12);
                    break;
                case "submitted":
                case "underreview":
                    score -= 6;
                    AddCollaborationSignal(signals, "External onboarding is still under review", "watch", -6);
                    break;
                case "draft":
                    score -= 10;
                    AddCollaborationSignal(signals, "External onboarding is still in draft state", "watch", -10);
                    break;
                case "rejected":
                    score -= 18;
                    AddCollaborationSignal(signals, "External onboarding was rejected and needs rework", "critical", -18);
                    break;
            }

            if (latestRegistration.Documents.Any())
            {
                score += 6;
                AddCollaborationSignal(signals, "Registration documents are on file", "positive", 6);
            }

            if (latestRegistration.Documents.Any(x => x.IsRejected))
            {
                score -= 8;
                AddCollaborationSignal(signals, "Some onboarding documents were rejected", "critical", -8);
            }
        }

        if (portalUserCount > 0)
        {
            score += 12;
            AddCollaborationSignal(signals, "Portal users are linked to the account", "positive", 12);
        }
        else
        {
            var noUserImpact = account.ActiveContractCount > 0 || account.OpenOpportunityCount > 0 || portalProjectCount > 0 || collaborationProjectCount > 0
                ? -12
                : -4;
            score += noUserImpact;
            AddCollaborationSignal(signals, "No portal users are linked to this account", noUserImpact <= -10 ? "critical" : "watch", noUserImpact);
        }

        if (activePortalUserCount > 0)
        {
            score += 8;
            AddCollaborationSignal(signals, "Active portal users can currently collaborate", "positive", 8);
        }
        else if (portalUserCount > 0)
        {
            score -= 10;
            AddCollaborationSignal(signals, "Portal users exist but none are active", "critical", -10);
        }

        if (adminUserCount > 0)
        {
            score += 6;
            AddCollaborationSignal(signals, "An active admin user can manage external access", "positive", 6);
        }
        else if (activePortalUserCount > 0)
        {
            score -= 6;
            AddCollaborationSignal(signals, "Active portal access exists without an admin user", "watch", -6);
        }

        if (assignedTenderCount > 0)
        {
            score += 8;
            AddCollaborationSignal(signals, $"{assignedTenderCount} tenders are actively assigned to the account", "positive", 8);
        }

        if (directedTenderAssignments > 0)
        {
            score += 4;
            AddCollaborationSignal(signals, "Tender work is routed to named external users", "positive", 4);
        }
        else if (tenderAssignments.Count > 0)
        {
            score -= 4;
            AddCollaborationSignal(signals, "Tender assignments exist but are not directed to specific users", "watch", -4);
        }

        if (portalProjectCount > 0)
        {
            score += 8;
            AddCollaborationSignal(signals, "Projects expose the external portal to this account", "positive", 8);
        }

        if (collaborationProjectCount > 0)
        {
            score += 8;
            AddCollaborationSignal(signals, "Projects have external collaboration enabled", "positive", 8);
        }

        if ((portalProjectCount > 0 || collaborationProjectCount > 0) && activePortalUserCount == 0)
        {
            score -= 16;
            AddCollaborationSignal(signals, "External project collaboration is enabled without active portal users", "critical", -16);
        }

        if (account.ActiveContractCount > 0 && activePortalUserCount > 0)
        {
            score += 6;
            AddCollaborationSignal(signals, "Active contracts are backed by live external access", "positive", 6);
        }
        else if (account.ActiveContractCount > 0 && activePortalUserCount == 0)
        {
            score -= 10;
            AddCollaborationSignal(signals, "Active contracts exist without active portal access", "critical", -10);
        }

        if (account.OpenOpportunityCount > 0 && NeedsCollaborationOnboardingAttention(latestRegistration?.Status))
        {
            score -= 8;
            AddCollaborationSignal(signals, "Commercial pursuit is active while onboarding is incomplete", "watch", -8);
        }

        if (account.IsAtRisk)
        {
            score -= 4;
            AddCollaborationSignal(signals, "Account health is already in a watch or risk band", "watch", -4);
        }

        score = Math.Clamp(score, 0, 100);
        var category = score switch
        {
            >= 80 => "Connected",
            >= 65 => "Partial",
            >= 50 => "Watch",
            _ => "Blocked"
        };

        return new AccountCollaborationAssessment
        {
            Score = score,
            Category = category,
            Signals = signals
                .OrderBy(x => x.ScoreImpact >= 0 ? 1 : 0)
                .ThenBy(x => x.ScoreImpact)
                .ThenBy(x => x.Label)
                .ToList(),
            LatestApplicationNumber = latestRegistration?.RegistrationNumber,
            LatestRegistrationLifecycleStatus = latestRegistration?.Status,
            LatestRegistrationSubmittedDate = latestRegistration?.SubmittedDate,
            LatestRegistrationApprovedDate = latestRegistration?.ApprovedDate,
            RegistrationDocumentCount = latestRegistration?.Documents.Count ?? 0,
            PortalUserCount = portalUserCount,
            ActivePortalUserCount = activePortalUserCount,
            AdminUserCount = adminUserCount,
            TenderAssignmentCount = tenderAssignments.Count,
            AssignedTenderCount = assignedTenderCount,
            PortalProjectCount = portalProjectCount,
            CollaborationProjectCount = collaborationProjectCount,
            RequiresEnablement = (activePortalUserCount == 0 && (portalProjectCount > 0 || collaborationProjectCount > 0 || account.ActiveContractCount > 0 || account.OpenOpportunityCount > 0))
                || (adminUserCount == 0 && activePortalUserCount > 0 && account.ActiveContractCount > 0)
                || NeedsCollaborationOnboardingAttention(latestRegistration?.Status)
        };
    }

    private static void AddCollaborationSignal(List<CrmCollaborationSignalDto> signals, string label, string severity, int scoreImpact)
    {
        if (signals.Any(x => x.Label == label))
        {
            return;
        }

        signals.Add(new CrmCollaborationSignalDto
        {
            Label = label,
            Severity = severity,
            ScoreImpact = scoreImpact
        });
    }

    private static bool NeedsCollaborationOnboardingAttention(string? status)
        => string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Submitted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "UnderReview", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase);

    private static CrmServiceListItemDto BuildServiceListItem(
        BusinessPartner partner,
        CrmAccountOverviewDto account,
        AccountServiceAssessment service)
        => new()
        {
            BusinessPartnerId = partner.Id,
            PartnerCode = partner.PartnerCode,
            PartnerName = partner.PartnerName,
            PartnerType = partner.PartnerType,
            RegistrationStatus = partner.RegistrationStatus,
            CustomerType = partner.CustomerType,
            SalesTerritory = partner.SalesTerritory,
            PortalUserCount = service.PortalUserCount,
            ActivePortalUserCount = service.ActivePortalUserCount,
            TicketCount = service.TicketCount,
            OpenTicketCount = service.OpenTicketCount,
            OverdueTicketCount = service.OverdueTicketCount,
            ComplaintTicketCount = service.ComplaintTicketCount,
            HelpdeskTicketCount = service.HelpdeskTicketCount,
            EnquiryTicketCount = service.EnquiryTicketCount,
            LinkedProblemCount = service.LinkedProblemCount,
            OpenProblemCount = service.OpenProblemCount,
            ResolvedTicketCount30Days = service.ResolvedTicketCount30Days,
            AverageFeedbackRating = service.AverageFeedbackRating,
            FeedbackResponseCount = service.FeedbackResponseCount,
            OpenOpportunityCount = account.OpenOpportunityCount,
            ActiveContractCount = account.ActiveContractCount,
            IsAtRisk = account.IsAtRisk,
            HealthScore = account.HealthScore,
            HealthCategory = account.HealthCategory,
            ServiceScore = service.Score,
            ServiceCategory = service.Category,
            RequiresAttention = service.RequiresAttention,
            HasSlaBreachRisk = service.HasSlaBreachRisk,
            LastTicketCreatedAt = service.LastTicketCreatedAt,
            LastResolvedAt = service.LastResolvedAt,
            NextMilestoneDate = account.NextMilestoneDate
        };

    private static CrmServiceTicketDto MapServiceTicket(EhcTicket ticket, DateTime now)
        => new()
        {
            TicketId = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            TicketType = ticket.TicketType.ToString(),
            Priority = ticket.Priority.ToString(),
            Source = ticket.Source.ToString(),
            Status = ticket.Status.ToString(),
            Subject = ticket.Subject,
            CategoryName = ticket.Category?.Name,
            RequesterName = ticket.RequesterUser?.FullName,
            AssignedToName = ticket.AssignedToUser?.FullName,
            CreatedAt = ticket.CreatedAt,
            FirstResponseDueAt = ticket.FirstResponseDueAt,
            ResolutionDueAt = ticket.ResolutionDueAt,
            ResolvedAt = ticket.ResolvedAt ?? ticket.ClosedAt,
            FeedbackRating = ticket.Feedbacks
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => (int?)x.Rating)
                .FirstOrDefault(),
            IsOpen = IsOpenTicketStatus(ticket.Status),
            IsOverdue = IsTicketOverdue(ticket, now),
            IsComplaint = ticket.TicketType == EhcTicketType.Complaint
        };

    private static CrmServiceProblemDto MapServiceProblem(EhcProblem problem, int linkedTicketCount)
        => new()
        {
            ProblemId = problem.Id,
            ProblemNumber = problem.ProblemNumber,
            Title = problem.Title,
            Status = problem.Status.ToString(),
            Priority = problem.Priority.ToString(),
            OwnerName = problem.OwnerUser?.FullName,
            CreatedFromTicketId = problem.CreatedFromTicketId,
            LinkedTicketCount = linkedTicketCount,
            CreatedAt = problem.CreatedAt
        };

    private static AccountServiceAssessment AssessAccountService(
        CrmAccountOverviewDto account,
        IReadOnlyCollection<BusinessPartnerUser> portalUsers,
        IReadOnlyCollection<EhcTicket> tickets,
        IReadOnlyCollection<EhcProblem> problems,
        DateTime now)
    {
        var portalUserCount = portalUsers.Count;
        var activePortalUserCount = portalUsers.Count(x => x.IsActive);
        var openTicketCount = tickets.Count(x => IsOpenTicketStatus(x.Status));
        var overdueTicketCount = tickets.Count(x => IsTicketOverdue(x, now));
        var complaintTicketCount = tickets.Count(x => x.TicketType == EhcTicketType.Complaint);
        var helpdeskTicketCount = tickets.Count(x => x.TicketType == EhcTicketType.Helpdesk);
        var enquiryTicketCount = tickets.Count(x => x.TicketType == EhcTicketType.Enquiry);
        var linkedProblemCount = problems.Count;
        var openProblemCount = problems.Count(x => IsOpenProblemStatus(x.Status));
        var resolvedTicketCount30Days = tickets.Count(x =>
        {
            var resolvedAt = x.ResolvedAt ?? x.ClosedAt;
            return resolvedAt.HasValue && resolvedAt.Value >= now.AddDays(-30);
        });
        var feedbackRatings = tickets
            .SelectMany(x => x.Feedbacks)
            .Select(x => x.Rating)
            .Where(x => x >= 1 && x <= 5)
            .ToList();
        decimal? averageFeedbackRating = feedbackRatings.Count > 0
            ? decimal.Round((decimal)feedbackRatings.Average(), 2)
            : null;
        var lastTicketCreatedAt = tickets
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefault();
        var lastResolvedAt = tickets
            .Select(x => x.ResolvedAt ?? x.ClosedAt)
            .Where(x => x.HasValue)
            .OrderByDescending(x => x)
            .FirstOrDefault();

        var score = 78;
        var signals = new List<CrmServiceSignalDto>();

        if (portalUserCount > 0)
        {
            score += 4;
            AddServiceSignal(signals, "Portal users are available for customer support interactions", "positive", 4);
        }
        else if (account.ActiveContractCount > 0)
        {
            score -= 8;
            AddServiceSignal(signals, "Active contracts exist without linked external support users", "watch", -8);
        }

        if (openTicketCount > 0)
        {
            var openImpact = -Math.Min(18, openTicketCount * 4);
            score += openImpact;
            AddServiceSignal(signals, $"{openTicketCount} service tickets are currently open", openTicketCount >= 3 ? "critical" : "watch", openImpact);
        }
        else if (tickets.Count > 0)
        {
            score += 6;
            AddServiceSignal(signals, "The recent support workload is fully resolved", "positive", 6);
        }

        if (overdueTicketCount > 0)
        {
            var overdueImpact = -Math.Min(24, overdueTicketCount * 12);
            score += overdueImpact;
            AddServiceSignal(signals, $"{overdueTicketCount} tickets are beyond their SLA window", "critical", overdueImpact);
        }

        if (complaintTicketCount > 0)
        {
            var complaintImpact = -Math.Min(12, complaintTicketCount * 3);
            score += complaintImpact;
            AddServiceSignal(signals, $"{complaintTicketCount} complaint tickets were raised by this account", complaintTicketCount >= 2 ? "critical" : "watch", complaintImpact);
        }

        if (openProblemCount > 0)
        {
            var problemImpact = -Math.Min(20, openProblemCount * 10);
            score += problemImpact;
            AddServiceSignal(signals, $"{openProblemCount} linked service problems remain open", "critical", problemImpact);
        }
        else if (linkedProblemCount > 0)
        {
            score += 4;
            AddServiceSignal(signals, "Linked service problems have been stabilized or closed", "positive", 4);
        }

        if (resolvedTicketCount30Days > 0)
        {
            var resolvedImpact = Math.Min(10, resolvedTicketCount30Days * 3);
            score += resolvedImpact;
            AddServiceSignal(signals, $"{resolvedTicketCount30Days} tickets were resolved in the last 30 days", "positive", resolvedImpact);
        }

        if (averageFeedbackRating.HasValue)
        {
            if (averageFeedbackRating.Value >= 4.5m)
            {
                score += 10;
                AddServiceSignal(signals, "Customer feedback is strongly positive", "positive", 10);
            }
            else if (averageFeedbackRating.Value >= 4.0m)
            {
                score += 6;
                AddServiceSignal(signals, "Customer feedback is trending positive", "positive", 6);
            }
            else if (averageFeedbackRating.Value < 3.0m)
            {
                score -= 12;
                AddServiceSignal(signals, "Customer feedback is low and needs intervention", "critical", -12);
            }
            else if (averageFeedbackRating.Value < 3.5m)
            {
                score -= 8;
                AddServiceSignal(signals, "Customer feedback is softening", "watch", -8);
            }
        }

        if (account.IsAtRisk)
        {
            score -= 4;
            AddServiceSignal(signals, "Account health is already in a watch or risk band", "watch", -4);
        }

        if (account.ActiveContractCount > 0 && openTicketCount == 0 && openProblemCount == 0 && activePortalUserCount > 0)
        {
            score += 6;
            AddServiceSignal(signals, "Active contracts are running without open service escalations", "positive", 6);
        }

        score = Math.Clamp(score, 0, 100);
        var category = score switch
        {
            >= 80 => "Healthy",
            >= 65 => "Monitor",
            >= 50 => "Escalate",
            _ => "Critical"
        };

        return new AccountServiceAssessment
        {
            Score = score,
            Category = category,
            Signals = signals
                .OrderBy(x => x.ScoreImpact >= 0 ? 1 : 0)
                .ThenBy(x => x.ScoreImpact)
                .ThenBy(x => x.Label)
                .ToList(),
            PortalUserCount = portalUserCount,
            ActivePortalUserCount = activePortalUserCount,
            TicketCount = tickets.Count,
            OpenTicketCount = openTicketCount,
            OverdueTicketCount = overdueTicketCount,
            ComplaintTicketCount = complaintTicketCount,
            HelpdeskTicketCount = helpdeskTicketCount,
            EnquiryTicketCount = enquiryTicketCount,
            LinkedProblemCount = linkedProblemCount,
            OpenProblemCount = openProblemCount,
            ResolvedTicketCount30Days = resolvedTicketCount30Days,
            AverageFeedbackRating = averageFeedbackRating,
            FeedbackResponseCount = feedbackRatings.Count,
            RequiresAttention = overdueTicketCount > 0
                || openProblemCount > 0
                || openTicketCount >= 3
                || complaintTicketCount >= 2
                || (averageFeedbackRating.HasValue && averageFeedbackRating.Value < 3.5m),
            HasSlaBreachRisk = overdueTicketCount > 0,
            LastTicketCreatedAt = lastTicketCreatedAt,
            LastResolvedAt = lastResolvedAt
        };
    }

    private static void AddServiceSignal(List<CrmServiceSignalDto> signals, string label, string severity, int scoreImpact)
    {
        if (signals.Any(x => x.Label == label))
        {
            return;
        }

        signals.Add(new CrmServiceSignalDto
        {
            Label = label,
            Severity = severity,
            ScoreImpact = scoreImpact
        });
    }

    private static bool IsOpenTicketStatus(EhcTicketStatus status)
        => status is not (EhcTicketStatus.Resolved or EhcTicketStatus.Closed);

    private static bool IsOpenProblemStatus(EhcProblemStatus status)
        => status is not (EhcProblemStatus.Resolved or EhcProblemStatus.Closed);

    private static bool HasFirstResponseBreach(EhcTicket ticket, DateTime now)
        => IsOpenTicketStatus(ticket.Status)
            && !ticket.FirstRespondedAt.HasValue
            && ticket.FirstResponseDueAt.HasValue
            && ticket.FirstResponseDueAt.Value < now;

    private static bool HasResolutionBreach(EhcTicket ticket, DateTime now)
        => IsOpenTicketStatus(ticket.Status)
            && ticket.ResolutionDueAt.HasValue
            && ticket.ResolutionDueAt.Value < now;

    private static bool IsTicketOverdue(EhcTicket ticket, DateTime now)
        => HasFirstResponseBreach(ticket, now) || HasResolutionBreach(ticket, now);

    private static CrmOpportunityOverviewDto MapOpportunityOverview(
        Opportunity opportunity,
        IReadOnlyDictionary<Guid, string> businessPartnerLookup,
        IReadOnlyDictionary<Guid, Lead> leadLookup)
    {
        var businessPartnerId = opportunity.CustomerId;
        leadLookup.TryGetValue(opportunity.LeadId ?? Guid.Empty, out var lead);

        return new CrmOpportunityOverviewDto
        {
            OpportunityId = opportunity.Id,
            Name = opportunity.Name,
            Stage = opportunity.Stage,
            Amount = opportunity.Amount,
            Currency = NormalizeCurrencyCode(opportunity.Currency, "USD"),
            Probability = opportunity.Probability,
            WeightedValue = decimal.Round(opportunity.Amount * opportunity.Probability / 100m, 2),
            ExpectedCloseDate = opportunity.ExpectedCloseDate,
            OpportunityType = opportunity.OpportunityType,
            LeadSource = opportunity.LeadSource,
            CustomerId = opportunity.CustomerId,
            BusinessPartnerId = businessPartnerId,
            BusinessPartnerName = businessPartnerId.HasValue
                ? businessPartnerLookup.GetValueOrDefault(businessPartnerId.Value)
                : null,
            LeadId = opportunity.LeadId,
            LeadName = lead == null ? null : GetLeadFullName(lead)
        };
    }

    private static CrmOpportunityListItemDto MapOpportunityListItem(
        Opportunity opportunity,
        IReadOnlyDictionary<Guid, string> businessPartnerLookup,
        IReadOnlyDictionary<Guid, Lead> leadLookup)
    {
        var overview = MapOpportunityOverview(opportunity, businessPartnerLookup, leadLookup);
        return new CrmOpportunityListItemDto
        {
            OpportunityId = overview.OpportunityId,
            Name = overview.Name,
            Stage = overview.Stage,
            Amount = overview.Amount,
            Currency = overview.Currency,
            Probability = overview.Probability,
            WeightedValue = overview.WeightedValue,
            ExpectedCloseDate = overview.ExpectedCloseDate,
            OpportunityType = overview.OpportunityType,
            LeadSource = overview.LeadSource,
            CustomerId = overview.CustomerId,
            BusinessPartnerId = overview.BusinessPartnerId,
            BusinessPartnerName = overview.BusinessPartnerName,
            LeadId = overview.LeadId,
            LeadName = overview.LeadName,
            ActualCloseDate = opportunity.ActualCloseDate,
            IsClosingSoon = !IsClosedOpportunityStage(opportunity.Stage)
                && opportunity.ExpectedCloseDate <= DateTime.UtcNow.AddDays(30),
            CreatedAt = opportunity.CreatedAt
        };
    }

    private static CrmLeadListItemDto MapLeadListItem(Lead lead, IReadOnlyCollection<Opportunity> opportunities)
    {
        var opportunityCount = opportunities.Count(x => x.LeadId.HasValue && x.LeadId.Value == lead.Id);
        return new CrmLeadListItemDto
        {
            LeadId = lead.Id,
            FirstName = lead.FirstName,
            LastName = lead.LastName,
            FullName = GetLeadFullName(lead),
            CompanyName = lead.CompanyName,
            JobTitle = lead.JobTitle,
            Email = lead.Email,
            Phone = lead.Phone,
            LeadSource = lead.LeadSource,
            LeadStatus = lead.LeadStatus,
            QualificationScore = lead.QualificationScore,
            EstimatedValue = lead.EstimatedValue,
            LastContactDate = lead.LastContactDate,
            NextFollowUpDate = lead.NextFollowUpDate,
            AssignedToId = lead.AssignedToId,
            OpportunityCount = opportunityCount,
            NeedsFollowUp = NeedsLeadFollowUp(lead, DateTime.UtcNow.AddDays(14)),
            CreatedAt = lead.CreatedAt
        };
    }

    private static Guid? ResolveActivityBusinessPartnerId(
        Activity activity,
        IReadOnlyDictionary<Guid, Opportunity> opportunityLookup,
        IReadOnlyDictionary<Guid, Lead> leadLookup)
    {
        if (activity.CustomerId.HasValue)
        {
            return activity.CustomerId.Value;
        }

        if (activity.OpportunityId.HasValue
            && opportunityLookup.TryGetValue(activity.OpportunityId.Value, out var opportunity)
            && opportunity.CustomerId.HasValue)
        {
            return opportunity.CustomerId.Value;
        }

        var resolvedLeadId = activity.LeadId;
        if (!resolvedLeadId.HasValue
            && activity.OpportunityId.HasValue
            && opportunityLookup.TryGetValue(activity.OpportunityId.Value, out var linkedOpportunity))
        {
            resolvedLeadId = linkedOpportunity.LeadId;
        }

        return resolvedLeadId.HasValue && leadLookup.TryGetValue(resolvedLeadId.Value, out var lead)
            ? lead.ConvertedCustomerId
            : null;
    }

    private static IEnumerable<CrmActivitySummaryDto> BuildSalesMilestoneActivities(
        IReadOnlyCollection<SalesOrder> salesOrders,
        IReadOnlyCollection<SalesAgreement> salesAgreements,
        IReadOnlyCollection<SalesAllocation> salesAllocations,
        IReadOnlyCollection<ReturnOrder> returnOrders,
        IReadOnlyCollection<CreditNote> creditNotes,
        IReadOnlyCollection<Refund> refunds,
        BusinessPartner account)
    {
        var items = new List<CrmActivitySummaryDto>();

        items.AddRange(salesOrders.Select(order => BuildSalesMilestoneActivity(
            order.Id,
            $"Sales Order {ResolveDocumentReference(order.DocumentNumber, order.Id)}",
            "Sales Order",
            order.OrderStatus.ToString(),
            order.DocumentDate,
            account.Id,
            account.PartnerName,
            order.OpportunityId,
            "SalesOrder",
            $"/sales/orders/{order.Id}")));

        items.AddRange(salesAgreements.Select(agreement => BuildSalesMilestoneActivity(
            agreement.Id,
            $"Sales Agreement {ResolveDocumentReference(agreement.DocumentNumber, agreement.Id)}",
            "Sales Agreement",
            agreement.AgreementStatus.ToString(),
            agreement.StartDate,
            account.Id,
            account.PartnerName,
            null,
            "SalesAgreement",
            $"/sales/agreements/{agreement.Id}")));

        items.AddRange(salesAllocations.Select(allocation => BuildSalesMilestoneActivity(
            allocation.Id,
            $"Sales Allocation {allocation.SourceItemName}",
            "Sales Allocation",
            allocation.Status,
            allocation.EffectiveDate ?? allocation.CreatedAt,
            account.Id,
            account.PartnerName,
            allocation.OpportunityId,
            "SalesAllocation",
            $"/sales/allocations/{allocation.Id}")));

        items.AddRange(returnOrders.Select(returnOrder => BuildSalesMilestoneActivity(
            returnOrder.Id,
            $"Return Order {ResolveDocumentReference(returnOrder.DocumentNumber, returnOrder.Id)}",
            "Return Order",
            returnOrder.ReturnStatus.ToString(),
            returnOrder.DocumentDate,
            account.Id,
            account.PartnerName,
            null,
            "ReturnOrder",
            $"/sales/return-orders?returnOrderId={returnOrder.Id}")));

        items.AddRange(creditNotes.Select(creditNote => BuildSalesMilestoneActivity(
            creditNote.Id,
            $"Credit Note {ResolveDocumentReference(creditNote.DocumentNumber, creditNote.Id)}",
            "Credit Note",
            creditNote.CreditNoteStatus.ToString(),
            creditNote.AppliedDate ?? creditNote.DocumentDate,
            account.Id,
            account.PartnerName,
            null,
            "CreditNote",
            $"/sales/credit-notes?creditNoteId={creditNote.Id}")));

        items.AddRange(refunds.Select(refund => BuildSalesMilestoneActivity(
            refund.Id,
            $"Refund {ResolveDocumentReference(refund.DocumentNumber, refund.Id)}",
            "Refund",
            refund.RefundStatus.ToString(),
            refund.ProcessedDate ?? refund.DocumentDate,
            account.Id,
            account.PartnerName,
            null,
            "Refund",
            $"/sales/refunds?refundId={refund.Id}")));

        return items;
    }

    private static CrmActivitySummaryDto BuildSalesMilestoneActivity(
        Guid entityId,
        string subject,
        string activityType,
        string status,
        DateTime activityDate,
        Guid businessPartnerId,
        string businessPartnerName,
        Guid? opportunityId,
        string relatedEntityType,
        string relatedEntityHref)
        => new()
        {
            ActivityId = entityId,
            Subject = subject,
            ActivityType = activityType,
            ActivityStatus = status,
            ActivityDate = activityDate,
            RequiresFollowUp = IsSalesMilestoneFollowUpStatus(status),
            Priority = 3,
            BusinessPartnerId = businessPartnerId,
            BusinessPartnerName = businessPartnerName,
            OpportunityId = opportunityId,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = entityId,
            RelatedEntityHref = relatedEntityHref
        };

    private static string ResolveDocumentReference(string? documentNumber, Guid fallbackId)
        => string.IsNullOrWhiteSpace(documentNumber)
            ? fallbackId.ToString("N")[..8].ToUpperInvariant()
            : documentNumber.Trim();

    private static bool IsSalesMilestoneFollowUpStatus(string status)
        => ContainsText(status, "Pending")
            || ContainsText(status, "Submitted")
            || ContainsText(status, "Draft")
            || ContainsText(status, "Reserved")
            || ContainsText(status, "Processing");

    private static CrmActivitySummaryDto MapActivitySummary(
        Activity activity,
        IReadOnlyDictionary<Guid, Opportunity> opportunityLookup,
        IReadOnlyDictionary<Guid, string> businessPartnerLookup,
        IReadOnlyDictionary<Guid, Lead> leadLookup)
    {
        opportunityLookup.TryGetValue(activity.OpportunityId ?? Guid.Empty, out var opportunity);
        var resolvedLeadId = activity.LeadId ?? opportunity?.LeadId;
        leadLookup.TryGetValue(resolvedLeadId ?? Guid.Empty, out var lead);
        var businessPartnerId = ResolveActivityBusinessPartnerId(activity, opportunityLookup, leadLookup);

        return new CrmActivitySummaryDto
        {
            ActivityId = activity.Id,
            Subject = activity.Subject,
            ActivityType = activity.ActivityType,
            ActivityStatus = activity.ActivityStatus,
            ActivityDate = activity.ActivityDate,
            DueDate = activity.DueDate ?? activity.NextFollowUpDate,
            RequiresFollowUp = activity.RequiresFollowUp,
            NextFollowUpDate = activity.NextFollowUpDate,
            Priority = activity.Priority,
            AssignedToId = activity.AssignedToId,
            BusinessPartnerId = businessPartnerId,
            BusinessPartnerName = businessPartnerId.HasValue
                ? businessPartnerLookup.GetValueOrDefault(businessPartnerId.Value)
                : null,
            LeadId = resolvedLeadId,
            LeadName = lead == null ? null : GetLeadFullName(lead),
            OpportunityId = activity.OpportunityId,
            OpportunityName = opportunity?.Name
        };
    }

    private static CrmActivityListItemDto MapActivityListItem(
        Activity activity,
        IReadOnlyDictionary<Guid, Opportunity> opportunityLookup,
        IReadOnlyDictionary<Guid, string> businessPartnerLookup,
        IReadOnlyDictionary<Guid, Lead> leadLookup)
    {
        var summary = MapActivitySummary(activity, opportunityLookup, businessPartnerLookup, leadLookup);
        var dueDate = activity.DueDate ?? activity.NextFollowUpDate;

        return new CrmActivityListItemDto
        {
            ActivityId = summary.ActivityId,
            Subject = summary.Subject,
            ActivityType = summary.ActivityType,
            ActivityStatus = summary.ActivityStatus,
            ActivityDate = summary.ActivityDate,
            DueDate = summary.DueDate,
            RequiresFollowUp = summary.RequiresFollowUp,
            NextFollowUpDate = summary.NextFollowUpDate,
            Priority = summary.Priority,
            AssignedToId = summary.AssignedToId,
            BusinessPartnerId = summary.BusinessPartnerId,
            BusinessPartnerName = summary.BusinessPartnerName,
            LeadId = summary.LeadId,
            LeadName = summary.LeadName,
            OpportunityId = summary.OpportunityId,
            OpportunityName = summary.OpportunityName,
            RelatedEntityType = summary.RelatedEntityType,
            RelatedEntityId = summary.RelatedEntityId,
            RelatedEntityHref = summary.RelatedEntityHref,
            IsOverdue = !IsClosedActivityStatus(activity.ActivityStatus)
                && dueDate.HasValue
                && dueDate.Value < DateTime.UtcNow,
            CreatedAt = activity.CreatedAt
        };
    }

    private static CrmActivityDetailDto MapActivityDetail(
        Activity activity,
        IReadOnlyDictionary<Guid, Opportunity> opportunityLookup,
        IReadOnlyDictionary<Guid, string> businessPartnerLookup,
        IReadOnlyDictionary<Guid, Lead> leadLookup)
    {
        var listItem = MapActivityListItem(activity, opportunityLookup, businessPartnerLookup, leadLookup);

        return new CrmActivityDetailDto
        {
            ActivityId = listItem.ActivityId,
            Subject = listItem.Subject,
            ActivityType = listItem.ActivityType,
            ActivityStatus = listItem.ActivityStatus,
            ActivityDate = listItem.ActivityDate,
            DueDate = listItem.DueDate,
            RequiresFollowUp = listItem.RequiresFollowUp,
            NextFollowUpDate = listItem.NextFollowUpDate,
            Priority = listItem.Priority,
            AssignedToId = listItem.AssignedToId,
            BusinessPartnerId = listItem.BusinessPartnerId,
            BusinessPartnerName = listItem.BusinessPartnerName,
            LeadId = listItem.LeadId,
            LeadName = listItem.LeadName,
            OpportunityId = listItem.OpportunityId,
            OpportunityName = listItem.OpportunityName,
            RelatedEntityType = listItem.RelatedEntityType,
            RelatedEntityId = listItem.RelatedEntityId,
            RelatedEntityHref = listItem.RelatedEntityHref,
            IsOverdue = listItem.IsOverdue,
            CreatedAt = listItem.CreatedAt,
            Description = activity.Description,
            Duration = activity.Duration,
            Location = activity.Location,
            Attendees = activity.Attendees,
            Outcome = activity.Outcome,
            Notes = activity.Notes
        };
    }

    private static Guid? ResolveQuoteBusinessPartnerId(
        Quote quote,
        IReadOnlyDictionary<Guid, Opportunity> opportunityLookup)
    {
        if (quote.CustomerId.HasValue)
        {
            return quote.CustomerId.Value;
        }

        return opportunityLookup.TryGetValue(quote.OpportunityId, out var opportunity)
            ? opportunity.CustomerId
            : null;
    }

    private static CrmQuoteSummaryDto MapQuoteSummary(
        Quote quote,
        IReadOnlyDictionary<Guid, Opportunity> opportunityLookup,
        IReadOnlyDictionary<Guid, string> businessPartnerLookup,
        IReadOnlyDictionary<Guid, Lead> leadLookup)
    {
        opportunityLookup.TryGetValue(quote.OpportunityId, out var opportunity);
        var businessPartnerId = ResolveQuoteBusinessPartnerId(quote, opportunityLookup);

        return new CrmQuoteSummaryDto
        {
            QuoteId = quote.Id,
            OpportunityId = quote.OpportunityId,
            QuoteName = quote.QuoteName,
            QuoteStatus = quote.QuoteStatus,
            Value = ResolveQuoteValue(quote),
            Currency = NormalizeCurrencyCode(quote.Currency, "USD"),
            ValidUntil = quote.ValidUntil,
            OpportunityName = opportunity?.Name,
            BusinessPartnerId = businessPartnerId,
            BusinessPartnerName = businessPartnerId.HasValue
                ? businessPartnerLookup.GetValueOrDefault(businessPartnerId.Value)
                : null,
            IsExpiringSoon = !IsClosedQuoteStatus(quote.QuoteStatus)
                && quote.ValidUntil <= DateTime.UtcNow.AddDays(14)
        };
    }

    private static CrmQuoteListItemDto MapQuoteListItem(
        Quote quote,
        IReadOnlyDictionary<Guid, Opportunity> opportunityLookup,
        IReadOnlyDictionary<Guid, string> businessPartnerLookup,
        IReadOnlyDictionary<Guid, Lead> leadLookup)
    {
        var summary = MapQuoteSummary(quote, opportunityLookup, businessPartnerLookup, leadLookup);
        opportunityLookup.TryGetValue(quote.OpportunityId, out var opportunity);
        leadLookup.TryGetValue(opportunity?.LeadId ?? Guid.Empty, out var lead);

        return new CrmQuoteListItemDto
        {
            QuoteId = summary.QuoteId,
            OpportunityId = summary.OpportunityId,
            QuoteName = summary.QuoteName,
            QuoteStatus = summary.QuoteStatus,
            Value = summary.Value,
            Currency = summary.Currency,
            ValidUntil = summary.ValidUntil,
            OpportunityName = summary.OpportunityName,
            BusinessPartnerId = summary.BusinessPartnerId,
            BusinessPartnerName = summary.BusinessPartnerName,
            IsExpiringSoon = summary.IsExpiringSoon,
            DocumentNumber = quote.DocumentNumber,
            DocumentDate = quote.DocumentDate,
            CreatedAt = quote.CreatedAt,
            SentDate = quote.SentDate,
            AcceptedDate = quote.AcceptedDate,
            LeadId = opportunity?.LeadId,
            LeadName = lead == null ? null : GetLeadFullName(lead),
            IsAccepted = string.Equals(quote.QuoteStatus, "Accepted", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static CrmProjectListItemDto MapProjectListItem(
        Project project,
        IReadOnlyDictionary<Guid, Contract> contractLookup,
        IReadOnlyDictionary<Guid, string> businessPartnerLookup,
        DateTime now)
    {
        contractLookup.TryGetValue(project.ContractId ?? Guid.Empty, out var linkedContract);
        var resolvedBusinessPartnerId = ResolveProjectBusinessPartnerId(project, contractLookup);

        return new CrmProjectListItemDto
        {
            ProjectId = project.Id,
            ProjectCode = project.ProjectCode,
            Title = project.Title,
            Status = project.Status,
            Value = decimal.Round(project.ApprovedBudget ?? project.EstimatedBudget ?? 0m, 2),
            ProgressPercent = project.ProgressPercent,
            BusinessPartnerId = project.BusinessPartnerId,
            ContractId = project.ContractId,
            RelationshipType = linkedContract != null
                ? "Contract"
                : project.BusinessPartnerId.HasValue
                    ? "Account"
                    : "Direct",
            TargetEndDate = project.TargetEndDate,
            ResolvedBusinessPartnerId = resolvedBusinessPartnerId,
            BusinessPartnerName = resolvedBusinessPartnerId.HasValue
                ? businessPartnerLookup.GetValueOrDefault(resolvedBusinessPartnerId.Value)
                : null,
            ContractNumber = linkedContract?.ContractNumber,
            ContractTitle = linkedContract?.ContractTitle,
            StartDate = project.StartDate,
            ActualEndDate = project.ActualEndDate,
            StatusRemarks = project.StatusRemarks,
            IsOverdue = IsActiveProject(project) && project.TargetEndDate.HasValue && project.TargetEndDate.Value < now,
            IsLinkedToActiveContract = linkedContract != null && IsActiveContract(linkedContract)
        };
    }

    private static CrmProjectDetailDto MapProjectDetail(
        Project project,
        Contract? linkedContract,
        string? businessPartnerName,
        DateTime now)
    {
        var contractLookup = linkedContract == null
            ? new Dictionary<Guid, Contract>()
            : new Dictionary<Guid, Contract> { [linkedContract.Id] = linkedContract };
        var resolvedBusinessPartnerId = project.BusinessPartnerId ?? linkedContract?.BusinessPartnerId;
        var businessPartnerLookup = resolvedBusinessPartnerId.HasValue && !string.IsNullOrWhiteSpace(businessPartnerName)
            ? new Dictionary<Guid, string> { [resolvedBusinessPartnerId.Value] = businessPartnerName }
            : new Dictionary<Guid, string>();
        var listItem = MapProjectListItem(project, contractLookup, businessPartnerLookup, now);

        return new CrmProjectDetailDto
        {
            ProjectId = listItem.ProjectId,
            ProjectCode = listItem.ProjectCode,
            Title = listItem.Title,
            Status = listItem.Status,
            Value = listItem.Value,
            ProgressPercent = listItem.ProgressPercent,
            BusinessPartnerId = listItem.BusinessPartnerId,
            ContractId = listItem.ContractId,
            RelationshipType = listItem.RelationshipType,
            TargetEndDate = listItem.TargetEndDate,
            ResolvedBusinessPartnerId = listItem.ResolvedBusinessPartnerId,
            BusinessPartnerName = listItem.BusinessPartnerName,
            ContractNumber = listItem.ContractNumber,
            ContractTitle = listItem.ContractTitle,
            StartDate = listItem.StartDate,
            ActualEndDate = listItem.ActualEndDate,
            StatusRemarks = listItem.StatusRemarks,
            IsOverdue = listItem.IsOverdue,
            IsLinkedToActiveContract = listItem.IsLinkedToActiveContract,
            Summary = project.Summary,
            BusinessCase = project.BusinessCase,
            Objectives = project.Objectives,
            Methodology = project.Methodology,
            EstimatedBudget = project.EstimatedBudget,
            ApprovedBudget = project.ApprovedBudget,
            ActualCost = project.ActualCost,
            BudgetStatus = project.BudgetStatus,
            ExternalPortalAccessEnabled = project.ExternalPortalAccessEnabled,
            ExternalCollaborationEnabled = project.ExternalCollaborationEnabled
        };
    }

    private static CrmProjectSummaryDto MapProjectSummary(Project project, string relationshipType = "Direct")
        => new()
        {
            ProjectId = project.Id,
            ProjectCode = project.ProjectCode,
            Title = project.Title,
            Status = project.Status,
            Value = decimal.Round(project.ApprovedBudget ?? project.EstimatedBudget ?? 0m, 2),
            ProgressPercent = project.ProgressPercent,
            BusinessPartnerId = project.BusinessPartnerId,
            ContractId = project.ContractId,
            RelationshipType = relationshipType,
            TargetEndDate = project.TargetEndDate
        };

    private static CrmContractListItemDto MapContractListItem(
        Contract contract,
        string? businessPartnerName,
        IReadOnlyCollection<Project> relatedProjects,
        DateTime contractWindow)
        => new()
        {
            ContractId = contract.Id,
            ContractNumber = contract.ContractNumber,
            ContractTitle = contract.ContractTitle,
            Status = contract.Status,
            ContractValue = contract.ContractValue,
            BusinessPartnerId = contract.BusinessPartnerId,
            TenderAwardId = contract.TenderAwardId,
            TenderId = contract.TenderId,
            RelationshipType = "Account",
            EndDate = contract.EndDate,
            BusinessPartnerName = businessPartnerName,
            Currency = NormalizeCurrencyCode(contract.Currency, "USD"),
            ContractType = contract.ContractType,
            StartDate = contract.StartDate,
            SignedDate = contract.SignedDate,
            PaymentTerms = contract.PaymentTerms,
            ProjectCount = relatedProjects.Count,
            ActiveProjectCount = relatedProjects.Count(IsActiveProject),
            IsActive = IsActiveContract(contract),
            IsExpiringSoon = IsActiveContract(contract)
                && contract.EndDate.HasValue
                && contract.EndDate.Value <= contractWindow
        };

    private static CrmContractSummaryDto MapContractSummary(Contract contract, string relationshipType = "Direct")
        => new()
        {
            ContractId = contract.Id,
            ContractNumber = contract.ContractNumber,
            ContractTitle = contract.ContractTitle,
            Status = contract.Status,
            ContractValue = contract.ContractValue,
            BusinessPartnerId = contract.BusinessPartnerId,
            TenderAwardId = contract.TenderAwardId,
            TenderId = contract.TenderId,
            RelationshipType = relationshipType,
            EndDate = contract.EndDate
        };

    private static CrmContractDetailDto MapContractDetail(
        Contract contract,
        string? businessPartnerName,
        IReadOnlyCollection<Project> relatedProjects,
        DateTime contractWindow)
    {
        var listItem = MapContractListItem(contract, businessPartnerName, relatedProjects, contractWindow);

        return new CrmContractDetailDto
        {
            ContractId = listItem.ContractId,
            ContractNumber = listItem.ContractNumber,
            ContractTitle = listItem.ContractTitle,
            Status = listItem.Status,
            ContractValue = listItem.ContractValue,
            BusinessPartnerId = listItem.BusinessPartnerId,
            TenderAwardId = listItem.TenderAwardId,
            TenderId = listItem.TenderId,
            RelationshipType = listItem.RelationshipType,
            EndDate = listItem.EndDate,
            BusinessPartnerName = listItem.BusinessPartnerName,
            Currency = listItem.Currency,
            ContractType = listItem.ContractType,
            StartDate = listItem.StartDate,
            SignedDate = listItem.SignedDate,
            PaymentTerms = listItem.PaymentTerms,
            ProjectCount = listItem.ProjectCount,
            ActiveProjectCount = listItem.ActiveProjectCount,
            IsActive = listItem.IsActive,
            IsExpiringSoon = listItem.IsExpiringSoon,
            ScopeOfWork = contract.ScopeOfWork,
            Deliverables = contract.Deliverables,
            SpecialConditions = contract.SpecialConditions,
            PenaltyClause = contract.PenaltyClause,
            SignedByName = contract.SignedByName,
            ContractorSignatoryName = contract.ContractorSignatoryName,
            ActivatedAt = contract.ActivatedAt,
            CompletedAt = contract.CompletedAt,
            TerminatedAt = contract.TerminatedAt,
            TerminationReason = contract.TerminationReason,
            Notes = contract.Notes
        };
    }

    private static List<CrmHealthSignalDto> BuildAccountHealthSignals(
        BusinessPartner partner,
        IReadOnlyCollection<Project> projects,
        IReadOnlyCollection<Contract> contracts,
        IReadOnlyCollection<TenderInvitation> tenderInvitations,
        IReadOnlyCollection<TenderBid> tenderBids,
        IReadOnlyCollection<TenderAward> tenderAwards,
        IReadOnlyCollection<Opportunity> opportunities,
        IReadOnlyCollection<Quote> quotes,
        bool hasLeadFollowUp,
        bool hasActivityFollowUp,
        DateTime now,
        DateTime contractWindow)
        => AssessAccountHealth(
                partner,
                projects,
                contracts,
                tenderInvitations,
                tenderBids,
                tenderAwards,
                opportunities,
                quotes,
                hasLeadFollowUp,
                hasActivityFollowUp,
                now,
                contractWindow)
            .Signals
            .OrderByDescending(x => Math.Abs(x.ScoreImpact))
            .ThenBy(x => x.Label)
            .ToList();

    private static CrmConversionChainDto BuildConversionChain(
        Opportunity opportunity,
        IReadOnlyCollection<Quote> quotes,
        IReadOnlyCollection<Contract> contracts,
        IReadOnlyCollection<Project> projects,
        IReadOnlyCollection<SalesOrder> salesOrders,
        IReadOnlyDictionary<Guid, string> businessPartnerLookup,
        IReadOnlyDictionary<Guid, Lead> leadLookup)
    {
        leadLookup.TryGetValue(opportunity.LeadId ?? Guid.Empty, out var lead);
        var businessPartnerId = opportunity.CustomerId
            ?? quotes.Select(x => x.CustomerId).FirstOrDefault(x => x.HasValue)
            ?? lead?.ConvertedCustomerId;
        var businessPartnerName = businessPartnerId.HasValue
            ? businessPartnerLookup.GetValueOrDefault(businessPartnerId.Value)
            : null;
        var contractIds = contracts.Select(x => x.Id).ToHashSet();
        var nodes = new List<CrmConversionChainNodeDto>();

        if (lead != null)
        {
            nodes.Add(new CrmConversionChainNodeDto
            {
                Stage = "Lead",
                EntityType = "Lead",
                EntityId = lead.Id,
                Title = GetLeadFullName(lead),
                Status = lead.LeadStatus,
                Amount = lead.EstimatedValue,
                ReferenceDate = lead.NextFollowUpDate ?? lead.LastContactDate ?? lead.CreatedAt,
                RelationshipType = "Direct",
                RelationshipNote = "Opportunity is directly linked to this lead."
            });
        }

        nodes.Add(new CrmConversionChainNodeDto
        {
            Stage = "Opportunity",
            EntityType = "Opportunity",
            EntityId = opportunity.Id,
            Title = opportunity.Name,
            Status = opportunity.Stage,
            Amount = opportunity.Amount,
            Currency = NormalizeCurrencyCode(opportunity.Currency, "USD"),
            ReferenceDate = opportunity.ExpectedCloseDate,
            RelationshipType = "Direct",
            RelationshipNote = "Primary commercial record in the CRM pipeline."
        });

        nodes.AddRange(quotes
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CrmConversionChainNodeDto
            {
                Stage = "Quote",
                EntityType = "Quote",
                EntityId = x.Id,
                Title = x.QuoteName,
                Status = x.QuoteStatus,
                Amount = ResolveQuoteValue(x),
                Currency = NormalizeCurrencyCode(x.Currency, "USD"),
                ReferenceDate = x.AcceptedDate ?? x.SentDate ?? x.ValidUntil,
                RelationshipType = "Direct",
                RelationshipNote = "Quote is directly linked to this opportunity.",
                ReferenceCode = x.DocumentNumber
            }));

        nodes.AddRange(salesOrders
            .OrderByDescending(x => x.DocumentDate)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new CrmConversionChainNodeDto
            {
                Stage = "Sales Order",
                EntityType = "SalesOrder",
                EntityId = x.Id,
                Title = $"Sales Order {ResolveDocumentReference(x.DocumentNumber, x.Id)}",
                Status = x.OrderStatus.ToString(),
                Amount = x.TotalAmount,
                Currency = NormalizeCurrencyCode(x.Currency, "USD"),
                ReferenceDate = x.DocumentDate,
                RelationshipType = x.QuoteId.HasValue ? "Quote" : "Opportunity",
                RelationshipNote = x.QuoteId.HasValue
                    ? "Sales order was created from a CRM quote in this opportunity."
                    : "Sales order is directly linked to this opportunity.",
                ReferenceCode = x.DocumentNumber
            }));

        nodes.AddRange(contracts
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CrmConversionChainNodeDto
            {
                Stage = "Contract",
                EntityType = "Contract",
                EntityId = x.Id,
                Title = x.ContractTitle,
                Status = x.Status,
                Amount = x.ContractValue,
                Currency = NormalizeCurrencyCode(x.Currency, "USD"),
                ReferenceDate = x.StartDate ?? x.CreatedAt,
                RelationshipType = "Account",
                RelationshipNote = "Contract is linked through the shared CRM account/business partner.",
                ReferenceCode = x.ContractNumber
            }));

        nodes.AddRange(projects
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CrmConversionChainNodeDto
            {
                Stage = "Project",
                EntityType = "Project",
                EntityId = x.Id,
                Title = x.Title,
                Status = x.Status,
                Amount = x.ApprovedBudget ?? x.EstimatedBudget,
                ReferenceDate = x.TargetEndDate ?? x.StartDate ?? x.CreatedAt,
                RelationshipType = x.ContractId.HasValue && contractIds.Contains(x.ContractId.Value)
                    ? "Contract"
                    : "Account",
                RelationshipNote = x.ContractId.HasValue && contractIds.Contains(x.ContractId.Value)
                    ? "Project is linked through a contract already associated with this CRM account."
                    : "Project is linked through the shared CRM account/business partner.",
                ReferenceCode = x.ProjectCode
            }));

        return new CrmConversionChainDto
        {
            OpportunityId = opportunity.Id,
            OpportunityName = opportunity.Name,
            BusinessPartnerId = businessPartnerId,
            BusinessPartnerName = businessPartnerName,
            LeadId = lead?.Id,
            LeadName = lead == null ? null : GetLeadFullName(lead),
            Nodes = nodes
        };
    }

    private static CrmTenderListItemDto MapTenderInvitationListItem(
        TenderInvitation invitation,
        Tender? tender,
        string? businessPartnerName,
        Contract? relatedContract,
        DateTime now)
        => new()
        {
            EntityId = invitation.Id,
            TenderId = invitation.TenderId,
            EntityType = "Invitation",
            BusinessPartnerId = invitation.BusinessPartnerId,
            BusinessPartnerName = businessPartnerName,
            TenderNumber = tender?.TenderNumber ?? invitation.TenderId.ToString(),
            TenderTitle = tender?.Title ?? "Tender invitation",
            TenderType = tender?.TenderType ?? string.Empty,
            Currency = ResolveCurrencyCode(tender?.Currency),
            ReferenceNumber = tender?.TenderNumber ?? invitation.TenderId.ToString(),
            Status = invitation.Status,
            Amount = tender?.EstimatedValue ?? 0m,
            CreatedAt = invitation.InvitedDate,
            SubmissionDeadline = tender?.SubmissionDeadline,
            RelatedContractId = relatedContract?.Id,
            RelatedContractNumber = relatedContract?.ContractNumber,
            TenderStatus = tender?.Status ?? string.Empty,
            PublishDate = tender?.PublishDate,
            AwardDate = tender?.AwardDate,
            IsClosingSoon = IsTenderClosingSoon(tender, now)
        };

    private static CrmTenderListItemDto MapTenderBidListItem(
        TenderBid bid,
        Tender? tender,
        string? businessPartnerName,
        TenderAward? relatedAward,
        Contract? relatedContract,
        DateTime now)
        => new()
        {
            EntityId = bid.Id,
            TenderId = bid.TenderId,
            EntityType = "Bid",
            BusinessPartnerId = bid.BusinessPartnerId,
            BusinessPartnerName = businessPartnerName,
            TenderNumber = tender?.TenderNumber ?? bid.TenderId.ToString(),
            TenderTitle = tender?.Title ?? "Tender bid",
            TenderType = tender?.TenderType ?? string.Empty,
            Currency = ResolveCurrencyCode(bid.Currency, new[] { tender?.Currency }),
            ReferenceNumber = bid.BidNumber,
            Status = bid.Status,
            Amount = bid.TotalBidAmount,
            CreatedAt = bid.SubmittedDate,
            SubmissionDeadline = tender?.SubmissionDeadline,
            RelatedContractId = relatedContract?.Id,
            RelatedContractNumber = relatedContract?.ContractNumber,
            TenderStatus = tender?.Status ?? string.Empty,
            PublishDate = tender?.PublishDate,
            AwardDate = relatedAward?.AwardDate ?? tender?.AwardDate,
            IsClosingSoon = IsTenderClosingSoon(tender, now)
        };

    private static CrmTenderListItemDto MapTenderAwardListItem(
        TenderAward award,
        Tender? tender,
        string? businessPartnerName,
        Contract? relatedContract,
        DateTime now)
        => new()
        {
            EntityId = award.Id,
            TenderId = award.TenderId,
            EntityType = "Award",
            BusinessPartnerId = award.BusinessPartnerId,
            BusinessPartnerName = businessPartnerName,
            TenderNumber = tender?.TenderNumber ?? award.TenderId.ToString(),
            TenderTitle = tender?.Title ?? "Tender award",
            TenderType = tender?.TenderType ?? string.Empty,
            Currency = ResolveCurrencyCode(award.Currency, new[] { tender?.Currency }),
            ReferenceNumber = tender?.TenderNumber ?? award.TenderId.ToString(),
            Status = award.Status,
            Amount = award.AwardedAmount,
            CreatedAt = award.AwardDate,
            SubmissionDeadline = tender?.SubmissionDeadline,
            RelatedContractId = relatedContract?.Id,
            RelatedContractNumber = relatedContract?.ContractNumber,
            TenderStatus = tender?.Status ?? string.Empty,
            PublishDate = tender?.PublishDate,
            AwardDate = award.AwardDate,
            IsClosingSoon = IsTenderClosingSoon(tender, now)
        };

    private static CrmTenderDetailDto MapTenderInvitationDetail(
        TenderInvitation invitation,
        Tender? tender,
        string? businessPartnerName,
        TenderAward? relatedAward,
        Contract? relatedContract,
        DateTime now)
    {
        var listItem = MapTenderInvitationListItem(invitation, tender, businessPartnerName, relatedContract, now);
        return new CrmTenderDetailDto
        {
            EntityId = listItem.EntityId,
            TenderId = listItem.TenderId,
            EntityType = listItem.EntityType,
            BusinessPartnerId = listItem.BusinessPartnerId,
            BusinessPartnerName = listItem.BusinessPartnerName,
            TenderNumber = listItem.TenderNumber,
            TenderTitle = listItem.TenderTitle,
            TenderType = listItem.TenderType,
            Currency = listItem.Currency,
            ReferenceNumber = listItem.ReferenceNumber,
            Status = listItem.Status,
            Amount = listItem.Amount,
            CreatedAt = listItem.CreatedAt,
            SubmissionDeadline = listItem.SubmissionDeadline,
            RelatedContractId = listItem.RelatedContractId,
            RelatedContractNumber = listItem.RelatedContractNumber,
            TenderStatus = listItem.TenderStatus,
            PublishDate = listItem.PublishDate,
            AwardDate = listItem.AwardDate,
            IsClosingSoon = listItem.IsClosingSoon,
            TenderAwardId = relatedAward?.Id,
            RelatedContractTitle = relatedContract?.ContractTitle,
            InvitedDate = invitation.InvitedDate,
            ViewedDate = invitation.ViewedDate,
            ResponseDate = invitation.ResponseDate,
            DeclineReason = invitation.DeclineReason,
            RelationshipNote = "This CRM account was invited into the procurement opportunity."
        };
    }

    private static CrmTenderDetailDto MapTenderBidDetail(
        TenderBid bid,
        Tender? tender,
        string? businessPartnerName,
        TenderAward? relatedAward,
        Contract? relatedContract,
        DateTime now)
    {
        var listItem = MapTenderBidListItem(bid, tender, businessPartnerName, relatedAward, relatedContract, now);
        return new CrmTenderDetailDto
        {
            EntityId = listItem.EntityId,
            TenderId = listItem.TenderId,
            EntityType = listItem.EntityType,
            BusinessPartnerId = listItem.BusinessPartnerId,
            BusinessPartnerName = listItem.BusinessPartnerName,
            TenderNumber = listItem.TenderNumber,
            TenderTitle = listItem.TenderTitle,
            TenderType = listItem.TenderType,
            Currency = listItem.Currency,
            ReferenceNumber = listItem.ReferenceNumber,
            Status = listItem.Status,
            Amount = listItem.Amount,
            CreatedAt = listItem.CreatedAt,
            SubmissionDeadline = listItem.SubmissionDeadline,
            RelatedContractId = listItem.RelatedContractId,
            RelatedContractNumber = listItem.RelatedContractNumber,
            TenderStatus = listItem.TenderStatus,
            PublishDate = listItem.PublishDate,
            AwardDate = listItem.AwardDate,
            IsClosingSoon = listItem.IsClosingSoon,
            TenderBidId = bid.Id,
            TenderAwardId = relatedAward?.Id,
            RelatedContractTitle = relatedContract?.ContractTitle,
            SubmittedDate = bid.SubmittedDate,
            TotalScore = bid.TotalScore,
            Rank = bid.Rank,
            DeliveryDays = bid.DeliveryDays,
            PaymentTerms = bid.PaymentTerms,
            WarrantyTerms = bid.WarrantyTerms,
            IsCompliant = bid.IsCompliant,
            NonComplianceReasons = bid.NonComplianceReasons,
            Notes = bid.EvaluationNotes,
            RelationshipNote = "This bid carries the commercial response from the CRM account into procurement."
        };
    }

    private static CrmTenderDetailDto MapTenderAwardDetail(
        TenderAward award,
        Tender? tender,
        string? businessPartnerName,
        Contract? relatedContract,
        DateTime now)
    {
        var listItem = MapTenderAwardListItem(award, tender, businessPartnerName, relatedContract, now);
        return new CrmTenderDetailDto
        {
            EntityId = listItem.EntityId,
            TenderId = listItem.TenderId,
            EntityType = listItem.EntityType,
            BusinessPartnerId = listItem.BusinessPartnerId,
            BusinessPartnerName = listItem.BusinessPartnerName,
            TenderNumber = listItem.TenderNumber,
            TenderTitle = listItem.TenderTitle,
            TenderType = listItem.TenderType,
            Currency = listItem.Currency,
            ReferenceNumber = listItem.ReferenceNumber,
            Status = listItem.Status,
            Amount = listItem.Amount,
            CreatedAt = listItem.CreatedAt,
            SubmissionDeadline = listItem.SubmissionDeadline,
            RelatedContractId = listItem.RelatedContractId,
            RelatedContractNumber = listItem.RelatedContractNumber,
            TenderStatus = listItem.TenderStatus,
            PublishDate = listItem.PublishDate,
            AwardDate = listItem.AwardDate,
            IsClosingSoon = listItem.IsClosingSoon,
            TenderBidId = award.TenderBidId,
            TenderAwardId = award.Id,
            RelatedContractTitle = relatedContract?.ContractTitle,
            OriginalBidAmount = award.OriginalBidAmount,
            IsNegotiated = award.IsNegotiated,
            AwardJustification = award.AwardJustification,
            PurchaseOrderId = award.PurchaseOrderId,
            Notes = award.Notes,
            RelationshipNote = "This award confirms procurement success and may convert into contract delivery."
        };
    }

    private static IEnumerable<CrmTenderSummaryDto> BuildTenderSummaries(
        IEnumerable<TenderInvitation> invitations,
        IEnumerable<TenderBid> bids,
        IEnumerable<TenderAward> awards,
        IReadOnlyDictionary<Guid, Tender> tenderLookup,
        BusinessPartner account,
        IReadOnlyCollection<Contract> contracts)
    {
        var contractLookupByAwardId = contracts
            .GroupBy(x => x.TenderAwardId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.CreatedAt).First());
        var awardLookupByBidId = awards
            .GroupBy(x => x.TenderBidId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.AwardDate).First());
        var awardLookupByPartnerTender = awards
            .GroupBy(x => (x.BusinessPartnerId, x.TenderId))
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.AwardDate).First());

        return invitations
            .Select(x =>
            {
                tenderLookup.TryGetValue(x.TenderId, out var relatedTender);
                awardLookupByPartnerTender.TryGetValue((x.BusinessPartnerId, x.TenderId), out var relatedAward);
                var relatedContract = relatedAward != null ? contractLookupByAwardId.GetValueOrDefault(relatedAward.Id) : null;
                return (CrmTenderSummaryDto)MapTenderInvitationListItem(
                    x,
                    relatedTender,
                    account.PartnerName,
                    relatedContract,
                    DateTime.UtcNow);
            })
            .Concat(bids.Select(x =>
            {
                tenderLookup.TryGetValue(x.TenderId, out var relatedTender);
                awardLookupByBidId.TryGetValue(x.Id, out var relatedAward);
                var relatedContract = relatedAward != null ? contractLookupByAwardId.GetValueOrDefault(relatedAward.Id) : null;
                return (CrmTenderSummaryDto)MapTenderBidListItem(
                    x,
                    relatedTender,
                    account.PartnerName,
                    relatedAward,
                    relatedContract,
                    DateTime.UtcNow);
            }))
            .Concat(awards.Select(x =>
            {
                tenderLookup.TryGetValue(x.TenderId, out var relatedTender);
                var relatedContract = contractLookupByAwardId.GetValueOrDefault(x.Id);
                return (CrmTenderSummaryDto)MapTenderAwardListItem(
                    x,
                    relatedTender,
                    account.PartnerName,
                    relatedContract,
                    DateTime.UtcNow);
            }));
    }

    private static AccountHealthAssessment AssessAccountHealth(
        BusinessPartner partner,
        IReadOnlyCollection<Project> projects,
        IReadOnlyCollection<Contract> contracts,
        IReadOnlyCollection<TenderInvitation> tenderInvitations,
        IReadOnlyCollection<TenderBid> tenderBids,
        IReadOnlyCollection<TenderAward> tenderAwards,
        IReadOnlyCollection<Opportunity> opportunities,
        IReadOnlyCollection<Quote> quotes,
        bool hasLeadFollowUp,
        bool hasActivityFollowUp,
        DateTime now,
        DateTime contractWindow)
    {
        var activeProjectCount = projects.Count(IsActiveProject);
        var activeContractCount = contracts.Count(IsActiveContract);
        var overdueProjectCount = projects.Count(x => IsActiveProject(x) && x.TargetEndDate.HasValue && x.TargetEndDate.Value < now);
        var expiringContractCount = contracts.Count(x => IsActiveContract(x) && x.EndDate.HasValue && x.EndDate.Value <= now.AddDays(30));
        var openOpportunityCount = opportunities.Count;
        var activeQuoteCount = quotes.Count;
        var nextMilestoneDate = contracts
            .Where(x => IsActiveContract(x) && x.EndDate.HasValue)
            .Select(x => x.EndDate)
            .Concat(projects.Where(x => IsActiveProject(x) && x.TargetEndDate.HasValue).Select(x => x.TargetEndDate))
            .Where(x => x.HasValue)
            .OrderBy(x => x)
            .FirstOrDefault();
        var hasOpenFollowUp = tenderInvitations.Any(x =>
                !string.Equals(x.Status, "Submitted", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(x.Status, "Declined", StringComparison.OrdinalIgnoreCase))
            || contracts.Any(x => IsActiveContract(x) && x.EndDate.HasValue && x.EndDate.Value <= contractWindow)
            || hasLeadFollowUp
            || hasActivityFollowUp;

        var score = 60;
        var signals = new List<CrmHealthSignalDto>();

        if (activeProjectCount > 0)
        {
            AddHealthSignal(signals, "Active delivery work underway", 8);
            score += 8;
        }

        if (activeContractCount > 0)
        {
            AddHealthSignal(signals, "Active contract coverage", 10);
            score += 10;
        }

        if (openOpportunityCount > 0)
        {
            AddHealthSignal(signals, "Pipeline is active for this account", 8);
            score += 8;
        }

        if (activeQuoteCount > 0)
        {
            AddHealthSignal(signals, "Quote activity is in motion", 6);
            score += 6;
        }

        if (tenderAwards.Any())
        {
            AddHealthSignal(signals, "Tender awards reinforce account momentum", 6);
            score += 6;
        }

        if (tenderBids.Any())
        {
            AddHealthSignal(signals, "Tender bids indicate active pursuit", 4);
            score += 4;
        }

        if (partner.PerformanceRating.HasValue)
        {
            var impact = partner.PerformanceRating.Value switch
            {
                >= 4m => 10,
                >= 3m => 4,
                _ => -12
            };
            AddHealthSignal(signals, $"Performance rating {partner.PerformanceRating.Value:0.0}", impact);
            score += impact;
        }

        var riskImpact = (partner.RiskLevel ?? string.Empty).ToLowerInvariant() switch
        {
            "low" => 4,
            "medium" => -8,
            "high" => -18,
            "critical" => -28,
            _ => 0
        };
        if (riskImpact != 0)
        {
            AddHealthSignal(signals, $"{partner.RiskLevel} risk profile", riskImpact);
            score += riskImpact;
        }

        if (partner.IsOnCreditHold)
        {
            AddHealthSignal(signals, "Customer is on credit hold", -25);
            score -= 25;
        }

        if (overdueProjectCount > 0)
        {
            AddHealthSignal(signals, "Overdue delivery milestones detected", -10);
            score -= 10;
        }

        if (expiringContractCount > 0)
        {
            AddHealthSignal(signals, "Active contracts are nearing expiry", -8);
            score -= 8;
        }

        if (hasOpenFollowUp)
        {
            AddHealthSignal(signals, "Follow-up queue needs attention", -6);
            score -= 6;
        }

        if (activeProjectCount == 0 && activeContractCount == 0 && openOpportunityCount == 0 && activeQuoteCount == 0)
        {
            AddHealthSignal(signals, "Limited active commercial footprint", -8);
            score -= 8;
        }

        score = Math.Clamp(score, 0, 100);
        var category = score switch
        {
            >= 80 => "Strong",
            >= 65 => "Healthy",
            >= 50 => "Watch",
            >= 35 => "At Risk",
            _ => "Critical"
        };
        var isAtRisk = score < 50
            || string.Equals(partner.RiskLevel, "High", StringComparison.OrdinalIgnoreCase)
            || string.Equals(partner.RiskLevel, "Critical", StringComparison.OrdinalIgnoreCase)
            || partner.IsOnCreditHold
            || overdueProjectCount > 0
            || expiringContractCount > 0;

        return new AccountHealthAssessment
        {
            Score = score,
            Category = category,
            Signals = signals,
            HasOpenFollowUp = hasOpenFollowUp,
            IsAtRisk = isAtRisk,
            NextMilestoneDate = nextMilestoneDate
        };
    }

    private static void AddHealthSignal(List<CrmHealthSignalDto> signals, string label, int impact)
    {
        if (impact == 0)
        {
            return;
        }

        signals.Add(new CrmHealthSignalDto
        {
            Label = label,
            Direction = impact > 0 ? "Positive" : "Negative",
            ScoreImpact = impact
        });
    }

    private static CrmCampaignListItemDto BuildCampaignListItem(
        Campaign campaign,
        IReadOnlyCollection<CampaignMember> members,
        IReadOnlyDictionary<Guid, Lead> leadLookup,
        IReadOnlyDictionary<Guid, List<Opportunity>> opportunitiesByLeadId,
        IReadOnlyCollection<Guid> influencedAccountIds,
        DateTime now,
        DateTime endingWindow)
    {
        var relatedLeads = members
            .Where(x => x.LeadId.HasValue)
            .Select(x => leadLookup.GetValueOrDefault(x.LeadId!.Value))
            .OfType<Lead>()
            .ToList();
        var relatedOpportunities = relatedLeads
            .SelectMany(x => opportunitiesByLeadId.GetValueOrDefault(x.Id) ?? new List<Opportunity>())
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();
        var openOpportunities = relatedOpportunities
            .Where(x => !IsClosedOpportunityStage(x.Stage))
            .ToList();
        var memberCount = members.Count;
        var respondedMemberCount = members.Count(IsRespondedCampaignMember);
        var actualAudience = campaign.ActualAudience > 0 ? campaign.ActualAudience : memberCount;
        var responseCount = campaign.ResponseCount > 0 ? campaign.ResponseCount : respondedMemberCount;
        var responseRate = actualAudience > 0
            ? decimal.Round(responseCount * 100m / actualAudience, 1)
            : 0m;
        var roiPercent = campaign.ActualCost > 0m
            ? decimal.Round((campaign.ActualRevenue - campaign.ActualCost) * 100m / campaign.ActualCost, 1)
            : 0m;

        return new CrmCampaignListItemDto
        {
            CampaignId = campaign.Id,
            Name = campaign.Name,
            CampaignType = campaign.CampaignType,
            CampaignStatus = campaign.CampaignStatus,
            StartDate = campaign.StartDate,
            EndDate = campaign.EndDate,
            Budget = campaign.Budget,
            ActualCost = campaign.ActualCost,
            ExpectedRevenue = campaign.ExpectedRevenue,
            ActualRevenue = campaign.ActualRevenue,
            RoiPercent = roiPercent,
            TargetAudience = campaign.TargetAudience,
            ActualAudience = actualAudience,
            ResponseCount = responseCount,
            ResponseRate = responseRate,
            LeadsGenerated = campaign.LeadsGenerated,
            OpportunitiesGenerated = campaign.OpportunitiesGenerated,
            MemberCount = memberCount,
            ActiveMemberCount = members.Count(x =>
                !string.Equals(x.MemberStatus, "Unsubscribed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(x.MemberStatus, "Bounced", StringComparison.OrdinalIgnoreCase)),
            RespondedMemberCount = respondedMemberCount,
            QualifiedLeadCount = relatedLeads.Count(x => string.Equals(x.LeadStatus, "Qualified", StringComparison.OrdinalIgnoreCase)),
            ConvertedLeadCount = relatedLeads.Count(x =>
                string.Equals(x.LeadStatus, "Converted", StringComparison.OrdinalIgnoreCase)
                || x.ConvertedCustomerId.HasValue),
            OpenOpportunityCount = openOpportunities.Count,
            WeightedPipelineValue = decimal.Round(openOpportunities.Sum(x => x.Amount * x.Probability / 100m), 2),
            InfluencedAccountCount = influencedAccountIds.Count,
            IsActive = IsActiveCampaign(campaign, now),
            IsEndingSoon = IsActiveCampaign(campaign, now)
                && campaign.EndDate.HasValue
                && campaign.EndDate.Value >= now.Date
                && campaign.EndDate.Value <= endingWindow,
            CreatedAt = campaign.CreatedAt
        };
    }

    private static CrmCampaignMemberDto MapCampaignMember(
        CampaignMember member,
        Lead? lead,
        IReadOnlyDictionary<Guid, string> businessPartnerNameLookup,
        IReadOnlyDictionary<Guid, List<Opportunity>> opportunitiesByLeadId)
    {
        var opportunities = lead == null
            ? new List<Opportunity>()
            : (opportunitiesByLeadId.GetValueOrDefault(lead.Id) ?? new List<Opportunity>());
        var openOpportunities = opportunities
            .Where(x => !IsClosedOpportunityStage(x.Stage))
            .ToList();
        var convertedBusinessPartnerId = lead?.ConvertedCustomerId ?? member.CustomerId;

        return new CrmCampaignMemberDto
        {
            MemberId = member.Id,
            LeadId = lead?.Id ?? member.LeadId ?? Guid.Empty,
            LeadName = lead == null ? "Unknown lead" : GetLeadFullName(lead),
            CompanyName = lead?.CompanyName,
            LeadStatus = lead?.LeadStatus ?? "Unknown",
            QualificationScore = lead?.QualificationScore ?? 0,
            EstimatedValue = lead?.EstimatedValue ?? 0m,
            ConvertedBusinessPartnerId = convertedBusinessPartnerId,
            ConvertedBusinessPartnerName = convertedBusinessPartnerId.HasValue
                ? businessPartnerNameLookup.GetValueOrDefault(convertedBusinessPartnerId.Value)
                : null,
            MemberStatus = member.MemberStatus,
            DateAdded = member.DateAdded,
            ResponseDate = member.ResponseDate,
            ResponseType = member.ResponseType,
            Notes = member.Notes,
            OpenOpportunityCount = openOpportunities.Count,
            WeightedPipelineValue = decimal.Round(openOpportunities.Sum(x => x.Amount * x.Probability / 100m), 2),
            NeedsFollowUp = lead != null && NeedsLeadFollowUp(lead, DateTime.UtcNow.AddDays(14))
        };
    }

    private static CrmCampaignInfluenceAccountDto MapCampaignInfluenceAccount(
        BusinessPartner partner,
        IReadOnlyCollection<Lead> relatedLeads,
        IReadOnlyCollection<Opportunity> relatedOpenOpportunities)
        => new()
        {
            BusinessPartnerId = partner.Id,
            PartnerCode = partner.PartnerCode,
            PartnerName = partner.PartnerName,
            PartnerType = partner.PartnerType,
            ConvertedLeadCount = relatedLeads.Count,
            OpenOpportunityCount = relatedOpenOpportunities.Count,
            WeightedPipelineValue = decimal.Round(relatedOpenOpportunities.Sum(x => x.Amount * x.Probability / 100m), 2)
        };

    private static CrmForecastDealDto MapForecastDeal(
        Opportunity opportunity,
        IReadOnlyDictionary<Guid, string> businessPartnerLookup,
        IReadOnlyDictionary<Guid, Lead> leadLookup,
        int quoteCount,
        IReadOnlyCollection<string>? campaignNames)
    {
        leadLookup.TryGetValue(opportunity.LeadId ?? Guid.Empty, out var lead);
        var resolvedBusinessPartnerId = opportunity.CustomerId ?? lead?.ConvertedCustomerId;
        var campaignList = campaignNames?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            ?? new List<string>();

        return new CrmForecastDealDto
        {
            OpportunityId = opportunity.Id,
            Name = opportunity.Name,
            Stage = opportunity.Stage,
            OpportunityType = opportunity.OpportunityType,
            BusinessPartnerId = resolvedBusinessPartnerId,
            BusinessPartnerName = resolvedBusinessPartnerId.HasValue
                ? businessPartnerLookup.GetValueOrDefault(resolvedBusinessPartnerId.Value)
                : lead?.CompanyName,
            LeadId = opportunity.LeadId,
            LeadName = lead == null ? null : GetLeadFullName(lead),
            Amount = opportunity.Amount,
            Currency = NormalizeCurrencyCode(opportunity.Currency, "USD"),
            Probability = opportunity.Probability,
            WeightedValue = decimal.Round(opportunity.Amount * opportunity.Probability / 100m, 2),
            ExpectedCloseDate = opportunity.ExpectedCloseDate,
            QuoteCount = quoteCount,
            CampaignCount = campaignList.Count,
            ForecastCategory = ResolveForecastCategory(opportunity.Probability),
            CampaignContext = campaignList.Count switch
            {
                0 => null,
                <= 3 => string.Join(", ", campaignList),
                _ => $"{string.Join(", ", campaignList.Take(3))} +{campaignList.Count - 3}"
            }
        };
    }

    private static Guid? ResolveOpportunityBusinessPartnerId(
        Opportunity opportunity,
        IReadOnlyCollection<Quote>? relatedQuotes,
        IReadOnlyDictionary<Guid, Lead> leadLookup)
    {
        if (opportunity.CustomerId.HasValue)
        {
            return opportunity.CustomerId.Value;
        }

        var quoteCustomerId = relatedQuotes?
            .Select(x => x.CustomerId)
            .FirstOrDefault(x => x.HasValue);
        if (quoteCustomerId.HasValue)
        {
            return quoteCustomerId.Value;
        }

        return opportunity.LeadId.HasValue && leadLookup.TryGetValue(opportunity.LeadId.Value, out var lead)
            ? lead.ConvertedCustomerId
            : null;
    }

    private static DateTime GetOpportunityConversionReferenceDate(Opportunity opportunity)
        => opportunity.ActualCloseDate ?? opportunity.ExpectedCloseDate;

    private static decimal ResolveQuoteValue(Quote quote)
    {
        var storedValue = quote.TotalAmount > 0m ? quote.TotalAmount : 0m;
        if (storedValue > 0m)
        {
            return storedValue;
        }

        return decimal.Round(quote.SubTotal - quote.DiscountAmount + quote.ShippingAmount, 2);
    }

    private static bool NeedsLeadFollowUp(Lead lead, DateTime followUpWindow)
        => !IsClosedLeadStatus(lead.LeadStatus)
            && lead.NextFollowUpDate.HasValue
            && lead.NextFollowUpDate.Value <= followUpWindow;

    private static bool NeedsActivityFollowUp(Activity activity, DateTime followUpWindow)
        => !IsClosedActivityStatus(activity.ActivityStatus)
            && ((activity.DueDate.HasValue && activity.DueDate.Value <= followUpWindow) || activity.RequiresFollowUp);

    private static bool IsClosedLeadStatus(string? status)
        => string.Equals(status, "Converted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Unqualified", StringComparison.OrdinalIgnoreCase);

    private static bool IsClosedOpportunityStage(string? stage)
        => string.Equals(stage, "Closed Won", StringComparison.OrdinalIgnoreCase)
            || string.Equals(stage, "Closed Lost", StringComparison.OrdinalIgnoreCase);

    private static bool IsClosedActivityStatus(string? status)
        => string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);

    private static bool IsClosedQuoteStatus(string? status)
        => string.Equals(status, "Accepted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Expired", StringComparison.OrdinalIgnoreCase);

    private static bool IsClosedCampaignStatus(string? status)
        => string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);

    private static bool IsActiveCampaign(Campaign campaign, DateTime now)
        => string.Equals(campaign.CampaignStatus, "Active", StringComparison.OrdinalIgnoreCase)
            && campaign.StartDate.Date <= now.Date
            && (!campaign.EndDate.HasValue || campaign.EndDate.Value.Date >= now.Date)
            && !IsClosedCampaignStatus(campaign.CampaignStatus);

    private static bool IsRespondedCampaignMember(CampaignMember member)
        => member.ResponseDate.HasValue
            || !string.IsNullOrWhiteSpace(member.ResponseType)
            || string.Equals(member.MemberStatus, "Responded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(member.MemberStatus, "Qualified", StringComparison.OrdinalIgnoreCase)
            || string.Equals(member.MemberStatus, "Converted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(member.MemberStatus, "Unsubscribed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(member.MemberStatus, "Bounced", StringComparison.OrdinalIgnoreCase);

    private static string ResolveForecastCategory(int probability)
        => probability switch
        {
            >= 80 => "Commit",
            >= 60 => "Likely",
            _ => "Upside"
        };

    private static string ResolveForecastCoverageCategory(
        DateTime? endDate,
        decimal contractValue,
        decimal renewalWeightedValue,
        DateTime now)
    {
        var coverageRatio = contractValue <= 0m ? 1m : renewalWeightedValue / contractValue;
        if (coverageRatio >= 1m)
        {
            return "Covered";
        }

        if (endDate.HasValue && endDate.Value <= now.AddDays(30))
        {
            return renewalWeightedValue <= 0m ? "Critical" : "Urgent";
        }

        if (coverageRatio >= 0.6m)
        {
            return "Watch";
        }

        return renewalWeightedValue <= 0m ? "Critical" : "Gap";
    }

    private static string ResolveConversionCoverageStatus(
        string? stage,
        int quoteCount,
        int contractCount,
        int projectCount)
    {
        if (projectCount > 0)
        {
            return "Project Live";
        }

        if (contractCount > 0)
        {
            return string.Equals(stage, "Closed Won", StringComparison.OrdinalIgnoreCase)
                ? "Closed Won"
                : "Contract Ready";
        }

        if (string.Equals(stage, "Closed Lost", StringComparison.OrdinalIgnoreCase))
        {
            return "Closed Lost";
        }

        if (quoteCount > 0)
        {
            return "Quoted";
        }

        return "Opportunity Only";
    }

    private static string? ResolveConversionJourneyLeakageReason(
        Opportunity opportunity,
        int quoteCount,
        int contractCount,
        int projectCount,
        DateTime now)
    {
        if (string.Equals(opportunity.Stage, "Closed Lost", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(opportunity.LossReason)
                ? "Opportunity was closed lost before downstream conversion."
                : $"Closed lost: {opportunity.LossReason}";
        }

        if (string.Equals(opportunity.Stage, "Closed Won", StringComparison.OrdinalIgnoreCase)
            && contractCount == 0)
        {
            return "Closed-won opportunity has not been converted into a contract yet.";
        }

        if (contractCount > 0 && projectCount == 0)
        {
            return "Contract exists, but no delivery project is linked yet.";
        }

        if (!IsClosedOpportunityStage(opportunity.Stage)
            && opportunity.ExpectedCloseDate <= now.AddDays(21)
            && quoteCount == 0)
        {
            return "Expected close is approaching, but no quote has been issued yet.";
        }

        if (!IsClosedOpportunityStage(opportunity.Stage)
            && opportunity.ExpectedCloseDate <= now.AddDays(21)
            && quoteCount > 0
            && contractCount == 0)
        {
            return "Quoted opportunity is close to expected close without contract conversion.";
        }

        return null;
    }

    private static bool IsActiveProject(Project project)
        => !string.Equals(project.Status, ProjectStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(project.Status, ProjectStatuses.Closed, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(project.Status, ProjectStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(project.Status, ProjectStatuses.Archived, StringComparison.OrdinalIgnoreCase);

    private static Guid? ResolveProjectBusinessPartnerId(Project project, IReadOnlyDictionary<Guid, Contract> contractLookup)
    {
        if (project.BusinessPartnerId.HasValue)
        {
            return project.BusinessPartnerId.Value;
        }

        return project.ContractId.HasValue && contractLookup.TryGetValue(project.ContractId.Value, out var linkedContract)
            ? linkedContract.BusinessPartnerId
            : null;
    }

    private static bool IsActiveContract(Contract contract)
        => string.Equals(contract.Status, "Active", StringComparison.OrdinalIgnoreCase)
            || string.Equals(contract.Status, "PendingSignature", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeTenderEntityType(string? entityType)
        => entityType?.Trim().ToLowerInvariant() switch
        {
            "invitation" => "Invitation",
            "bid" => "Bid",
            "award" => "Award",
            _ => null
        };

    private static bool IsTenderClosingSoon(Tender? tender, DateTime now)
        => tender?.SubmissionDeadline.HasValue == true
            && tender.SubmissionDeadline.Value <= now.AddDays(14)
            && !string.Equals(tender.Status, "Closed", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(tender.Status, "Awarded", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(tender.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsText(string? value, string search)
        => !string.IsNullOrWhiteSpace(value)
            && value.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static string GetLeadFullName(Lead lead)
        => $"{lead.FirstName} {lead.LastName}".Trim();

    private static string? CleanNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string CleanRequiredText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string NormalizeCurrencyCode(string? value, string fallback)
        => TryNormalizeCurrencyCode(value) ?? TryNormalizeCurrencyCode(fallback) ?? "USD";

    private static string ResolveCurrencyCode(string? primary, IEnumerable<string?>? fallbacks = null)
    {
        var normalizedPrimary = TryNormalizeCurrencyCode(primary);
        if (!string.IsNullOrWhiteSpace(normalizedPrimary))
        {
            return normalizedPrimary;
        }

        if (fallbacks != null)
        {
            foreach (var candidate in fallbacks)
            {
                var normalizedCandidate = TryNormalizeCurrencyCode(candidate);
                if (!string.IsNullOrWhiteSpace(normalizedCandidate))
                {
                    return normalizedCandidate;
                }
            }
        }

        return "USD";
    }

    private static string? TryNormalizeCurrencyCode(string? value)
    {
        var normalized = CleanNullable(value)?.ToUpperInvariant();
        return normalized is { Length: 3 } && normalized.All(char.IsLetter)
            ? normalized
            : null;
    }

    private static DateTime? ResolveActualCloseDate(string? stage, DateTime? actualCloseDate, DateTime now)
    {
        if (actualCloseDate.HasValue)
        {
            return actualCloseDate;
        }

        return IsClosedOpportunityStage(stage) ? now : null;
    }

    private static string ResolveLeadSource(string? leadSource, Lead? lead)
    {
        if (!string.IsNullOrWhiteSpace(leadSource))
        {
            return leadSource.Trim();
        }

        return lead?.LeadSource ?? "Unknown";
    }

    private static decimal CalculateRate(int numerator, int denominator)
        => denominator <= 0 ? 0m : decimal.Round(numerator * 100m / denominator, 1);

    private static int ClampTake(int take)
        => Math.Clamp(take, 3, 25);

    private static int ClampPageSize(int pageSize)
        => Math.Clamp(pageSize, 5, 100);

    private sealed class AccountHealthAssessment
    {
        public int Score { get; init; }
        public string Category { get; init; } = string.Empty;
        public List<CrmHealthSignalDto> Signals { get; init; } = new();
        public bool HasOpenFollowUp { get; init; }
        public bool IsAtRisk { get; init; }
        public DateTime? NextMilestoneDate { get; init; }
    }

    private sealed class AccountReadinessAssessment
    {
        public int Score { get; init; }
        public string Category { get; init; } = string.Empty;
        public List<CrmReadinessSignalDto> Signals { get; init; } = new();
        public bool HasCriticalGap { get; init; }
        public int DocumentCount { get; init; }
        public int VerifiedDocumentCount { get; init; }
        public int ExpiringDocumentCount { get; init; }
        public int ExpiredDocumentCount { get; init; }
        public int LicenseCount { get; init; }
        public int ExpiringLicenseCount { get; init; }
        public int ExpiredLicenseCount { get; init; }
        public int FinancialRecordCount { get; init; }
        public int? LatestFinancialYear { get; init; }
        public decimal? LatestAnnualRevenue { get; init; }
        public string? CreditRating { get; init; }
        public DateTime? NextComplianceDate { get; init; }
    }

    private sealed class AccountRiskAssessment
    {
        public int Score { get; init; }
        public string Category { get; init; } = string.Empty;
        public List<CrmRiskSignalDto> Signals { get; init; } = new();
        public int OpenIncidentCount { get; init; }
        public int CriticalIncidentCount { get; init; }
        public int PendingAppealCount { get; init; }
        public int OpenReviewFollowUpCount { get; init; }
        public decimal? LatestMetricScore { get; init; }
        public string? LatestMetricGrade { get; init; }
        public string? LatestMetricPeriod { get; init; }
        public DateTime? LatestMetricCalculatedAt { get; init; }
        public DateTime? LatestReviewDate { get; init; }
        public bool RequiresEscalation { get; init; }
    }

    private sealed class AccountCollaborationAssessment
    {
        public int Score { get; init; }
        public string Category { get; init; } = string.Empty;
        public List<CrmCollaborationSignalDto> Signals { get; init; } = new();
        public string? LatestApplicationNumber { get; init; }
        public string? LatestRegistrationLifecycleStatus { get; init; }
        public DateTime? LatestRegistrationSubmittedDate { get; init; }
        public DateTime? LatestRegistrationApprovedDate { get; init; }
        public int RegistrationDocumentCount { get; init; }
        public int PortalUserCount { get; init; }
        public int ActivePortalUserCount { get; init; }
        public int AdminUserCount { get; init; }
        public int TenderAssignmentCount { get; init; }
        public int AssignedTenderCount { get; init; }
        public int PortalProjectCount { get; init; }
        public int CollaborationProjectCount { get; init; }
        public bool RequiresEnablement { get; init; }
    }

    private sealed class AccountServiceAssessment
    {
        public int Score { get; init; }
        public string Category { get; init; } = string.Empty;
        public List<CrmServiceSignalDto> Signals { get; init; } = new();
        public int PortalUserCount { get; init; }
        public int ActivePortalUserCount { get; init; }
        public int TicketCount { get; init; }
        public int OpenTicketCount { get; init; }
        public int OverdueTicketCount { get; init; }
        public int ComplaintTicketCount { get; init; }
        public int HelpdeskTicketCount { get; init; }
        public int EnquiryTicketCount { get; init; }
        public int LinkedProblemCount { get; init; }
        public int OpenProblemCount { get; init; }
        public int ResolvedTicketCount30Days { get; init; }
        public decimal? AverageFeedbackRating { get; init; }
        public int FeedbackResponseCount { get; init; }
        public bool RequiresAttention { get; init; }
        public bool HasSlaBreachRisk { get; init; }
        public DateTime? LastTicketCreatedAt { get; init; }
        public DateTime? LastResolvedAt { get; init; }
    }
}
