using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class TDC0402FrameworkCallOffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementFrameworkAgreementBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CeilingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CommittedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IssuedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AvailableAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastMovementAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastExpiryAlertAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementFrameworkAgreementBalances", x => x.Id);
                    table.CheckConstraint("CK_ProcurementFrameworkAgreementBalances_State", "[CeilingAmount] > 0 AND [CommittedAmount] >= 0 AND [IssuedAmount] >= 0 AND [IssuedAmount] <= [CommittedAmount] AND [AvailableAmount] >= 0 AND [CommittedAmount] + [AvailableAmount] = [CeilingAmount] AND LEN([CurrencyCode]) = 3 AND LEN([IntegrityHash]) = 64");
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreementBalances_ProcurementFrameworkAgreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "ProcurementFrameworkAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreementBalances_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementFrameworkCallOffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CallOffNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceRequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorityKind = table.Column<int>(type: "int", nullable: false),
                    AuthorityValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AuthorityThreshold = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RequiredDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveryWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliveryAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AgreementNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AgreementVersion = table.Column<int>(type: "int", nullable: false),
                    AgreementIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AwardReadinessIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SupplierEligibilityDecisionHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PriceListReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PriceListVersion = table.Column<int>(type: "int", nullable: false),
                    AgreementEffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AgreementEffectiveEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BalanceDeductedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssuedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssuedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastOperationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastOperation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementFrameworkCallOffs", x => x.Id);
                    table.CheckConstraint("CK_ProcurementFrameworkCallOffs_State", "[Status] BETWEEN 0 AND 5 AND [AuthorityKind] BETWEEN 0 AND 3 AND [AgreementVersion] >= 1 AND [PriceListVersion] >= 1 AND [TotalAmount] > 0 AND LEN([CurrencyCode]) = 3 AND [RequiredDateUtc] >= [AgreementEffectiveFromUtc] AND [RequiredDateUtc] <= [AgreementEffectiveEndUtc] AND LEN([AgreementIntegrityHash]) = 64 AND LEN([AwardReadinessIntegrityHash]) = 64 AND LEN([SupplierEligibilityDecisionHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([SnapshotJson]) = 1 AND LEN([CreationCorrelationId]) > 0 AND LEN([LastOperationCorrelationId]) > 0 AND (([Status] = 1 AND [SubmittedById] IS NOT NULL AND [SubmittedAtUtc] IS NOT NULL AND [WorkflowDefinitionId] IS NOT NULL AND [WorkflowInstanceId] IS NOT NULL) OR [Status] <> 1) AND (([Status] IN (2, 3) AND [ApprovedById] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL AND [BalanceDeductedAtUtc] IS NOT NULL) OR [Status] NOT IN (2, 3)) AND (([Status] = 3 AND [IssuedById] IS NOT NULL AND [IssuedAtUtc] IS NOT NULL) OR [Status] <> 3) AND (([Status] = 4 AND [RejectedById] IS NOT NULL AND [RejectedAtUtc] IS NOT NULL) OR [Status] <> 4) AND (([Status] = 5 AND [CancelledById] IS NOT NULL AND [CancelledAtUtc] IS NOT NULL) OR [Status] <> 5)");
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkCallOffs_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkCallOffs_ProcurementFrameworkAgreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "ProcurementFrameworkAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkCallOffs_ProcurementFrameworkCallOffAuthorities_AuthorityId",
                        column: x => x.AuthorityId,
                        principalTable: "ProcurementFrameworkCallOffAuthorities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkCallOffs_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkCallOffs_PurchaseRequisitions_SourceRequisitionId",
                        column: x => x.SourceRequisitionId,
                        principalTable: "PurchaseRequisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkCallOffs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkCallOffs_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkCallOffs_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementFrameworkBalanceMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementBalanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CallOffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovementType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceBefore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementFrameworkBalanceMovements", x => x.Id);
                    table.CheckConstraint("CK_ProcurementFrameworkBalanceMovements_State", "[MovementType] BETWEEN 0 AND 2 AND [Amount] > 0 AND [BalanceBefore] >= 0 AND [BalanceAfter] >= 0 AND LEN([IdempotencyKey]) > 0 AND LEN([CorrelationId]) > 0 AND LEN([ActorName]) > 0 AND LEN([IntegrityHash]) = 64");
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkBalanceMovements_ProcurementFrameworkAgreementBalances_AgreementBalanceId",
                        column: x => x.AgreementBalanceId,
                        principalTable: "ProcurementFrameworkAgreementBalances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkBalanceMovements_ProcurementFrameworkAgreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "ProcurementFrameworkAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkBalanceMovements_ProcurementFrameworkCallOffs_CallOffId",
                        column: x => x.CallOffId,
                        principalTable: "ProcurementFrameworkCallOffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkBalanceMovements_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementFrameworkCallOffLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CallOffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementPriceLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequisitionItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SourceDemandQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PriceIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementFrameworkCallOffLines", x => x.Id);
                    table.CheckConstraint("CK_ProcurementFrameworkCallOffLines_State", "[Quantity] > 0 AND [UnitPrice] > 0 AND [LineTotal] > 0 AND [SourceDemandQuantity] > 0 AND [Quantity] <= [SourceDemandQuantity] AND LEN([ItemCode]) > 0 AND LEN([ItemName]) > 0 AND LEN([UnitOfMeasure]) > 0 AND LEN([PriceIntegrityHash]) = 64 AND LEN([IntegrityHash]) = 64");
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkCallOffLines_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkCallOffLines_ProcurementFrameworkCallOffs_CallOffId",
                        column: x => x.CallOffId,
                        principalTable: "ProcurementFrameworkCallOffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkCallOffLines_ProcurementFrameworkPriceListLines_AgreementPriceLineId",
                        column: x => x.AgreementPriceLineId,
                        principalTable: "ProcurementFrameworkPriceListLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkCallOffLines_PurchaseOrderItems_PurchaseOrderItemId",
                        column: x => x.PurchaseOrderItemId,
                        principalTable: "PurchaseOrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkCallOffLines_PurchaseRequisitionItems_PurchaseRequisitionItemId",
                        column: x => x.PurchaseRequisitionItemId,
                        principalTable: "PurchaseRequisitionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkCallOffLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementBalances_AgreementId",
                table: "ProcurementFrameworkAgreementBalances",
                column: "AgreementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementBalances_TenantId_AgreementId",
                table: "ProcurementFrameworkAgreementBalances",
                columns: new[] { "TenantId", "AgreementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementBalances_TenantId_LastExpiryAlertAtUtc",
                table: "ProcurementFrameworkAgreementBalances",
                columns: new[] { "TenantId", "LastExpiryAlertAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkBalanceMovements_AgreementBalanceId",
                table: "ProcurementFrameworkBalanceMovements",
                column: "AgreementBalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkBalanceMovements_AgreementId",
                table: "ProcurementFrameworkBalanceMovements",
                column: "AgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkBalanceMovements_CallOffId",
                table: "ProcurementFrameworkBalanceMovements",
                column: "CallOffId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkBalanceMovements_TenantId_AgreementId_OccurredAtUtc",
                table: "ProcurementFrameworkBalanceMovements",
                columns: new[] { "TenantId", "AgreementId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkBalanceMovements_TenantId_CallOffId_MovementType",
                table: "ProcurementFrameworkBalanceMovements",
                columns: new[] { "TenantId", "CallOffId", "MovementType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkBalanceMovements_TenantId_IdempotencyKey",
                table: "ProcurementFrameworkBalanceMovements",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffLines_AgreementPriceLineId",
                table: "ProcurementFrameworkCallOffLines",
                column: "AgreementPriceLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffLines_CallOffId",
                table: "ProcurementFrameworkCallOffLines",
                column: "CallOffId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffLines_InventoryItemId",
                table: "ProcurementFrameworkCallOffLines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffLines_PurchaseOrderItemId",
                table: "ProcurementFrameworkCallOffLines",
                column: "PurchaseOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffLines_PurchaseRequisitionItemId",
                table: "ProcurementFrameworkCallOffLines",
                column: "PurchaseRequisitionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffLines_TenantId_CallOffId_AgreementPriceLineId",
                table: "ProcurementFrameworkCallOffLines",
                columns: new[] { "TenantId", "CallOffId", "AgreementPriceLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffLines_TenantId_CallOffId_PurchaseRequisitionItemId",
                table: "ProcurementFrameworkCallOffLines",
                columns: new[] { "TenantId", "CallOffId", "PurchaseRequisitionItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffLines_TenantId_PurchaseOrderItemId",
                table: "ProcurementFrameworkCallOffLines",
                columns: new[] { "TenantId", "PurchaseOrderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffLines_TenantId_PurchaseRequisitionItemId",
                table: "ProcurementFrameworkCallOffLines",
                columns: new[] { "TenantId", "PurchaseRequisitionItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffs_AgreementId",
                table: "ProcurementFrameworkCallOffs",
                column: "AgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffs_AuthorityId",
                table: "ProcurementFrameworkCallOffs",
                column: "AuthorityId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffs_BusinessPartnerId",
                table: "ProcurementFrameworkCallOffs",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffs_PurchaseOrderId",
                table: "ProcurementFrameworkCallOffs",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffs_SourceRequisitionId",
                table: "ProcurementFrameworkCallOffs",
                column: "SourceRequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffs_TenantId_AgreementId_Status",
                table: "ProcurementFrameworkCallOffs",
                columns: new[] { "TenantId", "AgreementId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffs_TenantId_BusinessPartnerId_Status",
                table: "ProcurementFrameworkCallOffs",
                columns: new[] { "TenantId", "BusinessPartnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffs_TenantId_CallOffNumber",
                table: "ProcurementFrameworkCallOffs",
                columns: new[] { "TenantId", "CallOffNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffs_TenantId_CreationCorrelationId",
                table: "ProcurementFrameworkCallOffs",
                columns: new[] { "TenantId", "CreationCorrelationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffs_TenantId_PurchaseOrderId",
                table: "ProcurementFrameworkCallOffs",
                columns: new[] { "TenantId", "PurchaseOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffs_TenantId_SourceRequisitionId_Status",
                table: "ProcurementFrameworkCallOffs",
                columns: new[] { "TenantId", "SourceRequisitionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffs_TenantId_WorkflowInstanceId",
                table: "ProcurementFrameworkCallOffs",
                columns: new[] { "TenantId", "WorkflowInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffs_WorkflowDefinitionId",
                table: "ProcurementFrameworkCallOffs",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffs_WorkflowInstanceId",
                table: "ProcurementFrameworkCallOffs",
                column: "WorkflowInstanceId");

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [TR_ProcurementFrameworkCallOffs_Lifecycle]
                ON [ProcurementFrameworkCallOffs]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1 FROM deleted d
                        LEFT JOIN inserted i ON i.Id = d.Id
                        WHERE i.Id IS NULL)
                        THROW 51101, 'Framework call-offs cannot be physically deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted prior ON prior.Id = i.Id
                        LEFT JOIN [ProcurementFrameworkAgreements] agreement
                            ON agreement.Id = i.AgreementId
                           AND agreement.TenantId = i.TenantId
                        LEFT JOIN [BusinessPartners] partner
                            ON partner.Id = i.BusinessPartnerId
                           AND partner.TenantId = i.TenantId
                        LEFT JOIN [PurchaseOrders] purchaseOrder
                            ON purchaseOrder.Id = i.PurchaseOrderId
                           AND purchaseOrder.TenantId = i.TenantId
                        LEFT JOIN [PurchaseRequisitions] requisition
                            ON requisition.Id = i.SourceRequisitionId
                           AND requisition.TenantId = i.TenantId
                        LEFT JOIN [ProcurementFrameworkCallOffAuthorities] authority
                            ON authority.Id = i.AuthorityId
                           AND authority.AgreementId = i.AgreementId
                           AND authority.TenantId = i.TenantId
                        LEFT JOIN [WorkflowDefinitions] workflowDefinition
                            ON workflowDefinition.Id = i.WorkflowDefinitionId
                           AND workflowDefinition.TenantId = i.TenantId
                        LEFT JOIN [WorkflowInstances] workflowInstance
                            ON workflowInstance.Id = i.WorkflowInstanceId
                           AND workflowInstance.WorkflowDefinitionId = i.WorkflowDefinitionId
                           AND workflowInstance.EntityId = i.PurchaseOrderId
                           AND workflowInstance.TenantId = i.TenantId
                        OUTER APPLY (
                            SELECT MAX(extension.ProposedEndUtc) AS ApprovedEndUtc
                            FROM [ProcurementFrameworkAgreementExtensions] extension
                            WHERE extension.AgreementId = i.AgreementId
                              AND extension.TenantId = i.TenantId
                              AND extension.Status = 1
                              AND extension.IsDeleted = 0
                        ) extension
                        WHERE agreement.Id IS NULL OR partner.Id IS NULL
                           OR purchaseOrder.Id IS NULL OR requisition.Id IS NULL
                           OR authority.Id IS NULL OR i.IsDeleted <> 0
                           OR i.BusinessPartnerId <> agreement.BusinessPartnerId
                           OR i.AgreementNumber <> agreement.AgreementNumber
                           OR i.AgreementVersion <> agreement.Version
                           OR i.AgreementIntegrityHash <> agreement.IntegrityHash
                           OR i.AwardReadinessIntegrityHash <> agreement.SourceIntegrityHash
                           OR i.PriceListReference <> agreement.PriceListReference
                           OR i.PriceListVersion <> agreement.PriceListVersion
                           OR i.CurrencyCode <> agreement.CurrencyCode
                           OR i.AgreementEffectiveFromUtc <> agreement.EffectiveFromUtc
                           OR (prior.Id IS NULL
                               AND i.AgreementEffectiveEndUtc
                                    <> CASE
                                        WHEN extension.ApprovedEndUtc > agreement.EffectiveToUtc
                                            THEN extension.ApprovedEndUtc
                                        ELSE agreement.EffectiveToUtc
                                       END)
                           OR i.AuthorityKind <> authority.AuthorityKind
                           OR i.AuthorityValue <> authority.AuthorityValue
                           OR ISNULL(i.AuthorityThreshold, -1)
                                <> ISNULL(authority.MaximumCallOffAmount, -1)
                           OR purchaseOrder.OrderType <> 'FrameworkCallOff'
                           OR purchaseOrder.OrderNumber <> i.CallOffNumber
                           OR purchaseOrder.BusinessPartnerId <> i.BusinessPartnerId
                           OR purchaseOrder.SourceRequisitionId IS NULL
                           OR purchaseOrder.SourceRequisitionId <> i.SourceRequisitionId
                           OR purchaseOrder.TotalAmount <> i.TotalAmount
                           OR purchaseOrder.SubTotal <> i.TotalAmount
                           OR purchaseOrder.Currency <> i.CurrencyCode
                           OR purchaseOrder.RequiredDate IS NULL
                           OR purchaseOrder.RequiredDate <> i.RequiredDateUtc
                           OR ISNULL(purchaseOrder.DeliveryWarehouseId,
                                     '00000000-0000-0000-0000-000000000000')
                                <> ISNULL(i.DeliveryWarehouseId,
                                     '00000000-0000-0000-0000-000000000000')
                           OR purchaseOrder.ContractStartDate IS NULL
                           OR purchaseOrder.ContractStartDate <> i.AgreementEffectiveFromUtc
                           OR purchaseOrder.ContractEndDate IS NULL
                           OR purchaseOrder.ContractEndDate <> i.AgreementEffectiveEndUtc
                           OR purchaseOrder.ContractValue IS NULL
                           OR purchaseOrder.ContractValue <> agreement.CeilingAmount
                           OR ((prior.Id IS NULL OR i.Status NOT IN (4, 5))
                               AND requisition.Status <> 'Approved')
                           OR (i.WorkflowDefinitionId IS NULL
                               AND i.WorkflowInstanceId IS NOT NULL)
                           OR (i.WorkflowDefinitionId IS NOT NULL
                               AND workflowDefinition.Id IS NULL)
                           OR (i.WorkflowInstanceId IS NOT NULL
                               AND workflowInstance.Id IS NULL)
                           OR (prior.Id IS NULL
                               AND (agreement.Status <> 2
                                    OR agreement.EffectiveFromUtc > SYSUTCDATETIME()
                                    OR CASE
                                        WHEN extension.ApprovedEndUtc > agreement.EffectiveToUtc
                                            THEN extension.ApprovedEndUtc
                                        ELSE agreement.EffectiveToUtc
                                       END <= SYSUTCDATETIME())))
                        THROW 51102, 'Framework call-off references and server-derived commercial lineage are invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId
                           OR i.AgreementId <> d.AgreementId
                           OR i.PurchaseOrderId <> d.PurchaseOrderId
                           OR i.SourceRequisitionId <> d.SourceRequisitionId
                           OR i.BusinessPartnerId <> d.BusinessPartnerId
                           OR i.AuthorityId <> d.AuthorityId
                           OR i.AuthorityKind <> d.AuthorityKind
                           OR i.AuthorityValue <> d.AuthorityValue
                           OR ISNULL(i.AuthorityThreshold, -1)
                                <> ISNULL(d.AuthorityThreshold, -1)
                           OR i.CallOffNumber <> d.CallOffNumber
                           OR i.CurrencyCode <> d.CurrencyCode
                           OR i.TotalAmount <> d.TotalAmount
                           OR i.RequiredDateUtc <> d.RequiredDateUtc
                           OR ISNULL(i.DeliveryWarehouseId,
                                     '00000000-0000-0000-0000-000000000000')
                                <> ISNULL(d.DeliveryWarehouseId,
                                     '00000000-0000-0000-0000-000000000000')
                           OR ISNULL(i.DeliveryAddress, '') <> ISNULL(d.DeliveryAddress, '')
                           OR ISNULL(i.Notes, '') <> ISNULL(d.Notes, '')
                           OR i.AgreementNumber <> d.AgreementNumber
                           OR i.AgreementVersion <> d.AgreementVersion
                           OR i.AgreementIntegrityHash <> d.AgreementIntegrityHash
                           OR i.AwardReadinessIntegrityHash <> d.AwardReadinessIntegrityHash
                           OR i.SupplierEligibilityDecisionHash
                                <> d.SupplierEligibilityDecisionHash
                           OR i.PriceListReference <> d.PriceListReference
                           OR i.PriceListVersion <> d.PriceListVersion
                           OR i.AgreementEffectiveFromUtc <> d.AgreementEffectiveFromUtc
                           OR i.AgreementEffectiveEndUtc <> d.AgreementEffectiveEndUtc
                           OR i.CreatedByUserId <> d.CreatedByUserId
                           OR i.CreatedByName <> d.CreatedByName
                           OR i.CreationCorrelationId <> d.CreationCorrelationId
                           OR i.IsDeleted <> d.IsDeleted)
                        THROW 51103, 'Framework call-off source, commercial, authority, and tenant lineage are immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.Status <> d.Status
                          AND NOT (
                                (d.Status = 0 AND i.Status IN (1, 2, 5))
                             OR (d.Status = 1 AND i.Status IN (2, 4, 5))
                             OR (d.Status = 2 AND i.Status IN (3, 5))
                          ))
                        THROW 51104, 'Invalid framework call-off lifecycle transition.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        LEFT JOIN [ProcurementFrameworkCallOffAuthorities] authority
                            ON authority.Id = i.AuthorityId
                           AND authority.TenantId = i.TenantId
                        LEFT JOIN [ProcurementFrameworkAgreements] agreement
                            ON agreement.Id = i.AgreementId
                           AND agreement.TenantId = i.TenantId
                        OUTER APPLY (
                            SELECT MAX(extension.ProposedEndUtc) AS ApprovedEndUtc
                            FROM [ProcurementFrameworkAgreementExtensions] extension
                            WHERE extension.AgreementId = i.AgreementId
                              AND extension.TenantId = i.TenantId
                              AND extension.Status = 1
                              AND extension.IsDeleted = 0
                        ) extension
                        WHERE i.Status IN (1, 2, 3)
                          AND i.Status <> d.Status
                          AND (
                               agreement.Status <> 2
                            OR agreement.EffectiveFromUtc > SYSUTCDATETIME()
                            OR CASE
                                WHEN extension.ApprovedEndUtc > agreement.EffectiveToUtc
                                    THEN extension.ApprovedEndUtc
                                ELSE agreement.EffectiveToUtc
                               END <= SYSUTCDATETIME()
                            OR authority.IsActive <> 1
                            OR authority.ValidFromUtc > SYSUTCDATETIME()
                            OR CASE
                                WHEN authority.ValidToUtc = agreement.EffectiveToUtc
                                    AND extension.ApprovedEndUtc > agreement.EffectiveToUtc
                                    THEN extension.ApprovedEndUtc
                                ELSE authority.ValidToUtc
                               END <= SYSUTCDATETIME()
                            OR (authority.MaximumCallOffAmount IS NOT NULL
                                AND i.TotalAmount > authority.MaximumCallOffAmount)
                          ))
                        THROW 51105, 'An effective published agreement and current call-off authority are required for submission, approval, and issue.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [WorkflowInstances] workflowInstance
                            ON workflowInstance.Id = i.WorkflowInstanceId
                           AND workflowInstance.TenantId = i.TenantId
                        WHERE (i.Status = 1
                               AND (workflowInstance.Id IS NULL
                                    OR workflowInstance.Status NOT IN (0, 1, 5, 6)))
                           OR (i.Status IN (2, 3)
                               AND (workflowInstance.Id IS NULL
                                    OR workflowInstance.Status <> 2))
                           OR (i.Status = 4
                               AND (workflowInstance.Id IS NULL
                                    OR workflowInstance.Status NOT IN (3, 4))))
                        THROW 51106, 'The call-off outcome must agree with the shared purchase-order workflow.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status = 0 AND i.Status <> 0
                          AND (
                               NOT EXISTS (
                                   SELECT 1
                                   FROM [ProcurementFrameworkCallOffLines] line
                                   WHERE line.CallOffId = i.Id
                                     AND line.TenantId = i.TenantId
                                     AND line.IsDeleted = 0)
                            OR (SELECT COUNT_BIG(*)
                                FROM [ProcurementFrameworkCallOffLines] line
                                WHERE line.CallOffId = i.Id
                                  AND line.TenantId = i.TenantId
                                  AND line.IsDeleted = 0)
                               <> (SELECT COUNT_BIG(*)
                                   FROM [PurchaseOrderItems] purchaseOrderItem
                                   WHERE purchaseOrderItem.PurchaseOrderId = i.PurchaseOrderId
                                     AND purchaseOrderItem.TenantId = i.TenantId
                                     AND purchaseOrderItem.IsDeleted = 0)
                            OR (SELECT SUM(line.LineTotal)
                                FROM [ProcurementFrameworkCallOffLines] line
                                WHERE line.CallOffId = i.Id
                                  AND line.TenantId = i.TenantId
                                  AND line.IsDeleted = 0) <> i.TotalAmount
                            OR EXISTS (
                                SELECT 1
                                FROM [PurchaseOrderItems] purchaseOrderItem
                                LEFT JOIN [ProcurementFrameworkCallOffLines] line
                                    ON line.PurchaseOrderItemId = purchaseOrderItem.Id
                                   AND line.CallOffId = i.Id
                                   AND line.TenantId = i.TenantId
                                   AND line.IsDeleted = 0
                                WHERE purchaseOrderItem.PurchaseOrderId = i.PurchaseOrderId
                                  AND purchaseOrderItem.TenantId = i.TenantId
                                  AND purchaseOrderItem.IsDeleted = 0
                                  AND line.Id IS NULL)
                          ))
                        THROW 51107, 'A complete server-derived demand, price, and purchase-order line set is required before submission.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.BalanceDeductedAtUtc IS NOT NULL
                          AND NOT EXISTS (
                              SELECT 1
                              FROM [ProcurementFrameworkBalanceMovements] movement
                              WHERE movement.CallOffId = i.Id
                                AND movement.TenantId = i.TenantId
                                AND movement.MovementType = 0
                                AND movement.Amount = i.TotalAmount
                                AND movement.IsDeleted = 0))
                        THROW 51108, 'Approved call-offs require an immutable commitment movement.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.Status = 3
                          AND NOT EXISTS (
                              SELECT 1
                              FROM [ProcurementFrameworkBalanceMovements] movement
                              WHERE movement.CallOffId = i.Id
                                AND movement.TenantId = i.TenantId
                                AND movement.MovementType = 2
                                AND movement.Amount = i.TotalAmount
                                AND movement.IsDeleted = 0))
                        THROW 51109, 'Issued call-offs require an immutable issue movement.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status = 2 AND i.Status = 5
                          AND NOT EXISTS (
                              SELECT 1
                              FROM [ProcurementFrameworkBalanceMovements] movement
                              WHERE movement.CallOffId = i.Id
                                AND movement.TenantId = i.TenantId
                                AND movement.MovementType = 1
                                AND movement.Amount = i.TotalAmount
                                AND movement.IsDeleted = 0))
                        THROW 51110, 'Cancelling an approved call-off requires an immutable release movement.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [TR_ProcurementFrameworkCallOffLines_Protected]
                ON [ProcurementFrameworkCallOffLines]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51111, 'Framework call-off lines are immutable and cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [ProcurementFrameworkCallOffs] callOff
                            ON callOff.Id = i.CallOffId
                           AND callOff.TenantId = i.TenantId
                        LEFT JOIN [ProcurementFrameworkPriceListLines] priceLine
                            ON priceLine.Id = i.AgreementPriceLineId
                           AND priceLine.AgreementId = callOff.AgreementId
                           AND priceLine.TenantId = i.TenantId
                           AND priceLine.IsDeleted = 0
                        LEFT JOIN [PurchaseRequisitionItems] demandLine
                            ON demandLine.Id = i.PurchaseRequisitionItemId
                           AND demandLine.RequisitionId = callOff.SourceRequisitionId
                           AND demandLine.TenantId = i.TenantId
                           AND demandLine.IsDeleted = 0
                        LEFT JOIN [PurchaseOrderItems] purchaseOrderItem
                            ON purchaseOrderItem.Id = i.PurchaseOrderItemId
                           AND purchaseOrderItem.PurchaseOrderId = callOff.PurchaseOrderId
                           AND purchaseOrderItem.TenantId = i.TenantId
                           AND purchaseOrderItem.IsDeleted = 0
                        WHERE callOff.Id IS NULL OR callOff.Status <> 0 OR i.IsDeleted <> 0
                           OR priceLine.Id IS NULL OR demandLine.Id IS NULL
                           OR purchaseOrderItem.Id IS NULL
                           OR demandLine.InventoryItemId IS NULL
                           OR purchaseOrderItem.InventoryItemId IS NULL
                           OR i.InventoryItemId <> priceLine.InventoryItemId
                           OR i.InventoryItemId <> purchaseOrderItem.InventoryItemId
                           OR i.ItemCode <> priceLine.ItemCode
                           OR i.ItemName <> priceLine.ItemName
                           OR i.UnitOfMeasure <> priceLine.UnitOfMeasure
                           OR i.UnitOfMeasure <> purchaseOrderItem.UnitOfMeasure
                           OR i.Quantity <> purchaseOrderItem.OrderedQuantity
                           OR i.Quantity > demandLine.Quantity
                           OR i.UnitPrice <> priceLine.UnitPrice
                           OR i.UnitPrice <> purchaseOrderItem.UnitPrice
                           OR i.LineTotal <> ROUND(i.Quantity * i.UnitPrice, 2)
                           OR i.LineTotal <> purchaseOrderItem.LineTotal
                           OR i.SourceDemandQuantity <> demandLine.Quantity
                           OR i.PriceIntegrityHash <> priceLine.IntegrityHash)
                        THROW 51112, 'Call-off lines must use exact same-tenant framework prices, approved demand, and purchase-order lines.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted affected
                        CROSS APPLY (
                            SELECT SUM(line.Quantity) AS AllocatedQuantity
                            FROM [ProcurementFrameworkCallOffLines] line WITH (UPDLOCK, HOLDLOCK)
                            JOIN [ProcurementFrameworkCallOffs] callOff
                                ON callOff.Id = line.CallOffId
                               AND callOff.TenantId = line.TenantId
                            WHERE line.PurchaseRequisitionItemId
                                    = affected.PurchaseRequisitionItemId
                              AND line.TenantId = affected.TenantId
                              AND line.IsDeleted = 0
                              AND callOff.IsDeleted = 0
                              AND callOff.Status NOT IN (4, 5)
                        ) allocation
                        JOIN [PurchaseRequisitionItems] demandLine
                            ON demandLine.Id = affected.PurchaseRequisitionItemId
                           AND demandLine.TenantId = affected.TenantId
                        WHERE allocation.AllocatedQuantity > demandLine.Quantity)
                        THROW 51113, 'Framework call-offs cannot allocate more than the approved requisition demand.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [TR_ProcurementFrameworkBalanceMovements_AppendOnly]
                ON [ProcurementFrameworkBalanceMovements]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51121, 'Framework balance movements are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [ProcurementFrameworkAgreementBalances] balance
                            ON balance.Id = i.AgreementBalanceId
                           AND balance.AgreementId = i.AgreementId
                           AND balance.TenantId = i.TenantId
                           AND balance.IsDeleted = 0
                        LEFT JOIN [ProcurementFrameworkCallOffs] callOff
                            ON callOff.Id = i.CallOffId
                           AND callOff.AgreementId = i.AgreementId
                           AND callOff.TenantId = i.TenantId
                           AND callOff.IsDeleted = 0
                        WHERE balance.Id IS NULL OR callOff.Id IS NULL
                           OR i.IsDeleted <> 0 OR i.Amount <> callOff.TotalAmount
                           OR i.BalanceBefore <> balance.AvailableAmount
                           OR (i.MovementType = 0
                               AND (callOff.Status NOT IN (0, 1)
                                    OR i.BalanceAfter
                                       <> i.BalanceBefore - i.Amount))
                           OR (i.MovementType = 1
                               AND (callOff.Status <> 2
                                    OR i.BalanceAfter
                                       <> i.BalanceBefore + i.Amount))
                           OR (i.MovementType = 2
                               AND (callOff.Status <> 2
                                    OR i.BalanceAfter <> i.BalanceBefore)))
                        THROW 51122, 'Framework balance movement lineage, transition, or arithmetic is invalid.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [TR_ProcurementFrameworkAgreementBalances_Protected]
                ON [ProcurementFrameworkAgreementBalances]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1 FROM deleted d
                        LEFT JOIN inserted i ON i.Id = d.Id
                        WHERE i.Id IS NULL)
                        THROW 51131, 'Framework agreement balances cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.Id = i.Id
                        LEFT JOIN [ProcurementFrameworkAgreements] agreement
                            ON agreement.Id = i.AgreementId
                           AND agreement.TenantId = i.TenantId
                        OUTER APPLY (
                            SELECT
                                COALESCE(SUM(CASE
                                    WHEN movement.MovementType = 0 THEN movement.Amount
                                    WHEN movement.MovementType = 1 THEN -movement.Amount
                                    ELSE 0 END), 0) AS LedgerCommitted,
                                COALESCE(SUM(CASE
                                    WHEN movement.MovementType = 2 THEN movement.Amount
                                    ELSE 0 END), 0) AS LedgerIssued
                            FROM [ProcurementFrameworkBalanceMovements] movement
                            WHERE movement.AgreementBalanceId = i.Id
                              AND movement.TenantId = i.TenantId
                              AND movement.IsDeleted = 0
                        ) ledger
                        WHERE agreement.Id IS NULL OR i.IsDeleted <> 0
                           OR i.CurrencyCode <> agreement.CurrencyCode
                           OR i.CeilingAmount <> agreement.CeilingAmount
                           OR i.CommittedAmount <> ledger.LedgerCommitted
                           OR i.IssuedAmount <> ledger.LedgerIssued
                           OR i.AvailableAmount
                                <> i.CeilingAmount - ledger.LedgerCommitted
                           OR (d.Id IS NOT NULL
                               AND (i.TenantId <> d.TenantId
                                    OR i.AgreementId <> d.AgreementId
                                    OR i.CurrencyCode <> d.CurrencyCode
                                    OR i.CeilingAmount <> d.CeilingAmount))
                           OR (i.LastMovementId IS NOT NULL
                               AND NOT EXISTS (
                                   SELECT 1
                                   FROM [ProcurementFrameworkBalanceMovements] movement
                                   WHERE movement.Id = i.LastMovementId
                                     AND movement.AgreementBalanceId = i.Id
                                     AND movement.TenantId = i.TenantId
                                     AND movement.IsDeleted = 0)))
                        THROW 51132, 'Framework agreement balances must reconcile to the immutable movement ledger and agreement ceiling.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [TR_PurchaseOrders_FrameworkCallOffProtected]
                ON [PurchaseOrders]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM deleted d
                        LEFT JOIN inserted i ON i.Id = d.Id
                        JOIN [ProcurementFrameworkCallOffs] callOff
                            ON callOff.PurchaseOrderId = d.Id
                           AND callOff.TenantId = d.TenantId
                        WHERE i.Id IS NULL)
                        THROW 51141, 'Purchase orders linked to framework call-offs cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        JOIN [ProcurementFrameworkCallOffs] callOff
                            ON callOff.PurchaseOrderId = i.Id
                           AND callOff.TenantId = i.TenantId
                        WHERE i.TenantId <> d.TenantId
                           OR i.IsDeleted <> d.IsDeleted
                           OR i.OrderNumber <> d.OrderNumber
                           OR i.OrderType <> d.OrderType
                           OR i.BusinessPartnerId <> d.BusinessPartnerId
                           OR ISNULL(i.SourceRequisitionId,
                                     '00000000-0000-0000-0000-000000000000')
                                <> ISNULL(d.SourceRequisitionId,
                                     '00000000-0000-0000-0000-000000000000')
                           OR i.SubTotal <> d.SubTotal
                           OR i.TaxAmount <> d.TaxAmount
                           OR i.ShippingCost <> d.ShippingCost
                           OR i.MiscellaneousCost <> d.MiscellaneousCost
                           OR i.TotalAdditionalCost <> d.TotalAdditionalCost
                           OR i.DiscountAmount <> d.DiscountAmount
                           OR i.TotalAmount <> d.TotalAmount
                           OR i.Currency <> d.Currency
                           OR i.ExchangeRate <> d.ExchangeRate
                           OR ISNULL(i.RequiredDate, '19000101')
                                <> ISNULL(d.RequiredDate, '19000101')
                           OR ISNULL(i.ContractStartDate, '19000101')
                                <> ISNULL(d.ContractStartDate, '19000101')
                           OR ISNULL(i.ContractEndDate, '19000101')
                                <> ISNULL(d.ContractEndDate, '19000101')
                           OR ISNULL(i.ContractValue, -1)
                                <> ISNULL(d.ContractValue, -1)
                           OR ISNULL(i.DeliveryWarehouseId,
                                     '00000000-0000-0000-0000-000000000000')
                                <> ISNULL(d.DeliveryWarehouseId,
                                     '00000000-0000-0000-0000-000000000000')
                           OR ISNULL(i.DeliveryAddress, '')
                                <> ISNULL(d.DeliveryAddress, ''))
                        THROW 51142, 'Framework call-off purchase-order commercial and source fields are immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [ProcurementFrameworkCallOffs] callOff
                            ON callOff.PurchaseOrderId = i.Id
                           AND callOff.TenantId = i.TenantId
                        WHERE i.OrderType <> 'FrameworkCallOff'
                           OR i.IsDeleted <> 0
                           OR (callOff.Status = 0 AND i.Status <> 'Draft')
                           OR (callOff.Status = 1 AND i.Status <> 'Pending Approval')
                           OR (callOff.Status = 2 AND i.Status <> 'Approved')
                           OR (callOff.Status = 3 AND i.Status <> 'Sent')
                           OR (callOff.Status = 4 AND i.Status <> 'Rejected')
                           OR (callOff.Status = 5 AND i.Status <> 'Cancelled'))
                        THROW 51143, 'The linked purchase-order status must agree with the framework call-off lifecycle.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [TR_PurchaseOrderItems_FrameworkCallOffProtected]
                ON [PurchaseOrderItems]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM deleted affected
                        JOIN [ProcurementFrameworkCallOffs] callOff
                            ON callOff.PurchaseOrderId = affected.PurchaseOrderId
                           AND callOff.TenantId = affected.TenantId)
                        THROW 51151, 'Existing framework call-off purchase-order lines are immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted affected
                        JOIN [ProcurementFrameworkCallOffs] callOff
                            ON callOff.PurchaseOrderId = affected.PurchaseOrderId
                           AND callOff.TenantId = affected.TenantId
                        WHERE callOff.Status <> 0)
                        THROW 51152, 'New purchase-order lines cannot be added after a framework call-off leaves Draft.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS [TR_PurchaseOrderItems_FrameworkCallOffProtected];
                DROP TRIGGER IF EXISTS [TR_PurchaseOrders_FrameworkCallOffProtected];
                DROP TRIGGER IF EXISTS [TR_ProcurementFrameworkAgreementBalances_Protected];
                DROP TRIGGER IF EXISTS [TR_ProcurementFrameworkBalanceMovements_AppendOnly];
                DROP TRIGGER IF EXISTS [TR_ProcurementFrameworkCallOffLines_Protected];
                DROP TRIGGER IF EXISTS [TR_ProcurementFrameworkCallOffs_Lifecycle];
                """);

            migrationBuilder.DropTable(
                name: "ProcurementFrameworkBalanceMovements");

            migrationBuilder.DropTable(
                name: "ProcurementFrameworkCallOffLines");

            migrationBuilder.DropTable(
                name: "ProcurementFrameworkAgreementBalances");

            migrationBuilder.DropTable(
                name: "ProcurementFrameworkCallOffs");
        }
    }
}
