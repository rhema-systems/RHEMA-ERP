using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Assets;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Entities.HR.Training;
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

        // slice 3 — separation
        var (settlement, settlementLines) = SampleSettlement(unpaidSalary: 3000m, notice: 6000m, encashment: 900m, loan: 250m, tax: 400m, property: 150m);
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.SeparationSettlementReleased),
            HrFinancePostingCommandFactory.SeparationSettlementReleased(settlement, settlementLines, "SEP-2026-0001", Guid.NewGuid()));

        // slice 4 — employee receivables
        var surcharge = new AssetSurcharge
        {
            Id = Guid.NewGuid(), TenantId = Tenant, SurchargeNumber = "SUR-1", EmployeeId = Guid.NewGuid(), CurrencyCode = "GHS",
            AssessedAmount = 1200m, AmountRecovered = 400m, Status = AssetSurchargeStatus.Waived, ApprovalDate = DateTime.UtcNow,
            WaivedAt = DateTime.UtcNow, WaiverReason = "goodwill"
        };
        var recovery = new AssetSurchargeRecovery
        {
            Id = Guid.NewGuid(), TenantId = Tenant, SurchargeId = surcharge.Id, Amount = 400m,
            RecoveredOn = new DateOnly(2026, 9, 10), Method = AssetSurchargeRecoveryMethod.DirectPayment, Reference = "RCPT-1"
        };
        var fine = new StaffDisciplineFine
        {
            Id = Guid.NewGuid(), TenantId = Tenant, DisciplinaryActionId = Guid.NewGuid(), FineAmount = 300m,
            FinePaidAmount = 200m, FinePaymentStatus = DisciplinaryFinePaymentStatus.Waived, FinePaymentDate = DateTime.UtcNow
        };
        var bond = new TrainingServiceBond
        {
            Id = Guid.NewGuid(), TenantId = Tenant, EmployeeId = Guid.NewGuid(), Currency = "GHS", BondAmount = 5000m,
            BondDurationMonths = 24, RepaymentAmount = 2500m, Status = TrainingBondStatus.Waived, ExitDate = DateTime.UtcNow,
            SettledDate = DateTime.UtcNow, WaivedDate = DateTime.UtcNow, WaiverReason = "policy"
        };
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.AssetSurchargeApproved), HrFinancePostingCommandFactory.AssetSurchargeApproved(surcharge));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.AssetSurchargeRecovered), HrFinancePostingCommandFactory.AssetSurchargeRecovered(surcharge, recovery));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.AssetSurchargeWaived), HrFinancePostingCommandFactory.AssetSurchargeWaived(surcharge, approvalPosted: true));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.DisciplineFineImposed), HrFinancePostingCommandFactory.DisciplineFineImposed(fine, "DC-1", Guid.NewGuid()));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.DisciplineFineSettled), HrFinancePostingCommandFactory.DisciplineFineSettled(fine, "DC-1", Guid.NewGuid(), imposedPosted: true));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.TrainingBondBreached), HrFinancePostingCommandFactory.TrainingBondBreached(bond));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.TrainingBondSettled), HrFinancePostingCommandFactory.TrainingBondSettled(bond, breachPosted: true));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.TrainingBondWaived), HrFinancePostingCommandFactory.TrainingBondWaived(bond, breachPosted: true));

        // slice 5 — third-party payees
        var cost = new StaffRequisitionCost
        {
            Id = Guid.NewGuid(), TenantId = Tenant, RequisitionId = Guid.NewGuid(), Category = StaffRequisitionCostCategory.JobAdvertising,
            Purpose = "Sunday advert", Amount = 500m, Currency = "GHS", CostDate = new DateOnly(2026, 9, 1), SupplierId = Guid.NewGuid(),
            PayeeName = "Graphic Communications", Status = StaffRequisitionCostStatus.Approved
        };
        var premium = new MedicalInsurancePremiumRecord
        {
            Id = Guid.NewGuid(), TenantId = Tenant, ProviderId = Guid.NewGuid(), PlanId = Guid.NewGuid(),
            BillingPeriodStart = new DateOnly(2026, 7, 1), BillingPeriodEnd = new DateOnly(2026, 9, 30),
            TotalPremiumAmount = 18000m, EmployerContribution = 14000m, EmployeeContribution = 4000m,
            Status = MedicalInsurancePremiumPaymentStatus.Paid, PaymentDate = DateTime.UtcNow
        };
        var insurerClaim = new MedicalInsuranceClaim
        {
            Id = Guid.NewGuid(), TenantId = Tenant, PolicyId = Guid.NewGuid(), MedicalExpenseClaimId = Guid.NewGuid(),
            InsuranceClaimNumber = "INS-1", ClaimedAmount = 1200m, ApprovedAmount = 1000m, PaidAmount = 1000m,
            Status = MedicalInsuranceClaimStatus.Paid, PaymentDate = DateTime.UtcNow, PaymentReference = "RMT-1"
        };
        var nhis = new NHISClaim
        {
            Id = Guid.NewGuid(), TenantId = Tenant, ClaimNumber = "NHIS-1", EmployeeId = Guid.NewGuid(), FacilityId = Guid.NewGuid(),
            TotalCost = 80m, ApprovedAmount = 60m, Status = NHISClaimStatus.Paid, PaymentDate = DateTime.UtcNow
        };
        var incident = new SafetyIncident
        {
            Id = Guid.NewGuid(), TenantId = Tenant, IncidentNumber = "INC-2026-00001", InsuranceClaimFiled = true,
            ClaimReferenceNumber = "CLM-1", ClaimAmount = 5000m, ClaimApproved = true, AmountPaid = 4500m, ClaimFiledDate = DateTime.UtcNow
        };
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.RequisitionCostApproved), HrFinancePostingCommandFactory.RequisitionCostApproved(cost, "REQ-2026-0001"));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.MedicalPremiumPaid), HrFinancePostingCommandFactory.MedicalPremiumPaid(premium, "Acme Health"));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.MedicalInsurerRecoveryReceived), HrFinancePostingCommandFactory.MedicalInsurerRecoveryReceived(insurerClaim));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.NhisClaimReimbursed), HrFinancePostingCommandFactory.NhisClaimReimbursed(nhis));
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.SheInsuranceClaimReceived), HrFinancePostingCommandFactory.SheInsuranceClaimReceived(incident));

        // slice 6 — HR's one revenue
        var consultingInvoice = new TimesheetInvoice
        {
            Id = Guid.NewGuid(), TenantId = Tenant, InvoiceNumber = "TSI-2026-0001", ClientId = Guid.NewGuid(), ConsultantId = Guid.NewGuid(),
            BillingPeriodStart = new DateOnly(2026, 9, 1), BillingPeriodEnd = new DateOnly(2026, 9, 5), TotalHours = 32m, HourlyRate = 420m,
            SubTotal = 13440m, TaxPercentage = 21.9m, TaxAmount = 2943.36m, TotalAmount = 16383.36m, Currency = "GHS",
            Status = TimesheetInvoiceStatus.Sent, IssuedDate = new DateOnly(2026, 9, 8)
        };
        yield return (HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.TimesheetInvoiceSent), HrFinancePostingCommandFactory.TimesheetInvoiceSent(consultingInvoice, Guid.NewGuid(), "GPHA"));
    }

    [Fact]
    public void ConsultingInvoices_RaiseOneRevenueLineBeforeTax_AndSkipUnlinkedClients()
    {
        var invoice = new TimesheetInvoice { Id = Guid.NewGuid(), InvoiceNumber = "TSI-9", TotalHours = 10m, HourlyRate = 100m, SubTotal = 1000m, TaxPercentage = 21.9m, TaxAmount = 219m, TotalAmount = 1219m, Currency = "USD", Status = TimesheetInvoiceStatus.Sent };
        var command = HrFinancePostingCommandFactory.TimesheetInvoiceSent(invoice, Guid.NewGuid(), "Client");
        HrFinancePostingEventCatalog.GetRequired(command.EventCode).Kind.Should().Be(HrFinancePostingKind.CustomerInvoice);
        command.Lines.Should().ContainSingle(l => !l.IsDebit && l.Role == HrFinanceAccountRole.ConsultingRevenue && l.Amount == 1000m, "the receivable is billed hours before tax; Finance's tax group governs");
        command.TransactionCurrencyCode.Should().Be("USD");
        command.Description.Should().Contain("Finance's tax group governs");

        HrFinancePostingCommandFactory.TimesheetInvoiceSent(invoice, null, "Client").SkipReason.Should().Contain("not linked to a Finance customer");
    }

    [Fact]
    public void Slice6Services_RouteTheirMoneyEventsThroughTheAdapter()
    {
        var root = FindRepositoryRoot();
        var consulting = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "HR", "ConsultantServices.cs"));
        var actuals = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "HR", "Finance", "HrFinanceActualsService.cs"));

        Between(consulting, "public async Task<TimesheetInvoiceDto> SendAsync(", "public async Task<TimesheetInvoiceDto> MarkPaidAsync(")
            .Should().Contain("_financePosting.RunAsync").And.Contain("HrFinancePostingCommandFactory.TimesheetInvoiceSent(");
        Between(consulting, "public async Task<TimesheetInvoiceDto> MarkPaidAsync(", "public async Task<TimesheetInvoiceDto> VoidAsync(")
            .Should().Contain("EnsureNotPostedAsync", "once Finance holds the receivable, the receipt is Finance's fact");
        Between(consulting, "public async Task<TimesheetInvoiceDto> VoidAsync(", "public async Task<bool> DeleteAsync(")
            .Should().Contain("EnsureNotPostedAsync");
        consulting.Should().NotContain("IInvoiceService").And.NotContain("IJournalEntryService");

        // The budget actuals are a READ of Finance's balances: nothing is written to either budget.
        actuals.Should().Contain("IBookBalanceReadModelService").And.NotContain("ActualSpent =").And.NotContain("SpentAmount =").And.NotContain("SaveChangesAsync");
    }

    [Fact]
    public void ThirdPartyPayees_InvoiceSuppliersThroughAp_AndBookInsurerMoneyAsIncome()
    {
        // An approved cost with a supplier becomes ONE expense line for an AP invoice — the credit
        // side is Finance's AP control, never an HR role.
        var supplierCost = new StaffRequisitionCost { Id = Guid.NewGuid(), Category = StaffRequisitionCostCategory.RecruitmentAgencyFee, Purpose = "Search fee", Amount = 2500m, Currency = "USD", CostDate = new DateOnly(2026, 9, 1), SupplierId = Guid.NewGuid(), PayeeName = "Headhunters Ltd" };
        var ap = HrFinancePostingCommandFactory.RequisitionCostApproved(supplierCost, "REQ-9");
        HrFinancePostingEventCatalog.GetRequired(ap.EventCode).Kind.Should().Be(HrFinancePostingKind.VendorInvoice);
        ap.PayeeSupplierId.Should().Be(supplierCost.SupplierId);
        ap.TransactionCurrencyCode.Should().Be("USD");
        ap.Lines.Should().ContainSingle(l => l.IsDebit && l.Role == HrFinanceAccountRole.RecruitmentExpense && l.Amount == 2500m);

        // A cost paid to a person is not invoiced: there is no AP path for a non-supplier.
        var personCost = new StaffRequisitionCost { Id = Guid.NewGuid(), Category = StaffRequisitionCostCategory.JobAdvertising, Purpose = "Candidate travel", Amount = 100m, Currency = "GHS", PayeeName = "A. Candidate" };
        HrFinancePostingCommandFactory.RequisitionCostApproved(personCost, "REQ-9").SkipReason.Should().Contain("not a Procurement supplier");

        // A premium splits into the employer's expense and the employees' receivable, and leaves through clearing.
        var premium = new MedicalInsurancePremiumRecord { Id = Guid.NewGuid(), BillingPeriodStart = new DateOnly(2026, 7, 1), BillingPeriodEnd = new DateOnly(2026, 9, 30), TotalPremiumAmount = 18000m, EmployerContribution = 14000m, EmployeeContribution = 4000m };
        var paid = HrFinancePostingCommandFactory.MedicalPremiumPaid(premium, "Acme");
        paid.Lines.Should().ContainSingle(l => l.IsDebit && l.Role == HrFinanceAccountRole.MedicalExpense && l.Amount == 14000m);
        paid.Lines.Should().ContainSingle(l => l.IsDebit && l.Role == HrFinanceAccountRole.StaffReceivables && l.Amount == 4000m);
        paid.Lines.Should().ContainSingle(l => !l.IsDebit && l.Role == HrFinanceAccountRole.StaffPaymentsClearing && l.Amount == 18000m);

        // Insurer, NHIS and incident proceeds are the same shape: cash in, recoveries income.
        HrFinancePostingCommandFactory.NhisClaimReimbursed(new NHISClaim { Id = Guid.NewGuid(), ClaimNumber = "N", TotalCost = 80m, NHISCoveredAmount = 70m })
            .Lines.Should().ContainSingle(l => !l.IsDebit && l.Role == HrFinanceAccountRole.InsuranceRecoveriesIncome && l.Amount == 70m, "the covered amount stands in when nothing was approved");
        HrFinancePostingCommandFactory.SheInsuranceClaimReceived(new SafetyIncident { Id = Guid.NewGuid(), IncidentNumber = "INC", InsuranceClaimFiled = true, ClaimApproved = true, ClaimAmount = 5000m })
            .SkipReason.Should().Contain("No amount has been paid", "an approved but unpaid claim is not income yet");
        HrFinancePostingCommandFactory.MedicalInsurerRecoveryReceived(new MedicalInsuranceClaim { Id = Guid.NewGuid(), InsuranceClaimNumber = "I", PaidAmount = 0m })
            .SkipReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Slice5Services_RouteTheirMoneyEventsThroughTheAdapter()
    {
        var root = FindRepositoryRoot();
        var requisition = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "HR", "StaffRequisitionService.cs"));
        var medical = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "HR", "MedicalServices.cs"));
        var safety = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "HR", "StaffSafetyServices.cs"));

        Between(requisition, "public async Task<StaffRequisitionCostDto> DecideCostAsync(", "public async Task<IEnumerable<StaffRequisitionCostDto>> GetCostsAsync(")
            .Should().Contain("_financePosting.RunAsync").And.Contain("HrFinancePostingCommandFactory.RequisitionCostApproved(entity, requisitionNumber)");
        Between(requisition, "public async Task<bool> DeleteCostAsync(", "\n    public ").Should().Contain("EnsureNotPostedAsync");

        Between(medical, "public async Task<MedicalInsurancePremiumRecordDto> RecordPremiumPaymentAsync(", "\n    public ")
            .Should().Contain("HrFinancePostingCommandFactory.MedicalPremiumPaid(entity, providerName)").And.Contain("already recorded as paid");
        Between(medical, "public async Task<MedicalInsuranceClaimDto> RecordInsuranceClaimPaymentAsync(", "\n    public ")
            .Should().Contain("HrFinancePostingCommandFactory.MedicalInsurerRecoveryReceived(entity)");
        Between(medical, "public async Task<bool> RecordClaimPaymentAsync(RecordNHISClaimPaymentDto", "\n    public ")
            .Should().Contain("HrFinancePostingCommandFactory.NhisClaimReimbursed(entity)");

        Between(safety, "public async Task<bool> FileClaimAsync(", "\n    public ")
            .Should().Contain("HrFinancePostingCommandFactory.SheInsuranceClaimReceived(entity)").And.Contain("EnsureNotPostedAsync");

        // Nothing in HR writes a Finance journal or vendor invoice outside the adapter.
        foreach (var source in new[] { requisition, medical, safety })
            source.Should().NotContain("IJournalEntryService").And.NotContain("IVendorInvoiceService");
    }

    [Fact]
    public void Receivables_AreRaisedOnce_ClearedByWhatWasCollected_AndWrittenOffForTheRest()
    {
        var surcharge = new AssetSurcharge { Id = Guid.NewGuid(), SurchargeNumber = "SUR-2", CurrencyCode = "GHS", AssessedAmount = 1000m, AmountRecovered = 250m, ApprovalDate = DateTime.UtcNow, WaivedAt = DateTime.UtcNow, WaiverReason = "x" };
        HrFinancePostingCommandFactory.AssetSurchargeApproved(surcharge).Lines
            .Should().ContainSingle(l => l.IsDebit && l.Role == HrFinanceAccountRole.StaffReceivables && l.Amount == 1000m);

        // A payroll-deduction recovery is payroll's journal, not HR's; an exit-settlement one is the settlement's.
        foreach (var method in new[] { AssetSurchargeRecoveryMethod.PayrollDeduction, AssetSurchargeRecoveryMethod.ExitSettlement })
            HrFinancePostingCommandFactory.AssetSurchargeRecovered(surcharge, new AssetSurchargeRecovery { Id = Guid.NewGuid(), Amount = 100m, Method = method, RecoveredOn = new DateOnly(2026, 9, 1) })
                .SkipReason.Should().NotBeNullOrWhiteSpace($"{method}");
        HrFinancePostingCommandFactory.AssetSurchargeRecovered(surcharge, new AssetSurchargeRecovery { Id = Guid.NewGuid(), Amount = 100m, Method = AssetSurchargeRecoveryMethod.DirectPayment, RecoveredOn = new DateOnly(2026, 9, 1) })
            .Lines.Should().ContainSingle(l => l.IsDebit && l.Role == HrFinanceAccountRole.StaffPaymentsClearing && l.Amount == 100m);

        // Waiving writes off only what is still outstanding, and only if the receivable was ever posted.
        HrFinancePostingCommandFactory.AssetSurchargeWaived(surcharge, approvalPosted: true).Lines
            .Should().ContainSingle(l => l.IsDebit && l.Role == HrFinanceAccountRole.StaffReceivableWriteOff && l.Amount == 750m);
        HrFinancePostingCommandFactory.AssetSurchargeWaived(surcharge, approvalPosted: false).SkipReason.Should().NotBeNullOrWhiteSpace();

        // A fine closed as Waived after a part payment: the paid part clears through clearing, the rest is written off.
        var fine = new StaffDisciplineFine { Id = Guid.NewGuid(), FineAmount = 300m, FinePaidAmount = 200m, FinePaymentStatus = DisciplinaryFinePaymentStatus.Waived };
        var definition = HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.DisciplineFineSettled);
        var settled = HrFinancePostingCommandFactory.DisciplineFineSettled(fine, "DC-2", Guid.NewGuid(), imposedPosted: true);
        var (direct, _) = HrFinancePostingAdapter.ResolveRouteLines(definition, null, settled);
        direct.Should().ContainSingle(l => l.IsDebit && l.Role == HrFinanceAccountRole.StaffPaymentsClearing && l.Amount == 200m);
        direct.Should().ContainSingle(l => l.IsDebit && l.Role == HrFinanceAccountRole.StaffReceivableWriteOff && l.Amount == 100m);
        direct.Should().ContainSingle(l => !l.IsDebit && l.Role == HrFinanceAccountRole.StaffReceivables && l.Amount == 300m);
        // On the payroll route only the write-off posts; payroll's journal clears what was deducted.
        var (payroll, _) = HrFinancePostingAdapter.ResolveRouteLines(definition, new HrFinancePostingRule { SettlementRoute = HrFinanceSettlementRoute.Payroll }, settled);
        payroll.Should().ContainSingle(l => l.IsDebit && l.Role == HrFinanceAccountRole.StaffReceivableWriteOff && l.Amount == 100m);
        payroll.Should().NotContain(l => l.Role == HrFinanceAccountRole.StaffPaymentsClearing);
        // A fine fully paid on the payroll route posts nothing.
        var paidFine = new StaffDisciplineFine { Id = Guid.NewGuid(), FineAmount = 300m, FinePaidAmount = 300m, FinePaymentStatus = DisciplinaryFinePaymentStatus.FullyPaid };
        var (payrollPaid, skip) = HrFinancePostingAdapter.ResolveRouteLines(definition, new HrFinancePostingRule { SettlementRoute = HrFinanceSettlementRoute.Payroll },
            HrFinancePostingCommandFactory.DisciplineFineSettled(paidFine, "DC-3", Guid.NewGuid(), imposedPosted: true));
        payrollPaid.Should().BeEmpty();
        skip.Should().Contain("payroll");

        // A bond served in full posts nothing; a breach posts the pro-rata repayment in the bond's currency.
        HrFinancePostingCommandFactory.TrainingBondBreached(new TrainingServiceBond { Id = Guid.NewGuid(), Currency = "USD", BondAmount = 5000m, RepaymentAmount = 0m }).SkipReason.Should().NotBeNullOrWhiteSpace();
        var breached = HrFinancePostingCommandFactory.TrainingBondBreached(new TrainingServiceBond { Id = Guid.NewGuid(), Currency = "USD", BondAmount = 5000m, RepaymentAmount = 2500m, ExitDate = DateTime.UtcNow });
        breached.TransactionCurrencyCode.Should().Be("USD");
        breached.Lines.Should().ContainSingle(l => l.IsDebit && l.Role == HrFinanceAccountRole.StaffReceivables && l.Amount == 2500m);
    }

    [Fact]
    public void SettlementLinesRecoveringAPostedSurcharge_CreditTheReceivable_NotIncomeTwice()
    {
        var (settlement, lines) = SampleSettlement(unpaidSalary: 1000m, notice: 0m, encashment: 0m, loan: 0m, tax: 0m, property: 150m);
        var propertyLine = lines.Single(l => l.Category == SettlementLineCategory.PropertyRecovery);
        var definition = HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.SeparationSettlementReleased);

        var plain = HrFinancePostingCommandFactory.SeparationSettlementReleased(settlement, lines, "SEP-4", Guid.NewGuid());
        HrFinancePostingAdapter.ResolveRouteLines(definition, null, plain).Lines
            .Should().ContainSingle(l => !l.IsDebit && l.Role == HrFinanceAccountRole.EmployeeRecoveriesIncome && l.Amount == 150m);

        var traced = HrFinancePostingCommandFactory.SeparationSettlementReleased(settlement, lines, "SEP-4", Guid.NewGuid(), new HashSet<Guid> { propertyLine.Id });
        var tracedLines = HrFinancePostingAdapter.ResolveRouteLines(definition, null, traced).Lines;
        tracedLines.Should().ContainSingle(l => !l.IsDebit && l.Role == HrFinanceAccountRole.StaffReceivables && l.Amount == 150m);
        tracedLines.Should().NotContain(l => l.Role == HrFinanceAccountRole.EmployeeRecoveriesIncome);
        // The adapter refuses any role the catalogue entry does not declare — the live harness caught
        // exactly this on the first run. The builder's roles must sit inside the entry's, per side.
        tracedLines.Where(l => l.IsDebit).Select(l => l.Role).Should().BeSubsetOf(definition.DebitRoles);
        tracedLines.Where(l => !l.IsDebit).Select(l => l.Role).Should().BeSubsetOf(definition.CreditRoles);
    }

    [Fact]
    public void Slice4Services_RouteTheirMoneyEventsThroughTheAdapter()
    {
        var root = FindRepositoryRoot();
        var assets = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "HR", "AssetSurchargeService.cs"));
        var discipline = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "HR", "StaffDisciplineSubEntityServices.cs"));
        var bonds = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "HR", "Training", "TrainingServiceBondService.cs"));
        var separation = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "HR", "SeparationService.cs"));

        Between(assets, "public async Task ApproveAsync(", "public async Task RejectAsync(")
            .Should().Contain("outcome == WorkflowOutcome.Approved").And.Contain("HrFinancePostingCommandFactory.AssetSurchargeApproved(entity)");
        Between(assets, "public async Task<AssetSurchargeDto> RecordRecoveryAsync(", "public async Task<AssetSurchargeDto> WaiveAsync(")
            .Should().Contain("HrFinancePostingCommandFactory.AssetSurchargeRecovered(entity, recovery)");
        Between(assets, "public async Task<AssetSurchargeDto> WaiveAsync(", "public async Task<AssetSurchargeDto> CancelAsync(")
            .Should().Contain("HrFinancePostingCommandFactory.AssetSurchargeWaived(entity, approvalPosted)");

        Between(discipline, "public async Task<StaffDisciplineFineDto> RecordAsync(RecordFinePenaltyDto", "public async Task<StaffDisciplineFineDto> RecordPaymentAsync(")
            .Should().Contain("HrFinancePostingCommandFactory.DisciplineFineImposed(");
        Between(discipline, "public async Task<StaffDisciplineFineDto> RecordPaymentAsync(", "\n}")
            .Should().Contain("HrFinancePostingCommandFactory.DisciplineFineSettled(").And.Contain("DisciplinaryFinePaymentStatus.FullyPaid or DisciplinaryFinePaymentStatus.Waived");

        Between(bonds, "public async Task<TrainingServiceBondDto> RecordExitAsync(", "public async Task<TrainingServiceBondDto> WaiveAsync(")
            .Should().Contain("HrFinancePostingCommandFactory.TrainingBondBreached(entity)");
        Between(bonds, "public async Task<TrainingServiceBondDto> WaiveAsync(", "public async Task<TrainingServiceBondDto> SettleAsync(")
            .Should().Contain("HrFinancePostingCommandFactory.TrainingBondWaived(entity, breachPosted)");
        Between(bonds, "public async Task<TrainingServiceBondDto> SettleAsync(", "public async Task<bool> DeleteAsync(")
            .Should().Contain("HrFinancePostingCommandFactory.TrainingBondSettled(entity, breachPosted)");

        separation.Should().Contain("IsPostedAsync(HrFinancePostingEventCatalog.AssetSurchargeApproved", "a settlement must know which recoveries clear a posted receivable");
        foreach (var source in new[] { assets, discipline, bonds })
            source.Should().NotContain("IJournalEntryService");
    }

    private static (SeparationSettlement Settlement, List<SeparationSettlementLine> Lines) SampleSettlement(
        decimal unpaidSalary, decimal notice, decimal encashment, decimal loan, decimal tax, decimal property)
    {
        var settlement = new SeparationSettlement
        {
            Id = Guid.NewGuid(), TenantId = Tenant, SeparationId = Guid.NewGuid(), CurrencyCode = "GHS",
            FinalisedOn = DateTime.UtcNow, ReviewOutcome = SettlementReviewOutcome.Approved, ReviewedOn = DateTime.UtcNow
        };
        SeparationSettlementLine L(SettlementLineCategory c, bool deduction, decimal amount, int order) => new()
        {
            Id = Guid.NewGuid(), TenantId = Tenant, SettlementId = settlement.Id, Category = c, IsDeduction = deduction,
            Description = c.ToString(), Amount = amount, Computation = SettlementLineComputation.Computed, SortOrder = order
        };
        var lines = new List<SeparationSettlementLine>
        {
            L(SettlementLineCategory.UnpaidSalary, false, unpaidSalary, 1),
            L(SettlementLineCategory.NoticePay, false, notice, 2),
            L(SettlementLineCategory.LeaveEncashment, false, encashment, 3),
            L(SettlementLineCategory.LoanRepayment, true, loan, 4),
            L(SettlementLineCategory.TaxDeduction, true, tax, 5),
            L(SettlementLineCategory.PropertyRecovery, true, property, 6),
        };
        return (settlement, lines);
    }

    [Fact]
    public void SeparationSettlement_PostsALegPerLine_AndCarriesTheNetWhereTheRouteSays()
    {
        var (settlement, lines) = SampleSettlement(unpaidSalary: 3000m, notice: 6000m, encashment: 900m, loan: 250m, tax: 400m, property: 150m);
        var definition = HrFinancePostingEventCatalog.GetRequired(HrFinancePostingEventCatalog.SeparationSettlementReleased);
        var command = HrFinancePostingCommandFactory.SeparationSettlementReleased(settlement, lines, "SEP-1", Guid.NewGuid());
        command.TransactionCurrencyCode.Should().Be("GHS");

        // payroll (default): the net sits on the payable for the final run
        var (payroll, _) = HrFinancePostingAdapter.ResolveRouteLines(definition, null, command);
        payroll.Where(l => l.IsDebit && l.Role == HrFinanceAccountRole.SeparationExpense).Sum(l => l.Amount).Should().Be(9000m);
        payroll.Should().ContainSingle(l => l.IsDebit && l.Role == HrFinanceAccountRole.LeaveEncashmentExpense && l.Amount == 900m);
        payroll.Should().ContainSingle(l => !l.IsDebit && l.Role == HrFinanceAccountRole.StaffAdvancesReceivable && l.Amount == 250m);
        payroll.Should().ContainSingle(l => !l.IsDebit && l.Role == HrFinanceAccountRole.StatutoryDeductionsPayable && l.Amount == 400m);
        payroll.Should().ContainSingle(l => !l.IsDebit && l.Role == HrFinanceAccountRole.EmployeeRecoveriesIncome && l.Amount == 150m);
        payroll.Should().ContainSingle(l => !l.IsDebit && l.Role == HrFinanceAccountRole.StaffClaimsPayable && l.Amount == 9100m);
        payroll.Should().NotContain(l => l.Role == HrFinanceAccountRole.StaffPaymentsClearing);

        // direct: the same net goes to clearing
        var (direct, _) = HrFinancePostingAdapter.ResolveRouteLines(definition, new HrFinancePostingRule { SettlementRoute = HrFinanceSettlementRoute.Direct }, command);
        direct.Should().ContainSingle(l => !l.IsDebit && l.Role == HrFinanceAccountRole.StaffPaymentsClearing && l.Amount == 9100m);
        direct.Should().NotContain(l => l.Role == HrFinanceAccountRole.StaffClaimsPayable);

        // a leaver who owes more than they are due is a receivable, never a negative payable
        var (owing, owingLines) = SampleSettlement(unpaidSalary: 100m, notice: 0m, encashment: 0m, loan: 700m, tax: 0m, property: 0m);
        var owingCommand = HrFinancePostingCommandFactory.SeparationSettlementReleased(owing, owingLines, "SEP-2", Guid.NewGuid());
        var (owingPayroll, _) = HrFinancePostingAdapter.ResolveRouteLines(definition, null, owingCommand);
        owingPayroll.Should().ContainSingle(l => l.IsDebit && l.Role == HrFinanceAccountRole.StaffAdvancesReceivable && l.Amount == 600m);
        owingPayroll.Should().NotContain(l => l.Role == HrFinanceAccountRole.StaffClaimsPayable);
        owingPayroll.Where(l => l.IsDebit).Sum(l => l.Amount).Should().Be(owingPayroll.Where(l => !l.IsDebit).Sum(l => l.Amount));

        // lines that could not be computed carry no amount and are ignored, not posted as zero
        var (blank, blankLines) = SampleSettlement(0m, 0m, 0m, 0m, 0m, 0m);
        HrFinancePostingCommandFactory.SeparationSettlementReleased(blank, blankLines, "SEP-3", Guid.NewGuid()).SkipReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void SeparationService_PostsOnAuditApprovalOnly_AndGuardsTheLines()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "HR", "SeparationService.cs"));

        Between(source, "public async Task<SeparationSettlementDto> ApproveSettlementReviewAsync(", "public async Task<SeparationSettlementDto> ReturnSettlementAsync(")
            .Should().Contain("_financePosting.RunAsync").And.Contain("HrFinancePostingCommandFactory.SeparationSettlementReleased(");
        Between(source, "public async Task<SeparationSettlementDto> FinaliseSettlementAsync(", "public async Task<SeparationSettlementDto> ApproveSettlementReviewAsync(")
            .Should().NotContain("_financePosting.RunAsync", "the register says: the accounting event is the release, not the finalisation");
        Between(source, "public async Task<SeparationSettlementDto> ReturnSettlementAsync(", "private async Task<(EmployeeSeparation Separation, SeparationSettlement Settlement)>")
            .Should().Contain("EnsureNotPostedAsync");
        source.Should().Contain("\"Adding a settlement line\"");
        source.Split("\"Changing a settlement line\"").Length.Should().Be(3, "both the line update and the line delete are guarded");
        source.Should().NotContain("IJournalEntryService");
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

            if (definition.Kind == HrFinancePostingKind.CustomerInvoice)
            {
                // An AR hand-off carries exactly one revenue leg; the debit is Finance's receivable.
                definition.DebitRoles.Should().BeEmpty($"{definition.Code}: HR never names the receivable side of a customer invoice");
                command.Lines.Should().ContainSingle(l => !l.IsDebit && l.Amount > 0m, $"{definition.Code}");
                command.Lines.Select(l => l.Role).Should().BeSubsetOf(definition.CreditRoles, $"{definition.Code} credit roles");
                command.PayeeCustomerId.Should().NotBeNull($"{definition.Code}: the sample names a customer");
                continue;
            }

            if (definition.Kind == HrFinancePostingKind.VendorInvoice)
            {
                // An AP hand-off carries exactly one expense leg; the credit is Finance's AP control.
                definition.CreditRoles.Should().BeEmpty($"{definition.Code}: HR never names the payable side of a vendor invoice");
                command.Lines.Should().ContainSingle(l => l.IsDebit && l.Amount > 0m, $"{definition.Code}");
                command.Lines.Select(l => l.Role).Should().BeSubsetOf(definition.DebitRoles, $"{definition.Code} debit roles");
                command.PayeeSupplierId.Should().NotBeNull($"{definition.Code}: the sample names a supplier");
                continue;
            }

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
            // A journal names both sides; an AP invoice only its expense line (the payable is Finance's);
            // an AR invoice only its revenue line (the receivable is Finance's). (An expression tree
            // cannot hold a switch expression, hence the conditional chain.)
            && (e.Kind == HrFinancePostingKind.VendorInvoice ? e.DebitRoles.Count > 0 && e.CreditRoles.Count == 0
                : e.Kind == HrFinancePostingKind.CustomerInvoice ? e.DebitRoles.Count == 0 && e.CreditRoles.Count > 0
                : e.DebitRoles.Count > 0 && e.CreditRoles.Count > 0));

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
