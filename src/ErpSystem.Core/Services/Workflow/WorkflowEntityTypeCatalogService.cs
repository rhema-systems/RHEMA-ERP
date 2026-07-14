using ErpSystem.Core.Interfaces.Workflow;

namespace ErpSystem.Core.Services.Workflow;

public sealed class WorkflowEntityTypeCatalogService : IWorkflowEntityTypeCatalogService
{
    public IReadOnlyList<WorkflowEntityTypeCatalogItem> GetDefaultEntityTypes()
    {
        var defaults = new List<WorkflowEntityTypeCatalogItem>
        {
            new("WorkOrder", "Maintenance", "Maintenance work orders", "Settings", "#3B82F6", 10),
            new("JobCard", "Maintenance", "Maintenance job cards", "FileText", "#8B5CF6", 20),
            new("FleetTrip", "Maintenance", "Fleet trip requests and dispatch", "MapPin", "#0EA5E9", 25),
            new("FleetTripInspection", "Maintenance", "Fleet pre-start, post-trip, inspection, and service sheet approvals", "ClipboardCheck", "#14B8A6", 26),
            new("PurchaseOrder", "Procurement", "Procurement purchase orders", "FileText", "#F59E0B", 30),
            new("ProcurementPlan", "Procurement", "Annual, quarterly, and amended procurement plans", "ClipboardList", "#2563EB", 35),
            new("PurchaseRequisition", "Procurement", "Procurement requisitions", "FileText", "#F97316", 40),
            new("Tender", "Procurement", "Procurement tenders (RFQ/RFP/ITB/EOI)", "FileText", "#06B6D4", 45),
            new("RFQ", "Procurement", "Requests for Quotation (RFQs)", "FileText", "#06B6D4", 46),
            new("SupplierQuote", "Procurement", "Supplier quotes submitted in response to RFQs", "FileText", "#22C55E", 47),
            new("Bid", "Procurement", "Supplier bids submitted in response to tenders", "FileText", "#22C55E", 48),
            new("Evaluation", "Procurement", "Tender evaluations and scoring", "CheckCircle", "#F59E0B", 49),
            new("Asset", "Inventory", "Assets and equipment", "Database", "#10B981", 50),
            new("Inventory", "Inventory", "Inventory records", "Database", "#14B8A6", 60),
            new("InventoryTransfer", "Inventory", "Inventory transfers", "Truck", "#A855F7", 65),
            new("InventoryRequisition", "Inventory", "Inventory requisitions", "ClipboardList", "#0EA5E9", 68),
            new("Employee", "Human Resources", "Human resources employees", "Users", "#6366F1", 70),
            new("PayrollRun", "Human Resources", "HR payroll runs, payslip generation, and posting", "WalletCards", "#0EA5E9", 72),
            new("PayrollPayslipEmail", "Human Resources", "HR payroll payslip email notifications", "Mail", "#2563EB", 73),
            new("PayrollSalaryAdvance", "Human Resources", "HR payroll salary advance requests", "ReceiptText", "#14B8A6", 74),
            new("PayrollBonusSetup", "Human Resources", "HR payroll bonus setup and exception approval", "BadgePercent", "#F97316", 76),
            new("PayrollBackpaySetup", "Human Resources", "HR payroll salary back pay and salary increase setup", "TrendingUp", "#22C55E", 78),
            new("Project", "Projects", "Project management items", "CheckCircle", "#22C55E", 80),
            new("ProjectDeliverable", "Projects", "Project deliverable approvals and external sign-off", "PackageCheck", "#16A34A", 82),
            new("ProjectClosure", "Projects", "Project closure approval and close-out governance", "Flag", "#15803D", 84),
            new("Customer", "Sales", "Sales customers", "User", "#0EA5E9", 90),
            new("SalesOrder", "Sales", "Sales orders and customer sales transactions", "ShoppingCart", "#2563EB", 91),
            new("SalesAgreement", "Sales", "Sales, lease, tenancy, and plot allocation agreements", "FileText", "#7C3AED", 92),
            new("SalesAllocation", "Sales", "Sales reservations, plot allocations, and saleable source holds", "MapPinned", "#0891B2", 93),
            new("LandAcquisition", "Estate", "Estate land acquisition procedure and statutory registration approvals", "Landmark", "#0F766E", 94),
            new("EstateFacilityPropertySite", "Estate", "Estate facilities property and site management workflows", "Building2", "#0F766E", 95),
            new("EstateFacilityLease", "Estate", "Estate facilities lease management workflows", "FileSignature", "#0284C7", 96),
            new("EstateFacilityMaintenance", "Estate", "Estate facilities maintenance request, inspection, and closure workflows", "Wrench", "#B45309", 97),
            new("EstateFacilityComplaint", "Estate", "Estate facilities complaint, escalation, and resolution workflows", "MessageSquare", "#E11D48", 98),
            new("EstateFacilityServiceProvider", "Estate", "Estate facilities service provider contract, assignment, and performance workflows", "Briefcase", "#7C3AED", 99),
            new("EstateFacilityStaffCleaner", "Estate", "Estate facilities staff and cleaner assignment and supervision workflows", "ClipboardCheck", "#059669", 100),
            new("EstateFacilityAssetRegister", "Estate", "Estate facilities asset register and status workflows", "Database", "#4F46E5", 101),
            new("EstateFacilityDocument", "Estate", "Estate facilities document control workflows", "FileText", "#65A30D", 102),
            new("EstateRegistrySecretariat", "Estate", "Estate registry, secretarial intake, file movement, and client update workflows", "ClipboardList", "#334155", 103),
            new("EstateRecordsManagement", "Estate", "Estate records, registers, ledgers, amendments, and verification workflows", "Database", "#4F46E5", 104),
            new("EstateInspection", "Estate", "Land and landed-property inspection and site report workflows", "MapPin", "#0F766E", 105),
            new("EstateSearchApplication", "Estate", "Estate search application and search report workflows", "Search", "#0284C7", 106),
            new("EstateRecordAmendment", "Estate", "Estate change of address and record amendment workflows", "FilePenLine", "#0891B2", 107),
            new("EstateCertifiedTrueCopy", "Estate", "Certified true copy request and certification workflows", "FileCheck2", "#059669", 108),
            new("EstateJointOwnership", "Estate", "Joint ownership and addition-of-name workflows", "Users", "#7C3AED", 109),
            new("EstateTransfer", "Estate", "Transfer and portion-transfer of plot workflows", "ArrowRightLeft", "#2563EB", 110),
            new("EstateAssignment", "Estate", "Assignment consent, registration, and completion workflows", "FileSignature", "#6D28D9", 111),
            new("EstateLeasePreparation", "Estate", "Lease preparation, cadastral, invoice, and legal routing workflows", "FileText", "#B45309", 112),
            new("EstateLeaseRenewal", "Estate", "Lease surrender and renewal workflows", "RefreshCw", "#65A30D", 113),
            new("EstateServicedPlotAllocation", "Estate", "Serviced plot and HOS allocation workflows", "Landmark", "#16A34A", 114),
            new("EstateLandsPartiallyServiced", "Estate", "Lands and partially serviced schedule workflows", "Home", "#EA580C", 115),
            new("EstateHousingHomeOwnership", "Estate", "Housing, tenancy recognition, and home ownership scheme workflows", "Building2", "#E11D48", 116),
            new("EstateTraditionalLands", "Estate", "Traditional lands proposal and allocation workflows", "Trees", "#059669", 117),
            new("EstateTenancyRegularisation", "Estate", "Tenancy regularisation workflows", "BadgeCheck", "#CA8A04", 118),
            new("EstateReportingControls", "Estate", "Estate reporting, approvals, audit trail, and control workflows", "BarChart3", "#C026D3", 119),
            new("Refund", "Sales", "Customer refund requests and approvals", "RotateCcw", "#F97316", 95),
            new("CreditNote", "Sales", "Customer credit notes and adjustments", "ReceiptText", "#14B8A6", 96),
            new("BusinessPartner", "Procurement", "Business partner onboarding/approvals (suppliers/contractors/customers)", "Building", "#64748B", 100),
            new("Vendor", "Procurement", "Business partners and vendors", "Building", "#64748B", 105),
            new("Quality", "Quality", "Quality inspections", "CheckCircle", "#EF4444", 110),
            new("ServiceRequest", "Helpdesk", "Service catalog requests", "ClipboardList", "#10B981", 115),
            new("LegalProcedure", "Legal", "Legal department procedure manual workflows", "Gavel", "#334155", 120),
            new("LegalMortgage", "Legal", "Mortgage review, preparation, and approval workflows", "FileSignature", "#475569", 121),
            new("LegalMortgageInPrinciple", "Legal", "Mortgage in principle review and approval workflows", "FileCheck", "#64748B", 122),
            new("LegalCourtProcess", "Legal", "Court process review, filing, hearing, and follow-up workflows", "Scale", "#7C3AED", 123),
            new("LegalOtherCourtProcess", "Legal", "Other court process review and action workflows", "Scale", "#6D28D9", 124),
            new("LegalTerminationRecognition", "Legal", "Termination and recognition procedure workflows", "FileX", "#B45309", 125),
            new("LegalAssignmentSubleaseVesting", "Legal", "Assignment, sublease, and vesting procedure workflows", "FileText", "#0F766E", 126),
            new("LegalLeaseVariationRenewalSublease", "Legal", "Lease, deed of variation, renewal, and sublease procedure workflows", "FileText", "#0891B2", 127),
            new("LegalTransfer", "Legal", "Transfer procedure review and approval workflows", "ArrowRightLeft", "#2563EB", 128)
        };

        return defaults
            .Select((item, index) => item with { DisplayOrder = item.DisplayOrder == 0 ? (index + 1) * 10 : item.DisplayOrder })
            .ToList();
    }
}
