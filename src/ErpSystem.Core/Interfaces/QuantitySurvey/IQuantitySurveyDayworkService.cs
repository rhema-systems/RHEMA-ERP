using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.DocumentManagement;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyDayworkService
{
    Task<QuantitySurveyDayworkWorkspaceDto> GetWorkspaceAsync(Guid projectId, bool external, CancellationToken token = default);
    Task<QuantitySurveyDayworkSheetDto> SaveExternalAsync(Guid projectId, SaveQuantitySurveyDayworkRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyDayworkEvidenceDto> UploadEvidenceAsync(Guid id, Guid clientRequestId, string title, string fileName,
        string contentType, long fileSize, Func<Stream> openRead, bool external, string correlationId, CancellationToken token = default);
    Task<CentralDocumentRepositoryContent> OpenEvidenceAsync(Guid id, Guid evidenceId, bool external, CancellationToken token = default);
    Task<QuantitySurveyDayworkSheetDto> SignExternalAsync(Guid id, QuantitySurveyDayworkActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyDayworkSheetDto> VerifyAsync(Guid id, QuantitySurveyDayworkActionRequest request, bool accept, string correlationId, CancellationToken token = default);
}

public sealed class QuantitySurveyDayworkValidationException(string message) : Exception(message);
public sealed class QuantitySurveyDayworkConflictException(string message) : Exception(message);
public sealed class QuantitySurveyDayworkNotFoundException(string message) : Exception(message);
