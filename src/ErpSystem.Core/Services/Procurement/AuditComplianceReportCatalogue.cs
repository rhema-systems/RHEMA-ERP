using ErpSystem.Core.DTOs.Reports;

namespace ErpSystem.Core.Services.Procurement;

public sealed record AuditComplianceSystemReportDefinition(
    string Code,
    string Name,
    string Description,
    IReadOnlyList<ReportColumnDto> Columns,
    IReadOnlyList<string> Tags)
{
    public string Query => AuditComplianceReportCatalogue.QueryPrefix + Code;
}

public static class AuditComplianceReportCatalogue
{
    public const string QueryPrefix = "system://tdc/compliance/";
    public const string ReportType = "compliance";
    public const string ReadPermission = ProcurementStatutoryReportCatalogue.ReadPermission;
    public const string ExportPermission = ProcurementStatutoryReportCatalogue.ExportPermission;

    public const string OpeningCode = "opening-register";
    public const string CommitteeSignOffCode = "committee-signoff-register";
    public const string DueDiligenceCode = "due-diligence-register";
    public const string MatchingExceptionCode = "matching-exception-register";
    public const string PurchaseOrderPaymentCode = "po-payment-register";
    public const string InventoryAdjustmentCode = "inventory-adjustment-register";
    public const string OverrideCode = "override-exception-register";
    public const string DisposalCode = "disposal-compliance-register";

    public static IReadOnlyList<AuditComplianceSystemReportDefinition> Definitions { get; } =
    [
        Definition(OpeningCode, "Tender and RFQ Opening Register",
            "Opening evidence across formal tenders and RFQs, including entry, lateness, participant sign-off and integrity proof.",
            C("SourceType", "Source type"), C("SourceReference", "Source reference"), C("Status", "Status"),
            C("OpenedAt", "Opened", "DateTime"), C("ClosedAt", "Closed", "DateTime"),
            C("EntryCount", "Entries", "Integer"), C("LateEntryCount", "Late entries", "Integer"),
            C("ParticipantCount", "Participants", "Integer"), C("ParticipantSignOffCaptured", "Participant sign-off", "Boolean"),
            C("EvidenceReference", "Evidence reference"), C("IntegrityHash", "Integrity hash")),
        Definition(CommitteeSignOffCode, "Evaluation Committee Sign-off Register",
            "Committee appointment, quorum, signed attendance, scoring and meeting evidence register.",
            C("SourceType", "Source type"), C("SourceReference", "Source reference"), C("CommitteeCode", "Committee code"),
            C("CommitteeName", "Committee"), C("Version", "Version", "Integer"), C("Phase", "Phase"),
            C("MeetingSequence", "Meeting no.", "Integer"), C("Status", "Status"),
            C("ScheduledAt", "Scheduled", "DateTime"), C("ClosedAt", "Closed", "DateTime"),
            C("RequiredQuorum", "Required quorum", "Integer"), C("EligibleVoters", "Eligible voters", "Integer"),
            C("SignedAttendance", "Signed attendance", "Integer"), C("QuorumMet", "Quorum met", "Boolean"),
            C("SignedScoreSheets", "Signed score sheets", "Integer"), C("EvidenceReference", "Meeting evidence"),
            C("RemoteEvidenceReference", "Remote evidence"), C("IntegrityHash", "Integrity hash")),
        Definition(DueDiligenceCode, "Supplier Due Diligence Register",
            "Supplier due-diligence cycles, checks, evidence, policy lineage and approval outcomes.",
            C("ReviewReference", "Review reference"), C("SupplierCode", "Supplier code"), C("SupplierName", "Supplier"),
            C("ReviewType", "Review type"), C("Status", "Status"), C("Outcome", "Outcome"),
            C("PeriodStart", "Period start", "DateTime"), C("PeriodEnd", "Period end", "DateTime"),
            C("NextReviewDue", "Next review due", "DateTime"), C("PolicyProfile", "Policy profile"),
            C("PolicyVersion", "Policy version", "Integer"), C("CheckCount", "Checks", "Integer"),
            C("FailedChecks", "Failed checks", "Integer"), C("PendingChecks", "Pending checks", "Integer"),
            C("EvidenceCount", "Evidence", "Integer"), C("SubmittedAt", "Submitted", "DateTime"),
            C("ApprovedAt", "Approved", "DateTime"), C("RejectedAt", "Rejected", "DateTime"),
            C("IntegrityHash", "Integrity hash")),
        Definition(MatchingExceptionCode, "Three-way Matching Exception Register",
            "Finance-owned invoice/PO/receipt matching exceptions with corrective action, evidence and independent decision lineage.",
            C("ExceptionReference", "Exception reference"), C("InvoiceNumber", "Invoice number"),
            C("SupplierName", "Supplier"), C("PurchaseOrderNumber", "PO number"), C("Status", "Status"),
            C("VarianceType", "Variance type"), C("PriceTolerancePercent", "Price tolerance %", "Decimal", "N2"),
            C("QuantityTolerancePercent", "Quantity tolerance %", "Decimal", "N2"), C("RootCauseCategory", "Root cause"),
            C("CorrectiveActionStatus", "Corrective action"), C("CorrectiveActionDue", "Corrective due", "DateTime"),
            C("RequestedBy", "Requested by"), C("RequestedAt", "Requested", "DateTime"),
            C("DecisionBy", "Decision by"), C("DecisionAt", "Decision", "DateTime"),
            C("ExpiresAt", "Expires", "DateTime"), C("EvidenceCount", "Evidence", "Integer"),
            C("ApprovalControlEventId", "Approval event"), C("IntegrityHash", "Integrity hash")),
        Definition(PurchaseOrderPaymentCode, "Purchase Order Payment Register",
            "PO-linked supplier payment allocations with readiness, SOD, authorization, posting and reversal lineage.",
            C("PaymentNumber", "Payment number"), C("PaymentDate", "Payment date", "DateTime"), C("Status", "Status"),
            C("SupplierCode", "Supplier code"), C("SupplierName", "Supplier"), C("InvoiceNumber", "Invoice number"),
            C("PurchaseOrderNumber", "PO number"), C("Currency", "Currency"),
            C("PaymentAmount", "Payment amount", "Decimal", "N2"), C("AllocatedAmount", "Allocated amount", "Decimal", "N2"),
            C("PaymentMethod", "Payment method"), C("IsReversal", "Reversal", "Boolean"),
            C("ReadinessEvaluatedAt", "Readiness evaluated", "DateTime"), C("ReadinessControlEventId", "Readiness event"),
            C("SodControlEventId", "SOD event"), C("AuthorizedAt", "Authorized", "DateTime"),
            C("ClearedAt", "Cleared", "DateTime"), C("JournalEntryId", "Journal entry"),
            C("TransactionReference", "Transaction reference")),
        Definition(InventoryAdjustmentCode, "Inventory Adjustment Register",
            "Exact-location stock adjustments with quantities, approvals, evidence, Finance posting and reversal lineage.",
            C("AdjustmentNumber", "Adjustment number"), C("AdjustmentDate", "Adjustment date", "DateTime"),
            C("WarehouseCode", "Warehouse code"), C("WarehouseName", "Warehouse"), C("Status", "Status"),
            C("ReasonCode", "Reason code"), C("ItemCode", "Item code"), C("ItemName", "Item"),
            C("LocationCode", "Location"), C("SystemQuantity", "System quantity", "Decimal", "N2"),
            C("PhysicalQuantity", "Physical quantity", "Decimal", "N2"),
            C("AdjustmentQuantity", "Adjustment quantity", "Decimal", "N2"),
            C("UnitCost", "Unit cost", "Decimal", "N2"), C("AdjustmentValue", "Adjustment value", "Decimal", "N2"),
            C("SubmittedAt", "Submitted", "DateTime"), C("ApprovedAt", "Approved", "DateTime"),
            C("PostedAt", "Posted", "DateTime"), C("ReversedAt", "Reversed", "DateTime"),
            C("FinanceJournalEntryId", "Finance journal"), C("EvidenceCount", "Evidence", "Integer"),
            C("ActionCount", "Actions", "Integer"), C("IntegrityHash", "Integrity hash")),
        Definition(OverrideCode, "Override and Exception Register",
            "Approved, denied and consumed policy exceptions and emergency overrides with decision and evidence lineage.",
            C("EventKind", "Event kind"), C("Action", "Action"), C("Result", "Result"), C("RuleCode", "Rule code"),
            C("SourceType", "Source type"), C("SourceReference", "Source reference"), C("ItemCode", "Item code"),
            C("WarehouseCode", "Warehouse code"), C("LocationCode", "Location"), C("ActorReference", "Actor reference"),
            C("OccurredAt", "Occurred", "DateTime"), C("ApprovedAt", "Approved", "DateTime"),
            C("ExpiresAt", "Expires", "DateTime"), C("ConsumedAt", "Consumed", "DateTime"),
            C("EvidenceReference", "Evidence reference"), C("CorrelationId", "Correlation ID"),
            C("IntegrityHash", "Integrity hash")),
        Definition(DisposalCode, "Inventory Disposal Compliance Register",
            "Governed disposal identification, audit verification, committee, approval, execution, stock and proceeds evidence.",
            C("DisposalNumber", "Disposal number"), C("Status", "Status"), C("Method", "Method"),
            C("RequestedAt", "Requested", "DateTime"), C("WarehouseCode", "Warehouse code"), C("WarehouseName", "Warehouse"),
            C("ItemCode", "Item code"), C("ItemName", "Item"), C("LocationCode", "Location"),
            C("Quantity", "Quantity", "Decimal", "N2"), C("UnitCost", "Unit cost", "Decimal", "N2"),
            C("LineValue", "Line value", "Decimal", "N2"), C("AuditVerifiedAt", "Audit verified", "DateTime"),
            C("CommitteeReference", "Committee reference"), C("ApprovedAt", "Approved", "DateTime"),
            C("StockAdjustmentId", "Stock adjustment"), C("ProceedsAmount", "Proceeds", "Decimal", "N2"),
            C("ExecutionReference", "Execution reference"), C("CompletedAt", "Completed", "DateTime"),
            C("EvidenceCount", "Evidence", "Integer"), C("ActionCount", "Actions", "Integer"))
    ];

    public static AuditComplianceSystemReportDefinition? Resolve(string? query)
    {
        if (string.IsNullOrWhiteSpace(query) || !query.StartsWith(QueryPrefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var code = query[QueryPrefix.Length..].Trim();
        return Definitions.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
    }

    public static Dictionary<string, object> BuildParameters() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["startDate"] = Parameter("Start date", "date"),
        ["endDate"] = Parameter("End date", "date"),
        ["status"] = Parameter("Status", "text"),
        ["warehouseId"] = Parameter("Warehouse", "warehouse")
    };

    private static AuditComplianceSystemReportDefinition Definition(
        string code,
        string name,
        string description,
        params ReportColumnDto[] columns) =>
        new(code, name, description, columns,
            ["TDC-0704", "RPT-004", "audit", "compliance", code]);

    private static ReportColumnDto C(string name, string displayName, string type = "String", string? format = null) =>
        new() { Name = name, DisplayName = displayName, DataType = type, Format = format, IsVisible = true };

    private static Dictionary<string, object> Parameter(string label, string type) => new()
    {
        ["label"] = label,
        ["type"] = type,
        ["required"] = false
    };
}
