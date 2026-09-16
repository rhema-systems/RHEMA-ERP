using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Retires the consultant-client portal's own identity store and links client contacts to
    /// main-scheme accounts instead: drops <c>ConsultantClientPortalAccounts</c> and creates
    /// <c>ConsultantClientContacts</c> (client FK + Identity <c>Users</c> FK + invite audit).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why.</b> The consultant-client portal was the <c>PortalBearer</c> scheme's LAST tenant
    /// (the candidate half retired 2026-08-30). Decision 2026-08-31: contacts are invited by HR
    /// onto main-scheme Identity accounts under the <c>ConsultantClient</c> role (fenced by
    /// <c>ConsultantClientAccessMiddleware</c> and refused by <c>InternalOnly</c>), so the
    /// bespoke store goes; Identity owns every credential concern and the contact row is purely
    /// the authorisation anchor. <c>ConsultantClientPortalAccounts</c> held <b>zero rows</b>
    /// when dropped (verified via sqlcmd 2026-08-31), so no data story is needed.
    /// </para>
    /// <para>
    /// <b>The unique index filter carries <c>[IsDeleted] = 0</c> deliberately.</b> An unfiltered
    /// unique index over a soft delete is the defect this programme has fixed eight times: a
    /// removed row keeps the slot and the re-invite 500s naming nothing. With the filter, a
    /// soft-deleted contact row releases the (client, account) slot for a fresh invite.
    /// </para>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced because it was not idempotent.</b> <c>rebuild-db</c>
    /// builds from the EF model rather than the migration chain, so a database rebuilt after the
    /// entity change already lacks the portal table and already has the contacts table — bare
    /// <c>DropTable</c>/<c>CreateTable</c> would fail. Every step is guarded
    /// (<c>OBJECT_ID</c>/<c>sys.indexes</c>), so the migration is a no-op against a database
    /// already in the target shape and still does the work on one that is not.
    /// </para>
    /// </remarks>
    public partial class RetireConsultantClientPortalAndAddClientContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.ConsultantClientPortalAccounts', 'U') IS NOT NULL
    DROP TABLE [dbo].[ConsultantClientPortalAccounts];");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.ConsultantClientContacts', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ConsultantClientContacts] (
        [Id] uniqueidentifier NOT NULL,
        [ConsultantClientId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ContactName] nvarchar(200) NULL,
        [ContactRole] nvarchar(100) NULL,
        [IsActive] bit NOT NULL,
        [InvitedById] uniqueidentifier NULL,
        [InvitedAtUtc] datetime2 NULL,
        [LastInviteSentAtUtc] datetime2 NULL,
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
        CONSTRAINT [PK_ConsultantClientContacts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConsultantClientContacts_ConsultantClients_ConsultantClientId]
            FOREIGN KEY ([ConsultantClientId]) REFERENCES [dbo].[ConsultantClients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ConsultantClientContacts_Employees_InvitedById]
            FOREIGN KEY ([InvitedById]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ConsultantClientContacts_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ConsultantClientContacts_Users_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );
END;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ConsultantClientContact_Tenant_Client_User' AND object_id = OBJECT_ID('dbo.ConsultantClientContacts'))
    CREATE UNIQUE NONCLUSTERED INDEX [IX_ConsultantClientContact_Tenant_Client_User]
        ON [dbo].[ConsultantClientContacts] ([TenantId], [ConsultantClientId], [UserId])
        WHERE [IsDeleted] = 0;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ConsultantClientContacts_ConsultantClientId' AND object_id = OBJECT_ID('dbo.ConsultantClientContacts'))
    CREATE NONCLUSTERED INDEX [IX_ConsultantClientContacts_ConsultantClientId]
        ON [dbo].[ConsultantClientContacts] ([ConsultantClientId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ConsultantClientContacts_InvitedById' AND object_id = OBJECT_ID('dbo.ConsultantClientContacts'))
    CREATE NONCLUSTERED INDEX [IX_ConsultantClientContacts_InvitedById]
        ON [dbo].[ConsultantClientContacts] ([InvitedById]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ConsultantClientContacts_UserId' AND object_id = OBJECT_ID('dbo.ConsultantClientContacts'))
    CREATE NONCLUSTERED INDEX [IX_ConsultantClientContacts_UserId]
        ON [dbo].[ConsultantClientContacts] ([UserId]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.ConsultantClientContacts', 'U') IS NOT NULL
    DROP TABLE [dbo].[ConsultantClientContacts];");

            // The portal table is recreated empty (it held zero rows when dropped), guarded the
            // same way, so a Down against an already-rolled-back database is a no-op too.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.ConsultantClientPortalAccounts', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ConsultantClientPortalAccounts] (
        [Id] uniqueidentifier NOT NULL,
        [ConsultantClientId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [AccountSetupExpiry] datetime2 NULL,
        [AccountSetupToken] nvarchar(512) NULL,
        [ContactName] nvarchar(200) NULL,
        [ContactRole] nvarchar(100) NULL,
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
        CONSTRAINT [PK_ConsultantClientPortalAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConsultantClientPortalAccounts_ConsultantClients_ConsultantClientId]
            FOREIGN KEY ([ConsultantClientId]) REFERENCES [dbo].[ConsultantClients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ConsultantClientPortalAccounts_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );

    CREATE NONCLUSTERED INDEX [IX_ConsultantClientPortalAccounts_ConsultantClientId]
        ON [dbo].[ConsultantClientPortalAccounts] ([ConsultantClientId]);

    CREATE UNIQUE NONCLUSTERED INDEX [IX_ConsultantClientPortalAccounts_TenantId_Email]
        ON [dbo].[ConsultantClientPortalAccounts] ([TenantId], [Email]);
END;");
        }
    }
}
