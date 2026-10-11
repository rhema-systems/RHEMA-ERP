using ErpSystem.Api.Services.MobilePos;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;
using FinancePaymentMethod = ErpSystem.Core.Entities.Finance.PaymentMethod;

namespace ErpSystem.Api.Tests.Services.MobilePos;

public sealed class MobilePosTillEditingRelationalTests
{
    [Fact]
    public async Task TillEdit_ShouldReconcileChildrenAndRejectAStaleRelationalToken()
    {
        await using var fixture = await Fixture.CreateAsync();
        var listed = (await fixture.Service.GetTillsAsync(fixture.Store.Id, CancellationToken.None)).Single();
        var originalPaymentConfigurationId = fixture.PaymentConfiguration.Id;

        var updated = await fixture.Service.SaveTillAsync(fixture.Till.Id, fixture.Input(
            listed.RowVersion,
            "Updated till",
            allowOffline: true), CancellationToken.None);

        updated.Name.Should().Be("Updated till");
        (await fixture.Db.MobilePosTillPaymentMethods.AsNoTracking().SingleAsync()).Id
            .Should().Be(originalPaymentConfigurationId);
        (await fixture.Db.MobilePosTillPaymentMethods.AsNoTracking().SingleAsync()).AllowOffline
            .Should().BeTrue();

        var staleToken = updated.RowVersion;
        const string externalNotes = "Changed by another administrator";
        await fixture.Db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE MobilePosTills SET Notes = {externalNotes} WHERE Id = {fixture.Till.Id}");
        fixture.Db.ChangeTracker.Clear();

        var staleSave = () => fixture.Service.SaveTillAsync(fixture.Till.Id, fixture.Input(
            staleToken,
            "Stale overwrite",
            allowOffline: false), CancellationToken.None);
        var exception = await staleSave.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("MOBILE_POS_TILL_CONCURRENCY_CONFLICT");
        exception.Which.StatusCode.Should().Be(409);

        var persisted = await fixture.Db.MobilePosTills.AsNoTracking().SingleAsync();
        persisted.Name.Should().Be("Updated till");
        persisted.Notes.Should().Be("Changed by another administrator");
        (await fixture.Db.MobilePosTillPaymentMethods.AsNoTracking().SingleAsync()).AllowOffline
            .Should().BeTrue("a stale aggregate edit must not partially replace its payment methods");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private Fixture(
            SqliteConnection connection,
            ApplicationDbContext db,
            MobilePosFoundationService service,
            MobilePosStore store,
            MobilePosTill till,
            MobilePosTillPaymentMethod paymentConfiguration)
        {
            this.connection = connection;
            Db = db;
            Service = service;
            Store = store;
            Till = till;
            PaymentConfiguration = paymentConfiguration;
        }

        public ApplicationDbContext Db { get; }
        public MobilePosFoundationService Service { get; }
        public MobilePosStore Store { get; }
        public MobilePosTill Till { get; }
        public MobilePosTillPaymentMethod PaymentConfiguration { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var tenantId = Guid.NewGuid();
            var actorId = Guid.NewGuid();
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options,
                tenantId);
            await CreateSchemaAsync(db);

            var store = new MobilePosStore
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "STORE-A", Name = "Store A",
                Status = MobilePosStoreStatus.Active, LocationId = Guid.NewGuid(), CurrencyCode = "GHS",
                TimeZoneId = "Africa/Accra", DefaultWalkInBusinessPartnerId = Guid.NewGuid(),
                DefaultWalkInBusinessPartnerRoleId = Guid.NewGuid()
            };
            var liquidity = new LiquidityAccount
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "CASH-A", Name = "Cash till A",
                AccountType = LiquidityAccountType.CashTill, Currency = "GHS", GLAccountId = Guid.NewGuid(),
                IsActive = true
            };
            var paymentMethod = new FinancePaymentMethod
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "CASH", Name = "Cash",
                Type = PaymentMethodType.Cash, IsActive = true, RequiresBankAccount = false
            };
            var till = new MobilePosTill
            {
                Id = Guid.NewGuid(), TenantId = tenantId, MobilePosStoreId = store.Id,
                MobilePosStore = store, TillNumber = "TILL-A", Name = "Till A",
                Status = MobilePosTillStatus.Active, LiquidityAccountId = liquidity.Id,
                LiquidityAccount = liquidity
            };
            var paymentConfiguration = new MobilePosTillPaymentMethod
            {
                Id = Guid.NewGuid(), TenantId = tenantId, MobilePosTillId = till.Id, MobilePosTill = till,
                PaymentMethodId = paymentMethod.Id, PaymentMethod = paymentMethod, AllowOnline = true
            };
            db.AddRange(store, liquidity, paymentMethod, till, paymentConfiguration);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            // SQL Server changes a rowversion on every update. This trigger supplies the same
            // relational behavior for the portable SQLite regression fixture.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER MobilePosTills_RowVersion_Update
                AFTER UPDATE ON MobilePosTills
                BEGIN
                    UPDATE MobilePosTills SET RowVersion = randomblob(8) WHERE Id = NEW.Id;
                END;
                """);

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
            currentUser.SetupGet(item => item.UserId).Returns(actorId.ToString());
            currentUser.SetupGet(item => item.UserName).Returns("mobile.admin");
            currentUser.SetupGet(item => item.IpAddress).Returns("127.0.0.1");
            currentUser.SetupGet(item => item.UserAgent).Returns("Till relational test");
            var environment = new Mock<IHostEnvironment>();
            environment.SetupGet(item => item.EnvironmentName).Returns("Testing");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MobilePos:OfflineGrantSigningKey"] = new string('T', 64)
            }).Build();
            var service = new MobilePosFoundationService(
                db,
                currentUser.Object,
                environment.Object,
                new MobilePosOfflineGrantTokenService(configuration));
            return new Fixture(connection, db, service, store, till, paymentConfiguration);
        }

        public MobilePosTillUpsertDto Input(string rowVersion, string name, bool allowOffline) => new()
        {
            MobilePosStoreId = Store.Id,
            TillNumber = Till.TillNumber,
            Name = name,
            Status = MobilePosTillStatus.Active,
            LiquidityAccountId = Till.LiquidityAccountId,
            RowVersion = rowVersion,
            PaymentMethods =
            [
                new MobilePosTillPaymentMethodInputDto
                {
                    PaymentMethodId = PaymentConfiguration.PaymentMethodId,
                    AllowOnline = true,
                    AllowOffline = allowOffline
                }
            ]
        };

        private static async Task CreateSchemaAsync(ApplicationDbContext db)
        {
            var types = new[]
            {
                typeof(MobilePosStore), typeof(MobilePosTill), typeof(MobilePosTillPaymentMethod),
                typeof(MobilePosDevice), typeof(LiquidityAccount), typeof(FinancePaymentMethod), typeof(AuditLog)
            };
            foreach (var type in types)
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
                    $"\"{column.Name}\" {column.Type} DEFAULT {SqlDefault(column.Type, column.Name)}"));
                await db.Database.ExecuteSqlRawAsync(
                    $"CREATE TABLE \"{entity.GetTableName()}\" ({columnSql}, PRIMARY KEY ({string.Join(", ", key)}));");
            }
        }

        private static string SqlType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(byte[])) return "BLOB";
            if (type == typeof(decimal) || type == typeof(double) || type == typeof(float)) return "REAL";
            if (type == typeof(int) || type == typeof(long) || type == typeof(short) ||
                type == typeof(bool) || type.IsEnum) return "INTEGER";
            return "TEXT COLLATE NOCASE";
        }

        private static string SqlDefault(string sqlType, string columnName) => sqlType switch
        {
            "BLOB" => columnName == "RowVersion" ? "X'0102030405060708'" : "X''",
            "REAL" or "INTEGER" => "0",
            _ => "''"
        };

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
