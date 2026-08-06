using ErpSystem.Core.DTOs.Compliance;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Interfaces.Audit;

public interface IAuditRecordProvider
{
    string StoreKey { get; }
    string StoreName { get; }
    Task<AuditRecordDescriptor?> FindAsync(Guid tenantId, Guid recordId, CancellationToken cancellationToken = default);
}

public interface IAuditGovernanceService
{
    Task<AuditRecordGovernanceDto> GetAsync(string storeKey, Guid recordId, CancellationToken cancellationToken = default);
    Task<AuditRecordGovernanceDto> PlaceLegalHoldAsync(string storeKey, Guid recordId, AuditLifecycleCommandDto request, CancellationToken cancellationToken = default);
    Task<AuditRecordGovernanceDto> ReleaseLegalHoldAsync(string storeKey, Guid recordId, AuditLifecycleCommandDto request, CancellationToken cancellationToken = default);
    Task<AuditRecordGovernanceDto> ArchiveAsync(string storeKey, Guid recordId, AuditLifecycleCommandDto request, CancellationToken cancellationToken = default);
    Task<AuditRecordGovernanceDto> RestoreAsync(string storeKey, Guid recordId, AuditLifecycleCommandDto request, CancellationToken cancellationToken = default);
}

public interface IAuditEventCoverageContributor
{
    IReadOnlyList<AuditEventCoverageDefinitionDto> GetDefinitions();
}

public interface IAuditEventCoverageService
{
    AuditEventCoverageReportDto GetReport();
}

public sealed class AuditGovernanceNotFoundException(string message) : Exception(message);
public sealed class AuditGovernanceConflictException(string message) : Exception(message);
public sealed class AuditGovernanceValidationException(string message) : Exception(message);
