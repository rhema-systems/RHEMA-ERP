using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.HR.Assets;
using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Entities.HR.Safety;
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
    /// <param name="settlement">The settlement being released.</param>
    /// <param name="lines">Its lines.</param>
    /// <param name="separationNumber">The separation's human reference.</param>
    /// <param name="employeeId">The leaver.</param>
    /// <param name="linesRecoveringPostedReceivables">
    /// Ids of deduction lines that recover a receivable Finance already holds (an asset surcharge
    /// whose approval posted, carried into the clearance form). Those credit Staff receivables
    /// instead of recoveries income, or the recovery would be booked as income twice.
    /// </param>
    public static HrFinancePostingCommand SeparationSettlementReleased(
        SeparationSettlement settlement,
        IReadOnlyList<SeparationSettlementLine> lines,
        string separationNumber,
        Guid employeeId,
        IReadOnlySet<Guid>? linesRecoveringPostedReceivables = null)
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
                var role = linesRecoveringPostedReceivables?.Contains(line.Id) == true
                    ? HrFinanceAccountRole.StaffReceivables
                    : DeductionRole(line.Category);
                deductions.Add(new HrFinancePostingLine(role, false, amount, text));
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

    // ── Assets (slice 4) ─────────────────────────────────────────────────────────────────────

    public static HrFinancePostingCommand AssetSurchargeApproved(AssetSurcharge surcharge)
    {
        var amount = surcharge.AssessedAmount;
        var reference = surcharge.SurchargeNumber;
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.AssetSurchargeApproved,
            SourceDocumentId = surcharge.Id,
            SourceReference = reference,
            EmployeeId = surcharge.EmployeeId,
            SourceDate = surcharge.ApprovalDate,
            TransactionCurrencyCode = surcharge.CurrencyCode,
            Description = $"Asset surcharge {reference} approved ({surcharge.Reason})",
            SkipReason = amount <= 0m ? "The approved surcharge is zero; there is nothing to recognise." : null,
            Lines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.StaffReceivables, true, amount, $"Surcharge {reference} — owed by employee"),
                new HrFinancePostingLine(HrFinanceAccountRole.EmployeeRecoveriesIncome, false, amount, $"Surcharge {reference} — recovery income")
            ]
        };
    }

    /// <summary>One posting per recovery row; payroll and exit-settlement recoveries are booked by those journals.</summary>
    public static HrFinancePostingCommand AssetSurchargeRecovered(AssetSurcharge surcharge, AssetSurchargeRecovery recovery)
    {
        var reference = $"{surcharge.SurchargeNumber}/R{recovery.RecoveredOn:yyyyMMdd}";
        var skip = recovery.Method switch
        {
            AssetSurchargeRecoveryMethod.PayrollDeduction => "Recovered through payroll: payroll's own journal credits the staff receivable; HR posts nothing.",
            AssetSurchargeRecoveryMethod.ExitSettlement => "Recovered inside the final settlement: the settlement's release posts it; HR posts nothing here.",
            _ => recovery.Amount <= 0m ? "The recovery is zero; there is nothing to post." : null
        };
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.AssetSurchargeRecovered,
            SourceDocumentId = recovery.Id,
            SourceReference = reference,
            EmployeeId = surcharge.EmployeeId,
            SourceDate = recovery.RecoveredOn.ToDateTime(TimeOnly.MinValue),
            TransactionCurrencyCode = surcharge.CurrencyCode,
            Description = $"Surcharge {surcharge.SurchargeNumber} — {recovery.Amount:N2} recovered by {recovery.Method}" +
                          (string.IsNullOrWhiteSpace(recovery.Reference) ? string.Empty : $" ref {recovery.Reference}"),
            SkipReason = skip,
            Lines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.StaffPaymentsClearing, true, recovery.Amount, $"Surcharge {surcharge.SurchargeNumber} — recovery received"),
                new HrFinancePostingLine(HrFinanceAccountRole.StaffReceivables, false, recovery.Amount, $"Surcharge {surcharge.SurchargeNumber} — receivable reduced")
            ]
        };
    }

    public static HrFinancePostingCommand AssetSurchargeWaived(AssetSurcharge surcharge, bool approvalPosted)
    {
        var outstanding = Math.Max(0m, surcharge.AssessedAmount - surcharge.AmountRecovered);
        var reference = surcharge.SurchargeNumber;
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.AssetSurchargeWaived,
            SourceDocumentId = surcharge.Id,
            SourceReference = reference,
            EmployeeId = surcharge.EmployeeId,
            SourceDate = surcharge.WaivedAt,
            TransactionCurrencyCode = surcharge.CurrencyCode,
            Description = $"Asset surcharge {reference} waived: {surcharge.WaiverReason}",
            SkipReason = !approvalPosted
                ? "The surcharge's approval never reached Finance, so there is no receivable to write off."
                : outstanding <= 0m ? "Nothing was outstanding; there is nothing to write off." : null,
            Lines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.StaffReceivableWriteOff, true, outstanding, $"Surcharge {reference} — balance forgiven"),
                new HrFinancePostingLine(HrFinanceAccountRole.StaffReceivables, false, outstanding, $"Surcharge {reference} — receivable written off")
            ]
        };
    }

    // ── Discipline (slice 4) ─────────────────────────────────────────────────────────────────

    public static HrFinancePostingCommand DisciplineFineImposed(StaffDisciplineFine fine, string caseNumber, Guid employeeId)
    {
        var amount = fine.FineAmount ?? 0m;
        var reference = $"{caseNumber}/FINE";
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.DisciplineFineImposed,
            SourceDocumentId = fine.Id,
            SourceReference = reference,
            EmployeeId = employeeId,
            SourceDate = fine.CreatedAt,
            Description = $"Disciplinary fine on case {caseNumber} imposed",
            SkipReason = amount <= 0m ? "The fine is zero; there is nothing to recognise." : null,
            Lines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.StaffReceivables, true, amount, $"Fine {reference} — owed by employee"),
                new HrFinancePostingLine(HrFinanceAccountRole.EmployeeRecoveriesIncome, false, amount, $"Fine {reference} — penalty income")
            ]
        };
    }

    /// <summary>
    /// Fires when the fine closes (Fully Paid or Waived). What was paid clears through the
    /// clearing account (or is left to payroll on that route); what was forgiven is written off.
    /// </summary>
    public static HrFinancePostingCommand DisciplineFineSettled(StaffDisciplineFine fine, string caseNumber, Guid employeeId, bool imposedPosted)
    {
        var total = fine.FineAmount ?? 0m;
        var paid = Math.Min(Math.Max(fine.FinePaidAmount ?? 0m, 0m), total);
        var forgiven = fine.FinePaymentStatus == DisciplinaryFinePaymentStatus.Waived ? total - paid : 0m;
        var reference = $"{caseNumber}/FINE";

        var direct = new List<HrFinancePostingLine>();
        var payroll = new List<HrFinancePostingLine>();
        if (imposedPosted && total > 0m)
        {
            if (paid > 0m) direct.Add(new HrFinancePostingLine(HrFinanceAccountRole.StaffPaymentsClearing, true, paid, $"Fine {reference} — paid"));
            if (forgiven > 0m)
            {
                direct.Add(new HrFinancePostingLine(HrFinanceAccountRole.StaffReceivableWriteOff, true, forgiven, $"Fine {reference} — forgiven"));
                payroll.Add(new HrFinancePostingLine(HrFinanceAccountRole.StaffReceivableWriteOff, true, forgiven, $"Fine {reference} — forgiven"));
                payroll.Add(new HrFinancePostingLine(HrFinanceAccountRole.StaffReceivables, false, forgiven, $"Fine {reference} — receivable written off"));
            }
            if (paid + forgiven > 0m) direct.Add(new HrFinancePostingLine(HrFinanceAccountRole.StaffReceivables, false, paid + forgiven, $"Fine {reference} — receivable cleared"));
        }

        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.DisciplineFineSettled,
            SourceDocumentId = fine.Id,
            SourceReference = reference,
            EmployeeId = employeeId,
            SourceDate = fine.FinePaymentDate,
            Description = fine.FinePaymentStatus == DisciplinaryFinePaymentStatus.Waived
                ? $"Disciplinary fine on case {caseNumber} closed: {paid:N2} paid, {forgiven:N2} waived"
                : $"Disciplinary fine on case {caseNumber} fully paid ({paid:N2})",
            SkipReason = !imposedPosted
                ? "The fine's imposition never reached Finance, so there is no receivable to settle."
                : total <= 0m || paid + forgiven <= 0m ? "Nothing was owed; there is nothing to settle." : null,
            Lines = [],
            RouteDirectLines = direct,
            RoutePayrollLines = payroll,
            RoutePayrollSkipReason = "Recovered through payroll: payroll's own journal credits the staff receivable; HR posts nothing."
        };
    }

    // ── Training bonds (slice 4) ─────────────────────────────────────────────────────────────

    public static HrFinancePostingCommand TrainingBondBreached(TrainingServiceBond bond)
    {
        var amount = bond.RepaymentAmount ?? 0m;
        var reference = $"BOND-{bond.Id.ToString("N")[..8].ToUpperInvariant()}";
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.TrainingBondBreached,
            SourceDocumentId = bond.Id,
            SourceReference = reference,
            EmployeeId = bond.EmployeeId,
            SourceDate = bond.ExitDate,
            TransactionCurrencyCode = bond.Currency,
            Description = $"Training bond {reference} breached on exit; {amount:N2} of {bond.BondAmount:N2} repayable",
            SkipReason = amount <= 0m ? "The bond was served in full; nothing is repayable." : null,
            Lines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.StaffReceivables, true, amount, $"Bond {reference} — repayment owed by employee"),
                new HrFinancePostingLine(HrFinanceAccountRole.EmployeeRecoveriesIncome, false, amount, $"Bond {reference} — training cost recovered")
            ]
        };
    }

    public static HrFinancePostingCommand TrainingBondSettled(TrainingServiceBond bond, bool breachPosted)
    {
        var amount = bond.RepaymentAmount ?? 0m;
        var reference = $"BOND-{bond.Id.ToString("N")[..8].ToUpperInvariant()}";
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.TrainingBondSettled,
            SourceDocumentId = bond.Id,
            SourceReference = reference,
            EmployeeId = bond.EmployeeId,
            SourceDate = bond.SettledDate,
            TransactionCurrencyCode = bond.Currency,
            Description = $"Training bond {reference} repaid ({amount:N2})",
            SkipReason = !breachPosted
                ? "The bond's breach never reached Finance, so there is no receivable to settle."
                : amount <= 0m ? "Nothing was repayable; there is nothing to settle." : null,
            Lines = [],
            RouteDirectLines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.StaffPaymentsClearing, true, amount, $"Bond {reference} — repayment received"),
                new HrFinancePostingLine(HrFinanceAccountRole.StaffReceivables, false, amount, $"Bond {reference} — receivable cleared")
            ],
            RoutePayrollLines = [],
            RoutePayrollSkipReason = "Recovered through payroll: payroll's own journal credits the staff receivable; HR posts nothing."
        };
    }

    public static HrFinancePostingCommand TrainingBondWaived(TrainingServiceBond bond, bool breachPosted)
    {
        var amount = bond.RepaymentAmount ?? 0m;
        var reference = $"BOND-{bond.Id.ToString("N")[..8].ToUpperInvariant()}";
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.TrainingBondWaived,
            SourceDocumentId = bond.Id,
            SourceReference = reference,
            EmployeeId = bond.EmployeeId,
            SourceDate = bond.WaivedDate,
            TransactionCurrencyCode = bond.Currency,
            Description = $"Training bond {reference} waived: {bond.WaiverReason}",
            SkipReason = !breachPosted
                ? "No breach was posted for this bond, so there is no receivable to write off."
                : amount <= 0m ? "Nothing was repayable; there is nothing to write off." : null,
            Lines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.StaffReceivableWriteOff, true, amount, $"Bond {reference} — repayment forgiven"),
                new HrFinancePostingLine(HrFinanceAccountRole.StaffReceivables, false, amount, $"Bond {reference} — receivable written off")
            ]
        };
    }

    // ── Third-party payees (slice 5) ─────────────────────────────────────────────────────────

    /// <summary>
    /// The AP hand-off: one expense line to the cost's supplier. A cost paid to a person (a
    /// candidate's travel, a named payee with no supplier record) has no AP path and is Skipped —
    /// it stays HR-side, per backlog decision #1.
    /// </summary>
    public static HrFinancePostingCommand RequisitionCostApproved(StaffRequisitionCost cost, string requisitionNumber)
    {
        var reference = $"{requisitionNumber}/C{cost.Id.ToString("N")[..4].ToUpperInvariant()}";
        var payee = cost.SupplierId.HasValue ? cost.PayeeName ?? "supplier" : cost.PayeeName ?? "a named payee";
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.RequisitionCostApproved,
            SourceDocumentId = cost.Id,
            SourceReference = reference,
            SourceDate = cost.CostDate.ToDateTime(TimeOnly.MinValue),
            TransactionCurrencyCode = cost.Currency,
            PayeeSupplierId = cost.SupplierId,
            PayeeName = cost.PayeeName,
            Description = $"Recruitment cost {reference} — {cost.Category}: {cost.Purpose} ({payee})",
            SkipReason = cost.Amount <= 0m ? "The cost is zero; there is nothing to invoice."
                : !cost.SupplierId.HasValue ? $"Paid to {payee}, who is not a Procurement supplier: there is no AP path for a person, so the cost stays HR-side (backlog decision #1)."
                : null,
            Lines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.RecruitmentExpense, true, cost.Amount, $"{cost.Category}: {cost.Purpose} — {reference}")
            ]
        };
    }

    /// <summary>
    /// A premium paid to the insurer: the employer's share is medical expense, the employees' share
    /// a receivable payroll recovers, the whole bill leaves through clearing.
    /// </summary>
    public static HrFinancePostingCommand MedicalPremiumPaid(MedicalInsurancePremiumRecord premium, string providerName)
    {
        var employer = Math.Max(0m, premium.EmployerContribution);
        var employee = Math.Max(0m, premium.EmployeeContribution);
        var total = employer + employee;
        var reference = $"PREM-{premium.BillingPeriodStart:yyyyMM}-{premium.Id.ToString("N")[..6].ToUpperInvariant()}";
        var lines = new List<HrFinancePostingLine>();
        if (employer > 0m) lines.Add(new HrFinancePostingLine(HrFinanceAccountRole.MedicalExpense, true, employer, $"Premium {reference} — employer share, {providerName}"));
        if (employee > 0m) lines.Add(new HrFinancePostingLine(HrFinanceAccountRole.StaffReceivables, true, employee, $"Premium {reference} — employees' share, recovered through payroll"));
        if (total > 0m) lines.Add(new HrFinancePostingLine(HrFinanceAccountRole.StaffPaymentsClearing, false, total, $"Premium {reference} — paid to {providerName}"));
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.MedicalPremiumPaid,
            SourceDocumentId = premium.Id,
            SourceReference = reference,
            SourceDate = premium.PaymentDate,
            Description = $"Medical insurance premium {reference} for {premium.BillingPeriodStart:yyyy-MM-dd} to {premium.BillingPeriodEnd:yyyy-MM-dd} paid to {providerName}"
                          + (premium.TotalPremiumAmount != total ? $" (bill {premium.TotalPremiumAmount:N2}; contributions {total:N2})" : string.Empty),
            SkipReason = total <= 0m ? "The premium record carries no employer or employee contribution; there is nothing to post." : null,
            Lines = lines
        };
    }

    public static HrFinancePostingCommand MedicalInsurerRecoveryReceived(MedicalInsuranceClaim claim)
    {
        var amount = claim.PaidAmount ?? 0m;
        var reference = claim.InsuranceClaimNumber;
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.MedicalInsurerRecoveryReceived,
            SourceDocumentId = claim.Id,
            SourceReference = reference,
            SourceDate = claim.PaymentDate,
            Description = $"Insurer paid {amount:N2} on claim {reference}" + (string.IsNullOrWhiteSpace(claim.PaymentReference) ? string.Empty : $" ref {claim.PaymentReference}"),
            SkipReason = amount <= 0m ? "The insurer paid nothing on this claim; there is nothing to post." : null,
            Lines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.StaffPaymentsClearing, true, amount, $"Insurer recovery {reference} — received"),
                new HrFinancePostingLine(HrFinanceAccountRole.InsuranceRecoveriesIncome, false, amount, $"Insurer recovery {reference} — income")
            ]
        };
    }

    public static HrFinancePostingCommand NhisClaimReimbursed(NHISClaim claim)
    {
        var amount = claim.ApprovedAmount ?? claim.NHISCoveredAmount ?? 0m;
        var reference = claim.ClaimNumber;
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.NhisClaimReimbursed,
            SourceDocumentId = claim.Id,
            SourceReference = reference,
            EmployeeId = claim.EmployeeId,
            SourceDate = claim.PaymentDate,
            Description = $"NHIS reimbursed {amount:N2} on claim {reference}" + (string.IsNullOrWhiteSpace(claim.PaymentReference) ? string.Empty : $" ref {claim.PaymentReference}"),
            SkipReason = amount <= 0m ? "No approved or covered amount is recorded on this NHIS claim; there is nothing to post." : null,
            Lines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.StaffPaymentsClearing, true, amount, $"NHIS {reference} — received"),
                new HrFinancePostingLine(HrFinanceAccountRole.InsuranceRecoveriesIncome, false, amount, $"NHIS {reference} — income")
            ]
        };
    }

    public static HrFinancePostingCommand SheInsuranceClaimReceived(SafetyIncident incident)
    {
        var amount = incident.AmountPaid ?? 0m;
        var reference = string.IsNullOrWhiteSpace(incident.ClaimReferenceNumber) ? incident.IncidentNumber : $"{incident.IncidentNumber}/{incident.ClaimReferenceNumber}";
        return new HrFinancePostingCommand
        {
            EventCode = HrFinancePostingEventCatalog.SheInsuranceClaimReceived,
            SourceDocumentId = incident.Id,
            SourceReference = reference,
            SourceDate = incident.ClaimFiledDate,
            Description = $"Insurance claim on incident {incident.IncidentNumber} paid: {amount:N2}",
            SkipReason = !incident.InsuranceClaimFiled ? "No insurance claim is filed on this incident."
                : !incident.ClaimApproved ? "The insurance claim has not been approved; nothing has been received."
                : amount <= 0m ? "No amount has been paid on the claim yet; nothing to post." : null,
            Lines =
            [
                new HrFinancePostingLine(HrFinanceAccountRole.StaffPaymentsClearing, true, amount, $"Incident {incident.IncidentNumber} — insurance proceeds received"),
                new HrFinancePostingLine(HrFinanceAccountRole.InsuranceRecoveriesIncome, false, amount, $"Incident {incident.IncidentNumber} — insurance recovery")
            ]
        };
    }
}
