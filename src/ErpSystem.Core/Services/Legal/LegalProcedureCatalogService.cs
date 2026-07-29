using ErpSystem.Core.Interfaces.Legal;

namespace ErpSystem.Core.Services.Legal;

public sealed class LegalProcedureCatalogService : ILegalProcedureCatalogService
{
    private static readonly LegalProcedureCatalogItem[] Procedures =
    [
        new(
            "Legal Department Procedure Manual",
            "LegalProcedure",
            "Source: Legal - Procedure Manual",
            "General legal intake, review, drafting, approval, execution, and record keeping workflow.",
            "BookOpen",
            6,
            "slate"),
        new(
            "Mortgages",
            "LegalMortgage",
            "Source: Legal - Mortgages",
            "Mortgage request review, document preparation, execution support, and completion tracking.",
            "FileSignature",
            7,
            "cyan"),
        new(
            "Mortgage In Principle",
            "LegalMortgageInPrinciple",
            "Source: Legal - Mortgage In Principle",
            "Initial mortgage review, legal checks, recommendation, and approval routing.",
            "FileCheck2",
            5,
            "emerald"),
        new(
            "Court Processes",
            "LegalCourtProcess",
            "Source: Legal - Court Processes",
            "Court process receipt, review, response preparation, filing, hearing, and follow-up.",
            "Scale",
            7,
            "violet"),
        new(
            "Other Court Processes",
            "LegalOtherCourtProcess",
            "Source: Legal - Other Court Processes",
            "Non-standard court matters routed for legal action, evidence handling, and closure.",
            "Gavel",
            6,
            "purple"),
        new(
            "Termination / Recognition",
            "LegalTerminationRecognition",
            "Source: Legal - Termination / Recognition",
            "Termination and recognition requests reviewed through legal validation and approval stages.",
            "ShieldCheck",
            7,
            "amber"),
        new(
            "Assignment / Sublease / Vesting",
            "LegalAssignmentSubleaseVesting",
            "Source: Legal - Assignment / Sublease / Vesting",
            "Instrument review, party verification, drafting, consent checks, and completion workflow.",
            "Landmark",
            8,
            "teal"),
        new(
            "Leases / Deed of Variation / Renewal / Sublease",
            "LegalLeaseVariationRenewalSublease",
            "Source: Legal - Leases / Variation / Renewal / Sublease",
            "Lease drafting, variation, renewal, sublease review, approval, execution, and filing.",
            "FileText",
            8,
            "sky"),
        new(
            "Transfers",
            "LegalTransfer",
            "Source: Legal - Transfers",
            "Transfer request validation, document review, approval, execution, registration, and records.",
            "BadgeCheck",
            7,
            "blue")
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

        return procedure.EntityType switch
        {
            "LegalMortgage" => Mortgage(procedure),
            "LegalMortgageInPrinciple" => MortgageInPrinciple(procedure),
            "LegalCourtProcess" => CourtProcess(procedure, "Writ of summons"),
            "LegalOtherCourtProcess" => CourtProcess(procedure, "Other court process"),
            "LegalTerminationRecognition" => TerminationRecognition(procedure),
            "LegalAssignmentSubleaseVesting" => AssignmentSubleaseVesting(procedure),
            "LegalLeaseVariationRenewalSublease" => LeaseVariationRenewalSublease(procedure),
            "LegalTransfer" => Transfer(procedure),
            _ => General(procedure)
        };
    }

    private static LegalProcedureWorkspace General(LegalProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Receive and register", "Legal Registry / Legal Admin Assistant", "Receive the request, open the legal matter, and attach the property file or court docket.", "Record reference, source department, applicant, and received date.", "Confirm the request is addressed to the Head of Legal.", "Attach the property file, court process, or supporting documents."),
                Stage("Minute and assign", "Head of Legal", "Classify the matter and minute it to the Legal Officer, Legal Admin Assistant, or Secretary.", "Confirm procedure type and priority.", "Assign responsible officer.", "Set drafting, payment, filing, or approval instructions."),
                Stage("Due diligence", "Legal Officer", "Review ownership, payment, cadastral, schedule, site report, court deadline, and supporting-document completeness.", "Check payments and arrears.", "Confirm cadastral plan, schedule, or site report where applicable.", "Raise gaps with Estate, Registry, Finance, or the applicant."),
                Stage("Draft and vet", "Legal Admin Assistant / Secretary / Legal Officer", "Prepare the legal instrument or court process and complete legal review.", "Draft the required instrument or process.", "Send draft to the Legal Officer for vetting.", "Correct and approve the final draft."),
                Stage("Approve and execute", "Head of Legal / Managing Director", "Route the approved document for signature, sealing, client execution, or court filing.", "Obtain Head of Legal signature.", "Route to the Managing Director where required.", "Capture client, witness, or filing execution where required."),
                Stage("Dispatch and records", "Legal Admin Assistant / Estate Records", "Release completed documents and return the file for records update.", "Date and seal final documents.", "Dispatch applicant, court, or Lands Commission copies.", "Return the file to Estate or Registry for records amendment.")
            ],
            [
                Doc("Request memo", "Receive and register", true),
                Doc("Property file or court docket", "Receive and register", true),
                Doc("Payment evidence or fee approval", "Due diligence", false),
                Doc("Cadastral plan, site report, or schedule", "Due diligence", false),
                Doc("Draft instrument or court process", "Draft and vet", true)
            ],
            CommonFields("Procedure type"),
            ["Approved legal instrument or court process", "Signed dispatch copy", "Updated legal register", "Returned property file or court docket"],
            CommonHandoffs(true));

    private static LegalProcedureWorkspace Mortgage(LegalProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Receive mortgage file", "Head of Legal", "Receive the property file from Estate and minute it for consent-to-mortgage action.", "Confirm the request is for consent to mortgage.", "Identify lessee, property, and mortgagee details.", "Minute payment follow-up to the Legal Admin Assistant."),
                Stage("Client payment follow-up", "Legal Admin Assistant", "Call the client to make payment within the payment window and attach evidence to the file.", "Notify client of payable fees.", "Track the 30-day payment window.", "Verify receipt with Finance."),
                Stage("Draft consent", "Legal Admin Assistant", "Prepare the draft consent to mortgage letter after payment confirmation.", "Use the approved consent template.", "Capture lease, property, and mortgagee references.", "Send draft to Legal Officer for vetting."),
                Stage("Vetting", "Legal Officer", "Review the draft consent and confirm that legal conditions are satisfied.", "Check ownership and encumbrance position.", "Confirm lease references.", "Approve or return draft corrections."),
                Stage("Final signatures", "Head of Legal / Managing Director", "Finalize the letterhead copy and route it for Head of Legal and Managing Director signature.", "Print approved final copy.", "Obtain Head of Legal signature.", "Route for Managing Director signature where required."),
                Stage("Release and return file", "Legal Admin Assistant", "Date, seal, release the consent to the client, and return the file to Estate.", "Date and seal final consent.", "Call client for collection.", "Record dispatch and return file to Estate.")
            ],
            [
                Doc("Property file", "Receive mortgage file", true),
                Doc("Consent to mortgage request", "Receive mortgage file", true),
                Doc("Payment receipt", "Client payment follow-up", true),
                Doc("Existing lease document", "Receive mortgage file", true),
                Doc("Mortgagee details", "Receive mortgage file", true)
            ],
            MortgageFields("Mortgagee / bank name"),
            ["Consent to mortgage letter", "Signed and sealed client copy", "File return note to Estate"],
            CommonHandoffs(true));

    private static LegalProcedureWorkspace MortgageInPrinciple(LegalProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Minute file for payment", "Head of Legal", "Receive the mortgage-in-principle request and minute it for payment follow-up.", "Confirm request category.", "Assign Legal Admin Assistant.", "Record expected response date."),
                Stage("Payment follow-up", "Legal Admin Assistant", "Call the client for payment and verify evidence before drafting.", "Notify client of fee.", "Track the 30-day payment window.", "Attach receipt to file."),
                Stage("Draft response", "Legal Admin Assistant", "Prepare the draft mortgage-in-principle response.", "Capture applicant, property, and lender details.", "Reference payment evidence.", "Send draft to Legal Officer."),
                Stage("Legal vetting", "Legal Officer", "Vet the preliminary response and confirm no obvious legal objection.", "Review title and lease references.", "Check file completeness.", "Approve final wording."),
                Stage("Finalize and sign", "Head of Legal", "Print the finalized letter and obtain Head of Legal signature.", "Prepare final letterhead copy.", "Obtain signature.", "Release response and update file.")
            ],
            [
                Doc("Property file", "Minute file for payment", true),
                Doc("Mortgage in principle request", "Minute file for payment", true),
                Doc("Payment receipt", "Payment follow-up", true),
                Doc("Lease or allocation reference", "Minute file for payment", true)
            ],
            MortgageFields("Proposed lender"),
            ["Mortgage in principle letter", "Payment follow-up record", "Updated legal file"],
            CommonHandoffs(false));

    private static LegalProcedureWorkspace CourtProcess(LegalProcedureCatalogItem procedure, string processType) =>
        Workspace(
            procedure,
            [
                Stage("Receive court process", "Registry", "Receive the served process and capture service details.", "Accept service from bailiff or server.", "Record service date and time.", "Forward process to Legal Admin Assistant."),
                Stage("Register and open docket", "Legal Admin Assistant", "Record the process and open or update the court jacket.", "Enter process in the court register.", "Open court jacket or docket.", "Attach served process."),
                Stage("Forward for assignment", "Secretary / Head of Legal", "Route the docket to the Head of Legal and assign a Legal Officer.", "Forward process through Secretary.", "Assign Legal Officer.", "Capture assignment date and deadline."),
                Stage("Prepare response", "Legal Officer", "Request the property file and prepare appearance, defence, affidavit, or other response.", "Request property file from Estate.", "Review evidence and deadlines.", "Prepare required court process."),
                Stage("File at court", "Legal Admin Assistant", "File the prepared process at court and capture proof of filing.", "Submit filing copy to court registry.", "Obtain filed copy or receipt.", "Update the court process register."),
                Stage("Monitor and close", "Legal Officer", "Track hearings, subsequent processes, and closure actions.", "Update hearing dates.", "Prepare follow-up filings.", "Close docket when the matter concludes.")
            ],
            [
                Doc("Served court process", "Receive court process", true),
                Doc("Court jacket or docket", "Register and open docket", true),
                Doc("Property file", "Prepare response", processType == "Writ of summons"),
                Doc("Evidence bundle", "Prepare response", false),
                Doc("Filed court copy or receipt", "File at court", true)
            ],
            CourtFields(processType),
            ["Court docket", "Filed court process", "Hearing update log", "Closure note"],
            [
                Handoff("Registry", "Legal Admin Assistant", "Court process is served on the company"),
                Handoff("Legal Admin Assistant", "Secretary / Head of Legal", "Process is recorded and ready for assignment"),
                Handoff("Head of Legal", "Legal Officer", "Matter is assigned"),
                Handoff("Legal Officer", "Legal Admin Assistant", "Process is ready for court filing"),
                Handoff("Legal Admin Assistant", "Court Registry", "Filing copy is complete")
            ]);

    private static LegalProcedureWorkspace TerminationRecognition(LegalProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Due diligence and site review", "Legal Officer", "Review the property file and site report before termination or recognition action.", "Confirm tenancy or allocation details.", "Review site report.", "Confirm basis for termination or recognition."),
                Stage("Draft termination notice", "Legal Admin Assistant", "Prepare the draft termination letter after Head of Legal instruction.", "Draft termination notice.", "Send draft for Head of Legal approval.", "Record notice details."),
                Stage("Approve and paste notice", "Head of Legal / Legal Clerk", "Approve the notice and paste it on the affected property for 21 days.", "Sign approved notice.", "Paste notice on site.", "Track 21-day notice expiry."),
                Stage("Payment and recognition draft", "Head of Legal / Legal Admin Assistant", "After notice expiry, approve payment and draft the recognition or tenancy declaration.", "Confirm notice expiry.", "Approve and verify payment.", "Draft recognition document for vetting."),
                Stage("Execution", "Client / Legal Officer / Legal Admin Assistant", "Collect client, witness, Legal Officer, and Legal Admin Assistant signatures.", "Call client for signature.", "Obtain witness signatures.", "Capture internal signatures."),
                Stage("Final signature and records", "Head of Legal / Estate", "Obtain final Head of Legal signature and return the file for records amendment.", "Route to Head of Legal.", "Distribute signed copies.", "Return file to Estate.")
            ],
            [
                Doc("Property file", "Due diligence and site review", true),
                Doc("Site report", "Due diligence and site review", true),
                Doc("Termination notice", "Draft termination notice", true),
                Doc("Notice-pasting evidence", "Approve and paste notice", true),
                Doc("Payment receipt", "Payment and recognition draft", true),
                Doc("Recognition or tenancy declaration", "Payment and recognition draft", true)
            ],
            CommonFields("Recognition / termination request type"),
            ["Termination notice", "Recognition or tenancy declaration", "Signed client copies", "Estate records amendment request"],
            [
                Handoff("Legal Officer", "Legal Admin Assistant", "Termination notice is required"),
                Handoff("Legal Admin Assistant", "Head of Legal", "Draft notice is ready for approval"),
                Handoff("Head of Legal", "Legal Clerk", "Notice is signed for posting"),
                Handoff("Client", "Legal Admin Assistant", "Payment and signature are complete"),
                Handoff("Head of Legal", "Estate", "Final document is signed")
            ]);

    private static LegalProcedureWorkspace AssignmentSubleaseVesting(LegalProcedureCatalogItem procedure) =>
        InstrumentWorkspace(
            procedure,
            "assignment, sublease, or vesting",
            "Consent to assign / sublet / recognition of vesting",
            ["Consent to assign", "Consent to sublet", "Recognition of vesting", "Returned Estate file"]);

    private static LegalProcedureWorkspace LeaseVariationRenewalSublease(LegalProcedureCatalogItem procedure) =>
        InstrumentWorkspace(
            procedure,
            "lease, supplementary lease, deed of variation, renewal, or sublease",
            "Lease / variation / renewal / sublease instrument",
            ["Lease", "Supplementary lease", "Deed of variation", "Renewal instrument", "Sublease consent", "Registration release note"]);

    private static LegalProcedureWorkspace Transfer(LegalProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Due diligence", "Legal Officer", "Review the transfer request, property file, ownership position, and supporting documents.", "Confirm transferor and transferee details.", "Review property file.", "Identify payment and approval requirements."),
                Stage("Interview parties", "Legal Officer", "Invite applicants and transferees for interview and validation.", "Schedule interview.", "Confirm identity and capacity.", "Record interview outcome."),
                Stage("Fee calculation and approval", "Estate / Managing Director", "Refer the file to Estate for fee calculation and obtain approval.", "Request fee calculation.", "Obtain approval for calculated fees.", "Return approved file to Legal."),
                Stage("Payment and draft transfer", "Client / Legal Officer", "Receive payment and prepare transfer declaration forms.", "Confirm transfer fee payment.", "Prepare draft transfer declaration.", "Send draft for vetting."),
                Stage("Client execution", "Client / Witnesses", "Call the parties to execute transfer declarations with witnesses.", "Notify transferor and transferee.", "Capture witness signatures.", "Confirm execution completeness."),
                Stage("Legal signatures and records", "Legal Officer / Head of Legal / Estate", "Route for internal signatures, distribute signed copies, and return file to Estate for amendment.", "Obtain Legal Officer and Legal Admin Assistant signatures.", "Obtain Head of Legal signature.", "Forward file to Estate for records amendment.")
            ],
            [
                Doc("Property file", "Due diligence", true),
                Doc("Transfer request", "Due diligence", true),
                Doc("Transfer fee approval", "Fee calculation and approval", true),
                Doc("Payment receipt", "Payment and draft transfer", true),
                Doc("Transfer declaration forms", "Payment and draft transfer", true),
                Doc("Identity and witness details", "Client execution", true)
            ],
            CommonFields("Transfer request type"),
            ["Transfer declaration forms", "Signed transfer copies", "Estate amendment instruction", "Legal file copy"],
            [
                Handoff("Legal Officer", "Estate", "Fee calculation is required"),
                Handoff("Estate", "Managing Director", "Calculated fees need approval"),
                Handoff("Client", "Legal Officer", "Payment and execution are complete"),
                Handoff("Legal Officer", "Head of Legal", "Transfer is ready for final legal signature"),
                Handoff("Head of Legal", "Estate", "Records amendment is required")
            ]);

    private static LegalProcedureWorkspace InstrumentWorkspace(LegalProcedureCatalogItem procedure, string instrumentType, string documentName, IReadOnlyList<string> outputs) =>
        Workspace(
            procedure,
            [
                Stage("Receive and minute file", "Head of Legal", $"Receive the property file for {instrumentType} action and minute it for drafting.", "Confirm instrument type.", "Check property and party references.", "Minute the file to the drafter."),
                Stage("Due diligence", "Legal Officer", "Review payment, lease, cadastral, schedule, and party details before drafting.", "Check payments and arrears.", "Confirm parties and property references.", "Confirm cadastral plan and schedule."),
                Stage("Draft instrument", "Legal Admin Assistant / Secretary", $"Draft the {documentName.ToLowerInvariant()} using the approved template.", "Prepare draft instrument.", "Capture correct parties and property references.", "Send draft to Legal Officer."),
                Stage("Vetting", "Legal Officer", "Vet the draft and approve it for final printing.", "Review legal sufficiency.", "Confirm schedules and plan references.", "Approve or return corrections."),
                Stage("Signatures", "Head of Legal / Managing Director", "Print final copy and route for required internal signatures.", "Print final letterhead or instrument copy.", "Obtain Head of Legal signature.", "Route to Managing Director where required."),
                Stage("Release and records", "Legal Admin Assistant / Estate", "Date, seal, release the document, and return the property file for records update.", "Date and seal final document.", "Notify client for collection or signing.", "Return property file to Estate.")
            ],
            [
                Doc("Property file", "Receive and minute file", true),
                Doc($"{documentName} request", "Receive and minute file", true),
                Doc("Payment evidence", "Due diligence", true),
                Doc("Cadastral plan and schedule", "Due diligence", true),
                Doc($"Draft {documentName.ToLowerInvariant()}", "Draft instrument", true)
            ],
            CommonFields("Instrument type"),
            outputs,
            CommonHandoffs(true));

    private static LegalProcedureWorkspace Workspace(
        LegalProcedureCatalogItem procedure,
        IReadOnlyList<LegalWorkspaceStage> stages,
        IReadOnlyList<LegalWorkspaceDocument> documents,
        IReadOnlyList<LegalWorkspaceField> fields,
        IReadOnlyList<string> outputs,
        IReadOnlyList<LegalWorkspaceHandoff> handoffs) =>
        new(procedure, stages, documents, fields, outputs, handoffs);

    private static LegalWorkspaceStage Stage(string name, string owner, string summary, params string[] checklist) =>
        new(name, owner, summary, checklist);

    private static LegalWorkspaceDocument Doc(string name, string requiredFrom, bool isMandatory) =>
        new(name, requiredFrom, isMandatory);

    private static LegalWorkspaceField Field(string key, string label, string type, params string[] options) =>
        new(key, label, type, options.Length == 0 ? null : options);

    private static LegalWorkspaceHandoff Handoff(string fromRole, string toRole, string trigger) =>
        new(fromRole, toRole, trigger);

    private static IReadOnlyList<LegalWorkspaceField> CommonFields(string requestTypeLabel) =>
    [
        Field("referenceNumber", "Reference number", "text"),
        Field("requestType", requestTypeLabel, "select", "Lease", "Assignment", "Mortgage", "Transfer", "Court process", "Recognition"),
        Field("applicantName", "Applicant / party name", "text"),
        Field("propertyReference", "Property reference", "text"),
        Field("originatingDepartment", "Originating department", "select", "Estate", "Registry", "Finance", "External party", "Court"),
        Field("receivedDate", "Received date", "date"),
        Field("priority", "Priority", "select", "Normal", "Urgent", "Court deadline")
    ];

    private static IReadOnlyList<LegalWorkspaceField> MortgageFields(string lenderLabel) =>
    [
        Field("referenceNumber", "Reference number", "text"),
        Field("lesseeName", "Lessee name", "text"),
        Field("propertyReference", "Property reference", "text"),
        Field("lenderName", lenderLabel, "text"),
        Field("paymentStatus", "Payment status", "select", "Pending", "Paid", "Waived"),
        Field("receivedDate", "Received date", "date")
    ];

    private static IReadOnlyList<LegalWorkspaceField> CourtFields(string processType) =>
    [
        Field("suitNumber", "Suit number", "text"),
        Field("processType", "Process type", "text", processType),
        Field("courtName", "Court", "text"),
        Field("parties", "Parties", "textarea"),
        Field("serviceDate", "Service date", "date"),
        Field("responseDeadline", "Response deadline", "date"),
        Field("assignedOfficer", "Assigned Legal Officer", "text")
    ];

    private static IReadOnlyList<LegalWorkspaceHandoff> CommonHandoffs(bool includeManagingDirector)
    {
        var handoffs = new List<LegalWorkspaceHandoff>
        {
            Handoff("Estate / Registry", "Head of Legal", "File is received for legal action"),
            Handoff("Head of Legal", "Legal Admin Assistant / Secretary", "File is minuted for drafting or payment follow-up"),
            Handoff("Legal Admin Assistant / Secretary", "Legal Officer", "Draft is ready for vetting"),
            Handoff("Legal Officer", "Head of Legal", "Draft has been approved")
        };

        if (includeManagingDirector)
        {
            handoffs.Add(Handoff("Head of Legal", "Managing Director", "Executive signature is required"));
        }

        handoffs.Add(Handoff("Legal Admin Assistant", "Estate Records", "Final document is released and records need update"));
        return handoffs;
    }
}
