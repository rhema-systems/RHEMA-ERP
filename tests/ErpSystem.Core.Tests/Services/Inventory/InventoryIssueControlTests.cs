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
        voucher.GetDeclaredTriggers().Select(trigger => trigger.ModelName)
            .Should().Contain("TR_InventoryIssueVouchers_ControlledLifecycle");
        voucher.GetIndexes().Should().Contain(index => index.IsUnique && index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "TenantId", "InventoryRequisitionId", "IdempotencyKey" }));
        voucher.GetCheckConstraints().Select(value => value.Name).Should().Contain(new[]
        {
            "CK_InventoryIssueVouchers_Sod",
            "CK_InventoryIssueVouchers_Acknowledgement",
            "CK_InventoryIssueVouchers_Hashes",
            "CK_InventoryIssueVouchers_MovementReason",
            "CK_InventoryIssueVouchers_FinanceLineage"
        });

        var accountingRule = model.FindEntityType(typeof(InventoryIssueAccountingRule))!;
        accountingRule.GetDeclaredTriggers().Select(trigger => trigger.ModelName)
            .Should().Contain("TR_InventoryIssueAccountingRules_TenantOwner");

        model.FindEntityType(typeof(InventoryIssueVoucherLine))!.GetIndexes().Should().Contain(index =>
            index.IsUnique && index.GetFilter() == null && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    "TenantId", "InventoryIssueVoucherId", "InventoryRequisitionItemId", "LocationId",
                    "LotNumber", "BatchNumber", "SerialNumber"
                }));
        model.FindEntityType(typeof(InventoryIssueVoucherLine))!.GetDeclaredTriggers()
            .Select(trigger => trigger.ModelName).Should().Contain("TR_InventoryIssueVoucherLines_AppendOnly");

        var action = model.FindEntityType(typeof(InventoryIssueVoucherAction))!;
        action.GetDeclaredTriggers().Select(trigger => trigger.ModelName)
            .Should().Contain("TR_InventoryIssueVoucherActions_AppendOnly");
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
    public void Serial_tracking_transaction_line_migration_replaces_aggregate_line_uniqueness()
    {
        var migration = new SerialTrackingTransactionLines();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });

        builder.Operations.OfType<DropIndexOperation>().Select(value => value.Name).Should().Contain(new[]
        {
            "IX_InventoryIssueVoucherLines_TenantId_InventoryIssueVoucherId_InventoryRequisitionItemId",
            "IX_InventoryReturnVoucherLines_InventoryReturnVoucherId_InventoryRequisitionItemId"
        });
        var indexSql = string.Join(Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(value => value.Sql));
        indexSql.Should().Contain("CREATE UNIQUE INDEX [UX_InventoryIssueVoucherLines_Tracking]");
        indexSql.Should().Contain("CREATE UNIQUE INDEX [UX_InventoryReturnVoucherLines_Tracking]");
        indexSql.Should().Contain("[SerialNumber]");
        indexSql.Should().NotContain("WHERE");
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
        results.SelectMany(value => value.MemberNames).Should().Contain(nameof(IssueRequisitionDto.MovementReasonCode));
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
    public void Finance_asset_lifecycle_migration_uses_authoritative_owners_and_database_hard_stops()
    {
        var migration = new INVREQFU003IssueFinanceAssetLifecycle();
        var up = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { up });

        up.Operations.OfType<CreateTableOperation>().Select(value => value.Name).Should().BeEquivalentTo(
            "InventoryIssueAccountingRules", "InventoryIssueFinanceLineages", "InventoryIssueReturnAllocations");
        var lineage = up.Operations.OfType<CreateTableOperation>()
            .Single(value => value.Name == "InventoryIssueFinanceLineages");
        lineage.Columns.Single(value => value.Name == "IssuedValue").ColumnType.Should().Be("decimal(18,2)");
        var allocation = up.Operations.OfType<CreateTableOperation>()
            .Single(value => value.Name == "InventoryIssueReturnAllocations");
        allocation.Columns.Single(value => value.Name == "Value").ColumnType.Should().Be("decimal(18,2)");

        var sql = string.Join(Environment.NewLine, up.Operations.OfType<SqlOperation>().Select(value => value.Sql));
        sql.Should().Contain("TR_InventoryIssueAccountingRules_TenantOwner");
        sql.Should().Contain("TR_InventoryIssueFinanceLineages_Lifecycle");
        sql.Should().Contain("TR_InventoryIssueReturnAllocations_Immutable");
        sql.Should().Contain("TR_InventoryIssueVouchers_ControlledLifecycle");
        sql.Should().Contain("v.FinancePostingEventId IS NULL OR v.FinanceJournalEntryId IS NULL");
        sql.Should().Contain("stock issue requires complete posted Finance/asset lineage");
        sql.Should().Contain("SourceDocumentType = N'InventoryIssueVoucher'");
        sql.Should().Contain("j.IsBalanced = 1");
        sql.Should().Contain("rj.OriginalJournalEntryId <> i.ReturnJournalEntryId");
        sql.Should().Contain("expense.AccountType <> 5");
        sql.Should().Contain("asset.AccountType <> 1");

        var down = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Down", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { down });
        var downSql = string.Join(Environment.NewLine,
            down.Operations.OfType<SqlOperation>().Select(value => value.Sql));
        downSql.Should().Contain("CREATE OR ALTER TRIGGER [dbo].[TR_InventoryIssueVouchers_ControlledLifecycle]");
        downSql.Should().Contain("CREATE OR ALTER TRIGGER [dbo].[TR_StockMovements_GovernedRequisitionIssue]");
    }

    [Fact]
    public void Full_return_reversal_migration_keeps_new_returns_issued_but_allows_posted_compensation()
    {
        var migration = new INVREQFU003AllowPostedFullReturnReversal();
        var up = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { up });

        var sql = up.Operations.OfType<SqlOperation>().Should().ContainSingle().Subject.Sql;
        sql.Should().Contain("r.Status NOT IN (5,6,7)");
        sql.Should().Contain("r.Status = 3 AND i.Status IN (4,5)");
        sql.Should().Contain("INV_RETURN_TRIGGER_DRIFT");

        var down = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Down", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { down });
        down.Operations.OfType<SqlOperation>().Should().ContainSingle().Which.Sql
            .Should().Contain("r.Status NOT IN (5,6,7)");
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
            Mock.Of<IInventoryIssueFinanceAssetService>(),
            Mock.Of<IInventoryValuationService>(),
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
