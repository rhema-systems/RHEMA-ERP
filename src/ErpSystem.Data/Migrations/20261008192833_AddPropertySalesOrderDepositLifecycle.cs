using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertySalesOrderDepositLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Quotes_TenantId",
                table: "Quotes");

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyEnquiryTicketId",
                table: "Quotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentificationNumber",
                table: "EhcTickets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IdentificationTypeId",
                table: "EhcTickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IdentificationTypeModules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdentificationTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_IdentificationTypeModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdentificationTypeModules_IdentificationTypes_IdentificationTypeId",
                        column: x => x.IdentificationTypeId,
                        principalTable: "IdentificationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IdentificationTypeModules_TenantModules_TenantModuleId",
                        column: x => x.TenantModuleId,
                        principalTable: "TenantModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IdentificationTypeModules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrderCustomerDeposits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TenderType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExternalBankName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ExternalAccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ChequeNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DepositReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IdentificationReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PropertyDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_SalesOrderCustomerDeposits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesOrderCustomerDeposits_CustomerPayment_CustomerPaymentId",
                        column: x => x.CustomerPaymentId,
                        principalTable: "CustomerPayment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrderCustomerDeposits_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrderCustomerDeposits_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM [TenantModules]
                    WHERE [TenantId] = '00000000-0000-0000-0000-000000000001'
                      AND [ModuleName] = N'Estate'
                )
                BEGIN
                    INSERT INTO [TenantModules]
                    (
                        [Id], [TenantId], [ModuleName], [Status], [EnabledDate], [CreatedAt], [IsDeleted]
                    )
                    VALUES
                    (
                        '00000000-0000-0000-0000-000000010008',
                        '00000000-0000-0000-0000-000000000001',
                        N'Estate',
                        1,
                        '2025-01-01T00:00:00.0000000Z',
                        '2025-01-01T00:00:00.0000000Z',
                        0
                    );
                END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_PropertyEnquiryTicketId",
                table: "Quotes",
                column: "PropertyEnquiryTicketId");

            migrationBuilder.CreateIndex(
                name: "UX_Quote_Tenant_PropertyEnquiryTicket",
                table: "Quotes",
                columns: new[] { "TenantId", "PropertyEnquiryTicketId" },
                unique: true,
                filter: "[PropertyEnquiryTicketId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EhcTickets_IdentificationTypeId",
                table: "EhcTickets",
                column: "IdentificationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcTickets_TenantId_IdentificationTypeId",
                table: "EhcTickets",
                columns: new[] { "TenantId", "IdentificationTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_IdentificationTypeModule_Tenant_Module",
                table: "IdentificationTypeModules",
                columns: new[] { "TenantId", "TenantModuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_IdentificationTypeModules_IdentificationTypeId",
                table: "IdentificationTypeModules",
                column: "IdentificationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentificationTypeModules_TenantModuleId",
                table: "IdentificationTypeModules",
                column: "TenantModuleId");

            migrationBuilder.CreateIndex(
                name: "UX_IdentificationTypeModule_Tenant_Type_Module",
                table: "IdentificationTypeModules",
                columns: new[] { "TenantId", "IdentificationTypeId", "TenantModuleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderCustomerDeposits_CustomerPaymentId",
                table: "SalesOrderCustomerDeposits",
                column: "CustomerPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderCustomerDeposits_SalesOrderId",
                table: "SalesOrderCustomerDeposits",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderCustomerDeposits_TenantId_CustomerPaymentId",
                table: "SalesOrderCustomerDeposits",
                columns: new[] { "TenantId", "CustomerPaymentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderCustomerDeposits_TenantId_IdempotencyKey",
                table: "SalesOrderCustomerDeposits",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderCustomerDeposits_TenantId_SalesOrderId",
                table: "SalesOrderCustomerDeposits",
                columns: new[] { "TenantId", "SalesOrderId" });

            migrationBuilder.AddForeignKey(
                name: "FK_EhcTickets_IdentificationTypes_IdentificationTypeId",
                table: "EhcTickets",
                column: "IdentificationTypeId",
                principalTable: "IdentificationTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_EhcTickets_PropertyEnquiryTicketId",
                table: "Quotes",
                column: "PropertyEnquiryTicketId",
                principalTable: "EhcTickets",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EhcTickets_IdentificationTypes_IdentificationTypeId",
                table: "EhcTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_EhcTickets_PropertyEnquiryTicketId",
                table: "Quotes");

            migrationBuilder.DropTable(
                name: "IdentificationTypeModules");

            migrationBuilder.DropTable(
                name: "SalesOrderCustomerDeposits");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_PropertyEnquiryTicketId",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "UX_Quote_Tenant_PropertyEnquiryTicket",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_EhcTickets_IdentificationTypeId",
                table: "EhcTickets");

            migrationBuilder.DropIndex(
                name: "IX_EhcTickets_TenantId_IdentificationTypeId",
                table: "EhcTickets");

            migrationBuilder.DropColumn(
                name: "PropertyEnquiryTicketId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "IdentificationNumber",
                table: "EhcTickets");

            migrationBuilder.DropColumn(
                name: "IdentificationTypeId",
                table: "EhcTickets");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_TenantId",
                table: "Quotes",
                column: "TenantId");
        }
    }
}
