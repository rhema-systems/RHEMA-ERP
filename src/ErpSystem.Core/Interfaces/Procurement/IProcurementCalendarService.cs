using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementCalendarService
{
    Task<ProcurementCalendarSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementCalendarProfileDto>> GetProfilesAsync(CancellationToken cancellationToken = default);
    Task<ProcurementCalendarProfileDto> GetProfileAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProcurementCalendarProfileDto> CreateProfileAsync(SaveProcurementCalendarProfileRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementCalendarProfileDto> UpdateProfileAsync(Guid id, SaveProcurementCalendarProfileRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementCalendarProfileDto> CloneProfileAsync(Guid id, CloneProcurementCalendarProfileRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementCalendarProfileDto> PublishProfileAsync(Guid id, ProcurementCalendarLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementCalendarProfileDto> RetireProfileAsync(Guid id, ProcurementCalendarLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task DeleteDraftAsync(Guid id, ProcurementCalendarLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementCalendarOccurrencePageDto> SearchOccurrencesAsync(ProcurementCalendarOccurrenceSearchRequest request, CancellationToken cancellationToken = default);
    Task<ProcurementCalendarOccurrenceDto> GetOccurrenceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProcurementCalendarOccurrenceDto> AcknowledgeAsync(Guid id, ProcurementCalendarOccurrenceActionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementCalendarOccurrenceDto> CompleteAsync(Guid id, ProcurementCalendarOccurrenceActionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementCalendarOccurrenceDto> CancelAsync(Guid id, ProcurementCalendarOccurrenceActionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementCalendarRunDto> RunAsync(ProcurementCalendarRunRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementCalendarRunDto>> GetRunsAsync(int take = 50, CancellationToken cancellationToken = default);
    IReadOnlyList<string> GetTimeZones();
}

public interface IProcurementCalendarProcessor
{
    Task<ProcurementCalendarRunDto> ProcessTenantAsync(Guid tenantId, DateTime evaluationAtUtc, int? horizonDays,
        ProcurementCalendarRunTrigger trigger, Guid? requestedById, string requestedByName, string reason,
        string correlationId, CancellationToken cancellationToken = default);
}

public sealed class ProcurementCalendarNotFoundException(string message) : InvalidOperationException(message);
public sealed class ProcurementCalendarConflictException(string message) : InvalidOperationException(message);
public sealed class ProcurementCalendarAuthorizationException(string message) : UnauthorizedAccessException(message);
public sealed class ProcurementCalendarValidationException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
