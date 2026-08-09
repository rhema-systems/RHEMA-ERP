namespace ErpSystem.Core.Services.QuantitySurvey;

public sealed record QuantitySurveyPermissionDefinition(string Code, string Name, string Description);
public sealed record QuantitySurveyRoleDefinition(string Code, string Name, string Description, IReadOnlyList<string> Permissions);
public sealed record QuantitySurveyOperationAccessDefinition(
    string Operation,
    string Permission,
    bool RequiresProjectMembership,
    bool RequiresConfiguredAuthority);

public static class QuantitySurveyAccessControlRegistry
{
    public const string Category = "Quantity Survey";
    public const string Read = "quantity-survey.configuration.read";
    public const string Manage = "quantity-survey.configuration.manage";
    public const string Approve = "quantity-survey.configuration.approve";
    public const string AuditRead = "quantity-survey.audit.read";
    public const string WorkspaceRead = "quantity-survey.workspace.read";
    public const string BoqManage = "quantity-survey.boq.manage";
    public const string RatesManage = "quantity-survey.rates.manage";
    public const string EstimatesManage = "quantity-survey.estimates.manage";
    public const string MeasurementsManage = "quantity-survey.measurements.manage";
    public const string ValuationsManage = "quantity-survey.valuations.manage";
    public const string CertificatesManage = "quantity-survey.certificates.manage";
    public const string VariationsManage = "quantity-survey.variations.manage";
    public const string ClaimsManage = "quantity-survey.claims.manage";
    public const string FinalAccountsManage = "quantity-survey.final-accounts.manage";
    public const string TransactionsApprove = "quantity-survey.transactions.approve";
    public const string ReportsRead = "quantity-survey.reports.read";
    public const string ExternalAccessManage = "quantity-survey.external-access.manage";

    public static IReadOnlyList<QuantitySurveyPermissionDefinition> Permissions { get; } =
    [
        new(Read, "Read QS configuration", "View effective and historical QS configuration profiles."),
        new(Manage, "Manage QS configuration", "Create and edit draft QS configuration profiles and evidence."),
        new(Approve, "Approve QS configuration", "Approve decisions and publish or retire QS configuration profiles."),
        new(AuditRead, "Read QS audit", "View immutable QS configuration and transaction audit history."),
        new(WorkspaceRead, "Read QS workspace", "View QS records only where central project and contract access also allow it."),
        new(BoqManage, "Manage QS BoQs", "Prepare, revise, import, compare, and submit BoQs within assigned projects."),
        new(RatesManage, "Manage QS rates", "Prepare rate libraries, market surveys, build-ups, and escalation inputs."),
        new(EstimatesManage, "Manage QS estimates", "Prepare cost plans, estimates, assumptions, and budget reconciliations."),
        new(MeasurementsManage, "Manage QS measurements", "Capture taking-off, measurement, and remeasurement records."),
        new(ValuationsManage, "Manage QS valuations", "Prepare and vet valuation worksheets within assigned projects and contracts."),
        new(CertificatesManage, "Manage QS certificates", "Prepare payment certificates, retention, and advance-recovery records."),
        new(VariationsManage, "Manage QS variations", "Prepare variation, change-order, and daywork commercial records."),
        new(ClaimsManage, "Manage QS claims", "Register and vet contractor or subcontractor claims."),
        new(FinalAccountsManage, "Manage QS final accounts", "Prepare and reconcile project and subcontract final accounts."),
        new(TransactionsApprove, "Approve QS transactions", "Approve governed QS records within configured authority and assigned project scope."),
        new(ReportsRead, "Read QS reports", "View QS reports and drilldowns within assigned project, contract, section, and unit scope."),
        new(ExternalAccessManage, "Manage QS external access", "Manage contractor and consultant access through the shared project external-access controls.")
    ];

    public static IReadOnlyList<QuantitySurveyRoleDefinition> Roles { get; } =
    [
        new("TDC_QUANTITY_SURVEYOR", "TDC Quantity Surveyor", "Prepares governed QS records within assigned projects and configured operational authority.", [Read, Manage, AuditRead, WorkspaceRead, BoqManage, RatesManage, EstimatesManage, MeasurementsManage, ValuationsManage, CertificatesManage, VariationsManage, ClaimsManage, FinalAccountsManage, ReportsRead]),
        new("TDC_SUPERVISING_QUANTITY_SURVEYOR", "TDC Supervising Quantity Surveyor", "Reviews and approves governed QS records within assigned projects and configured delegated authority.", [Read, Manage, Approve, AuditRead, WorkspaceRead, BoqManage, RatesManage, EstimatesManage, MeasurementsManage, ValuationsManage, CertificatesManage, VariationsManage, ClaimsManage, FinalAccountsManage, TransactionsApprove, ReportsRead, ExternalAccessManage]),
        new("TDC_ASSISTANT_QUANTITY_SURVEYOR", "TDC Assistant Quantity Surveyor", "Supports controlled QS preparation and evidence collection within assigned projects.", [Read, WorkspaceRead, BoqManage, RatesManage, EstimatesManage, MeasurementsManage, ValuationsManage, VariationsManage, ClaimsManage, ReportsRead])
    ];

    public static IReadOnlyList<QuantitySurveyOperationAccessDefinition> Operations { get; } =
    [
        new("ViewWorkspace", WorkspaceRead, true, false),
        new("ManageBoq", BoqManage, true, false),
        new("ManageRates", RatesManage, true, false),
        new("ManageEstimates", EstimatesManage, true, false),
        new("ManageMeasurements", MeasurementsManage, true, false),
        new("ManageValuations", ValuationsManage, true, false),
        new("ManageCertificates", CertificatesManage, true, false),
        new("ManageVariations", VariationsManage, true, false),
        new("ManageClaims", ClaimsManage, true, false),
        new("ManageFinalAccounts", FinalAccountsManage, true, false),
        new("ApproveTransaction", TransactionsApprove, true, true),
        new("ReadReports", ReportsRead, true, false),
        new("ManageExternalAccess", ExternalAccessManage, true, false),
        new("ManageConfiguration", Manage, false, false),
        new("ApproveConfiguration", Approve, false, false),
        new("ReadAudit", AuditRead, false, false)
    ];

    public static QuantitySurveyOperationAccessDefinition GetOperation(string operation)
        => Operations.SingleOrDefault(value => string.Equals(value.Operation, operation, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"QS access operation '{operation}' is not registered.");
}
