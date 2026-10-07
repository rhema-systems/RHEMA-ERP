namespace ErpSystem.Shared;

public sealed record CrmPermissionDefinition(
    string Name,
    string DisplayName,
    string Description,
    string Category);

public static class CrmPermissions
{
    public const string Category = "CRM";

    public const string Access = "crm.access";
    public const string Read = "crm.read";
    public const string Manage = "crm.manage";

    public static readonly CrmPermissionDefinition[] All =
    [
        new(Access, "Access CRM", "Access CRM workspaces and navigation.", Category),
        new(Read, "View CRM", "View CRM accounts, leads, opportunities, activities, and opportunity stages.", Category),
        new(Manage, "Manage CRM", "Manage CRM records and opportunity-stage configuration.", Category)
    ];

    public static readonly string[] AllNames = All.Select(permission => permission.Name).ToArray();
}
