namespace ErpSystem.Core.Interfaces.Legal;

public interface ILegalProcedureCatalogService
{
    IReadOnlyList<LegalProcedureCatalogItem> GetProcedures();
    LegalProcedureWorkspace? GetProcedureWorkspace(string entityType);
}

public sealed record LegalProcedureCatalogItem(
    string Title,
    string EntityType,
    string Icon,
    int StageCount,
    string Accent);

public sealed record LegalProcedureWorkspace(
    LegalProcedureCatalogItem Procedure,
    IReadOnlyList<LegalWorkspaceStage> Stages,
    IReadOnlyList<LegalWorkspaceDocument> RequiredDocuments,
    IReadOnlyList<LegalWorkspaceField> IntakeFields,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<LegalWorkspaceHandoff> Handoffs);

public sealed record LegalWorkspaceStage(
    string Name,
    string Owner,
    IReadOnlyList<string> Checklist);

public sealed record LegalWorkspaceDocument(
    string Name,
    string RequiredFrom,
    bool IsMandatory);

public sealed record LegalWorkspaceField(
    string Key,
    string Label,
    string Type,
    IReadOnlyList<string>? Options = null);

public sealed record LegalWorkspaceHandoff(
    string FromRole,
    string ToRole,
    string Trigger);
