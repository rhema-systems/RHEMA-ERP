using ErpSystem.Core.DTOs.QuantitySurvey;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyBoqSpreadsheetService
{
    Task<QuantitySurveyBoqFileDto> CreateTemplateAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyBoqFileDto> ExportAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyBoqImportPreviewDto> PreviewAsync(
        Guid projectId,
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
    Task<QuantitySurveyBoqImportCommitResultDto> CommitAsync(
        Guid projectId,
        Guid sessionId,
        CommitQuantitySurveyBoqImportDto request,
        CancellationToken cancellationToken = default);
    Task<QuantitySurveyBoqFileDto> CreateErrorWorkbookAsync(
        Guid projectId,
        Guid sessionId,
        CancellationToken cancellationToken = default);
}
