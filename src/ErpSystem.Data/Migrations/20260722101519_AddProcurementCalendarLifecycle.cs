using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementCalendarLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementCalendarProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GenerationHorizonDays = table.Column<int>(type: "int", nullable: false),
                    CatchUpDays = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangeSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApprovalReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SupersedesProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RetiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetiredByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_ProcurementCalendarProfiles", x => x.Id);
                    table.CheckConstraint("CK_ProcurementCalendarProfiles_CatchUp", "[CatchUpDays] BETWEEN 0 AND 365");
                    table.CheckConstraint("CK_ProcurementCalendarProfiles_EffectivePeriod", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] >= [EffectiveFromUtc]");
                    table.CheckConstraint("CK_ProcurementCalendarProfiles_Horizon", "[GenerationHorizonDays] BETWEEN 1 AND 730");
                    table.CheckConstraint("CK_ProcurementCalendarProfiles_Status", "[Status] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_ProcurementCalendarProfiles_Version", "[Version] >= 1");
                    table.ForeignKey(
                        name: "FK_ProcurementCalendarProfiles_ProcurementCalendarProfiles_SupersedesProfileId",
                        column: x => x.SupersedesProfileId,
                        principalTable: "ProcurementCalendarProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementCalendarProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementCalendarRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunKey = table.Column<string>(type: "varchar(160)", unicode: false, maxLength: 160, nullable: false),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    EvaluationAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WindowStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WindowEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ProfilesEvaluated = table.Column<int>(type: "int", nullable: false),
                    RulesEvaluated = table.Column<int>(type: "int", nullable: false),
                    CreatedCount = table.Column<int>(type: "int", nullable: false),
                    RescheduledCount = table.Column<int>(type: "int", nullable: false),
                    ReminderCount = table.Column<int>(type: "int", nullable: false),
                    DueCount = table.Column<int>(type: "int", nullable: false),
                    EscalationCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    ErrorSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_ProcurementCalendarRuns", x => x.Id);
                    table.CheckConstraint("CK_ProcurementCalendarRuns_Attempt", "[AttemptCount] >= 1");
                    table.CheckConstraint("CK_ProcurementCalendarRuns_Status", "[Status] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_ProcurementCalendarRuns_Trigger", "[Trigger] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_ProcurementCalendarRuns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementCalendarRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DueMonth = table.Column<int>(type: "int", nullable: false),
                    DueDay = table.Column<int>(type: "int", nullable: false),
                    DueLocalTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    ReminderLeadDays = table.Column<int>(type: "int", nullable: false),
                    EscalationAfterDays = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerRoleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EscalationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EscalationRoleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StatutoryReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementCalendarRules", x => x.Id);
                    table.CheckConstraint("CK_ProcurementCalendarRules_DueDay", "[DueDay] BETWEEN 1 AND 31");
                    table.CheckConstraint("CK_ProcurementCalendarRules_DueMonth", "[DueMonth] BETWEEN 1 AND 12");
                    table.CheckConstraint("CK_ProcurementCalendarRules_Escalation", "[EscalationAfterDays] BETWEEN 0 AND 365");
                    table.CheckConstraint("CK_ProcurementCalendarRules_EscalationOwner", "([EscalationUserId] IS NOT NULL AND [EscalationRoleName] IS NULL) OR ([EscalationUserId] IS NULL AND [EscalationRoleName] IS NOT NULL)");
                    table.CheckConstraint("CK_ProcurementCalendarRules_EventType", "[EventType] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_ProcurementCalendarRules_Owner", "([OwnerUserId] IS NOT NULL AND [OwnerRoleName] IS NULL) OR ([OwnerUserId] IS NULL AND [OwnerRoleName] IS NOT NULL)");
                    table.CheckConstraint("CK_ProcurementCalendarRules_Reminder", "[ReminderLeadDays] BETWEEN 0 AND 365");
                    table.ForeignKey(
                        name: "FK_ProcurementCalendarRules_ProcurementCalendarProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "ProcurementCalendarProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementCalendarRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementCalendarOccurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurrenceKey = table.Column<string>(type: "varchar(160)", unicode: false, maxLength: 160, nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileVersion = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    CalendarYear = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReminderLeadDays = table.Column<int>(type: "int", nullable: false),
                    EscalationAfterDays = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    OwnerRoleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EscalationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EscalationOwnerName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EscalationRoleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StatutoryReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastEvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RescheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RescheduleReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcknowledgedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ActionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReminderNotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReminderSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueNotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueNotificationSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalationNotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EscalatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessingAttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastProcessingError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProcurementCalendarOccurrences", x => x.Id);
                    table.CheckConstraint("CK_ProcurementCalendarOccurrences_EventType", "[EventType] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_ProcurementCalendarOccurrences_ProfileVersion", "[ProfileVersion] >= 1");
                    table.CheckConstraint("CK_ProcurementCalendarOccurrences_Status", "[Status] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_ProcurementCalendarOccurrences_Year", "[CalendarYear] BETWEEN 2000 AND 9999");
                    table.ForeignKey(
                        name: "FK_ProcurementCalendarOccurrences_ProcurementCalendarProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "ProcurementCalendarProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementCalendarOccurrences_ProcurementCalendarRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "ProcurementCalendarRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementCalendarOccurrences_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCalendarOccurrences_ProfileId",
                table: "ProcurementCalendarOccurrences",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCalendarOccurrences_RuleId",
                table: "ProcurementCalendarOccurrences",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCalendarOccurrences_TenantId_OccurrenceKey",
                table: "ProcurementCalendarOccurrences",
                columns: new[] { "TenantId", "OccurrenceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCalendarOccurrences_TenantId_OwnerUserId_Status",
                table: "ProcurementCalendarOccurrences",
                columns: new[] { "TenantId", "OwnerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCalendarOccurrences_TenantId_ProfileKey_RuleKey_CalendarYear",
                table: "ProcurementCalendarOccurrences",
                columns: new[] { "TenantId", "ProfileKey", "RuleKey", "CalendarYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCalendarOccurrences_TenantId_Status_DueAtUtc",
                table: "ProcurementCalendarOccurrences",
                columns: new[] { "TenantId", "Status", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCalendarProfiles_SupersedesProfileId",
                table: "ProcurementCalendarProfiles",
                column: "SupersedesProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCalendarProfiles_TenantId_ProfileCode_Version",
                table: "ProcurementCalendarProfiles",
                columns: new[] { "TenantId", "ProfileCode", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCalendarProfiles_TenantId_ProfileKey_Status",
                table: "ProcurementCalendarProfiles",
                columns: new[] { "TenantId", "ProfileKey", "Status" },
                unique: true,
                filter: "[Status] = 0 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCalendarProfiles_TenantId_ProfileKey_Version",
                table: "ProcurementCalendarProfiles",
                columns: new[] { "TenantId", "ProfileKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCalendarProfiles_TenantId_Status_EffectiveFromUtc",
                table: "ProcurementCalendarProfiles",
                columns: new[] { "TenantId", "Status", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCalendarRules_ProfileId",
                table: "ProcurementCalendarRules",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCalendarRules_TenantId_ProfileId_EventType",
                table: "ProcurementCalendarRules",
                columns: new[] { "TenantId", "ProfileId", "EventType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCalendarRules_TenantId_ProfileId_RuleKey",
                table: "ProcurementCalendarRules",
                columns: new[] { "TenantId", "ProfileId", "RuleKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCalendarRuns_TenantId_RunKey",
                table: "ProcurementCalendarRuns",
                columns: new[] { "TenantId", "RunKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCalendarRuns_TenantId_StartedAtUtc",
                table: "ProcurementCalendarRuns",
                columns: new[] { "TenantId", "StartedAtUtc" });

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementCalendarProfiles_LifecycleGuard]
                ON [dbo].[ProcurementCalendarProfiles]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id] = d.[Id]
                               WHERE i.[Id] IS NULL AND d.[Status] <> 0)
                        THROW 51050, 'Published and retired procurement calendar profiles cannot be deleted.', 1;

                    IF EXISTS
                    (
                        SELECT 1 FROM deleted d INNER JOIN inserted i ON i.[Id] = d.[Id]
                        WHERE d.[Status] = 2
                           OR (d.[Status] = 1 AND i.[Status] <> 2)
                           OR (d.[Status] = 1 AND
                               (i.[ProfileKey] <> d.[ProfileKey] OR i.[ProfileCode] <> d.[ProfileCode]
                                OR i.[Version] <> d.[Version] OR i.[Name] <> d.[Name]
                                OR ISNULL(i.[Description], '') <> ISNULL(d.[Description], '')
                                OR i.[TimeZoneId] <> d.[TimeZoneId]
                                OR i.[GenerationHorizonDays] <> d.[GenerationHorizonDays]
                                OR i.[CatchUpDays] <> d.[CatchUpDays]
                                OR i.[EffectiveFromUtc] <> d.[EffectiveFromUtc]
                                OR ISNULL(i.[EffectiveToUtc], '9999-12-31') <> ISNULL(d.[EffectiveToUtc], '9999-12-31')
                                OR ISNULL(i.[ApprovalReference], '') <> ISNULL(d.[ApprovalReference], '')))
                    )
                        THROW 51050, 'Published and retired procurement calendar definitions are immutable.', 1;

                    IF EXISTS
                    (
                        SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE i.[Status] = 1 AND ISNULL(d.[Status], -1) <> 1
                          AND (i.[PublishedAtUtc] IS NULL OR i.[PublishedById] IS NULL
                               OR NULLIF(LTRIM(RTRIM(i.[ApprovalReference])), '') IS NULL
                               OR (SELECT COUNT(*) FROM [dbo].[ProcurementCalendarRules] r
                                   WHERE r.[ProfileId] = i.[Id] AND r.[TenantId] = i.[TenantId]
                                     AND r.[IsDeleted] = 0 AND r.[IsEnabled] = 1) <> 7)
                    )
                        THROW 51050, 'A procurement calendar requires approval lineage and all seven enabled obligations before publication.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementCalendarRules_LifecycleGuard]
                ON [dbo].[ProcurementCalendarRules]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS
                    (
                        SELECT 1 FROM inserted i
                        LEFT JOIN [dbo].[ProcurementCalendarProfiles] p ON p.[Id] = i.[ProfileId]
                        WHERE p.[Id] IS NULL OR p.[TenantId] <> i.[TenantId] OR p.[IsDeleted] = 1
                           OR p.[Status] <> 0
                    ) OR EXISTS
                    (
                        SELECT 1 FROM deleted d
                        LEFT JOIN [dbo].[ProcurementCalendarProfiles] p ON p.[Id] = d.[ProfileId]
                        WHERE p.[Id] IS NULL OR p.[TenantId] <> d.[TenantId] OR p.[Status] <> 0
                    )
                        THROW 51051, 'Calendar rules must remain tenant-consistent and can change only while their profile is Draft.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementCalendarOccurrences_TenantGuard]
                ON [dbo].[ProcurementCalendarOccurrences]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS
                    (
                        SELECT 1 FROM inserted i
                        LEFT JOIN [dbo].[ProcurementCalendarProfiles] p ON p.[Id] = i.[ProfileId]
                        LEFT JOIN [dbo].[ProcurementCalendarRules] r ON r.[Id] = i.[RuleId]
                        WHERE p.[Id] IS NULL OR r.[Id] IS NULL OR p.[TenantId] <> i.[TenantId]
                           OR r.[TenantId] <> i.[TenantId] OR r.[ProfileId] <> p.[Id]
                           OR p.[ProfileKey] <> i.[ProfileKey] OR r.[RuleKey] <> i.[RuleKey]
                           OR p.[Version] <> i.[ProfileVersion] OR r.[EventType] <> i.[EventType]
                           OR p.[Status] <> 1 OR p.[IsDeleted] = 1 OR r.[IsDeleted] = 1
                    )
                        THROW 51052, 'Calendar occurrence tenant or published-profile lineage is invalid.', 1;

                    IF EXISTS
                    (
                        SELECT 1 FROM deleted d INNER JOIN inserted i ON i.[Id] = d.[Id]
                        WHERE d.[Status] IN (4, 5)
                           OR (d.[Status] = 0 AND i.[Status] NOT IN (0, 1, 2, 3, 4, 5))
                           OR (d.[Status] = 1 AND i.[Status] NOT IN (1, 2, 3, 4, 5))
                           OR (d.[Status] = 2 AND i.[Status] NOT IN (2, 3, 4, 5))
                           OR (d.[Status] = 3 AND i.[Status] NOT IN (3, 4, 5))
                    )
                        THROW 51053, 'Calendar occurrence status transition is invalid or terminal.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementCalendarOccurrences_TenantGuard];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementCalendarRules_LifecycleGuard];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementCalendarProfiles_LifecycleGuard];");

            migrationBuilder.DropTable(
                name: "ProcurementCalendarOccurrences");

            migrationBuilder.DropTable(
                name: "ProcurementCalendarRuns");

            migrationBuilder.DropTable(
                name: "ProcurementCalendarRules");

            migrationBuilder.DropTable(
                name: "ProcurementCalendarProfiles");
        }
    }
}
