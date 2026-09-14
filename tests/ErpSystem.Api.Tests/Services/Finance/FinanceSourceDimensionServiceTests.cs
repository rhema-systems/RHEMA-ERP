using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceSourceDimensionServiceTests
{
    private static readonly DateTime DocumentDate = new(2026, 8, 15);

    [Fact]
    public async Task Additional_account_required_dimension_uses_one_editable_line_and_readiness_union()
    {
        await using var fixture = await Fixture.CreateAsync(ruleType: null, defaultValue: false);
        var (additional, rule) = await AddAccountRuleAsync(fixture, "Required");
        var lines = new[] { new FinanceSourceDocumentLineContext(fixture.LineId, fixture.Account.Id, [additional.Id]) };
        var result = await fixture.Service.SynchronizeDraftAsync(fixture.Producer, fixture.DocumentId, DocumentDate,
            lines, null, true, null, "Capture multi-account source coding.");
        result.Lines.Should().ContainSingle(line => line.SourceLineId == fixture.LineId);
        result.Lines.Single().AdditionalAccountIds.Should().Equal(additional.Id);
        result.Lines.Single().RequiredDimensionCodes.Should().Equal("DEPT");
        result.Lines.Single().ReadinessWarnings.Should().ContainSingle(message => message.Contains("DEPT"));
        fixture.Context.FinanceSourceDimensionAssignments.Should().HaveCount(2);

        var frozen = await fixture.Service.ValidateAndFreezeAsync(fixture.Producer, fixture.DocumentId, DocumentDate, lines, false);
        frozen.Lines.Single().IsFrozen.Should().BeTrue();
        fixture.Context.FinanceDimensionSnapshots.Should().HaveCount(1);
        rule.IsEvidenceLocked.Should().BeTrue("even empty optional-capture coding must freeze every actual account rule");
    }

    [Fact]
    public async Task Secondary_fixed_value_satisfies_required_primary_and_hashes_and_locks_every_account_rule()
    {
        await using var fixture = await Fixture.CreateAsync("Required", defaultValue: false);
        var (additional, _) = await AddAccountRuleAsync(fixture, "Fixed", "FIN");
        var lines = new[] { new FinanceSourceDocumentLineContext(fixture.LineId, fixture.Account.Id, [additional.Id]) };
        await fixture.Service.SynchronizeDraftAsync(fixture.Producer, fixture.DocumentId, DocumentDate, lines, null, true, null,
            "Resolve all actual posting account rules.");
        var result = await fixture.Service.ValidateAndFreezeAsync(fixture.Producer, fixture.DocumentId, DocumentDate, lines, false);
        result.Lines.Single().Values.Should().ContainSingle(value => value.ValueCode == "FIN" && value.IsReadOnly && value.RuleType == "Fixed");
        var rules = await fixture.Context.FinanceDimensionAccountRules.OrderBy(rule => rule.FinanceDimensionDefinitionId)
            .ThenBy(rule => rule.AccountId).ThenBy(rule => rule.Id).ToListAsync();
        rules.Should().HaveCount(2).And.OnlyContain(rule => rule.IsEvidenceLocked);
        var evidence = string.Join("|", rules.Select(rule => $"{rule.Id:N}:{rule.RuleFamilyId:N}:{rule.RuleVersion}:{rule.RuleType}"));
        fixture.Context.FinanceDimensionSnapshots.Single().RuleEvidenceHash.Should().Be(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(evidence))));
        var posting = await fixture.Service.GetPostingDimensionsAsync(fixture.Producer, fixture.DocumentId);
        posting.Should().HaveCount(1);
        posting.Keys.Should().Equal(fixture.LineId);
        posting[fixture.LineId].Should().ContainSingle(value => value.ValueCode == "FIN");
    }

    [Theory]
    [InlineData("Required", false, "Prohibited", null)]
    [InlineData("Fixed", true, "Prohibited", null)]
    [InlineData("Fixed", true, "Fixed", "OPS")]
    public async Task Contradictory_account_rules_fail_with_actionable_conflict(string primaryRule, bool primaryDefault, string secondaryRule, string? secondaryDefault)
    {
        await using var fixture = await Fixture.CreateAsync(primaryRule, primaryDefault);
        var (additional, _) = await AddAccountRuleAsync(fixture, secondaryRule, secondaryDefault);
        var action = () => fixture.Service.SynchronizeDraftAsync(fixture.Producer, fixture.DocumentId, DocumentDate,
            [new FinanceSourceDocumentLineContext(fixture.LineId, fixture.Account.Id, [additional.Id])], null, true, null, "Try conflicting account coding.");
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("FIN_DIMENSION_ACCOUNT_CONFLICT:*DEPT*Align*separate*");
        fixture.Context.FinanceDimensionSnapshots.Should().BeEmpty();
    }

    [Fact]
    public async Task Conflicting_nonfixed_defaults_are_not_arbitrarily_selected_and_explicit_coding_remains_editable()
    {
        await using var fixture = await Fixture.CreateAsync("Required", true);
        var (additional, _) = await AddAccountRuleAsync(fixture, "Required", "OPS");
        var lines = new[] { new FinanceSourceDocumentLineContext(fixture.LineId, fixture.Account.Id, [additional.Id]) };
        var empty = await fixture.Service.SynchronizeDraftAsync(fixture.Producer, fixture.DocumentId, DocumentDate,
            lines, null, true, null, "Capture ambiguous defaults for user selection.");
        empty.Lines.Single().Values.Should().BeEmpty();
        empty.Lines.Single().ReadinessWarnings.Should().ContainSingle();
        var edited = await fixture.Service.SynchronizeDraftAsync(fixture.Producer, fixture.DocumentId, DocumentDate,
            lines, fixture.Input("OPS"), true, null, "User selected common coding value.");
        edited.Lines.Single().Values.Should().ContainSingle(value => value.ValueCode == "OPS" && !value.IsReadOnly);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Additional_account_cannot_be_foreign_or_inactive(bool foreign)
    {
        await using var fixture = await Fixture.CreateAsync(null, false);
        var (additional, _) = await AddAccountRuleAsync(fixture, "Optional");
        if (foreign) additional.TenantId = Guid.NewGuid(); else additional.Status = AccountStatus.Inactive;
        await fixture.Context.SaveChangesAsync();
        var action = () => fixture.Service.SynchronizeDraftAsync(fixture.Producer, fixture.DocumentId, DocumentDate,
            [new FinanceSourceDocumentLineContext(fixture.LineId, fixture.Account.Id, [additional.Id])], null, true, null, "Try invalid posting account.");
        await action.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*account*tenant*");
    }

    [Fact]
    public async Task Frozen_history_uses_original_snapshot_when_current_multiaccount_rules_become_conflicting()
    {
        await using var fixture = await Fixture.CreateAsync("Required", false);
        var (additional, oldRule) = await AddAccountRuleAsync(fixture, "Optional");
        oldRule.RouteId = null; oldRule.SourceModule = null; oldRule.SourceDocumentType = null; oldRule.PostingAction = null;
        await fixture.Context.SaveChangesAsync();
        var lines = new[] { new FinanceSourceDocumentLineContext(fixture.LineId, fixture.Account.Id, [additional.Id]) };
        await fixture.Service.SynchronizeDraftAsync(fixture.Producer, fixture.DocumentId, DocumentDate, lines, fixture.Input("FIN"), true, null, "Capture original coding.");
        var frozen = await fixture.Service.ValidateAndFreezeAsync(fixture.Producer, fixture.DocumentId, DocumentDate, lines, false);
        var hash = fixture.Context.FinanceDimensionSnapshots.Single().RuleEvidenceHash;
        fixture.Context.FinanceDimensionAccountRules.Add(new FinanceDimensionAccountRule
        {
            TenantId = fixture.TenantId, AccountId = additional.Id, FinanceDimensionDefinitionId = oldRule.FinanceDimensionDefinitionId,
            RuleFamilyId = Guid.NewGuid(), RuleVersion = 1, RuleType = "Prohibited", IsActive = true,
            RouteId = FinanceDimensionRouteId.FinanceApVendorInvoice, SourceModule = "AP", SourceDocumentType = "VendorInvoice",
            PostingAction = "Post", EffectiveDate = DocumentDate
        });
        await fixture.Context.SaveChangesAsync();

        var history = await fixture.Service.GetAsync(fixture.Producer, fixture.DocumentId, DocumentDate, lines);

        history.Lines.Single().CombinationHash.Should().Be(frozen.Lines.Single().CombinationHash);
        history.Lines.Single().Values.Should().ContainSingle(value => value.ValueCode == "FIN");
        history.Lines.Single().ReadinessWarnings.Should().BeEmpty();
        fixture.Context.FinanceDimensionSnapshots.Single().RuleEvidenceHash.Should().Be(hash);
    }

    private static async Task<(Account Account, FinanceDimensionAccountRule Rule)> AddAccountRuleAsync(Fixture fixture, string type, string? defaultCode = null)
    {
        var definition = await fixture.Context.FinanceDimensionDefinitions.SingleAsync();
        var account = new Account { TenantId = fixture.TenantId, AccountCode = "6001", AccountNumber = "6001", AccountName = "Second posting account",
            AccountType = AccountType.Expense, CurrencyCode = "GHS", Status = AccountStatus.Active, AllowDirectPosting = true };
        var rule = new FinanceDimensionAccountRule { TenantId = fixture.TenantId, RuleFamilyId = Guid.NewGuid(), RuleVersion = 1,
            AccountId = account.Id, FinanceDimensionDefinitionId = definition.Id, RuleType = type,
            DefaultDimensionValueId = defaultCode == null ? null : (await fixture.Context.FinanceDimensionValues.SingleAsync(value => value.Code == defaultCode)).Id,
            RouteId = FinanceDimensionRouteId.FinanceApVendorInvoice, SourceModule = "AP", SourceDocumentType = "VendorInvoice", PostingAction = "Post",
            EffectiveDate = new DateTime(2025, 1, 1), IsActive = true };
        fixture.Context.Accounts.Add(account); fixture.Context.FinanceDimensionAccountRules.Add(rule);
        await fixture.Context.SaveChangesAsync();
        return (account, rule);
    }

    [Fact]
    public async Task CaptureOptionalPersistsExplicitEmptyLineAndReadinessWarning()
    {
        await using var fixture = await Fixture.CreateAsync("Required", defaultValue: false);

        var result = await fixture.Service.SynchronizeDraftAsync(
            fixture.Producer,
            fixture.DocumentId,
            DocumentDate,
            [new FinanceSourceDocumentLineContext(fixture.LineId, fixture.Account.Id)],
            input: null,
            inheritDefaultForUnassignedLines: true,
            budgetReservationSourceDocumentType: null,
            "Customer invoice created.");

        result.CertificationState.Should().Be(FinanceDimensionCertificationState.CaptureOptional);
        result.ReadinessWarnings.Should().ContainSingle(message => message.Contains("DEPT"));
        result.Lines.Should().ContainSingle(line =>
            line.SourceLineId == fixture.LineId && line.FinanceDimensionSetId == null);
        fixture.Context.FinanceSourceDimensionAssignments.Should().HaveCount(2);
    }

    [Fact]
    public async Task FixedRuleOverridesSpoofedDraftValueAndReturnsItReadOnly()
    {
        await using var fixture = await Fixture.CreateAsync("Fixed", defaultValue: true);

        var result = await fixture.Service.SynchronizeDraftAsync(
            fixture.Producer,
            fixture.DocumentId,
            DocumentDate,
            [new FinanceSourceDocumentLineContext(fixture.LineId, fixture.Account.Id)],
            new FinanceSourceDocumentDimensionInputDto
            {
                Lines =
                [
                    new FinanceSourceLineDimensionInputDto
                    {
                        SourceLineId = fixture.LineId,
                        AccountId = fixture.Account.Id,
                        Dimensions =
                        [
                            new FinancePostingDimensionValueDto
                            {
                                DimensionCode = "DEPT",
                                ValueCode = "OPS"
                            }
                        ]
                    }
                ]
            },
            inheritDefaultForUnassignedLines: true,
            budgetReservationSourceDocumentType: null,
            "Vendor invoice draft changed.");

        result.Lines.Single().Values.Should().ContainSingle(value =>
            value.DimensionCode == "DEPT"
            && value.ValueCode == "FIN"
            && value.RuleType == "Fixed"
            && value.IsReadOnly);
    }

    [Fact]
    public async Task ApplyDefaultSkipsProhibitedLineButExplicitAssignmentStillFails()
    {
        await using var fixture = await Fixture.CreateAsync("Prohibited", defaultValue: false);
        var lines = new[] { new FinanceSourceDocumentLineContext(fixture.LineId, fixture.Account.Id) };
        var inherited = await fixture.Service.SynchronizeDraftAsync(
            fixture.Producer,
            fixture.DocumentId,
            DocumentDate,
            lines,
            new FinanceSourceDocumentDimensionInputDto
            {
                DefaultDimensions =
                [
                    new FinancePostingDimensionValueDto
                    {
                        DimensionCode = "DEPT",
                        ValueCode = "OPS"
                    }
                ],
                ApplyDefaultToEligibleLines = true
            },
            inheritDefaultForUnassignedLines: true,
            budgetReservationSourceDocumentType: null,
            "Apply document defaults.");

        inherited.DefaultValues.Should().ContainSingle(value => value.ValueCode == "OPS");
        inherited.Lines.Single().Values.Should().BeEmpty();

        var explicitAttempt = () => fixture.Service.SynchronizeDraftAsync(
            fixture.Producer,
            fixture.DocumentId,
            DocumentDate,
            lines,
            fixture.Input("OPS"),
            inheritDefaultForUnassignedLines: true,
            budgetReservationSourceDocumentType: null,
            "Explicit prohibited assignment.");
        await explicitAttempt.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*prohibited*");
    }

    [Fact]
    public async Task DimensionChangeReleasesReservationMarksBudgetStaleAndAuditsHashes()
    {
        await using var fixture = await Fixture.CreateAsync(ruleType: null, defaultValue: false);
        var firstInput = fixture.Input("FIN");
        await fixture.Service.SynchronizeDraftAsync(
            fixture.Producer,
            fixture.DocumentId,
            DocumentDate,
            [new FinanceSourceDocumentLineContext(fixture.LineId, fixture.Account.Id)],
            firstInput,
            inheritDefaultForUnassignedLines: true,
            "VendorInvoice",
            "Vendor invoice created.");
        await fixture.Service.MarkBudgetEvidenceCurrentAsync(
            fixture.Producer, fixture.DocumentId, new string('A', 64));
        var reservation = new FinanceBudgetReservation
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            SourceDocumentType = "VendorInvoice",
            SourceDocumentId = fixture.DocumentId,
            CurrencyCode = "GHS",
            TransactionCurrencyCode = "GHS",
            Status = "Reserved",
            EvaluationHash = new string('B', 64),
            ReservationVersion = 3
        };
        fixture.Context.FinanceBudgetReservations.Add(reservation);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.SynchronizeDraftAsync(
            fixture.Producer,
            fixture.DocumentId,
            DocumentDate,
            [new FinanceSourceDocumentLineContext(fixture.LineId, fixture.Account.Id)],
            fixture.Input("OPS"),
            inheritDefaultForUnassignedLines: true,
            "VendorInvoice",
            "Vendor invoice dimensions changed.");

        result.BudgetEvidenceStatus.Should().Be("Stale");
        fixture.Budget.Verify(service => service.ReleaseAsync(
            reservation.Id,
            It.Is<ReleaseFinanceBudgetReservationDto>(request =>
                request.ExpectedVersion == 3 && request.Reason.Contains("re-evaluation")),
            It.IsAny<CancellationToken>()), Times.Once);
        var change = await fixture.Context.FinanceSourceDimensionChanges
            .OrderByDescending(item => item.ChangedAt).FirstAsync();
        change.PreviousCombinationHash.Should().NotBe(change.NewCombinationHash);
        change.BudgetEvidenceBecameStale.Should().BeTrue();
        change.ReleasedBudgetReservationIdsJson.Should().Contain(reservation.Id.ToString());
        change.Reason = "tampered";
        var tamper = () => fixture.Context.SaveChangesAsync();
        await tamper.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*immutable*");
    }

    [Fact]
    public async Task AddingDimensionsToExistingEmptyEvidenceAlsoMarksBudgetStale()
    {
        await using var fixture = await Fixture.CreateAsync(ruleType: null, defaultValue: false);
        var lines = new[] { new FinanceSourceDocumentLineContext(fixture.LineId, fixture.Account.Id) };
        await fixture.Service.SynchronizeDraftAsync(
            fixture.Producer, fixture.DocumentId, DocumentDate, lines, null, true,
            "VendorInvoice", "Vendor invoice created without coding.");
        await fixture.Service.MarkBudgetEvidenceCurrentAsync(
            fixture.Producer, fixture.DocumentId, new string('A', 64));
        var reservation = new FinanceBudgetReservation
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            SourceDocumentType = "VendorInvoice",
            SourceDocumentId = fixture.DocumentId,
            CurrencyCode = "GHS",
            TransactionCurrencyCode = "GHS",
            Status = "Reserved",
            EvaluationHash = new string('B', 64),
            ReservationVersion = 1
        };
        fixture.Context.FinanceBudgetReservations.Add(reservation);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.SynchronizeDraftAsync(
            fixture.Producer, fixture.DocumentId, DocumentDate, lines, fixture.Input("FIN"), true,
            "VendorInvoice", "Added source coding to an existing line.");

        result.BudgetEvidenceStatus.Should().Be("Stale");
        fixture.Budget.Verify(service => service.ReleaseAsync(
            reservation.Id,
            It.IsAny<ReleaseFinanceBudgetReservationDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Context.FinanceSourceDimensionChanges.Should().Contain(change =>
            change.SourceLineId == fixture.LineId
            && change.PreviousCombinationHash == "NONE"
            && change.BudgetEvidenceBecameStale);
    }

    [Fact]
    public async Task EmptyCaptureOptionalEvidenceFreezesAndCanBeSupersededInDraftWithoutLosingSnapshotAudit()
    {
        await using var fixture = await Fixture.CreateAsync("Fixed", defaultValue: true);
        var lines = new[] { new FinanceSourceDocumentLineContext(fixture.LineId, fixture.Account.Id) };
        await fixture.Service.SynchronizeDraftAsync(
            fixture.Producer, fixture.DocumentId, DocumentDate, lines, null, true, null, "Created.");
        var frozen = await fixture.Service.ValidateAndFreezeAsync(
            fixture.Producer, fixture.DocumentId, DocumentDate, lines, false);
        var snapshotId = (await fixture.Context.FinanceSourceDimensionAssignments
            .SingleAsync(item => item.SourceLineId == fixture.LineId)).FinanceDimensionSnapshotId;

        frozen.Lines.Single().IsFrozen.Should().BeTrue();
        snapshotId.Should().NotBeNull();

        var reopened = await fixture.Service.SynchronizeDraftAsync(
            fixture.Producer, fixture.DocumentId, DocumentDate, lines, null, true, null,
            "Rejected document edited in Draft.");

        reopened.Lines.Single().IsFrozen.Should().BeFalse();
        fixture.Context.FinanceSourceDimensionChanges.Should().Contain(item =>
            item.PreviousFinanceDimensionSnapshotId == snapshotId
            && item.NewFinanceDimensionSnapshotId == null);
        fixture.Context.FinanceDimensionSnapshots.Should().ContainSingle(item => item.Id == snapshotId);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required ApplicationDbContext Context { get; init; }
        public required FinanceSourceDimensionService Service { get; init; }
        public required Mock<IFinanceBudgetCommitmentService> Budget { get; init; }
        public required Guid TenantId { get; init; }
        public required Guid DocumentId { get; init; }
        public required Guid LineId { get; init; }
        public required Account Account { get; init; }
        public required FinancePostingProducerContext Producer { get; init; }

        public FinanceSourceDocumentDimensionInputDto Input(string valueCode) => new()
        {
            Lines =
            [
                new FinanceSourceLineDimensionInputDto
                {
                    SourceLineId = LineId,
                    AccountId = Account.Id,
                    Dimensions =
                    [
                        new FinancePostingDimensionValueDto
                        {
                            DimensionCode = "DEPT",
                            ValueCode = valueCode
                        }
                    ]
                }
            ]
        };

        public static async Task<Fixture> CreateAsync(string? ruleType, bool defaultValue)
        {
            var tenantId = Guid.NewGuid();
            var db = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase($"finance-source-dimensions-{Guid.NewGuid():N}").Options);
            var account = new Account
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = "6000", AccountNumber = "6000",
                AccountName = "Operating expense", AccountType = AccountType.Expense, CurrencyCode = "GHS",
                Status = AccountStatus.Active, AllowDirectPosting = true
            };
            var definition = new FinanceDimensionDefinition
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "DEPT", Name = "Department",
                Classification = "Analytical", ValueSourceType = "Lookup", IsActive = true
            };
            var finance = new FinanceDimensionValue
            {
                Id = Guid.NewGuid(), TenantId = tenantId, FinanceDimensionDefinitionId = definition.Id,
                Code = "FIN", Name = "Finance", EffectiveDate = new DateTime(2025, 1, 1), IsActive = true
            };
            var operations = new FinanceDimensionValue
            {
                Id = Guid.NewGuid(), TenantId = tenantId, FinanceDimensionDefinitionId = definition.Id,
                Code = "OPS", Name = "Operations", EffectiveDate = new DateTime(2025, 1, 1), IsActive = true
            };
            db.Accounts.Add(account);
            db.FinanceDimensionDefinitions.Add(definition);
            db.FinanceDimensionValues.AddRange(finance, operations);
            if (ruleType is not null)
                db.FinanceDimensionAccountRules.Add(new FinanceDimensionAccountRule
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, RuleFamilyId = Guid.NewGuid(), RuleVersion = 1,
                    AccountId = account.Id, FinanceDimensionDefinitionId = definition.Id,
                    RuleType = ruleType, DefaultDimensionValueId = defaultValue ? finance.Id : null,
                    RouteId = FinanceDimensionRouteId.FinanceApVendorInvoice,
                    SourceModule = "AP", SourceDocumentType = "VendorInvoice", PostingAction = "Post",
                    EffectiveDate = new DateTime(2025, 1, 1), IsActive = true
                });
            await db.SaveChangesAsync();

            var user = new Mock<ICurrentUserService>();
            user.SetupGet(item => item.TenantId).Returns(tenantId);
            user.SetupGet(item => item.UserId).Returns(Guid.NewGuid().ToString());
            user.SetupGet(item => item.UserName).Returns("finance.source.test");
            user.SetupGet(item => item.Claims).Returns(new Dictionary<string, string>());
            var budget = new Mock<IFinanceBudgetCommitmentService>();
            budget.Setup(service => service.ReleaseAsync(
                    It.IsAny<Guid>(), It.IsAny<ReleaseFinanceBudgetReservationDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, ReleaseFinanceBudgetReservationDto _, CancellationToken _) =>
                    new FinanceBudgetReservationDto { Id = id, Status = "Released" });
            var dimensions = new FinanceDimensionAdministrationService(db, user.Object);
            var store = new FinanceSourceDimensionAssignmentStore(db, user.Object);
            var service = new FinanceSourceDimensionService(db, user.Object, store, dimensions, budget.Object);
            return new Fixture
            {
                Context = db,
                Service = service,
                Budget = budget,
                TenantId = tenantId,
                DocumentId = Guid.NewGuid(),
                LineId = Guid.NewGuid(),
                Account = account,
                Producer = new FinancePostingProducerContext(FinanceDimensionRouteId.FinanceApVendorInvoice)
            };
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
