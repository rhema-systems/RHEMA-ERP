using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Data.Repositories;
using ErpSystem.Data.Repositories.Inventory;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryIssueControlTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    [Fact]
    public void Ef_model_has_requisition_concurrency_voucher_replay_and_append_only_lineage()
    {
        var model = _context.GetService<IDesignTimeModel>().Model;
        var requisition = model.FindEntityType(typeof(InventoryRequisition))!;
        requisition.FindProperty(nameof(InventoryRequisition.RowVersion))!.IsConcurrencyToken.Should().BeTrue();

        var voucher = model.FindEntityType(typeof(InventoryIssueVoucher))!;
        voucher.FindProperty(nameof(InventoryIssueVoucher.RowVersion))!.IsConcurrencyToken.Should().BeTrue();
        voucher.GetIndexes().Should().Contain(index => index.IsUnique && index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "TenantId", "InventoryRequisitionId", "IdempotencyKey" }));
        voucher.GetCheckConstraints().Select(value => value.Name).Should().Contain(new[]
        {
            "CK_InventoryIssueVouchers_Sod",
            "CK_InventoryIssueVouchers_Acknowledgement",
            "CK_InventoryIssueVouchers_Hashes"
        });

        var action = model.FindEntityType(typeof(InventoryIssueVoucherAction))!;
        action.GetIndexes().Should().Contain(index => index.IsUnique && index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "TenantId", "InventoryIssueVoucherId", "Sequence" }));
    }

    [Fact]
    public void Stock_movement_has_required_voucher_lineage_relationship()
    {
        var movement = _context.Model.FindEntityType(typeof(StockMovement))!;
        movement.FindProperty(nameof(StockMovement.InventoryIssueVoucherId)).Should().NotBeNull();
        movement.GetForeignKeys().Should().Contain(key =>
            key.Properties.Single().Name == nameof(StockMovement.InventoryIssueVoucherId) &&
            key.PrincipalEntityType.ClrType == typeof(InventoryIssueVoucher));
    }

    [Fact]
    public void Issue_request_requires_replay_concurrency_receiver_and_positive_lines()
    {
        var request = new IssueRequisitionDto
        {
            Items = new List<IssueRequisitionItemDto>()
        };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(request, new ValidationContext(request), results, true).Should().BeFalse();
        results.SelectMany(value => value.MemberNames).Should().Contain(nameof(IssueRequisitionDto.IdempotencyKey));
        results.SelectMany(value => value.MemberNames).Should().Contain(nameof(IssueRequisitionDto.RowVersion));
        results.SelectMany(value => value.MemberNames).Should().Contain(nameof(IssueRequisitionDto.ReceiverUserId));
    }

    [Fact]
    public void Migration_is_three_table_delta_with_sql_hard_stops_and_no_parallel_stock_update()
    {
        var migration = new TDC0606ControlledInventoryIssue();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });

        builder.Operations.OfType<CreateTableOperation>().Select(value => value.Name).Should().BeEquivalentTo(
            "InventoryIssueVouchers", "InventoryIssueVoucherLines", "InventoryIssueVoucherActions");
        builder.Operations.OfType<AddColumnOperation>().Should().Contain(value =>
            value.Table == "InventoryRequisitions" && value.Name == "RowVersion");
        builder.Operations.OfType<AddColumnOperation>().Should().Contain(value =>
            value.Table == "StockMovements" && value.Name == "InventoryIssueVoucherId");

        var sql = string.Join(Environment.NewLine, builder.Operations.OfType<SqlOperation>().Select(value => value.Sql));
        sql.Should().Contain("TR_InventoryIssueVouchers_ControlledLifecycle");
        sql.Should().Contain("INV_ISSUE_APPROVED_SOURCE_REQUIRED");
        sql.Should().Contain("d.ReceiverComment IS NULL");
        sql.Should().Contain("i.IntegrityHash <> d.IntegrityHash");
        sql.Should().Contain("i.IsDeleted <> d.IsDeleted");
        sql.Should().Contain("TR_InventoryIssueVoucherLines_AppendOnly");
        sql.Should().Contain("line.InventoryRequisitionItemId = i.InventoryRequisitionItemId");
        sql.Should().Contain(") > r.IssuedQuantity");
        sql.Should().Contain("TR_InventoryIssueVoucherActions_AppendOnly");
        sql.Should().Contain("TR_StockMovements_GovernedRequisitionIssue");
        sql.Should().Contain("INV_ISSUE_APPROVED_VOUCHER_REQUIRED");
        sql.Should().NotContain("UPDATE [dbo].[WarehouseQuantities]");
        sql.Should().NotContain("UPDATE [dbo].[InventoryItems]");
    }

    [Fact]
    public void Store_issue_voucher_uses_shared_document_output_type()
    {
        DocumentTypes.InventoryStoreIssueVoucher.Should().Be("Inventory.StoreIssueVoucher");
    }

    [Fact]
    public async Task Receiver_only_access_returns_only_vouchers_assigned_to_that_receiver()
    {
        var tenantId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Name = "Issue tenant", Code = $"T{tenantId:N}"[..20] };
        var requester = User("requester");
        var approver = User("approver");
        var issuer = User("issuer");
        var receiver = User("receiver", receiverId);
        var otherReceiver = User("other.receiver");
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "ISSUE-WH", Name = "Issue warehouse", IsActive = true
        };
        var requisition = new InventoryRequisition
        {
            Id = Guid.NewGuid(), TenantId = tenantId, RequisitionNumber = "REQ-RECEIVER-SCOPE",
            DepartmentId = Guid.NewGuid(), WarehouseId = warehouse.Id, Warehouse = warehouse,
            RequestedById = requester.Id, RequestedBy = requester,
            ApprovedById = approver.Id, ApprovedBy = approver,
            IssuedById = issuer.Id, IssuedBy = issuer,
            RequestDate = DateTime.UtcNow, Status = RequisitionStatus.Issued
        };
        var ownVoucher = Voucher("SIV-OWN", receiver);
        var otherVoucher = Voucher("SIV-OTHER", otherReceiver);
        _context.AddRange(tenant, requester, approver, issuer, receiver, otherReceiver,
            warehouse, requisition, ownVoucher, otherVoucher);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(value => value.TenantId).Returns(tenantId);
        current.SetupGet(value => value.UserId).Returns(receiverId);
        current.SetupGet(value => value.Username).Returns("receiver");
        current.SetupGet(value => value.IsAuthenticated).Returns(true);
        current.SetupGet(value => value.IsExternalUser).Returns(false);
        var access = new Mock<IProcurementAccessControlService>();
        access.Setup(value => value.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = false });
        using var unitOfWork = new UnitOfWork(_context);
        var service = new InventoryRequisitionService(
            new InventoryRequisitionRepository(_context),
            Mock.Of<IInventoryRequisitionItemRepository>(),
            Mock.Of<IInventoryItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            Mock.Of<IWarehouseLocationRepository>(),
            Mock.Of<IWarehouseQuantityRepository>(),
            Mock.Of<IStockMovementRepository>(),
            Mock.Of<IConsignmentSettlementService>(),
            Mock.Of<IProjectRepository>(),
            Mock.Of<IProjectService>(),
            unitOfWork,
            current.Object,
            Mock.Of<IWorkflowIntegrationService>(),
            Mock.Of<IWorkflowStatusAdapterRegistry>(),
            Mock.Of<IInventoryTrackingControlService>(),
            Mock.Of<IInventoryNegativeStockControlService>(),
            Mock.Of<IInventoryProjectReservationService>(),
            access.Object,
            Mock.Of<IProcurementControlEventService>(),
            Mock.Of<IInventoryReturnControlService>(),
            NullLogger<InventoryRequisitionService>.Instance);

        var result = await service.GetIssueVouchersAsync(requisition.Id);

        result.Should().ContainSingle().Which.Id.Should().Be(ownVoucher.Id);
        result.Should().NotContain(value => value.Id == otherVoucher.Id);

        ApplicationUser User(string username, Guid? id = null) => new()
        {
            Id = id ?? Guid.NewGuid(), TenantId = tenantId, Tenant = tenant,
            UserName = username, NormalizedUserName = username.ToUpperInvariant(),
            Email = $"{username}@example.test", NormalizedEmail = $"{username}@example.test".ToUpperInvariant(),
            FirstName = username, LastName = "Test", IsActive = true
        };

        InventoryIssueVoucher Voucher(string number, ApplicationUser assignedReceiver) => new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, VoucherNumber = number,
            InventoryRequisitionId = requisition.Id, InventoryRequisition = requisition,
            Status = InventoryIssueVoucherStatus.Issued, WarehouseId = warehouse.Id, Warehouse = warehouse,
            DepartmentId = requisition.DepartmentId, RequestedById = requester.Id, RequestedBy = requester,
            ApprovedById = approver.Id, ApprovedBy = approver, IssuedById = issuer.Id, IssuedBy = issuer,
            ReceiverUserId = assignedReceiver.Id, ReceiverUser = assignedReceiver,
            IssuedAtUtc = DateTime.UtcNow, IdempotencyKey = number, PayloadHash = new string('a', 64),
            CorrelationId = number, SourceSnapshotJson = "{}", IntegrityHash = new string('b', 64)
        };
    }

    public void Dispose() => _context.Dispose();
}
