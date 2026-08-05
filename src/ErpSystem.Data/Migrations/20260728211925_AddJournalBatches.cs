using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JournalBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookClassification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ControlCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BatchType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PostingStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReversalStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExpectedDebitTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpectedJournalCount = table.Column<int>(type: "int", nullable: true),
                    SubmittedDebitTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SubmittedCreditTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SubmittedJournalCount = table.Column<int>(type: "int", nullable: true),
                    SubmittedLineCount = table.Column<int>(type: "int", nullable: true),
                    ContentFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostingCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversalOfJournalBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalBatches", x => x.Id);
                    table.CheckConstraint("CK_JournalBatches_ExpectedDebitTotal", "[ExpectedDebitTotal] > 0");
                    table.CheckConstraint("CK_JournalBatches_ExpectedJournalCount", "[ExpectedJournalCount] IS NULL OR [ExpectedJournalCount] > 0");
                    table.ForeignKey(
                        name: "FK_JournalBatches_FiscalPeriods_FiscalPeriodId",
                        column: x => x.FiscalPeriodId,
                        principalTable: "FiscalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalBatches_JournalBatches_ReversalOfJournalBatchId",
                        column: x => x.ReversalOfJournalBatchId,
                        principalTable: "JournalBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalBatches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalBatchAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalBatchAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalBatchAttachments_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalBatchAttachments_JournalBatches_JournalBatchId",
                        column: x => x.JournalBatchId,
                        principalTable: "JournalBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalBatchAttachments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalBatchImportSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviewToken = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TemplateVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    JournalCount = table.Column<int>(type: "int", nullable: false),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    CommittedJournalBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NormalizedPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalBatchImportSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalBatchImportSessions_JournalBatches_CommittedJournalBatchId",
                        column: x => x.CommittedJournalBatchId,
                        principalTable: "JournalBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalBatchImportSessions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalBatchPostingRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunNumber = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SelectedDebitTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SelectedEntryCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalBatchPostingRuns", x => x.Id);
                    table.CheckConstraint("CK_JournalBatchPostingRuns_RunNumber", "[RunNumber] > 0");
                    table.CheckConstraint("CK_JournalBatchPostingRuns_SelectedEntryCount", "[SelectedEntryCount] > 0");
                    table.ForeignKey(
                        name: "FK_JournalBatchPostingRuns_JournalBatches_JournalBatchId",
                        column: x => x.JournalBatchId,
                        principalTable: "JournalBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalBatchPostingRuns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalBatchItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FinalReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinalReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalRejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubmittedContentFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PostingStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PostedInRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversalJournalBatchItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalBatchItems", x => x.Id);
                    table.CheckConstraint("CK_JournalBatchItems_SequenceNumber", "[SequenceNumber] > 0");
                    table.ForeignKey(
                        name: "FK_JournalBatchItems_JournalBatchItems_ReversalJournalBatchItemId",
                        column: x => x.ReversalJournalBatchItemId,
                        principalTable: "JournalBatchItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalBatchItems_JournalBatchPostingRuns_PostedInRunId",
                        column: x => x.PostedInRunId,
                        principalTable: "JournalBatchPostingRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalBatchItems_JournalBatches_JournalBatchId",
                        column: x => x.JournalBatchId,
                        principalTable: "JournalBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalBatchItems_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalBatchItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalBatchItemReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalBatchItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowStepInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowStageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalBatchItemReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalBatchItemReviews_JournalBatchItems_JournalBatchItemId",
                        column: x => x.JournalBatchItemId,
                        principalTable: "JournalBatchItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalBatchItemReviews_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalBatchPostingRunItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalBatchPostingRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalBatchItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancePostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalBatchPostingRunItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalBatchPostingRunItems_FinancePostingEvents_FinancePostingEventId",
                        column: x => x.FinancePostingEventId,
                        principalTable: "FinancePostingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalBatchPostingRunItems_JournalBatchItems_JournalBatchItemId",
                        column: x => x.JournalBatchItemId,
                        principalTable: "JournalBatchItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalBatchPostingRunItems_JournalBatchPostingRuns_JournalBatchPostingRunId",
                        column: x => x.JournalBatchPostingRunId,
                        principalTable: "JournalBatchPostingRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalBatchPostingRunItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchAttachments_FileUploadRecordId",
                table: "JournalBatchAttachments",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchAttachments_JournalBatchId",
                table: "JournalBatchAttachments",
                column: "JournalBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchAttachments_TenantId_JournalBatchId_FileUploadRecordId",
                table: "JournalBatchAttachments",
                columns: new[] { "TenantId", "JournalBatchId", "FileUploadRecordId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatches_FiscalPeriodId",
                table: "JournalBatches",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatches_ReversalOfJournalBatchId",
                table: "JournalBatches",
                column: "ReversalOfJournalBatchId",
                unique: true,
                filter: "[ReversalOfJournalBatchId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatches_TenantId_ApprovalStatus_FiscalPeriodId",
                table: "JournalBatches",
                columns: new[] { "TenantId", "ApprovalStatus", "FiscalPeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatches_TenantId_BatchNumber",
                table: "JournalBatches",
                columns: new[] { "TenantId", "BatchNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatches_TenantId_CreatedAt",
                table: "JournalBatches",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatches_TenantId_PostingStatus_FiscalPeriodId",
                table: "JournalBatches",
                columns: new[] { "TenantId", "PostingStatus", "FiscalPeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatches_TenantId_ReversalOfJournalBatchId",
                table: "JournalBatches",
                columns: new[] { "TenantId", "ReversalOfJournalBatchId" },
                unique: true,
                filter: "[ReversalOfJournalBatchId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchImportSessions_CommittedJournalBatchId",
                table: "JournalBatchImportSessions",
                column: "CommittedJournalBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchImportSessions_TenantId_IdempotencyKey",
                table: "JournalBatchImportSessions",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchImportSessions_TenantId_PreviewToken",
                table: "JournalBatchImportSessions",
                columns: new[] { "TenantId", "PreviewToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchImportSessions_TenantId_UploadedByUserId_ExpiresAt",
                table: "JournalBatchImportSessions",
                columns: new[] { "TenantId", "UploadedByUserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchItemReviews_JournalBatchItemId",
                table: "JournalBatchItemReviews",
                column: "JournalBatchItemId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchItemReviews_TenantId_JournalBatchItemId_WorkflowInstanceId_WorkflowStageKey",
                table: "JournalBatchItemReviews",
                columns: new[] { "TenantId", "JournalBatchItemId", "WorkflowInstanceId", "WorkflowStageKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchItems_JournalBatchId",
                table: "JournalBatchItems",
                column: "JournalBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchItems_JournalEntryId",
                table: "JournalBatchItems",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchItems_PostedInRunId",
                table: "JournalBatchItems",
                column: "PostedInRunId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchItems_ReversalJournalBatchItemId",
                table: "JournalBatchItems",
                column: "ReversalJournalBatchItemId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchItems_TenantId_JournalBatchId_ReviewStatus_PostingStatus",
                table: "JournalBatchItems",
                columns: new[] { "TenantId", "JournalBatchId", "ReviewStatus", "PostingStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchItems_TenantId_JournalBatchId_SequenceNumber",
                table: "JournalBatchItems",
                columns: new[] { "TenantId", "JournalBatchId", "SequenceNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchItems_TenantId_JournalEntryId",
                table: "JournalBatchItems",
                columns: new[] { "TenantId", "JournalEntryId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchPostingRunItems_FinancePostingEventId",
                table: "JournalBatchPostingRunItems",
                column: "FinancePostingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchPostingRunItems_JournalBatchItemId",
                table: "JournalBatchPostingRunItems",
                column: "JournalBatchItemId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchPostingRunItems_JournalBatchPostingRunId",
                table: "JournalBatchPostingRunItems",
                column: "JournalBatchPostingRunId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchPostingRunItems_TenantId_JournalBatchPostingRunId_JournalBatchItemId",
                table: "JournalBatchPostingRunItems",
                columns: new[] { "TenantId", "JournalBatchPostingRunId", "JournalBatchItemId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchPostingRuns_JournalBatchId",
                table: "JournalBatchPostingRuns",
                column: "JournalBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchPostingRuns_TenantId_JournalBatchId_IdempotencyKey",
                table: "JournalBatchPostingRuns",
                columns: new[] { "TenantId", "JournalBatchId", "IdempotencyKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatchPostingRuns_TenantId_JournalBatchId_RunNumber",
                table: "JournalBatchPostingRuns",
                columns: new[] { "TenantId", "JournalBatchId", "RunNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalBatchAttachments");

            migrationBuilder.DropTable(
                name: "JournalBatchImportSessions");

            migrationBuilder.DropTable(
                name: "JournalBatchItemReviews");

            migrationBuilder.DropTable(
                name: "JournalBatchPostingRunItems");

            migrationBuilder.DropTable(
                name: "JournalBatchItems");

            migrationBuilder.DropTable(
                name: "JournalBatchPostingRuns");

            migrationBuilder.DropTable(
                name: "JournalBatches");
        }
    }
}
