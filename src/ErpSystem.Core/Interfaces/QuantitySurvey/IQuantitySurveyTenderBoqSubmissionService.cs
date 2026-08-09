using ErpSystem.Core.DTOs.QuantitySurvey;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyTenderBoqSubmissionService
{
    Task<QuantitySurveyTenderBoqContextDto> GetExternalContextAsync(
        Guid tenderBidId,
        CancellationToken cancellationToken = default);

    Task<QuantitySurveyBoqFileDto> CreateExternalTemplateAsync(
        Guid tenderBidId,
        CancellationToken cancellationToken = default);

    Task<QuantitySurveyTenderBoqSubmissionDto> PreviewExternalAsync(
        Guid tenderBidId,
        Stream stream,
        string fileName,
        string contentType,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<QuantitySurveyTenderBoqSubmissionDto> CommitExternalAsync(
        Guid tenderBidId,
        Guid submissionId,
        CommitQuantitySurveyTenderBoqSubmissionDto request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<QuantitySurveyTenderBoqSubmissionDto?> GetLatestExternalAsync(
        Guid tenderBidId,
        CancellationToken cancellationToken = default);

    Task EnsureReadyForTenderSubmissionAsync(
        Guid tenderBidId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuantitySurveyTenderBoqSubmissionDto>> GetInternalHistoryAsync(
        Guid tenderBidId,
        CancellationToken cancellationToken = default);

    Task<QuantitySurveyTenderBoqSubmissionDto> VetInternalAsync(
        Guid tenderBidId,
        Guid submissionId,
        VetQuantitySurveyTenderBoqSubmissionDto request,
        string correlationId,
        CancellationToken cancellationToken = default);
}
