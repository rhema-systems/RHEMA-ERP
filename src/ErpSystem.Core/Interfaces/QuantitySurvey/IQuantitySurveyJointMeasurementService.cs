using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.DocumentManagement;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyJointMeasurementService
{
    Task<QuantitySurveyJointMeasurementLookupsDto> GetLookupsAsync(Guid projectId, bool external, CancellationToken token = default);
    Task<QuantitySurveyJointMeasurementPageDto> ListAsync(QuantitySurveyJointMeasurementListRequest request, bool external, CancellationToken token = default);
    Task<QuantitySurveyJointMeasurementDto> GetAsync(Guid id, bool external, CancellationToken token = default);
    Task<QuantitySurveyJointMeasurementDto> CreateAsync(CreateQuantitySurveyJointMeasurementRequest request, bool external, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyJointMeasurementDto> SubmitAsync(Guid id, QuantitySurveyJointMeasurementLifecycleRequest request, bool external, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyJointMeasurementDto> ScheduleAsync(Guid id, ScheduleQuantitySurveyJointMeasurementRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyJointMeasurementDto> LinkMeasurementAsync(Guid id, LinkQuantitySurveyJointMeasurementSheetRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyJointMeasurementDto> AttendAsync(Guid id, Guid participantId, AttendQuantitySurveyJointMeasurementRequest request, bool external, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyJointMeasurementDto> EndorseAsync(Guid id, Guid participantId, EndorseQuantitySurveyJointMeasurementRequest request, bool external, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyJointMeasurementDto> SubmitForApprovalAsync(Guid id, QuantitySurveyJointMeasurementLifecycleRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyJointMeasurementDto> ApproveAsync(Guid id, QuantitySurveyJointMeasurementLifecycleRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyJointMeasurementDto> RejectAsync(Guid id, QuantitySurveyJointMeasurementLifecycleRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyJointMeasurementEvidenceDto> AddEvidenceAsync(Guid id, Stream stream, string fileName, string contentType, AddQuantitySurveyJointMeasurementEvidenceRequest request, bool external, string correlationId, CancellationToken token = default);
    Task<CentralDocumentRepositoryContent> OpenEvidenceAsync(Guid id, Guid evidenceId, bool external, CancellationToken token = default);
    Task<IReadOnlyList<QuantitySurveyJointMeasurementRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default);
}

public class QuantitySurveyJointMeasurementValidationException(string message) : Exception(message);
public class QuantitySurveyJointMeasurementConflictException(string message) : Exception(message);
public class QuantitySurveyJointMeasurementNotFoundException(string message) : Exception(message);
