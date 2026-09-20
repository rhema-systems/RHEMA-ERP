using ErpSystem.Core.Interfaces.Estate;

namespace ErpSystem.Core.Services.Estate;

public sealed class PropertyManagementProcedureCatalogService : IPropertyManagementProcedureCatalogService
{
    private static readonly FacilitiesWorkspaceStage[] ListingApplicationManualStages =
    [
        new("Estate intake review", "Estate Manager / Property Manager",
            "Estate verifies the customer, listing, and completed Sales transaction before taking the application forward.",
            [
                "Customer and property listing references are verified",
                "Completed Sales opportunity and agreed transaction are recorded"
            ]),
        new("Commercial and availability review", "Estate Manager / Property Manager",
            "Estate confirms the listing remains available and records the commercial and reservation position.",
            [
                "Availability and reservation position are confirmed",
                "Commercial review outcome is recorded"
            ]),
        new("Estate decision and agreement", "Estate Manager / Property Manager",
            "Estate records the management decision and generates the agreement for Legal review.",
            [
                "Management decision is recorded",
                "Agreement is generated for the approved request"
            ]),
        new("Legal agreement review", "Legal Officer / Head of Legal",
            "Legal reviews the generated sale, lease, or tenancy agreement before customer execution.",
            [
                "Generated agreement is lodged with Legal",
                "Legal approval is recorded on the Estate case"
            ]),
        new("Customer agreement execution", "Estate Manager / Property Manager",
            "The approved agreement is sent to the customer, signed by the customer, approved internally, and digitally signed.",
            [
                "Customer signed agreement is received",
                "Internal approval and digital signature are complete"
            ]),
        new("Payment, billing and Finance check", "Estate / Finance",
            "Estate and Finance confirm the Sales amount paid, any remaining Estate balance, rent billing readiness, and invoice/payment outcome.",
            [
                "Sales payment and Estate balance are checked",
                "Finance invoice, payment, or rent billing readiness is recorded"
            ]),
        new("Legal conveyance or lease follow-up", "Legal / Estate",
            "Sale cases complete Legal conveyance and registration; rental cases confirm lease execution and move-in readiness.",
            [
                "Sale conveyance and registration is completed where applicable",
                "Rental move-in and billing readiness is confirmed where applicable"
            ]),
        new("Estate completion", "Estate Manager / Property Manager",
            "Estate closes the application only after agreement, payment, Legal, records, and customer outcome controls are complete.",
            [
                "Customer outcome is recorded",
                "Estate application closeout is complete"
            ])
    ];

    private static readonly FacilitiesWorkspaceField[] ListingApplicationFields =
    [
        new("applicationReference", "Property request reference", "text"),
        new("sourceWorkspace", "Source workspace", "text"),
        new("sourceReference", "Customer Business Partner reference", "text"),
        new("customerAccountReference", "Customer account reference", "text"),
        new("customerName", "Customer / company name", "text"),
        new("propertyUnit", "Property / unit", "text"),
        new("listingId", "Listing ID", "text"),
        new("listingRecordType", "Listing record type", "text"),
        new("listingReference", "Listing reference", "text"),
        new("listingType", "Published listing type", "text"),
        new("requestType", "Request type", "text"),
        new("listingPrice", "Published price / rent", "text"),
        new("offerAmount", "Purchase offer amount", "text"),
        new("currency", "Currency", "text"),
        new("salesAmountPaid", "Amount paid in Sales", "text"),
        new("salesPaymentReference", "Sales payment reference", "text"),
        new("estateRemainingAmount", "Balance for Estate processing", "text"),
        new("premiumChargeRequired", "Premium charge required", "select", ["No", "Yes"]),
        new("premiumChargeAmount", "Premium charge amount", "text"),
        new("premiumChargeInvoiceId", "Premium charge invoice ID", "text"),
        new("premiumChargeInvoiceReference", "Premium charge invoice reference", "text"),
        new("premiumChargeInvoiceStatus", "Premium charge invoice status", "text"),
        new("premiumChargePaidAmount", "Premium charge paid amount", "text"),
        new("premiumChargeBalance", "Premium charge balance", "text"),
        new("premiumChargePaymentStatus", "Premium charge payment status", "select", ["Not required", "Pending invoice", "Invoiced", "Payment pending", "Paid", "Waived"]),
        new("requestedLeaseTerm", "Requested lease term", "text"),
        new("requestMessage", "Customer message", "textarea"),
        new("customerValidationStatus", "Customer validation status", "select", ["Pending", "Validated", "Failed"]),
        new("listingValidationStatus", "Listing validation status", "select", ["Pending", "Validated", "Failed"]),
        new("availabilityCheck", "Availability check", "select", ["Pending", "Available", "Unavailable"]),
        new("commercialReviewStatus", "Commercial review status", "select", ["Pending", "Reviewed", "Exception required"]),
        new("decisionStatus", "Management decision", "select", ["Pending review", "Approved", "Rejected", "More information required"]),
        new("reservationStatus", "Reservation status", "select", ["Not reserved", "Reserved", "Released"]),
        new("customerNotificationStatus", "Customer notification status", "text"),
        new("customerAcceptanceStatus", "Customer acceptance status", "text"),
        new("customerAcceptanceDate", "Customer acceptance date", "date"),
        new("agreementTemplateReference", "Agreement template reference", "text"),
        new("generatedAgreementReference", "Generated agreement reference", "text"),
        new("signedAgreementReference", "Signed agreement reference", "text"),
        new("agreementExecutionStatus", "Agreement execution status", "text"),
        new("internalApprovalStatus", "Internal agreement approval status", "text"),
        new("internalSignatureStatus", "Internal digital signature status", "text"),
        new("finalSignedAgreementReference", "Final signed agreement reference", "text"),
        new("finalSignedAgreementVersion", "Final signed agreement version", "text"),
        new("legalAgreementReviewCaseId", "Legal agreement review case ID", "text"),
        new("legalAgreementReviewReference", "Legal agreement review reference", "text"),
        new("legalAgreementReviewStatus", "Legal agreement review status", "text"),
        new("legalConveyanceCaseId", "Legal conveyance case ID", "text"),
        new("legalConveyanceReference", "Legal conveyance reference", "text"),
        new("legalConveyanceStatus", "Legal conveyance / registration status", "text"),
        new("legalLastMatterCaseId", "Latest linked Legal matter ID", "text"),
        new("legalLastMatterReference", "Latest linked Legal matter reference", "text"),
        new("legalLastMatterType", "Latest linked Legal matter type", "text"),
        new("legalLastMatterStatus", "Latest linked Legal matter status", "text"),
        new("salePaymentStatus", "Sale payment status", "text"),
        new("saleInvoiceId", "Sale invoice ID", "text"),
        new("saleInvoiceReference", "Sale invoice reference", "text"),
        new("saleInvoiceStatus", "Sale invoice status", "text"),
        new("saleInvoiceAmount", "Sale invoice amount", "text"),
        new("saleInvoicePaidAmount", "Sale invoice paid amount", "text"),
        new("saleInvoiceBalance", "Sale invoice balance", "text"),
        new("salePaymentCheckStatus", "Sale payment check", "text"),
        new("ownershipTransferStatus", "Ownership transfer status", "text"),
        new("moveInDate", "Approved move-in date", "date"),
        new("moveInEffectiveStatus", "Move-in effective status", "text"),
        new("billingStartDate", "Billing start date", "date"),
        new("billingStartStatus", "Billing start status", "text"),
        new("receivedDate", "Received date", "date"),
        new("applicationStatus", "Request status", "select", ["Submitted", "Under review", "Approved", "Rejected", "Closed"]),
        new("notes", "Property request notes", "textarea")
    ];

    private static readonly FacilitiesProcedureCatalogItem[] Procedures =
    [
        new("Property and Unit Register", "EstatePropertyManagementPropertyUnit", "Source: Estate / Property Management - Property Management ERP Module", "Property, site, unit, space, common-area, and service-area receiving/register operations routed through configured workflows.", "Building2", 0, "teal", "Register"),
        new("Lease Management", "EstatePropertyManagementLease", "Source: Estate / Property Management - Lease Management", "Lease setup, variation, renewal, termination, agreement, signature, and billing handoff operations routed through configured workflows.", "FileCheck", 0, "amber", "Case Workflow"),
        new("Tenant / Occupant Operations", "EstatePropertyManagementTenantOccupant", "Source: Estate / Property Management - Property Management ERP BRS", "Tenant and occupant operations using existing CRM, Finance AR customer, business partner, or tenant administration sources routed through configured workflows.", "Users", 0, "cyan", "Register"),
        new("Billing / Service Charge Operations", "EstatePropertyManagementBillingServiceCharge", "Source: Estate / Property Management -> Finance AR", "Billing readiness and service-charge instruction operations routed through configured workflows and existing Finance AR.", "CreditCard", 0, "purple", "Operational Queue"),
        new("Ground Rent Administration", "EstatePropertyManagementGroundRent", "Source: Estate / Property Management - Ground Rent", "Land-only ground-rent account setup, assessment, review, billing readiness, and finance handoff routed through configured workflows.", "Banknote", 0, "emerald", "Operational Queue"),
        new("Listing / Application Operations", "EstatePropertyManagementListingApplication", "Source: External Portal -> Estate / Property Management", "External portal rent/sale listing request intake, review, reservation, and decision operations routed through a published workflow or the Estate manual-review stages.", "ClipboardList", 4, "blue", "Case Workflow"),
        new("Occupancy / Availability Operations", "EstatePropertyManagementOccupancyAvailability", "Source: Estate / Property Management - Occupancy Operations", "Availability, reservation, occupancy, move-in, move-out, sale, block, and portal visibility operations routed through configured workflows.", "Home", 0, "rose", "Operational Queue"),
        new("Move-in / Move-out / Handover Operations", "EstatePropertyManagementMoveInMoveOutHandover", "Source: Estate / Property Management - Handover", "Move-in, move-out, key/access, condition, snag, handover, handback, and linked billing/records operations routed through configured workflows.", "ClipboardCheck", 0, "orange", "Operational Queue"),
        new("Property Documents / Records Index", "EstatePropertyManagementDocumentRecordIndex", "Source: Estate / Property Management -> Central DMS", "Property Management document index, module metadata, access, retention, lifecycle, DMS reference, version, annotation, and comment readiness routed through configured workflows.", "FileText", 0, "lime", "Register")
    ];

    public IReadOnlyList<FacilitiesProcedureCatalogItem> GetProcedures() => Procedures;

    public FacilitiesProcedureWorkspace? GetProcedureWorkspace(string entityType)
    {
        var procedure = Procedures.FirstOrDefault(item =>
            string.Equals(item.EntityType, entityType, StringComparison.OrdinalIgnoreCase));

        if (procedure is null)
        {
            return null;
        }

        var isListingApplication = string.Equals(
            entityType,
            "EstatePropertyManagementListingApplication",
            StringComparison.OrdinalIgnoreCase);
        IReadOnlyList<FacilitiesWorkspaceField> fields = isListingApplication
            ? ListingApplicationFields
            : [];
        IReadOnlyList<FacilitiesWorkspaceStage> stages = isListingApplication
            ? ListingApplicationManualStages
            : [];

        return new FacilitiesProcedureWorkspace(procedure, stages, [], fields, [], []);
    }
}
