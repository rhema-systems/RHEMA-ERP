using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Api.Services.Finance.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class SubledgerSettlementReadModelFoundationTests
{
    private static readonly DateTime AsOfDate = new(2026, 7, 31);
    private static readonly DateTime InvoiceDate = new(2026, 7, 10);
    private static readonly DateTime SettlementDate = new(2026, 7, 20);

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "AccountsPayable")]
    public async Task ApPostedInvoiceAppearsInSettlementReadModel()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        var invoice = SeedPostedApInvoice(db, fixture, "AP-001", 100m);
        await db.SaveChangesAsync();

        var service = CreateSettlementService(db, tenantId);
        var result = await service.RebuildAsync(new SubledgerSettlementRebuildRequestDto { SourceModule = "AP", AsOfDate = AsOfDate });

        result.ApDocumentCount.Should().Be(1);
        var balance = (await service.GetBalancesAsync("AP", AsOfDate)).Should().ContainSingle().Subject;
        balance.SourceDocumentId.Should().Be(invoice.Id);
        balance.OriginalDocumentAmount.Should().Be(100m);
        balance.OutstandingAmount.Should().Be(100m);
        balance.SourcePostingEventId.Should().NotBeNull();
        balance.SourceJournalEntryId.Should().Be(invoice.JournalEntryId);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ArPostedInvoiceAppearsInSettlementReadModel()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        var invoice = SeedPostedArInvoice(db, fixture, "AR-001", 100m);
        await db.SaveChangesAsync();

        var service = CreateSettlementService(db, tenantId);
        var result = await service.RebuildAsync(new SubledgerSettlementRebuildRequestDto { SourceModule = "AR", AsOfDate = AsOfDate });

        result.ArDocumentCount.Should().Be(1);
        var balance = (await service.GetBalancesAsync("AR", AsOfDate)).Should().ContainSingle().Subject;
        balance.SourceDocumentId.Should().Be(invoice.Id);
        balance.OriginalDocumentAmount.Should().Be(100m);
        balance.OutstandingAmount.Should().Be(100m);
        balance.SourcePostingEventId.Should().NotBeNull();
        balance.SourceJournalEntryId.Should().Be(invoice.JournalEntryId);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "Reporting")]
    public async Task UnpostedApAndArInvoicesAreExcluded()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        SeedApInvoice(db, fixture, "AP-UNPOSTED", 100m);
        SeedArInvoice(db, fixture, "AR-UNPOSTED", 100m);
        await db.SaveChangesAsync();

        var service = CreateSettlementService(db, tenantId);
        var result = await service.RebuildAsync(new SubledgerSettlementRebuildRequestDto { SourceModule = "Both", AsOfDate = AsOfDate });

        result.ApDocumentCount.Should().Be(0);
        result.ArDocumentCount.Should().Be(0);
        (await service.GetBalancesAsync("AP", AsOfDate)).Should().BeEmpty();
        (await service.GetBalancesAsync("AR", AsOfDate)).Should().BeEmpty();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "AccountsPayable")]
    public async Task PartialApPaymentReducesOutstandingAndHandlesWithholding()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        var invoice = SeedPostedApInvoice(db, fixture, "AP-002", 100m, invoice => invoice.PaidAmount = 40m);
        var payment = SeedPostedApPayment(db, fixture, invoice, "APP-001", allocatedAmount: 40m, withholdingAmount: 10m);
        await db.SaveChangesAsync();

        var balance = (await CreateSettlementService(db, tenantId)
            .GetBalancesAfterRebuildAsync("AP", AsOfDate)).Should().ContainSingle().Subject;

        balance.SettledAmount.Should().Be(40m);
        balance.WithheldAmount.Should().Be(10m);
        balance.OutstandingAmount.Should().Be(50m);
        balance.OperationalVariance.Should().Be(-10m);
        balance.DiagnosticFlags.Should().Contain("OperationalSnapshotVariance");
        var application = balance.Applications.Should().ContainSingle().Subject;
        application.SettlementSourceId.Should().Be(payment.Payment.Id);
        application.WithheldAmount.Should().Be(10m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "AccountsReceivable")]
    public async Task PartialArReceiptReducesOutstandingAndReportsWithholding()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        var invoice = SeedPostedArInvoice(db, fixture, "AR-002", 100m, invoice => invoice.PaidAmount = 40m);
        var receipt = SeedPostedArReceipt(db, fixture, invoice, "ARR-001", allocatedAmount: 40m, withholdingAmount: 5m, vatWithholdingAmount: 5m);
        await db.SaveChangesAsync();

        var balance = (await CreateSettlementService(db, tenantId)
            .GetBalancesAfterRebuildAsync("AR", AsOfDate)).Should().ContainSingle().Subject;

        balance.SettledAmount.Should().Be(30m);
        balance.WithheldAmount.Should().Be(10m);
        balance.OutstandingAmount.Should().Be(60m);
        balance.OperationalVariance.Should().Be(0m);
        var application = balance.Applications.Should().ContainSingle().Subject;
        application.SettlementSourceId.Should().Be(receipt.Payment.Id);
        application.SettledAmount.Should().Be(30m);
        application.WithheldAmount.Should().Be(10m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "AccountsPayable")]
    public async Task FullApPaymentClosesOutstanding()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        var invoice = SeedPostedApInvoice(db, fixture, "AP-003", 100m, invoice => invoice.PaidAmount = 100m);
        SeedPostedApPayment(db, fixture, invoice, "APP-002", allocatedAmount: 100m);
        await db.SaveChangesAsync();

        var balance = (await CreateSettlementService(db, tenantId)
            .GetBalancesAfterRebuildAsync("AP", AsOfDate)).Should().ContainSingle().Subject;

        balance.OutstandingAmount.Should().Be(0m);
        balance.SettlementStatus.Should().Be(SubledgerSettlementStatuses.Settled);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "AccountsReceivable")]
    public async Task FullArReceiptClosesOutstanding()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        var invoice = SeedPostedArInvoice(db, fixture, "AR-003", 100m, invoice => invoice.PaidAmount = 100m);
        SeedPostedArReceipt(db, fixture, invoice, "ARR-002", allocatedAmount: 100m);
        await db.SaveChangesAsync();

        var balance = (await CreateSettlementService(db, tenantId)
            .GetBalancesAfterRebuildAsync("AR", AsOfDate)).Should().ContainSingle().Subject;

        balance.OutstandingAmount.Should().Be(0m);
        balance.SettlementStatus.Should().Be(SubledgerSettlementStatuses.Settled);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ArCreditNoteReducesOutstanding()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        var invoice = SeedPostedArInvoice(db, fixture, "AR-004", 100m, invoice => invoice.CreditedAmount = 25m);
        var creditNote = SeedPostedSalesCreditNote(db, fixture, invoice, "CN-001", 25m);
        await db.SaveChangesAsync();

        var balance = (await CreateSettlementService(db, tenantId)
            .GetBalancesAfterRebuildAsync("AR", AsOfDate)).Should().ContainSingle().Subject;

        balance.CreditedAmount.Should().Be(25m);
        balance.OutstandingAmount.Should().Be(75m);
        balance.Applications.Should().ContainSingle(a =>
            a.SettlementSourceType == "SalesCreditNote" &&
            a.SettlementSourceId == creditNote.Id &&
            a.CreditedAmount == 25m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "AccountsReceivable")]
    public async Task SalesCreditNoteWithLegacyConflictingReferencesUsesItsAppliedInvoiceOnly()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        var originalInvoice = SeedPostedArInvoice(db, fixture, "AR-005", 100m);
        var appliedInvoice = SeedPostedArInvoice(db, fixture, "AR-006", 100m);
        var creditNote = SeedPostedSalesCreditNote(db, fixture, originalInvoice, "CN-002", 25m);
        creditNote.AppliedToInvoiceId = appliedInvoice.Id;
        await db.SaveChangesAsync();

        var result = await CreateSettlementService(db, tenantId)
            .RebuildAsync(new SubledgerSettlementRebuildRequestDto
            {
                SourceModule = "AR",
                AsOfDate = AsOfDate
            });

        var balances = await db.SubledgerSettlementBalances
            .Where(b => b.SourceModule == "AR")
            .ToListAsync();
        balances.Single(b => b.SourceDocumentId == originalInvoice.Id).CreditedAmount.Should().Be(0m);
        balances.Single(b => b.SourceDocumentId == appliedInvoice.Id).CreditedAmount.Should().Be(25m);
        result.Diagnostics.Should().Contain(d => d.Code == "SalesCreditNoteTargetConflict" && d.SourceDocumentId == creditNote.Id);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "MultiCurrency")]
    public async Task ForeignCurrencyApSettlementUsesPostedSnapshotsAndFxLink()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        var invoice = SeedPostedApInvoice(db, fixture, "AP-FX-001", 100m, i =>
        {
            i.CurrencyCode = "USD";
            i.ExchangeRate = 12m;
            i.BaseCurrencyAmount = 1200m;
        }, functionalAmount: 1200m);
        var payment = SeedPostedApPayment(db, fixture, invoice, "APP-FX-001", 50m, functionalAmount: 650m);
        var fx = SeedFxSettlement(db, tenantId, "AP", payment.Allocation.Id, invoice.Id, payment.Payment.Id, fixture.ApControl.Id, fixture.FxGainLoss.Id);
        await db.SaveChangesAsync();

        var balance = (await CreateSettlementService(db, tenantId)
            .GetBalancesAfterRebuildAsync("AP", AsOfDate)).Should().ContainSingle().Subject;

        balance.DocumentCurrencyCode.Should().Be("USD");
        balance.FunctionalCurrencyCode.Should().Be("GHS");
        balance.OriginalFunctionalAmount.Should().Be(1200m);
        balance.Applications.Should().ContainSingle().Subject.FxRealizedSettlementId.Should().Be(fx.Id);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "MultiCurrency")]
    public async Task ForeignCurrencyArSettlementUsesPostedSnapshotsAndFxLink()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        var invoice = SeedPostedArInvoice(db, fixture, "AR-FX-001", 100m, i =>
        {
            i.CurrencyCode = "USD";
            i.ExchangeRate = 12m;
            i.BaseCurrencyAmount = 1200m;
        }, functionalAmount: 1200m);
        var receipt = SeedPostedArReceipt(db, fixture, invoice, "ARR-FX-001", 50m, functionalAmount: 650m);
        var fx = SeedFxSettlement(db, tenantId, "AR", receipt.Allocation.Id, invoice.Id, receipt.Payment.Id, fixture.ArControl.Id, fixture.FxGainLoss.Id);
        await db.SaveChangesAsync();

        var balance = (await CreateSettlementService(db, tenantId)
            .GetBalancesAfterRebuildAsync("AR", AsOfDate)).Should().ContainSingle().Subject;

        balance.DocumentCurrencyCode.Should().Be("USD");
        balance.FunctionalCurrencyCode.Should().Be("GHS");
        balance.OriginalFunctionalAmount.Should().Be(1200m);
        balance.Applications.Should().ContainSingle().Subject.FxRealizedSettlementId.Should().Be(fx.Id);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "TenantIsolation")]
    public async Task CrossTenantAllocationsAreDiagnosedAndExcluded()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        var invoice = SeedPostedApInvoice(db, fixture, "AP-XTENANT", 100m);
        SeedTenant(db, otherTenantId, "OTH");
        db.Set<VendorPaymentAllocation>().Add(new VendorPaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            VendorInvoiceId = invoice.Id,
            VendorPaymentId = Guid.NewGuid(),
            AllocatedAmount = 100m,
            AllocationDate = SettlementDate
        });
        await db.SaveChangesAsync();

        var service = CreateSettlementService(db, tenantId);
        var result = await service.RebuildAsync(new SubledgerSettlementRebuildRequestDto { SourceModule = "AP", AsOfDate = AsOfDate });
        var balance = (await service.GetBalancesAsync("AP", AsOfDate)).Should().ContainSingle().Subject;

        result.Diagnostics.Should().Contain(d => d.Code == "CrossTenantAllocation");
        balance.Applications.Should().BeEmpty();
        balance.OutstandingAmount.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "AccountsPayable")]
    public async Task ApAgingUsesSettlementReadModelInsteadOfMutablePaidField()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        var invoice = SeedPostedApInvoice(db, fixture, "AP-AGING", 100m, i => i.PaidAmount = 100m);
        SeedPostedApPayment(db, fixture, invoice, "APP-AGING", 40m);
        await db.SaveChangesAsync();

        var audit = new CapturingFinanceAuditService();
        var service = CreateApReportsService(db, tenantId, audit);
        var report = await service.GetAgingReportAsync(AsOfDate);

        report.UsesSettlementReadModel.Should().BeTrue();
        report.TotalOutstanding.Should().Be(60m);
        report.TotalInvoices.Should().Be(1);
        report.Diagnostics.Should().Contain(d => d.Code == "OperationalSnapshotVariance");
        audit.EventTypes.Should().Contain(FinanceAuditEvents.ApAgingGeneratedFromSettlementReadModel);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ArAgingUsesSettlementReadModelInsteadOfMutablePaidFields()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        var invoice = SeedPostedArInvoice(db, fixture, "AR-AGING", 100m, i => i.PaidAmount = 100m);
        SeedPostedArReceipt(db, fixture, invoice, "ARR-AGING", 40m);
        await db.SaveChangesAsync();

        var audit = new CapturingFinanceAuditService();
        var service = CreateArReportsService(db, tenantId, audit);
        var report = await service.GetAgingReportAsync(AsOfDate);

        report.UsesSettlementReadModel.Should().BeTrue();
        report.Summary.GrandTotal.Should().Be(60m);
        report.Customers.Should().ContainSingle();
        report.Diagnostics.Should().Contain(d => d.Code == "OperationalSnapshotVariance");
        audit.EventTypes.Should().Contain(FinanceAuditEvents.ArAgingGeneratedFromSettlementReadModel);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "AccountsPayable")]
    public async Task ApControlReconciliationShowsZeroVarianceForCleanData()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        var invoice = SeedPostedApInvoice(db, fixture, "AP-CTRL", 100m, i => i.PaidAmount = 40m);
        SeedPostedApPayment(db, fixture, invoice, "APP-CTRL", 40m);
        await db.SaveChangesAsync();

        var report = await CreateSettlementService(db, tenantId).GetControlReconciliationAsync("AP", AsOfDate);

        report.ReadModelOutstanding.Should().Be(60m);
        report.PostedGlControlBalance.Should().Be(60m);
        report.Variance.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ArControlReconciliationShowsZeroVarianceForCleanData()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        var invoice = SeedPostedArInvoice(db, fixture, "AR-CTRL", 100m, i => i.PaidAmount = 40m);
        SeedPostedArReceipt(db, fixture, invoice, "ARR-CTRL", 40m);
        await db.SaveChangesAsync();

        var report = await CreateSettlementService(db, tenantId).GetControlReconciliationAsync("AR", AsOfDate);

        report.ReadModelOutstanding.Should().Be(60m);
        report.PostedGlControlBalance.Should().Be(60m);
        report.Variance.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "Diagnostics")]
    public async Task MissingSourceJournalIsDiagnosed()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        SeedPostedApInvoice(db, fixture, "AP-NOJOURNAL", 100m, journalBackReference: false);
        await db.SaveChangesAsync();

        var result = await CreateSettlementService(db, tenantId)
            .RebuildAsync(new SubledgerSettlementRebuildRequestDto { SourceModule = "AP", AsOfDate = AsOfDate });
        var balance = await db.SubledgerSettlementBalances.SingleAsync();

        result.Diagnostics.Should().Contain(d => d.Code == "MissingSourceJournal");
        balance.HasDiagnostics.Should().BeTrue();
        balance.DiagnosticFlags.Should().Contain("MissingSourceJournal");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "Reporting")]
    public async Task RebuildIsIdempotent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        var invoice = SeedPostedArInvoice(db, fixture, "AR-IDEMP", 100m, i => i.PaidAmount = 25m);
        SeedPostedArReceipt(db, fixture, invoice, "ARR-IDEMP", 25m);
        await db.SaveChangesAsync();
        var service = CreateSettlementService(db, tenantId);

        await service.RebuildAsync(new SubledgerSettlementRebuildRequestDto { SourceModule = "AR", AsOfDate = AsOfDate });
        await service.RebuildAsync(new SubledgerSettlementRebuildRequestDto { SourceModule = "AR", AsOfDate = AsOfDate });

        (await db.SubledgerSettlementBalances.CountAsync()).Should().Be(1);
        (await db.SubledgerSettlementApplications.CountAsync()).Should().Be(1);
        (await db.SubledgerSettlementBalances.SingleAsync()).OutstandingAmount.Should().Be(75m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerSettlementReadModel")]
    [Trait("Category", "Audit")]
    public async Task RebuildAndControlReportsEmitAuditEvents()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFinanceFixture(db, tenantId);
        SeedPostedApInvoice(db, fixture, "AP-AUDIT", 100m);
        SeedPostedArInvoice(db, fixture, "AR-AUDIT", 100m);
        await db.SaveChangesAsync();
        var audit = new CapturingFinanceAuditService();
        var service = CreateSettlementService(db, tenantId, audit);

        await service.RebuildAsync(new SubledgerSettlementRebuildRequestDto { SourceModule = "Both", AsOfDate = AsOfDate });
        await service.GetControlReconciliationAsync("AP", AsOfDate);
        await service.GetControlReconciliationAsync("AR", AsOfDate);

        audit.EventTypes.Should().Contain(FinanceAuditEvents.ApSettlementReadModelRebuilt);
        audit.EventTypes.Should().Contain(FinanceAuditEvents.ArSettlementReadModelRebuilt);
        audit.EventTypes.Should().Contain(FinanceAuditEvents.ApControlReconciliationGenerated);
        audit.EventTypes.Should().Contain(FinanceAuditEvents.ArControlReconciliationGenerated);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"subledger-settlement-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static SubledgerSettlementReadModelService CreateSettlementService(
        ApplicationDbContext db,
        Guid tenantId,
        CapturingFinanceAuditService? audit = null)
        => new(
            db,
            CreateCurrentUser(tenantId).Object,
            Mock.Of<ILogger<SubledgerSettlementReadModelService>>(),
            audit);

    private static ApReportsService CreateApReportsService(
        ApplicationDbContext db,
        Guid tenantId,
        CapturingFinanceAuditService? audit = null)
    {
        var settlementService = CreateSettlementService(db, tenantId, audit);
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
        return new ApReportsService(
            new UnitOfWork(db),
            CreateCurrentUser(tenantId).Object,
            tenantSettings.Object,
            Mock.Of<ILogger<ApReportsService>>(),
            settlementService,
            audit);
    }

    private static ArReportsService CreateArReportsService(
        ApplicationDbContext db,
        Guid tenantId,
        CapturingFinanceAuditService? audit = null)
    {
        var settlementService = CreateSettlementService(db, tenantId, audit);
        return new ArReportsService(
            new UnitOfWork(db),
            CreateCurrentUser(tenantId).Object,
            Mock.Of<ILogger<ArReportsService>>(),
            settlementService,
            audit);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("subledger.settlement.tests");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("settlement-tests");
        return currentUser;
    }

    private static FinanceFixture SeedFinanceFixture(ApplicationDbContext db, Guid tenantId)
    {
        SeedTenant(db, tenantId);
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "July 2026",
            PeriodCode = "2026-07",
            PeriodNumber = 7,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            IsOpen = true,
            IsClosed = false,
            PeriodStatus = "Open"
        };
        db.FiscalPeriods.Add(period);

        var apControl = SeedAccount(db, tenantId, AccountType.Liability, "2000", "AP Control");
        var arControl = SeedAccount(db, tenantId, AccountType.Asset, "1200", "AR Control");
        var expense = SeedAccount(db, tenantId, AccountType.Expense, "5000", "Expense");
        var revenue = SeedAccount(db, tenantId, AccountType.Revenue, "4000", "Revenue");
        var cash = SeedAccount(db, tenantId, AccountType.Asset, "1000", "Cash");
        var fxGainLoss = SeedAccount(db, tenantId, AccountType.Expense, "6999", "FX Gain Loss");

        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountApId = apControl.Id,
            ControlAccountArId = arControl.Id
        });

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierCode = "SUP-001",
            Name = "Settlement Supplier",
            SupplierType = "Vendor",
            DefaultApAccountId = apControl.Id
        };
        db.Suppliers.Add(supplier);

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerCode = "CUS-001",
            CustomerName = "Settlement Customer",
            CustomerType = "Corporate",
            DefaultArAccountId = arControl.Id,
            CurrencyCode = "GHS"
        };
        db.Set<Customer>().Add(customer);

        return new FinanceFixture(tenantId, period, apControl, arControl, expense, revenue, cash, fxGainLoss, supplier, customer);
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string code = "TEN")
        => db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {code}",
            Code = code,
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });

    private static Account SeedAccount(ApplicationDbContext db, Guid tenantId, AccountType type, string number, string name)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = number,
            AccountNumber = number,
            AccountName = name,
            AccountType = type,
            AccountCategory = name,
            IFRSLineItem = name,
            BaseLineItem = name,
            LocalLineItem = name,
            Status = AccountStatus.Active,
            AllowDirectPosting = true
        };
        db.Accounts.Add(account);
        return account;
    }

    private static VendorInvoice SeedApInvoice(ApplicationDbContext db, FinanceFixture fixture, string number, decimal amount)
    {
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            InvoiceNumber = number,
            SupplierId = fixture.Supplier.Id,
            SupplierName = fixture.Supplier.Name,
            InvoiceDate = InvoiceDate,
            DueDate = new DateTime(2026, 8, 9),
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            SubTotal = amount,
            TotalAmount = amount,
            BaseCurrencyAmount = amount,
            Status = VendorInvoiceStatus.Approved,
            ApprovalStatus = "Approved"
        };
        db.VendorInvoices.Add(invoice);
        return invoice;
    }

    private static VendorInvoice SeedPostedApInvoice(
        ApplicationDbContext db,
        FinanceFixture fixture,
        string number,
        decimal amount,
        Action<VendorInvoice>? configure = null,
        decimal? functionalAmount = null,
        bool journalBackReference = true)
    {
        var invoice = SeedApInvoice(db, fixture, number, amount);
        configure?.Invoke(invoice);
        var posted = SeedPostedSource(db, fixture, "AP", "VendorInvoice", invoice.Id, invoice.InvoiceNumber, InvoiceDate, functionalAmount ?? invoice.TotalAmount,
            (fixture.Expense.Id, functionalAmount ?? invoice.TotalAmount, 0m),
            (fixture.ApControl.Id, 0m, functionalAmount ?? invoice.TotalAmount));
        if (journalBackReference)
        {
            invoice.JournalEntryId = posted.Journal.Id;
        }
        else
        {
            posted.Event.JournalEntryId = null;
        }

        return invoice;
    }

    private static (VendorPayment Payment, VendorPaymentAllocation Allocation) SeedPostedApPayment(
        ApplicationDbContext db,
        FinanceFixture fixture,
        VendorInvoice invoice,
        string number,
        decimal allocatedAmount,
        decimal withholdingAmount = 0m,
        decimal functionalAmount = 0m)
    {
        functionalAmount = functionalAmount == 0m ? allocatedAmount + withholdingAmount : functionalAmount;
        var payment = new VendorPayment
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            PaymentNumber = number,
            SupplierId = fixture.Supplier.Id,
            PaymentDate = SettlementDate,
            TotalAmount = allocatedAmount,
            AllocatedAmount = allocatedAmount,
            CurrencyCode = invoice.CurrencyCode,
            ExchangeRate = invoice.ExchangeRate,
            Status = VendorPaymentStatus.Processed
        };
        db.Set<VendorPayment>().Add(payment);

        var allocation = new VendorPaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            VendorPaymentId = payment.Id,
            VendorPayment = payment,
            VendorInvoiceId = invoice.Id,
            AllocatedAmount = allocatedAmount,
            WithholdingTaxAmount = withholdingAmount,
            AllocationDate = SettlementDate
        };
        db.Set<VendorPaymentAllocation>().Add(allocation);
        payment.Allocations.Add(allocation);

        var posted = SeedPostedSource(db, fixture, "AP", "VendorPayment", payment.Id, payment.PaymentNumber, SettlementDate, functionalAmount,
            (fixture.ApControl.Id, functionalAmount, 0m),
            (fixture.Cash.Id, 0m, functionalAmount));
        payment.JournalEntryId = posted.Journal.Id;
        return (payment, allocation);
    }

    private static Invoice SeedArInvoice(ApplicationDbContext db, FinanceFixture fixture, string number, decimal amount)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            InvoiceNumber = number,
            BusinessPartnerId = fixture.Customer.Id,
            CustomerName = fixture.Customer.CustomerName,
            InvoiceDate = InvoiceDate,
            DueDate = new DateTime(2026, 8, 9),
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            SubTotal = amount,
            TotalAmount = amount,
            BaseCurrencyAmount = amount,
            Status = InvoiceStatus.Sent
        };
        db.Invoices.Add(invoice);
        return invoice;
    }

    private static Invoice SeedPostedArInvoice(
        ApplicationDbContext db,
        FinanceFixture fixture,
        string number,
        decimal amount,
        Action<Invoice>? configure = null,
        decimal? functionalAmount = null)
    {
        var invoice = SeedArInvoice(db, fixture, number, amount);
        configure?.Invoke(invoice);
        var posted = SeedPostedSource(db, fixture, "AR", "CustomerInvoice", invoice.Id, invoice.InvoiceNumber, InvoiceDate, functionalAmount ?? invoice.TotalAmount,
            (fixture.ArControl.Id, functionalAmount ?? invoice.TotalAmount, 0m),
            (fixture.Revenue.Id, 0m, functionalAmount ?? invoice.TotalAmount));
        invoice.JournalEntryId = posted.Journal.Id;
        return invoice;
    }

    private static (CustomerPayment Payment, PaymentAllocation Allocation) SeedPostedArReceipt(
        ApplicationDbContext db,
        FinanceFixture fixture,
        Invoice invoice,
        string number,
        decimal allocatedAmount,
        decimal withholdingAmount = 0m,
        decimal vatWithholdingAmount = 0m,
        decimal functionalAmount = 0m)
    {
        functionalAmount = functionalAmount == 0m ? allocatedAmount : functionalAmount;
        var payment = new CustomerPayment
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            PaymentNumber = number,
            CustomerId = fixture.Customer.Id,
            PaymentDate = SettlementDate,
            TotalAmount = allocatedAmount - withholdingAmount - vatWithholdingAmount,
            AllocatedAmount = allocatedAmount,
            CurrencyCode = invoice.CurrencyCode,
            ExchangeRate = invoice.ExchangeRate,
            WithholdingTaxAmount = withholdingAmount,
            VatWithholdingAmount = vatWithholdingAmount,
            Status = "Posted"
        };
        db.Set<CustomerPayment>().Add(payment);

        var allocation = new PaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            CustomerPaymentId = payment.Id,
            CustomerPayment = payment,
            InvoiceId = invoice.Id,
            AllocatedAmount = allocatedAmount,
            AllocationDate = SettlementDate
        };
        db.Set<PaymentAllocation>().Add(allocation);
        payment.Allocations.Add(allocation);

        var posted = SeedPostedSource(db, fixture, "AR", "CustomerPayment", payment.Id, payment.PaymentNumber, SettlementDate, functionalAmount,
            (fixture.Cash.Id, functionalAmount, 0m),
            (fixture.ArControl.Id, 0m, functionalAmount));
        payment.JournalEntryId = posted.Journal.Id;
        return (payment, allocation);
    }

    private static CreditNote SeedPostedSalesCreditNote(
        ApplicationDbContext db,
        FinanceFixture fixture,
        Invoice invoice,
        string number,
        decimal amount)
    {
        var creditNote = new CreditNote
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            CustomerId = fixture.Customer.Id,
            DocumentNumber = number,
            DocumentDate = SettlementDate,
            AppliedDate = SettlementDate,
            OriginalInvoiceId = invoice.Id,
            AppliedToInvoiceId = invoice.Id,
            CreditNoteStatus = CreditNoteStatus.Applied,
            TotalAmount = amount,
            Currency = invoice.CurrencyCode,
            ExchangeRate = invoice.ExchangeRate,
            ApprovalStatus = "Approved"
        };
        db.CreditNotes.Add(creditNote);

        var posted = SeedPostedSource(db, fixture, "AR", "SalesCreditNote", creditNote.Id, creditNote.DocumentNumber, SettlementDate, amount,
            (fixture.Revenue.Id, amount, 0m),
            (fixture.ArControl.Id, 0m, amount));
        creditNote.JournalEntryId = posted.Journal.Id;
        return creditNote;
    }

    private static FxRealizedSettlement SeedFxSettlement(
        ApplicationDbContext db,
        Guid tenantId,
        string module,
        Guid allocationId,
        Guid invoiceId,
        Guid settlementId,
        Guid controlAccountId,
        Guid gainLossAccountId)
    {
        var fx = new FxRealizedSettlement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = module,
            SettlementDocumentType = module == "AP" ? "VendorPayment" : "CustomerPayment",
            SettlementDocumentId = settlementId,
            SettlementAllocationId = allocationId,
            InvoiceDocumentType = module == "AP" ? "VendorInvoice" : "CustomerInvoice",
            InvoiceDocumentId = invoiceId,
            ControlAccountId = controlAccountId,
            TransactionCurrency = "USD",
            FunctionalCurrencyCode = "GHS",
            SettledForeignAmount = 50m,
            HistoricalExchangeRate = 12m,
            SettlementExchangeRate = 13m,
            HistoricalFunctionalAmount = 600m,
            SettlementFunctionalAmount = 650m,
            GainLossAmount = 50m,
            GainLossType = "Gain",
            GainLossAccountId = gainLossAccountId,
            Status = "Posted",
            IdempotencyKey = $"fx-{allocationId}",
            SettlementDate = SettlementDate,
            PostedAt = DateTime.UtcNow
        };
        db.FxRealizedSettlements.Add(fx);
        return fx;
    }

    private static (JournalEntry Journal, FinancePostingEvent Event) SeedPostedSource(
        ApplicationDbContext db,
        FinanceFixture fixture,
        string sourceModule,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string reference,
        DateTime postingDate,
        decimal functionalAmount,
        params (Guid AccountId, decimal Debit, decimal Credit)[] lines)
    {
        var journal = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            JournalEntryNumber = $"JE-{reference}",
            JournalType = "Subledger",
            EntryDate = postingDate,
            Description = reference,
            FiscalPeriodId = fixture.Period.Id,
            PostingStatus = "Posted",
            BookClassification = "IFRS",
            SourceModule = sourceModule,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            TotalDebitAmount = lines.Sum(l => l.Debit),
            TotalCreditAmount = lines.Sum(l => l.Credit),
            IsBalanced = lines.Sum(l => l.Debit) == lines.Sum(l => l.Credit)
        };
        db.JournalEntries.Add(journal);

        var lineNumber = 1;
        foreach (var line in lines)
        {
            db.AccountTransactions.Add(new AccountTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = fixture.TenantId,
                JournalEntryId = journal.Id,
                AccountId = line.AccountId,
                FiscalPeriodId = fixture.Period.Id,
                TransactionDate = postingDate,
                DebitAmount = line.Debit,
                CreditAmount = line.Credit,
                PostingStatus = "Posted",
                BookClassification = "IFRS",
                LineNumber = lineNumber++,
                FunctionalCurrencyCode = "GHS",
                TransactionCurrency = "GHS",
                SourceModule = sourceModule,
                SourceDocumentType = sourceDocumentType,
                SourceDocumentId = sourceDocumentId,
                SourceReferenceNumber = reference
            });
        }

        var postingEvent = new FinancePostingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            SourceModule = sourceModule,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            SourceDocumentReference = reference,
            PostingAction = "Post",
            PostingStatus = "Posted",
            PostingDate = postingDate,
            PostedAt = DateTime.UtcNow,
            JournalEntryId = journal.Id,
            FunctionalCurrencyCode = "GHS",
            TotalDebitAmount = functionalAmount,
            TotalCreditAmount = functionalAmount,
            BookClassification = "IFRS"
        };
        db.FinancePostingEvents.Add(postingEvent);
        return (journal, postingEvent);
    }

    private sealed record FinanceFixture(
        Guid TenantId,
        FiscalPeriod Period,
        Account ApControl,
        Account ArControl,
        Account Expense,
        Account Revenue,
        Account Cash,
        Account FxGainLoss,
        Supplier Supplier,
        Customer Customer);

    private sealed class CapturingFinanceAuditService : IFinanceAuditService
    {
        public List<FinanceAuditEventDto> Events { get; } = new();

        public IEnumerable<string> EventTypes => Events.Select(e => e.EventType);

        public Task<AuditLog> RecordAsync(FinanceAuditEventDto auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.FromResult(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = auditEvent.TenantId,
                UserId = Guid.NewGuid(),
                Username = "subledger.settlement.tests",
                Action = auditEvent.EventType,
                Resource = auditEvent.Resource ?? auditEvent.SourceDocumentType ?? "Finance",
                ResourceId = auditEvent.ResourceId,
                IpAddress = "127.0.0.1",
                UserAgent = "settlement-tests"
            });
        }

        public Task<IReadOnlyList<AuditLog>> GetAuditTrailAsync(
            Guid tenantId,
            string resource,
            string resourceId,
            int limit = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditLog>>(Array.Empty<AuditLog>());
    }
}

internal static class SubledgerSettlementReadModelTestExtensions
{
    public static async Task<IReadOnlyList<SubledgerSettlementBalance>> GetBalancesAfterRebuildAsync(
        this ISubledgerSettlementReadModelService service,
        string module,
        DateTime asOfDate)
    {
        await service.RebuildAsync(new SubledgerSettlementRebuildRequestDto
        {
            SourceModule = module,
            AsOfDate = asOfDate
        });

        return await service.GetBalancesAsync(module, asOfDate);
    }
}
