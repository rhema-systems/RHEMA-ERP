using ErpSystem.Core.DTOs.QuantitySurvey;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyAdvanceRecoveryService
{
    Task<QuantitySurveyAdvanceRecoveryWorkspaceDto> GetWorkspaceAsync(Guid projectId, CancellationToken token = default);
    Task<QuantitySurveyAdvanceRecoveryAgreementDto> CreateAsync(Guid projectId, CreateQuantitySurveyAdvanceRecoveryRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyAdvanceRecoveryAgreementDto> SubmitAsync(Guid id, QuantitySurveyAdvanceRecoveryActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyAdvanceRecoveryAgreementDto> ApproveAsync(Guid id, QuantitySurveyAdvanceRecoveryActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyAdvanceRecoveryAgreementDto> RejectAsync(Guid id, QuantitySurveyAdvanceRecoveryActionRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<QuantitySurveyAdvanceRecoveryRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default);
}

public class QuantitySurveyAdvanceRecoveryException(string message) : Exception(message);
public sealed class QuantitySurveyAdvanceRecoveryNotFoundException(string message) : QuantitySurveyAdvanceRecoveryException(message);
public sealed class QuantitySurveyAdvanceRecoveryValidationException(string message) : QuantitySurveyAdvanceRecoveryException(message);
public sealed class QuantitySurveyAdvanceRecoveryConflictException(string message) : QuantitySurveyAdvanceRecoveryException(message);
