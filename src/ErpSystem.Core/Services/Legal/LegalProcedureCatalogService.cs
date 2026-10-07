using ErpSystem.Core.Interfaces.Legal;

namespace ErpSystem.Core.Services.Legal;

public sealed class LegalProcedureCatalogService : ILegalProcedureCatalogService
{
    private static readonly LegalProcedureCatalogItem[] Procedures =
    [
        new("General Legal Matters", "LegalProcedure", "BookOpen", 6, "slate"),
        new("Property Agreement Reviews", "LegalPropertyAgreementReview", "FileCheck2", 4, "emerald"),
        new("Legal Opinions / Advisory", "LegalOpinionAdvisory", "MessageSquare", 5, "indigo"),
        new("External Counsel Management", "LegalExternalCounsel", "Briefcase", 5, "zinc"),
        new("Mortgages", "LegalMortgage", "FileSignature", 6, "cyan"),
        new("Mortgage In Principle", "LegalMortgageInPrinciple", "FileCheck2", 5, "emerald"),
        new("Court Processes", "LegalCourtProcess", "Scale", 7, "violet"),
        new("Other Court Processes", "LegalOtherCourtProcess", "Gavel", 5, "purple"),
        new("Termination / Recognition", "LegalTerminationRecognition", "ShieldCheck", 8, "amber"),
        new("Assignment / Sublease / Vesting", "LegalAssignmentSubleaseVesting", "Landmark", 6, "teal"),
        new("Leases / Deed of Variation / Renewal / Sublease", "LegalLeaseVariationRenewalSublease", "FileText", 7, "sky"),
        new("Transfers", "LegalTransfer", "BadgeCheck", 8, "blue")
    ];

    public IReadOnlyList<LegalProcedureCatalogItem> GetProcedures() => Procedures;

    public LegalProcedureWorkspace? GetProcedureWorkspace(string entityType)
    {
        var procedure = Procedures.FirstOrDefault(item =>
            string.Equals(item.EntityType, entityType, StringComparison.OrdinalIgnoreCase));

        if (procedure is null)
        {
            return null;
        }

        var stages = BuildStages(procedure.EntityType);

        return new LegalProcedureWorkspace(
            procedure with { StageCount = stages.Count },
            stages,
            BuildDocuments(procedure.EntityType),
            BuildFields(procedure.EntityType),
            BuildOutputs(procedure.EntityType),
            BuildHandoffs(procedure.EntityType));
    }

    private static IReadOnlyList<LegalWorkspaceStage> BuildStages(string entityType)
    {
        if (string.Equals(entityType, "LegalPropertyAgreementReview", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                Stage("Legal Intake", "Legal Admin Assistant", "The linked property transaction and draft agreement are registered in Legal.", ["Confirm Estate source reference", "Confirm draft agreement is attached", "Assign Legal Officer"]),
                Stage("Agreement Vetting", "Legal Officer", "The Legal Officer checks parties, property particulars, commercial terms, obligations, execution blocks, and legal risk before the customer signs.", ["Verify parties and property", "Review clauses and schedules", "Release vetted agreement to customer or return for correction"]),
                Stage("Customer Signature Return", "Legal Admin Assistant", "The vetted agreement is available in the customer portal and Legal waits for the customer-signed upload.", ["Confirm agreement is released to the customer portal", "Confirm customer signed agreement upload", "Forward returned agreement to Head of Legal"]),
                Stage("Head of Legal Signature", "Head of Legal", "After the customer returns the signed agreement, the Head of Legal applies the final Legal signature.", ["Confirm customer signed agreement", "Apply Head of Legal signature", "Return final signed agreement to Property Management"])
            ];
        }

        if (string.Equals(entityType, "LegalCourtProcess", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                Stage("Registry Receipt", "Registry", "Court process is received and service details are captured.", ["Record service date", "Attach served process", "Create legal registry entry"]),
                Stage("Legal Admin Recording", "Legal Admin Assistant", "Process is received by Legal Admin and recorded for routing.", ["Open matter record", "Assign legal reference", "Forward to Secretary"]),
                Stage("Secretary Routing", "Secretary", "Secretary forwards the process for Head of Legal assignment.", ["Confirm docket type", "Prepare routing note", "Send to Head of Legal"]),
                Stage("Head of Legal Assignment", "Head of Legal", "Head of Legal assigns the matter to a Legal Officer.", ["Select Legal Officer", "Set response deadline", "Record assignment note"]),
                Stage("Court Jacket Opening", "Legal Officer", "Legal Officer opens or requests a court jacket for the matter.", ["Open court jacket", "Attach source evidence", "Return jacket to Legal Officer"]),
                Stage("Process Preparation", "Legal Officer", "Legal Officer prepares response or court process for filing.", ["Draft response", "Review legal position", "Send to Legal Admin for filing"]),
                Stage("Court Filing", "Legal Admin Assistant", "Legal Admin files process at court and records filing evidence.", ["File at court", "Upload filing receipt", "Update next hearing / follow-up"])
            ];
        }

        if (string.Equals(entityType, "LegalOtherCourtProcess", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                Stage("Registry Receipt", "Registry", "Other court process is received and registered.", ["Record service date", "Attach document", "Create registry entry"]),
                Stage("Legal Admin Recording", "Legal Admin Assistant", "Legal Admin records the process and forwards it.", ["Assign legal reference", "Update process register", "Forward to Secretary"]),
                Stage("Secretary Assignment", "Secretary", "Secretary sends the process to the assigned Legal Officer.", ["Confirm assigned officer", "Set action deadline", "Forward process"]),
                Stage("Legal Action Preparation", "Legal Officer", "Legal Officer prepares any required legal response or action.", ["Review process", "Prepare action", "Send to Legal Admin if filing is needed"]),
                Stage("Court Filing / Closeout", "Legal Admin Assistant", "Legal Admin files any process at court and closes or follows up.", ["File at court", "Upload evidence", "Record closeout or next action"])
            ];
        }

        if (string.Equals(entityType, "LegalTransfer", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                Stage("Head of Legal Minuting", "Head of Legal", "File is minuted for transfer-fee payment and processing.", ["Receive Estate file", "Minute payment requirement", "Assign Legal Admin"]),
                Stage("Client Payment Call", "Legal Admin Assistant", "Client is called to pay transfer fee within the approved period.", ["Notify client", "Track 30-day payment window", "Capture payment evidence"]),
                Stage("Transfer Drafting", "Legal Admin Assistant", "After payment, the draft transfer form is prepared.", ["Confirm payment", "Prepare transfer draft", "Send to Legal Officer"]),
                Stage("Legal Vetting", "Legal Officer", "Legal Officer vets and approves the transfer draft.", ["Review parties", "Review property details", "Approve or return draft"]),
                Stage("Client Execution", "Legal Admin Assistant", "Legal Admin captures the client-signed transfer form and routes the execution pack.", ["Invite parties", "Capture signature date", "Attach executed copy"]),
                Stage("Legal Officer Signature", "Legal Officer", "Legal Officer signs the transfer instrument.", ["Confirm execution", "Sign instrument", "Forward for LAA signature"]),
                Stage("Legal Admin Signature", "Legal Admin Assistant", "Legal Admin signs or attests as required.", ["Apply LAA signature", "Check execution pack", "Send to Head of Legal"]),
                Stage("Head of Legal Signature", "Head of Legal", "Head of Legal signs the transfer instrument.", ["Sign transfer", "Release for Legal Admin closeout"]),
                Stage("Legal Admin Closeout", "Legal Admin Assistant", "Legal Admin distributes signed forms and returns the file for Estate records amendment.", ["Distribute signed forms", "Return file to Estate Records", "Upload Estate return note"])
            ];
        }

        if (string.Equals(entityType, "LegalTerminationRecognition", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                Stage("Termination Draft Instruction", "Head of Legal", "Head of Legal refers file for drafting of termination letter.", ["Receive file", "Confirm termination basis", "Minute Legal Admin"]),
                Stage("Termination Draft", "Legal Admin Assistant", "Draft termination letter is prepared for approval.", ["Prepare draft", "Attach site report", "Send to Head of Legal"]),
                Stage("Termination Approval", "Head of Legal", "Termination letter is approved and signed.", ["Approve letter", "Sign letter", "Release for posting"]),
                Stage("Notice Posting", "Legal Clerk", "Termination notice is pasted for 21 days.", ["Record posting date", "Track 21-day expiry", "Upload posting evidence"]),
                Stage("Payment Approval", "Head of Legal", "File returns for payment approval before recognition.", ["Review posting outcome", "Approve payment request", "Notify client"]),
                Stage("Recognition Vetting", "Legal Officer", "Draft recognition is prepared and vetted.", ["Confirm payment", "Vet recognition draft", "Approve recognition"]),
                Stage("Client and Legal Execution", "Client Signature", "Client signs, then Legal Officer and Legal Admin sign.", ["Capture client signature", "Capture Legal Officer signature", "Capture Legal Admin signature"]),
                Stage("Head of Legal Closeout", "Head of Legal", "File is sent to Head of Legal for signature and Estate update.", ["Sign recognition", "Update legal record", "Return to Estate Records"])
            ];
        }

        if (string.Equals(entityType, "LegalMortgage", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                Stage("Head of Legal Minuting", "Head of Legal", "File is minuted for payment and mortgage processing.", ["Receive Estate file", "Confirm mortgage request", "Minute Legal Admin"]),
                Stage("Client Payment Call", "Legal Admin Assistant", "Client is called to make payment within 30 days.", ["Notify client", "Track 30-day window", "Capture receipt"]),
                Stage("Draft Mortgage Letter", "Legal Admin Assistant", "After payment, draft mortgage letter is prepared and sent for vetting.", ["Confirm payment", "Prepare draft", "Send to Legal Officer"]),
                Stage("Legal Vetting", "Legal Officer", "Legal Officer approves or returns the draft.", ["Vet draft", "Confirm property file", "Approve draft"]),
                Stage("Head of Legal Signature", "Head of Legal", "Letter is finalized and sent to Head of Legal for signature.", ["Finalize letter", "HOL signature", "Prepare MD signature pack"]),
                Stage("Managing Director Signature", "Managing Director", "Head of Legal sends the letter for Managing Director signature.", ["MD signature", "Seal / date letter", "Release to client and return file"])
            ];
        }

        if (string.Equals(entityType, "LegalMortgageInPrinciple", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                Stage("Head of Legal Minuting", "Head of Legal", "File is minuted for payment and in-principle review.", ["Receive request", "Minute payment", "Assign Legal Admin"]),
                Stage("Client Payment Call", "Legal Admin Assistant", "Client is called for payment within 30 days.", ["Notify client", "Track payment deadline", "Capture receipt"]),
                Stage("Draft In-Principle Letter", "Legal Admin Assistant", "After payment, draft mortgage-in-principle letter is prepared.", ["Confirm payment", "Prepare draft", "Send for vetting"]),
                Stage("Legal Vetting", "Legal Officer", "Legal Officer approves the draft.", ["Vet draft", "Confirm conditions", "Approve or return"]),
                Stage("Head of Legal Signature", "Head of Legal", "Letter is finalized and sent to Head of Legal for signature.", ["Finalize letter", "Sign letter", "Release to client / Estate"])
            ];
        }

        if (string.Equals(entityType, "LegalAssignmentSubleaseVesting", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                Stage("Head of Legal Minuting", "Head of Legal", "File is minuted to Legal Admin Assistant for drafting.", ["Receive Estate file", "Confirm instrument type", "Assign Legal Admin"]),
                Stage("Draft Letter / Instrument", "Legal Admin Assistant", "Legal Admin drafts the letter or instrument and sends it to Legal Officer.", ["Prepare draft", "Attach property file references", "Send to Legal Officer"]),
                Stage("Legal Officer Approval", "Legal Officer", "Legal Officer reviews and approves the draft.", ["Vet parties", "Confirm consent / vesting details", "Approve or return"]),
                Stage("Head of Legal Signature Pack", "Legal Admin Assistant", "Approved file is sent to Head of Legal for signature routing.", ["Print / finalize", "Prepare signature pack", "Send to Head of Legal"]),
                Stage("Executive Signature Routing", "Head of Legal", "Head of Legal signs or sends file to Managing Director where required.", ["HOL signature", "MD signature if required", "Seal / date instrument"]),
                Stage("File Return and Closeout", "Legal Admin Assistant", "Completed instrument is dispatched and file is returned.", ["Release document", "Update legal register", "Return file to Estate"])
            ];
        }

        if (string.Equals(entityType, "LegalLeaseVariationRenewalSublease", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                Stage("Head of Legal Minuting", "Head of Legal", "File is minuted to Secretary for lease or variation drafting.", ["Receive Estate file", "Confirm request type", "Minute Secretary"]),
                Stage("Secretary Drafting", "Secretary", "Secretary drafts lease, deed of variation, renewal, or sublease.", ["Prepare draft", "Attach schedule details", "Send to Legal Officer"]),
                Stage("Legal Officer Approval", "Legal Officer", "Legal Officer approves or returns the draft.", ["Review draft", "Confirm schedule insertion", "Approve draft"]),
                Stage("Legal Admin Assignment", "Secretary", "Secretary assigns approved file to Legal Admin Assistant.", ["Prepare execution pack", "Assign Legal Admin", "Call client"]),
                Stage("Client Execution", "Client Signature", "Client and witnesses are called to sign the lease or instrument.", ["Invite client", "Capture client signature", "Capture witness signature"]),
                Stage("Head of Legal Signature", "Head of Legal", "Lease is sealed and sent to Head of Legal for signature.", ["Seal instrument", "HOL signature", "Prepare MD routing"]),
                Stage("Managing Director Signature", "Managing Director", "Lease is sent to Managing Director for signature and completion.", ["MD signature", "Record pickup / registration handoff", "Return file to Estate"])
            ];
        }

        if (string.Equals(entityType, "LegalOpinionAdvisory", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                Stage("Advisory Intake", "Head of Legal", "Legal opinion request is received, logged, and assigned.", ["Capture request", "Attach source documents", "Assign Legal Officer"]),
                Stage("Issue Review", "Legal Officer", "Legal Officer reviews facts, laws, and risk position.", ["Confirm issue statement", "Review documents", "Record legal risk"]),
                Stage("Advice Drafting", "Legal Officer", "Draft legal opinion or advice memo is prepared.", ["Prepare draft", "Add recommendations", "Mark confidentiality"]),
                Stage("Approval", "Head of Legal", "Head of Legal reviews and approves the advice.", ["Review draft", "Approve or return", "Set dispatch recipient"]),
                Stage("Dispatch and Closure", "Legal Admin Assistant", "Approved advice is dispatched and closed in the legal register.", ["Dispatch advice", "Upload final memo", "Close matter"])
            ];
        }

        if (string.Equals(entityType, "LegalExternalCounsel", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                Stage("Counsel Instruction", "Head of Legal", "External counsel instruction or retainer request is captured.", ["Confirm scope", "Select counsel", "Approve instruction"]),
                Stage("Matter Assignment", "Legal Officer", "Matter is assigned and deliverables are agreed.", ["Record deliverables", "Set due dates", "Share source documents"]),
                Stage("Deliverable Review", "Legal Officer", "External counsel deliverables are reviewed.", ["Receive deliverable", "Review advice / filing", "Record action points"]),
                Stage("Fee and Invoice Control", "Legal Admin Assistant", "Fees, invoices, and AP references are tracked.", ["Capture fee note", "Link invoice / AP reference", "Confirm approval"]),
                Stage("Performance Closeout", "Head of Legal", "Matter performance and closeout notes are recorded.", ["Rate performance", "Close matter", "Archive counsel records"])
            ];
        }

        return
        [
            Stage("Legal Intake", "Head of Legal", "Legal file, request, or property record is received and minuted.", ["Capture source department", "Attach source file", "Assign responsible role"]),
            Stage("Due Diligence", "Legal Officer", "Legal Officer checks property file, payments, cadastral plan, schedule, and legal risk.", ["Confirm payment status", "Confirm cadastral plan", "Record due diligence outcome"]),
            Stage("Draft Preparation", "Secretary / Legal Admin Assistant", "Draft letter, instrument, consent, or response is prepared.", ["Prepare draft", "Use approved template", "Send for vetting"]),
            Stage("Legal Review and Approval", "Legal Officer / Head of Legal", "Draft is vetted, approved, or returned for correction.", ["Vet draft", "Record approval decision", "Prepare signature pack"]),
            Stage("Execution and Signature", "Client / Head of Legal / Managing Director", "Document is signed, sealed, dated, or routed for executive signature.", ["Capture signatures", "Seal / date document", "Record pickup or dispatch"]),
            Stage("Filing and Return", "Legal Admin Assistant", "Completed document is filed and property file is returned to originating department.", ["Upload final document", "Update legal register", "Return file to Estate"])
        ];
    }

    private static IReadOnlyList<LegalWorkspaceDocument> BuildDocuments(string entityType)
    {
        if (string.Equals(entityType, "LegalPropertyAgreementReview", StringComparison.OrdinalIgnoreCase))
        {
            return UniqueDocuments([
                Doc("Generated draft agreement", "Legal Intake", true),
                Doc("Legal review note", "Agreement Vetting", true),
                Doc("Customer signed agreement", "Customer Signature Return", true),
                Doc("Head of Legal signed agreement", "Head of Legal Signature", true)
            ]);
        }

        if (string.Equals(entityType, "LegalCourtProcess", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entityType, "LegalOtherCourtProcess", StringComparison.OrdinalIgnoreCase))
        {
            return UniqueDocuments([
                Doc("Served court process", "Registry / Court bailiff", true),
                Doc("Court jacket / docket", "Legal Officer", true),
                Doc("Filed response / court filing evidence", "Legal Admin Assistant / Court", true),
                Doc("Hearing notice or next action note", "Court / Legal Officer", false)
            ]);
        }

        if (string.Equals(entityType, "LegalLeaseVariationRenewalSublease", StringComparison.OrdinalIgnoreCase))
        {
            return UniqueDocuments([
                Doc("Lease request and Estate forwarding letter", "Estate Department", true),
                Doc("Cadastral plan and schedule", "Estate / Planning", true),
                Doc("Draft lease / deed of variation / sublease", "Secretary", true),
                Doc("Client and witness execution evidence", "Client / Legal", true),
                Doc("Registration handover evidence", "Legal / Lands Commission", false)
            ]);
        }

        if (string.Equals(entityType, "LegalAssignmentSubleaseVesting", StringComparison.OrdinalIgnoreCase))
        {
            return UniqueDocuments([
                Doc("Consent to assign / sublease request", "Estate / Applicant", true),
                Doc("Recognition of vesting evidence", "Applicant / Legal", false),
                Doc("Draft consent / recognition letter", "Legal Admin Assistant", true),
                Doc("Signed and sealed instrument", "Legal Department", true)
            ]);
        }

        if (string.Equals(entityType, "LegalMortgage", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entityType, "LegalMortgageInPrinciple", StringComparison.OrdinalIgnoreCase))
        {
            return UniqueDocuments([
                Doc("Mortgage consent request", "Estate / Applicant", true),
                Doc("Financial institution / mortgagee letter", "Applicant / Mortgagee", true),
                Doc("Draft mortgage consent / in-principle letter", "Legal Admin Assistant", true),
                Doc("Signed mortgage letter", "Head of Legal / MD", true)
            ]);
        }

        if (string.Equals(entityType, "LegalTerminationRecognition", StringComparison.OrdinalIgnoreCase))
        {
            return UniqueDocuments([
                Doc("Site report", "Estate / Legal Clerk", true),
                Doc("Termination notice", "Legal Admin Assistant", true),
                Doc("21-day posting evidence", "Legal Clerk", true),
                Doc("Recognition draft / declaration", "Legal Officer", true),
                Doc("Executed recognition document", "Client / Legal", true)
            ]);
        }

        if (string.Equals(entityType, "LegalTransfer", StringComparison.OrdinalIgnoreCase))
        {
            return UniqueDocuments([
                Doc("Transfer file from Estate", "Estate Department", true),
                Doc("Transfer fee payment receipt", "Finance / Client", true),
                Doc("Draft transfer form", "Legal Admin Assistant", true),
                Doc("Executed transfer form", "Client / Legal", true),
                Doc("Signed transfer distribution / Estate return note", "Legal Admin Assistant", true)
            ]);
        }

        return UniqueDocuments([
            Doc("Source request / forwarding minute", "Estate / Originating Department", true),
            Doc("Property file extract", "Estate Records", true),
            Doc("Payment / receipt evidence", "Finance / Client", false),
            Doc("Legal review note", "Legal Officer", true),
            Doc("Final signed / dispatched document", "Legal Department", true)
        ]);
    }

    private static IReadOnlyList<LegalWorkspaceDocument> UniqueDocuments(IEnumerable<LegalWorkspaceDocument> documents)
    {
        return documents
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static IReadOnlyList<LegalWorkspaceField> BuildFields(string entityType)
    {
        var fields = new List<LegalWorkspaceField>
        {
            Field("referenceNumber", "Legal reference number", "text"),
            Field("legalFileNumber", "Legal file / matter number", "text"),
            Field("legalFileRegisterStatus", "Legal file register status", "select", ["Pending registration", "Registered", "Returned for correction", "Closed"]),
            Field("registryReceiptReference", "Registry receipt reference", "text"),
            Field("registryReceiptDate", "Registry receipt date", "date"),
            Field("secretaryRoutingDate", "Secretary routing date", "date"),
            Field("holAssignmentDate", "Head of Legal assignment date", "date"),
            Field("loOwnershipStatus", "Legal Officer ownership status", "select", ["Unassigned", "Assigned", "In review", "Returned", "Completed"]),
            Field("sourceProcedureCaseId", "Originating procedure case ID", "text"),
            Field("sourceEntityType", "Originating entity type", "text"),
            Field("sourceRecordReference", "Originating transaction reference", "text"),
            Field("matterPurpose", "Legal matter purpose", "text"),
            Field("transactionType", "Property transaction type", "text"),
            Field("agreementReference", "Agreement / DMS reference", "text"),
            Field("sourceDepartment", "Source department", "select", ["Estate", "Property Management", "Finance", "Managing Director", "External Party", "Court / Registry", "Other"]),
            Field("propertyFileReference", "Property file reference", "text"),
            Field("propertyNumber", "Property / plot / house number", "text"),
            Field("estateManagedAssetId", "Linked property ID", "text"),
            Field("customerBusinessPartnerId", "Applicant customer ID", "text"),
            Field("applicantName", "Applicant / lessee / client name", "text"),
            Field("receivedDate", "Received date", "date"),
            Field("assignedLegalOfficer", "Assigned Legal Officer", "text"),
            Field("paymentStatus", "Payment status", "select", ["Not required", "Pending", "Paid", "Waived / exception approved"]),
            Field("paymentDueDate", "Payment due date", "date"),
            Field("clientPaymentDate", "Client payment date", "date"),
            Field("paymentReceiptReference", "Payment / receipt reference", "text"),
            Field("financeVerificationStatus", "Finance verification status", "select", ["Not required", "Pending Finance verification", "Verified", "Rejected", "Exception approved"]),
            Field("financeVerificationReference", "Finance verification reference", "text"),
            Field("cadastralPlanStatus", "Cadastral plan status", "select", ["Not required", "Pending", "Available", "Returned for correction"]),
            Field("scheduleStatus", "Schedule insertion status", "select", ["Not required", "Pending", "Inserted", "Returned for correction"]),
            Field("dueDiligenceStatus", "Due diligence status", "select", ["Not started", "In progress", "Cleared", "Issue found", "Returned"]),
            Field("templateCode", "Document template code", "text"),
            Field("generatedDocumentStatus", "Generated document status", "select", ["Not started", "Template selected", "Draft generated", "Under review", "Final generated", "Not required"]),
            Field("draftDocumentReference", "Draft document reference", "text"),
            Field("draftVersionStatus", "Draft version status", "select", ["Not started", "Current draft", "Returned for correction", "Approved for execution", "Finalized"]),
            Field("vettingCommentStatus", "Vetting comments status", "select", ["Not started", "Open comments", "Comments resolved", "No comments"]),
            Field("legalVettingStatus", "Legal vetting status", "select", ["Not started", "Under review", "Approved", "Returned for correction"]),
            Field("signatureStatus", "Signature status", "select", ["Not started", "Client signed", "Legal signed", "Head of Legal signed", "MD signed", "Fully signed"]),
            Field("clientSignatureDate", "Client signature date", "date"),
            Field("legalOfficerSignatureDate", "Legal Officer signature date", "date"),
            Field("legalAdminSignatureDate", "Legal Admin signature date", "date"),
            Field("headOfLegalSignatureDate", "Head of Legal signature date", "date"),
            Field("managingDirectorSignatureDate", "Managing Director signature date", "date"),
            Field("sealStatus", "Seal / dating status", "select", ["Not required", "Pending", "Sealed", "Dated", "Sealed and dated"]),
            Field("sealRegisterNumber", "Seal register number", "text"),
            Field("sealedDate", "Sealed date", "date"),
            Field("dispatchStatus", "Dispatch / pickup status", "select", ["Not started", "Client notified", "Picked up", "Dispatched", "Filed"]),
            Field("dispatchMethod", "Dispatch / collection method", "select", ["Not recorded", "Client pickup", "Courier", "Registered mail", "Internal handoff", "Portal release"]),
            Field("dispatchReference", "Dispatch / collection reference", "text"),
            Field("dispatchRecipient", "Dispatch recipient", "text"),
            Field("collectionDate", "Collection / dispatch date", "date"),
            Field("estateReturnStatus", "Estate file return status", "select", ["Not required", "Pending return", "Returned to Estate", "Returned for correction"]),
            Field("estateFileReturnDate", "Estate file return date", "date"),
            Field("estateRecordsAmendmentStatus", "Estate records amendment status", "select", ["Not required", "Pending", "Confirmed", "Returned for correction"]),
            Field("estateRecordsAmendmentDate", "Estate records amendment date", "date"),
            Field("revenueUpdateStatus", "Revenue update status", "select", ["Not required", "Pending", "Updated", "Returned for correction"]),
            Field("landsCommissionHandoff", "Lands Commission handoff status", "select", ["Not required", "Pending", "Sent", "Registered", "Returned"]),
            Field("landsCommissionSubmissionDate", "Lands Commission submission date", "date"),
            Field("landsCommissionRegistrationNumber", "Lands Commission registration number", "text"),
            Field("landsCommissionReturnDate", "Lands Commission return date", "date"),
            Field("closeoutNotes", "Closeout notes", "textarea")
        };

        if (string.Equals(entityType, "LegalCourtProcess", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entityType, "LegalOtherCourtProcess", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                Field("courtProcessType", "Court process type", "select", ["Writ of Summons", "Motion", "Notice", "Order", "Letter", "Other"]),
                Field("courtName", "Court name", "text"),
                Field("caseNumber", "Court case number", "text"),
                Field("courtVenue", "Court venue / location", "text"),
                Field("judgeName", "Judge / adjudicator", "text"),
                Field("claimantName", "Claimant / applicant", "text"),
                Field("defendantName", "Defendant / respondent", "text"),
                Field("claimAmount", "Claim / exposure amount", "currency"),
                Field("litigationRisk", "Litigation risk", "select", ["Low", "Medium", "High", "Critical"]),
                Field("courtMatterStatus", "Court matter status", "select", ["Pending", "Active", "Stayed", "Awaiting judgment", "On appeal", "Closed"]),
                Field("courtOutcome", "Court outcome", "select", ["Pending", "Won", "Lost", "Settled", "Withdrawn", "Struck out", "Not applicable"]),
                Field("serviceDate", "Service date", "date"),
                Field("responseDeadline", "Response / filing deadline", "date"),
                Field("courtJacketReference", "Court jacket / docket reference", "text"),
                Field("courtJacketMovementStatus", "Court jacket movement status", "select", ["Not opened", "Opened", "With Legal Officer", "Returned to Registry", "Closed"]),
                Field("appearanceDeadline", "Appearance deadline", "date"),
                Field("filingReference", "Court filing reference", "text"),
                Field("filingReceiptReference", "Filing receipt reference", "text"),
                Field("nextHearingDate", "Next hearing date", "date"),
                Field("adjournmentHistory", "Adjournment / hearing history", "textarea"),
                Field("judgmentDate", "Judgment / settlement date", "date"),
                Field("judgmentSummary", "Judgment / settlement summary", "textarea"),
                Field("litigationExposureAmount", "Litigation exposure / provision amount", "currency"),
                Field("appealStatus", "Appeal status", "select", ["Not applicable", "Under consideration", "Filed", "Concluded"])
            ]);
        }

        if (string.Equals(entityType, "LegalLeaseVariationRenewalSublease", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                Field("instrumentType", "Instrument type", "select", ["Lease", "Supplementary Lease", "Deed of Variation", "Renewal", "Sublease"]),
                Field("lesseeName", "Lessee name", "text"),
                Field("leaseTerm", "Lease term", "text"),
                Field("scheduleReference", "Schedule reference", "text"),
                Field("clientExecutionDate", "Client execution date", "date"),
                Field("witnessName", "Witness name", "text"),
                Field("registrationHandoffReference", "Registration handoff reference", "text")
            ]);
        }

        if (string.Equals(entityType, "LegalAssignmentSubleaseVesting", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                Field("instrumentType", "Instrument type", "select", ["Assignment", "Sublease", "Recognition of Vesting"]),
                Field("assignorName", "Assignor / current lessee", "text"),
                Field("assigneeName", "Assignee / incoming party", "text"),
                Field("vestingInstrumentReference", "Vesting instrument reference", "text"),
                Field("consentDecision", "Consent / recognition decision", "select", ["Pending", "Approved", "Returned", "Rejected"])
            ]);
        }

        if (string.Equals(entityType, "LegalMortgage", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entityType, "LegalMortgageInPrinciple", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                Field("mortgageType", "Mortgage type", "select", ["Consent to Mortgage", "Mortgage in Principle"]),
                Field("mortgageeName", "Mortgagee / financial institution", "text"),
                Field("mortgageLetterReference", "Mortgage letter reference", "text"),
                Field("mdSignatureRequired", "MD signature required", "select", ["Yes", "No"])
            ]);
        }

        if (string.Equals(entityType, "LegalTerminationRecognition", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                Field("terminationReason", "Termination reason", "textarea"),
                Field("siteReportReference", "Site report reference", "text"),
                Field("noticePostingStartDate", "Notice posting start date", "date"),
                Field("noticePostingEndDate", "Notice posting end date", "date"),
                Field("recognitionApplicantName", "Recognition applicant name", "text"),
                Field("recognitionPaymentStatus", "Recognition payment status", "select", ["Pending", "Paid", "Waived / exception approved"]),
                Field("recognitionDocumentReference", "Recognition document reference", "text")
            ]);
        }

        if (string.Equals(entityType, "LegalTransfer", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                Field("transferorName", "Transferor name", "text"),
                Field("transfereeName", "Transferee name", "text"),
                Field("interviewDate", "Applicant / transferee interview date", "date"),
                Field("transferFeePayable", "Transfer fee payable", "currency"),
                Field("transferFeePaymentRequestReference", "Transfer fee payment request reference", "text"),
                Field("transferFeeInvoiceReference", "Transfer fee invoice reference", "text"),
                Field("transferFeeInvoiceStatus", "Transfer fee invoice status", "text"),
                Field("transferFeeInvoiceAmount", "Transfer fee invoice amount", "currency"),
                Field("transferFeeInvoicePaidAmount", "Transfer fee invoice paid amount", "currency"),
                Field("transferFeeInvoiceBalance", "Transfer fee invoice balance", "currency"),
                Field("transferFeePaymentCheckStatus", "Transfer fee payment check", "text"),
                Field("transferFeeReceipt", "Transfer fee receipt", "text"),
                Field("mdApprovalReference", "Managing Director approval reference", "text"),
                Field("transferDeclarationReference", "Transfer declaration reference", "text"),
                Field("distributionStatus", "Signed transfer distribution status", "select", ["Not started", "Distributed", "Returned to Estate Records"])
            ]);
        }

        if (string.Equals(entityType, "LegalOpinionAdvisory", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                Field("advisorySubject", "Advisory subject", "text"),
                Field("confidentialityLevel", "Confidentiality level", "select", ["Internal", "Restricted", "Privileged", "Board / Executive"]),
                Field("legalRiskLevel", "Legal risk level", "select", ["Low", "Medium", "High", "Critical"]),
                Field("opinionDueDate", "Opinion due date", "date"),
                Field("recommendationStatus", "Recommendation / advice status", "select", ["Not started", "Drafting", "Under HOL review", "Approved", "Dispatched", "Closed"]),
                Field("adviceRecipient", "Advice recipient", "text")
            ]);
        }

        if (string.Equals(entityType, "LegalExternalCounsel", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                Field("counselName", "External counsel / law firm", "text"),
                Field("instructionReference", "Instruction / retainer reference", "text"),
                Field("counselMatterStatus", "External counsel matter status", "select", ["Instruction pending", "In progress", "Deliverable received", "Invoice pending", "Closed"]),
                Field("counselDeliverableDueDate", "Counsel deliverable due date", "date"),
                Field("feeEstimate", "Fee estimate", "currency"),
                Field("approvedCounselFee", "Approved counsel fee", "currency"),
                Field("invoiceReference", "Invoice / AP reference", "text"),
                Field("performanceRating", "Performance rating", "select", ["Not rated", "Good", "Satisfactory", "Needs attention"])
            ]);
        }

        return UniqueFields(fields);
    }

    private static IReadOnlyList<string> BuildOutputs(string entityType)
    {
        if (string.Equals(entityType, "LegalCourtProcess", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entityType, "LegalOtherCourtProcess", StringComparison.OrdinalIgnoreCase))
        {
            return ["Recorded court process", "Court jacket / docket", "Filed response or process", "Court calendar entry", "Judgment / outcome tracker", "Legal closeout note"];
        }

        if (string.Equals(entityType, "LegalTerminationRecognition", StringComparison.OrdinalIgnoreCase))
        {
            return ["Termination notice", "21-day posting evidence", "Recognition document", "Executed signature pack", "Dispatch / collection register entry", "Estate records update handoff"];
        }

        if (string.Equals(entityType, "LegalTransfer", StringComparison.OrdinalIgnoreCase))
        {
            return ["Transfer fee confirmation", "Transfer declaration", "Executed transfer form", "Signed distribution pack", "Signature / sealing register entry", "Estate records amendment handoff"];
        }

        if (string.Equals(entityType, "LegalMortgage", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entityType, "LegalMortgageInPrinciple", StringComparison.OrdinalIgnoreCase))
        {
            return ["Payment confirmation", "Draft mortgage letter", "Vetted mortgage consent / in-principle letter", "Signed letter", "Dispatch / collection register entry", "Estate file return note"];
        }

        if (string.Equals(entityType, "LegalLeaseVariationRenewalSublease", StringComparison.OrdinalIgnoreCase))
        {
            return ["Draft lease / variation / renewal / sublease", "Execution pack", "Signed and sealed instrument", "Seal register entry", "Registration handoff", "Estate file return note"];
        }

        if (string.Equals(entityType, "LegalAssignmentSubleaseVesting", StringComparison.OrdinalIgnoreCase))
        {
            return ["Draft consent / recognition instrument", "Vetted legal letter", "Signed and sealed instrument", "Client release note", "Dispatch / collection register entry", "Estate file return note"];
        }

        if (string.Equals(entityType, "LegalOpinionAdvisory", StringComparison.OrdinalIgnoreCase))
        {
            return ["Issue summary", "Research / review note", "Legal opinion memo", "Approved advice", "Confidential dispatch evidence"];
        }

        if (string.Equals(entityType, "LegalExternalCounsel", StringComparison.OrdinalIgnoreCase))
        {
            return ["Counsel instruction", "Retainer / fee record", "Deliverable review", "Invoice/AP handoff", "Performance closeout"];
        }

        return ["Legal intake record", "Legal file movement entry", "Due diligence note", "Generated draft document", "Approved / signed document", "Dispatch or Estate return evidence"];
    }

    private static IReadOnlyList<LegalWorkspaceHandoff> BuildHandoffs(string entityType)
    {
        var handoffs = new List<LegalWorkspaceHandoff>
        {
            Handoff("Estate / Originating Department", "Head of Legal", "Property file, request, or court process is submitted to Legal."),
            Handoff("Head of Legal", "Legal Officer / Legal Admin", "Matter is minuted and assigned for due diligence or drafting."),
            Handoff("Legal Officer", "Head of Legal", "Draft or advice is approved for signature routing."),
            Handoff("Legal Admin Assistant", "Estate / Originating Department", "Completed file is returned with signed document or legal closeout note.")
        };

        if (string.Equals(entityType, "LegalCourtProcess", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entityType, "LegalOtherCourtProcess", StringComparison.OrdinalIgnoreCase))
        {
            handoffs.Add(Handoff("Legal Admin Assistant", "Court", "Approved response or process is filed at court."));
        }

        if (string.Equals(entityType, "LegalMortgage", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entityType, "LegalLeaseVariationRenewalSublease", StringComparison.OrdinalIgnoreCase))
        {
            handoffs.Add(Handoff("Head of Legal", "Managing Director", "Document requires executive signature."));
        }

        if (string.Equals(entityType, "LegalLeaseVariationRenewalSublease", StringComparison.OrdinalIgnoreCase))
        {
            handoffs.Add(Handoff("Legal Department", "Lands Commission", "Executed instrument is handed over for registration where applicable."));
        }

        if (string.Equals(entityType, "LegalTransfer", StringComparison.OrdinalIgnoreCase))
        {
            handoffs.Add(Handoff("Legal Department", "Estate Records", "Signed transfer forms are returned for records and ledger amendment."));
        }

        return handoffs;
    }

    private static LegalWorkspaceStage Stage(
        string name,
        string owner,
        string summary,
        IReadOnlyList<string> checklist) =>
        new(name, owner, checklist);

    private static LegalWorkspaceDocument Doc(string name, string requiredFrom, bool isMandatory) =>
        new(name, requiredFrom, isMandatory);

    private static LegalWorkspaceField Field(
        string key,
        string label,
        string type,
        IReadOnlyList<string>? options = null) =>
        new(key, label, type, options);

    private static IReadOnlyList<LegalWorkspaceField> UniqueFields(IEnumerable<LegalWorkspaceField> fields) =>
        fields
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

    private static LegalWorkspaceHandoff Handoff(string fromRole, string toRole, string trigger) =>
        new(fromRole, toRole, trigger);
}
