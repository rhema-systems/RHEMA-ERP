using ErpSystem.Core.Interfaces.QuantitySurvey;

namespace ErpSystem.Core.Services.QuantitySurvey;

public sealed record QuantitySurveyValuationSourceLine(
    Guid ProjectBoqVersionLineId, Guid BoqLineKey, int Sequence, string Label, string Description,
    string? UnitOfMeasure, string Currency, decimal BoqQuantity, decimal UnitRate,
    decimal MeasuredToDateQuantity, decimal PreviouslyCertifiedQuantity,
    decimal PreviouslyCertifiedValue, decimal PreviousRetentionValue);

public sealed record QuantitySurveyValuationLineCalculation(
    QuantitySurveyValuationSourceLine Source, decimal CurrentClaimedQuantity, decimal CurrentCertifiedQuantity,
    decimal DisputedQuantity, decimal MeasuredToDateValue, decimal CurrentClaimedValue, decimal CurrentCertifiedValue,
    decimal CurrentPeriodCertifiedValue, decimal DisputedValue, decimal RetentionToDateValue,
    decimal CurrentRetentionValue, decimal NetCurrentValue, string? ReviewNote);

public sealed record QuantitySurveyValuationTotals(
    decimal MeasuredToDateValue, decimal PreviouslyCertifiedValue, decimal CurrentClaimedValue,
    decimal CurrentCertifiedValue, decimal CurrentPeriodCertifiedValue, decimal DisputedValue,
    decimal RetentionToDateValue, decimal CurrentRetentionValue, decimal NetCurrentValue);

public static class QuantitySurveyValuationWorksheetRules
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["Draft"] = new HashSet<string>(["Draft", "ContractorSubmitted", "QsVetted"], StringComparer.Ordinal),
            ["ContractorSubmitted"] = new HashSet<string>(["ContractorSubmitted", "UnderQsReview", "QsVetted"], StringComparer.Ordinal),
            ["UnderQsReview"] = new HashSet<string>(["UnderQsReview", "QsVetted"], StringComparer.Ordinal),
            ["QsVetted"] = new HashSet<string>(["QsVetted", "ConsultantEndorsed", "PendingApproval"], StringComparer.Ordinal),
            ["ConsultantEndorsed"] = new HashSet<string>(["ConsultantEndorsed", "PendingApproval"], StringComparer.Ordinal),
            ["PendingApproval"] = new HashSet<string>(["PendingApproval", "Approved", "Rejected"], StringComparer.Ordinal),
            ["Approved"] = new HashSet<string>(["Approved"], StringComparer.Ordinal),
            ["Rejected"] = new HashSet<string>(["Rejected"], StringComparer.Ordinal)
        };

    public static void RequireTransition(string currentStatus, string nextStatus)
    {
        if (!AllowedTransitions.TryGetValue(currentStatus, out var allowed) || !allowed.Contains(nextStatus))
            throw Validation($"Interim valuation cannot move from {currentStatus} to {nextStatus}.");
    }

    public static void RequireIndependentApprover(
        Guid approverId, Guid preparedById, Guid? contractorSubmittedById,
        Guid? qsVettedById, Guid? consultantEndorsedById)
    {
        if (approverId == preparedById || approverId == contractorSubmittedById ||
            approverId == qsVettedById || approverId == consultantEndorsedById)
            throw Validation("Maker-checker control prevents a preparer, submitter, QS reviewer, or endorser from deciding this valuation.");
    }

    public static bool IsCertificateReady(
        string status, string approvalStatus, bool contractorSubmissionRequired,
        Guid? contractorSubmittedById, bool consultantEndorsementRequired,
        Guid? consultantEndorsedById, bool supportingEvidenceRequired, int evidenceCount,
        Guid? workflowInstanceId, Guid? approvedById)
        => status == "Approved" && approvalStatus == "Approved" && workflowInstanceId.HasValue &&
           approvedById.HasValue && (!contractorSubmissionRequired || contractorSubmittedById.HasValue) &&
           (!consultantEndorsementRequired || consultantEndorsedById.HasValue) &&
           (!supportingEvidenceRequired || evidenceCount > 0);

    public static QuantitySurveyValuationLineCalculation Calculate(
        QuantitySurveyValuationSourceLine source, decimal claimedToDate, decimal certifiedToDate,
        decimal retentionPercentage, string? reviewNote)
    {
        claimedToDate = Quantity(claimedToDate);
        certifiedToDate = Quantity(certifiedToDate);
        retentionPercentage = decimal.Round(retentionPercentage, 4, MidpointRounding.AwayFromZero);
        if (retentionPercentage is < 0 or > 100)
            throw Validation("Retention percentage must be between 0 and 100.");
        if (claimedToDate > source.MeasuredToDateQuantity)
            throw Validation($"Claimed quantity for {source.Label} cannot exceed the recorded measurement-to-date quantity.");
        if (certifiedToDate > claimedToDate)
            throw Validation($"Certified quantity for {source.Label} cannot exceed the claimed quantity.");
        if (certifiedToDate < source.PreviouslyCertifiedQuantity)
            throw Validation($"Certified quantity for {source.Label} cannot be less than its previously certified quantity.");

        var note = string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote.Trim();
        var disputedQuantity = Quantity(claimedToDate - certifiedToDate);
        if (disputedQuantity > 0 && string.IsNullOrWhiteSpace(note))
            throw Validation($"Enter a review note for the disputed quantity on {source.Label}.");
        if (note?.Length > 1000) throw Validation("A line review note cannot exceed 1000 characters.");

        var measuredValue = Money(source.MeasuredToDateQuantity * source.UnitRate);
        var claimedValue = Money(claimedToDate * source.UnitRate);
        var certifiedValue = Money(certifiedToDate * source.UnitRate);
        var currentPeriod = Money(certifiedValue - source.PreviouslyCertifiedValue);
        var disputedValue = Money(claimedValue - certifiedValue);
        var retentionToDate = Money(certifiedValue * retentionPercentage / 100m);
        var currentRetention = Money(retentionToDate - source.PreviousRetentionValue);
        if (currentPeriod < 0 || currentRetention < 0)
            throw Validation($"Prior certification or retention for {source.Label} is inconsistent with this worksheet.");
        return new(source, claimedToDate, certifiedToDate, disputedQuantity, measuredValue,
            claimedValue, certifiedValue, currentPeriod, disputedValue, retentionToDate,
            currentRetention, Money(currentPeriod - currentRetention), note);
    }

    public static QuantitySurveyValuationTotals Total(IEnumerable<QuantitySurveyValuationLineCalculation> lines)
    {
        var values = lines.ToList();
        if (values.Count == 0) throw Validation("The selected approved BoQ contains no valuation lines.");
        return new(
            Money(values.Sum(value => value.MeasuredToDateValue)),
            Money(values.Sum(value => value.Source.PreviouslyCertifiedValue)),
            Money(values.Sum(value => value.CurrentClaimedValue)),
            Money(values.Sum(value => value.CurrentCertifiedValue)),
            Money(values.Sum(value => value.CurrentPeriodCertifiedValue)),
            Money(values.Sum(value => value.DisputedValue)),
            Money(values.Sum(value => value.RetentionToDateValue)),
            Money(values.Sum(value => value.CurrentRetentionValue)),
            Money(values.Sum(value => value.NetCurrentValue)));
    }

    private static decimal Quantity(decimal value)
    {
        var result = decimal.Round(value, 4, MidpointRounding.AwayFromZero);
        if (result < 0) throw Validation("Valuation quantities cannot be negative.");
        return result;
    }
    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static QuantitySurveyValuationWorksheetValidationException Validation(string message) => new(message);
}
