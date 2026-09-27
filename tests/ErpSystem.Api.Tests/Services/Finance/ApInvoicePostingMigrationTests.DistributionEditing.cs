using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed partial class ApInvoicePostingMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DistributionEdit_PostedProfileBasedSplits_RequireHistoricalBasisWhenProfileChanges(bool missingProfile)
    {
        var tenant = Guid.NewGuid(); await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenant, ProcurementDistributionDraft);
        fixture.Invoice.LineItems.Single().GLAccountId = null;
        fixture.Invoice.ExpenseAccountId = null;
        var alternate = SeedAccount(db, tenant, "6201", AccountType.Expense);
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenant);
        var input = DistributionInput(await service.GetDistributionAsync(fixture.Invoice.Id));
        var debit = input.Lines.Single(x => x.Debit > 0); debit.Debit = 60m;
        input.Lines.Add(new() { LineId = Guid.NewGuid(), GroupId = debit.GroupId, AccountId = alternate.Id, Debit = 40m });
        await service.SaveDistributionAsync(fixture.Invoice.Id, input);
        var invoice = await db.VendorInvoices.SingleAsync(x => x.Id == fixture.Invoice.Id);
        invoice.Status = VendorInvoiceStatus.Approved; invoice.ApprovalStatus = "Approved";
        invoice.ApprovedById = Guid.NewGuid(); invoice.ApprovedDate = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var posted = await service.PostAsync(invoice.Id);
        var profile = await db.Set<BusinessPartnerApProfileVersion>().SingleAsync(x => x.Id == invoice.BusinessPartnerApProfileVersionId);
        if (missingProfile) profile.IsDeleted = true;
        else profile.DefaultExpenseAccountId = alternate.Id;
        await db.SaveChangesAsync();

        Func<Task> post = () => service.PostAsync(invoice.Id);
        await post.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP_INVOICE_DISTRIBUTION_HISTORY_REQUIRED:*");

        (await db.JournalEntries.CountAsync(x => x.SourceDocumentId == invoice.Id)).Should().Be(1);
        (await db.AccountTransactions.CountAsync(x => x.JournalEntryId == posted.JournalEntryId)).Should().Be(3);
        (await db.FinancePostingEvents.CountAsync(x => x.SourceDocumentId == invoice.Id)).Should().Be(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0.5)]
    public async Task DistributionEdit_PostedSplits_ShouldSupportLinkedSupplierCredit(decimal quantity)
    {
        var tenant = Guid.NewGuid(); await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenant, ProcurementDistributionDraft);
        var alternate = SeedAccount(db, tenant, "SPLIT-CREDIT", AccountType.Expense);
        var partner = fixture.Supplier;
        await db.SaveChangesAsync();
        var (invoiceService, _) = CreateService(db, tenant);
        var input = DistributionInput(await invoiceService.GetDistributionAsync(fixture.Invoice.Id));
        var debit = input.Lines.Single(row => row.Debit > 0); debit.Debit = 60;
        input.Lines.Add(new() { LineId = Guid.NewGuid(), GroupId = debit.GroupId, AccountId = alternate.Id, Debit = 40 });
        // A user may also split a protected control row, but the immutable journal retains one control transaction.
        var credit = input.Lines.Single(row => row.Credit > 0); credit.Credit = 25;
        input.Lines.Add(new() { LineId = Guid.NewGuid(), GroupId = credit.GroupId, AccountId = credit.AccountId, Credit = 75 });
        await invoiceService.SaveDistributionAsync(fixture.Invoice.Id, input);
        var invoice = await db.VendorInvoices.Include(row => row.LineItems).SingleAsync();
        invoice.Status = VendorInvoiceStatus.Approved; invoice.ApprovalStatus = "Approved";
        invoice.ApprovedById = Guid.NewGuid(); invoice.ApprovedDate = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await invoiceService.PostAsync(invoice.Id);
        invoice = await db.VendorInvoices.Include(row => row.LineItems).SingleAsync();
        (await db.AccountTransactions.CountAsync(row => row.JournalEntryId == invoice.JournalEntryId && row.TransactionTag == "AP-Control"))
            .Should().Be(1);
        var service = WriteoffService(db, tenant);
        // Posting clears the unit-of-work tracker; reuse the reloaded partner graph.
        partner = await db.BusinessPartners.SingleAsync(row => row.Id == partner.Id);
        var note = WriteoffNote(tenant, partner);
        note.BusinessPartnerRoleId = invoice.BusinessPartnerRoleId;
        note.BusinessPartnerApProfileVersionId = invoice.BusinessPartnerApProfileVersionId;
        note.OriginalVendorInvoiceId = invoice.Id; note.OriginalVendorInvoice = invoice;
        var source = invoice.LineItems.Single();
        var dto = new CreateSupplierDebitNoteLineItemDto
        {
            Description = "Supplier credit for split invoice", LineItemType = source.LineItemType,
            OriginalVendorInvoiceLineItemId = source.Id, Quantity = quantity, UnitPrice = source.UnitPrice
        };
        var replace = typeof(SupplierDebitNoteService).GetMethod("ReplaceLinesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        await (Task)replace.Invoke(service, [note, new[] { dto }, invoice, CancellationToken.None])!;
        note.Status = SupplierDebitNoteStatus.Approved; note.ApprovedById = Guid.NewGuid(); note.ApprovedAt = DateTime.UtcNow;
        db.SupplierDebitNotes.Add(note); await db.SaveChangesAsync();
        var posted = await service.PostAsync(note.Id);
        var retry = await service.PostAsync(note.Id);
        retry.JournalEntryId.Should().Be(posted.JournalEntryId);
        var journal = await db.AccountTransactions.Where(row => row.JournalEntryId == posted.JournalEntryId).ToListAsync();
        journal.Should().HaveCount(3);
        journal.Single(row => row.AccountId == fixture.ExpenseAccount.Id).CreditAmount.Should().Be(60 * quantity);
        journal.Single(row => row.AccountId == alternate.Id).CreditAmount.Should().Be(40 * quantity);
        journal.Single(row => row.AccountId == fixture.ApAccount.Id).DebitAmount.Should().Be(100 * quantity);
        journal.Select(row => row.LineNumber).Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    private static void ProcurementDistributionDraft(VendorInvoice invoice)
    {
        invoice.Status = VendorInvoiceStatus.Draft;
        invoice.ApprovalStatus = "Draft";
        invoice.ApprovedById = null; invoice.ApprovedDate = null;
        invoice.AcceptedSupplyKind = ProcurementAcceptedSupplyKind.WorksPaymentCertificate;
        invoice.AcceptedSupplySourceId = Guid.NewGuid();
    }

    private static SaveVendorInvoiceDistributionDto DistributionInput(VendorInvoiceDistributionDto source) => new()
    {
        Version = source.Version, BasisVersion = source.BasisVersion,
        Lines = source.Lines.Select(line => new SaveVendorInvoiceDistributionLineDto
        {
            LineId = Guid.Parse(line.LineId), GroupId = line.GroupId, AccountId = line.AccountId,
            Debit = line.Debit, Credit = line.Credit
        }).ToList()
    };

    [Fact]
    public async Task DistributionEdit_SaveReloadPostRetry_ShouldUseSavedSplitsAndLockPostedJournal()
    {
        var tenantId = Guid.NewGuid(); await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, ProcurementDistributionDraft);
        var alternate = SeedAccount(db, tenantId, "6200", AccountType.Expense);
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);
        var initial = await service.GetDistributionAsync(fixture.Invoice.Id);
        initial.CanEdit.Should().BeTrue();
        var input = DistributionInput(initial);
        var debit = input.Lines.Single(line => line.Debit > 0); debit.Debit = 60;
        input.Lines.Add(new() { LineId = Guid.NewGuid(), GroupId = debit.GroupId, AccountId = alternate.Id, Debit = 40 });
        var saved = await service.SaveDistributionAsync(fixture.Invoice.Id, input);
        saved.HasOverrides.Should().BeTrue(); saved.NeedsReview.Should().BeFalse();
        saved.Lines.Should().HaveCount(3);
        (await db.AccountTransactions.AnyAsync()).Should().BeFalse("saving does not post");
        db.ChangeTracker.Clear();
        var reloaded = await service.GetDistributionAsync(fixture.Invoice.Id);
        reloaded.Lines.Single(line => line.AccountId == alternate.Id).Debit.Should().Be(40);
        var approved = await db.VendorInvoices.SingleAsync(value => value.Id == fixture.Invoice.Id);
        approved.Status = VendorInvoiceStatus.Approved; approved.ApprovalStatus = "Approved";
        approved.ApprovedById = Guid.NewGuid(); approved.ApprovedDate = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var posted = await service.PostAsync(approved.Id);
        var retry = await service.PostAsync(approved.Id);
        retry.JournalEntryId.Should().Be(posted.JournalEntryId);
        var journal = await db.AccountTransactions.Where(value => value.JournalEntryId == posted.JournalEntryId).ToListAsync();
        journal.Should().HaveCount(3);
        journal.Single(line => line.AccountId == alternate.Id).DebitAmount.Should().Be(40);
        journal.Single(line => line.AccountId == alternate.Id).SourceDocumentLineId.Should().Be(fixture.Invoice.LineItems.Single().Id);
        journal.Sum(line => line.DebitAmount).Should().Be(journal.Sum(line => line.CreditAmount));
        (await service.GetDistributionAsync(approved.Id)).CanEdit.Should().BeFalse();
        await ((Func<Task>)(() => service.SaveDistributionAsync(approved.Id, DistributionInput(saved))))
            .Should().ThrowAsync<InvoiceDistributionConflictException>();
        (await db.FinancePostingEvents.CountAsync(value => value.SourceDocumentId == approved.Id && value.PostingAction == "Post"))
            .Should().Be(1);
    }

    [Theory]
    [InlineData("unbalanced")]
    [InlineData("both-sides")]
    [InlineData("precision")]
    [InlineData("balanced-inflation")]
    [InlineData("control-account")]
    [InlineData("foreign-tenant")]
    [InlineData("inactive")]
    [InlineData("wrong-type")]
    [InlineData("duplicate-id")]
    [InlineData("missing-group")]
    public async Task DistributionEdit_InvalidInput_ShouldNeverSaveOrPost(string scenario)
    {
        var tenantId = Guid.NewGuid(); await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, ProcurementDistributionDraft);
        var alternate = scenario == "foreign-tenant"
            ? new Account { TenantId = Guid.NewGuid(), AccountNumber = "FOREIGN", AccountName = "Foreign account", AccountType = AccountType.Expense, Status = AccountStatus.Active, AllowDirectPosting = true }
            : SeedAccount(db, tenantId, "INVALID-SPLIT", scenario == "wrong-type" ? AccountType.Revenue : AccountType.Expense);
        if (scenario == "foreign-tenant") db.Accounts.Add(alternate);
        if (scenario == "inactive") alternate.Status = AccountStatus.Inactive;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);
        var input = DistributionInput(await service.GetDistributionAsync(fixture.Invoice.Id));
        var debit = input.Lines.Single(line => line.Debit > 0);
        var credit = input.Lines.Single(line => line.Credit > 0);
        switch (scenario)
        {
            case "unbalanced": debit.Debit = 99; break;
            case "both-sides": debit.Credit = 1; break;
            case "precision": debit.Debit = 100.001m; break;
            case "balanced-inflation": debit.Debit = credit.Credit = 101; break;
            case "control-account": credit.AccountId = alternate.Id; break;
            case "duplicate-id": credit.LineId = debit.LineId; break;
            case "missing-group": debit.GroupId = "unknown"; break;
            default: debit.AccountId = alternate.Id; break;
        }
        await ((Func<Task>)(() => service.SaveDistributionAsync(fixture.Invoice.Id, input))).Should().ThrowAsync<InvalidOperationException>();
        (await db.VendorInvoices.AsNoTracking().SingleAsync(value => value.Id == fixture.Invoice.Id)).DistributionDraftJson.Should().BeNull();
        (await db.AccountTransactions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task DistributionEdit_StaleVersionAndChangedBasis_ShouldRequireReviewBeforePosting()
    {
        var tenantId = Guid.NewGuid(); await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, ProcurementDistributionDraft);
        var (service, _) = CreateService(db, tenantId);
        var input = DistributionInput(await service.GetDistributionAsync(fixture.Invoice.Id));
        await service.SaveDistributionAsync(fixture.Invoice.Id, input);
        await ((Func<Task>)(() => service.SaveDistributionAsync(fixture.Invoice.Id, input))).Should().ThrowAsync<InvoiceDistributionConflictException>();
        var invoice = await db.VendorInvoices.Include(value => value.LineItems).SingleAsync();
        invoice.TotalAmount = invoice.SubTotal = 120; invoice.LineItems.Single().UnitPrice = 120;
        await db.SaveChangesAsync();
        var review = await service.GetDistributionAsync(invoice.Id);
        review.NeedsReview.Should().BeTrue(); review.TotalDebit.Should().Be(120);
        invoice.Status = VendorInvoiceStatus.Approved; invoice.ApprovalStatus = "Approved";
        invoice.ApprovedById = Guid.NewGuid(); invoice.ApprovedDate = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await ((Func<Task>)(() => service.PostAsync(invoice.Id))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*Review and save Distribution*");
        (await db.AccountTransactions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task DistributionEdit_AccountDisabledAfterSave_ShouldBlockPosting()
    {
        var tenantId = Guid.NewGuid(); await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, ProcurementDistributionDraft);
        var alternate = SeedAccount(db, tenantId, "EDITABLE", AccountType.Expense); await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);
        var input = DistributionInput(await service.GetDistributionAsync(fixture.Invoice.Id));
        input.Lines.Single(line => line.Debit > 0).AccountId = alternate.Id;
        await service.SaveDistributionAsync(fixture.Invoice.Id, input);
        var invoice = await db.VendorInvoices.SingleAsync();
        var account = await db.Accounts.SingleAsync(value => value.Id == alternate.Id);
        account.Status = AccountStatus.Inactive;
        invoice.Status = VendorInvoiceStatus.Approved; invoice.ApprovalStatus = "Approved";
        invoice.ApprovedById = Guid.NewGuid(); invoice.ApprovedDate = DateTime.UtcNow; await db.SaveChangesAsync();
        await ((Func<Task>)(() => service.PostAsync(invoice.Id))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*active posting accounts*");
        (await db.AccountTransactions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task DistributionEdit_Reset_ShouldClearSavedSplitsWithoutPosting()
    {
        var tenantId = Guid.NewGuid(); await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, ProcurementDistributionDraft);
        var (service, _) = CreateService(db, tenantId);
        var saved = await service.SaveDistributionAsync(fixture.Invoice.Id, DistributionInput(await service.GetDistributionAsync(fixture.Invoice.Id)));
        var reset = await service.ResetDistributionAsync(fixture.Invoice.Id, DistributionInput(saved));
        reset.HasOverrides.Should().BeFalse(); reset.TotalDebit.Should().Be(100);
        (await db.AccountTransactions.AnyAsync()).Should().BeFalse();
    }

    [Theory]
    [InlineData(99, true)]
    [InlineData(100, false)]
    public async Task DistributionEdit_ForeignCurrencySplits_MustReconcileAtTheInvoiceRate(int firstDebit, bool valid)
    {
        var tenantId = Guid.NewGuid(); await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice =>
        {
            ProcurementDistributionDraft(invoice); invoice.CurrencyCode = "USD"; invoice.ExchangeRate = 3;
        });
        var (service, _) = CreateService(db, tenantId);
        var source = await service.GetDistributionAsync(fixture.Invoice.Id);
        source.TotalDebit.Should().Be(300);
        var input = DistributionInput(source); var debit = input.Lines.Single(line => line.Debit > 0);
        debit.Debit = firstDebit;
        input.Lines.Add(new() { LineId = Guid.NewGuid(), GroupId = debit.GroupId, AccountId = debit.AccountId, Debit = 300 - firstDebit });
        if (valid)
        {
            var saved = await service.SaveDistributionAsync(fixture.Invoice.Id, input);
            saved.HasOverrides.Should().BeTrue(); saved.TotalDebit.Should().Be(saved.TotalCredit);
        }
        else await ((Func<Task>)(() => service.SaveDistributionAsync(fixture.Invoice.Id, input)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*exchange-rate rounding difference*");
        (await db.AccountTransactions.AnyAsync()).Should().BeFalse();
    }

    [Theory]
    [InlineData(VendorInvoiceStatus.Approved)]
    [InlineData(VendorInvoiceStatus.PendingApproval)]
    [InlineData(VendorInvoiceStatus.Voided)]
    public async Task DistributionEdit_NonDraft_ShouldRejectEdits(VendorInvoiceStatus status)
    {
        var tenantId = Guid.NewGuid(); await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice => { ProcurementDistributionDraft(invoice); invoice.Status = status; });
        var (service, _) = CreateService(db, tenantId);
        var view = await service.GetDistributionAsync(fixture.Invoice.Id); view.CanEdit.Should().BeFalse();
        await ((Func<Task>)(() => service.SaveDistributionAsync(fixture.Invoice.Id, DistributionInput(view))))
            .Should().ThrowAsync<InvoiceDistributionConflictException>();
    }

    [Fact]
    public async Task DistributionEdit_ManualFinanceAndForeignTenant_ShouldRemainOutsideBoundary()
    {
        var tenantId = Guid.NewGuid(); await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice => invoice.Status = VendorInvoiceStatus.Draft);
        var (service, _) = CreateService(db, tenantId);
        var view = await service.GetDistributionAsync(fixture.Invoice.Id); view.CanEdit.Should().BeFalse();
        await ((Func<Task>)(() => service.SaveDistributionAsync(fixture.Invoice.Id, DistributionInput(view))))
            .Should().ThrowAsync<InvoiceDistributionConflictException>();
        var (foreign, _) = CreateService(db, Guid.NewGuid());
        await ((Func<Task>)(() => foreign.SaveDistributionAsync(fixture.Invoice.Id, DistributionInput(view))))
            .Should().ThrowAsync<KeyNotFoundException>();
    }
}
