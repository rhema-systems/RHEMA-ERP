using ErpSystem.Core.Interfaces.Estate;

namespace ErpSystem.Core.Services.Estate;

public sealed class PropertyManagementProcedureCatalogService : IPropertyManagementProcedureCatalogService
{
    private static readonly FacilitiesWorkspaceStage[] ListingApplicationManualStages =
    [
        new("Estate intake review", "Estate Manager / Property Manager",
            [
                "Customer and property listing references are verified",
                "Completed Sales opportunity and agreed transaction are recorded"
            ]),
        new("Commercial and availability review", "Estate Manager / Property Manager",
            [
                "Availability and reservation position are confirmed",
                "Commercial review outcome is recorded"
            ]),
        new("Estate decision and agreement", "Estate Manager / Property Manager",
            [
                "Management decision is recorded",
                "Agreement is generated for the approved request"
            ]),
        new("Legal agreement review", "Legal Officer / Head of Legal",
            [
                "Generated agreement is lodged with Legal",
                "Legal approval is recorded on the Estate case"
            ]),
        new("Customer agreement execution", "Estate Manager / Property Manager",
            [
                "Customer signed agreement is received",
                "Internal approval and digital signature are complete"
            ]),
        new("Payment, billing and Finance check", "Estate / Finance",
            [
                "Sales payment and Estate balance are checked",
                "Finance invoice, payment, or rent billing readiness is recorded"
            ]),
        new("Legal conveyance or lease follow-up", "Legal / Estate",
            [
                "Sale conveyance and registration is completed where applicable",
                "Rental move-in and billing readiness is confirmed where applicable"
            ]),
        new("Estate completion", "Estate Manager / Property Manager",
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
        new("listingPrice", "Agreed sale / full lease amount / monthly rent", "text"),
        new("offerAmount", "Purchase offer amount", "text"),
        new("currency", "Currency", "text"),
        new("salesAmountPaid", "Amount paid in Sales", "text"),
        new("salesPaymentReference", "Sales payment reference", "text"),
        new("estateRemainingAmount", "Balance for Estate processing", "text"),
        new("groundRentRequired", "Annual ground rent required", "select", ["No", "Yes"]),
        new("premiumChargeRequired", "Premium charge required", "select", ["No", "Yes"]),
        new("premiumChargeAmount", "Premium charge amount", "text"),
        new("premiumChargeInvoiceId", "Premium charge invoice ID", "text"),
        new("premiumChargeInvoiceReference", "Premium charge invoice reference", "text"),
        new("premiumChargeInvoiceStatus", "Premium charge invoice status", "text"),
        new("premiumChargePaidAmount", "Premium charge paid amount", "text"),
        new("premiumChargeBalance", "Premium charge balance", "text"),
        new("premiumChargePaymentStatus", "Premium charge payment status", "select", ["Not required", "Pending invoice", "Invoiced", "Payment pending", "Paid"]),
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
        new("agreementSigningLocation", "Agreement signing location", "select", ["Legal", "Estate"]),
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
        new("salePaymentStatus", "Sale or lease payment status", "text"),
        new("saleInvoiceId", "Sale invoice ID", "text"),
        new("saleInvoiceReference", "Balance invoice reference", "text"),
        new("saleInvoiceStatus", "Balance invoice status", "text"),
        new("saleInvoiceAmount", "Balance invoice amount", "text"),
        new("saleInvoicePaidAmount", "Balance invoice paid amount", "text"),
        new("saleInvoiceBalance", "Balance invoice remaining", "text"),
        new("salePaymentCheckStatus", "Sale or lease payment check", "text"),
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
        new("Property and Unit Register", "EstatePropertyManagementPropertyUnit", "Building2", 0, "teal", "Register"),
        new("Lease Management", "EstatePropertyManagementLease", "FileCheck", 0, "amber", "Case Workflow"),
        new("Tenant / Occupant Operations", "EstatePropertyManagementTenantOccupant", "Users", 0, "cyan", "Register"),
        new("Billing / Service Charge Operations", "EstatePropertyManagementBillingServiceCharge", "CreditCard", 0, "purple", "Operational Queue"),
        new("Ground Rent Administration", "EstatePropertyManagementGroundRent", "Banknote", 0, "emerald", "Operational Queue"),
        new("Listing / Application Operations", "EstatePropertyManagementListingApplication", "ClipboardList", 4, "blue", "Case Workflow"),
        new("Occupancy / Availability Operations", "EstatePropertyManagementOccupancyAvailability", "Home", 0, "rose", "Operational Queue"),
        new("Move-in / Move-out / Handover Operations", "EstatePropertyManagementMoveInMoveOutHandover", "ClipboardCheck", 0, "orange", "Operational Queue"),
        new("Property Documents / Records Index", "EstatePropertyManagementDocumentRecordIndex", "FileText", 0, "lime", "Register")
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
