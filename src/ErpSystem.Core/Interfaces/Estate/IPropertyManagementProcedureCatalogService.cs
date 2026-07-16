namespace ErpSystem.Core.Interfaces.Estate;

public interface IPropertyManagementProcedureCatalogService
{
    IReadOnlyList<FacilitiesProcedureCatalogItem> GetProcedures();
    FacilitiesProcedureWorkspace? GetProcedureWorkspace(string entityType);
}
