using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Finance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed partial class ApInvoicePostingMigrationTests
{
    [Fact]
    public void SupplierTaxFallbackMigration_ShouldPreserveExistingRowsAndGuardRollback()
    {
        var migration = new ErpSystem.Data.Migrations.SupplierInvoiceTaxFallback();
        var up = new Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var down = new Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        migration.GetType().GetMethod("Up", flags)!.Invoke(migration, [up]);
        migration.GetType().GetMethod("Down", flags)!.Invoke(migration, [down]);
        var column = up.Operations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.AddColumnOperation>().Single();
        column.Table.Should().Be("VendorInvoice");
        column.Name.Should().Be(nameof(VendorInvoice.SupplierTaxFallbackAccountId));
        column.IsNullable.Should().BeTrue();
        column.DefaultValue.Should().BeNull();
        up.Operations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.AddForeignKeyOperation>().Single()
            .OnDelete.Should().Be(Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Restrict);
        up.Operations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>().Should().BeEmpty();
        down.Operations.First().Should().BeOfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>()
            .Which.Sql.Should().Contain("SupplierTaxFallbackAccountId IS NOT NULL").And.Contain("THROW");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task SupplierTaxFallback_ShouldRetainCapturedAccountAndRespectTaxRuleOwnership(
        bool ruleHasAccount, bool recoverable)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        var fallback = SeedAccount(db, tenantId, "SUPPLIER-INPUT-TAX", AccountType.Asset);
        var partner = SeedTaxDefaultPartner(fixture.Supplier, fallback.Id);
        db.BusinessPartners.Add(partner);
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId,
            taxEngine: SupplierInputTaxEngine(ruleHasAccount ? fixture.TaxAccount.Id : null, recoverable));
        var created = await service.CreateAsync(SupplierTaxInvoice(fixture, applyDefaults: true));
        var invoice = await db.VendorInvoices.SingleAsync(value => value.Id == created.Id);
        invoice.SupplierTaxFallbackAccountId.Should().Be(fallback.Id);

        // A later master change must not redirect an already captured/approved document.
        partner.DefaultTaxAccountId = SeedAccount(db, tenantId, "NEW-INPUT-TAX", AccountType.Asset).Id;
        invoice.Status = VendorInvoiceStatus.Approved;
        invoice.ApprovalStatus = "Approved";
        invoice.ApprovedById = Guid.NewGuid();
        invoice.ApprovedDate = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var expectedTaxAccount = !recoverable ? fixture.ExpenseAccount.Id
            : ruleHasAccount ? fixture.TaxAccount.Id : fallback.Id;
        var proposed = await service.GetDistributionAsync(invoice.Id);
        proposed.Lines.Where(line => line.AccountId == expectedTaxAccount).Sum(line => line.Debit)
            .Should().Be(recoverable ? 15m : 115m);
        var posted = await service.PostAsync(invoice.Id);
        var retry = await service.PostAsync(invoice.Id);
        retry.JournalEntryId.Should().Be(posted.JournalEntryId);
        var lines = await db.AccountTransactions.Where(line => line.JournalEntryId == posted.JournalEntryId).ToListAsync();
        lines.Where(line => line.AccountId == expectedTaxAccount).Sum(line => line.DebitAmount)
            .Should().Be(recoverable ? 15m : 115m);
        lines.Where(line => line.AccountId == fixture.ApAccount.Id).Sum(line => line.CreditAmount).Should().Be(115m);
        lines.Sum(line => line.DebitAmount).Should().Be(lines.Sum(line => line.CreditAmount));
        var taxSnapshot = await db.Set<TaxCalculation>().SingleAsync(value => value.DocumentId == invoice.Id);
        taxSnapshot.DocumentLineId.Should().Be(invoice.LineItems.Single().Id);
        taxSnapshot.PostingAccountId.Should().Be(expectedTaxAccount);
        lines.Single(line => line.TransactionTag == "AP-Tax-VAT").SourceDocumentLineId
            .Should().Be(invoice.LineItems.Single().Id);
        (await db.FinancePostingEvents.CountAsync(value => value.SourceDocumentId == invoice.Id && value.PostingAction == "Post"))
            .Should().Be(1);
        if (!recoverable || ruleHasAccount) lines.Should().NotContain(line => line.AccountId == fallback.Id);
        if (recoverable && !ruleHasAccount)
        {
            // Supplier credits must reverse the original tax account after the
            // supplier default changes, without consulting today's tax rule.
            var creditService = WriteoffService(db, tenantId);
            var note = WriteoffNote(tenantId, partner, fixture.Supplier.Id);
            note.DebitNoteNumber = "SUPPLIER-TAX-CREDIT";
            note.OriginalVendorInvoiceId = invoice.Id;
            note.OriginalVendorInvoice = invoice;
            var replaceLines = typeof(ErpSystem.Api.Services.Finance.AP.SupplierDebitNoteService)
                .GetMethod("ReplaceLinesAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var sourceLine = invoice.LineItems.Single();
            await (Task)replaceLines.Invoke(creditService, [note, new[] { new CreateSupplierDebitNoteLineItemDto
            {
                OriginalVendorInvoiceLineItemId = sourceLine.Id, Description = "Supplier purchase credit",
                Quantity = 1m, UnitPrice = 100m
            } }, invoice, CancellationToken.None])!;
            note.LineItems.Single().TaxComponents.Single().ResolvedCreditAccountId.Should().Be(fallback.Id);
            note.Status = SupplierDebitNoteStatus.Approved;
            note.ApprovedById = Guid.NewGuid(); note.ApprovedAt = DateTime.UtcNow;
            db.SupplierDebitNotes.Add(note);
            await db.SaveChangesAsync();
            var credited = await creditService.PostAsync(note.Id);
            var creditRetry = await creditService.PostAsync(note.Id);
            creditRetry.JournalEntryId.Should().Be(credited.JournalEntryId);
            var creditLines = await db.AccountTransactions.Where(line => line.JournalEntryId == credited.JournalEntryId).ToListAsync();
            creditLines.Single(line => line.AccountId == fallback.Id).CreditAmount.Should().Be(15m);
            creditLines.Single(line => line.AccountId == fixture.ApAccount.Id).DebitAmount.Should().Be(115m);
            var reversed = await creditService.ReverseAsync(note.Id, new() { Reason = "Credit rescinded", ReversalDate = note.DebitNoteDate });
            var reversalLines = await db.AccountTransactions.Where(line => line.JournalEntryId == reversed.ReversalJournalEntryId).ToListAsync();
            reversalLines.Single(line => line.AccountId == fallback.Id).DebitAmount.Should().Be(15m);
        }
    }

    [Fact]
    public async Task SupplierTaxFallback_ShouldNotBeAppliedWhenDefaultsWereDeclined()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        db.BusinessPartners.Add(SeedTaxDefaultPartner(fixture.Supplier, fixture.TaxAccount.Id));
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId, taxEngine: SupplierInputTaxEngine(null, true));
        var created = await service.CreateAsync(SupplierTaxInvoice(fixture, applyDefaults: false));
        var invoice = await db.VendorInvoices.SingleAsync(value => value.Id == created.Id);
        invoice.SupplierTaxFallbackAccountId.Should().BeNull();
        invoice.Status = VendorInvoiceStatus.Approved; invoice.ApprovalStatus = "Approved";
        invoice.ApprovedById = Guid.NewGuid(); invoice.ApprovedDate = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var post = () => service.PostAsync(invoice.Id);
        await post.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tax account is not configured*");
        (await db.AccountTransactions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task SupplierTaxFallback_ShouldPersistDistinctTaxLineageForEachInvoiceLine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.SubTotal = 200m; invoice.TaxAmount = 30m; invoice.TotalAmount = 230m;
            invoice.LineItems.Single().TaxRate = 15m; invoice.LineItems.Single().TaxAmount = 15m;
        });
        fixture.Invoice.SupplierTaxFallbackAccountId = fixture.TaxAccount.Id;
        var secondLine = new VendorInvoiceLineItem
        {
            TenantId = tenantId, VendorInvoiceId = fixture.Invoice.Id, Description = "Second taxable line",
            Quantity = 1m, UnitPrice = 100m, GLAccountId = fixture.ExpenseAccount.Id,
            TaxRate = 15m, TaxAmount = 15m, LineItemType = "Expense"
        };
        fixture.Invoice.LineItems.Add(secondLine);
        db.Set<VendorInvoiceLineItem>().Add(secondLine);
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId, taxEngine: SupplierInputTaxEngine(null, true));
        var posted = await service.PostAsync(fixture.Invoice.Id);
        await service.PostAsync(fixture.Invoice.Id);
        var snapshots = await db.Set<TaxCalculation>().Where(value => value.DocumentId == fixture.Invoice.Id).ToListAsync();
        snapshots.Should().HaveCount(2);
        snapshots.Select(value => value.DocumentLineId).Should().BeEquivalentTo(fixture.Invoice.LineItems.Select(line => (Guid?)line.Id));
        snapshots.Should().OnlyContain(value => value.PostingAccountId == fixture.TaxAccount.Id);
        (await db.AccountTransactions.Where(line => line.JournalEntryId == posted.JournalEntryId && line.AccountId == fixture.TaxAccount.Id)
            .SumAsync(line => line.DebitAmount)).Should().Be(30m);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("inactive")]
    [InlineData("expense")]
    [InlineData("not-postable")]
    [InlineData("expired")]
    public async Task SupplierTaxFallback_ShouldRejectInvalidCapturedAccounts(string invalidReason)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        var fallback = SeedAccount(db, tenantId, "INVALID-INPUT-TAX", AccountType.Asset);
        if (invalidReason == "tenant") fallback.TenantId = Guid.NewGuid();
        if (invalidReason == "inactive") fallback.Status = AccountStatus.Inactive;
        if (invalidReason == "expense") fallback.AccountType = AccountType.Expense;
        if (invalidReason == "not-postable") fallback.AllowDirectPosting = false;
        if (invalidReason == "expired") fallback.ExpirationDate = fixture.Invoice.InvoiceDate;
        db.BusinessPartners.Add(SeedTaxDefaultPartner(fixture.Supplier, fallback.Id));
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId, taxEngine: SupplierInputTaxEngine(null, true));
        var create = () => service.CreateAsync(SupplierTaxInvoice(fixture, applyDefaults: true));
        await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("*supplier input-tax fallback*");
        (await db.VendorInvoices.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SupplierTaxFallback_ShouldRejectInvalidEffectiveAccountWithoutMaskingRuleErrors(bool ruleHasAccount)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.TaxAmount = 15m; invoice.TotalAmount = 115m;
            invoice.LineItems.Single().TaxAmount = 15m; invoice.LineItems.Single().TaxRate = 15m;
        });
        fixture.Invoice.SupplierTaxFallbackAccountId = ruleHasAccount
            ? SeedAccount(db, tenantId, "ELIGIBLE-TAX-FALLBACK", AccountType.Asset).Id
            : fixture.TaxAccount.Id;
        fixture.TaxAccount.Status = AccountStatus.Inactive;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId,
            taxEngine: SupplierInputTaxEngine(ruleHasAccount ? fixture.TaxAccount.Id : null, true));
        var post = () => service.PostAsync(fixture.Invoice.Id);
        await post.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(ruleHasAccount ? "*not active*" : "*supplier input-tax fallback*");
        (await db.AccountTransactions.AnyAsync()).Should().BeFalse();
    }

    private static BusinessPartner SeedTaxDefaultPartner(Supplier supplier, Guid fallbackAccountId) => new()
    {
        TenantId = supplier.TenantId, PartnerCode = supplier.SupplierCode, PartnerName = "Tax supplier",
        PartnerType = "Supplier", IsActive = true, ApprovalStatus = "Approved", RegistrationStatus = "Active",
        DefaultTaxAccountId = fallbackAccountId
    };

    private static VendorInvoiceCreateDto SupplierTaxInvoice(ApInvoiceFixture fixture, bool applyDefaults) => new()
    {
        SupplierId = fixture.Supplier.Id, SupplierInvoiceNumber = "SUPPLIER-TAX-TEST",
        InvoiceDate = fixture.Invoice.InvoiceDate, CurrencyCode = "GHS", ExchangeRate = 1m,
        ApplyBusinessPartnerDefaults = applyDefaults,
        LineItems = [new() { LineItemType = "Expense", Description = "Taxable purchase", Quantity = 1m,
            UnitPrice = 100m, GLAccountId = fixture.ExpenseAccount.Id, TaxGroupId = Guid.NewGuid() }]
    };

    private static ITaxCalculationEngine SupplierInputTaxEngine(Guid? ruleAccountId, bool recoverable)
    {
        var engine = new Mock<ITaxCalculationEngine>();
        engine.Setup(value => value.CalculateTaxesAsync(It.IsAny<TaxCalculationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxCalculationResultDto
            {
                BaseAmount = 100m, TotalTaxAmount = 15m, GrandTotal = 115m,
                TaxBreakdowns = [new() { TaxId = Guid.NewGuid(), TaxCode = "VAT", TaxName = "Purchase VAT",
                    TaxRate = 15m, TaxAmount = 15m, TaxableAmount = 100m, IsInputTaxDeductible = recoverable,
                    TaxReceivableAccountId = ruleAccountId }]
            });
        return engine.Object;
    }
}
