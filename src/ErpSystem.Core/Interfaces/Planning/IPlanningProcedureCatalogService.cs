namespace ErpSystem.Core.Interfaces.Planning;

public interface IPlanningProcedureCatalogService
{
    IReadOnlyList<PlanningProcedureCatalogItem> GetProcedures();
    PlanningProcedureWorkspace? GetProcedureWorkspace(string entityType);
}

public sealed record PlanningProcedureCatalogItem(
    string Title,
    string EntityType,
    string Source,
    string Summary,
    string Icon,
    int StageCount,
    string Accent);

public sealed record PlanningProcedureWorkspace(
    PlanningProcedureCatalogItem Procedure,
    IReadOnlyList<PlanningWorkspaceStage> Stages,
    IReadOnlyList<PlanningWorkspaceDocument> RequiredDocuments,
    IReadOnlyList<PlanningWorkspaceField> IntakeFields,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<PlanningWorkspaceHandoff> Handoffs);

public sealed record PlanningWorkspaceStage(
    string Name,
    string Owner,
    string Summary,
    IReadOnlyList<string> Checklist);

public sealed record PlanningWorkspaceDocument(
    string Name,
    string RequiredFrom,
    bool IsMandatory);

public sealed record PlanningWorkspaceField(
    string Key,
    string Label,
    string Type,
    IReadOnlyList<string>? Options = null);

public sealed record PlanningWorkspaceHandoff(
    string FromRole,
    string ToRole,
    string Trigger);
