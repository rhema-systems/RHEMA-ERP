namespace ErpSystem.Core.Services.Procurement;

public sealed record ProcurementPermissionDefinition(
    string Code,
    string Name,
    string Description,
    bool IsMutation,
    bool IsWarehouseScoped = false);

public sealed record ProcurementRoleDefinition(
    string Code,
    string Name,
    string Description,
    IReadOnlyList<string> PermissionCodes,
    bool IsReadOnly = false);

public sealed record ProcurementCommitteeTemplate(
    string Code,
    string Name,
    string Description,
    string CommitteeType,
    int SuggestedQuorum,
    string RequiredRoleCode);

public sealed record ProcurementWorkflowTemplate(
    string Code,
    string Name,
    string Description,
    string EntityTypeCode,
    string EntityTypeName,
    string InitiatorRoleCode,
    string ApprovalRoleCode);

public static class ProcurementAccessControlRegistry
{
    public const string Category = "TDC Procurement";
    public const string InternalAuditRole = "TDC_INTERNAL_AUDIT";
    public const string IctAdministratorRole = "TDC_ICT_ADMINISTRATOR";
    public const string SupplierPaymentVerifyPermission =
        "procurement.supplier.payment.verify";
    public const string TenderPaymentVerifyPermission =
        "procurement.tender.payment.verify";

    public static IReadOnlyList<ProcurementPermissionDefinition> Permissions { get; } =
    [
        P("procurement.records.read", "Read procurement records", "View procurement, supplier, contract, approval, and stores records.", false),
        P("procurement.reports.read", "Read procurement reports", "View procurement and inventory reports.", false),
        P("procurement.reports.export", "Export procurement reports", "Export authorised procurement and inventory reports.", false),
        P("procurement.audit.read", "Read procurement audit", "View immutable procurement, access, approval, and inventory audit history.", false),
        P("procurement.calendar.view", "View procurement calendar", "View annual procurement obligations, owned tasks, run history, and escalation status.", false),
        P("procurement.calendar.manage", "Manage procurement calendar", "Configure, publish, retire, reschedule, and cancel controlled procurement calendar obligations.", true),
        P("procurement.calendar.run", "Run procurement calendar", "Run tenant-scoped calendar generation, catch-up, reminder, and escalation processing.", true),
        P("procurement.plan.manage", "Manage procurement plans", "Prepare and consolidate procurement plans.", true),
        P("procurement.plan.approve", "Approve procurement plans", "Approve procurement plans within delegated authority.", true),
        P("procurement.budget.manage", "Manage procurement budgets", "Prepare departmental procurement budgets and submit them to the shared workflow.", true),
        P("procurement.budget.approve", "Approve procurement budgets", "Approve or reject procurement budgets at an assigned shared workflow step.", true),
        P("procurement.requisition.create", "Create requisitions", "Create and submit procurement requisitions.", true),
        P("procurement.requisition.process", "Process requisitions", "Review and process approved requisitions for sourcing.", true),
        P("procurement.requisition.approve", "Approve requisitions", "Approve requisitions within delegated authority.", true),
        P("procurement.sourcing.manage", "Manage sourcing", "Administer RFQ, tender, restricted, single-source, QBS, QCBS, and framework sourcing.", true),
        P("procurement.sourcing.approve", "Approve sourcing", "Approve sourcing process decisions within delegated authority.", true),
        P("procurement.tender.administer", "Administer tenders", "Manage tender issue, receipt, opening, clarification, and recommendation records.", true),
        P("procurement.tender.evaluate", "Evaluate tenders", "Record technical and financial tender evaluation scores and sign-off.", true),
        P(TenderPaymentVerifyPermission, "Verify tender fee payments", "Independently verify or reject supplier tender-fee payment claims before bid submission.", true),
        P("procurement.tender.observe", "Observe tender controls", "Observe tender opening, evaluation, and approval records without mutation.", false),
        P("procurement.tender.approve", "Approve tender awards", "Approve tender recommendations and awards within delegated authority.", true),
        P("procurement.supplier.manage", "Manage supplier onboarding", "Prepare supplier onboarding, compliance, AVL, and profile records.", true),
        P("procurement.supplier.review", "Review supplier compliance", "Review supplier due diligence, eligibility, and risk evidence.", false),
        P(SupplierPaymentVerifyPermission, "Verify supplier onboarding payments", "Independently verify a supplier-onboarding payment claim, post the trusted receipt to Finance, and activate the applicant token.", true),
        P("procurement.supplier.approve", "Approve suppliers", "Approve supplier onboarding and AVL decisions.", true),
        P("procurement.purchase-order.create", "Create purchase orders", "Create purchase orders from approved procurement sources.", true),
        P("procurement.purchase-order.approve", "Approve purchase orders", "Approve purchase orders within delegated authority.", true),
        P("procurement.contract.manage", "Manage procurement contracts", "Prepare and administer procurement contracts and amendments.", true),
        P("procurement.contract.approve", "Approve procurement contracts", "Approve procurement contracts and controlled amendments.", true),
        P("procurement.inventory.read", "Read inventory records", "View warehouse, stock, movement, receipt, count, transfer, and disposal records.", false, true),
        P("procurement.inventory.receive", "Receive inventory", "Record GRN/MRN receipt and inspection activity for assigned warehouses.", true, true),
        P("procurement.inventory.issue", "Issue inventory", "Issue approved stock from assigned warehouses.", true, true),
        P("procurement.inventory.transfer", "Transfer inventory", "Initiate and process transfers for assigned warehouses.", true, true),
        P("procurement.inventory.count", "Count inventory", "Perform cycle and physical counts for assigned warehouses.", true, true),
        P("procurement.inventory.adjust.request", "Request stock adjustments", "Prepare stock-adjustment requests for assigned warehouses.", true, true),
        P("procurement.inventory.adjust.approve", "Approve stock adjustments", "Approve controlled stock adjustments for assigned warehouses.", true, true),
        P("procurement.inventory.master-data.manage", "Manage inventory master data", "Maintain governed inventory-item identifiers, units, and imports.", true),
        P("Inventory.EmergencyOverride", "Use emergency negative-stock override", "Consume an independently approved DEC-010 emergency override for an exact warehouse transaction.", true, true),
        P("procurement.inventory.disposal.request", "Request inventory disposal", "Initiate disposal or write-off for assigned warehouses.", true, true),
        P("procurement.inventory.disposal.approve", "Approve inventory disposal", "Approve disposal or write-off within delegated authority.", true, true),
        P("procurement.access.manage", "Manage procurement access", "Configure TDC procurement responsibilities, committees, and warehouse scopes.", true),
        P("procurement.workflow.configure", "Configure procurement workflows", "Configure TDC procurement Draft workflow definitions in the shared workflow designer.", true)
    ];

    private static readonly string[] Read = ["procurement.records.read", "procurement.reports.read", "procurement.calendar.view"];
    private static readonly string[] AuditRead = [.. Read, "procurement.reports.export", "procurement.audit.read"];

    public static IReadOnlyList<ProcurementRoleDefinition> Roles { get; } =
    [
        R("TDC_REQUISITIONER", "TDC Requisitioner", "User-department requester.", [.. Read, "procurement.requisition.create"]),
        R("TDC_USER_DEPARTMENT_HEAD", "TDC User Department Head", "User-department oversight and requisition approval.", [.. Read, "procurement.requisition.create", "procurement.requisition.approve"]),
        R("TDC_PROCUREMENT_OFFICER", "TDC Procurement Officer", "Requisition processing, sourcing, tender administration, PO creation, supplier and contract follow-up.", [.. Read, "procurement.plan.manage", "procurement.budget.manage", "procurement.calendar.manage", "procurement.calendar.run", "procurement.requisition.create", "procurement.requisition.process", "procurement.sourcing.manage", "procurement.tender.administer", TenderPaymentVerifyPermission, "procurement.supplier.manage", SupplierPaymentVerifyPermission, "procurement.purchase-order.create", "procurement.contract.manage"]),
        R("TDC_SENIOR_PROCUREMENT_OFFICER", "TDC Senior Procurement Officer", "Senior operational procurement oversight.", [.. Read, "procurement.plan.manage", "procurement.budget.manage", "procurement.calendar.manage", "procurement.calendar.run", "procurement.requisition.create", "procurement.requisition.process", "procurement.sourcing.manage", "procurement.sourcing.approve", "procurement.tender.administer", TenderPaymentVerifyPermission, "procurement.supplier.manage", "procurement.supplier.review", "procurement.purchase-order.create", "procurement.contract.manage"]),
        R("TDC_HEAD_OF_PROCUREMENT", "TDC Head of Procurement", "Procurement oversight, sourcing approval, reporting, supplier and contract oversight.", [.. AuditRead, "procurement.plan.manage", "procurement.plan.approve", "procurement.calendar.manage", "procurement.calendar.run", "procurement.requisition.process", "procurement.sourcing.manage", "procurement.sourcing.approve", "procurement.tender.administer", "procurement.tender.approve", "procurement.supplier.review", "procurement.supplier.approve", "procurement.purchase-order.approve", "procurement.contract.manage", "procurement.contract.approve"]),
        R("TDC_MANAGING_DIRECTOR", "TDC Managing Director", "Head of Entity and delegated executive approver.", [.. AuditRead, "procurement.plan.approve", "procurement.requisition.approve", "procurement.sourcing.approve", "procurement.tender.approve", "procurement.purchase-order.approve", "procurement.contract.approve", "procurement.inventory.disposal.approve"]),
        R("TDC_EXECUTIVE_APPROVER", "TDC Executive Approver", "Executive approval within delegated thresholds.", [.. Read, "procurement.requisition.approve", "procurement.sourcing.approve", "procurement.purchase-order.approve", "procurement.contract.approve"]),
        R("TDC_BOARD_APPROVER", "TDC Board Approver", "Board-level approval and oversight.", [.. AuditRead, "procurement.plan.approve", "procurement.sourcing.approve", "procurement.tender.approve", "procurement.contract.approve", "procurement.inventory.disposal.approve"]),
        R("TDC_ETC_MEMBER", "TDC Entity Tender Committee Member", "Entity Tender Committee review and approval.", [.. Read, "procurement.sourcing.approve", "procurement.tender.approve"]),
        R("TDC_CENTRAL_REVIEW_MEMBER", "TDC Central Tender Review Member", "Central Tender Review Committee review and approval.", [.. Read, "procurement.sourcing.approve", "procurement.tender.approve"]),
        R("TDC_EVALUATOR", "TDC Tender Evaluator", "Assigned technical and financial evaluator.", [.. Read, "procurement.tender.evaluate"]),
        R("TDC_OBSERVER", "TDC Procurement Observer", "Non-voting tender and procurement observer.", [.. Read, "procurement.tender.observe"], true),
        R("TDC_STORES_OFFICER", "TDC Stores Officer", "Warehouse receipt, issue, transfer, count, and disposal initiation restricted to assigned locations.", [.. Read, "procurement.inventory.read", "procurement.inventory.receive", "procurement.inventory.issue", "procurement.inventory.transfer", "procurement.inventory.count", "procurement.inventory.adjust.request", "procurement.inventory.disposal.request"]),
        R("TDC_STORES_MANAGER", "TDC Stores Manager", "Stores oversight and controlled warehouse approvals.", [.. AuditRead, "procurement.inventory.read", "procurement.inventory.receive", "procurement.inventory.issue", "procurement.inventory.transfer", "procurement.inventory.count", "procurement.inventory.adjust.request", "procurement.inventory.adjust.approve", "procurement.inventory.master-data.manage", "Inventory.EmergencyOverride", "procurement.inventory.disposal.request", "procurement.inventory.disposal.approve"]),
        R("TDC_FINANCE_REVIEWER", "TDC Finance Reviewer", "Budget, financial-control, PO, stock-variance, and disposal review.", [.. AuditRead, "procurement.budget.approve", "procurement.requisition.approve", "procurement.sourcing.approve", "procurement.purchase-order.approve", "procurement.inventory.read", "procurement.inventory.adjust.approve", "procurement.inventory.disposal.approve"]),
        R("TDC_LEGAL_REVIEWER", "TDC Legal Reviewer", "Legal review of sourcing, supplier, award, and contract decisions.", [.. Read, "procurement.sourcing.approve", "procurement.supplier.review", "procurement.contract.approve"]),
        R(InternalAuditRole, "TDC Internal Audit", "Read-only access to procurement, inventory, supplier, contract, approval, evidence, and audit records.", [.. AuditRead, "procurement.tender.observe", "procurement.supplier.review", "procurement.inventory.read"], true),
        R(IctAdministratorRole, "TDC ICT Administrator", "Identity, access, configuration, security, and shared-workflow administration.", ["procurement.access.manage", "procurement.workflow.configure", "procurement.calendar.view", "procurement.calendar.manage", "procurement.calendar.run", "procurement.inventory.master-data.manage"]),
        R("TDC_DISPOSAL_COMMITTEE_MEMBER", "TDC Disposal Committee Member", "Disposal and write-off committee review and approval.", [.. Read, "procurement.inventory.read", "procurement.inventory.disposal.approve"])
    ];

    public static IReadOnlyList<ProcurementCommitteeTemplate> Committees { get; } =
    [
        new("TDC_ETC", "Entity Tender Committee", "Entity-level tender review and approval; membership and quorum require TDC approval.", "EntityTenderCommittee", 3, "TDC_ETC_MEMBER"),
        new("TDC_CTRC", "Central Tender Review Committee", "Central review for procurement above delegated thresholds.", "CentralTenderReviewCommittee", 3, "TDC_CENTRAL_REVIEW_MEMBER"),
        new("TDC_EVALUATION", "Evaluation Committee", "Duly constituted technical and financial evaluation panel.", "EvaluationCommittee", 3, "TDC_EVALUATOR"),
        new("TDC_DISPOSAL", "Disposal Committee", "Committee for obsolete, expired, damaged, surplus, write-off, and disposal decisions.", "DisposalCommittee", 3, "TDC_DISPOSAL_COMMITTEE_MEMBER")
    ];

    public static IReadOnlyList<ProcurementWorkflowTemplate> Workflows { get; } =
    [
        W("TDC_PROCUREMENT_PLAN", "TDC Procurement Plan Approval", "PROCUREMENT_PLAN", "Procurement Plan", "TDC_HEAD_OF_PROCUREMENT", "TDC_MANAGING_DIRECTOR"),
        W("TDC_PROCUREMENT_BUDGET", "TDC Procurement Budget Approval", "PROCUREMENT_BUDGET", "Procurement Budget", "TDC_PROCUREMENT_OFFICER", "TDC_FINANCE_REVIEWER"),
        W("TDC_PURCHASE_REQUISITION", "TDC Purchase Requisition Approval", "PURCHASE_REQUISITION", "Purchase Requisition", "TDC_REQUISITIONER", "TDC_MANAGING_DIRECTOR"),
        W("TDC_SOURCING", "TDC Sourcing Approval", "PROCUREMENT_SOURCING", "Procurement Sourcing", "TDC_PROCUREMENT_OFFICER", "TDC_HEAD_OF_PROCUREMENT"),
        W("TDC_TENDER_EVALUATION", "TDC Tender Evaluation", "TENDER_EVALUATION", "Tender Evaluation", "TDC_PROCUREMENT_OFFICER", "TDC_EVALUATOR"),
        W("TDC_TENDER_AWARD", "TDC Tender Award Approval", "TENDER_AWARD", "Tender Award", "TDC_HEAD_OF_PROCUREMENT", "TDC_ETC_MEMBER"),
        W("TDC_PURCHASE_ORDER", "TDC Purchase Order Approval", "PURCHASE_ORDER", "Purchase Order", "TDC_PROCUREMENT_OFFICER", "TDC_HEAD_OF_PROCUREMENT"),
        W("TDC_CONTRACT", "TDC Procurement Contract Approval", "PROCUREMENT_CONTRACT", "Procurement Contract", "TDC_PROCUREMENT_OFFICER", "TDC_LEGAL_REVIEWER"),
        W("TDC_SUPPLIER_ONBOARDING", "TDC Supplier Onboarding Approval", "SUPPLIER_ONBOARDING", "Supplier Onboarding", "TDC_PROCUREMENT_OFFICER", "TDC_HEAD_OF_PROCUREMENT"),
        W("TDC_SUPPLIER_CHANGE", "TDC Supplier Change Approval", "SUPPLIER_CHANGE", "Supplier Change", "TDC_PROCUREMENT_OFFICER", "TDC_HEAD_OF_PROCUREMENT"),
        W("TDC_STOCK_ADJUSTMENT", "TDC Stock Adjustment Approval", "STOCK_ADJUSTMENT", "Stock Adjustment", "TDC_STORES_OFFICER", "TDC_STORES_MANAGER"),
        W("TDC_INVENTORY_TRANSFER", "TDC Inventory Transfer Approval", "INVENTORY_TRANSFER", "Inventory Transfer", "TDC_STORES_OFFICER", "TDC_STORES_MANAGER"),
        W("TDC_SUPPLIER_RETURN", "TDC Supplier Return Approval", "SUPPLIER_RETURN", "Supplier Return", "TDC_STORES_OFFICER", "TDC_STORES_MANAGER"),
        W("TDC_RECEIPT_INSPECTION", "Procurement Receipt Inspection Approval", "PROCUREMENT_RECEIPT_INSPECTION", "Procurement Receipt Inspection", "TDC_STORES_OFFICER", "TDC_STORES_MANAGER"),
        W("TDC_DISPOSAL", "TDC Inventory Disposal Approval", "INVENTORY_DISPOSAL", "Inventory Disposal", "TDC_STORES_MANAGER", "TDC_DISPOSAL_COMMITTEE_MEMBER"),
        W("TDC_PROCUREMENT_EXCEPTION", "TDC Procurement Exception Approval", "PROCUREMENT_EXCEPTION", "Procurement Exception", "TDC_HEAD_OF_PROCUREMENT", "TDC_MANAGING_DIRECTOR")
    ];

    public static ProcurementRoleDefinition? FindRole(string code) =>
        Roles.FirstOrDefault(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));

    public static ProcurementPermissionDefinition? FindPermission(string code) =>
        Permissions.FirstOrDefault(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));

    private static ProcurementPermissionDefinition P(string code, string name, string description, bool mutation, bool warehouse = false) =>
        new(code, name, description, mutation, warehouse);

    private static ProcurementRoleDefinition R(string code, string name, string description, IReadOnlyList<string> permissions, bool readOnly = false) =>
        new(code, name, description, permissions, readOnly);

    private static ProcurementWorkflowTemplate W(string code, string name, string entityCode, string entityName, string initiator, string approver) =>
        new(code, name, "Unapproved TDC Draft route seeded from the SRS; configure and publish only after DEC-003/DEC-004 approval.", entityCode, entityName, initiator, approver);
}
