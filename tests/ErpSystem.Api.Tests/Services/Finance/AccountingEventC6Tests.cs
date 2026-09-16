using System.Reflection;
using System.Collections.Immutable;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingEventC6Tests
{
    [Fact]
    public async Task Orchestrator_IsDisabledByDefault_BeforeAnyDatabaseOrLeafWork()
    {
        await using var db = Context();
        var user = new Mock<ICurrentUserService>();
        var leaf = new FinancePostingEngine(db, user.Object, NullLogger<FinancePostingEngine>.Instance);
        var service = new AccountingEventService(db, user.Object, Mock.Of<IAccountingBookApplicabilityService>(),
            leaf, Mock.Of<IFinanceAuditService>(), Options.Create(new AccountingEventOptions()));

        await FluentActions.Awaiting(() => service.CreateAsync(new CreateAccountingEventDto()))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("ACCOUNTING_EVENT_ORCHESTRATION_DISABLED:*");
    }

    [Fact]
    public void Controller_UsesSeparatePrepareReleaseAndReadPermissions()
    {
        var methods = typeof(AccountingEventsController).GetMethods(BindingFlags.Instance | BindingFlags.Public);
        methods.Single(item => item.Name == nameof(AccountingEventsController.Create))
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(FinancePermissions.PrepareAccountingEvents);
        methods.Single(item => item.Name == nameof(AccountingEventsController.Release))
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(FinancePermissions.OrchestrateAccountingEvents);
        methods.Single(item => item.Name == nameof(AccountingEventsController.GetBook))
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(FinancePermissions.ViewAccountingEvents);
    }

    [Fact]
    public void PublicPostingContract_DoesNotExposeParallelBookAuthority()
    {
        typeof(IFinancePostingEngine).GetMethods().Should().NotContain(item =>
            item.Name.Contains("AccountingEvent", StringComparison.Ordinal));
        typeof(FinancePostingEngine).Assembly.GetType(
            "ErpSystem.Api.Services.Finance.GL.IAccountingEventPostingLeaf")!.IsNotPublic.Should().BeTrue();
        typeof(FinancePostingEngine).Assembly.GetType(
            "ErpSystem.Api.Services.Finance.GL.AccountingEventPostingAuthority")!.GetProperties()
            .Select(item => item.Name).Should().Contain([
                "AccountingEventId", "AccountingBookSelectionEvidenceId", "AccountingBookId", "AuthorityFingerprint", "OrderedSelectedBookIds"]);
    }

    [Fact]
    public async Task ForgedExactBookAuthority_IsRejectedBeforeValidationCanMutateOrAudit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        var tenantId = Guid.NewGuid(); var eventId = Guid.NewGuid(); var evidenceId = Guid.NewGuid(); var selectedBookId = Guid.NewGuid();
        var accountingEvent = new AccountingEvent
        {
            Id = eventId, TenantId = tenantId, Version = 1, AccountingBookSelectionEvidenceId = evidenceId,
            Status = AccountingEventStatuses.Pending,
            Postings = [new AccountingEventPosting
            {
                TenantId = tenantId, AccountingEventId = eventId, EventVersion = 1, AccountingBookId = selectedBookId,
                AccountingBookCodeSnapshot = "IFRS", AuthorityFingerprint = new string('A', 64), Status = AccountingEventStatuses.Pending
            }]
        };
        db.Attach(accountingEvent); await using var transaction = await db.Database.BeginTransactionAsync();
        var user = new Mock<ICurrentUserService>(); user.SetupGet(item => item.TenantId).Returns(tenantId);
        user.SetupGet(item => item.UserId).Returns(Guid.NewGuid().ToString());
        var audit = new Mock<IFinanceAuditService>();
        var engine = new FinancePostingEngine(db, user.Object, NullLogger<FinancePostingEngine>.Instance, audit.Object);
        var interfaceType = typeof(FinancePostingEngine).Assembly.GetType("ErpSystem.Api.Services.Finance.GL.IAccountingEventPostingLeaf")!;
        var authorityType = typeof(FinancePostingEngine).Assembly.GetType("ErpSystem.Api.Services.Finance.GL.AccountingEventPostingAuthority")!;
        var forgedBookId = Guid.NewGuid();
        var forgedAuthority = Activator.CreateInstance(authorityType,
            [eventId, evidenceId, forgedBookId, new string('B', 64), ImmutableArray.Create(forgedBookId)])!;
        var task = (Task)interfaceType.GetMethod("PostAsync")!.Invoke(engine,
            [Request().PostingRequest, forgedAuthority, CancellationToken.None])!;

        await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exact book is not part of the AccountingEvent frozen authority*");
        db.ChangeTracker.Entries().Should().OnlyContain(entry => entry.State == EntityState.Unchanged);
        db.ChangeTracker.Entries<ErpSystem.Core.Entities.AuditLog>().Should().BeEmpty();
        db.ChangeTracker.Entries<FinancePostingEvent>().Should().BeEmpty();
        db.ChangeTracker.Entries<JournalEntry>().Should().BeEmpty();
        db.ChangeTracker.Entries<AccountBalance>().Should().BeEmpty();
        audit.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnrelatedDirectPostingMatch_IsNotAcceptedForSelectedOrUnselectedBook(bool useSelectedBook)
    {
        await using var db = Context(); var tenantId = Guid.NewGuid(); var eventId = Guid.NewGuid(); var evidenceId = Guid.NewGuid();
        var selectedBook = Guid.NewGuid(); var matchedBook = useSelectedBook ? selectedBook : Guid.NewGuid();
        var request = Request().PostingRequest;
        var accountingEvent = new AccountingEvent
        {
            Id = eventId, TenantId = tenantId, Version = 1, RootAccountingEventId = eventId,
            AccountingBookSelectionEvidenceId = evidenceId, Status = AccountingEventStatuses.Pending,
            Postings = [new AccountingEventPosting
            {
                TenantId = tenantId, EventVersion = 1, AccountingBookId = selectedBook, SelectionOrder = 1,
                AccountingBookCodeSnapshot = "IFRS", AuthorityFingerprint = new string('A', 64), Status = AccountingEventStatuses.Pending
            }]
        };
        var unrelated = new FinancePostingEvent
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountingBookId = matchedBook,
            SourceDocumentType = request.SourceDocumentType, SourceDocumentId = request.SourceDocumentId,
            PostingAction = request.PostingAction, PostingStatus = "Posted"
        };
        db.AddRange(accountingEvent, unrelated); await db.SaveChangesAsync();
        var user = new Mock<ICurrentUserService>(); user.SetupGet(item => item.TenantId).Returns(tenantId);
        var audit = new Mock<IFinanceAuditService>();
        var engine = new FinancePostingEngine(db, user.Object, NullLogger<FinancePostingEngine>.Instance, audit.Object);
        var interfaceType = typeof(FinancePostingEngine).Assembly.GetType("ErpSystem.Api.Services.Finance.GL.IAccountingEventPostingLeaf")!;
        var authorityType = typeof(FinancePostingEngine).Assembly.GetType("ErpSystem.Api.Services.Finance.GL.AccountingEventPostingAuthority")!;
        var authority = Activator.CreateInstance(authorityType,
            [eventId, evidenceId, selectedBook, new string('A', 64), ImmutableArray.Create(selectedBook)])!;
        var task = (Task)interfaceType.GetMethod("PostAsync")!.Invoke(engine,
            [request, authority, CancellationToken.None])!;

        await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("ACCOUNTING_EVENT_UNRELATED_POSTING_MATCH:*");
        db.ChangeTracker.Entries().Should().OnlyContain(item => item.State == EntityState.Unchanged);
        audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NonPostedJournal_CannotEstablishAccountingEventSiblingOwnership()
    {
        await using var db = Context(); var tenantId = Guid.NewGuid(); var eventId = Guid.NewGuid(); var evidenceId = Guid.NewGuid();
        var bookId = Guid.NewGuid(); var postingEventId = Guid.NewGuid(); var journalId = Guid.NewGuid();
        var request = Request().PostingRequest;
        var accountingEvent = new AccountingEvent
        {
            Id = eventId, TenantId = tenantId, Version = 1, RootAccountingEventId = eventId,
            AccountingBookSelectionEvidenceId = evidenceId, Status = AccountingEventStatuses.Pending,
            Postings = [new AccountingEventPosting
            {
                TenantId = tenantId, EventVersion = 1, AccountingBookId = bookId, SelectionOrder = 1,
                AccountingBookCodeSnapshot = "IFRS", AuthorityFingerprint = new string('A', 64),
                Status = AccountingEventStatuses.Posted, FinancePostingEventId = postingEventId,
                JournalEntryId = journalId, PostedAtUtc = DateTime.UtcNow
            }]
        };
        var selectionBook = new AccountingBookSelectionEvidenceBook
        {
            TenantId = tenantId, AccountingBookSelectionEvidenceId = evidenceId, AccountingBookId = bookId,
            SelectionOrder = 1, AccountingBookCodeSnapshot = "IFRS", AuthorityFingerprint = new string('A', 64)
        };
        var journal = new JournalEntry
        {
            Id = journalId, TenantId = tenantId, AccountingBookId = bookId,
            BookClassification = "IFRS", PostingStatus = "Draft"
        };
        var postingEvent = new FinancePostingEvent
        {
            Id = postingEventId, TenantId = tenantId, AccountingBookId = bookId,
            BookClassification = "IFRS", JournalEntryId = journalId, PostingStatus = "Posted",
            SourceDocumentType = request.SourceDocumentType, SourceDocumentId = request.SourceDocumentId,
            PostingAction = request.PostingAction
        };
        db.AddRange(accountingEvent, selectionBook, journal, postingEvent); await db.SaveChangesAsync();
        var user = new Mock<ICurrentUserService>(); user.SetupGet(item => item.TenantId).Returns(tenantId);
        var audit = new Mock<IFinanceAuditService>();
        var engine = new FinancePostingEngine(db, user.Object, NullLogger<FinancePostingEngine>.Instance, audit.Object);
        var interfaceType = typeof(FinancePostingEngine).Assembly.GetType("ErpSystem.Api.Services.Finance.GL.IAccountingEventPostingLeaf")!;
        var authorityType = typeof(FinancePostingEngine).Assembly.GetType("ErpSystem.Api.Services.Finance.GL.AccountingEventPostingAuthority")!;
        var authority = Activator.CreateInstance(authorityType,
            [eventId, evidenceId, bookId, new string('A', 64), ImmutableArray.Create(bookId)])!;
        var task = (Task)interfaceType.GetMethod("PostAsync")!.Invoke(engine,
            [request, authority, CancellationToken.None])!;

        await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("ACCOUNTING_EVENT_UNRELATED_POSTING_MATCH:*");
        db.ChangeTracker.Entries().Should().OnlyContain(item => item.State == EntityState.Unchanged);
        audit.VerifyNoOtherCalls();
    }

    [Fact]
    public void RetryReleaseEvidence_RejectsChangedCheckerOrNormalizedReason()
    {
        var maker = Guid.NewGuid(); var checker = Guid.NewGuid();
        var item = new AccountingEvent { PreparedByUserId = maker, ReleasedByUserId = checker, ReleaseReason = "Approved" };
        InvokeReleaseMatch(item, checker, "Approved");

        FluentActions.Invoking(() => InvokeReleaseMatch(item, Guid.NewGuid(), "Approved"))
            .Should().Throw<TargetInvocationException>().Which.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().StartWith("ACCOUNTING_EVENT_RELEASE_CHECKER_CONFLICT:");
        FluentActions.Invoking(() => InvokeReleaseMatch(item, checker, "Changed"))
            .Should().Throw<TargetInvocationException>().Which.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().StartWith("ACCOUNTING_EVENT_RELEASE_REASON_CONFLICT:");
    }

    [Theory]
    [InlineData("UNKNOWN", "GOODS.RECEIPT", "POST")]
    [InlineData("ALL", "GOODS.RECEIPT", "POST")]
    [InlineData("INV", "1RECEIPT", "POST")]
    [InlineData("INV", "GOODS/RECEIPT", "POST")]
    [InlineData("INV", "GÓODS", "POST")]
    [InlineData("INV", "GOODS.RECEIPT", "POST!")]
    public void PreparedIdentityNormalizer_RejectsNonCanonicalOrUnknownIdentity(string module, string document, string action)
    {
        FluentActions.Invoking(() => FinancePreparedIdentityNormalizer.Normalize(module, document, action))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void PreparedIdentityNormalizer_AcceptsRegisteredAsciiCanonicalIdentity()
    {
        FinancePreparedIdentityNormalizer.Normalize(" inv ", " goods.receipt ", " post ")
            .Should().Be(new FinancePreparedIdentityNormalizer.Identity("INV", "GOODS.RECEIPT", "POST"));
    }

    [Fact]
    public void FingerprintDateEncoding_IsPortableAndExplicitForEveryDateTimeKind()
    {
        var method = typeof(AccountingEventService).GetMethod("CanonicalDateTime", BindingFlags.NonPublic | BindingFlags.Static)!;
        string Encode(DateTime value) => (string)method.Invoke(null, [value])!;
        var ticks = new DateTime(2026, 9, 7, 12, 34, 56, DateTimeKind.Unspecified).Ticks;
        var unspecified = new DateTime(ticks, DateTimeKind.Unspecified);
        var utcWallClock = new DateTime(ticks, DateTimeKind.Utc);
        var local = new DateTime(ticks, DateTimeKind.Local);

        Encode(unspecified).Should().Be(Encode(utcWallClock));
        Encode(local).Should().Be(Encode(local.ToUniversalTime()));
        Encode(utcWallClock.AddTicks(1)).Should().NotBe(Encode(utcWallClock));
        Encode(utcWallClock.AddDays(1)).Should().NotBe(Encode(utcWallClock));
    }

    [Fact]
    public void OriginalLeafIdentity_ComesFromCanonicalEventRatherThanCallerCaseVariants()
    {
        var request = Request().PostingRequest; request.SourceModule = " inv "; request.SourceDocumentType = " goods.receipt "; request.PostingAction = " post ";
        var accountingEvent = new AccountingEvent
        {
            Id = Guid.NewGuid(), OriginatingModuleCode = "INV", SourceDocumentType = "GOODS.RECEIPT",
            SourceDocumentId = request.SourceDocumentId, PostingAction = "POST"
        };
        var copied = (FinancePostingRequestV2Dto)typeof(AccountingEventService)
            .GetMethod("CopyForBook", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [request, "IFRS", "leaf-key", true, false, accountingEvent])!;

        copied.SourceModule.Should().Be("INV"); copied.OriginModuleCode.Should().Be("INV");
        copied.SourceDocumentType.Should().Be("GOODS.RECEIPT"); copied.PostingAction.Should().Be("POST");
    }

    [Fact]
    public void Fingerprint_BindsOrderedLinesDimensionsTaxesAndBudgetEvidence()
    {
        var baseline = Request();
        var baselineHash = Fingerprint(baseline);

        var changedLine = Request(); changedLine.PostingRequest.Lines[0].DebitAmount = 11m;
        var changedDimension = Request(); changedDimension.PostingRequest.Lines[0].Dimensions[0].ValueCode = "NORTH";
        var changedTax = Request(); changedTax.PostingRequest.TaxCalculationSnapshots[0].TaxAmount = 2m;
        var changedBudget = Request(); changedBudget.PostingRequest.BudgetReservationIds = [Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")];

        new[] { Fingerprint(changedLine), Fingerprint(changedDimension), Fingerprint(changedTax), Fingerprint(changedBudget) }
            .Should().OnlyHaveUniqueItems().And.NotContain(baselineHash);
    }

    [Fact]
    public void Fingerprint_V2LengthPrefixesPreventDelimiterAndNullSentinelCollisions()
    {
        var left = Request(); left.PostingRequest.SourceDocumentReference = "A|B"; left.PostingRequest.Description = "C";
        var right = Request(); right.PostingRequest.SourceDocumentReference = "A"; right.PostingRequest.Description = "B|C";
        Fingerprint(left).Should().NotBe(Fingerprint(right));

        var missing = Request(); missing.PostingRequest.ReversalReason = null;
        var empty = Request(); empty.PostingRequest.ReversalReason = string.Empty;
        var sentinelText = Request(); sentinelText.PostingRequest.ReversalReason = "~";
        Fingerprint(missing).Should().NotBe(Fingerprint(empty));
        Fingerprint(missing).Should().NotBe(Fingerprint(sentinelText));
    }

    [Fact]
    public void Fingerprint_BindsDerivedVersionAndRootIdentity()
    {
        var request = Request(); var root = Guid.Parse("12121212-1212-1212-1212-121212121212");
        Fingerprint(request, 1, root).Should().NotBe(Fingerprint(request, 2, root));
        Fingerprint(request, 1, root).Should().NotBe(Fingerprint(request, 1, Guid.NewGuid()));
    }

    [Fact]
    public void Fingerprint_UsesCanonicalTaxOrder_AndExcludesLeafRoutingFieldsOwnedByTheOrchestrator()
    {
        var first = RichRequest(); var secondTax = new FinanceTaxCalculationSnapshotDto
        {
            DocumentType = "GOODS.RECEIPT", DocumentId = first.PostingRequest.SourceDocumentId,
            TaxId = Guid.NewGuid(), CalculationOrder = 2, CalculationDate = first.PostingRequest.PostingDate, TaxAmount = 2m
        };
        first.PostingRequest.TaxCalculationSnapshots = [first.PostingRequest.TaxCalculationSnapshots[0], secondTax];
        var reordered = RichRequest(); reordered.PostingRequest.TaxCalculationSnapshots = [secondTax, reordered.PostingRequest.TaxCalculationSnapshots[0]];
        reordered.PostingRequest.TaxCalculationSnapshots[0].DocumentType = "goods.receipt";
        Fingerprint(first).Should().Be(Fingerprint(reordered));

        var dimensionOrder = RichRequest();
        dimensionOrder.PostingRequest.Lines[0].Dimensions =
        [
            dimensionOrder.PostingRequest.Lines[0].Dimensions[0],
            new FinancePostingDimensionValueDto { DimensionCode = "department", ValueCode = "HQ" }
        ];
        var reversedDimensionOrder = RichRequest();
        reversedDimensionOrder.PostingRequest.Lines[0].Dimensions =
        [
            new FinancePostingDimensionValueDto { DimensionCode = "DEPARTMENT", ValueCode = "hq" },
            reversedDimensionOrder.PostingRequest.Lines[0].Dimensions[0]
        ];
        Fingerprint(dimensionOrder).Should().Be(Fingerprint(reversedDimensionOrder));

        var routed = RichRequest(); routed.PostingRequest.AccountingBookCode = "LOCAL";
        routed.PostingRequest.IdempotencyKey = "caller-leaf-key";
        Fingerprint(routed).Should().Be(Fingerprint(RichRequest()),
            "C5 evidence owns book selection and C6 derives every leaf idempotency key");
    }

    [Fact]
    public void Fingerprint_BindsEverySemanticHeaderLineDimensionTaxAndBudgetField()
    {
        var baseline = Fingerprint(RichRequest());
        var mutations = new Action<CreateAccountingEventDto>[]
        {
            x => x.EventKind = "Correction", x => x.SupersedesAccountingEventId = Guid.NewGuid(),
            x => x.CorrectsAccountingEventId = Guid.NewGuid(), x => x.ReversesAccountingEventId = Guid.NewGuid(),
            x => x.SelectionIdempotencyKey = "event-2", x => x.ExpectedCalculationInputHash = new string('C', 64),
            x => x.ExpectedSelectionFingerprint = new string('D', 64), x => x.PostingRequest.SourceModule = "PO",
            x => x.PostingRequest.OriginModuleCode = "PO", x => x.PostingRequest.SourceDocumentType = "OTHER",
            x => x.PostingRequest.SourceDocumentId = Guid.NewGuid(), x => x.PostingRequest.SourceDocumentTenantId = Guid.NewGuid(),
            x => x.PostingRequest.ExistingJournalEntryId = Guid.NewGuid(), x => x.PostingRequest.ReversalOfJournalEntryId = Guid.NewGuid(),
            x => x.PostingRequest.ReversalReason = "Changed", x => x.PostingRequest.ReversalType = "Changed",
            x => x.PostingRequest.PostingAction = "Reverse", x => x.PostingRequest.SourceDocumentReference = "Changed",
            x => x.PostingRequest.Description = "Changed", x => x.PostingRequest.PostingDate = x.PostingRequest.PostingDate.AddDays(1),
            x => x.PostingRequest.FiscalPeriodId = Guid.NewGuid(), x => x.PostingRequest.JournalType = "Manual",
            x => x.PostingRequest.FunctionalCurrencyCode = "USD", x => x.PostingRequest.ReturnExistingOnDuplicate = false,
            x => x.PostingRequest.ExchangeRateTypeOverride = "Spot", x => x.PostingRequest.ExchangeRateQuoteSideOverride = "Ask",
            x => x.PostingRequest.ExchangeRateOverrideReason = "Changed", x => x.PostingRequest.ExchangeRateOverrideApprovedByUserId = Guid.NewGuid(),
            x => x.PostingRequest.ExchangeRateOverrideApprovedAt = x.PostingRequest.ExchangeRateOverrideApprovedAt!.Value.AddMinutes(1),
            x => x.PostingRequest.PreserveHistoricalExchangeRateSnapshot = false, x => x.PostingRequest.AllowPostingToClosedPeriod = false,
            x => x.PostingRequest.BudgetReservationSourceDocumentType = "OTHER", x => x.PostingRequest.BudgetReservationIds = [Guid.NewGuid()],
            x => x.PostingRequest.Lines[0].AccountId = Guid.NewGuid(), x => x.PostingRequest.Lines[0].SourceDocumentLineId = Guid.NewGuid(),
            x => x.PostingRequest.Lines[0].Description = "Changed", x => x.PostingRequest.Lines[0].DebitAmount++,
            x => x.PostingRequest.Lines[0].CreditAmount++, x => x.PostingRequest.Lines[0].TransactionCurrency = "USD",
            x => x.PostingRequest.Lines[0].TransactionDebitAmount++, x => x.PostingRequest.Lines[0].TransactionCreditAmount++,
            x => x.PostingRequest.Lines[0].ForeignCurrencyAmount++, x => x.PostingRequest.Lines[0].ExchangeRateId = Guid.NewGuid(),
            x => x.PostingRequest.Lines[0].ExchangeRate++, x => x.PostingRequest.Lines[0].ExchangeRateSource = "Changed",
            x => x.PostingRequest.Lines[0].ExchangeRateDate = x.PostingRequest.Lines[0].ExchangeRateDate!.Value.AddDays(1),
            x => x.PostingRequest.Lines[0].SourceReferenceNumber = "Changed", x => x.PostingRequest.Lines[0].LineNumber++,
            x => x.PostingRequest.Lines[0].FinanceDimensionSetId = Guid.NewGuid(), x => x.PostingRequest.Lines[0].SegmentString = "Changed",
            x => x.PostingRequest.Lines[0].Notes = "Changed", x => x.PostingRequest.Lines[0].TransactionTag = "Changed",
            x => x.PostingRequest.Lines[0].Dimensions[0].DimensionCode = "DEPT", x => x.PostingRequest.Lines[0].Dimensions[0].ValueCode = "NORTH",
            x => x.PostingRequest.Lines[0].Dimensions[0].SourceEntityType = "Project", x => x.PostingRequest.Lines[0].Dimensions[0].SourceEntityId = Guid.NewGuid(),
            x => x.PostingRequest.TaxCalculationSnapshots[0].DocumentType = "OTHER", x => x.PostingRequest.TaxCalculationSnapshots[0].DocumentId = Guid.NewGuid(),
            x => x.PostingRequest.TaxCalculationSnapshots[0].DocumentLineId = Guid.NewGuid(), x => x.PostingRequest.TaxCalculationSnapshots[0].TaxId = Guid.NewGuid(),
            x => x.PostingRequest.TaxCalculationSnapshots[0].TaxGroupId = Guid.NewGuid(), x => x.PostingRequest.TaxCalculationSnapshots[0].PostingAccountId = Guid.NewGuid(),
            x => x.PostingRequest.TaxCalculationSnapshots[0].BaseAmount++, x => x.PostingRequest.TaxCalculationSnapshots[0].TaxableAmount++,
            x => x.PostingRequest.TaxCalculationSnapshots[0].TaxRate++, x => x.PostingRequest.TaxCalculationSnapshots[0].TaxAmount++,
            x => x.PostingRequest.TaxCalculationSnapshots[0].CompoundBasis = ErpSystem.Core.Enums.CompoundBasis.Cumulative,
            x => x.PostingRequest.TaxCalculationSnapshots[0].CalculationOrder++,
            x => x.PostingRequest.TaxCalculationSnapshots[0].CalculationDate = x.PostingRequest.TaxCalculationSnapshots[0].CalculationDate.AddDays(1),
            x => x.PostingRequest.TaxCalculationSnapshots[0].IsManualOverride = false,
            x => x.PostingRequest.TaxCalculationSnapshots[0].OverrideReason = "Changed"
        };

        var mutatedHashes = mutations.Select(mutation =>
        {
            var request = RichRequest(); mutation(request); return Fingerprint(request);
        });
        mutatedHashes.Should().OnlyContain(hash => hash != baseline);
    }

    [Fact]
    public void Model_RequiresCanonicalGroupBookAndAppendOnlyAttemptCoordinates()
    {
        using var db = Context();
        var eventType = db.Model.FindEntityType(typeof(AccountingEvent))!;
        eventType.GetIndexes().Should().Contain(item => item.IsUnique
            && item.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId", "IdempotencyKey" }));
        var postingType = db.Model.FindEntityType(typeof(AccountingEventPosting))!;
        postingType.GetIndexes().Should().Contain(item => item.IsUnique
            && item.Properties.Select(property => property.Name).SequenceEqual(new[]
                { "TenantId", "AccountingEventId", "EventVersion", "AccountingBookId" }));
        postingType.GetForeignKeys().Should().Contain(item =>
            item.Properties.Select(property => property.Name).SequenceEqual(new[]
                { "TenantId", "AccountingEventId", "EventVersion" })
            && item.PrincipalKey.Properties.Select(property => property.Name).SequenceEqual(new[]
                { "TenantId", "Id", "Version" }));
        postingType.GetIndexes().Should().Contain(item => item.IsUnique && item.GetFilter() == "[FinancePostingEventId] IS NOT NULL"
            && item.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId", "FinancePostingEventId" }));
        postingType.GetIndexes().Should().Contain(item => item.IsUnique && item.GetFilter() == "[JournalEntryId] IS NOT NULL"
            && item.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId", "JournalEntryId" }));
        var attemptType = db.Model.FindEntityType(typeof(AccountingEventAttempt))!;
        attemptType.GetIndexes().Should().Contain(item => item.IsUnique
            && item.Properties.Any(property => property.Name == nameof(AccountingEventAttempt.AttemptNumber)));
    }

    [Fact]
    public void CorrectionAndReversalSelection_IsCopiedFromTargetFrozenEvidence()
    {
        var firstBook = Guid.NewGuid(); var secondBook = Guid.NewGuid();
        var target = new AccountingEvent
        {
            AccountingBookSelectionEvidenceId = Guid.NewGuid(), SelectionFingerprint = new string('F', 64),
            Postings =
            [
                new AccountingEventPosting { AccountingBookId = secondBook, AccountingBookCodeSnapshot = "LOCAL", SelectionOrder = 2, AuthorityFingerprint = new string('B', 64) },
                new AccountingEventPosting { AccountingBookId = firstBook, AccountingBookCodeSnapshot = "IFRS", SelectionOrder = 1, AuthorityFingerprint = new string('A', 64) }
            ]
        };
        var selection = (AccountingBookSelectionDto)typeof(AccountingEventService)
            .GetMethod("SelectionFromTarget", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [target])!;

        selection.SelectionEvidenceId.Should().Be(target.AccountingBookSelectionEvidenceId);
        selection.SelectionFingerprint.Should().Be(target.SelectionFingerprint);
        selection.Books.Select(item => item.AccountingBookId).Should().Equal(firstBook, secondBook);
    }

    [Fact]
    public void SuccessorIdentityMustMatchTarget_WhileItsPostingDateMayAdvance()
    {
        var target = new AccountingEvent
        {
            OriginatingModuleCode = "INV", SourceDocumentType = "GOODS.RECEIPT",
            SourceDocumentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            PostingAction = "POST", EventDate = new DateTime(2026, 8, 1)
        };
        var successor = Request().PostingRequest;
        successor.PostingDate = new DateTime(2026, 9, 7);
        typeof(AccountingEventService).GetMethod("RequireCanonicalTargetIdentity",
            BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [target, successor]);

        successor.SourceDocumentType = "OTHER";
        var invocation = FluentActions.Invoking(() => typeof(AccountingEventService)
            .GetMethod("RequireCanonicalTargetIdentity", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [target, successor])).Should().Throw<TargetInvocationException>().Which;
        invocation.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().StartWith("ACCOUNTING_EVENT_LINEAGE_IDENTITY_CONFLICT:");
    }

    private static string Fingerprint(CreateAccountingEventDto request, int version = 1, Guid rootId = default) => (string)typeof(AccountingEventService)
        .GetMethod("Fingerprint", BindingFlags.NonPublic | BindingFlags.Static)!
        .Invoke(null, [request, request.SelectionIdempotencyKey.Trim().ToUpperInvariant(), version,
            rootId == Guid.Empty ? Guid.Parse("12121212-1212-1212-1212-121212121212") : rootId])!;

    private static void InvokeReleaseMatch(AccountingEvent item, Guid checker, string reason) => typeof(AccountingEventService)
        .GetMethod("RequireReleaseMatch", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [item, checker, reason]);

    private static CreateAccountingEventDto Request()
    {
        var dimension = new FinancePostingDimensionValueDto { DimensionCode = "REGION", ValueCode = "SOUTH" };
        return new CreateAccountingEventDto
        {
            SelectionIdempotencyKey = "event-1", ExpectedCalculationInputHash = new string('A', 64),
            ExpectedSelectionFingerprint = new string('B', 64),
            PostingRequest = new FinancePostingRequestV2Dto
            {
                SourceModule = "INV", OriginModuleCode = "INV", SourceDocumentType = "GOODS.RECEIPT",
                SourceDocumentId = Guid.Parse("11111111-1111-1111-1111-111111111111"), PostingAction = "POST",
                PostingDate = new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc),
                Lines = [new FinancePostingLineDto { AccountId = Guid.Parse("22222222-2222-2222-2222-222222222222"), DebitAmount = 10m, Dimensions = [dimension] }],
                TaxCalculationSnapshots = [new FinanceTaxCalculationSnapshotDto { DocumentType = "GOODS.RECEIPT", DocumentId = Guid.Parse("11111111-1111-1111-1111-111111111111"), TaxId = Guid.Parse("33333333-3333-3333-3333-333333333333"), TaxAmount = 1m }]
            }
        };
    }

    private static CreateAccountingEventDto RichRequest()
    {
        var request = Request(); var posting = request.PostingRequest; var line = posting.Lines[0]; var tax = posting.TaxCalculationSnapshots[0];
        posting.SourceDocumentTenantId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        posting.ExistingJournalEntryId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        posting.ReversalOfJournalEntryId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        posting.ReversalReason = "Original reason"; posting.ReversalType = "Exact";
        posting.SourceDocumentReference = "REF-1"; posting.Description = "Description";
        posting.FiscalPeriodId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        posting.ExchangeRateTypeOverride = "Closing"; posting.ExchangeRateQuoteSideOverride = "Bid";
        posting.ExchangeRateOverrideReason = "Approved";
        posting.ExchangeRateOverrideApprovedByUserId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        posting.ExchangeRateOverrideApprovedAt = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);
        posting.PreserveHistoricalExchangeRateSnapshot = true; posting.AllowPostingToClosedPeriod = true;
        posting.BudgetReservationSourceDocumentType = "GOODS.RECEIPT";
        posting.BudgetReservationIds = [Guid.Parse("99999999-9999-9999-9999-999999999999")];
        line.SourceDocumentLineId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"); line.Description = "Line";
        line.CreditAmount = 2m; line.TransactionCurrency = "EUR"; line.TransactionDebitAmount = 10m;
        line.TransactionCreditAmount = 2m; line.ForeignCurrencyAmount = 8m;
        line.ExchangeRateId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"); line.ExchangeRate = 1.25m;
        line.ExchangeRateSource = "Policy"; line.ExchangeRateDate = posting.PostingDate; line.SourceReferenceNumber = "LINE-1";
        line.LineNumber = 1; line.FinanceDimensionSetId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        line.SegmentString = "01-1000"; line.Notes = "Note"; line.TransactionTag = "Tag";
        line.Dimensions[0].SourceEntityType = "Estate"; line.Dimensions[0].SourceEntityId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        tax.DocumentLineId = line.SourceDocumentLineId; tax.TaxGroupId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        tax.PostingAccountId = line.AccountId; tax.BaseAmount = 10m; tax.TaxableAmount = 8m; tax.TaxRate = 0.125m;
        tax.CalculationOrder = 1; tax.CalculationDate = posting.PostingDate; tax.IsManualOverride = true; tax.OverrideReason = "Approved";
        return request;
    }

    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
