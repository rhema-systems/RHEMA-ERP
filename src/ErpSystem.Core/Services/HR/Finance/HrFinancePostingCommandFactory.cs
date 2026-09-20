using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Entities.HR.StaffLeave;
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

    // ── Leave (slice 2) ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One journal: the encashment is recognised and, on the direct route, settled. Payroll route
    /// leaves the credit on the payable for the run to clear.
    /// </summary>
    public static HrFinancePostingCommand LeaveEncashmentProcessed(LeaveEncashment encashment)
    {
        var amount = encashment.AmountPaid;
        var reference = $"ENC-{encashment.Year}-{encashment.Id.ToString("N")[..8].ToUpperInvariant()}";
        var narration = $"Leave encashment {reference} — {encashment.DaysEncashed:0.##} day(s)";
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.LeaveEncashmentProcessed,
            SourceDocumentId = encashment.Id,
            SourceReference = reference,
            EmployeeId = encashment.EmployeeId,
            SourceDate = encashment.ProcessedDate,
            Description = $"Leave encashment {reference} processed" +
                          (string.IsNullOrWhiteSpace(encashment.PaymentReference) ? string.Empty : $" ref {encashment.PaymentReference}"),
            SkipReason = amount <= 0m ? "The encashment amount is zero; there is nothing to recognise." : null,
            Lines = [],
            RouteDirectLines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.LeaveEncashmentExpense, true, amount, narration),
                new HrFinancePostingLine(HrFinanceAccountRole.StaffPaymentsClearing, false, amount, $"{narration} — paid")
            ],
            RoutePayrollLines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.LeaveEncashmentExpense, true, amount, narration),
                new HrFinancePostingLine(HrFinanceAccountRole.StaffClaimsPayable, false, amount, $"{narration} — payable, cleared by payroll")
            ]
        };
    }

    // ── Awards (slice 2) ─────────────────────────────────────────────────────────────────────

    public static HrFinancePostingCommand AwardConferred(EmployeeAward award)
    {
        var amount = award.MonetaryAmount ?? 0m;
        var reference = award.AwardNumber;
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.AwardConferred,
            SourceDocumentId = award.Id,
            SourceReference = reference,
            EmployeeId = award.EmployeeId,
            SourceDate = award.AwardDate,
            Description = $"Award {reference} conferred",
            SkipReason = amount <= 0m ? "The award carries no monetary value; there is nothing to recognise." : null,
            Lines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.AwardsExpense, true, amount, $"Award {reference} — expense"),
                new HrFinancePostingLine(HrFinanceAccountRole.StaffClaimsPayable, false, amount, $"Award {reference} — payable to employee")
            ]
        };
    }

    /// <summary>
    /// Settles the conferred value; a payment that differs from it adjusts the expense so the
    /// payable is cleared exactly and the difference is visible where it belongs.
    /// </summary>
    /// <param name="award">The paid award.</param>
    /// <param name="conferralPosted">
    /// Whether <c>AWARD_CONFERRED</c> reached Finance for this award. When it did not (an award
    /// priced after conferral, or conferred while the rule was off) there is no payable to clear,
    /// so the payment recognises the expense and settles it in one journal — and on the payroll
    /// route recognises it against the payable for the run to clear.
    /// </param>
    public static HrFinancePostingCommand AwardPaid(EmployeeAward award, bool conferralPosted = true)
    {
        var conferred = award.MonetaryAmount ?? 0m;
        var paid = award.AmountPaid ?? conferred;
        var reference = award.AwardNumber;
        var lines = new List<HrFinancePostingLine>();
        var payrollLines = new List<HrFinancePostingLine>();
        if (!conferralPosted)
        {
            if (paid > 0m)
            {
                lines.Add(new HrFinancePostingLine(HrFinanceAccountRole.AwardsExpense, true, paid, $"Award {reference} — expense (recognised at payment)"));
                lines.Add(new HrFinancePostingLine(HrFinanceAccountRole.StaffPaymentsClearing, false, paid, $"Award {reference} — paid"));
                payrollLines.Add(new HrFinancePostingLine(HrFinanceAccountRole.AwardsExpense, true, paid, $"Award {reference} — expense (recognised at payment)"));
                payrollLines.Add(new HrFinancePostingLine(HrFinanceAccountRole.StaffClaimsPayable, false, paid, $"Award {reference} — payable, cleared by payroll"));
            }
        }
        else
        {
            if (conferred > 0m)
                lines.Add(new HrFinancePostingLine(HrFinanceAccountRole.StaffClaimsPayable, true, conferred, $"Award {reference} — payable settled"));
            if (paid > 0m)
                lines.Add(new HrFinancePostingLine(HrFinanceAccountRole.StaffPaymentsClearing, false, paid, $"Award {reference} — paid"));
            var difference = paid - conferred;
            if (difference > 0m)
                lines.Add(new HrFinancePostingLine(HrFinanceAccountRole.AwardsExpense, true, difference, $"Award {reference} — paid above conferred value"));
            else if (difference < 0m)
                lines.Add(new HrFinancePostingLine(HrFinanceAccountRole.AwardsExpense, false, -difference, $"Award {reference} — paid below conferred value"));
        }

        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.AwardPaid,
            SourceDocumentId = award.Id,
            SourceReference = reference,
            EmployeeId = award.EmployeeId,
            SourceDate = award.PaymentDate,
            Description = $"Award {reference} paid" +
                          (string.IsNullOrWhiteSpace(award.PaymentReference) ? string.Empty : $" ref {award.PaymentReference}"),
            SkipReason = conferred <= 0m && paid <= 0m ? "Nothing was owed or paid; there is nothing to settle." : null,
            Lines = [],
            RouteDirectLines = lines,
            RoutePayrollLines = payrollLines,
            RoutePayrollSkipReason = "Settled through payroll: payroll's own journal clears the staff claims payable; HR posts nothing."
        };
    }

    public static HrFinancePostingCommand LongServiceAwardProcessed(LongServiceAward award)
    {
        var amount = award.MonetaryAmount ?? 0m;
        var reference = $"LSA-{award.YearsOfService}Y-{award.Id.ToString("N")[..8].ToUpperInvariant()}";
        var narration = $"Long-service award {reference} ({award.YearsOfService} years)";
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.LongServiceAwardProcessed,
            SourceDocumentId = award.Id,
            SourceReference = reference,
            EmployeeId = award.EmployeeId,
            SourceDate = award.ProcessedDate,
            Description = $"{narration} processed",
            SkipReason = amount <= 0m
                ? "The long-service rung carries no monetary value (unpriced ladder or leave-only award); there is nothing to recognise."
                : null,
            Lines = [],
            RouteDirectLines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.AwardsExpense, true, amount, narration),
                new HrFinancePostingLine(HrFinanceAccountRole.StaffPaymentsClearing, false, amount, $"{narration} — paid")
            ],
            RoutePayrollLines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.AwardsExpense, true, amount, narration),
                new HrFinancePostingLine(HrFinanceAccountRole.StaffClaimsPayable, false, amount, $"{narration} — payable, cleared by payroll")
            ]
        };
    }

    // ── Benefits (slice 2) ───────────────────────────────────────────────────────────────────

    public static HrFinancePostingCommand BenefitUtilizationApproved(BenefitUtilization claim, EmployeeBenefitEnrollment enrollment)
    {
        var amount = claim.Amount;
        var reference = string.IsNullOrWhiteSpace(claim.ReferenceNumber)
            ? $"BEN-{claim.Id.ToString("N")[..8].ToUpperInvariant()}"
            : claim.ReferenceNumber!;
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.BenefitUtilizationApproved,
            SourceDocumentId = claim.Id,
            SourceReference = reference,
            EmployeeId = enrollment.EmployeeId,
            SourceDate = claim.ApprovedDate ?? claim.ClaimDate.ToDateTime(TimeOnly.MinValue),
            TransactionCurrencyCode = enrollment.Currency,
            Description = $"Benefit claim {reference} approved" +
                          (string.IsNullOrWhiteSpace(claim.Description) ? string.Empty : $" — {claim.Description}"),
            SkipReason = amount <= 0m
                ? "The claim amount is zero; there is nothing to recognise."
                : claim.Type == BenefitUtilizationType.Reversal
                    ? "A reversal utilisation adjusts the balance only; it is not a payable."
                    : null,
            Lines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.BenefitsExpense, true, amount, $"Benefit claim {reference} — expense"),
                new HrFinancePostingLine(HrFinanceAccountRole.StaffClaimsPayable, false, amount, $"Benefit claim {reference} — payable to employee")
            ]
        };
    }

    public static HrFinancePostingCommand BenefitUtilizationPaid(BenefitUtilization claim, EmployeeBenefitEnrollment enrollment)
    {
        var amount = claim.Amount;
        var reference = string.IsNullOrWhiteSpace(claim.ReferenceNumber)
            ? $"BEN-{claim.Id.ToString("N")[..8].ToUpperInvariant()}"
            : claim.ReferenceNumber!;
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.BenefitUtilizationPaid,
            SourceDocumentId = claim.Id,
            SourceReference = reference,
            EmployeeId = enrollment.EmployeeId,
            SourceDate = claim.ApprovedDate,
            TransactionCurrencyCode = enrollment.Currency,
            Description = $"Benefit claim {reference} paid",
            SkipReason = amount <= 0m || claim.Type == BenefitUtilizationType.Reversal
                ? "Nothing is owed on this utilisation; there is nothing to settle."
                : null,
            Lines = [],
            RouteDirectLines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.StaffClaimsPayable, true, amount, $"Benefit claim {reference} — payable settled"),
                new HrFinancePostingLine(HrFinanceAccountRole.StaffPaymentsClearing, false, amount, $"Benefit claim {reference} — paid")
            ],
            RoutePayrollLines = [],
            RoutePayrollSkipReason = "Settled through payroll: payroll's own journal clears the staff claims payable; HR posts nothing."
        };
    }

    // ── Separation (slice 3) ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// The final settlement, released by Internal Audit's approval. Earnings debit the expense the
    /// category belongs to; deductions credit the receivable they recover (loans, advances), the
    /// recovery income (property, other) or the statutory payable (tax). The net goes to clearing
    /// (direct) or the payable (payroll's final run); a leaver who owes more than they are due is
    /// carried as a receivable, never as a negative payable.
    /// </summary>
    public static HrFinancePostingCommand SeparationSettlementReleased(
        SeparationSettlement settlement,
        IReadOnlyList<SeparationSettlementLine> lines,
        string separationNumber,
        Guid employeeId)
    {
        var reference = string.IsNullOrWhiteSpace(separationNumber) ? $"SEP-{settlement.Id.ToString("N")[..8].ToUpperInvariant()}" : separationNumber;
        var earnings = new List<HrFinancePostingLine>();
        var deductions = new List<HrFinancePostingLine>();
        decimal totalEarnings = 0m, totalDeductions = 0m;

        foreach (var line in lines.Where(l => !l.IsDeleted && l.Amount is > 0m))
        {
            var amount = line.Amount!.Value;
            var text = $"Settlement {reference} — {line.Category}: {line.Description}";
            if (line.IsDeduction)
            {
                totalDeductions += amount;
                deductions.Add(new HrFinancePostingLine(DeductionRole(line.Category), false, amount, text));
            }
            else
            {
                totalEarnings += amount;
                earnings.Add(new HrFinancePostingLine(EarningRole(line.Category), true, amount, text));
            }
        }

        var net = totalEarnings - totalDeductions;
        List<HrFinancePostingLine> Compose(HrFinanceAccountRole netCreditRole, string netText)
        {
            var all = new List<HrFinancePostingLine>();
            all.AddRange(earnings);
            all.AddRange(deductions);
            if (net > 0m) all.Add(new HrFinancePostingLine(netCreditRole, false, net, netText));
            else if (net < 0m) all.Add(new HrFinancePostingLine(HrFinanceAccountRole.StaffAdvancesReceivable, true, -net, $"Settlement {reference} — leaver owes the balance"));
            return all;
        }

        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.SeparationSettlementReleased,
            SourceDocumentId = settlement.Id,
            SourceReference = reference,
            EmployeeId = employeeId,
            SourceDate = settlement.ReviewedOn ?? settlement.FinalisedOn,
            TransactionCurrencyCode = settlement.CurrencyCode,
            Description = $"Final settlement {reference} released by Internal Audit (earnings {totalEarnings:N2}, deductions {totalDeductions:N2})",
            SkipReason = totalEarnings <= 0m && totalDeductions <= 0m
                ? "The settlement has no computed amounts; there is nothing to post."
                : null,
            Lines = [],
            RouteDirectLines = Compose(HrFinanceAccountRole.StaffPaymentsClearing, $"Settlement {reference} — net paid to leaver"),
            RoutePayrollLines = Compose(HrFinanceAccountRole.StaffClaimsPayable, $"Settlement {reference} — net payable, cleared by the final payroll run")
        };
    }

    private static HrFinanceAccountRole EarningRole(SettlementLineCategory category) => category switch
    {
        SettlementLineCategory.LeaveEncashment => HrFinanceAccountRole.LeaveEncashmentExpense,
        SettlementLineCategory.BenefitPayment => HrFinanceAccountRole.BenefitsExpense,
        _ => HrFinanceAccountRole.SeparationExpense
    };

    private static HrFinanceAccountRole DeductionRole(SettlementLineCategory category) => category switch
    {
        SettlementLineCategory.LoanRepayment => HrFinanceAccountRole.StaffAdvancesReceivable,
        SettlementLineCategory.SalaryAdvanceRecovery => HrFinanceAccountRole.StaffAdvancesReceivable,
        SettlementLineCategory.TravelAdvanceRecovery => HrFinanceAccountRole.StaffAdvancesReceivable,
        SettlementLineCategory.TaxDeduction => HrFinanceAccountRole.StatutoryDeductionsPayable,
        _ => HrFinanceAccountRole.EmployeeRecoveriesIncome
    };
}
