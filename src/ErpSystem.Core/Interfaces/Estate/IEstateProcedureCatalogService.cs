namespace ErpSystem.Core.Interfaces.Estate;

public interface IEstateProcedureCatalogService
{
    IReadOnlyList<EstateProcedureCatalogItem> GetProcedures();
}

public sealed record EstateProcedureCatalogItem(
    string Title,
    string EntityType,
    string Source,
    string Summary,
    string Icon,
    int StageCount,
    string Accent,
    string WorkspaceType = "Case Workflow");
