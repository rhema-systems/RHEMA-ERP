using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Finance-owned orchestration over the existing collection-activity aggregate.
/// The service joins activities to posted AR settlement evidence and never creates
/// a second debt ledger or competing customer-balance calculation.
/// </summary>
public interface IArCollectionFollowUpService
{
    Task<ArCollectionWorkQueueDto> GetWorkQueueAsync(
        DateTime? asOfDate = null,
        int minimumDaysOverdue = 1,
        string? status = null,
        Guid? assignedToId = null,
        string? search = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default);

    Task<ArCollectionSummaryDto> GetSummaryAsync(DateTime? asOfDate = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArCollectionAssigneeDto>> GetAssigneesAsync(CancellationToken cancellationToken = default);
    Task<GenerateArCollectionTasksResultDto> GenerateTasksAsync(GenerateArCollectionTasksDto request, CancellationToken cancellationToken = default);
    Task<ArCollectionWorkItemDto> CreateTaskAsync(CreateArCollectionTaskDto request, CancellationToken cancellationToken = default);
    Task<ArCollectionWorkItemDto> UpdateTaskAsync(Guid taskId, UpdateArCollectionTaskDto request, CancellationToken cancellationToken = default);
    Task<ArCollectionHistoryItemDto> RecordReminderAsync(Guid taskId, RecordArCollectionReminderDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArCollectionHistoryItemDto>> GetHistoryAsync(Guid taskId, CancellationToken cancellationToken = default);
}
