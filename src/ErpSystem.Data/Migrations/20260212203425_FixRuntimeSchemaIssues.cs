using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixRuntimeSchemaIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartners_BusinessPartners_ParentId",
                table: "BusinessPartners");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartnerUsers_Users_UserId",
                table: "BusinessPartnerUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_BusinessPartners_BusinessPartnerId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_TenderAwards_TenderAwardId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Tenders_TenderId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationCriteria_Tenants_TenantId",
                table: "EvaluationCriteria");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationCriteria_Users_CreatedById",
                table: "EvaluationCriteria");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationTemplateCriteria_EvaluationCriteria_EvaluationCriterionId",
                table: "EvaluationTemplateCriteria");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationTemplateCriteria_EvaluationTemplates_EvaluationTemplateId",
                table: "EvaluationTemplateCriteria");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationTemplateCriteria_Tenants_TenantId",
                table: "EvaluationTemplateCriteria");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationTemplates_Tenants_TenantId",
                table: "EvaluationTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationTemplates_Users_CreatedById",
                table: "EvaluationTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_FleetBatteryEvents_MaintenanceAssets_VehicleAssetId",
                table: "FleetBatteryEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_FleetComplianceItems_MaintenanceAssets_VehicleAssetId",
                table: "FleetComplianceItems");

            migrationBuilder.DropForeignKey(
                name: "FK_FleetCostEntries_FleetIncidents_FleetIncidentId",
                table: "FleetCostEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_FleetFuelTransactions_FleetTrips_FleetTripId",
                table: "FleetFuelTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_FleetFuelTransactions_MaintenanceAssets_VehicleAssetId",
                table: "FleetFuelTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_FleetTrips_Employees_DriverEmployeeId",
                table: "FleetTrips");

            migrationBuilder.DropForeignKey(
                name: "FK_FleetTrips_MaintenanceAssets_VehicleAssetId",
                table: "FleetTrips");

            migrationBuilder.DropForeignKey(
                name: "FK_FleetTyreEvents_MaintenanceAssets_VehicleAssetId",
                table: "FleetTyreEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryBalances_InventoryItems_InventoryItemId",
                table: "InventoryBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryBalances_Warehouses_WarehouseId",
                table: "InventoryBalances");

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
                name: "FK_InventoryLayers_InventoryItems_InventoryItemId",
                table: "InventoryLayers");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryLayers_Warehouses_WarehouseId",
                table: "InventoryLayers");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_InventoryItems_InventoryItemId",
                table: "InventoryMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_Warehouses_WarehouseId",
                table: "InventoryMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransferItems_InventoryItems_InventoryItemId",
                table: "InventoryTransferItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransferItems_InventoryTransfers_InventoryTransferId",
                table: "InventoryTransferItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransferItems_Tenants_TenantId",
                table: "InventoryTransferItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransferItems_WarehouseLocations_DestinationLocationId",
                table: "InventoryTransferItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransferItems_WarehouseLocations_SourceLocationId",
                table: "InventoryTransferItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransfers_Warehouses_DestinationWarehouseId",
                table: "InventoryTransfers");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransfers_Warehouses_SourceWarehouseId",
                table: "InventoryTransfers");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemUnitsOfMeasure_InventoryItems_InventoryItemId",
                table: "ItemUnitsOfMeasure");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemUnitsOfMeasure_Tenants_TenantId",
                table: "ItemUnitsOfMeasure");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemUnitsOfMeasure_UnitsOfMeasure_UnitOfMeasureId",
                table: "ItemUnitsOfMeasure");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationTopics_EmailTemplates_EmailTemplateId",
                table: "NotificationTopics");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceBondRequests_BusinessPartners_BusinessPartnerId",
                table: "PerformanceBondRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceBondRequests_TenderBids_TenderBidId",
                table: "PerformanceBondRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceReviews_Users_ReviewedById",
                table: "PerformanceReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_PriceHistories_MarketAnalyses_MarketAnalysisId",
                table: "PriceHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementBudgets_Departments_DepartmentId",
                table: "ProcurementBudgets");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementPlanItemSuppliers_BusinessPartners_SupplierId",
                table: "ProcurementPlanItemSuppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementPlans_Departments_DepartmentId",
                table: "ProcurementPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementSchedules_Departments_DepartmentId",
                table: "ProcurementSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementSchedules_ProcurementPlans_ProcurementPlanId",
                table: "ProcurementSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderItems_ItemUnitsOfMeasure_ItemUnitOfMeasureId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestForQuotationAwardLines_BusinessPartners_BusinessPartnerId",
                table: "RequestForQuotationAwardLines");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestForQuotationAwardLines_RequestForQuotationItems_RfqItemId",
                table: "RequestForQuotationAwardLines");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestForQuotationAwardLines_RequestForQuotationQuotes_QuoteId",
                table: "RequestForQuotationAwardLines");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestForQuotationInvitations_BusinessPartners_BusinessPartnerId",
                table: "RequestForQuotationInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestForQuotationQuoteItems_RequestForQuotationItems_RfqItemId",
                table: "RequestForQuotationQuoteItems");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestForQuotationQuotes_BusinessPartners_BusinessPartnerId",
                table: "RequestForQuotationQuotes");

            migrationBuilder.DropForeignKey(
                name: "FK_StockAdjustments_Warehouses_WarehouseId",
                table: "StockAdjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Warehouses_WarehouseId",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAssignments_Users_AssignedById",
                table: "TenderAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwards_BusinessPartners_BusinessPartnerId",
                table: "TenderAwards");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwards_TenderBidLots_BidLotId",
                table: "TenderAwards");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwards_TenderBids_TenderBidId",
                table: "TenderAwards");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwards_TenderLots_LotId",
                table: "TenderAwards");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwards_TenderNegotiations_NegotiationId",
                table: "TenderAwards");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwards_Tenders_TenderId",
                table: "TenderAwards");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwardVerificationBidders_BusinessPartners_BusinessPartnerId",
                table: "TenderAwardVerificationBidders");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwardVerificationBidders_TenderBids_TenderBidId",
                table: "TenderAwardVerificationBidders");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwardVerificationItemDocuments_Users_UploadedById",
                table: "TenderAwardVerificationItemDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwardVerificationItemResults_AwardVerificationChecklistItems_ChecklistItemId",
                table: "TenderAwardVerificationItemResults");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwardVerifications_AwardVerificationChecklistTemplates_TemplateId",
                table: "TenderAwardVerifications");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwardVerifications_Tenders_TenderId",
                table: "TenderAwardVerifications");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidDocuments_Tenants_TenantId",
                table: "TenderBidDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidDocuments_TenderBids_TenderBidId",
                table: "TenderBidDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidDocuments_Users_UploadedById",
                table: "TenderBidDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidItems_Tenants_TenantId",
                table: "TenderBidItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidItems_TenderBidLots_BidLotId",
                table: "TenderBidItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidItems_TenderBids_TenderBidId",
                table: "TenderBidItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidItems_TenderItems_TenderItemId",
                table: "TenderBidItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidLots_Tenants_TenantId",
                table: "TenderBidLots");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidLots_TenderBids_TenderBidId",
                table: "TenderBidLots");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidLots_TenderLots_LotId",
                table: "TenderBidLots");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBids_BusinessPartners_BusinessPartnerId",
                table: "TenderBids");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBids_Tenders_TenderId",
                table: "TenderBids");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderClarifications_BusinessPartners_BusinessPartnerId",
                table: "TenderClarifications");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderClarifications_Tenants_TenantId",
                table: "TenderClarifications");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderClarifications_Tenders_TenderId",
                table: "TenderClarifications");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderClarifications_Users_AnsweredById",
                table: "TenderClarifications");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderClarifications_Users_QuestionById",
                table: "TenderClarifications");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderDocuments_Tenants_TenantId",
                table: "TenderDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderDocuments_Tenders_TenderId",
                table: "TenderDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderDocuments_Users_UploadedById",
                table: "TenderDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderEvaluations_Tenants_TenantId",
                table: "TenderEvaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderEvaluations_TenderBids_TenderBidId",
                table: "TenderEvaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderEvaluations_TenderEvaluators_TenderEvaluatorId",
                table: "TenderEvaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderEvaluators_Tenants_TenantId",
                table: "TenderEvaluators");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderEvaluators_Tenders_TenderId",
                table: "TenderEvaluators");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderEvaluators_Users_AssignedById",
                table: "TenderEvaluators");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderEvaluators_Users_UserId",
                table: "TenderEvaluators");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderInterviews_Tenants_TenantId",
                table: "TenderInterviews");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderInterviews_TenderBids_TenderBidId",
                table: "TenderInterviews");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderInterviews_Tenders_TenderId",
                table: "TenderInterviews");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderInterviews_Users_ConductedById",
                table: "TenderInterviews");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderInvitations_BusinessPartners_BusinessPartnerId",
                table: "TenderInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderInvitations_Tenants_TenantId",
                table: "TenderInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderInvitations_Tenders_TenderId",
                table: "TenderInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderInvitations_Users_InvitedById",
                table: "TenderInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderItems_TenderLots_LotId",
                table: "TenderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderNegotiationItems_TenderBidItems_TenderBidItemId",
                table: "TenderNegotiationItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderNegotiations_TenderBidLots_BidLotId",
                table: "TenderNegotiations");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderRevisions_Tenants_TenantId",
                table: "TenderRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderRevisions_Tenders_TenderId",
                table: "TenderRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderRevisions_Users_RevisedById",
                table: "TenderRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_Tenders_EvaluationTemplates_EvaluationTemplateId",
                table: "Tenders");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasureConversions_Tenants_TenantId",
                table: "UnitOfMeasureConversions");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasureConversions_UnitsOfMeasure_FromUnitId",
                table: "UnitOfMeasureConversions");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasureConversions_UnitsOfMeasure_ToUnitId",
                table: "UnitOfMeasureConversions");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasureScheduleDetails_Tenants_TenantId",
                table: "UnitOfMeasureScheduleDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasureScheduleDetails_UnitOfMeasureSchedules_ScheduleId",
                table: "UnitOfMeasureScheduleDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasureScheduleDetails_UnitsOfMeasure_UnitOfMeasureId",
                table: "UnitOfMeasureScheduleDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasureSchedules_Tenants_TenantId",
                table: "UnitOfMeasureSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasureSchedules_UnitsOfMeasure_BaseUnitOfMeasureId",
                table: "UnitOfMeasureSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitsOfMeasure_Tenants_TenantId",
                table: "UnitsOfMeasure");

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

            migrationBuilder.DropTable(
                name: "AssetConditionItemResults");

            migrationBuilder.DropTable(
                name: "ConsignmentSettlements");

            migrationBuilder.DropTable(
                name: "CustomerGroups");

            migrationBuilder.DropTable(
                name: "InventoryRequisitionItems");

            migrationBuilder.DropTable(
                name: "ItemClasses");

            migrationBuilder.DropTable(
                name: "ItemSuppliers");

            migrationBuilder.DropTable(
                name: "LandedCostAllocations");

            migrationBuilder.DropTable(
                name: "MaintenanceAttachmentAccessLogs");

            migrationBuilder.DropTable(
                name: "PaymentTerms");

            migrationBuilder.DropTable(
                name: "PhysicalCountItems");

            migrationBuilder.DropTable(
                name: "PriceGroups");

            migrationBuilder.DropTable(
                name: "PriceListChangeHistories");

            migrationBuilder.DropTable(
                name: "PriceListLines");

            migrationBuilder.DropTable(
                name: "PurchaseReturnItems");

            migrationBuilder.DropTable(
                name: "SuggestedSalesItems");

            migrationBuilder.DropTable(
                name: "SupplierGroups");

            migrationBuilder.DropTable(
                name: "TenderDocumentTypes");

            migrationBuilder.DropTable(
                name: "TenderTemplates");

            migrationBuilder.DropTable(
                name: "AssetConditionRecords");

            migrationBuilder.DropTable(
                name: "PreInspectionChecklistItems");

            migrationBuilder.DropTable(
                name: "InventoryRequisitions");

            migrationBuilder.DropTable(
                name: "InventoryCostLayers");

            migrationBuilder.DropTable(
                name: "LandedCostItems");

            migrationBuilder.DropTable(
                name: "PhysicalCounts");

            migrationBuilder.DropTable(
                name: "GoodsReceiptNoteItems");

            migrationBuilder.DropTable(
                name: "PurchaseReturns");

            migrationBuilder.DropTable(
                name: "PriceLists");

            migrationBuilder.DropTable(
                name: "PreInspectionChecklistTemplates");

            migrationBuilder.DropTable(
                name: "LandedCosts");

            migrationBuilder.DropTable(
                name: "GoodsReceiptNotes");

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
                name: "IX_Tenders_PublishDate",
                table: "Tenders");

            migrationBuilder.DropIndex(
                name: "IX_Tenders_Status",
                table: "Tenders");

            migrationBuilder.DropIndex(
                name: "IX_Tenders_SubmissionDeadline",
                table: "Tenders");

            migrationBuilder.DropIndex(
                name: "IX_Tenders_TenantId_TenderNumber",
                table: "Tenders");

            migrationBuilder.DropIndex(
                name: "IX_TenderLots_Status",
                table: "TenderLots");

            migrationBuilder.DropIndex(
                name: "IX_TenderLots_TenderId_LotCode",
                table: "TenderLots");

            migrationBuilder.DropIndex(
                name: "IX_TenderBids_Status",
                table: "TenderBids");

            migrationBuilder.DropIndex(
                name: "IX_TenderBids_TenantId_BidNumber",
                table: "TenderBids");

            migrationBuilder.DropIndex(
                name: "IX_TenderAwardVerifications_Status",
                table: "TenderAwardVerifications");

            migrationBuilder.DropIndex(
                name: "IX_TenderAwardVerificationItemResults_BidderId_ChecklistItemId",
                table: "TenderAwardVerificationItemResults");

            migrationBuilder.DropIndex(
                name: "IX_TenderAwards_Status",
                table: "TenderAwards");

            migrationBuilder.DropIndex(
                name: "IX_TenderAssignments_AssignmentType",
                table: "TenderAssignments");

            migrationBuilder.DropIndex(
                name: "IX_TenderAssignments_TenderId_BusinessPartnerId_AssignedToUserId",
                table: "TenderAssignments");

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
                name: "IX_SystemExceptionLogs_TenantId_CreatedAt",
                table: "SystemExceptionLogs");

            migrationBuilder.DropIndex(
                name: "IX_SystemExceptionLogs_TenantId_Fingerprint",
                table: "SystemExceptionLogs");

            migrationBuilder.DropIndex(
                name: "IX_SystemExceptionLogs_TenantId_IsResolved_CreatedAt",
                table: "SystemExceptionLogs");

            migrationBuilder.DropIndex(
                name: "IX_SystemExceptionLogs_TenantId_LastOccurredAt",
                table: "SystemExceptionLogs");

            migrationBuilder.DropIndex(
                name: "IX_SystemExceptionLogs_TenantId_Level_CreatedAt",
                table: "SystemExceptionLogs");

            migrationBuilder.DropIndex(
                name: "IX_SystemExceptionLogs_TraceId",
                table: "SystemExceptionLogs");

            migrationBuilder.DropIndex(
                name: "IX_SystemExceptionLogs_UserId",
                table: "SystemExceptionLogs");

            migrationBuilder.DropIndex(
                name: "IX_SupplierPerformanceMetrics_BusinessPartnerId_MetricPeriod_Year_Month_Quarter",
                table: "SupplierPerformanceMetrics");

            migrationBuilder.DropIndex(
                name: "IX_SupplierPerformanceMetrics_OverallPerformanceScore",
                table: "SupplierPerformanceMetrics");

            migrationBuilder.DropIndex(
                name: "IX_SupplierPerformanceMetrics_Year",
                table: "SupplierPerformanceMetrics");

            migrationBuilder.DropIndex(
                name: "IX_SupplierConsolidations_ItemCategory",
                table: "SupplierConsolidations");

            migrationBuilder.DropIndex(
                name: "IX_SupplierConsolidations_Status",
                table: "SupplierConsolidations");

            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotations_Status",
                table: "RequestForQuotations");

            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotations_SubmissionDeadline",
                table: "RequestForQuotations");

            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotations_TenantId_RfqNumber",
                table: "RequestForQuotations");

            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotationQuotes_TenantId_RfqId_BusinessPartnerId",
                table: "RequestForQuotationQuotes");

            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotationQuoteItems_TenantId_QuoteId_RfqItemId",
                table: "RequestForQuotationQuoteItems");

            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotationItems_SourcePurchaseRequisitionItemId",
                table: "RequestForQuotationItems");

            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotationItems_TenantId_RfqId_LineNumber",
                table: "RequestForQuotationItems");

            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotationInvitations_TenantId_RfqId_BusinessPartnerId",
                table: "RequestForQuotationInvitations");

            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotationAwardLines_TenantId_RfqId_RfqItemId",
                table: "RequestForQuotationAwardLines");

            migrationBuilder.DropIndex(
                name: "IX_QualityIncidents_IncidentDate",
                table: "QualityIncidents");

            migrationBuilder.DropIndex(
                name: "IX_QualityIncidents_IncidentNumber",
                table: "QualityIncidents");

            migrationBuilder.DropIndex(
                name: "IX_QualityIncidents_Severity",
                table: "QualityIncidents");

            migrationBuilder.DropIndex(
                name: "IX_QualityIncidents_Status",
                table: "QualityIncidents");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementSchedules_PlannedStartDate",
                table: "ProcurementSchedules");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementSchedules_Status",
                table: "ProcurementSchedules");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementPlans_FiscalYear",
                table: "ProcurementPlans");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementPlans_PlanNumber",
                table: "ProcurementPlans");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementPlans_Status",
                table: "ProcurementPlans");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementPlanItemSuppliers_ProcurementPlanItemId_SupplierId",
                table: "ProcurementPlanItemSuppliers");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementPlanItemSuppliers_SupplierId",
                table: "ProcurementPlanItemSuppliers");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementPlanItems_IsCritical",
                table: "ProcurementPlanItems");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementPlanItems_ItemCategory",
                table: "ProcurementPlanItems");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementBudgets_BudgetCode",
                table: "ProcurementBudgets");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementBudgets_FiscalYear",
                table: "ProcurementBudgets");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementBudgets_Status",
                table: "ProcurementBudgets");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementBudgetRevisions_RevisionNumber",
                table: "ProcurementBudgetRevisions");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementBudgetRevisions_Status",
                table: "ProcurementBudgetRevisions");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementBudgetAllocations_CategoryName",
                table: "ProcurementBudgetAllocations");

            migrationBuilder.DropIndex(
                name: "IX_PriceHistories_PriceDate",
                table: "PriceHistories");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceReviews_ReviewDate",
                table: "PerformanceReviews");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceReviews_ReviewNumber",
                table: "PerformanceReviews");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceReviews_Status",
                table: "PerformanceReviews");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceBondRequests_Status",
                table: "PerformanceBondRequests");

            migrationBuilder.DropIndex(
                name: "IX_NotificationTopics_TenantId_EntityType",
                table: "NotificationTopics");

            migrationBuilder.DropIndex(
                name: "IX_NotificationTopics_TenantId_Key",
                table: "NotificationTopics");

            migrationBuilder.DropIndex(
                name: "IX_NotificationTopicRecipients_TenantId_TopicId",
                table: "NotificationTopicRecipients");

            migrationBuilder.DropIndex(
                name: "IX_MarketAnalyses_AnalysisPeriodStart",
                table: "MarketAnalyses");

            migrationBuilder.DropIndex(
                name: "IX_MarketAnalyses_ItemCategory",
                table: "MarketAnalyses");

            migrationBuilder.DropIndex(
                name: "IX_MarketAnalyses_ItemDescription",
                table: "MarketAnalyses");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransfers_RequestDate",
                table: "InventoryTransfers");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransfers_Status",
                table: "InventoryTransfers");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransfers_TransferNumber",
                table: "InventoryTransfers");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_IsPosted",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_Item_Warehouse_Date",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_MovementDate",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_MovementType",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_PostingDate",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_ReferenceId",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_ReferenceType",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_TenantId_MovementNumber",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryLayers_FIFO_Consumption",
                table: "InventoryLayers");

            migrationBuilder.DropIndex(
                name: "IX_InventoryLayers_IsActive",
                table: "InventoryLayers");

            migrationBuilder.DropIndex(
                name: "IX_InventoryLayers_IsFullyConsumed",
                table: "InventoryLayers");

            migrationBuilder.DropIndex(
                name: "IX_InventoryLayers_LayerDate",
                table: "InventoryLayers");

            migrationBuilder.DropIndex(
                name: "IX_InventoryLayers_TenantId_LayerNumber",
                table: "InventoryLayers");

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
                name: "IX_InventoryBalances_LastMovementDate",
                table: "InventoryBalances");

            migrationBuilder.DropIndex(
                name: "IX_InventoryBalances_QuantityOnHand",
                table: "InventoryBalances");

            migrationBuilder.DropIndex(
                name: "IX_InventoryBalances_Unique_Item_Warehouse_Location",
                table: "InventoryBalances");

            migrationBuilder.DropIndex(
                name: "IX_FleetVehicleAssignments_TenantId_EmployeeId_IsActive",
                table: "FleetVehicleAssignments");

            migrationBuilder.DropIndex(
                name: "IX_FleetVehicleAssignments_TenantId_VehicleAssetId_IsActive",
                table: "FleetVehicleAssignments");

            migrationBuilder.DropIndex(
                name: "IX_FleetTyres_TenantId_SerialNumber_IsDeleted",
                table: "FleetTyres");

            migrationBuilder.DropIndex(
                name: "IX_FleetTyres_TenantId_VehicleAssetId_Status_IsDeleted",
                table: "FleetTyres");

            migrationBuilder.DropIndex(
                name: "IX_FleetTyreEvents_TenantId_FleetTyreId_EventAtUtc_IsDeleted",
                table: "FleetTyreEvents");

            migrationBuilder.DropIndex(
                name: "IX_FleetTyreEvents_TenantId_VehicleAssetId_EventAtUtc_IsDeleted",
                table: "FleetTyreEvents");

            migrationBuilder.DropIndex(
                name: "IX_FleetTrips_TenantId_DriverEmployeeId",
                table: "FleetTrips");

            migrationBuilder.DropIndex(
                name: "IX_FleetTrips_TenantId_RequestedByUserId",
                table: "FleetTrips");

            migrationBuilder.DropIndex(
                name: "IX_FleetTrips_TenantId_Status",
                table: "FleetTrips");

            migrationBuilder.DropIndex(
                name: "IX_FleetTrips_TenantId_VehicleAssetId",
                table: "FleetTrips");

            migrationBuilder.DropIndex(
                name: "IX_FleetTripInspections_TenantId_FleetTripId_InspectionKind_IsDeleted",
                table: "FleetTripInspections");

            migrationBuilder.DropIndex(
                name: "IX_FleetTripInspections_TenantId_InspectionTemplateId_IsDeleted",
                table: "FleetTripInspections");

            migrationBuilder.DropIndex(
                name: "IX_FleetIncidents_TenantId_Status_IsDeleted",
                table: "FleetIncidents");

            migrationBuilder.DropIndex(
                name: "IX_FleetIncidents_TenantId_VehicleAssetId_OccurredAtUtc_IsDeleted",
                table: "FleetIncidents");

            migrationBuilder.DropIndex(
                name: "IX_FleetFuelTransactions_TenantId_FleetTripId",
                table: "FleetFuelTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FleetFuelTransactions_TenantId_VehicleAssetId_FuelledAt",
                table: "FleetFuelTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FleetExternalRepairs_TenantId_VehicleAssetId_Status_IsDeleted",
                table: "FleetExternalRepairs");

            migrationBuilder.DropIndex(
                name: "IX_FleetExternalRepairs_TenantId_VendorBusinessPartnerId_IsDeleted",
                table: "FleetExternalRepairs");

            migrationBuilder.DropIndex(
                name: "IX_FleetDefects_TenantId_FleetTripId_IsDeleted",
                table: "FleetDefects");

            migrationBuilder.DropIndex(
                name: "IX_FleetDefects_TenantId_VehicleAssetId_Status_IsDeleted",
                table: "FleetDefects");

            migrationBuilder.DropIndex(
                name: "IX_FleetCostEntries_TenantId_CostType_IsDeleted",
                table: "FleetCostEntries");

            migrationBuilder.DropIndex(
                name: "IX_FleetCostEntries_TenantId_VehicleAssetId_CostDateUtc_IsDeleted",
                table: "FleetCostEntries");

            migrationBuilder.DropIndex(
                name: "IX_FleetComplianceItems_TenantId_ExpiryDate",
                table: "FleetComplianceItems");

            migrationBuilder.DropIndex(
                name: "IX_FleetComplianceItems_TenantId_VehicleAssetId",
                table: "FleetComplianceItems");

            migrationBuilder.DropIndex(
                name: "IX_FleetComplianceItems_TenantId_VehicleAssetId_IsCritical_ExpiryDate",
                table: "FleetComplianceItems");

            migrationBuilder.DropIndex(
                name: "IX_FleetBatteryEvents_TenantId_FleetBatteryId_EventAtUtc_IsDeleted",
                table: "FleetBatteryEvents");

            migrationBuilder.DropIndex(
                name: "IX_FleetBatteryEvents_TenantId_VehicleAssetId_EventAtUtc_IsDeleted",
                table: "FleetBatteryEvents");

            migrationBuilder.DropIndex(
                name: "IX_FleetBatteries_TenantId_SerialNumber_IsDeleted",
                table: "FleetBatteries");

            migrationBuilder.DropIndex(
                name: "IX_FleetBatteries_TenantId_VehicleAssetId_Status_IsDeleted",
                table: "FleetBatteries");

            migrationBuilder.DropIndex(
                name: "IX_EmergencySuppliers_IsActive",
                table: "EmergencySuppliers");

            migrationBuilder.DropIndex(
                name: "IX_EmergencyProcurementPlans_CriticalityLevel",
                table: "EmergencyProcurementPlans");

            migrationBuilder.DropIndex(
                name: "IX_EmergencyProcurementPlans_EmergencyType",
                table: "EmergencyProcurementPlans");

            migrationBuilder.DropIndex(
                name: "IX_EmergencyProcurementPlans_PlanCode",
                table: "EmergencyProcurementPlans");

            migrationBuilder.DropIndex(
                name: "IX_EmergencyProcurementPlans_Status",
                table: "EmergencyProcurementPlans");

            migrationBuilder.DropIndex(
                name: "IX_EmergencyProcurementItems_CriticalityLevel",
                table: "EmergencyProcurementItems");

            migrationBuilder.DropIndex(
                name: "IX_EmergencyProcurementItems_ItemCategory",
                table: "EmergencyProcurementItems");

            migrationBuilder.DropIndex(
                name: "IX_EmailCampaigns_TenantId_Name",
                table: "EmailCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_EmailCampaigns_TenantId_Status",
                table: "EmailCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_EmailCampaignRecipients_TenantId_EmailCampaignId_Email",
                table: "EmailCampaignRecipients");

            migrationBuilder.DropIndex(
                name: "IX_EmailCampaignRecipients_TenantId_EmailCampaignId_Status",
                table: "EmailCampaignRecipients");

            migrationBuilder.DropIndex(
                name: "IX_DistributedLocks_LockName",
                table: "DistributedLocks");

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
                name: "IX_Contracts_ContractNumber",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_Status",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_ContractMilestones_PlannedDate",
                table: "ContractMilestones");

            migrationBuilder.DropIndex(
                name: "IX_ContractMilestones_Status",
                table: "ContractMilestones");

            migrationBuilder.DropIndex(
                name: "IX_ContractDocuments_DocumentType",
                table: "ContractDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ContractAmendments_AmendmentNumber",
                table: "ContractAmendments");

            migrationBuilder.DropIndex(
                name: "IX_ContractAmendments_Status",
                table: "ContractAmendments");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartnerUsers_BusinessPartnerId_UserId",
                table: "BusinessPartnerUsers");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartnerUsers_IsActive",
                table: "BusinessPartnerUsers");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartnerUsers_Role",
                table: "BusinessPartnerUsers");

            migrationBuilder.DropIndex(
                name: "IX_AwardVerificationChecklistTemplates_IsActive",
                table: "AwardVerificationChecklistTemplates");

            migrationBuilder.DropIndex(
                name: "IX_AwardVerificationChecklistTemplates_IsDefault",
                table: "AwardVerificationChecklistTemplates");

            migrationBuilder.DropIndex(
                name: "IX_AwardVerificationChecklistTemplates_TenantId_Name",
                table: "AwardVerificationChecklistTemplates");

            migrationBuilder.DropIndex(
                name: "IX_AwardVerificationChecklistItems_DisplayOrder",
                table: "AwardVerificationChecklistItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UnitsOfMeasure",
                table: "UnitsOfMeasure");

            migrationBuilder.DropIndex(
                name: "IX_UnitsOfMeasure_Category",
                table: "UnitsOfMeasure");

            migrationBuilder.DropIndex(
                name: "IX_UnitsOfMeasure_Code",
                table: "UnitsOfMeasure");

            migrationBuilder.DropIndex(
                name: "IX_UnitsOfMeasure_IsActive",
                table: "UnitsOfMeasure");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UnitOfMeasureSchedules",
                table: "UnitOfMeasureSchedules");

            migrationBuilder.DropIndex(
                name: "IX_UnitOfMeasureSchedules_IsActive",
                table: "UnitOfMeasureSchedules");

            migrationBuilder.DropIndex(
                name: "IX_UnitOfMeasureSchedules_ScheduleId",
                table: "UnitOfMeasureSchedules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UnitOfMeasureScheduleDetails",
                table: "UnitOfMeasureScheduleDetails");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UnitOfMeasureConversions",
                table: "UnitOfMeasureConversions");

            migrationBuilder.DropIndex(
                name: "IX_UnitOfMeasureConversions_FromUnitId_ToUnitId",
                table: "UnitOfMeasureConversions");

            migrationBuilder.DropIndex(
                name: "IX_UnitOfMeasureConversions_IsActive",
                table: "UnitOfMeasureConversions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderRevisions",
                table: "TenderRevisions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderInvitations",
                table: "TenderInvitations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderInterviews",
                table: "TenderInterviews");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderEvaluators",
                table: "TenderEvaluators");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderEvaluations",
                table: "TenderEvaluations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderDocuments",
                table: "TenderDocuments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderClarifications",
                table: "TenderClarifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderBidLots",
                table: "TenderBidLots");

            migrationBuilder.DropIndex(
                name: "IX_TenderBidLots_Status",
                table: "TenderBidLots");

            migrationBuilder.DropIndex(
                name: "IX_TenderBidLots_TenderBidId_LotId",
                table: "TenderBidLots");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderBidItems",
                table: "TenderBidItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderBidDocuments",
                table: "TenderBidDocuments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ItemUnitsOfMeasure",
                table: "ItemUnitsOfMeasure");

            migrationBuilder.DropIndex(
                name: "IX_ItemUnitsOfMeasure_InventoryItemId_UnitOfMeasureId",
                table: "ItemUnitsOfMeasure");

            migrationBuilder.DropIndex(
                name: "IX_ItemUnitsOfMeasure_IsActive",
                table: "ItemUnitsOfMeasure");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InventoryTransferItems",
                table: "InventoryTransferItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EvaluationTemplates",
                table: "EvaluationTemplates");

            migrationBuilder.DropIndex(
                name: "IX_EvaluationTemplates_IsActive",
                table: "EvaluationTemplates");

            migrationBuilder.DropIndex(
                name: "IX_EvaluationTemplates_IsDefault",
                table: "EvaluationTemplates");

            migrationBuilder.DropIndex(
                name: "IX_EvaluationTemplates_TenantId_TemplateName",
                table: "EvaluationTemplates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EvaluationTemplateCriteria",
                table: "EvaluationTemplateCriteria");

            migrationBuilder.DropIndex(
                name: "IX_EvaluationTemplateCriteria_DisplayOrder",
                table: "EvaluationTemplateCriteria");

            migrationBuilder.DropIndex(
                name: "IX_EvaluationTemplateCriteria_EvaluationTemplateId_EvaluationCriterionId",
                table: "EvaluationTemplateCriteria");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EvaluationCriteria",
                table: "EvaluationCriteria");

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
                name: "TaxCode",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ThumbnailUrl",
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
                name: "Symbol",
                table: "Currencies");

            migrationBuilder.RenameTable(
                name: "UnitsOfMeasure",
                newName: "UnitOfMeasure");

            migrationBuilder.RenameTable(
                name: "UnitOfMeasureSchedules",
                newName: "UnitOfMeasureSchedule");

            migrationBuilder.RenameTable(
                name: "UnitOfMeasureScheduleDetails",
                newName: "UnitOfMeasureScheduleDetail");

            migrationBuilder.RenameTable(
                name: "UnitOfMeasureConversions",
                newName: "UnitOfMeasureConversion");

            migrationBuilder.RenameTable(
                name: "TenderRevisions",
                newName: "TenderRevision");

            migrationBuilder.RenameTable(
                name: "TenderInvitations",
                newName: "TenderInvitation");

            migrationBuilder.RenameTable(
                name: "TenderInterviews",
                newName: "TenderInterview");

            migrationBuilder.RenameTable(
                name: "TenderEvaluators",
                newName: "TenderEvaluator");

            migrationBuilder.RenameTable(
                name: "TenderEvaluations",
                newName: "TenderEvaluation");

            migrationBuilder.RenameTable(
                name: "TenderDocuments",
                newName: "TenderDocument");

            migrationBuilder.RenameTable(
                name: "TenderClarifications",
                newName: "TenderClarification");

            migrationBuilder.RenameTable(
                name: "TenderBidLots",
                newName: "TenderBidLot");

            migrationBuilder.RenameTable(
                name: "TenderBidItems",
                newName: "TenderBidItem");

            migrationBuilder.RenameTable(
                name: "TenderBidDocuments",
                newName: "TenderBidDocument");

            migrationBuilder.RenameTable(
                name: "ItemUnitsOfMeasure",
                newName: "ItemUnitOfMeasure");

            migrationBuilder.RenameTable(
                name: "InventoryTransferItems",
                newName: "InventoryTransferItem");

            migrationBuilder.RenameTable(
                name: "EvaluationTemplates",
                newName: "EvaluationTemplate");

            migrationBuilder.RenameTable(
                name: "EvaluationTemplateCriteria",
                newName: "EvaluationTemplateCriterion");

            migrationBuilder.RenameTable(
                name: "EvaluationCriteria",
                newName: "EvaluationCriterion");

            migrationBuilder.RenameColumn(
                name: "SubstituteItem4Id",
                table: "InventoryItems",
                newName: "DefaultTaxGroupId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryItems_SubstituteItem4Id",
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
                name: "Country",
                table: "Currencies",
                newName: "PluralName");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "Currencies",
                newName: "NumericCode");

            migrationBuilder.RenameIndex(
                name: "IX_UnitsOfMeasure_TenantId",
                table: "UnitOfMeasure",
                newName: "IX_UnitOfMeasure_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitOfMeasureSchedules_TenantId",
                table: "UnitOfMeasureSchedule",
                newName: "IX_UnitOfMeasureSchedule_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitOfMeasureSchedules_BaseUnitOfMeasureId",
                table: "UnitOfMeasureSchedule",
                newName: "IX_UnitOfMeasureSchedule_BaseUnitOfMeasureId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitOfMeasureScheduleDetails_UnitOfMeasureId",
                table: "UnitOfMeasureScheduleDetail",
                newName: "IX_UnitOfMeasureScheduleDetail_UnitOfMeasureId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitOfMeasureScheduleDetails_TenantId",
                table: "UnitOfMeasureScheduleDetail",
                newName: "IX_UnitOfMeasureScheduleDetail_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitOfMeasureScheduleDetails_ScheduleId",
                table: "UnitOfMeasureScheduleDetail",
                newName: "IX_UnitOfMeasureScheduleDetail_ScheduleId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitOfMeasureConversions_ToUnitId",
                table: "UnitOfMeasureConversion",
                newName: "IX_UnitOfMeasureConversion_ToUnitId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitOfMeasureConversions_TenantId",
                table: "UnitOfMeasureConversion",
                newName: "IX_UnitOfMeasureConversion_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderRevisions_TenderId",
                table: "TenderRevision",
                newName: "IX_TenderRevision_TenderId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderRevisions_TenantId",
                table: "TenderRevision",
                newName: "IX_TenderRevision_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderRevisions_RevisedById",
                table: "TenderRevision",
                newName: "IX_TenderRevision_RevisedById");

            migrationBuilder.RenameIndex(
                name: "IX_TenderInvitations_TenderId",
                table: "TenderInvitation",
                newName: "IX_TenderInvitation_TenderId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderInvitations_TenantId",
                table: "TenderInvitation",
                newName: "IX_TenderInvitation_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderInvitations_InvitedById",
                table: "TenderInvitation",
                newName: "IX_TenderInvitation_InvitedById");

            migrationBuilder.RenameIndex(
                name: "IX_TenderInvitations_BusinessPartnerId",
                table: "TenderInvitation",
                newName: "IX_TenderInvitation_BusinessPartnerId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderInterviews_TenderId",
                table: "TenderInterview",
                newName: "IX_TenderInterview_TenderId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderInterviews_TenderBidId",
                table: "TenderInterview",
                newName: "IX_TenderInterview_TenderBidId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderInterviews_TenantId",
                table: "TenderInterview",
                newName: "IX_TenderInterview_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderInterviews_ConductedById",
                table: "TenderInterview",
                newName: "IX_TenderInterview_ConductedById");

            migrationBuilder.RenameIndex(
                name: "IX_TenderEvaluators_UserId",
                table: "TenderEvaluator",
                newName: "IX_TenderEvaluator_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderEvaluators_TenderId",
                table: "TenderEvaluator",
                newName: "IX_TenderEvaluator_TenderId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderEvaluators_TenantId",
                table: "TenderEvaluator",
                newName: "IX_TenderEvaluator_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderEvaluators_AssignedById",
                table: "TenderEvaluator",
                newName: "IX_TenderEvaluator_AssignedById");

            migrationBuilder.RenameIndex(
                name: "IX_TenderEvaluations_TenderEvaluatorId",
                table: "TenderEvaluation",
                newName: "IX_TenderEvaluation_TenderEvaluatorId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderEvaluations_TenderBidId",
                table: "TenderEvaluation",
                newName: "IX_TenderEvaluation_TenderBidId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderEvaluations_TenantId",
                table: "TenderEvaluation",
                newName: "IX_TenderEvaluation_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderDocuments_UploadedById",
                table: "TenderDocument",
                newName: "IX_TenderDocument_UploadedById");

            migrationBuilder.RenameIndex(
                name: "IX_TenderDocuments_TenderId",
                table: "TenderDocument",
                newName: "IX_TenderDocument_TenderId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderDocuments_TenantId",
                table: "TenderDocument",
                newName: "IX_TenderDocument_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderClarifications_TenderId",
                table: "TenderClarification",
                newName: "IX_TenderClarification_TenderId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderClarifications_TenantId",
                table: "TenderClarification",
                newName: "IX_TenderClarification_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderClarifications_QuestionById",
                table: "TenderClarification",
                newName: "IX_TenderClarification_QuestionById");

            migrationBuilder.RenameIndex(
                name: "IX_TenderClarifications_BusinessPartnerId",
                table: "TenderClarification",
                newName: "IX_TenderClarification_BusinessPartnerId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderClarifications_AnsweredById",
                table: "TenderClarification",
                newName: "IX_TenderClarification_AnsweredById");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidLots_TenderBidId",
                table: "TenderBidLot",
                newName: "IX_TenderBidLot_TenderBidId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidLots_TenantId",
                table: "TenderBidLot",
                newName: "IX_TenderBidLot_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidLots_LotId",
                table: "TenderBidLot",
                newName: "IX_TenderBidLot_LotId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidItems_TenderItemId",
                table: "TenderBidItem",
                newName: "IX_TenderBidItem_TenderItemId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidItems_TenderBidId",
                table: "TenderBidItem",
                newName: "IX_TenderBidItem_TenderBidId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidItems_TenantId",
                table: "TenderBidItem",
                newName: "IX_TenderBidItem_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidItems_BidLotId",
                table: "TenderBidItem",
                newName: "IX_TenderBidItem_BidLotId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidDocuments_UploadedById",
                table: "TenderBidDocument",
                newName: "IX_TenderBidDocument_UploadedById");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidDocuments_TenderBidId",
                table: "TenderBidDocument",
                newName: "IX_TenderBidDocument_TenderBidId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidDocuments_TenantId",
                table: "TenderBidDocument",
                newName: "IX_TenderBidDocument_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_ItemUnitsOfMeasure_UnitOfMeasureId",
                table: "ItemUnitOfMeasure",
                newName: "IX_ItemUnitOfMeasure_UnitOfMeasureId");

            migrationBuilder.RenameIndex(
                name: "IX_ItemUnitsOfMeasure_TenantId",
                table: "ItemUnitOfMeasure",
                newName: "IX_ItemUnitOfMeasure_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryTransferItems_TenantId",
                table: "InventoryTransferItem",
                newName: "IX_InventoryTransferItem_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryTransferItems_SourceLocationId",
                table: "InventoryTransferItem",
                newName: "IX_InventoryTransferItem_SourceLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryTransferItems_InventoryTransferId",
                table: "InventoryTransferItem",
                newName: "IX_InventoryTransferItem_InventoryTransferId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryTransferItems_InventoryItemId",
                table: "InventoryTransferItem",
                newName: "IX_InventoryTransferItem_InventoryItemId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryTransferItems_DestinationLocationId",
                table: "InventoryTransferItem",
                newName: "IX_InventoryTransferItem_DestinationLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_EvaluationTemplates_CreatedById",
                table: "EvaluationTemplate",
                newName: "IX_EvaluationTemplate_CreatedById");

            migrationBuilder.RenameIndex(
                name: "IX_EvaluationTemplateCriteria_TenantId",
                table: "EvaluationTemplateCriterion",
                newName: "IX_EvaluationTemplateCriterion_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_EvaluationTemplateCriteria_EvaluationCriterionId",
                table: "EvaluationTemplateCriterion",
                newName: "IX_EvaluationTemplateCriterion_EvaluationCriterionId");

            migrationBuilder.RenameIndex(
                name: "IX_EvaluationCriteria_TenantId",
                table: "EvaluationCriterion",
                newName: "IX_EvaluationCriterion_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_EvaluationCriteria_CreatedById",
                table: "EvaluationCriterion",
                newName: "IX_EvaluationCriterion_CreatedById");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "TenderLots",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "LotCode",
                table: "TenderLots",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

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
                name: "WarehouseId",
                table: "StockMovements",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "WarehouseId",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reference",
                table: "StockAdjustments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPartnerId",
                table: "ProcurementPlanItemSuppliers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<bool>(
                name: "IsSystem",
                table: "NotificationTopics",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsRequired",
                table: "NotificationTopics",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsSystem",
                table: "NotificationTopicRecipients",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

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

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "FleetCostEntries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Manual");

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

            migrationBuilder.AddColumn<int>(
                name: "TransactionCount",
                table: "Currencies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Contracts",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldDefaultValue: "USD");

            migrationBuilder.AlterColumn<string>(
                name: "TemplateName",
                table: "EvaluationTemplate",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "EvaluationTemplate",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "EvaluationTemplate",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UnitOfMeasure",
                table: "UnitOfMeasure",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UnitOfMeasureSchedule",
                table: "UnitOfMeasureSchedule",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UnitOfMeasureScheduleDetail",
                table: "UnitOfMeasureScheduleDetail",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UnitOfMeasureConversion",
                table: "UnitOfMeasureConversion",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderRevision",
                table: "TenderRevision",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderInvitation",
                table: "TenderInvitation",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderInterview",
                table: "TenderInterview",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderEvaluator",
                table: "TenderEvaluator",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderEvaluation",
                table: "TenderEvaluation",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderDocument",
                table: "TenderDocument",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderClarification",
                table: "TenderClarification",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderBidLot",
                table: "TenderBidLot",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderBidItem",
                table: "TenderBidItem",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderBidDocument",
                table: "TenderBidDocument",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ItemUnitOfMeasure",
                table: "ItemUnitOfMeasure",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InventoryTransferItem",
                table: "InventoryTransferItem",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EvaluationTemplate",
                table: "EvaluationTemplate",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EvaluationTemplateCriterion",
                table: "EvaluationTemplateCriterion",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EvaluationCriterion",
                table: "EvaluationCriterion",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountType = table.Column<int>(type: "int", nullable: false),
                    AccountCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AccountSubCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ParentAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsSegmented = table.Column<bool>(type: "bit", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsMultiCurrency = table.Column<bool>(type: "bit", nullable: false),
                    IsIFRSClassified = table.Column<bool>(type: "bit", nullable: false),
                    IsBaseClassified = table.Column<bool>(type: "bit", nullable: false),
                    IsLocalClassified = table.Column<bool>(type: "bit", nullable: false),
                    IFRSLineItem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BaseLineItem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LocalLineItem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AllowDirectPosting = table.Column<bool>(type: "bit", nullable: false),
                    IsControlAccount = table.Column<bool>(type: "bit", nullable: false),
                    RequireDepartmentCode = table.Column<bool>(type: "bit", nullable: false),
                    RequireProjectCode = table.Column<bool>(type: "bit", nullable: false),
                    BudgetTrackingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DebitBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreditBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LastTransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstateModuleLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayrollModuleLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcurementModuleLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaxReportingCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CashFlowClassification = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsSystemAccount = table.Column<bool>(type: "bit", nullable: false),
                    InactivatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InactivationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Accounts_Accounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Accounts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountSegmentStructures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SegmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SegmentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SegmentPosition = table.Column<int>(type: "int", nullable: false),
                    SegmentLength = table.Column<int>(type: "int", nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SeparatorCharacter = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    LookupTableRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    IsReportingDimension = table.Column<bool>(type: "bit", nullable: false),
                    IsNaturalAccount = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AccountSegmentStructures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountSegmentStructures_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetVerificationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VerifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AssetVerificationSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetVerificationSessions_Employees_VerifiedById",
                        column: x => x.VerifiedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetVerificationSessions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BankBranch = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    AccountType = table.Column<int>(type: "int", nullable: false),
                    GLAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AvailableBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    OpeningDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContactPerson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TaxId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OutstandingBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PaymentTermsDays = table.Column<int>(type: "int", nullable: false),
                    PriceGroup = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastOrderDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Customers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TargetCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    InverseRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RateType = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    RateSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsManualEntry = table.Column<bool>(type: "bit", nullable: false),
                    APIEndpoint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    APIResponseMetadata = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    HasBeenUsedInTransactions = table.Column<bool>(type: "bit", nullable: false),
                    TransactionCount = table.Column<int>(type: "int", nullable: false),
                    FirstUsedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUsedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RateChangePercentage = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RateChangeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PreviousRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExceedsVarianceThreshold = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExchangeRates_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExchangeRates_ExchangeRates_PreviousRateId",
                        column: x => x.PreviousRateId,
                        principalTable: "ExchangeRates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExchangeRates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModuleDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IconClass = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethod",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RequiresBankAccount = table.Column<bool>(type: "bit", nullable: false),
                    RequiresReference = table.Column<bool>(type: "bit", nullable: false),
                    DefaultGLAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethod", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RatioDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NumeratorType = table.Column<int>(type: "int", nullable: false),
                    NumeratorAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NumeratorConstant = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    DenominatorType = table.Column<int>(type: "int", nullable: false),
                    DenominatorAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DenominatorConstant = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ResultFormat = table.Column<int>(type: "int", nullable: false),
                    DecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_RatioDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatioDefinitions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Taxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Applicability = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsInputTaxDeductible = table.Column<bool>(type: "bit", nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxPayableAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaxReceivableAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_Taxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Taxes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Applicability = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_TaxGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxGroups_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Applicability = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_TaxTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_UnitTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountCurrencyLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    RevaluationRequired = table.Column<bool>(type: "bit", nullable: false),
                    RevaluationFrequency = table.Column<int>(type: "int", nullable: false),
                    TransactionRateType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RevaluationRateType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InactivationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HasTransactionHistory = table.Column<bool>(type: "bit", nullable: false),
                    TransactionCount = table.Column<int>(type: "int", nullable: false),
                    FirstTransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ForeignCurrencyBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BaseCurrencyEquivalent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CurrentExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RateEffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRevaluationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRevaluationAdjustment = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CumulativeRevaluationAdjustment = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountCurrencyLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountCurrencyLinks_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountCurrencyLinks_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountCurrencyLinks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoaType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoaConfigurationLocked = table.Column<bool>(type: "bit", nullable: false),
                    BaseCurrency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccountSeparator = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetainedEarningsAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnrealizedGainLossAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RealizedGainLossAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SuspenseAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ControlAccountArId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ControlAccountApId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ControlAccountInventoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ControlAccountPayrollId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ControlAccountTaxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceSettings_Accounts_ControlAccountApId",
                        column: x => x.ControlAccountApId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinanceSettings_Accounts_ControlAccountArId",
                        column: x => x.ControlAccountArId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinanceSettings_Accounts_ControlAccountInventoryId",
                        column: x => x.ControlAccountInventoryId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinanceSettings_Accounts_ControlAccountPayrollId",
                        column: x => x.ControlAccountPayrollId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinanceSettings_Accounts_ControlAccountTaxId",
                        column: x => x.ControlAccountTaxId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinanceSettings_Accounts_RealizedGainLossAccountId",
                        column: x => x.RealizedGainLossAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinanceSettings_Accounts_RetainedEarningsAccountId",
                        column: x => x.RetainedEarningsAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinanceSettings_Accounts_SuspenseAccountId",
                        column: x => x.SuspenseAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinanceSettings_Accounts_UnrealizedGainLossAccountId",
                        column: x => x.UnrealizedGainLossAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinanceSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FixedAssetCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DefaultMethod = table.Column<int>(type: "int", nullable: false),
                    DefaultUsefulLifeMonths = table.Column<int>(type: "int", nullable: false),
                    DefaultResidualValuePercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AssetAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccumulatedDepreciationAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepreciationExpenseAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GainOnDisposalAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LossOnDisposalAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_FixedAssetCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Accounts_AccumulatedDepreciationAccountId",
                        column: x => x.AccumulatedDepreciationAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Accounts_AssetAccountId",
                        column: x => x.AssetAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Accounts_DepreciationExpenseAccountId",
                        column: x => x.DepreciationExpenseAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Accounts_GainOnDisposalAccountId",
                        column: x => x.GainOnDisposalAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Accounts_LossOnDisposalAccountId",
                        column: x => x.LossOnDisposalAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VendorInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SupplierInvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BaseCurrencyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentTermsDays = table.Column<int>(type: "int", nullable: false),
                    EarlyPaymentDiscountPercentage = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    EarlyPaymentDiscountDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EarlyPaymentDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WithholdingTaxRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    WithholdingTaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MatchingType = table.Column<int>(type: "int", nullable: false),
                    MatchingStatus = table.Column<int>(type: "int", nullable: false),
                    MatchingNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExpenseAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorInvoices_Accounts_ApAccountId",
                        column: x => x.ApAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VendorInvoices_Accounts_ExpenseAccountId",
                        column: x => x.ExpenseAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VendorInvoices_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VendorInvoices_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VendorInvoices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SegmentLookupValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SegmentStructureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SegmentValue = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParentValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_SegmentLookupValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SegmentLookupValues_AccountSegmentStructures_SegmentStructureId",
                        column: x => x.SegmentStructureId,
                        principalTable: "AccountSegmentStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SegmentLookupValues_SegmentLookupValues_ParentValueId",
                        column: x => x.ParentValueId,
                        principalTable: "SegmentLookupValues",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SegmentLookupValues_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankStatement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatementNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalDebits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCredits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankStatement_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cheque",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChequeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PayeeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PresentedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClearedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Memo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CashTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StatusReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cheque", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cheque_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BatchDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDateFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDateTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentCount = table.Column<int>(type: "int", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentBatches_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentBatches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CheckNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TransactionReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClearedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCreditNote = table.Column<bool>(type: "bit", nullable: false),
                    CreditNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPayments_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CustomerPayments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerPayments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BaseCurrencyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentTermsDays = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invoices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TransactionDocumentModuleMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModuleDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ModuleDefinitionId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionDocumentModuleMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionDocumentModuleMappings_ModuleDefinitions_ModuleDefinitionId",
                        column: x => x.ModuleDefinitionId,
                        principalTable: "ModuleDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionDocumentModuleMappings_ModuleDefinitions_ModuleDefinitionId1",
                        column: x => x.ModuleDefinitionId1,
                        principalTable: "ModuleDefinitions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TaxRateHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TaxRateHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxRateHistory_Taxes_TaxId",
                        column: x => x.TaxId,
                        principalTable: "Taxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaxRateHistory_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxThresholds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    CumulativeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsThresholdExceeded = table.Column<bool>(type: "bit", nullable: false),
                    ThresholdExceededDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_TaxThresholds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxThresholds_Taxes_TaxId",
                        column: x => x.TaxId,
                        principalTable: "Taxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaxThresholds_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxCalculations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CompoundBasis = table.Column<int>(type: "int", nullable: false),
                    CalculationOrder = table.Column<int>(type: "int", nullable: false),
                    CalculationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsManualOverride = table.Column<bool>(type: "bit", nullable: false),
                    OverrideReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TaxCalculations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxCalculations_TaxGroups_TaxGroupId",
                        column: x => x.TaxGroupId,
                        principalTable: "TaxGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TaxCalculations_Taxes_TaxId",
                        column: x => x.TaxId,
                        principalTable: "Taxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxCalculations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxGroupComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalculationOrder = table.Column<int>(type: "int", nullable: false),
                    CompoundBasis = table.Column<int>(type: "int", nullable: false),
                    AppliesOnTaxCodes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TaxGroupComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxGroupComponents_TaxGroups_TaxGroupId",
                        column: x => x.TaxGroupId,
                        principalTable: "TaxGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaxGroupComponents_Taxes_TaxId",
                        column: x => x.TaxId,
                        principalTable: "Taxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxGroupComponents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    TaxGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProductCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ServiceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_TaxRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxRules_TaxGroups_TaxGroupId",
                        column: x => x.TaxGroupId,
                        principalTable: "TaxGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_TaxRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxRates_TaxTypes_TaxTypeId",
                        column: x => x.TaxTypeId,
                        principalTable: "TaxTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaxRates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UnitTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccountLevel = table.Column<int>(type: "int", nullable: false),
                    IsPostingAccount = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_UnitAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitAccounts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitAccounts_UnitAccounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalTable: "UnitAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitAccounts_UnitTypes_UnitTypeId",
                        column: x => x.UnitTypeId,
                        principalTable: "UnitTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FixedAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FixedAssetCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlacedInServiceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InstallationCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AcquisitionCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetBookValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DepreciationMethod = table.Column<int>(type: "int", nullable: false),
                    DepreciationConvention = table.Column<int>(type: "int", nullable: false),
                    UsefulLifeMonths = table.Column<int>(type: "int", nullable: false),
                    ResidualValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DisposalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MaintenanceAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_FixedAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixedAssets_FixedAssetCategories_FixedAssetCategoryId",
                        column: x => x.FixedAssetCategoryId,
                        principalTable: "FixedAssetCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssets_MaintenanceAssets_MaintenanceAssetId",
                        column: x => x.MaintenanceAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FixedAssets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VendorInvoiceLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineItemType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GLAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurchaseOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DiscountPercentage = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorInvoiceLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceLineItems_Accounts_GLAccountId",
                        column: x => x.GLAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VendorInvoiceLineItems_PurchaseOrderItems_PurchaseOrderItemId",
                        column: x => x.PurchaseOrderItemId,
                        principalTable: "PurchaseOrderItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VendorInvoiceLineItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceLineItems_VendorInvoices_VendorInvoiceId",
                        column: x => x.VendorInvoiceId,
                        principalTable: "VendorInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountSegmentValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SegmentStructureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SegmentValue = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SegmentLookupValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SegmentValueDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SegmentPosition = table.Column<int>(type: "int", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AccountSegmentValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountSegmentValues_AccountSegmentStructures_SegmentStructureId",
                        column: x => x.SegmentStructureId,
                        principalTable: "AccountSegmentStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountSegmentValues_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountSegmentValues_SegmentLookupValues_SegmentLookupValueId",
                        column: x => x.SegmentLookupValueId,
                        principalTable: "SegmentLookupValues",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountSegmentValues_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankReconciliation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReconciliationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StatementBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BookBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Difference = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MatchedCount = table.Column<int>(type: "int", nullable: false),
                    UnmatchedBookCount = table.Column<int>(type: "int", nullable: false),
                    UnmatchedStatementCount = table.Column<int>(type: "int", nullable: false),
                    ReconciledBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReconciledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankReconciliation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankReconciliation_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankReconciliation_BankStatement_StatementId",
                        column: x => x.StatementId,
                        principalTable: "BankStatement",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VendorPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChequeNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TransactionReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WithholdingTaxRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    WithholdingTaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountTaken = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AuthorizedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorizedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClearedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorPayments_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VendorPayments_PaymentBatches_PaymentBatchId",
                        column: x => x.PaymentBatchId,
                        principalTable: "PaymentBatches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VendorPayments_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VendorPayments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineItemType = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GLAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DiscountPercentage = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLineItems_Accounts_GLAccountId",
                        column: x => x.GLAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoiceLineItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceLineItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AllocationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsReversal = table.Column<bool>(type: "bit", nullable: false),
                    OriginalAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_CustomerPayments_CustomerPaymentId",
                        column: x => x.CustomerPaymentId,
                        principalTable: "CustomerPayments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BankAccount = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CheckNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TransactionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AllocationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourceAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocationType = table.Column<int>(type: "int", nullable: false),
                    DriverUnitAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AutoReverse = table.Column<bool>(type: "bit", nullable: false),
                    LastRunDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AllocationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllocationRules_Accounts_SourceAccountId",
                        column: x => x.SourceAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AllocationRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AllocationRules_UnitAccounts_DriverUnitAccountId",
                        column: x => x.DriverUnitAccountId,
                        principalTable: "UnitAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FixedAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ResultingBookValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_AssetTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetTransactions_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetTransactions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FixedAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransferDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransferType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FromLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FromCustodianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ToCustodianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TransferCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_AssetTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetTransfers_Employees_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetTransfers_Employees_FromCustodianId",
                        column: x => x.FromCustodianId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetTransfers_Employees_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetTransfers_Employees_ToCustodianId",
                        column: x => x.ToCustodianId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetTransfers_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetTransfers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetVerificationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FixedAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Condition = table.Column<int>(type: "int", nullable: false),
                    CurrentLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_AssetVerificationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetVerificationItems_AssetVerificationSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AssetVerificationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetVerificationItems_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetVerificationItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<int>(type: "int", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToBankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PayeeOrPayer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GLAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsReconciled = table.Column<bool>(type: "bit", nullable: false),
                    ReconciliationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChequeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsPosted = table.Column<bool>(type: "bit", nullable: false),
                    PostedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentMethodId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashTransactions_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashTransactions_BankAccounts_ToBankAccountId",
                        column: x => x.ToBankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashTransactions_BankReconciliation_ReconciliationId",
                        column: x => x.ReconciliationId,
                        principalTable: "BankReconciliation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CashTransactions_Cheque_ChequeId",
                        column: x => x.ChequeId,
                        principalTable: "Cheque",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CashTransactions_PaymentMethod_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethod",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CashTransactions_PaymentMethod_PaymentMethodId1",
                        column: x => x.PaymentMethodId1,
                        principalTable: "PaymentMethod",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentBatchItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ItemStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentBatchItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentBatchItems_PaymentBatches_PaymentBatchId",
                        column: x => x.PaymentBatchId,
                        principalTable: "PaymentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentBatchItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentBatchItems_VendorPayments_VendorPaymentId",
                        column: x => x.VendorPaymentId,
                        principalTable: "VendorPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendorPaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WithholdingTaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AllocationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsReversal = table.Column<bool>(type: "bit", nullable: false),
                    OriginalAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorInvoiceId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorPaymentAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorPaymentAllocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VendorPaymentAllocations_VendorInvoices_VendorInvoiceId",
                        column: x => x.VendorInvoiceId,
                        principalTable: "VendorInvoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VendorPaymentAllocations_VendorInvoices_VendorInvoiceId1",
                        column: x => x.VendorInvoiceId1,
                        principalTable: "VendorInvoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VendorPaymentAllocations_VendorPayments_VendorPaymentId",
                        column: x => x.VendorPaymentId,
                        principalTable: "VendorPayments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AllocationTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocationRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FixedPercentage = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TargetDriverUnitAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CostCenterCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_AllocationTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllocationTargets_Accounts_TargetAccountId",
                        column: x => x.TargetAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AllocationTargets_AllocationRules_AllocationRuleId",
                        column: x => x.AllocationRuleId,
                        principalTable: "AllocationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AllocationTargets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AllocationTargets_UnitAccounts_TargetDriverUnitAccountId",
                        column: x => x.TargetDriverUnitAccountId,
                        principalTable: "UnitAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankStatementLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankStatementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsMatched = table.Column<bool>(type: "bit", nullable: false),
                    MatchedTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReconciliationMatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankStatementLine_BankStatement_BankStatementId",
                        column: x => x.BankStatementId,
                        principalTable: "BankStatement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankStatementLine_CashTransactions_MatchedTransactionId",
                        column: x => x.MatchedTransactionId,
                        principalTable: "CashTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationMatch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReconciliationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankStatementLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsAutoMatched = table.Column<bool>(type: "bit", nullable: false),
                    MatchConfidence = table.Column<int>(type: "int", nullable: true),
                    MatchedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationMatch", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconciliationMatch_BankReconciliation_ReconciliationId",
                        column: x => x.ReconciliationId,
                        principalTable: "BankReconciliation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReconciliationMatch_BankStatementLine_BankStatementLineId",
                        column: x => x.BankStatementLineId,
                        principalTable: "BankStatementLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReconciliationMatch_CashTransactions_CashTransactionId",
                        column: x => x.CashTransactionId,
                        principalTable: "CashTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookClassification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OpeningBalanceType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PeriodDebits = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PeriodCredits = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PeriodNetMovement = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ClosingBalanceType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    YearToDateDebits = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    YearToDateCredits = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    YearToDateNetMovement = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SegmentString = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DepartmentSegment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CostCenterSegment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProjectSegment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LocationSegment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    BaseCurrencyEquivalent = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    UnrealizedGainLoss = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TransactionCount = table.Column<int>(type: "int", nullable: false),
                    LastTransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTransactionUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsReconciled = table.Column<bool>(type: "bit", nullable: false),
                    LastReconciledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReconciliationDiscrepancy = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LockedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HasActivity = table.Column<bool>(type: "bit", nullable: false),
                    IsZeroBalance = table.Column<bool>(type: "bit", nullable: false),
                    IsNegativeBalance = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountBalances_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountBalances_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ForeignCurrencyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ExchangeRateSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExchangeRateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceModule = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BookClassification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostingStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsReversed = table.Column<bool>(type: "bit", nullable: false),
                    ReversalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversalTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SegmentString = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsRevaluationEntry = table.Column<bool>(type: "bit", nullable: false),
                    RevaluationBatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RevaluationType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TransactionTag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AccountId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FiscalPeriodId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JournalEntryId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountTransactions_AccountTransactions_OriginalTransactionId",
                        column: x => x.OriginalTransactionId,
                        principalTable: "AccountTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountTransactions_AccountTransactions_ReversalTransactionId",
                        column: x => x.ReversalTransactionId,
                        principalTable: "AccountTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountTransactions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountTransactions_Accounts_AccountId1",
                        column: x => x.AccountId1,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountTransactions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetDepreciationSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FixedAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepreciationAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AccumulatedDepreciation = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NetBookValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsPosted = table.Column<bool>(type: "bit", nullable: false),
                    PostedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsProjected = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AssetDepreciationSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetDepreciationSchedules_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetDepreciationSchedules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetDisposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FixedAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisposalDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisposalType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SaleProceeds = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DisposalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetBookValueAtDisposal = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    GainOrLoss = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BuyerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AssetDisposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetDisposals_Employees_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDisposals_Employees_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDisposals_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDisposals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BudgetEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BudgetReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountBase = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_BudgetEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetEntries_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BudgetReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BudgetScenarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SegmentValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApproverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_BudgetReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetReturns_AccountSegmentValues_SegmentValueId",
                        column: x => x.SegmentValueId,
                        principalTable: "AccountSegmentValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetReturns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BudgetScenarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LockedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_BudgetScenarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetScenarios_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FiscalPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PeriodCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PeriodNumber = table.Column<int>(type: "int", nullable: false),
                    PeriodType = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodDays = table.Column<int>(type: "int", nullable: false),
                    PeriodStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LockedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LockReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsCloseInitiated = table.Column<bool>(type: "bit", nullable: false),
                    CloseInitiatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CloseInitiatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    ClosedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TrialBalanceValidated = table.Column<bool>(type: "bit", nullable: false),
                    TrialBalanceValidatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BankReconciliationComplete = table.Column<bool>(type: "bit", nullable: false),
                    BankReconciliationCompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrencyRevaluationComplete = table.Column<bool>(type: "bit", nullable: false),
                    CurrencyRevaluationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DepreciationComplete = table.Column<bool>(type: "bit", nullable: false),
                    DepreciationCompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InventoryValuationComplete = table.Column<bool>(type: "bit", nullable: false),
                    InventoryValuationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AccrualsComplete = table.Column<bool>(type: "bit", nullable: false),
                    AccrualsCompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HasBeenReopened = table.Column<bool>(type: "bit", nullable: false),
                    ReopenCount = table.Column<int>(type: "int", nullable: false),
                    LastReopenedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastReopenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReopenReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsYearEnd = table.Column<bool>(type: "bit", nullable: false),
                    YearEndCloseComplete = table.Column<bool>(type: "bit", nullable: false),
                    YearEndCloseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    YearEndClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TotalJournalEntries = table.Column<int>(type: "int", nullable: false),
                    TotalTransactionLines = table.Column<int>(type: "int", nullable: false),
                    TotalDebits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCredits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceDifference = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ClosingNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AllowBackdating = table.Column<bool>(type: "bit", nullable: false),
                    AllowFutureDating = table.Column<bool>(type: "bit", nullable: false),
                    MaxTransactionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalPeriods_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    JournalType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceModule = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TotalDebitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceDifference = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsBalanced = table.Column<bool>(type: "bit", nullable: false),
                    IsMultiCurrency = table.Column<bool>(type: "bit", nullable: false),
                    PrimaryCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    BookClassification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostingStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ApprovalWorkflowId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsReversed = table.Column<bool>(type: "bit", nullable: false),
                    ReversalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversalJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsRecurring = table.Column<bool>(type: "bit", nullable: false),
                    RecurringTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecurrenceFrequency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NextRecurrenceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRevaluationEntry = table.Column<bool>(type: "bit", nullable: false),
                    RevaluationBatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RevaluationType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsAutoReversalEntry = table.Column<bool>(type: "bit", nullable: false),
                    IsImported = table.Column<bool>(type: "bit", nullable: false),
                    ImportBatchReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EntryTag = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    HasAttachments = table.Column<bool>(type: "bit", nullable: false),
                    AttachmentCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntries_FiscalPeriods_FiscalPeriodId",
                        column: x => x.FiscalPeriodId,
                        principalTable: "FiscalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JournalEntries_JournalEntries_OriginalJournalEntryId",
                        column: x => x.OriginalJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JournalEntries_JournalEntries_ReversalJournalEntryId",
                        column: x => x.ReversalJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JournalEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeriodModuleLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LockedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LockReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UnlockedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UnlockedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnlockReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ModuleDefinitionId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodModuleLocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeriodModuleLocks_FiscalPeriods_FiscalPeriodId",
                        column: x => x.FiscalPeriodId,
                        principalTable: "FiscalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PeriodModuleLocks_ModuleDefinitions_ModuleDefinitionId",
                        column: x => x.ModuleDefinitionId,
                        principalTable: "ModuleDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodModuleLocks_ModuleDefinitions_ModuleDefinitionId1",
                        column: x => x.ModuleDefinitionId1,
                        principalTable: "ModuleDefinitions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FiscalYears",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYearName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FiscalYearCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    FiscalYearType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalDays = table.Column<int>(type: "int", nullable: false),
                    NumberOfPeriods = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LockedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LockReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsCloseInitiated = table.Column<bool>(type: "bit", nullable: false),
                    CloseInitiatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CloseInitiatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    ClosedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AllPeriodsClosedValidated = table.Column<bool>(type: "bit", nullable: false),
                    AllPeriodsClosedValidatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalDepreciationComplete = table.Column<bool>(type: "bit", nullable: false),
                    FinalDepreciationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    YearEndRevaluationComplete = table.Column<bool>(type: "bit", nullable: false),
                    YearEndRevaluationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    YearEndInventoryComplete = table.Column<bool>(type: "bit", nullable: false),
                    YearEndInventoryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    YearEndAccrualsComplete = table.Column<bool>(type: "bit", nullable: false),
                    YearEndAccrualsDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    YearEndTrialBalanceValidated = table.Column<bool>(type: "bit", nullable: false),
                    YearEndTrialBalanceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetainedEarningsTransferComplete = table.Column<bool>(type: "bit", nullable: false),
                    RetainedEarningsTransferDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosingJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NetIncomeTransferred = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    OpeningBalancesGenerated = table.Column<bool>(type: "bit", nullable: false),
                    OpeningBalancesGeneratedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextFiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpeningBalanceJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HasBeenReopened = table.Column<bool>(type: "bit", nullable: false),
                    ReopenCount = table.Column<int>(type: "int", nullable: false),
                    LastReopenedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastReopenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReopenReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReportingFramework = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BaseCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TotalJournalEntries = table.Column<int>(type: "int", nullable: false),
                    TotalTransactionLines = table.Column<int>(type: "int", nullable: false),
                    TotalDebits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCredits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceDifference = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalExpenses = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetIncome = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsBudgetApproved = table.Column<bool>(type: "bit", nullable: false),
                    BudgetApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BudgetApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BudgetedRevenue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    BudgetedExpenses = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    BudgetedNetIncome = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    IsAuditComplete = table.Column<bool>(type: "bit", nullable: false),
                    AuditCompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditFirm = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AuditOpinion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AuditReportReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    YearEndClosingNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalYears_FiscalYears_NextFiscalYearId",
                        column: x => x.NextFiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FiscalYears_JournalEntries_ClosingJournalEntryId",
                        column: x => x.ClosingJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FiscalYears_JournalEntries_OpeningBalanceJournalEntryId",
                        column: x => x.OpeningBalanceJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FiscalYears_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitAccountBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PeriodActivity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_UnitAccountBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitAccountBalances_FiscalPeriods_FiscalPeriodId",
                        column: x => x.FiscalPeriodId,
                        principalTable: "FiscalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitAccountBalances_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitAccountBalances_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitAccountBalances_UnitAccounts_UnitAccountId",
                        column: x => x.UnitAccountId,
                        principalTable: "UnitAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnitAccountBudgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BudgetQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BudgetVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_UnitAccountBudgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitAccountBudgets_FiscalPeriods_FiscalPeriodId",
                        column: x => x.FiscalPeriodId,
                        principalTable: "FiscalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitAccountBudgets_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitAccountBudgets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitAccountBudgets_UnitAccounts_UnitAccountId",
                        column: x => x.UnitAccountId,
                        principalTable: "UnitAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnitJournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SourceDocument = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsReversal = table.Column<bool>(type: "bit", nullable: false),
                    ReversedEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_UnitJournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitJournalEntries_FiscalPeriods_FiscalPeriodId",
                        column: x => x.FiscalPeriodId,
                        principalTable: "FiscalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitJournalEntries_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitJournalEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitJournalEntryLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    UnitAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_UnitJournalEntryLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitJournalEntryLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitJournalEntryLines_UnitAccounts_UnitAccountId",
                        column: x => x.UnitAccountId,
                        principalTable: "UnitAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitJournalEntryLines_UnitJournalEntries_UnitJournalEntryId",
                        column: x => x.UnitJournalEntryId,
                        principalTable: "UnitJournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(3607));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(3662));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(3673));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(3676));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(3973));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(3988));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4005));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4014));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4038));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4048));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4057));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4064));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4077));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4089));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4099));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4125));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4137));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4155));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4164));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4175));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4226));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4229));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4230));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4231));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4232));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4234));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4235));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4236));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4237));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4238));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4239));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4240));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4241));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4242));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4242));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4243));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4299));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4301));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4302));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4303));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4311));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4312));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4313));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4314));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4315));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4316));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4316));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4317));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4318));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4319));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4320));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4386));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4387));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4389));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4390));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4391));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4391));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4392));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4393));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4394));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4395));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4409));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4411));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(4412));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0002-000000000001"), null, new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, null, null, new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0002-000000000002"), null, new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, null, null, new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0002-000000000003"), null, new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, null, null, new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0002-000000000004"), null, new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, null, null, new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0002-000000000005"), null, new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, null, null, new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0002-000000000006"), null, new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, null, null, new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0002-000000000007"), null, new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, null, null, new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "BaseCurrency", "BaseCurrencyName", "CreatedAt", "CurrencyDecimalPlaces", "CurrencySymbol" },
                values: new object[] { "GHS", null, new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), 2, null });

            migrationBuilder.InsertData(
                table: "UnitTypes",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "CreatedById", "DecimalPlaces", "DeletedAt", "DeletedBy", "Description", "IsActive", "IsDeleted", "LastModifiedById", "Name", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-1001-000000000001"), "EMP", new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 0, null, null, "Headcount/Full-Time Equivalents (FTE)", true, false, null, "Employees", new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-1001-000000000002"), "SQFT", new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 2, null, null, "Floor space area measurement", true, false, null, "Square Feet", new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-1001-000000000003"), "HRS", new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 2, null, null, "Time measurement in hours", true, false, null, "Hours", new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-1001-000000000004"), "UNITS", new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(3849), null, null, 0, null, null, "Generic unit count (production, sales, etc.)", true, false, null, "Units", new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-1001-000000000005"), "PCT", new DateTime(2026, 2, 12, 20, 34, 22, 51, DateTimeKind.Utc).AddTicks(3853), null, null, 2, null, null, "Percentage values (0-100)", true, false, null, "Percentage", new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-1001-000000000006"), "KWH", new DateTime(2025, 11, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 2, null, null, "Energy consumption measurement", true, false, null, "Kilowatt Hours", new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenders_TenantId",
                table: "Tenders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBids_TenantId",
                table: "TenderBids",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianTeams_TechnicianId",
                table: "TechnicianTeams",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSkillAssignments_TechnicianId1",
                table: "TechnicianSkillAssignments",
                column: "TechnicianId1");

            migrationBuilder.CreateIndex(
                name: "IX_SystemExceptionLogs_TenantId",
                table: "SystemExceptionLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotations_TenantId",
                table: "RequestForQuotations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationQuotes_TenantId",
                table: "RequestForQuotationQuotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationQuoteItems_TenantId",
                table: "RequestForQuotationQuoteItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationItems_TenantId",
                table: "RequestForQuotationItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationInvitations_TenantId",
                table: "RequestForQuotationInvitations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationAwardLines_TenantId",
                table: "RequestForQuotationAwardLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlanItemSuppliers_BusinessPartnerId",
                table: "ProcurementPlanItemSuppliers",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTopics_TenantId",
                table: "NotificationTopics",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTopicRecipients_TenantId",
                table: "NotificationTopicRecipients",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_MaintenanceTypeId1",
                table: "MaintenanceSchedules",
                column: "MaintenanceTypeId1");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_TenantId",
                table: "InventoryMovements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLayers_TenantId",
                table: "InventoryLayers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCategories_DefaultTaxGroupId",
                table: "InventoryCategories",
                column: "DefaultTaxGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_TenantId",
                table: "InventoryBalances",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetVehicleAssignments_TenantId",
                table: "FleetVehicleAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyres_TenantId",
                table: "FleetTyres",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyreEvents_TenantId",
                table: "FleetTyreEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_TenantId",
                table: "FleetTrips",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTripInspections_TenantId",
                table: "FleetTripInspections",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetIncidents_TenantId",
                table: "FleetIncidents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetFuelTransactions_TenantId",
                table: "FleetFuelTransactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetExternalRepairs_TenantId",
                table: "FleetExternalRepairs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetDefects_TenantId",
                table: "FleetDefects",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_TenantId",
                table: "FleetCostEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetComplianceItems_TenantId",
                table: "FleetComplianceItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteryEvents_TenantId",
                table: "FleetBatteryEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteries_TenantId",
                table: "FleetBatteries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaigns_TenantId",
                table: "EmailCampaigns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignRecipients_TenantId",
                table: "EmailCampaignRecipients",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_TenantId",
                table: "Currencies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AwardVerificationChecklistTemplates_TenantId",
                table: "AwardVerificationChecklistTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureConversion_FromUnitId",
                table: "UnitOfMeasureConversion",
                column: "FromUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemUnitOfMeasure_InventoryItemId",
                table: "ItemUnitOfMeasure",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationTemplate_TenantId",
                table: "EvaluationTemplate",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationTemplateCriterion_EvaluationTemplateId",
                table: "EvaluationTemplateCriterion",
                column: "EvaluationTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_AccountId",
                table: "AccountBalances",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_FiscalPeriodId",
                table: "AccountBalances",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_TenantId",
                table: "AccountBalances",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountCurrencyLinks_AccountId",
                table: "AccountCurrencyLinks",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountCurrencyLinks_CurrencyId",
                table: "AccountCurrencyLinks",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountCurrencyLinks_TenantId",
                table: "AccountCurrencyLinks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_ParentAccountId",
                table: "Accounts",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_TenantId",
                table: "Accounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentStructures_TenantId",
                table: "AccountSegmentStructures",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentValues_AccountId",
                table: "AccountSegmentValues",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentValues_SegmentLookupValueId",
                table: "AccountSegmentValues",
                column: "SegmentLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentValues_SegmentStructureId",
                table: "AccountSegmentValues",
                column: "SegmentStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentValues_TenantId",
                table: "AccountSegmentValues",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_AccountId",
                table: "AccountTransactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_AccountId1",
                table: "AccountTransactions",
                column: "AccountId1");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_FiscalPeriodId",
                table: "AccountTransactions",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_FiscalPeriodId1",
                table: "AccountTransactions",
                column: "FiscalPeriodId1");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_JournalEntryId",
                table: "AccountTransactions",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_JournalEntryId1",
                table: "AccountTransactions",
                column: "JournalEntryId1");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_OriginalTransactionId",
                table: "AccountTransactions",
                column: "OriginalTransactionId",
                unique: true,
                filter: "[OriginalTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_ReversalTransactionId",
                table: "AccountTransactions",
                column: "ReversalTransactionId",
                unique: true,
                filter: "[ReversalTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_TenantId",
                table: "AccountTransactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationRules_DriverUnitAccountId",
                table: "AllocationRules",
                column: "DriverUnitAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationRules_IsActive",
                table: "AllocationRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationRules_SourceAccountId",
                table: "AllocationRules",
                column: "SourceAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationRules_TenantId_Code",
                table: "AllocationRules",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AllocationTargets_AllocationRuleId",
                table: "AllocationTargets",
                column: "AllocationRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationTargets_TargetAccountId",
                table: "AllocationTargets",
                column: "TargetAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationTargets_TargetDriverUnitAccountId",
                table: "AllocationTargets",
                column: "TargetDriverUnitAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationTargets_TenantId",
                table: "AllocationTargets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationSchedules_FiscalPeriodId",
                table: "AssetDepreciationSchedules",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationSchedules_FixedAssetId_FiscalPeriodId",
                table: "AssetDepreciationSchedules",
                columns: new[] { "FixedAssetId", "FiscalPeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationSchedules_IsPosted",
                table: "AssetDepreciationSchedules",
                column: "IsPosted");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationSchedules_JournalEntryId",
                table: "AssetDepreciationSchedules",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationSchedules_TenantId",
                table: "AssetDepreciationSchedules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDisposals_ApprovedById",
                table: "AssetDisposals",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDisposals_DisposalDate",
                table: "AssetDisposals",
                column: "DisposalDate");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDisposals_FixedAssetId",
                table: "AssetDisposals",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDisposals_JournalEntryId",
                table: "AssetDisposals",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDisposals_RequestedById",
                table: "AssetDisposals",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDisposals_Status",
                table: "AssetDisposals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDisposals_TenantId",
                table: "AssetDisposals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransactions_FixedAssetId",
                table: "AssetTransactions",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransactions_TenantId",
                table: "AssetTransactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransactions_TransactionDate",
                table: "AssetTransactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransactions_TransactionType",
                table: "AssetTransactions",
                column: "TransactionType");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransfers_ApprovedById",
                table: "AssetTransfers",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransfers_FixedAssetId",
                table: "AssetTransfers",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransfers_FromCustodianId",
                table: "AssetTransfers",
                column: "FromCustodianId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransfers_RequestedById",
                table: "AssetTransfers",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransfers_Status",
                table: "AssetTransfers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransfers_TenantId",
                table: "AssetTransfers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransfers_ToCustodianId",
                table: "AssetTransfers",
                column: "ToCustodianId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransfers_TransferDate",
                table: "AssetTransfers",
                column: "TransferDate");

            migrationBuilder.CreateIndex(
                name: "IX_AssetVerificationItems_FixedAssetId",
                table: "AssetVerificationItems",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetVerificationItems_SessionId",
                table: "AssetVerificationItems",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetVerificationItems_TenantId",
                table: "AssetVerificationItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetVerificationSessions_ScheduledDate",
                table: "AssetVerificationSessions",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_AssetVerificationSessions_Status",
                table: "AssetVerificationSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AssetVerificationSessions_TenantId",
                table: "AssetVerificationSessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetVerificationSessions_VerifiedById",
                table: "AssetVerificationSessions",
                column: "VerifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliation_BankAccountId",
                table: "BankReconciliation",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliation_StatementId",
                table: "BankReconciliation",
                column: "StatementId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatement_BankAccountId",
                table: "BankStatement",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLine_BankStatementId",
                table: "BankStatementLine",
                column: "BankStatementId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLine_MatchedTransactionId",
                table: "BankStatementLine",
                column: "MatchedTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_AccountId",
                table: "BudgetEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_BudgetReturnId",
                table: "BudgetEntries",
                column: "BudgetReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_FiscalPeriodId",
                table: "BudgetEntries",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_TenantId",
                table: "BudgetEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetReturns_BudgetScenarioId",
                table: "BudgetReturns",
                column: "BudgetScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetReturns_SegmentValueId",
                table: "BudgetReturns",
                column: "SegmentValueId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetReturns_TenantId",
                table: "BudgetReturns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetScenarios_FiscalYearId",
                table: "BudgetScenarios",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetScenarios_TenantId",
                table: "BudgetScenarios",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransactions_BankAccountId",
                table: "CashTransactions",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransactions_ChequeId",
                table: "CashTransactions",
                column: "ChequeId",
                unique: true,
                filter: "[ChequeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransactions_PaymentMethodId",
                table: "CashTransactions",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransactions_PaymentMethodId1",
                table: "CashTransactions",
                column: "PaymentMethodId1");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransactions_ReconciliationId",
                table: "CashTransactions",
                column: "ReconciliationId");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransactions_ToBankAccountId",
                table: "CashTransactions",
                column: "ToBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Cheque_BankAccountId",
                table: "Cheque",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_BankAccountId",
                table: "CustomerPayments",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_CustomerId",
                table: "CustomerPayments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_TenantId",
                table: "CustomerPayments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId",
                table: "Customers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_CurrencyId",
                table: "ExchangeRates",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_PreviousRateId",
                table: "ExchangeRates",
                column: "PreviousRateId",
                unique: true,
                filter: "[PreviousRateId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_TenantId",
                table: "ExchangeRates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_ControlAccountApId",
                table: "FinanceSettings",
                column: "ControlAccountApId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_ControlAccountArId",
                table: "FinanceSettings",
                column: "ControlAccountArId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_ControlAccountInventoryId",
                table: "FinanceSettings",
                column: "ControlAccountInventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_ControlAccountPayrollId",
                table: "FinanceSettings",
                column: "ControlAccountPayrollId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_ControlAccountTaxId",
                table: "FinanceSettings",
                column: "ControlAccountTaxId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_RealizedGainLossAccountId",
                table: "FinanceSettings",
                column: "RealizedGainLossAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_RetainedEarningsAccountId",
                table: "FinanceSettings",
                column: "RetainedEarningsAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_SuspenseAccountId",
                table: "FinanceSettings",
                column: "SuspenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_TenantId",
                table: "FinanceSettings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_UnrealizedGainLossAccountId",
                table: "FinanceSettings",
                column: "UnrealizedGainLossAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalPeriods_FiscalYearId",
                table: "FiscalPeriods",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalPeriods_TenantId",
                table: "FiscalPeriods",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_ClosingJournalEntryId",
                table: "FiscalYears",
                column: "ClosingJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_NextFiscalYearId",
                table: "FiscalYears",
                column: "NextFiscalYearId",
                unique: true,
                filter: "[NextFiscalYearId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_OpeningBalanceJournalEntryId",
                table: "FiscalYears",
                column: "OpeningBalanceJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_TenantId",
                table: "FiscalYears",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_AccumulatedDepreciationAccountId",
                table: "FixedAssetCategories",
                column: "AccumulatedDepreciationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_AssetAccountId",
                table: "FixedAssetCategories",
                column: "AssetAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_Code",
                table: "FixedAssetCategories",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_DepreciationExpenseAccountId",
                table: "FixedAssetCategories",
                column: "DepreciationExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_GainOnDisposalAccountId",
                table: "FixedAssetCategories",
                column: "GainOnDisposalAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_LossOnDisposalAccountId",
                table: "FixedAssetCategories",
                column: "LossOnDisposalAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_Name",
                table: "FixedAssetCategories",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_TenantId",
                table: "FixedAssetCategories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_AssetCode",
                table: "FixedAssets",
                column: "AssetCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_FixedAssetCategoryId",
                table: "FixedAssets",
                column: "FixedAssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_MaintenanceAssetId",
                table: "FixedAssets",
                column: "MaintenanceAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_Status",
                table: "FixedAssets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_TenantId_AssetCode",
                table: "FixedAssets",
                columns: new[] { "TenantId", "AssetCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_GLAccountId",
                table: "InvoiceLineItems",
                column: "GLAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_InvoiceId",
                table: "InvoiceLineItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_TenantId",
                table: "InvoiceLineItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CustomerId",
                table: "Invoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId",
                table: "Invoices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_FiscalPeriodId",
                table: "JournalEntries",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_OriginalJournalEntryId",
                table: "JournalEntries",
                column: "OriginalJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_ReversalJournalEntryId",
                table: "JournalEntries",
                column: "ReversalJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId",
                table: "JournalEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleDefinitions_IsActive",
                table: "ModuleDefinitions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleDefinitions_ModuleCode",
                table: "ModuleDefinitions",
                column: "ModuleCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModuleDefinitions_SortOrder",
                table: "ModuleDefinitions",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_CustomerPaymentId",
                table: "PaymentAllocations",
                column: "CustomerPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_InvoiceId",
                table: "PaymentAllocations",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_TenantId",
                table: "PaymentAllocations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatches_BankAccountId",
                table: "PaymentBatches",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatches_TenantId",
                table: "PaymentBatches",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatchItems_PaymentBatchId",
                table: "PaymentBatchItems",
                column: "PaymentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatchItems_TenantId",
                table: "PaymentBatchItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatchItems_VendorPaymentId",
                table: "PaymentBatchItems",
                column: "VendorPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InvoiceId",
                table: "Payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodModuleLocks_FiscalPeriodId_ModuleDefinitionId",
                table: "PeriodModuleLocks",
                columns: new[] { "FiscalPeriodId", "ModuleDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodModuleLocks_ModuleDefinitionId",
                table: "PeriodModuleLocks",
                column: "ModuleDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodModuleLocks_ModuleDefinitionId1",
                table: "PeriodModuleLocks",
                column: "ModuleDefinitionId1");

            migrationBuilder.CreateIndex(
                name: "IX_RatioDefinitions_IsActive",
                table: "RatioDefinitions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RatioDefinitions_TenantId_Code",
                table: "RatioDefinitions",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationMatch_BankStatementLineId",
                table: "ReconciliationMatch",
                column: "BankStatementLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationMatch_CashTransactionId",
                table: "ReconciliationMatch",
                column: "CashTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationMatch_ReconciliationId",
                table: "ReconciliationMatch",
                column: "ReconciliationId");

            migrationBuilder.CreateIndex(
                name: "IX_SegmentLookupValues_ParentValueId",
                table: "SegmentLookupValues",
                column: "ParentValueId");

            migrationBuilder.CreateIndex(
                name: "IX_SegmentLookupValues_SegmentStructureId",
                table: "SegmentLookupValues",
                column: "SegmentStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_SegmentLookupValues_TenantId",
                table: "SegmentLookupValues",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxCalculations_TaxGroupId",
                table: "TaxCalculations",
                column: "TaxGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxCalculations_TaxId",
                table: "TaxCalculations",
                column: "TaxId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxCalculations_TenantId",
                table: "TaxCalculations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Taxes_Code",
                table: "Taxes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Taxes_TenantId",
                table: "Taxes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxGroupComponents_TaxGroupId",
                table: "TaxGroupComponents",
                column: "TaxGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxGroupComponents_TaxId",
                table: "TaxGroupComponents",
                column: "TaxId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxGroupComponents_TenantId",
                table: "TaxGroupComponents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxGroups_Code",
                table: "TaxGroups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxGroups_TenantId",
                table: "TaxGroups",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxRateHistory_TaxId",
                table: "TaxRateHistory",
                column: "TaxId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxRateHistory_TenantId",
                table: "TaxRateHistory",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxRates_TaxTypeId",
                table: "TaxRates",
                column: "TaxTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxRates_TenantId",
                table: "TaxRates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxRules_IsActive",
                table: "TaxRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TaxRules_Priority",
                table: "TaxRules",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_TaxRules_ProductCategoryId",
                table: "TaxRules",
                column: "ProductCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxRules_TaxGroupId",
                table: "TaxRules",
                column: "TaxGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxRules_TenantId",
                table: "TaxRules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxThresholds_TaxId",
                table: "TaxThresholds",
                column: "TaxId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxThresholds_TenantId",
                table: "TaxThresholds",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxTypes_TenantId",
                table: "TaxTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionDocumentModuleMappings_DocumentType",
                table: "TransactionDocumentModuleMappings",
                column: "DocumentType",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionDocumentModuleMappings_ModuleDefinitionId",
                table: "TransactionDocumentModuleMappings",
                column: "ModuleDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionDocumentModuleMappings_ModuleDefinitionId1",
                table: "TransactionDocumentModuleMappings",
                column: "ModuleDefinitionId1");

            migrationBuilder.CreateIndex(
                name: "IX_UnitAccountBalances_FiscalPeriodId",
                table: "UnitAccountBalances",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitAccountBalances_FiscalYearId",
                table: "UnitAccountBalances",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitAccountBalances_TenantId",
                table: "UnitAccountBalances",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitAccountBalances_UnitAccountId_FiscalPeriodId",
                table: "UnitAccountBalances",
                columns: new[] { "UnitAccountId", "FiscalPeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitAccountBudgets_FiscalPeriodId",
                table: "UnitAccountBudgets",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitAccountBudgets_FiscalYearId",
                table: "UnitAccountBudgets",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitAccountBudgets_IsActive",
                table: "UnitAccountBudgets",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UnitAccountBudgets_TenantId",
                table: "UnitAccountBudgets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitAccountBudgets_UnitAccountId_FiscalPeriodId_BudgetVersion",
                table: "UnitAccountBudgets",
                columns: new[] { "UnitAccountId", "FiscalPeriodId", "BudgetVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitAccounts_IsActive",
                table: "UnitAccounts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UnitAccounts_IsPostingAccount",
                table: "UnitAccounts",
                column: "IsPostingAccount");

            migrationBuilder.CreateIndex(
                name: "IX_UnitAccounts_ParentAccountId",
                table: "UnitAccounts",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitAccounts_TenantId_AccountNumber",
                table: "UnitAccounts",
                columns: new[] { "TenantId", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitAccounts_UnitTypeId",
                table: "UnitAccounts",
                column: "UnitTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitJournalEntries_EntryDate",
                table: "UnitJournalEntries",
                column: "EntryDate");

            migrationBuilder.CreateIndex(
                name: "IX_UnitJournalEntries_FiscalPeriodId",
                table: "UnitJournalEntries",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitJournalEntries_FiscalYearId",
                table: "UnitJournalEntries",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitJournalEntries_Status",
                table: "UnitJournalEntries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UnitJournalEntries_TenantId_EntryNumber",
                table: "UnitJournalEntries",
                columns: new[] { "TenantId", "EntryNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitJournalEntryLines_TenantId",
                table: "UnitJournalEntryLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitJournalEntryLines_UnitAccountId",
                table: "UnitJournalEntryLines",
                column: "UnitAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitJournalEntryLines_UnitJournalEntryId",
                table: "UnitJournalEntryLines",
                column: "UnitJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitTypes_IsActive",
                table: "UnitTypes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UnitTypes_TenantId_Code",
                table: "UnitTypes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceLineItems_GLAccountId",
                table: "VendorInvoiceLineItems",
                column: "GLAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceLineItems_PurchaseOrderItemId",
                table: "VendorInvoiceLineItems",
                column: "PurchaseOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceLineItems_TenantId",
                table: "VendorInvoiceLineItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceLineItems_VendorInvoiceId",
                table: "VendorInvoiceLineItems",
                column: "VendorInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoices_ApAccountId",
                table: "VendorInvoices",
                column: "ApAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoices_ExpenseAccountId",
                table: "VendorInvoices",
                column: "ExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoices_PurchaseOrderId",
                table: "VendorInvoices",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoices_SupplierId",
                table: "VendorInvoices",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoices_TenantId",
                table: "VendorInvoices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPaymentAllocations_TenantId",
                table: "VendorPaymentAllocations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPaymentAllocations_VendorInvoiceId",
                table: "VendorPaymentAllocations",
                column: "VendorInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPaymentAllocations_VendorInvoiceId1",
                table: "VendorPaymentAllocations",
                column: "VendorInvoiceId1");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPaymentAllocations_VendorPaymentId",
                table: "VendorPaymentAllocations",
                column: "VendorPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPayments_BankAccountId",
                table: "VendorPayments",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPayments_PaymentBatchId",
                table: "VendorPayments",
                column: "PaymentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPayments_SupplierId",
                table: "VendorPayments",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPayments_TenantId",
                table: "VendorPayments",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartners_BusinessPartners_ParentId",
                table: "BusinessPartners",
                column: "ParentId",
                principalTable: "BusinessPartners",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartnerUsers_Users_UserId",
                table: "BusinessPartnerUsers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_BusinessPartners_BusinessPartnerId",
                table: "Contracts",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_TenderAwards_TenderAwardId",
                table: "Contracts",
                column: "TenderAwardId",
                principalTable: "TenderAwards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Tenders_TenderId",
                table: "Contracts",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationCriterion_Tenants_TenantId",
                table: "EvaluationCriterion",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationCriterion_Users_CreatedById",
                table: "EvaluationCriterion",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationTemplate_Tenants_TenantId",
                table: "EvaluationTemplate",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationTemplate_Users_CreatedById",
                table: "EvaluationTemplate",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationTemplateCriterion_EvaluationCriterion_EvaluationCriterionId",
                table: "EvaluationTemplateCriterion",
                column: "EvaluationCriterionId",
                principalTable: "EvaluationCriterion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationTemplateCriterion_EvaluationTemplate_EvaluationTemplateId",
                table: "EvaluationTemplateCriterion",
                column: "EvaluationTemplateId",
                principalTable: "EvaluationTemplate",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationTemplateCriterion_Tenants_TenantId",
                table: "EvaluationTemplateCriterion",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FleetBatteryEvents_MaintenanceAssets_VehicleAssetId",
                table: "FleetBatteryEvents",
                column: "VehicleAssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FleetComplianceItems_MaintenanceAssets_VehicleAssetId",
                table: "FleetComplianceItems",
                column: "VehicleAssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FleetCostEntries_FleetIncidents_FleetIncidentId",
                table: "FleetCostEntries",
                column: "FleetIncidentId",
                principalTable: "FleetIncidents",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FleetFuelTransactions_FleetTrips_FleetTripId",
                table: "FleetFuelTransactions",
                column: "FleetTripId",
                principalTable: "FleetTrips",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FleetFuelTransactions_MaintenanceAssets_VehicleAssetId",
                table: "FleetFuelTransactions",
                column: "VehicleAssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FleetTrips_Employees_DriverEmployeeId",
                table: "FleetTrips",
                column: "DriverEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FleetTrips_MaintenanceAssets_VehicleAssetId",
                table: "FleetTrips",
                column: "VehicleAssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FleetTyreEvents_MaintenanceAssets_VehicleAssetId",
                table: "FleetTyreEvents",
                column: "VehicleAssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryBalances_InventoryItems_InventoryItemId",
                table: "InventoryBalances",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryBalances_Warehouses_WarehouseId",
                table: "InventoryBalances",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCategories_TaxGroups_DefaultTaxGroupId",
                table: "InventoryCategories",
                column: "DefaultTaxGroupId",
                principalTable: "TaxGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_TaxGroups_DefaultTaxGroupId",
                table: "InventoryItems",
                column: "DefaultTaxGroupId",
                principalTable: "TaxGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_UnitOfMeasureSchedule_UnitOfMeasureScheduleId",
                table: "InventoryItems",
                column: "UnitOfMeasureScheduleId",
                principalTable: "UnitOfMeasureSchedule",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryLayers_InventoryItems_InventoryItemId",
                table: "InventoryLayers",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryLayers_Warehouses_WarehouseId",
                table: "InventoryLayers",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_InventoryItems_InventoryItemId",
                table: "InventoryMovements",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_Warehouses_WarehouseId",
                table: "InventoryMovements",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransferItem_InventoryItems_InventoryItemId",
                table: "InventoryTransferItem",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransferItem_InventoryTransfers_InventoryTransferId",
                table: "InventoryTransferItem",
                column: "InventoryTransferId",
                principalTable: "InventoryTransfers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransferItem_Tenants_TenantId",
                table: "InventoryTransferItem",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransferItem_WarehouseLocations_DestinationLocationId",
                table: "InventoryTransferItem",
                column: "DestinationLocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransferItem_WarehouseLocations_SourceLocationId",
                table: "InventoryTransferItem",
                column: "SourceLocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransfers_Warehouses_DestinationWarehouseId",
                table: "InventoryTransfers",
                column: "DestinationWarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransfers_Warehouses_SourceWarehouseId",
                table: "InventoryTransfers",
                column: "SourceWarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemUnitOfMeasure_InventoryItems_InventoryItemId",
                table: "ItemUnitOfMeasure",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemUnitOfMeasure_Tenants_TenantId",
                table: "ItemUnitOfMeasure",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemUnitOfMeasure_UnitOfMeasure_UnitOfMeasureId",
                table: "ItemUnitOfMeasure",
                column: "UnitOfMeasureId",
                principalTable: "UnitOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceSchedules_MaintenanceTypes_MaintenanceTypeId1",
                table: "MaintenanceSchedules",
                column: "MaintenanceTypeId1",
                principalTable: "MaintenanceTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationTopics_EmailTemplates_EmailTemplateId",
                table: "NotificationTopics",
                column: "EmailTemplateId",
                principalTable: "EmailTemplates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceBondRequests_BusinessPartners_BusinessPartnerId",
                table: "PerformanceBondRequests",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceBondRequests_TenderBids_TenderBidId",
                table: "PerformanceBondRequests",
                column: "TenderBidId",
                principalTable: "TenderBids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceReviews_Users_ReviewedById",
                table: "PerformanceReviews",
                column: "ReviewedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceHistories_MarketAnalyses_MarketAnalysisId",
                table: "PriceHistories",
                column: "MarketAnalysisId",
                principalTable: "MarketAnalyses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementBudgets_Departments_DepartmentId",
                table: "ProcurementBudgets",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementPlanItemSuppliers_BusinessPartners_BusinessPartnerId",
                table: "ProcurementPlanItemSuppliers",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementPlans_Departments_DepartmentId",
                table: "ProcurementPlans",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementSchedules_Departments_DepartmentId",
                table: "ProcurementSchedules",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementSchedules_ProcurementPlans_ProcurementPlanId",
                table: "ProcurementSchedules",
                column: "ProcurementPlanId",
                principalTable: "ProcurementPlans",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItems_ItemUnitOfMeasure_ItemUnitOfMeasureId",
                table: "PurchaseOrderItems",
                column: "ItemUnitOfMeasureId",
                principalTable: "ItemUnitOfMeasure",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RequestForQuotationAwardLines_BusinessPartners_BusinessPartnerId",
                table: "RequestForQuotationAwardLines",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestForQuotationAwardLines_RequestForQuotationItems_RfqItemId",
                table: "RequestForQuotationAwardLines",
                column: "RfqItemId",
                principalTable: "RequestForQuotationItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestForQuotationAwardLines_RequestForQuotationQuotes_QuoteId",
                table: "RequestForQuotationAwardLines",
                column: "QuoteId",
                principalTable: "RequestForQuotationQuotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestForQuotationInvitations_BusinessPartners_BusinessPartnerId",
                table: "RequestForQuotationInvitations",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestForQuotationQuoteItems_RequestForQuotationItems_RfqItemId",
                table: "RequestForQuotationQuoteItems",
                column: "RfqItemId",
                principalTable: "RequestForQuotationItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestForQuotationQuotes_BusinessPartners_BusinessPartnerId",
                table: "RequestForQuotationQuotes",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StockAdjustments_Warehouses_WarehouseId",
                table: "StockAdjustments",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Warehouses_WarehouseId",
                table: "StockMovements",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicianSkillAssignments_Technicians_TechnicianId1",
                table: "TechnicianSkillAssignments",
                column: "TechnicianId1",
                principalTable: "Technicians",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicianTeams_Technicians_TechnicianId",
                table: "TechnicianTeams",
                column: "TechnicianId",
                principalTable: "Technicians",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAssignments_Users_AssignedById",
                table: "TenderAssignments",
                column: "AssignedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwards_BusinessPartners_BusinessPartnerId",
                table: "TenderAwards",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwards_TenderBidLot_BidLotId",
                table: "TenderAwards",
                column: "BidLotId",
                principalTable: "TenderBidLot",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwards_TenderBids_TenderBidId",
                table: "TenderAwards",
                column: "TenderBidId",
                principalTable: "TenderBids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwards_TenderLots_LotId",
                table: "TenderAwards",
                column: "LotId",
                principalTable: "TenderLots",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwards_TenderNegotiations_NegotiationId",
                table: "TenderAwards",
                column: "NegotiationId",
                principalTable: "TenderNegotiations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwards_Tenders_TenderId",
                table: "TenderAwards",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwardVerificationBidders_BusinessPartners_BusinessPartnerId",
                table: "TenderAwardVerificationBidders",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwardVerificationBidders_TenderBids_TenderBidId",
                table: "TenderAwardVerificationBidders",
                column: "TenderBidId",
                principalTable: "TenderBids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwardVerificationItemDocuments_Users_UploadedById",
                table: "TenderAwardVerificationItemDocuments",
                column: "UploadedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwardVerificationItemResults_AwardVerificationChecklistItems_ChecklistItemId",
                table: "TenderAwardVerificationItemResults",
                column: "ChecklistItemId",
                principalTable: "AwardVerificationChecklistItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwardVerifications_AwardVerificationChecklistTemplates_TemplateId",
                table: "TenderAwardVerifications",
                column: "TemplateId",
                principalTable: "AwardVerificationChecklistTemplates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwardVerifications_Tenders_TenderId",
                table: "TenderAwardVerifications",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidDocument_Tenants_TenantId",
                table: "TenderBidDocument",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidDocument_TenderBids_TenderBidId",
                table: "TenderBidDocument",
                column: "TenderBidId",
                principalTable: "TenderBids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidDocument_Users_UploadedById",
                table: "TenderBidDocument",
                column: "UploadedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidItem_Tenants_TenantId",
                table: "TenderBidItem",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidItem_TenderBidLot_BidLotId",
                table: "TenderBidItem",
                column: "BidLotId",
                principalTable: "TenderBidLot",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidItem_TenderBids_TenderBidId",
                table: "TenderBidItem",
                column: "TenderBidId",
                principalTable: "TenderBids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidItem_TenderItems_TenderItemId",
                table: "TenderBidItem",
                column: "TenderItemId",
                principalTable: "TenderItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidLot_Tenants_TenantId",
                table: "TenderBidLot",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidLot_TenderBids_TenderBidId",
                table: "TenderBidLot",
                column: "TenderBidId",
                principalTable: "TenderBids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidLot_TenderLots_LotId",
                table: "TenderBidLot",
                column: "LotId",
                principalTable: "TenderLots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBids_BusinessPartners_BusinessPartnerId",
                table: "TenderBids",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBids_Tenders_TenderId",
                table: "TenderBids",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderClarification_BusinessPartners_BusinessPartnerId",
                table: "TenderClarification",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderClarification_Tenants_TenantId",
                table: "TenderClarification",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderClarification_Tenders_TenderId",
                table: "TenderClarification",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderClarification_Users_AnsweredById",
                table: "TenderClarification",
                column: "AnsweredById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderClarification_Users_QuestionById",
                table: "TenderClarification",
                column: "QuestionById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderDocument_Tenants_TenantId",
                table: "TenderDocument",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderDocument_Tenders_TenderId",
                table: "TenderDocument",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderDocument_Users_UploadedById",
                table: "TenderDocument",
                column: "UploadedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderEvaluation_Tenants_TenantId",
                table: "TenderEvaluation",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderEvaluation_TenderBids_TenderBidId",
                table: "TenderEvaluation",
                column: "TenderBidId",
                principalTable: "TenderBids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderEvaluation_TenderEvaluator_TenderEvaluatorId",
                table: "TenderEvaluation",
                column: "TenderEvaluatorId",
                principalTable: "TenderEvaluator",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderEvaluator_Tenants_TenantId",
                table: "TenderEvaluator",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderEvaluator_Tenders_TenderId",
                table: "TenderEvaluator",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderEvaluator_Users_AssignedById",
                table: "TenderEvaluator",
                column: "AssignedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderEvaluator_Users_UserId",
                table: "TenderEvaluator",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderInterview_Tenants_TenantId",
                table: "TenderInterview",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderInterview_TenderBids_TenderBidId",
                table: "TenderInterview",
                column: "TenderBidId",
                principalTable: "TenderBids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderInterview_Tenders_TenderId",
                table: "TenderInterview",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderInterview_Users_ConductedById",
                table: "TenderInterview",
                column: "ConductedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderInvitation_BusinessPartners_BusinessPartnerId",
                table: "TenderInvitation",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderInvitation_Tenants_TenantId",
                table: "TenderInvitation",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderInvitation_Tenders_TenderId",
                table: "TenderInvitation",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderInvitation_Users_InvitedById",
                table: "TenderInvitation",
                column: "InvitedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderItems_TenderLots_LotId",
                table: "TenderItems",
                column: "LotId",
                principalTable: "TenderLots",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderNegotiationItems_TenderBidItem_TenderBidItemId",
                table: "TenderNegotiationItems",
                column: "TenderBidItemId",
                principalTable: "TenderBidItem",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderNegotiations_TenderBidLot_BidLotId",
                table: "TenderNegotiations",
                column: "BidLotId",
                principalTable: "TenderBidLot",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderRevision_Tenants_TenantId",
                table: "TenderRevision",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderRevision_Tenders_TenderId",
                table: "TenderRevision",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderRevision_Users_RevisedById",
                table: "TenderRevision",
                column: "RevisedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenders_EvaluationTemplate_EvaluationTemplateId",
                table: "Tenders",
                column: "EvaluationTemplateId",
                principalTable: "EvaluationTemplate",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasure_Tenants_TenantId",
                table: "UnitOfMeasure",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasureConversion_Tenants_TenantId",
                table: "UnitOfMeasureConversion",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasureConversion_UnitOfMeasure_FromUnitId",
                table: "UnitOfMeasureConversion",
                column: "FromUnitId",
                principalTable: "UnitOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasureConversion_UnitOfMeasure_ToUnitId",
                table: "UnitOfMeasureConversion",
                column: "ToUnitId",
                principalTable: "UnitOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasureSchedule_Tenants_TenantId",
                table: "UnitOfMeasureSchedule",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasureSchedule_UnitOfMeasure_BaseUnitOfMeasureId",
                table: "UnitOfMeasureSchedule",
                column: "BaseUnitOfMeasureId",
                principalTable: "UnitOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasureScheduleDetail_Tenants_TenantId",
                table: "UnitOfMeasureScheduleDetail",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasureScheduleDetail_UnitOfMeasureSchedule_ScheduleId",
                table: "UnitOfMeasureScheduleDetail",
                column: "ScheduleId",
                principalTable: "UnitOfMeasureSchedule",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasureScheduleDetail_UnitOfMeasure_UnitOfMeasureId",
                table: "UnitOfMeasureScheduleDetail",
                column: "UnitOfMeasureId",
                principalTable: "UnitOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrderLabor_Employees_TechnicianId",
                table: "WorkOrderLabor",
                column: "TechnicianId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountBalances_FiscalPeriods_FiscalPeriodId",
                table: "AccountBalances",
                column: "FiscalPeriodId",
                principalTable: "FiscalPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountTransactions_FiscalPeriods_FiscalPeriodId",
                table: "AccountTransactions",
                column: "FiscalPeriodId",
                principalTable: "FiscalPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountTransactions_FiscalPeriods_FiscalPeriodId1",
                table: "AccountTransactions",
                column: "FiscalPeriodId1",
                principalTable: "FiscalPeriods",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountTransactions_JournalEntries_JournalEntryId",
                table: "AccountTransactions",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountTransactions_JournalEntries_JournalEntryId1",
                table: "AccountTransactions",
                column: "JournalEntryId1",
                principalTable: "JournalEntries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetDepreciationSchedules_FiscalPeriods_FiscalPeriodId",
                table: "AssetDepreciationSchedules",
                column: "FiscalPeriodId",
                principalTable: "FiscalPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetDepreciationSchedules_JournalEntries_JournalEntryId",
                table: "AssetDepreciationSchedules",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetDisposals_JournalEntries_JournalEntryId",
                table: "AssetDisposals",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_BudgetReturns_BudgetReturnId",
                table: "BudgetEntries",
                column: "BudgetReturnId",
                principalTable: "BudgetReturns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_FiscalPeriods_FiscalPeriodId",
                table: "BudgetEntries",
                column: "FiscalPeriodId",
                principalTable: "FiscalPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetReturns_BudgetScenarios_BudgetScenarioId",
                table: "BudgetReturns",
                column: "BudgetScenarioId",
                principalTable: "BudgetScenarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetScenarios_FiscalYears_FiscalYearId",
                table: "BudgetScenarios",
                column: "FiscalYearId",
                principalTable: "FiscalYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FiscalPeriods_FiscalYears_FiscalYearId",
                table: "FiscalPeriods",
                column: "FiscalYearId",
                principalTable: "FiscalYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartners_BusinessPartners_ParentId",
                table: "BusinessPartners");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartnerUsers_Users_UserId",
                table: "BusinessPartnerUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_BusinessPartners_BusinessPartnerId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_TenderAwards_TenderAwardId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Tenders_TenderId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationCriterion_Tenants_TenantId",
                table: "EvaluationCriterion");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationCriterion_Users_CreatedById",
                table: "EvaluationCriterion");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationTemplate_Tenants_TenantId",
                table: "EvaluationTemplate");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationTemplate_Users_CreatedById",
                table: "EvaluationTemplate");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationTemplateCriterion_EvaluationCriterion_EvaluationCriterionId",
                table: "EvaluationTemplateCriterion");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationTemplateCriterion_EvaluationTemplate_EvaluationTemplateId",
                table: "EvaluationTemplateCriterion");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationTemplateCriterion_Tenants_TenantId",
                table: "EvaluationTemplateCriterion");

            migrationBuilder.DropForeignKey(
                name: "FK_FleetBatteryEvents_MaintenanceAssets_VehicleAssetId",
                table: "FleetBatteryEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_FleetComplianceItems_MaintenanceAssets_VehicleAssetId",
                table: "FleetComplianceItems");

            migrationBuilder.DropForeignKey(
                name: "FK_FleetCostEntries_FleetIncidents_FleetIncidentId",
                table: "FleetCostEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_FleetFuelTransactions_FleetTrips_FleetTripId",
                table: "FleetFuelTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_FleetFuelTransactions_MaintenanceAssets_VehicleAssetId",
                table: "FleetFuelTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_FleetTrips_Employees_DriverEmployeeId",
                table: "FleetTrips");

            migrationBuilder.DropForeignKey(
                name: "FK_FleetTrips_MaintenanceAssets_VehicleAssetId",
                table: "FleetTrips");

            migrationBuilder.DropForeignKey(
                name: "FK_FleetTyreEvents_MaintenanceAssets_VehicleAssetId",
                table: "FleetTyreEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryBalances_InventoryItems_InventoryItemId",
                table: "InventoryBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryBalances_Warehouses_WarehouseId",
                table: "InventoryBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryCategories_TaxGroups_DefaultTaxGroupId",
                table: "InventoryCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_TaxGroups_DefaultTaxGroupId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_UnitOfMeasureSchedule_UnitOfMeasureScheduleId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryLayers_InventoryItems_InventoryItemId",
                table: "InventoryLayers");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryLayers_Warehouses_WarehouseId",
                table: "InventoryLayers");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_InventoryItems_InventoryItemId",
                table: "InventoryMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_Warehouses_WarehouseId",
                table: "InventoryMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransferItem_InventoryItems_InventoryItemId",
                table: "InventoryTransferItem");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransferItem_InventoryTransfers_InventoryTransferId",
                table: "InventoryTransferItem");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransferItem_Tenants_TenantId",
                table: "InventoryTransferItem");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransferItem_WarehouseLocations_DestinationLocationId",
                table: "InventoryTransferItem");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransferItem_WarehouseLocations_SourceLocationId",
                table: "InventoryTransferItem");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransfers_Warehouses_DestinationWarehouseId",
                table: "InventoryTransfers");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransfers_Warehouses_SourceWarehouseId",
                table: "InventoryTransfers");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemUnitOfMeasure_InventoryItems_InventoryItemId",
                table: "ItemUnitOfMeasure");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemUnitOfMeasure_Tenants_TenantId",
                table: "ItemUnitOfMeasure");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemUnitOfMeasure_UnitOfMeasure_UnitOfMeasureId",
                table: "ItemUnitOfMeasure");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceSchedules_MaintenanceTypes_MaintenanceTypeId1",
                table: "MaintenanceSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationTopics_EmailTemplates_EmailTemplateId",
                table: "NotificationTopics");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceBondRequests_BusinessPartners_BusinessPartnerId",
                table: "PerformanceBondRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceBondRequests_TenderBids_TenderBidId",
                table: "PerformanceBondRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceReviews_Users_ReviewedById",
                table: "PerformanceReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_PriceHistories_MarketAnalyses_MarketAnalysisId",
                table: "PriceHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementBudgets_Departments_DepartmentId",
                table: "ProcurementBudgets");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementPlanItemSuppliers_BusinessPartners_BusinessPartnerId",
                table: "ProcurementPlanItemSuppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementPlans_Departments_DepartmentId",
                table: "ProcurementPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementSchedules_Departments_DepartmentId",
                table: "ProcurementSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementSchedules_ProcurementPlans_ProcurementPlanId",
                table: "ProcurementSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderItems_ItemUnitOfMeasure_ItemUnitOfMeasureId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestForQuotationAwardLines_BusinessPartners_BusinessPartnerId",
                table: "RequestForQuotationAwardLines");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestForQuotationAwardLines_RequestForQuotationItems_RfqItemId",
                table: "RequestForQuotationAwardLines");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestForQuotationAwardLines_RequestForQuotationQuotes_QuoteId",
                table: "RequestForQuotationAwardLines");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestForQuotationInvitations_BusinessPartners_BusinessPartnerId",
                table: "RequestForQuotationInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestForQuotationQuoteItems_RequestForQuotationItems_RfqItemId",
                table: "RequestForQuotationQuoteItems");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestForQuotationQuotes_BusinessPartners_BusinessPartnerId",
                table: "RequestForQuotationQuotes");

            migrationBuilder.DropForeignKey(
                name: "FK_StockAdjustments_Warehouses_WarehouseId",
                table: "StockAdjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Warehouses_WarehouseId",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_TechnicianSkillAssignments_Technicians_TechnicianId1",
                table: "TechnicianSkillAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_TechnicianTeams_Technicians_TechnicianId",
                table: "TechnicianTeams");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAssignments_Users_AssignedById",
                table: "TenderAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwards_BusinessPartners_BusinessPartnerId",
                table: "TenderAwards");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwards_TenderBidLot_BidLotId",
                table: "TenderAwards");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwards_TenderBids_TenderBidId",
                table: "TenderAwards");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwards_TenderLots_LotId",
                table: "TenderAwards");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwards_TenderNegotiations_NegotiationId",
                table: "TenderAwards");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwards_Tenders_TenderId",
                table: "TenderAwards");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwardVerificationBidders_BusinessPartners_BusinessPartnerId",
                table: "TenderAwardVerificationBidders");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwardVerificationBidders_TenderBids_TenderBidId",
                table: "TenderAwardVerificationBidders");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwardVerificationItemDocuments_Users_UploadedById",
                table: "TenderAwardVerificationItemDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwardVerificationItemResults_AwardVerificationChecklistItems_ChecklistItemId",
                table: "TenderAwardVerificationItemResults");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwardVerifications_AwardVerificationChecklistTemplates_TemplateId",
                table: "TenderAwardVerifications");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwardVerifications_Tenders_TenderId",
                table: "TenderAwardVerifications");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidDocument_Tenants_TenantId",
                table: "TenderBidDocument");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidDocument_TenderBids_TenderBidId",
                table: "TenderBidDocument");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidDocument_Users_UploadedById",
                table: "TenderBidDocument");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidItem_Tenants_TenantId",
                table: "TenderBidItem");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidItem_TenderBidLot_BidLotId",
                table: "TenderBidItem");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidItem_TenderBids_TenderBidId",
                table: "TenderBidItem");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidItem_TenderItems_TenderItemId",
                table: "TenderBidItem");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidLot_Tenants_TenantId",
                table: "TenderBidLot");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidLot_TenderBids_TenderBidId",
                table: "TenderBidLot");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidLot_TenderLots_LotId",
                table: "TenderBidLot");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBids_BusinessPartners_BusinessPartnerId",
                table: "TenderBids");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBids_Tenders_TenderId",
                table: "TenderBids");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderClarification_BusinessPartners_BusinessPartnerId",
                table: "TenderClarification");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderClarification_Tenants_TenantId",
                table: "TenderClarification");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderClarification_Tenders_TenderId",
                table: "TenderClarification");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderClarification_Users_AnsweredById",
                table: "TenderClarification");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderClarification_Users_QuestionById",
                table: "TenderClarification");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderDocument_Tenants_TenantId",
                table: "TenderDocument");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderDocument_Tenders_TenderId",
                table: "TenderDocument");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderDocument_Users_UploadedById",
                table: "TenderDocument");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderEvaluation_Tenants_TenantId",
                table: "TenderEvaluation");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderEvaluation_TenderBids_TenderBidId",
                table: "TenderEvaluation");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderEvaluation_TenderEvaluator_TenderEvaluatorId",
                table: "TenderEvaluation");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderEvaluator_Tenants_TenantId",
                table: "TenderEvaluator");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderEvaluator_Tenders_TenderId",
                table: "TenderEvaluator");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderEvaluator_Users_AssignedById",
                table: "TenderEvaluator");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderEvaluator_Users_UserId",
                table: "TenderEvaluator");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderInterview_Tenants_TenantId",
                table: "TenderInterview");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderInterview_TenderBids_TenderBidId",
                table: "TenderInterview");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderInterview_Tenders_TenderId",
                table: "TenderInterview");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderInterview_Users_ConductedById",
                table: "TenderInterview");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderInvitation_BusinessPartners_BusinessPartnerId",
                table: "TenderInvitation");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderInvitation_Tenants_TenantId",
                table: "TenderInvitation");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderInvitation_Tenders_TenderId",
                table: "TenderInvitation");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderInvitation_Users_InvitedById",
                table: "TenderInvitation");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderItems_TenderLots_LotId",
                table: "TenderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderNegotiationItems_TenderBidItem_TenderBidItemId",
                table: "TenderNegotiationItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderNegotiations_TenderBidLot_BidLotId",
                table: "TenderNegotiations");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderRevision_Tenants_TenantId",
                table: "TenderRevision");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderRevision_Tenders_TenderId",
                table: "TenderRevision");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderRevision_Users_RevisedById",
                table: "TenderRevision");

            migrationBuilder.DropForeignKey(
                name: "FK_Tenders_EvaluationTemplate_EvaluationTemplateId",
                table: "Tenders");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasure_Tenants_TenantId",
                table: "UnitOfMeasure");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasureConversion_Tenants_TenantId",
                table: "UnitOfMeasureConversion");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasureConversion_UnitOfMeasure_FromUnitId",
                table: "UnitOfMeasureConversion");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasureConversion_UnitOfMeasure_ToUnitId",
                table: "UnitOfMeasureConversion");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasureSchedule_Tenants_TenantId",
                table: "UnitOfMeasureSchedule");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasureSchedule_UnitOfMeasure_BaseUnitOfMeasureId",
                table: "UnitOfMeasureSchedule");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasureScheduleDetail_Tenants_TenantId",
                table: "UnitOfMeasureScheduleDetail");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasureScheduleDetail_UnitOfMeasureSchedule_ScheduleId",
                table: "UnitOfMeasureScheduleDetail");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasureScheduleDetail_UnitOfMeasure_UnitOfMeasureId",
                table: "UnitOfMeasureScheduleDetail");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrderLabor_Employees_TechnicianId",
                table: "WorkOrderLabor");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_FiscalPeriods_FiscalPeriodId",
                table: "JournalEntries");

            migrationBuilder.DropTable(
                name: "AccountBalances");

            migrationBuilder.DropTable(
                name: "AccountCurrencyLinks");

            migrationBuilder.DropTable(
                name: "AccountTransactions");

            migrationBuilder.DropTable(
                name: "AllocationTargets");

            migrationBuilder.DropTable(
                name: "AssetDepreciationSchedules");

            migrationBuilder.DropTable(
                name: "AssetDisposals");

            migrationBuilder.DropTable(
                name: "AssetTransactions");

            migrationBuilder.DropTable(
                name: "AssetTransfers");

            migrationBuilder.DropTable(
                name: "AssetVerificationItems");

            migrationBuilder.DropTable(
                name: "BudgetEntries");

            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropTable(
                name: "FinanceSettings");

            migrationBuilder.DropTable(
                name: "InvoiceLineItems");

            migrationBuilder.DropTable(
                name: "PaymentAllocations");

            migrationBuilder.DropTable(
                name: "PaymentBatchItems");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PeriodModuleLocks");

            migrationBuilder.DropTable(
                name: "RatioDefinitions");

            migrationBuilder.DropTable(
                name: "ReconciliationMatch");

            migrationBuilder.DropTable(
                name: "TaxCalculations");

            migrationBuilder.DropTable(
                name: "TaxGroupComponents");

            migrationBuilder.DropTable(
                name: "TaxRateHistory");

            migrationBuilder.DropTable(
                name: "TaxRates");

            migrationBuilder.DropTable(
                name: "TaxRules");

            migrationBuilder.DropTable(
                name: "TaxThresholds");

            migrationBuilder.DropTable(
                name: "TransactionDocumentModuleMappings");

            migrationBuilder.DropTable(
                name: "UnitAccountBalances");

            migrationBuilder.DropTable(
                name: "UnitAccountBudgets");

            migrationBuilder.DropTable(
                name: "UnitJournalEntryLines");

            migrationBuilder.DropTable(
                name: "VendorInvoiceLineItems");

            migrationBuilder.DropTable(
                name: "VendorPaymentAllocations");

            migrationBuilder.DropTable(
                name: "AllocationRules");

            migrationBuilder.DropTable(
                name: "AssetVerificationSessions");

            migrationBuilder.DropTable(
                name: "FixedAssets");

            migrationBuilder.DropTable(
                name: "BudgetReturns");

            migrationBuilder.DropTable(
                name: "CustomerPayments");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "BankStatementLine");

            migrationBuilder.DropTable(
                name: "TaxTypes");

            migrationBuilder.DropTable(
                name: "TaxGroups");

            migrationBuilder.DropTable(
                name: "Taxes");

            migrationBuilder.DropTable(
                name: "ModuleDefinitions");

            migrationBuilder.DropTable(
                name: "UnitJournalEntries");

            migrationBuilder.DropTable(
                name: "VendorInvoices");

            migrationBuilder.DropTable(
                name: "VendorPayments");

            migrationBuilder.DropTable(
                name: "UnitAccounts");

            migrationBuilder.DropTable(
                name: "FixedAssetCategories");

            migrationBuilder.DropTable(
                name: "AccountSegmentValues");

            migrationBuilder.DropTable(
                name: "BudgetScenarios");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "CashTransactions");

            migrationBuilder.DropTable(
                name: "PaymentBatches");

            migrationBuilder.DropTable(
                name: "UnitTypes");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "SegmentLookupValues");

            migrationBuilder.DropTable(
                name: "BankReconciliation");

            migrationBuilder.DropTable(
                name: "Cheque");

            migrationBuilder.DropTable(
                name: "PaymentMethod");

            migrationBuilder.DropTable(
                name: "AccountSegmentStructures");

            migrationBuilder.DropTable(
                name: "BankStatement");

            migrationBuilder.DropTable(
                name: "BankAccounts");

            migrationBuilder.DropTable(
                name: "FiscalPeriods");

            migrationBuilder.DropTable(
                name: "FiscalYears");

            migrationBuilder.DropTable(
                name: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_Tenders_TenantId",
                table: "Tenders");

            migrationBuilder.DropIndex(
                name: "IX_TenderBids_TenantId",
                table: "TenderBids");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianTeams_TechnicianId",
                table: "TechnicianTeams");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianSkillAssignments_TechnicianId1",
                table: "TechnicianSkillAssignments");

            migrationBuilder.DropIndex(
                name: "IX_SystemExceptionLogs_TenantId",
                table: "SystemExceptionLogs");

            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotations_TenantId",
                table: "RequestForQuotations");

            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotationQuotes_TenantId",
                table: "RequestForQuotationQuotes");

            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotationQuoteItems_TenantId",
                table: "RequestForQuotationQuoteItems");

            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotationItems_TenantId",
                table: "RequestForQuotationItems");

            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotationInvitations_TenantId",
                table: "RequestForQuotationInvitations");

            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotationAwardLines_TenantId",
                table: "RequestForQuotationAwardLines");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementPlanItemSuppliers_BusinessPartnerId",
                table: "ProcurementPlanItemSuppliers");

            migrationBuilder.DropIndex(
                name: "IX_NotificationTopics_TenantId",
                table: "NotificationTopics");

            migrationBuilder.DropIndex(
                name: "IX_NotificationTopicRecipients_TenantId",
                table: "NotificationTopicRecipients");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceSchedules_MaintenanceTypeId1",
                table: "MaintenanceSchedules");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_TenantId",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryLayers_TenantId",
                table: "InventoryLayers");

            migrationBuilder.DropIndex(
                name: "IX_InventoryCategories_DefaultTaxGroupId",
                table: "InventoryCategories");

            migrationBuilder.DropIndex(
                name: "IX_InventoryBalances_TenantId",
                table: "InventoryBalances");

            migrationBuilder.DropIndex(
                name: "IX_FleetVehicleAssignments_TenantId",
                table: "FleetVehicleAssignments");

            migrationBuilder.DropIndex(
                name: "IX_FleetTyres_TenantId",
                table: "FleetTyres");

            migrationBuilder.DropIndex(
                name: "IX_FleetTyreEvents_TenantId",
                table: "FleetTyreEvents");

            migrationBuilder.DropIndex(
                name: "IX_FleetTrips_TenantId",
                table: "FleetTrips");

            migrationBuilder.DropIndex(
                name: "IX_FleetTripInspections_TenantId",
                table: "FleetTripInspections");

            migrationBuilder.DropIndex(
                name: "IX_FleetIncidents_TenantId",
                table: "FleetIncidents");

            migrationBuilder.DropIndex(
                name: "IX_FleetFuelTransactions_TenantId",
                table: "FleetFuelTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FleetExternalRepairs_TenantId",
                table: "FleetExternalRepairs");

            migrationBuilder.DropIndex(
                name: "IX_FleetDefects_TenantId",
                table: "FleetDefects");

            migrationBuilder.DropIndex(
                name: "IX_FleetCostEntries_TenantId",
                table: "FleetCostEntries");

            migrationBuilder.DropIndex(
                name: "IX_FleetComplianceItems_TenantId",
                table: "FleetComplianceItems");

            migrationBuilder.DropIndex(
                name: "IX_FleetBatteryEvents_TenantId",
                table: "FleetBatteryEvents");

            migrationBuilder.DropIndex(
                name: "IX_FleetBatteries_TenantId",
                table: "FleetBatteries");

            migrationBuilder.DropIndex(
                name: "IX_EmailCampaigns_TenantId",
                table: "EmailCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_EmailCampaignRecipients_TenantId",
                table: "EmailCampaignRecipients");

            migrationBuilder.DropIndex(
                name: "IX_Currencies_TenantId",
                table: "Currencies");

            migrationBuilder.DropIndex(
                name: "IX_AwardVerificationChecklistTemplates_TenantId",
                table: "AwardVerificationChecklistTemplates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UnitOfMeasureScheduleDetail",
                table: "UnitOfMeasureScheduleDetail");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UnitOfMeasureSchedule",
                table: "UnitOfMeasureSchedule");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UnitOfMeasureConversion",
                table: "UnitOfMeasureConversion");

            migrationBuilder.DropIndex(
                name: "IX_UnitOfMeasureConversion_FromUnitId",
                table: "UnitOfMeasureConversion");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UnitOfMeasure",
                table: "UnitOfMeasure");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderRevision",
                table: "TenderRevision");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderInvitation",
                table: "TenderInvitation");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderInterview",
                table: "TenderInterview");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderEvaluator",
                table: "TenderEvaluator");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderEvaluation",
                table: "TenderEvaluation");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderDocument",
                table: "TenderDocument");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderClarification",
                table: "TenderClarification");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderBidLot",
                table: "TenderBidLot");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderBidItem",
                table: "TenderBidItem");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenderBidDocument",
                table: "TenderBidDocument");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ItemUnitOfMeasure",
                table: "ItemUnitOfMeasure");

            migrationBuilder.DropIndex(
                name: "IX_ItemUnitOfMeasure_InventoryItemId",
                table: "ItemUnitOfMeasure");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InventoryTransferItem",
                table: "InventoryTransferItem");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EvaluationTemplateCriterion",
                table: "EvaluationTemplateCriterion");

            migrationBuilder.DropIndex(
                name: "IX_EvaluationTemplateCriterion_EvaluationTemplateId",
                table: "EvaluationTemplateCriterion");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EvaluationTemplate",
                table: "EvaluationTemplate");

            migrationBuilder.DropIndex(
                name: "IX_EvaluationTemplate_TenantId",
                table: "EvaluationTemplate");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EvaluationCriterion",
                table: "EvaluationCriterion");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000001"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000002"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000003"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000004"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000005"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000006"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000007"));

            migrationBuilder.DropColumn(
                name: "BaseCurrency",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BaseCurrencyName",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CurrencyDecimalPlaces",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CurrencySymbol",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TechnicianId",
                table: "TechnicianTeams");

            migrationBuilder.DropColumn(
                name: "TechnicianId1",
                table: "TechnicianSkillAssignments");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerId",
                table: "ProcurementPlanItemSuppliers");

            migrationBuilder.DropColumn(
                name: "MaintenanceTypeId1",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "DefaultTaxGroupId",
                table: "InventoryCategories");

            migrationBuilder.DropColumn(
                name: "AccountLinkageCount",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "ActivationDate",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "AutoRetrieveExchangeRate",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "CentralBank",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "CountriesUsingCurrency",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "CountryName",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "CurrencyClassification",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "CurrencySymbol",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "DeactivationDate",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "DecimalSeparator",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "DefaultRateType",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "DigitGrouping",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "EffectiveDate",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "ExchangeRateUpdateFrequency",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "ExpirationDate",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "FirstTransactionDate",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "FormatExample",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "GeographicRegion",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "HasAccountLinkages",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "HasBeenRedenominated",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "HasRestrictions",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "HasTransactionHistory",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "LastTransactionDate",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "MinorUnitName",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "MinorUnitRatio",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "PreviousCurrencyCode",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "RateVarianceThresholdPercentage",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "RedenominationNotes",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "RedenominationRatio",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "RestrictionsDescription",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "RoundingMethod",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "SymbolPosition",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "ThousandsSeparator",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "TransactionCount",
                table: "Currencies");

            migrationBuilder.RenameTable(
                name: "UnitOfMeasureScheduleDetail",
                newName: "UnitOfMeasureScheduleDetails");

            migrationBuilder.RenameTable(
                name: "UnitOfMeasureSchedule",
                newName: "UnitOfMeasureSchedules");

            migrationBuilder.RenameTable(
                name: "UnitOfMeasureConversion",
                newName: "UnitOfMeasureConversions");

            migrationBuilder.RenameTable(
                name: "UnitOfMeasure",
                newName: "UnitsOfMeasure");

            migrationBuilder.RenameTable(
                name: "TenderRevision",
                newName: "TenderRevisions");

            migrationBuilder.RenameTable(
                name: "TenderInvitation",
                newName: "TenderInvitations");

            migrationBuilder.RenameTable(
                name: "TenderInterview",
                newName: "TenderInterviews");

            migrationBuilder.RenameTable(
                name: "TenderEvaluator",
                newName: "TenderEvaluators");

            migrationBuilder.RenameTable(
                name: "TenderEvaluation",
                newName: "TenderEvaluations");

            migrationBuilder.RenameTable(
                name: "TenderDocument",
                newName: "TenderDocuments");

            migrationBuilder.RenameTable(
                name: "TenderClarification",
                newName: "TenderClarifications");

            migrationBuilder.RenameTable(
                name: "TenderBidLot",
                newName: "TenderBidLots");

            migrationBuilder.RenameTable(
                name: "TenderBidItem",
                newName: "TenderBidItems");

            migrationBuilder.RenameTable(
                name: "TenderBidDocument",
                newName: "TenderBidDocuments");

            migrationBuilder.RenameTable(
                name: "ItemUnitOfMeasure",
                newName: "ItemUnitsOfMeasure");

            migrationBuilder.RenameTable(
                name: "InventoryTransferItem",
                newName: "InventoryTransferItems");

            migrationBuilder.RenameTable(
                name: "EvaluationTemplateCriterion",
                newName: "EvaluationTemplateCriteria");

            migrationBuilder.RenameTable(
                name: "EvaluationTemplate",
                newName: "EvaluationTemplates");

            migrationBuilder.RenameTable(
                name: "EvaluationCriterion",
                newName: "EvaluationCriteria");

            migrationBuilder.RenameColumn(
                name: "DefaultTaxGroupId",
                table: "InventoryItems",
                newName: "SubstituteItem4Id");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryItems_DefaultTaxGroupId",
                table: "InventoryItems",
                newName: "IX_InventoryItems_SubstituteItem4Id");

            migrationBuilder.RenameColumn(
                name: "RoundingPrecision",
                table: "Currencies",
                newName: "ExchangeRate");

            migrationBuilder.RenameColumn(
                name: "RedenominationDate",
                table: "Currencies",
                newName: "ExchangeRateDate");

            migrationBuilder.RenameColumn(
                name: "PluralName",
                table: "Currencies",
                newName: "Country");

            migrationBuilder.RenameColumn(
                name: "NumericCode",
                table: "Currencies",
                newName: "Code");

            migrationBuilder.RenameColumn(
                name: "MinorUnitPluralName",
                table: "Currencies",
                newName: "FormatString");

            migrationBuilder.RenameColumn(
                name: "CurrencyName",
                table: "Currencies",
                newName: "Name");

            migrationBuilder.RenameIndex(
                name: "IX_UnitOfMeasureScheduleDetail_UnitOfMeasureId",
                table: "UnitOfMeasureScheduleDetails",
                newName: "IX_UnitOfMeasureScheduleDetails_UnitOfMeasureId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitOfMeasureScheduleDetail_TenantId",
                table: "UnitOfMeasureScheduleDetails",
                newName: "IX_UnitOfMeasureScheduleDetails_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitOfMeasureScheduleDetail_ScheduleId",
                table: "UnitOfMeasureScheduleDetails",
                newName: "IX_UnitOfMeasureScheduleDetails_ScheduleId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitOfMeasureSchedule_TenantId",
                table: "UnitOfMeasureSchedules",
                newName: "IX_UnitOfMeasureSchedules_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitOfMeasureSchedule_BaseUnitOfMeasureId",
                table: "UnitOfMeasureSchedules",
                newName: "IX_UnitOfMeasureSchedules_BaseUnitOfMeasureId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitOfMeasureConversion_ToUnitId",
                table: "UnitOfMeasureConversions",
                newName: "IX_UnitOfMeasureConversions_ToUnitId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitOfMeasureConversion_TenantId",
                table: "UnitOfMeasureConversions",
                newName: "IX_UnitOfMeasureConversions_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitOfMeasure_TenantId",
                table: "UnitsOfMeasure",
                newName: "IX_UnitsOfMeasure_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderRevision_TenderId",
                table: "TenderRevisions",
                newName: "IX_TenderRevisions_TenderId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderRevision_TenantId",
                table: "TenderRevisions",
                newName: "IX_TenderRevisions_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderRevision_RevisedById",
                table: "TenderRevisions",
                newName: "IX_TenderRevisions_RevisedById");

            migrationBuilder.RenameIndex(
                name: "IX_TenderInvitation_TenderId",
                table: "TenderInvitations",
                newName: "IX_TenderInvitations_TenderId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderInvitation_TenantId",
                table: "TenderInvitations",
                newName: "IX_TenderInvitations_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderInvitation_InvitedById",
                table: "TenderInvitations",
                newName: "IX_TenderInvitations_InvitedById");

            migrationBuilder.RenameIndex(
                name: "IX_TenderInvitation_BusinessPartnerId",
                table: "TenderInvitations",
                newName: "IX_TenderInvitations_BusinessPartnerId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderInterview_TenderId",
                table: "TenderInterviews",
                newName: "IX_TenderInterviews_TenderId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderInterview_TenderBidId",
                table: "TenderInterviews",
                newName: "IX_TenderInterviews_TenderBidId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderInterview_TenantId",
                table: "TenderInterviews",
                newName: "IX_TenderInterviews_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderInterview_ConductedById",
                table: "TenderInterviews",
                newName: "IX_TenderInterviews_ConductedById");

            migrationBuilder.RenameIndex(
                name: "IX_TenderEvaluator_UserId",
                table: "TenderEvaluators",
                newName: "IX_TenderEvaluators_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderEvaluator_TenderId",
                table: "TenderEvaluators",
                newName: "IX_TenderEvaluators_TenderId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderEvaluator_TenantId",
                table: "TenderEvaluators",
                newName: "IX_TenderEvaluators_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderEvaluator_AssignedById",
                table: "TenderEvaluators",
                newName: "IX_TenderEvaluators_AssignedById");

            migrationBuilder.RenameIndex(
                name: "IX_TenderEvaluation_TenderEvaluatorId",
                table: "TenderEvaluations",
                newName: "IX_TenderEvaluations_TenderEvaluatorId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderEvaluation_TenderBidId",
                table: "TenderEvaluations",
                newName: "IX_TenderEvaluations_TenderBidId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderEvaluation_TenantId",
                table: "TenderEvaluations",
                newName: "IX_TenderEvaluations_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderDocument_UploadedById",
                table: "TenderDocuments",
                newName: "IX_TenderDocuments_UploadedById");

            migrationBuilder.RenameIndex(
                name: "IX_TenderDocument_TenderId",
                table: "TenderDocuments",
                newName: "IX_TenderDocuments_TenderId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderDocument_TenantId",
                table: "TenderDocuments",
                newName: "IX_TenderDocuments_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderClarification_TenderId",
                table: "TenderClarifications",
                newName: "IX_TenderClarifications_TenderId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderClarification_TenantId",
                table: "TenderClarifications",
                newName: "IX_TenderClarifications_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderClarification_QuestionById",
                table: "TenderClarifications",
                newName: "IX_TenderClarifications_QuestionById");

            migrationBuilder.RenameIndex(
                name: "IX_TenderClarification_BusinessPartnerId",
                table: "TenderClarifications",
                newName: "IX_TenderClarifications_BusinessPartnerId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderClarification_AnsweredById",
                table: "TenderClarifications",
                newName: "IX_TenderClarifications_AnsweredById");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidLot_TenderBidId",
                table: "TenderBidLots",
                newName: "IX_TenderBidLots_TenderBidId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidLot_TenantId",
                table: "TenderBidLots",
                newName: "IX_TenderBidLots_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidLot_LotId",
                table: "TenderBidLots",
                newName: "IX_TenderBidLots_LotId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidItem_TenderItemId",
                table: "TenderBidItems",
                newName: "IX_TenderBidItems_TenderItemId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidItem_TenderBidId",
                table: "TenderBidItems",
                newName: "IX_TenderBidItems_TenderBidId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidItem_TenantId",
                table: "TenderBidItems",
                newName: "IX_TenderBidItems_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidItem_BidLotId",
                table: "TenderBidItems",
                newName: "IX_TenderBidItems_BidLotId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidDocument_UploadedById",
                table: "TenderBidDocuments",
                newName: "IX_TenderBidDocuments_UploadedById");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidDocument_TenderBidId",
                table: "TenderBidDocuments",
                newName: "IX_TenderBidDocuments_TenderBidId");

            migrationBuilder.RenameIndex(
                name: "IX_TenderBidDocument_TenantId",
                table: "TenderBidDocuments",
                newName: "IX_TenderBidDocuments_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_ItemUnitOfMeasure_UnitOfMeasureId",
                table: "ItemUnitsOfMeasure",
                newName: "IX_ItemUnitsOfMeasure_UnitOfMeasureId");

            migrationBuilder.RenameIndex(
                name: "IX_ItemUnitOfMeasure_TenantId",
                table: "ItemUnitsOfMeasure",
                newName: "IX_ItemUnitsOfMeasure_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryTransferItem_TenantId",
                table: "InventoryTransferItems",
                newName: "IX_InventoryTransferItems_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryTransferItem_SourceLocationId",
                table: "InventoryTransferItems",
                newName: "IX_InventoryTransferItems_SourceLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryTransferItem_InventoryTransferId",
                table: "InventoryTransferItems",
                newName: "IX_InventoryTransferItems_InventoryTransferId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryTransferItem_InventoryItemId",
                table: "InventoryTransferItems",
                newName: "IX_InventoryTransferItems_InventoryItemId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryTransferItem_DestinationLocationId",
                table: "InventoryTransferItems",
                newName: "IX_InventoryTransferItems_DestinationLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_EvaluationTemplateCriterion_TenantId",
                table: "EvaluationTemplateCriteria",
                newName: "IX_EvaluationTemplateCriteria_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_EvaluationTemplateCriterion_EvaluationCriterionId",
                table: "EvaluationTemplateCriteria",
                newName: "IX_EvaluationTemplateCriteria_EvaluationCriterionId");

            migrationBuilder.RenameIndex(
                name: "IX_EvaluationTemplate_CreatedById",
                table: "EvaluationTemplates",
                newName: "IX_EvaluationTemplates_CreatedById");

            migrationBuilder.RenameIndex(
                name: "IX_EvaluationCriterion_TenantId",
                table: "EvaluationCriteria",
                newName: "IX_EvaluationCriteria_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_EvaluationCriterion_CreatedById",
                table: "EvaluationCriteria",
                newName: "IX_EvaluationCriteria_CreatedById");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "CloseTime",
                table: "Warehouses",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostCenter",
                table: "Warehouses",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultInTransitLocationId",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultQuarantineLocationId",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultReceivingLocationId",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultShippingLocationId",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GLAccountCode",
                table: "Warehouses",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasInspectionArea",
                table: "Warehouses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasQuarantineArea",
                table: "Warehouses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasReceivingDock",
                table: "Warehouses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasShippingDock",
                table: "Warehouses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTemperatureControlled",
                table: "Warehouses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ManagerId",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxTemperature",
                table: "Warehouses",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxWeightCapacity",
                table: "Warehouses",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinTemperature",
                table: "Warehouses",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "OpenTime",
                table: "Warehouses",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingHours",
                table: "Warehouses",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemperatureUnit",
                table: "Warehouses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCapacityCubicFeet",
                table: "Warehouses",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCapacitySquareFeet",
                table: "Warehouses",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalLocations",
                table: "Warehouses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UsedCapacitySquareFeet",
                table: "Warehouses",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsedLocations",
                table: "Warehouses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ABCClass",
                table: "WarehouseLocations",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aisle",
                table: "WarehouseLocations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bin",
                table: "WarehouseLocations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ColumnNumber",
                table: "WarehouseLocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DedicatedItemCode",
                table: "WarehouseLocations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DedicatedItemId",
                table: "WarehouseLocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDamageLocation",
                table: "WarehouseLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsInTransitLocation",
                table: "WarehouseLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsInspectionLocation",
                table: "WarehouseLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsQuarantineLocation",
                table: "WarehouseLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReturnLocation",
                table: "WarehouseLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsShippingLocation",
                table: "WarehouseLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsStagingLocation",
                table: "WarehouseLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LevelNumber",
                table: "WarehouseLocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationBarcode",
                table: "WarehouseLocations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LocationHierarchyType",
                table: "WarehouseLocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PickSequence",
                table: "WarehouseLocations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Rack",
                table: "WarehouseLocations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RowNumber",
                table: "WarehouseLocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Shelf",
                table: "WarehouseLocations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemperatureZone",
                table: "WarehouseLocations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Zone",
                table: "WarehouseLocations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "TenderLots",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "LotCode",
                table: "TenderLots",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "WarehouseId",
                table: "StockMovements",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "WarehouseId",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Reference",
                table: "StockAdjustments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<bool>(
                name: "IsSystem",
                table: "NotificationTopics",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsRequired",
                table: "NotificationTopics",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsSystem",
                table: "NotificationTopicRecipients",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<bool>(
                name: "AllowBackorder",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AlternateBarcode",
                table: "InventoryItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoReorder",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "InventoryItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BaseUnitOfMeasureEntityId",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BaseUnitOfMeasureId",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryOfOrigin",
                table: "InventoryItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyDecimals",
                table: "InventoryItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentCost",
                table: "InventoryItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DaysBeforeExpiryWarning",
                table: "InventoryItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultWarehouseId",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Feature",
                table: "InventoryItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenericDescription",
                table: "InventoryItems",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HSCode",
                table: "InventoryItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "InventoryItems",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInFulfillment",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInInvoices",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInOrders",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInQuotes",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFinishedGood",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFinishedGoodComponent",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsKit",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsKitComponent",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsProcurementItem",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTaxable",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ItemClassId",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ListPrice",
                table: "InventoryItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "LongDescription",
                table: "InventoryItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LotCategory",
                table: "InventoryItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MaintainCalendarYearHistory",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MaintainFiscalYearHistory",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MaintainTransactionHistory",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumOrderQuantity",
                table: "InventoryItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MinimumShelfLifeDays",
                table: "InventoryItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MovementClass",
                table: "InventoryItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OrderMultiple",
                table: "InventoryItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceGroupId",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimarySupplierId",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseTaxOption",
                table: "InventoryItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseTaxScheduleId",
                table: "InventoryItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QRCode",
                table: "InventoryItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantityDecimals",
                table: "InventoryItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequireApprovalForPurchase",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SalesTaxOption",
                table: "InventoryItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesTaxScheduleId",
                table: "InventoryItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortDescription",
                table: "InventoryItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Style",
                table: "InventoryItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubCategoryId",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubstituteItem1Id",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubstituteItem2Id",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubstituteItem3Id",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxCode",
                table: "InventoryItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailUrl",
                table: "InventoryItems",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WarnBeforeLotExpires",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "WarrantyDays",
                table: "InventoryItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "XYZClass",
                table: "InventoryItems",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "FleetCostEntries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Manual",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "Symbol",
                table: "Currencies",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Contracts",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD",
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<string>(
                name: "TemplateName",
                table: "EvaluationTemplates",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "EvaluationTemplates",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "EvaluationTemplates",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UnitOfMeasureScheduleDetails",
                table: "UnitOfMeasureScheduleDetails",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UnitOfMeasureSchedules",
                table: "UnitOfMeasureSchedules",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UnitOfMeasureConversions",
                table: "UnitOfMeasureConversions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UnitsOfMeasure",
                table: "UnitsOfMeasure",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderRevisions",
                table: "TenderRevisions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderInvitations",
                table: "TenderInvitations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderInterviews",
                table: "TenderInterviews",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderEvaluators",
                table: "TenderEvaluators",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderEvaluations",
                table: "TenderEvaluations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderDocuments",
                table: "TenderDocuments",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderClarifications",
                table: "TenderClarifications",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderBidLots",
                table: "TenderBidLots",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderBidItems",
                table: "TenderBidItems",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenderBidDocuments",
                table: "TenderBidDocuments",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ItemUnitsOfMeasure",
                table: "ItemUnitsOfMeasure",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InventoryTransferItems",
                table: "InventoryTransferItems",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EvaluationTemplateCriteria",
                table: "EvaluationTemplateCriteria",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EvaluationTemplates",
                table: "EvaluationTemplates",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EvaluationCriteria",
                table: "EvaluationCriteria",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ConsignmentSettlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoicedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvoicedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MovementType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReferenceType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsignmentSettlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsignmentSettlements_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalTable: "StockMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsignmentSettlements_Tenants_TenantId",
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

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(4765));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(4841));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(4844));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(4847));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5231));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5243));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5252));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5261));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5282));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5293));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5301));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5308));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5340));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5353));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5368));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5376));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5390));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5406));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5414));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5423));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5502));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5504));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5505));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5506));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5507));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5509));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5510));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5511));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5511));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5513));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5514));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5515));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5516));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5516));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5517));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5518));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5600));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5603));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5604));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5604));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5606));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5606));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5607));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5608));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5609));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5610));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5611));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5611));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5612));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5613));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5614));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5681));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5683));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5685));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5686));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5687));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5687));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5688));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5689));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5690));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5691));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5705));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5706));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5715));

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
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(4447));

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_DefaultInTransitLocationId",
                table: "Warehouses",
                column: "DefaultInTransitLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_DefaultQuarantineLocationId",
                table: "Warehouses",
                column: "DefaultQuarantineLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_DefaultReceivingLocationId",
                table: "Warehouses",
                column: "DefaultReceivingLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_DefaultShippingLocationId",
                table: "Warehouses",
                column: "DefaultShippingLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_ManagerId",
                table: "Warehouses",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_DedicatedItemId",
                table: "WarehouseLocations",
                column: "DedicatedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenders_PublishDate",
                table: "Tenders",
                column: "PublishDate");

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
                name: "IX_TenderLots_Status",
                table: "TenderLots",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TenderLots_TenderId_LotCode",
                table: "TenderLots",
                columns: new[] { "TenderId", "LotCode" },
                unique: true);

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
                name: "IX_TenderAwardVerifications_Status",
                table: "TenderAwardVerifications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationItemResults_BidderId_ChecklistItemId",
                table: "TenderAwardVerificationItemResults",
                columns: new[] { "BidderId", "ChecklistItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwards_Status",
                table: "TenderAwards",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAssignments_AssignmentType",
                table: "TenderAssignments",
                column: "AssignmentType");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAssignments_TenderId_BusinessPartnerId_AssignedToUserId",
                table: "TenderAssignments",
                columns: new[] { "TenderId", "BusinessPartnerId", "AssignedToUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_EmployeeNumber",
                table: "Technicians",
                column: "EmployeeNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_IsActive",
                table: "Technicians",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_Specialization",
                table: "Technicians",
                column: "Specialization");

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
                name: "IX_SupplierPerformanceMetrics_BusinessPartnerId_MetricPeriod_Year_Month_Quarter",
                table: "SupplierPerformanceMetrics",
                columns: new[] { "BusinessPartnerId", "MetricPeriod", "Year", "Month", "Quarter" },
                unique: true,
                filter: "[Month] IS NOT NULL AND [Quarter] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPerformanceMetrics_OverallPerformanceScore",
                table: "SupplierPerformanceMetrics",
                column: "OverallPerformanceScore");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPerformanceMetrics_Year",
                table: "SupplierPerformanceMetrics",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierConsolidations_ItemCategory",
                table: "SupplierConsolidations",
                column: "ItemCategory");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierConsolidations_Status",
                table: "SupplierConsolidations",
                column: "Status");

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
                name: "IX_RequestForQuotationQuotes_TenantId_RfqId_BusinessPartnerId",
                table: "RequestForQuotationQuotes",
                columns: new[] { "TenantId", "RfqId", "BusinessPartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationQuoteItems_TenantId_QuoteId_RfqItemId",
                table: "RequestForQuotationQuoteItems",
                columns: new[] { "TenantId", "QuoteId", "RfqItemId" },
                unique: true,
                filter: "[IsDeleted] = 0");

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
                name: "IX_RequestForQuotationInvitations_TenantId_RfqId_BusinessPartnerId",
                table: "RequestForQuotationInvitations",
                columns: new[] { "TenantId", "RfqId", "BusinessPartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationAwardLines_TenantId_RfqId_RfqItemId",
                table: "RequestForQuotationAwardLines",
                columns: new[] { "TenantId", "RfqId", "RfqItemId" },
                unique: true);

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
                name: "IX_QualityIncidents_Severity",
                table: "QualityIncidents",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_QualityIncidents_Status",
                table: "QualityIncidents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSchedules_PlannedStartDate",
                table: "ProcurementSchedules",
                column: "PlannedStartDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSchedules_Status",
                table: "ProcurementSchedules",
                column: "Status");

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
                name: "IX_ProcurementPlans_Status",
                table: "ProcurementPlans",
                column: "Status");

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
                name: "IX_ProcurementPlanItems_IsCritical",
                table: "ProcurementPlanItems",
                column: "IsCritical");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlanItems_ItemCategory",
                table: "ProcurementPlanItems",
                column: "ItemCategory");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgets_BudgetCode",
                table: "ProcurementBudgets",
                column: "BudgetCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgets_FiscalYear",
                table: "ProcurementBudgets",
                column: "FiscalYear");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgets_Status",
                table: "ProcurementBudgets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetRevisions_RevisionNumber",
                table: "ProcurementBudgetRevisions",
                column: "RevisionNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetRevisions_Status",
                table: "ProcurementBudgetRevisions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetAllocations_CategoryName",
                table: "ProcurementBudgetAllocations",
                column: "CategoryName");

            migrationBuilder.CreateIndex(
                name: "IX_PriceHistories_PriceDate",
                table: "PriceHistories",
                column: "PriceDate");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_ReviewDate",
                table: "PerformanceReviews",
                column: "ReviewDate");

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
                name: "IX_PerformanceBondRequests_Status",
                table: "PerformanceBondRequests",
                column: "Status");

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
                name: "IX_NotificationTopicRecipients_TenantId_TopicId",
                table: "NotificationTopicRecipients",
                columns: new[] { "TenantId", "TopicId" });

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
                name: "IX_InventoryTransfers_RequestDate",
                table: "InventoryTransfers",
                column: "RequestDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_Status",
                table: "InventoryTransfers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_TransferNumber",
                table: "InventoryTransfers",
                column: "TransferNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_IsPosted",
                table: "InventoryMovements",
                column: "IsPosted");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_Item_Warehouse_Date",
                table: "InventoryMovements",
                columns: new[] { "TenantId", "InventoryItemId", "WarehouseId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_MovementDate",
                table: "InventoryMovements",
                column: "MovementDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_MovementType",
                table: "InventoryMovements",
                column: "MovementType");

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
                name: "IX_InventoryMovements_TenantId_MovementNumber",
                table: "InventoryMovements",
                columns: new[] { "TenantId", "MovementNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLayers_FIFO_Consumption",
                table: "InventoryLayers",
                columns: new[] { "TenantId", "InventoryItemId", "WarehouseId", "IsFullyConsumed", "LayerDate" });

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
                name: "IX_InventoryLayers_TenantId_LayerNumber",
                table: "InventoryLayers",
                columns: new[] { "TenantId", "LayerNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_BaseUnitOfMeasureEntityId",
                table: "InventoryItems",
                column: "BaseUnitOfMeasureEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_DefaultWarehouseId",
                table: "InventoryItems",
                column: "DefaultWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_IsFinishedGood",
                table: "InventoryItems",
                column: "IsFinishedGood");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_IsKit",
                table: "InventoryItems",
                column: "IsKit");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ItemClassId",
                table: "InventoryItems",
                column: "ItemClassId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_PriceGroupId",
                table: "InventoryItems",
                column: "PriceGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_SubCategoryId",
                table: "InventoryItems",
                column: "SubCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_SubstituteItem1Id",
                table: "InventoryItems",
                column: "SubstituteItem1Id");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_SubstituteItem2Id",
                table: "InventoryItems",
                column: "SubstituteItem2Id");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_SubstituteItem3Id",
                table: "InventoryItems",
                column: "SubstituteItem3Id");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_LastMovementDate",
                table: "InventoryBalances",
                column: "LastMovementDate");

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
                name: "IX_FleetVehicleAssignments_TenantId_EmployeeId_IsActive",
                table: "FleetVehicleAssignments",
                columns: new[] { "TenantId", "EmployeeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetVehicleAssignments_TenantId_VehicleAssetId_IsActive",
                table: "FleetVehicleAssignments",
                columns: new[] { "TenantId", "VehicleAssetId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyres_TenantId_SerialNumber_IsDeleted",
                table: "FleetTyres",
                columns: new[] { "TenantId", "SerialNumber", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyres_TenantId_VehicleAssetId_Status_IsDeleted",
                table: "FleetTyres",
                columns: new[] { "TenantId", "VehicleAssetId", "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyreEvents_TenantId_FleetTyreId_EventAtUtc_IsDeleted",
                table: "FleetTyreEvents",
                columns: new[] { "TenantId", "FleetTyreId", "EventAtUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyreEvents_TenantId_VehicleAssetId_EventAtUtc_IsDeleted",
                table: "FleetTyreEvents",
                columns: new[] { "TenantId", "VehicleAssetId", "EventAtUtc", "IsDeleted" });

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
                name: "IX_FleetTripInspections_TenantId_FleetTripId_InspectionKind_IsDeleted",
                table: "FleetTripInspections",
                columns: new[] { "TenantId", "FleetTripId", "InspectionKind", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTripInspections_TenantId_InspectionTemplateId_IsDeleted",
                table: "FleetTripInspections",
                columns: new[] { "TenantId", "InspectionTemplateId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetIncidents_TenantId_Status_IsDeleted",
                table: "FleetIncidents",
                columns: new[] { "TenantId", "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetIncidents_TenantId_VehicleAssetId_OccurredAtUtc_IsDeleted",
                table: "FleetIncidents",
                columns: new[] { "TenantId", "VehicleAssetId", "OccurredAtUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetFuelTransactions_TenantId_FleetTripId",
                table: "FleetFuelTransactions",
                columns: new[] { "TenantId", "FleetTripId" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetFuelTransactions_TenantId_VehicleAssetId_FuelledAt",
                table: "FleetFuelTransactions",
                columns: new[] { "TenantId", "VehicleAssetId", "FuelledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetExternalRepairs_TenantId_VehicleAssetId_Status_IsDeleted",
                table: "FleetExternalRepairs",
                columns: new[] { "TenantId", "VehicleAssetId", "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetExternalRepairs_TenantId_VendorBusinessPartnerId_IsDeleted",
                table: "FleetExternalRepairs",
                columns: new[] { "TenantId", "VendorBusinessPartnerId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetDefects_TenantId_FleetTripId_IsDeleted",
                table: "FleetDefects",
                columns: new[] { "TenantId", "FleetTripId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetDefects_TenantId_VehicleAssetId_Status_IsDeleted",
                table: "FleetDefects",
                columns: new[] { "TenantId", "VehicleAssetId", "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_TenantId_CostType_IsDeleted",
                table: "FleetCostEntries",
                columns: new[] { "TenantId", "CostType", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_TenantId_VehicleAssetId_CostDateUtc_IsDeleted",
                table: "FleetCostEntries",
                columns: new[] { "TenantId", "VehicleAssetId", "CostDateUtc", "IsDeleted" });

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
                name: "IX_FleetBatteryEvents_TenantId_FleetBatteryId_EventAtUtc_IsDeleted",
                table: "FleetBatteryEvents",
                columns: new[] { "TenantId", "FleetBatteryId", "EventAtUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteryEvents_TenantId_VehicleAssetId_EventAtUtc_IsDeleted",
                table: "FleetBatteryEvents",
                columns: new[] { "TenantId", "VehicleAssetId", "EventAtUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteries_TenantId_SerialNumber_IsDeleted",
                table: "FleetBatteries",
                columns: new[] { "TenantId", "SerialNumber", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteries_TenantId_VehicleAssetId_Status_IsDeleted",
                table: "FleetBatteries",
                columns: new[] { "TenantId", "VehicleAssetId", "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_EmergencySuppliers_IsActive",
                table: "EmergencySuppliers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProcurementPlans_CriticalityLevel",
                table: "EmergencyProcurementPlans",
                column: "CriticalityLevel");

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
                name: "IX_EmergencyProcurementItems_CriticalityLevel",
                table: "EmergencyProcurementItems",
                column: "CriticalityLevel");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProcurementItems_ItemCategory",
                table: "EmergencyProcurementItems",
                column: "ItemCategory");

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
                name: "IX_EmailCampaignRecipients_TenantId_EmailCampaignId_Email",
                table: "EmailCampaignRecipients",
                columns: new[] { "TenantId", "EmailCampaignId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignRecipients_TenantId_EmailCampaignId_Status",
                table: "EmailCampaignRecipients",
                columns: new[] { "TenantId", "EmailCampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DistributedLocks_LockName",
                table: "DistributedLocks",
                column: "LockName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_Code",
                table: "Currencies",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_IsActive",
                table: "Currencies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_IsBaseCurrency",
                table: "Currencies",
                column: "IsBaseCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_TenantId_Code",
                table: "Currencies",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ContractNumber",
                table: "Contracts",
                column: "ContractNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_Status",
                table: "Contracts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContractMilestones_PlannedDate",
                table: "ContractMilestones",
                column: "PlannedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ContractMilestones_Status",
                table: "ContractMilestones",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContractDocuments_DocumentType",
                table: "ContractDocuments",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_AmendmentNumber",
                table: "ContractAmendments",
                column: "AmendmentNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_Status",
                table: "ContractAmendments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerUsers_BusinessPartnerId_UserId",
                table: "BusinessPartnerUsers",
                columns: new[] { "BusinessPartnerId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerUsers_IsActive",
                table: "BusinessPartnerUsers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerUsers_Role",
                table: "BusinessPartnerUsers",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_AwardVerificationChecklistTemplates_IsActive",
                table: "AwardVerificationChecklistTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AwardVerificationChecklistTemplates_IsDefault",
                table: "AwardVerificationChecklistTemplates",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_AwardVerificationChecklistTemplates_TenantId_Name",
                table: "AwardVerificationChecklistTemplates",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AwardVerificationChecklistItems_DisplayOrder",
                table: "AwardVerificationChecklistItems",
                column: "DisplayOrder");

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
                name: "IX_UnitOfMeasureConversions_FromUnitId_ToUnitId",
                table: "UnitOfMeasureConversions",
                columns: new[] { "FromUnitId", "ToUnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureConversions_IsActive",
                table: "UnitOfMeasureConversions",
                column: "IsActive");

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
                name: "IX_TenderBidLots_Status",
                table: "TenderBidLots",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidLots_TenderBidId_LotId",
                table: "TenderBidLots",
                columns: new[] { "TenderBidId", "LotId" },
                unique: true);

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
                name: "IX_EvaluationTemplateCriteria_DisplayOrder",
                table: "EvaluationTemplateCriteria",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationTemplateCriteria_EvaluationTemplateId_EvaluationCriterionId",
                table: "EvaluationTemplateCriteria",
                columns: new[] { "EvaluationTemplateId", "EvaluationCriterionId" },
                unique: true);

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
                name: "IX_AssetConditionItemResults_ChecklistItemId",
                table: "AssetConditionItemResults",
                column: "ChecklistItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetConditionItemResults_ConditionRecordId_ChecklistItemId",
                table: "AssetConditionItemResults",
                columns: new[] { "ConditionRecordId", "ChecklistItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetConditionItemResults_TenantId_ConditionRecordId",
                table: "AssetConditionItemResults",
                columns: new[] { "TenantId", "ConditionRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetConditionRecords_AdmissionId",
                table: "AssetConditionRecords",
                column: "AdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetConditionRecords_AssetId",
                table: "AssetConditionRecords",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetConditionRecords_DischargeId",
                table: "AssetConditionRecords",
                column: "DischargeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetConditionRecords_InspectionNumber",
                table: "AssetConditionRecords",
                column: "InspectionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetConditionRecords_TemplateId",
                table: "AssetConditionRecords",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetConditionRecords_TenantId_AssetId",
                table: "AssetConditionRecords",
                columns: new[] { "TenantId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetConditionRecords_TenantId_InspectionDate",
                table: "AssetConditionRecords",
                columns: new[] { "TenantId", "InspectionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetConditionRecords_TenantId_InspectionType",
                table: "AssetConditionRecords",
                columns: new[] { "TenantId", "InspectionType" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsignmentSettlements_StockMovementId",
                table: "ConsignmentSettlements",
                column: "StockMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsignmentSettlements_TenantId_StockMovementId",
                table: "ConsignmentSettlements",
                columns: new[] { "TenantId", "StockMovementId" },
                unique: true);

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
                name: "IX_TenderDocumentTypes_CreatedById",
                table: "TenderDocumentTypes",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderDocumentTypes_TenantId",
                table: "TenderDocumentTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderTemplates_CreatedById",
                table: "TenderTemplates",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenderTemplates_TenantId",
                table: "TenderTemplates",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartners_BusinessPartners_ParentId",
                table: "BusinessPartners",
                column: "ParentId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartnerUsers_Users_UserId",
                table: "BusinessPartnerUsers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_BusinessPartners_BusinessPartnerId",
                table: "Contracts",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_TenderAwards_TenderAwardId",
                table: "Contracts",
                column: "TenderAwardId",
                principalTable: "TenderAwards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Tenders_TenderId",
                table: "Contracts",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationCriteria_Tenants_TenantId",
                table: "EvaluationCriteria",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationCriteria_Users_CreatedById",
                table: "EvaluationCriteria",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationTemplateCriteria_EvaluationCriteria_EvaluationCriterionId",
                table: "EvaluationTemplateCriteria",
                column: "EvaluationCriterionId",
                principalTable: "EvaluationCriteria",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationTemplateCriteria_EvaluationTemplates_EvaluationTemplateId",
                table: "EvaluationTemplateCriteria",
                column: "EvaluationTemplateId",
                principalTable: "EvaluationTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationTemplateCriteria_Tenants_TenantId",
                table: "EvaluationTemplateCriteria",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationTemplates_Tenants_TenantId",
                table: "EvaluationTemplates",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationTemplates_Users_CreatedById",
                table: "EvaluationTemplates",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FleetBatteryEvents_MaintenanceAssets_VehicleAssetId",
                table: "FleetBatteryEvents",
                column: "VehicleAssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FleetComplianceItems_MaintenanceAssets_VehicleAssetId",
                table: "FleetComplianceItems",
                column: "VehicleAssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FleetCostEntries_FleetIncidents_FleetIncidentId",
                table: "FleetCostEntries",
                column: "FleetIncidentId",
                principalTable: "FleetIncidents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FleetFuelTransactions_FleetTrips_FleetTripId",
                table: "FleetFuelTransactions",
                column: "FleetTripId",
                principalTable: "FleetTrips",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FleetFuelTransactions_MaintenanceAssets_VehicleAssetId",
                table: "FleetFuelTransactions",
                column: "VehicleAssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FleetTrips_Employees_DriverEmployeeId",
                table: "FleetTrips",
                column: "DriverEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FleetTrips_MaintenanceAssets_VehicleAssetId",
                table: "FleetTrips",
                column: "VehicleAssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FleetTyreEvents_MaintenanceAssets_VehicleAssetId",
                table: "FleetTyreEvents",
                column: "VehicleAssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryBalances_InventoryItems_InventoryItemId",
                table: "InventoryBalances",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryBalances_Warehouses_WarehouseId",
                table: "InventoryBalances",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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
                name: "FK_InventoryLayers_InventoryItems_InventoryItemId",
                table: "InventoryLayers",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryLayers_Warehouses_WarehouseId",
                table: "InventoryLayers",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_InventoryItems_InventoryItemId",
                table: "InventoryMovements",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_Warehouses_WarehouseId",
                table: "InventoryMovements",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransferItems_InventoryItems_InventoryItemId",
                table: "InventoryTransferItems",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransferItems_InventoryTransfers_InventoryTransferId",
                table: "InventoryTransferItems",
                column: "InventoryTransferId",
                principalTable: "InventoryTransfers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransferItems_Tenants_TenantId",
                table: "InventoryTransferItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransferItems_WarehouseLocations_DestinationLocationId",
                table: "InventoryTransferItems",
                column: "DestinationLocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransferItems_WarehouseLocations_SourceLocationId",
                table: "InventoryTransferItems",
                column: "SourceLocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransfers_Warehouses_DestinationWarehouseId",
                table: "InventoryTransfers",
                column: "DestinationWarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransfers_Warehouses_SourceWarehouseId",
                table: "InventoryTransfers",
                column: "SourceWarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemUnitsOfMeasure_InventoryItems_InventoryItemId",
                table: "ItemUnitsOfMeasure",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemUnitsOfMeasure_Tenants_TenantId",
                table: "ItemUnitsOfMeasure",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemUnitsOfMeasure_UnitsOfMeasure_UnitOfMeasureId",
                table: "ItemUnitsOfMeasure",
                column: "UnitOfMeasureId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationTopics_EmailTemplates_EmailTemplateId",
                table: "NotificationTopics",
                column: "EmailTemplateId",
                principalTable: "EmailTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceBondRequests_BusinessPartners_BusinessPartnerId",
                table: "PerformanceBondRequests",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceBondRequests_TenderBids_TenderBidId",
                table: "PerformanceBondRequests",
                column: "TenderBidId",
                principalTable: "TenderBids",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceReviews_Users_ReviewedById",
                table: "PerformanceReviews",
                column: "ReviewedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PriceHistories_MarketAnalyses_MarketAnalysisId",
                table: "PriceHistories",
                column: "MarketAnalysisId",
                principalTable: "MarketAnalyses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementBudgets_Departments_DepartmentId",
                table: "ProcurementBudgets",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementPlanItemSuppliers_BusinessPartners_SupplierId",
                table: "ProcurementPlanItemSuppliers",
                column: "SupplierId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementPlans_Departments_DepartmentId",
                table: "ProcurementPlans",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementSchedules_Departments_DepartmentId",
                table: "ProcurementSchedules",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementSchedules_ProcurementPlans_ProcurementPlanId",
                table: "ProcurementSchedules",
                column: "ProcurementPlanId",
                principalTable: "ProcurementPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItems_ItemUnitsOfMeasure_ItemUnitOfMeasureId",
                table: "PurchaseOrderItems",
                column: "ItemUnitOfMeasureId",
                principalTable: "ItemUnitsOfMeasure",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RequestForQuotationAwardLines_BusinessPartners_BusinessPartnerId",
                table: "RequestForQuotationAwardLines",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestForQuotationAwardLines_RequestForQuotationItems_RfqItemId",
                table: "RequestForQuotationAwardLines",
                column: "RfqItemId",
                principalTable: "RequestForQuotationItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestForQuotationAwardLines_RequestForQuotationQuotes_QuoteId",
                table: "RequestForQuotationAwardLines",
                column: "QuoteId",
                principalTable: "RequestForQuotationQuotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestForQuotationInvitations_BusinessPartners_BusinessPartnerId",
                table: "RequestForQuotationInvitations",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestForQuotationQuoteItems_RequestForQuotationItems_RfqItemId",
                table: "RequestForQuotationQuoteItems",
                column: "RfqItemId",
                principalTable: "RequestForQuotationItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestForQuotationQuotes_BusinessPartners_BusinessPartnerId",
                table: "RequestForQuotationQuotes",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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
                name: "FK_TenderAssignments_Users_AssignedById",
                table: "TenderAssignments",
                column: "AssignedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwards_BusinessPartners_BusinessPartnerId",
                table: "TenderAwards",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwards_TenderBidLots_BidLotId",
                table: "TenderAwards",
                column: "BidLotId",
                principalTable: "TenderBidLots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwards_TenderBids_TenderBidId",
                table: "TenderAwards",
                column: "TenderBidId",
                principalTable: "TenderBids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwards_TenderLots_LotId",
                table: "TenderAwards",
                column: "LotId",
                principalTable: "TenderLots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwards_TenderNegotiations_NegotiationId",
                table: "TenderAwards",
                column: "NegotiationId",
                principalTable: "TenderNegotiations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwards_Tenders_TenderId",
                table: "TenderAwards",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwardVerificationBidders_BusinessPartners_BusinessPartnerId",
                table: "TenderAwardVerificationBidders",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwardVerificationBidders_TenderBids_TenderBidId",
                table: "TenderAwardVerificationBidders",
                column: "TenderBidId",
                principalTable: "TenderBids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwardVerificationItemDocuments_Users_UploadedById",
                table: "TenderAwardVerificationItemDocuments",
                column: "UploadedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwardVerificationItemResults_AwardVerificationChecklistItems_ChecklistItemId",
                table: "TenderAwardVerificationItemResults",
                column: "ChecklistItemId",
                principalTable: "AwardVerificationChecklistItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwardVerifications_AwardVerificationChecklistTemplates_TemplateId",
                table: "TenderAwardVerifications",
                column: "TemplateId",
                principalTable: "AwardVerificationChecklistTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwardVerifications_Tenders_TenderId",
                table: "TenderAwardVerifications",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidDocuments_Tenants_TenantId",
                table: "TenderBidDocuments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidDocuments_TenderBids_TenderBidId",
                table: "TenderBidDocuments",
                column: "TenderBidId",
                principalTable: "TenderBids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidDocuments_Users_UploadedById",
                table: "TenderBidDocuments",
                column: "UploadedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidItems_Tenants_TenantId",
                table: "TenderBidItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidItems_TenderBidLots_BidLotId",
                table: "TenderBidItems",
                column: "BidLotId",
                principalTable: "TenderBidLots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidItems_TenderBids_TenderBidId",
                table: "TenderBidItems",
                column: "TenderBidId",
                principalTable: "TenderBids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidItems_TenderItems_TenderItemId",
                table: "TenderBidItems",
                column: "TenderItemId",
                principalTable: "TenderItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidLots_Tenants_TenantId",
                table: "TenderBidLots",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidLots_TenderBids_TenderBidId",
                table: "TenderBidLots",
                column: "TenderBidId",
                principalTable: "TenderBids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidLots_TenderLots_LotId",
                table: "TenderBidLots",
                column: "LotId",
                principalTable: "TenderLots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBids_BusinessPartners_BusinessPartnerId",
                table: "TenderBids",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBids_Tenders_TenderId",
                table: "TenderBids",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderClarifications_BusinessPartners_BusinessPartnerId",
                table: "TenderClarifications",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderClarifications_Tenants_TenantId",
                table: "TenderClarifications",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderClarifications_Tenders_TenderId",
                table: "TenderClarifications",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderClarifications_Users_AnsweredById",
                table: "TenderClarifications",
                column: "AnsweredById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderClarifications_Users_QuestionById",
                table: "TenderClarifications",
                column: "QuestionById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderDocuments_Tenants_TenantId",
                table: "TenderDocuments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderDocuments_Tenders_TenderId",
                table: "TenderDocuments",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderDocuments_Users_UploadedById",
                table: "TenderDocuments",
                column: "UploadedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderEvaluations_Tenants_TenantId",
                table: "TenderEvaluations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderEvaluations_TenderBids_TenderBidId",
                table: "TenderEvaluations",
                column: "TenderBidId",
                principalTable: "TenderBids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderEvaluations_TenderEvaluators_TenderEvaluatorId",
                table: "TenderEvaluations",
                column: "TenderEvaluatorId",
                principalTable: "TenderEvaluators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderEvaluators_Tenants_TenantId",
                table: "TenderEvaluators",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderEvaluators_Tenders_TenderId",
                table: "TenderEvaluators",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderEvaluators_Users_AssignedById",
                table: "TenderEvaluators",
                column: "AssignedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderEvaluators_Users_UserId",
                table: "TenderEvaluators",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderInterviews_Tenants_TenantId",
                table: "TenderInterviews",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderInterviews_TenderBids_TenderBidId",
                table: "TenderInterviews",
                column: "TenderBidId",
                principalTable: "TenderBids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderInterviews_Tenders_TenderId",
                table: "TenderInterviews",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderInterviews_Users_ConductedById",
                table: "TenderInterviews",
                column: "ConductedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderInvitations_BusinessPartners_BusinessPartnerId",
                table: "TenderInvitations",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderInvitations_Tenants_TenantId",
                table: "TenderInvitations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderInvitations_Tenders_TenderId",
                table: "TenderInvitations",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderInvitations_Users_InvitedById",
                table: "TenderInvitations",
                column: "InvitedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderItems_TenderLots_LotId",
                table: "TenderItems",
                column: "LotId",
                principalTable: "TenderLots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderNegotiationItems_TenderBidItems_TenderBidItemId",
                table: "TenderNegotiationItems",
                column: "TenderBidItemId",
                principalTable: "TenderBidItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderNegotiations_TenderBidLots_BidLotId",
                table: "TenderNegotiations",
                column: "BidLotId",
                principalTable: "TenderBidLots",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderRevisions_Tenants_TenantId",
                table: "TenderRevisions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderRevisions_Tenders_TenderId",
                table: "TenderRevisions",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderRevisions_Users_RevisedById",
                table: "TenderRevisions",
                column: "RevisedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenders_EvaluationTemplates_EvaluationTemplateId",
                table: "Tenders",
                column: "EvaluationTemplateId",
                principalTable: "EvaluationTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasureConversions_Tenants_TenantId",
                table: "UnitOfMeasureConversions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasureConversions_UnitsOfMeasure_FromUnitId",
                table: "UnitOfMeasureConversions",
                column: "FromUnitId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasureConversions_UnitsOfMeasure_ToUnitId",
                table: "UnitOfMeasureConversions",
                column: "ToUnitId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasureScheduleDetails_Tenants_TenantId",
                table: "UnitOfMeasureScheduleDetails",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasureScheduleDetails_UnitOfMeasureSchedules_ScheduleId",
                table: "UnitOfMeasureScheduleDetails",
                column: "ScheduleId",
                principalTable: "UnitOfMeasureSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasureScheduleDetails_UnitsOfMeasure_UnitOfMeasureId",
                table: "UnitOfMeasureScheduleDetails",
                column: "UnitOfMeasureId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasureSchedules_Tenants_TenantId",
                table: "UnitOfMeasureSchedules",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasureSchedules_UnitsOfMeasure_BaseUnitOfMeasureId",
                table: "UnitOfMeasureSchedules",
                column: "BaseUnitOfMeasureId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitsOfMeasure_Tenants_TenantId",
                table: "UnitsOfMeasure",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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
        }
    }
}
