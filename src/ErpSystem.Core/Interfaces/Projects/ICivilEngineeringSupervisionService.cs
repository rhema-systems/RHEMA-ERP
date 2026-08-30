using ErpSystem.Core.DTOs.Projects;

namespace ErpSystem.Core.Interfaces.Projects;

public interface ICivilEngineeringSupervisionService
{
    Task<CivilEngineeringProjectEngineerAssignmentLookupsDto> GetProjectEngineerAssignmentLookupsAsync(Guid projectId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringProjectEngineerAssignmentDto>> ListProjectEngineerAssignmentsAsync(Guid projectId, CancellationToken token = default);
    Task<CivilEngineeringProjectEngineerAssignmentDto> AssignProjectEngineerAsync(Guid projectId, AssignCivilEngineeringProjectEngineerRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringProjectEngineerAssignmentDto> EndProjectEngineerAssignmentAsync(Guid assignmentId, EndCivilEngineeringProjectEngineerAssignmentRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringProjectEngineerAssignmentRevisionDto>> GetProjectEngineerAssignmentHistoryAsync(Guid assignmentId, CancellationToken token = default);
}

public interface ICivilEngineeringSiteInstructionService
{
    Task<CivilEngineeringSiteInstructionLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringSiteInstructionRoutingDto>> ListAsync(Guid projectId, CancellationToken token = default);
    Task<CivilEngineeringSiteInstructionRoutingDto> IssueAsync(Guid projectId, CreateCivilEngineeringSiteInstructionRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringSiteInstructionRoutingDto> ProcessProjectManagerRoutingAsync(Guid routingId, ProcessCivilEngineeringSiteInstructionRoutingRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringSiteInstructionRoutingDto> RecordContractorResponseAsync(Guid routingId, RespondToCivilEngineeringSiteInstructionRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringSiteInstructionRoutingDto> ReviewContractorResponseAsync(Guid routingId, ReviewCivilEngineeringSiteInstructionResponseRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringSiteInstructionRoutingDto> RecordEngineeringFollowUpAsync(Guid routingId, FollowUpCivilEngineeringSiteInstructionRequest request, string correlationId, CancellationToken token = default);
}

public interface ICivilEngineeringRfiService
{
    Task<CivilEngineeringRfiLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringRfiRoutingDto>> ListAsync(Guid projectId, CancellationToken token = default);
    Task<CivilEngineeringRfiRoutingDto> CreateExternalAsync(Guid projectId, CreateCivilEngineeringRfiRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringRfiRoutingDto> SubmitProjectEngineerResponseAsync(Guid routingId, SubmitCivilEngineeringRfiResponseRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringRfiRoutingDto> ProcessProjectManagerResponseAsync(Guid routingId, ProcessCivilEngineeringRfiResponseRequest request, string correlationId, CancellationToken token = default);
}

public interface ICivilEngineeringQualityTestService
{
    Task<CivilEngineeringQualityTestLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringQualityTestReportDto>> ListAsync(Guid projectId, CancellationToken token = default);
    Task<CivilEngineeringQualityTestReportDto> CreateAsync(Guid projectId, CreateCivilEngineeringQualityTestReportRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringQualityTestReportDto> ProcessAsync(Guid reportId, ProcessCivilEngineeringQualityTestReportRequest request, string correlationId, CancellationToken token = default);
}

/// <summary>
/// Governed Civil inspection scheduling over the Projects quality checkpoint and
/// non-conformance owners. It does not introduce a parallel work-completion register.
/// </summary>
public interface ICivilEngineeringInspectionControlService
{
    Task<CivilEngineeringInspectionLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringInspectionControlDto>> ListAsync(Guid projectId, CancellationToken token = default);
    Task<CivilEngineeringInspectionControlDto> CreateAsync(Guid projectId, CreateCivilEngineeringInspectionControlRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringInspectionControlDto> ProcessAsync(Guid inspectionControlId, ProcessCivilEngineeringInspectionControlRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringInspectionRevisionDto>> GetHistoryAsync(Guid inspectionControlId, CancellationToken token = default);
}

/// <summary>
/// Civil review overlay for QS-owned payment certificates. It stores no certificate,
/// workflow-instance, AP invoice, payment or ledger state.
/// </summary>
public interface ICivilEngineeringIpcEndorsementService
{
    Task<CivilEngineeringIpcEndorsementLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringIpcEndorsementDto>> ListAsync(Guid projectId, CancellationToken token = default);
    Task<CivilEngineeringIpcEndorsementDto> SubmitAsync(Guid certificateId, SubmitCivilEngineeringIpcEndorsementRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringIpcEndorsementDto> ReviewAsync(Guid endorsementId, ReviewCivilEngineeringIpcEndorsementRequest request, string correlationId, CancellationToken token = default);
    Task EnsureCertificateCanBeAmendedAsync(Guid certificateId, CancellationToken token = default);
    Task EnsureCertificateCanProceedAsync(Guid certificateId, CancellationToken token = default);
}

public interface ICivilEngineeringWeeklySupervisionService
{
    Task<CivilEngineeringWeeklySupervisionLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringWeeklySupervisionReportDto>> ListAsync(Guid projectId, CancellationToken token = default);
    Task<CivilEngineeringWeeklySupervisionReportDto> CreateAsync(Guid projectId, CreateCivilEngineeringWeeklySupervisionReportRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringWeeklySupervisionReportDto> ProcessAsync(Guid reportId, ProcessCivilEngineeringWeeklySupervisionReportRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringWeeklySupervisionReportDto>> EscalateOverdueAsync(Guid projectId, Guid clientRequestId, string correlationId, CancellationToken token = default);
}

/// <summary>
/// Civil's governed extension-of-time control over Project's authoritative EOT record.
/// QS retains variations/budgets and Procurement retains the Works contract.
/// </summary>
public interface ICivilEngineeringExtensionOfTimeService
{
    Task<CivilEngineeringExtensionOfTimeLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringExtensionOfTimeControlDto>> ListAsync(Guid projectId, CancellationToken token = default);
    Task<CivilEngineeringExtensionOfTimeControlDto> CreateAsync(Guid projectId, CreateCivilEngineeringExtensionOfTimeRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringExtensionOfTimeControlDto> ReviewAsync(Guid controlId, ReviewCivilEngineeringExtensionOfTimeRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringExtensionOfTimeRevisionDto>> GetHistoryAsync(Guid controlId, CancellationToken token = default);
}

public sealed class CivilEngineeringSupervisionNotFoundException(string message) : KeyNotFoundException(message);
public sealed class CivilEngineeringSupervisionValidationException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringSupervisionConflictException(string message) : InvalidOperationException(message);
