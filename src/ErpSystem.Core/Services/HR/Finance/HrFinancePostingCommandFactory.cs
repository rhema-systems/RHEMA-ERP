using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR.Finance;

/// <summary>
/// Builds the posting command for each catalogued HR money event from the source entity as it
/// stands. The area service calls the same builder the register's retry calls, so what a trigger
/// posts and what a back-fill posts can never differ.
/// </summary>
/// <remarks>
/// <para>Pure functions over entities — no I/O, no tenant, no clock — so the contract tests can
/// hand them a claim and assert the lines. Amounts are the entity's own derived figures
/// (<c>AmountApproved</c>, <c>TotalApproved</c>, <c>AdvanceDeducted</c>, <c>NetPayable</c>); this
/// class never re-derives money that an area already computes, which is the rule the travel
/// slice-4 hardening established ("one formula, in one place").</para>
///
/// <para>⚠ Travel claim totals are already functional-currency sums (each line's
/// <c>AmountBaseCurrency</c> is valued through Finance's rate when the line is written), so a
/// claim command carries no transaction currency even when the claim's own <c>CurrencyCode</c> is
/// foreign. An advance is different: its amounts are in the advance currency, so the command
/// carries that currency and the adapter converts.</para>
/// </remarks>
public static class HrFinancePostingCommandFactory
{
    // ── Medical ──────────────────────────────────────────────────────────────────────────────

    public static HrFinancePostingCommand MedicalClaimApproved(MedicalExpenseClaim claim)
    {
        var amount = claim.AmountApproved ?? 0m;
        var reference = claim.ClaimNumber;
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.MedicalClaimApproved,
            SourceDocumentId = claim.Id,
            SourceReference = reference,
            EmployeeId = claim.EmployeeId,
            SourceDate = claim.ClaimDate,
            Description = $"Medical claim {reference} approved ({claim.ExpenseType})",
            SkipReason = amount <= 0m ? "The approved amount is zero; there is nothing to recognise." : null,
            Lines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.MedicalExpense, true, amount, $"Medical claim {reference} — expense"),
                new HrFinancePostingLine(HrFinanceAccountRole.StaffClaimsPayable, false, amount, $"Medical claim {reference} — payable to employee")
            ]
        };
    }

    public static HrFinancePostingCommand MedicalClaimPaid(MedicalExpenseClaim claim)
    {
        var amount = claim.AmountApproved ?? 0m;
        var reference = claim.ClaimNumber;
        var method = claim.PaymentMethod?.ToString() ?? "payment";
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.MedicalClaimPaid,
            SourceDocumentId = claim.Id,
            SourceReference = reference,
            EmployeeId = claim.EmployeeId,
            SourceDate = claim.PaymentDate,
            Description = $"Medical claim {reference} paid by {method}" +
                          (string.IsNullOrWhiteSpace(claim.PaymentReference) ? string.Empty : $" ref {claim.PaymentReference}"),
            SkipReason = amount <= 0m
                ? "The approved amount is zero; there is nothing to settle."
                : claim.PaymentMethod == PaymentMethod.SalaryDeduction
                    ? "Settled through payroll (salary deduction): payroll's own journal clears the staff claims payable; HR posts nothing."
                    : null,
            Lines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.StaffClaimsPayable, true, amount, $"Medical claim {reference} — payable settled"),
                new HrFinancePostingLine(HrFinanceAccountRole.StaffPaymentsClearing, false, amount, $"Medical claim {reference} — paid by {method}")
            ]
        };
    }

    // ── Staff travel ─────────────────────────────────────────────────────────────────────────

    public static HrFinancePostingCommand TravelClaimApproved(StaffTravelExpenseClaim claim)
    {
        var amount = claim.TotalApproved;
        var reference = claim.ClaimNumber;
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.TravelClaimApproved,
            SourceDocumentId = claim.Id,
            SourceReference = reference,
            EmployeeId = claim.EmployeeId,
            SourceDate = claim.FinanceReviewedAt ?? claim.SubmittedAt,
            Description = $"Travel expense claim {reference} approved ({claim.ClaimType})",
            SkipReason = amount <= 0m ? "The approved total is zero; there is nothing to recognise." : null,
            Lines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.TravelExpense, true, amount, $"Travel claim {reference} — expense"),
                new HrFinancePostingLine(HrFinanceAccountRole.StaffClaimsPayable, false, amount, $"Travel claim {reference} — payable to employee")
            ]
        };
    }

    /// <summary>
    /// Settlement of a travel claim. Three legs at most: the payable is cleared in full; the part
    /// recovered from an advance credits the receivable; the rest credits the clearing account.
    /// Paid by payroll offset, only the advance recovery posts (payroll clears the payable itself).
    /// </summary>
    public static HrFinancePostingCommand TravelClaimPaid(StaffTravelExpenseClaim claim)
    {
        var reference = claim.ClaimNumber;
        var payable = claim.TotalApproved;
        var recovered = Math.Min(Math.Max(claim.AdvanceDeducted, 0m), payable);
        var net = payable - recovered;
        var viaPayroll = claim.PaymentMethod == TravelPaymentMethod.PayrollOffset;
        var method = claim.PaymentMethod?.ToString() ?? "payment";

        var lines = new List<HrFinancePostingLine>();
        string? skip = null;
        if (payable <= 0m)
        {
            skip = "The approved total is zero; there is nothing to settle.";
        }
        else if (viaPayroll)
        {
            if (recovered > 0m)
            {
                lines.Add(new HrFinancePostingLine(HrFinanceAccountRole.StaffClaimsPayable, true, recovered, $"Travel claim {reference} — payable settled against advance"));
                lines.Add(new HrFinancePostingLine(HrFinanceAccountRole.StaffAdvancesReceivable, false, recovered, $"Travel claim {reference} — advance recovered"));
            }
            else
            {
                skip = "Settled through payroll (payroll offset) with no advance to recover: payroll's own journal clears the staff claims payable; HR posts nothing.";
            }
        }
        else
        {
            lines.Add(new HrFinancePostingLine(HrFinanceAccountRole.StaffClaimsPayable, true, payable, $"Travel claim {reference} — payable settled"));
            if (recovered > 0m)
                lines.Add(new HrFinancePostingLine(HrFinanceAccountRole.StaffAdvancesReceivable, false, recovered, $"Travel claim {reference} — advance recovered"));
            if (net > 0m)
                lines.Add(new HrFinancePostingLine(HrFinanceAccountRole.StaffPaymentsClearing, false, net, $"Travel claim {reference} — paid by {method}"));
        }

        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.TravelClaimPaid,
            SourceDocumentId = claim.Id,
            SourceReference = reference,
            EmployeeId = claim.EmployeeId,
            SourceDate = claim.PaidAt,
            Description = viaPayroll
                ? $"Travel expense claim {reference} settled by payroll offset" + (recovered > 0m ? $"; {recovered:N2} recovered from advance" : string.Empty)
                : $"Travel expense claim {reference} paid by {method}" +
                  (string.IsNullOrWhiteSpace(claim.PaymentReference) ? string.Empty : $" ref {claim.PaymentReference}") +
                  (recovered > 0m ? $"; {recovered:N2} recovered from advance" : string.Empty),
            SkipReason = skip,
            Lines = lines
        };
    }

    public static HrFinancePostingCommand TravelAdvanceDisbursed(StaffTravelAdvance advance)
    {
        var amount = advance.ApprovedAmount ?? 0m;
        var reference = advance.AdvanceNumber;
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.TravelAdvanceDisbursed,
            SourceDocumentId = advance.Id,
            SourceReference = reference,
            EmployeeId = advance.EmployeeId,
            SourceDate = advance.DisbursedAt,
            TransactionCurrencyCode = advance.CurrencyCode,
            Description = $"Travel advance {reference} disbursed ({advance.AdvanceType}, {advance.CurrencyCode} {amount:N2})",
            SkipReason = amount <= 0m ? "The approved amount is zero; there is nothing to disburse." : null,
            Lines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.StaffAdvancesReceivable, true, amount, $"Travel advance {reference} — receivable from employee"),
                new HrFinancePostingLine(HrFinanceAccountRole.StaffPaymentsClearing, false, amount, $"Travel advance {reference} — disbursed")
            ]
        };
    }
}
