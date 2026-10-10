using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Api.Controllers.MobilePos;
using ErpSystem.Api.Services.MobilePos;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.MobilePos;

public sealed class MobilePosSyncServiceTests
{
    [Fact]
    public void PushOfflineCommand_ShouldRequireDynamicOfflineAndTillPermissions()
    {
        var policies = typeof(MobilePosRuntimeController)
            .GetMethod(nameof(MobilePosRuntimeController.PushOfflineCommand))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .ToArray();

        policies.Should().BeEquivalentTo(MobilePosPermissions.UseOffline, MobilePosPermissions.OperateTill);
    }

    [Fact]
    public async Task PushAsync_ShouldValidateEnvelopeGrantAndReturnCanonicalSale()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.PushAsync(fixture.Request(), CancellationToken.None);

        result.State.Should().Be("Synced");
        result.ClientMutationId.Should().Be("mutation-1");
        result.Sale!.SaleId.Should().Be(fixture.SaleId);
        fixture.Grants.Verify(service => service.AuthorizeAsync(
            It.Is<MobilePosOfflineGrantValidationRequest>(request =>
                request.CommandType == "CashSale"
                && request.SchemaVersion == 1
                && request.TransactionAmount == 10m
                && request.DeviceId == fixture.DeviceId),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Sales.Verify(service => service.CompleteOfflineAsync(
            It.Is<MobilePosCompleteSaleRequestDto>(request => request.ClientMutationId == "mutation-1"),
            It.IsAny<MobilePosOfflineGrantAuthorization>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PushAsync_ShouldValidateCashReceiptGrantAndReturnCanonicalCollection()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.PushAsync(fixture.CollectionRequest(), CancellationToken.None);

        result.State.Should().Be("Synced");
        result.Collection!.CollectionId.Should().Be(fixture.CollectionId);
        fixture.Grants.Verify(service => service.AuthorizeAsync(
            It.Is<MobilePosOfflineGrantValidationRequest>(request =>
                request.CommandType == "CashReceipt"
                && request.SchemaVersion == 1
                && request.TransactionAmount == 25m
                && request.DeviceId == fixture.DeviceId),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Collections.Verify(service => service.CompleteOfflineAsync(
            It.Is<MobilePosCompleteCollectionRequestDto>(request =>
                request.ClientMutationId == "collection-mutation-1"),
            It.IsAny<MobilePosOfflineGrantAuthorization>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PushAsync_ShouldRejectChangedPayloadBeforeGrantOrFinanceWork()
    {
        var fixture = Fixture.Create();
        var request = fixture.Request();
        request.PayloadHash = new string('0', 64);

        var result = await fixture.Service.PushAsync(request, CancellationToken.None);

        result.State.Should().Be("Rejected");
        result.ErrorCode.Should().Be("MOBILE_POS_SYNC_PAYLOAD_HASH_MISMATCH");
        fixture.Grants.Verify(service => service.AuthorizeAsync(
            It.IsAny<MobilePosOfflineGrantValidationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Sales.Verify(service => service.CompleteOfflineAsync(
            It.IsAny<MobilePosCompleteSaleRequestDto>(), It.IsAny<MobilePosOfflineGrantAuthorization>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PushAsync_ShouldReturnAnExplicitConflictState()
    {
        var fixture = Fixture.Create(conflict: true);

        var result = await fixture.Service.PushAsync(fixture.Request(), CancellationToken.None);

        result.State.Should().Be("Conflict");
        result.ErrorCode.Should().Be("MOBILE_POS_SYNC_IDEMPOTENCY_CONFLICT");
    }

    private sealed class Fixture
    {
        private const string PayloadJson = "{\"clientMutationId\":\"mutation-1\",\"expectedTotalAmount\":10,\"lines\":[],\"localReference\":\"LOCAL-1\",\"occurredAtUtc\":\"2026-10-09T10:00:00Z\",\"tenders\":[{\"amount\":10,\"paymentMethodId\":\"11111111-1111-1111-1111-111111111111\"}]}";
        private const string CollectionPayloadJson = "{\"allocations\":[{\"amount\":25,\"invoiceId\":\"22222222-2222-2222-2222-222222222222\"}],\"businessPartnerId\":\"33333333-3333-3333-3333-333333333333\",\"businessPartnerRoleId\":\"44444444-4444-4444-4444-444444444444\",\"clientMutationId\":\"collection-mutation-1\",\"installationId\":\"install-1\",\"localReference\":\"COL-LOCAL-1\",\"occurredAtUtc\":\"2026-10-09T10:00:00Z\",\"tenders\":[{\"amount\":25,\"paymentMethodId\":\"11111111-1111-1111-1111-111111111111\"}]}";

        private Fixture(
            MobilePosSyncService service,
            Mock<IMobilePosOfflineGrantValidationService> grants,
            Mock<IMobilePosSaleService> sales,
            Mock<IMobilePosCollectionService> collections,
            Guid deviceId,
            Guid saleId,
            Guid collectionId)
        {
            Service = service;
            Grants = grants;
            Sales = sales;
            Collections = collections;
            DeviceId = deviceId;
            SaleId = saleId;
            CollectionId = collectionId;
        }

        public MobilePosSyncService Service { get; }
        public Mock<IMobilePosOfflineGrantValidationService> Grants { get; }
        public Mock<IMobilePosSaleService> Sales { get; }
        public Mock<IMobilePosCollectionService> Collections { get; }
        public Guid DeviceId { get; }
        public Guid SaleId { get; }
        public Guid CollectionId { get; }

        public static Fixture Create(bool conflict = false)
        {
            var deviceId = Guid.NewGuid();
            var saleId = Guid.NewGuid();
            var collectionId = Guid.NewGuid();
            var grants = new Mock<IMobilePosOfflineGrantValidationService>();
            var authorization = new MobilePosOfflineGrantAuthorization(
                new MobilePosOfflineGrant { Id = Guid.NewGuid(), MobilePosDeviceId = deviceId },
                new MobilePosOfflineGrantPolicySnapshotDto());
            grants.Setup(service => service.AuthorizeAsync(
                    It.IsAny<MobilePosOfflineGrantValidationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(authorization);
            var sales = new Mock<IMobilePosSaleService>();
            if (conflict)
            {
                sales.Setup(service => service.CompleteOfflineAsync(
                        It.IsAny<MobilePosCompleteSaleRequestDto>(), authorization, It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new MobilePosMutationConflictException("Already used."));
            }
            else
            {
                sales.Setup(service => service.CompleteOfflineAsync(
                        It.IsAny<MobilePosCompleteSaleRequestDto>(), authorization, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new MobilePosSaleResultDto { SaleId = saleId });
            }
            var collections = new Mock<IMobilePosCollectionService>();
            collections.Setup(service => service.CompleteOfflineAsync(
                    It.IsAny<MobilePosCompleteCollectionRequestDto>(), authorization, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MobilePosCollectionResultDto { CollectionId = collectionId });
            return new Fixture(
                new MobilePosSyncService(grants.Object, sales.Object, collections.Object),
                grants,
                sales,
                collections,
                deviceId,
                saleId,
                collectionId);
        }

        public MobilePosSyncPushRequestDto Request()
        {
            using var document = JsonDocument.Parse(PayloadJson);
            return new MobilePosSyncPushRequestDto
            {
                OfflineGrantToken = "signed-token",
                OfflineGrantId = Guid.NewGuid(),
                DeviceId = DeviceId,
                StoreId = Guid.NewGuid(),
                TillId = Guid.NewGuid(),
                TillSessionId = Guid.NewGuid(),
                ClientMutationId = "mutation-1",
                LocalReference = "LOCAL-1",
                CommandType = "CashSale",
                SchemaVersion = 1,
                PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(PayloadJson))).ToLowerInvariant(),
                Payload = document.RootElement.Clone()
            };
        }

        public MobilePosSyncPushRequestDto CollectionRequest()
        {
            using var document = JsonDocument.Parse(CollectionPayloadJson);
            return new MobilePosSyncPushRequestDto
            {
                OfflineGrantToken = "signed-token",
                OfflineGrantId = Guid.NewGuid(),
                DeviceId = DeviceId,
                StoreId = Guid.NewGuid(),
                TillId = Guid.NewGuid(),
                TillSessionId = Guid.NewGuid(),
                ClientMutationId = "collection-mutation-1",
                LocalReference = "COL-LOCAL-1",
                CommandType = "CashReceipt",
                SchemaVersion = 1,
                PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CollectionPayloadJson))).ToLowerInvariant(),
                Payload = document.RootElement.Clone()
            };
        }
    }
}
