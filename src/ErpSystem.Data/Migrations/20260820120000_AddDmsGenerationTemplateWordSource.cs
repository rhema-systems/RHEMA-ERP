using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDmsGenerationTemplateWordSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration originally shipped without discovery metadata and was later
            // duplicated by 20260822130000. Some databases therefore already contain all
            // or part of this schema without this migration in __EFMigrationsHistory.
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'dbo.CentralDocumentGenerationTemplates', N'U') IS NULL
                    THROW 51000, 'CentralDocumentGenerationTemplates is required before adding uploaded template metadata.', 1;

                IF COL_LENGTH(N'dbo.CentralDocumentGenerationTemplates', N'TemplateFileUploadRecordId') IS NULL
                    ALTER TABLE [dbo].[CentralDocumentGenerationTemplates] ADD [TemplateFileUploadRecordId] uniqueidentifier NULL;

                IF COL_LENGTH(N'dbo.CentralDocumentGenerationTemplates', N'TemplateRepositoryPath') IS NULL
                    ALTER TABLE [dbo].[CentralDocumentGenerationTemplates] ADD [TemplateRepositoryPath] nvarchar(500) NULL;

                IF COL_LENGTH(N'dbo.CentralDocumentGenerationTemplates', N'TemplateFileName') IS NULL
                    ALTER TABLE [dbo].[CentralDocumentGenerationTemplates] ADD [TemplateFileName] nvarchar(250) NULL;

                IF COL_LENGTH(N'dbo.CentralDocumentGenerationTemplates', N'TemplateContentType') IS NULL
                    ALTER TABLE [dbo].[CentralDocumentGenerationTemplates] ADD [TemplateContentType] nvarchar(200) NULL;

                IF COL_LENGTH(N'dbo.CentralDocumentGenerationTemplates', N'TemplateFileSize') IS NULL
                    ALTER TABLE [dbo].[CentralDocumentGenerationTemplates] ADD [TemplateFileSize] bigint NULL;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [object_id] = OBJECT_ID(N'dbo.CentralDocumentGenerationTemplates')
                      AND [name] = N'IX_CentralDocumentGenerationTemplates_TenantId_TemplateFileUploadRecordId'
                )
                    CREATE INDEX [IX_CentralDocumentGenerationTemplates_TenantId_TemplateFileUploadRecordId]
                        ON [dbo].[CentralDocumentGenerationTemplates] ([TenantId], [TemplateFileUploadRecordId]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [object_id] = OBJECT_ID(N'dbo.CentralDocumentGenerationTemplates')
                      AND [name] = N'IX_CentralDocumentGenerationTemplates_TenantId_TemplateFileUploadRecordId'
                )
                    DROP INDEX [IX_CentralDocumentGenerationTemplates_TenantId_TemplateFileUploadRecordId]
                        ON [dbo].[CentralDocumentGenerationTemplates];

                IF COL_LENGTH(N'dbo.CentralDocumentGenerationTemplates', N'TemplateFileUploadRecordId') IS NOT NULL
                    ALTER TABLE [dbo].[CentralDocumentGenerationTemplates] DROP COLUMN [TemplateFileUploadRecordId];

                IF COL_LENGTH(N'dbo.CentralDocumentGenerationTemplates', N'TemplateRepositoryPath') IS NOT NULL
                    ALTER TABLE [dbo].[CentralDocumentGenerationTemplates] DROP COLUMN [TemplateRepositoryPath];

                IF COL_LENGTH(N'dbo.CentralDocumentGenerationTemplates', N'TemplateFileName') IS NOT NULL
                    ALTER TABLE [dbo].[CentralDocumentGenerationTemplates] DROP COLUMN [TemplateFileName];

                IF COL_LENGTH(N'dbo.CentralDocumentGenerationTemplates', N'TemplateContentType') IS NOT NULL
                    ALTER TABLE [dbo].[CentralDocumentGenerationTemplates] DROP COLUMN [TemplateContentType];

                IF COL_LENGTH(N'dbo.CentralDocumentGenerationTemplates', N'TemplateFileSize') IS NOT NULL
                    ALTER TABLE [dbo].[CentralDocumentGenerationTemplates] DROP COLUMN [TemplateFileSize];
                """);
        }
    }
}
