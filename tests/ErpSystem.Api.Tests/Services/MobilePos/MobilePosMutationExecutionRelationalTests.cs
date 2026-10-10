using ErpSystem.Api.Services.MobilePos;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.MobilePos;

public sealed class MobilePosMutationExecutionRelationalTests
{
    [Fact]
    public async Task UnexpectedHandlerFailure_ShouldRollbackAllWritesClearTrackingAndAllowExactRetry()
    {
        await using var fixture = await Fixture.CreateAsync();
        var markerDeviceId = Guid.NewGuid();
        var handlerCalls = 0;

        async Task<MobilePosMutationCompletion<TestResult>> Handler(CancellationToken cancellationToken)
        {
            handlerCalls++;
            fixture.Db.MobilePosDevices.Add(fixture.MarkerDevice(markerDeviceId));
            await fixture.Db.SaveChangesAsync(cancellationToken);
            if (handlerCalls == 1)
                throw new InvalidOperationException("injected Mobile POS owner failure");

            return new MobilePosMutationCompletion<TestResult>(new TestResult("INV-REPAIRED"));
        }

        var failed = () => fixture.Service.ExecuteAsync(
            fixture.DeviceId,
            "relational-failure-001",
            "Sale.Create",
            1,
            new TestCommand("basket-relational-001"),
            Handler,
            CancellationToken.None);

        await failed.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("injected Mobile POS owner failure");
        fixture.Db.Database.CurrentTransaction.Should().BeNull();
        fixture.Db.ChangeTracker.Entries().Should().BeEmpty(
            "rolled-back rows must not survive in the shared DbContext state");
        (await fixture.Db.MobileMutationReceipts.AsNoTracking().CountAsync()).Should().Be(0);
        (await fixture.Db.MobilePosDevices.AsNoTracking().CountAsync(item => item.Id == markerDeviceId))
            .Should().Be(0);

        var repaired = await fixture.Service.ExecuteAsync(
            fixture.DeviceId,
            "relational-failure-001",
            "Sale.Create",
            1,
            new TestCommand("basket-relational-001"),
            Handler,
            CancellationToken.None);

        repaired.IsReplay.Should().BeFalse();
        repaired.Result.InvoiceNumber.Should().Be("INV-REPAIRED");
        handlerCalls.Should().Be(2);
        (await fixture.Db.MobilePosDevices.AsNoTracking().CountAsync(item => item.Id == markerDeviceId))
            .Should().Be(1);
        var receipt = await fixture.Db.MobileMutationReceipts.AsNoTracking().SingleAsync();
        receipt.Status.Should().Be(MobileMutationReceiptStatus.Completed);
        receipt.ClientMutationId.Should().Be("relational-failure-001");
    }

    [Fact]
    public async Task BusinessRejectionAfterOwnerWrite_ShouldRollbackOwnerAndPersistOnlyReplayableRejection()
    {
        await using var fixture = await Fixture.CreateAsync();
        var markerDeviceId = Guid.NewGuid();
        var handlerCalls = 0;

        async Task<MobilePosMutationCompletion<TestResult>> Handler(CancellationToken cancellationToken)
        {
            handlerCalls++;
            fixture.Db.MobilePosDevices.Add(fixture.MarkerDevice(markerDeviceId));
            await fixture.Db.SaveChangesAsync(cancellationToken);
            throw new MobilePosCommandRejectedException(
                "MOBILE_POS_INJECTED_REJECTION",
                "Injected rejection after the owner write.");
        }

        var first = () => fixture.Service.ExecuteAsync(
            fixture.DeviceId,
            "relational-rejection-001",
            "Sale.Create",
            1,
            new TestCommand("basket-relational-002"),
            Handler,
            CancellationToken.None);
        await first.Should().ThrowAsync<MobilePosCommandRejectedException>()
            .Where(exception => exception.Code == "MOBILE_POS_INJECTED_REJECTION" && !exception.IsReplay);

        fixture.Db.Database.CurrentTransaction.Should().BeNull();
        (await fixture.Db.MobilePosDevices.AsNoTracking().CountAsync(item => item.Id == markerDeviceId))
            .Should().Be(0, "the owner write belongs to the rolled-back command transaction");
        var receipt = await fixture.Db.MobileMutationReceipts.AsNoTracking().SingleAsync();
        receipt.Status.Should().Be(MobileMutationReceiptStatus.Rejected);
        receipt.ErrorCode.Should().Be("MOBILE_POS_INJECTED_REJECTION");

        var replay = () => fixture.Service.ExecuteAsync(
            fixture.DeviceId,
            "relational-rejection-001",
            "Sale.Create",
            1,
            new TestCommand("basket-relational-002"),
            Handler,
            CancellationToken.None);
        await replay.Should().ThrowAsync<MobilePosCommandRejectedException>()
            .Where(exception => exception.Code == "MOBILE_POS_INJECTED_REJECTION" && exception.IsReplay);
        handlerCalls.Should().Be(1);
        (await fixture.Db.MobileMutationReceipts.AsNoTracking().CountAsync()).Should().Be(1);
    }

    private sealed record TestCommand(string BasketReference);
    private sealed record TestResult(string InvoiceNumber);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private Fixture(
            SqliteConnection connection,
            ApplicationDbContext db,
            MobilePosMutationExecutionService service,
            Guid tenantId,
            Guid actorId,
            Guid deviceId)
        {
            _connection = connection;
            Db = db;
            Service = service;
            TenantId = tenantId;
            ActorId = actorId;
            DeviceId = deviceId;
        }

        public ApplicationDbContext Db { get; }
        public MobilePosMutationExecutionService Service { get; }
        public Guid TenantId { get; }
        public Guid ActorId { get; }
        public Guid DeviceId { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var tenantId = Guid.NewGuid();
            var actorId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlite(connection)
                    .Options,
                tenantId);
            await CreateSchemaAsync(db);
            db.MobilePosDevices.Add(BuildDevice(tenantId, actorId, deviceId, "registered-source"));
            await db.SaveChangesAsync();

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
            currentUser.SetupGet(item => item.UserId).Returns(actorId.ToString());
            currentUser.SetupGet(item => item.UserName).Returns("mobile.cashier");
            var service = new MobilePosMutationExecutionService(db, new UnitOfWork(db), currentUser.Object);
            return new Fixture(connection, db, service, tenantId, actorId, deviceId);
        }

        public MobilePosDevice MarkerDevice(Guid id) =>
            BuildDevice(TenantId, ActorId, id, $"marker-{id:N}");

        private static MobilePosDevice BuildDevice(
            Guid tenantId,
            Guid actorId,
            Guid id,
            string installationSeed) => new()
        {
            Id = id,
            TenantId = tenantId,
            InstallationIdHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(installationSeed))).ToLowerInvariant(),
            DeviceName = installationSeed,
            RequestedByUserId = actorId,
            Status = MobilePosDeviceStatus.Active
        };

        private static async Task CreateSchemaAsync(ApplicationDbContext db)
        {
            foreach (var type in new[] { typeof(MobilePosDevice), typeof(MobileMutationReceipt) })
            {
                var entity = db.Model.FindEntityType(type)!;
                var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                var columns = entity.GetProperties()
                    .Select(property => new
                    {
                        Name = property.GetColumnName(store)!,
                        Type = SqlType(property.ClrType)
                    })
                    .GroupBy(column => column.Name)
                    .Select(group => group.First())
                    .ToArray();
                var key = entity.FindPrimaryKey()!.Properties
                    .Select(property => $"\"{property.GetColumnName(store)}\"");
                var columnSql = string.Join(", ", columns.Select(column =>
                    $"\"{column.Name}\" {column.Type} DEFAULT {SqlDefault(column.Type)}"));
                await db.Database.ExecuteSqlRawAsync(
                    $"CREATE TABLE \"{entity.GetTableName()}\" ({columnSql}, PRIMARY KEY ({string.Join(", ", key)}));");
            }
        }

        private static string SqlType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(byte[])) return "BLOB";
            if (type == typeof(decimal) || type == typeof(double) || type == typeof(float)) return "REAL";
            if (type == typeof(int) || type == typeof(long) || type == typeof(short)
                || type == typeof(bool) || type.IsEnum) return "INTEGER";
            return "TEXT COLLATE NOCASE";
        }

        private static string SqlDefault(string sqlType) => sqlType switch
        {
            "BLOB" => "X'0000000000000000'",
            "REAL" => "0",
            "INTEGER" => "0",
            _ => "''"
        };

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
