using ErpSystem.Core.Interfaces.Legal;

namespace ErpSystem.Core.Services.Legal;

public sealed class LegalProcedureCatalogService : ILegalProcedureCatalogService
{
    private static readonly LegalProcedureCatalogItem[] Procedures =
    [
        new("Legal Department Procedure Manual", "LegalProcedure", "Source: Legal - Procedure Manual", "General legal intake, review, drafting, approval, execution, and record keeping through configured workflows.", "BookOpen", 0, "slate"),
        new("Legal Opinions / Advisory", "LegalOpinionAdvisory", "Source: Legal - Advisory / Opinion Requests", "Legal opinion requests, issue summaries, research notes, advice memos, confidentiality, approvals, and closure routed through configured workflows.", "MessageSquare", 0, "indigo"),
        new("External Counsel Management", "LegalExternalCounsel", "Source: Legal - External Counsel / Law Firm Oversight", "External counsel instructions, retainers, matter assignment, fees, performance, invoices, and closeout routed through configured workflows.", "Briefcase", 0, "zinc"),
        new("Mortgages", "LegalMortgage", "Source: Legal - Mortgages", "Mortgage request review, document preparation, execution support, and completion tracking through configured workflows.", "FileSignature", 0, "cyan"),
        new("Mortgage In Principle", "LegalMortgageInPrinciple", "Source: Legal - Mortgage In Principle", "Initial mortgage review, legal checks, recommendation, and approval routing through configured workflows.", "FileCheck2", 0, "emerald"),
        new("Court Processes", "LegalCourtProcess", "Source: Legal - Court Processes", "Court process receipt, review, response preparation, filing, hearing, and follow-up through configured workflows.", "Scale", 0, "violet"),
        new("Other Court Processes", "LegalOtherCourtProcess", "Source: Legal - Other Court Processes", "Non-standard court matters routed for configured legal action, evidence handling, and closure.", "Gavel", 0, "purple"),
        new("Termination / Recognition", "LegalTerminationRecognition", "Source: Legal - Termination / Recognition", "Termination and recognition requests reviewed through configured legal workflows.", "ShieldCheck", 0, "amber"),
        new("Assignment / Sublease / Vesting", "LegalAssignmentSubleaseVesting", "Source: Legal - Assignment / Sublease / Vesting", "Instrument review, party verification, drafting, consent checks, and completion through configured workflows.", "Landmark", 0, "teal"),
        new("Leases / Deed of Variation / Renewal / Sublease", "LegalLeaseVariationRenewalSublease", "Source: Legal - Leases / Variation / Renewal / Sublease", "Lease drafting, variation, renewal, sublease review, approval, execution, and filing through configured workflows.", "FileText", 0, "sky"),
        new("Transfers", "LegalTransfer", "Source: Legal - Transfers", "Transfer request validation, document review, approval, execution, registration, and records through configured workflows.", "BadgeCheck", 0, "blue")
    ];

    public IReadOnlyList<LegalProcedureCatalogItem> GetProcedures() => Procedures;

    public LegalProcedureWorkspace? GetProcedureWorkspace(string entityType)
    {
        var procedure = Procedures.FirstOrDefault(item =>
            string.Equals(item.EntityType, entityType, StringComparison.OrdinalIgnoreCase));

        return procedure is null
            ? null
            : new LegalProcedureWorkspace(procedure, [], [], [], [], []);
    }
}
