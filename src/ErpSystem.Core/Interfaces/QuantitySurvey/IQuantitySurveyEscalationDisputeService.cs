using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.DocumentManagement;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyEscalationDisputeService
{
    Task<IReadOnlyList<QuantitySurveyEscalationDisputeLookupDto>> GetCalculationLookupsAsync(CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationDisputePageDto> ListAsync(QuantitySurveyEscalationDisputeListRequest request, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationDisputeDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationDisputeDto> OpenAsync(CreateQuantitySurveyEscalationDisputeRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationDisputeDto> RespondAsync(Guid id, RespondQuantitySurveyEscalationDisputeRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationDisputeDto> ResolveAsync(Guid id, ResolveQuantitySurveyEscalationDisputeRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationDisputeAttachmentDto> AddAttachmentAsync(Guid id, Stream stream, string fileName, string contentType, AddQuantitySurveyEscalationDisputeAttachmentRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<CentralDocumentRepositoryContent> OpenAttachmentAsync(Guid id, Guid attachmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuantitySurveyEscalationDisputeRevisionDto>> HistoryAsync(Guid id, CancellationToken cancellationToken = default);
}

public class QuantitySurveyEscalationDisputeException(string message) : Exception(message);
public sealed class QuantitySurveyEscalationDisputeNotFoundException(string message) : QuantitySurveyEscalationDisputeException(message);
public sealed class QuantitySurveyEscalationDisputeConflictException(string message) : QuantitySurveyEscalationDisputeException(message);
public sealed class QuantitySurveyEscalationDisputeValidationException(string message) : QuantitySurveyEscalationDisputeException(message);
