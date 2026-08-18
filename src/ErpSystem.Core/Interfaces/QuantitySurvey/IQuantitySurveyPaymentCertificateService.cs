using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.QuantitySurvey;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyPaymentCertificateService
{
    Task<QuantitySurveyPaymentCertificateLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default);
    Task<IReadOnlyList<QuantitySurveyPaymentCertificateDto>> ListAsync(Guid projectId, CancellationToken token = default);
    Task<QuantitySurveyPaymentCertificateDto> GetAsync(Guid id, CancellationToken token = default);
    Task<QuantitySurveyPaymentCertificateDto> GenerateAsync(Guid projectId, GenerateQuantitySurveyPaymentCertificateRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyPaymentCertificateDto> UpdateAsync(Guid id, UpdateQuantitySurveyPaymentCertificateRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyPaymentCertificateDto> SubmitAsync(Guid id, QuantitySurveyPaymentCertificateActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyPaymentCertificateDto> ApproveAsync(Guid id, QuantitySurveyPaymentCertificateActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyPaymentCertificateDto> RejectAsync(Guid id, QuantitySurveyPaymentCertificateActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyPaymentCertificateDto> HandoffToApAsync(Guid id, QuantitySurveyPaymentCertificateActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyPaymentCertificateDto> RefreshPaymentStatusAsync(Guid id, string correlationId, CancellationToken token = default);
    Task<RenderedDocumentDto> RenderAsync(Guid id, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<QuantitySurveyPaymentCertificateRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default);
}

public class QuantitySurveyPaymentCertificateException(string message) : Exception(message);
public sealed class QuantitySurveyPaymentCertificateNotFoundException(string message) : QuantitySurveyPaymentCertificateException(message);
public sealed class QuantitySurveyPaymentCertificateValidationException(string message) : QuantitySurveyPaymentCertificateException(message);
public sealed class QuantitySurveyPaymentCertificateConflictException(string message) : QuantitySurveyPaymentCertificateException(message);
