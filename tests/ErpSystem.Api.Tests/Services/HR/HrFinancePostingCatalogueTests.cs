using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Medical;
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
            command.Lines.Should().HaveCountGreaterThanOrEqualTo(2);
            command.Lines.Should().OnlyContain(l => l.Amount > 0m);
            command.Lines.Where(l => l.IsDebit).Sum(l => l.Amount)
                .Should().Be(command.Lines.Where(l => !l.IsDebit).Sum(l => l.Amount), $"{definition.Code} must balance");
            command.Lines.Where(l => l.IsDebit).Select(l => l.Role).Should().BeSubsetOf(definition.DebitRoles, $"{definition.Code} debit roles");
            command.Lines.Where(l => !l.IsDebit).Select(l => l.Role).Should().BeSubsetOf(definition.CreditRoles, $"{definition.Code} credit roles");
        }
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
