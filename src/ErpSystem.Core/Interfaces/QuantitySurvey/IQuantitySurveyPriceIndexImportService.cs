using ErpSystem.Core.DTOs.QuantitySurvey;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyPriceIndexImportService
{
    Task<QuantitySurveyPriceIndexFileDto> CreateTemplateAsync(Guid indexFamilyId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyPriceIndexImportDto> StageAsync(Guid indexFamilyId, Stream stream, string fileName, string contentType, StageQuantitySurveyPriceIndexImportRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyPriceIndexImportPageDto> ListAsync(QuantitySurveyPriceIndexImportListRequest request, CancellationToken cancellationToken = default);
    Task<QuantitySurveyPriceIndexImportDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<QuantitySurveyPriceIndexImportDto> SubmitAsync(Guid id, QuantitySurveyPriceIndexImportLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyPriceIndexImportDto> ApproveAsync(Guid id, QuantitySurveyPriceIndexImportLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyPriceIndexImportDto> RejectAsync(Guid id, QuantitySurveyPriceIndexImportLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuantitySurveyPriceIndexImportRevisionDto>> HistoryAsync(Guid id, CancellationToken cancellationToken = default);
}

public class QuantitySurveyPriceIndexImportException(string message) : Exception(message);
public sealed class QuantitySurveyPriceIndexImportNotFoundException(string message) : QuantitySurveyPriceIndexImportException(message);
public sealed class QuantitySurveyPriceIndexImportConflictException(string message) : QuantitySurveyPriceIndexImportException(message);
public sealed class QuantitySurveyPriceIndexImportValidationException(string message) : QuantitySurveyPriceIndexImportException(message);
