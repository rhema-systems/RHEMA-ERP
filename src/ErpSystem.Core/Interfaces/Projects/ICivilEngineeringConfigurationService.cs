using ErpSystem.Core.DTOs.Projects;

namespace ErpSystem.Core.Interfaces.Projects;

public interface ICivilEngineeringConfigurationService
{
    IReadOnlyList<CivilEngineeringDecisionSchemaDto> GetSchemas();
    Task<CivilEngineeringLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default);
    Task<CivilEngineeringPagedResult<CivilEngineeringProfileSummaryDto>> GetProfilesAsync(CivilEngineeringProfileListRequest request, CancellationToken cancellationToken = default);
    Task<CivilEngineeringProfileDto> GetProfileAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CivilEngineeringProfileDto?> GetEffectiveProfileAsync(DateTime atUtc, CancellationToken cancellationToken = default);
    Task<CivilEngineeringProfileDto> CreateProfileAsync(CreateCivilEngineeringProfileRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<CivilEngineeringProfileDto> UpdateProfileAsync(Guid id, UpdateCivilEngineeringProfileRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<CivilEngineeringDecisionDto> SaveDecisionAsync(Guid profileId, string key, SaveCivilEngineeringDecisionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<CivilEngineeringDecisionDto> SubmitDecisionAsync(Guid profileId, string key, SubmitCivilEngineeringDecisionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<CivilEngineeringDecisionDto> ApproveDecisionAsync(Guid profileId, string key, DecideCivilEngineeringDecisionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<CivilEngineeringDecisionDto> RejectDecisionAsync(Guid profileId, string key, DecideCivilEngineeringDecisionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<CivilEngineeringEvidenceDto> LinkEvidenceAsync(Guid profileId, string key, LinkCivilEngineeringEvidenceRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task UnlinkEvidenceAsync(Guid profileId, string key, Guid evidenceId, string rowVersion, string? reason, string correlationId, CancellationToken cancellationToken = default);
    Task<CivilEngineeringValidationResultDto> ValidateProfileAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CivilEngineeringProfileDto> PublishProfileAsync(Guid id, CivilEngineeringLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<CivilEngineeringProfileDto> RetireProfileAsync(Guid id, CivilEngineeringLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<CivilEngineeringProfileDto> CloneDraftAsync(Guid id, CloneCivilEngineeringProfileRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task DeleteDraftAsync(Guid id, CivilEngineeringLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CivilEngineeringRevisionDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default);
}
