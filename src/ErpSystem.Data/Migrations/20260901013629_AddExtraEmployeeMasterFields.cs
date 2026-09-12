using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// The Employee Master feedback block: fields the record could not hold at all (finish plan,
    /// lane 3a) — an employee's own disability, hometown and gender description; a guarantor's
    /// surety and its currency; a referee's written reference; the residence permit and permit
    /// ISSUE dates on an expatriate posting; who actually accompanied that posting; and photographs
    /// on the employee, the dependant and the guarantor, through the controlled upload gate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ Disability is DUPLICATED with <c>EmployeeDependents</c>, not moved from it.</b> The
    /// feedback recorded these fields as sitting "on EmployeeDependent, not Employee", which reads
    /// as a misplacement and is not one: a dependant's disability and an employee's own are
    /// different facts about different people, and both are needed. The dependant's columns are
    /// untouched. Decided by the user, 2026-09-01.
    /// </para>
    /// <para>
    /// <b>⚠ The photographs are the seventh instance of the caller-supplied path sink.</b>
    /// <c>Employees.PicturePath</c> and <c>EmployeeDependents.PicturePath</c> are on the create AND
    /// update DTOs, mapped straight onto the entity, and no photo upload endpoint has ever existed —
    /// so the only way to set an employee photo was to type a location. Both legacy columns SURVIVE
    /// on purpose: they hold ported values and the download helper falls back to them, so dropping
    /// them here would lose every image the port brought over. Nothing new writes them.
    /// </para>
    /// <para>
    /// <b>⚠ <c>Employees.HasDisability</c> is the only NOT NULL column here, and <c>false</c> is the
    /// right value for every existing row</b> — "not recorded as having a disability" is what an
    /// unset flag has always meant. Contrast the previous migration, where the scaffold's
    /// <c>defaultValue: 0</c> would have meant a zero-hour deadline and a divide-by-zero.
    /// </para>
    /// <para>
    /// <b>The scaffolded body was replaced with guarded SQL</b>, as in
    /// <c>20260901001749_AddEmployeeDocuments</c> and its four siblings: <c>rebuild-db</c> builds
    /// from the EF model, so a rebuilt database already has all thirty-nine columns and the new
    /// table, and a bare <c>AddColumn</c> or <c>CreateTable</c> fails. Every step is a no-op against
    /// a database already in the target shape.
    /// </para>
    /// <para>
    /// <b>No data operation</b> — see the previous migration for why one would break in Debug.
    /// </para>
    /// </remarks>
    public partial class AddExtraEmployeeMasterFields : Migration
    {
        /// <summary>Column, table, SQL type — every one nullable except where a default is given.</summary>
        private static readonly (string Table, string Column, string Type, string Default)[] Columns =
        {
            // The employee's own record.
            ("Employees", "GenderDescription", "nvarchar(100)", null),
            ("Employees", "Hometown", "nvarchar(150)", null),
            // NOT NULL: an unrecorded disability is "no", which is what every existing row means.
            ("Employees", "HasDisability", "bit", "0"),
            ("Employees", "DisabilityDescription", "nvarchar(500)", null),
            ("Employees", "PhotoFileUploadRecordId", "uniqueidentifier", null),
            ("Employees", "PhotoDocumentRecordId", "uniqueidentifier", null),
            ("Employees", "PhotoDocumentVersionId", "uniqueidentifier", null),
            ("Employees", "PhotoFileName", "nvarchar(255)", null),
            ("Employees", "PhotoMimeType", "nvarchar(150)", null),
            ("Employees", "PhotoFileSizeBytes", "bigint", null),

            // Dependants — the gender description and a gated photograph.
            ("EmployeeDependents", "GenderDescription", "nvarchar(100)", null),
            ("EmployeeDependents", "PhotoFileUploadRecordId", "uniqueidentifier", null),
            ("EmployeeDependents", "PhotoDocumentRecordId", "uniqueidentifier", null),
            ("EmployeeDependents", "PhotoDocumentVersionId", "uniqueidentifier", null),
            ("EmployeeDependents", "PhotoFileName", "nvarchar(255)", null),
            ("EmployeeDependents", "PhotoMimeType", "nvarchar(150)", null),
            ("EmployeeDependents", "PhotoFileSizeBytes", "bigint", null),

            // Guarantors — what they stand surety for, in which currency, and their photograph.
            // ⚠ AmountGuaranteed is distinct from MonthlyIncome: the row could say how solvent the
            // guarantor was and never what they had undertaken.
            ("EmployeeGuarantors", "AmountGuaranteed", "decimal(18,2)", null),
            ("EmployeeGuarantors", "AmountGuaranteedCurrencyCode", "nvarchar(3)", null),
            ("EmployeeGuarantors", "GenderDescription", "nvarchar(100)", null),
            ("EmployeeGuarantors", "PhotoFileUploadRecordId", "uniqueidentifier", null),
            ("EmployeeGuarantors", "PhotoDocumentRecordId", "uniqueidentifier", null),
            ("EmployeeGuarantors", "PhotoDocumentVersionId", "uniqueidentifier", null),
            ("EmployeeGuarantors", "PhotoFileName", "nvarchar(255)", null),
            ("EmployeeGuarantors", "PhotoMimeType", "nvarchar(150)", null),
            ("EmployeeGuarantors", "PhotoFileSizeBytes", "bigint", null),

            // Referees — the letter they actually wrote, which had nowhere to live.
            ("EmployeeReferees", "LetterFileUploadRecordId", "uniqueidentifier", null),
            ("EmployeeReferees", "LetterDocumentRecordId", "uniqueidentifier", null),
            ("EmployeeReferees", "LetterDocumentVersionId", "uniqueidentifier", null),
            ("EmployeeReferees", "LetterFileName", "nvarchar(255)", null),
            ("EmployeeReferees", "LetterMimeType", "nvarchar(150)", null),
            ("EmployeeReferees", "LetterFileSizeBytes", "bigint", null),

            // The post's guarantor requirement. ⚠ RequiresGuarantor was a bare bool: a post could
            // demand a guarantor and never say for how much.
            ("EmployeePositions", "RequiredGuarantorAmount", "decimal(18,2)", null),
            ("EmployeePositions", "RequiredGuarantorCurrencyCode", "nvarchar(3)", null),

            // The expatriate posting. ⚠ Every permit carried an expiry and no ISSUE date, so the
            // record could never say how long something was granted for; and the residence permit
            // is a different instrument from the work permit, on a different authority's clock.
            ("ExpatriateAssignments", "VisaIssueDate", "date", null),
            ("ExpatriateAssignments", "WorkPermitIssueDate", "date", null),
            ("ExpatriateAssignments", "ResidentPermitNumber", "nvarchar(100)", null),
            ("ExpatriateAssignments", "ResidentPermitIssueDate", "date", null),
            ("ExpatriateAssignments", "ResidentPermitExpiryDate", "date", null),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column, type, @default) in Columns)
            {
                var nullability = @default is null
                    ? "NULL"
                    : $"NOT NULL CONSTRAINT [DF_{table}_{column}] DEFAULT ({@default})";

                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {type} {nullability};");
            }

            // ⚠ FamilyAccompanying was a bare bool: the posting could assert a family had come and
            // never say who, so nobody could count the residence permits owed or see whose lapsed
            // next. Each accompanying person carries their own permit on their own clock.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.ExpatriateFamilyMembers', 'U') IS NULL
CREATE TABLE [dbo].[ExpatriateFamilyMembers] (
    [Id]                       uniqueidentifier NOT NULL,
    [ExpatriateAssignmentId]   uniqueidentifier NOT NULL,
    [FullName]                 nvarchar(150)    NOT NULL,
    -- The SAME enum EmployeeDependent uses, with the same description-for-Other companion: one
    -- kinship vocabulary, and a value the permit count can group by rather than prose.
    [Relationship]             int              NOT NULL,
    [RelationshipDescription]  nvarchar(100)    NULL,
    [Gender]                   int              NULL,
    [GenderDescription]        nvarchar(100)    NULL,
    [DateOfBirth]              date             NULL,
    [PassportNumber]           nvarchar(100)    NULL,
    [PassportExpiryDate]       date             NULL,
    -- Their own residence permit, separate from the assignee's.
    [ResidentPermitNumber]     nvarchar(100)    NULL,
    [ResidentPermitIssueDate]  date             NULL,
    [ResidentPermitExpiryDate] date             NULL,
    [ArrivalDate]              date             NULL,
    -- Set when they leave ahead of the assignee, so the row stays true without being deleted.
    [DepartureDate]            date             NULL,
    [Notes]                    nvarchar(500)    NULL,
    [CreatedAt]                datetime2        NOT NULL,
    [UpdatedAt]                datetime2        NULL,
    [CreatedBy]                nvarchar(max)    NULL,
    [UpdatedBy]                nvarchar(max)    NULL,
    [CreatedById]              uniqueidentifier NULL,
    [LastModifiedById]         uniqueidentifier NULL,
    [IsDeleted]                bit              NOT NULL,
    [DeletedAt]                datetime2        NULL,
    [DeletedBy]                nvarchar(max)    NULL,
    [TenantId]                 uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ExpatriateFamilyMembers] PRIMARY KEY ([Id]),
    -- Cascade, unlike most HR children: a family member exists only as part of a posting, and the
    -- posting already cascades from the employee.
    CONSTRAINT [FK_ExpatriateFamilyMembers_ExpatriateAssignments_ExpatriateAssignmentId]
        FOREIGN KEY ([ExpatriateAssignmentId])
        REFERENCES [dbo].[ExpatriateAssignments] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ExpatriateFamilyMembers_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            foreach (var (name, table, columns) in new[]
            {
                ("IX_ExpatriateFamilyMembers_ExpatriateAssignmentId", "ExpatriateFamilyMembers",
                    "[ExpatriateAssignmentId]"),
                ("IX_ExpatriateFamilyMembers_TenantId_ExpatriateAssignmentId", "ExpatriateFamilyMembers",
                    "[TenantId], [ExpatriateAssignmentId]"),
                // Ahead of the permit-expiry sweep that is owed: the only cross-posting question
                // anyone asks of this table is "whose permit lapses next".
                ("IX_ExpatriateFamilyMembers_TenantId_ResidentPermitExpiryDate", "ExpatriateFamilyMembers",
                    "[TenantId], [ResidentPermitExpiryDate]"),
            })
            {
                migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{name}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{name}] ON [dbo].[{table}] ({columns});");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.ExpatriateFamilyMembers', 'U') IS NOT NULL
    DROP TABLE [dbo].[ExpatriateFamilyMembers];");

            foreach (var (table, column, _, @default) in Columns)
            {
                // ⚠ A default constraint must go before its column, and on a model-built database
                // its name is server-generated — so it is looked up rather than guessed.
                var dropDefault = @default is null ? string.Empty : $@"
    DECLARE @c_{column} sysname;
    SELECT @c_{column} = dc.name
      FROM sys.default_constraints dc
      JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
     WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @c_{column} IS NOT NULL
        EXEC('ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [' + @c_{column} + ']');
";

                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN{dropDefault}
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END;");
            }
        }
    }
}
