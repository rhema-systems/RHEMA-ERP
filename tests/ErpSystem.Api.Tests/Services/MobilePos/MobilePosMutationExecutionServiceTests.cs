using ErpSystem.Api.Services.MobilePos;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.MobilePos;

public sealed class MobilePosMutationExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldPersistAndReplayTheCanonicalResultWithoutRunningTheHandlerAgain()
    {
        await using var fixture = Fixture.Create();
        var canonicalInvoiceId = Guid.NewGuid();
        var canonicalPaymentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var handlerCalls = 0;

        Task<MobilePosMutationCompletion<TestResult>> Handler(CancellationToken _)
        {
            handlerCalls++;
            return Task.FromResult(new MobilePosMutationCompletion<TestResult>(
                new TestResult("INV-000123", 125.50m),
                CanonicalInvoiceId: canonicalInvoiceId,
                CanonicalCustomerPaymentIds: canonicalPaymentIds));
        }

        var first = await fixture.Service.ExecuteAsync(
            fixture.DeviceId, "sale-local-001", "Sale.Create", 1,
            new TestCommand("basket-001", 125.50m), Handler, CancellationToken.None);
        var replay = await fixture.Service.ExecuteAsync(
            fixture.DeviceId, "sale-local-001", "Sale.Create", 1,
            new TestCommand("basket-001", 125.50m), Handler, CancellationToken.None);

        handlerCalls.Should().Be(1);
        first.IsReplay.Should().BeFalse();
        replay.IsReplay.Should().BeTrue();
        replay.ReceiptId.Should().Be(first.ReceiptId);
        replay.Result.Should().BeEquivalentTo(first.Result);
        var receipt = await fixture.Db.MobileMutationReceipts.SingleAsync();
        receipt.Status.Should().Be(MobileMutationReceiptStatus.Completed);
        receipt.RequestHash.Should().HaveLength(64);
        receipt.ResultHash.Should().HaveLength(64);
        receipt.CanonicalInvoiceId.Should().Be(canonicalInvoiceId);
        receipt.CanonicalCustomerPaymentIdsJson.Should().Contain(canonicalPaymentIds[0].ToString());
        receipt.ReplayCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectReuseOfAClientMutationIdForChangedContent()
    {
        await using var fixture = Fixture.Create();
        var handlerCalls = 0;
        Task<MobilePosMutationCompletion<TestResult>> Handler(CancellationToken _)
        {
            handlerCalls++;
            return Task.FromResult(new MobilePosMutationCompletion<TestResult>(
                new TestResult("INV-000124", 10m)));
        }

        await fixture.Service.ExecuteAsync(
            fixture.DeviceId, "sale-local-002", "Sale.Create", 1,
            new TestCommand("basket-002", 10m), Handler, CancellationToken.None);
        var action = () => fixture.Service.ExecuteAsync(
            fixture.DeviceId, "sale-local-002", "Sale.Create", 1,
            new TestCommand("basket-002", 11m), Handler, CancellationToken.None);

        await action.Should().ThrowAsync<MobilePosMutationConflictException>()
            .WithMessage("*different Mobile POS request*");
        handlerCalls.Should().Be(1);
        (await fixture.Db.MobileMutationReceipts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPersistAndReplayAnExplicitBusinessRejection()
    {
        await using var fixture = Fixture.Create();
        var handlerCalls = 0;
        Task<MobilePosMutationCompletion<TestResult>> Handler(CancellationToken _)
        {
            handlerCalls++;
            throw new MobilePosCommandRejectedException(
                "MOBILE_POS_PRICE_CHANGED",
                "The server price changed after the basket was prepared.");
        }

        var first = () => fixture.Service.ExecuteAsync(
            fixture.DeviceId, "sale-local-003", "Sale.Create", 1,
            new TestCommand("basket-003", 25m), Handler, CancellationToken.None);
        await first.Should().ThrowAsync<MobilePosCommandRejectedException>()
            .Where(exception => exception.Code == "MOBILE_POS_PRICE_CHANGED" && !exception.IsReplay);

        var replay = () => fixture.Service.ExecuteAsync(
            fixture.DeviceId, "sale-local-003", "Sale.Create", 1,
            new TestCommand("basket-003", 25m), Handler, CancellationToken.None);
        await replay.Should().ThrowAsync<MobilePosCommandRejectedException>()
            .Where(exception => exception.Code == "MOBILE_POS_PRICE_CHANGED" && exception.IsReplay);

        handlerCalls.Should().Be(1);
        var receipt = await fixture.Db.MobileMutationReceipts.SingleAsync();
        receipt.Status.Should().Be(MobileMutationReceiptStatus.Rejected);
        receipt.ErrorCode.Should().Be("MOBILE_POS_PRICE_CHANGED");
        receipt.ErrorDetail.Should().Be("The server price changed after the basket was prepared.");
    }

    private sealed record TestCommand(string BasketReference, decimal Amount);
    private sealed record TestResult(string InvoiceNumber, decimal Amount);

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(
            ApplicationDbContext db,
            MobilePosMutationExecutionService service,
            Guid deviceId)
        {
            Db = db;
            Service = service;
            DeviceId = deviceId;
        }

        public ApplicationDbContext Db { get; }
        public MobilePosMutationExecutionService Service { get; }
        public Guid DeviceId { get; }

        public static Fixture Create()
        {
            var tenantId = Guid.NewGuid();
            var actorId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"mobile-pos-idempotency-{Guid.NewGuid():N}")
                .Options, tenantId);
            db.MobilePosDevices.Add(new MobilePosDevice
            {
                Id = deviceId,
                TenantId = tenantId,
                InstallationIdHash = new string('a', 64),
                DeviceName = "Registered test device",
                RequestedByUserId = actorId,
                Status = MobilePosDeviceStatus.Active
            });
            db.SaveChanges();

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
            currentUser.SetupGet(item => item.UserId).Returns(actorId.ToString());
            currentUser.SetupGet(item => item.UserName).Returns("mobile.cashier");
            var unitOfWork = new UnitOfWork(db);
            var service = new MobilePosMutationExecutionService(db, unitOfWork, currentUser.Object);
            return new Fixture(db, service, deviceId);
        }

        public async ValueTask DisposeAsync() => await Db.DisposeAsync();
    }
}
