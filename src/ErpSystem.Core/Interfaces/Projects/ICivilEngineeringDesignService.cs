using ErpSystem.Core.DTOs.Projects;

namespace ErpSystem.Core.Interfaces.Projects;

public interface ICivilEngineeringDesignService
{
    Task<CivilEngineeringDesignLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringDesignCaseDto>> ListAsync(Guid projectId, CancellationToken token = default);
    Task<CivilEngineeringDesignCaseDto> GetAsync(Guid id, CancellationToken token = default);
    Task<CivilEngineeringDesignCaseDto> CreateAsync(CreateCivilEngineeringDesignCaseRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringDesignCaseDto> TransitionAsync(Guid id, CivilEngineeringDesignTransitionRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringDesignRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringReconnaissanceReportDto>> ListReconnaissanceAsync(Guid designCaseId, CancellationToken token = default);
    Task<CivilEngineeringReconnaissanceReportDto> CreateReconnaissanceAsync(Guid designCaseId, CreateCivilEngineeringReconnaissanceRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringReconnaissanceReportDto> UpdateReconnaissanceAsync(Guid designCaseId, Guid reportId, UpdateCivilEngineeringReconnaissanceRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringReconnaissanceReportDto> CompleteReconnaissanceAsync(Guid designCaseId, Guid reportId, CompleteCivilEngineeringReconnaissanceRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringDesignInputRequestDto>> ListInformationRequestsAsync(Guid designCaseId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringDesignInputRequestDto>> ListAssignedInformationRequestsAsync(CancellationToken token = default);
    Task<CivilEngineeringDesignInputRequestDto> CreateInformationRequestAsync(Guid designCaseId, CreateCivilEngineeringDesignInputRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringDesignInputRequestDto> SubmitInformationResponseAsync(Guid requestId, SubmitCivilEngineeringDesignInputResponseRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringDesignInputRequestDto> ReviewInformationResponseAsync(Guid requestId, ReviewCivilEngineeringDesignInputRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringDocumentLookupsDto> GetDocumentLookupsAsync(Guid designCaseId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringDocumentDto>> ListDocumentsAsync(Guid designCaseId, CancellationToken token = default);
    Task<CivilEngineeringDocumentDto> CreateDocumentAsync(Guid designCaseId, CreateCivilEngineeringDocumentRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringDocumentDto> SubmitDocumentAsync(Guid documentId, SubmitCivilEngineeringDocumentRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringDocumentDto> ReviewDocumentAsync(Guid documentId, ReviewCivilEngineeringDocumentRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringPlanningGisLookupsDto> GetPlanningGisLookupsAsync(Guid designCaseId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringPlanningGisValidationDto>> ListPlanningGisValidationsAsync(Guid designCaseId, CancellationToken token = default);
    Task<CivilEngineeringPlanningGisValidationDto> CreatePlanningGisValidationAsync(Guid designCaseId, CreateCivilEngineeringPlanningGisValidationRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringPlanningGisValidationDto> SubmitPlanningGisValidationAsync(Guid validationId, CivilEngineeringPlanningGisSubmitRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringPlanningGisValidationDto> DecidePlanningGisValidationAsync(Guid validationId, CivilEngineeringPlanningGisDecisionRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringPlanningGisValidationRevisionDto>> PlanningGisHistoryAsync(Guid validationId, CancellationToken token = default);
    Task<CivilEngineeringCommercialReadinessDto> GetCommercialReadinessAsync(Guid designCaseId, CancellationToken token = default);
}

public sealed class CivilEngineeringDesignNotFoundException(string message) : KeyNotFoundException(message);
public sealed class CivilEngineeringDesignValidationException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringDesignConflictException(string message) : InvalidOperationException(message);
