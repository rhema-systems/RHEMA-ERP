using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR area 10 slice 15 — SHE audit management (SheAudits + team, findings and
    /// finding actions, the unified CA tracker's fifth source), stop-work authority
    /// (SheStopWorkOrders), statutory incident submissions
    /// (SheStatutoryIncidentSubmissions), and the LessonsLearned column deferred
    /// from slice 2 (FR-ENV-027).
    ///
    /// The scaffolded CreateTable/CreateIndex/AddColumn bodies are replaced with
    /// guarded SQL (repo convention): local dev DBs are built from the EF model by
    /// rebuild-db, so a DB can already carry these objects without this migration
    /// being stamped — every operation checks before it acts. The generated
    /// Designer and the regenerated snapshot are kept as scaffolded.
    /// </summary>
    public partial class AddSheAuditStopWorkStatutory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SafetyIncidents]', N'LessonsLearned') IS NULL
    ALTER TABLE [SafetyIncidents] ADD [LessonsLearned] nvarchar(2000) NULL;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheAudits]', N'U') IS NULL
BEGIN
    CREATE TABLE [SheAudits] (
        [Id] uniqueidentifier NOT NULL,
        [AuditNumber] nvarchar(30) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Type] int NOT NULL,
        [Standard] nvarchar(200) NULL,
        [Scope] nvarchar(1000) NULL,
        [Objectives] nvarchar(1000) NULL,
        [LocationId] uniqueidentifier NULL,
        [OrganizationUnitId] uniqueidentifier NULL,
        [LeadAuditorId] uniqueidentifier NOT NULL,
        [ExternalAuditorName] nvarchar(200) NULL,
        [ExternalAuditorOrganization] nvarchar(200) NULL,
        [PlannedStartDate] datetime2 NOT NULL,
        [PlannedEndDate] datetime2 NULL,
        [ActualStartDate] datetime2 NULL,
        [ActualEndDate] datetime2 NULL,
        [Status] int NOT NULL,
        [Summary] nvarchar(4000) NULL,
        [ReportDocumentPath] nvarchar(500) NULL,
        [ReportIssuedDate] datetime2 NULL,
        [ClosedDate] datetime2 NULL,
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
        CONSTRAINT [PK_SheAudits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SheAudits_Employees_ClosedById] FOREIGN KEY ([ClosedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheAudits_Employees_LeadAuditorId] FOREIGN KEY ([LeadAuditorId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheAudits_Locations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [Locations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheAudits_OrganizationUnits_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheAudits_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheAuditTeamMembers]', N'U') IS NULL
BEGIN
    CREATE TABLE [SheAuditTeamMembers] (
        [Id] uniqueidentifier NOT NULL,
        [AuditId] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Role] nvarchar(100) NOT NULL,
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
        CONSTRAINT [PK_SheAuditTeamMembers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SheAuditTeamMembers_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheAuditTeamMembers_SheAudits_AuditId] FOREIGN KEY ([AuditId]) REFERENCES [SheAudits] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheAuditTeamMembers_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheAuditFindings]', N'U') IS NULL
BEGIN
    CREATE TABLE [SheAuditFindings] (
        [Id] uniqueidentifier NOT NULL,
        [AuditId] uniqueidentifier NOT NULL,
        [FindingNumber] int NOT NULL,
        [Classification] int NOT NULL,
        [ClauseReference] nvarchar(100) NULL,
        [Description] nvarchar(2000) NOT NULL,
        [Evidence] nvarchar(2000) NULL,
        [Status] int NOT NULL,
        [ResponsiblePersonId] uniqueidentifier NULL,
        [DueDate] datetime2 NULL,
        [ResolutionNotes] nvarchar(2000) NULL,
        [ResolvedDate] datetime2 NULL,
        [VerifiedById] uniqueidentifier NULL,
        [VerifiedDate] datetime2 NULL,
        [VerificationNotes] nvarchar(1000) NULL,
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
        CONSTRAINT [PK_SheAuditFindings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SheAuditFindings_Employees_ResponsiblePersonId] FOREIGN KEY ([ResponsiblePersonId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheAuditFindings_Employees_VerifiedById] FOREIGN KEY ([VerifiedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheAuditFindings_SheAudits_AuditId] FOREIGN KEY ([AuditId]) REFERENCES [SheAudits] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheAuditFindings_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheAuditFindingActions]', N'U') IS NULL
BEGIN
    CREATE TABLE [SheAuditFindingActions] (
        [Id] uniqueidentifier NOT NULL,
        [FindingId] uniqueidentifier NOT NULL,
        [CorrectiveActionTemplateId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [DueDate] datetime2 NULL,
        [CompletionDate] datetime2 NULL,
        [CompletionNotes] nvarchar(500) NULL,
        [AssignedToId] uniqueidentifier NULL,
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
        CONSTRAINT [PK_SheAuditFindingActions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SheAuditFindingActions_Employees_AssignedToId] FOREIGN KEY ([AssignedToId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheAuditFindingActions_SheAuditFindings_FindingId] FOREIGN KEY ([FindingId]) REFERENCES [SheAuditFindings] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheAuditFindingActions_SheCorrectiveActionTemplates_CorrectiveActionTemplateId] FOREIGN KEY ([CorrectiveActionTemplateId]) REFERENCES [SheCorrectiveActionTemplates] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheAuditFindingActions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheStopWorkOrders]', N'U') IS NULL
BEGIN
    CREATE TABLE [SheStopWorkOrders] (
        [Id] uniqueidentifier NOT NULL,
        [OrderNumber] nvarchar(30) NOT NULL,
        [RaisedById] uniqueidentifier NOT NULL,
        [RaisedDate] datetime2 NOT NULL,
        [LocationId] uniqueidentifier NULL,
        [SpecificArea] nvarchar(200) NULL,
        [WorkDescription] nvarchar(1000) NOT NULL,
        [ReasonDescription] nvarchar(2000) NOT NULL,
        [ImmediateActionsTaken] nvarchar(1000) NULL,
        [PermitToWorkId] uniqueidentifier NULL,
        [HazardId] uniqueidentifier NULL,
        [IncidentId] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [RoutedToId] uniqueidentifier NULL,
        [RoutedDate] datetime2 NULL,
        [ResolutionDescription] nvarchar(2000) NULL,
        [ResolvedById] uniqueidentifier NULL,
        [ResolvedDate] datetime2 NULL,
        [ClearedById] uniqueidentifier NULL,
        [ClearedDate] datetime2 NULL,
        [ClearanceNotes] nvarchar(1000) NULL,
        [CancelledById] uniqueidentifier NULL,
        [CancelledDate] datetime2 NULL,
        [CancellationReason] nvarchar(1000) NULL,
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
        CONSTRAINT [PK_SheStopWorkOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SheStopWorkOrders_Employees_CancelledById] FOREIGN KEY ([CancelledById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheStopWorkOrders_Employees_ClearedById] FOREIGN KEY ([ClearedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheStopWorkOrders_Employees_RaisedById] FOREIGN KEY ([RaisedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheStopWorkOrders_Employees_ResolvedById] FOREIGN KEY ([ResolvedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheStopWorkOrders_Employees_RoutedToId] FOREIGN KEY ([RoutedToId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheStopWorkOrders_Locations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [Locations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheStopWorkOrders_SafetyIncidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [SafetyIncidents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheStopWorkOrders_SheHazards_HazardId] FOREIGN KEY ([HazardId]) REFERENCES [SheHazards] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheStopWorkOrders_ShePermitToWorks_PermitToWorkId] FOREIGN KEY ([PermitToWorkId]) REFERENCES [ShePermitToWorks] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheStopWorkOrders_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheStatutoryIncidentSubmissions]', N'U') IS NULL
BEGIN
    CREATE TABLE [SheStatutoryIncidentSubmissions] (
        [Id] uniqueidentifier NOT NULL,
        [IncidentId] uniqueidentifier NOT NULL,
        [RegulatoryBodyId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [Method] int NOT NULL,
        [SubmissionDate] datetime2 NOT NULL,
        [ReferenceNumber] nvarchar(100) NULL,
        [SubmittedById] uniqueidentifier NOT NULL,
        [DocumentPath] nvarchar(500) NULL,
        [AcknowledgementReceived] bit NOT NULL,
        [AcknowledgementDate] datetime2 NULL,
        [AcknowledgementReference] nvarchar(100) NULL,
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
        CONSTRAINT [PK_SheStatutoryIncidentSubmissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SheStatutoryIncidentSubmissions_Employees_SubmittedById] FOREIGN KEY ([SubmittedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheStatutoryIncidentSubmissions_SafetyIncidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [SafetyIncidents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheStatutoryIncidentSubmissions_SheRegulatoryBodies_RegulatoryBodyId] FOREIGN KEY ([RegulatoryBodyId]) REFERENCES [SheRegulatoryBodies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheStatutoryIncidentSubmissions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            // Indexes — one guarded statement each (the scaffolded set, verbatim names).
            foreach (var (index, table, columns, unique) in new (string, string, string, bool)[]
            {
                ("IX_SheAuditFindingActions_AssignedToId", "SheAuditFindingActions", "[AssignedToId]", false),
                ("IX_SheAuditFindingActions_CorrectiveActionTemplateId", "SheAuditFindingActions", "[CorrectiveActionTemplateId]", false),
                ("IX_SheAuditFindingActions_FindingId", "SheAuditFindingActions", "[FindingId]", false),
                ("IX_SheAuditFindingActions_TenantId", "SheAuditFindingActions", "[TenantId]", false),
                ("IX_SheAuditFindings_AuditId_FindingNumber", "SheAuditFindings", "[AuditId], [FindingNumber]", false),
                ("IX_SheAuditFindings_ResponsiblePersonId", "SheAuditFindings", "[ResponsiblePersonId]", false),
                ("IX_SheAuditFindings_TenantId", "SheAuditFindings", "[TenantId]", false),
                ("IX_SheAuditFindings_VerifiedById", "SheAuditFindings", "[VerifiedById]", false),
                ("IX_SheAudits_ClosedById", "SheAudits", "[ClosedById]", false),
                ("IX_SheAudits_LeadAuditorId", "SheAudits", "[LeadAuditorId]", false),
                ("IX_SheAudits_LocationId", "SheAudits", "[LocationId]", false),
                ("IX_SheAudits_OrganizationUnitId", "SheAudits", "[OrganizationUnitId]", false),
                ("IX_SheAudits_Status", "SheAudits", "[Status]", false),
                ("IX_SheAudits_TenantId_AuditNumber", "SheAudits", "[TenantId], [AuditNumber]", true),
                ("IX_SheAuditTeamMembers_AuditId", "SheAuditTeamMembers", "[AuditId]", false),
                ("IX_SheAuditTeamMembers_EmployeeId", "SheAuditTeamMembers", "[EmployeeId]", false),
                ("IX_SheAuditTeamMembers_TenantId", "SheAuditTeamMembers", "[TenantId]", false),
                ("IX_SheStatutoryIncidentSubmissions_IncidentId", "SheStatutoryIncidentSubmissions", "[IncidentId]", false),
                ("IX_SheStatutoryIncidentSubmissions_RegulatoryBodyId", "SheStatutoryIncidentSubmissions", "[RegulatoryBodyId]", false),
                ("IX_SheStatutoryIncidentSubmissions_SubmittedById", "SheStatutoryIncidentSubmissions", "[SubmittedById]", false),
                ("IX_SheStatutoryIncidentSubmissions_TenantId", "SheStatutoryIncidentSubmissions", "[TenantId]", false),
                ("IX_SheStopWorkOrders_CancelledById", "SheStopWorkOrders", "[CancelledById]", false),
                ("IX_SheStopWorkOrders_ClearedById", "SheStopWorkOrders", "[ClearedById]", false),
                ("IX_SheStopWorkOrders_HazardId", "SheStopWorkOrders", "[HazardId]", false),
                ("IX_SheStopWorkOrders_IncidentId", "SheStopWorkOrders", "[IncidentId]", false),
                ("IX_SheStopWorkOrders_LocationId", "SheStopWorkOrders", "[LocationId]", false),
                ("IX_SheStopWorkOrders_PermitToWorkId", "SheStopWorkOrders", "[PermitToWorkId]", false),
                ("IX_SheStopWorkOrders_RaisedById", "SheStopWorkOrders", "[RaisedById]", false),
                ("IX_SheStopWorkOrders_ResolvedById", "SheStopWorkOrders", "[ResolvedById]", false),
                ("IX_SheStopWorkOrders_RoutedToId", "SheStopWorkOrders", "[RoutedToId]", false),
                ("IX_SheStopWorkOrders_Status", "SheStopWorkOrders", "[Status]", false),
                ("IX_SheStopWorkOrders_TenantId_OrderNumber", "SheStopWorkOrders", "[TenantId], [OrderNumber]", true),
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
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheAuditFindingActions]', N'U') IS NOT NULL DROP TABLE [SheAuditFindingActions];
");
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheAuditTeamMembers]', N'U') IS NOT NULL DROP TABLE [SheAuditTeamMembers];
");
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheStatutoryIncidentSubmissions]', N'U') IS NOT NULL DROP TABLE [SheStatutoryIncidentSubmissions];
");
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheStopWorkOrders]', N'U') IS NOT NULL DROP TABLE [SheStopWorkOrders];
");
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheAuditFindings]', N'U') IS NOT NULL DROP TABLE [SheAuditFindings];
");
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheAudits]', N'U') IS NOT NULL DROP TABLE [SheAudits];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SafetyIncidents]', N'LessonsLearned') IS NOT NULL
    ALTER TABLE [SafetyIncidents] DROP COLUMN [LessonsLearned];
");
        }
    }
}
