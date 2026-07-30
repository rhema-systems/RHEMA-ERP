using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Services.Procurement;

public sealed record ProcurementContractOperationsMilestoneInput(
    Guid Id,
    string Reference,
    string Status,
    DateTime? PlannedDate,
    decimal PaymentAmount);

public sealed record ProcurementContractOperationsDeliveryInput(
    Guid Id,
    string Reference,
    string Status,
    DateTime? PromisedDate,
    decimal Amount);

public sealed record ProcurementContractOperationsInvoiceInput(
    Guid Id,
    string Reference,
    string Status,
    DateTime? DueDate,
    decimal OutstandingAmount);

public sealed record ProcurementContractOperationsRuleInput(
    Guid ContractId,
    string ContractNumber,
    string ContractStatus,
    decimal ContractValue,
    string Currency,
    DateTime? EndDate,
    string? PenaltyClause,
    decimal RetentionHeldAmount,
    decimal RetentionReleasedAmount,
    decimal? SupplierPerformanceScore,
    decimal? SupplierPerformanceTarget,
    bool SupplierPerformanceBreached,
    decimal? SupplierRiskScore,
    bool SupplierRiskBreached,
    bool CurrencyConflict,
    IReadOnlyList<ProcurementContractOperationsMilestoneInput> Milestones,
    IReadOnlyList<ProcurementContractOperationsDeliveryInput> Deliveries,
    IReadOnlyList<ProcurementContractOperationsInvoiceInput> Invoices);

public static class ProcurementContractOperationsRules
{
    public static IReadOnlyList<ProcurementContractOperationsPromptDto> Evaluate(
        ProcurementContractOperationsRuleInput input,
        DateTime nowUtc)
    {
        var now = nowUtc.Kind == DateTimeKind.Utc
            ? nowUtc
            : nowUtc.ToUniversalTime();
        var prompts = new List<ProcurementContractOperationsPromptDto>();
        var hasDelay = false;

        foreach (var milestone in input.Milestones
                     .Where(item => IsOpenMilestone(item.Status) &&
                                    item.PlannedDate.HasValue &&
                                    item.PlannedDate.Value.Date < now.Date)
                     .OrderBy(item => item.PlannedDate))
        {
            var days = DaysOverdue(milestone.PlannedDate!.Value, now);
            hasDelay = true;
            prompts.Add(Prompt(
                $"MILESTONE-{milestone.Id:N}",
                "MilestoneDelay",
                DelaySeverity(days),
                $"Milestone overdue: {milestone.Reference}",
                $"{milestone.Reference} is {days} day(s) overdue.",
                "Validate delay evidence, obtain an independent contract decision, and process any penalty through the controlled amendment or Finance workflow.",
                milestone.Id,
                milestone.Reference,
                milestone.PlannedDate,
                days,
                input.PenaltyClause,
                milestone.PaymentAmount));
        }

        foreach (var delivery in input.Deliveries
                     .Where(item => IsOpenDelivery(item.Status) &&
                                    item.PromisedDate.HasValue &&
                                    item.PromisedDate.Value.Date < now.Date)
                     .OrderBy(item => item.PromisedDate))
        {
            var days = DaysOverdue(delivery.PromisedDate!.Value, now);
            hasDelay = true;
            prompts.Add(Prompt(
                $"DELIVERY-{delivery.Id:N}",
                "DeliveryDelay",
                DelaySeverity(days),
                $"Delivery overdue: {delivery.Reference}",
                $"{delivery.Reference} is {days} day(s) beyond its promised date.",
                "Reconcile delivery evidence and route any penalty or extension through the existing controlled contract workflow.",
                delivery.Id,
                delivery.Reference,
                delivery.PromisedDate,
                days,
                input.PenaltyClause,
                delivery.Amount));
        }

        foreach (var invoice in input.Invoices
                     .Where(item => item.OutstandingAmount > 0m &&
                                    item.DueDate.HasValue &&
                                    item.DueDate.Value.Date < now.Date &&
                                    !IsVoidedInvoice(item.Status))
                     .OrderBy(item => item.DueDate))
        {
            var days = DaysOverdue(invoice.DueDate!.Value, now);
            prompts.Add(new ProcurementContractOperationsPromptDto
            {
                Key = $"PAYMENT-{invoice.Id:N}",
                Type = "PaymentOverdue",
                Severity = DelaySeverity(days),
                Title = $"Payment overdue: {invoice.Reference}",
                Message = $"{input.Currency} {invoice.OutstandingAmount:N2} remains outstanding for {days} day(s).",
                RecommendedAction = "Review the approved invoice and payment workflow; this dashboard does not post, approve, or release funds.",
                SourceId = invoice.Id,
                SourceReference = invoice.Reference,
                DueAtUtc = invoice.DueDate,
                DaysOverdue = days,
                CalculationBasis = "Outstanding amount is read from the tenant-safe Accounts Payable invoice.",
                RequiresIndependentApproval = true,
                AmountAutoPosted = false
            });
        }

        if (hasDelay && string.IsNullOrWhiteSpace(input.PenaltyClause))
        {
            prompts.Add(new ProcurementContractOperationsPromptDto
            {
                Key = $"PENALTY-TERMS-{input.ContractId:N}",
                Type = "PenaltyConfiguration",
                Severity = "High",
                Title = "Delay detected without penalty terms",
                Message = "A delay exists, but the contract has no recorded penalty clause from which a monetary consequence can be derived.",
                RecommendedAction = "Escalate for Legal and contract-management review; do not calculate or post a penalty outside the controlled workflow.",
                SourceId = input.ContractId,
                SourceReference = input.ContractNumber,
                CalculationBasis = "No configured contract penalty clause.",
                RequiresIndependentApproval = true,
                AmountAutoPosted = false
            });
        }

        if (input.EndDate.HasValue && IsOperational(input.ContractStatus))
        {
            var daysToExpiry = (input.EndDate.Value.Date - now.Date).Days;
            if (daysToExpiry <= 90)
            {
                var expired = daysToExpiry < 0;
                prompts.Add(new ProcurementContractOperationsPromptDto
                {
                    Key = $"EXPIRY-{input.ContractId:N}",
                    Type = expired ? "ExpiredContract" : "Renewal",
                    Severity = expired || daysToExpiry <= 14 ? "Critical" :
                        daysToExpiry <= 30 ? "High" : "Medium",
                    Title = expired ? "Contract term has expired" : "Contract renewal window",
                    Message = expired
                        ? $"The contract end date passed {-daysToExpiry} day(s) ago."
                        : $"The contract reaches its end date in {daysToExpiry} day(s).",
                    RecommendedAction = expired
                        ? "Stop uncontrolled commitments and route extension, completion, or closure through the existing contract controls."
                        : "Begin the renewal, completion, or retender decision with current performance, risk, and spend evidence.",
                    SourceId = input.ContractId,
                    SourceReference = input.ContractNumber,
                    DueAtUtc = input.EndDate,
                    DaysOverdue = Math.Max(0, -daysToExpiry),
                    CalculationBasis = "Contract end date.",
                    RequiresIndependentApproval = true,
                    AmountAutoPosted = false
                });
            }
        }

        var unreleasedRetention = Math.Max(0m,
            input.RetentionHeldAmount - input.RetentionReleasedAmount);
        if (unreleasedRetention > 0m &&
            (input.ContractStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
             input.EndDate.HasValue && input.EndDate.Value.Date < now.Date))
        {
            prompts.Add(new ProcurementContractOperationsPromptDto
            {
                Key = $"RETENTION-{input.ContractId:N}",
                Type = "RetentionReview",
                Severity = "High",
                Title = "Retention release review required",
                Message = $"{input.Currency} {unreleasedRetention:N2} remains held after completion or term expiry.",
                RecommendedAction = "Verify acceptance, defects, warranty, certificate, and Finance evidence before an independently approved release.",
                SourceId = input.ContractId,
                SourceReference = input.ContractNumber,
                EstimatedPenaltyAmount = null,
                CalculationBasis = "Issued/approved project payment certificates: retention held less retention released.",
                RequiresIndependentApproval = true,
                AmountAutoPosted = false
            });
        }

        if (input.SupplierPerformanceBreached)
        {
            prompts.Add(new ProcurementContractOperationsPromptDto
            {
                Key = $"PERFORMANCE-{input.ContractId:N}",
                Type = "SlaKpiBreach",
                Severity = "High",
                Title = "Supplier SLA/KPI threshold breached",
                Message = input.SupplierPerformanceScore.HasValue
                    ? $"Latest supplier score is {input.SupplierPerformanceScore:N2} against target {input.SupplierPerformanceTarget:N2}."
                    : "The latest governed supplier scorecard reports a minimum-score breach.",
                RecommendedAction = "Review the governed scorecard and route corrective action or a contractual consequence through the existing workflow.",
                SourceId = input.ContractId,
                SourceReference = input.ContractNumber,
                CalculationBasis = "Latest governed supplier-performance scorecard.",
                RequiresIndependentApproval = true,
                AmountAutoPosted = false
            });
        }

        if (input.SupplierRiskBreached)
        {
            prompts.Add(new ProcurementContractOperationsPromptDto
            {
                Key = $"RISK-{input.ContractId:N}",
                Type = "SupplierRisk",
                Severity = "Critical",
                Title = "Supplier risk threshold breached",
                Message = input.SupplierRiskScore.HasValue
                    ? $"Latest supplier risk score is {input.SupplierRiskScore:N2} and is outside the governed tolerance."
                    : "The latest governed supplier assessment reports a risk threshold breach.",
                RecommendedAction = "Escalate through the existing supplier-risk and contract decision controls before renewal, amendment, or further commitment.",
                SourceId = input.ContractId,
                SourceReference = input.ContractNumber,
                CalculationBasis = "Latest governed supplier-risk assessment.",
                RequiresIndependentApproval = true,
                AmountAutoPosted = false
            });
        }

        if (input.CurrencyConflict)
        {
            prompts.Add(new ProcurementContractOperationsPromptDto
            {
                Key = $"CURRENCY-{input.ContractId:N}",
                Type = "CurrencyMismatch",
                Severity = "Critical",
                Title = "Contract operations currency mismatch",
                Message = "One or more linked purchase orders, invoices, or certificates use a different currency, so monetary totals exclude those records.",
                RecommendedAction = "Reconcile source lineage and approved amendments before relying on spend, payment, retention, or penalty totals.",
                SourceId = input.ContractId,
                SourceReference = input.ContractNumber,
                CalculationBasis = $"Contract currency is {input.Currency}.",
                RequiresIndependentApproval = true,
                AmountAutoPosted = false
            });
        }

        return prompts
            .OrderBy(item => SeverityRank(item.Severity))
            .ThenBy(item => item.DueAtUtc)
            .ThenBy(item => item.Key)
            .ToList();
    }

    public static string RenewalStatus(DateTime? endDate, string status, DateTime nowUtc)
    {
        if (!endDate.HasValue || !IsOperational(status)) return "NotApplicable";
        var days = (endDate.Value.Date - nowUtc.Date).Days;
        return days < 0 ? "Expired" :
            days <= 30 ? "Due" :
            days <= 90 ? "Upcoming" : "Current";
    }

    public static string OverallRisk(
        IReadOnlyCollection<ProcurementContractOperationsPromptDto> prompts)
    {
        if (prompts.Any(item => item.Severity == "Critical")) return "Critical";
        if (prompts.Any(item => item.Severity == "High")) return "High";
        if (prompts.Any(item => item.Severity == "Medium")) return "Medium";
        return "Low";
    }

    private static ProcurementContractOperationsPromptDto Prompt(
        string key,
        string type,
        string severity,
        string title,
        string message,
        string action,
        Guid sourceId,
        string sourceReference,
        DateTime? dueAt,
        int days,
        string? penaltyClause,
        decimal sourceAmount) => new()
        {
            Key = key,
            Type = type,
            Severity = severity,
            Title = title,
            Message = message,
            RecommendedAction = action,
            SourceId = sourceId,
            SourceReference = sourceReference,
            DueAtUtc = dueAt,
            DaysOverdue = days,
            EstimatedPenaltyAmount = null,
            CalculationBasis = string.IsNullOrWhiteSpace(penaltyClause)
                ? "No penalty clause is recorded; no monetary estimate is produced."
                : $"Penalty clause requires controlled human interpretation against source amount {sourceAmount:N2}; no amount is automatically posted.",
            RequiresIndependentApproval = true,
            AmountAutoPosted = false
        };

    private static bool IsOpenMilestone(string status) =>
        !status.Equals("Completed", StringComparison.OrdinalIgnoreCase) &&
        !status.Equals("Invoiced", StringComparison.OrdinalIgnoreCase) &&
        !status.Equals("Paid", StringComparison.OrdinalIgnoreCase) &&
        !status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);

    private static bool IsOpenDelivery(string status) =>
        !status.Equals("Received", StringComparison.OrdinalIgnoreCase) &&
        !status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) &&
        !status.Equals("Closed", StringComparison.OrdinalIgnoreCase);

    private static bool IsVoidedInvoice(string status) =>
        status.Equals("Voided", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("Rejected", StringComparison.OrdinalIgnoreCase);

    private static bool IsOperational(string status) =>
        status.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("Suspended", StringComparison.OrdinalIgnoreCase);

    private static int DaysOverdue(DateTime dueAt, DateTime nowUtc) =>
        Math.Max(0, (nowUtc.Date - dueAt.Date).Days);

    private static string DelaySeverity(int days) =>
        days >= 30 ? "Critical" : days >= 7 ? "High" : "Medium";

    private static int SeverityRank(string severity) => severity switch
    {
        "Critical" => 0,
        "High" => 1,
        "Medium" => 2,
        _ => 3
    };
}
