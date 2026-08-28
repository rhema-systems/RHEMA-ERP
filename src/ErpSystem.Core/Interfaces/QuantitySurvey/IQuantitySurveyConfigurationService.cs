using ErpSystem.Core.DTOs.QuantitySurvey;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyConfigurationService
{
    IReadOnlyList<QuantitySurveyDecisionSchemaDto> GetSchemas();
    Task<QuantitySurveyLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default);
    Task<QuantitySurveyPagedResult<QuantitySurveyProfileSummaryDto>> GetProfilesAsync(QuantitySurveyProfileListRequest request, CancellationToken cancellationToken = default);
    Task<QuantitySurveyProfileDto> GetProfileAsync(Guid id, CancellationToken cancellationToken = default);
    Task<QuantitySurveyProfileDto?> GetEffectiveProfileAsync(DateTime atUtc, CancellationToken cancellationToken = default);
    Task<QuantitySurveyProfileDto> CreateProfileAsync(CreateQuantitySurveyProfileRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyProfileDto> UpdateProfileAsync(Guid id, UpdateQuantitySurveyProfileRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyDecisionDto> SaveDecisionAsync(Guid profileId, string key, SaveQuantitySurveyDecisionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyDecisionDto> SubmitDecisionAsync(Guid profileId, string key, SubmitQuantitySurveyDecisionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyDecisionDto> ApproveDecisionAsync(Guid profileId, string key, DecideQuantitySurveyDecisionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyDecisionDto> RejectDecisionAsync(Guid profileId, string key, DecideQuantitySurveyDecisionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEvidenceDto> LinkEvidenceAsync(Guid profileId, string key, LinkQuantitySurveyEvidenceRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task UnlinkEvidenceAsync(Guid profileId, string key, Guid evidenceId, string rowVersion, string? reason, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyValidationResultDto> ValidateProfileAsync(Guid id, CancellationToken cancellationToken = default);
    Task<QuantitySurveyProfileDto> PublishProfileAsync(Guid id, QuantitySurveyLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyProfileDto> RetireProfileAsync(Guid id, QuantitySurveyLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyProfileDto> CloneDraftAsync(Guid id, CloneQuantitySurveyProfileRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task DeleteDraftAsync(Guid id, QuantitySurveyLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuantitySurveyRevisionDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default);
}
