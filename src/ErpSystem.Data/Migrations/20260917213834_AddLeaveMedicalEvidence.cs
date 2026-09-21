using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Leave residue plan, slice G3 (R-15a): excuse duty and the medical board — typed leave
    /// evidence, and the two rules that can now be written against it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder create the
    /// schema from the EF model, so a rebuilt database already has these columns and a bare
    /// <c>AddColumn</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>Purely additive, and inert on every existing row.</b> The two rules only fire when
    /// <c>RequiresMedicalCertificate</c> is on, and it arrives <b>off</b> — so no leave type starts
    /// refusing a request that was fine yesterday, and nothing is back-filled.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ One of the scaffold's defaults was wrong.</b> EF emitted <c>defaultValue: 0</c> for
    /// <c>SelfCertificationDays</c>, while the entity's own default is <b>3</b>. Harmless today,
    /// because the flag that consults it is off — but the moment somebody switched medical
    /// certificates on for an existing leave type, they would silently have inherited <i>"a
    /// certificate is required for even a single day"</i> rather than the three days the entity, the
    /// form and the documentation all promise. A database default that contradicts the C# default is
    /// a disagreement waiting to surface, so this uses 3. Same class of correction as
    /// <c>20260917203359_AddLeaveEncashmentPolicySettings</c>, which had six of them.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ <c>MedicalBoardThresholdDays</c> is deliberately left NULL on existing rows</b>, although
    /// the entity defaults it to 90. This one is NOT an oversight: null means <i>"no board is ever
    /// required"</i>, and imposing a board threshold retroactively on leave types that already exist
    /// would be a policy decision nobody has taken. A type created from now on gets 90 as a starting
    /// suggestion; a type that predates this migration keeps no board until somebody sets one. Both
    /// read correctly on the form, which maps null to an empty field and says so.
    /// </para>
    ///
    /// <para>
    /// <c>EvidenceKind</c> defaults to <c>0</c> = <c>Other</c>, which is exactly what every
    /// attachment uploaded before this column existed genuinely was: an untyped supporting document.
    /// It must not default to anything else, or files nobody classified would start satisfying a
    /// medical-certificate requirement.
    /// </para>
    /// </remarks>
    public partial class AddLeaveMedicalEvidence : Migration
    {
        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string DropColumn(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @df_{table}_{column} sysname;
    SELECT @df_{table}_{column} = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @df_{table}_{column} IS NOT NULL
        EXEC('ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [' + @df_{table}_{column} + ']');
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";

        private const string Types = "LeaveTypes";
        private const string Attachments = "LeaveRequestAttachments";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Off, so this whole slice is inert until a client turns it on for a leave type.
            migrationBuilder.Sql(AddColumn(Types, "RequiresMedicalCertificate",
                "bit NOT NULL CONSTRAINT [DF_LeaveTypes_RequiresMedicalCertificate] DEFAULT 0"));

            // ⚠ 3, NOT the scaffold's 0 — see the remarks. 0 would mean "a certificate for one day".
            migrationBuilder.Sql(AddColumn(Types, "SelfCertificationDays",
                "int NOT NULL CONSTRAINT [DF_LeaveTypes_SelfCertificationDays] DEFAULT 3"));

            // ⚠ Nullable with NO default, deliberately: null means no board is ever required, and an
            // existing leave type should not acquire one because a migration ran.
            migrationBuilder.Sql(AddColumn(Types, "MedicalBoardThresholdDays", "int NULL"));

            // 0 = Other. Every attachment that predates this column was an untyped supporting
            // document, and it must stay one.
            migrationBuilder.Sql(AddColumn(Attachments, "EvidenceKind",
                "int NOT NULL CONSTRAINT [DF_LeaveRequestAttachments_EvidenceKind] DEFAULT 0"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropColumn(Types, "RequiresMedicalCertificate"));
            migrationBuilder.Sql(DropColumn(Types, "SelfCertificationDays"));
            migrationBuilder.Sql(DropColumn(Types, "MedicalBoardThresholdDays"));
            migrationBuilder.Sql(DropColumn(Attachments, "EvidenceKind"));
        }
    }
}
