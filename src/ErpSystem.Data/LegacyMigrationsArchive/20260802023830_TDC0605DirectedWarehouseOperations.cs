using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class TDC0605DirectedWarehouseOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryDirectedTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TaskType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DestinationLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SuggestionKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CapacitySnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsQuarantine = table.Column<bool>(type: "bit", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkedInventoryTransferId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_InventoryDirectedTasks", x => x.Id);
                    table.CheckConstraint("CK_InventoryDirectedTasks_Hashes", "LEN([SuggestionKey]) = 64 AND LEN([PayloadHash]) = 64 AND LEN([IntegrityHash]) = 64");
                    table.CheckConstraint("CK_InventoryDirectedTasks_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_InventoryDirectedTasks_Status", "[Status] IN (1,2,3,4,5)");
                    table.CheckConstraint("CK_InventoryDirectedTasks_TaskType", "[TaskType] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_InventoryDirectedTasks_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDirectedTasks_InventoryTransfers_LinkedInventoryTransferId",
                        column: x => x.LinkedInventoryTransferId,
                        principalTable: "InventoryTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDirectedTasks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDirectedTasks_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDirectedTasks_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDirectedTasks_WarehouseLocations_DestinationLocationId",
                        column: x => x.DestinationLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDirectedTasks_WarehouseLocations_SourceLocationId",
                        column: x => x.SourceLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDirectedTasks_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryDirectedTaskActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    StatusAfter = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_InventoryDirectedTaskActions", x => x.Id);
                    table.CheckConstraint("CK_InventoryDirectedTaskActions_ActionType", "[ActionType] IN (1,2,3,4,5,6,7)");
                    table.CheckConstraint("CK_InventoryDirectedTaskActions_IntegrityHash", "LEN([IntegrityHash]) = 64");
                    table.CheckConstraint("CK_InventoryDirectedTaskActions_Sequence", "[Sequence] > 0");
                    table.CheckConstraint("CK_InventoryDirectedTaskActions_StatusAfter", "[StatusAfter] IN (1,2,3,4,5)");
                    table.ForeignKey(
                        name: "FK_InventoryDirectedTaskActions_InventoryDirectedTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "InventoryDirectedTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryDirectedTaskActions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDirectedTaskActions_TaskId",
                table: "InventoryDirectedTaskActions",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDirectedTaskActions_TenantId_OccurredAtUtc",
                table: "InventoryDirectedTaskActions",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDirectedTaskActions_TenantId_TaskId_Sequence",
                table: "InventoryDirectedTaskActions",
                columns: new[] { "TenantId", "TaskId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDirectedTasks_AssignedToUserId",
                table: "InventoryDirectedTasks",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDirectedTasks_CreatedByUserId",
                table: "InventoryDirectedTasks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDirectedTasks_DestinationLocationId",
                table: "InventoryDirectedTasks",
                column: "DestinationLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDirectedTasks_InventoryItemId",
                table: "InventoryDirectedTasks",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDirectedTasks_LinkedInventoryTransferId",
                table: "InventoryDirectedTasks",
                column: "LinkedInventoryTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDirectedTasks_SourceLocationId",
                table: "InventoryDirectedTasks",
                column: "SourceLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDirectedTasks_TenantId_IdempotencyKey",
                table: "InventoryDirectedTasks",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDirectedTasks_TenantId_SourceDocumentType_SourceDocumentId_SourceLineId",
                table: "InventoryDirectedTasks",
                columns: new[] { "TenantId", "SourceDocumentType", "SourceDocumentId", "SourceLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDirectedTasks_TenantId_SuggestionKey",
                table: "InventoryDirectedTasks",
                columns: new[] { "TenantId", "SuggestionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDirectedTasks_TenantId_TaskNumber",
                table: "InventoryDirectedTasks",
                columns: new[] { "TenantId", "TaskNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDirectedTasks_TenantId_WarehouseId_Status_TaskType",
                table: "InventoryDirectedTasks",
                columns: new[] { "TenantId", "WarehouseId", "Status", "TaskType" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDirectedTasks_WarehouseId",
                table: "InventoryDirectedTasks",
                column: "WarehouseId");

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_InventoryDirectedTasks_Integrity]
                ON [dbo].[InventoryDirectedTasks]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
                    BEGIN
                        THROW 51000, 'Directed warehouse tasks cannot be deleted; use the terminal cancellation lifecycle.', 1;
                    END;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[Warehouses] w
                          ON w.[Id] = i.[WarehouseId] AND w.[TenantId] = i.[TenantId] AND w.[IsDeleted] = 0
                        LEFT JOIN [dbo].[InventoryItems] item
                          ON item.[Id] = i.[InventoryItemId] AND item.[TenantId] = i.[TenantId] AND item.[IsDeleted] = 0
                        WHERE w.[Id] IS NULL OR item.[Id] IS NULL)
                    BEGIN
                        THROW 51000, 'Directed task warehouse and inventory item must belong to the task tenant.', 1;
                    END;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[WarehouseLocations] sourceLocation
                          ON sourceLocation.[Id] = i.[SourceLocationId]
                         AND sourceLocation.[TenantId] = i.[TenantId]
                         AND sourceLocation.[WarehouseId] = i.[WarehouseId]
                         AND sourceLocation.[IsDeleted] = 0
                        LEFT JOIN [dbo].[WarehouseLocations] destinationLocation
                          ON destinationLocation.[Id] = i.[DestinationLocationId]
                         AND destinationLocation.[TenantId] = i.[TenantId]
                         AND destinationLocation.[WarehouseId] = i.[WarehouseId]
                         AND destinationLocation.[IsDeleted] = 0
                        WHERE (i.[SourceLocationId] IS NOT NULL AND sourceLocation.[Id] IS NULL)
                           OR (i.[DestinationLocationId] IS NOT NULL AND destinationLocation.[Id] IS NULL))
                    BEGIN
                        THROW 51000, 'Directed task bins must belong to the task warehouse and tenant.', 1;
                    END;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE (i.[TaskType] = 1 AND i.[DestinationLocationId] IS NULL)
                           OR (i.[TaskType] = 2 AND (i.[SourceLocationId] IS NULL OR i.[DestinationLocationId] IS NOT NULL))
                           OR (i.[TaskType] = 3 AND (i.[SourceLocationId] IS NULL OR i.[DestinationLocationId] IS NULL OR i.[SourceLocationId] = i.[DestinationLocationId])))
                    BEGIN
                        THROW 51000, 'Directed task type and bin route are inconsistent.', 1;
                    END;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [dbo].[WarehouseLocations] destinationLocation ON destinationLocation.[Id] = i.[DestinationLocationId]
                        WHERE (i.[IsQuarantine] = 1 AND destinationLocation.[IsQuarantineLocation] = 0)
                           OR (i.[IsQuarantine] = 0 AND i.[TaskType] IN (1,3)
                               AND (destinationLocation.[IsQuarantineLocation] = 1
                                    OR destinationLocation.[IsDamageLocation] = 1
                                    OR destinationLocation.[IsInTransitLocation] = 1)))
                    BEGIN
                        THROW 51000, 'Directed task quarantine disposition does not match the destination bin.', 1;
                    END;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[InventoryTransfers] transferRecord
                          ON transferRecord.[Id] = i.[LinkedInventoryTransferId]
                         AND transferRecord.[TenantId] = i.[TenantId]
                         AND transferRecord.[SourceWarehouseId] = i.[WarehouseId]
                         AND transferRecord.[DestinationWarehouseId] = i.[WarehouseId]
                         AND transferRecord.[IsDeleted] = 0
                        WHERE (i.[Status] = 3 AND (i.[TaskType] <> 3 OR i.[LinkedInventoryTransferId] IS NULL))
                           OR (i.[LinkedInventoryTransferId] IS NOT NULL AND (i.[TaskType] <> 3 OR transferRecord.[Id] IS NULL)))
                    BEGIN
                        THROW 51000, 'Awaiting directed work must reference a same-warehouse replenishment transfer in the same tenant.', 1;
                    END;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE (i.[Status] = 2 AND (i.[StartedAtUtc] IS NULL OR i.[StartedByUserId] IS NULL))
                           OR (i.[Status] = 4 AND (i.[CompletedAtUtc] IS NULL OR i.[CompletedByUserId] IS NULL))
                           OR (i.[Status] = 5 AND (i.[CancelledAtUtc] IS NULL OR NULLIF(LTRIM(RTRIM(i.[CancellationReason])), '') IS NULL)))
                    BEGIN
                        THROW 51000, 'Directed task status requires its matching actor, timestamp, and terminal reason.', 1;
                    END;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE NOT EXISTS (
                            SELECT 1 FROM [dbo].[UserTenants] ut
                            JOIN [dbo].[Users] u ON u.[Id] = ut.[UserId] AND u.[IsActive] = 1
                            WHERE ut.[TenantId] = i.[TenantId] AND ut.[UserId] = i.[AssignedToUserId]
                              AND ut.[Status] = 0 AND ut.[IsDeleted] = 0
                              AND (ut.[ExpiresAt] IS NULL OR ut.[ExpiresAt] > SYSUTCDATETIME()))
                           OR EXISTS (
                            SELECT 1 FROM [dbo].[UserRoles] ur
                            JOIN [dbo].[AspNetRoles] roleRecord ON roleRecord.[Id] = ur.[RoleId]
                            WHERE ur.[UserId] = i.[AssignedToUserId] AND roleRecord.[Name] = 'ExternalUser')
                           OR NOT EXISTS (
                            SELECT 1 FROM [dbo].[UserTenants] ut
                            JOIN [dbo].[Users] u ON u.[Id] = ut.[UserId] AND u.[IsActive] = 1
                            WHERE ut.[TenantId] = i.[TenantId] AND ut.[UserId] = i.[CreatedByUserId]
                              AND ut.[Status] = 0 AND ut.[IsDeleted] = 0
                              AND (ut.[ExpiresAt] IS NULL OR ut.[ExpiresAt] > SYSUTCDATETIME()))
                           OR EXISTS (
                            SELECT 1 FROM [dbo].[UserRoles] ur
                            JOIN [dbo].[AspNetRoles] roleRecord ON roleRecord.[Id] = ur.[RoleId]
                            WHERE ur.[UserId] = i.[CreatedByUserId] AND roleRecord.[Name] = 'ExternalUser'))
                    BEGIN
                        THROW 51000, 'Directed task creator and assignee must be active users in the task tenant.', 1;
                    END;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        CROSS APPLY (VALUES (i.[StartedByUserId]), (i.[CompletedByUserId])) actor([UserId])
                        WHERE actor.[UserId] IS NOT NULL AND (
                            NOT EXISTS (
                                SELECT 1 FROM [dbo].[UserTenants] ut
                                JOIN [dbo].[Users] u ON u.[Id] = ut.[UserId] AND u.[IsActive] = 1
                                WHERE ut.[TenantId] = i.[TenantId] AND ut.[UserId] = actor.[UserId]
                                  AND ut.[Status] = 0 AND ut.[IsDeleted] = 0
                                  AND (ut.[ExpiresAt] IS NULL OR ut.[ExpiresAt] > SYSUTCDATETIME()))
                            OR EXISTS (
                                SELECT 1 FROM [dbo].[UserRoles] ur
                                JOIN [dbo].[AspNetRoles] roleRecord ON roleRecord.[Id] = ur.[RoleId]
                                WHERE ur.[UserId] = actor.[UserId] AND roleRecord.[Name] = 'ExternalUser')))
                    BEGIN
                        THROW 51000, 'Directed task lifecycle actors must be active internal users in the task tenant.', 1;
                    END;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE i.[TaskNumber] <> d.[TaskNumber]
                           OR i.[TaskType] <> d.[TaskType]
                           OR i.[WarehouseId] <> d.[WarehouseId]
                           OR i.[InventoryItemId] <> d.[InventoryItemId]
                           OR ISNULL(i.[SourceLocationId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[SourceLocationId], '00000000-0000-0000-0000-000000000000')
                           OR ISNULL(i.[DestinationLocationId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[DestinationLocationId], '00000000-0000-0000-0000-000000000000')
                           OR i.[Quantity] <> d.[Quantity]
                           OR i.[SourceDocumentType] <> d.[SourceDocumentType]
                           OR i.[SourceDocumentId] <> d.[SourceDocumentId]
                           OR i.[SourceLineId] <> d.[SourceLineId]
                           OR i.[SourceReference] <> d.[SourceReference]
                           OR i.[SuggestionKey] <> d.[SuggestionKey]
                           OR i.[IdempotencyKey] <> d.[IdempotencyKey]
                           OR i.[PayloadHash] <> d.[PayloadHash]
                           OR i.[IsQuarantine] <> d.[IsQuarantine]
                           OR i.[AssignedToUserId] <> d.[AssignedToUserId]
                           OR i.[CreatedByUserId] <> d.[CreatedByUserId]
                           OR i.[AssignedAtUtc] <> d.[AssignedAtUtc]
                           OR i.[TenantId] <> d.[TenantId]
                           OR i.[IsDeleted] <> d.[IsDeleted]
                           OR (d.[StartedByUserId] IS NOT NULL AND
                               (i.[StartedByUserId] IS NULL OR i.[StartedByUserId] <> d.[StartedByUserId]
                                OR i.[StartedAtUtc] <> d.[StartedAtUtc]))
                           OR (d.[CompletedByUserId] IS NOT NULL AND
                               (i.[CompletedByUserId] IS NULL OR i.[CompletedByUserId] <> d.[CompletedByUserId]
                                OR i.[CompletedAtUtc] <> d.[CompletedAtUtc]))
                           OR (d.[CancelledAtUtc] IS NOT NULL AND
                               (i.[CancelledAtUtc] IS NULL OR i.[CancelledAtUtc] <> d.[CancelledAtUtc]
                                OR i.[CancellationReason] <> d.[CancellationReason])))
                    BEGIN
                        THROW 51000, 'Directed task source, assignment, replay, and tenant fields are immutable.', 1;
                    END;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE i.[Status] <> d.[Status]
                          AND NOT (
                               (d.[Status] = 1 AND i.[Status] IN (2,4,5))
                            OR (d.[Status] = 2 AND i.[Status] IN (3,4,5))
                            OR (d.[Status] = 3 AND i.[Status] IN (4,5))))
                    BEGIN
                        THROW 51000, 'The directed task status transition is not allowed.', 1;
                    END;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_InventoryDirectedTaskActions_AppendOnly]
                ON [dbo].[InventoryDirectedTaskActions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                    BEGIN
                        THROW 51000, 'Directed task actions are append-only and cannot be changed or deleted.', 1;
                    END;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[InventoryDirectedTasks] taskRecord
                          ON taskRecord.[Id] = i.[TaskId] AND taskRecord.[TenantId] = i.[TenantId]
                        WHERE taskRecord.[Id] IS NULL OR taskRecord.[Status] <> i.[StatusAfter])
                    BEGIN
                        THROW 51000, 'Directed task action tenant or resulting status does not match its task.', 1;
                    END;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE (i.[ActionType] = 1 AND i.[StatusAfter] <> 1)
                           OR (i.[ActionType] = 2 AND i.[StatusAfter] <> 2)
                           OR (i.[ActionType] IN (3,4,6) AND i.[StatusAfter] <> 4)
                           OR (i.[ActionType] = 5 AND i.[StatusAfter] <> 3)
                           OR (i.[ActionType] = 7 AND i.[StatusAfter] <> 5))
                    BEGIN
                        THROW 51000, 'Directed task action type and resulting status are inconsistent.', 1;
                    END;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.[Sequence] <> 1 + ISNULL((
                            SELECT MAX(previousAction.[Sequence])
                            FROM [dbo].[InventoryDirectedTaskActions] previousAction
                            WHERE previousAction.[TaskId] = i.[TaskId]
                              AND previousAction.[TenantId] = i.[TenantId]
                              AND previousAction.[Id] <> i.[Id]
                              AND previousAction.[Sequence] < i.[Sequence]), 0))
                    BEGIN
                        THROW 51000, 'Directed task action sequence must be contiguous.', 1;
                    END;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE NOT EXISTS (
                            SELECT 1 FROM [dbo].[UserTenants] ut
                            JOIN [dbo].[Users] u ON u.[Id] = ut.[UserId] AND u.[IsActive] = 1
                            WHERE ut.[TenantId] = i.[TenantId] AND ut.[UserId] = i.[ActorUserId]
                              AND ut.[Status] = 0 AND ut.[IsDeleted] = 0
                              AND (ut.[ExpiresAt] IS NULL OR ut.[ExpiresAt] > i.[OccurredAtUtc])))
                    BEGIN
                        THROW 51000, 'Directed task action actor must be an active user in the action tenant.', 1;
                    END;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryDirectedTaskActions_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryDirectedTasks_Integrity];");

            migrationBuilder.DropTable(
                name: "InventoryDirectedTaskActions");

            migrationBuilder.DropTable(
                name: "InventoryDirectedTasks");
        }
    }
}
