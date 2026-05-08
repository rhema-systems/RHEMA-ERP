using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Data;

namespace ErpSystem.Api.Services.Sales;

/// <summary>
/// Business logic for Sales Agreement lifecycle.
/// </summary>
public class SalesAgreementService : ISalesAgreementService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SalesAgreementService> _logger;
    private readonly ICurrentUserService _currentUserService;

    public SalesAgreementService(
        ApplicationDbContext context,
        ILogger<SalesAgreementService> logger,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _logger = logger;
        _currentUserService = currentUserService;
    }

    // ── CRUD ─────────────────────────────────────────────────────────────

    public async Task<SalesAgreementDetailDto> GetByIdAsync(Guid id)
    {
        var agreement = await _context.Set<SalesAgreement>()
            .Include(a => a.BusinessPartner)
            .Include(a => a.SalesRep)
            .Include(a => a.ApprovedBy)
            .Include(a => a.TerminatedBy)
            .Include(a => a.Lines).ThenInclude(l => l.Product)
            .Include(a => a.Milestones)
            .Include(a => a.Renewals).ThenInclude(r => r.RenewedBy)
            .Include(a => a.Documents).ThenInclude(d => d.UploadedBy)
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new KeyNotFoundException($"Sales Agreement {id} not found");

        var projectUnitContext = await GetProjectUnitContextAsync(agreement.Id, agreement.TenantId);
        return MapToDetail(agreement, projectUnitContext);
    }

    public async Task<(List<SalesAgreementSummaryDto> Items, int TotalCount)> GetAllAsync(
        int page = 1, int pageSize = 20,
        string? search = null, string? status = null, string? agreementType = null,
        Guid? customerId = null, DateTime? startDateFrom = null, DateTime? startDateTo = null,
        bool projectLinkedOnly = false, bool releasedUnitsOnly = false)
    {
        var query = _context.Set<SalesAgreement>()
            .Include(a => a.BusinessPartner)
            .Include(a => a.SalesRep)
            .Include(a => a.Milestones)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(a =>
                a.DocumentNumber.ToLower().Contains(s) ||
                a.AgreementTitle.ToLower().Contains(s) ||
                a.CustomerName.ToLower().Contains(s) ||
                (a.PropertyReference != null && a.PropertyReference.ToLower().Contains(s)) ||
                _context.Set<ProjectUnit>().Any(unit =>
                    unit.TenantId == a.TenantId
                    && unit.SalesAgreementId == a.Id
                    && !unit.IsDeleted
                    && (
                        (unit.Code != null && unit.Code.ToLower().Contains(s))
                        || unit.Name.ToLower().Contains(s)
                        || unit.Project.ProjectCode.ToLower().Contains(s)
                        || unit.Project.Title.ToLower().Contains(s))));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SalesAgreementStatus>(status, out var statusEnum))
            query = query.Where(a => a.AgreementStatus == statusEnum);

        if (!string.IsNullOrWhiteSpace(agreementType) && Enum.TryParse<SalesAgreementType>(agreementType, out var typeEnum))
            query = query.Where(a => a.AgreementType == typeEnum);

        if (customerId.HasValue)
            query = query.Where(a => a.BusinessPartnerId == customerId.Value);

        if (startDateFrom.HasValue)
            query = query.Where(a => a.StartDate >= startDateFrom.Value);

        if (startDateTo.HasValue)
            query = query.Where(a => a.StartDate <= startDateTo.Value);

        if (projectLinkedOnly)
            query = query.Where(a => _context.Set<ProjectUnit>().Any(unit =>
                unit.TenantId == a.TenantId
                && unit.SalesAgreementId == a.Id
                && !unit.IsDeleted));

        if (releasedUnitsOnly)
            query = query.Where(a => _context.Set<ProjectUnit>().Any(unit =>
                unit.TenantId == a.TenantId
                && unit.SalesAgreementId == a.Id
                && unit.IsReleasedForMarket
                && !unit.IsDeleted));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var projectUnitContextLookup = await GetProjectUnitContextsByAgreementIdsAsync(
            items.Select(item => item.Id),
            items.FirstOrDefault()?.TenantId);

        var summaries = items.Select(item => new SalesAgreementSummaryDto
        {
            Id = item.Id,
            DocumentNumber = item.DocumentNumber,
            AgreementTitle = item.AgreementTitle,
            CustomerName = item.CustomerName,
            BusinessPartnerId = item.BusinessPartnerId,
            AgreementType = item.AgreementType.ToString(),
            AgreementStatus = item.AgreementStatus.ToString(),
            StartDate = item.StartDate,
            EndDate = item.EndDate,
            AgreedValue = item.AgreedValue,
            UtilizedValue = item.UtilizedValue,
            Currency = item.Currency,
            PropertyReference = item.PropertyReference,
            SalesRepName = item.SalesRep != null ? item.SalesRep.FirstName + " " + item.SalesRep.LastName : null,
            AutoRenew = item.AutoRenew,
            CompletedMilestones = item.Milestones.Count(m => m.Status == "Completed" || m.Status == "Paid"),
            TotalMilestones = item.Milestones.Count,
            CreatedAt = item.CreatedAt,
            ProjectUnitContext = projectUnitContextLookup.TryGetValue(item.Id, out var context) ? context : null
        }).ToList();

        return (summaries, totalCount);
    }

    public async Task<SalesAgreementDetailDto> CreateAsync(CreateSalesAgreementDto dto)
    {
        var bp = await _context.Set<BusinessPartner>().FindAsync(dto.BusinessPartnerId)
            ?? throw new ArgumentException("Business Partner not found");
        var tenantId = await ResolveAgreementTenantIdAsync(bp);
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Cannot create sales agreement because no valid tenant could be resolved for the selected customer.");
        }

        var agreement = new SalesAgreement
        {
            Id = Guid.NewGuid(),
            DocumentNumber = await GenerateDocumentNumberAsync(),
            BusinessPartnerId = dto.BusinessPartnerId,
            CustomerName = bp.PartnerName ?? bp.PartnerCode,
            AgreementTitle = dto.AgreementTitle,
            AgreementType = Enum.TryParse<SalesAgreementType>(dto.AgreementType, out var at) ? at : SalesAgreementType.General,
            AgreementStatus = SalesAgreementStatus.Draft,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            ExpiryWarningDays = dto.ExpiryWarningDays,
            AutoRenew = dto.AutoRenew,
            RenewalPeriodMonths = dto.RenewalPeriodMonths,
            AgreedValue = dto.AgreedValue,
            MinimumCommitment = dto.MinimumCommitment,
            MaximumCommitment = dto.MaximumCommitment,
            Currency = dto.Currency,
            DiscountPercentage = dto.DiscountPercentage,
            PricingTerms = dto.PricingTerms,
            PaymentSchedule = dto.PaymentSchedule,
            PropertyReference = dto.PropertyReference,
            PropertyType = Enum.TryParse<PropertyType>(dto.PropertyType, out var pt) ? pt : null,
            PropertyDescription = dto.PropertyDescription,
            PropertyLocation = dto.PropertyLocation,
            SalesRepId = dto.SalesRepId,
            Notes = dto.Notes,
            InternalNotes = dto.InternalNotes,
            TermsAndConditions = dto.TermsAndConditions,
            TenantId = tenantId,
        };

        // Lines
        if (dto.Lines?.Any() == true)
        {
            int lineNum = 1;
            foreach (var l in dto.Lines)
            {
                agreement.Lines.Add(new SalesAgreementLine
                {
                    Id = Guid.NewGuid(),
                    SalesAgreementId = agreement.Id,
                    LineNumber = lineNum++,
                    ProductId = l.ProductId,
                    Description = l.Description,
                    ProductCode = l.ProductCode,
                    AgreedPrice = l.AgreedPrice,
                    MinimumQuantity = l.MinimumQuantity,
                    MaximumQuantity = l.MaximumQuantity,
                    Unit = l.Unit,
                    DiscountPercentage = l.DiscountPercentage,
                    DiscountTiersJson = l.DiscountTiersJson,
                    Notes = l.Notes,
                    TenantId = tenantId,
                });
            }
        }

        // Milestones
        if (dto.Milestones?.Any() == true)
        {
            int seq = 1;
            foreach (var m in dto.Milestones)
            {
                agreement.Milestones.Add(new SalesAgreementMilestone
                {
                    Id = Guid.NewGuid(),
                    SalesAgreementId = agreement.Id,
                    SequenceNumber = seq++,
                    MilestoneName = m.MilestoneName,
                    Description = m.Description,
                    PaymentPercentage = m.PaymentPercentage,
                    PaymentAmount = m.PaymentAmount,
                    DueDate = m.DueDate,
                    Notes = m.Notes,
                    Status = "Pending",
                    TenantId = tenantId,
                });
            }
        }

        _context.Set<SalesAgreement>().Add(agreement);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created Sales Agreement {Number}", agreement.DocumentNumber);

        return await GetByIdAsync(agreement.Id);
    }

    private async Task<Guid> ResolveAgreementTenantIdAsync(BusinessPartner businessPartner)
    {
        var candidateTenantIds = new List<Guid>();
        if (businessPartner.TenantId != Guid.Empty)
        {
            candidateTenantIds.Add(businessPartner.TenantId);
        }

        if (_currentUserService.TenantId.HasValue && _currentUserService.TenantId.Value != Guid.Empty)
        {
            candidateTenantIds.Add(_currentUserService.TenantId.Value);
        }

        candidateTenantIds = candidateTenantIds.Distinct().ToList();
        if (candidateTenantIds.Count == 0)
        {
            return Guid.Empty;
        }

        var validTenantIds = await _context.Set<Tenant>()
            .Where(x => candidateTenantIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();

        if (businessPartner.TenantId != Guid.Empty && validTenantIds.Contains(businessPartner.TenantId))
        {
            return businessPartner.TenantId;
        }

        if (_currentUserService.TenantId.HasValue && validTenantIds.Contains(_currentUserService.TenantId.Value))
        {
            _logger.LogWarning(
                "Falling back to current user tenant {TenantId} while creating sales agreement for business partner {BusinessPartnerId} because partner tenant {BusinessPartnerTenantId} is not valid.",
                _currentUserService.TenantId.Value,
                businessPartner.Id,
                businessPartner.TenantId);
            return _currentUserService.TenantId.Value;
        }

        return Guid.Empty;
    }

    public async Task<SalesAgreementDetailDto> UpdateAsync(Guid id, UpdateSalesAgreementDto dto)
    {
        var agreement = await _context.Set<SalesAgreement>().FindAsync(id)
            ?? throw new KeyNotFoundException($"Sales Agreement {id} not found");

        if (agreement.AgreementStatus != SalesAgreementStatus.Draft)
            throw new InvalidOperationException("Only Draft agreements can be edited");

        if (dto.AgreementTitle != null) agreement.AgreementTitle = dto.AgreementTitle;
        if (dto.EndDate.HasValue) agreement.EndDate = dto.EndDate;
        if (dto.ExpiryWarningDays.HasValue) agreement.ExpiryWarningDays = dto.ExpiryWarningDays.Value;
        if (dto.AutoRenew.HasValue) agreement.AutoRenew = dto.AutoRenew.Value;
        if (dto.RenewalPeriodMonths.HasValue) agreement.RenewalPeriodMonths = dto.RenewalPeriodMonths;
        if (dto.AgreedValue.HasValue) agreement.AgreedValue = dto.AgreedValue.Value;
        if (dto.MinimumCommitment.HasValue) agreement.MinimumCommitment = dto.MinimumCommitment.Value;
        if (dto.MaximumCommitment.HasValue) agreement.MaximumCommitment = dto.MaximumCommitment.Value;
        if (dto.DiscountPercentage.HasValue) agreement.DiscountPercentage = dto.DiscountPercentage;
        if (dto.PricingTerms != null) agreement.PricingTerms = dto.PricingTerms;
        if (dto.PaymentSchedule != null) agreement.PaymentSchedule = dto.PaymentSchedule;
        if (dto.PropertyReference != null) agreement.PropertyReference = dto.PropertyReference;
        if (dto.PropertyType != null && Enum.TryParse<PropertyType>(dto.PropertyType, out var pt))
            agreement.PropertyType = pt;
        if (dto.PropertyDescription != null) agreement.PropertyDescription = dto.PropertyDescription;
        if (dto.PropertyLocation != null) agreement.PropertyLocation = dto.PropertyLocation;
        if (dto.SalesRepId.HasValue) agreement.SalesRepId = dto.SalesRepId;
        if (dto.Notes != null) agreement.Notes = dto.Notes;
        if (dto.InternalNotes != null) agreement.InternalNotes = dto.InternalNotes;
        if (dto.TermsAndConditions != null) agreement.TermsAndConditions = dto.TermsAndConditions;

        await SyncLinkedProjectUnitsAsync(agreement);
        await _context.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────

    public async Task<SalesAgreementDetailDto> SubmitForApprovalAsync(Guid id)
    {
        var agreement = await _context.Set<SalesAgreement>().FindAsync(id)
            ?? throw new KeyNotFoundException($"Sales Agreement {id} not found");

        if (agreement.AgreementStatus != SalesAgreementStatus.Draft)
            throw new InvalidOperationException("Only Draft agreements can be submitted for approval");

        agreement.AgreementStatus = SalesAgreementStatus.PendingApproval;
        await SyncLinkedProjectUnitsAsync(agreement);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Sales Agreement {Number} submitted for approval", agreement.DocumentNumber);

        return await GetByIdAsync(id);
    }

    public async Task<SalesAgreementDetailDto> ProcessApprovalAsync(Guid id, SalesAgreementApprovalDto dto)
    {
        var agreement = await _context.Set<SalesAgreement>().FindAsync(id)
            ?? throw new KeyNotFoundException($"Sales Agreement {id} not found");

        if (agreement.AgreementStatus != SalesAgreementStatus.PendingApproval)
            throw new InvalidOperationException("Only PendingApproval agreements can be approved/rejected");

        if (dto.IsApproved)
        {
            agreement.AgreementStatus = SalesAgreementStatus.Active;
            agreement.ApprovedDate = DateTime.UtcNow;
            agreement.ApprovalComments = dto.Comments;
            _logger.LogInformation("Sales Agreement {Number} approved", agreement.DocumentNumber);
        }
        else
        {
            agreement.AgreementStatus = SalesAgreementStatus.Draft; // Rejected → back to Draft for revision
            agreement.ApprovalComments = dto.Comments;
            _logger.LogInformation("Sales Agreement {Number} rejected", agreement.DocumentNumber);
        }

        await SyncLinkedProjectUnitsAsync(agreement);
        await _context.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<SalesAgreementDetailDto> ActivateAsync(Guid id)
    {
        var agreement = await _context.Set<SalesAgreement>().FindAsync(id)
            ?? throw new KeyNotFoundException($"Sales Agreement {id} not found");

        if (agreement.AgreementStatus != SalesAgreementStatus.PendingApproval &&
            agreement.AgreementStatus != SalesAgreementStatus.Suspended)
            throw new InvalidOperationException("Agreement cannot be activated from current status");

        agreement.AgreementStatus = SalesAgreementStatus.Active;
        await SyncLinkedProjectUnitsAsync(agreement);
        await _context.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<SalesAgreementDetailDto> SuspendAsync(Guid id, string? reason = null)
    {
        var agreement = await _context.Set<SalesAgreement>().FindAsync(id)
            ?? throw new KeyNotFoundException($"Sales Agreement {id} not found");

        if (agreement.AgreementStatus != SalesAgreementStatus.Active)
            throw new InvalidOperationException("Only Active agreements can be suspended");

        agreement.AgreementStatus = SalesAgreementStatus.Suspended;
        if (reason != null) agreement.InternalNotes = $"{agreement.InternalNotes}\n[Suspended] {reason}".Trim();
        await SyncLinkedProjectUnitsAsync(agreement);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Sales Agreement {Number} suspended", agreement.DocumentNumber);

        return await GetByIdAsync(id);
    }

    public async Task<SalesAgreementDetailDto> ResumeAsync(Guid id)
    {
        var agreement = await _context.Set<SalesAgreement>().FindAsync(id)
            ?? throw new KeyNotFoundException($"Sales Agreement {id} not found");

        if (agreement.AgreementStatus != SalesAgreementStatus.Suspended)
            throw new InvalidOperationException("Only Suspended agreements can be resumed");

        agreement.AgreementStatus = SalesAgreementStatus.Active;
        await SyncLinkedProjectUnitsAsync(agreement);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Sales Agreement {Number} resumed", agreement.DocumentNumber);

        return await GetByIdAsync(id);
    }

    public async Task<SalesAgreementDetailDto> TerminateAsync(Guid id, TerminateAgreementDto dto)
    {
        var agreement = await _context.Set<SalesAgreement>().FindAsync(id)
            ?? throw new KeyNotFoundException($"Sales Agreement {id} not found");

        if (agreement.AgreementStatus == SalesAgreementStatus.Terminated ||
            agreement.AgreementStatus == SalesAgreementStatus.Expired)
            throw new InvalidOperationException("Agreement is already terminated or expired");

        agreement.AgreementStatus = SalesAgreementStatus.Terminated;
        agreement.TerminatedDate = DateTime.UtcNow;
        agreement.TerminationReason = dto.Reason;
        await SyncLinkedProjectUnitsAsync(agreement);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Sales Agreement {Number} terminated: {Reason}", agreement.DocumentNumber, dto.Reason);

        return await GetByIdAsync(id);
    }

    public async Task<SalesAgreementDetailDto> RenewAsync(Guid id, RenewAgreementDto dto)
    {
        var agreement = await _context.Set<SalesAgreement>()
            .Include(a => a.Renewals)
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new KeyNotFoundException($"Sales Agreement {id} not found");

        if (agreement.AgreementStatus != SalesAgreementStatus.Active &&
            agreement.AgreementStatus != SalesAgreementStatus.Expiring)
            throw new InvalidOperationException("Only Active or Expiring agreements can be renewed");

        // Record renewal history
        var renewal = new SalesAgreementRenewal
        {
            Id = Guid.NewGuid(),
            SalesAgreementId = agreement.Id,
            RenewalNumber = agreement.Renewals.Count + 1,
            PreviousStartDate = agreement.StartDate,
            PreviousEndDate = agreement.EndDate ?? DateTime.UtcNow,
            NewStartDate = agreement.EndDate ?? DateTime.UtcNow,
            NewEndDate = dto.NewEndDate,
            PreviousValue = agreement.AgreedValue,
            NewValue = dto.NewValue ?? agreement.AgreedValue,
            PriceChangePercentage = dto.NewValue.HasValue && agreement.AgreedValue > 0
                ? ((dto.NewValue.Value - agreement.AgreedValue) / agreement.AgreedValue) * 100
                : null,
            RenewalTerms = dto.RenewalTerms,
            Notes = dto.Notes,
            RenewedDate = DateTime.UtcNow,
        };

        agreement.Renewals.Add(renewal);

        // Update the agreement
        agreement.StartDate = renewal.NewStartDate;
        agreement.EndDate = dto.NewEndDate;
        if (dto.NewValue.HasValue) agreement.AgreedValue = dto.NewValue.Value;
        agreement.AgreementStatus = SalesAgreementStatus.Active;
        agreement.UtilizedValue = 0; // Reset utilization for new period

        await SyncLinkedProjectUnitsAsync(agreement);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Sales Agreement {Number} renewed to {EndDate}", agreement.DocumentNumber, dto.NewEndDate);

        return await GetByIdAsync(id);
    }

    // ── Milestones ──────────────────────────────────────────────────────

    public async Task<SalesAgreementMilestoneDto> UpdateMilestoneStatusAsync(Guid milestoneId, UpdateMilestoneStatusDto dto)
    {
        var milestone = await _context.Set<SalesAgreementMilestone>().FindAsync(milestoneId)
            ?? throw new KeyNotFoundException($"Milestone {milestoneId} not found");

        milestone.Status = dto.Status;
        if (dto.Status == "Completed") milestone.CompletedDate = dto.ActualDate ?? DateTime.UtcNow;
        if (dto.Status == "Paid") milestone.PaidDate = dto.ActualDate ?? DateTime.UtcNow;
        if (dto.InvoiceNumber != null) milestone.InvoiceNumber = dto.InvoiceNumber;
        if (dto.Notes != null) milestone.Notes = dto.Notes;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Milestone {Name} updated to {Status}", milestone.MilestoneName, dto.Status);

        return new SalesAgreementMilestoneDto
        {
            Id = milestone.Id,
            SequenceNumber = milestone.SequenceNumber,
            MilestoneName = milestone.MilestoneName,
            Description = milestone.Description,
            PaymentPercentage = milestone.PaymentPercentage,
            PaymentAmount = milestone.PaymentAmount,
            DueDate = milestone.DueDate,
            CompletedDate = milestone.CompletedDate,
            PaidDate = milestone.PaidDate,
            Status = milestone.Status,
            InvoiceNumber = milestone.InvoiceNumber,
            Notes = milestone.Notes,
        };
    }

    // ── Queries ─────────────────────────────────────────────────────────

    public async Task<List<SalesAgreementSummaryDto>> GetExpiringAgreementsAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        var (items, _) = await GetAllAsync(1, 100);
        return items.Where(a =>
            a.AgreementStatus == "Active" &&
            a.EndDate.HasValue &&
            a.EndDate.Value <= cutoff).ToList();
    }

    public async Task<List<SalesAgreementSummaryDto>> GetByCustomerAsync(Guid businessPartnerId)
    {
        var (items, _) = await GetAllAsync(1, 100, customerId: businessPartnerId);
        return items;
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private async Task<string> GenerateDocumentNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var count = await _context.Set<SalesAgreement>()
            .CountAsync(a => a.CreatedAt.Year == year);
        return $"SA-{year}-{(count + 1):D5}";
    }

    private async Task SyncLinkedProjectUnitsAsync(SalesAgreement agreement)
    {
        var linkedUnits = await _context.Set<ProjectUnit>()
            .Where(unit => unit.TenantId == agreement.TenantId && unit.SalesAgreementId == agreement.Id && !unit.IsDeleted)
            .ToListAsync();

        if (linkedUnits.Count == 0)
        {
            return;
        }

        var linkedOrderIds = linkedUnits
            .Where(unit => unit.SalesOrderId.HasValue)
            .Select(unit => unit.SalesOrderId!.Value)
            .Distinct()
            .ToList();

        var linkedOrders = linkedOrderIds.Count == 0
            ? new Dictionary<Guid, SalesOrder>()
            : await _context.Set<SalesOrder>()
                .Where(order => order.TenantId == agreement.TenantId && linkedOrderIds.Contains(order.Id))
                .ToDictionaryAsync(order => order.Id);

        foreach (var unit in linkedUnits)
        {
            linkedOrders.TryGetValue(unit.SalesOrderId ?? Guid.Empty, out var linkedOrder);
            var nextStatus = ProjectUnitSalesSyncRules.ResolveStatusFromAgreement(
                unit.Status,
                unit.IsReleasedForMarket,
                agreement.AgreementType,
                agreement.AgreementStatus,
                linkedOrder?.OrderType,
                linkedOrder?.OrderStatus);

            if (!string.Equals(unit.Status, nextStatus, StringComparison.OrdinalIgnoreCase))
            {
                unit.Status = nextStatus;
            }
        }
    }

    private async Task<SalesLinkedProjectUnitContextDto?> GetProjectUnitContextAsync(Guid salesAgreementId, Guid tenantId)
    {
        var linkedUnit = await _context.Set<ProjectUnit>()
            .Include(unit => unit.Project)
            .Include(unit => unit.SalesAgreement)
            .Include(unit => unit.SalesOrder)
            .FirstOrDefaultAsync(unit =>
                unit.TenantId == tenantId
                && unit.SalesAgreementId == salesAgreementId
                && !unit.IsDeleted);

        return linkedUnit == null
            ? null
            : ProjectUnitPresentationRules.BuildSalesLinkedProjectUnitContext(linkedUnit);
    }

    private async Task<Dictionary<Guid, SalesLinkedProjectUnitContextDto>> GetProjectUnitContextsByAgreementIdsAsync(
        IEnumerable<Guid> salesAgreementIds,
        Guid? tenantId)
    {
        var distinctIds = salesAgreementIds.Distinct().ToList();
        if (distinctIds.Count == 0 || !tenantId.HasValue)
        {
            return new Dictionary<Guid, SalesLinkedProjectUnitContextDto>();
        }

        var linkedUnits = await _context.Set<ProjectUnit>()
            .Include(unit => unit.Project)
            .Include(unit => unit.SalesAgreement)
            .Include(unit => unit.SalesOrder)
            .Where(unit =>
                unit.TenantId == tenantId.Value
                && unit.SalesAgreementId.HasValue
                && distinctIds.Contains(unit.SalesAgreementId.Value)
                && !unit.IsDeleted)
            .ToListAsync();

        return linkedUnits
            .GroupBy(unit => unit.SalesAgreementId!.Value)
            .ToDictionary(
                group => group.Key,
                group => ProjectUnitPresentationRules.BuildSalesLinkedProjectUnitContext(group.First()));
    }

    private static SalesAgreementDetailDto MapToDetail(SalesAgreement a, SalesLinkedProjectUnitContextDto? projectUnitContext = null)
    {
        return new SalesAgreementDetailDto
        {
            Id = a.Id,
            DocumentNumber = a.DocumentNumber,
            AgreementTitle = a.AgreementTitle,
            BusinessPartnerId = a.BusinessPartnerId,
            CustomerName = a.CustomerName,
            AgreementType = a.AgreementType.ToString(),
            AgreementStatus = a.AgreementStatus.ToString(),
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            ExpiryWarningDays = a.ExpiryWarningDays,
            AutoRenew = a.AutoRenew,
            RenewalPeriodMonths = a.RenewalPeriodMonths,
            AgreedValue = a.AgreedValue,
            MinimumCommitment = a.MinimumCommitment,
            MaximumCommitment = a.MaximumCommitment,
            UtilizedValue = a.UtilizedValue,
            Currency = a.Currency,
            DiscountPercentage = a.DiscountPercentage,
            PricingTerms = a.PricingTerms,
            PaymentSchedule = a.PaymentSchedule,
            PropertyReference = a.PropertyReference,
            PropertyType = a.PropertyType?.ToString(),
            PropertyDescription = a.PropertyDescription,
            PropertyLocation = a.PropertyLocation,
            SalesRepId = a.SalesRepId,
            SalesRepName = a.SalesRep != null ? $"{a.SalesRep.FirstName} {a.SalesRep.LastName}" : null,
            ApprovedByName = a.ApprovedBy != null ? $"{a.ApprovedBy.FirstName} {a.ApprovedBy.LastName}" : null,
            ApprovedDate = a.ApprovedDate,
            ApprovalComments = a.ApprovalComments,
            TerminatedDate = a.TerminatedDate,
            TerminationReason = a.TerminationReason,
            TerminatedByName = a.TerminatedBy != null ? $"{a.TerminatedBy.FirstName} {a.TerminatedBy.LastName}" : null,
            Notes = a.Notes,
            InternalNotes = a.InternalNotes,
            TermsAndConditions = a.TermsAndConditions,
            CreatedAt = a.CreatedAt,
            ModifiedAt = a.UpdatedAt,
            Lines = a.Lines.OrderBy(l => l.LineNumber).Select(l => new SalesAgreementLineDto
            {
                Id = l.Id, LineNumber = l.LineNumber, ProductId = l.ProductId,
                Description = l.Description, ProductCode = l.ProductCode,
                AgreedPrice = l.AgreedPrice, MinimumQuantity = l.MinimumQuantity,
                MaximumQuantity = l.MaximumQuantity, UtilizedQuantity = l.UtilizedQuantity,
                Unit = l.Unit, DiscountPercentage = l.DiscountPercentage,
                DiscountTiersJson = l.DiscountTiersJson, Notes = l.Notes,
            }).ToList(),
            Milestones = a.Milestones.OrderBy(m => m.SequenceNumber).Select(m => new SalesAgreementMilestoneDto
            {
                Id = m.Id, SequenceNumber = m.SequenceNumber, MilestoneName = m.MilestoneName,
                Description = m.Description, PaymentPercentage = m.PaymentPercentage,
                PaymentAmount = m.PaymentAmount, DueDate = m.DueDate,
                CompletedDate = m.CompletedDate, PaidDate = m.PaidDate,
                Status = m.Status, InvoiceNumber = m.InvoiceNumber, Notes = m.Notes,
            }).ToList(),
            Renewals = a.Renewals.OrderByDescending(r => r.RenewedDate).Select(r => new SalesAgreementRenewalDto
            {
                Id = r.Id, RenewalNumber = r.RenewalNumber,
                PreviousStartDate = r.PreviousStartDate, PreviousEndDate = r.PreviousEndDate,
                NewStartDate = r.NewStartDate, NewEndDate = r.NewEndDate,
                PreviousValue = r.PreviousValue, NewValue = r.NewValue,
                PriceChangePercentage = r.PriceChangePercentage,
                RenewalTerms = r.RenewalTerms, Notes = r.Notes,
                RenewedByName = r.RenewedBy != null ? $"{r.RenewedBy.FirstName} {r.RenewedBy.LastName}" : null,
                RenewedDate = r.RenewedDate,
            }).ToList(),
            Documents = a.Documents.OrderByDescending(d => d.CreatedAt).Select(d => new SalesAgreementDocumentDto
            {
                Id = d.Id, FileName = d.FileName, FilePath = d.FilePath,
                ContentType = d.ContentType, FileSize = d.FileSize,
                DocumentType = d.DocumentType, Description = d.Description,
                UploadedByName = d.UploadedBy != null ? $"{d.UploadedBy.FirstName} {d.UploadedBy.LastName}" : null,
                CreatedAt = d.CreatedAt,
            }).ToList(),
            ProjectUnitContext = projectUnitContext
        };
    }
}
