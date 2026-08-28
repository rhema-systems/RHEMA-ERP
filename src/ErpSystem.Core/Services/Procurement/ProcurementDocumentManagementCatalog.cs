using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Shared;

namespace ErpSystem.Core.Services.Procurement;

public sealed record ProcurementDocumentFamilyDefinition(
    ProcurementDocumentFamily Code,
    string Name,
    string Description,
    string SourceEntityType,
    string TemplateCode,
    string TemplateDocumentType,
    string AccessProfile,
    string ReadPermission,
    IReadOnlyList<string> UploadPermissions,
    IReadOnlyList<string> Classifications);

public static class ProcurementDocumentManagementCatalog
{
    public const string SourceModule = "Procurement";

    public static IReadOnlyList<ProcurementDocumentFamilyDefinition> Families { get; } =
    [
        Family(ProcurementDocumentFamily.Requisition, "Purchase requisition documents",
            "Specifications, scope, drawings, estimates and other supporting records for the internal request.",
            "PurchaseRequisition", "TDC-PROC-REQUISITION", "PurchaseRequisitionEvidence", "Procurement requisition restricted",
            "procurement.records.read", ["procurement.requisition.create"],
            ["Specification", "Scope of work", "Drawing", "Cost estimate", "Budget support", "Justification", "Supporting document", "Other"]),
        Family(ProcurementDocumentFamily.Tender, "Tender documents",
            "Specifications, terms, notices, drawings and controlled addenda.",
            "Tender", "TDC-PROC-TENDER", "TenderDocument", "Procurement tender restricted",
            "procurement.records.read", ["procurement.sourcing.manage", "procurement.tender.administer"],
            ["Specification", "Terms and conditions", "Drawing", "Notice", "Addendum", "Other"]),
        Family(ProcurementDocumentFamily.Evaluation, "Evaluation evidence",
            "Evaluation working papers, score evidence, reports and committee records.",
            "TenderEvaluation", "TDC-PROC-EVALUATION", "TenderEvaluationEvidence", "Procurement evaluation restricted",
            "procurement.records.read", ["procurement.tender.evaluate", "procurement.tender.administer"],
            ["Evaluation report", "Score evidence", "Committee minutes", "Conflict declaration", "Due diligence", "Other"]),
        Family(ProcurementDocumentFamily.Approval, "Approval evidence",
            "Workflow decisions, authority evidence, signed approvals and statutory review records.",
            "WorkflowInstance", "TDC-PROC-APPROVAL", "ProcurementApprovalEvidence", "Procurement approval restricted",
            "procurement.records.read", ["procurement.sourcing.approve", "procurement.tender.approve", "procurement.purchase-order.approve", "procurement.contract.approve"],
            ["Approval decision", "Authority evidence", "Signed approval", "Statutory review", "Committee resolution", "Other"]),
        Family(ProcurementDocumentFamily.Supplier, "Supplier documents",
            "Supplier onboarding, compliance, eligibility and due-diligence evidence.",
            "BusinessPartnerRegistration", "PROC-SUP-EVD", "SupplierEvidence", "Procurement supplier restricted",
            "procurement.records.read", ["procurement.supplier.manage", "procurement.supplier.review"],
            ["Registration", "Statutory certificate", "Licence", "Tax clearance", "Financial statement", "Due diligence", "Other"]),
        Family(ProcurementDocumentFamily.Contract, "Contract documents",
            "Draft, executed, amendment, security and contract-administration evidence.",
            "Contract", "PROC-CON-EVD", "ContractEvidence", "Procurement contract restricted",
            "procurement.records.read", ["procurement.contract.manage"],
            ["Contract", "Amendment", "Addendum", "Specification", "Certificate", "Invoice", "Receipt", "Correspondence", "Other"]),
        Family(ProcurementDocumentFamily.GoodsReceiptNote, "GRN documents",
            "Goods receipt and inspection evidence linked to the governed GRN.",
            "GoodsReceiptNote", "TDC-PROC-GRN-EVD", "GoodsReceiptEvidence", "Procurement receipt restricted",
            "procurement.inventory.read", ["procurement.inventory.receive"],
            ["GRN", "Delivery note", "Inspection evidence", "Acceptance certificate", "Photo", "Other"]),
        Family(ProcurementDocumentFamily.MaterialReceiptNote, "MRN documents",
            "Material receipt and acceptance evidence linked to the governed MRN.",
            "ProcurementReceiptDocument", "TDC-PROC-MRN-EVD", "MaterialReceiptEvidence", "Procurement receipt restricted",
            "procurement.inventory.read", ["procurement.inventory.receive"],
            ["MRN", "Delivery note", "Inspection evidence", "Acceptance certificate", "Photo", "Other"]),
        Family(ProcurementDocumentFamily.VendorInvoice, "Vendor invoice documents",
            "Supplier invoice source documents and three-way-match evidence; Finance remains the invoice owner.",
            "VendorInvoice", "TDC-PROC-INVOICE", "VendorInvoiceEvidence", "Procurement invoice restricted",
            FinancePermissions.ViewFinance, [FinancePermissions.CreateApInvoices, FinancePermissions.ManageApInvoices, FinancePermissions.MaintainApInvoices],
            ["Supplier invoice", "Credit note", "Tax invoice", "Match evidence", "Supporting schedule", "Other"]),
        Family(ProcurementDocumentFamily.Disposal, "Disposal documents",
            "Identification, audit, committee, approval, execution and proceeds evidence.",
            "InventoryDisposalCase", "TDC-PROC-DISPOSAL", "InventoryDisposalEvidence", "Inventory disposal restricted",
            "procurement.inventory.read", ["procurement.inventory.disposal.request", "procurement.inventory.disposal.approve"],
            ["Identification evidence", "Audit verification", "Committee minutes", "Approval", "Execution evidence", "Proceeds evidence", "Other"])
    ];

    public static ProcurementDocumentFamilyDefinition? Find(ProcurementDocumentFamily code) =>
        Families.FirstOrDefault(item => item.Code == code);

    private static ProcurementDocumentFamilyDefinition Family(
        ProcurementDocumentFamily code,
        string name,
        string description,
        string sourceEntityType,
        string templateCode,
        string templateDocumentType,
        string accessProfile,
        string readPermission,
        IReadOnlyList<string> uploadPermissions,
        IReadOnlyList<string> classifications) =>
        new(code, name, description, sourceEntityType, templateCode, templateDocumentType,
            accessProfile, readPermission, uploadPermissions, classifications);
}
