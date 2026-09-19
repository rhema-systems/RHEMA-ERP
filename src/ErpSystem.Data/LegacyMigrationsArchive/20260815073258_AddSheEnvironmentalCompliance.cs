using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR area 10 slice 17 — Part D environmental core (FR-ENV). Seven new
    /// registers: environmental permits/licences (documents on the central DMS),
    /// monitoring schedules, regulatory updates, sustainability initiatives,
    /// compliance reviews with their append-only action trail, and monthly
    /// environmental reports. Plus column adds: StorageLocation on waste
    /// records (FR-ENV-020), PreventiveActions/LessonsLearned on environmental
    /// incidents (FR-ENV-027), ScheduleId on monitoring records (FR-ENV-023),
    /// and the EnvironmentalPermitsExpired run counter (FR-ENV-019).
    ///
    /// The scaffolded CreateTable/AddColumn/CreateIndex bodies are replaced
    /// with guarded SQL (repo convention): local dev DBs are built from the EF
    /// model by rebuild-db, so a DB can already carry these objects without
    /// this migration being stamped — every operation checks before it acts.
    /// The generated Designer and the regenerated snapshot are kept as
    /// scaffolded.
    /// </summary>
    public partial class AddSheEnvironmentalCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Column adds on existing tables ──
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SheWasteDisposalRecords]', N'StorageLocation') IS NULL
    ALTER TABLE [SheWasteDisposalRecords] ADD [StorageLocation] nvarchar(200) NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SheReminderRuns]', N'EnvironmentalPermitsExpired') IS NULL
    ALTER TABLE [SheReminderRuns] ADD [EnvironmentalPermitsExpired] int NOT NULL
        CONSTRAINT [DF_SheReminderRuns_EnvironmentalPermitsExpired] DEFAULT 0;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SheEnvironmentalMonitoringRecords]', N'ScheduleId') IS NULL
    ALTER TABLE [SheEnvironmentalMonitoringRecords] ADD [ScheduleId] uniqueidentifier NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SheEnvironmentalIncidents]', N'LessonsLearned') IS NULL
    ALTER TABLE [SheEnvironmentalIncidents] ADD [LessonsLearned] nvarchar(2000) NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SheEnvironmentalIncidents]', N'PreventiveActions') IS NULL
    ALTER TABLE [SheEnvironmentalIncidents] ADD [PreventiveActions] nvarchar(2000) NULL;
");

            // ── X. Environmental permit & licence register ──
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheEnvironmentalPermits]', N'U') IS NULL
BEGIN
    CREATE TABLE [SheEnvironmentalPermits] (
        [Id] uniqueidentifier NOT NULL,
        [RegisterNumber] nvarchar(30) NOT NULL,
        [PermitName] nvarchar(300) NOT NULL,
        [PermitType] int NOT NULL,
        [AuthorityReferenceNumber] nvarchar(100) NULL,
        [IssuingBodyId] uniqueidentifier NULL,
        [ResponsibleOfficerId] uniqueidentifier NOT NULL,
        [LocationId] uniqueidentifier NULL,
        [Description] nvarchar(1000) NULL,
        [Conditions] nvarchar(2000) NULL,
        [IssueDate] datetime2 NOT NULL,
        [ExpiryDate] datetime2 NOT NULL,
        [RenewalPeriodMonths] int NULL,
        [Status] int NOT NULL,
        [DocumentRecordId] uniqueidentifier NULL,
        [CurrentVersionLabel] nvarchar(20) NULL,
        [LastRenewedDate] datetime2 NULL,
        [LastRenewedById] uniqueidentifier NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SheEnvironmentalPermits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SheEnvironmentalPermits_CentralDocumentRecords_DocumentRecordId] FOREIGN KEY ([DocumentRecordId]) REFERENCES [CentralDocumentRecords] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheEnvironmentalPermits_Employees_LastRenewedById] FOREIGN KEY ([LastRenewedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheEnvironmentalPermits_Employees_ResponsibleOfficerId] FOREIGN KEY ([ResponsibleOfficerId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheEnvironmentalPermits_Locations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [Locations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheEnvironmentalPermits_SheRegulatoryBodies_IssuingBodyId] FOREIGN KEY ([IssuingBodyId]) REFERENCES [SheRegulatoryBodies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheEnvironmentalPermits_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            // ── Y. Environmental monitoring schedules ──
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheEnvironmentalMonitoringSchedules]', N'U') IS NULL
BEGIN
    CREATE TABLE [SheEnvironmentalMonitoringSchedules] (
        [Id] uniqueidentifier NOT NULL,
        [ScheduleNumber] nvarchar(30) NOT NULL,
        [MonitoringType] int NOT NULL,
        [LocationId] uniqueidentifier NULL,
        [MonitoringPoint] nvarchar(200) NULL,
        [Description] nvarchar(1000) NULL,
        [FrequencyDays] int NOT NULL,
        [NextDueDate] datetime2 NOT NULL,
        [LastPerformedDate] datetime2 NULL,
        [ResponsibleOfficerId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SheEnvironmentalMonitoringSchedules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SheEnvironmentalMonitoringSchedules_Employees_ResponsibleOfficerId] FOREIGN KEY ([ResponsibleOfficerId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheEnvironmentalMonitoringSchedules_Locations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [Locations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheEnvironmentalMonitoringSchedules_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            // The evidencing-record link (FR-ENV-023/024) — the column add above,
            // the schedules table, then the FK between them.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[FK_SheEnvironmentalMonitoringRecords_SheEnvironmentalMonitoringSchedules_ScheduleId]', N'F') IS NULL
    ALTER TABLE [SheEnvironmentalMonitoringRecords]
        ADD CONSTRAINT [FK_SheEnvironmentalMonitoringRecords_SheEnvironmentalMonitoringSchedules_ScheduleId]
        FOREIGN KEY ([ScheduleId]) REFERENCES [SheEnvironmentalMonitoringSchedules] ([Id]) ON DELETE NO ACTION;
");

            // ── Z. Regulatory updates register ──
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheRegulatoryUpdates]', N'U') IS NULL
BEGIN
    CREATE TABLE [SheRegulatoryUpdates] (
        [Id] uniqueidentifier NOT NULL,
        [UpdateNumber] nvarchar(30) NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [RegulationReference] nvarchar(100) NULL,
        [RegulatoryBodyId] uniqueidentifier NULL,
        [AuthorityName] nvarchar(200) NULL,
        [Domain] int NOT NULL,
        [Summary] nvarchar(2000) NOT NULL,
        [IssueDate] datetime2 NOT NULL,
        [EffectiveDate] datetime2 NULL,
        [AffectedDepartments] nvarchar(500) NULL,
        [ComplianceDeadline] datetime2 NULL,
        [RiskLevel] int NOT NULL,
        [RequiredActions] nvarchar(2000) NULL,
        [Status] int NOT NULL,
        [ComplianceStatus] int NOT NULL,
        [ReviewDate] datetime2 NULL,
        [OfficerComments] nvarchar(1000) NULL,
        [LinkedObligationId] uniqueidentifier NULL,
        [ManagementNotifiedAt] datetime2 NULL,
        [ManagementNotifiedById] uniqueidentifier NULL,
        [RecordedById] uniqueidentifier NOT NULL,
        [ClosedAt] datetime2 NULL,
        [ClosedById] uniqueidentifier NULL,
        [ClosureNotes] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SheRegulatoryUpdates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SheRegulatoryUpdates_Employees_ClosedById] FOREIGN KEY ([ClosedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheRegulatoryUpdates_Employees_ManagementNotifiedById] FOREIGN KEY ([ManagementNotifiedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheRegulatoryUpdates_Employees_RecordedById] FOREIGN KEY ([RecordedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheRegulatoryUpdates_SheRegulatoryBodies_RegulatoryBodyId] FOREIGN KEY ([RegulatoryBodyId]) REFERENCES [SheRegulatoryBodies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheRegulatoryUpdates_SheRegulatoryObligations_LinkedObligationId] FOREIGN KEY ([LinkedObligationId]) REFERENCES [SheRegulatoryObligations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheRegulatoryUpdates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            // ── AA. Sustainability initiatives ──
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheSustainabilityInitiatives]', N'U') IS NULL
BEGIN
    CREATE TABLE [SheSustainabilityInitiatives] (
        [Id] uniqueidentifier NOT NULL,
        [InitiativeNumber] nvarchar(30) NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [Category] int NOT NULL,
        [Description] nvarchar(2000) NULL,
        [LocationId] uniqueidentifier NULL,
        [OwnerId] uniqueidentifier NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NULL,
        [Status] int NOT NULL,
        [TargetValue] decimal(18,4) NULL,
        [ActualValue] decimal(18,4) NULL,
        [MeasurementUnit] nvarchar(50) NULL,
        [EstimatedCostSavings] decimal(18,2) NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SheSustainabilityInitiatives] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SheSustainabilityInitiatives_Employees_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheSustainabilityInitiatives_Locations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [Locations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheSustainabilityInitiatives_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            // ── AB. Environmental compliance reviews + action trail ──
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheEnvironmentalReviews]', N'U') IS NULL
BEGIN
    CREATE TABLE [SheEnvironmentalReviews] (
        [Id] uniqueidentifier NOT NULL,
        [ReviewNumber] nvarchar(30) NOT NULL,
        [ProjectName] nvarchar(300) NOT NULL,
        [WorkClassification] int NOT NULL,
        [ProjectReference] nvarchar(200) NULL,
        [OrganizationUnitId] uniqueidentifier NULL,
        [ResponsibleManagerId] uniqueidentifier NULL,
        [SubmittedById] uniqueidentifier NOT NULL,
        [SubmittedDate] datetime2 NOT NULL,
        [PlannedStartDate] datetime2 NULL,
        [Description] nvarchar(3000) NOT NULL,
        [ApplicableLaws] nvarchar(1000) NULL,
        [PermitRequired] bit NOT NULL,
        [ComplianceChecklist] nvarchar(2000) NULL,
        [RequiresRegistration] bit NOT NULL,
        [RequiresEnvironmentalPermit] bit NOT NULL,
        [RequiresFullEia] bit NOT NULL,
        [RequiresRiskAssessment] bit NOT NULL,
        [RequiresEpaSubmission] bit NOT NULL,
        [RequiresManagementApproval] bit NOT NULL,
        [ScreeningNotes] nvarchar(2000) NULL,
        [ScreeningCompletedDate] datetime2 NULL,
        [ScreenedById] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [OfficerComments] nvarchar(2000) NULL,
        [ApprovedDate] datetime2 NULL,
        [ApprovedById] uniqueidentifier NULL,
        [ManagementApprovedDate] datetime2 NULL,
        [ManagementApprovedById] uniqueidentifier NULL,
        [EpaSubmissionDate] datetime2 NULL,
        [EpaSubmissionReference] nvarchar(100) NULL,
        [ClearanceIssuedDate] datetime2 NULL,
        [ClearanceIssuedById] uniqueidentifier NULL,
        [CommencementApprovedDate] datetime2 NULL,
        [CommencementApprovedById] uniqueidentifier NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SheEnvironmentalReviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SheEnvironmentalReviews_Employees_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheEnvironmentalReviews_Employees_ClearanceIssuedById] FOREIGN KEY ([ClearanceIssuedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheEnvironmentalReviews_Employees_CommencementApprovedById] FOREIGN KEY ([CommencementApprovedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheEnvironmentalReviews_Employees_ManagementApprovedById] FOREIGN KEY ([ManagementApprovedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheEnvironmentalReviews_Employees_ResponsibleManagerId] FOREIGN KEY ([ResponsibleManagerId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheEnvironmentalReviews_Employees_ScreenedById] FOREIGN KEY ([ScreenedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheEnvironmentalReviews_Employees_SubmittedById] FOREIGN KEY ([SubmittedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheEnvironmentalReviews_OrganizationUnits_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheEnvironmentalReviews_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheEnvironmentalReviewActions]', N'U') IS NULL
BEGIN
    CREATE TABLE [SheEnvironmentalReviewActions] (
        [Id] uniqueidentifier NOT NULL,
        [ReviewId] uniqueidentifier NOT NULL,
        [Action] nvarchar(60) NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [ActorId] uniqueidentifier NOT NULL,
        [ActionDate] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SheEnvironmentalReviewActions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SheEnvironmentalReviewActions_Employees_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheEnvironmentalReviewActions_SheEnvironmentalReviews_ReviewId] FOREIGN KEY ([ReviewId]) REFERENCES [SheEnvironmentalReviews] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheEnvironmentalReviewActions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            // ── AC. Monthly environmental reports ──
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheMonthlyEnvironmentalReports]', N'U') IS NULL
BEGIN
    CREATE TABLE [SheMonthlyEnvironmentalReports] (
        [Id] uniqueidentifier NOT NULL,
        [ReportNumber] nvarchar(30) NOT NULL,
        [Year] int NOT NULL,
        [Month] int NOT NULL,
        [PeriodStart] datetime2 NOT NULL,
        [PeriodEnd] datetime2 NOT NULL,
        [GeneratedAt] datetime2 NOT NULL,
        [GeneratedById] uniqueidentifier NULL,
        [ObligationsTotal] int NOT NULL,
        [ObligationsCompliant] int NOT NULL,
        [CompliancePercentage] decimal(18,4) NULL,
        [PermitsActive] int NOT NULL,
        [PermitsExpiringIn90Days] int NOT NULL,
        [PermitsExpired] int NOT NULL,
        [ProjectsReviewed] int NOT NULL,
        [ClearancesIssued] int NOT NULL,
        [WasteGeneratedKg] decimal(18,4) NOT NULL,
        [WasteRecycledKg] decimal(18,4) NOT NULL,
        [WasteRecyclingRate] decimal(18,4) NULL,
        [EnvironmentalIncidents] int NOT NULL,
        [EnvironmentalIncidentsClosed] int NOT NULL,
        [MonitoringExceedances] int NOT NULL,
        [AuditFindingsRaised] int NOT NULL,
        [CorrectiveActionsOpen] int NOT NULL,
        [NewRegulatoryUpdates] int NOT NULL,
        [SustainabilityInitiativesActive] int NOT NULL,
        [SustainabilityInitiativesCompleted] int NOT NULL,
        [SustainabilityCostSavings] decimal(18,2) NOT NULL,
        [OfficerSummary] nvarchar(3000) NULL,
        [SubmittedToManagementAt] datetime2 NULL,
        [SubmittedById] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SheMonthlyEnvironmentalReports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SheMonthlyEnvironmentalReports_Employees_GeneratedById] FOREIGN KEY ([GeneratedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheMonthlyEnvironmentalReports_Employees_SubmittedById] FOREIGN KEY ([SubmittedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheMonthlyEnvironmentalReports_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            // ── Indexes ──
            foreach (var (index, table, columns, unique) in new (string, string, string, bool)[]
            {
                ("IX_SheEnvironmentalMonitoringRecords_ScheduleId", "SheEnvironmentalMonitoringRecords", "[ScheduleId]", false),
                ("IX_SheEnvironmentalMonitoringSchedules_LocationId", "SheEnvironmentalMonitoringSchedules", "[LocationId]", false),
                ("IX_SheEnvironmentalMonitoringSchedules_ResponsibleOfficerId", "SheEnvironmentalMonitoringSchedules", "[ResponsibleOfficerId]", false),
                ("IX_SheEnvironmentalMonitoringSchedules_TenantId_NextDueDate", "SheEnvironmentalMonitoringSchedules", "[TenantId], [NextDueDate]", false),
                ("IX_SheEnvironmentalMonitoringSchedules_TenantId_ScheduleNumber", "SheEnvironmentalMonitoringSchedules", "[TenantId], [ScheduleNumber]", true),
                ("IX_SheEnvironmentalPermits_DocumentRecordId", "SheEnvironmentalPermits", "[DocumentRecordId]", false),
                ("IX_SheEnvironmentalPermits_IssuingBodyId", "SheEnvironmentalPermits", "[IssuingBodyId]", false),
                ("IX_SheEnvironmentalPermits_LastRenewedById", "SheEnvironmentalPermits", "[LastRenewedById]", false),
                ("IX_SheEnvironmentalPermits_LocationId", "SheEnvironmentalPermits", "[LocationId]", false),
                ("IX_SheEnvironmentalPermits_ResponsibleOfficerId", "SheEnvironmentalPermits", "[ResponsibleOfficerId]", false),
                ("IX_SheEnvironmentalPermits_Status", "SheEnvironmentalPermits", "[Status]", false),
                ("IX_SheEnvironmentalPermits_TenantId_ExpiryDate", "SheEnvironmentalPermits", "[TenantId], [ExpiryDate]", false),
                ("IX_SheEnvironmentalPermits_TenantId_RegisterNumber", "SheEnvironmentalPermits", "[TenantId], [RegisterNumber]", true),
                ("IX_SheEnvironmentalReviewActions_ActorId", "SheEnvironmentalReviewActions", "[ActorId]", false),
                ("IX_SheEnvironmentalReviewActions_ReviewId", "SheEnvironmentalReviewActions", "[ReviewId]", false),
                ("IX_SheEnvironmentalReviewActions_TenantId", "SheEnvironmentalReviewActions", "[TenantId]", false),
                ("IX_SheEnvironmentalReviews_ApprovedById", "SheEnvironmentalReviews", "[ApprovedById]", false),
                ("IX_SheEnvironmentalReviews_ClearanceIssuedById", "SheEnvironmentalReviews", "[ClearanceIssuedById]", false),
                ("IX_SheEnvironmentalReviews_CommencementApprovedById", "SheEnvironmentalReviews", "[CommencementApprovedById]", false),
                ("IX_SheEnvironmentalReviews_ManagementApprovedById", "SheEnvironmentalReviews", "[ManagementApprovedById]", false),
                ("IX_SheEnvironmentalReviews_OrganizationUnitId", "SheEnvironmentalReviews", "[OrganizationUnitId]", false),
                ("IX_SheEnvironmentalReviews_ResponsibleManagerId", "SheEnvironmentalReviews", "[ResponsibleManagerId]", false),
                ("IX_SheEnvironmentalReviews_ScreenedById", "SheEnvironmentalReviews", "[ScreenedById]", false),
                ("IX_SheEnvironmentalReviews_Status", "SheEnvironmentalReviews", "[Status]", false),
                ("IX_SheEnvironmentalReviews_SubmittedById", "SheEnvironmentalReviews", "[SubmittedById]", false),
                ("IX_SheEnvironmentalReviews_TenantId_PlannedStartDate", "SheEnvironmentalReviews", "[TenantId], [PlannedStartDate]", false),
                ("IX_SheEnvironmentalReviews_TenantId_ReviewNumber", "SheEnvironmentalReviews", "[TenantId], [ReviewNumber]", true),
                ("IX_SheMonthlyEnvironmentalReports_GeneratedById", "SheMonthlyEnvironmentalReports", "[GeneratedById]", false),
                ("IX_SheMonthlyEnvironmentalReports_SubmittedById", "SheMonthlyEnvironmentalReports", "[SubmittedById]", false),
                ("IX_SheMonthlyEnvironmentalReports_TenantId_ReportNumber", "SheMonthlyEnvironmentalReports", "[TenantId], [ReportNumber]", true),
                ("IX_SheMonthlyEnvironmentalReports_TenantId_Year_Month", "SheMonthlyEnvironmentalReports", "[TenantId], [Year], [Month]", true),
                ("IX_SheRegulatoryUpdates_ClosedById", "SheRegulatoryUpdates", "[ClosedById]", false),
                ("IX_SheRegulatoryUpdates_Domain", "SheRegulatoryUpdates", "[Domain]", false),
                ("IX_SheRegulatoryUpdates_LinkedObligationId", "SheRegulatoryUpdates", "[LinkedObligationId]", false),
                ("IX_SheRegulatoryUpdates_ManagementNotifiedById", "SheRegulatoryUpdates", "[ManagementNotifiedById]", false),
                ("IX_SheRegulatoryUpdates_RecordedById", "SheRegulatoryUpdates", "[RecordedById]", false),
                ("IX_SheRegulatoryUpdates_RegulatoryBodyId", "SheRegulatoryUpdates", "[RegulatoryBodyId]", false),
                ("IX_SheRegulatoryUpdates_Status", "SheRegulatoryUpdates", "[Status]", false),
                ("IX_SheRegulatoryUpdates_TenantId_UpdateNumber", "SheRegulatoryUpdates", "[TenantId], [UpdateNumber]", true),
                ("IX_SheSustainabilityInitiatives_Category", "SheSustainabilityInitiatives", "[Category]", false),
                ("IX_SheSustainabilityInitiatives_LocationId", "SheSustainabilityInitiatives", "[LocationId]", false),
                ("IX_SheSustainabilityInitiatives_OwnerId", "SheSustainabilityInitiatives", "[OwnerId]", false),
                ("IX_SheSustainabilityInitiatives_Status", "SheSustainabilityInitiatives", "[Status]", false),
                ("IX_SheSustainabilityInitiatives_TenantId_InitiativeNumber", "SheSustainabilityInitiatives", "[TenantId], [InitiativeNumber]", true),
            })
            {
                migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'{index}' AND [object_id] = OBJECT_ID(N'[{table}]'))
    CREATE {(unique ? "UNIQUE " : "")}INDEX [{index}] ON [{table}] ({columns});
");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Children before parents; the FK and index on the surviving
            // monitoring-records table before its column.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[FK_SheEnvironmentalMonitoringRecords_SheEnvironmentalMonitoringSchedules_ScheduleId]', N'F') IS NOT NULL
    ALTER TABLE [SheEnvironmentalMonitoringRecords] DROP CONSTRAINT [FK_SheEnvironmentalMonitoringRecords_SheEnvironmentalMonitoringSchedules_ScheduleId];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SheEnvironmentalMonitoringRecords_ScheduleId' AND [object_id] = OBJECT_ID(N'[SheEnvironmentalMonitoringRecords]'))
    DROP INDEX [IX_SheEnvironmentalMonitoringRecords_ScheduleId] ON [SheEnvironmentalMonitoringRecords];
");
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheEnvironmentalReviewActions]', N'U') IS NOT NULL DROP TABLE [SheEnvironmentalReviewActions];
IF OBJECT_ID(N'[SheEnvironmentalReviews]', N'U') IS NOT NULL DROP TABLE [SheEnvironmentalReviews];
IF OBJECT_ID(N'[SheEnvironmentalMonitoringSchedules]', N'U') IS NOT NULL DROP TABLE [SheEnvironmentalMonitoringSchedules];
IF OBJECT_ID(N'[SheEnvironmentalPermits]', N'U') IS NOT NULL DROP TABLE [SheEnvironmentalPermits];
IF OBJECT_ID(N'[SheMonthlyEnvironmentalReports]', N'U') IS NOT NULL DROP TABLE [SheMonthlyEnvironmentalReports];
IF OBJECT_ID(N'[SheRegulatoryUpdates]', N'U') IS NOT NULL DROP TABLE [SheRegulatoryUpdates];
IF OBJECT_ID(N'[SheSustainabilityInitiatives]', N'U') IS NOT NULL DROP TABLE [SheSustainabilityInitiatives];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SheEnvironmentalMonitoringRecords]', N'ScheduleId') IS NOT NULL
    ALTER TABLE [SheEnvironmentalMonitoringRecords] DROP COLUMN [ScheduleId];
IF COL_LENGTH(N'[SheEnvironmentalIncidents]', N'LessonsLearned') IS NOT NULL
    ALTER TABLE [SheEnvironmentalIncidents] DROP COLUMN [LessonsLearned];
IF COL_LENGTH(N'[SheEnvironmentalIncidents]', N'PreventiveActions') IS NOT NULL
    ALTER TABLE [SheEnvironmentalIncidents] DROP COLUMN [PreventiveActions];
IF COL_LENGTH(N'[SheWasteDisposalRecords]', N'StorageLocation') IS NOT NULL
    ALTER TABLE [SheWasteDisposalRecords] DROP COLUMN [StorageLocation];
IF OBJECT_ID(N'[DF_SheReminderRuns_EnvironmentalPermitsExpired]', N'D') IS NOT NULL
    ALTER TABLE [SheReminderRuns] DROP CONSTRAINT [DF_SheReminderRuns_EnvironmentalPermitsExpired];
IF COL_LENGTH(N'[SheReminderRuns]', N'EnvironmentalPermitsExpired') IS NOT NULL
    ALTER TABLE [SheReminderRuns] DROP COLUMN [EnvironmentalPermitsExpired];
");
        }
    }
}
