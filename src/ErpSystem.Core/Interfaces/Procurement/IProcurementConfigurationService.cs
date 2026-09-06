using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementConfigurationService
{
    IReadOnlyList<ProcurementDecisionSchemaDto> GetDecisionSchemas();
    Task<ProcurementConfigurationPagedResult<ProcurementConfigurationProfileSummaryDto>> GetProfilesAsync(ProcurementConfigurationProfileListRequest request, CancellationToken cancellationToken = default);
    Task<ProcurementConfigurationProfileDto> GetProfileAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProcurementConfigurationProfileDto?> GetEffectiveProfileAsync(string profileCode, DateTime atUtc, CancellationToken cancellationToken = default);
    Task<ProcurementConfigurationProfileDto> CreateProfileAsync(CreateProcurementConfigurationProfileRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementConfigurationProfileDto> UpdateProfileAsync(Guid id, UpdateProcurementConfigurationProfileRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementConfigurationDecisionDto> SaveDecisionAsync(Guid profileId, string decisionKey, SaveProcurementConfigurationDecisionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementConfigurationProfileDto> WithdrawDecisionAsync(Guid profileId, string decisionKey, WithdrawProcurementConfigurationDecisionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementConfigurationValidationResultDto> ValidateProfileAsync(Guid id, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementConfigurationProfileDto> PublishProfileAsync(Guid id, ProcurementConfigurationLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementConfigurationProfileDto> RetireProfileAsync(Guid id, ProcurementConfigurationLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementConfigurationProfileDto> CloneDraftAsync(Guid id, CloneProcurementConfigurationProfileRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task DeleteDraftAsync(Guid id, ProcurementConfigurationLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementConfigurationEvidenceLinkDto> LinkEvidenceAsync(Guid profileId, string decisionKey, LinkProcurementConfigurationEvidenceRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task UnlinkEvidenceAsync(Guid profileId, string decisionKey, Guid evidenceId, string decisionRowVersion, string? reason, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementConfigurationRevisionDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default);
}
