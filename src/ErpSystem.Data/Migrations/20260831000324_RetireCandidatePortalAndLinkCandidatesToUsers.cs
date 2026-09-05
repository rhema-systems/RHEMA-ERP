using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Retires the candidate portal's own identity store and links candidates to main-scheme
    /// accounts instead: drops <c>CandidatePortalAccounts</c> and adds
    /// <c>JobCandidates.UserId</c> (FK to <c>Users</c>) with a filtered unique index.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why.</b> The candidate careers portal ran on its own <c>PortalBearer</c> auth with a
    /// bespoke account table. Decision 2026-08-30: candidates self-register on the main JWT
    /// scheme under the <c>Candidate</c> role (fenced by <c>CandidateAccessMiddleware</c> and
    /// refused by <c>InternalOnly</c>), so the bespoke store goes and the ownership link moves
    /// onto the candidate row itself — the old link pointed the other way
    /// (<c>CandidatePortalAccounts.JobCandidateId</c>). <c>CandidatePortalAccounts</c> held
    /// <b>zero rows</b> when dropped (verified 2026-08-30), so no data story is needed.
    /// </para>
    /// <para>
    /// <b>The index filter carries <c>[IsDeleted] = 0</c> deliberately.</b> An unfiltered unique
    /// index over a soft delete is the defect this programme has fixed eight times: a removed row
    /// keeps the slot and the re-link 500s naming nothing. With the filter, a soft-deleted
    /// candidate releases their account for a fresh profile.
    /// </para>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced because it was not idempotent.</b> <c>rebuild-db</c>
    /// builds from the EF model rather than the migration chain, so a database rebuilt after the
    /// entity change already lacks the portal table and already has the column, index and FK —
    /// bare <c>DropTable</c>/<c>AddColumn</c> would fail. Every step is guarded
    /// (<c>OBJECT_ID</c>/<c>COL_LENGTH</c>/<c>sys.indexes</c>), so the migration is a no-op
    /// against a database already in the target shape and still does the work on one that is not.
    /// </para>
    /// </remarks>
    public partial class RetireCandidatePortalAndLinkCandidatesToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.CandidatePortalAccounts', 'U') IS NOT NULL
    DROP TABLE [dbo].[CandidatePortalAccounts];");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.JobCandidates', 'UserId') IS NULL
    ALTER TABLE [dbo].[JobCandidates] ADD [UserId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobCandidate_UserId' AND object_id = OBJECT_ID('dbo.JobCandidates'))
    CREATE UNIQUE NONCLUSTERED INDEX [IX_JobCandidate_UserId]
        ON [dbo].[JobCandidates] ([UserId])
        WHERE [UserId] IS NOT NULL AND [IsDeleted] = 0;");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.FK_JobCandidates_Users_UserId', 'F') IS NULL
    ALTER TABLE [dbo].[JobCandidates]
        ADD CONSTRAINT [FK_JobCandidates_Users_UserId]
        FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.FK_JobCandidates_Users_UserId', 'F') IS NOT NULL
    ALTER TABLE [dbo].[JobCandidates] DROP CONSTRAINT [FK_JobCandidates_Users_UserId];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobCandidate_UserId' AND object_id = OBJECT_ID('dbo.JobCandidates'))
    DROP INDEX [IX_JobCandidate_UserId] ON [dbo].[JobCandidates];");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.JobCandidates', 'UserId') IS NOT NULL
    ALTER TABLE [dbo].[JobCandidates] DROP COLUMN [UserId];");

            // The portal table is recreated empty (it held zero rows when dropped), guarded the
            // same way, so a Down against an already-rolled-back database is a no-op too.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.CandidatePortalAccounts', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CandidatePortalAccounts] (
        [Id] uniqueidentifier NOT NULL,
        [JobCandidateId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [Email] nvarchar(200) NOT NULL,
        [EmailVerificationExpiry] datetime2 NULL,
        [EmailVerificationToken] nvarchar(512) NULL,
        [FailedLoginAttempts] int NOT NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [IsEmailVerified] bit NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [LastVerificationEmailSentAtUtc] datetime2 NULL,
        [LockedOutUntil] datetime2 NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [PasswordResetExpiry] datetime2 NULL,
        [PasswordResetToken] nvarchar(512) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CandidatePortalAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CandidatePortalAccounts_JobCandidates_JobCandidateId]
            FOREIGN KEY ([JobCandidateId]) REFERENCES [dbo].[JobCandidates] ([Id]),
        CONSTRAINT [FK_CandidatePortalAccounts_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_CandidatePortalAccounts_JobCandidateId]
        ON [dbo].[CandidatePortalAccounts] ([JobCandidateId]);
    CREATE INDEX [IX_CandidatePortalAccounts_TenantId]
        ON [dbo].[CandidatePortalAccounts] ([TenantId]);
END");
        }
    }
}
