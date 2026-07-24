using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementPrequalificationService
{
    Task<IReadOnlyList<ProcurementPrequalificationSummaryDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<ProcurementPrequalificationReadinessDto> GetReadinessAsync(CancellationToken cancellationToken = default);
    Task<ProcurementPrequalificationExerciseDto> GetAsync(Guid exerciseId, CancellationToken cancellationToken = default);
    Task<ProcurementPrequalificationExerciseDto> CreateAsync(CreateProcurementPrequalificationExerciseRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementPrequalificationExerciseDto> AdvertiseAsync(Guid exerciseId, AdvertiseProcurementPrequalificationRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementPrequalificationApplicationDto> SubmitApplicationAsync(Guid exerciseId, SubmitProcurementPrequalificationApplicationRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementPrequalificationExerciseDto> CloseAsync(Guid exerciseId, CloseProcurementPrequalificationRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementPrequalificationApplicationDto> EvaluateAsync(Guid exerciseId, Guid applicationId, EvaluateProcurementPrequalificationApplicationRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementPrequalificationExerciseDto> SubmitDecisionAsync(Guid exerciseId, SubmitProcurementPrequalificationDecisionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementPrequalificationExerciseDto> DecideAsync(Guid exerciseId, DecideProcurementPrequalificationRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementPrequalificationExerciseDto> ExpireAsync(Guid exerciseId, ExpireProcurementPrequalificationRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierEligibilityDto> CheckEligibilityAsync(Guid businessPartnerId, Guid categoryId, DateTime? atUtc, CancellationToken cancellationToken = default);
}

public sealed class ProcurementPrequalificationNotFoundException(string code, string message) : InvalidOperationException(message) { public string Code { get; } = code; }
public sealed class ProcurementPrequalificationConflictException(string code, string message) : InvalidOperationException(message) { public string Code { get; } = code; }
public sealed class ProcurementPrequalificationValidationException(string code, string message) : InvalidOperationException(message) { public string Code { get; } = code; }
public sealed class ProcurementPrequalificationAuthorizationException(string message) : UnauthorizedAccessException(message);
