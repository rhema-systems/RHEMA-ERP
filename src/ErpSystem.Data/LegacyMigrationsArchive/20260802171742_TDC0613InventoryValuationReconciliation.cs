using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace ErpSystem.Data.Migrations;

public partial class TDC0613InventoryValuationReconciliation : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable("InventoryValuationReconciliations", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			int? num = 50;
			OperationBuilder<AddColumnOperation> reconciliationNumber = table.Column<string>("nvarchar(50)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> fiscalPeriodId = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> cutoffDateUtc = table.Column<DateTime>("datetime2", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> status = table.Column<int>("int", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 3;
			OperationBuilder<AddColumnOperation> functionalCurrencyCode = table.Column<string>("nvarchar(3)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> inventoryControlAccountId = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> receiptInventoryValue = table.Column<decimal>("decimal(18,4)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> postedLandedCostValue = table.Column<decimal>("decimal(18,2)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> landedCostInventoryValue = table.Column<decimal>("decimal(18,2)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> landedCostVarianceValue = table.Column<decimal>("decimal(18,2)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> inventorySubledgerValue = table.Column<decimal>("decimal(18,4)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> inventoryBalanceCacheValue = table.Column<decimal>("decimal(18,4)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> currentMovementValue = table.Column<decimal>("decimal(18,4)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> generalLedgerValue = table.Column<decimal>("decimal(18,4)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> reconciliationVariance = table.Column<decimal>("decimal(18,4)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> toleranceAmount = table.Column<decimal>("decimal(18,2)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> receiptExceptionCount = table.Column<int>("int", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> landedCostExceptionCount = table.Column<int>("int", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> valuationExceptionCount = table.Column<int>("int", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> generalLedgerExceptionCount = table.Column<int>("int", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> exceptionCount = table.Column<int>("int", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> snapshotJson = table.Column<string>("nvarchar(max)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> exceptionsJson = table.Column<string>("nvarchar(max)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 64;
			OperationBuilder<AddColumnOperation> snapshotHash = table.Column<string>("nvarchar(64)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> generatedById = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> generatedAtUtc = table.Column<DateTime>("datetime2", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> frozenById = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> frozenAtUtc = table.Column<DateTime>("datetime2", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> periodModuleLockId = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 100;
			OperationBuilder<AddColumnOperation> idempotencyKey = table.Column<string>("nvarchar(100)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 64;
			OperationBuilder<AddColumnOperation> payloadHash = table.Column<string>("nvarchar(64)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 100;
			return new
			{
				Id = id,
				ReconciliationNumber = reconciliationNumber,
				FiscalPeriodId = fiscalPeriodId,
				CutoffDateUtc = cutoffDateUtc,
				Status = status,
				FunctionalCurrencyCode = functionalCurrencyCode,
				InventoryControlAccountId = inventoryControlAccountId,
				ReceiptInventoryValue = receiptInventoryValue,
				PostedLandedCostValue = postedLandedCostValue,
				LandedCostInventoryValue = landedCostInventoryValue,
				LandedCostVarianceValue = landedCostVarianceValue,
				InventorySubledgerValue = inventorySubledgerValue,
				InventoryBalanceCacheValue = inventoryBalanceCacheValue,
				CurrentMovementValue = currentMovementValue,
				GeneralLedgerValue = generalLedgerValue,
				ReconciliationVariance = reconciliationVariance,
				ToleranceAmount = toleranceAmount,
				ReceiptExceptionCount = receiptExceptionCount,
				LandedCostExceptionCount = landedCostExceptionCount,
				ValuationExceptionCount = valuationExceptionCount,
				GeneralLedgerExceptionCount = generalLedgerExceptionCount,
				ExceptionCount = exceptionCount,
				SnapshotJson = snapshotJson,
				ExceptionsJson = exceptionsJson,
				SnapshotHash = snapshotHash,
				GeneratedById = generatedById,
				GeneratedAtUtc = generatedAtUtc,
				FrozenById = frozenById,
				FrozenAtUtc = frozenAtUtc,
				PeriodModuleLockId = periodModuleLockId,
				IdempotencyKey = idempotencyKey,
				PayloadHash = payloadHash,
				CorrelationId = table.Column<string>("nvarchar(100)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				RowVersion = table.Column<byte[]>("rowversion", (bool?)null, (int?)null, true, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				CreatedAt = table.Column<DateTime>("datetime2", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				UpdatedAt = table.Column<DateTime>("datetime2", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				CreatedBy = table.Column<string>("nvarchar(max)", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				UpdatedBy = table.Column<string>("nvarchar(max)", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				CreatedById = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				LastModifiedById = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				IsDeleted = table.Column<bool>("bit", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				DeletedAt = table.Column<DateTime>("datetime2", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				DeletedBy = table.Column<string>("nvarchar(max)", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				TenantId = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null)
			};
		}, (string)null, table =>
		{
			table.PrimaryKey("PK_InventoryValuationReconciliations", x => (object)x.Id);
			table.CheckConstraint("CK_InventoryValuationReconciliations_ExceptionCounts", "[ReceiptExceptionCount] >= 0 AND [LandedCostExceptionCount] >= 0 AND [ValuationExceptionCount] >= 0 AND [GeneralLedgerExceptionCount] >= 0 AND [ExceptionCount] = [ReceiptExceptionCount] + [LandedCostExceptionCount] + [ValuationExceptionCount] + [GeneralLedgerExceptionCount]");
			table.CheckConstraint("CK_InventoryValuationReconciliations_FrozenLifecycle", "([Status] = 2 AND [FrozenById] IS NOT NULL AND [FrozenAtUtc] IS NOT NULL AND [PeriodModuleLockId] IS NOT NULL AND [FrozenById] <> [GeneratedById]) OR ([Status] <> 2 AND [FrozenById] IS NULL AND [FrozenAtUtc] IS NULL AND [PeriodModuleLockId] IS NULL)");
			table.CheckConstraint("CK_InventoryValuationReconciliations_Hashes", "LEN([SnapshotHash]) = 64 AND LEN([PayloadHash]) = 64 AND LEN([IdempotencyKey]) > 0 AND LEN([CorrelationId]) > 0");
			table.CheckConstraint("CK_InventoryValuationReconciliations_Reconciled", "[Status] = 0 OR ([ExceptionCount] = 0 AND ABS([ReconciliationVariance]) <= [ToleranceAmount])");
			table.CheckConstraint("CK_InventoryValuationReconciliations_Status", "[Status] BETWEEN 0 AND 2");
			table.CheckConstraint("CK_InventoryValuationReconciliations_Tolerance", "[ToleranceAmount] >= 0");
			table.ForeignKey("FK_InventoryValuationReconciliations_Accounts_InventoryControlAccountId", x => (object)x.InventoryControlAccountId, "Accounts", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
			table.ForeignKey("FK_InventoryValuationReconciliations_FiscalPeriods_FiscalPeriodId", x => (object)x.FiscalPeriodId, "FiscalPeriods", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
			table.ForeignKey("FK_InventoryValuationReconciliations_PeriodModuleLock_PeriodModuleLockId", x => (object)x.PeriodModuleLockId, "PeriodModuleLock", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
			table.ForeignKey("FK_InventoryValuationReconciliations_Tenants_TenantId", x => (object)x.TenantId, "Tenants", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
		}, (string)null);
		migrationBuilder.CreateTable("InventoryValuationReconciliationActions", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> reconciliationId = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> sequence = table.Column<int>("int", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> actionType = table.Column<int>("int", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> previousStatus = table.Column<int>("int", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> newStatus = table.Column<int>("int", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> actorUserId = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> occurredAtUtc = table.Column<DateTime>("datetime2", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			int? num = 100;
			OperationBuilder<AddColumnOperation> idempotencyKey = table.Column<string>("nvarchar(100)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 64;
			OperationBuilder<AddColumnOperation> payloadHash = table.Column<string>("nvarchar(64)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 100;
			OperationBuilder<AddColumnOperation> correlationId = table.Column<string>("nvarchar(100)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 1000;
			OperationBuilder<AddColumnOperation> reason = table.Column<string>("nvarchar(1000)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 64;
			OperationBuilder<AddColumnOperation> previousHash = table.Column<string>("nvarchar(64)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 64;
			return new
			{
				Id = id,
				ReconciliationId = reconciliationId,
				Sequence = sequence,
				ActionType = actionType,
				PreviousStatus = previousStatus,
				NewStatus = newStatus,
				ActorUserId = actorUserId,
				OccurredAtUtc = occurredAtUtc,
				IdempotencyKey = idempotencyKey,
				PayloadHash = payloadHash,
				CorrelationId = correlationId,
				Reason = reason,
				PreviousHash = previousHash,
				IntegrityHash = table.Column<string>("nvarchar(64)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				CreatedAt = table.Column<DateTime>("datetime2", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				UpdatedAt = table.Column<DateTime>("datetime2", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				CreatedBy = table.Column<string>("nvarchar(max)", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				UpdatedBy = table.Column<string>("nvarchar(max)", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				CreatedById = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				LastModifiedById = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				IsDeleted = table.Column<bool>("bit", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				DeletedAt = table.Column<DateTime>("datetime2", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				DeletedBy = table.Column<string>("nvarchar(max)", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				TenantId = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null)
			};
		}, (string)null, table =>
		{
			table.PrimaryKey("PK_InventoryValuationReconciliationActions", x => (object)x.Id);
			table.CheckConstraint("CK_InventoryValuationReconciliationActions_Hashes", "LEN([PayloadHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ([PreviousHash] IS NULL OR LEN([PreviousHash]) = 64)");
			table.CheckConstraint("CK_InventoryValuationReconciliationActions_Sequence", "[Sequence] > 0");
			table.ForeignKey("FK_InventoryValuationReconciliationActions_InventoryValuationReconciliations_ReconciliationId", x => (object)x.ReconciliationId, "InventoryValuationReconciliations", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
			table.ForeignKey("FK_InventoryValuationReconciliationActions_Tenants_TenantId", x => (object)x.TenantId, "Tenants", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
		}, (string)null);
		migrationBuilder.CreateIndex("IX_InventoryValuationReconciliationActions_ReconciliationId", "InventoryValuationReconciliationActions", "ReconciliationId", (string)null, false, (string)null, (bool[])null);
		migrationBuilder.CreateIndex("IX_InventoryValuationReconciliationActions_TenantId_ReconciliationId_IdempotencyKey", "InventoryValuationReconciliationActions", new string[3] { "TenantId", "ReconciliationId", "IdempotencyKey" }, (string)null, true, (string)null, (bool[])null);
		migrationBuilder.CreateIndex("IX_InventoryValuationReconciliationActions_TenantId_ReconciliationId_Sequence", "InventoryValuationReconciliationActions", new string[3] { "TenantId", "ReconciliationId", "Sequence" }, (string)null, true, (string)null, (bool[])null);
		migrationBuilder.CreateIndex("IX_InventoryValuationReconciliations_FiscalPeriodId", "InventoryValuationReconciliations", "FiscalPeriodId", (string)null, false, (string)null, (bool[])null);
		migrationBuilder.CreateIndex("IX_InventoryValuationReconciliations_InventoryControlAccountId", "InventoryValuationReconciliations", "InventoryControlAccountId", (string)null, false, (string)null, (bool[])null);
		migrationBuilder.CreateIndex("IX_InventoryValuationReconciliations_PeriodModuleLockId", "InventoryValuationReconciliations", "PeriodModuleLockId", (string)null, false, (string)null, (bool[])null);
		migrationBuilder.CreateIndex("IX_InventoryValuationReconciliations_TenantId_FiscalPeriodId_GeneratedAtUtc", "InventoryValuationReconciliations", new string[3] { "TenantId", "FiscalPeriodId", "GeneratedAtUtc" }, (string)null, false, (string)null, (bool[])null);
		migrationBuilder.CreateIndex("IX_InventoryValuationReconciliations_TenantId_FiscalPeriodId_IdempotencyKey", "InventoryValuationReconciliations", new string[3] { "TenantId", "FiscalPeriodId", "IdempotencyKey" }, (string)null, true, (string)null, (bool[])null);
		migrationBuilder.CreateIndex("IX_InventoryValuationReconciliations_TenantId_ReconciliationNumber", "InventoryValuationReconciliations", new string[2] { "TenantId", "ReconciliationNumber" }, (string)null, true, (string)null, (bool[])null);
		migrationBuilder.Sql("CREATE OR ALTER TRIGGER [dbo].[TR_InventoryValuationReconciliationActions_Immutable]\nON [dbo].[InventoryValuationReconciliationActions]\nAFTER UPDATE, DELETE\nAS\nBEGIN\n    SET NOCOUNT ON;\n    THROW 51090, 'INV_VALUATION_RECONCILIATION_ACTION_IMMUTABLE', 1;\nEND;", false);
		migrationBuilder.Sql("CREATE OR ALTER TRIGGER [dbo].[TR_InventoryValuationReconciliations_Guard]\nON [dbo].[InventoryValuationReconciliations]\nAFTER UPDATE, DELETE\nAS\nBEGIN\n    SET NOCOUNT ON;\n\n    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)\n        THROW 51091, 'INV_VALUATION_RECONCILIATION_DELETE_PROHIBITED', 1;\n\n    IF EXISTS (\n        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id\n        WHERE i.TenantId <> d.TenantId\n           OR i.ReconciliationNumber <> d.ReconciliationNumber\n           OR i.FiscalPeriodId <> d.FiscalPeriodId\n           OR i.CutoffDateUtc <> d.CutoffDateUtc\n           OR i.FunctionalCurrencyCode <> d.FunctionalCurrencyCode\n           OR i.InventoryControlAccountId <> d.InventoryControlAccountId\n           OR i.ReceiptInventoryValue <> d.ReceiptInventoryValue\n           OR i.PostedLandedCostValue <> d.PostedLandedCostValue\n           OR i.LandedCostInventoryValue <> d.LandedCostInventoryValue\n           OR i.LandedCostVarianceValue <> d.LandedCostVarianceValue\n           OR i.InventorySubledgerValue <> d.InventorySubledgerValue\n           OR i.InventoryBalanceCacheValue <> d.InventoryBalanceCacheValue\n           OR i.CurrentMovementValue <> d.CurrentMovementValue\n           OR i.GeneralLedgerValue <> d.GeneralLedgerValue\n           OR i.ReconciliationVariance <> d.ReconciliationVariance\n           OR i.ToleranceAmount <> d.ToleranceAmount\n           OR i.ReceiptExceptionCount <> d.ReceiptExceptionCount\n           OR i.LandedCostExceptionCount <> d.LandedCostExceptionCount\n           OR i.ValuationExceptionCount <> d.ValuationExceptionCount\n           OR i.GeneralLedgerExceptionCount <> d.GeneralLedgerExceptionCount\n           OR i.ExceptionCount <> d.ExceptionCount\n           OR i.SnapshotJson <> d.SnapshotJson\n           OR i.ExceptionsJson <> d.ExceptionsJson\n           OR i.SnapshotHash <> d.SnapshotHash\n           OR i.GeneratedById <> d.GeneratedById\n           OR i.GeneratedAtUtc <> d.GeneratedAtUtc\n           OR i.IdempotencyKey <> d.IdempotencyKey\n           OR i.PayloadHash <> d.PayloadHash\n           OR i.CorrelationId <> d.CorrelationId\n           OR i.IsDeleted <> d.IsDeleted)\n        THROW 51092, 'INV_VALUATION_RECONCILIATION_SNAPSHOT_IMMUTABLE', 1;\n\n    IF EXISTS (\n        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id\n        WHERE d.Status = 2\n           OR i.Status <> d.Status AND NOT (d.Status = 1 AND i.Status = 2))\n        THROW 51093, 'INV_VALUATION_RECONCILIATION_TRANSITION_INVALID', 1;\n\n    IF EXISTS (\n        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id\n        WHERE d.Status = 1 AND i.Status = 2\n          AND (i.FrozenById IS NULL OR i.FrozenById = i.GeneratedById))\n        THROW 51094, 'INV_VALUATION_RECONCILIATION_INDEPENDENT_FREEZE_REQUIRED', 1;\n\n    IF EXISTS (\n        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id\n        WHERE d.Status = 1 AND i.Status = 2\n          AND (i.ExceptionCount <> 0 OR ABS(i.ReconciliationVariance) > i.ToleranceAmount\n               OR i.FrozenAtUtc IS NULL OR i.PeriodModuleLockId IS NULL))\n        THROW 51095, 'INV_VALUATION_RECONCILIATION_FREEZE_INVALID', 1;\nEND;", false);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryValuationReconciliationActions_Immutable];", false);
		migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryValuationReconciliations_Guard];", false);
		migrationBuilder.DropTable("InventoryValuationReconciliationActions", (string)null);
		migrationBuilder.DropTable("InventoryValuationReconciliations", (string)null);
	}
}
