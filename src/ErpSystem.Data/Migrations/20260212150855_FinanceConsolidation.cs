using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinanceConsolidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartners_BusinessPartners_ParentId",
                table: "BusinessPartners");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartners_Users_SalesRepresentativeId",
                table: "BusinessPartners");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartners_Users_UserId",
                table: "BusinessPartners");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_InventoryCategories_SubCategoryId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_InventoryItems_SubstituteItem1Id",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_InventoryItems_SubstituteItem2Id",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_InventoryItems_SubstituteItem3Id",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_InventoryItems_SubstituteItem4Id",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_ItemClasses_ItemClassId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_PriceGroups_PriceGroupId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_UnitOfMeasureSchedules_UnitOfMeasureScheduleId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_UnitsOfMeasure_BaseUnitOfMeasureEntityId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_Warehouses_DefaultWarehouseId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderItems_InventoryItems_InventoryItemId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderItems_ItemUnitsOfMeasure_ItemUnitOfMeasureId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderItems_Warehouses_WarehouseId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_BusinessPartners_BusinessPartnerId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_PurchaseRequisitions_SourceRequisitionId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_TenderAwards_TenderAwardId1",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Users_LastAmendedById",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitionItems_BusinessPartners_PreferredBusinessPartnerId",
                table: "PurchaseRequisitionItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_Users_CurrentApproverId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_Users_LastAmendedById",
                table: "PurchaseRequisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_QualityControlChecklists_MaintenanceAssetCategories_AssetCategoryId",
                table: "QualityControlChecklists");

            migrationBuilder.DropForeignKey(
                name: "FK_QualityControlChecklists_MaintenanceTypes_MaintenanceTypeId",
                table: "QualityControlChecklists");

            migrationBuilder.DropForeignKey(
                name: "FK_QualityControlChecklists_WorkOrderTypes_WorkOrderTypeId",
                table: "QualityControlChecklists");

            migrationBuilder.DropForeignKey(
                name: "FK_StockAdjustments_Warehouses_WarehouseId",
                table: "StockAdjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Warehouses_WarehouseId",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseLocations_InventoryItems_DedicatedItemId",
                table: "WarehouseLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_Users_ManagerId",
                table: "Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_WarehouseLocations_DefaultInTransitLocationId",
                table: "Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_WarehouseLocations_DefaultQuarantineLocationId",
                table: "Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_WarehouseLocations_DefaultReceivingLocationId",
                table: "Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_WarehouseLocations_DefaultShippingLocationId",
                table: "Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Users_ApprovedById",
                table: "WorkOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Users_CompletedById",
                table: "WorkOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Users_QualityCheckedById",
                table: "WorkOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Users_RequestedById",
                table: "WorkOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Users_SupervisorId",
                table: "WorkOrders");

            migrationBuilder.DropTable(
                name: "AssetConditionItemResults");

            migrationBuilder.DropTable(
                name: "BlacklistHistories");

            migrationBuilder.DropTable(
                name: "BusinessPartnerUsers");

            migrationBuilder.DropTable(
                name: "ContractAmendments");

            migrationBuilder.DropTable(
                name: "ContractDocuments");

            migrationBuilder.DropTable(
                name: "ContractMilestones");

            migrationBuilder.DropTable(
                name: "CustomerGroups");

            migrationBuilder.DropTable(
                name: "DistributedLocks");

            migrationBuilder.DropTable(
                name: "EmailCampaignRecipients");

            migrationBuilder.DropTable(
                name: "EmergencyProcurementItems");

            migrationBuilder.DropTable(
                name: "EmergencySuppliers");

            migrationBuilder.DropTable(
                name: "EvaluationTemplateCriteria");

            migrationBuilder.DropTable(
                name: "FleetComplianceItems");

            migrationBuilder.DropTable(
                name: "FleetCostEntries");

            migrationBuilder.DropTable(
                name: "FleetDefects");

            migrationBuilder.DropTable(
                name: "FleetVehicleAssignments");

            migrationBuilder.DropTable(
                name: "InventoryBalances");

            migrationBuilder.DropTable(
                name: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "InventoryRequisitionItems");

            migrationBuilder.DropTable(
                name: "InventoryTransferItems");

            migrationBuilder.DropTable(
                name: "ItemClasses");

            migrationBuilder.DropTable(
                name: "ItemSuppliers");

            migrationBuilder.DropTable(
                name: "ItemUnitsOfMeasure");

            migrationBuilder.DropTable(
                name: "LandedCostAllocations");

            migrationBuilder.DropTable(
                name: "MaintenanceAttachmentAccessLogs");

            migrationBuilder.DropTable(
                name: "MaintenanceSettings");

            migrationBuilder.DropTable(
                name: "NotificationTopicRecipients");

            migrationBuilder.DropTable(
                name: "PaymentTerms");

            migrationBuilder.DropTable(
                name: "PerformanceBondRequests");

            migrationBuilder.DropTable(
                name: "PerformanceReviews");

            migrationBuilder.DropTable(
                name: "PhysicalCountItems");

            migrationBuilder.DropTable(
                name: "PriceGroups");

            migrationBuilder.DropTable(
                name: "PriceHistories");

            migrationBuilder.DropTable(
                name: "PriceListChangeHistories");

            migrationBuilder.DropTable(
                name: "PriceListLines");

            migrationBuilder.DropTable(
                name: "ProcurementBudgetAllocations");

            migrationBuilder.DropTable(
                name: "ProcurementBudgetRevisions");

            migrationBuilder.DropTable(
                name: "ProcurementPlanItemSuppliers");

            migrationBuilder.DropTable(
                name: "ProcurementSchedules");

            migrationBuilder.DropTable(
                name: "ProcurementSettings");

            migrationBuilder.DropTable(
                name: "PurchaseReturnItems");

            migrationBuilder.DropTable(
                name: "QualityIncidents");

            migrationBuilder.DropTable(
                name: "RequestForQuotationAwardLines");

            migrationBuilder.DropTable(
                name: "RequestForQuotationInvitations");

            migrationBuilder.DropTable(
                name: "RequestForQuotationQuoteItems");

            migrationBuilder.DropTable(
                name: "SuggestedSalesItems");

            migrationBuilder.DropTable(
                name: "SupplierConsolidations");

            migrationBuilder.DropTable(
                name: "SupplierGroups");

            migrationBuilder.DropTable(
                name: "SupplierPerformanceMetrics");

            migrationBuilder.DropTable(
                name: "SystemExceptionLogs");

            migrationBuilder.DropTable(
                name: "TenderAssignments");

            migrationBuilder.DropTable(
                name: "TenderAwardVerificationItemDocuments");

            migrationBuilder.DropTable(
                name: "TenderBidDocuments");

            migrationBuilder.DropTable(
                name: "TenderClarifications");

            migrationBuilder.DropTable(
                name: "TenderDocuments");

            migrationBuilder.DropTable(
                name: "TenderDocumentTypes");

            migrationBuilder.DropTable(
                name: "TenderEvaluations");

            migrationBuilder.DropTable(
                name: "TenderInterviews");

            migrationBuilder.DropTable(
                name: "TenderInvitations");

            migrationBuilder.DropTable(
                name: "TenderNegotiationItems");

            migrationBuilder.DropTable(
                name: "TenderPayments");

            migrationBuilder.DropTable(
                name: "TenderRevisions");

            migrationBuilder.DropTable(
                name: "TenderTemplates");

            migrationBuilder.DropTable(
                name: "TenderViewLogs");

            migrationBuilder.DropTable(
                name: "UnitOfMeasureConversions");

            migrationBuilder.DropTable(
                name: "UnitOfMeasureScheduleDetails");

            migrationBuilder.DropTable(
                name: "AssetConditionRecords");

            migrationBuilder.DropTable(
                name: "PreInspectionChecklistItems");

            migrationBuilder.DropTable(
                name: "BlacklistAppeals");

            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "EmailCampaigns");

            migrationBuilder.DropTable(
                name: "EmergencyProcurementPlans");

            migrationBuilder.DropTable(
                name: "EvaluationCriteria");

            migrationBuilder.DropTable(
                name: "FleetBatteryEvents");

            migrationBuilder.DropTable(
                name: "FleetExternalRepairs");

            migrationBuilder.DropTable(
                name: "FleetFuelTransactions");

            migrationBuilder.DropTable(
                name: "FleetIncidents");

            migrationBuilder.DropTable(
                name: "FleetTyreEvents");

            migrationBuilder.DropTable(
                name: "FleetTripInspections");

            migrationBuilder.DropTable(
                name: "InventoryLayers");

            migrationBuilder.DropTable(
                name: "InventoryRequisitions");

            migrationBuilder.DropTable(
                name: "InventoryTransfers");

            migrationBuilder.DropTable(
                name: "InventoryCostLayers");

            migrationBuilder.DropTable(
                name: "LandedCostItems");

            migrationBuilder.DropTable(
                name: "NotificationTopics");

            migrationBuilder.DropTable(
                name: "PhysicalCounts");

            migrationBuilder.DropTable(
                name: "MarketAnalyses");

            migrationBuilder.DropTable(
                name: "ProcurementBudgets");

            migrationBuilder.DropTable(
                name: "ProcurementPlanItems");

            migrationBuilder.DropTable(
                name: "GoodsReceiptNoteItems");

            migrationBuilder.DropTable(
                name: "PurchaseReturns");

            migrationBuilder.DropTable(
                name: "RequestForQuotationItems");

            migrationBuilder.DropTable(
                name: "RequestForQuotationQuotes");

            migrationBuilder.DropTable(
                name: "PriceLists");

            migrationBuilder.DropTable(
                name: "TenderAwardVerificationItemResults");

            migrationBuilder.DropTable(
                name: "TenderEvaluators");

            migrationBuilder.DropTable(
                name: "TenderBidItems");

            migrationBuilder.DropTable(
                name: "TenderFees");

            migrationBuilder.DropTable(
                name: "UnitOfMeasureSchedules");

            migrationBuilder.DropTable(
                name: "PreInspectionChecklistTemplates");

            migrationBuilder.DropTable(
                name: "TenderAwards");

            migrationBuilder.DropTable(
                name: "FleetBatteries");

            migrationBuilder.DropTable(
                name: "FleetTyres");

            migrationBuilder.DropTable(
                name: "FleetTrips");

            migrationBuilder.DropTable(
                name: "LandedCosts");

            migrationBuilder.DropTable(
                name: "ProcurementPlans");

            migrationBuilder.DropTable(
                name: "RequestForQuotations");

            migrationBuilder.DropTable(
                name: "AwardVerificationChecklistItems");

            migrationBuilder.DropTable(
                name: "TenderAwardVerificationBidders");

            migrationBuilder.DropTable(
                name: "TenderItems");

            migrationBuilder.DropTable(
                name: "UnitsOfMeasure");

            migrationBuilder.DropTable(
                name: "TenderNegotiations");

            migrationBuilder.DropTable(
                name: "GoodsReceiptNotes");

            migrationBuilder.DropTable(
                name: "TenderAwardVerifications");

            migrationBuilder.DropTable(
                name: "TenderBidLots");

            migrationBuilder.DropTable(
                name: "AwardVerificationChecklistTemplates");

            migrationBuilder.DropTable(
                name: "TenderBids");

            migrationBuilder.DropTable(
                name: "TenderLots");

            migrationBuilder.DropTable(
                name: "Tenders");

            migrationBuilder.DropTable(
                name: "EvaluationTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_DefaultInTransitLocationId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_DefaultQuarantineLocationId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_DefaultReceivingLocationId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_DefaultShippingLocationId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_ManagerId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseLocations_DedicatedItemId",
                table: "WarehouseLocations");

            migrationBuilder.DropIndex(
                name: "IX_Technicians_EmployeeNumber",
                table: "Technicians");

            migrationBuilder.DropIndex(
                name: "IX_Technicians_IsActive",
                table: "Technicians");

            migrationBuilder.DropIndex(
                name: "IX_Technicians_Specialization",
                table: "Technicians");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_WarehouseId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockAdjustments_WarehouseId",
                table: "StockAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_QualityControlChecklists_AssetCategoryId",
                table: "QualityControlChecklists");

            migrationBuilder.DropIndex(
                name: "IX_QualityControlChecklists_MaintenanceTypeId",
                table: "QualityControlChecklists");

            migrationBuilder.DropIndex(
                name: "IX_QualityControlChecklists_WorkOrderTypeId",
                table: "QualityControlChecklists");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_CurrentApproverId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_LastAmendedById",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_BusinessPartnerId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_LastAmendedById",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_SourceRequisitionId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_TenderAwardId1",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderItems_ItemUnitOfMeasureId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderItems_WarehouseId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_BaseUnitOfMeasureEntityId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_DefaultWarehouseId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_IsFinishedGood",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_IsKit",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_ItemClassId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_PriceGroupId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_SubCategoryId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_SubstituteItem1Id",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_SubstituteItem2Id",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_SubstituteItem3Id",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_SubstituteItem4Id",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_Currencies_Code",
                table: "Currencies");

            migrationBuilder.DropIndex(
                name: "IX_Currencies_IsActive",
                table: "Currencies");

            migrationBuilder.DropIndex(
                name: "IX_Currencies_IsBaseCurrency",
                table: "Currencies");

            migrationBuilder.DropIndex(
                name: "IX_Currencies_TenantId_Code",
                table: "Currencies");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartners_ParentId",
                table: "BusinessPartners");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartners_SalesRepresentativeId",
                table: "BusinessPartners");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartners_UserId",
                table: "BusinessPartners");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000010001"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000010002"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000010003"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000010004"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000010005"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000010006"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000010007"));

            migrationBuilder.DropColumn(
                name: "BillingExcludedAt",
                table: "WorkOrderTools");

            migrationBuilder.DropColumn(
                name: "BillingExcludedBy",
                table: "WorkOrderTools");

            migrationBuilder.DropColumn(
                name: "BillingExclusionReason",
                table: "WorkOrderTools");

            migrationBuilder.DropColumn(
                name: "IsExcludedFromBilling",
                table: "WorkOrderTools");

            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "WorkOrderTasks");

            migrationBuilder.DropColumn(
                name: "BillingType",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "FixedAmount",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "CloseTime",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "CostCenter",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "DefaultInTransitLocationId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "DefaultQuarantineLocationId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "DefaultReceivingLocationId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "DefaultShippingLocationId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "GLAccountCode",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "HasInspectionArea",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "HasQuarantineArea",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "HasReceivingDock",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "HasShippingDock",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "IsConsignmentWarehouse",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "IsTemperatureControlled",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "MaxTemperature",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "MaxWeightCapacity",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "MinTemperature",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "OpenTime",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "OperatingHours",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "TemperatureUnit",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "TotalCapacityCubicFeet",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "TotalCapacitySquareFeet",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "TotalLocations",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "UsedCapacitySquareFeet",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "UsedLocations",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "ABCClass",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "Aisle",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "Bin",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "ColumnNumber",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "DedicatedItemCode",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "DedicatedItemId",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "IsDamageLocation",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "IsInTransitLocation",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "IsInspectionLocation",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "IsQuarantineLocation",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "IsReturnLocation",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "IsShippingLocation",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "IsStagingLocation",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "LevelNumber",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "LocationBarcode",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "LocationHierarchyType",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "PickSequence",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "Rack",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "RowNumber",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "Shelf",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "TemperatureZone",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "Zone",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "BlacklistDate",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BlacklistExpiryDate",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BlacklistReason",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsBlacklisted",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "StockAdjustmentItems");

            migrationBuilder.DropColumn(
                name: "AssetCategoryId",
                table: "QualityControlChecklists");

            migrationBuilder.DropColumn(
                name: "MaintenanceTypeId",
                table: "QualityControlChecklists");

            migrationBuilder.DropColumn(
                name: "WorkOrderTypeId",
                table: "QualityControlChecklists");

            migrationBuilder.DropColumn(
                name: "AmendmentNotes",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ApprovalHistory",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ApprovalLevel",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "BudgetAllocated",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "BudgetCode",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "BudgetRemaining",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "BudgetValidated",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "CurrentApproverId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "DeliveryAddress",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "DeliveryInstructions",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "DeliveryWarehouseId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "GeneratedFrom",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "IsAutoGenerated",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "LastAmendedAt",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "LastAmendedById",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "PreferredBusinessPartnerId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ProjectCode",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ProjectName",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "RequiredApprovalLevel",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "RequisitionType",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "SourcePlanId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "AutoCloseOnReceipt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "BudgetCode",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "BudgetValidated",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ContractEndDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ContractId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ContractNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ContractRemainingValue",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ContractStartDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ContractUsedValue",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ContractValue",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CostAllocationMethod",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CostApportionmentBasis",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CostsAllocated",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ExpenseGLAccount",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "LastAmendedAt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "LastAmendedById",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "MiscellaneousCost",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "OrderType",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SourceRequisitionId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SourceRequisitionNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SourceRfqAwardType",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SourceRfqId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SourceRfqNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SourceRfqQuoteId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "TenderAwardId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "TenderAwardId1",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "TenderNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "TolerancePercent",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "TotalAdditionalCost",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ItemUnitOfMeasureId",
                table: "PurchaseOrderReceiptItems");

            migrationBuilder.DropColumn(
                name: "UnitOfMeasure",
                table: "PurchaseOrderReceiptItems");

            migrationBuilder.DropColumn(
                name: "AllocatedAdditionalCost",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "AllocatedCostPerUnit",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "ItemUnitOfMeasureId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "LandedUnitCost",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "PriceListLineId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "SourceRfqItemId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "SourceRfqQuoteId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "SourceRfqQuoteItemId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "UnitOfMeasure",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "FixedAmount",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "OwnershipType",
                table: "MaintenanceAssets");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "MaintenanceAssets");

            migrationBuilder.DropColumn(
                name: "AllowBackorder",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "AlternateBarcode",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "AutoReorder",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "BaseUnitOfMeasureEntityId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "BaseUnitOfMeasureId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "CountryOfOrigin",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "CurrencyDecimals",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "CurrentCost",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "DailyRentalRate",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "DaysBeforeExpiryWarning",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "DefaultWarehouseId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "Feature",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "GenericDescription",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "HSCode",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IncludeInFulfillment",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IncludeInInvoices",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IncludeInOrders",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IncludeInQuotes",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IsFinishedGood",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IsFinishedGoodComponent",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IsKit",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IsKitComponent",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IsProcurementItem",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IsTaxable",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IsValuationLocked",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ItemClassId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ListPrice",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "LongDescription",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "LotCategory",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "MaintainCalendarYearHistory",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "MaintainFiscalYearHistory",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "MaintainTransactionHistory",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "MinimumOrderQuantity",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "MinimumShelfLifeDays",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "MovementClass",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "OrderMultiple",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "PriceGroupId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "PrimarySupplierId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "PurchaseTaxOption",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "PurchaseTaxScheduleId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "QRCode",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "QuantityDecimals",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "RequireApprovalForPurchase",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "SalesTaxOption",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "SalesTaxScheduleId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ShippingWeight",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ShortDescription",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "Style",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "SubCategoryId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "SubstituteItem1Id",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "SubstituteItem2Id",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "SubstituteItem3Id",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "SubstituteItem4Id",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "TaxCode",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ThumbnailUrl",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ValuationMethod",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "WarnBeforeLotExpires",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "WarrantyDays",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "XYZClass",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "AllowPhotos",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "AssetTypes",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "EstimatedDuration",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "Frequency",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "InspectorRoles",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "RequiresSignature",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "Symbol",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "AverageOrderValue",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "CreditHoldDate",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "CreditHoldReason",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "CreditLimit",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "CustomerAccountNumber",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "CustomerSince",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "CustomerType",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "DefaultDiscount",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "DeliveryInstructions",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "IsOnCreditHold",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "IsTaxExempt",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "LastPurchaseDate",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "LoyaltyPoints",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "LoyaltyTier",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "OutstandingBalance",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "PaymentTerms",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "PreferredShippingMethod",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "PriceList",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "SalesRepresentativeId",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "SalesTerritory",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "TaxExemptionExpiry",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "TaxExemptionNumber",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "TotalLifetimePurchases",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "IsRejected",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.DropColumn(
                name: "RejectedById",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.DropColumn(
                name: "RejectedDate",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.RenameColumn(
                name: "PreferredBusinessPartnerId",
                table: "PurchaseRequisitionItems",
                newName: "PreferredSupplierId");

            migrationBuilder.RenameIndex(
                name: "IX_PurchaseRequisitionItems_PreferredBusinessPartnerId",
                table: "PurchaseRequisitionItems",
                newName: "IX_PurchaseRequisitionItems_PreferredSupplierId");

            migrationBuilder.RenameColumn(
                name: "BusinessPartnerOrderNumber",
                table: "PurchaseOrders",
                newName: "SupplierOrderNumber");

            migrationBuilder.RenameColumn(
                name: "BusinessPartnerItemCode",
                table: "PurchaseOrderItems",
                newName: "SupplierItemCode");

            migrationBuilder.RenameColumn(
                name: "UnitOfMeasureScheduleId",
                table: "InventoryItems",
                newName: "DefaultTaxGroupId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryItems_UnitOfMeasureScheduleId",
                table: "InventoryItems",
                newName: "IX_InventoryItems_DefaultTaxGroupId");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Currencies",
                newName: "CurrencyName");

            migrationBuilder.RenameColumn(
                name: "FormatString",
                table: "Currencies",
                newName: "MinorUnitPluralName");

            migrationBuilder.RenameColumn(
                name: "ExchangeRateDate",
                table: "Currencies",
                newName: "RedenominationDate");

            migrationBuilder.RenameColumn(
                name: "ExchangeRate",
                table: "Currencies",
                newName: "RoundingPrecision");

            migrationBuilder.RenameColumn(
                name: "DisplayOrder",
                table: "Currencies",
                newName: "TransactionCount");

            migrationBuilder.RenameColumn(
                name: "Country",
                table: "Currencies",
                newName: "PluralName");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "Currencies",
                newName: "NumericCode");

            migrationBuilder.AddColumn<string>(
                name: "BaseCurrency",
                table: "Tenants",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BaseCurrencyName",
                table: "Tenants",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyDecimalPlaces",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CurrencySymbol",
                table: "Tenants",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TechnicianId",
                table: "TechnicianTeams",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TechnicianId1",
                table: "TechnicianSkillAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SupplierId",
                table: "PurchaseOrders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "InventoryItemId",
                table: "PurchaseOrderItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MaintenanceTypeId1",
                table: "MaintenanceSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultTaxGroupId",
                table: "InventoryCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountLinkageCount",
                table: "Currencies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivationDate",
                table: "Currencies",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "AutoRetrieveExchangeRate",
                table: "Currencies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CentralBank",
                table: "Currencies",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountriesUsingCurrency",
                table: "Currencies",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Currencies",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryName",
                table: "Currencies",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyClassification",
                table: "Currencies",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Currencies",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CurrencySymbol",
                table: "Currencies",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivationDate",
                table: "Currencies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                table: "Currencies",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecimalSeparator",
                table: "Currencies",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DefaultRateType",
                table: "Currencies",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DigitGrouping",
                table: "Currencies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveDate",
                table: "Currencies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExchangeRateUpdateFrequency",
                table: "Currencies",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpirationDate",
                table: "Currencies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstTransactionDate",
                table: "Currencies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormatExample",
                table: "Currencies",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeographicRegion",
                table: "Currencies",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasAccountLinkages",
                table: "Currencies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasBeenRedenominated",
                table: "Currencies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasRestrictions",
                table: "Currencies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasTransactionHistory",
                table: "Currencies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTransactionDate",
                table: "Currencies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "Currencies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinorUnitName",
                table: "Currencies",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinorUnitRatio",
                table: "Currencies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Currencies",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousCurrencyCode",
                table: "Currencies",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Currencies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RateVarianceThresholdPercentage",
                table: "Currencies",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedenominationNotes",
                table: "Currencies",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RedenominationRatio",
                table: "Currencies",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "Currencies",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RestrictionsDescription",
                table: "Currencies",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoundingMethod",
                table: "Currencies",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Currencies",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SymbolPosition",
                table: "Currencies",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Currencies",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThousandsSeparator",
                table: "Currencies",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ApplicantEmail",
                table: "BusinessPartnerRegistrations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "DistributedLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcquiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcquiredBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastHeartbeatUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LockName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributedLocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FromEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FromName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HtmlContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsTemplate = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReplyTo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ScheduledFor = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TagsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TemplateData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TextContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailCampaigns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyProcurementPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BudgetReserve = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriticalityLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmergencyType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EscalationContacts = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MaxApprovalLimit = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NextReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PlanCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RapidProcurementProcess = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UtilizedReserve = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyProcurementPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmergencyProcurementPlans_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmergencyProcurementPlans_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmergencyProcurementPlans_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EvaluationCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriterionCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CriterionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    EvaluationType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaxScore = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationCriteria_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationCriteria_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EvaluationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    FinancialWeight = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MinimumTechnicalScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PassingScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ScoringMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TechnicalWeight = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TenderType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationTemplates_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FleetBatteries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InstalledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Position = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RemovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Spec = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetBatteries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetBatteries_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetBatteries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FleetComplianceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComplianceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentLinks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsCritical = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastDueSoonReminderSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastOverdueReminderSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetComplianceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetComplianceItems_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetComplianceItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FleetExternalRepairs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActualCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AdditionalData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InvoicedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetExternalRepairs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetExternalRepairs_BusinessPartners_VendorBusinessPartnerId",
                        column: x => x.VendorBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetExternalRepairs_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetExternalRepairs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetExternalRepairs_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FleetTrips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActualEndAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualStartAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Destination = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DispatchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DispatchedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EndMileage = table.Column<double>(type: "float", nullable: true),
                    EndOperatingHours = table.Column<double>(type: "float", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Origin = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PlannedEndAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PlannedStartAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartMileage = table.Column<double>(type: "float", nullable: true),
                    StartOperatingHours = table.Column<double>(type: "float", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetTrips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetTrips_Employees_DriverEmployeeId",
                        column: x => x.DriverEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FleetTrips_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetTrips_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FleetTyres",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InstalledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Position = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RemovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Size = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TreadDepthMm = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetTyres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetTyres_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetTyres_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FleetVehicleAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignmentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetVehicleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetVehicleAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetVehicleAssignments_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetVehicleAssignments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivingLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CarrierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryNoteNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DriverName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GRNNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InspectionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InspectionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InspectionResult = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurchaseOrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequiresInspection = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StockUpdated = table.Column<bool>(type: "bit", nullable: false),
                    StockUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TotalItems = table.Column<int>(type: "int", nullable: false),
                    TotalQuantityAccepted = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalQuantityReceived = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalQuantityRejected = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VehicleNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNotes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNotes_Users_InspectedById",
                        column: x => x.InspectedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNotes_Users_ReceivedById",
                        column: x => x.ReceivedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNotes_WarehouseLocations_ReceivingLocationId",
                        column: x => x.ReceivingLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNotes_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InventoryBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AverageUnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastCountDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastIssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastMovementDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRecalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastReceiptDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QuantityAllocated = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    QuantityAvailable = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    QuantityOnOrder = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryBalances_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryBalances_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryBalances_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryBalances_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryCostLayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConsumedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InventoryItemId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsFullyConsumed = table.Column<bool>(type: "bit", nullable: false),
                    LandedCostPerUnit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LayerDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LayerNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OriginalQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RemainingValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TotalUnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCostLayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_InventoryItems_InventoryItemId1",
                        column: x => x.InventoryItemId1,
                        principalTable: "InventoryItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InventoryLayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsFullyConsumed = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LayerDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LayerNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OriginalQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RemainingValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryLayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryLayers_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLayers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLayers_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryLayers_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryRequisitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssuedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CostCenter = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IssuedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProjectCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequisitionNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequisitionType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalItems = table.Column<int>(type: "int", nullable: false),
                    TotalQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryRequisitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryRequisitions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryRequisitions_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryRequisitions_Users_IssuedById",
                        column: x => x.IssuedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryRequisitions_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryRequisitions_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryRequisitions_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DestinationLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DestinationWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InTransitLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ShippedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CarrierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CostAllocationMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CostApportionmentBasis = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CostsAllocated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExpenseGLAccount = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MiscellaneousCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MiscellaneousCostDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShippedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShippingCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalAdditionalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalItems = table.Column<int>(type: "int", nullable: false),
                    TotalQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TransferNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VehicleNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Users_ReceivedById",
                        column: x => x.ReceivedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Users_ShippedById",
                        column: x => x.ShippedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_WarehouseLocations_DestinationLocationId",
                        column: x => x.DestinationLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_WarehouseLocations_InTransitLocationId",
                        column: x => x.InTransitLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_WarehouseLocations_SourceLocationId",
                        column: x => x.SourceLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Warehouses_DestinationWarehouseId",
                        column: x => x.DestinationWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Warehouses_SourceWarehouseId",
                        column: x => x.SourceWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ItemClasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultIsLotTracked = table.Column<bool>(type: "bit", nullable: false),
                    DefaultIsSerialTracked = table.Column<bool>(type: "bit", nullable: false),
                    DefaultItemType = table.Column<int>(type: "int", nullable: false),
                    DefaultRequiresInspection = table.Column<bool>(type: "bit", nullable: false),
                    DefaultTaxCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DefaultValuationMethod = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemClasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemClasses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemSuppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsPreferred = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: false),
                    MinimumOrderQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OrderMultiple = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PriceEffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PriceExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SupplierItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemSuppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemSuppliers_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemSuppliers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceAttachmentAccessLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessType = table.Column<int>(type: "int", nullable: false),
                    AccessedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceAttachmentAccessLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceAttachmentAccessLogs_Employees_AccessedByUserId",
                        column: x => x.AccessedByUserId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceAttachmentAccessLogs_MaintenanceAttachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "MaintenanceAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlockFleetDispatchWhenComplianceDueSoon = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FleetComplianceDueSoonDays = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnalysisCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AnalysisPeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AnalysisPeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CurrentMarketPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ForecastedPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HistoricalAveragePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ItemCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ItemDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MarketRiskLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Opportunities = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OptimalPurchaseMonth = table.Column<int>(type: "int", nullable: true),
                    PreparedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PriceChangePercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PriceTrend = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecommendedStrategy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RiskFactors = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SeasonalPattern = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StrategyRationale = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketAnalyses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketAnalyses_Users_PreparedById",
                        column: x => x.PreparedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NotificationTopics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionUrlTemplate = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EnableEmail = table.Column<bool>(type: "bit", nullable: false),
                    EnableInApp = table.Column<bool>(type: "bit", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InAppBodyTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InAppTitleTemplate = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Key = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTopics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationTopics_EmailTemplates_EmailTemplateId",
                        column: x => x.EmailTemplateId,
                        principalTable: "EmailTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NotificationTopics_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTerms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicableTo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DiscountDays = table.Column<int>(type: "int", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    DueDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTerms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTerms_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcknowledgedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcknowledgedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActionItems = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AreasForImprovement = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ComplianceScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CostCompetitivenessScore = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerServiceScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryPerformanceScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    FollowUpDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InnovationScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OverallGrade = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OverallScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PeriodEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    QualityScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Recommendations = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequiresFollowUp = table.Column<bool>(type: "bit", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReviewPeriod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Strengths = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupplierComments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierCommentsDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceReviews_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PerformanceReviews_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceReviews_Users_AcknowledgedById",
                        column: x => x.AcknowledgedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PerformanceReviews_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PhysicalCounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CountedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InitiatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlindCount = table.Column<bool>(type: "bit", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CountDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CountType = table.Column<int>(type: "int", nullable: false),
                    CountedItems = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FreezeInventory = table.Column<bool>(type: "bit", nullable: false),
                    IncludeZeroStock = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PostedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalCountedQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalItems = table.Column<int>(type: "int", nullable: false),
                    TotalSystemQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalVarianceQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalVarianceValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VarianceItems = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysicalCounts_InventoryCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "InventoryCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCounts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCounts_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCounts_Users_CountedById",
                        column: x => x.CountedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCounts_Users_InitiatedById",
                        column: x => x.InitiatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCounts_Users_PostedById",
                        column: x => x.PostedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCounts_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCounts_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PreInspectionChecklistTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "General"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    VersionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreInspectionChecklistTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreInspectionChecklistTemplates_MaintenanceAssetCategories_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalTable: "MaintenanceAssetCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreInspectionChecklistTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DefaultMarginPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DefaultMarkupPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PriceGroupCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceGroups_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceListChangeHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangePercent = table.Column<decimal>(type: "decimal(18,4)", precision: 5, scale: 2, nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChangedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 4, nullable: false),
                    OldPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 4, nullable: false),
                    PriceListId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PriceListLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceListChangeHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceListChangeHistories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupersededPriceListId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicableEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApplicableEntityType = table.Column<int>(type: "int", nullable: false),
                    ApprovalComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "USD"),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PriceListCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceLists_PriceLists_SupersededPriceListId",
                        column: x => x.SupersededPriceListId,
                        principalTable: "PriceLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceLists_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviousVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalComments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedBudget = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PlanDurationYears = table.Column<int>(type: "int", nullable: false),
                    PlanEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlanNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PlanStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreparedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewComments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TotalEstimatedBudget = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementPlans_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPlans_ProcurementPlans_PreviousVersionId",
                        column: x => x.PreviousVersionId,
                        principalTable: "ProcurementPlans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProcurementPlans_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPlans_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProcurementPlans_Users_PreparedById",
                        column: x => x.PreparedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProcurementPlans_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProcurementSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllowBackorders = table.Column<bool>(type: "bit", nullable: false),
                    AllowMultipleSuppliersPerItem = table.Column<bool>(type: "bit", nullable: false),
                    AllowNonInventoryItems = table.Column<bool>(type: "bit", nullable: false),
                    AutoApprovalThreshold = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AutoCreateInventoryItems = table.Column<bool>(type: "bit", nullable: false),
                    AutoCreateSupplierItems = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultItemCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultUnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultValuationMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnforceSupplierCatalog = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PurchaseOrderNumberFormat = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PurchaseOrderReceiptNumberFormat = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PurchaseRequisitionNumberFormat = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequireApprovalForPO = table.Column<bool>(type: "bit", nullable: false),
                    RequireContractForPO = table.Column<bool>(type: "bit", nullable: false),
                    RequireDeliveryDate = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValidateBudgetBeforePO = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityIncidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcknowledgedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcknowledgedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrectiveAction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FinancialImpact = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    IncidentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IncidentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IncidentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreventiveAction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QuantityAffected = table.Column<int>(type: "int", nullable: false),
                    ReportedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequiresSupplierResponse = table.Column<bool>(type: "bit", nullable: false),
                    Resolution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolvedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RootCause = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SupplierResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierResponseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityIncidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityIncidents_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QualityIncidents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityIncidents_Users_AcknowledgedById",
                        column: x => x.AcknowledgedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QualityIncidents_Users_ReportedById",
                        column: x => x.ReportedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QualityIncidents_Users_ResolvedById",
                        column: x => x.ResolvedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RequestForQuotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AwardedBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourcePurchaseRequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AwardedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ExternalRecipientEmails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RfqNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SubmissionDeadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestForQuotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestForQuotations_BusinessPartners_AwardedBusinessPartnerId",
                        column: x => x.AwardedBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RequestForQuotations_PurchaseRequisitions_SourcePurchaseRequisitionId",
                        column: x => x.SourcePurchaseRequisitionId,
                        principalTable: "PurchaseRequisitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RequestForQuotations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SuggestedSalesItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuggestedItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    SuggestOnFulfillment = table.Column<bool>(type: "bit", nullable: false),
                    SuggestOnInvoice = table.Column<bool>(type: "bit", nullable: false),
                    SuggestOnOrder = table.Column<bool>(type: "bit", nullable: false),
                    SuggestOnQuote = table.Column<bool>(type: "bit", nullable: false),
                    SuggestedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuggestedSalesItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SuggestedSalesItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SuggestedSalesItems_InventoryItems_SuggestedItemId",
                        column: x => x.SuggestedItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SuggestedSalesItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierConsolidations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActualSavings = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AnalysisPeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AnalysisPeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsolidationCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CurrentSupplierCount = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ImplementationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ImplementationPlan = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ItemCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OpportunityLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PotentialSavings = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PreferredSupplierIds = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreparedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecommendedStrategy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RecommendedSupplierCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StrategyRationale = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SuppliersToPhaseOut = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TotalSpend = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierConsolidations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierConsolidations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierConsolidations_Users_PreparedById",
                        column: x => x.PreparedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SupplierPerformanceMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalculatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AverageDeliveryDelayDays = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AveragePriceVariance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AverageResponseTimeHours = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ComplaintsReceived = table.Column<int>(type: "int", nullable: false),
                    ComplaintsResolved = table.Column<int>(type: "int", nullable: false),
                    ComplianceScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ContractViolations = table.Column<int>(type: "int", nullable: false),
                    CostCompetitivenessScore = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostSavingInitiatives = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerServiceRating = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DefectRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DefectiveItems = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EarlyDeliveries = table.Column<int>(type: "int", nullable: false),
                    EstimatedCostSavings = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InnovationSuggestions = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LateDeliveries = table.Column<int>(type: "int", nullable: false),
                    MetricPeriod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Month = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OnTimeDeliveries = table.Column<int>(type: "int", nullable: false),
                    OnTimeDeliveryRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OverallPerformanceScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PerformanceGrade = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    QualityAcceptanceRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Quarter = table.Column<int>(type: "int", nullable: true),
                    RejectedItems = table.Column<int>(type: "int", nullable: false),
                    ReturnedItems = table.Column<int>(type: "int", nullable: false),
                    TermsBreaches = table.Column<int>(type: "int", nullable: false),
                    TotalItemsReceived = table.Column<int>(type: "int", nullable: false),
                    TotalOrders = table.Column<int>(type: "int", nullable: false),
                    TotalPurchaseValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Year = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPerformanceMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierPerformanceMetrics_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierPerformanceMetrics_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPerformanceMetrics_Users_CalculatedById",
                        column: x => x.CalculatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SystemExceptionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExceptionType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FirstOccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FullMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastOccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Logger = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OccurrenceCount = table.Column<int>(type: "int", nullable: false),
                    QueryString = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReferrerUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RemoteIpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    RequestMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RequestPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ShortMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemExceptionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemExceptionLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenderDocumentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllowedFileTypes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    DocumentCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DocumentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaxFileSizeMB = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderDocumentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderDocumentTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderDocumentTypes_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllowPartialBids = table.Column<bool>(type: "bit", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DefaultValidityDays = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryWeightage = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EvaluationCriteriaJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExperienceWeightage = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PriceWeightage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QualityWeightage = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RequiredDocuments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequiresPrequalification = table.Column<bool>(type: "bit", nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TenderType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TermsAndConditions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderTemplates_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UnitsOfMeasure",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsBaseUnit = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitsOfMeasure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitsOfMeasure_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AwardVerificationChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    ItemText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwardVerificationChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AwardVerificationChecklistItems_AwardVerificationChecklistTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "AwardVerificationChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AwardVerificationChecklistItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BlacklistHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelatedAppealId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActionByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BlacklistDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlacklistExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlacklistHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlacklistHistories_BlacklistAppeals_RelatedAppealId",
                        column: x => x.RelatedAppealId,
                        principalTable: "BlacklistAppeals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BlacklistHistories_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BlacklistHistories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BlacklistHistories_Users_ActionById",
                        column: x => x.ActionById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmailCampaignRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailCampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailCampaignRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailCampaignRecipients_EmailCampaigns_EmailCampaignId",
                        column: x => x.EmailCampaignId,
                        principalTable: "EmailCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailCampaignRecipients_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyProcurementItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmergencyProcurementPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlternativeItems = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriticalityLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CurrentStockLevel = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmergencyOrderQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ItemCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ItemDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaxLeadTimeDays = table.Column<int>(type: "int", nullable: false),
                    MinimumStockLevel = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Specifications = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyProcurementItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmergencyProcurementItems_EmergencyProcurementPlans_EmergencyProcurementPlanId",
                        column: x => x.EmergencyProcurementPlanId,
                        principalTable: "EmergencyProcurementPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmergencyProcurementItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmergencySuppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmergencyProcurementPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContactPerson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContractExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasEmergencyContract = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ItemsProvided = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastVerifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ResponseTimeHours = table.Column<int>(type: "int", nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencySuppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmergencySuppliers_EmergencyProcurementPlans_EmergencyProcurementPlanId",
                        column: x => x.EmergencyProcurementPlanId,
                        principalTable: "EmergencyProcurementPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmergencySuppliers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmergencySuppliers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationTemplateCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluationCriterionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluationTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaxScore = table.Column<int>(type: "int", nullable: false),
                    MinimumScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,4)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationTemplateCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationTemplateCriteria_EvaluationCriteria_EvaluationCriterionId",
                        column: x => x.EvaluationCriterionId,
                        principalTable: "EvaluationCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationTemplateCriteria_EvaluationTemplates_EvaluationTemplateId",
                        column: x => x.EvaluationTemplateId,
                        principalTable: "EvaluationTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvaluationTemplateCriteria_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tenders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AwardedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvaluationTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptanceDeclarationDocumentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AcceptanceDeclarationDocumentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AllowPartialBids = table.Column<bool>(type: "bit", nullable: false),
                    AwardDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryWeightage = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    EvaluationCriteriaJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExperienceWeightage = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    FinancialWeight = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MinimumPerformanceRating = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MinimumTechnicalScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OpeningDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PriceWeightage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PublishDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QualityWeightage = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RequiredDocuments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequiresAcceptanceDeclaration = table.Column<bool>(type: "bit", nullable: false),
                    RequiresPrequalification = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubmissionDeadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TechnicalWeight = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TenderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TenderType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TermsAndConditions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UseQCBSEvaluation = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tenders_EvaluationTemplates_EvaluationTemplateId",
                        column: x => x.EvaluationTemplateId,
                        principalTable: "EvaluationTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tenders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tenders_Users_AwardedById",
                        column: x => x.AwardedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tenders_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tenders_Users_PublishedById",
                        column: x => x.PublishedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FleetBatteryEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FleetBatteryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CostAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EventAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FromPosition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ToPosition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetBatteryEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetBatteryEvents_FleetBatteries_FleetBatteryId",
                        column: x => x.FleetBatteryId,
                        principalTable: "FleetBatteries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetBatteryEvents_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetBatteryEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FleetFuelTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FleetTripId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FuelledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MileageAtFuel = table.Column<double>(type: "float", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OperatingHoursAtFuel = table.Column<double>(type: "float", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReceiptReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VendorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetFuelTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetFuelTransactions_FleetTrips_FleetTripId",
                        column: x => x.FleetTripId,
                        principalTable: "FleetTrips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FleetFuelTransactions_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetFuelTransactions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FleetIncidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FleetTripId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActualRepairCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AdditionalData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ClaimNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClaimSettledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClaimStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ClaimSubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DamageAssessment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EstimatedRepairCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IncidentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InsuranceCompany = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PolicyNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetIncidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetIncidents_Employees_DriverEmployeeId",
                        column: x => x.DriverEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetIncidents_FleetTrips_FleetTripId",
                        column: x => x.FleetTripId,
                        principalTable: "FleetTrips",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetIncidents_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetIncidents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetIncidents_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FleetTripInspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FleetTripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectorEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InspectionData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InspectionKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OverallResult = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetTripInspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetTripInspections_Employees_InspectorEmployeeId",
                        column: x => x.InspectorEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetTripInspections_FleetTrips_FleetTripId",
                        column: x => x.FleetTripId,
                        principalTable: "FleetTrips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetTripInspections_InspectionTemplates_InspectionTemplateId",
                        column: x => x.InspectionTemplateId,
                        principalTable: "InspectionTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetTripInspections_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FleetTyreEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FleetTyreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CostAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EventAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FromPosition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ToPosition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TreadDepthMm = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetTyreEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetTyreEvents_FleetTyres_FleetTyreId",
                        column: x => x.FleetTyreId,
                        principalTable: "FleetTyres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetTyreEvents_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetTyreEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptNoteItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsReceiptNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InspectionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InspectionResult = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LineValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManufactureDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OrderedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PurchaseOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RejectedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptNoteItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNoteItems_GoodsReceiptNotes_GoodsReceiptNoteId",
                        column: x => x.GoodsReceiptNoteId,
                        principalTable: "GoodsReceiptNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNoteItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNoteItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNoteItems_WarehouseLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LandedCosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GoodsReceiptNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CostDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GRNNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LandedCostNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PostedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnallocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VendorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandedCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandedCosts_GoodsReceiptNotes_GoodsReceiptNoteId",
                        column: x => x.GoodsReceiptNoteId,
                        principalTable: "GoodsReceiptNotes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LandedCosts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandedCosts_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LandedCosts_Users_PostedById",
                        column: x => x.PostedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GoodsReceiptNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcknowledgedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CarrierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditNoteNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DebitNoteDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DebitNoteNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DebitNoteRequired = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GRNNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurchaseOrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RefundDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RefundReceived = table.Column<bool>(type: "bit", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReturnNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReturnReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReturnReasonDetails = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ShippedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TotalItems = table.Column<int>(type: "int", nullable: false),
                    TotalQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_GoodsReceiptNotes_GoodsReceiptNoteId",
                        column: x => x.GoodsReceiptNoteId,
                        principalTable: "GoodsReceiptNotes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CostLayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversedMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsPosted = table.Column<bool>(type: "bit", nullable: false),
                    IsReversal = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MovementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MovementNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MovementType = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReferenceType = table.Column<int>(type: "int", nullable: false),
                    RunningBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RunningValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VarianceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_InventoryLayers_CostLayerId",
                        column: x => x.CostLayerId,
                        principalTable: "InventoryLayers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryMovements_InventoryMovements_ReversedMovementId",
                        column: x => x.ReversedMovementId,
                        principalTable: "InventoryMovements",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Users_PostedById",
                        column: x => x.PostedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryMovements_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryRequisitionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryRequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IssuedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LineValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryRequisitionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryRequisitionItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryRequisitionItems_InventoryRequisitions_InventoryRequisitionId",
                        column: x => x.InventoryRequisitionId,
                        principalTable: "InventoryRequisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryRequisitionItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryRequisitionItems_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransferItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryTransferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedCostPerUnit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DamageNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DamagedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandedUnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LineValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RequestedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ShippedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalAllocatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransferItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryTransferItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferItems_InventoryTransfers_InventoryTransferId",
                        column: x => x.InventoryTransferId,
                        principalTable: "InventoryTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryTransferItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferItems_WarehouseLocations_DestinationLocationId",
                        column: x => x.DestinationLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransferItems_WarehouseLocations_SourceLocationId",
                        column: x => x.SourceLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PriceHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarketAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ItemCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ItemDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PriceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PriceSource = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceHistories_MarketAnalyses_MarketAnalysisId",
                        column: x => x.MarketAnalysisId,
                        principalTable: "MarketAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PriceHistories_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PriceHistories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTopicRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecipientKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RecipientValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SendEmail = table.Column<bool>(type: "bit", nullable: false),
                    SendInApp = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTopicRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationTopicRecipients_NotificationTopics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "NotificationTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationTopicRecipients_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalCountItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PhysicalCountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountAttempts = table.Column<int>(type: "int", nullable: false),
                    CountedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CountedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsCounted = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequiresRecount = table.Column<bool>(type: "bit", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SystemQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VarianceQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    VarianceReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VarianceValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalCountItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysicalCountItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountItems_PhysicalCounts_PhysicalCountId",
                        column: x => x.PhysicalCountId,
                        principalTable: "PhysicalCounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhysicalCountItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountItems_Users_CountedById",
                        column: x => x.CountedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCountItems_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AssetConditionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DischargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneralNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InspectionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InspectionNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InspectionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Admission"),
                    InspectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PhotoPaths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "InProgress"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetConditionRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetConditionRecords_AssetAdmissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "AssetAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssetConditionRecords_AssetDischarges_DischargeId",
                        column: x => x.DischargeId,
                        principalTable: "AssetDischarges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssetConditionRecords_MaintenanceAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetConditionRecords_PreInspectionChecklistTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "PreInspectionChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetConditionRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PreInspectionChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllowRepairReplacement = table.Column<bool>(type: "bit", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "General"),
                    ChoiceOptions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultRepairReplacementAction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DefaultValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EstimatedRepairHours = table.Column<double>(type: "float", nullable: false),
                    EstimatedReplacementHours = table.Column<double>(type: "float", nullable: false),
                    HelpText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Boolean"),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaxValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MinValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RequiresPhoto = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreInspectionChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreInspectionChecklistItems_PreInspectionChecklistTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "PreInspectionChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PreInspectionChecklistItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefaultPriceListId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultCreditLimit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 2, nullable: true),
                    DefaultDiscountPercent = table.Column<decimal>(type: "decimal(18,4)", precision: 5, scale: 2, nullable: false),
                    DefaultPaymentTerms = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    GroupCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerGroups_PriceLists_DefaultPriceListId",
                        column: x => x.DefaultPriceListId,
                        principalTable: "PriceLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerGroups_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceListLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriceListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "decimal(18,4)", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsTaxInclusive = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastPriceUpdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: true),
                    MaxQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MinQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MinimumOrderQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    NetPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OrderMultiple = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PreviousPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 4, nullable: true),
                    PriceChangePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 5, scale: 2, nullable: true),
                    RoundingRule = table.Column<int>(type: "int", nullable: false),
                    SupplierItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "EA"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceListLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceListLines_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PriceListLines_PriceLists_PriceListId",
                        column: x => x.PriceListId,
                        principalTable: "PriceLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PriceListLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefaultPriceListId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultLeadTimeDays = table.Column<int>(type: "int", nullable: false),
                    DefaultPaymentTerms = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    GroupCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierGroups_PriceLists_DefaultPriceListId",
                        column: x => x.DefaultPriceListId,
                        principalTable: "PriceLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierGroups_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementBudgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BudgetCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CommittedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ControlLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RemainingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UtilizedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WarningThresholdPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementBudgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementBudgets_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBudgets_ProcurementPlans_ProcurementPlanId",
                        column: x => x.ProcurementPlanId,
                        principalTable: "ProcurementPlans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProcurementBudgets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBudgets_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPlanItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreferredSupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcurementPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlternativeSuppliers = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstimatedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    EstimatedTotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstimatedUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsCritical = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ItemCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ItemDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PlannedProcurementMonth = table.Column<int>(type: "int", nullable: true),
                    PlannedQuarter = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PreferredSupplierName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProcurementMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequiredDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Specifications = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementPlanItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementPlanItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProcurementPlanItems_ProcurementPlans_ProcurementPlanId",
                        column: x => x.ProcurementPlanId,
                        principalTable: "ProcurementPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementPlanItems_Suppliers_PreferredSupplierId",
                        column: x => x.PreferredSupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProcurementPlanItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RequestForQuotationInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RfqId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvitedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestForQuotationInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationInvitations_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationInvitations_RequestForQuotations_RfqId",
                        column: x => x.RfqId,
                        principalTable: "RequestForQuotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationInvitations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RequestForQuotationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RfqId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RequiredDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourcePurchaseRequisitionItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Specifications = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestForQuotationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RequestForQuotationItems_RequestForQuotations_RfqId",
                        column: x => x.RfqId,
                        principalTable: "RequestForQuotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RequestForQuotationQuotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RfqId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestForQuotationQuotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationQuotes_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationQuotes_RequestForQuotations_RfqId",
                        column: x => x.RfqId,
                        principalTable: "RequestForQuotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationQuotes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemUnitsOfMeasure",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConversionToBase = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsBaseUnit = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsPurchaseUnit = table.Column<bool>(type: "bit", nullable: false),
                    IsSalesUnit = table.Column<bool>(type: "bit", nullable: false),
                    IsStockingUnit = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemUnitsOfMeasure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemUnitsOfMeasure_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemUnitsOfMeasure_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemUnitsOfMeasure_UnitsOfMeasure_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitOfMeasureConversions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitOfMeasureConversions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitOfMeasureConversions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitOfMeasureConversions_UnitsOfMeasure_FromUnitId",
                        column: x => x.FromUnitId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitOfMeasureConversions_UnitsOfMeasure_ToUnitId",
                        column: x => x.ToUnitId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitOfMeasureSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseUnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QuantityDecimals = table.Column<int>(type: "int", nullable: false),
                    ScheduleId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitOfMeasureSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitOfMeasureSchedules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitOfMeasureSchedules_UnitsOfMeasure_BaseUnitOfMeasureId",
                        column: x => x.BaseUnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenderAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignmentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderAssignments_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderAssignments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderAssignments_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderAssignments_Users_AssignedById",
                        column: x => x.AssignedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenderAssignments_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderAwardVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderAwardVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderAwardVerifications_AwardVerificationChecklistTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "AwardVerificationChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TenderAwardVerifications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderAwardVerifications_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderAwardVerifications_Users_CompletedById",
                        column: x => x.CompletedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenderAwardVerifications_Users_StartedById",
                        column: x => x.StartedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderBids",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedDeclaration = table.Column<bool>(type: "bit", nullable: false),
                    AssociationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BidNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CombinedScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CommercialProposal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    DeclarationAcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryDays = table.Column<int>(type: "int", nullable: true),
                    DeliveryScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    DisqualificationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EvaluatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EvaluationNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExperienceScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    FinancialScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    IsCompliant = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsQualifiedTechnically = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NonComplianceReasons = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OpenedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PriceScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    QualityScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Rank = table.Column<int>(type: "int", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TechnicalProposal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicalScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TotalBidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WarrantyTerms = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderBids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderBids_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderBids_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderBids_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderBids_Users_EvaluatedById",
                        column: x => x.EvaluatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenderBids_Users_OpenedById",
                        column: x => x.OpenedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderClarifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnsweredById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QuestionById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnswerDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Question = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuestionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderClarifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderClarifications_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenderClarifications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderClarifications_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderClarifications_Users_AnsweredById",
                        column: x => x.AnsweredById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenderClarifications_Users_QuestionById",
                        column: x => x.QuestionById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    FileType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderDocuments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderDocuments_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderDocuments_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderEvaluators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WeightagePercentage = table.Column<decimal>(type: "decimal(18,4)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderEvaluators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderEvaluators_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderEvaluators_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderEvaluators_Users_AssignedById",
                        column: x => x.AssignedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenderEvaluators_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenderFees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BankAccountDetails = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FeeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderFees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderFees_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderFees_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenderInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeclineReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvitedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ViewedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderInvitations_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderInvitations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderInvitations_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderInvitations_Users_InvitedById",
                        column: x => x.InvitedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    EstimatedValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LotCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LotNumber = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequiredDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Specifications = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderLots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderLots_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenderRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewSubmissionDeadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotificationSent = table.Column<bool>(type: "bit", nullable: false),
                    NotificationSentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequiresRebid = table.Column<bool>(type: "bit", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevisionNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RevisionType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderRevisions_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderRevisions_Users_RevisedById",
                        column: x => x.RevisedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderViewLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderViewLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderViewLogs_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenderViewLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderViewLogs_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderViewLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FleetDefects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FleetTripId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FleetTripInspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdditionalData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetDefects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetDefects_Employees_ReportedByEmployeeId",
                        column: x => x.ReportedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetDefects_FleetTripInspections_FleetTripInspectionId",
                        column: x => x.FleetTripInspectionId,
                        principalTable: "FleetTripInspections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetDefects_FleetTrips_FleetTripId",
                        column: x => x.FleetTripId,
                        principalTable: "FleetTrips",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetDefects_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetDefects_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetDefects_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FleetCostEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FleetBatteryEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FleetExternalRepairId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FleetFuelTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FleetIncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FleetTripId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FleetTyreEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CostType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Manual"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetCostEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetCostEntries_FleetBatteryEvents_FleetBatteryEventId",
                        column: x => x.FleetBatteryEventId,
                        principalTable: "FleetBatteryEvents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetCostEntries_FleetExternalRepairs_FleetExternalRepairId",
                        column: x => x.FleetExternalRepairId,
                        principalTable: "FleetExternalRepairs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetCostEntries_FleetFuelTransactions_FleetFuelTransactionId",
                        column: x => x.FleetFuelTransactionId,
                        principalTable: "FleetFuelTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetCostEntries_FleetIncidents_FleetIncidentId",
                        column: x => x.FleetIncidentId,
                        principalTable: "FleetIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetCostEntries_FleetTrips_FleetTripId",
                        column: x => x.FleetTripId,
                        principalTable: "FleetTrips",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetCostEntries_FleetTyreEvents_FleetTyreEventId",
                        column: x => x.FleetTyreEventId,
                        principalTable: "FleetTyreEvents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetCostEntries_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetCostEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetCostEntries_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LandedCostItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandedCostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocationMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountInBaseCurrency = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostType = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandedCostItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandedCostItems_LandedCosts_LandedCostId",
                        column: x => x.LandedCostId,
                        principalTable: "LandedCosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LandedCostItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReturnItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsReceiptNoteItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurchaseReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LineValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReturnQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReturnReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReturnReasonDetails = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StockReversed = table.Column<bool>(type: "bit", nullable: false),
                    StockReversedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReturnItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_GoodsReceiptNoteItems_GoodsReceiptNoteItemId",
                        column: x => x.GoodsReceiptNoteItemId,
                        principalTable: "GoodsReceiptNoteItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_PurchaseReturns_PurchaseReturnId",
                        column: x => x.PurchaseReturnId,
                        principalTable: "PurchaseReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AssetConditionItemResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConditionRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InspectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsPresent = table.Column<bool>(type: "bit", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NumericValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PhotoPaths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RepairReplacementAction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SelectedOption = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TaskCreated = table.Column<bool>(type: "bit", nullable: false),
                    TextValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetConditionItemResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetConditionItemResults_AssetConditionRecords_ConditionRecordId",
                        column: x => x.ConditionRecordId,
                        principalTable: "AssetConditionRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetConditionItemResults_PreInspectionChecklistItems_ChecklistItemId",
                        column: x => x.ChecklistItemId,
                        principalTable: "PreInspectionChecklistItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetConditionItemResults_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementBudgetAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementBudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CategoryDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CategoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RemainingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UtilizedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementBudgetAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementBudgetAllocations_ProcurementBudgets_ProcurementBudgetId",
                        column: x => x.ProcurementBudgetId,
                        principalTable: "ProcurementBudgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementBudgetAllocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementBudgetRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcurementBudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PreviousAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    RevisionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementBudgetRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementBudgetRevisions_ProcurementBudgets_ProcurementBudgetId",
                        column: x => x.ProcurementBudgetId,
                        principalTable: "ProcurementBudgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementBudgetRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBudgetRevisions_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPlanItemSuppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementPlanItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsPreferred = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    QuotedUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SupplierItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementPlanItemSuppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementPlanItemSuppliers_BusinessPartners_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPlanItemSuppliers_ProcurementPlanItems_ProcurementPlanItemId",
                        column: x => x.ProcurementPlanItemId,
                        principalTable: "ProcurementPlanItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementPlanItemSuppliers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcurementPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcurementPlanItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActualEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CashFlowNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConsiderCashFlow = table.Column<bool>(type: "bit", nullable: false),
                    ConsiderSeasonalPricing = table.Column<bool>(type: "bit", nullable: false),
                    ConsolidationNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConsolidationOpportunity = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsOptimalTiming = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PlannedEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlannedStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ScheduleType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SeasonalNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StorageLimitations = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TimingRationale = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementSchedules_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSchedules_ProcurementPlanItems_ProcurementPlanItemId",
                        column: x => x.ProcurementPlanItemId,
                        principalTable: "ProcurementPlanItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProcurementSchedules_ProcurementPlans_ProcurementPlanId",
                        column: x => x.ProcurementPlanId,
                        principalTable: "ProcurementPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSchedules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RequestForQuotationAwardLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RfqId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RfqItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QuoteItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestForQuotationAwardLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationAwardLines_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationAwardLines_RequestForQuotationItems_RfqItemId",
                        column: x => x.RfqItemId,
                        principalTable: "RequestForQuotationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationAwardLines_RequestForQuotationQuotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "RequestForQuotationQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationAwardLines_RequestForQuotations_RfqId",
                        column: x => x.RfqId,
                        principalTable: "RequestForQuotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationAwardLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RequestForQuotationQuoteItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RfqItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestForQuotationQuoteItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationQuoteItems_RequestForQuotationItems_RfqItemId",
                        column: x => x.RfqItemId,
                        principalTable: "RequestForQuotationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationQuoteItems_RequestForQuotationQuotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "RequestForQuotationQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationQuoteItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitOfMeasureScheduleDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitOfMeasureScheduleDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitOfMeasureScheduleDetails_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitOfMeasureScheduleDetails_UnitOfMeasureSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "UnitOfMeasureSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UnitOfMeasureScheduleDetails_UnitsOfMeasure_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenderAwardVerificationBidders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VerificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VerifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OverallComments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderAwardVerificationBidders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderAwardVerificationBidders_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderAwardVerificationBidders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderAwardVerificationBidders_TenderAwardVerifications_VerificationId",
                        column: x => x.VerificationId,
                        principalTable: "TenderAwardVerifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderAwardVerificationBidders_TenderBids_TenderBidId",
                        column: x => x.TenderBidId,
                        principalTable: "TenderBids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderAwardVerificationBidders_Users_VerifiedById",
                        column: x => x.VerifiedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderBidDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    FileType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderBidDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderBidDocuments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderBidDocuments_TenderBids_TenderBidId",
                        column: x => x.TenderBidId,
                        principalTable: "TenderBids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderBidDocuments_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderInterviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConductedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Agenda = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InterviewNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InterviewScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    InterviewType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PanelMembers = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderInterviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderInterviews_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderInterviews_TenderBids_TenderBidId",
                        column: x => x.TenderBidId,
                        principalTable: "TenderBids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderInterviews_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderInterviews_Users_ConductedById",
                        column: x => x.ConductedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderEvaluatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommercialComments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComplianceScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    EvaluationCriteriaJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EvaluationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExperienceScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsRecommended = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OverallComments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PriceScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    QualityScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Recommendation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TechnicalComments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicalScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TotalScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderEvaluations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderEvaluations_TenderBids_TenderBidId",
                        column: x => x.TenderBidId,
                        principalTable: "TenderBids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderEvaluations_TenderEvaluators_TenderEvaluatorId",
                        column: x => x.TenderEvaluatorId,
                        principalTable: "TenderEvaluators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenderPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderFeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VerifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaymentProof = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderPayments_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderPayments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderPayments_TenderFees_TenderFeeId",
                        column: x => x.TenderFeeId,
                        principalTable: "TenderFees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderPayments_Users_VerifiedById",
                        column: x => x.VerifiedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderBidLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommercialProposal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryDays = table.Column<int>(type: "int", nullable: true),
                    DeliveryScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    EvaluationNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PriceScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    QualityScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Rank = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TechnicalProposal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalLotAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WarrantyTerms = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderBidLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderBidLots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderBidLots_TenderBids_TenderBidId",
                        column: x => x.TenderBidId,
                        principalTable: "TenderBids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderBidLots_TenderLots_LotId",
                        column: x => x.LotId,
                        principalTable: "TenderLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RequiredDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Specifications = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderItems_TenderLots_LotId",
                        column: x => x.LotId,
                        principalTable: "TenderLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderItems_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LandedCostAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CostLayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GoodsReceiptNoteItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandedCostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandedCostItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostPerUnit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandedCostAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandedCostAllocations_GoodsReceiptNoteItems_GoodsReceiptNoteItemId",
                        column: x => x.GoodsReceiptNoteItemId,
                        principalTable: "GoodsReceiptNoteItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LandedCostAllocations_InventoryCostLayers_CostLayerId",
                        column: x => x.CostLayerId,
                        principalTable: "InventoryCostLayers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LandedCostAllocations_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LandedCostAllocations_LandedCostItems_LandedCostItemId",
                        column: x => x.LandedCostItemId,
                        principalTable: "LandedCostItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LandedCostAllocations_LandedCosts_LandedCostId",
                        column: x => x.LandedCostId,
                        principalTable: "LandedCosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LandedCostAllocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenderAwardVerificationItemResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VerifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderAwardVerificationItemResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderAwardVerificationItemResults_AwardVerificationChecklistItems_ChecklistItemId",
                        column: x => x.ChecklistItemId,
                        principalTable: "AwardVerificationChecklistItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderAwardVerificationItemResults_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderAwardVerificationItemResults_TenderAwardVerificationBidders_BidderId",
                        column: x => x.BidderId,
                        principalTable: "TenderAwardVerificationBidders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderAwardVerificationItemResults_Users_VerifiedById",
                        column: x => x.VerifiedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderNegotiations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidLotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvitedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvitedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NegotiatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderNegotiations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderNegotiations_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderNegotiations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderNegotiations_TenderBidLots_BidLotId",
                        column: x => x.BidLotId,
                        principalTable: "TenderBidLots",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenderNegotiations_TenderBids_TenderBidId",
                        column: x => x.TenderBidId,
                        principalTable: "TenderBids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderNegotiations_TenderLots_LotId",
                        column: x => x.LotId,
                        principalTable: "TenderLots",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenderNegotiations_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderNegotiations_Users_CompletedById",
                        column: x => x.CompletedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenderNegotiations_Users_InvitedById",
                        column: x => x.InvitedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderBidItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidLotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryDays = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OfferedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Specifications = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TechnicalDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderBidItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderBidItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderBidItems_TenderBidLots_BidLotId",
                        column: x => x.BidLotId,
                        principalTable: "TenderBidLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderBidItems_TenderBids_TenderBidId",
                        column: x => x.TenderBidId,
                        principalTable: "TenderBids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderBidItems_TenderItems_TenderItemId",
                        column: x => x.TenderItemId,
                        principalTable: "TenderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenderAwardVerificationItemDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderAwardVerificationItemDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderAwardVerificationItemDocuments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderAwardVerificationItemDocuments_TenderAwardVerificationItemResults_ItemResultId",
                        column: x => x.ItemResultId,
                        principalTable: "TenderAwardVerificationItemResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderAwardVerificationItemDocuments_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenderAwards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AwardedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BidLotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NegotiationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AwardDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AwardJustification = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AwardedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsNegotiated = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalBidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderAwards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderAwards_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderAwards_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderAwards_TenderBidLots_BidLotId",
                        column: x => x.BidLotId,
                        principalTable: "TenderBidLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderAwards_TenderBids_TenderBidId",
                        column: x => x.TenderBidId,
                        principalTable: "TenderBids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderAwards_TenderLots_LotId",
                        column: x => x.LotId,
                        principalTable: "TenderLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderAwards_TenderNegotiations_NegotiationId",
                        column: x => x.NegotiationId,
                        principalTable: "TenderNegotiations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderAwards_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderAwards_Users_AwardedById",
                        column: x => x.AwardedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderNegotiationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NegotiationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderBidItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ItemDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NegotiatedTotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NegotiatedUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalTotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OriginalUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderNegotiationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderNegotiationItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderNegotiationItems_TenderBidItems_TenderBidItemId",
                        column: x => x.TenderBidItemId,
                        principalTable: "TenderBidItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderNegotiationItems_TenderNegotiations_NegotiationId",
                        column: x => x.NegotiationId,
                        principalTable: "TenderNegotiations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SignedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderAwardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContractDocumentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContractNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContractTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContractType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContractValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 2, nullable: false),
                    ContractorSignatoryName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContractorSignedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Deliverables = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DurationDays = table.Column<int>(type: "int", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PenaltyClause = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetentionPercentage = table.Column<decimal>(type: "decimal(18,4)", precision: 5, scale: 2, nullable: false),
                    ScopeOfWork = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SignedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SpecialConditions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TerminatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TerminationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WarrantyPeriodDays = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contracts_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_TenderAwards_TenderAwardId",
                        column: x => x.TenderAwardId,
                        principalTable: "TenderAwards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_TenderBids_TenderBidId",
                        column: x => x.TenderBidId,
                        principalTable: "TenderBids",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Contracts_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Contracts_Users_SignedById",
                        column: x => x.SignedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PerformanceBondRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderAwardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubmittedFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubmittedFileSize = table.Column<long>(type: "bigint", nullable: true),
                    SubmittedFileType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TemplateFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TemplateFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TemplateFileSize = table.Column<long>(type: "bigint", nullable: true),
                    TemplateFileType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceBondRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceBondRequests_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PerformanceBondRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceBondRequests_TenderAwards_TenderAwardId",
                        column: x => x.TenderAwardId,
                        principalTable: "TenderAwards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PerformanceBondRequests_TenderBids_TenderBidId",
                        column: x => x.TenderBidId,
                        principalTable: "TenderBids",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PerformanceBondRequests_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PerformanceBondRequests_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PerformanceBondRequests_Users_SubmittedById",
                        column: x => x.SubmittedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContractAmendments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmendmentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AmendmentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApprovalNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DaysExtended = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NewValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreviousEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PreviousValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 2, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScopeChanges = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueChange = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAmendments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractAmendments_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractAmendments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractAmendments_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContractAmendments_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContractDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractDocuments_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractDocuments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractDocuments_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContractMilestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActualDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InvoicedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MilestoneName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentPercentage = table.Column<decimal>(type: "decimal(18,4)", precision: 5, scale: 2, nullable: false),
                    PlannedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractMilestones_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractMilestones_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(1782));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(2042));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(2070));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(2073));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(2789));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(2807));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(2817));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(2825));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(2842));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(2855));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(2865));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3037));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3188));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3209));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3225));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3248));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3266));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3293));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3305));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3316));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3492));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3525));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3528));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3529));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3530));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3532));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3533));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3535));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3536));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3537));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3539));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3540));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3541));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3542));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3543));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3544));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3718));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3721));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3722));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3723));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3724));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3725));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3726));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3727));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3728));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3729));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3730));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3731));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3732));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3732));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3733));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3857));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3858));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3861));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3862));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3863));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3864));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3865));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3866));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3867));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3868));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3887));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3888));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(3889));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000010001"), null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0000-000000010002"), null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0000-000000010003"), null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0000-000000010004"), null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0000-000000010005"), null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0000-000000010006"), null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0000-000000010007"), null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 2, 51, 18, 96, DateTimeKind.Utc).AddTicks(964));

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_AmendmentNumber",
                table: "ContractAmendments",
                column: "AmendmentNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_ApprovedById",
                table: "ContractAmendments",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_ContractId",
                table: "ContractAmendments",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_RequestedById",
                table: "ContractAmendments",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_Status",
                table: "ContractAmendments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_TenantId",
                table: "ContractAmendments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractDocuments_ContractId",
                table: "ContractDocuments",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractDocuments_DocumentType",
                table: "ContractDocuments",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_ContractDocuments_TenantId",
                table: "ContractDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractDocuments_UploadedById",
                table: "ContractDocuments",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_ContractMilestones_ContractId",
                table: "ContractMilestones",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractMilestones_PlannedDate",
                table: "ContractMilestones",
                column: "PlannedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ContractMilestones_Status",
                table: "ContractMilestones",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContractMilestones_TenantId",
                table: "ContractMilestones",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_BusinessPartnerId",
                table: "Contracts",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ContractNumber",
                table: "Contracts",
                column: "ContractNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_CreatedById",
                table: "Contracts",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_SignedById",
                table: "Contracts",
                column: "SignedById");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_Status",
                table: "Contracts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_TenantId",
                table: "Contracts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_TenderAwardId",
                table: "Contracts",
                column: "TenderAwardId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_TenderBidId",
                table: "Contracts",
                column: "TenderBidId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_TenderId",
                table: "Contracts",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerGroups_DefaultPriceListId",
                table: "CustomerGroups",
                column: "DefaultPriceListId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerGroups_TenantId",
                table: "CustomerGroups",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerGroups_TenantId_GroupCode",
                table: "CustomerGroups",
                columns: new[] { "TenantId", "GroupCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DistributedLocks_LockName",
                table: "DistributedLocks",
                column: "LockName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignRecipients_EmailCampaignId",
                table: "EmailCampaignRecipients",
                column: "EmailCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignRecipients_TenantId_EmailCampaignId_Email",
                table: "EmailCampaignRecipients",
                columns: new[] { "TenantId", "EmailCampaignId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignRecipients_TenantId_EmailCampaignId_Status",
                table: "EmailCampaignRecipients",
                columns: new[] { "TenantId", "EmailCampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaigns_TenantId_Name",
                table: "EmailCampaigns",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaigns_TenantId_Status",
                table: "EmailCampaigns",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProcurementItems_CriticalityLevel",
                table: "EmergencyProcurementItems",
                column: "CriticalityLevel");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProcurementItems_EmergencyProcurementPlanId",
                table: "EmergencyProcurementItems",
                column: "EmergencyProcurementPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProcurementItems_ItemCategory",
                table: "EmergencyProcurementItems",
                column: "ItemCategory");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProcurementItems_TenantId",
                table: "EmergencyProcurementItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProcurementPlans_ApprovedById",
                table: "EmergencyProcurementPlans",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProcurementPlans_CriticalityLevel",
                table: "EmergencyProcurementPlans",
                column: "CriticalityLevel");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProcurementPlans_DepartmentId",
                table: "EmergencyProcurementPlans",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProcurementPlans_EmergencyType",
                table: "EmergencyProcurementPlans",
                column: "EmergencyType");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProcurementPlans_PlanCode",
                table: "EmergencyProcurementPlans",
                column: "PlanCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProcurementPlans_Status",
                table: "EmergencyProcurementPlans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProcurementPlans_TenantId",
                table: "EmergencyProcurementPlans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencySuppliers_EmergencyProcurementPlanId",
                table: "EmergencySuppliers",
                column: "EmergencyProcurementPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencySuppliers_IsActive",
                table: "EmergencySuppliers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencySuppliers_SupplierId",
                table: "EmergencySuppliers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencySuppliers_TenantId",
                table: "EmergencySuppliers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCriteria_CreatedById",
                table: "EvaluationCriteria",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCriteria_TenantId",
                table: "EvaluationCriteria",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationTemplateCriteria_DisplayOrder",
                table: "EvaluationTemplateCriteria",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationTemplateCriteria_EvaluationCriterionId",
                table: "EvaluationTemplateCriteria",
                column: "EvaluationCriterionId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationTemplateCriteria_EvaluationTemplateId_EvaluationCriterionId",
                table: "EvaluationTemplateCriteria",
                columns: new[] { "EvaluationTemplateId", "EvaluationCriterionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationTemplateCriteria_TenantId",
                table: "EvaluationTemplateCriteria",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationTemplates_CreatedById",
                table: "EvaluationTemplates",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationTemplates_IsActive",
                table: "EvaluationTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationTemplates_IsDefault",
                table: "EvaluationTemplates",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationTemplates_TenantId_TemplateName",
                table: "EvaluationTemplates",
                columns: new[] { "TenantId", "TemplateName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteries_TenantId_SerialNumber_IsDeleted",
                table: "FleetBatteries",
                columns: new[] { "TenantId", "SerialNumber", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteries_TenantId_VehicleAssetId_Status_IsDeleted",
                table: "FleetBatteries",
                columns: new[] { "TenantId", "VehicleAssetId", "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteries_VehicleAssetId",
                table: "FleetBatteries",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteryEvents_FleetBatteryId",
                table: "FleetBatteryEvents",
                column: "FleetBatteryId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteryEvents_TenantId_FleetBatteryId_EventAtUtc_IsDeleted",
                table: "FleetBatteryEvents",
                columns: new[] { "TenantId", "FleetBatteryId", "EventAtUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteryEvents_TenantId_VehicleAssetId_EventAtUtc_IsDeleted",
                table: "FleetBatteryEvents",
                columns: new[] { "TenantId", "VehicleAssetId", "EventAtUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteryEvents_VehicleAssetId",
                table: "FleetBatteryEvents",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetComplianceItems_TenantId_ExpiryDate",
                table: "FleetComplianceItems",
                columns: new[] { "TenantId", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetComplianceItems_TenantId_VehicleAssetId",
                table: "FleetComplianceItems",
                columns: new[] { "TenantId", "VehicleAssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetComplianceItems_TenantId_VehicleAssetId_IsCritical_ExpiryDate",
                table: "FleetComplianceItems",
                columns: new[] { "TenantId", "VehicleAssetId", "IsCritical", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetComplianceItems_VehicleAssetId",
                table: "FleetComplianceItems",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_FleetBatteryEventId",
                table: "FleetCostEntries",
                column: "FleetBatteryEventId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_FleetExternalRepairId",
                table: "FleetCostEntries",
                column: "FleetExternalRepairId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_FleetFuelTransactionId",
                table: "FleetCostEntries",
                column: "FleetFuelTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_FleetIncidentId",
                table: "FleetCostEntries",
                column: "FleetIncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_FleetTripId",
                table: "FleetCostEntries",
                column: "FleetTripId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_FleetTyreEventId",
                table: "FleetCostEntries",
                column: "FleetTyreEventId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_TenantId_CostType_IsDeleted",
                table: "FleetCostEntries",
                columns: new[] { "TenantId", "CostType", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_TenantId_VehicleAssetId_CostDateUtc_IsDeleted",
                table: "FleetCostEntries",
                columns: new[] { "TenantId", "VehicleAssetId", "CostDateUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_VehicleAssetId",
                table: "FleetCostEntries",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_WorkOrderId",
                table: "FleetCostEntries",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetDefects_FleetTripId",
                table: "FleetDefects",
                column: "FleetTripId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetDefects_FleetTripInspectionId",
                table: "FleetDefects",
                column: "FleetTripInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetDefects_ReportedByEmployeeId",
                table: "FleetDefects",
                column: "ReportedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetDefects_TenantId_FleetTripId_IsDeleted",
                table: "FleetDefects",
                columns: new[] { "TenantId", "FleetTripId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetDefects_TenantId_VehicleAssetId_Status_IsDeleted",
                table: "FleetDefects",
                columns: new[] { "TenantId", "VehicleAssetId", "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetDefects_VehicleAssetId",
                table: "FleetDefects",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetDefects_WorkOrderId",
                table: "FleetDefects",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetExternalRepairs_TenantId_VehicleAssetId_Status_IsDeleted",
                table: "FleetExternalRepairs",
                columns: new[] { "TenantId", "VehicleAssetId", "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetExternalRepairs_TenantId_VendorBusinessPartnerId_IsDeleted",
                table: "FleetExternalRepairs",
                columns: new[] { "TenantId", "VendorBusinessPartnerId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetExternalRepairs_VehicleAssetId",
                table: "FleetExternalRepairs",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetExternalRepairs_VendorBusinessPartnerId",
                table: "FleetExternalRepairs",
                column: "VendorBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetExternalRepairs_WorkOrderId",
                table: "FleetExternalRepairs",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetFuelTransactions_FleetTripId",
                table: "FleetFuelTransactions",
                column: "FleetTripId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetFuelTransactions_TenantId_FleetTripId",
                table: "FleetFuelTransactions",
                columns: new[] { "TenantId", "FleetTripId" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetFuelTransactions_TenantId_VehicleAssetId_FuelledAt",
                table: "FleetFuelTransactions",
                columns: new[] { "TenantId", "VehicleAssetId", "FuelledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetFuelTransactions_VehicleAssetId",
                table: "FleetFuelTransactions",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetIncidents_DriverEmployeeId",
                table: "FleetIncidents",
                column: "DriverEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetIncidents_FleetTripId",
                table: "FleetIncidents",
                column: "FleetTripId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetIncidents_TenantId_Status_IsDeleted",
                table: "FleetIncidents",
                columns: new[] { "TenantId", "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetIncidents_TenantId_VehicleAssetId_OccurredAtUtc_IsDeleted",
                table: "FleetIncidents",
                columns: new[] { "TenantId", "VehicleAssetId", "OccurredAtUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetIncidents_VehicleAssetId",
                table: "FleetIncidents",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetIncidents_WorkOrderId",
                table: "FleetIncidents",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTripInspections_FleetTripId",
                table: "FleetTripInspections",
                column: "FleetTripId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTripInspections_InspectionTemplateId",
                table: "FleetTripInspections",
                column: "InspectionTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTripInspections_InspectorEmployeeId",
                table: "FleetTripInspections",
                column: "InspectorEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTripInspections_TenantId_FleetTripId_InspectionKind_IsDeleted",
                table: "FleetTripInspections",
                columns: new[] { "TenantId", "FleetTripId", "InspectionKind", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTripInspections_TenantId_InspectionTemplateId_IsDeleted",
                table: "FleetTripInspections",
                columns: new[] { "TenantId", "InspectionTemplateId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_DriverEmployeeId",
                table: "FleetTrips",
                column: "DriverEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_TenantId_DriverEmployeeId",
                table: "FleetTrips",
                columns: new[] { "TenantId", "DriverEmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_TenantId_RequestedByUserId",
                table: "FleetTrips",
                columns: new[] { "TenantId", "RequestedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_TenantId_Status",
                table: "FleetTrips",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_TenantId_VehicleAssetId",
                table: "FleetTrips",
                columns: new[] { "TenantId", "VehicleAssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_VehicleAssetId",
                table: "FleetTrips",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyreEvents_FleetTyreId",
                table: "FleetTyreEvents",
                column: "FleetTyreId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyreEvents_TenantId_FleetTyreId_EventAtUtc_IsDeleted",
                table: "FleetTyreEvents",
                columns: new[] { "TenantId", "FleetTyreId", "EventAtUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyreEvents_TenantId_VehicleAssetId_EventAtUtc_IsDeleted",
                table: "FleetTyreEvents",
                columns: new[] { "TenantId", "VehicleAssetId", "EventAtUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyreEvents_VehicleAssetId",
                table: "FleetTyreEvents",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyres_TenantId_SerialNumber_IsDeleted",
                table: "FleetTyres",
                columns: new[] { "TenantId", "SerialNumber", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyres_TenantId_VehicleAssetId_Status_IsDeleted",
                table: "FleetTyres",
                columns: new[] { "TenantId", "VehicleAssetId", "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyres_VehicleAssetId",
                table: "FleetTyres",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetVehicleAssignments_EmployeeId",
                table: "FleetVehicleAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetVehicleAssignments_TenantId_EmployeeId_IsActive",
                table: "FleetVehicleAssignments",
                columns: new[] { "TenantId", "EmployeeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetVehicleAssignments_TenantId_VehicleAssetId_IsActive",
                table: "FleetVehicleAssignments",
                columns: new[] { "TenantId", "VehicleAssetId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetVehicleAssignments_VehicleAssetId",
                table: "FleetVehicleAssignments",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNoteItems_GoodsReceiptNoteId",
                table: "GoodsReceiptNoteItems",
                column: "GoodsReceiptNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNoteItems_InventoryItemId",
                table: "GoodsReceiptNoteItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNoteItems_StorageLocationId",
                table: "GoodsReceiptNoteItems",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNoteItems_TenantId",
                table: "GoodsReceiptNoteItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_GRNNumber",
                table: "GoodsReceiptNotes",
                column: "GRNNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_InspectedById",
                table: "GoodsReceiptNotes",
                column: "InspectedById");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_ReceiptDate",
                table: "GoodsReceiptNotes",
                column: "ReceiptDate");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_ReceivedById",
                table: "GoodsReceiptNotes",
                column: "ReceivedById");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_ReceivingLocationId",
                table: "GoodsReceiptNotes",
                column: "ReceivingLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_Status",
                table: "GoodsReceiptNotes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_SupplierId",
                table: "GoodsReceiptNotes",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_TenantId",
                table: "GoodsReceiptNotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_WarehouseId",
                table: "GoodsReceiptNotes",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_InventoryItemId",
                table: "InventoryBalances",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_LastMovementDate",
                table: "InventoryBalances",
                column: "LastMovementDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_LocationId",
                table: "InventoryBalances",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_QuantityOnHand",
                table: "InventoryBalances",
                columns: new[] { "TenantId", "QuantityOnHand" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_Unique_Item_Warehouse_Location",
                table: "InventoryBalances",
                columns: new[] { "TenantId", "InventoryItemId", "WarehouseId", "LocationId" },
                unique: true,
                filter: "[LocationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_WarehouseId",
                table: "InventoryBalances",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_InventoryItemId",
                table: "InventoryCostLayers",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_InventoryItemId1",
                table: "InventoryCostLayers",
                column: "InventoryItemId1");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_IsFullyConsumed",
                table: "InventoryCostLayers",
                column: "IsFullyConsumed");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_LayerDate",
                table: "InventoryCostLayers",
                column: "LayerDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_LocationId",
                table: "InventoryCostLayers",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_TenantId",
                table: "InventoryCostLayers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_WarehouseId",
                table: "InventoryCostLayers",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLayers_FIFO_Consumption",
                table: "InventoryLayers",
                columns: new[] { "TenantId", "InventoryItemId", "WarehouseId", "IsFullyConsumed", "LayerDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLayers_InventoryItemId",
                table: "InventoryLayers",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLayers_IsActive",
                table: "InventoryLayers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLayers_IsFullyConsumed",
                table: "InventoryLayers",
                column: "IsFullyConsumed");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLayers_LayerDate",
                table: "InventoryLayers",
                column: "LayerDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLayers_LocationId",
                table: "InventoryLayers",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLayers_TenantId_LayerNumber",
                table: "InventoryLayers",
                columns: new[] { "TenantId", "LayerNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLayers_WarehouseId",
                table: "InventoryLayers",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_CostLayerId",
                table: "InventoryMovements",
                column: "CostLayerId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_CreatedById",
                table: "InventoryMovements",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_InventoryItemId",
                table: "InventoryMovements",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_IsPosted",
                table: "InventoryMovements",
                column: "IsPosted");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_Item_Warehouse_Date",
                table: "InventoryMovements",
                columns: new[] { "TenantId", "InventoryItemId", "WarehouseId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_LocationId",
                table: "InventoryMovements",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_MovementDate",
                table: "InventoryMovements",
                column: "MovementDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_MovementType",
                table: "InventoryMovements",
                column: "MovementType");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_PostedById",
                table: "InventoryMovements",
                column: "PostedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_PostingDate",
                table: "InventoryMovements",
                column: "PostingDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ReferenceId",
                table: "InventoryMovements",
                column: "ReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ReferenceType",
                table: "InventoryMovements",
                column: "ReferenceType");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ReversedMovementId",
                table: "InventoryMovements",
                column: "ReversedMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_TenantId_MovementNumber",
                table: "InventoryMovements",
                columns: new[] { "TenantId", "MovementNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_WarehouseId",
                table: "InventoryMovements",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRequisitionItems_InventoryItemId",
                table: "InventoryRequisitionItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRequisitionItems_InventoryRequisitionId",
                table: "InventoryRequisitionItems",
                column: "InventoryRequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRequisitionItems_LocationId",
                table: "InventoryRequisitionItems",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRequisitionItems_TenantId",
                table: "InventoryRequisitionItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRequisitions_ApprovedById",
                table: "InventoryRequisitions",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRequisitions_DepartmentId",
                table: "InventoryRequisitions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRequisitions_IssuedById",
                table: "InventoryRequisitions",
                column: "IssuedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRequisitions_LocationId",
                table: "InventoryRequisitions",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRequisitions_RequestDate",
                table: "InventoryRequisitions",
                column: "RequestDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRequisitions_RequestedById",
                table: "InventoryRequisitions",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRequisitions_RequiredDate",
                table: "InventoryRequisitions",
                column: "RequiredDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRequisitions_RequisitionNumber",
                table: "InventoryRequisitions",
                column: "RequisitionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRequisitions_Status",
                table: "InventoryRequisitions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRequisitions_TenantId_Status",
                table: "InventoryRequisitions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRequisitions_WarehouseId",
                table: "InventoryRequisitions",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferItems_DestinationLocationId",
                table: "InventoryTransferItems",
                column: "DestinationLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferItems_InventoryItemId",
                table: "InventoryTransferItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferItems_InventoryTransferId",
                table: "InventoryTransferItems",
                column: "InventoryTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferItems_SourceLocationId",
                table: "InventoryTransferItems",
                column: "SourceLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferItems_TenantId",
                table: "InventoryTransferItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_ApprovedById",
                table: "InventoryTransfers",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_DestinationLocationId",
                table: "InventoryTransfers",
                column: "DestinationLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_DestinationWarehouseId",
                table: "InventoryTransfers",
                column: "DestinationWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_InTransitLocationId",
                table: "InventoryTransfers",
                column: "InTransitLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_ReceivedById",
                table: "InventoryTransfers",
                column: "ReceivedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_RequestDate",
                table: "InventoryTransfers",
                column: "RequestDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_RequestedById",
                table: "InventoryTransfers",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_ShippedById",
                table: "InventoryTransfers",
                column: "ShippedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_SourceLocationId",
                table: "InventoryTransfers",
                column: "SourceLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_SourceWarehouseId",
                table: "InventoryTransfers",
                column: "SourceWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_Status",
                table: "InventoryTransfers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_TenantId",
                table: "InventoryTransfers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_TransferNumber",
                table: "InventoryTransfers",
                column: "TransferNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemClasses_ClassId",
                table: "ItemClasses",
                column: "ClassId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemClasses_IsActive",
                table: "ItemClasses",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ItemClasses_TenantId",
                table: "ItemClasses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSuppliers_InventoryItemId",
                table: "ItemSuppliers",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSuppliers_TenantId",
                table: "ItemSuppliers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemUnitsOfMeasure_InventoryItemId_UnitOfMeasureId",
                table: "ItemUnitsOfMeasure",
                columns: new[] { "InventoryItemId", "UnitOfMeasureId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemUnitsOfMeasure_IsActive",
                table: "ItemUnitsOfMeasure",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ItemUnitsOfMeasure_TenantId",
                table: "ItemUnitsOfMeasure",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemUnitsOfMeasure_UnitOfMeasureId",
                table: "ItemUnitsOfMeasure",
                column: "UnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostAllocations_CostLayerId",
                table: "LandedCostAllocations",
                column: "CostLayerId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostAllocations_GoodsReceiptNoteItemId",
                table: "LandedCostAllocations",
                column: "GoodsReceiptNoteItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostAllocations_InventoryItemId",
                table: "LandedCostAllocations",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostAllocations_LandedCostId",
                table: "LandedCostAllocations",
                column: "LandedCostId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostAllocations_LandedCostItemId",
                table: "LandedCostAllocations",
                column: "LandedCostItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostAllocations_TenantId",
                table: "LandedCostAllocations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostItems_LandedCostId",
                table: "LandedCostItems",
                column: "LandedCostId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostItems_TenantId",
                table: "LandedCostItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCosts_ApprovedById",
                table: "LandedCosts",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCosts_GoodsReceiptNoteId",
                table: "LandedCosts",
                column: "GoodsReceiptNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCosts_LandedCostNumber",
                table: "LandedCosts",
                column: "LandedCostNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandedCosts_PostedById",
                table: "LandedCosts",
                column: "PostedById");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCosts_Status",
                table: "LandedCosts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCosts_TenantId",
                table: "LandedCosts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAttachmentAccessLogs_AccessedByUserId",
                table: "MaintenanceAttachmentAccessLogs",
                column: "AccessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAttachmentAccessLogs_AttachmentId",
                table: "MaintenanceAttachmentAccessLogs",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSettings_TenantId",
                table: "MaintenanceSettings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketAnalyses_AnalysisPeriodStart",
                table: "MarketAnalyses",
                column: "AnalysisPeriodStart");

            migrationBuilder.CreateIndex(
                name: "IX_MarketAnalyses_ItemCategory",
                table: "MarketAnalyses",
                column: "ItemCategory");

            migrationBuilder.CreateIndex(
                name: "IX_MarketAnalyses_ItemDescription",
                table: "MarketAnalyses",
                column: "ItemDescription");

            migrationBuilder.CreateIndex(
                name: "IX_MarketAnalyses_PreparedById",
                table: "MarketAnalyses",
                column: "PreparedById");

            migrationBuilder.CreateIndex(
                name: "IX_MarketAnalyses_TenantId",
                table: "MarketAnalyses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTopicRecipients_TenantId_TopicId",
                table: "NotificationTopicRecipients",
                columns: new[] { "TenantId", "TopicId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTopicRecipients_TopicId",
                table: "NotificationTopicRecipients",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTopics_EmailTemplateId",
                table: "NotificationTopics",
                column: "EmailTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTopics_TenantId_EntityType",
                table: "NotificationTopics",
                columns: new[] { "TenantId", "EntityType" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTopics_TenantId_Key",
                table: "NotificationTopics",
                columns: new[] { "TenantId", "Key" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_ApplicableTo",
                table: "PaymentTerms",
                column: "ApplicableTo");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_Code",
                table: "PaymentTerms",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_IsActive",
                table: "PaymentTerms",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_IsDefault",
                table: "PaymentTerms",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_TenantId_Code",
                table: "PaymentTerms",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceBondRequests_BusinessPartnerId",
                table: "PerformanceBondRequests",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceBondRequests_RequestedById",
                table: "PerformanceBondRequests",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceBondRequests_ReviewedById",
                table: "PerformanceBondRequests",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceBondRequests_Status",
                table: "PerformanceBondRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceBondRequests_SubmittedById",
                table: "PerformanceBondRequests",
                column: "SubmittedById");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceBondRequests_TenantId",
                table: "PerformanceBondRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceBondRequests_TenderAwardId",
                table: "PerformanceBondRequests",
                column: "TenderAwardId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceBondRequests_TenderBidId",
                table: "PerformanceBondRequests",
                column: "TenderBidId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_AcknowledgedById",
                table: "PerformanceReviews",
                column: "AcknowledgedById");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_BusinessPartnerId",
                table: "PerformanceReviews",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_ReviewDate",
                table: "PerformanceReviews",
                column: "ReviewDate");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_ReviewedById",
                table: "PerformanceReviews",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_ReviewNumber",
                table: "PerformanceReviews",
                column: "ReviewNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_Status",
                table: "PerformanceReviews",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_TenantId",
                table: "PerformanceReviews",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountItems_CountedById",
                table: "PhysicalCountItems",
                column: "CountedById");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountItems_InventoryItemId",
                table: "PhysicalCountItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountItems_LocationId",
                table: "PhysicalCountItems",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountItems_PhysicalCountId",
                table: "PhysicalCountItems",
                column: "PhysicalCountId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountItems_TenantId",
                table: "PhysicalCountItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_ApprovedById",
                table: "PhysicalCounts",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_CategoryId",
                table: "PhysicalCounts",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_CountDate",
                table: "PhysicalCounts",
                column: "CountDate");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_CountedById",
                table: "PhysicalCounts",
                column: "CountedById");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_CountNumber",
                table: "PhysicalCounts",
                column: "CountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_InitiatedById",
                table: "PhysicalCounts",
                column: "InitiatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_LocationId",
                table: "PhysicalCounts",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_PostedById",
                table: "PhysicalCounts",
                column: "PostedById");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_Status",
                table: "PhysicalCounts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_TenantId",
                table: "PhysicalCounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_WarehouseId",
                table: "PhysicalCounts",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_PreInspectionChecklistItems_TemplateId",
                table: "PreInspectionChecklistItems",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PreInspectionChecklistItems_TenantId_TemplateId",
                table: "PreInspectionChecklistItems",
                columns: new[] { "TenantId", "TemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_PreInspectionChecklistTemplates_AssetCategoryId",
                table: "PreInspectionChecklistTemplates",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PreInspectionChecklistTemplates_TenantId_AssetCategoryId",
                table: "PreInspectionChecklistTemplates",
                columns: new[] { "TenantId", "AssetCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_PreInspectionChecklistTemplates_TenantId_IsActive",
                table: "PreInspectionChecklistTemplates",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceGroups_IsActive",
                table: "PriceGroups",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PriceGroups_PriceGroupCode",
                table: "PriceGroups",
                column: "PriceGroupCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceGroups_TenantId",
                table: "PriceGroups",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceHistories_MarketAnalysisId",
                table: "PriceHistories",
                column: "MarketAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceHistories_PriceDate",
                table: "PriceHistories",
                column: "PriceDate");

            migrationBuilder.CreateIndex(
                name: "IX_PriceHistories_SupplierId",
                table: "PriceHistories",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceHistories_TenantId",
                table: "PriceHistories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListChangeHistories_EffectiveDate",
                table: "PriceListChangeHistories",
                column: "EffectiveDate");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListChangeHistories_InventoryItemId",
                table: "PriceListChangeHistories",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListChangeHistories_PriceListLineId",
                table: "PriceListChangeHistories",
                column: "PriceListLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListChangeHistories_TenantId",
                table: "PriceListChangeHistories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListLines_InventoryItemId",
                table: "PriceListLines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListLines_PriceListId",
                table: "PriceListLines",
                column: "PriceListId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListLines_PriceListId_InventoryItemId_MinQuantity",
                table: "PriceListLines",
                columns: new[] { "PriceListId", "InventoryItemId", "MinQuantity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceListLines_TenantId",
                table: "PriceListLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_ApprovalStatus",
                table: "PriceLists",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_PriceListCode",
                table: "PriceLists",
                column: "PriceListCode");

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_Status",
                table: "PriceLists",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_SupersededPriceListId",
                table: "PriceLists",
                column: "SupersededPriceListId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_TenantId",
                table: "PriceLists",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_TenantId_PriceListCode",
                table: "PriceLists",
                columns: new[] { "TenantId", "PriceListCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_Type",
                table: "PriceLists",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetAllocations_CategoryName",
                table: "ProcurementBudgetAllocations",
                column: "CategoryName");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetAllocations_ProcurementBudgetId",
                table: "ProcurementBudgetAllocations",
                column: "ProcurementBudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetAllocations_TenantId",
                table: "ProcurementBudgetAllocations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetRevisions_ApprovedById",
                table: "ProcurementBudgetRevisions",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetRevisions_ProcurementBudgetId",
                table: "ProcurementBudgetRevisions",
                column: "ProcurementBudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetRevisions_RevisionNumber",
                table: "ProcurementBudgetRevisions",
                column: "RevisionNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetRevisions_Status",
                table: "ProcurementBudgetRevisions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetRevisions_TenantId",
                table: "ProcurementBudgetRevisions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgets_ApprovedById",
                table: "ProcurementBudgets",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgets_BudgetCode",
                table: "ProcurementBudgets",
                column: "BudgetCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgets_DepartmentId",
                table: "ProcurementBudgets",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgets_FiscalYear",
                table: "ProcurementBudgets",
                column: "FiscalYear");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgets_ProcurementPlanId",
                table: "ProcurementBudgets",
                column: "ProcurementPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgets_Status",
                table: "ProcurementBudgets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgets_TenantId",
                table: "ProcurementBudgets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlanItems_InventoryItemId",
                table: "ProcurementPlanItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlanItems_IsCritical",
                table: "ProcurementPlanItems",
                column: "IsCritical");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlanItems_ItemCategory",
                table: "ProcurementPlanItems",
                column: "ItemCategory");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlanItems_PreferredSupplierId",
                table: "ProcurementPlanItems",
                column: "PreferredSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlanItems_ProcurementPlanId",
                table: "ProcurementPlanItems",
                column: "ProcurementPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlanItems_TenantId",
                table: "ProcurementPlanItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlanItemSuppliers_ProcurementPlanItemId",
                table: "ProcurementPlanItemSuppliers",
                column: "ProcurementPlanItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlanItemSuppliers_ProcurementPlanItemId_SupplierId",
                table: "ProcurementPlanItemSuppliers",
                columns: new[] { "ProcurementPlanItemId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlanItemSuppliers_SupplierId",
                table: "ProcurementPlanItemSuppliers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlanItemSuppliers_TenantId",
                table: "ProcurementPlanItemSuppliers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlans_ApprovedById",
                table: "ProcurementPlans",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlans_DepartmentId",
                table: "ProcurementPlans",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlans_FiscalYear",
                table: "ProcurementPlans",
                column: "FiscalYear");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlans_PlanNumber",
                table: "ProcurementPlans",
                column: "PlanNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlans_PreparedById",
                table: "ProcurementPlans",
                column: "PreparedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlans_PreviousVersionId",
                table: "ProcurementPlans",
                column: "PreviousVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlans_ReviewedById",
                table: "ProcurementPlans",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlans_Status",
                table: "ProcurementPlans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlans_TenantId",
                table: "ProcurementPlans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSchedules_DepartmentId",
                table: "ProcurementSchedules",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSchedules_PlannedStartDate",
                table: "ProcurementSchedules",
                column: "PlannedStartDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSchedules_ProcurementPlanId",
                table: "ProcurementSchedules",
                column: "ProcurementPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSchedules_ProcurementPlanItemId",
                table: "ProcurementSchedules",
                column: "ProcurementPlanItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSchedules_Status",
                table: "ProcurementSchedules",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSchedules_TenantId",
                table: "ProcurementSchedules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSettings_TenantId",
                table: "ProcurementSettings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_GoodsReceiptNoteItemId",
                table: "PurchaseReturnItems",
                column: "GoodsReceiptNoteItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_InventoryItemId",
                table: "PurchaseReturnItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_LocationId",
                table: "PurchaseReturnItems",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_PurchaseReturnId",
                table: "PurchaseReturnItems",
                column: "PurchaseReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_TenantId",
                table: "PurchaseReturnItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_ApprovedById",
                table: "PurchaseReturns",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_GoodsReceiptNoteId",
                table: "PurchaseReturns",
                column: "GoodsReceiptNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_RequestedById",
                table: "PurchaseReturns",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_ReturnDate",
                table: "PurchaseReturns",
                column: "ReturnDate");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_ReturnNumber",
                table: "PurchaseReturns",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_Status",
                table: "PurchaseReturns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_SupplierId",
                table: "PurchaseReturns",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_TenantId",
                table: "PurchaseReturns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_WarehouseId",
                table: "PurchaseReturns",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityIncidents_AcknowledgedById",
                table: "QualityIncidents",
                column: "AcknowledgedById");

            migrationBuilder.CreateIndex(
                name: "IX_QualityIncidents_BusinessPartnerId",
                table: "QualityIncidents",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityIncidents_IncidentDate",
                table: "QualityIncidents",
                column: "IncidentDate");

            migrationBuilder.CreateIndex(
                name: "IX_QualityIncidents_IncidentNumber",
                table: "QualityIncidents",
                column: "IncidentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QualityIncidents_ReportedById",
                table: "QualityIncidents",
                column: "ReportedById");

            migrationBuilder.CreateIndex(
                name: "IX_QualityIncidents_ResolvedById",
                table: "QualityIncidents",
                column: "ResolvedById");

            migrationBuilder.CreateIndex(
                name: "IX_QualityIncidents_Severity",
                table: "QualityIncidents",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_QualityIncidents_Status",
                table: "QualityIncidents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_QualityIncidents_TenantId",
                table: "QualityIncidents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationAwardLines_BusinessPartnerId",
                table: "RequestForQuotationAwardLines",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationAwardLines_QuoteId",
                table: "RequestForQuotationAwardLines",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationAwardLines_RfqId",
                table: "RequestForQuotationAwardLines",
                column: "RfqId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationAwardLines_RfqItemId",
                table: "RequestForQuotationAwardLines",
                column: "RfqItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationAwardLines_TenantId_RfqId_RfqItemId",
                table: "RequestForQuotationAwardLines",
                columns: new[] { "TenantId", "RfqId", "RfqItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationInvitations_BusinessPartnerId",
                table: "RequestForQuotationInvitations",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationInvitations_RfqId",
                table: "RequestForQuotationInvitations",
                column: "RfqId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationInvitations_TenantId_RfqId_BusinessPartnerId",
                table: "RequestForQuotationInvitations",
                columns: new[] { "TenantId", "RfqId", "BusinessPartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationItems_InventoryItemId",
                table: "RequestForQuotationItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationItems_RfqId",
                table: "RequestForQuotationItems",
                column: "RfqId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationItems_SourcePurchaseRequisitionItemId",
                table: "RequestForQuotationItems",
                column: "SourcePurchaseRequisitionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationItems_TenantId_RfqId_LineNumber",
                table: "RequestForQuotationItems",
                columns: new[] { "TenantId", "RfqId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationQuoteItems_QuoteId",
                table: "RequestForQuotationQuoteItems",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationQuoteItems_RfqItemId",
                table: "RequestForQuotationQuoteItems",
                column: "RfqItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationQuoteItems_TenantId_QuoteId_RfqItemId",
                table: "RequestForQuotationQuoteItems",
                columns: new[] { "TenantId", "QuoteId", "RfqItemId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationQuotes_BusinessPartnerId",
                table: "RequestForQuotationQuotes",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationQuotes_RfqId",
                table: "RequestForQuotationQuotes",
                column: "RfqId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationQuotes_TenantId_RfqId_BusinessPartnerId",
                table: "RequestForQuotationQuotes",
                columns: new[] { "TenantId", "RfqId", "BusinessPartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotations_AwardedBusinessPartnerId",
                table: "RequestForQuotations",
                column: "AwardedBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotations_SourcePurchaseRequisitionId",
                table: "RequestForQuotations",
                column: "SourcePurchaseRequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotations_Status",
                table: "RequestForQuotations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotations_SubmissionDeadline",
                table: "RequestForQuotations",
                column: "SubmissionDeadline");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotations_TenantId_RfqNumber",
                table: "RequestForQuotations",
                columns: new[] { "TenantId", "RfqNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SuggestedSalesItems_InventoryItemId",
                table: "SuggestedSalesItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SuggestedSalesItems_IsActive",
                table: "SuggestedSalesItems",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SuggestedSalesItems_SuggestedItemId",
                table: "SuggestedSalesItems",
                column: "SuggestedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SuggestedSalesItems_TenantId",
                table: "SuggestedSalesItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierConsolidations_ItemCategory",
                table: "SupplierConsolidations",
                column: "ItemCategory");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierConsolidations_PreparedById",
                table: "SupplierConsolidations",
                column: "PreparedById");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierConsolidations_Status",
                table: "SupplierConsolidations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierConsolidations_TenantId",
                table: "SupplierConsolidations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierGroups_DefaultPriceListId",
                table: "SupplierGroups",
                column: "DefaultPriceListId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierGroups_TenantId",
                table: "SupplierGroups",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierGroups_TenantId_GroupCode",
                table: "SupplierGroups",
                columns: new[] { "TenantId", "GroupCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPerformanceMetrics_BusinessPartnerId",
                table: "SupplierPerformanceMetrics",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPerformanceMetrics_BusinessPartnerId_MetricPeriod_Year_Month_Quarter",
                table: "SupplierPerformanceMetrics",
                columns: new[] { "BusinessPartnerId", "MetricPeriod", "Year", "Month", "Quarter" },
                unique: true,
                filter: "[Month] IS NOT NULL AND [Quarter] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPerformanceMetrics_CalculatedById",
                table: "SupplierPerformanceMetrics",
                column: "CalculatedById");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPerformanceMetrics_OverallPerformanceScore",
                table: "SupplierPerformanceMetrics",
                column: "OverallPerformanceScore");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPerformanceMetrics_TenantId",
                table: "SupplierPerformanceMetrics",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPerformanceMetrics_Year",
                table: "SupplierPerformanceMetrics",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_SystemExceptionLogs_TenantId_CreatedAt",
                table: "SystemExceptionLogs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemExceptionLogs_TenantId_Fingerprint",
                table: "SystemExceptionLogs",
                columns: new[] { "TenantId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemExceptionLogs_TenantId_IsResolved_CreatedAt",
                table: "SystemExceptionLogs",
                columns: new[] { "TenantId", "IsResolved", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemExceptionLogs_TenantId_LastOccurredAt",
                table: "SystemExceptionLogs",
                columns: new[] { "TenantId", "LastOccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemExceptionLogs_TenantId_Level_CreatedAt",
                table: "SystemExceptionLogs",
                columns: new[] { "TenantId", "Level", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemExceptionLogs_TraceId",
                table: "SystemExceptionLogs",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemExceptionLogs_UserId",
                table: "SystemExceptionLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAssignments_AssignedById",
                table: "TenderAssignments",
                column: "AssignedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAssignments_AssignedToUserId",
                table: "TenderAssignments",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAssignments_AssignmentType",
                table: "TenderAssignments",
                column: "AssignmentType");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAssignments_BusinessPartnerId",
                table: "TenderAssignments",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAssignments_TenantId",
                table: "TenderAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAssignments_TenderId",
                table: "TenderAssignments",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAssignments_TenderId_BusinessPartnerId_AssignedToUserId",
                table: "TenderAssignments",
                columns: new[] { "TenderId", "BusinessPartnerId", "AssignedToUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwards_AwardedById",
                table: "TenderAwards",
                column: "AwardedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwards_BidLotId",
                table: "TenderAwards",
                column: "BidLotId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwards_BusinessPartnerId",
                table: "TenderAwards",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwards_LotId",
                table: "TenderAwards",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwards_NegotiationId",
                table: "TenderAwards",
                column: "NegotiationId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwards_Status",
                table: "TenderAwards",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwards_TenantId",
                table: "TenderAwards",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwards_TenderBidId",
                table: "TenderAwards",
                column: "TenderBidId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwards_TenderId",
                table: "TenderAwards",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationBidders_BusinessPartnerId",
                table: "TenderAwardVerificationBidders",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationBidders_TenantId",
                table: "TenderAwardVerificationBidders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationBidders_TenderBidId",
                table: "TenderAwardVerificationBidders",
                column: "TenderBidId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationBidders_VerificationId",
                table: "TenderAwardVerificationBidders",
                column: "VerificationId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationBidders_VerifiedById",
                table: "TenderAwardVerificationBidders",
                column: "VerifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationItemDocuments_ItemResultId",
                table: "TenderAwardVerificationItemDocuments",
                column: "ItemResultId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationItemDocuments_TenantId",
                table: "TenderAwardVerificationItemDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationItemDocuments_UploadedById",
                table: "TenderAwardVerificationItemDocuments",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationItemResults_BidderId",
                table: "TenderAwardVerificationItemResults",
                column: "BidderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationItemResults_BidderId_ChecklistItemId",
                table: "TenderAwardVerificationItemResults",
                columns: new[] { "BidderId", "ChecklistItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationItemResults_ChecklistItemId",
                table: "TenderAwardVerificationItemResults",
                column: "ChecklistItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationItemResults_TenantId",
                table: "TenderAwardVerificationItemResults",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationItemResults_VerifiedById",
                table: "TenderAwardVerificationItemResults",
                column: "VerifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerifications_CompletedById",
                table: "TenderAwardVerifications",
                column: "CompletedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerifications_StartedById",
                table: "TenderAwardVerifications",
                column: "StartedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerifications_Status",
                table: "TenderAwardVerifications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerifications_TemplateId",
                table: "TenderAwardVerifications",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerifications_TenantId",
                table: "TenderAwardVerifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerifications_TenderId",
                table: "TenderAwardVerifications",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidDocuments_TenantId",
                table: "TenderBidDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidDocuments_TenderBidId",
                table: "TenderBidDocuments",
                column: "TenderBidId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidDocuments_UploadedById",
                table: "TenderBidDocuments",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidItems_BidLotId",
                table: "TenderBidItems",
                column: "BidLotId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidItems_TenantId",
                table: "TenderBidItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidItems_TenderBidId",
                table: "TenderBidItems",
                column: "TenderBidId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidItems_TenderItemId",
                table: "TenderBidItems",
                column: "TenderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidLots_LotId",
                table: "TenderBidLots",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidLots_Status",
                table: "TenderBidLots",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidLots_TenantId",
                table: "TenderBidLots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidLots_TenderBidId",
                table: "TenderBidLots",
                column: "TenderBidId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidLots_TenderBidId_LotId",
                table: "TenderBidLots",
                columns: new[] { "TenderBidId", "LotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenderBids_BusinessPartnerId",
                table: "TenderBids",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBids_EvaluatedById",
                table: "TenderBids",
                column: "EvaluatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBids_OpenedById",
                table: "TenderBids",
                column: "OpenedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBids_Status",
                table: "TenderBids",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBids_TenantId_BidNumber",
                table: "TenderBids",
                columns: new[] { "TenantId", "BidNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenderBids_TenderId",
                table: "TenderBids",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderClarifications_AnsweredById",
                table: "TenderClarifications",
                column: "AnsweredById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderClarifications_BusinessPartnerId",
                table: "TenderClarifications",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderClarifications_QuestionById",
                table: "TenderClarifications",
                column: "QuestionById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderClarifications_TenantId",
                table: "TenderClarifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderClarifications_TenderId",
                table: "TenderClarifications",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderDocuments_TenantId",
                table: "TenderDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderDocuments_TenderId",
                table: "TenderDocuments",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderDocuments_UploadedById",
                table: "TenderDocuments",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderDocumentTypes_CreatedById",
                table: "TenderDocumentTypes",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderDocumentTypes_TenantId",
                table: "TenderDocumentTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderEvaluations_TenantId",
                table: "TenderEvaluations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderEvaluations_TenderBidId",
                table: "TenderEvaluations",
                column: "TenderBidId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderEvaluations_TenderEvaluatorId",
                table: "TenderEvaluations",
                column: "TenderEvaluatorId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderEvaluators_AssignedById",
                table: "TenderEvaluators",
                column: "AssignedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderEvaluators_TenantId",
                table: "TenderEvaluators",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderEvaluators_TenderId",
                table: "TenderEvaluators",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderEvaluators_UserId",
                table: "TenderEvaluators",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderFees_TenantId",
                table: "TenderFees",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderFees_TenderId",
                table: "TenderFees",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderInterviews_ConductedById",
                table: "TenderInterviews",
                column: "ConductedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderInterviews_TenantId",
                table: "TenderInterviews",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderInterviews_TenderBidId",
                table: "TenderInterviews",
                column: "TenderBidId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderInterviews_TenderId",
                table: "TenderInterviews",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderInvitations_BusinessPartnerId",
                table: "TenderInvitations",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderInvitations_InvitedById",
                table: "TenderInvitations",
                column: "InvitedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderInvitations_TenantId",
                table: "TenderInvitations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderInvitations_TenderId",
                table: "TenderInvitations",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderItems_LotId",
                table: "TenderItems",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderItems_TenantId",
                table: "TenderItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderItems_TenderId",
                table: "TenderItems",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderLots_Status",
                table: "TenderLots",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TenderLots_TenantId",
                table: "TenderLots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderLots_TenderId",
                table: "TenderLots",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderLots_TenderId_LotCode",
                table: "TenderLots",
                columns: new[] { "TenderId", "LotCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenderNegotiationItems_NegotiationId",
                table: "TenderNegotiationItems",
                column: "NegotiationId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderNegotiationItems_TenantId",
                table: "TenderNegotiationItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderNegotiationItems_TenderBidItemId",
                table: "TenderNegotiationItems",
                column: "TenderBidItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderNegotiations_BidLotId",
                table: "TenderNegotiations",
                column: "BidLotId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderNegotiations_BusinessPartnerId",
                table: "TenderNegotiations",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderNegotiations_CompletedById",
                table: "TenderNegotiations",
                column: "CompletedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderNegotiations_InvitedById",
                table: "TenderNegotiations",
                column: "InvitedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderNegotiations_LotId",
                table: "TenderNegotiations",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderNegotiations_TenantId",
                table: "TenderNegotiations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderNegotiations_TenderBidId",
                table: "TenderNegotiations",
                column: "TenderBidId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderNegotiations_TenderId",
                table: "TenderNegotiations",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderPayments_BusinessPartnerId",
                table: "TenderPayments",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderPayments_TenantId",
                table: "TenderPayments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderPayments_TenderFeeId",
                table: "TenderPayments",
                column: "TenderFeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderPayments_VerifiedById",
                table: "TenderPayments",
                column: "VerifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderRevisions_RevisedById",
                table: "TenderRevisions",
                column: "RevisedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderRevisions_TenantId",
                table: "TenderRevisions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderRevisions_TenderId",
                table: "TenderRevisions",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenders_AwardedById",
                table: "Tenders",
                column: "AwardedById");

            migrationBuilder.CreateIndex(
                name: "IX_Tenders_CreatedById",
                table: "Tenders",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Tenders_EvaluationTemplateId",
                table: "Tenders",
                column: "EvaluationTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenders_PublishDate",
                table: "Tenders",
                column: "PublishDate");

            migrationBuilder.CreateIndex(
                name: "IX_Tenders_PublishedById",
                table: "Tenders",
                column: "PublishedById");

            migrationBuilder.CreateIndex(
                name: "IX_Tenders_Status",
                table: "Tenders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Tenders_SubmissionDeadline",
                table: "Tenders",
                column: "SubmissionDeadline");

            migrationBuilder.CreateIndex(
                name: "IX_Tenders_TenantId_TenderNumber",
                table: "Tenders",
                columns: new[] { "TenantId", "TenderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenderTemplates_CreatedById",
                table: "TenderTemplates",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderTemplates_TenantId",
                table: "TenderTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderViewLogs_BusinessPartnerId",
                table: "TenderViewLogs",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderViewLogs_TenantId",
                table: "TenderViewLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderViewLogs_TenderId",
                table: "TenderViewLogs",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderViewLogs_UserId",
                table: "TenderViewLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureConversions_FromUnitId_ToUnitId",
                table: "UnitOfMeasureConversions",
                columns: new[] { "FromUnitId", "ToUnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureConversions_IsActive",
                table: "UnitOfMeasureConversions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureConversions_TenantId",
                table: "UnitOfMeasureConversions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureConversions_ToUnitId",
                table: "UnitOfMeasureConversions",
                column: "ToUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureScheduleDetails_ScheduleId",
                table: "UnitOfMeasureScheduleDetails",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureScheduleDetails_TenantId",
                table: "UnitOfMeasureScheduleDetails",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureScheduleDetails_UnitOfMeasureId",
                table: "UnitOfMeasureScheduleDetails",
                column: "UnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureSchedules_BaseUnitOfMeasureId",
                table: "UnitOfMeasureSchedules",
                column: "BaseUnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureSchedules_IsActive",
                table: "UnitOfMeasureSchedules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureSchedules_ScheduleId",
                table: "UnitOfMeasureSchedules",
                column: "ScheduleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureSchedules_TenantId",
                table: "UnitOfMeasureSchedules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_Category",
                table: "UnitsOfMeasure",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_Code",
                table: "UnitsOfMeasure",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_IsActive",
                table: "UnitsOfMeasure",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_TenantId",
                table: "UnitsOfMeasure",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartners_BusinessPartners_ParentId",
                table: "BusinessPartners",
                column: "ParentId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartners_Users_SalesRepresentativeId",
                table: "BusinessPartners",
                column: "SalesRepresentativeId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartners_Users_UserId",
                table: "BusinessPartners",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_InventoryCategories_SubCategoryId",
                table: "InventoryItems",
                column: "SubCategoryId",
                principalTable: "InventoryCategories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_InventoryItems_SubstituteItem1Id",
                table: "InventoryItems",
                column: "SubstituteItem1Id",
                principalTable: "InventoryItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_InventoryItems_SubstituteItem2Id",
                table: "InventoryItems",
                column: "SubstituteItem2Id",
                principalTable: "InventoryItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_InventoryItems_SubstituteItem3Id",
                table: "InventoryItems",
                column: "SubstituteItem3Id",
                principalTable: "InventoryItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_InventoryItems_SubstituteItem4Id",
                table: "InventoryItems",
                column: "SubstituteItem4Id",
                principalTable: "InventoryItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_ItemClasses_ItemClassId",
                table: "InventoryItems",
                column: "ItemClassId",
                principalTable: "ItemClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_PriceGroups_PriceGroupId",
                table: "InventoryItems",
                column: "PriceGroupId",
                principalTable: "PriceGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_UnitOfMeasureSchedules_UnitOfMeasureScheduleId",
                table: "InventoryItems",
                column: "UnitOfMeasureScheduleId",
                principalTable: "UnitOfMeasureSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_UnitsOfMeasure_BaseUnitOfMeasureEntityId",
                table: "InventoryItems",
                column: "BaseUnitOfMeasureEntityId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_Warehouses_DefaultWarehouseId",
                table: "InventoryItems",
                column: "DefaultWarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItems_InventoryItems_InventoryItemId",
                table: "PurchaseOrderItems",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItems_ItemUnitsOfMeasure_ItemUnitOfMeasureId",
                table: "PurchaseOrderItems",
                column: "ItemUnitOfMeasureId",
                principalTable: "ItemUnitsOfMeasure",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItems_Warehouses_WarehouseId",
                table: "PurchaseOrderItems",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_BusinessPartners_BusinessPartnerId",
                table: "PurchaseOrders",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_PurchaseRequisitions_SourceRequisitionId",
                table: "PurchaseOrders",
                column: "SourceRequisitionId",
                principalTable: "PurchaseRequisitions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_TenderAwards_TenderAwardId1",
                table: "PurchaseOrders",
                column: "TenderAwardId1",
                principalTable: "TenderAwards",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Users_LastAmendedById",
                table: "PurchaseOrders",
                column: "LastAmendedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitionItems_BusinessPartners_PreferredBusinessPartnerId",
                table: "PurchaseRequisitionItems",
                column: "PreferredBusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_Users_CurrentApproverId",
                table: "PurchaseRequisitions",
                column: "CurrentApproverId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_Users_LastAmendedById",
                table: "PurchaseRequisitions",
                column: "LastAmendedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QualityControlChecklists_MaintenanceAssetCategories_AssetCategoryId",
                table: "QualityControlChecklists",
                column: "AssetCategoryId",
                principalTable: "MaintenanceAssetCategories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QualityControlChecklists_MaintenanceTypes_MaintenanceTypeId",
                table: "QualityControlChecklists",
                column: "MaintenanceTypeId",
                principalTable: "MaintenanceTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QualityControlChecklists_WorkOrderTypes_WorkOrderTypeId",
                table: "QualityControlChecklists",
                column: "WorkOrderTypeId",
                principalTable: "WorkOrderTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StockAdjustments_Warehouses_WarehouseId",
                table: "StockAdjustments",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Warehouses_WarehouseId",
                table: "StockMovements",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseLocations_InventoryItems_DedicatedItemId",
                table: "WarehouseLocations",
                column: "DedicatedItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_Users_ManagerId",
                table: "Warehouses",
                column: "ManagerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_WarehouseLocations_DefaultInTransitLocationId",
                table: "Warehouses",
                column: "DefaultInTransitLocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_WarehouseLocations_DefaultQuarantineLocationId",
                table: "Warehouses",
                column: "DefaultQuarantineLocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_WarehouseLocations_DefaultReceivingLocationId",
                table: "Warehouses",
                column: "DefaultReceivingLocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_WarehouseLocations_DefaultShippingLocationId",
                table: "Warehouses",
                column: "DefaultShippingLocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Users_ApprovedById",
                table: "WorkOrders",
                column: "ApprovedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Users_CompletedById",
                table: "WorkOrders",
                column: "CompletedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Users_QualityCheckedById",
                table: "WorkOrders",
                column: "QualityCheckedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Users_RequestedById",
                table: "WorkOrders",
                column: "RequestedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Users_SupervisorId",
                table: "WorkOrders",
                column: "SupervisorId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
