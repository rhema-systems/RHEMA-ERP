using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    private async Task<ProjectFinalAccountComputation> BuildProjectFinalAccountDerivationAsync(
        Project project,
        Guid? requestedContractId,
        string? preferredCurrency = null,
        DateTime? settlementDate = null)
    {
        var contract = await ValidateProjectCommercialContractAsync(requestedContractId ?? project.ContractId);
        var effectiveContractId = contract?.Id;
        var packages = (await GetProjectPackageEntitiesAsync(project.Id)).ToList();
        var packageContractLookup = packages
            .Where(x => x.ContractId.HasValue)
            .ToDictionary(x => x.Id, x => x.ContractId!.Value);

        var variationOrders = (await GetProjectVariationOrderEntitiesAsync(project.Id))
            .Where(x => MatchesFinalAccountScope(effectiveContractId, x.ContractId, x.ProjectPackageId, packageContractLookup))
            .ToList();
        var interimValuations = (await GetProjectInterimValuationEntitiesAsync(project.Id))
            .Where(x => MatchesFinalAccountScope(effectiveContractId, x.ContractId, x.ProjectPackageId, packageContractLookup))
            .ToList();
        var paymentCertificates = (await GetProjectPaymentCertificateEntitiesAsync(project.Id))
            .Where(x => MatchesFinalAccountScope(effectiveContractId, x.ContractId, x.ProjectPackageId, packageContractLookup))
            .ToList();

        var approvedVariationAmount = decimal.Round(variationOrders
            .Where(IsProjectVariationOrderApprovedForFinalAccount)
            .Sum(x => x.ApprovedAmount ?? x.EstimatedAmount ?? 0m), 2);
        var claimedAmount = decimal.Round(interimValuations
            .Where(IsProjectInterimValuationClaimStatus)
            .Sum(x => x.NetValuationAmount), 2);
        var certifiedCertificates = paymentCertificates
            .Where(IsProjectPaymentCertificateCertifiedStatus)
            .ToList();
        var certifiedToDate = decimal.Round(certifiedCertificates.Sum(x => x.NetCertifiedAmount), 2);
        var retentionHeldAmount = decimal.Round(certifiedCertificates.Sum(x => x.RetentionHeldAmount), 2);
        var retentionReleasedAmount = decimal.Round(certifiedCertificates.Sum(x => x.RetentionReleasedAmount), 2);
        var deductionAmount = decimal.Round(certifiedCertificates.Sum(x => x.OtherDeductionsAmount), 2);
        var claimAmount = decimal.Round(Math.Max(claimedAmount - certifiedToDate, 0m), 2);
        var adjustmentAmount = decimal.Round(retentionReleasedAmount - retentionHeldAmount, 2);
        var originalContractValue = decimal.Round(contract?.ContractValue ?? 0m, 2);
        var finalAccountValue = decimal.Round(originalContractValue + approvedVariationAmount + claimAmount + adjustmentAmount - deductionAmount, 2);
        var finalPaymentAmount = decimal.Round(finalAccountValue - certifiedToDate, 2);
        var currencyCode = await ResolveProjectCurrencyAsync(preferredCurrency ?? contract?.Currency ?? await GetProjectBaseCurrencyCodeAsync(project));

        return new ProjectFinalAccountComputation(
            effectiveContractId,
            originalContractValue,
            approvedVariationAmount,
            claimAmount,
            deductionAmount,
            adjustmentAmount,
            certifiedToDate,
            retentionHeldAmount,
            retentionReleasedAmount,
            finalAccountValue,
            finalPaymentAmount,
            currencyCode,
            settlementDate ?? DateTime.UtcNow.Date);
    }

    private async Task SyncProjectFinalPaymentBillingStepAsync(Project project, ProjectFinalAccountComputation computation)
    {
        var scheduleRepo = _unitOfWork.Repository<ProjectBillingSchedule>();
        var invoiceRequestRepo = _unitOfWork.Repository<ProjectInvoiceRequest>();

        var existingSchedules = (await scheduleRepo.FindAsync(x =>
                x.ProjectId == project.Id
                && x.TenantId == _currentUserProvider.TenantId
                && (x.BillingType == "FinalPayment" || x.Name == "Final Account Settlement")))
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
        var schedule = existingSchedules.FirstOrDefault();

        var existingInvoiceRequests = schedule == null
            ? new List<ProjectInvoiceRequest>()
            : (await invoiceRequestRepo.FindAsync(x =>
                    x.ProjectId == project.Id
                    && x.TenantId == _currentUserProvider.TenantId
                    && x.BillingScheduleId == schedule.Id))
                .ToList();
        var hasLockedInvoiceWorkflow = existingInvoiceRequests.Any(x =>
            !string.Equals(x.Status, "Draft", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(x.Status, "Rejected", StringComparison.OrdinalIgnoreCase));
        if (schedule != null
            && (string.Equals(schedule.Status, "Invoiced", StringComparison.OrdinalIgnoreCase) || hasLockedInvoiceWorkflow))
        {
            return;
        }

        if (schedule == null)
        {
            schedule = new ProjectBillingSchedule
            {
                TenantId = _currentUserProvider.TenantId,
                ProjectId = project.Id,
                ContractId = computation.ContractId ?? project.ContractId,
                Name = "Final Account Settlement",
                BillingType = "FinalPayment",
                Amount = computation.FinalPaymentAmount,
                BillingDate = computation.SettlementDate,
                Status = "Draft",
                Description = "Auto-synced from the project final account.",
                IsBillable = computation.FinalPaymentAmount > 0m,
                CreatedBy = _currentUserProvider.Username,
                CreatedById = _currentUserProvider.UserId
            };

            await scheduleRepo.AddAsync(schedule);
        }
        else
        {
            schedule.ContractId = computation.ContractId ?? project.ContractId;
            schedule.Name = "Final Account Settlement";
            schedule.BillingType = "FinalPayment";
            schedule.Amount = computation.FinalPaymentAmount;
            schedule.BillingDate = computation.SettlementDate;
            schedule.Description = "Auto-synced from the project final account.";
            schedule.IsBillable = computation.FinalPaymentAmount > 0m;
            schedule.UpdatedBy = _currentUserProvider.Username;
            schedule.LastModifiedById = _currentUserProvider.UserId;
            await scheduleRepo.UpdateAsync(schedule);
        }

        var editableInvoiceRequests = existingInvoiceRequests
            .Where(x => string.Equals(x.Status, "Draft", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var invoiceRequest in editableInvoiceRequests)
        {
            invoiceRequest.ContractId = computation.ContractId ?? project.ContractId;
            invoiceRequest.RequestedAmount = computation.FinalPaymentAmount;
            invoiceRequest.Currency = computation.CurrencyCode;
            invoiceRequest.Notes = AppendDecisionNote(invoiceRequest.Notes, "FinalAccountSync", "Updated from project final account.");
            invoiceRequest.UpdatedBy = _currentUserProvider.Username;
            invoiceRequest.LastModifiedById = _currentUserProvider.UserId;
            await invoiceRequestRepo.UpdateAsync(invoiceRequest);
        }
    }

    private static bool MatchesFinalAccountScope(
        Guid? effectiveContractId,
        Guid? documentContractId,
        Guid? documentPackageId,
        IReadOnlyDictionary<Guid, Guid> packageContractLookup)
    {
        if (!effectiveContractId.HasValue)
        {
            return true;
        }

        if (documentContractId.HasValue)
        {
            return documentContractId == effectiveContractId;
        }

        if (documentPackageId.HasValue
            && packageContractLookup.TryGetValue(documentPackageId.Value, out var packageContractId))
        {
            return packageContractId == effectiveContractId;
        }

        return true;
    }

    private static bool IsProjectVariationOrderApprovedForFinalAccount(ProjectVariationOrder variationOrder)
        => string.Equals(variationOrder.Status, ProjectVariationOrderStatuses.Approved, StringComparison.OrdinalIgnoreCase)
           || string.Equals(variationOrder.Status, ProjectVariationOrderStatuses.Implemented, StringComparison.OrdinalIgnoreCase)
           || string.Equals(variationOrder.Status, ProjectVariationOrderStatuses.Closed, StringComparison.OrdinalIgnoreCase);

    private static bool IsProjectInterimValuationClaimStatus(ProjectInterimValuation interimValuation)
        => string.Equals(interimValuation.Status, ProjectInterimValuationStatuses.Submitted, StringComparison.OrdinalIgnoreCase)
           || string.Equals(interimValuation.Status, ProjectInterimValuationStatuses.UnderReview, StringComparison.OrdinalIgnoreCase)
           || string.Equals(interimValuation.Status, ProjectInterimValuationStatuses.Certified, StringComparison.OrdinalIgnoreCase)
           || string.Equals(interimValuation.Status, ProjectInterimValuationStatuses.Paid, StringComparison.OrdinalIgnoreCase);

    private sealed record ProjectFinalAccountComputation(
        Guid? ContractId,
        decimal OriginalContractValue,
        decimal ApprovedVariationAmount,
        decimal ClaimAmount,
        decimal DeductionAmount,
        decimal AdjustmentAmount,
        decimal CertifiedToDate,
        decimal RetentionHeldAmount,
        decimal RetentionReleasedAmount,
        decimal FinalAccountValue,
        decimal FinalPaymentAmount,
        string CurrencyCode,
        DateTime SettlementDate);
}
