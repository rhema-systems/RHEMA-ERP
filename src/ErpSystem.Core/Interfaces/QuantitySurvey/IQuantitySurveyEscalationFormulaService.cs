using ErpSystem.Core.DTOs.QuantitySurvey;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyEscalationFormulaService
{
    Task<QuantitySurveyEscalationLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuantitySurveyIndexFamilyDto>> GetIndexFamiliesAsync(QuantitySurveyIndexFamilyListRequest request, CancellationToken cancellationToken = default);
    Task<QuantitySurveyIndexFamilyDto> CreateIndexFamilyAsync(SaveQuantitySurveyIndexFamilyRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyIndexFamilyDto> UpdateIndexFamilyAsync(Guid id, SaveQuantitySurveyIndexFamilyRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationFormulaPageDto> GetFormulasAsync(QuantitySurveyEscalationFormulaListRequest request, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationFormulaDto> GetFormulaAsync(Guid id, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationFormulaDto> CreateFormulaAsync(CreateQuantitySurveyEscalationFormulaRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationFormulaDto> UpdateFormulaAsync(Guid id, UpdateQuantitySurveyEscalationFormulaRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationFormulaDto> SubmitFormulaAsync(Guid id, QuantitySurveyEscalationLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationFormulaDto> ApproveFormulaAsync(Guid id, QuantitySurveyEscalationLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationFormulaDto> RejectFormulaAsync(Guid id, QuantitySurveyEscalationLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationFormulaDto> RetireFormulaAsync(Guid id, QuantitySurveyEscalationLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuantitySurveyEscalationRevisionDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default);
}

public class QuantitySurveyEscalationException(string message) : Exception(message);
public sealed class QuantitySurveyEscalationNotFoundException(string message) : QuantitySurveyEscalationException(message);
public sealed class QuantitySurveyEscalationConflictException(string message) : QuantitySurveyEscalationException(message);
public sealed class QuantitySurveyEscalationValidationException(string message) : QuantitySurveyEscalationException(message);
