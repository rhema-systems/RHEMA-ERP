using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 5, lane K (decision A6), the medical board's step one: what a board is for, who stopped
    /// it and when, and its papers.
    /// </summary>
    /// <remarks>
    /// <para><b>Three columns on <c>MedicalBoards</c>:</b></para>
    /// <list type="bullet">
    ///   <item><c>Purpose</c> (int, NOT NULL): the question the board is asked (K1). ⚠ Every existing
    ///   board becomes <b>5 = Other</b>, never the scaffold's 0, which is no purpose at all. Other is
    ///   also the honest answer — nobody recorded the question — and it keeps every existing board
    ///   exactly as able to satisfy the leave evidence gate as it was (K6 accepts Other).</item>
    ///   <item><c>CancelledOn</c> (date, nullable) and <c>CancelledById</c> (uniqueidentifier,
    ///   nullable): when a board was stopped and by whom (K5). Existing cancelled boards take
    ///   <c>CancelledOn</c> from <c>UpdatedAt</c>: every write refuses a cancelled board, so its last
    ///   update IS the cancellation. Who did it was never recorded (<c>LastModifiedById</c> is empty),
    ///   so <c>CancelledById</c> stays null on them and the page says only when.</item>
    /// </list>
    ///
    /// <para><b>One table, <c>MedicalBoardDocuments</c></b> (K4): a board's papers, uploaded through
    /// the controlled gate. It cascades from the board, like its members and sittings. ⚠ The uploader
    /// is <b>Restrict</b>: the board already cascades from <c>Employees</c>, and a second cascading
    /// path from <c>Employees</c> into this table is one SQL Server refuses.</para>
    ///
    /// <para>⚠ Guarded SQL, as on every HR migration: <c>rebuild-db</c> and the UAT builder create the
    /// schema from the EF model, so a rebuilt database already has all of this. The backfill and the
    /// indexes are in their own batches, because SQL Server compiles a batch before running it and
    /// could not name a column the same batch adds.</para>
    ///
    /// <para>Proven on a scratch database before it was applied: Up twice, Down twice, Up again, over
    /// existing boards in every status.</para>
    /// </remarks>
    public partial class AddMedicalBoardPurposeAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.MedicalBoards', 'Purpose') IS NULL
    ALTER TABLE [dbo].[MedicalBoards] ADD [Purpose] int NOT NULL DEFAULT (5);
IF COL_LENGTH('dbo.MedicalBoards', 'CancelledOn') IS NULL
    ALTER TABLE [dbo].[MedicalBoards] ADD [CancelledOn] date NULL;
IF COL_LENGTH('dbo.MedicalBoards', 'CancelledById') IS NULL
    ALTER TABLE [dbo].[MedicalBoards] ADD [CancelledById] uniqueidentifier NULL;");

            // Boards cancelled before K5: the date they were stopped, from their last update. Only rows
            // that have none yet, so a second run changes nothing.
            migrationBuilder.Sql(@"
UPDATE [dbo].[MedicalBoards]
   SET [CancelledOn] = CAST([UpdatedAt] AS date)
 WHERE [Status] = 4
   AND [CancelledOn] IS NULL
   AND [UpdatedAt] IS NOT NULL;");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.MedicalBoardDocuments', 'U') IS NULL
CREATE TABLE [dbo].[MedicalBoardDocuments] (
    [Id]                 uniqueidentifier NOT NULL,
    [BoardId]            uniqueidentifier NOT NULL,
    [FileName]           nvarchar(255)    NOT NULL,
    [FileSize]           bigint           NULL,
    [Description]        nvarchar(500)    NULL,
    [UploadDate]         datetime2        NOT NULL,
    [UploadedById]       uniqueidentifier NOT NULL,
    [FileUploadRecordId] uniqueidentifier NULL,
    [DocumentRecordId]   uniqueidentifier NULL,
    [DocumentVersionId]  uniqueidentifier NULL,
    [CreatedAt]          datetime2        NOT NULL,
    [UpdatedAt]          datetime2        NULL,
    [CreatedBy]          nvarchar(max)    NULL,
    [UpdatedBy]          nvarchar(max)    NULL,
    [CreatedById]        uniqueidentifier NULL,
    [LastModifiedById]   uniqueidentifier NULL,
    [IsDeleted]          bit              NOT NULL,
    [DeletedAt]          datetime2        NULL,
    [DeletedBy]          nvarchar(max)    NULL,
    [TenantId]           uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MedicalBoardDocuments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MedicalBoardDocuments_Employees_UploadedById] FOREIGN KEY ([UploadedById])
        REFERENCES [dbo].[Employees] ([Id]),
    CONSTRAINT [FK_MedicalBoardDocuments_MedicalBoards_BoardId] FOREIGN KEY ([BoardId])
        REFERENCES [dbo].[MedicalBoards] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_MedicalBoardDocuments_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MedicalBoardDocuments_BoardId'
                 AND object_id = OBJECT_ID('dbo.MedicalBoardDocuments'))
    CREATE INDEX [IX_MedicalBoardDocuments_BoardId] ON [dbo].[MedicalBoardDocuments] ([BoardId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MedicalBoardDocuments_TenantId'
                 AND object_id = OBJECT_ID('dbo.MedicalBoardDocuments'))
    CREATE INDEX [IX_MedicalBoardDocuments_TenantId] ON [dbo].[MedicalBoardDocuments] ([TenantId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MedicalBoardDocuments_UploadedById'
                 AND object_id = OBJECT_ID('dbo.MedicalBoardDocuments'))
    CREATE INDEX [IX_MedicalBoardDocuments_UploadedById] ON [dbo].[MedicalBoardDocuments] ([UploadedById]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ The papers' rows go with the table; the scanned files and their DMS records stay, as
            // they do when any HR attachment row is removed.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.MedicalBoardDocuments', 'U') IS NOT NULL
    DROP TABLE [dbo].[MedicalBoardDocuments];");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.MedicalBoards', 'CancelledById') IS NOT NULL
    ALTER TABLE [dbo].[MedicalBoards] DROP COLUMN [CancelledById];
IF COL_LENGTH('dbo.MedicalBoards', 'CancelledOn') IS NOT NULL
    ALTER TABLE [dbo].[MedicalBoards] DROP COLUMN [CancelledOn];");

            migrationBuilder.Sql(DropColumnWithDefaultSql("MedicalBoards", "Purpose"));
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
