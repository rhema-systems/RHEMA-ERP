using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Inventory;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryIssueReceiptTests
{
    [Fact]
    public async Task Search_respects_receiver_scope_tenant_deletion_and_does_not_modify_vouchers()
    {
        using var f = await Fixture.Create();
        // Designated receivers may read even when they did not request or approve the requisition.
        f.Requisition.RequestedById = f.Voucher.IssuedById;
        await f.Db.SaveChangesAsync();
        var results = await f.Service.SearchIssueVouchersAsync(" SIV ", 1);
        results.Should().ContainSingle().Which.Id.Should().Be(f.Voucher.Id);
        (await f.Service.SearchIssueVouchersAsync("REQ", 1)).Should().ContainSingle();
        f.Current.SetupGet(actor => actor.UserId).Returns(Guid.NewGuid());
        (await f.Service.SearchIssueVouchersAsync("SIV", 1)).Should().BeEmpty();
        f.Current.SetupGet(actor => actor.UserId).Returns(f.Voucher.ReceiverUserId);
        (await f.Service.SearchIssueVouchersAsync("S", 1)).Should().BeEmpty();
        f.Current.SetupGet(actor => actor.TenantId).Returns(Guid.NewGuid());
        (await f.Service.SearchIssueVouchersAsync("SIV", 1)).Should().BeEmpty();
        f.Current.SetupGet(actor => actor.TenantId).Returns(f.Voucher.TenantId);
        f.Voucher.IsDeleted = true;
        await f.Db.SaveChangesAsync();
        (await f.Service.SearchIssueVouchersAsync("SIV", 1)).Should().BeEmpty();
        (await f.Db.Set<InventoryIssueVoucherReceiptLine>().CountAsync()).Should().Be(0);
        f.Finance.Invocations.Should().BeEmpty();
        f.Valuation.Invocations.Should().BeEmpty();
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service.SearchIssueVouchersAsync("SIV", 1, cancelled.Token));
        f.Current.SetupGet(actor => actor.IsExternalUser).Returns(true);
        await Assert.ThrowsAsync<InventoryIssueAuthorizationException>(() => f.Service.SearchIssueVouchersAsync("SIV", 1));
    }

    [Fact]
    public void Request_requires_an_explicit_receiving_grid()
    {
        var request = new AcknowledgeInventoryIssueVoucherRequest
            { RowVersion = "AQ==", Comment = "Received", IdempotencyKey = "receipt" };
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), errors, true).Should().BeFalse();
        errors.SelectMany(error => error.MemberNames).Should().Contain(nameof(request.Lines));
    }

    [Fact]
    public async Task Partial_then_final_receipt_retains_actual_history_without_stock_or_finance_effects()
    {
        using var f = await Fixture.Create();
        var partial = await f.Receive(92, "first");
        partial.Status.Should().Be(InventoryIssueVoucherStatus.Issued);
        partial.Lines.Single().ReceivedQuantity.Should().Be(92);
        partial.Lines.Single().OutstandingQuantity.Should().Be(8);
        partial.AcknowledgedAtUtc.Should().BeNull();
        var final = await f.Receive(8, "second");
        final.Status.Should().Be(InventoryIssueVoucherStatus.Acknowledged);
        final.Lines.Single().ReceivedQuantity.Should().Be(100);
        final.Lines.Single().OutstandingQuantity.Should().Be(0);
        final.Actions.Select(action => action.ActionType).Should().Equal(
            InventoryIssueVoucherActionType.Issued, InventoryIssueVoucherActionType.PartiallyAcknowledged,
            InventoryIssueVoucherActionType.Acknowledged);
        (await f.Db.Set<InventoryIssueVoucherReceiptLine>().CountAsync()).Should().Be(2);
        (await f.Db.Set<StockMovement>().CountAsync()).Should().Be(0);
        f.Valuation.Invocations.Should().BeEmpty();
        f.Finance.Invocations.Should().BeEmpty();
        f.Line.Quantity.Should().Be(100);
        f.Line.UnitCost.Should().Be(10);
        f.Line.TotalValue.Should().Be(1000);
    }

    [Fact]
    public async Task Full_receipt_and_replay_are_idempotent_even_with_stale_rowversion()
    {
        using var f = await Fixture.Create();
        await f.Receive(100, "first");
        var request = f.Request(100, "first");
        request.RowVersion = "stale";
        var replay = await f.Service.AcknowledgeIssueVoucherAsync(f.Voucher.Id, request, "test");
        replay.Lines.Single().ReceivedQuantity.Should().Be(100);
        (await f.Db.Set<InventoryIssueVoucherReceiptLine>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Earlier_partial_key_replays_after_later_receipt_but_changed_payload_fails()
    {
        using var f = await Fixture.Create();
        await f.Receive(92, "first");
        await f.Receive(8, "second");
        (await f.Receive(92, "first")).Lines.Single().ReceivedQuantity.Should().Be(100);
        await Assert.ThrowsAsync<InventoryIssueControlException>(() => f.Receive(91, "first"));
        (await f.Db.Set<InventoryIssueVoucherReceiptLine>().CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData("negative")]
    [InlineData("zero")]
    [InlineData("precision")]
    [InlineData("duplicate")]
    [InlineData("foreign")]
    [InlineData("over")]
    [InlineData("serial")]
    [InlineData("stale")]
    public async Task Invalid_receipt_is_rejected_without_receipt_rows(string kind)
    {
        using var f = await Fixture.Create();
        var request = f.Request(1, kind);
        switch (kind)
        {
            case "negative": request.Lines[0].ReceivedQuantity = -1; break;
            case "zero": request.Lines[0].ReceivedQuantity = 0; break;
            case "precision": request.Lines[0].ReceivedQuantity = 0.00001m; break;
            case "duplicate": request.Lines.Add(request.Lines[0]); break;
            case "foreign": request.Lines[0].IssueVoucherLineId = Guid.NewGuid(); break;
            case "over": request.Lines[0].ReceivedQuantity = 101; break;
            case "serial": f.Line.SerialNumber = "SERIAL"; await f.Db.SaveChangesAsync(); request.Lines[0].ReceivedQuantity = .5m; break;
            case "stale": request.RowVersion = "Ag=="; break;
        }
        await Assert.ThrowsAsync<InventoryIssueControlException>(() =>
            f.Service.AcknowledgeIssueVoucherAsync(f.Voucher.Id, request, "test"));
        (await f.Db.Set<InventoryIssueVoucherReceiptLine>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Cumulative_overreceipt_and_wrong_receiver_are_rejected()
    {
        using var f = await Fixture.Create();
        await f.Receive(92, "first");
        await Assert.ThrowsAsync<InventoryIssueControlException>(() => f.Receive(9, "over"));
        f.Current.SetupGet(user => user.UserId).Returns(Guid.NewGuid());
        await Assert.ThrowsAsync<InventoryIssueAuthorizationException>(() => f.Receive(8, "other"));
        (await f.Db.Set<InventoryIssueVoucherReceiptLine>().SumAsync(row => row.ReceivedQuantity)).Should().Be(92);
    }

    [Fact]
    public async Task Completion_waits_for_receipt_and_legacy_acknowledgements_keep_historical_full_quantity()
    {
        using var f = await Fixture.Create();
        await Assert.ThrowsAsync<InventoryIssueControlException>(() => f.Service.CompleteAsync(f.Requisition.Id));
        var legacyVoucher = await f.Db.Set<InventoryIssueVoucher>().SingleAsync(value => value.Id == f.Voucher.Id);
        legacyVoucher.Status = InventoryIssueVoucherStatus.Acknowledged;
        legacyVoucher.AcknowledgedById = legacyVoucher.ReceiverUserId;
        legacyVoucher.AcknowledgedAtUtc = DateTime.UtcNow;
        await f.Db.SaveChangesAsync();
        var historical = await f.Service.GetIssueVoucherAsync(f.Voucher.Id);
        historical!.IsLegacyAcknowledgement.Should().BeTrue();
        historical.Lines.Single().ReceivedQuantity.Should().Be(100);
        historical.Lines.Single().OutstandingQuantity.Should().Be(0);
        (await f.Db.Set<InventoryIssueVoucherReceiptLine>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Completion_rechecks_all_issued_receipts_and_only_finishes_after_final_acknowledgement()
    {
        using var f = await Fixture.Create();
        await f.Receive(92, "first");
        await Assert.ThrowsAsync<InventoryIssueControlException>(() => f.Service.CompleteAsync(f.Requisition.Id));
        var partial = await f.Db.Set<InventoryRequisition>().AsNoTracking().SingleAsync(value => value.Id == f.Requisition.Id);
        partial.Status.Should().Be(RequisitionStatus.Issued);
        partial.CompletedDate.Should().BeNull();
        await f.Receive(8, "final");

        (await f.Service.CompleteAsync(f.Requisition.Id)).Should().BeTrue();

        var completed = await f.Db.Set<InventoryRequisition>().AsNoTracking().SingleAsync(value => value.Id == f.Requisition.Id);
        completed.Status.Should().Be(RequisitionStatus.Completed);
        completed.CompletedDate.Should().NotBeNull();
        (await f.Db.Set<InventoryIssueVoucherReceiptLine>().SumAsync(value => value.ReceivedQuantity)).Should().Be(100);
        f.Valuation.VerifyNoOtherCalls();
        f.Finance.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Unfinalized_receipt_evidence_cannot_be_projected_or_replayed_as_success()
    {
        using var f = await Fixture.Create();
        await f.Receive(92, "first");
        await f.Receive(8, "pending");
        // Simulate a direct-SQL recovery commit containing the action and lines but
        // missing the final parent transition. InMemory does not claim SQL parity.
        var voucher = await f.Db.Set<InventoryIssueVoucher>().SingleAsync(value => value.Id == f.Voucher.Id);
        voucher.ReceiptSequence = 1;
        voucher.Status = InventoryIssueVoucherStatus.Issued;
        voucher.AcknowledgedById = null;
        voucher.AcknowledgedAtUtc = null;
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        var readError = await Assert.ThrowsAsync<InventoryIssueControlException>(() => f.Service.GetIssueVoucherAsync(f.Voucher.Id));
        readError.Code.Should().Be("INV_ISSUE_RECEIPT_HISTORY_INCOMPLETE");
        var replayError = await Assert.ThrowsAsync<InventoryIssueControlException>(() => f.Receive(8, "pending"));
        replayError.Code.Should().Be("INV_ISSUE_RECEIPT_HISTORY_INCOMPLETE");

        (await f.Db.Set<InventoryIssueVoucherReceiptLine>().CountAsync()).Should().Be(2);
        (await f.Db.Set<InventoryIssueVoucherAction>().CountAsync()).Should().Be(3);
        var unchanged = await f.Db.Set<InventoryIssueVoucher>().AsNoTracking().SingleAsync(value => value.Id == f.Voucher.Id);
        unchanged.ReceiptSequence.Should().Be(1);
        unchanged.Status.Should().Be(InventoryIssueVoucherStatus.Issued);
        f.Valuation.VerifyNoOtherCalls();
        f.Finance.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Return_of_received_stock_preserves_outstanding_but_unreceived_return_requires_reconciliation()
    {
        using var f = await Fixture.Create();
        await f.Receive(92, "first");
        await f.AddReturn(10);
        (await f.Receive(8, "second")).Lines.Single().ReceivedQuantity.Should().Be(100);
        using var ambiguous = await Fixture.Create();
        await ambiguous.AddReturn(10);
        var error = await Assert.ThrowsAsync<InventoryIssueControlException>(() => ambiguous.Receive(90, "first"));
        error.Message.Should().Contain("Reconcile");
    }

    private sealed class Fixture : IDisposable
    {
        public ApplicationDbContext Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        public Mock<ICurrentUserProvider> Current { get; } = new();
        public Mock<IInventoryValuationService> Valuation { get; } = new();
        public Mock<IInventoryIssueFinanceAssetService> Finance { get; } = new();
        public Mock<IProcurementAccessControlService> Access { get; } = new();
        public InventoryRequisitionService Service { get; private set; } = null!;
        public InventoryRequisition Requisition { get; private set; } = null!;
        public InventoryIssueVoucher Voucher { get; private set; } = null!;
        public InventoryIssueVoucherLine Line { get; private set; } = null!;
        private UnitOfWork unit = null!;

        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Receipt tenant", Code = "RCPT" };
            var receiver = new ApplicationUser { Id = Guid.NewGuid(), TenantId = tenant.Id, Tenant = tenant,
                UserName = "receiver", FirstName = "Receipt", LastName = "Receiver", IsActive = true };
            var approver = new ApplicationUser { Id = Guid.NewGuid(), TenantId = tenant.Id, Tenant = tenant,
                UserName = "approver", FirstName = "Receipt", LastName = "Approver", IsActive = true };
            var issuer = new ApplicationUser { Id = Guid.NewGuid(), TenantId = tenant.Id, Tenant = tenant,
                UserName = "issuer", FirstName = "Receipt", LastName = "Issuer", IsActive = true };
            var warehouse = new Warehouse { Id = Guid.NewGuid(), TenantId = tenant.Id, Code = "WH", Name = "Store" };
            var item = new InventoryItem { Id = Guid.NewGuid(), TenantId = tenant.Id, ItemCode = "ITEM", Name = "Stock" };
            f.Requisition = new InventoryRequisition { Id = Guid.NewGuid(), TenantId = tenant.Id,
                RequisitionNumber = "REQ", WarehouseId = warehouse.Id, Warehouse = warehouse, Status = RequisitionStatus.Issued,
                RequestedById = receiver.Id, ApprovedById = approver.Id, RequestDate = DateTime.UtcNow };
            var reqLine = new InventoryRequisitionItem { Id = Guid.NewGuid(), TenantId = tenant.Id,
                InventoryRequisitionId = f.Requisition.Id, InventoryRequisition = f.Requisition, InventoryItemId = item.Id,
                InventoryItem = item, RequestedQuantity = 100, ApprovedQuantity = 100, IssuedQuantity = 100,
                ItemCode = item.ItemCode, ItemName = item.Name };
            f.Requisition.Items.Add(reqLine);
            f.Voucher = new InventoryIssueVoucher { Id = Guid.NewGuid(), TenantId = tenant.Id,
                InventoryRequisitionId = f.Requisition.Id, InventoryRequisition = f.Requisition,
                VoucherNumber = "SIV", WarehouseId = warehouse.Id, Warehouse = warehouse,
                ReceiverUserId = receiver.Id, ReceiverUser = receiver, RequestedById = receiver.Id,
                ApprovedById = approver.Id, ApprovedBy = approver, IssuedById = issuer.Id, IssuedBy = issuer,
                Status = InventoryIssueVoucherStatus.Issued, IssuedAtUtc = DateTime.UtcNow,
                RowVersion = new byte[] { 1 }, IdempotencyKey = "issue", PayloadHash = new string('a', 64),
                IntegrityHash = new string('b', 64), CorrelationId = "test", MovementReasonCode = InventoryIssueMovementReasons.DepartmentConsumption };
            f.Line = new InventoryIssueVoucherLine { Id = Guid.NewGuid(), TenantId = tenant.Id,
                InventoryIssueVoucherId = f.Voucher.Id, InventoryIssueVoucher = f.Voucher,
                InventoryRequisitionItemId = reqLine.Id, InventoryRequisitionItem = reqLine,
                InventoryItemId = item.Id, InventoryItem = item, WarehouseId = warehouse.Id,
                Quantity = 100, UnitCost = 10, TotalValue = 1000, IntegrityHash = new string('c', 64) };
            f.Voucher.Lines.Add(f.Line);
            f.Voucher.Actions.Add(new InventoryIssueVoucherAction { TenantId = tenant.Id, InventoryIssueVoucherId = f.Voucher.Id,
                ActionType = InventoryIssueVoucherActionType.Issued, StatusAfter = InventoryIssueVoucherStatus.Issued,
                Sequence = 1, ActorUserId = f.Voucher.IssuedById, ActorName = "issuer", Comment = "Issued", CorrelationId = "test",
                IntegrityHash = new string('d', 64) });
            f.Db.AddRange(tenant, receiver, approver, issuer, warehouse, item, f.Requisition, f.Voucher,
                new UserTenant { UserId = receiver.Id, User = receiver, TenantId = tenant.Id, Status = UserTenantStatus.Active },
                new UserTenant { UserId = approver.Id, User = approver, TenantId = tenant.Id, Status = UserTenantStatus.Active },
                new UserTenant { UserId = issuer.Id, User = issuer, TenantId = tenant.Id, Status = UserTenantStatus.Active });
            await f.Db.SaveChangesAsync();
            f.Current.SetupGet(user => user.TenantId).Returns(tenant.Id);
            f.Current.SetupGet(user => user.UserId).Returns(receiver.Id);
            f.Current.SetupGet(user => user.Username).Returns("receiver");
            f.Current.SetupGet(user => user.IsAuthenticated).Returns(true);
            f.Current.SetupGet(user => user.IsExternalUser).Returns(false);
            f.Access.Setup(access => access.CheckCapabilityAsync(It.IsAny<ErpSystem.Core.DTOs.Procurement.ProcurementAccessCapabilityRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ErpSystem.Core.DTOs.Procurement.ProcurementAccessCapabilityDecisionDto { Allowed = false });
            f.unit = new UnitOfWork(f.Db);
            f.Service = new InventoryRequisitionService(new InventoryRequisitionRepository(f.Db),
                Mock.Of<IInventoryRequisitionItemRepository>(), Mock.Of<IInventoryItemRepository>(), Mock.Of<IWarehouseRepository>(),
                Mock.Of<IWarehouseLocationRepository>(), Mock.Of<IWarehouseQuantityRepository>(), Mock.Of<IStockMovementRepository>(),
                Mock.Of<IConsignmentSettlementService>(), Mock.Of<IProjectRepository>(), Mock.Of<IProjectService>(), f.unit, f.Current.Object,
                Mock.Of<IWorkflowIntegrationService>(), Mock.Of<IWorkflowStatusAdapterRegistry>(), Mock.Of<IInventoryTrackingControlService>(),
                Mock.Of<IInventoryNegativeStockControlService>(), Mock.Of<IInventoryProjectReservationService>(),
                f.Access.Object, Mock.Of<IProcurementControlEventService>(),
                Mock.Of<IInventoryReturnControlService>(), f.Finance.Object, f.Valuation.Object,
                NullLogger<InventoryRequisitionService>.Instance);
            return f;
        }

        public AcknowledgeInventoryIssueVoucherRequest Request(decimal quantity, string key) => new()
        {
            RowVersion = "AQ==", Comment = "Actual receipt", IdempotencyKey = key,
            Lines = new() { new() { IssueVoucherLineId = Line.Id, ReceivedQuantity = quantity } }
        };
        public Task<InventoryIssueVoucherDto> Receive(decimal quantity, string key) =>
            Service.AcknowledgeIssueVoucherAsync(Voucher.Id, Request(quantity, key), "test");
        public async Task AddReturn(decimal quantity)
        {
            var lineage = new InventoryIssueFinanceLineage { Id = Guid.NewGuid(), TenantId = Voucher.TenantId,
                InventoryIssueVoucherLineId = Line.Id, InventoryIssueVoucherLine = Line,
                IssuedQuantity = 100, ReturnedQuantity = quantity, IntegrityHash = new string('e', 64) };
            Db.Add(new InventoryIssueReturnAllocation { TenantId = Voucher.TenantId,
                InventoryIssueFinanceLineageId = lineage.Id, InventoryIssueFinanceLineage = lineage,
                Quantity = quantity, IntegrityHash = new string('f', 64) });
            await Db.SaveChangesAsync();
        }
        public void Dispose() { unit?.Dispose(); Db.Dispose(); }
    }
}
