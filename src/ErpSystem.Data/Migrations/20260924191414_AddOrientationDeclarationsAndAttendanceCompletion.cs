using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, lane R: orientation that can be completed (P2c's O-15). A programme's declaration now
    /// has words, every enrolment on it has one to sign, and a programme that is only its live session
    /// is completed by attending it.
    /// </summary>
    /// <remarks>
    /// <para><b>Five columns.</b></para>
    /// <list type="bullet">
    /// <item><c>OrientationPrograms.AcknowledgementTitle</c> (300) and <c>AcknowledgementText</c>
    /// (4000): the words each enrolment's declaration is copied from (decision R-D1).</item>
    /// <item><c>EmployeeOrientations.AttendanceConfirmedAt</c>, <c>AttendanceConfirmedByEmployeeId</c>
    /// and <c>AttendanceConfirmationNote</c> (1000). They are the content gate of a programme that is
    /// only its live session (R-D2, R-D3). The officer's id has no FK, like
    /// <c>EnrolledByEmployeeId</c>.</item>
    /// </list>
    /// <para><b>Two data steps.</b> Each is keyed on VALUES, never on a row id, and each does nothing
    /// on a second run.</para>
    /// <list type="bullet">
    /// <item>The seeded programmes ORI-ONB-001 and ORI-CMP-001 get their declaration's words, where
    /// they require one and have none. They are the same words <c>OrientationDataSeeder</c> writes
    /// on a rebuild.</item>
    /// <item>Every live enrolment on a programme that requires a declaration, and has none, gets one,
    /// Presented, in the programme's words or in the default <c>OrientationCompletionRules</c>
    /// builds. Kojo Ansah's is among them. "Live" leaves out the enrolments HR ended (cancelled,
    /// no-show, withdrawn) and those already completed or exempted. A dry run on
    /// <c>ErpSystemDB_UAT</c>, before this was written, counted 195 rows: 194 on ORI-ONB-001 and 1
    /// on ORI-CMP-001.</item>
    /// </list>
    /// <para>⚠ Guarded SQL throughout, as on every HR migration. <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has the columns, and its
    /// seeder writes the words and the declarations itself.</para>
    /// </remarks>
    public partial class AddOrientationDeclarationsAndAttendanceCompletion : Migration
    {
        private const string Marker = "migration:AddOrientationDeclarationsAndAttendanceCompletion";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.OrientationPrograms', 'AcknowledgementTitle') IS NULL
    ALTER TABLE [dbo].[OrientationPrograms] ADD [AcknowledgementTitle] nvarchar(300) NULL;
IF COL_LENGTH('dbo.OrientationPrograms', 'AcknowledgementText') IS NULL
    ALTER TABLE [dbo].[OrientationPrograms] ADD [AcknowledgementText] nvarchar(4000) NULL;
IF COL_LENGTH('dbo.EmployeeOrientations', 'AttendanceConfirmedAt') IS NULL
    ALTER TABLE [dbo].[EmployeeOrientations] ADD [AttendanceConfirmedAt] datetime2 NULL;
IF COL_LENGTH('dbo.EmployeeOrientations', 'AttendanceConfirmedByEmployeeId') IS NULL
    ALTER TABLE [dbo].[EmployeeOrientations] ADD [AttendanceConfirmedByEmployeeId] uniqueidentifier NULL;
IF COL_LENGTH('dbo.EmployeeOrientations', 'AttendanceConfirmationNote') IS NULL
    ALTER TABLE [dbo].[EmployeeOrientations] ADD [AttendanceConfirmationNote] nvarchar(1000) NULL;");

            // Separate batches: SQL Server compiles a batch before running it, so nothing could name
            // the columns in the batch that adds them.
            migrationBuilder.Sql(@"
UPDATE [dbo].[OrientationPrograms]
SET [AcknowledgementTitle] = COALESCE(NULLIF(LTRIM(RTRIM([AcknowledgementTitle])), N''), N'Code of Conduct Acknowledgement'),
    [AcknowledgementText] = N'I confirm that I have read, understood and agree to abide by the Company Code of Conduct.'
WHERE [ProgramCode] = N'ORI-ONB-001' AND [RequiresAcknowledgement] = 1 AND [IsDeleted] = 0
  AND ([AcknowledgementText] IS NULL OR LTRIM(RTRIM([AcknowledgementText])) = N'');

UPDATE [dbo].[OrientationPrograms]
SET [AcknowledgementTitle] = COALESCE(NULLIF(LTRIM(RTRIM([AcknowledgementTitle])), N''), N'Anti-Harassment Declaration'),
    [AcknowledgementText] = N'I confirm that I have completed the anti-harassment training, that I understand what harassment is and how to report it, and that I will uphold the Code of Conduct.'
WHERE [ProgramCode] = N'ORI-CMP-001' AND [RequiresAcknowledgement] = 1 AND [IsDeleted] = 0
  AND ([AcknowledgementText] IS NULL OR LTRIM(RTRIM([AcknowledgementText])) = N'');");

            // Every live enrolment on a programme that requires a declaration, and has none, gets one.
            // The fallback words are OrientationCompletionRules.DeclarationTitle and DeclarationText's.
            // Status 2 is Presented; enrolment statuses 6, 7, 8 are Cancelled, NoShow and Withdrawn;
            // completion statuses 5 and 8 are Completed and Exempted.
            migrationBuilder.Sql($@"
INSERT INTO [dbo].[OrientationAcknowledgements]
    ([Id], [TenantId], [EmployeeOrientationId], [Title], [AcknowledgementText], [Status],
     [PresentedAt], [CreatedAt], [CreatedBy], [IsDeleted])
SELECT NEWID(), e.[TenantId], e.[Id],
       LEFT(COALESCE(NULLIF(LTRIM(RTRIM(p.[AcknowledgementTitle])), N''), p.[Title] + N' — declaration'), 300),
       COALESCE(NULLIF(LTRIM(RTRIM(p.[AcknowledgementText])), N''),
                N'I confirm that I have completed ' + p.[Title] + N', that I understand what it sets out, and that I will abide by it.'),
       2, SYSUTCDATETIME(), SYSUTCDATETIME(), N'{Marker}', 0
FROM [dbo].[EmployeeOrientations] e
JOIN [dbo].[OrientationPrograms] p ON p.[Id] = e.[ProgramId]
WHERE e.[IsDeleted] = 0 AND p.[IsDeleted] = 0 AND p.[RequiresAcknowledgement] = 1
  AND e.[EnrollmentStatus] NOT IN (6, 7, 8)
  AND e.[CompletionStatus] NOT IN (5, 8)
  AND NOT EXISTS (SELECT 1 FROM [dbo].[OrientationAcknowledgements] a
                  WHERE a.[EmployeeOrientationId] = e.[Id] AND a.[IsDeleted] = 0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The declarations this migration made, where nobody has signed or declined them yet. A
            // declaration made later by an enrolment keeps its own copy of the words, so it stays.
            migrationBuilder.Sql($@"
DELETE FROM [dbo].[OrientationAcknowledgements]
WHERE [CreatedBy] = N'{Marker}' AND [SignedAt] IS NULL AND [DeclinedAt] IS NULL;");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.EmployeeOrientations', 'AttendanceConfirmationNote') IS NOT NULL
    ALTER TABLE [dbo].[EmployeeOrientations] DROP COLUMN [AttendanceConfirmationNote];
IF COL_LENGTH('dbo.EmployeeOrientations', 'AttendanceConfirmedByEmployeeId') IS NOT NULL
    ALTER TABLE [dbo].[EmployeeOrientations] DROP COLUMN [AttendanceConfirmedByEmployeeId];
IF COL_LENGTH('dbo.EmployeeOrientations', 'AttendanceConfirmedAt') IS NOT NULL
    ALTER TABLE [dbo].[EmployeeOrientations] DROP COLUMN [AttendanceConfirmedAt];
IF COL_LENGTH('dbo.OrientationPrograms', 'AcknowledgementText') IS NOT NULL
    ALTER TABLE [dbo].[OrientationPrograms] DROP COLUMN [AcknowledgementText];
IF COL_LENGTH('dbo.OrientationPrograms', 'AcknowledgementTitle') IS NOT NULL
    ALTER TABLE [dbo].[OrientationPrograms] DROP COLUMN [AcknowledgementTitle];");
        }
    }
}
