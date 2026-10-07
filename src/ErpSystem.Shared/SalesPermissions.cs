namespace ErpSystem.Shared;

public sealed record SalesPermissionDefinition(
    string Name,
    string DisplayName,
    string Description,
    string Category);

public static class SalesPermissions
{
    public const string Category = "Sales";

    public const string Access = "sales.access";
    public const string Read = "sales.read";
    public const string Manage = "sales.manage";
    public const string Approve = "sales.approve";
    public const string Configure = "sales.configure";
    public const string ViewReports = "sales.reports.read";

    public static readonly SalesPermissionDefinition[] All =
    [
        new(Access, "Access Sales", "Access Sales workspaces and navigation.", Category),
        new(Read, "View Sales", "View Sales records, transactions, and operational status.", Category),
        new(Manage, "Manage Sales", "Create and maintain Sales records and transactions.", Category),
        new(Approve, "Approve Sales", "Approve governed Sales transactions and commercial decisions.", Category),
        new(Configure, "Configure Sales", "Manage Sales setup, sources, templates, and reference configuration.", Category),
        new(ViewReports, "View Sales Reports", "View Sales reports, summaries, forecasts, and analytics.", Category)
    ];

    public static readonly string[] AllNames = All.Select(permission => permission.Name).ToArray();
}
