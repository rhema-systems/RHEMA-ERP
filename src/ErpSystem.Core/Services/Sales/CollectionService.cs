using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Sales;

public class CollectionService : ICollectionService
{
    private readonly IGenericRepository<CollectionActivity> _activityRepo;
    private readonly IGenericRepository<PaymentPlan> _planRepo;
    private readonly IGenericRepository<PaymentPlanInstallment> _installmentRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<CollectionService> _logger;

    public CollectionService(
        IGenericRepository<CollectionActivity> activityRepo,
        IGenericRepository<PaymentPlan> planRepo,
        IGenericRepository<PaymentPlanInstallment> installmentRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<CollectionService> logger)
    {
        _activityRepo = activityRepo;
        _planRepo = planRepo;
        _installmentRepo = installmentRepo;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // ═════════════════════════════════════
    //  COLLECTION ACTIVITIES
    // ═════════════════════════════════════

    public async Task<CollectionActivityDetailDto> CreateActivityAsync(CreateCollectionActivityDto dto)
    {
        var activity = new CollectionActivity
        {
            Subject = dto.Subject,
            ActivityType = dto.ActivityType,
            Description = dto.Description,
            ActivityDate = dto.ActivityDate ?? DateTime.UtcNow,
            FollowUpDate = dto.FollowUpDate,
            CollectionStatus = "Pending",
            OutstandingAmount = dto.OutstandingAmount,
            CustomerId = dto.CustomerId,
            InvoiceId = dto.InvoiceId,
            AssignedToId = dto.AssignedToId ?? _currentUserProvider.UserId,
            Notes = dto.Notes,
            TenantId = _currentUserProvider.TenantId
        };

        await _activityRepo.AddAsync(activity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created collection activity: {Subject} for customer {CustomerId}", activity.Subject, activity.CustomerId);
        return await GetActivityByIdAsync(activity.Id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<CollectionActivityDetailDto> UpdateActivityAsync(Guid id, UpdateCollectionActivityDto dto)
    {
        var activity = await _activityRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Collection activity {id} not found");

        if (dto.CollectionStatus != null) activity.CollectionStatus = dto.CollectionStatus;
        if (dto.Outcome != null) activity.Outcome = dto.Outcome;
        if (dto.PromisedAmount.HasValue) activity.PromisedAmount = dto.PromisedAmount.Value;
        if (dto.PromisedPayDate.HasValue) activity.PromisedPayDate = dto.PromisedPayDate;
        if (dto.FollowUpDate.HasValue) activity.FollowUpDate = dto.FollowUpDate;
        if (dto.Notes != null) activity.Notes = dto.Notes;

        await _activityRepo.UpdateAsync(activity);
        await _unitOfWork.SaveChangesAsync();
        return await GetActivityByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<CollectionActivityDetailDto?> GetActivityByIdAsync(Guid id)
    {
        var activity = await _activityRepo.GetByIdAsync(id,
            a => a.Customer,
            a => a.Invoice!,
            a => a.AssignedTo!);
        return activity == null ? null : MapActivityDetailDto(activity);
    }

    public async Task<PagedResult<CollectionActivitySummaryDto>> GetActivitiesAsync(
        int page = 1, int pageSize = 20,
        string? search = null, string? status = null, string? activityType = null,
        Guid? customerId = null, Guid? assignedToId = null,
        DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _activityRepo.GetQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(a => a.Subject.Contains(search));
        if (!string.IsNullOrEmpty(status))
            query = query.Where(a => a.CollectionStatus == status);
        if (!string.IsNullOrEmpty(activityType))
            query = query.Where(a => a.ActivityType == activityType);
        if (customerId.HasValue)
            query = query.Where(a => a.CustomerId == customerId.Value);
        if (assignedToId.HasValue)
            query = query.Where(a => a.AssignedToId == assignedToId.Value);
        if (startDate.HasValue)
            query = query.Where(a => a.ActivityDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(a => a.ActivityDate <= endDate.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(a => a.Customer)
            .Include(a => a.AssignedTo)
            .OrderByDescending(a => a.ActivityDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return new PagedResult<CollectionActivitySummaryDto>
        {
            Items = items.Select(MapActivitySummaryDto).ToList(),
            TotalCount = totalCount, Page = page, PageSize = pageSize
        };
    }

    public async Task<List<CollectionActivitySummaryDto>> GetOverdueFollowUpsAsync(int daysOverdue = 0, Guid? assignedToId = null)
    {
        var cutoff = DateTime.UtcNow.AddDays(-daysOverdue);
        var query = _activityRepo.GetQueryable()
            .Where(a => a.FollowUpDate.HasValue && a.FollowUpDate <= cutoff && a.CollectionStatus != "Resolved" && a.CollectionStatus != "WrittenOff");

        if (assignedToId.HasValue)
            query = query.Where(a => a.AssignedToId == assignedToId.Value);

        var items = await query
            .Include(a => a.Customer).Include(a => a.AssignedTo)
            .OrderBy(a => a.FollowUpDate)
            .ToListAsync();

        return items.Select(MapActivitySummaryDto).ToList();
    }

    // ═════════════════════════════════════
    //  PAYMENT PLANS
    // ═════════════════════════════════════

    public async Task<PaymentPlanDetailDto> CreatePlanAsync(CreatePaymentPlanDto dto)
    {
        var tenantId = _currentUserProvider.TenantId;
        var plan = new PaymentPlan
        {
            PlanName = dto.PlanName,
            CustomerId = dto.CustomerId,
            TotalDebt = dto.TotalDebt,
            NumberOfInstallments = dto.NumberOfInstallments,
            Frequency = dto.Frequency,
            StartDate = dto.StartDate,
            PlanStatus = "Draft",
            Terms = dto.Terms,
            Notes = dto.Notes,
            TenantId = tenantId
        };

        await _planRepo.AddAsync(plan);

        // Auto-generate installments
        var installmentAmount = Math.Round(dto.TotalDebt / dto.NumberOfInstallments, 2);
        var remainder = dto.TotalDebt - (installmentAmount * dto.NumberOfInstallments);

        for (int i = 0; i < dto.NumberOfInstallments; i++)
        {
            var dueDate = dto.Frequency switch
            {
                "Weekly" => dto.StartDate.AddDays(7 * i),
                "Bi-Weekly" => dto.StartDate.AddDays(14 * i),
                "Quarterly" => dto.StartDate.AddMonths(3 * i),
                _ => dto.StartDate.AddMonths(i) // Monthly default
            };

            var amount = installmentAmount;
            if (i == dto.NumberOfInstallments - 1)
                amount += remainder; // Add penny remainder to last installment

            var installment = new PaymentPlanInstallment
            {
                PaymentPlanId = plan.Id,
                InstallmentNumber = i + 1,
                AmountDue = amount,
                DueDate = dueDate,
                InstallmentStatus = "Pending",
                TenantId = tenantId
            };
            await _installmentRepo.AddAsync(installment);
        }

        plan.EndDate = dto.Frequency switch
        {
            "Weekly" => dto.StartDate.AddDays(7 * (dto.NumberOfInstallments - 1)),
            "Bi-Weekly" => dto.StartDate.AddDays(14 * (dto.NumberOfInstallments - 1)),
            "Quarterly" => dto.StartDate.AddMonths(3 * (dto.NumberOfInstallments - 1)),
            _ => dto.StartDate.AddMonths(dto.NumberOfInstallments - 1)
        };

        await _planRepo.UpdateAsync(plan);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created Payment Plan '{PlanName}' — {Installments} installments of ~{Amount}",
            plan.PlanName, dto.NumberOfInstallments, installmentAmount);
        return await GetPlanByIdAsync(plan.Id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<PaymentPlanDetailDto?> GetPlanByIdAsync(Guid id)
    {
        var plan = await _planRepo.GetByIdAsync(id,
            p => p.Customer,
            p => p.ApprovedBy!,
            p => p.Installments);
        return plan == null ? null : MapPlanDetailDto(plan);
    }

    public async Task<PagedResult<PaymentPlanSummaryDto>> GetPlansAsync(
        int page = 1, int pageSize = 20,
        string? search = null, string? status = null,
        Guid? customerId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _planRepo.GetQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.PlanName.Contains(search));
        if (!string.IsNullOrEmpty(status))
            query = query.Where(p => p.PlanStatus == status);
        if (customerId.HasValue)
            query = query.Where(p => p.CustomerId == customerId.Value);
        if (startDate.HasValue)
            query = query.Where(p => p.StartDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(p => p.StartDate <= endDate.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(p => p.Customer).Include(p => p.Installments)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return new PagedResult<PaymentPlanSummaryDto>
        {
            Items = items.Select(MapPlanSummaryDto).ToList(),
            TotalCount = totalCount, Page = page, PageSize = pageSize
        };
    }

    public async Task<PaymentPlanDetailDto> ApprovePlanAsync(Guid id)
    {
        var plan = await _planRepo.GetByIdAsync(id) ?? throw new InvalidOperationException($"Payment plan {id} not found");
        if (plan.PlanStatus != "Draft")
            throw new InvalidOperationException("Only draft plans can be approved");
        plan.PlanStatus = "Active";
        plan.ApprovedById = _currentUserProvider.UserId;
        plan.ApprovedDate = DateTime.UtcNow;
        await _planRepo.UpdateAsync(plan);
        await _unitOfWork.SaveChangesAsync();
        return await GetPlanByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<PaymentPlanDetailDto> CancelPlanAsync(Guid id, string? reason = null)
    {
        var plan = await _planRepo.GetByIdAsync(id) ?? throw new InvalidOperationException($"Payment plan {id} not found");
        plan.PlanStatus = "Cancelled";
        if (reason != null) plan.Notes = $"{plan.Notes}\n[Cancelled] {reason}".Trim();
        await _planRepo.UpdateAsync(plan);
        await _unitOfWork.SaveChangesAsync();
        return await GetPlanByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<PaymentPlanDetailDto> RecordInstallmentPaymentAsync(Guid planId, Guid installmentId, RecordInstallmentPaymentDto dto)
    {
        var plan = await _planRepo.GetByIdAsync(planId, p => p.Installments)
            ?? throw new InvalidOperationException($"Payment plan {planId} not found");

        if (plan.PlanStatus != "Active")
            throw new InvalidOperationException("Can only record payments on active plans");

        var installment = plan.Installments.FirstOrDefault(i => i.Id == installmentId)
            ?? throw new InvalidOperationException($"Installment {installmentId} not found");

        installment.AmountPaid += dto.AmountPaid;
        installment.PaymentReference = dto.PaymentReference;
        if (dto.Notes != null) installment.Notes = dto.Notes;

        installment.InstallmentStatus = installment.AmountPaid >= installment.AmountDue ? "Paid" : "PartiallyPaid";
        if (installment.InstallmentStatus == "Paid") installment.PaidDate = DateTime.UtcNow;

        plan.TotalPaid += dto.AmountPaid;
        plan.InstallmentsPaid = plan.Installments.Count(i => i.InstallmentStatus == "Paid");

        // Check if all installments are paid
        if (plan.Installments.All(i => i.InstallmentStatus == "Paid"))
        {
            plan.PlanStatus = "Completed";
            _logger.LogInformation("Payment Plan '{PlanName}' fully paid", plan.PlanName);
        }

        await _installmentRepo.UpdateAsync(installment);
        await _planRepo.UpdateAsync(plan);
        await _unitOfWork.SaveChangesAsync();

        return await GetPlanByIdAsync(planId) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<List<PaymentPlanInstallmentDto>> GetOverdueInstallmentsAsync(Guid? customerId = null)
    {
        var query = _installmentRepo.GetQueryable()
            .Where(i => i.DueDate < DateTime.UtcNow && i.InstallmentStatus != "Paid");

        if (customerId.HasValue)
            query = query.Where(i => i.PaymentPlan.CustomerId == customerId.Value);

        var items = await query
            .Include(i => i.PaymentPlan).ThenInclude(p => p.Customer)
            .OrderBy(i => i.DueDate)
            .ToListAsync();
        return items.Select(MapInstallmentDto).ToList();
    }

    // ═════════════════════════════════════
    //  MAPPING
    // ═════════════════════════════════════

    private static CollectionActivitySummaryDto MapActivitySummaryDto(CollectionActivity a) => new()
    {
        Id = a.Id,
        Subject = a.Subject,
        ActivityType = a.ActivityType,
        CollectionStatus = a.CollectionStatus,
        CustomerName = a.Customer?.CustomerName,
        OutstandingAmount = a.OutstandingAmount,
        PromisedAmount = a.PromisedAmount,
        PromisedPayDate = a.PromisedPayDate,
        Outcome = a.Outcome,
        ActivityDate = a.ActivityDate,
        FollowUpDate = a.FollowUpDate,
        AssignedToName = a.AssignedTo?.UserName,
        CreatedAt = a.CreatedAt
    };

    private static CollectionActivityDetailDto MapActivityDetailDto(CollectionActivity a) => new()
    {
        Id = a.Id,
        Subject = a.Subject,
        ActivityType = a.ActivityType,
        CollectionStatus = a.CollectionStatus,
        CustomerName = a.Customer?.CustomerName,
        OutstandingAmount = a.OutstandingAmount,
        PromisedAmount = a.PromisedAmount,
        PromisedPayDate = a.PromisedPayDate,
        Outcome = a.Outcome,
        ActivityDate = a.ActivityDate,
        FollowUpDate = a.FollowUpDate,
        AssignedToName = a.AssignedTo?.UserName,
        CreatedAt = a.CreatedAt,
        CustomerId = a.CustomerId,
        InvoiceId = a.InvoiceId,
        InvoiceNumber = a.Invoice?.InvoiceNumber,
        Description = a.Description,
        AssignedToId = a.AssignedToId,
        Notes = a.Notes
    };

    private static PaymentPlanSummaryDto MapPlanSummaryDto(PaymentPlan p) => new()
    {
        Id = p.Id,
        PlanName = p.PlanName,
        PlanStatus = p.PlanStatus,
        CustomerName = p.Customer?.CustomerName,
        TotalDebt = p.TotalDebt,
        TotalPaid = p.TotalPaid,
        RemainingBalance = p.TotalDebt - p.TotalPaid,
        NumberOfInstallments = p.NumberOfInstallments,
        InstallmentsPaid = p.InstallmentsPaid,
        Frequency = p.Frequency,
        StartDate = p.StartDate,
        EndDate = p.EndDate,
        CreatedAt = p.CreatedAt
    };

    private static PaymentPlanDetailDto MapPlanDetailDto(PaymentPlan p) => new()
    {
        Id = p.Id,
        PlanName = p.PlanName,
        PlanStatus = p.PlanStatus,
        CustomerName = p.Customer?.CustomerName,
        TotalDebt = p.TotalDebt,
        TotalPaid = p.TotalPaid,
        RemainingBalance = p.TotalDebt - p.TotalPaid,
        NumberOfInstallments = p.NumberOfInstallments,
        InstallmentsPaid = p.InstallmentsPaid,
        Frequency = p.Frequency,
        StartDate = p.StartDate,
        EndDate = p.EndDate,
        CreatedAt = p.CreatedAt,
        CustomerId = p.CustomerId,
        ApprovedById = p.ApprovedById,
        ApprovedByName = p.ApprovedBy?.UserName,
        ApprovedDate = p.ApprovedDate,
        Terms = p.Terms,
        Notes = p.Notes,
        Installments = p.Installments?.OrderBy(i => i.InstallmentNumber).Select(MapInstallmentDto).ToList() ?? new()
    };

    private static PaymentPlanInstallmentDto MapInstallmentDto(PaymentPlanInstallment i) => new()
    {
        Id = i.Id,
        InstallmentNumber = i.InstallmentNumber,
        AmountDue = i.AmountDue,
        AmountPaid = i.AmountPaid,
        Balance = i.AmountDue - i.AmountPaid,
        DueDate = i.DueDate,
        PaidDate = i.PaidDate,
        InstallmentStatus = i.InstallmentStatus,
        PaymentReference = i.PaymentReference,
        Notes = i.Notes
    };
}
