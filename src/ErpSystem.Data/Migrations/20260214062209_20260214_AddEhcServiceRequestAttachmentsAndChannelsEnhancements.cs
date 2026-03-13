using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class _20260214_AddEhcServiceRequestAttachmentsAndChannelsEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveFailureCount",
                table: "EhcInboundEmailChannels",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GraphClientState",
                table: "EhcInboundEmailChannels",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GraphSubscriptionExpiresAtUtc",
                table: "EhcInboundEmailChannels",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GraphSubscriptionId",
                table: "EhcInboundEmailChannels",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAtUtc",
                table: "EhcInboundEmailChannels",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastProcessedMessageCount",
                table: "EhcInboundEmailChannels",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSuccessAtUtc",
                table: "EhcInboundEmailChannels",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastWebhookReceivedAtUtc",
                table: "EhcInboundEmailChannels",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseGraphWebhook",
                table: "EhcInboundEmailChannels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "EhcInboundEmailWebhookQueueItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GraphMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_EhcInboundEmailWebhookQueueItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcInboundEmailWebhookQueueItems_EhcInboundEmailChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "EhcInboundEmailChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EhcInboundEmailWebhookQueueItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EhcInboundMessagingChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    ToAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequireKnownSender = table.Column<bool>(type: "bit", nullable: false),
                    AutoProvisionUnknownSenders = table.Column<bool>(type: "bit", nullable: false),
                    DefaultCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultTicketType = table.Column<int>(type: "int", nullable: false),
                    DefaultPriority = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_EhcInboundMessagingChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcInboundMessagingChannels_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EhcServiceRequestAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_EhcServiceRequestAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcServiceRequestAttachments_EhcServiceRequests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "EhcServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EhcServiceRequestAttachments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EhcServiceRequestAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_EhcServiceRequestAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcServiceRequestAuditEvents_EhcServiceRequests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "EhcServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EhcServiceRequestAuditEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcServiceRequestAuditEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EhcInboundMessagingMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FromAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ToAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_EhcInboundMessagingMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcInboundMessagingMessages_EhcInboundMessagingChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "EhcInboundMessagingChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EhcInboundMessagingMessages_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EhcInboundEmailWebhookQueueItems_ChannelId",
                table: "EhcInboundEmailWebhookQueueItems",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcInboundEmailWebhookQueueItems_TenantId",
                table: "EhcInboundEmailWebhookQueueItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcInboundMessagingChannels_TenantId",
                table: "EhcInboundMessagingChannels",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcInboundMessagingMessages_ChannelId",
                table: "EhcInboundMessagingMessages",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcInboundMessagingMessages_TenantId",
                table: "EhcInboundMessagingMessages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcServiceRequestAttachments_ServiceRequestId",
                table: "EhcServiceRequestAttachments",
                column: "ServiceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcServiceRequestAttachments_TenantId",
                table: "EhcServiceRequestAttachments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcServiceRequestAuditEvents_ActorUserId",
                table: "EhcServiceRequestAuditEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcServiceRequestAuditEvents_ServiceRequestId",
                table: "EhcServiceRequestAuditEvents",
                column: "ServiceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcServiceRequestAuditEvents_TenantId",
                table: "EhcServiceRequestAuditEvents",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EhcInboundEmailWebhookQueueItems");

            migrationBuilder.DropTable(
                name: "EhcInboundMessagingMessages");

            migrationBuilder.DropTable(
                name: "EhcServiceRequestAttachments");

            migrationBuilder.DropTable(
                name: "EhcServiceRequestAuditEvents");

            migrationBuilder.DropTable(
                name: "EhcInboundMessagingChannels");

            migrationBuilder.DropColumn(
                name: "ConsecutiveFailureCount",
                table: "EhcInboundEmailChannels");

            migrationBuilder.DropColumn(
                name: "GraphClientState",
                table: "EhcInboundEmailChannels");

            migrationBuilder.DropColumn(
                name: "GraphSubscriptionExpiresAtUtc",
                table: "EhcInboundEmailChannels");

            migrationBuilder.DropColumn(
                name: "GraphSubscriptionId",
                table: "EhcInboundEmailChannels");

            migrationBuilder.DropColumn(
                name: "LastAttemptAtUtc",
                table: "EhcInboundEmailChannels");

            migrationBuilder.DropColumn(
                name: "LastProcessedMessageCount",
                table: "EhcInboundEmailChannels");

            migrationBuilder.DropColumn(
                name: "LastSuccessAtUtc",
                table: "EhcInboundEmailChannels");

            migrationBuilder.DropColumn(
                name: "LastWebhookReceivedAtUtc",
                table: "EhcInboundEmailChannels");

            migrationBuilder.DropColumn(
                name: "UseGraphWebhook",
                table: "EhcInboundEmailChannels");
        }
    }
}
