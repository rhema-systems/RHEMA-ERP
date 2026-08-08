using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
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

    private sealed class Fixture : IAsyncDisposable
    {
        private Guid _activeUserId;
        private string _activeUserName;

        private Fixture(ApplicationDbContext db, Guid tenantId, Guid preparerId, Guid checkerId,
            Guid unitId, Guid currencyId, Mock<ICurrentUserService> currentUser)
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
            Service = new QuantitySurveyRateLibraryService(db, currentUser.Object);
        }

        public ApplicationDbContext Db { get; }
        public QuantitySurveyRateLibraryService Service { get; }
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
            profile.Decisions.Add(decision);
            db.AddRange(unit, currency, profile);
            await db.SaveChangesAsync();

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(value => value.TenantId).Returns(tenantId);
            return new Fixture(db, tenantId, preparerId, checkerId, unit.Id, currency.Id, currentUser);
        }

        public Task<QuantitySurveyRateLibraryItemDto> CreateItemAsync() =>
            Service.CreateItemAsync(new CreateQuantitySurveyRateLibraryItemRequest
            {
                Code = "RATE-001",
                Name = "Concrete works",
                Category = QuantitySurveyRateItemCategory.StandardItem,
                UnitOfMeasureId = UnitId
            }, "create-item", default);

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
