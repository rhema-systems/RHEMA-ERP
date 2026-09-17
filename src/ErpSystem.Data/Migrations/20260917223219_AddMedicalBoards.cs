using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Leave residue plan, slice G4 (R-15b): the medical board — a panel convened to rule on an
    /// employee's fitness, its members, its sittings and its recommendation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder create the
    /// schema from the EF model, so a rebuilt database already has these objects and a bare
    /// <c>CreateTable</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>The tables live in Medical because a ruling on fitness is a clinical record.</b> The
    /// SHE↔Medical ownership boundary settles it, and everything here is gated on
    /// <c>HR.Medical.*</c> — including the reads, because findings and sitting notes describe
    /// somebody's health.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ Two bridge columns, and neither carries a foreign key.</b>
    /// <c>LeaveRequests.MedicalBoardId</c> and <c>EmployeeSeparations.MedicalBoardId</c> are bare
    /// <c>uniqueidentifier NULL</c> on purpose. The bridge is <b>by reference and one way</b>: leave
    /// reads a board to satisfy its evidence rule, separation reads one to justify a medical
    /// retirement, and <b>neither writes to it</b>. A real FK with a navigation would pull the board
    /// into those modules' object graphs, where an EF fixup could modify a clinical record through a
    /// leave save. The scaffold agreed — it produced plain columns — and this note exists so nobody
    /// "tidies up the missing foreign key" later.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ No shadow FK was minted, and that was worth checking.</b> <c>MedicalBoardMember</c> has
    /// navigations to both <c>Physician</c> and <c>Employee</c>, and <c>MedicalBoard</c> has its own
    /// <c>Employee</c> — each pairs with a matching <c>XId</c> property by EF convention, so no
    /// <c>EmployeeId1</c> appeared. The board's own <c>RequestedById</c> and <c>ConcludedById</c> are
    /// deliberately bare Guids for the opposite reason: a second, differently-named navigation to
    /// <c>Employee</c> on the same entity cannot be paired and would mint one.
    /// </para>
    /// </remarks>
    public partial class AddMedicalBoards : Migration
    {
        private const string Boards = "MedicalBoards";
        private const string Members = "MedicalBoardMembers";
        private const string Sittings = "MedicalBoardSittings";

        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string DropColumn(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];";

        private static string CreateIndex(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        private static string DropTable(string table) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
    DROP TABLE [dbo].[{table}];";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── The board ───────────────────────────────────────────────────────────────────
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Boards}', 'U') IS NULL
CREATE TABLE [dbo].[{Boards}] (
    [Id]                          uniqueidentifier NOT NULL,
    [BoardNumber]                 nvarchar(40)     NOT NULL,
    [EmployeeId]                  uniqueidentifier NOT NULL,
    [Status]                      int              NOT NULL,
    [Reason]                      nvarchar(1000)   NOT NULL,
    [RequestedById]               uniqueidentifier NULL,
    [RequestedOn]                 date             NOT NULL,
    [ConvenedOn]                  date             NULL,
    [HealthProfileId]             uniqueidentifier NULL,
    [BasedOnExamId]               uniqueidentifier NULL,
    [FacilityId]                  uniqueidentifier NULL,
    [Outcome]                     int              NULL,
    [Findings]                    nvarchar(4000)   NULL,
    [Recommendation]              nvarchar(4000)   NULL,
    [Restrictions]                nvarchar(2000)   NULL,
    [ReviewDueDate]               date             NULL,
    [RecommendsMedicalRetirement] bit              NOT NULL,
    [ConcludedOn]                 date             NULL,
    [ConcludedById]               uniqueidentifier NULL,
    [CancellationReason]          nvarchar(1000)   NULL,
    [CreatedAt]                   datetime2        NOT NULL,
    [UpdatedAt]                   datetime2        NULL,
    [CreatedBy]                   nvarchar(max)    NULL,
    [UpdatedBy]                   nvarchar(max)    NULL,
    [CreatedById]                 uniqueidentifier NULL,
    [LastModifiedById]            uniqueidentifier NULL,
    [IsDeleted]                   bit              NOT NULL,
    [DeletedAt]                   datetime2        NULL,
    [DeletedBy]                   nvarchar(max)    NULL,
    [TenantId]                    uniqueidentifier NOT NULL,
    CONSTRAINT [PK_{Boards}] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_{Boards}_Employees_EmployeeId] FOREIGN KEY ([EmployeeId])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_{Boards}_EmployeeHealthProfiles_HealthProfileId] FOREIGN KEY ([HealthProfileId])
        REFERENCES [dbo].[EmployeeHealthProfiles] ([Id]),
    CONSTRAINT [FK_{Boards}_EmployeeMedicalExams_BasedOnExamId] FOREIGN KEY ([BasedOnExamId])
        REFERENCES [dbo].[EmployeeMedicalExams] ([Id]),
    CONSTRAINT [FK_{Boards}_HealthcareFacilities_FacilityId] FOREIGN KEY ([FacilityId])
        REFERENCES [dbo].[HealthcareFacilities] ([Id]),
    CONSTRAINT [FK_{Boards}_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");

            // ── Its members ─────────────────────────────────────────────────────────────────
            //
            // ⚠ PhysicianId, EmployeeId and MemberName are ALL nullable: a member is identified by
            // exactly one of them, and which one depends on who they are. The rule that at least one
            // must be present is enforced in the service rather than by a CHECK constraint, so the
            // refusal can say what to do instead of failing on a constraint name.
            //
            // ⚠ The FK to Employees is deliberately NOT cascade. A board's membership is part of the
            // record of what it decided; deleting an employee must not quietly remove them from a
            // board they sat on and leave the minutes short of a member.
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Members}', 'U') IS NULL
CREATE TABLE [dbo].[{Members}] (
    [Id]               uniqueidentifier NOT NULL,
    [BoardId]          uniqueidentifier NOT NULL,
    [PhysicianId]      uniqueidentifier NULL,
    [EmployeeId]       uniqueidentifier NULL,
    [MemberName]       nvarchar(200)    NULL,
    [Institution]      nvarchar(200)    NULL,
    [Role]             int              NOT NULL,
    [CreatedAt]        datetime2        NOT NULL,
    [UpdatedAt]        datetime2        NULL,
    [CreatedBy]        nvarchar(max)    NULL,
    [UpdatedBy]        nvarchar(max)    NULL,
    [CreatedById]      uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted]        bit              NOT NULL,
    [DeletedAt]        datetime2        NULL,
    [DeletedBy]        nvarchar(max)    NULL,
    [TenantId]         uniqueidentifier NOT NULL,
    CONSTRAINT [PK_{Members}] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_{Members}_{Boards}_BoardId] FOREIGN KEY ([BoardId])
        REFERENCES [dbo].[{Boards}] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_{Members}_Employees_EmployeeId] FOREIGN KEY ([EmployeeId])
        REFERENCES [dbo].[Employees] ([Id]),
    CONSTRAINT [FK_{Members}_Physicians_PhysicianId] FOREIGN KEY ([PhysicianId])
        REFERENCES [dbo].[Physicians] ([Id]),
    CONSTRAINT [FK_{Members}_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");

            // ── Its sittings ────────────────────────────────────────────────────────────────
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Sittings}', 'U') IS NULL
CREATE TABLE [dbo].[{Sittings}] (
    [Id]               uniqueidentifier NOT NULL,
    [BoardId]          uniqueidentifier NOT NULL,
    [SittingDate]      date             NOT NULL,
    [Venue]            nvarchar(300)    NULL,
    [Notes]            nvarchar(4000)   NULL,
    [CreatedAt]        datetime2        NOT NULL,
    [UpdatedAt]        datetime2        NULL,
    [CreatedBy]        nvarchar(max)    NULL,
    [UpdatedBy]        nvarchar(max)    NULL,
    [CreatedById]      uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted]        bit              NOT NULL,
    [DeletedAt]        datetime2        NULL,
    [DeletedBy]        nvarchar(max)    NULL,
    [TenantId]         uniqueidentifier NOT NULL,
    CONSTRAINT [PK_{Sittings}] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_{Sittings}_{Boards}_BoardId] FOREIGN KEY ([BoardId])
        REFERENCES [dbo].[{Boards}] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_{Sittings}_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");

            migrationBuilder.Sql(CreateIndex(Boards, $"IX_{Boards}_EmployeeId", "[EmployeeId]"));
            migrationBuilder.Sql(CreateIndex(Boards, $"IX_{Boards}_TenantId", "[TenantId]"));
            migrationBuilder.Sql(CreateIndex(Boards, $"IX_{Boards}_HealthProfileId", "[HealthProfileId]"));
            migrationBuilder.Sql(CreateIndex(Boards, $"IX_{Boards}_BasedOnExamId", "[BasedOnExamId]"));
            migrationBuilder.Sql(CreateIndex(Boards, $"IX_{Boards}_FacilityId", "[FacilityId]"));

            migrationBuilder.Sql(CreateIndex(Members, $"IX_{Members}_BoardId", "[BoardId]"));
            migrationBuilder.Sql(CreateIndex(Members, $"IX_{Members}_TenantId", "[TenantId]"));
            migrationBuilder.Sql(CreateIndex(Members, $"IX_{Members}_PhysicianId", "[PhysicianId]"));
            migrationBuilder.Sql(CreateIndex(Members, $"IX_{Members}_EmployeeId", "[EmployeeId]"));

            migrationBuilder.Sql(CreateIndex(Sittings, $"IX_{Sittings}_BoardId", "[BoardId]"));
            migrationBuilder.Sql(CreateIndex(Sittings, $"IX_{Sittings}_TenantId", "[TenantId]"));

            // ── The two bridges. Plain columns, no FK — see the remarks. ────────────────────
            migrationBuilder.Sql(AddColumn("LeaveRequests", "MedicalBoardId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn("EmployeeSeparations", "MedicalBoardId", "uniqueidentifier NULL"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropColumn("LeaveRequests", "MedicalBoardId"));
            migrationBuilder.Sql(DropColumn("EmployeeSeparations", "MedicalBoardId"));

            // Children first — the cascades would handle it, but an explicit order survives a
            // partially-applied Up, which is the case a guarded Down exists for.
            migrationBuilder.Sql(DropTable(Sittings));
            migrationBuilder.Sql(DropTable(Members));
            migrationBuilder.Sql(DropTable(Boards));
        }
    }
}
