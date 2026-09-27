using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.DocumentManagement;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyValuationWorksheetService
{
    Task<IReadOnlyList<QuantitySurveyValuationWorksheetDto>> SearchAsync(string search, int take = 8, CancellationToken token = default);
    Task<QuantitySurveyValuationLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default);
    Task<QuantitySurveyValuationWorksheetDto> GetAsync(Guid interimValuationId, Guid? projectBoqVersionId, CancellationToken token = default);
    Task<QuantitySurveyValuationWorksheetDto> SaveAsync(Guid interimValuationId, SaveQuantitySurveyValuationWorksheetRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<QuantitySurveyValuationWorksheetRevisionDto>> HistoryAsync(Guid interimValuationId, CancellationToken token = default);
    Task<QuantitySurveyValuationLookupsDto> GetExternalLookupsAsync(Guid projectId, CancellationToken token = default);
    Task<IReadOnlyList<QuantitySurveyValuationWorksheetDto>> GetExternalProjectValuationsAsync(Guid projectId, CancellationToken token = default);
    Task<QuantitySurveyValuationWorksheetDto> GetExternalAsync(Guid worksheetId, CancellationToken token = default);
    Task<QuantitySurveyValuationWorksheetDto> SaveContractorClaimAsync(Guid worksheetId, SaveQuantitySurveyValuationWorksheetRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyValuationWorksheetDto> SubmitContractorClaimAsync(Guid worksheetId, QuantitySurveyValuationEndorsementRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyValuationWorksheetDto> VetAsync(Guid worksheetId, QuantitySurveyValuationLifecycleRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyValuationWorksheetDto> EndorseConsultantAsync(Guid worksheetId, QuantitySurveyValuationEndorsementRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyValuationWorksheetDto> SubmitApprovalAsync(Guid worksheetId, QuantitySurveyValuationLifecycleRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyValuationWorksheetDto> ApproveAsync(Guid worksheetId, QuantitySurveyValuationLifecycleRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyValuationWorksheetDto> RejectAsync(Guid worksheetId, QuantitySurveyValuationLifecycleRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyValuationEvidenceDto> AddEvidenceAsync(Guid worksheetId, Stream stream, string fileName, string contentType, AddQuantitySurveyValuationEvidenceRequest request, bool external, string correlationId, CancellationToken token = default);
    Task<CentralDocumentRepositoryContent> OpenEvidenceAsync(Guid worksheetId, Guid evidenceId, bool external, CancellationToken token = default);
}

public class QuantitySurveyValuationWorksheetException(string message) : Exception(message);
public sealed class QuantitySurveyValuationWorksheetNotFoundException(string message) : QuantitySurveyValuationWorksheetException(message);
public sealed class QuantitySurveyValuationWorksheetValidationException(string message) : QuantitySurveyValuationWorksheetException(message);
public sealed class QuantitySurveyValuationWorksheetConflictException(string message) : QuantitySurveyValuationWorksheetException(message);
