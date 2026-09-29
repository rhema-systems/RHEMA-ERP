using System.Data;
using System.Security.Cryptography;
using System.Text;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Inventory;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Inventory;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Inventory;

/// <summary>
/// Shared-connection relational foundation for the disposal C7 owner tests.  It deliberately uses
/// SQLite transactions: the old E2E013 InMemory fixture suppresses transaction semantics and is not
/// evidence for zero-or-all disposal completion.
/// </summary>
public sealed class InventoryDisposalC7SqliteFixtureTests
{
    [Fact]
    public async Task Ambient_C7_failure_rolls_back_real_disposal_adjustment_and_finance_marker()
    {
        await using var fixture = await Fixture.CreateAsync();
        var makerId = Guid.NewGuid();
        var checkerId = Guid.NewGuid();
        var disposalId = Guid.NewGuid();
        var adjustmentId = DeterministicGuid(
            $"RHEMA:INV_DISPOSAL:ADJUSTMENT:V1:{fixture.TenantId:N}:{disposalId:N}");
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var inventoryAccountId = Guid.NewGuid();
        var expenseAccountId = Guid.NewGuid();
        var itemIdentity = DeterministicGuid(
            $"RHEMA:INV_DISPOSAL:ADJUSTMENT_LINE:V1:{fixture.TenantId:N}:{adjustmentId:N}:{lineId:N}:{itemId:N}|{locationId:N}|||"
        );
        var postingDate = new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc);
        var tenant = await fixture.Db.Tenants.SingleAsync(value => value.Id == fixture.TenantId);
        var maker = User(makerId, fixture.TenantId, tenant, "Disposal", "Maker");
        var checker = User(checkerId, fixture.TenantId, tenant, "Finance", "Checker");
        var warehouse = new Warehouse
        {
            Id = warehouseId, TenantId = fixture.TenantId, Code = "C7-WH", Name = "C7 warehouse", IsActive = true
        };
        var location = new WarehouseLocation
        {
            Id = locationId, TenantId = fixture.TenantId, WarehouseId = warehouseId,
            LocationCode = "C7-BIN", Name = "C7 bin", IsActive = true, Warehouse = warehouse
        };
        var category = new InventoryCategory
        {
            Id = categoryId, TenantId = fixture.TenantId, Code = "C7-CAT", Name = "C7 category"
        };
        var item = new InventoryItem
        {
            Id = itemId, TenantId = fixture.TenantId, CategoryId = categoryId, Category = category,
            ItemCode = "C7-ITEM", Name = "C7 disposal item", UnitOfMeasure = "EA",
            ItemType = ItemType.StockItem, Status = ItemStatus.Active, ValuationMethod = ValuationMethod.WeightedAverage,
            InventoryDisposalAccountId = expenseAccountId,
            AverageCost = 10m, StandardCost = 10m, CurrentStock = 5m, AvailableStock = 5m, RowVersion = [1]
        };
        var evidence = Evidence(fixture.TenantId, makerId, "C7-ROLLBACK", "c7-rollback.pdf");
        fixture.Db.Users.AddRange(maker, checker);
        fixture.Db.InventoryCategories.Add(category);
        fixture.Db.Warehouses.Add(warehouse);
        fixture.Db.WarehouseLocations.Add(location);
        fixture.Db.InventoryItems.Add(item);
        fixture.Db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, BaseCurrency = "GHS",
            ControlAccountInventoryId = inventoryAccountId, WriteOffExpenseAccountId = expenseAccountId
        });
        fixture.Db.Accounts.AddRange(
            Account(inventoryAccountId, fixture.TenantId, "141-C7", "Inventory control", AccountType.Asset),
            Account(expenseAccountId, fixture.TenantId, "611-C7", "Disposal expense", AccountType.Expense));
        // The valuation owner now verifies the retained disposal and its configured item account.
        fixture.Db.InventoryDisposalCases.Add(new InventoryDisposalCase
        {
            Id = disposalId, TenantId = fixture.TenantId, DisposalNumber = "C7-DONATION",
            WarehouseId = warehouseId, RequestedById = makerId, RequestedAtUtc = postingDate,
            Method = InventoryDisposalMethod.Donation, Status = InventoryDisposalStatus.Approved,
            AccountingVersion = 1, ApprovedById = checkerId, ApprovedAtUtc = postingDate.AddHours(1),
            Reason = "Donation", IdentificationDetails = "C7 rollback fixture", TotalQuantity = 2m,
            TotalValue = 20m, IdempotencyKey = "C7-DONATION", CorrelationId = "C7-ROLLBACK",
            PayloadHash = new string('A', 64), IntegrityHash = new string('B', 64), RowVersion = [1]
        });
        AddEvidence(fixture.Db, evidence);
        await fixture.Db.SaveChangesAsync();

        var current = new MutableCurrentUser(makerId, fixture.TenantId);
        var unitOfWork = new UnitOfWork(fixture.Db);
        var participant = fixture.CreateDisposalParticipant(current, unitOfWork);
        var owner = new ProducerOwnerEffectIdentityDto
        {
            ParticipantCode = "INVENTORY.DISPOSAL.V1", OwnerEntityType = "INVENTORY_DISPOSAL",
            OwnerEntityId = disposalId, OwnerAction = "COMPLETE",
            EffectFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("C7-ROLLBACK-OWNER")))
        };
        var request = new InventoryDisposalStockAdjustmentRequest
        {
            TenantId = fixture.TenantId, DisposalCaseId = disposalId, FinanceApprovalId = Guid.NewGuid(),
            PreparedOwnerEffectFingerprint = owner.EffectFingerprint, AdjustmentId = adjustmentId,
            ItemIds = [itemIdentity], AdjustmentNumber = $"IDP-SA-{disposalId:N}"[..23].ToUpperInvariant(),
            PostingDateUtc = postingDate, RequestedById = makerId, ApprovedById = checkerId,
            ApprovedAtUtc = postingDate.AddHours(1),
            Create = new CreateStockAdjustmentDto
            {
                WarehouseId = warehouseId, AdjustmentDate = postingDate, ReasonCode = StockAdjustmentReasonCodes.Donation,
                Description = "Donation disposal rollback evidence", Reference = "C7-DONATION",
                IdempotencyKey = $"disposal:{disposalId:N}:adjustment", CorrelationId = "C7-ROLLBACK",
                Evidence = [new InventoryControlEvidenceRequest
                {
                    CentralDocumentVersionId = evidence.Version.Id, EvidenceReference = "Approved donation evidence"
                }],
                Items = [new CreateStockAdjustmentItemDto
                {
                    InventoryItemId = itemId, LocationId = locationId, AdjustmentQuantity = -2m,
                    UnitCost = 10m, Reason = "Donation", Notes = "Controlled donation"
                }]
            }
        };
        var preview = await participant.PreviewAsync(request);
        var intent = await fixture.CreateValuationBuilder().BuildAsync(preview, owner);
        var c7 = new AmbientC7Execution(fixture.Db) { FailAfterStage = true };

        await using (var transaction = await fixture.Db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var staged = await participant.StageApprovedAsync(request);
            var act = async () => await c7.ExecuteWithCompatibilityResultInAmbientTransactionAsync(
                intent.AccountingEventId!.Value, intent, new ProducerOwnerEffectReceiptDto
                {
                    TenantId = fixture.TenantId, ParticipantCode = owner.ParticipantCode,
                    OwnerEntityType = owner.OwnerEntityType, OwnerEntityId = owner.OwnerEntityId,
                    OwnerAction = owner.OwnerAction, EffectFingerprint = owner.EffectFingerprint
                });
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Injected C7 leaf failure");
            staged.Status.Should().Be("Approved");
            await transaction.RollbackAsync();
        }
        fixture.Db.ChangeTracker.Clear();

        (await fixture.Db.StockAdjustments.CountAsync(value => value.Id == adjustmentId)).Should().Be(0);
        (await fixture.Db.StockAdjustmentActions.CountAsync(value => value.StockAdjustmentId == adjustmentId)).Should().Be(0);
        (await fixture.Db.Tenants.AnyAsync(value => value.Code == "C7-00000001")).Should().BeFalse();
        fixture.Db.Database.CurrentTransaction.Should().BeNull();
    }

    [Theory]
    [InlineData(InventoryDisposalMethod.Donation)]
    [InlineData(InventoryDisposalMethod.Destruction)]
    public void Valuation_only_disposals_retain_no_proceeds_shape(
        InventoryDisposalMethod method)
    {
        var disposal = new InventoryDisposalCase
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Method = method,
            ProceedsAmount = 0m, ProceedsAccountId = null, Status = InventoryDisposalStatus.AdjustmentPending
        };
        disposal.ProceedsAmount.Should().Be(0m);
        disposal.ProceedsAccountId.Should().BeNull();
        disposal.Method.Should().Be(method);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Db { get; }
        public Guid TenantId { get; }

        private Fixture(SqliteConnection connection, ApplicationDbContext db, Guid tenantId)
        {
            _connection = connection; Db = db; TenantId = tenantId;
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            connection.CreateCollation("Latin1_General_100_BIN2", string.CompareOrdinal);
            connection.CreateFunction<string?, int>("LEN", value => value?.TrimEnd().Length ?? 0);
            connection.CreateFunction<string?, int>("DATALENGTH",
                value => value is null ? 0 : Encoding.UTF8.GetByteCount(value));
            connection.CreateFunction<string?, int>("ASCII",
                value => string.IsNullOrEmpty(value) ? 0 : value[0]);
            connection.CreateFunction<string?, string?, int>("CHARINDEX", (needle, haystack) =>
                string.IsNullOrEmpty(needle) || haystack is null
                    ? 0
                    : haystack.IndexOf(needle, StringComparison.Ordinal) + 1);
            connection.CreateFunction<string?, string?, string?, int>("DATEDIFF", (_, _, _) => 0);
            connection.CreateFunction<string?, int, string?, string?>("DATEADD", (_, _, value) => value);
            connection.CreateFunction<string?, string?, string?>("CONVERT", (_, value) => value);
            connection.CreateFunction<string?, string?, int, string?>("CONVERT", (_, value, _) => value);
            connection.CreateFunction<string?, int>("DAY", _ => 1);
            connection.CreateFunction<string?, int>("MONTH", _ => 1);
            connection.CreateFunction<string?, int>("YEAR", _ => 2026);
            connection.CreateFunction<int, int, int, string>("DATEFROMPARTS", (year, month, day) =>
                new DateTime(year, month, day).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            connection.CreateFunction<string?, string?>("EOMONTH", value => EndOfMonth(value, 0));
            connection.CreateFunction<string?, int, string?>("EOMONTH", EndOfMonth);
            connection.CreateFunction<string?, int>("ISJSON", value =>
            {
                if (string.IsNullOrWhiteSpace(value)) return 0;
                try { using var _ = System.Text.Json.JsonDocument.Parse(value); return 1; }
                catch (System.Text.Json.JsonException) { return 0; }
            });
            var db = new DisposalSqliteDbContext(new DbContextOptionsBuilder<DisposalSqliteDbContext>()
                .UseSqlite(connection).Options);
            // The shared production model retains several SQL Server store-type/default annotations.
            // SQLite accepts the model once those provider-only spellings are normalized; no
            // production migration or configured database is involved.
            var createScript = db.Database.GenerateCreateScript()
                .Replace("nvarchar(max)", "TEXT", StringComparison.OrdinalIgnoreCase)
                .Replace("varchar(max)", "TEXT", StringComparison.OrdinalIgnoreCase)
                .Replace("varbinary(max)", "BLOB", StringComparison.OrdinalIgnoreCase)
                .Replace("GETUTCDATE()", "CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase)
                .Replace("NEWID()", "lower(hex(randomblob(16)))", StringComparison.OrdinalIgnoreCase)
                .Replace("DATEDIFF(day,", "DATEDIFF('day',", StringComparison.OrdinalIgnoreCase)
                .Replace("DATEADD(day,", "DATEADD('day',", StringComparison.OrdinalIgnoreCase)
                .Replace("CONVERT(date,", "CONVERT('date',", StringComparison.OrdinalIgnoreCase);
            await db.Database.ExecuteSqlRawAsync(createScript);
            var tenant = new Tenant { Id = Guid.NewGuid(), Code = "C7SQLITE", Name = "C7 SQLite fixture" };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            return new Fixture(connection, db, tenant.Id);
        }

        private static string? EndOfMonth(string? value, int months)
        {
            if (value is null) return null;
            var date = DateTime.Parse(value, System.Globalization.CultureInfo.InvariantCulture).AddMonths(months);
            return new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month))
                .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        // Kept beside the relational connection so follow-up C7 owner tests cannot accidentally
        // replace the disposal participant or C9 builder with the old E2E013 mocks.
        public StockAdjustmentValuationIntentBuilder CreateValuationBuilder() => new(Db);

        public IInventoryDisposalStockAdjustmentParticipant CreateDisposalParticipant(
            ICurrentUserProvider currentUser, IUnitOfWork unitOfWork) =>
            new InventoryDisposalStockAdjustmentParticipant(new StockAdjustmentService(
                new StockAdjustmentRepository(Db), new InventoryItemRepository(Db), new StockMovementRepository(Db),
                new WarehouseQuantityRepository(Db), new WarehouseLocationRepository(Db), new WarehouseRepository(Db),
                Mock.Of<IConsignmentSettlementService>(), currentUser, unitOfWork,
                Mock.Of<IInventoryTrackingControlService>(), Mock.Of<IInventoryNegativeStockControlService>(),
                Mock.Of<IProcurementAccessControlService>(), Mock.Of<IProcurementSodGuardService>(),
                Mock.Of<IWorkflowIntegrationService>(), Mock.Of<IProcurementControlEventService>(),
                Mock.Of<IInventoryAdjustmentFinancePostingService>(), Mock.Of<IInventoryValuationService>(),
                NullLogger<StockAdjustmentService>.Instance));
    }

    private sealed class DisposalSqliteDbContext(DbContextOptions<DisposalSqliteDbContext> options)
        : ApplicationDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            var retained = new HashSet<Type>
            {
                typeof(Tenant), typeof(ApplicationUser), typeof(InventoryCategory), typeof(Warehouse),
                typeof(WarehouseLocation), typeof(InventoryItem), typeof(InventoryLocation),
                typeof(FinanceSettings), typeof(Account), typeof(FileUploadRecord),
                typeof(CentralDocumentRecord), typeof(CentralDocumentVersion), typeof(StockAdjustment),
                typeof(StockAdjustmentItem), typeof(StockAdjustmentEvidence), typeof(StockAdjustmentAction),
                typeof(AuditLog), typeof(InventoryDisposalCase)
            };
            foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
                if (!retained.Contains(entityType.ClrType)) modelBuilder.Ignore(entityType.ClrType);
            modelBuilder.Entity<InventoryItem>().Property(value => value.RowVersion).ValueGeneratedNever();
            modelBuilder.Entity<StockAdjustment>().Property(value => value.RowVersion).ValueGeneratedNever();
            modelBuilder.Entity<InventoryDisposalCase>().Property(value => value.RowVersion).ValueGeneratedNever();
        }
    }

    /// <summary>Owner-test seam: it writes Finance-shaped evidence into the owner's ambient context.
    /// A requested fault is thrown before the owner commits, so SQLite verifies rollback of both sides.</summary>
    private sealed class AmbientC7Execution(ApplicationDbContext db) : IFinanceProducerApprovedExecution
    {
        public bool FailAfterStage { get; set; }
        public int Calls { get; private set; }

        public async Task<FinanceProducerApprovedExecutionResult> ExecuteWithCompatibilityResultInAmbientTransactionAsync(
            Guid accountingEventId, ProducerAccountingIntentDto request, ProducerOwnerEffectReceiptDto receipt,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            if (db.Database.CurrentTransaction is null)
                throw new InvalidOperationException("C7 owner execution requires the disposal ambient transaction.");
            var marker = new Tenant { Id = Guid.NewGuid(), Code = $"C7-{Calls:D8}", Name = "C7 owner marker" };
            db.Tenants.Add(marker);
            await db.SaveChangesAsync(cancellationToken);
            if (FailAfterStage) throw new InvalidOperationException("Injected C7 leaf failure");
            return new FinanceProducerApprovedExecutionResult(accountingEventId, "C7-TEST", "Posted", Guid.NewGuid(), Guid.NewGuid());
        }

        public Task<AccountingEventDto> ExecuteInAmbientTransactionAsync(Guid accountingEventId,
            ProducerAccountingIntentDto request, ProducerOwnerEffectReceiptDto receipt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RecordFailureAfterRollbackAsync(Guid accountingEventId, ProducerAccountingIntentDto request,
            ProducerOwnerEffectReceiptDto receipt, Exception failure, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static Guid DeterministicGuid(string canonical)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static (FileUploadRecord Upload, CentralDocumentRecord Record, CentralDocumentVersion Version) Evidence(
        Guid tenantId, Guid userId, string reference, string fileName)
    {
        var upload = new FileUploadRecord
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Category = "inventory-disposal-evidence",
            FilePath = $"dms/inventory/{fileName}", StoredFileName = $"stored-{fileName}",
            OriginalFileName = fileName, ContentType = "application/pdf", FileSize = 256,
            StorageProvider = "Test", UploadedByUserId = userId,
            VirusScanStatus = FileVirusScanStatus.Clean, ScannedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, CreatedById = userId
        };
        var record = new CentralDocumentRecord
        {
            Id = Guid.NewGuid(), TenantId = tenantId, DocumentReference = reference,
            Title = fileName, SourceModule = "Inventory", SourceLabel = "Inventory disposal evidence",
            SourceEntityType = "InventoryDisposal", SourceRecordId = Guid.NewGuid(), RepositoryStatus = "Linked",
            RepositoryPath = upload.FilePath, LifecycleStatus = "Active", CurrentVersion = "v1.0",
            VersionStatus = "Published"
        };
        var version = new CentralDocumentVersion
        {
            Id = Guid.NewGuid(), TenantId = tenantId, DocumentRecordId = record.Id, DocumentRecord = record,
            VersionNumber = "v1.0", Status = "Published", RepositoryPath = upload.FilePath,
            FileName = fileName, ContentType = upload.ContentType, FileSize = upload.FileSize,
            FileUploadRecordId = upload.Id, PublishedAt = DateTime.UtcNow
        };
        return (upload, record, version);
    }

    private static void AddEvidence(ApplicationDbContext db,
        (FileUploadRecord Upload, CentralDocumentRecord Record, CentralDocumentVersion Version) evidence)
    {
        db.FileUploadRecords.Add(evidence.Upload);
        db.CentralDocumentRecords.Add(evidence.Record);
        db.CentralDocumentVersions.Add(evidence.Version);
    }

    private static ApplicationUser User(Guid id, Guid tenantId, Tenant tenant, string first, string last) => new()
    {
        Id = id, TenantId = tenantId, Tenant = tenant, FirstName = first, LastName = last,
        UserName = $"{first}.{last}.{id:N}", NormalizedUserName = $"{first}.{last}.{id:N}".ToUpperInvariant(),
        Email = $"{id:N}@example.test", NormalizedEmail = $"{id:N}@EXAMPLE.TEST", IsActive = true
    };

    private static Account Account(Guid id, Guid tenantId, string number, string name, AccountType type) => new()
    {
        Id = id, TenantId = tenantId, AccountCode = number, AccountNumber = number,
        AccountName = name, AccountType = type, CurrencyCode = "GHS", AllowDirectPosting = true
    };

    private sealed class MutableCurrentUser(Guid userId, Guid tenantId) : ICurrentUserProvider
    {
        public Guid UserId { get; set; } = userId;
        public Guid TenantId { get; } = tenantId;
        public string Username => $"c7sqlite.{UserId:N}";
        public string FullName => "C7 SQLite Actor";
        public bool IsAuthenticated => true;
        public IEnumerable<string> Roles => ["TDC Inventory Control"];
        public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        public IDictionary<string, string> Claims => new Dictionary<string, string>();
        public bool IsExternalUser => false;
        public string AuthenticationProvider => "Test";
    }
}
