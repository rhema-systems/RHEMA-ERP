using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class MaintenancePropertyFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderItems_InventoryItems_InventoryItemId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderReceiptItems_WarehouseLocations_LocationId",
                table: "PurchaseOrderReceiptItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Warehouses_DeliveryWarehouseId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_DeliveryWarehouseId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_AssetTypes_Code",
                table: "AssetTypes");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("1f33cb93-dce0-453c-b7ed-9efd20a90d05"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("4f37828e-cf46-4daf-8e6d-7235bb99f404"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("4fdd066f-98ff-42ea-9a53-ba29abd712b8"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("5502c101-06bd-4cbd-88db-3290d6371a34"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("6bcfab55-7e1a-4848-bf86-de7cc2754a03"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("a6769cc2-0256-4a35-93f4-8452fdb87cb6"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("e24d1390-2e88-4e91-a7d4-03ef7e877164"));

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SupplierAddress",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SupplierName",
                table: "PurchaseOrders");

            migrationBuilder.RenameColumn(
                name: "ContactPerson",
                table: "PurchaseOrders",
                newName: "SupplierOrderNumber");

            migrationBuilder.RenameColumn(
                name: "ContactEmail",
                table: "PurchaseOrders",
                newName: "ReferenceNumber");

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "TechnicianSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "TechnicianAvailabilities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryInstructions",
                table: "PurchaseOrders",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                table: "PurchaseOrders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Terms",
                table: "PurchaseOrders",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InspectionDate",
                table: "PurchaseOrderReceipts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InspectionNotes",
                table: "PurchaseOrderReceipts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InspectionResult",
                table: "PurchaseOrderReceipts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresInspection",
                table: "PurchaseOrderReceipts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "QualityNotes",
                table: "PurchaseOrderReceiptItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualityStatus",
                table: "PurchaseOrderReceiptItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemDescription",
                table: "PurchaseOrderItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaintenanceAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AttachmentType = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsMainImage = table.Column<bool>(type: "bit", nullable: false),
                    ImageWidth = table.Column<int>(type: "int", nullable: true),
                    ImageHeight = table.Column<int>(type: "int", nullable: true),
                    ThumbnailPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    LocationDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DocumentVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceAttachments_Employees_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequisitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequisitionNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequisitionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CostCenter = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Justification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequisitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitions_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitions_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SupplierType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ZipCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PrimaryContactName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PrimaryContactTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PrimaryContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PrimaryContactEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TaxId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ShippingTerms = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsPreferred = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LastOrderDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContractStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContractEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Suppliers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceAttachmentTag",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TagValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceAttachmentTag", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceAttachmentTag_MaintenanceAttachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "MaintenanceAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequisitionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ItemDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EstimatedUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PreferredSupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Specifications = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequisitionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitionItems_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitionItems_PurchaseRequisitions_RequisitionId",
                        column: x => x.RequisitionId,
                        principalTable: "PurchaseRequisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitionItems_Suppliers_PreferredSupplierId",
                        column: x => x.PreferredSupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitionItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    ContactType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierContacts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierContacts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierItemCatalogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SupplierItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MinimumOrderQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: false),
                    IsPreferred = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierItemCatalogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierItemCatalogs_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierItemCatalogs_Tenants_TenantId",
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
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(5870));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(5919));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(5921));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(5923));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6163));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6191));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6198));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6206));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6214));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6220));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6225));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6237));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6245));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6253));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6259));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6265));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6273));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6286));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6298));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6303));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6347));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6355));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6356));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6357));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6358));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6359));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6360));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6361));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6362));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6363));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6363));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6364));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6365));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6365));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6366));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6367));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6405));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6407));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6408));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6409));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6409));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6410));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6410));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6411));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6412));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6412));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6413));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6414));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6414));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6415));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6415));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6458));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6459));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6461));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6461));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6468));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6469));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6469));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6470));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6471));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6471));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6488));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6489));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6490));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("207cdcc2-4870-43bb-99bf-6784e1f91224"), null, new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(5994), null, null, null, null, null, new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(5991), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("5ffa0e52-148a-48a7-9fd0-a8cf69d859a9"), null, new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6026), null, null, null, null, null, new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6025), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("754862ee-42c7-41a9-84d8-753b8fbe188a"), null, new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6064), null, null, null, null, null, new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6063), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("b02de857-b9fa-4438-8d5d-d20e2e5ec2e3"), null, new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6051), null, null, null, null, null, new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6051), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("b940483b-800c-4d4e-86f0-093055414b7a"), null, new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6039), null, null, null, null, null, new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6039), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c6094034-7737-4ec7-9a28-55f53ed7c186"), null, new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6076), null, null, null, null, null, new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6076), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("e12d49c9-9fc9-475c-9057-8de080a832db"), null, new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6009), null, null, null, null, null, new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(6009), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 5, 56, 24, 869, DateTimeKind.Utc).AddTicks(5627));

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianShifts_IsActive",
                table: "TechnicianShifts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianShifts_StartTime",
                table: "TechnicianShifts",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSchedules_EmployeeId",
                table: "TechnicianSchedules",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSchedules_ScheduleType",
                table: "TechnicianSchedules",
                column: "ScheduleType");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSchedules_StartDate",
                table: "TechnicianSchedules",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSchedules_Status",
                table: "TechnicianSchedules",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianAvailabilities_AvailabilityType",
                table: "TechnicianAvailabilities",
                column: "AvailabilityType");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianAvailabilities_EmployeeId",
                table: "TechnicianAvailabilities",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianAvailabilities_Reason",
                table: "TechnicianAvailabilities",
                column: "Reason");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianAvailabilities_StartDate",
                table: "TechnicianAvailabilities",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypes_Code",
                table: "AssetTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypeFields_AssetTypeId",
                table: "AssetTypeFields",
                column: "AssetTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypeFields_FieldName",
                table: "AssetTypeFields",
                column: "FieldName");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAttachments_UploadedByUserId",
                table: "MaintenanceAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAttachmentTag_AttachmentId",
                table: "MaintenanceAttachmentTag",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitionItems_PreferredSupplierId",
                table: "PurchaseRequisitionItems",
                column: "PreferredSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitionItems_PurchaseOrderId",
                table: "PurchaseRequisitionItems",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitionItems_RequisitionId",
                table: "PurchaseRequisitionItems",
                column: "RequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitionItems_TenantId",
                table: "PurchaseRequisitionItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_ApprovedById",
                table: "PurchaseRequisitions",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_RequestedById",
                table: "PurchaseRequisitions",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_TenantId",
                table: "PurchaseRequisitions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContacts_SupplierId",
                table: "SupplierContacts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContacts_TenantId",
                table: "SupplierContacts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierItemCatalogs_SupplierId",
                table: "SupplierItemCatalogs",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierItemCatalogs_TenantId",
                table: "SupplierItemCatalogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId",
                table: "Suppliers",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicianAvailabilities_Employees_EmployeeId",
                table: "TechnicianAvailabilities",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicianSchedules_Employees_EmployeeId",
                table: "TechnicianSchedules",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_TechnicianAvailabilities_Employees_EmployeeId",
                table: "TechnicianAvailabilities");

            migrationBuilder.DropForeignKey(
                name: "FK_TechnicianSchedules_Employees_EmployeeId",
                table: "TechnicianSchedules");

            migrationBuilder.DropTable(
                name: "MaintenanceAttachmentTag");

            migrationBuilder.DropTable(
                name: "PurchaseRequisitionItems");

            migrationBuilder.DropTable(
                name: "SupplierContacts");

            migrationBuilder.DropTable(
                name: "SupplierItemCatalogs");

            migrationBuilder.DropTable(
                name: "MaintenanceAttachments");

            migrationBuilder.DropTable(
                name: "PurchaseRequisitions");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianShifts_IsActive",
                table: "TechnicianShifts");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianShifts_StartTime",
                table: "TechnicianShifts");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianSchedules_EmployeeId",
                table: "TechnicianSchedules");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianSchedules_ScheduleType",
                table: "TechnicianSchedules");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianSchedules_StartDate",
                table: "TechnicianSchedules");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianSchedules_Status",
                table: "TechnicianSchedules");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianAvailabilities_AvailabilityType",
                table: "TechnicianAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianAvailabilities_EmployeeId",
                table: "TechnicianAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianAvailabilities_Reason",
                table: "TechnicianAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianAvailabilities_StartDate",
                table: "TechnicianAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_SupplierId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_AssetTypes_Code",
                table: "AssetTypes");

            migrationBuilder.DropIndex(
                name: "IX_AssetTypeFields_AssetTypeId",
                table: "AssetTypeFields");

            migrationBuilder.DropIndex(
                name: "IX_AssetTypeFields_FieldName",
                table: "AssetTypeFields");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("207cdcc2-4870-43bb-99bf-6784e1f91224"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("5ffa0e52-148a-48a7-9fd0-a8cf69d859a9"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("754862ee-42c7-41a9-84d8-753b8fbe188a"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("b02de857-b9fa-4438-8d5d-d20e2e5ec2e3"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("b940483b-800c-4d4e-86f0-093055414b7a"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c6094034-7737-4ec7-9a28-55f53ed7c186"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("e12d49c9-9fc9-475c-9057-8de080a832db"));

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "TechnicianSchedules");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "TechnicianAvailabilities");

            migrationBuilder.DropColumn(
                name: "DeliveryInstructions",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "Terms",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "InspectionDate",
                table: "PurchaseOrderReceipts");

            migrationBuilder.DropColumn(
                name: "InspectionNotes",
                table: "PurchaseOrderReceipts");

            migrationBuilder.DropColumn(
                name: "InspectionResult",
                table: "PurchaseOrderReceipts");

            migrationBuilder.DropColumn(
                name: "RequiresInspection",
                table: "PurchaseOrderReceipts");

            migrationBuilder.DropColumn(
                name: "QualityNotes",
                table: "PurchaseOrderReceiptItems");

            migrationBuilder.DropColumn(
                name: "QualityStatus",
                table: "PurchaseOrderReceiptItems");

            migrationBuilder.DropColumn(
                name: "ItemDescription",
                table: "PurchaseOrderItems");

            migrationBuilder.RenameColumn(
                name: "SupplierOrderNumber",
                table: "PurchaseOrders",
                newName: "ContactPerson");

            migrationBuilder.RenameColumn(
                name: "ReferenceNumber",
                table: "PurchaseOrders",
                newName: "ContactEmail");

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "PurchaseOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierAddress",
                table: "PurchaseOrders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierName",
                table: "PurchaseOrders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7229));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7267));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7269));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7271));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7498));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7513));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7520));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7528));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7538));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7545));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7551));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7557));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7566));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7575));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7581));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7588));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7597));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7608));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7620));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7626));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7671));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7673));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7674));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7675));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7676));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7677));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7678));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7679));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7680));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7681));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7682));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7682));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7683));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7683));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7684));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7685));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7717));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7719));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7719));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7720));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7721));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7721));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7722));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7722));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7723));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7724));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7724));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7725));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7726));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7726));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7727));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7767));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7768));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7770));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7771));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7771));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7772));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7772));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7773));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7774));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7774));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7787));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7788));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7789));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("1f33cb93-dce0-453c-b7ed-9efd20a90d05"), null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7332), null, null, null, null, null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7330), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("4f37828e-cf46-4daf-8e6d-7235bb99f404"), null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7400), null, null, null, null, null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7400), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("4fdd066f-98ff-42ea-9a53-ba29abd712b8"), null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7371), null, null, null, null, null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7371), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("5502c101-06bd-4cbd-88db-3290d6371a34"), null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7386), null, null, null, null, null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7386), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("6bcfab55-7e1a-4848-bf86-de7cc2754a03"), null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7416), null, null, null, null, null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7416), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("a6769cc2-0256-4a35-93f4-8452fdb87cb6"), null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7349), null, null, null, null, null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7349), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("e24d1390-2e88-4e91-a7d4-03ef7e877164"), null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7431), null, null, null, null, null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7430), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7040));

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_DeliveryWarehouseId",
                table: "PurchaseOrders",
                column: "DeliveryWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypes_Code",
                table: "AssetTypes",
                column: "Code");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItems_InventoryItems_InventoryItemId",
                table: "PurchaseOrderItems",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderReceiptItems_WarehouseLocations_LocationId",
                table: "PurchaseOrderReceiptItems",
                column: "LocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Warehouses_DeliveryWarehouseId",
                table: "PurchaseOrders",
                column: "DeliveryWarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");
        }
    }
}
