using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 5, lane K-II-a: a medical board becomes a panel that hears CASES — one per employee —
    /// each decided at a sitting whose attendance is who decided.
    /// </summary>
    /// <remarks>
    /// <para><b>New:</b> <c>MedicalBoardCases</c> (unique per board and employee, among live rows),
    /// <c>MedicalBoardSittingAttendances</c> (unique per sitting and member), <c>MedicalBoards.Kind</c>
    /// (1 = the employer's own board, for every existing board), <c>MedicalBoardDocuments.CaseId</c>, and
    /// <c>CompanyHrPolicySettings.MedicalBoardQuorum</c> (1 for every existing tenant, today's rule —
    /// never the scaffold's 0, which would let a case be decided with nobody present).</para>
    ///
    /// <para><b>Moved:</b> every existing board becomes a board with one case, carrying its employee,
    /// purpose, reason, clinical references and finding. A Concluded board's case is Concluded, decided
    /// at the board's last sitting; a Cancelled board's case is Withdrawn, with the cancellation's date,
    /// actor and reason; any other board's case is Listed. ⚠ <b>No attendance is invented</b>: who sat
    /// at a sitting before K-II-a was never recorded, so those findings read "decided before
    /// attendance was kept" rather than naming a panel nobody wrote down. Then the moved columns are
    /// dropped from <c>MedicalBoards</c>, with their foreign keys, indexes and default constraints.</para>
    ///
    /// <para>⚠ <b>The scaffold RENAMED <c>Purpose</c> to <c>Kind</c></b> — it saw one int column go and
    /// another arrive. Applied, every board's purpose (1–5) would have become its "kind" (1–3, so 4 and
    /// 5 no kind at all) and the question each board was asked would have been lost. Here <c>Kind</c> is
    /// a new column and <c>Purpose</c> moves to the case.</para>
    ///
    /// <para>⚠ <b>Down refuses rather than losing a case.</b> The previous shape holds one employee per
    /// board, so a board with more than one live case, or none, cannot be put back; Down stops with a
    /// message naming how many, instead of choosing which findings to throw away.</para>
    ///
    /// <para>Guarded SQL, as on every HR migration: <c>rebuild-db</c> and the UAT builder create the
    /// schema from the EF model, so a rebuilt database already has the new shape and the data move
    /// finds nothing to move. Statements naming columns that may not exist run as dynamic SQL, because
    /// SQL Server compiles a batch before running it.</para>
    /// </remarks>
    public partial class AddMedicalBoardCases : Migration
    {
        private const string Boards = "MedicalBoards";
        private const string Cases = "MedicalBoardCases";
        private const string Attendances = "MedicalBoardSittingAttendances";

        /// <summary>The columns that move from the board to its case, and leave the board.</summary>
        private static readonly string[] MovedColumns =
        {
            "EmployeeId", "Purpose", "Reason", "HealthProfileId", "BasedOnExamId", "Outcome", "Findings",
            "Recommendation", "Restrictions", "ReviewDueDate", "RecommendsMedicalRetirement", "ConcludedById",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. The new tables ──────────────────────────────────────────────────────────────────
            //
            // ⚠ The case's employee is NOT cascade (the board's own was): a finding is part of the
            // record, and deleting an employee must not quietly delete what a board decided about them.
            // It also keeps a single cascade path into this table — from the board.
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Cases}', 'U') IS NULL
CREATE TABLE [dbo].[{Cases}] (
    [Id]                          uniqueidentifier NOT NULL,
    [BoardId]                     uniqueidentifier NOT NULL,
    [EmployeeId]                  uniqueidentifier NOT NULL,
    [Purpose]                     int              NOT NULL,
    [Reason]                      nvarchar(1000)   NOT NULL,
    [RequestedById]               uniqueidentifier NULL,
    [RequestedOn]                 date             NOT NULL,
    [HealthProfileId]             uniqueidentifier NULL,
    [BasedOnExamId]               uniqueidentifier NULL,
    [Status]                      int              NOT NULL,
    [DecidedAtSittingId]          uniqueidentifier NULL,
    [Outcome]                     int              NULL,
    [Findings]                    nvarchar(4000)   NULL,
    [Recommendation]              nvarchar(4000)   NULL,
    [Restrictions]                nvarchar(2000)   NULL,
    [ReviewDueDate]               date             NULL,
    [RecommendsMedicalRetirement] bit              NOT NULL,
    [ConcludedOn]                 date             NULL,
    [ConcludedById]               uniqueidentifier NULL,
    [WithdrawnOn]                 date             NULL,
    [WithdrawnById]               uniqueidentifier NULL,
    [WithdrawalReason]            nvarchar(1000)   NULL,
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
    CONSTRAINT [PK_{Cases}] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_{Cases}_{Boards}_BoardId] FOREIGN KEY ([BoardId])
        REFERENCES [dbo].[{Boards}] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_{Cases}_Employees_EmployeeId] FOREIGN KEY ([EmployeeId])
        REFERENCES [dbo].[Employees] ([Id]),
    CONSTRAINT [FK_{Cases}_EmployeeHealthProfiles_HealthProfileId] FOREIGN KEY ([HealthProfileId])
        REFERENCES [dbo].[EmployeeHealthProfiles] ([Id]),
    CONSTRAINT [FK_{Cases}_EmployeeMedicalExams_BasedOnExamId] FOREIGN KEY ([BasedOnExamId])
        REFERENCES [dbo].[EmployeeMedicalExams] ([Id]),
    CONSTRAINT [FK_{Cases}_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");

            // ⚠ MemberId is a bare Guid on purpose: a foreign key to the members would be a second
            // cascade path from the board (through members, and through sittings), which SQL Server
            // refuses. The service checks it.
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Attendances}', 'U') IS NULL
CREATE TABLE [dbo].[{Attendances}] (
    [Id]               uniqueidentifier NOT NULL,
    [SittingId]        uniqueidentifier NOT NULL,
    [MemberId]         uniqueidentifier NOT NULL,
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
    CONSTRAINT [PK_{Attendances}] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_{Attendances}_MedicalBoardSittings_SittingId] FOREIGN KEY ([SittingId])
        REFERENCES [dbo].[MedicalBoardSittings] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_{Attendances}_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");

            // ── 2. The new columns ─────────────────────────────────────────────────────────────────
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Boards}', 'Kind') IS NULL
    ALTER TABLE [dbo].[{Boards}] ADD [Kind] int NOT NULL DEFAULT (1);
IF COL_LENGTH('dbo.MedicalBoardDocuments', 'CaseId') IS NULL
    ALTER TABLE [dbo].[MedicalBoardDocuments] ADD [CaseId] uniqueidentifier NULL;
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'MedicalBoardQuorum') IS NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings] ADD [MedicalBoardQuorum] int NOT NULL DEFAULT (1);");

            // ── 3. Every existing board becomes a board with one case ──────────────────────────────
            //
            // Only while the board still carries its employee (a model-built database never did), and
            // only for boards without a case yet, so a second run moves nothing. Dynamic, because the
            // columns it reads are gone once step 4 has run.
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Boards}', 'EmployeeId') IS NOT NULL
   AND OBJECT_ID('dbo.{Cases}', 'U') IS NOT NULL
EXEC sp_executesql N'
INSERT INTO [dbo].[{Cases}] (
    [Id], [BoardId], [EmployeeId], [Purpose], [Reason], [RequestedById], [RequestedOn],
    [HealthProfileId], [BasedOnExamId], [Status], [DecidedAtSittingId], [Outcome], [Findings],
    [Recommendation], [Restrictions], [ReviewDueDate], [RecommendsMedicalRetirement],
    [ConcludedOn], [ConcludedById], [WithdrawnOn], [WithdrawnById], [WithdrawalReason],
    [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [CreatedById], [LastModifiedById],
    [IsDeleted], [DeletedAt], [DeletedBy], [TenantId])
SELECT
    NEWID(), b.[Id], b.[EmployeeId], b.[Purpose], b.[Reason], b.[RequestedById], b.[RequestedOn],
    b.[HealthProfileId], b.[BasedOnExamId],
    CASE b.[Status] WHEN 3 THEN 2 WHEN 4 THEN 3 ELSE 1 END,
    CASE WHEN b.[Status] = 3 THEN (
        SELECT TOP (1) s.[Id] FROM [dbo].[MedicalBoardSittings] s
        WHERE s.[BoardId] = b.[Id] AND s.[IsDeleted] = 0
        ORDER BY s.[SittingDate] DESC, s.[CreatedAt] DESC) END,
    b.[Outcome], b.[Findings], b.[Recommendation], b.[Restrictions], b.[ReviewDueDate],
    b.[RecommendsMedicalRetirement],
    CASE WHEN b.[Status] = 3 THEN b.[ConcludedOn] END,
    CASE WHEN b.[Status] = 3 THEN b.[ConcludedById] END,
    CASE WHEN b.[Status] = 4 THEN b.[CancelledOn] END,
    CASE WHEN b.[Status] = 4 THEN b.[CancelledById] END,
    CASE WHEN b.[Status] = 4 THEN LEFT(
        CASE WHEN b.[ConvenedOn] IS NULL THEN N''The request for a board was cancelled: ''
             ELSE N''The board was dissolved: '' END
        + COALESCE(NULLIF(LTRIM(RTRIM(b.[CancellationReason])), N''''), N''no reason was recorded.''), 1000) END,
    b.[CreatedAt], b.[UpdatedAt], b.[CreatedBy], b.[UpdatedBy], b.[CreatedById], b.[LastModifiedById],
    b.[IsDeleted], b.[DeletedAt], b.[DeletedBy], b.[TenantId]
FROM [dbo].[{Boards}] b
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[{Cases}] c WHERE c.[BoardId] = b.[Id]);';");

            // ── 4. The moved columns leave the board ───────────────────────────────────────────────
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.FK_{Boards}_Employees_EmployeeId', 'F') IS NOT NULL
    ALTER TABLE [dbo].[{Boards}] DROP CONSTRAINT [FK_{Boards}_Employees_EmployeeId];
IF OBJECT_ID('dbo.FK_{Boards}_EmployeeHealthProfiles_HealthProfileId', 'F') IS NOT NULL
    ALTER TABLE [dbo].[{Boards}] DROP CONSTRAINT [FK_{Boards}_EmployeeHealthProfiles_HealthProfileId];
IF OBJECT_ID('dbo.FK_{Boards}_EmployeeMedicalExams_BasedOnExamId', 'F') IS NOT NULL
    ALTER TABLE [dbo].[{Boards}] DROP CONSTRAINT [FK_{Boards}_EmployeeMedicalExams_BasedOnExamId];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_{Boards}_EmployeeId' AND object_id = OBJECT_ID('dbo.{Boards}'))
    DROP INDEX [IX_{Boards}_EmployeeId] ON [dbo].[{Boards}];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_{Boards}_HealthProfileId' AND object_id = OBJECT_ID('dbo.{Boards}'))
    DROP INDEX [IX_{Boards}_HealthProfileId] ON [dbo].[{Boards}];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_{Boards}_BasedOnExamId' AND object_id = OBJECT_ID('dbo.{Boards}'))
    DROP INDEX [IX_{Boards}_BasedOnExamId] ON [dbo].[{Boards}];");

            foreach (var column in MovedColumns)
                migrationBuilder.Sql(DropColumnWithDefaultSql(Boards, column));

            // ── 5. Indexes on the new tables ───────────────────────────────────────────────────────
            migrationBuilder.Sql(CreateIndex(Cases, $"IX_{Cases}_BasedOnExamId", "[BasedOnExamId]"));
            migrationBuilder.Sql(CreateIndex(Cases, $"IX_{Cases}_EmployeeId", "[EmployeeId]"));
            migrationBuilder.Sql(CreateIndex(Cases, $"IX_{Cases}_HealthProfileId", "[HealthProfileId]"));
            migrationBuilder.Sql(CreateIndex(Cases, $"IX_{Cases}_TenantId", "[TenantId]"));
            migrationBuilder.Sql(CreateIndex(Cases, $"UX_{Cases}_Board_Employee", "[BoardId], [EmployeeId]",
                unique: true, filter: "[IsDeleted] = 0"));
            migrationBuilder.Sql(CreateIndex(Attendances, $"IX_{Attendances}_TenantId", "[TenantId]"));
            migrationBuilder.Sql(CreateIndex(Attendances, $"UX_{Attendances}_Sitting_Member", "[SittingId], [MemberId]",
                unique: true, filter: "[IsDeleted] = 0"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── 1. Refuse what the previous shape cannot hold ──────────────────────────────────────
            //
            // One employee per board: a board with several live cases, or none, has no faithful
            // representation there. Stop and say so rather than pick which findings to lose.
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Cases}', 'U') IS NOT NULL AND COL_LENGTH('dbo.{Boards}', 'EmployeeId') IS NULL
BEGIN
    DECLARE @unrepresentable int;
    EXEC sp_executesql N'
        SELECT @n = COUNT(*) FROM [dbo].[{Boards}] b
        WHERE (SELECT COUNT(*) FROM [dbo].[{Cases}] c WHERE c.[BoardId] = b.[Id] AND c.[IsDeleted] = 0) > 1
           OR NOT EXISTS (SELECT 1 FROM [dbo].[{Cases}] c WHERE c.[BoardId] = b.[Id]);',
        N'@n int OUTPUT', @n = @unrepresentable OUTPUT;
    IF @unrepresentable > 0
    BEGIN
        DECLARE @message nvarchar(400) = CONCAT(@unrepresentable,
            N' medical board(s) hear more than one case, or none. The previous shape holds one employee per board, so this migration cannot be reverted without losing findings.');
        THROW 50001, @message, 1;
    END
END");

            // ── 2. The board's columns come back ───────────────────────────────────────────────────
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Boards}', 'EmployeeId') IS NULL
    ALTER TABLE [dbo].[{Boards}] ADD [EmployeeId] uniqueidentifier NULL;
IF COL_LENGTH('dbo.{Boards}', 'Purpose') IS NULL
    ALTER TABLE [dbo].[{Boards}] ADD [Purpose] int NOT NULL DEFAULT (5);
IF COL_LENGTH('dbo.{Boards}', 'Reason') IS NULL
    ALTER TABLE [dbo].[{Boards}] ADD [Reason] nvarchar(1000) NOT NULL DEFAULT (N'');
IF COL_LENGTH('dbo.{Boards}', 'HealthProfileId') IS NULL
    ALTER TABLE [dbo].[{Boards}] ADD [HealthProfileId] uniqueidentifier NULL;
IF COL_LENGTH('dbo.{Boards}', 'BasedOnExamId') IS NULL
    ALTER TABLE [dbo].[{Boards}] ADD [BasedOnExamId] uniqueidentifier NULL;
IF COL_LENGTH('dbo.{Boards}', 'Outcome') IS NULL
    ALTER TABLE [dbo].[{Boards}] ADD [Outcome] int NULL;
IF COL_LENGTH('dbo.{Boards}', 'Findings') IS NULL
    ALTER TABLE [dbo].[{Boards}] ADD [Findings] nvarchar(4000) NULL;
IF COL_LENGTH('dbo.{Boards}', 'Recommendation') IS NULL
    ALTER TABLE [dbo].[{Boards}] ADD [Recommendation] nvarchar(4000) NULL;
IF COL_LENGTH('dbo.{Boards}', 'Restrictions') IS NULL
    ALTER TABLE [dbo].[{Boards}] ADD [Restrictions] nvarchar(2000) NULL;
IF COL_LENGTH('dbo.{Boards}', 'ReviewDueDate') IS NULL
    ALTER TABLE [dbo].[{Boards}] ADD [ReviewDueDate] date NULL;
IF COL_LENGTH('dbo.{Boards}', 'RecommendsMedicalRetirement') IS NULL
    ALTER TABLE [dbo].[{Boards}] ADD [RecommendsMedicalRetirement] bit NOT NULL DEFAULT (0);
IF COL_LENGTH('dbo.{Boards}', 'ConcludedById') IS NULL
    ALTER TABLE [dbo].[{Boards}] ADD [ConcludedById] uniqueidentifier NULL;");

            // ── 3. Copied back from each board's one case ──────────────────────────────────────────
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Cases}', 'U') IS NOT NULL
EXEC sp_executesql N'
UPDATE b SET
    b.[EmployeeId] = c.[EmployeeId], b.[Purpose] = c.[Purpose], b.[Reason] = c.[Reason],
    b.[HealthProfileId] = c.[HealthProfileId], b.[BasedOnExamId] = c.[BasedOnExamId],
    b.[Outcome] = c.[Outcome], b.[Findings] = c.[Findings], b.[Recommendation] = c.[Recommendation],
    b.[Restrictions] = c.[Restrictions], b.[ReviewDueDate] = c.[ReviewDueDate],
    b.[RecommendsMedicalRetirement] = c.[RecommendsMedicalRetirement], b.[ConcludedById] = c.[ConcludedById]
FROM [dbo].[{Boards}] b
CROSS APPLY (
    SELECT TOP (1) * FROM [dbo].[{Cases}] x
    WHERE x.[BoardId] = b.[Id]
    ORDER BY x.[IsDeleted], x.[RequestedOn], x.[CreatedAt]) c
WHERE b.[EmployeeId] IS NULL;';");

            // ── 4. The board's employee is required again, with its keys and indexes ───────────────
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Boards}', 'EmployeeId') IS NOT NULL
   AND COLUMNPROPERTY(OBJECT_ID('dbo.{Boards}'), 'EmployeeId', 'AllowsNull') = 1
    ALTER TABLE [dbo].[{Boards}] ALTER COLUMN [EmployeeId] uniqueidentifier NOT NULL;");

            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.FK_{Boards}_Employees_EmployeeId', 'F') IS NULL
    ALTER TABLE [dbo].[{Boards}] ADD CONSTRAINT [FK_{Boards}_Employees_EmployeeId]
        FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE CASCADE;
IF OBJECT_ID('dbo.FK_{Boards}_EmployeeHealthProfiles_HealthProfileId', 'F') IS NULL
    ALTER TABLE [dbo].[{Boards}] ADD CONSTRAINT [FK_{Boards}_EmployeeHealthProfiles_HealthProfileId]
        FOREIGN KEY ([HealthProfileId]) REFERENCES [dbo].[EmployeeHealthProfiles] ([Id]);
IF OBJECT_ID('dbo.FK_{Boards}_EmployeeMedicalExams_BasedOnExamId', 'F') IS NULL
    ALTER TABLE [dbo].[{Boards}] ADD CONSTRAINT [FK_{Boards}_EmployeeMedicalExams_BasedOnExamId]
        FOREIGN KEY ([BasedOnExamId]) REFERENCES [dbo].[EmployeeMedicalExams] ([Id]);");

            migrationBuilder.Sql(CreateIndex(Boards, $"IX_{Boards}_EmployeeId", "[EmployeeId]"));
            migrationBuilder.Sql(CreateIndex(Boards, $"IX_{Boards}_HealthProfileId", "[HealthProfileId]"));
            migrationBuilder.Sql(CreateIndex(Boards, $"IX_{Boards}_BasedOnExamId", "[BasedOnExamId]"));

            // ── 5. What K-II-a added goes ──────────────────────────────────────────────────────────
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Attendances}', 'U') IS NOT NULL
    DROP TABLE [dbo].[{Attendances}];
IF OBJECT_ID('dbo.{Cases}', 'U') IS NOT NULL
    DROP TABLE [dbo].[{Cases}];");

            migrationBuilder.Sql(DropColumnWithDefaultSql("MedicalBoardDocuments", "CaseId"));
            migrationBuilder.Sql(DropColumnWithDefaultSql("CompanyHrPolicySettings", "MedicalBoardQuorum"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Boards, "Kind"));
        }

        private static string CreateIndex(string table, string index, string columns, bool unique = false, string filter = null)
        {
            var uniqueness = unique ? "UNIQUE " : string.Empty;
            var where = filter is null ? string.Empty : " WHERE " + filter;
            return $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE {uniqueness}INDEX [{index}] ON [dbo].[{table}] ({columns}){where};";
        }

        /// <summary>
        /// Drops a column that may carry an unnamed default: the default constraint first (SQL Server
        /// named it, or there is none on a model-built database), then the column.
        /// </summary>
        private static string DropColumnWithDefaultSql(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @default sysname;
    SELECT @default = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @default IS NOT NULL
    BEGIN
        DECLARE @drop nvarchar(400) = N'ALTER TABLE [dbo].[{table}] DROP CONSTRAINT ' + QUOTENAME(@default);
        EXEC sp_executesql @drop;
    END
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";
    }
}
