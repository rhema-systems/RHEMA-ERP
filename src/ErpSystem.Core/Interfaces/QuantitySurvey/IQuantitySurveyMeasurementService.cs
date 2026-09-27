using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.DocumentManagement;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyMeasurementService
{
    Task<IReadOnlyList<QuantitySurveyMeasurementDto>> SearchAsync(string search, int take = 8, CancellationToken token = default);
    Task<QuantitySurveyMeasurementLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default);
    Task<QuantitySurveyMeasurementPageDto> ListAsync(QuantitySurveyMeasurementListRequest request, CancellationToken token = default);
    Task<QuantitySurveyMeasurementDto> GetAsync(Guid id, CancellationToken token = default);
    Task<QuantitySurveyMeasurementDto> CreateAsync(CreateQuantitySurveyMeasurementRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyMeasurementDto> UpdateAsync(Guid id, UpdateQuantitySurveyMeasurementRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyMeasurementDto> RecordAsync(Guid id, RecordQuantitySurveyMeasurementRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyMeasurementAttachmentDto> AddAttachmentAsync(Guid id, Stream stream, string fileName, string contentType, AddQuantitySurveyMeasurementAttachmentRequest request, string correlationId, CancellationToken token = default);
    Task<CentralDocumentRepositoryContent> OpenAttachmentAsync(Guid id, Guid attachmentId, CancellationToken token = default);
    Task<IReadOnlyList<QuantitySurveyMeasurementRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default);
}

public class QuantitySurveyMeasurementException(string message) : Exception(message);
public sealed class QuantitySurveyMeasurementNotFoundException(string message) : QuantitySurveyMeasurementException(message);
public sealed class QuantitySurveyMeasurementConflictException(string message) : QuantitySurveyMeasurementException(message);
public sealed class QuantitySurveyMeasurementValidationException(string message) : QuantitySurveyMeasurementException(message);
