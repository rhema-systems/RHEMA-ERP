using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using ErpSystem.Data.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyRateLibraryServiceTests
{
    [Fact]
    public async Task Controlled_master_references_are_tenant_safe()
    {
        await using var fixture = await Fixture.CreateAsync();
        var otherTenantUnit = new UnitOfMeasure
        {
            TenantId = Guid.NewGuid(),
            Code = "KG",
            Name = "Kilogram",
            IsActive = true
        };
        fixture.Db.UnitsOfMeasure.Add(otherTenantUnit);
        await fixture.Db.SaveChangesAsync();

        var action = () => fixture.Service.CreateItemAsync(new CreateQuantitySurveyRateLibraryItemRequest
        {
            Code = "MAT-001",
            Name = "Controlled material",
            Category = QuantitySurveyRateItemCategory.Material,
            UnitOfMeasureId = otherTenantUnit.Id
        }, "tenant-boundary", default);

        await action.Should().ThrowAsync<QuantitySurveyRateLibraryValidationException>()
            .WithMessage("*shared inventory master*");
    }

    [Fact]
    public async Task Preparer_cannot_publish_and_future_replacement_keeps_current_rate_effective()
    {
        await using var fixture = await Fixture.CreateAsync();
        var item = await fixture.CreateItemAsync();
        var currentDraft = await fixture.CreateRateAsync(item.Id, DateTime.UtcNow.Date.AddDays(-10), 100m);

        var selfApproval = () => fixture.Service.PublishRateAsync(item.Id, currentDraft.Id,
            new QuantitySurveyRateLifecycleRequest { RowVersion = currentDraft.RowVersion, Reason = "Approve current baseline" },
            "self-approval", default);
        await selfApproval.Should().ThrowAsync<QuantitySurveyRateLibraryConflictException>()
            .WithMessage("*preparer cannot publish*");

        fixture.UseChecker();
        var current = await fixture.Service.PublishRateAsync(item.Id, currentDraft.Id,
            new QuantitySurveyRateLifecycleRequest { RowVersion = currentDraft.RowVersion, Reason = "Independent baseline approval" },
            "publish-current", default);
        current.LifecycleStatus.Should().Be(QuantitySurveyRateLifecycleStatus.Published);

        fixture.UsePreparer();
        var futureDate = DateTime.UtcNow.Date.AddDays(30);
        var futureDraft = await fixture.CreateRateAsync(item.Id, futureDate, 125m);
        fixture.UseChecker();
        var future = await fixture.Service.PublishRateAsync(item.Id, futureDraft.Id,
            new QuantitySurveyRateLifecycleRequest { RowVersion = futureDraft.RowVersion, Reason = "Approved future market update" },
            "publish-future", default);

        var persistedCurrent = await fixture.Db.QuantitySurveyRateLibraryRates.AsNoTracking()
            .SingleAsync(value => value.Id == current.Id);
        persistedCurrent.LifecycleStatus.Should().Be(QuantitySurveyRateLifecycleStatus.Published);
        persistedCurrent.EffectiveTo.Should().Be(futureDate.AddTicks(-1));
        future.LifecycleStatus.Should().Be(QuantitySurveyRateLifecycleStatus.Published);

        var nowView = await fixture.Service.GetItemsAsync(new QuantitySurveyRateLibraryListRequest
        {
            EffectiveAt = DateTime.UtcNow
        });
        nowView.Items.Single().CurrentRate!.Id.Should().Be(current.Id);
        var futureView = await fixture.Service.GetItemsAsync(new QuantitySurveyRateLibraryListRequest
        {
            EffectiveAt = futureDate.AddDays(1)
        });
        futureView.Items.Single().CurrentRate!.Id.Should().Be(future.Id);
    }

    [Fact]
    public async Task Market_source_requires_published_central_DMS_evidence_when_policy_requires_it()
    {
        await using var fixture = await Fixture.CreateAsync(requireMarketEvidence: true);
        var item = await fixture.CreateItemAsync();
        var source = await fixture.SeedMarketSurveySourceAsync();

        var action = () => fixture.Service.PrepareMarketSurveyUpdateAsync(item.Id,
            new PrepareQuantitySurveyMarketSurveyUpdateRequest
        {
            MarketAnalysisId = source.AnalysisId,
            PriceBasis = QuantitySurveyMarketSurveyPriceBasis.AverageSurveyPrice,
            EffectiveFrom = DateTime.UtcNow.Date,
            ChangeReason = "Market survey update"
        }, "missing-evidence", default);

        await action.Should().ThrowAsync<QuantitySurveyRateLibraryValidationException>()
            .WithMessage("*central DMS*");
    }

    [Fact]
    public async Task Governed_market_survey_update_uses_controlled_source_and_snapshots_previous_value()
    {
        await using var fixture = await Fixture.CreateAsync(requireMarketEvidence: true);
        var item = await fixture.CreateItemAsync();
        var baselineDraft = await fixture.CreateRateAsync(item.Id, DateTime.UtcNow.Date.AddDays(-20), 100m);
        fixture.UseChecker();
        _ = await fixture.Service.PublishRateAsync(item.Id, baselineDraft.Id,
            new QuantitySurveyRateLifecycleRequest
            {
                RowVersion = baselineDraft.RowVersion,
                Reason = "Independent baseline approval"
            }, "market-baseline-publish", default);
        fixture.UsePreparer();
        var source = await fixture.SeedMarketSurveySourceAsync();

        var prepared = await fixture.Service.PrepareMarketSurveyUpdateAsync(item.Id,
            new PrepareQuantitySurveyMarketSurveyUpdateRequest
            {
                MarketAnalysisId = source.AnalysisId,
                PriceBasis = QuantitySurveyMarketSurveyPriceBasis.AverageSurveyPrice,
                EffectiveFrom = DateTime.UtcNow.Date,
                CentralDocumentVersionId = source.EvidenceVersionId,
                ChangeReason = "Quarterly concrete market survey"
            }, "market-update", default);

        prepared.LifecycleStatus.Should().Be(QuantitySurveyRateLifecycleStatus.Draft);
        prepared.SourceType.Should().Be(QuantitySurveyRateSourceType.MarketSurvey);
        prepared.UnitRate.Should().Be(125m);
        prepared.PreviousRateId.Should().Be(baselineDraft.Id);
        prepared.PreviousUnitRate.Should().Be(100m);
        prepared.VariancePercent.Should().Be(25m);
        prepared.MarketAnalysisId.Should().Be(source.AnalysisId);
        prepared.MarketSurveyQuoteCount.Should().Be(2);
        prepared.NextReviewDueAt.Should().BeAfter(DateTime.UtcNow.Date);

        var audit = (await fixture.Service.GetHistoryAsync(item.Id))
            .Single(value => value.Action == QuantitySurveyAuditEventMap.CreateMarketSurveyUpdate);
        audit.BeforeJson.Should().Contain(baselineDraft.Id.ToString());
        audit.AfterJson.Should().Contain(source.AnalysisId.ToString());

        var bypass = () => fixture.Service.CreateRateAsync(item.Id, new SaveQuantitySurveyRateRequest
        {
            UnitRate = 999m,
            CurrencyId = fixture.CurrencyId,
            EffectiveFrom = DateTime.UtcNow.Date,
            SourceType = QuantitySurveyRateSourceType.MarketSurvey,
            SourceDate = DateTime.UtcNow.Date,
            CentralDocumentVersionId = source.EvidenceVersionId,
            ChangeReason = "Attempt direct market update"
        }, "market-bypass", default);
        await bypass.Should().ThrowAsync<QuantitySurveyRateLibraryValidationException>()
            .WithMessage("*governed market-survey update*");
    }

    [Fact]
    public async Task Version_sequence_includes_soft_deleted_history_and_audit_is_append_only()
    {
        await using var fixture = await Fixture.CreateAsync();
        var item = await fixture.CreateItemAsync();
        fixture.Db.QuantitySurveyRateLibraryRates.Add(new QuantitySurveyRateLibraryRate
        {
            TenantId = fixture.TenantId,
            RateLibraryItemId = item.Id,
            Version = 4,
            UnitRate = 1m,
            CurrencyId = fixture.CurrencyId,
            CurrencyCodeSnapshot = "GHS",
            EffectiveFrom = DateTime.UtcNow.Date.AddYears(-1),
            SourceDate = DateTime.UtcNow.Date.AddYears(-1),
            SourceType = QuantitySurveyRateSourceType.Baseline,
            LifecycleStatus = QuantitySurveyRateLifecycleStatus.Retired,
            ChangeReason = "Historical deleted revision",
            PreparedById = fixture.PreparerId,
            PreparedAt = DateTime.UtcNow.AddYears(-1),
            AuditAction = QuantitySurveyAuditEventMap.RetireRate,
            CorrelationId = "historical",
            IsDeleted = true
        });
        await fixture.Db.SaveChangesAsync();

        var created = await fixture.CreateRateAsync(item.Id, DateTime.UtcNow.Date, 90m);

        created.Version.Should().Be(5);
        var history = await fixture.Service.GetHistoryAsync(item.Id);
        history.Select(value => value.Action).Should().Contain([
            QuantitySurveyAuditEventMap.CreateRateLibraryItem,
            QuantitySurveyAuditEventMap.CreateRateDraft
        ]);
        history.Select(value => value.CorrelationId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Completed_project_boq_cost_is_promoted_with_locked_lineage_and_cannot_be_reused()
    {
        await using var fixture = await Fixture.CreateAsync();
        var item = await fixture.CreateItemAsync();
        var sourceId = await fixture.SeedCompletedBoqSourceAsync(accessible: true);

        var sources = await fixture.Service.GetHistoricalRateSourcesAsync(
            item.Id, QuantitySurveyHistoricalRateSourceType.CompletedBoqLine);
        var source = sources.Should().ContainSingle().Subject;
        source.SourceId.Should().Be(sourceId);
        source.UnitRate.Should().Be(145m);
        source.CanPromote.Should().BeTrue();

        var prepared = await fixture.Service.PrepareHistoricalRateAsync(item.Id,
            new PrepareQuantitySurveyHistoricalRateRequest
            {
                SourceType = source.SourceType,
                SourceId = source.SourceId,
                SourceIntegrityHash = source.IntegrityHash,
                EffectiveFrom = DateTime.UtcNow.Date,
                ChangeReason = "Reuse the independently approved completed-project rate"
            }, "historical-promotion", default);

        prepared.LifecycleStatus.Should().Be(QuantitySurveyRateLifecycleStatus.Draft);
        prepared.SourceType.Should().Be(QuantitySurveyRateSourceType.HistoricalProject);
        prepared.HistoricalSourceId.Should().Be(sourceId);
        prepared.HistoricalProjectId.Should().Be(source.ProjectId);
        prepared.HistoricalProjectCode.Should().Be("PRJ-COMPLETE-001");
        prepared.HistoricalQuantity.Should().Be(20m);
        prepared.HistoricalTotalAmount.Should().Be(2900m);
        prepared.HistoricalSourceHash.Should().Be(source.IntegrityHash);

        var duplicate = () => fixture.Service.PrepareHistoricalRateAsync(item.Id,
            new PrepareQuantitySurveyHistoricalRateRequest
            {
                SourceType = source.SourceType,
                SourceId = source.SourceId,
                SourceIntegrityHash = source.IntegrityHash,
                EffectiveFrom = DateTime.UtcNow.Date,
                ChangeReason = "Attempt to reuse the same source"
            }, "historical-duplicate", default);
        await duplicate.Should().ThrowAsync<QuantitySurveyRateLibraryConflictException>()
            .WithMessage("*already been promoted*");

        var archivedRate = await fixture.Db.QuantitySurveyRateLibraryRates
            .SingleAsync(value => value.Id == prepared.Id);
        archivedRate.IsDeleted = true;
        await fixture.Db.SaveChangesAsync();
        var archivedSource = (await fixture.Service.GetHistoricalRateSourcesAsync(item.Id))
            .Single(value => value.SourceId == sourceId);
        archivedSource.CanPromote.Should().BeFalse(
            "the database uniqueness key includes archived rate versions");

        var audit = (await fixture.Service.GetHistoryAsync(item.Id))
            .Single(value => value.Action == QuantitySurveyAuditEventMap.PromoteHistoricalRate);
        audit.BeforeJson.Should().Contain(sourceId.ToString());
        audit.AfterJson.Should().Contain(source.IntegrityHash);
    }

    [Fact]
    public async Task Historical_source_must_be_accessible_and_unchanged_when_promoted()
    {
        await using var fixture = await Fixture.CreateAsync();
        var item = await fixture.CreateItemAsync();
        _ = await fixture.SeedCompletedBoqSourceAsync(accessible: false);

        (await fixture.Service.GetHistoricalRateSourcesAsync(item.Id)).Should().BeEmpty();

        var sourceId = await fixture.SeedCompletedBoqSourceAsync(accessible: true, projectCode: "PRJ-COMPLETE-002");
        var source = (await fixture.Service.GetHistoricalRateSourcesAsync(item.Id))
            .Single(value => value.SourceId == sourceId);
        var line = await fixture.Db.ProjectBoqVersionLines.SingleAsync(value => value.Id == sourceId);
        line.UnitRate = 155m;
        line.LineAmount = 3100m;
        await fixture.Db.SaveChangesAsync();

        var stale = () => fixture.Service.PrepareHistoricalRateAsync(item.Id,
            new PrepareQuantitySurveyHistoricalRateRequest
            {
                SourceType = source.SourceType,
                SourceId = source.SourceId,
                SourceIntegrityHash = source.IntegrityHash,
                EffectiveFrom = DateTime.UtcNow.Date,
                ChangeReason = "Attempt promotion from stale browser data"
            }, "historical-stale", default);
        await stale.Should().ThrowAsync<QuantitySurveyRateLibraryConflictException>()
            .WithMessage("*changed after it was selected*");
    }

    [Fact]
    public async Task Historical_and_purchase_order_sources_cannot_bypass_governed_promotion()
    {
        await using var fixture = await Fixture.CreateAsync();
        var item = await fixture.CreateItemAsync();

        foreach (var sourceType in new[]
                 {
                     QuantitySurveyRateSourceType.HistoricalProject,
                     QuantitySurveyRateSourceType.PurchaseOrder
                 })
        {
            var bypass = () => fixture.Service.CreateRateAsync(item.Id,
                new SaveQuantitySurveyRateRequest
                {
                    UnitRate = 999m,
                    CurrencyId = fixture.CurrencyId,
                    EffectiveFrom = DateTime.UtcNow.Date,
                    SourceType = sourceType,
                    SourceDate = DateTime.UtcNow.Date,
                    ChangeReason = "Attempt direct authoritative-source entry"
                }, $"historical-bypass-{sourceType}", default);
            await bypass.Should().ThrowAsync<QuantitySurveyRateLibraryValidationException>()
                .WithMessage("*governed source action*");
        }
    }

    [Fact]
    public async Task Every_supported_historical_family_resolves_from_its_owner_and_prepares_a_rate_draft()
    {
        await using var fixture = await Fixture.CreateAsync();
        var inventoryItemId = await fixture.SeedInventoryItemAsync();
        var item = await fixture.CreateItemAsync(inventoryItemId);
        await fixture.SeedAllHistoricalSourceFamiliesAsync(inventoryItemId);

        var sources = await fixture.Service.GetHistoricalRateSourcesAsync(item.Id);
        sources.Select(value => value.SourceType).Should().BeEquivalentTo(new[]
        {
            QuantitySurveyHistoricalRateSourceType.CompletedBoqLine,
            QuantitySurveyHistoricalRateSourceType.CertifiedValuation,
            QuantitySurveyHistoricalRateSourceType.ProcurementPrice,
            QuantitySurveyHistoricalRateSourceType.ActualProjectCost
        });

        foreach (var source in sources)
        {
            var prepared = await fixture.Service.PrepareHistoricalRateAsync(item.Id,
                new PrepareQuantitySurveyHistoricalRateRequest
                {
                    SourceType = source.SourceType,
                    SourceId = source.SourceId,
                    SourceIntegrityHash = source.IntegrityHash,
                    EffectiveFrom = DateTime.UtcNow.Date,
                    ChangeReason = $"Promote governed {source.SourceType} evidence"
                }, $"promote-{source.SourceType}", default);

            prepared.HistoricalSourceType.Should().Be(source.SourceType);
            prepared.HistoricalSourceId.Should().Be(source.SourceId);
            prepared.UnitRate.Should().Be(source.UnitRate);
            prepared.LifecycleStatus.Should().Be(QuantitySurveyRateLifecycleStatus.Draft);
        }
    }

    [Fact]
    public async Task Governed_rate_build_up_uses_published_sources_policy_caps_and_safe_retry()
    {
        await using var fixture = await Fixture.CreateAsync();
        var target = await fixture.CreateItemAsync();
        var material = await fixture.CreateItemAsync(
            "MAT-RATE-001", "Cement material", QuantitySurveyRateItemCategory.Material);
        var materialDraft = await fixture.CreateRateAsync(material.Id, DateTime.UtcNow.Date.AddDays(-5), 50m);
        fixture.UseChecker();
        _ = await fixture.Service.PublishRateAsync(material.Id, materialDraft.Id,
            new QuantitySurveyRateLifecycleRequest
            {
                RowVersion = materialDraft.RowVersion,
                Reason = "Approve controlled material source rate"
            }, "publish-build-up-source", default);
        fixture.UsePreparer();

        var context = await fixture.Service.GetRateBuildUpContextAsync(
            target.Id,
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date,
            fixture.CurrencyId);
        context.Sources.Should().ContainSingle(value => value.RateId == materialDraft.Id);
        context.MaximumOverheadPercent.Should().Be(15m);

        var request = new PreviewQuantitySurveyRateBuildUpRequest
        {
            SourceDate = DateTime.UtcNow.Date,
            CurrencyId = fixture.CurrencyId,
            EffectiveFrom = DateTime.UtcNow.Date,
            Lines =
            [
                new QuantitySurveyRateBuildUpLineRequest
                {
                    Sequence = 1,
                    Component = QuantitySurveyRateComponent.Material,
                    CalculationMethod = QuantitySurveyRateBuildUpCalculationMethod.QuantityTimesPublishedRate,
                    SourceRateId = materialDraft.Id,
                    Quantity = 2m
                },
                new QuantitySurveyRateBuildUpLineRequest
                {
                    Sequence = 2,
                    Component = QuantitySurveyRateComponent.Wastage,
                    CalculationMethod = QuantitySurveyRateBuildUpCalculationMethod.Percentage,
                    PercentageBasis = QuantitySurveyRateBuildUpPercentageBasis.MaterialSubtotal,
                    Percentage = 5m
                },
                new QuantitySurveyRateBuildUpLineRequest
                {
                    Sequence = 3,
                    Component = QuantitySurveyRateComponent.Overhead,
                    CalculationMethod = QuantitySurveyRateBuildUpCalculationMethod.Percentage,
                    PercentageBasis = QuantitySurveyRateBuildUpPercentageBasis.DirectCost,
                    Percentage = 10m
                },
                new QuantitySurveyRateBuildUpLineRequest
                {
                    Sequence = 4,
                    Component = QuantitySurveyRateComponent.Profit,
                    CalculationMethod = QuantitySurveyRateBuildUpCalculationMethod.Percentage,
                    PercentageBasis = QuantitySurveyRateBuildUpPercentageBasis.RunningTotal,
                    Percentage = 10m
                }
            ]
        };
        var preview = await fixture.Service.PreviewRateBuildUpAsync(target.Id, request);
        preview.MaterialSubtotal.Should().Be(100m);
        preview.DirectCost.Should().Be(100m);
        preview.AddOnCost.Should().Be(26.5m);
        preview.UnitRate.Should().Be(126.5m);

        var clientRequestId = Guid.NewGuid();
        var prepare = new PrepareQuantitySurveyRateBuildUpRequest
        {
            ClientRequestId = clientRequestId,
            PreviewIntegrityHash = preview.IntegrityHash,
            ChangeReason = "Prepare transparent tender build-up",
            SourceDate = request.SourceDate,
            CurrencyId = request.CurrencyId,
            EffectiveFrom = request.EffectiveFrom,
            Lines = request.Lines
        };
        var created = await fixture.Service.PrepareRateBuildUpAsync(
            target.Id, prepare, "prepare-rate-build-up", default);
        var retried = await fixture.Service.PrepareRateBuildUpAsync(
            target.Id, prepare, "prepare-rate-build-up-retry", default);

        created.Id.Should().Be(retried.Id);
        created.GeneratedRate.SourceType.Should().Be(QuantitySurveyRateSourceType.RateBuildUp);
        created.GeneratedRate.RateBuildUpId.Should().Be(created.Id);
        created.GeneratedRate.LifecycleStatus.Should().Be(QuantitySurveyRateLifecycleStatus.Draft);
        created.Lines.Should().HaveCount(4);
        (await fixture.Service.GetHistoryAsync(target.Id))
            .Should().ContainSingle(value => value.Action == QuantitySurveyAuditEventMap.CreateRateBuildUp);

        var bypass = () => fixture.Service.CreateRateAsync(target.Id,
            new SaveQuantitySurveyRateRequest
            {
                UnitRate = 999m,
                CurrencyId = fixture.CurrencyId,
                EffectiveFrom = DateTime.UtcNow.Date,
                SourceDate = DateTime.UtcNow.Date,
                SourceType = QuantitySurveyRateSourceType.RateBuildUp,
                ChangeReason = "Attempt direct formula bypass"
            }, "rate-build-up-bypass", default);
        await bypass.Should().ThrowAsync<QuantitySurveyRateLibraryValidationException>()
            .WithMessage("*governed component calculation workflow*");
    }

    [Fact]
    public async Task Rate_build_up_rejects_percentage_above_the_effective_policy_cap()
    {
        await using var fixture = await Fixture.CreateAsync();
        var target = await fixture.CreateItemAsync();

        var action = () => fixture.Service.PreviewRateBuildUpAsync(target.Id,
            new PreviewQuantitySurveyRateBuildUpRequest
            {
                SourceDate = DateTime.UtcNow.Date,
                CurrencyId = fixture.CurrencyId,
                EffectiveFrom = DateTime.UtcNow.Date,
                Lines =
                [
                    new QuantitySurveyRateBuildUpLineRequest
                    {
                        Sequence = 1,
                        Component = QuantitySurveyRateComponent.Transport,
                        CalculationMethod = QuantitySurveyRateBuildUpCalculationMethod.FixedAmount,
                        FixedAmount = 100m
                    },
                    new QuantitySurveyRateBuildUpLineRequest
                    {
                        Sequence = 2,
                        Component = QuantitySurveyRateComponent.Overhead,
                        CalculationMethod = QuantitySurveyRateBuildUpCalculationMethod.Percentage,
                        PercentageBasis = QuantitySurveyRateBuildUpPercentageBasis.DirectCost,
                        Percentage = 16m
                    }
                ]
            }, default);

        await action.Should().ThrowAsync<QuantitySurveyRateLibraryValidationException>()
            .WithMessage("*effective policy ceiling of 15%*");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Guid _activeUserId;
        private string _activeUserName;

        private readonly HashSet<Guid> _accessibleProjectIds = [];

        private Fixture(ApplicationDbContext db, Guid tenantId, Guid preparerId, Guid checkerId,
            Guid unitId, Guid currencyId, Mock<ICurrentUserService> currentUser,
            Mock<IProjectService> projectService)
        {
            Db = db;
            TenantId = tenantId;
            PreparerId = preparerId;
            CheckerId = checkerId;
            UnitId = unitId;
            CurrencyId = currencyId;
            _activeUserId = preparerId;
            _activeUserName = "qs.preparer";
            currentUser.SetupGet(value => value.UserId).Returns(() => _activeUserId.ToString());
            currentUser.SetupGet(value => value.UserName).Returns(() => _activeUserName);
            currentUser.SetupGet(value => value.Roles).Returns(() =>
                _activeUserId == CheckerId
                    ? ["TDC_SUPERVISING_QUANTITY_SURVEYOR"]
                    : ["TDC_QUANTITY_SURVEYOR"]);
            ProjectService = projectService;
            projectService.Setup(value => value.LookupProjectsAsync(
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<int>()))
                .ReturnsAsync((string? _, string? status, Guid? _, Guid? _, Guid? _, int _) =>
                    Db.Projects.Local
                        .Where(project => _accessibleProjectIds.Contains(project.Id) &&
                            (string.IsNullOrWhiteSpace(status) || project.Status == status))
                        .Select(project => new ProjectLookupDto
                        {
                            Id = project.Id,
                            ProjectCode = project.ProjectCode,
                            Title = project.Title,
                            Status = project.Status
                        })
                        .ToList());
            Service = new QuantitySurveyRateLibraryService(db, currentUser.Object, projectService.Object);
        }

        public ApplicationDbContext Db { get; }
        public QuantitySurveyRateLibraryService Service { get; }
        public Mock<IProjectService> ProjectService { get; }
        public Guid TenantId { get; }
        public Guid PreparerId { get; }
        public Guid CheckerId { get; }
        public Guid UnitId { get; }
        public Guid CurrencyId { get; }

        public static async Task<Fixture> CreateAsync(bool requireMarketEvidence = false)
        {
            var tenantId = Guid.NewGuid();
            var preparerId = Guid.NewGuid();
            var checkerId = Guid.NewGuid();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"qs-rate-library-{Guid.NewGuid():N}")
                .ConfigureWarnings(value => value.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
            var unit = new UnitOfMeasure
            {
                TenantId = tenantId,
                Code = "M2",
                Name = "Square metre",
                Category = "Area",
                IsActive = true
            };
            var currency = new Currency
            {
                TenantId = tenantId,
                CurrencyCode = "GHS",
                NumericCode = "936",
                CurrencyName = "Ghana Cedi",
                Status = "Active",
                IsBaseCurrency = true
            };
            var profile = new QuantitySurveyConfigurationProfile
            {
                TenantId = tenantId,
                ProfileCode = "TDC-QUANTITY-SURVEY",
                Name = "QS effective policy",
                Version = 1,
                LifecycleStatus = QuantitySurveyConfigurationProfileStatus.Published,
                EffectiveFrom = DateTime.UtcNow.Date.AddYears(-1),
                PublishedAt = DateTime.UtcNow.AddDays(-1)
            };
            var policy = new QsRateLibraryValue
            {
                EffectiveFrom = DateTime.UtcNow.Date.AddYears(-1),
                Dimensions = [QuantitySurveyRateDimension.Period],
                UpdateCadenceMonths = 3,
                RequireMarketEvidence = requireMarketEvidence
            };
            var decision = new QuantitySurveyConfigurationDecision
            {
                TenantId = tenantId,
                ProfileId = profile.Id,
                Profile = profile,
                DecisionKey = "QS-DEC-005",
                OwnerGroup = "QS + Procurement",
                Status = QuantitySurveyConfigurationDecisionStatus.Approved,
                ApprovalStatus = QuantitySurveyConfigurationApprovalStatus.Approved,
                EvidenceStatus = QuantitySurveyConfigurationEvidenceStatus.Verified,
                ValueJson = JsonSerializer.Serialize(policy, new JsonSerializerOptions
                {
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) }
                })
            };
            var buildUpDecision = new QuantitySurveyConfigurationDecision
            {
                TenantId = tenantId,
                ProfileId = profile.Id,
                Profile = profile,
                DecisionKey = "QS-DEC-004",
                OwnerGroup = "QS + Finance",
                Status = QuantitySurveyConfigurationDecisionStatus.Approved,
                ApprovalStatus = QuantitySurveyConfigurationApprovalStatus.Approved,
                EvidenceStatus = QuantitySurveyConfigurationEvidenceStatus.Verified,
                ValueJson = JsonSerializer.Serialize(new QsRateBuildUpValue
                {
                    EffectiveFrom = DateTime.UtcNow.Date.AddYears(-1),
                    Components = Enum.GetValues<QuantitySurveyRateComponent>().ToList(),
                    MaximumOverheadPercent = 15m,
                    MaximumProfitPercent = 10m,
                    MaximumContingencyPercent = 10m,
                    MaximumWastagePercent = 5m,
                    DecimalPlaces = 2
                }, new JsonSerializerOptions
                {
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) }
                })
            };
            profile.Decisions.Add(decision);
            profile.Decisions.Add(buildUpDecision);
            db.AddRange(unit, currency, profile);
            await db.SaveChangesAsync();

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(value => value.TenantId).Returns(tenantId);
            return new Fixture(db, tenantId, preparerId, checkerId, unit.Id, currency.Id,
                currentUser, new Mock<IProjectService>());
        }

        public Task<QuantitySurveyRateLibraryItemDto> CreateItemAsync(Guid? inventoryItemId = null) =>
            Service.CreateItemAsync(new CreateQuantitySurveyRateLibraryItemRequest
            {
                Code = "RATE-001",
                Name = "Concrete works",
                Category = QuantitySurveyRateItemCategory.StandardItem,
                UnitOfMeasureId = UnitId,
                InventoryItemId = inventoryItemId
            }, "create-item", default);

        public Task<QuantitySurveyRateLibraryItemDto> CreateItemAsync(
            string code,
            string name,
            QuantitySurveyRateItemCategory category) =>
            Service.CreateItemAsync(new CreateQuantitySurveyRateLibraryItemRequest
            {
                Code = code,
                Name = name,
                Category = category,
                UnitOfMeasureId = UnitId
            }, $"create-item-{code}", default);

        public Task<QuantitySurveyRateDto> CreateRateAsync(Guid itemId, DateTime effectiveFrom, decimal rate) =>
            Service.CreateRateAsync(itemId, new SaveQuantitySurveyRateRequest
            {
                UnitRate = rate,
                CurrencyId = CurrencyId,
                EffectiveFrom = effectiveFrom,
                SourceType = QuantitySurveyRateSourceType.Baseline,
                SourceDate = DateTime.UtcNow.Date,
                ChangeReason = $"Prepare rate {rate}"
            }, $"create-rate-{rate}", default);

        public async Task<(Guid AnalysisId, Guid EvidenceVersionId)> SeedMarketSurveySourceAsync()
        {
            var analysis = new MarketAnalysis
            {
                TenantId = TenantId,
                AnalysisCode = "MA-TEST-001",
                Title = "Concrete market survey",
                ItemCategory = "Materials",
                ItemDescription = "Concrete works",
                AnalysisPeriodStart = DateTime.UtcNow.Date.AddDays(-10),
                AnalysisPeriodEnd = DateTime.UtcNow.Date.AddDays(-1),
                CurrentMarketPrice = 124m,
                ForecastedPrice = 130m,
                Currency = "GHS",
                Status = "Published"
            };
            analysis.PriceHistories.Add(new PriceHistory
            {
                TenantId = TenantId,
                MarketAnalysisId = analysis.Id,
                PriceDate = DateTime.UtcNow.Date.AddDays(-2),
                UnitPrice = 120m,
                Currency = "GHS",
                UnitOfMeasure = "M2",
                PriceSource = "MarketSurvey"
            });
            analysis.PriceHistories.Add(new PriceHistory
            {
                TenantId = TenantId,
                MarketAnalysisId = analysis.Id,
                PriceDate = DateTime.UtcNow.Date.AddDays(-1),
                UnitPrice = 130m,
                Currency = "GHS",
                UnitOfMeasure = "M2",
                PriceSource = "SupplierQuote"
            });
            var record = new CentralDocumentRecord
            {
                TenantId = TenantId,
                DocumentReference = "DMS-QS-MKT-001",
                Title = "Concrete market survey evidence",
                SourceModule = "Quantity Survey",
                SourceLabel = "Market survey evidence",
                LifecycleStatus = "Active",
                RepositoryStatus = "Linked",
                VersionStatus = "Published",
                CurrentVersion = "v1.0"
            };
            var version = new CentralDocumentVersion
            {
                TenantId = TenantId,
                DocumentRecordId = record.Id,
                DocumentRecord = record,
                VersionNumber = "v1.0",
                Status = "Published",
                PublishedAt = DateTime.UtcNow.AddMinutes(-5)
            };
            Db.AddRange(analysis, record, version);
            await Db.SaveChangesAsync();
            return (analysis.Id, version.Id);
        }

        public async Task<Guid> SeedCompletedBoqSourceAsync(
            bool accessible,
            string projectCode = "PRJ-COMPLETE-001")
        {
            var project = new Project
            {
                TenantId = TenantId,
                ProjectCode = projectCode,
                Title = $"Completed project {projectCode}",
                Status = ProjectStatuses.Completed,
                BaseCurrencyCode = "GHS"
            };
            var version = new ProjectBoqVersion
            {
                TenantId = TenantId,
                ProjectId = project.Id,
                Project = project,
                VersionNumber = 1,
                VersionType = QuantitySurveyBoqVersionType.Approved,
                Status = ProjectBoqVersionStatuses.Approved,
                ApprovalStatus = ProjectBoqVersionStatuses.Approved,
                PublishedAt = DateTime.UtcNow.AddDays(-5),
                ChangeSummary = "Approved completed-project BoQ",
                AuditAction = QuantitySurveyAuditEventMap.PublishBoqVersion,
                SnapshotHash = new string('A', 64),
                LineCount = 1,
                SnapshotAt = DateTime.UtcNow.AddDays(-5),
                ActorRoles = "TDC_SUPERVISING_QUANTITY_SURVEYOR",
                CorrelationId = $"completed-boq-{projectCode}"
            };
            var line = new ProjectBoqVersionLine
            {
                TenantId = TenantId,
                ProjectId = project.Id,
                ProjectBoqVersionId = version.Id,
                Version = version,
                LineKey = Guid.NewGuid(),
                LineNumber = "1.1",
                ItemCode = "RATE-001",
                Description = "Concrete works",
                Quantity = 20m,
                UnitOfMeasure = "M2",
                UnitRate = 145m,
                LineAmount = 2900m,
                Currency = "GHS"
            };
            Db.AddRange(project, version, line);
            await Db.SaveChangesAsync();
            if (accessible) _accessibleProjectIds.Add(project.Id);
            return line.Id;
        }

        public async Task<Guid> SeedInventoryItemAsync()
        {
            var item = new InventoryItem
            {
                TenantId = TenantId,
                ItemCode = "INV-RATE-001",
                Name = "Concrete material",
                CategoryId = Guid.NewGuid(),
                UnitOfMeasure = "M2",
                Status = ItemStatus.Active,
                IsProjectApplicable = true
            };
            Db.InventoryItems.Add(item);
            await Db.SaveChangesAsync();
            return item.Id;
        }

        public async Task SeedAllHistoricalSourceFamiliesAsync(Guid inventoryItemId)
        {
            var project = new Project
            {
                TenantId = TenantId,
                ProjectCode = "PRJ-HISTORY-ALL",
                Title = "Completed historical source project",
                Status = ProjectStatuses.Completed,
                BaseCurrencyCode = "GHS"
            };
            var package = new ProjectPackage
            {
                TenantId = TenantId,
                ProjectId = project.Id,
                Project = project,
                Code = "PKG-01",
                Name = "Concrete package",
                Status = ProjectPackageStatuses.Completed,
                BudgetAmount = 3000m,
                Currency = "GHS"
            };
            var version = new ProjectBoqVersion
            {
                TenantId = TenantId,
                ProjectId = project.Id,
                Project = project,
                VersionNumber = 1,
                VersionType = QuantitySurveyBoqVersionType.Approved,
                Status = ProjectBoqVersionStatuses.Approved,
                ApprovalStatus = ProjectBoqVersionStatuses.Approved,
                PublishedAt = DateTime.UtcNow.AddDays(-10),
                ChangeSummary = "Approved historical BoQ",
                AuditAction = QuantitySurveyAuditEventMap.PublishBoqVersion,
                SnapshotHash = new string('B', 64),
                LineCount = 1,
                SnapshotAt = DateTime.UtcNow.AddDays(-10),
                ActorRoles = "TDC_SUPERVISING_QUANTITY_SURVEYOR",
                CorrelationId = "historical-all-boq"
            };
            var boqLine = new ProjectBoqVersionLine
            {
                TenantId = TenantId,
                ProjectId = project.Id,
                ProjectBoqVersionId = version.Id,
                Version = version,
                ProjectPackageId = package.Id,
                LineKey = Guid.NewGuid(),
                LineNumber = "1.1",
                ItemCode = "RATE-001",
                Description = "Completed concrete works",
                Quantity = 20m,
                UnitOfMeasure = "M2",
                UnitRate = 150m,
                LineAmount = 3000m,
                Currency = "GHS"
            };
            var valuation = new ProjectInterimValuation
            {
                TenantId = TenantId,
                ProjectId = project.Id,
                Project = project,
                ProjectPackageId = package.Id,
                ProjectPackage = package,
                ValuationNumber = "VAL-HIST-001",
                Title = "Certified single-package valuation",
                Status = ProjectInterimValuationStatuses.Certified,
                ValuationDate = DateTime.UtcNow.AddDays(-7),
                GrossWorkValue = 3000m,
                NetValuationAmount = 3000m,
                Currency = "GHS"
            };
            valuation.CompletedProjectPackages.Add(new ProjectInterimValuationPackageCompletion
            {
                TenantId = TenantId,
                ProjectInterimValuationId = valuation.Id,
                ProjectInterimValuation = valuation,
                ProjectPackageId = package.Id,
                ProjectPackage = package
            });
            var partner = new BusinessPartner
            {
                TenantId = TenantId,
                PartnerCode = "SUP-HIST-001",
                PartnerName = "Historical supplier",
                PartnerType = "Supplier",
                IsActive = true
            };
            var requisition = new PurchaseRequisition
            {
                TenantId = TenantId,
                RequisitionNumber = "PR-HIST-001",
                RequestedById = PreparerId,
                Status = "Ordered",
                ProjectId = project.Id,
                ProjectCode = project.ProjectCode,
                ProjectName = project.Title
            };
            var order = new PurchaseOrder
            {
                TenantId = TenantId,
                OrderNumber = "PO-HIST-001",
                BusinessPartnerId = partner.Id,
                BusinessPartner = partner,
                Status = "Received",
                OrderDate = DateTime.UtcNow.AddDays(-8),
                ApprovedAt = DateTime.UtcNow.AddDays(-8),
                ReceivedDate = DateTime.UtcNow.AddDays(-6),
                Currency = "GHS",
                SourceRequisitionId = requisition.Id,
                SourceRequisition = requisition
            };
            var orderLine = new PurchaseOrderItem
            {
                TenantId = TenantId,
                PurchaseOrderId = order.Id,
                PurchaseOrder = order,
                InventoryItemId = inventoryItemId,
                ItemDescription = "Concrete material",
                OrderedQuantity = 20m,
                ReceivedQuantity = 20m,
                UnitOfMeasure = "M2",
                UnitPrice = 152m,
                LandedUnitCost = 155m,
                LineTotal = 3040m
            };
            var actualCost = new ProjectMaterialCostEntry
            {
                TenantId = TenantId,
                ProjectId = project.Id,
                Project = project,
                EntryDate = DateTime.UtcNow.AddDays(-4),
                EntryType = "InventoryIssue",
                PostingState = "Posted",
                AffectsActualCost = true,
                SourceDocumentNumber = "ISS-HIST-001",
                SourceTransactionType = "InventoryIssue",
                InventoryItemId = inventoryItemId,
                InventoryItemCode = "INV-RATE-001",
                InventoryItemName = "Concrete material",
                Quantity = 20m,
                UnitOfMeasure = "M2",
                UnitCost = 158m,
                Amount = 3160m,
                Currency = "GHS"
            };

            Db.AddRange(project, package, version, boqLine, valuation, partner,
                requisition, order, orderLine, actualCost);
            await Db.SaveChangesAsync();
            _accessibleProjectIds.Add(project.Id);
        }

        public void UsePreparer()
        {
            _activeUserId = PreparerId;
            _activeUserName = "qs.preparer";
        }

        public void UseChecker()
        {
            _activeUserId = CheckerId;
            _activeUserName = "qs.checker";
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
