using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, lane E — the recruitment test and aptitude exam engine (E1).
    /// </summary>
    /// <remarks>
    /// <para>Seven tables: the paper (<c>RecruitmentTests</c>, its sections, questions and options),
    /// who has to sit it (<c>RecruitmentTestAssignments</c>) and what happened when they did
    /// (<c>RecruitmentTestSittings</c>, <c>RecruitmentTestAnswers</c>).</para>
    ///
    /// <para><b>Nothing existing is touched.</b> <c>JobApplicantTestResults</c> — the result ledger
    /// that was all there was before this lane — is KEPT and written to when a sitting is finalised.
    /// An offline test is real, and the ledger is where it lives; the engine becomes one way of
    /// producing a row rather than the only one.</para>
    ///
    /// <para><b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration
    /// since <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has all seven tables and a
    /// bare <c>CreateTable</c> stops the chain. <b>The column list, the types, the foreign keys and
    /// the indexes below were read out of the scaffold, not retyped</b> — transcribing 490 lines by
    /// hand is how a type drifts from the model and a rebuilt database stops matching a migrated one.
    /// Same approach as <c>20260920154018_AddHrFinancePosting</c>.</para>
    ///
    /// <para><b>No default-value trap here, and worth saying why not.</b> The recurring fault this
    /// round keeps recording — EF scaffolding <c>defaultValue: 0</c> for a column whose real default
    /// is not zero — cannot arise on a NEW table: there are no existing rows to be wrong, and EF
    /// emits no defaults at all. <c>MaxAttempts</c> (1) and <c>Points</c> (1) are C# initialisers and
    /// apply to every row this code inserts. ⚠ A hand-written <c>INSERT</c> that omits them would get
    /// SQL's 0, and a <c>MaxAttempts</c> of 0 means nobody can sit anything — so a seeder must set
    /// them explicitly.</para>
    ///
    /// <para>⚠ <b>Delete behaviour is deliberately asymmetric.</b> Sections, questions and options
    /// CASCADE from the paper — they have no meaning without it. Everything on the SITTING side is
    /// Restrict: deleting a test must never silently delete somebody's marked answers, which are the
    /// evidence behind a shortlisting score.</para>
    /// </remarks>
    public partial class AddRecruitmentTestEngine : Migration
    {
        private static string CreateIndex(string table, string index, string columns, bool unique = false) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE {(unique ? "UNIQUE " : string.Empty)}INDEX [{index}] ON [dbo].[{table}] ({columns});";

        private static string DropTable(string table) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
    DROP TABLE [dbo].[{table}];";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.RecruitmentTests', 'U') IS NULL
CREATE TABLE [dbo].[RecruitmentTests] (
    [Id]                uniqueidentifier  NOT NULL,
    [TestCode]          nvarchar(50)      NOT NULL,
    [Name]              nvarchar(200)     NOT NULL,
    [Description]       nvarchar(2000)    NULL,
    [Instructions]      nvarchar(4000)    NULL,
    [TestType]          int               NOT NULL,
    [DurationMinutes]   int               NULL,
    [PassMarkPercent]   decimal(18,4)     NULL,
    [MaxAttempts]       int               NOT NULL,
    [ShuffleQuestions]  bit               NOT NULL,
    [ShuffleOptions]    bit               NOT NULL,
    [IsActive]          bit               NOT NULL,
    [CreatedAt]         datetime2         NOT NULL,
    [UpdatedAt]         datetime2         NULL,
    [CreatedBy]         nvarchar(max)     NULL,
    [UpdatedBy]         nvarchar(max)     NULL,
    [CreatedById]       uniqueidentifier  NULL,
    [LastModifiedById]  uniqueidentifier  NULL,
    [IsDeleted]         bit               NOT NULL,
    [DeletedAt]         datetime2         NULL,
    [DeletedBy]         nvarchar(max)     NULL,
    [TenantId]          uniqueidentifier  NOT NULL,
    CONSTRAINT [PK_RecruitmentTests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecruitmentTests_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");
            migrationBuilder.Sql(CreateIndex("RecruitmentTests", "IX_RecruitmentTest_IsActive", "[IsActive]"));
            migrationBuilder.Sql(CreateIndex("RecruitmentTests", "IX_RecruitmentTest_Tenant_Code", "[TenantId], [TestCode]", unique: true));

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.RecruitmentTestAssignments', 'U') IS NULL
CREATE TABLE [dbo].[RecruitmentTestAssignments] (
    [Id]                    uniqueidentifier  NOT NULL,
    [RecruitmentTestId]     uniqueidentifier  NOT NULL,
    [JobVacancyId]          uniqueidentifier  NULL,
    [JobApplicationId]      uniqueidentifier  NULL,
    [OpensAt]               datetime2         NULL,
    [ClosesAt]              datetime2         NULL,
    [IsRequired]            bit               NOT NULL,
    [ExtraAttemptsGranted]  int               NOT NULL,
    [ExtraAttemptReason]    nvarchar(1000)    NULL,
    [InvitedAt]             datetime2         NULL,
    [CreatedAt]             datetime2         NOT NULL,
    [UpdatedAt]             datetime2         NULL,
    [CreatedBy]             nvarchar(max)     NULL,
    [UpdatedBy]             nvarchar(max)     NULL,
    [CreatedById]           uniqueidentifier  NULL,
    [LastModifiedById]      uniqueidentifier  NULL,
    [IsDeleted]             bit               NOT NULL,
    [DeletedAt]             datetime2         NULL,
    [DeletedBy]             nvarchar(max)     NULL,
    [TenantId]              uniqueidentifier  NOT NULL,
    CONSTRAINT [PK_RecruitmentTestAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecruitmentTestAssignments_JobApplications_JobApplicationId] FOREIGN KEY ([JobApplicationId])
        REFERENCES [dbo].[JobApplications] ([Id]),
    CONSTRAINT [FK_RecruitmentTestAssignments_JobVacancies_JobVacancyId] FOREIGN KEY ([JobVacancyId])
        REFERENCES [dbo].[JobVacancies] ([Id]),
    CONSTRAINT [FK_RecruitmentTestAssignments_RecruitmentTests_RecruitmentTestId] FOREIGN KEY ([RecruitmentTestId])
        REFERENCES [dbo].[RecruitmentTests] ([Id]),
    CONSTRAINT [FK_RecruitmentTestAssignments_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");
            migrationBuilder.Sql(CreateIndex("RecruitmentTestAssignments", "IX_RecruitmentTestAssignment_Application", "[JobApplicationId]"));
            migrationBuilder.Sql(CreateIndex("RecruitmentTestAssignments", "IX_RecruitmentTestAssignment_Test", "[RecruitmentTestId]"));
            migrationBuilder.Sql(CreateIndex("RecruitmentTestAssignments", "IX_RecruitmentTestAssignment_Vacancy", "[JobVacancyId]"));
            migrationBuilder.Sql(CreateIndex("RecruitmentTestAssignments", "IX_RecruitmentTestAssignments_TenantId", "[TenantId]"));

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.RecruitmentTestSections', 'U') IS NULL
CREATE TABLE [dbo].[RecruitmentTestSections] (
    [Id]                 uniqueidentifier  NOT NULL,
    [RecruitmentTestId]  uniqueidentifier  NOT NULL,
    [Name]               nvarchar(200)     NOT NULL,
    [Description]        nvarchar(1000)    NULL,
    [DisplayOrder]       int               NOT NULL,
    [CreatedAt]          datetime2         NOT NULL,
    [UpdatedAt]          datetime2         NULL,
    [CreatedBy]          nvarchar(max)     NULL,
    [UpdatedBy]          nvarchar(max)     NULL,
    [CreatedById]        uniqueidentifier  NULL,
    [LastModifiedById]   uniqueidentifier  NULL,
    [IsDeleted]          bit               NOT NULL,
    [DeletedAt]          datetime2         NULL,
    [DeletedBy]          nvarchar(max)     NULL,
    [TenantId]           uniqueidentifier  NOT NULL,
    CONSTRAINT [PK_RecruitmentTestSections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecruitmentTestSections_RecruitmentTests_RecruitmentTestId] FOREIGN KEY ([RecruitmentTestId])
        REFERENCES [dbo].[RecruitmentTests] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_RecruitmentTestSections_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");
            migrationBuilder.Sql(CreateIndex("RecruitmentTestSections", "IX_RecruitmentTestSection_Test", "[RecruitmentTestId]"));
            migrationBuilder.Sql(CreateIndex("RecruitmentTestSections", "IX_RecruitmentTestSections_TenantId", "[TenantId]"));

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.RecruitmentTestSittings', 'U') IS NULL
CREATE TABLE [dbo].[RecruitmentTestSittings] (
    [Id]                           uniqueidentifier  NOT NULL,
    [RecruitmentTestAssignmentId]  uniqueidentifier  NOT NULL,
    [JobApplicationId]             uniqueidentifier  NOT NULL,
    [AttemptNumber]                int               NOT NULL,
    [AccessTokenHash]              nvarchar(200)     NULL,
    [AccessTokenLast4]             nvarchar(8)       NULL,
    [AccessTokenExpiresAt]         datetime2         NULL,
    [StartedAt]                    datetime2         NULL,
    [SubmittedAt]                  datetime2         NULL,
    [MustSubmitBy]                 datetime2         NULL,
    [Status]                       int               NOT NULL,
    [AutoScore]                    decimal(18,4)     NULL,
    [ManualScore]                  decimal(18,4)     NULL,
    [FinalScore]                   decimal(18,4)     NULL,
    [TotalPoints]                  decimal(18,2)     NULL,
    [ScorePercent]                 decimal(18,4)     NULL,
    [Passed]                       bit               NULL,
    [MarkedById]                   uniqueidentifier  NULL,
    [MarkedAt]                     datetime2         NULL,
    [MarkerNotes]                  nvarchar(2000)    NULL,
    [JobApplicantTestResultId]     uniqueidentifier  NULL,
    [CreatedAt]                    datetime2         NOT NULL,
    [UpdatedAt]                    datetime2         NULL,
    [CreatedBy]                    nvarchar(max)     NULL,
    [UpdatedBy]                    nvarchar(max)     NULL,
    [CreatedById]                  uniqueidentifier  NULL,
    [LastModifiedById]             uniqueidentifier  NULL,
    [IsDeleted]                    bit               NOT NULL,
    [DeletedAt]                    datetime2         NULL,
    [DeletedBy]                    nvarchar(max)     NULL,
    [TenantId]                     uniqueidentifier  NOT NULL,
    CONSTRAINT [PK_RecruitmentTestSittings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecruitmentTestSittings_JobApplicantTestResults_JobApplicantTestResultId] FOREIGN KEY ([JobApplicantTestResultId])
        REFERENCES [dbo].[JobApplicantTestResults] ([Id]),
    CONSTRAINT [FK_RecruitmentTestSittings_JobApplications_JobApplicationId] FOREIGN KEY ([JobApplicationId])
        REFERENCES [dbo].[JobApplications] ([Id]),
    CONSTRAINT [FK_RecruitmentTestSittings_RecruitmentTestAssignments_RecruitmentTestAssignmentId] FOREIGN KEY ([RecruitmentTestAssignmentId])
        REFERENCES [dbo].[RecruitmentTestAssignments] ([Id]),
    CONSTRAINT [FK_RecruitmentTestSittings_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");
            migrationBuilder.Sql(CreateIndex("RecruitmentTestSittings", "IX_RecruitmentTestSitting_Application", "[JobApplicationId]"));
            migrationBuilder.Sql(CreateIndex("RecruitmentTestSittings", "IX_RecruitmentTestSitting_Assignment", "[RecruitmentTestAssignmentId]"));
            migrationBuilder.Sql(CreateIndex("RecruitmentTestSittings", "IX_RecruitmentTestSitting_Assignment_Application_Attempt", "[RecruitmentTestAssignmentId], [JobApplicationId], [AttemptNumber]", unique: true));
            migrationBuilder.Sql(CreateIndex("RecruitmentTestSittings", "IX_RecruitmentTestSitting_Status", "[Status]"));
            migrationBuilder.Sql(CreateIndex("RecruitmentTestSittings", "IX_RecruitmentTestSittings_JobApplicantTestResultId", "[JobApplicantTestResultId]"));
            migrationBuilder.Sql(CreateIndex("RecruitmentTestSittings", "IX_RecruitmentTestSittings_TenantId", "[TenantId]"));

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.RecruitmentTestQuestions', 'U') IS NULL
CREATE TABLE [dbo].[RecruitmentTestQuestions] (
    [Id]                        uniqueidentifier  NOT NULL,
    [RecruitmentTestId]         uniqueidentifier  NOT NULL,
    [RecruitmentTestSectionId]  uniqueidentifier  NULL,
    [QuestionText]              nvarchar(2000)    NOT NULL,
    [QuestionType]              int               NOT NULL,
    [Points]                    decimal(18,4)     NOT NULL,
    [ExpectedAnswer]            nvarchar(500)     NULL,
    [Explanation]               nvarchar(2000)    NULL,
    [DisplayOrder]              int               NOT NULL,
    [CreatedAt]                 datetime2         NOT NULL,
    [UpdatedAt]                 datetime2         NULL,
    [CreatedBy]                 nvarchar(max)     NULL,
    [UpdatedBy]                 nvarchar(max)     NULL,
    [CreatedById]               uniqueidentifier  NULL,
    [LastModifiedById]          uniqueidentifier  NULL,
    [IsDeleted]                 bit               NOT NULL,
    [DeletedAt]                 datetime2         NULL,
    [DeletedBy]                 nvarchar(max)     NULL,
    [TenantId]                  uniqueidentifier  NOT NULL,
    CONSTRAINT [PK_RecruitmentTestQuestions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecruitmentTestQuestions_RecruitmentTestSections_RecruitmentTestSectionId] FOREIGN KEY ([RecruitmentTestSectionId])
        REFERENCES [dbo].[RecruitmentTestSections] ([Id]),
    CONSTRAINT [FK_RecruitmentTestQuestions_RecruitmentTests_RecruitmentTestId] FOREIGN KEY ([RecruitmentTestId])
        REFERENCES [dbo].[RecruitmentTests] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_RecruitmentTestQuestions_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");
            migrationBuilder.Sql(CreateIndex("RecruitmentTestQuestions", "IX_RecruitmentTestQuestion_Section", "[RecruitmentTestSectionId]"));
            migrationBuilder.Sql(CreateIndex("RecruitmentTestQuestions", "IX_RecruitmentTestQuestion_Test", "[RecruitmentTestId]"));
            migrationBuilder.Sql(CreateIndex("RecruitmentTestQuestions", "IX_RecruitmentTestQuestions_TenantId", "[TenantId]"));

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.RecruitmentTestQuestionOptions', 'U') IS NULL
CREATE TABLE [dbo].[RecruitmentTestQuestionOptions] (
    [Id]                         uniqueidentifier  NOT NULL,
    [RecruitmentTestQuestionId]  uniqueidentifier  NOT NULL,
    [OptionText]                 nvarchar(1000)    NOT NULL,
    [IsCorrect]                  bit               NOT NULL,
    [DisplayOrder]               int               NOT NULL,
    [CreatedAt]                  datetime2         NOT NULL,
    [UpdatedAt]                  datetime2         NULL,
    [CreatedBy]                  nvarchar(max)     NULL,
    [UpdatedBy]                  nvarchar(max)     NULL,
    [CreatedById]                uniqueidentifier  NULL,
    [LastModifiedById]           uniqueidentifier  NULL,
    [IsDeleted]                  bit               NOT NULL,
    [DeletedAt]                  datetime2         NULL,
    [DeletedBy]                  nvarchar(max)     NULL,
    [TenantId]                   uniqueidentifier  NOT NULL,
    CONSTRAINT [PK_RecruitmentTestQuestionOptions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecruitmentTestQuestionOptions_RecruitmentTestQuestions_RecruitmentTestQuestionId] FOREIGN KEY ([RecruitmentTestQuestionId])
        REFERENCES [dbo].[RecruitmentTestQuestions] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_RecruitmentTestQuestionOptions_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");
            migrationBuilder.Sql(CreateIndex("RecruitmentTestQuestionOptions", "IX_RecruitmentTestOption_Question", "[RecruitmentTestQuestionId]"));
            migrationBuilder.Sql(CreateIndex("RecruitmentTestQuestionOptions", "IX_RecruitmentTestQuestionOptions_TenantId", "[TenantId]"));

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.RecruitmentTestAnswers', 'U') IS NULL
CREATE TABLE [dbo].[RecruitmentTestAnswers] (
    [Id]                         uniqueidentifier  NOT NULL,
    [RecruitmentTestSittingId]   uniqueidentifier  NOT NULL,
    [RecruitmentTestQuestionId]  uniqueidentifier  NOT NULL,
    [SelectedOptionId]           uniqueidentifier  NULL,
    [FreeTextAnswer]             nvarchar(4000)    NULL,
    [NumericAnswer]              nvarchar(100)     NULL,
    [IsCorrect]                  bit               NOT NULL,
    [PointsAwarded]              decimal(18,4)     NOT NULL,
    [AnsweredAt]                 datetime2         NOT NULL,
    [IsManuallyMarked]           bit               NOT NULL,
    [MarkerComment]              nvarchar(1000)    NULL,
    [CreatedAt]                  datetime2         NOT NULL,
    [UpdatedAt]                  datetime2         NULL,
    [CreatedBy]                  nvarchar(max)     NULL,
    [UpdatedBy]                  nvarchar(max)     NULL,
    [CreatedById]                uniqueidentifier  NULL,
    [LastModifiedById]           uniqueidentifier  NULL,
    [IsDeleted]                  bit               NOT NULL,
    [DeletedAt]                  datetime2         NULL,
    [DeletedBy]                  nvarchar(max)     NULL,
    [TenantId]                   uniqueidentifier  NOT NULL,
    CONSTRAINT [PK_RecruitmentTestAnswers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecruitmentTestAnswers_RecruitmentTestQuestionOptions_SelectedOptionId] FOREIGN KEY ([SelectedOptionId])
        REFERENCES [dbo].[RecruitmentTestQuestionOptions] ([Id]),
    CONSTRAINT [FK_RecruitmentTestAnswers_RecruitmentTestQuestions_RecruitmentTestQuestionId] FOREIGN KEY ([RecruitmentTestQuestionId])
        REFERENCES [dbo].[RecruitmentTestQuestions] ([Id]),
    CONSTRAINT [FK_RecruitmentTestAnswers_RecruitmentTestSittings_RecruitmentTestSittingId] FOREIGN KEY ([RecruitmentTestSittingId])
        REFERENCES [dbo].[RecruitmentTestSittings] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_RecruitmentTestAnswers_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");
            migrationBuilder.Sql(CreateIndex("RecruitmentTestAnswers", "IX_RecruitmentTestAnswer_Question", "[RecruitmentTestQuestionId]"));
            migrationBuilder.Sql(CreateIndex("RecruitmentTestAnswers", "IX_RecruitmentTestAnswer_Sitting", "[RecruitmentTestSittingId]"));
            migrationBuilder.Sql(CreateIndex("RecruitmentTestAnswers", "IX_RecruitmentTestAnswers_SelectedOptionId", "[SelectedOptionId]"));
            migrationBuilder.Sql(CreateIndex("RecruitmentTestAnswers", "IX_RecruitmentTestAnswers_TenantId", "[TenantId]"));
        }

        /// <inheritdoc />
        /// <remarks>
        /// ⚠ Dropped CHILDREN FIRST — answers before sittings, options before questions — or the
        /// foreign keys refuse. EF's own creation order is dependency-correct, so reversing it is
        /// the drop order.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropTable("RecruitmentTestAnswers"));
            migrationBuilder.Sql(DropTable("RecruitmentTestQuestionOptions"));
            migrationBuilder.Sql(DropTable("RecruitmentTestQuestions"));
            migrationBuilder.Sql(DropTable("RecruitmentTestSittings"));
            migrationBuilder.Sql(DropTable("RecruitmentTestSections"));
            migrationBuilder.Sql(DropTable("RecruitmentTestAssignments"));
            migrationBuilder.Sql(DropTable("RecruitmentTests"));
        }
    }
}
