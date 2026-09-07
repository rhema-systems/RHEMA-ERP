using System.Reflection;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
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

    private static string Fingerprint(CreateAccountingEventDto request) => (string)typeof(AccountingEventService)
        .GetMethod("Fingerprint", BindingFlags.NonPublic | BindingFlags.Static)!
        .Invoke(null, [request, "EVENT-1"])!;

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

    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
