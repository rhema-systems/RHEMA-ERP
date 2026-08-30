using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

public sealed partial class CivilEngineeringDesignService
{
    /// <summary>
    /// Revalidates, rather than copies, the authoritative commercial records needed
    /// by an Engineering Case before downstream execution is started.  Civil owns
    /// neither the contract/budget/QS/Finance records nor any service-delivery ledger.
    /// </summary>
    public async Task<CivilEngineeringCommercialReadinessDto> GetCommercialReadinessAsync(
        Guid designCaseId,
        CancellationToken token = default)
    {
        var designCase = await RequiredAsync(designCaseId, false, token);
        await RequireProjectAsync(designCase.ProjectId);

        var now = DateTime.UtcNow;
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(value =>
                value.TenantId == TenantId && value.Id == designCase.ProjectId && !value.IsDeleted,
            token) ?? throw new UnauthorizedAccessException("You are not permitted to access the selected project.");

        var currentBoq = await db.ProjectBoqVersions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == project.Id && !value.IsDeleted
                            && value.VersionType == QuantitySurveyBoqVersionType.Approved
                            && value.Status == ProjectBoqVersionStatuses.Approved
                            && value.ApprovalStatus == ProjectBoqVersionStatuses.Approved
                            && value.PublishedAt.HasValue)
            .OrderByDescending(value => value.PublishedAt)
            .ThenByDescending(value => value.VersionNumber)
            .FirstOrDefaultAsync(token);

        var approvedEstimate = currentBoq is null
            ? null
            : await db.QuantitySurveyEstimateVersions.AsNoTracking()
                .Where(value => value.TenantId == TenantId && value.ProjectId == project.Id
                                && value.ProjectBoqVersionId == currentBoq.Id && !value.IsDeleted
                                && value.Status == QuantitySurveyEstimateStatuses.Approved
                                && value.ApprovalStatus == QuantitySurveyEstimateStatuses.Approved)
                .OrderByDescending(value => value.ApprovedAt)
                .ThenByDescending(value => value.VersionNumber)
                .FirstOrDefaultAsync(token);

        var budget = await db.ProjectBudgetRevisions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == project.Id && !value.IsDeleted
                            && value.Status == "Approved" && value.ApprovedAt.HasValue
                            && value.EffectiveDate <= now.Date)
            .OrderByDescending(value => value.EffectiveDate)
            .ThenByDescending(value => value.VersionNumber)
            .FirstOrDefaultAsync(token);

        var contract = project.ContractId.HasValue
            ? await db.Contracts.AsNoTracking().SingleOrDefaultAsync(value =>
                value.TenantId == TenantId && value.Id == project.ContractId.Value && !value.IsDeleted,
                token)
            : null;
        var contractor = contract is null
            ? null
            : await db.BusinessPartners.AsNoTracking().SingleOrDefaultAsync(value =>
                value.TenantId == TenantId && value.Id == contract.BusinessPartnerId && !value.IsDeleted,
                token);

        var requisitions = await db.PurchaseRequisitions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.ApprovedAt ?? value.RequisitionDate)
            .ToListAsync(token);
        var requisitionIds = requisitions.Select(value => value.Id).ToList();
        var orders = requisitionIds.Count == 0
            ? new List<PurchaseOrder>()
            : await db.PurchaseOrders.AsNoTracking()
                .Where(value => value.TenantId == TenantId && !value.IsDeleted
                                && value.SourceRequisitionId.HasValue
                                && requisitionIds.Contains(value.SourceRequisitionId.Value))
                .OrderByDescending(value => value.ApprovedAt ?? value.OrderDate)
                .ToListAsync(token);

        var approvedRequisition = requisitions.FirstOrDefault(value =>
            string.Equals(value.Status, "Approved", StringComparison.OrdinalIgnoreCase)
            && value.BudgetValidated && value.ApprovedAt.HasValue);
        var authorizedOrder = orders.FirstOrDefault(value =>
            value.BudgetValidated
            && value.Status is "Approved" or "Sent" or "Acknowledged" or "PartiallyReceived" or "Received");

        var approvedWorksheet = await db.QuantitySurveyValuationWorksheets.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == project.Id && !value.IsDeleted
                            && value.Status == QuantitySurveyValuationWorkflowStatuses.Approved
                            && value.ApprovalStatus == QuantitySurveyValuationWorkflowStatuses.Approved)
            .OrderByDescending(value => value.ApprovedAt)
            .FirstOrDefaultAsync(token);
        var currentVariations = await db.ProjectVariationOrders.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == project.Id && !value.IsDeleted
                            && value.Status != ProjectVariationOrderStatuses.Approved
                            && value.Status != ProjectVariationOrderStatuses.Implemented
                            && value.Status != ProjectVariationOrderStatuses.Closed
                            && value.Status != ProjectVariationOrderStatuses.Rejected)
            .OrderByDescending(value => value.RequestedDate)
            .ToListAsync(token);
        var approvedCertificate = await db.ProjectPaymentCertificates.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == project.Id && !value.IsDeleted
                            && (value.Status == ProjectPaymentCertificateStatuses.Approved
                                || value.Status == ProjectPaymentCertificateStatuses.Paid))
            .OrderByDescending(value => value.ApprovedAt ?? value.IssueDate)
            .FirstOrDefaultAsync(token);

        var ipcEvidenceIds = await db.ProjectCivilIpcEndorsements.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == project.Id && !value.IsDeleted
                            && value.Status == CivilEngineeringIpcEndorsementStatuses.Endorsed
                            && value.EvidenceDocumentRecordId.HasValue && value.EvidenceDocumentVersionId.HasValue)
            .Select(value => new { value.EvidenceDocumentRecordId, value.EvidenceDocumentVersionId })
            .ToListAsync(token);
        var certificateEvidenceVersionIds = approvedCertificate?.CentralDocumentVersionId is { } certificateVersionId
            ? new[] { certificateVersionId }
            : Array.Empty<Guid>();
        var currentEvidenceVersionIds = ipcEvidenceIds.Select(value => value.EvidenceDocumentVersionId!.Value)
            .Concat(certificateEvidenceVersionIds)
            .Distinct()
            .ToList();
        var currentEvidenceCount = currentEvidenceVersionIds.Count == 0
            ? 0
            : await db.CentralDocumentVersions.AsNoTracking()
                .Include(value => value.DocumentRecord)
                .Where(CentralDocumentEvidenceRules.CurrentPublished())
                .CountAsync(value => value.TenantId == TenantId && currentEvidenceVersionIds.Contains(value.Id), token);

        var contractActive = contract is not null && string.Equals(contract.Status, "Active", StringComparison.OrdinalIgnoreCase);
        var contractorActive = contractor is not null && contractor.IsActive && !contractor.IsBlacklisted
                               && string.Equals(contractor.RegistrationStatus, "Approved", StringComparison.OrdinalIgnoreCase);
        var gates = new List<CivilEngineeringCommercialReadinessGateDto>
        {
            Gate("QS_BOQ", "Current approved QS BoQ", currentBoq is not null,
                currentBoq is null ? "No approved, published BoQ is available for this project."
                    : $"BoQ v{currentBoq.VersionNumber} was published {currentBoq.PublishedAt:dd MMM yyyy}."),
            Gate("QS_ESTIMATE", "Approved QS cost plan", approvedEstimate is not null,
                approvedEstimate is null ? "No approved estimate is linked to the current approved BoQ."
                    : $"{approvedEstimate.Name} v{approvedEstimate.VersionNumber} is approved."),
            Gate("BUDGET", "Current Finance/Budget approval", budget is not null,
                budget is null ? "No effective approved project budget revision is available."
                    : $"{budget.RevisionName} v{budget.VersionNumber} is effective from {budget.EffectiveDate:dd MMM yyyy}."),
            Gate("CONTRACT", "Active Procurement contract", contractActive,
                contract is null ? "No canonical Procurement contract is linked to this project."
                    : contractActive ? $"{contract.ContractNumber} is Active." : $"{contract.ContractNumber} is currently {contract.Status}."),
            Gate("CONTRACTOR", "Active approved contractor", contractorActive,
                contractor is null ? "The linked contract has no available contractor record."
                    : contractorActive ? $"{contractor.PartnerName} is active and approved."
                        : $"{contractor.PartnerName} is not active and approved for execution."),
            Gate("PROCUREMENT", "Approved budget-validated project procurement", authorizedOrder is not null,
                authorizedOrder is null
                    ? approvedRequisition is null
                        ? "No approved, budget-validated purchase requisition is linked to this project."
                        : "An approved requisition exists, but no approved, budget-validated purchase order is linked to it."
                    : $"Purchase order {authorizedOrder.OrderNumber} is {authorizedOrder.Status} and budget validated."),
            Gate("VARIATION", "No unresolved commercial variation", currentVariations.Count == 0,
                currentVariations.Count == 0 ? "No draft, submitted, under-review, or pending-approval variation is open."
                    : $"{currentVariations.Count} commercial variation(s) remain unresolved."),
        };

        var links = new List<CivilEngineeringCommercialReadinessLinkDto>();
        if (currentBoq is not null)
            links.Add(Link("Quantity Survey", "BoQ version", currentBoq.Id, $"BoQ v{currentBoq.VersionNumber}", currentBoq.Status,
                $"Approved/published {currentBoq.PublishedAt:dd MMM yyyy}."));
        if (approvedEstimate is not null)
            links.Add(Link("Quantity Survey", "Estimate", approvedEstimate.Id, approvedEstimate.Name, approvedEstimate.Status,
                $"Version {approvedEstimate.VersionNumber}; total {approvedEstimate.CurrencyCodeSnapshot} {approvedEstimate.TotalAmount:N2}."));
        if (approvedWorksheet is not null)
            links.Add(Link("Quantity Survey", "Valuation worksheet", approvedWorksheet.Id,
                $"Valuation worksheet {approvedWorksheet.Id.ToString()[..8]}", approvedWorksheet.Status,
                $"Certified current value {approvedWorksheet.NetCurrentValue:N2}."));
        if (budget is not null)
            links.Add(Link("Finance/Budget", "Project budget revision", budget.Id, budget.RevisionName, budget.Status,
                $"v{budget.VersionNumber}; approved budget {budget.ApprovedBudget:N2}."));
        if (contract is not null)
            links.Add(Link("Procurement", "Contract", contract.Id, contract.ContractNumber, contract.Status,
                contract.ContractTitle));
        if (contractor is not null)
            links.Add(Link("Business Partners", "Contractor", contractor.Id, contractor.PartnerName, contractor.RegistrationStatus,
                contractor.IsBlacklisted ? "Blacklisted." : contractor.IsActive ? "Active." : "Inactive."));
        foreach (var requisition in requisitions.Take(10))
            links.Add(Link("Procurement", "Purchase requisition", requisition.Id, requisition.RequisitionNumber, requisition.Status,
                requisition.BudgetValidated ? "Budget validated." : "Budget not validated."));
        foreach (var order in orders.Take(10))
            links.Add(Link("Procurement", "Purchase order", order.Id, order.OrderNumber, order.Status,
                order.BudgetValidated ? "Budget validated." : "Budget not validated."));
        foreach (var variation in currentVariations.Take(10))
            links.Add(Link("Quantity Survey", "Open variation", variation.Id,
                variation.ReferenceNumber ?? variation.Title, variation.Status,
                "Resolve through the authoritative QS/contract/budget variation flow before execution."));
        if (approvedCertificate is not null)
            links.Add(Link("Quantity Survey / Finance", "Payment certificate", approvedCertificate.Id,
                approvedCertificate.CertificateNumber ?? approvedCertificate.Title, approvedCertificate.Status,
                $"AP handoff is {approvedCertificate.ApHandoffStatus}."));
        links.Add(Link("Central DMS", "Service-delivery evidence", null,
            "Current published evidence", currentEvidenceCount > 0 ? "Available" : "Not yet available",
            currentEvidenceCount > 0
                ? $"{currentEvidenceCount} current published IPC/certificate evidence document(s) are available."
                : "No current published IPC or payment-certificate evidence exists yet; this is expected before the first delivery/certificate."));

        return new CivilEngineeringCommercialReadinessDto
        {
            DesignCaseId = designCase.Id,
            ProjectId = project.Id,
            RevalidatedAt = now,
            ReadyForExecution = gates.All(value => value.IsSatisfied),
            Gates = gates,
            Links = links
        };
    }

    private static CivilEngineeringCommercialReadinessGateDto Gate(string code, string title, bool isSatisfied, string detail) => new()
    {
        Code = code,
        Title = title,
        IsSatisfied = isSatisfied,
        Detail = detail
    };

    private static CivilEngineeringCommercialReadinessLinkDto Link(
        string owner,
        string recordType,
        Guid? recordId,
        string reference,
        string status,
        string detail) => new()
    {
        Owner = owner,
        RecordType = recordType,
        RecordId = recordId,
        Reference = reference,
        Status = status,
        Detail = detail
    };
}
