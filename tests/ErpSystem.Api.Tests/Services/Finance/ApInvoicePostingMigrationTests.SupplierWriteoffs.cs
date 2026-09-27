using System.Reflection;
using System.Data;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed partial class ApInvoicePostingMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SupplierWriteoff_ShouldCaptureAccountRequireApprovalPostOnceAndReverseOriginalAccount(bool explicitAccount)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenant);
        var mapped = SeedAccount(db, tenant, "WRITE-OFF", AccountType.Revenue);
        var partner = fixture.Supplier;
        partner.DefaultWriteoffAccountId = mapped.Id;
        await db.SaveChangesAsync();
        var service = WriteoffService(db, tenant, out var unitOfWork);
        var note = WriteoffNote(tenant, partner);
        note.BusinessPartnerRoleId = fixture.Invoice.BusinessPartnerRoleId;
        note.BusinessPartnerApProfileVersionId = fixture.Invoice.BusinessPartnerApProfileVersionId;
        await ReplaceWriteoffLines(service, note, new() { LineItemType = "Writeoff", Description = "Agreed liability reduction",
            Quantity = 1m, UnitPrice = 25m, GLAccountId = explicitAccount ? fixture.ExpenseAccount.Id : mapped.Id });
        var expected = explicitAccount ? fixture.ExpenseAccount.Id : mapped.Id;
        note.LineItems.Single().GLAccountId.Should().Be(expected);
        note.LineItems.Single().ResolvedCreditAccountId.Should().Be(expected);
        db.SupplierDebitNotes.Add(note);
        await db.SaveChangesAsync();

        await ((Func<Task>)(() => service.PostAsync(note.Id))).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only approved*");
        (await db.AccountTransactions.AnyAsync()).Should().BeFalse();
        note = await db.SupplierDebitNotes.Include(value => value.LineItems).Include(value => value.Vendor)
            .SingleAsync(value => value.Id == note.Id);
        note.Status = SupplierDebitNoteStatus.Approved;
        note.ApprovedById = Guid.NewGuid(); note.ApprovedAt = DateTime.UtcNow;
        note.Vendor.DefaultWriteoffAccountId = Guid.NewGuid();
        await db.SaveChangesAsync();
        var result = await service.PostAsync(note.Id);
        var retry = await service.PostAsync(note.Id);
        retry.JournalEntryId.Should().Be(result.JournalEntryId);
        var transactions = await db.AccountTransactions.Where(line => line.JournalEntryId == result.JournalEntryId).ToListAsync();
        transactions.Should().HaveCount(2);
        transactions.Single(line => line.AccountId == expected).CreditAmount.Should().Be(25m);
        transactions.Single(line => line.AccountId == expected).TransactionTag.Should().Be("AP-Writeoff");
        transactions.Single(line => line.AccountId == fixture.ApAccount.Id).DebitAmount.Should().Be(25m);
        (await db.FinancePostingEvents.CountAsync(item => item.SourceDocumentId == note.Id)).Should().Be(1);
        note.DirectInvoiceAppliedAmount.Should().Be(0m);
        fixture.Invoice.PaidAmount.Should().Be(0m);

        var reversed = await service.ReverseAsync(note.Id, new() { Reason = "Agreement rescinded", ReversalDate = note.DebitNoteDate });
        var reversalLines = await db.AccountTransactions.Where(line => line.JournalEntryId == reversed.ReversalJournalEntryId).ToListAsync();
        reversalLines.Single(line => line.AccountId == expected).DebitAmount.Should().Be(25m);
        reversalLines.Single(line => line.AccountId == fixture.ApAccount.Id).CreditAmount.Should().Be(25m);
        unitOfWork.Verify(unit => unit.BeginTransactionAsync(IsolationLevel.Serializable, It.IsAny<CancellationToken>()), Times.Exactly(4));
        unitOfWork.Verify(unit => unit.AcquireTransactionLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
        unitOfWork.Verify(unit => unit.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Theory]
    [InlineData("tax-group")]
    [InlineData("tax-rate")]
    [InlineData("tax-amount")]
    [InlineData("discount-rate")]
    [InlineData("discount-amount")]
    [InlineData("source-line")]
    public async Task SupplierWriteoff_ShouldRejectPurchaseAndDiscountEvidence(string invalid)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var dto = new CreateSupplierDebitNoteLineItemDto { LineItemType = "Writeoff", Description = "Settlement", Quantity = 1m, UnitPrice = 25m };
        if (invalid == "tax-group") dto.TaxGroupId = Guid.NewGuid();
        if (invalid == "tax-rate") dto.TaxRate = 1m;
        if (invalid == "tax-amount") dto.TaxAmount = 1m;
        if (invalid == "discount-rate") dto.DiscountPercentage = 1m;
        if (invalid == "discount-amount") dto.DiscountAmount = 1m;
        if (invalid == "source-line") dto.OriginalVendorInvoiceLineItemId = Guid.NewGuid();
        var note = WriteoffNote(tenant, new BusinessPartner { TenantId = tenant });
        await ((Func<Task>)(() => ReplaceWriteoffLines(WriteoffService(db, tenant), note, dto)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("AP_WRITEOFF_SETTLEMENT_ONLY*");
        note.LineItems.Should().BeEmpty();
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("inactive")]
    [InlineData("asset")]
    [InlineData("missing")]
    public async Task SupplierWriteoff_ShouldRejectIneligibleMapping(string invalid)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedApprovedApInvoiceAsync(db, tenant);
        var account = SeedAccount(db, tenant, "WRITE-OFF-INVALID", invalid == "asset" ? AccountType.Asset : AccountType.Revenue);
        if (invalid == "foreign") account.TenantId = Guid.NewGuid();
        if (invalid == "inactive") account.Status = AccountStatus.Inactive;
        var partner = new BusinessPartner { TenantId = tenant, PartnerCode = "WO", PartnerName = "Writeoff supplier",
            PartnerType = "Supplier", IsActive = true, DefaultWriteoffAccountId = invalid == "missing" ? null : account.Id };
        db.BusinessPartners.Add(partner); await db.SaveChangesAsync();
        var note = WriteoffNote(tenant, partner);
        await ((Func<Task>)(() => ReplaceWriteoffLines(WriteoffService(db, tenant), note,
            new() { LineItemType = "Writeoff", Description = "Settlement", Quantity = 1m, UnitPrice = 25m, GLAccountId = invalid == "missing" ? null : account.Id })))
            .Should().ThrowAsync<InvalidOperationException>();
        (await db.AccountTransactions.AnyAsync()).Should().BeFalse();
    }

    private static SupplierDebitNote WriteoffNote(Guid tenant, BusinessPartner partner) => new()
    {
        TenantId = tenant, VendorId = partner.Id, Vendor = partner,
        DebitNoteNumber = "WO-TEST", DebitNoteDate = new DateTime(2026, 7, 5), Reason = "Agreed liability reduction",
        CurrencyCode = "GHS", ExchangeRate = 1m, Status = SupplierDebitNoteStatus.Draft
    };

    private static async Task ReplaceWriteoffLines(SupplierDebitNoteService service, SupplierDebitNote note, CreateSupplierDebitNoteLineItemDto dto)
    {
        var method = typeof(SupplierDebitNoteService).GetMethod("ReplaceLinesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(service, [note, new[] { dto }, null, CancellationToken.None])!;
    }

    private static SupplierDebitNoteService WriteoffService(ApplicationDbContext db, Guid tenant) => WriteoffService(db, tenant, out _);

    private static SupplierDebitNoteService WriteoffService(ApplicationDbContext db, Guid tenant, out Mock<IUnitOfWork> unitOfWork)
    {
        var current = CreateCurrentUser(tenant);
        var audit = Mock.Of<IFinanceAuditService>();
        var engine = new FinancePostingEngine(db, current.Object, Mock.Of<ILogger<FinancePostingEngine>>(), audit);
        // Keep public approval/post/reversal behavior and the real ledger engine. EF InMemory
        // cannot provide SQL isolation/application locks; assert their orchestration here.
        // This fixture does not establish production concurrency behavior.
        var transactionActive = false;
        unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        unitOfWork.Setup(unit => unit.ExecuteInStrategyAsync(It.IsAny<Func<Task<SupplierDebitNoteDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task<SupplierDebitNoteDto>> operation, CancellationToken _) => operation());
        unitOfWork.Setup(unit => unit.BeginTransactionAsync(IsolationLevel.Serializable, It.IsAny<CancellationToken>()))
            .Callback(() => transactionActive = true).Returns(Task.CompletedTask);
        unitOfWork.SetupGet(unit => unit.HasActiveTransaction).Returns(() => transactionActive);
        unitOfWork.Setup(unit => unit.AcquireTransactionLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => transactionActive.Should().BeTrue()).Returns(Task.CompletedTask);
        unitOfWork.Setup(unit => unit.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => transactionActive = false).Returns(Task.CompletedTask);
        unitOfWork.Setup(unit => unit.RollbackAsync(It.IsAny<CancellationToken>()))
            .Callback(() => transactionActive = false).Returns(Task.CompletedTask);
        unitOfWork.Setup(unit => unit.ClearTrackedChanges()).Callback(() => db.ChangeTracker.Clear());
        return new SupplierDebitNoteService(db, unitOfWork.Object, current.Object, null!, null!, engine, audit,
            null!, Mock.Of<ILogger<SupplierDebitNoteService>>());
    }
}
