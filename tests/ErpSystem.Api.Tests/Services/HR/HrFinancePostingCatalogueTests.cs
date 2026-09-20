using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.HR.Finance;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.HR;

/// <summary>
/// Keeps the HR Finance posting catalogue, the command factory and the area services honest with
/// each other: every catalogued event has a builder that stays inside its declared roles and
/// balances; every builder's zero case skips; and the two area services still route their money
/// events through the adapter (the seam a refactor could quietly remove).
/// </summary>
public sealed class HrFinancePostingCatalogueTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static IEnumerable<(HrFinancePostingEventDefinition Event, HrFinancePostingCommand Command)> SampleCommands()
    {
        var medical = new MedicalExpenseClaim
        {
            Id = Guid.NewGuid(), TenantId = Tenant, ClaimNumber = "MC-1", EmployeeId = Guid.NewGuid(),
            AmountRequested = 500m, AmountApproved = 450m, Status = ClaimStatus.Paid,
            PaymentProcessed = true, PaymentMethod = PaymentMethod.MobileMoney, PaymentReference = "MM-1"
        };
        var travel = new StaffTravelExpenseClaim
        {
            Id = Guid.NewGuid(), TenantId = Tenant, ClaimNumber = "EXP-1", EmployeeId = Guid.NewGuid(), CurrencyCode = "GHS",
            TotalApproved = 1200m, AdvanceDeducted = 200m, NetPayable = 1000m, Status = TravelClaimStatus.Paid,
            PaymentMethod = TravelPaymentMethod.Cheque
        };
        var advance = new StaffTravelAdvance
        {
            Id = Guid.NewGuid(), TenantId = Tenant, AdvanceNumber = "ADV-1", EmployeeId = Guid.NewGuid(),
            CurrencyCode = "GHS", RequestedAmount = 400m, ApprovedAmount = 400m, Status = TravelAdvanceStatus.Disbursed
        };

        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.MedicalClaimApproved), HrFinancePostingCommandFactory.MedicalClaimApproved(medical));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.MedicalClaimPaid), HrFinancePostingCommandFactory.MedicalClaimPaid(medical));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.TravelClaimApproved), HrFinancePostingCommandFactory.TravelClaimApproved(travel));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.TravelClaimPaid), HrFinancePostingCommandFactory.TravelClaimPaid(travel));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.TravelAdvanceDisbursed), HrFinancePostingCommandFactory.TravelAdvanceDisbursed(advance));

        // slice 2 — employee payables
        var encashment = new LeaveEncashment
        {
            Id = Guid.NewGuid(), TenantId = Tenant, EmployeeId = Guid.NewGuid(), Year = 2026, DaysEncashed = 5,
            AmountPaid = 1500m, Status = LeaveEncashmentStatus.Processed, ProcessedDate = DateTime.UtcNow, PaymentReference = "ENC-REF"
        };
        var award = new EmployeeAward
        {
            Id = Guid.NewGuid(), TenantId = Tenant, AwardNumber = "AWD-1", EmployeeId = Guid.NewGuid(), AwardTypeId = Guid.NewGuid(),
            AwardDate = DateTime.UtcNow, MonetaryAmount = 2000m, PaymentProcessed = true, PaymentDate = DateTime.UtcNow,
            PaymentReference = "PAY-1", AmountPaid = 1800m
        };
        var lsa = new LongServiceAward
        {
            Id = Guid.NewGuid(), TenantId = Tenant, EmployeeId = Guid.NewGuid(), AwardTypeId = Guid.NewGuid(),
            YearsOfService = 10, MonetaryAmount = 5000m, IsProcessed = true, ProcessedDate = DateTime.UtcNow
        };
        var enrollment = new EmployeeBenefitEnrollment { Id = Guid.NewGuid(), TenantId = Tenant, EmployeeId = Guid.NewGuid(), Currency = "GHS" };
        var benefit = new BenefitUtilization
        {
            Id = Guid.NewGuid(), TenantId = Tenant, EnrollmentId = enrollment.Id, Amount = 350m, ClaimDate = new DateOnly(2026, 9, 1),
            Status = BenefitClaimStatus.Paid, ReferenceNumber = "BEN-1", ApprovedDate = DateTime.UtcNow
        };

        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.LeaveEncashmentProcessed), HrFinancePostingCommandFactory.LeaveEncashmentProcessed(encashment));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.AwardConferred), HrFinancePostingCommandFactory.AwardConferred(award));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.AwardPaid), HrFinancePostingCommandFactory.AwardPaid(award));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.LongServiceAwardProcessed), HrFinancePostingCommandFactory.LongServiceAwardProcessed(lsa));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.BenefitUtilizationApproved), HrFinancePostingCommandFactory.BenefitUtilizationApproved(benefit, enrollment));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.BenefitUtilizationPaid), HrFinancePostingCommandFactory.BenefitUtilizationPaid(benefit, enrollment));
    }

    [Fact]
    public void EveryCatalogueEvent_HasABuilder_ThatBalancesInsideItsDeclaredRoles()
    {
        var samples = SampleCommands().ToList();

        samples.Select(s => s.Event.Code).Should().BeEquivalentTo(
            HrFinancePostingEventCatalog.Events.Select(e => e.Code),
            "an event without a builder cannot be retried from the register");

        foreach (var (definition, command) in samples)
        {
            command.EventCode.Should().Be(definition.Code);
            command.SkipReason.Should().BeNull();
            command.SourceReference.Should().NotBeNullOrWhiteSpace();
            command.Description.Should().NotBeNullOrWhiteSpace();
            (command.RouteDirectLines is not null).Should().Be(definition.SupportsSettlementRoute,
                $"{definition.Code}: a command carries route-dependent lines exactly when its event supports a settlement route");

            // Every route that has lines must balance inside the declared roles.
            foreach (var route in new[] { HrFinanceSettlementRoute.Direct, HrFinanceSettlementRoute.Payroll })
            {
                var rule = new HrFinancePostingRule { EventCode = definition.Code, IsEnabled = true, SettlementRoute = route };
                var (lines, skip) = HrFinancePostingAdapter.ResolveRouteLines(definition, rule, command);
                if (lines.Count == 0)
                {
                    skip.Should().NotBeNullOrWhiteSpace($"{definition.Code} on {route} posts nothing and must say why");
                    continue;
                }
                lines.Should().HaveCountGreaterThanOrEqualTo(2, $"{definition.Code} on {route}");
                lines.Should().OnlyContain(l => l.Amount > 0m);
                lines.Where(l => l.IsDebit).Sum(l => l.Amount)
                    .Should().Be(lines.Where(l => !l.IsDebit).Sum(l => l.Amount), $"{definition.Code} on {route} must balance");
                lines.Where(l => l.IsDebit).Select(l => l.Role).Should().BeSubsetOf(definition.DebitRoles, $"{definition.Code} debit roles");
                lines.Where(l => !l.IsDebit).Select(l => l.Role).Should().BeSubsetOf(definition.CreditRoles, $"{definition.Code} credit roles");
                if (!definition.SupportsSettlementRoute) break; // route-independent: one pass is the whole truth
            }
        }
    }

    [Fact]
    public void SettlementRoute_ChoosesTheCreditSide_AndPayrollSkipsPureSettlements()
    {
        var encashment = new LeaveEncashment { Id = Guid.NewGuid(), Year = 2026, DaysEncashed = 2, AmountPaid = 600m, Status = LeaveEncashmentStatus.Processed };
        var leave = HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.LeaveEncashmentProcessed);
        var leaveCommand = HrFinancePostingCommandFactory.LeaveEncashmentProcessed(encashment);

        // Catalogue default for leave is payroll: the credit lands on the payable.
        var (defaultLines, _) = HrFinancePostingAdapter.ResolveRouteLines(leave, null, leaveCommand);
        defaultLines.Should().ContainSingle(l => !l.IsDebit && l.Role == HrFinanceAccountRole.StaffClaimsPayable && l.Amount == 600m);
        // An administrator flips it to direct: the credit lands on clearing.
        var (directLines, _) = HrFinancePostingAdapter.ResolveRouteLines(leave,
            new HrFinancePostingRule { SettlementRoute = HrFinanceSettlementRoute.Direct }, leaveCommand);
        directLines.Should().ContainSingle(l => !l.IsDebit && l.Role == HrFinanceAccountRole.StaffPaymentsClearing && l.Amount == 600m);

        // A pure settlement (award paid) on the payroll route posts nothing and says so.
        var award = new EmployeeAward { Id = Guid.NewGuid(), AwardNumber = "AWD-P", MonetaryAmount = 900m, AmountPaid = 900m, PaymentProcessed = true };
        var paid = HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.AwardPaid);
        var (payrollLines, skip) = HrFinancePostingAdapter.ResolveRouteLines(paid,
            new HrFinancePostingRule { SettlementRoute = HrFinanceSettlementRoute.Payroll }, HrFinancePostingCommandFactory.AwardPaid(award));
        payrollLines.Should().BeEmpty();
        skip.Should().Contain("payroll");

        // A payment below the conferred value clears the whole payable and credits the difference to expense.
        var shortPaid = new EmployeeAward { Id = Guid.NewGuid(), AwardNumber = "AWD-S", MonetaryAmount = 1000m, AmountPaid = 800m, PaymentProcessed = true };
        var (shortLines, _) = HrFinancePostingAdapter.ResolveRouteLines(paid, null, HrFinancePostingCommandFactory.AwardPaid(shortPaid));
        shortLines.Should().ContainSingle(l => l.IsDebit && l.Role == HrFinanceAccountRole.StaffClaimsPayable && l.Amount == 1000m);
        shortLines.Should().ContainSingle(l => !l.IsDebit && l.Role == HrFinanceAccountRole.StaffPaymentsClearing && l.Amount == 800m);
        shortLines.Should().ContainSingle(l => !l.IsDebit && l.Role == HrFinanceAccountRole.AwardsExpense && l.Amount == 200m);
    }

    [Fact]
    public void Slice2Services_RouteTheirMoneyEventsThroughTheAdapter()
    {
        var root = FindRepositoryRoot();
        var leave = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "HR", "LeaveEncashmentService.cs"));
        var awards = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "HR", "AwardsServices.cs"));
        var benefits = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "HR", "EmployeeBenefitEnrollmentService.cs"));

        Between(leave, "public async Task<LeaveEncashmentDto> MarkAsProcessedAsync(", "public async Task<IEnumerable<LeaveEncashmentDto>> GetEmployeeEncashmentsAsync(")
            .Should().Contain("_financePosting.RunAsync").And.Contain("HrFinancePostingCommandFactory.LeaveEncashmentProcessed(entity)")
            .And.NotContain("ExecuteInTransactionAsync", "the adapter owns the transaction now");

        Between(awards, "public async Task<EmployeeAwardDto> CreateAsync(", "public async Task<EmployeeAwardDto> CreateFromNominationAsync(")
            .Should().Contain("HrFinancePostingCommandFactory.AwardConferred(entity)");
        Between(awards, "public async Task<EmployeeAwardDto> CreateFromNominationAsync(", "public async Task<EmployeeAwardDto> UpdateAsync(")
            .Should().Contain("HrFinancePostingCommandFactory.AwardConferred(entity)");
        Between(awards, "public async Task ProcessPaymentAsync(", "public async Task ProcessLeaveAsync(")
            .Should().Contain("RunWithFinanceAsync").And.Contain("entity.AmountPaid = paid").And.Contain("HrFinancePostingCommandFactory.AwardPaid(entity, conferralPosted)")
            .And.Contain("IsPostedAsync(HrFinancePostingEventCatalog.AwardConferred", "a payment must know whether the conferral it settles ever posted");
        Between(awards, "public async Task ProcessAsync(Guid userId, ProcessLongServiceAwardDto dto)", "#endregion")
            .Should().Contain("HrFinancePostingCommandFactory.LongServiceAwardProcessed(entity)").And.Contain("if (entity.IsProcessed)");
        awards.Should().Contain("GuardNotPostedAsync(id, \"Editing this award\")")
            .And.Contain("GuardNotPostedAsync(id, \"Deleting this award\")")
            .And.Contain("GuardNotPostedAsync(id, \"Editing this long-service award\")")
            .And.Contain("GuardNotPostedAsync(id, \"Deleting this long-service award\")");

        Between(benefits, "public async Task<BenefitUtilizationDto> ChangeClaimStatusAsync(", "\n    public ")
            .Should().Contain("_financePosting.RunAsync")
            .And.Contain("HrFinancePostingCommandFactory.BenefitUtilizationApproved(tracked, enrollment)")
            .And.Contain("HrFinancePostingCommandFactory.BenefitUtilizationPaid(tracked, enrollment)")
            .And.Contain("A claim must be Approved before it can be Paid")
            .And.Contain("EnsureNotPostedAsync");

        foreach (var source in new[] { leave, awards, benefits })
            source.Should().NotContain("IJournalEntryService").And.NotContain("CreateJournalEntryAsync");
    }

    [Fact]
    public void Catalogue_IsWellFormed_AndEveryRoleHasARequiredAccountType()
    {
        var events = HrFinancePostingEventCatalog.Events;
        events.Select(e => e.Code).Should().OnlyHaveUniqueItems();
        events.Should().OnlyContain(e =>
            !string.IsNullOrWhiteSpace(e.Name) && !string.IsNullOrWhiteSpace(e.SourceDocumentType)
            && !string.IsNullOrWhiteSpace(e.Trigger) && !string.IsNullOrWhiteSpace(e.Treatment)
            && e.DebitRoles.Count > 0 && e.CreditRoles.Count > 0);

        // Two events on one source document type must never share a posting action — Finance
        // de-duplicates on (type, id, action).
        events.GroupBy(e => e.SourceDocumentType).Should().OnlyContain(g =>
            g.Select(e => HrFinancePostingAdapter.ResolvePostingAction(e, 1)).Distinct().Count() == g.Count());

        foreach (var role in Enum.GetValues<HrFinanceAccountRole>())
        {
            var act = () => HrFinancePostingEventCatalog.RequiredAccountType(role);
            act.Should().NotThrow();
            HrFinanceAccountRoleNames.Name(role).Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void ZeroAmounts_ProduceASkipNotAPosting()
    {
        HrFinancePostingCommandFactory.MedicalClaimApproved(new MedicalExpenseClaim { ClaimNumber = "MC-0", AmountApproved = 0m })
            .SkipReason.Should().NotBeNullOrWhiteSpace();
        HrFinancePostingCommandFactory.TravelClaimApproved(new StaffTravelExpenseClaim { ClaimNumber = "EXP-0", CurrencyCode = "GHS", TotalApproved = 0m })
            .SkipReason.Should().NotBeNullOrWhiteSpace();
        HrFinancePostingCommandFactory.TravelAdvanceDisbursed(new StaffTravelAdvance { AdvanceNumber = "ADV-0", CurrencyCode = "GHS", ApprovedAmount = null })
            .SkipReason.Should().NotBeNullOrWhiteSpace();
        HrFinancePostingCommandFactory.MedicalClaimPaid(new MedicalExpenseClaim { ClaimNumber = "MC-S", AmountApproved = 100m, PaymentMethod = PaymentMethod.SalaryDeduction })
            .SkipReason.Should().Contain("payroll");
    }

    [Fact]
    public void TravelAdvanceCommands_CarryTheAdvanceCurrency_AndClaimCommandsDoNot()
    {
        HrFinancePostingCommandFactory.TravelAdvanceDisbursed(new StaffTravelAdvance { AdvanceNumber = "ADV-U", CurrencyCode = "USD", ApprovedAmount = 10m })
            .TransactionCurrencyCode.Should().Be("USD");
        // Claim totals are functional sums already (each line was valued through Finance's rate).
        HrFinancePostingCommandFactory.TravelClaimApproved(new StaffTravelExpenseClaim { ClaimNumber = "EXP-U", CurrencyCode = "USD", TotalApproved = 10m })
            .TransactionCurrencyCode.Should().BeNull();
    }

    [Fact]
    public void AreaServices_StillRouteTheirMoneyEventsThroughTheAdapter()
    {
        var root = FindRepositoryRoot();
        var medical = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "HR", "MedicalServices.cs"));
        var travel = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "HR", "StaffTravelFinanceService.cs"));

        // Medical: approval and payment post; edit and delete are guarded.
        Between(medical, "public async Task<bool> ProcessApprovalAsync(", "public async Task<bool> ProcessPaymentAsync(")
            .Should().Contain("RunWithFinanceAsync").And.Contain("HrFinancePostingCommandFactory.MedicalClaimApproved(claim)");
        Between(medical, "public async Task<bool> ProcessPaymentAsync(", "public async Task<bool> FlagClaimAsync(")
            .Should().Contain("RunWithFinanceAsync").And.Contain("HrFinancePostingCommandFactory.MedicalClaimPaid(entity)");
        medical.Should().Contain("GuardNotPostedAsync(entity.Id, \"Editing this claim\"")
            .And.Contain("GuardNotPostedAsync(entity.Id, \"Deleting this claim\"");

        // Travel: review, pay and disburse post; claim, line and advance edits are guarded.
        Between(travel, "public async Task<bool> ReviewClaimAsync(", "public async Task<bool> PayClaimAsync(")
            .Should().Contain("_financePosting.RunAsync").And.Contain("HrFinancePostingCommandFactory.TravelClaimApproved(entity)");
        Between(travel, "public async Task<bool> PayClaimAsync(", "// ---- Expense claim lines")
            .Should().Contain("_financePosting.RunAsync").And.Contain("await SettleLinkedAdvanceAsync(entity, ct);")
            .And.Contain("HrFinancePostingCommandFactory.TravelClaimPaid(entity)");
        Between(travel, "public async Task<bool> DisburseAdvanceAsync(", "// ---- Per-diem rates")
            .Should().Contain("_financePosting.RunAsync").And.Contain("HrFinancePostingCommandFactory.TravelAdvanceDisbursed(entity)");
        foreach (var method in new[] { "UpdateClaimAsync(", "AddClaimLineAsync(", "UpdateClaimLineAsync(", "ReviewClaimLineAsync(", "DeleteClaimLineAsync(" })
            Between(travel, method, "\n    public ").Should().Contain("GuardClaimNotPostedAsync", method);
        Between(travel, "UpdateAdvanceAsync(", "\n    public ").Should().Contain("GuardAdvanceNotPostedAsync");

        // Nothing in HR writes a Finance journal directly (the Finance owner's 2026-08-31 rule).
        foreach (var source in new[] { medical, travel })
            source.Should().NotContain("IJournalEntryService").And.NotContain("CreateJournalEntryAsync");
    }

    private static string Between(string source, string start, string end)
    {
        var i = source.IndexOf(start, StringComparison.Ordinal);
        i.Should().BeGreaterThanOrEqualTo(0, $"'{start}' must exist");
        var j = source.IndexOf(end, i + start.Length, StringComparison.Ordinal);
        j.Should().BeGreaterThan(i, $"'{end}' must follow '{start}'");
        return source[i..j];
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) && Directory.Exists(Path.Combine(directory.FullName, "tests")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root could not be located.");
    }
}
