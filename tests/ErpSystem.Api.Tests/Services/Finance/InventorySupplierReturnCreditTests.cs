using System.Reflection;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Data.Migrations;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class InventorySupplierReturnCreditTests
{
    [Theory]
    [InlineData(706.87, 700, 0, 6.87, 0)]
    [InlineData(690, 700, 0, 0, 10)]
    [InlineData(700, 700, 0, 0, 0)]
    [InlineData(706.87, 700, 105, 6.87, 0)]
    public void Credit_clears_actual_dispatch_carrying_value_without_second_inventory_reversal(
        decimal carrying, decimal principal, decimal tax, decimal varianceDebit, decimal varianceCredit)
    {
        var fixture = PostingFixture(carrying, principal, tax);
        var result = Build(fixture.Note, fixture.Dispatch, fixture.Variance, fixture.Lines, fixture.Carrying);
        result.Sum(x => x.DebitAmount).Should().Be(result.Sum(x => x.CreditAmount));
        result.Where(x => x.AccountId == fixture.Dispatch.ClearingAccountId).Sum(x => x.CreditAmount).Should().Be(carrying);
        result.Where(x => x.AccountId == fixture.Variance).Sum(x => x.DebitAmount).Should().Be(varianceDebit);
        result.Where(x => x.AccountId == fixture.Variance).Sum(x => x.CreditAmount).Should().Be(varianceCredit);
        result.Should().NotContain(x => x.AccountId == fixture.Dispatch.InventoryAccountId);
        result.Should().ContainSingle(x => x.TransactionTag == "AP-SupplierDebitNote-Control" && x.DebitAmount == principal + tax);
        if (tax > 0) result.Should().ContainSingle(x => x.TransactionTag == "AP-SupplierDebitNote-Tax" && x.CreditAmount == tax);
    }

    [Fact]
    public void Original_purchase_discount_is_net_in_clearing_not_a_second_discount_reversal()
    {
        var fixture = PostingFixture(706.87m, 750m, 0);
        fixture.Lines[0].DebitAmount = 700;
        fixture.Lines.Add(new FinancePostingLineDto { AccountId = Guid.NewGuid(), DebitAmount = 50,
            TransactionTag = "AP-SupplierDebitNote-Discount", SourceDocumentLineId = fixture.Carrying.Single().Key });
        var result = Build(fixture.Note, fixture.Dispatch, fixture.Variance, fixture.Lines, fixture.Carrying);
        result.Should().NotContain(x => x.TransactionTag == "AP-SupplierDebitNote-Discount");
        result.Single(x => x.TransactionTag == "RTV-Credit-Variance").DebitAmount.Should().Be(6.87m);
        result.Sum(x => x.DebitAmount - x.CreditAmount).Should().Be(0);
    }

    [Fact]
    public void Carrying_allocation_mismatch_cannot_be_posted()
    {
        var fixture = PostingFixture(706.87m, 700m, 0);
        fixture.Carrying[fixture.Carrying.Single().Key] = 700;
        Action act = () => Build(fixture.Note, fixture.Dispatch, fixture.Variance, fixture.Lines, fixture.Carrying);
        act.Should().Throw<TargetInvocationException>().WithInnerException<InvalidOperationException>()
            .WithMessage("RTV_CARRYING_ALLOCATION_MISMATCH");
    }

    [Fact]
    public void Exact_line_match_accepts_only_same_tenant_original_po_item()
    {
        var (source, invoice) = SourceFixture();
        Matches(source, invoice).Should().BeTrue();
        invoice.TenantId = Guid.NewGuid();
        Matches(source, invoice).Should().BeFalse();
    }

    [Fact]
    public void Returned_base_units_are_compared_to_converted_original_purchase_units()
    {
        var (source, invoice) = SourceFixture();
        source.Items.Single().ReturnQuantity = 20;
        invoice.LineItems.Single().Quantity = 1;
        Matches(source, invoice).Should().BeFalse();
        var converted = new Dictionary<Guid, decimal> { [source.Items.Single().Id] = 1m };
        var result = (bool)typeof(SupplierDebitNoteService).GetMethod("HasExactReturnInvoiceLines", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { source, invoice, converted })!;
        result.Should().BeTrue();
    }

    [Fact]
    public void Each_credit_line_retains_its_inherited_dimensions_and_exact_carrying_allocation()
    {
        var fixture = PostingFixture(706.87m, 700m, 0);
        var first = fixture.Lines.Single(x => x.TransactionTag == "AP-SupplierDebitNote-Line");
        first.Dimensions = new[] { new FinancePostingDimensionValueDto { DimensionCode = "DEPARTMENT", ValueCode = "OPERATIONS" } };
        var result = Build(fixture.Note, fixture.Dispatch, fixture.Variance, fixture.Lines, fixture.Carrying);
        foreach (var line in result.Where(x => x.TransactionTag is "RTV-Credit-Clearing" or "RTV-Credit-Variance"))
        {
            line.SourceDocumentLineId.Should().Be(first.SourceDocumentLineId);
            line.Dimensions.Should().BeSameAs(first.Dimensions);
        }
    }

    [Theory]
    [InlineData("wrong-po")]
    [InlineData("wrong-item")]
    [InlineData("no-po-line")]
    [InlineData("unposted")]
    [InlineData("voided")]
    [InlineData("over-quantity")]
    [InlineData("duplicate-source")]
    [InlineData("deleted-line")]
    public void Faked_or_ambiguous_invoice_source_cannot_be_used(string change)
    {
        var (source, invoice) = SourceFixture();
        var line = invoice.LineItems.Single();
        switch (change)
        {
            case "wrong-po": invoice.PurchaseOrderId = Guid.NewGuid(); break;
            case "wrong-item": line.InventoryItemId = Guid.NewGuid(); break;
            case "no-po-line": line.PurchaseOrderItemId = null; source.Items.Single().GoodsReceiptNoteItem!.PurchaseOrderItemId = null; break;
            case "unposted": invoice.JournalEntryId = null; break;
            case "voided": invoice.Status = VendorInvoiceStatus.Voided; break;
            case "over-quantity": source.Items.Single().ReturnQuantity = 21; break;
            case "duplicate-source": invoice.LineItems.Add(new VendorInvoiceLineItem { Id = Guid.NewGuid(), TenantId = line.TenantId,
                PurchaseOrderItemId = line.PurchaseOrderItemId, InventoryItemId = line.InventoryItemId, Quantity = line.Quantity, UnitPrice = line.UnitPrice }); break;
            case "deleted-line": line.IsDeleted = true; break;
        }
        Matches(source, invoice).Should().BeFalse();
    }

    [Fact]
    public void Existing_debit_notes_remain_non_rtv_with_no_direct_application()
    {
        var note = new SupplierDebitNote();
        note.InventoryPurchaseReturnId.Should().BeNull();
        note.DirectInvoiceAppliedAmount.Should().Be(0);
        note.DirectInvoiceAppliedAt.Should().BeNull();
        note.SupplierReturnId.Should().BeNull();
    }

    [Theory]
    [InlineData(SupplierDebitNoteStatus.Draft, true)]
    [InlineData(SupplierDebitNoteStatus.Rejected, true)]
    [InlineData(SupplierDebitNoteStatus.PendingApproval, false)]
    [InlineData(SupplierDebitNoteStatus.Approved, false)]
    [InlineData(SupplierDebitNoteStatus.Posted, false)]
    [InlineData(SupplierDebitNoteStatus.Cancelled, false)]
    [InlineData(SupplierDebitNoteStatus.Reversed, false)]
    public void Header_corrections_are_limited_to_unposted_draft_or_rejected_credit(SupplierDebitNoteStatus status, bool allowed)
    {
        var note = new SupplierDebitNote { InventoryPurchaseReturnId = Guid.NewGuid(), Status = status };
        Action act = () => typeof(SupplierDebitNoteService).GetMethod("RequireInventoryCreditHeaderEditable", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { note });
        if (allowed) act.Should().NotThrow();
        else act.Should().Throw<TargetInvocationException>().WithInnerException<InvalidOperationException>();
    }

    [Theory]
    [InlineData("no-return")]
    [InlineData("journal")]
    [InlineData("event")]
    [InlineData("application")]
    public void Header_correction_cannot_rewrite_standalone_or_posted_evidence(string evidence)
    {
        var note = new SupplierDebitNote { InventoryPurchaseReturnId = Guid.NewGuid(), Status = SupplierDebitNoteStatus.Draft };
        switch (evidence)
        {
            case "no-return": note.InventoryPurchaseReturnId = null; break;
            case "journal": note.JournalEntryId = Guid.NewGuid(); break;
            case "event": note.PostingEventId = Guid.NewGuid(); break;
            case "application": note.DirectInvoiceAppliedAt = DateTime.UtcNow; break;
        }
        Action act = () => typeof(SupplierDebitNoteService).GetMethod("RequireInventoryCreditHeaderEditable", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { note });
        act.Should().Throw<TargetInvocationException>().WithInnerException<InvalidOperationException>();
    }

    [Fact]
    public void Header_api_has_no_supplier_source_quantity_tax_or_account_fields_and_requires_rowversion()
    {
        typeof(UpdateInventoryReturnCreditHeaderDto).GetProperties().Select(x => x.Name).Should()
            .BeEquivalentTo("SupplierCreditNoteReference", "CreditDate", "Reason", "RowVersion");
        typeof(UpdateInventoryReturnCreditHeaderDto).GetProperty("RowVersion")!
            .GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>().Should().NotBeNull();
        typeof(SupplierDebitNotesController).GetMethod("UpdateReturnCreditHeader")!
            .GetCustomAttributes<AuthorizeAttribute>().Should().Contain(x => x.Policy == FinancePermissions.ManageApSupplierDebitNotes);
    }

    [Fact]
    public void Credit_creation_uses_canonical_finance_manage_permission_and_post_remains_separate()
    {
        var create = typeof(SupplierDebitNotesController).GetMethod("CreateReturnCredit")!;
        create.GetCustomAttributes<AuthorizeAttribute>().Should().Contain(x => x.Policy == FinancePermissions.ManageApSupplierDebitNotes);
        typeof(SupplierDebitNotesController).GetMethod("Post")!.GetCustomAttributes<AuthorizeAttribute>()
            .Should().Contain(x => x.Policy == FinancePermissions.PostApSupplierDebitNotes);
        typeof(CreateInventoryReturnCreditDto).GetProperties().Should().NotContain(x => x.Name == "Amount" || x.Name == "PaidAmount" || x.Name == "GLAccountId" || x.Name == "VendorPaymentId");
    }

    [Theory]
    [InlineData("posted", true)]
    [InlineData("draft", false)]
    [InlineData("reversed", false)]
    [InlineData("other-tenant", false)]
    [InlineData("other-invoice", false)]
    [InlineData("other-type", false)]
    [InlineData("missing", false)]
    public void Original_invoice_requires_its_own_posted_unreversed_journal(string change, bool expected)
    {
        var (_, invoice) = SourceFixture();
        JournalEntry? journal = new() { Id = invoice.JournalEntryId!.Value, TenantId = invoice.TenantId,
            SourceDocumentId = invoice.Id, SourceDocumentType = "VendorInvoice", PostingStatus = "Posted" };
        switch (change)
        {
            case "draft": journal.PostingStatus = "Draft"; break;
            case "reversed": journal.IsReversed = true; break;
            case "other-tenant": journal.TenantId = Guid.NewGuid(); break;
            case "other-invoice": journal.SourceDocumentId = Guid.NewGuid(); break;
            case "other-type": journal.SourceDocumentType = "ManualJournal"; break;
            case "missing": journal = null; break;
        }
        var result = (bool)typeof(SupplierDebitNoteService).GetMethod("HasPostedOriginalInvoiceEvidence", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object?[] { invoice, journal })!;
        result.Should().Be(expected);
    }

    [Fact]
    public void Database_guards_retain_immutable_dispatch_and_original_invoice_application()
    {
        InventorySupplierReturnInvoiceCredit.DispatchGuard.Should().Contain("RTV_FINANCE_DISPATCH_IMMUTABLE").And.Contain("ApSupplierIdentityLinks");
        InventorySupplierReturnInvoiceCredit.CreditGuard.Should().Contain("RTV_CREDIT_LINK_OR_APPLICATION_IMMUTABLE")
            .And.Contain("i.DirectInvoiceAppliedAmount<>i.TotalAmount").And.Contain("i.SupplierReturnId IS NOT NULL");
    }

    [Fact]
    public void Migration_uses_the_existing_singular_vendor_invoice_table_for_fk_and_guards()
    {
        var links = new InventorySupplierReturnInvoiceCredit().UpOperations.OfType<AddForeignKeyOperation>().ToList();
        links.Should().ContainSingle(x => x.Table == "InventorySupplierReturnPostings" &&
            x.Columns.SequenceEqual(new[] { "OriginalVendorInvoiceId" }) && x.PrincipalTable == "VendorInvoice");
        links.Should().NotContain(x => x.PrincipalTable == "VendorInvoices");
        InventorySupplierReturnInvoiceCredit.DispatchGuard.Should().Contain("LEFT JOIN dbo.VendorInvoice v")
            .And.NotContain("dbo.VendorInvoices");
        InventorySupplierReturnInvoiceCredit.CreditGuard.Should().Contain("LEFT JOIN dbo.VendorInvoice v")
            .And.NotContain("dbo.VendorInvoices");
    }

    private static bool Matches(PurchaseReturn source, VendorInvoice invoice) => (bool)typeof(SupplierDebitNoteService)
        .GetMethod("HasExactReturnInvoiceLines", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object?[] { source, invoice, null })!;

    private static IReadOnlyList<FinancePostingLineDto> Build(SupplierDebitNote note, InventorySupplierReturnPosting dispatch,
        Guid variance, List<FinancePostingLineDto> lines, Dictionary<Guid, decimal> carrying) =>
        (IReadOnlyList<FinancePostingLineDto>)typeof(SupplierDebitNoteService)
            .GetMethod("BuildReturnClearingLines", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { lines, note, dispatch, variance, "GHS", carrying })!;

    private static (SupplierDebitNote Note, InventorySupplierReturnPosting Dispatch, Guid Variance, List<FinancePostingLineDto> Lines,
        Dictionary<Guid, decimal> Carrying) PostingFixture(decimal carrying, decimal principal, decimal tax)
    {
        var lineId = Guid.NewGuid();
        var dispatch = new InventorySupplierReturnPosting { ClearingAccountId = Guid.NewGuid(), InventoryAccountId = Guid.NewGuid(), CarryingAmount = carrying };
        var lines = new List<FinancePostingLineDto>
        {
            new() { AccountId = Guid.NewGuid(), DebitAmount = principal + tax, TransactionTag = "AP-SupplierDebitNote-Control" },
            new() { AccountId = dispatch.InventoryAccountId, CreditAmount = principal, TransactionTag = "AP-SupplierDebitNote-Line", SourceDocumentLineId = lineId }
        };
        if (tax > 0) lines.Add(new() { AccountId = Guid.NewGuid(), CreditAmount = tax, TransactionTag = "AP-SupplierDebitNote-Tax", SourceDocumentLineId = lineId });
        return (new SupplierDebitNote { DebitNoteNumber = "DN-TEST" }, dispatch, Guid.NewGuid(), lines, new() { [lineId] = carrying });
    }

    private static (PurchaseReturn Source, VendorInvoice Invoice) SourceFixture()
    {
        var tenant = Guid.NewGuid(); var po = Guid.NewGuid(); var poLine = Guid.NewGuid(); var item = Guid.NewGuid();
        var source = new PurchaseReturn { TenantId = tenant, PurchaseOrderId = po };
        source.Items.Add(new PurchaseReturnItem { Id = Guid.NewGuid(), TenantId = tenant, InventoryItemId = item, ReturnQuantity = 1,
            GoodsReceiptNoteItem = new GoodsReceiptNoteItem { PurchaseOrderItemId = poLine, InventoryItemId = item, TenantId = tenant } });
        var invoice = new VendorInvoice { TenantId = tenant, PurchaseOrderId = po, JournalEntryId = Guid.NewGuid(), Status = VendorInvoiceStatus.Approved };
        invoice.LineItems.Add(new VendorInvoiceLineItem { TenantId = tenant, InventoryItemId = item, PurchaseOrderItemId = poLine, Quantity = 20, UnitPrice = 700 });
        return (source, invoice);
    }
}
