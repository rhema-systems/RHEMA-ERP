using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEhcServiceCatalogAndInboundEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EhcInboundEmailChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    FolderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RequireKnownSender = table.Column<bool>(type: "bit", nullable: false),
                    AutoProvisionUnknownSenders = table.Column<bool>(type: "bit", nullable: false),
                    DefaultCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultTicketType = table.Column<int>(type: "int", nullable: false),
                    DefaultPriority = table.Column<int>(type: "int", nullable: false),
                    GraphDeltaLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_EhcInboundEmailChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcInboundEmailChannels_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EhcServiceRequestTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    WorkflowName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FormDefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_EhcServiceRequestTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcServiceRequestTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EhcInboundEmailMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GraphMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InternetMessageId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ConversationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_EhcInboundEmailMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcInboundEmailMessages_EhcInboundEmailChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "EhcInboundEmailChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EhcInboundEmailMessages_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EhcServiceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequesterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FormDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_EhcServiceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcServiceRequests_EhcServiceRequestTypes_RequestTypeId",
                        column: x => x.RequestTypeId,
                        principalTable: "EhcServiceRequestTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EhcServiceRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcServiceRequests_Users_RequesterUserId",
                        column: x => x.RequesterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EhcInboundEmailChannels_TenantId",
                table: "EhcInboundEmailChannels",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcInboundEmailMessages_ChannelId",
                table: "EhcInboundEmailMessages",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcInboundEmailMessages_TenantId",
                table: "EhcInboundEmailMessages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcServiceRequests_RequesterUserId",
                table: "EhcServiceRequests",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcServiceRequests_RequestTypeId",
                table: "EhcServiceRequests",
                column: "RequestTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcServiceRequests_TenantId",
                table: "EhcServiceRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcServiceRequestTypes_TenantId",
                table: "EhcServiceRequestTypes",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EhcInboundEmailMessages");

            migrationBuilder.DropTable(
                name: "EhcServiceRequests");

            migrationBuilder.DropTable(
                name: "EhcInboundEmailChannels");

            migrationBuilder.DropTable(
                name: "EhcServiceRequestTypes");
        }
    }
}
