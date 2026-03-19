using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Core.Interfaces.Sales;

/// <summary>
/// Service interface for Collections & Debt Management.
/// Handles overdue invoice tracking and structured payment plans.
/// </summary>
public interface ICollectionService
{
    // ── Collection Activities ──
    Task<CollectionActivityDetailDto> CreateActivityAsync(CreateCollectionActivityDto dto);
    Task<CollectionActivityDetailDto> UpdateActivityAsync(Guid id, UpdateCollectionActivityDto dto);
    Task<CollectionActivityDetailDto?> GetActivityByIdAsync(Guid id);
    Task<PagedResult<CollectionActivitySummaryDto>> GetActivitiesAsync(
        int page = 1, int pageSize = 20,
        string? search = null, string? status = null, string? activityType = null,
        Guid? customerId = null, Guid? assignedToId = null,
        DateTime? startDate = null, DateTime? endDate = null);
    Task<List<CollectionActivitySummaryDto>> GetOverdueFollowUpsAsync(int daysOverdue = 0, Guid? assignedToId = null);

    // ── Payment Plans ──
    Task<PaymentPlanDetailDto> CreatePlanAsync(CreatePaymentPlanDto dto);
    Task<PaymentPlanDetailDto?> GetPlanByIdAsync(Guid id);
    Task<PagedResult<PaymentPlanSummaryDto>> GetPlansAsync(
        int page = 1, int pageSize = 20,
        string? search = null, string? status = null,
        Guid? customerId = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<PaymentPlanDetailDto> ApprovePlanAsync(Guid id);
    Task<PaymentPlanDetailDto> CancelPlanAsync(Guid id, string? reason = null);
    Task<PaymentPlanDetailDto> RecordInstallmentPaymentAsync(Guid planId, Guid installmentId, RecordInstallmentPaymentDto dto);
    Task<List<PaymentPlanInstallmentDto>> GetOverdueInstallmentsAsync(Guid? customerId = null);
}
