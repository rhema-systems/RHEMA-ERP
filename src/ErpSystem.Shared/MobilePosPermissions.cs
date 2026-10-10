namespace ErpSystem.Shared;

public sealed record MobilePosPermissionDefinition(
    string Name,
    string DisplayName,
    string Description,
    string Category);

/// <summary>
/// Permission catalogue for RHEMA Field POS and Revenue Collection. Tenant administrators may
/// assign these capabilities to any dynamic role; API authorization must not depend on role names.
/// </summary>
public static class MobilePosPermissions
{
    public const string CategoryAccess = "Mobile POS - Access";
    public const string CategoryAdministration = "Mobile POS - Administration";
    public const string CategoryTill = "Mobile POS - Till";
    public const string CategoryTransactions = "Mobile POS - Transactions";
    public const string CategoryOperations = "Mobile POS - Operations";

    public const string Access = "MobilePOS.Access";
    public const string ViewStore = "MobilePOS.Store.View";
    public const string ManageStore = "MobilePOS.Store.Manage";
    public const string EnrollDevice = "MobilePOS.Device.Enroll";
    public const string ApproveDevice = "MobilePOS.Device.Approve";
    public const string OperateTill = "MobilePOS.Till.Operate";
    public const string CloseTill = "MobilePOS.Till.Close";
    public const string ReviewTill = "MobilePOS.Till.Review";
    public const string CorrectTill = "MobilePOS.Till.Correct";
    public const string CreateInvoice = "MobilePOS.Invoice.Create";
    public const string ViewCustomer = "MobilePOS.Customer.View";
    public const string PostInvoice = "MobilePOS.Invoice.Post";
    public const string CollectPayment = "MobilePOS.Payment.Collect";
    public const string ApplyDiscount = "MobilePOS.Discount.Apply";
    public const string ReprintReceipt = "MobilePOS.Receipt.Reprint";
    public const string CreateReturn = "MobilePOS.Return.Create";
    public const string CreateReversal = "MobilePOS.Reversal.Create";
    public const string UseOffline = "MobilePOS.Offline.Use";
    public const string ResolveSync = "MobilePOS.Sync.Resolve";
    public const string ViewReports = "MobilePOS.Reports.View";

    public static readonly MobilePosPermissionDefinition[] All =
    [
        new(Access, "Access Mobile POS", "Open the RHEMA Field POS and Revenue Collection application.", CategoryAccess),
        new(ViewStore, "View Mobile POS Store", "View the assigned store, till, payment, receipt, and offline configuration.", CategoryAccess),
        new(ManageStore, "Manage Mobile POS Stores", "Configure stores, tills, walk-in customers, tender mappings, receipt settings, and offline policies.", CategoryAdministration),
        new(EnrollDevice, "Request Device Enrollment", "Request enrollment of the current mobile installation.", CategoryAdministration),
        new(ApproveDevice, "Approve Mobile Devices", "Approve, assign, suspend, revoke, or retire Mobile POS devices.", CategoryAdministration),
        new(OperateTill, "Operate Mobile POS Till", "Use an assigned till and open or operate the user's own custody session.", CategoryTill),
        new(CloseTill, "Submit Mobile POS Till Close", "Submit the user's till count and day-end evidence.", CategoryTill),
        new(ReviewTill, "Review Mobile POS Till Close", "Review variances and approve or return till closures.", CategoryTill),
        new(CorrectTill, "Correct Mobile POS Till Session", "Open a governed correction for a closed till session.", CategoryTill),
        new(CreateInvoice, "Create Mobile POS Invoice", "Create an invoice or immediate sale through Mobile POS.", CategoryTransactions),
        new(ViewCustomer, "View Mobile POS Customers", "Search approved customers and view their outstanding invoices in Mobile POS.", CategoryTransactions),
        new(PostInvoice, "Post Mobile POS Invoice", "Post a Mobile POS invoice where Finance policy permits immediate posting.", CategoryTransactions),
        new(CollectPayment, "Collect Mobile POS Payment", "Create and allocate customer receipts through Mobile POS.", CategoryTransactions),
        new(ApplyDiscount, "Apply Mobile POS Discount", "Apply a line discount during Mobile POS checkout.", CategoryTransactions),
        new(ReprintReceipt, "Reprint Mobile POS Receipt", "Reprint an existing receipt with audit evidence.", CategoryTransactions),
        new(CreateReturn, "Create Mobile POS Return", "Initiate a governed Mobile POS return or credit workflow.", CategoryTransactions),
        new(CreateReversal, "Create Mobile POS Reversal", "Initiate a governed Mobile POS reversal.", CategoryTransactions),
        new(UseOffline, "Use Mobile POS Offline", "Operate within a signed and unexpired Mobile POS offline grant.", CategoryOperations),
        new(ResolveSync, "Resolve Mobile POS Sync", "Review and resolve Mobile POS synchronization exceptions.", CategoryOperations),
        new(ViewReports, "View Mobile POS Reports", "View Mobile POS collections, till, tender, variance, and synchronization reports.", CategoryOperations)
    ];

    public static readonly string[] AllNames = All.Select(permission => permission.Name).ToArray();
}
