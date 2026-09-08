using System.Text.Json;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class StockAdjustmentValuationIntentBuilderC9Tests
{
    [Fact]
    public async Task Preview_IsPureDeterministicAndExpandsEveryOrdinaryValuationAndDimensionCase()
    {
        var tenantId = Guid.NewGuid();
        var accounts = (Inventory: Guid.NewGuid(), Expense: Guid.NewGuid(), Recovery: Guid.NewGuid());
        var saves = new CountingSaveChangesInterceptor();
        await using var db = Context(saves);
        await SeedSettingsAsync(db, tenantId, accounts.Inventory, accounts.Expense, accounts.Recovery, baseCurrency: " usd ");
        saves.Count = 0;
        db.ChangeTracker.Clear();
        var adjustment = Adjustment(tenantId);
        var shortage = Item(tenantId, -2m, -10.005m, new DateTime(2026, 1, 1), "Shortage item");
        var recovery = Item(tenantId, 3m, 7.999m, new DateTime(2026, 1, 2), null);
        var zero = Item(tenantId, 1m, 0.004m, new DateTime(2026, 1, 3), "Rounded zero");
        var deleted = Item(tenantId, -1m, -99m, new DateTime(2025, 1, 1), "Deleted");
        deleted.IsDeleted = true;
        adjustment.Items = [recovery, zero, deleted, shortage];
        var builder = new StockAdjustmentValuationIntentBuilder(db);

        var first = await builder.BuildAsync(adjustment);
        var second = await builder.BuildAsync(adjustment);
        adjustment.Items = [shortage, deleted, zero, recovery];
        var reordered = await builder.BuildAsync(adjustment);

        JsonSerializer.Serialize(first).Should().Be(JsonSerializer.Serialize(second));
        JsonSerializer.Serialize(reordered).Should().Be(JsonSerializer.Serialize(first),
            "caller graph order cannot alter the canonical economic intent");
        first.AccountingEventId.Should().NotBeNull().And.NotBe(adjustment.Id);
        first.IdempotencyKey.Should().Be($"STOCK_ADJUSTMENT:{tenantId:N}:{adjustment.Id:N}:POST".ToUpperInvariant());
        first.ParticipantIdentity.Should().Be(StockAdjustmentValuationIntentBuilder.ParticipantCode);
        first.ExpectedOwnerEffect.OwnerEntityId.Should().Be(adjustment.Id);
        first.ExpectedOwnerEffect.EffectFingerprint.Should().MatchRegex("^[0-9A-F]{64}$");
        first.PostingRequest.SourceDocumentId.Should().Be(adjustment.Id);
        first.PostingRequest.SourceDocumentTenantId.Should().Be(tenantId);
        first.PostingRequest.PostingAction.Should().Be("PostStockAdjustment");
        first.PostingRequest.FunctionalCurrencyCode.Should().Be("USD");
        first.PostingRequest.Lines.Should().HaveCount(4);
        first.PostingRequest.Lines.Select(line => line.TransactionTag).Should().Equal(
            "INV-ADJ-EXPENSE", "INV-ADJ-CONTROL", "INV-ADJ-CONTROL", "INV-ADJ-RECOVERY");
        first.PostingRequest.Lines.Select(line => line.AccountId).Should().Equal(
            accounts.Expense, accounts.Inventory, accounts.Inventory, accounts.Recovery);
        first.PostingRequest.Lines.Select(line => (line.DebitAmount, line.CreditAmount)).Should().Equal(
            (10.00m, 0m), (0m, 10.00m), (8.00m, 0m), (0m, 8.00m));
        first.PostingRequest.Lines.Should().OnlyContain(line => line.TransactionCurrency == "USD"
            && line.ExchangeRate == 1m && line.ExchangeRateSource == "Functional currency"
            && line.ExchangeRateDate == adjustment.AdjustmentDate && line.Dimensions.Count == 0);
        first.PostingRequest.Lines.Select(line => line.SourceDocumentLineId).Should().Equal(
            FinanceExternalDimensionIdentity.SourceLine(FinanceExternalProducerContractId.InventoryStockAdjustment,
                adjustment.Id, "writeoff-expense", shortage.Id),
            FinanceExternalDimensionIdentity.SourceLine(FinanceExternalProducerContractId.InventoryStockAdjustment,
                adjustment.Id, "inventory-control", shortage.Id),
            FinanceExternalDimensionIdentity.SourceLine(FinanceExternalProducerContractId.InventoryStockAdjustment,
                adjustment.Id, "inventory-control", recovery.Id),
            FinanceExternalDimensionIdentity.SourceLine(FinanceExternalProducerContractId.InventoryStockAdjustment,
                adjustment.Id, "recovery-income", recovery.Id));
        recovery.AdjustmentValue = 9m;
        var changedEconomics = await builder.BuildAsync(adjustment);
        changedEconomics.AccountingEventId.Should().Be(first.AccountingEventId,
            "the preassigned source identity, not payload contents, controls event identity");
        changedEconomics.IdempotencyKey.Should().Be(first.IdempotencyKey);
        changedEconomics.ExpectedOwnerEffect.EffectFingerprint.Should().NotBe(first.ExpectedOwnerEffect.EffectFingerprint,
            "a material valuation change must produce conflicting owner-effect evidence");
        saves.Count.Should().Be(0, "preview must never call SaveChanges");
        db.ChangeTracker.Entries().Should().BeEmpty("all Finance lookups must remain read-only");
        (await db.FinanceSettings.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Preview_OpeningStockPreservesClearingEconomicsEvidenceAndCurrencyFallbackWithoutSelectingABook()
    {
        var tenantId = Guid.NewGuid();
        var inventory = Guid.NewGuid(); var clearing = Guid.NewGuid();
        await using var db = Context();
        await SeedSettingsAsync(db, tenantId, inventory, Guid.NewGuid(), Guid.NewGuid(), clearing, "  ");
        db.ChangeTracker.Clear();
        var adjustment = Adjustment(tenantId);
        adjustment.ReasonCode = "INITIAL_STOCK";
        adjustment.Status = "Approved";
        adjustment.ApprovedById = Guid.NewGuid();
        adjustment.ApprovedAt = DateTime.UtcNow;
        adjustment.PayloadHash = new string('A', 64);
        adjustment.IntegrityHash = new string('B', 64);
        adjustment.BookClassification = "LOCAL_GAAP";
        adjustment.Reference = "OPENING-SCHEDULE-01";
        adjustment.Items = [Item(tenantId, 2m, 20.125m, DateTime.UtcNow, "Opening item", unitCost: 10.0625m)];

        var intent = await new StockAdjustmentValuationIntentBuilder(db).BuildAsync(adjustment);

        intent.PostingRequest.PostingAction.Should().Be("PostOpeningStock");
        intent.PostingRequest.Description.Should().Be(
            $"Inventory opening stock {adjustment.AdjustmentNumber} - source schedule {adjustment.Reference}");
        intent.PostingRequest.FunctionalCurrencyCode.Should().Be("GHS");
        intent.PostingRequest.Lines.Select(line => line.TransactionTag)
            .Should().Equal("INV-OPEN-CONTROL", "INV-OPEN-MIGRATION");
        intent.PostingRequest.Lines.Select(line => line.AccountId).Should().Equal(inventory, clearing);
        intent.PostingRequest.Lines.Select(line => (line.DebitAmount, line.CreditAmount))
            .Should().Equal((20.12m, 0m), (0m, 20.12m));
        typeof(ProducerFinancePostingRequestDto).GetProperty("AccountingBookCode").Should().BeNull(
            "the neutral C8 member cannot select or enumerate accounting books");
    }

    [Fact]
    public async Task Preview_AcceptsOneCanonicalFullGroupOwnerEffectWithoutChangingItsIdentity()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        await SeedSettingsAsync(db, tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        db.ChangeTracker.Clear();
        var adjustment = Adjustment(tenantId);
        adjustment.Items = [Item(tenantId, -1m, -12m, DateTime.UtcNow, "Disposal valuation")];
        var groupOwner = new ProducerOwnerEffectIdentityDto
        {
            ParticipantCode = "inventory.disposal.v1",
            OwnerEntityType = "inventory_disposal",
            OwnerEntityId = Guid.NewGuid(),
            OwnerAction = "dispose",
            EffectFingerprint = new string('c', 64)
        };

        var intent = await new StockAdjustmentValuationIntentBuilder(db).BuildAsync(adjustment, groupOwner);

        intent.ParticipantIdentity.Should().Be("INVENTORY.DISPOSAL.V1");
        intent.ExpectedOwnerEffect.ParticipantCode.Should().Be("INVENTORY.DISPOSAL.V1");
        intent.ExpectedOwnerEffect.OwnerEntityType.Should().Be("INVENTORY_DISPOSAL");
        intent.ExpectedOwnerEffect.OwnerEntityId.Should().Be(groupOwner.OwnerEntityId);
        intent.ExpectedOwnerEffect.OwnerAction.Should().Be("DISPOSE");
        intent.ExpectedOwnerEffect.EffectFingerprint.Should().Be(new string('C', 64));
    }

    [Fact]
    public async Task Preview_NormalizesEquivalentUtcLocalAndSqlUnspecifiedDatesButConflictsOnActualTimeChange()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        await SeedSettingsAsync(db, tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        db.ChangeTracker.Clear();
        var adjustment = Adjustment(tenantId);
        adjustment.Items = [Item(tenantId, -1m, -12m, DateTime.UtcNow, "Date normalization")];
        var utc = new DateTime(2026, 9, 8, 12, 34, 56, 789, DateTimeKind.Utc).AddTicks(1234);
        var builder = new StockAdjustmentValuationIntentBuilder(db);

        adjustment.AdjustmentDate = utc;
        var utcIntent = await builder.BuildAsync(adjustment);
        adjustment.AdjustmentDate = utc.ToLocalTime();
        var localIntent = await builder.BuildAsync(adjustment);
        adjustment.AdjustmentDate = DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);
        var unspecifiedIntent = await builder.BuildAsync(adjustment);

        localIntent.ExpectedOwnerEffect.EffectFingerprint.Should().Be(utcIntent.ExpectedOwnerEffect.EffectFingerprint,
            "the same instant represented as Local must use the reviewed C6 UTC canonical form");
        unspecifiedIntent.ExpectedOwnerEffect.EffectFingerprint.Should().Be(utcIntent.ExpectedOwnerEffect.EffectFingerprint,
            "SQL/EF Unspecified values preserve the UTC wall-clock ticks used before persistence");
        localIntent.AccountingEventId.Should().Be(utcIntent.AccountingEventId);
        unspecifiedIntent.IdempotencyKey.Should().Be(utcIntent.IdempotencyKey);

        adjustment.AdjustmentDate = DateTime.SpecifyKind(utc.AddTicks(1), DateTimeKind.Unspecified);
        var changedInstant = await builder.BuildAsync(adjustment);
        changedInstant.ExpectedOwnerEffect.EffectFingerprint.Should().NotBe(utcIntent.ExpectedOwnerEffect.EffectFingerprint,
            "a genuine tick change is a conflicting economic request");
    }

    [Fact]
    public async Task Preview_FailsClosedForTenantSourceOwnerAndAccountAuthorityConflicts()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        await SeedSettingsAsync(db, tenantId, Guid.NewGuid(), null, Guid.NewGuid());
        db.ChangeTracker.Clear();
        var builder = new StockAdjustmentValuationIntentBuilder(db);

        var empty = Adjustment(tenantId); empty.Id = Guid.Empty;
        await FluentActions.Awaiting(() => builder.BuildAsync(empty)).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("STOCK_ADJUSTMENT_SOURCE_IDENTITY_REQUIRED*");
        var crossTenant = Adjustment(tenantId);
        crossTenant.Items = [Item(Guid.NewGuid(), -1m, -1m, DateTime.UtcNow, "Wrong tenant")];
        await FluentActions.Awaiting(() => builder.BuildAsync(crossTenant)).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("STOCK_ADJUSTMENT_SOURCE_CONFLICT*");
        var crossTenantMaster = Adjustment(tenantId);
        crossTenantMaster.Items = [Item(tenantId, -1m, -1m, DateTime.UtcNow, "Wrong master tenant")];
        crossTenantMaster.Items.Single().InventoryItem!.TenantId = Guid.NewGuid();
        await FluentActions.Awaiting(() => builder.BuildAsync(crossTenantMaster)).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("STOCK_ADJUSTMENT_SOURCE_CONFLICT*");
        var duplicate = Adjustment(tenantId); var duplicateId = Guid.NewGuid();
        var firstDuplicate = Item(tenantId, -1m, -1m, DateTime.UtcNow, "One");
        var secondDuplicate = Item(tenantId, -1m, -1m, DateTime.UtcNow, "Two");
        firstDuplicate.Id = duplicateId; secondDuplicate.Id = duplicateId;
        duplicate.Items = [firstDuplicate, secondDuplicate];
        await FluentActions.Awaiting(() => builder.BuildAsync(duplicate)).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("STOCK_ADJUSTMENT_SOURCE_CONFLICT*");
        var missingExpense = Adjustment(tenantId);
        missingExpense.Items = [Item(tenantId, -1m, -1m, DateTime.UtcNow, "Shortage")];
        await FluentActions.Awaiting(() => builder.BuildAsync(missingExpense)).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("*Write-off Expense Account*");

        await using var missingRecoveryDb = Context();
        await SeedSettingsAsync(missingRecoveryDb, tenantId, Guid.NewGuid(), Guid.NewGuid(), null);
        missingRecoveryDb.ChangeTracker.Clear();
        var missingRecovery = Adjustment(tenantId);
        missingRecovery.Items = [Item(tenantId, 1m, 1m, DateTime.UtcNow, "Recovery")];
        await FluentActions.Awaiting(() => new StockAdjustmentValuationIntentBuilder(missingRecoveryDb).BuildAsync(missingRecovery)).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("*Write-off Recovery Account*");

        await using var validDb = Context();
        await SeedSettingsAsync(validDb, tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        validDb.ChangeTracker.Clear();
        var valid = Adjustment(tenantId); valid.Items = [Item(tenantId, 1m, 1m, DateTime.UtcNow, "Found")];
        var pseudo = new ProducerOwnerEffectIdentityDto { ParticipantCode = "ALL_ACTIVE_BOOKS",
            OwnerEntityType = "STOCK_ADJUSTMENT", OwnerEntityId = valid.Id, OwnerAction = "POST", EffectFingerprint = new string('A', 64) };
        await FluentActions.Awaiting(() => new StockAdjustmentValuationIntentBuilder(validDb).BuildAsync(valid, pseudo)).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("STOCK_ADJUSTMENT_OWNER_EFFECT_INVALID*");
    }

    [Fact]
    public async Task LegacyAdapter_MapsTheExactNeutralEconomicsThenRetainsDimensionWritesBookAndRetryRequest()
    {
        var tenantId = Guid.NewGuid();
        var inventory = Guid.NewGuid(); var expense = Guid.NewGuid(); var recovery = Guid.NewGuid();
        await using var db = Context();
        await SeedSettingsAsync(db, tenantId, inventory, expense, recovery, baseCurrency: "eur");
        db.ChangeTracker.Clear();
        var adjustment = Adjustment(tenantId);
        adjustment.Items = [Item(tenantId, -2m, -20m, DateTime.UtcNow, "Legacy parity")];
        var requests = new List<FinancePostingRequestV2Dto>();
        var posting = new Mock<IFinancePostingEngine>(MockBehavior.Strict);
        posting.Setup(engine => engine.PostAsync(It.IsAny<FinancePostingRequestV2Dto>(),
                It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestV2Dto, FinancePostingProducerContext, CancellationToken>((request, _, _) => requests.Add(request))
            .ReturnsAsync(new FinancePostingResultDto { PostingEventId = Guid.NewGuid(), JournalEntryId = Guid.NewGuid() });
        var dimensions = new Mock<IFinanceSourceDimensionService>(MockBehavior.Strict);
        dimensions.Setup(service => service.SynchronizeDraftAsync(It.IsAny<FinancePostingProducerContext>(), adjustment.Id,
                adjustment.AdjustmentDate, It.IsAny<IReadOnlyList<FinanceSourceDocumentLineContext>>(), null, false, null,
                "Inventory stock-adjustment Finance adapter capture", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceSourceDocumentDimensionDto());
        dimensions.Setup(service => service.ValidateAndFreezeAsync(It.IsAny<FinancePostingProducerContext>(), adjustment.Id,
                adjustment.AdjustmentDate, It.IsAny<IReadOnlyList<FinanceSourceDocumentLineContext>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceSourceDocumentDimensionDto());
        dimensions.Setup(service => service.ResolvePostingDimensionsAsync(It.IsAny<FinancePostingProducerContext>(), adjustment.Id,
                It.IsAny<Guid>(), It.IsAny<Guid>(), adjustment.AdjustmentDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FinancePostingDimensionValueDto>());
        var service = new InventoryAdjustmentFinancePostingService(db, posting.Object, dimensions.Object,
            new StockAdjustmentValuationIntentBuilder(db));

        await service.PostAsync(adjustment);
        await service.PostAsync(adjustment);

        requests.Should().HaveCount(2);
        JsonSerializer.Serialize(requests[0]).Should().Be(JsonSerializer.Serialize(requests[1]),
            "an exact legacy retry must submit the identical governed request");
        var request = requests[0];
        request.SourceModule.Should().Be("Inventory");
        request.OriginModuleCode.Should().Be(FinanceModuleLockCatalog.Inventory);
        request.SourceDocumentType.Should().Be("StockAdjustment");
        request.SourceDocumentId.Should().Be(adjustment.Id);
        request.SourceDocumentTenantId.Should().Be(tenantId);
        request.PostingAction.Should().Be("PostStockAdjustment");
        request.SourceDocumentReference.Should().Be(adjustment.AdjustmentNumber);
        request.Description.Should().Be($"Inventory stock adjustment {adjustment.AdjustmentNumber} - {adjustment.ReasonCode}");
        request.PostingDate.Should().Be(adjustment.AdjustmentDate);
        request.JournalType.Should().Be("System Generated");
        request.AccountingBookCode.Should().Be("IFRS");
        request.FunctionalCurrencyCode.Should().Be("EUR");
        request.IdempotencyKey.Should().Be($"StockAdjustment:{tenantId:N}:{adjustment.Id:N}:Post");
        request.Lines.Should().HaveCount(2);
        dimensions.Verify(service => service.SynchronizeDraftAsync(It.IsAny<FinancePostingProducerContext>(), adjustment.Id,
            adjustment.AdjustmentDate, It.IsAny<IReadOnlyList<FinanceSourceDocumentLineContext>>(), null, false, null,
            "Inventory stock-adjustment Finance adapter capture", It.IsAny<CancellationToken>()), Times.Exactly(2));
        posting.Verify(engine => engine.PostAsync(It.IsAny<FinancePostingRequestV2Dto>(),
            It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public void BuilderSurface_HasNoC5DimensionPostingOrTransactionCapability()
    {
        typeof(StockAdjustmentValuationIntentBuilder).GetConstructors().Should().ContainSingle();
        typeof(StockAdjustmentValuationIntentBuilder).GetConstructors().Single().GetParameters()
            .Select(parameter => parameter.ParameterType).Should().Equal(typeof(ApplicationDbContext));
        typeof(StockAdjustmentValuationIntentBuilder).GetInterfaces()
            .Should().ContainSingle(type => type == typeof(IStockAdjustmentValuationIntentBuilder));
        typeof(StockAdjustmentValuationIntentBuilder).GetMethods()
            .Where(method => method.DeclaringType == typeof(StockAdjustmentValuationIntentBuilder))
            .Should().ContainSingle(method => method.Name == nameof(IStockAdjustmentValuationIntentBuilder.BuildAsync));
    }

    private static ApplicationDbContext Context(CountingSaveChangesInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"stock-adjustment-c9-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new ApplicationDbContext(options.Options);
    }

    private static async Task SeedSettingsAsync(ApplicationDbContext db, Guid tenantId, Guid inventory,
        Guid? expense, Guid? recovery, Guid? clearing = null, string baseCurrency = "GHS")
    {
        db.FinanceSettings.Add(new FinanceSettings { Id = Guid.NewGuid(), TenantId = tenantId,
            ControlAccountInventoryId = inventory, WriteOffExpenseAccountId = expense,
            WriteOffRecoveryAccountId = recovery, MigrationClearingAccountId = clearing, BaseCurrency = baseCurrency });
        await db.SaveChangesAsync();
    }

    private static StockAdjustment Adjustment(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, AdjustmentNumber = "ADJ-C9-001",
        AdjustmentDate = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc),
        WarehouseId = Guid.NewGuid(), Reference = "C9-SOURCE", ReasonCode = "CycleCount",
        Status = "Approved", BookClassification = "IFRS"
    };

    private static StockAdjustmentItem Item(Guid tenantId, decimal quantity, decimal value, DateTime createdAt,
        string? name, decimal unitCost = 1m)
    {
        var item = new StockAdjustmentItem
        {
            Id = Guid.NewGuid(), TenantId = tenantId, InventoryItemId = Guid.NewGuid(),
            AdjustmentQuantity = quantity, AdjustmentValue = value, UnitCost = unitCost, CreatedAt = createdAt
        };
        if (name is not null) item.InventoryItem = new InventoryItem { TenantId = tenantId, Name = name };
        return item;
    }

    private sealed class CountingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public int Count { get; set; }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Count++;
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }
}
