using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class _20260213_AddEhcAgentReplyProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent creation: some environments may already have this table created manually.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[EhcAgentReplyProfiles]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[EhcAgentReplyProfiles] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Signature] nvarchar(2000) NULL,
        [IsSignatureEnabled] bit NOT NULL,
        [AppendSignatureToReplies] bit NOT NULL,
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
        CONSTRAINT [PK_EhcAgentReplyProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EhcAgentReplyProfiles_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EhcAgentReplyProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[EhcAgentReplyProfiles]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EhcAgentReplyProfiles_TenantId_UserId' AND object_id = OBJECT_ID(N'[dbo].[EhcAgentReplyProfiles]'))
        CREATE UNIQUE INDEX [IX_EhcAgentReplyProfiles_TenantId_UserId] ON [dbo].[EhcAgentReplyProfiles] ([TenantId], [UserId]);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EhcAgentReplyProfiles_UserId' AND object_id = OBJECT_ID(N'[dbo].[EhcAgentReplyProfiles]'))
        CREATE INDEX [IX_EhcAgentReplyProfiles_UserId] ON [dbo].[EhcAgentReplyProfiles] ([UserId]);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[EhcAgentReplyProfiles]', N'U') IS NOT NULL
    DROP TABLE [dbo].[EhcAgentReplyProfiles];
");
        }
    }
}
