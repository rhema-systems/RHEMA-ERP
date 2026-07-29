namespace ErpSystem.Core.Interfaces.DocumentManagement;

public interface ICentralDocumentManagementService
{
    IReadOnlyList<CentralDocumentWorkspaceItem> GetWorkspaces();
    CentralDocumentWorkspace? GetWorkspace(string entityType);
    CentralDocumentDashboard GetDashboard();
    IReadOnlyList<CentralDocumentMetadataTemplate> GetMetadataTemplates();
    IReadOnlyList<CentralDocumentRegisterItem> GetRegisterItems();
}

public sealed record CentralDocumentWorkspaceItem(
    string Title,
    string EntityType,
    string Source,
    string Summary,
    string Icon,
    int StageCount,
    string Accent);

public sealed record CentralDocumentWorkspace(
    CentralDocumentWorkspaceItem Workspace,
    IReadOnlyList<CentralDocumentStage> Stages,
    IReadOnlyList<CentralDocumentField> Fields,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<CentralDocumentHandoff> Handoffs);

public sealed record CentralDocumentStage(
    string Name,
    string Owner,
    string Summary,
    IReadOnlyList<string> Checklist);

public sealed record CentralDocumentField(
    string Key,
    string Label,
    string Type,
    IReadOnlyList<string>? Options = null);

public sealed record CentralDocumentHandoff(
    string FromModule,
    string ToModule,
    string Trigger);

public sealed record CentralDocumentDashboard(
    IReadOnlyList<CentralDocumentMetric> Metrics,
    IReadOnlyList<CentralDocumentMetric> Readiness,
    IReadOnlyList<CentralDocumentModuleQueue> ModuleQueues);

public sealed record CentralDocumentMetric(
    string Label,
    string Value,
    string Detail,
    string Trend);

public sealed record CentralDocumentModuleQueue(
    string Module,
    string SourceLabel,
    int PendingMetadata,
    int PendingVersion,
    int OpenAnnotations,
    int RetentionReviews);

public sealed record CentralDocumentMetadataTemplate(
    string Module,
    string DocumentType,
    string TemplateCode,
    IReadOnlyList<string> RequiredFields,
    IReadOnlyList<string> Relationships,
    string RetentionRule,
    string AccessProfile,
    string SourceLabel);

public sealed record CentralDocumentRegisterItem(
    string DocumentReference,
    string Title,
    string Module,
    string SourceRecord,
    string TemplateCode,
    string Version,
    string RepositoryStatus,
    string AnnotationStatus,
    string CommentStatus,
    string RetentionStatus,
    string SourceLabel);
