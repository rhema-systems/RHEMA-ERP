namespace ErpSystem.Core.Interfaces.Estate;

public interface IFacilitiesProcedureCatalogService
{
    IReadOnlyList<FacilitiesProcedureCatalogItem> GetProcedures();
    FacilitiesProcedureWorkspace? GetProcedureWorkspace(string entityType);
}

public sealed record FacilitiesProcedureCatalogItem(
    string Title,
    string EntityType,
    string Source,
    string Summary,
    string Icon,
    int StageCount,
    string Accent);

public sealed record FacilitiesProcedureWorkspace(
    FacilitiesProcedureCatalogItem Procedure,
    IReadOnlyList<FacilitiesWorkspaceStage> Stages,
    IReadOnlyList<FacilitiesWorkspaceDocument> RequiredDocuments,
    IReadOnlyList<FacilitiesWorkspaceField> IntakeFields,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<FacilitiesWorkspaceHandoff> Handoffs);

public sealed record FacilitiesWorkspaceStage(
    string Name,
    string Owner,
    string Summary,
    IReadOnlyList<string> Checklist);

public sealed record FacilitiesWorkspaceDocument(
    string Name,
    string RequiredFrom,
    bool IsMandatory);

public sealed record FacilitiesWorkspaceField(
    string Key,
    string Label,
    string Type,
    IReadOnlyList<string>? Options = null);

public sealed record FacilitiesWorkspaceHandoff(
    string FromRole,
    string ToRole,
    string Trigger);
