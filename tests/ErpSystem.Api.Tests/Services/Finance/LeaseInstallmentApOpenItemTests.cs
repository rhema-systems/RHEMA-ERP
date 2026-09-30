using ErpSystem.Api.Authorization;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class LeaseInstallmentApOpenItemTests
{
    [Fact]
    public void Prepare_endpoint_and_permission_convention_require_ap_create_only()
    {
        var method = typeof(LeaseAccountingController).GetMethod(
            nameof(LeaseAccountingController.PreparePeriodPayable));

        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Select(item => item.Policy)
            .Should().ContainSingle(item => item == FinancePermissions.CreateApInvoices,
                "callers without the AP-create policy must receive the normal authorization denial");
        FinancePermissionPolicyMap.GetRequiredPolicies(
                nameof(LeaseAccountingController), method.Name, ["POST"])
            .Should().BeEquivalentTo([FinancePermissions.CreateApInvoices],
                "lease payable preparation is a maker action, never AP approval or posting authority");
    }

    [Fact]
    public async Task Model_enforces_one_active_invoice_and_one_successor_per_tenant_source()
    {
        await using var fixture = new Fixture();
        var invoice = fixture.Context.Model.FindEntityType(typeof(VendorInvoice));
        var leaseSourceKey = new[] { nameof(VendorInvoice.TenantId), nameof(VendorInvoice.LeaseScheduleLineId) };

        invoice.Should().NotBeNull();
        var activeSource = invoice!.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(leaseSourceKey));
        activeSource.IsUnique.Should().BeTrue();
        activeSource.GetFilter().Should().Contain("[Status] <> 7");
        var successor = invoice.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(VendorInvoice.TenantId), nameof(VendorInvoice.ReplacesLeaseVendorInvoiceId)]));
        successor.IsUnique.Should().BeTrue();
        successor.GetFilter().Should().Contain("[ReplacesLeaseVendorInvoiceId] IS NOT NULL");
        invoice.GetForeignKeys().Should().Contain(foreignKey =>
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual(leaseSourceKey));
        invoice.GetIndexes().Should().Contain(index => index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { nameof(VendorInvoice.TenantId), nameof(VendorInvoice.SourceBookAuthorityId) }));
        invoice.GetForeignKeys().Should().Contain(foreignKey =>
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(VendorInvoice.TenantId), nameof(VendorInvoice.SourceBookAuthorityId) }) &&
            foreignKey.PrincipalKey.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(FinanceSourceBookAuthority.TenantId), nameof(FinanceSourceBookAuthority.Id) }));
    }

    [Fact]
    public async Task Prepare_retry_reuses_one_draft_without_posting_or_paying_the_schedule()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync();
        var schedule = lease.ScheduleLines.Single();

        var first = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, schedule.Id);
        var retry = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, schedule.Id);

        retry.Id.Should().Be(first.Id);
        first.Status.Should().Be(VendorInvoiceStatus.Draft);
        first.ApprovalStatus.Should().Be("Draft");
        first.JournalEntryId.Should().BeNull();
        first.PaidAmount.Should().Be(0m);
        first.WithholdingDecisionPending.Should().BeTrue();
        first.LeaseAccountingBookId.Should().Be(fixture.BookId);
        first.LeaseAccountingBookCode.Should().Be("PRIMARY");
        first.LeaseFunctionalCurrencyCode.Should().Be("GHS");
        first.LineItems.Should().BeEquivalentTo(
        [
            new { LeaseComponent = (LeaseInvoiceComponent?)LeaseInvoiceComponent.Principal, GLAccountId = (Guid?)fixture.LiabilityAccountId, UnitPrice = 90m },
            new { LeaseComponent = (LeaseInvoiceComponent?)LeaseInvoiceComponent.Interest, GLAccountId = (Guid?)fixture.InterestAccountId, UnitPrice = 10m }
        ], options => options.ExcludingMissingMembers());

        (await fixture.Context.VendorInvoices.CountAsync(item => !item.IsDeleted)).Should().Be(1);
        (await fixture.Context.Set<FinancePostingEvent>().CountAsync(item =>
            item.SourceDocumentType == "LeasePeriodPosting")).Should().Be(0);
        var persisted = await fixture.Context.Set<LeaseScheduleLine>().AsNoTracking().SingleAsync(item => item.Id == schedule.Id);
        persisted.IsPosted.Should().BeFalse("draft creation is neither AP accounting recognition nor settlement");
    }

    [Fact]
    public async Task Prepare_next_period_before_prior_ap_post_fails_without_partial_invoice()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync(twoPeriods: true);
        var second = lease.ScheduleLines.Single(item => item.PeriodNumber == 2);

        var prepare = () => fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, second.Id);

        await prepare.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*prior lease period must be accounting-posted through AP*");
        (await fixture.Context.VendorInvoices.CountAsync()).Should().Be(0);
        (await fixture.Context.Set<LeaseScheduleLine>().AsNoTracking().AnyAsync(item => item.IsPosted)).Should().BeFalse();
    }

    [Fact]
    public async Task Submit_is_blocked_until_both_withholding_and_tax_are_reviewed()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync();
        var draft = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, lease.ScheduleLines.Single().Id);

        var pendingWht = () => fixture.Service.SubmitForApprovalAsync(draft.Id);
        await pendingWht.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP_WHT_CONFIRMATION_REQUIRED:*");

        var invoice = await fixture.Context.VendorInvoices
            .Include(item => item.LineItems)
            .SingleAsync(item => item.Id == draft.Id);
        invoice.WithholdingDecisionPending = false;
        await fixture.Context.SaveChangesAsync();

        var pendingTax = () => fixture.Service.SubmitForApprovalAsync(draft.Id);
        await pendingTax.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tax treatment for every lease instalment line*");

        var unchanged = await fixture.Context.VendorInvoices.AsNoTracking().SingleAsync(item => item.Id == draft.Id);
        unchanged.Status.Should().Be(VendorInvoiceStatus.Draft);
        unchanged.JournalEntryId.Should().BeNull();
        (await fixture.Context.Set<LeaseScheduleLine>().AsNoTracking()
            .SingleAsync(item => item.Id == lease.ScheduleLines.Single().Id)).IsPosted.Should().BeFalse();
    }

    [Fact]
    public async Task Pending_tax_also_blocks_approve_and_post_before_any_finance_posting_call()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync();
        var draft = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, lease.ScheduleLines.Single().Id);
        var invoice = await fixture.Context.VendorInvoices
            .Include(item => item.LineItems)
            .SingleAsync(item => item.Id == draft.Id);
        invoice.WithholdingDecisionPending = false;
        invoice.Status = VendorInvoiceStatus.PendingApproval;
        await fixture.Context.SaveChangesAsync();

        var approve = () => fixture.Service.ApproveAsync(invoice.Id);
        await approve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tax treatment for every lease instalment line*");

        invoice = await fixture.Context.VendorInvoices
            .Include(item => item.LineItems)
            .SingleAsync(item => item.Id == draft.Id);
        invoice.Status = VendorInvoiceStatus.Approved;
        invoice.ApprovalRequired = false;
        invoice.ApprovalStatus = "NotRequired";
        await fixture.Context.SaveChangesAsync();

        var post = () => fixture.Service.PostAsync(invoice.Id);
        await post.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tax treatment for every lease instalment line*");
        fixture.FinanceEngine.Verify(item => item.PostAsync(
            It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_requires_maker_checker_and_freezes_inherited_recognition_authority()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync();
        var draft = await fixture.Service.CreateLeaseInstallmentDraftAsync(
            lease.Id, lease.ScheduleLines.Single().Id);
        await fixture.ReviewLeaseTaxAsync(draft.Id);
        var workflowId = Guid.NewGuid();
        fixture.ConfigureWorkflow(approvalRequired: true, workflowId);

        var submitted = await fixture.Service.SubmitForApprovalAsync(draft.Id);

        submitted.Status.Should().Be(VendorInvoiceStatus.PendingApproval);
        var retained = await fixture.Context.VendorInvoices.AsNoTracking()
            .SingleAsync(item => item.Id == draft.Id);
        retained.SourceBookAuthorityId.Should().NotBeNull();
        fixture.BookAuthorities.Verify(item => item.RetainExistingPostedOriginalAsync(
            It.Is<FinanceSourceBookAuthorityFreezeRequest>(request =>
                request.SourceDocumentId == lease.Id && request.SourceDocumentType == "LeaseRecognition" &&
                request.FreezeStage == FinanceSourceBookAuthorityFreezeStages.LegacyPosted),
            lease.RecognitionJournalEntryId!.Value,
            lease.RecognitionPostingEventId!.Value,
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.BookAuthorities.Verify(item => item.FreezeInheritedAsync(
            It.Is<FinanceSourceBookAuthorityFreezeRequest>(request =>
                request.SourceDocumentId == draft.Id && request.SourceWorkflowInstanceId == workflowId &&
                request.FreezeStage == FinanceSourceBookAuthorityFreezeStages.Submitted),
            It.Is<IReadOnlyCollection<FinanceSourceBookAuthorityOriginRequest>>(origins =>
                origins.Count == 1 && origins.Single().Role == "LEASE_RECOGNITION"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Submit_without_approval_workflow_fails_without_fabricating_book_authority()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync();
        var draft = await fixture.Service.CreateLeaseInstallmentDraftAsync(
            lease.Id, lease.ScheduleLines.Single().Id);
        await fixture.ReviewLeaseTaxAsync(draft.Id);
        fixture.ConfigureWorkflow(approvalRequired: false, workflowId: null);

        var submit = () => fixture.Service.SubmitForApprovalAsync(draft.Id);

        await submit.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires an active maker/checker approval workflow*");
        var retained = await fixture.Context.VendorInvoices.AsNoTracking()
            .SingleAsync(item => item.Id == draft.Id);
        retained.Status.Should().Be(VendorInvoiceStatus.Draft);
        retained.SourceBookAuthorityId.Should().BeNull();
        fixture.BookAuthorities.Verify(item => item.FreezeInheritedAsync(
            It.IsAny<FinanceSourceBookAuthorityFreezeRequest>(),
            It.IsAny<IReadOnlyCollection<FinanceSourceBookAuthorityOriginRequest>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Legacy_approved_lease_invoice_without_authority_fails_before_posting()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync();
        var draft = await fixture.Service.CreateLeaseInstallmentDraftAsync(
            lease.Id, lease.ScheduleLines.Single().Id);
        await fixture.ReviewLeaseTaxAsync(draft.Id);
        var invoice = await fixture.Context.VendorInvoices.Include(item => item.LineItems)
            .SingleAsync(item => item.Id == draft.Id);
        invoice.Status = VendorInvoiceStatus.Approved;
        invoice.ApprovalRequired = true;
        invoice.ApprovalStatus = "Approved";
        invoice.ApprovedById = Guid.NewGuid();
        invoice.ApprovedDate = DateTime.UtcNow;
        await fixture.Context.SaveChangesAsync();

        var post = () => fixture.Service.PostAsync(invoice.Id);

        await post.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("LEASE_AP_SOURCE_BOOK_AUTHORITY_REQUIRED:*");
        fixture.FinanceEngine.Verify(item => item.PostAsync(
            It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<CancellationToken>()), Times.Never);
        (await fixture.Context.Set<LeaseScheduleLine>().AsNoTracking()
            .SingleAsync(item => item.Id == lease.ScheduleLines.Single().Id)).IsPosted.Should().BeFalse();
    }

    [Fact]
    public async Task Ap_post_uses_frozen_principal_interest_and_control_accounts_without_marking_paid()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync();
        var scheduleId = lease.ScheduleLines.Single().Id;
        var draft = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, scheduleId);
        var invoice = await fixture.Context.VendorInvoices
            .Include(item => item.LineItems)
            .SingleAsync(item => item.Id == draft.Id);
        invoice.WithholdingDecisionPending = false;
        invoice.Status = VendorInvoiceStatus.Approved;
        invoice.ApprovalRequired = false;
        invoice.ApprovalStatus = "NotRequired";
        foreach (var line in invoice.LineItems)
            line.TaxTreatment = TaxTreatment.OutOfScope;
        await fixture.AttachSubmittedAuthorityAsync(invoice);
        (await fixture.Context.AccountingBooks.SingleAsync(item => item.Id == fixture.BookId)).IsDefault = false;
        await fixture.Context.SaveChangesAsync();

        var posted = await fixture.Service.PostAsync(invoice.Id);

        fixture.PostedRequest.Should().NotBeNull();
        fixture.PostedRequest!.AccountingBookCode.Should().Be("PRIMARY");
        fixture.PostedRequest.FunctionalCurrencyCode.Should().Be("GHS");
        fixture.PostedRequest.Lines.Should().ContainSingle(line =>
            line.TransactionTag == "AP-LEASE-PRINCIPAL" &&
            line.AccountId == fixture.LiabilityAccountId && line.DebitAmount == 90m && line.CreditAmount == 0m);
        fixture.PostedRequest.Lines.Should().ContainSingle(line =>
            line.TransactionTag == "AP-LEASE-INTEREST" &&
            line.AccountId == fixture.InterestAccountId && line.DebitAmount == 10m && line.CreditAmount == 0m);
        fixture.PostedRequest.Lines.Should().ContainSingle(line =>
            line.TransactionTag == "AP-Control" &&
            line.AccountId == fixture.ApAccountId && line.DebitAmount == 0m && line.CreditAmount == 100m);
        posted.JournalEntryId.Should().NotBeNull();
        posted.PaidAmount.Should().Be(0m);
        posted.Status.Should().Be(VendorInvoiceStatus.Approved);
        (await fixture.Context.Set<LeaseScheduleLine>().AsNoTracking().SingleAsync(item => item.Id == scheduleId))
            .IsPosted.Should().BeTrue("AP recognition posted; settlement remains ordinary AP truth");
        (await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, scheduleId)).Id.Should().Be(invoice.Id,
            "an uncertain prepare response must reconcile to the canonical invoice even after lease completion");
        (await fixture.Context.Set<FinancePostingEvent>().CountAsync(item =>
            item.SourceDocumentType == "LeasePeriodPosting")).Should().Be(0);
        fixture.BookAuthorities.Verify(item => item.RequireForPostingAsync(
            It.Is<FinanceSourceBookAuthorityFreezeRequest>(request => request.SourceDocumentId == invoice.Id),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        fixture.BookAuthorities.Verify(item => item.BindOriginalPostingAsync(
            invoice.SourceBookAuthorityId!.Value,
            It.IsAny<Guid>(),
            posted.JournalEntryId!.Value,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Failed_ap_post_leaves_invoice_and_schedule_unposted_for_safe_retry()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync();
        var scheduleId = lease.ScheduleLines.Single().Id;
        var draft = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, scheduleId);
        var invoice = await fixture.Context.VendorInvoices
            .Include(item => item.LineItems)
            .SingleAsync(item => item.Id == draft.Id);
        invoice.WithholdingDecisionPending = false;
        invoice.Status = VendorInvoiceStatus.Approved;
        invoice.ApprovalRequired = false;
        invoice.ApprovalStatus = "NotRequired";
        foreach (var line in invoice.LineItems)
            line.TaxTreatment = TaxTreatment.OutOfScope;
        await fixture.AttachSubmittedAuthorityAsync(invoice);
        await fixture.Context.SaveChangesAsync();
        fixture.FinanceEngine.Setup(item => item.PostAsync(
                It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated posting-engine failure"));

        var post = () => fixture.Service.PostAsync(invoice.Id);

        await post.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("simulated posting-engine failure");
        var persisted = await fixture.Context.VendorInvoices.AsNoTracking().SingleAsync(item => item.Id == invoice.Id);
        persisted.JournalEntryId.Should().BeNull();
        persisted.PaidAmount.Should().Be(0m);
        (await fixture.Context.Set<LeaseScheduleLine>().AsNoTracking().SingleAsync(item => item.Id == scheduleId))
            .IsPosted.Should().BeFalse();
        fixture.BookAuthorities.Verify(item => item.BindOriginalPostingAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        (await fixture.Context.Set<FinancePostingEvent>().CountAsync(item =>
            item.SourceDocumentType == "LeasePeriodPosting")).Should().Be(0);
    }

    [Fact]
    public async Task Posted_then_voided_instalment_creates_one_retained_linked_replacement_and_retry_reuses_it()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync();
        var scheduleId = lease.ScheduleLines.Single().Id;
        var draft = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, scheduleId);
        var invoice = await fixture.Context.VendorInvoices
            .Include(item => item.LineItems)
            .SingleAsync(item => item.Id == draft.Id);
        invoice.WithholdingDecisionPending = false;
        invoice.Status = VendorInvoiceStatus.Approved;
        invoice.ApprovalRequired = false;
        invoice.ApprovalStatus = "NotRequired";
        foreach (var line in invoice.LineItems) line.TaxTreatment = TaxTreatment.OutOfScope;
        await fixture.AttachSubmittedAuthorityAsync(invoice);
        await fixture.Context.SaveChangesAsync();
        var posted = await fixture.Service.PostAsync(invoice.Id);
        var originalEventId = Guid.NewGuid();
        var originalJournal = new JournalEntry
        {
            Id = posted.JournalEntryId!.Value, TenantId = fixture.TenantId,
            JournalEntryNumber = "JE-AP-VOID-001", JournalType = "System Generated",
            EntryDate = invoice.InvoiceDate, PostingDate = invoice.InvoiceDate,
            Description = "AP lease instalment", SourceModule = "AP",
            SourceDocumentType = "VendorInvoice", SourceDocumentId = invoice.Id,
            TotalDebitAmount = 100m, TotalCreditAmount = 100m, IsBalanced = true,
            PostingStatus = "Posted", AccountingBookId = fixture.BookId, BookClassification = "PRIMARY"
        };
        fixture.Context.JournalEntries.Add(originalJournal);
        fixture.Context.Set<FinancePostingEvent>().Add(new FinancePostingEvent
        {
            Id = originalEventId, TenantId = fixture.TenantId, SourceModule = "AP",
            SourceDocumentType = "VendorInvoice", SourceDocumentId = invoice.Id,
            PostingAction = "Post", PostingStatus = "Posted", PostingDate = invoice.InvoiceDate,
            PostedAt = DateTime.UtcNow, JournalEntryId = originalJournal.Id, JournalEntry = originalJournal,
            TotalDebitAmount = 100m, TotalCreditAmount = 100m, FunctionalCurrencyCode = "GHS",
            BookClassification = "PRIMARY", AccountingBookId = fixture.BookId
        });
        fixture.SetBoundPostingEvidence(invoice.Id, originalEventId, originalJournal.Id);
        await fixture.Context.SaveChangesAsync();
        fixture.FinanceEngine.Setup(item => item.GetReversalPlanAsync(
                originalEventId, It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceReversalPlanDto
            {
                IsDefined = true, OriginalPostingEventId = originalEventId,
                OriginalJournalEntryId = originalJournal.Id, PostingAction = "Reverse",
                ReversalDate = DateTime.UtcNow.Date,
                ReversalLines = fixture.PostedRequest!.Lines.Select(line => new FinancePostingLineDto
                {
                    AccountId = line.AccountId, DebitAmount = line.CreditAmount,
                    CreditAmount = line.DebitAmount, TransactionCurrency = line.TransactionCurrency,
                    TransactionDebitAmount = line.TransactionCreditAmount,
                    TransactionCreditAmount = line.TransactionDebitAmount,
                    SourceDocumentLineId = line.SourceDocumentLineId,
                    TransactionTag = line.TransactionTag
                }).ToArray()
            });

        var voided = await fixture.Service.VoidAsync(invoice.Id, "approved lease correction");
        var replacement = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, scheduleId);
        var retry = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, scheduleId);

        voided.Status.Should().Be(VendorInvoiceStatus.Voided);
        replacement.Id.Should().NotBe(invoice.Id);
        replacement.ReplacesLeaseVendorInvoiceId.Should().Be(invoice.Id);
        retry.Id.Should().Be(replacement.Id);
        (await fixture.Context.VendorInvoices.CountAsync(item => !item.IsDeleted)).Should().Be(2);
        (await fixture.Context.VendorInvoices.AsNoTracking().SingleAsync(item => item.Id == invoice.Id))
            .Status.Should().Be(VendorInvoiceStatus.Voided);
        (await fixture.Context.Set<LeaseScheduleLine>().AsNoTracking().SingleAsync(item => item.Id == scheduleId))
            .IsPosted.Should().BeFalse("the replacement is only a draft");

        var replacementEntity = await fixture.Context.VendorInvoices
            .Include(item => item.LineItems)
            .SingleAsync(item => item.Id == replacement.Id);
        replacementEntity.WithholdingDecisionPending = false;
        replacementEntity.Status = VendorInvoiceStatus.Approved;
        replacementEntity.ApprovalRequired = false;
        replacementEntity.ApprovalStatus = "NotRequired";
        foreach (var line in replacementEntity.LineItems) line.TaxTreatment = TaxTreatment.OutOfScope;
        await fixture.AttachSubmittedAuthorityAsync(replacementEntity);
        await fixture.Context.SaveChangesAsync();
        await fixture.Service.PostAsync(replacement.Id);

        var originalVoidRetry = await fixture.Service.VoidAsync(invoice.Id, "idempotent retry");

        originalVoidRetry.Status.Should().Be(VendorInvoiceStatus.Voided);
        (await fixture.Context.Set<LeaseScheduleLine>().AsNoTracking().SingleAsync(item => item.Id == scheduleId))
            .IsPosted.Should().BeTrue("the posted replacement owns the current accounting state");
    }

    [Fact]
    public async Task Voided_terminal_with_settlement_evidence_cannot_be_replaced()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync();
        var schedule = lease.ScheduleLines.Single();
        var draft = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, schedule.Id);
        var invoice = await fixture.Context.VendorInvoices.SingleAsync(item => item.Id == draft.Id);
        invoice.Status = VendorInvoiceStatus.Voided;
        invoice.PaidAmount = 10m;
        await fixture.Context.SaveChangesAsync();

        var replace = () => fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, schedule.Id);

        await replace.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("LEASE_AP_REPLACEMENT_SETTLEMENT_EXISTS:*");
        (await fixture.Context.VendorInvoices.CountAsync(item => !item.IsDeleted)).Should().Be(1);
        (await fixture.Context.Set<LeaseScheduleLine>().AsNoTracking().SingleAsync(item => item.Id == schedule.Id))
            .IsPosted.Should().BeFalse();
    }

    [Fact]
    public async Task Replacement_rejects_predecessor_moved_to_another_schedule()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync(twoPeriods: true);
        var first = lease.ScheduleLines.Single(item => item.PeriodNumber == 1);
        var second = lease.ScheduleLines.Single(item => item.PeriodNumber == 2);
        var original = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, first.Id);
        var originalEntity = await fixture.Context.VendorInvoices.SingleAsync(item => item.Id == original.Id);
        originalEntity.Status = VendorInvoiceStatus.Voided;
        await fixture.Context.SaveChangesAsync();
        var replacement = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, first.Id);
        originalEntity = await fixture.Context.VendorInvoices.SingleAsync(item => item.Id == original.Id);
        originalEntity.LeaseScheduleLineId = second.Id;
        await fixture.Context.SaveChangesAsync();

        var retry = () => fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, first.Id);

        await retry.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("LEASE_AP_REPLACEMENT_AMBIGUOUS:*");
        replacement.ReplacesLeaseVendorInvoiceId.Should().Be(original.Id);
    }

    [Fact]
    public async Task Replacement_rejects_cross_tenant_predecessor_and_retains_both_records()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync();
        var schedule = lease.ScheduleLines.Single();
        var original = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, schedule.Id);
        var originalEntity = await fixture.Context.VendorInvoices.SingleAsync(item => item.Id == original.Id);
        originalEntity.Status = VendorInvoiceStatus.Voided;
        await fixture.Context.SaveChangesAsync();
        var replacement = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, schedule.Id);
        var foreignPredecessor = new VendorInvoice
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), InvoiceNumber = "VI-FOREIGN-PREDECESSOR",
            BusinessPartnerId = Guid.NewGuid(), SupplierName = "Foreign tenant supplier",
            BusinessPartnerCode = "FOREIGN", InvoiceDate = schedule.PeriodDate,
            DueDate = schedule.PeriodDate, CurrencyCode = "GHS", ExchangeRate = 1m,
            Status = VendorInvoiceStatus.Voided, ApprovalStatus = "Voided"
        };
        fixture.Context.VendorInvoices.Add(foreignPredecessor);
        var replacementEntity = await fixture.Context.VendorInvoices.SingleAsync(item => item.Id == replacement.Id);
        replacementEntity.ReplacesLeaseVendorInvoiceId = foreignPredecessor.Id;
        await fixture.Context.SaveChangesAsync();

        var retry = () => fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, schedule.Id);

        await retry.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("LEASE_AP_REPLACEMENT_AMBIGUOUS:*");
        (await fixture.Context.VendorInvoices.IgnoreQueryFilters().CountAsync()).Should().Be(3);
        originalEntity.TenantId.Should().Be(fixture.TenantId);
        foreignPredecessor.TenantId.Should().NotBe(fixture.TenantId);
    }

    [Fact]
    public async Task Replacement_retry_rejects_cyclic_retained_lineage_even_with_one_active_invoice()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync();
        var schedule = lease.ScheduleLines.Single();
        var original = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, schedule.Id);
        var originalEntity = await fixture.Context.VendorInvoices.SingleAsync(item => item.Id == original.Id);
        originalEntity.Status = VendorInvoiceStatus.Voided;
        await fixture.Context.SaveChangesAsync();
        var replacement = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, schedule.Id);
        originalEntity = await fixture.Context.VendorInvoices.SingleAsync(item => item.Id == original.Id);
        originalEntity.ReplacesLeaseVendorInvoiceId = replacement.Id;
        await fixture.Context.SaveChangesAsync();

        var retry = () => fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, schedule.Id);

        await retry.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("LEASE_AP_REPLACEMENT_AMBIGUOUS:*");
        (await fixture.Context.VendorInvoices.CountAsync(item => !item.IsDeleted)).Should().Be(2);
    }

    [Fact]
    public async Task Immutable_schedule_amount_edit_is_rejected_without_mutating_the_draft()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync();
        var created = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, lease.ScheduleLines.Single().Id);
        var principal = created.LineItems.Single(item => item.LeaseComponent == LeaseInvoiceComponent.Principal);
        var interest = created.LineItems.Single(item => item.LeaseComponent == LeaseInvoiceComponent.Interest);

        var update = () => fixture.Service.UpdateAsync(new VendorInvoiceUpdateDto
        {
            Id = created.Id,
            InvoiceDate = created.InvoiceDate,
            DueDate = created.DueDate,
            CurrencyCode = created.CurrencyCode,
            ExchangeRate = created.ExchangeRate,
            PaymentTermsDays = created.PaymentTermsDays,
            MatchingType = InvoiceMatchingType.None,
            Reference = created.Reference,
            Notes = created.Notes,
            ApplySupplierWithholdingDefaults = false,
            LineItems =
            [
                CopyLine(principal, principal.UnitPrice + 1m),
                CopyLine(interest, interest.UnitPrice)
            ]
        });

        await update.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*amounts, principal/interest accounts, date, currency, book and source lineage cannot be changed*");
        var stored = await fixture.Context.VendorInvoices.AsNoTracking()
            .Include(item => item.LineItems)
            .SingleAsync(item => item.Id == created.Id);
        stored.SubTotal.Should().Be(100m);
        stored.LineItems.Single(item => item.LeaseComponent == LeaseInvoiceComponent.Principal)
            .UnitPrice.Should().Be(90m);
    }

    [Fact]
    public async Task Existing_standalone_period_journal_blocks_ap_creation_and_never_posts_twice()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync();
        var schedule = lease.ScheduleLines.Single();
        fixture.Context.Set<FinancePostingEvent>().Add(new FinancePostingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            SourceModule = "FixedAssets",
            SourceDocumentType = "LeasePeriodPosting",
            SourceDocumentId = schedule.Id,
            PostingStatus = "Posted",
            PostingDate = schedule.PeriodDate,
            PostedAt = DateTime.UtcNow,
            FunctionalCurrencyCode = "GHS",
            BookClassification = "PRIMARY",
            AccountingBookId = fixture.BookId
        });
        await fixture.Context.SaveChangesAsync();

        var prepare = () => fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, schedule.Id);

        await prepare.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*standalone lease-period journal already exists*");
        (await fixture.Context.VendorInvoices.CountAsync()).Should().Be(0);
        (await fixture.Context.Set<FinancePostingEvent>().CountAsync(item =>
            item.SourceDocumentType == "LeasePeriodPosting")).Should().Be(1);
        (await fixture.Context.Set<LeaseScheduleLine>().AsNoTracking().SingleAsync(item => item.Id == schedule.Id))
            .IsPosted.Should().BeFalse();
    }

    [Fact]
    public async Task Legacy_interest_lease_without_original_authority_fails_closed_and_rolls_back()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync(freezeAuthority: false);
        var schedule = lease.ScheduleLines.Single();

        var prepare = () => fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, schedule.Id);

        await prepare.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("LEASE_AUTHORITY_REMEDIATION_REQUIRED:*");
        (await fixture.Context.VendorInvoices.CountAsync()).Should().Be(0);
        var unchanged = await fixture.Context.LeaseContracts.AsNoTracking().SingleAsync(item => item.Id == lease.Id);
        unchanged.AccountingBookId.Should().BeNull();
        unchanged.InterestExpenseAccountId.Should().BeNull();
        (await fixture.Context.Set<LeaseScheduleLine>().AsNoTracking().SingleAsync(item => item.Id == schedule.Id))
            .IsPosted.Should().BeFalse();
    }

    [Fact]
    public async Task Legacy_primary_recognition_ignores_generated_replica_and_freezes_only_original_authority()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync(freezeAuthority: false);
        var schedule = lease.ScheduleLines.Single();
        schedule.InterestExpense = 0m;
        schedule.PrincipalReduction = schedule.PaymentAmount;
        await fixture.Context.SaveChangesAsync();
        var original = await fixture.SeedLegacyRecognitionAsync(lease, includeGeneratedReplica: true);

        var draft = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, schedule.Id);

        draft.LeaseAccountingBookId.Should().Be(fixture.BookId);
        draft.LineItems.Should().ContainSingle(item =>
            item.LeaseComponent == LeaseInvoiceComponent.Principal &&
            item.GLAccountId == fixture.LiabilityAccountId && item.UnitPrice == schedule.PaymentAmount);
        var frozen = await fixture.Context.LeaseContracts.AsNoTracking().SingleAsync(item => item.Id == lease.Id);
        frozen.RecognitionPostingEventId.Should().Be(original.EventId);
        frozen.RecognitionJournalEntryId.Should().Be(original.JournalId);
        frozen.InterestExpenseAccountId.Should().Be(fixture.LiabilityAccountId,
            "a zero-interest legacy lease needs no inferred current interest default");
    }

    [Fact]
    public async Task Legacy_interest_authority_rejects_reversed_period_journal_and_rolls_back_freeze()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync(twoPeriods: true, freezeAuthority: false);
        var first = lease.ScheduleLines.Single(item => item.PeriodNumber == 1);
        var second = lease.ScheduleLines.Single(item => item.PeriodNumber == 2);
        first.IsPosted = true;
        await fixture.Context.SaveChangesAsync();
        await fixture.SeedLegacyRecognitionAsync(lease);
        await fixture.SeedLegacyPeriodInterestAsync(lease, first, reversed: true);

        var prepare = () => fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, second.Id);

        await prepare.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("LEASE_AUTHORITY_REMEDIATION_REQUIRED:*immutable posted interest-account evidence*");
        (await fixture.Context.VendorInvoices.CountAsync()).Should().Be(0);
        var unchanged = await fixture.Context.LeaseContracts.AsNoTracking().SingleAsync(item => item.Id == lease.Id);
        unchanged.RecognitionPostingEventId.Should().BeNull();
        unchanged.InterestExpenseAccountId.Should().BeNull();
        (await fixture.Context.Set<LeaseScheduleLine>().AsNoTracking().SingleAsync(item => item.Id == second.Id))
            .IsPosted.Should().BeFalse();
    }

    [Fact]
    public async Task Frozen_recognition_authority_rejects_wrong_entry_date_even_when_posting_date_matches()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync();
        var journal = await fixture.Context.JournalEntries.SingleAsync(item =>
            item.Id == lease.RecognitionJournalEntryId);
        journal.EntryDate = lease.StartDate.AddDays(1);
        journal.PostingDate = lease.StartDate;
        await fixture.Context.SaveChangesAsync();

        var prepare = () => fixture.Service.CreateLeaseInstallmentDraftAsync(
            lease.Id, lease.ScheduleLines.Single().Id);

        await prepare.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("LEASE_AUTHORITY_REMEDIATION_REQUIRED:*");
        (await fixture.Context.VendorInvoices.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Legacy_period_interest_authority_rejects_wrong_entry_date_and_rolls_back_freeze()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync(twoPeriods: true, freezeAuthority: false);
        var first = lease.ScheduleLines.Single(item => item.PeriodNumber == 1);
        var second = lease.ScheduleLines.Single(item => item.PeriodNumber == 2);
        first.IsPosted = true;
        await fixture.Context.SaveChangesAsync();
        await fixture.SeedLegacyRecognitionAsync(lease);
        await fixture.SeedLegacyPeriodInterestAsync(lease, first, reversed: false, entryDateOffsetDays: 1);

        var prepare = () => fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, second.Id);

        await prepare.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("LEASE_AUTHORITY_REMEDIATION_REQUIRED:*immutable posted interest-account evidence*");
        (await fixture.Context.VendorInvoices.CountAsync()).Should().Be(0);
        var unchanged = await fixture.Context.LeaseContracts.AsNoTracking().SingleAsync(item => item.Id == lease.Id);
        unchanged.RecognitionPostingEventId.Should().BeNull();
        unchanged.InterestExpenseAccountId.Should().BeNull();
    }

    [Fact]
    public async Task Deleting_unposted_draft_releases_source_for_truthful_recreation()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync();
        var schedule = lease.ScheduleLines.Single();
        var first = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, schedule.Id);

        await fixture.Service.DeleteAsync(first.Id);
        var replacement = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, schedule.Id);

        replacement.Id.Should().NotBe(first.Id);
        (await fixture.Context.VendorInvoices.CountAsync(item => !item.IsDeleted)).Should().Be(1);
        (await fixture.Context.VendorInvoices.IgnoreQueryFilters().CountAsync(item => item.IsDeleted)).Should().Be(1);
        (await fixture.Context.Set<LeaseScheduleLine>().AsNoTracking().SingleAsync(item => item.Id == schedule.Id))
            .IsPosted.Should().BeFalse();
    }

    [Fact]
    public async Task Void_retry_with_settlement_evidence_cannot_reopen_the_instalment()
    {
        await using var fixture = new Fixture();
        var lease = await fixture.AddLeaseAsync();
        var scheduleId = lease.ScheduleLines.Single().Id;
        var draft = await fixture.Service.CreateLeaseInstallmentDraftAsync(lease.Id, scheduleId);
        var invoice = await fixture.Context.VendorInvoices.SingleAsync(item => item.Id == draft.Id);
        invoice.Status = VendorInvoiceStatus.Voided;
        var schedule = await fixture.Context.Set<LeaseScheduleLine>().SingleAsync(item => item.Id == scheduleId);
        schedule.IsPosted = true;
        fixture.Context.Set<VendorPaymentAllocation>().Add(new VendorPaymentAllocation
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, VendorPaymentId = Guid.NewGuid(),
            VendorInvoiceId = invoice.Id, VendorInvoice = invoice, AllocatedAmount = 25m,
            PaymentCurrencyAmount = 25m, InvoiceCurrencyCode = "GHS", PaymentCurrencyCode = "GHS",
            PaymentFunctionalAmount = 25m, SettlementFunctionalAmount = 25m
        });
        await fixture.Context.SaveChangesAsync();

        var retryVoid = () => fixture.Service.VoidAsync(invoice.Id, "retry after uncertain response");

        await retryVoid.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*retains active settlement evidence*");
        (await fixture.Context.Set<LeaseScheduleLine>().AsNoTracking().SingleAsync(item => item.Id == scheduleId))
            .IsPosted.Should().BeTrue("settlement evidence must be reconciled before reopening the instalment");
    }

    [Fact]
    public async Task Another_tenants_lease_source_is_not_addressable()
    {
        await using var fixture = new Fixture();
        var otherLease = await fixture.AddLeaseAsync(tenantId: Guid.NewGuid());

        var prepare = () => fixture.Service.CreateLeaseInstallmentDraftAsync(
            otherLease.Id, otherLease.ScheduleLines.Single().Id);

        await prepare.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found in this tenant*");
        (await fixture.Context.VendorInvoices.CountAsync()).Should().Be(0);
    }

    private static VendorInvoiceLineItemCreateDto CopyLine(VendorInvoiceLineItemDto line, decimal amount) => new()
    {
        Id = line.Id,
        LineItemType = line.LineItemType,
        GLAccountId = line.GLAccountId,
        Description = line.Description,
        Quantity = line.Quantity,
        UnitPrice = amount,
        TaxTreatment = line.TaxTreatment,
        DiscountPercentage = line.DiscountPercentage,
        Unit = line.Unit
    };

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _recognitionAuthorityId = Guid.NewGuid();
        private readonly Dictionary<Guid, Guid> _authorityIds = [];
        private readonly Dictionary<Guid, FinanceSourceBookAuthorityFreezeRequest> _authorityRequests = [];
        private readonly Dictionary<Guid, (Guid EventId, Guid JournalId)> _boundEvidence = [];
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid BookId { get; } = Guid.NewGuid();
        public Guid RouAccountId { get; } = Guid.NewGuid();
        public Guid LiabilityAccountId { get; } = Guid.NewGuid();
        public Guid InterestAccountId { get; } = Guid.NewGuid();
        public Guid ApAccountId { get; } = Guid.NewGuid();
        public ApplicationDbContext Context { get; }
        public VendorInvoiceService Service { get; }
        public Mock<IFinancePostingEngine> FinanceEngine { get; } = new();
        public Mock<IFinanceSourceBookAuthorityService> BookAuthorities { get; } = new();
        public Mock<IWorkflowIntegrationService> WorkflowIntegration { get; } = new();
        public FinancePostingRequestV2Dto? PostedRequest { get; private set; }

        public Fixture()
        {
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
            var current = new Mock<ICurrentUserService>();
            current.SetupGet(item => item.TenantId).Returns(TenantId);
            current.SetupGet(item => item.UserName).Returns("lease-ap-maker");
            current.SetupGet(item => item.UserId).Returns(_userId.ToString());
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            var numbering = new Mock<IDocumentNumberingService>();
            numbering.Setup(item => item.GenerateAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<DateTime?>(),
                    It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => $"VI-{Guid.NewGuid():N}");
            var dimensions = new Mock<IFinanceSourceDimensionService>();
            dimensions.Setup(item => item.SynchronizeDraftAsync(
                    It.IsAny<FinancePostingProducerContext>(), It.IsAny<Guid>(), It.IsAny<DateTime>(),
                    It.IsAny<IReadOnlyList<FinanceSourceDocumentLineContext>>(),
                    It.IsAny<FinanceSourceDocumentDimensionInputDto?>(), It.IsAny<bool>(),
                    It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FinanceSourceDocumentDimensionDto());
            dimensions.Setup(item => item.GetAsync(
                    It.IsAny<FinancePostingProducerContext>(), It.IsAny<Guid>(), It.IsAny<DateTime>(),
                    It.IsAny<IReadOnlyList<FinanceSourceDocumentLineContext>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FinanceSourceDocumentDimensionDto());
            BookAuthorities.Setup(item => item.RetainExistingPostedOriginalAsync(
                    It.IsAny<FinanceSourceBookAuthorityFreezeRequest>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((FinanceSourceBookAuthorityFreezeRequest request, Guid journalId, Guid? eventId,
                    CancellationToken _) => AuthorityResult(
                        request, _recognitionAuthorityId, eventId, journalId,
                        FinanceSourceBookAuthoritySelectionBases.RetainedPostedOriginal));
            BookAuthorities.Setup(item => item.FreezeInheritedAsync(
                    It.IsAny<FinanceSourceBookAuthorityFreezeRequest>(),
                    It.IsAny<IReadOnlyCollection<FinanceSourceBookAuthorityOriginRequest>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((FinanceSourceBookAuthorityFreezeRequest request,
                    IReadOnlyCollection<FinanceSourceBookAuthorityOriginRequest> _, CancellationToken _) =>
                {
                    var authorityId = AuthorityIdFor(request.SourceDocumentId);
                    _authorityRequests[authorityId] = request;
                    return AuthorityResult(request, authorityId);
                });
            BookAuthorities.Setup(item => item.RequireForPostingAsync(
                    It.IsAny<FinanceSourceBookAuthorityFreezeRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FinanceSourceBookAuthorityFreezeRequest request, CancellationToken _) =>
                {
                    var authorityId = AuthorityIdFor(request.SourceDocumentId);
                    _authorityRequests[authorityId] = request;
                    return AuthorityResult(request, authorityId);
                });
            BookAuthorities.Setup(item => item.BindOriginalPostingAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid authorityId, Guid eventId, Guid journalId, CancellationToken _) =>
                {
                    _boundEvidence[authorityId] = (eventId, journalId);
                    return AuthorityResult(_authorityRequests[authorityId], authorityId, eventId, journalId);
                });
            BookAuthorities.Setup(item => item.RequireBoundOriginalAsync(
                    It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid authorityId, CancellationToken _) =>
                {
                    var evidence = _boundEvidence[authorityId];
                    return AuthorityResult(_authorityRequests[authorityId], authorityId,
                        evidence.EventId, evidence.JournalId);
                });
            FinanceEngine.Setup(item => item.PostAsync(
                    It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<CancellationToken>()))
                .Callback<FinancePostingRequestV2Dto, CancellationToken>((request, _) => PostedRequest = request)
                .ReturnsAsync((FinancePostingRequestV2Dto request, CancellationToken _) => new FinancePostingResultDto
                {
                    PostingEventId = Guid.NewGuid(), JournalEntryId = Guid.NewGuid(),
                    JournalEntryNumber = $"JE-AP-{Guid.NewGuid():N}", PostingStatus = "Posted",
                    TotalDebitAmount = request.Lines.Sum(line => line.DebitAmount),
                    TotalCreditAmount = request.Lines.Sum(line => line.CreditAmount),
                    FunctionalCurrencyCode = request.FunctionalCurrencyCode,
                    PostingDate = request.PostingDate, SourceModule = request.SourceModule,
                    OriginModuleCode = request.OriginModuleCode ?? "FIN",
                    SourceDocumentType = request.SourceDocumentType,
                    SourceDocumentId = request.SourceDocumentId, PostingAction = request.PostingAction
                });
            Service = new VendorInvoiceService(
                new UnitOfWork(Context), current.Object, Mock.Of<IInventoryValuationService>(),
                NullLogger<VendorInvoiceService>.Instance, numbering.Object, Mock.Of<IWorkflowService>(),
                financePostingEngine: FinanceEngine.Object,
                sourceDimensions: dimensions.Object,
                workflowIntegration: WorkflowIntegration.Object,
                sourceBookAuthorities: BookAuthorities.Object);
        }

        public void ConfigureWorkflow(bool approvalRequired, Guid? workflowId)
        {
            WorkflowIntegration.Setup(item => item.SubmitAsync("VendorInvoice", It.IsAny<Guid>()))
                .ReturnsAsync(new WorkflowIntegrationResult(
                    new WorkflowExecutionResult
                    {
                        Success = true,
                        Status = approvalRequired ? WorkflowInstanceStatus.InProgress : WorkflowInstanceStatus.Completed,
                        WorkflowInstanceId = workflowId
                    },
                    approvalRequired ? WorkflowOutcome.Pending : WorkflowOutcome.Approved,
                    approvalRequired));
        }

        public async Task ReviewLeaseTaxAsync(Guid invoiceId)
        {
            var invoice = await Context.VendorInvoices.Include(item => item.LineItems)
                .SingleAsync(item => item.Id == invoiceId);
            invoice.WithholdingDecisionPending = false;
            foreach (var line in invoice.LineItems.Where(item => !item.IsDeleted))
                line.TaxTreatment = TaxTreatment.OutOfScope;
            await Context.SaveChangesAsync();
        }

        public async Task<Guid> AttachSubmittedAuthorityAsync(VendorInvoice invoice)
        {
            var authorityId = AuthorityIdFor(invoice.Id);
            var workflowId = Guid.NewGuid();
            var request = new FinanceSourceBookAuthorityFreezeRequest
            {
                OriginModuleCode = "FIN",
                SourceDocumentType = "VENDORINVOICE",
                SourceDocumentId = invoice.Id,
                PostingAction = "POST",
                EffectiveDate = invoice.InvoiceDate,
                TransactionCurrencyCode = invoice.CurrencyCode,
                FreezeStage = FinanceSourceBookAuthorityFreezeStages.Submitted,
                SourceWorkflowInstanceId = workflowId
            };
            _authorityRequests[authorityId] = request;
            invoice.SourceBookAuthorityId = authorityId;
            if (!await Context.FinanceSourceBookAuthorities.AnyAsync(item => item.Id == authorityId))
            {
                Context.FinanceSourceBookAuthorities.Add(new FinanceSourceBookAuthority
                {
                    Id = authorityId,
                    TenantId = TenantId,
                    OriginModuleCode = "FIN",
                    SourceDocumentType = "VENDORINVOICE",
                    SourceDocumentId = invoice.Id,
                    PostingAction = "POST",
                    AuthorityVersion = 1,
                    SourceWorkflowInstanceId = workflowId,
                    FreezeStage = FinanceSourceBookAuthorityFreezeStages.Submitted,
                    EffectiveDate = invoice.InvoiceDate,
                    AccountingBookId = BookId,
                    AccountingBookCode = "PRIMARY",
                    FunctionalCurrencyCode = "GHS",
                    TransactionCurrencyCode = invoice.CurrencyCode,
                    SelectionBasis = FinanceSourceBookAuthoritySelectionBases.InheritedOriginal,
                    AuthorityFingerprint = new string('A', 64),
                    FrozenByUserId = _userId,
                    FrozenAtUtc = DateTime.UtcNow
                });
            }
            await Context.SaveChangesAsync();
            return authorityId;
        }

        public void SetBoundPostingEvidence(Guid invoiceId, Guid eventId, Guid journalId)
        {
            var authorityId = AuthorityIdFor(invoiceId);
            _boundEvidence[authorityId] = (eventId, journalId);
        }

        private Guid AuthorityIdFor(Guid sourceDocumentId)
        {
            if (_authorityIds.TryGetValue(sourceDocumentId, out var authorityId)) return authorityId;
            authorityId = Guid.NewGuid();
            _authorityIds[sourceDocumentId] = authorityId;
            return authorityId;
        }

        private FinanceSourceBookAuthorityResult AuthorityResult(
            FinanceSourceBookAuthorityFreezeRequest request,
            Guid authorityId,
            Guid? eventId = null,
            Guid? journalId = null,
            string selectionBasis = FinanceSourceBookAuthoritySelectionBases.InheritedOriginal) => new(
                authorityId,
                1,
                "FIN",
                request.SourceDocumentType.Trim().ToUpperInvariant(),
                request.SourceDocumentId,
                "POST",
                request.EffectiveDate.Date,
                request.FreezeStage,
                request.SourceWorkflowInstanceId,
                request.SourceWorkflowEntityType,
                BookId,
                "PRIMARY",
                "GHS",
                request.TransactionCurrencyCode.Trim().ToUpperInvariant(),
                selectionBasis,
                new string('A', 64),
                eventId,
                journalId);

        public async Task<LeaseContract> AddLeaseAsync(
            bool twoPeriods = false,
            Guid? tenantId = null,
            bool freezeAuthority = true)
        {
            var ownerTenantId = tenantId ?? TenantId;
            var partner = new BusinessPartner
            {
                Id = Guid.NewGuid(), TenantId = ownerTenantId, PartnerCode = $"LESSOR-{Guid.NewGuid():N}",
                PartnerName = "Governed Lease Lessor", LegalName = "Governed Lease Lessor Limited",
                TaxIdentificationNumber = "TIN-LEASE-001", PartnerType = "Supplier",
                RegistrationStatus = "Approved", ApprovalStatus = "Approved", IsActive = true, Currency = "GHS"
            };
            var role = new BusinessPartnerRole
            {
                Id = Guid.NewGuid(), TenantId = ownerTenantId, BusinessPartnerId = partner.Id,
                RoleType = BusinessPartnerRoleType.Supplier, Status = BusinessPartnerRoleStatus.Active,
                ActiveFromUtc = new DateTime(2025, 1, 1)
            };
            var profile = new BusinessPartnerApProfileVersion
            {
                Id = Guid.NewGuid(), TenantId = ownerTenantId, BusinessPartnerRoleId = role.Id,
                VersionNumber = 1, Status = BusinessPartnerFinanceProfileStatus.Approved,
                EffectiveFrom = new DateTime(2025, 1, 1), SubjectToWithholding = false,
                ApprovedAtUtc = new DateTime(2025, 1, 1), ApprovedById = Guid.NewGuid()
            };
            var bookId = ownerTenantId == TenantId ? BookId : Guid.NewGuid();
            var book = new AccountingBook
            {
                Id = bookId, TenantId = ownerTenantId, Code = "PRIMARY", Name = "Primary Book",
                Purpose = "Statutory", BookType = AccountingBookType.PrimaryFull,
                LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS",
                IsActive = true, IsDefault = true, AllowsPosting = true
            };
            var period = new FiscalPeriod
            {
                Id = Guid.NewGuid(), TenantId = ownerTenantId, FiscalYearId = Guid.NewGuid(),
                PeriodName = "FY 2026", PeriodCode = "2026", PeriodNumber = 1,
                StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31),
                PeriodDays = 365, PeriodStatus = "Open", IsOpen = true
            };
            var recognitionEventId = freezeAuthority ? Guid.NewGuid() : (Guid?)null;
            var recognitionJournalId = freezeAuthority ? Guid.NewGuid() : (Guid?)null;
            var lease = new LeaseContract
            {
                Id = Guid.NewGuid(), TenantId = ownerTenantId, ContractNumber = $"LEASE-{Guid.NewGuid():N}",
                Description = "Office lease", LessorId = partner.Id, Lessor = partner,
                StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 8, 31),
                MonthlyPaymentAmount = 100m, PaymentFrequency = PaymentFrequency.Monthly,
                AnnualDiscountRate = .08m, TotalPeriods = twoPeriods ? 2 : 1, PresentValue = twoPeriods ? 190m : 95m,
                Status = LeaseStatus.Active, RowVersion = [],
                RecognitionPostingEventId = recognitionEventId,
                RecognitionJournalEntryId = recognitionJournalId,
                AccountingBookId = freezeAuthority ? book.Id : null,
                AccountingBookCode = freezeAuthority ? book.Code : null,
                FunctionalCurrencyCode = freezeAuthority ? "GHS" : null,
                RouAssetAccountId = freezeAuthority ? RouAccountId : null,
                LeaseLiabilityAccountId = freezeAuthority ? LiabilityAccountId : null,
                InterestExpenseAccountId = freezeAuthority ? InterestAccountId : null
            };
            lease.ScheduleLines.Add(Line(lease, 1, new DateTime(2026, 9, 30), 90m, 10m, twoPeriods ? 100m : 5m));
            if (twoPeriods)
                lease.ScheduleLines.Add(Line(lease, 2, new DateTime(2026, 10, 31), 95m, 5m, 0m));

            Context.BusinessPartners.Add(partner);
            Context.Set<BusinessPartnerRole>().Add(role);
            Context.Set<BusinessPartnerApProfileVersion>().Add(profile);
            Context.AccountingBooks.Add(book);
            Context.FiscalPeriods.Add(period);
            Context.FinanceSettings.Add(new FinanceSettings
            {
                Id = Guid.NewGuid(), TenantId = ownerTenantId, BaseCurrency = "GHS",
                ControlAccountApId = ApAccountId
            });
            Context.Accounts.AddRange(
                Account(RouAccountId, ownerTenantId, "ROU", AccountType.Asset),
                Account(LiabilityAccountId, ownerTenantId, "LEASE-LIAB", AccountType.Liability, isControl: true),
                Account(InterestAccountId, ownerTenantId, "LEASE-INT", AccountType.Expense),
                Account(ApAccountId, ownerTenantId, "AP-CONTROL", AccountType.Liability, isControl: true));
            Context.LeaseContracts.Add(lease);
            if (freezeAuthority)
            {
                var journal = new JournalEntry
                {
                    Id = recognitionJournalId!.Value, TenantId = ownerTenantId,
                    JournalEntryNumber = $"JE-{Guid.NewGuid():N}", JournalType = "System Generated",
                    EntryDate = lease.StartDate, PostingDate = lease.StartDate,
                    Description = $"Lease recognition {lease.ContractNumber}",
                    SourceModule = "FixedAssets", SourceDocumentType = "LeaseRecognition",
                    SourceDocumentId = lease.Id, TotalDebitAmount = lease.PresentValue,
                    TotalCreditAmount = lease.PresentValue, IsBalanced = true, PostingStatus = "Posted",
                    AccountingBookId = book.Id, BookClassification = book.Code
                };
                var recognition = new FinancePostingEvent
                {
                    Id = recognitionEventId!.Value, TenantId = ownerTenantId, SourceModule = "FixedAssets",
                    SourceDocumentType = "LeaseRecognition", SourceDocumentId = lease.Id,
                    PostingAction = "Post", PostingStatus = "Posted", PostingDate = lease.StartDate, PostedAt = lease.StartDate,
                    JournalEntryId = journal.Id, JournalEntry = journal,
                    TotalDebitAmount = lease.PresentValue, TotalCreditAmount = lease.PresentValue,
                    FunctionalCurrencyCode = "GHS", BookClassification = book.Code,
                    AccountingBookId = book.Id
                };
                Context.JournalEntries.Add(journal);
                Context.Set<FinancePostingEvent>().Add(recognition);
                Context.AccountTransactions.AddRange(
                    RecognitionLine(lease, journal, period.Id, RouAccountId, "ROU-ASSET", lease.PresentValue, 0m),
                    RecognitionLine(lease, journal, period.Id, LiabilityAccountId, "LEASE-LIABILITY", 0m, lease.PresentValue));
            }
            await Context.SaveChangesAsync();
            return lease;
        }

        public async Task<(Guid EventId, Guid JournalId)> SeedLegacyRecognitionAsync(
            LeaseContract lease,
            bool includeGeneratedReplica = false)
        {
            var periodId = await Context.FiscalPeriods.Where(item => item.TenantId == lease.TenantId)
                .Select(item => item.Id).SingleAsync();
            var journal = new JournalEntry
            {
                Id = Guid.NewGuid(), TenantId = lease.TenantId,
                JournalEntryNumber = $"JE-LEGACY-{Guid.NewGuid():N}", JournalType = "System Generated",
                EntryDate = lease.StartDate, PostingDate = lease.StartDate,
                Description = $"Original recognition {lease.ContractNumber}", SourceModule = "FixedAssets",
                SourceDocumentType = "LeaseRecognition", SourceDocumentId = lease.Id,
                TotalDebitAmount = lease.PresentValue, TotalCreditAmount = lease.PresentValue,
                IsBalanced = true, PostingStatus = "Posted", AccountingBookId = BookId,
                BookClassification = "PRIMARY"
            };
            var postingEvent = new FinancePostingEvent
            {
                Id = Guid.NewGuid(), TenantId = lease.TenantId, SourceModule = "FixedAssets",
                SourceDocumentType = "LeaseRecognition", SourceDocumentId = lease.Id,
                PostingAction = "Post", PostingStatus = "Posted", PostingDate = lease.StartDate,
                PostedAt = lease.StartDate, JournalEntryId = journal.Id, JournalEntry = journal,
                TotalDebitAmount = lease.PresentValue, TotalCreditAmount = lease.PresentValue,
                FunctionalCurrencyCode = "GHS", BookClassification = "PRIMARY", AccountingBookId = BookId
            };
            Context.JournalEntries.Add(journal);
            Context.Set<FinancePostingEvent>().Add(postingEvent);
            Context.AccountTransactions.AddRange(
                RecognitionLine(lease, journal, periodId, RouAccountId, "ROU-ASSET", lease.PresentValue, 0m),
                RecognitionLine(lease, journal, periodId, LiabilityAccountId, "LEASE-LIABILITY", 0m, lease.PresentValue));
            if (includeGeneratedReplica)
            {
                var replica = new JournalEntry
                {
                    Id = Guid.NewGuid(), TenantId = lease.TenantId,
                    JournalEntryNumber = $"JE-REPLICA-{Guid.NewGuid():N}", JournalType = "System Generated",
                    EntryDate = lease.StartDate, PostingDate = lease.StartDate,
                    Description = $"Generated replica {lease.ContractNumber}", SourceModule = "FixedAssets",
                    SourceDocumentType = "LeaseRecognition", SourceDocumentId = lease.Id,
                    TotalDebitAmount = lease.PresentValue, TotalCreditAmount = lease.PresentValue,
                    IsBalanced = true, PostingStatus = "Posted", AccountingBookId = BookId,
                    BookClassification = "PRIMARY", ReplicatedFromJournalEntryId = journal.Id
                };
                Context.JournalEntries.Add(replica);
                Context.Set<FinancePostingEvent>().Add(new FinancePostingEvent
                {
                    Id = Guid.NewGuid(), TenantId = lease.TenantId, SourceModule = "FixedAssets",
                    SourceDocumentType = "LeaseRecognition", SourceDocumentId = lease.Id,
                    PostingAction = "Post", PostingStatus = "Posted", PostingDate = lease.StartDate,
                    PostedAt = lease.StartDate, JournalEntryId = replica.Id, JournalEntry = replica,
                    TotalDebitAmount = lease.PresentValue, TotalCreditAmount = lease.PresentValue,
                    FunctionalCurrencyCode = "GHS", BookClassification = "PRIMARY", AccountingBookId = BookId
                });
            }
            await Context.SaveChangesAsync();
            return (postingEvent.Id, journal.Id);
        }

        public async Task SeedLegacyPeriodInterestAsync(
            LeaseContract lease,
            LeaseScheduleLine schedule,
            bool reversed,
            int entryDateOffsetDays = 0)
        {
            var periodId = await Context.FiscalPeriods.Where(item => item.TenantId == lease.TenantId)
                .Select(item => item.Id).SingleAsync();
            var journal = new JournalEntry
            {
                Id = Guid.NewGuid(), TenantId = lease.TenantId,
                JournalEntryNumber = $"JE-LEGACY-PERIOD-{Guid.NewGuid():N}", JournalType = "System Generated",
                EntryDate = schedule.PeriodDate.AddDays(entryDateOffsetDays), PostingDate = schedule.PeriodDate,
                Description = $"Legacy period {schedule.PeriodNumber}", SourceModule = "FixedAssets",
                SourceDocumentType = "LeasePeriodPosting", SourceDocumentId = schedule.Id,
                TotalDebitAmount = schedule.PaymentAmount, TotalCreditAmount = schedule.PaymentAmount,
                IsBalanced = true, PostingStatus = "Posted", IsReversed = reversed,
                AccountingBookId = BookId, BookClassification = "PRIMARY"
            };
            Context.JournalEntries.Add(journal);
            Context.Set<FinancePostingEvent>().Add(new FinancePostingEvent
            {
                Id = Guid.NewGuid(), TenantId = lease.TenantId, SourceModule = "FixedAssets",
                SourceDocumentType = "LeasePeriodPosting", SourceDocumentId = schedule.Id,
                PostingAction = "Post", PostingStatus = "Posted", PostingDate = schedule.PeriodDate,
                PostedAt = schedule.PeriodDate, JournalEntryId = journal.Id, JournalEntry = journal,
                TotalDebitAmount = schedule.PaymentAmount, TotalCreditAmount = schedule.PaymentAmount,
                FunctionalCurrencyCode = "GHS", BookClassification = "PRIMARY", AccountingBookId = BookId
            });
            Context.AccountTransactions.Add(new AccountTransaction
            {
                Id = Guid.NewGuid(), TenantId = lease.TenantId, AccountId = InterestAccountId,
                JournalEntryId = journal.Id, TransactionDate = schedule.PeriodDate,
                DebitAmount = schedule.InterestExpense, CreditAmount = 0m,
                FunctionalCurrencyCode = "GHS", TransactionCurrency = "GHS",
                TransactionDebitAmount = schedule.InterestExpense, TransactionCreditAmount = 0m,
                SourceModule = "FixedAssets", SourceDocumentId = schedule.Id,
                SourceDocumentType = "LeasePeriodPosting",
                SourceDocumentLineId = FinanceSourceLineIdentity.Create(
                    schedule.Id, "INTEREST", lease.Id, schedule.Id),
                BookClassification = "PRIMARY", AccountingBookId = BookId,
                FiscalPeriodId = periodId, PostedDate = schedule.PeriodDate, PostingStatus = "Posted"
            });
            await Context.SaveChangesAsync();
        }

        private static LeaseScheduleLine Line(
            LeaseContract lease, int period, DateTime date, decimal principal, decimal interest, decimal balance) => new()
        {
            Id = Guid.NewGuid(), TenantId = lease.TenantId, LeaseContractId = lease.Id, LeaseContract = lease,
            PeriodNumber = period, PeriodDate = date, PaymentAmount = principal + interest,
            PrincipalReduction = principal, InterestExpense = interest, RemainingLiability = balance
        };

        private static AccountTransaction RecognitionLine(
            LeaseContract lease,
            JournalEntry journal,
            Guid fiscalPeriodId,
            Guid accountId,
            string sourceComponent,
            decimal debit,
            decimal credit) => new()
        {
            Id = Guid.NewGuid(), TenantId = lease.TenantId, AccountId = accountId,
            JournalEntryId = journal.Id, TransactionDate = lease.StartDate,
            DebitAmount = debit, CreditAmount = credit, FunctionalCurrencyCode = "GHS",
            TransactionCurrency = "GHS", TransactionDebitAmount = debit,
            TransactionCreditAmount = credit, SourceModule = "FixedAssets",
            SourceDocumentId = lease.Id, SourceDocumentType = "LeaseRecognition",
            SourceDocumentLineId = FinanceSourceLineIdentity.Create(lease.Id, sourceComponent, lease.Id),
            BookClassification = journal.BookClassification, AccountingBookId = journal.AccountingBookId,
            FiscalPeriodId = fiscalPeriodId, PostedDate = lease.StartDate, PostingStatus = "Posted"
        };

        private static Account Account(
            Guid id, Guid tenantId, string code, AccountType type, bool isControl = false) => new()
        {
            Id = id, TenantId = tenantId, AccountCode = code, AccountNumber = code,
            AccountName = code, AccountType = type, CurrencyCode = "GHS",
            Status = AccountStatus.Active, IsControlAccount = isControl,
            AllowDirectPosting = !isControl
        };

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
