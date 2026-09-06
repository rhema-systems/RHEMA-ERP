using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext)), Migration("20260906190846_AddAccountingBookApplicabilityFoundation")]
    public partial class AddAccountingBookApplicabilityFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing tenants receive no fabricated parallel rules: the runtime primary-only fallback is authoritative.
            // This complete C1-C4 predecessor audit runs before every C5 mutation.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[AccountingBooks]', N'U') IS NULL OR OBJECT_ID(N'[Tenants]', N'U') IS NULL
   OR COL_LENGTH(N'AccountingBooks', N'TenantId') IS NULL OR COL_LENGTH(N'AccountingBooks', N'Code') IS NULL
   OR COL_LENGTH(N'AccountingBooks', N'BookType') IS NULL OR COL_LENGTH(N'AccountingBooks', N'LifecycleStatus') IS NULL
    THROW 51000, 'C5_SCHEMA_PREFLIGHT: governed C1-C4 AccountingBook predecessor is required.', 1;

IF OBJECT_ID(N'[AccountingBookApplicabilityPolicies]', N'U') IS NOT NULL
   OR OBJECT_ID(N'[AccountingBookApplicabilityRules]', N'U') IS NOT NULL
   OR OBJECT_ID(N'[AccountingBookApplicabilityRuleBooks]', N'U') IS NOT NULL
   OR OBJECT_ID(N'[AccountingBookSelectionEvidence]', N'U') IS NOT NULL
   OR OBJECT_ID(N'[AccountingBookSelectionEvidenceBooks]', N'U') IS NOT NULL
    THROW 51000, 'C5_SCHEMA_PREFLIGHT: conflicting C5 tables already exist; no mutation was performed.', 1;

IF EXISTS (
    SELECT 1 FROM [AccountingBooks] b
    LEFT JOIN [Tenants] t ON t.[Id] = b.[TenantId]
    WHERE b.[IsDeleted] = 0 AND (t.[Id] IS NULL OR t.[IsDeleted] = 1
       OR LEN(b.[Code]) = 0
       OR b.[Code] COLLATE Latin1_General_100_BIN2 <> UPPER(LTRIM(RTRIM(b.[Code]))) COLLATE Latin1_General_100_BIN2
       OR DATALENGTH(b.[Code]) <> DATALENGTH(UPPER(LTRIM(RTRIM(b.[Code]))))
       OR LEFT(b.[Code], 1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
       OR b.[Code] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_]%'
       OR b.[Code] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')
       OR b.[BookType] NOT IN (1,2,3) OR b.[LifecycleStatus] NOT IN (1,2,3,4,5,6))
)
    THROW 51000, 'C5_BOOK_PREFLIGHT: live accounting-book lineage is noncanonical, pseudo, or cross-tenant.', 1;
");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AccountingBooks_TenantId_Id_Code",
                table: "AccountingBooks",
                columns: new[] { "TenantId", "Id", "Code" });

            migrationBuilder.CreateTable(
                name: "AccountingBookApplicabilityPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SupersedesPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PolicyStatus = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RetiredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetirementRequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetirementRequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetirementReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RetirementWorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetirementDecisionStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RetirementDecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetirementDecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetirementDecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AccountingBookApplicabilityPolicies", x => x.Id);
                    table.UniqueConstraint("AK_AccountingBookApplicabilityPolicies_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_AccountingBookApplicabilityPolicies_TenantId_Id_Version", x => new { x.TenantId, x.Id, x.Version });
                    table.UniqueConstraint("AK_AccountingBookApplicabilityPolicies_TenantId_PolicyCode_Id", x => new { x.TenantId, x.PolicyCode, x.Id });
                    table.CheckConstraint("CK_AccountingBookApplicabilityPolicies_ApprovalShape", "([PolicyStatus] IN (1,2,4) AND [ApprovedByUserId] IS NULL AND [ApprovedAtUtc] IS NULL AND [RetiredByUserId] IS NULL AND [RetiredAtUtc] IS NULL) OR ([PolicyStatus] = 3 AND [ApprovedByUserId] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL AND [RetiredByUserId] IS NULL AND [RetiredAtUtc] IS NULL) OR ([PolicyStatus] = 5 AND [ApprovedByUserId] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL AND [RetiredByUserId] IS NOT NULL AND [RetiredAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_AccountingBookApplicabilityPolicies_CodeCanonical", "LEN([PolicyCode]) > 0 AND [PolicyCode] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([PolicyCode]))) COLLATE Latin1_General_100_BIN2 AND DATALENGTH([PolicyCode]) = DATALENGTH(UPPER(LTRIM(RTRIM([PolicyCode])))) AND LEFT([PolicyCode], 1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [PolicyCode] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_]%' AND [PolicyCode] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.CheckConstraint("CK_AccountingBookApplicabilityPolicies_EffectiveRange", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_AccountingBookApplicabilityPolicies_VersionLineageShape", "([Version] = 1 AND [SupersedesPolicyId] IS NULL) OR ([Version] > 1 AND [SupersedesPolicyId] IS NOT NULL)");
                    table.CheckConstraint("CK_AccountingBookApplicabilityPolicies_MakerChecker", "[ApprovedByUserId] IS NULL OR [ApprovedByUserId] <> [PreparedByUserId]");
                    table.CheckConstraint("CK_AccountingBookApplicabilityPolicies_NoDelete", "[IsDeleted] = 0");
                    table.CheckConstraint("CK_AccountingBookApplicabilityPolicies_RetirementMakerChecker", "[RetiredByUserId] IS NULL OR [RetiredByUserId] <> [RetirementRequestedByUserId]");
                    table.CheckConstraint("CK_AccountingBookApplicabilityPolicies_RetirementRequestShape", "([RetirementDecisionStatus] IS NULL AND [RetirementRequestedByUserId] IS NULL AND [RetirementRequestedAtUtc] IS NULL AND [RetirementReason] IS NULL AND [RetirementWorkflowInstanceId] IS NULL AND [RetirementDecidedByUserId] IS NULL AND [RetirementDecidedAtUtc] IS NULL) OR ([RetirementDecisionStatus] = 'Pending' AND [RetirementRequestedByUserId] IS NOT NULL AND [RetirementRequestedAtUtc] IS NOT NULL AND [RetirementReason] IS NOT NULL AND [RetirementWorkflowInstanceId] IS NOT NULL AND [RetirementDecidedByUserId] IS NULL AND [RetirementDecidedAtUtc] IS NULL) OR ([RetirementDecisionStatus] IN ('Approved','Rejected') AND [RetirementRequestedByUserId] IS NOT NULL AND [RetirementRequestedAtUtc] IS NOT NULL AND [RetirementReason] IS NOT NULL AND [RetirementWorkflowInstanceId] IS NOT NULL AND [RetirementDecidedByUserId] IS NOT NULL AND [RetirementDecidedAtUtc] IS NOT NULL AND [RetirementDecisionReason] IS NOT NULL)");
                    table.CheckConstraint("CK_AccountingBookApplicabilityPolicies_Status", "[PolicyStatus] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_AccountingBookApplicabilityPolicies_AccountingBookApplicabilityPolicies_TenantId_PolicyCode_SupersedesPolicyId",
                        columns: x => new { x.TenantId, x.PolicyCode, x.SupersedesPolicyId },
                        principalTable: "AccountingBookApplicabilityPolicies",
                        principalColumns: new[] { "TenantId", "PolicyCode", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookApplicabilityPolicies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountingBookApplicabilityRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingBookApplicabilityPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    OriginatingModuleCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PostingAction = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AccountingBookApplicabilityRules", x => x.Id);
                    table.UniqueConstraint("AK_AccountingBookApplicabilityRules_TenantId_AccountingBookApplicabilityPolicyId_Id", x => new { x.TenantId, x.AccountingBookApplicabilityPolicyId, x.Id });
                    table.UniqueConstraint("AK_AccountingBookApplicabilityRules_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_AccountingBookApplicabilityRules_ActionCanonical", "LEN([PostingAction]) > 0 AND LEFT([PostingAction],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [PostingAction] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([PostingAction]))) COLLATE Latin1_General_100_BIN2 AND [PostingAction] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND [PostingAction] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.CheckConstraint("CK_AccountingBookApplicabilityRules_DocumentCanonical", "LEN([SourceDocumentType]) > 0 AND LEFT([SourceDocumentType],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [SourceDocumentType] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([SourceDocumentType]))) COLLATE Latin1_General_100_BIN2 AND [SourceDocumentType] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND [SourceDocumentType] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.CheckConstraint("CK_AccountingBookApplicabilityRules_ModuleCanonical", "LEN([OriginatingModuleCode]) > 0 AND [OriginatingModuleCode] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([OriginatingModuleCode]))) COLLATE Latin1_General_100_BIN2 AND [OriginatingModuleCode] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%'");
                    table.CheckConstraint("CK_AccountingBookApplicabilityRules_ModuleSupported", "[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2 IN (N'FIN',N'INV',N'PROC',N'SALES',N'HR',N'QS',N'ESTATE',N'LEGAL',N'MAINT')");
                    table.CheckConstraint("CK_AccountingBookApplicabilityRules_NoDelete", "[IsDeleted] = 0");
                    table.CheckConstraint("CK_AccountingBookApplicabilityRules_Priority", "[Priority] >= 0");
                    table.CheckConstraint("CK_AccountingBookApplicabilityRules_RuleCodeCanonical", "LEN([RuleCode]) > 0 AND LEFT([RuleCode],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [RuleCode] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([RuleCode]))) COLLATE Latin1_General_100_BIN2 AND [RuleCode] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_]%' AND [RuleCode] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.ForeignKey(
                        name: "FK_AccountingBookApplicabilityRules_AccountingBookApplicabilityPolicies_TenantId_AccountingBookApplicabilityPolicyId",
                        columns: x => new { x.TenantId, x.AccountingBookApplicabilityPolicyId },
                        principalTable: "AccountingBookApplicabilityPolicies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookApplicabilityRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountingBookApplicabilityRuleBooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingBookApplicabilityRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectionOrder = table.Column<int>(type: "int", nullable: false),
                    AccountingBookCodeSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_AccountingBookApplicabilityRuleBooks", x => x.Id);
                    table.CheckConstraint("CK_AccountingBookApplicabilityRuleBooks_CodeCanonical", "LEN([AccountingBookCodeSnapshot]) > 0 AND LEFT([AccountingBookCodeSnapshot],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [AccountingBookCodeSnapshot] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([AccountingBookCodeSnapshot]))) COLLATE Latin1_General_100_BIN2 AND [AccountingBookCodeSnapshot] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_]%' AND [AccountingBookCodeSnapshot] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.CheckConstraint("CK_AccountingBookApplicabilityRuleBooks_NoDelete", "[IsDeleted] = 0");
                    table.ForeignKey(
                        name: "FK_AccountingBookApplicabilityRuleBooks_AccountingBookApplicabilityRules_TenantId_AccountingBookApplicabilityRuleId",
                        columns: x => new { x.TenantId, x.AccountingBookApplicabilityRuleId },
                        principalTable: "AccountingBookApplicabilityRules",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookApplicabilityRuleBooks_AccountingBooks_TenantId_AccountingBookId_AccountingBookCodeSnapshot",
                        columns: x => new { x.TenantId, x.AccountingBookId, x.AccountingBookCodeSnapshot },
                        principalTable: "AccountingBooks",
                        principalColumns: new[] { "TenantId", "Id", "Code" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookApplicabilityRuleBooks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountingBookSelectionEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingBookApplicabilityPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccountingBookApplicabilityRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PolicyVersion = table.Column<int>(type: "int", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OriginatingModuleCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PostingAction = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CalculationInputHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SelectionFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FrozenByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FrozenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AccountingBookSelectionEvidence", x => x.Id);
                    table.UniqueConstraint("AK_AccountingBookSelectionEvidence_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_AccountingBookSelectionEvidence_ActionCanonical", "LEN([PostingAction]) > 0 AND LEFT([PostingAction],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [PostingAction] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([PostingAction]))) COLLATE Latin1_General_100_BIN2 AND [PostingAction] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND [PostingAction] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.CheckConstraint("CK_AccountingBookSelectionEvidence_DocumentCanonical", "LEN([SourceDocumentType]) > 0 AND LEFT([SourceDocumentType],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [SourceDocumentType] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([SourceDocumentType]))) COLLATE Latin1_General_100_BIN2 AND [SourceDocumentType] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND [SourceDocumentType] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.CheckConstraint("CK_AccountingBookSelectionEvidence_Fingerprint", "LEN([SelectionFingerprint]) = 64 AND [SelectionFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AccountingBookSelectionEvidence_InputHash", "LEN([CalculationInputHash]) = 64 AND [CalculationInputHash] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AccountingBookSelectionEvidence_ModuleCanonical", "LEN([OriginatingModuleCode]) > 0 AND [OriginatingModuleCode] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([OriginatingModuleCode]))) COLLATE Latin1_General_100_BIN2 AND [OriginatingModuleCode] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%'");
                    table.CheckConstraint("CK_AccountingBookSelectionEvidence_ModuleSupported", "[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2 IN (N'FIN',N'INV',N'PROC',N'SALES',N'HR',N'QS',N'ESTATE',N'LEGAL',N'MAINT')");
                    table.CheckConstraint("CK_AccountingBookSelectionEvidence_NoDelete", "[IsDeleted] = 0");
                    table.CheckConstraint("CK_AccountingBookSelectionEvidence_RuleLineage", "([AccountingBookApplicabilityPolicyId] IS NULL AND [AccountingBookApplicabilityRuleId] IS NULL AND [PolicyVersion] IS NULL) OR ([AccountingBookApplicabilityPolicyId] IS NOT NULL AND [AccountingBookApplicabilityRuleId] IS NOT NULL AND [PolicyVersion] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AccountingBookSelectionEvidence_AccountingBookApplicabilityPolicies_TenantId_AccountingBookApplicabilityPolicyId_PolicyVersi~",
                        columns: x => new { x.TenantId, x.AccountingBookApplicabilityPolicyId, x.PolicyVersion },
                        principalTable: "AccountingBookApplicabilityPolicies",
                        principalColumns: new[] { "TenantId", "Id", "Version" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookSelectionEvidence_AccountingBookApplicabilityRules_TenantId_AccountingBookApplicabilityPolicyId_AccountingBook~",
                        columns: x => new { x.TenantId, x.AccountingBookApplicabilityPolicyId, x.AccountingBookApplicabilityRuleId },
                        principalTable: "AccountingBookApplicabilityRules",
                        principalColumns: new[] { "TenantId", "AccountingBookApplicabilityPolicyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookSelectionEvidence_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountingBookSelectionEvidenceBooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingBookSelectionEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectionOrder = table.Column<int>(type: "int", nullable: false),
                    AccountingBookCodeSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AuthorityFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_AccountingBookSelectionEvidenceBooks", x => x.Id);
                    table.CheckConstraint("CK_AccountingBookSelectionEvidenceBooks_AuthorityFingerprint", "LEN([AuthorityFingerprint]) = 64 AND [AuthorityFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AccountingBookSelectionEvidenceBooks_CodeCanonical", "LEN([AccountingBookCodeSnapshot]) > 0 AND LEFT([AccountingBookCodeSnapshot],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [AccountingBookCodeSnapshot] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([AccountingBookCodeSnapshot]))) COLLATE Latin1_General_100_BIN2 AND [AccountingBookCodeSnapshot] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_]%' AND [AccountingBookCodeSnapshot] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.CheckConstraint("CK_AccountingBookSelectionEvidenceBooks_NoDelete", "[IsDeleted] = 0");
                    table.ForeignKey(
                        name: "FK_AccountingBookSelectionEvidenceBooks_AccountingBookSelectionEvidence_TenantId_AccountingBookSelectionEvidenceId",
                        columns: x => new { x.TenantId, x.AccountingBookSelectionEvidenceId },
                        principalTable: "AccountingBookSelectionEvidence",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookSelectionEvidenceBooks_AccountingBooks_TenantId_AccountingBookId_AccountingBookCodeSnapshot",
                        columns: x => new { x.TenantId, x.AccountingBookId, x.AccountingBookCodeSnapshot },
                        principalTable: "AccountingBooks",
                        principalColumns: new[] { "TenantId", "Id", "Code" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookSelectionEvidenceBooks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookApplicabilityPolicies_TenantId_PolicyCode_SupersedesPolicyId",
                table: "AccountingBookApplicabilityPolicies",
                columns: new[] { "TenantId", "PolicyCode", "SupersedesPolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookApplicabilityPolicies_TenantId_PolicyCode_Version",
                table: "AccountingBookApplicabilityPolicies",
                columns: new[] { "TenantId", "PolicyCode", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookApplicabilityRuleBooks_TenantId_AccountingBookApplicabilityRuleId_AccountingBookId",
                table: "AccountingBookApplicabilityRuleBooks",
                columns: new[] { "TenantId", "AccountingBookApplicabilityRuleId", "AccountingBookId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookApplicabilityRuleBooks_TenantId_AccountingBookApplicabilityRuleId_SelectionOrder",
                table: "AccountingBookApplicabilityRuleBooks",
                columns: new[] { "TenantId", "AccountingBookApplicabilityRuleId", "SelectionOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookApplicabilityRuleBooks_TenantId_AccountingBookId_AccountingBookCodeSnapshot",
                table: "AccountingBookApplicabilityRuleBooks",
                columns: new[] { "TenantId", "AccountingBookId", "AccountingBookCodeSnapshot" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookApplicabilityRules_TenantId_AccountingBookApplicabilityPolicyId_OriginatingModuleCode_SourceDocumentType_Posti~",
                table: "AccountingBookApplicabilityRules",
                columns: new[] { "TenantId", "AccountingBookApplicabilityPolicyId", "OriginatingModuleCode", "SourceDocumentType", "PostingAction", "Priority" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookApplicabilityRules_TenantId_AccountingBookApplicabilityPolicyId_RuleCode",
                table: "AccountingBookApplicabilityRules",
                columns: new[] { "TenantId", "AccountingBookApplicabilityPolicyId", "RuleCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookSelectionEvidence_TenantId_AccountingBookApplicabilityPolicyId_AccountingBookApplicabilityRuleId",
                table: "AccountingBookSelectionEvidence",
                columns: new[] { "TenantId", "AccountingBookApplicabilityPolicyId", "AccountingBookApplicabilityRuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookSelectionEvidence_TenantId_AccountingBookApplicabilityPolicyId_PolicyVersion",
                table: "AccountingBookSelectionEvidence",
                columns: new[] { "TenantId", "AccountingBookApplicabilityPolicyId", "PolicyVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookSelectionEvidence_TenantId_IdempotencyKey",
                table: "AccountingBookSelectionEvidence",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookSelectionEvidenceBooks_TenantId_AccountingBookId_AccountingBookCodeSnapshot",
                table: "AccountingBookSelectionEvidenceBooks",
                columns: new[] { "TenantId", "AccountingBookId", "AccountingBookCodeSnapshot" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookSelectionEvidenceBooks_TenantId_AccountingBookSelectionEvidenceId_AccountingBookId",
                table: "AccountingBookSelectionEvidenceBooks",
                columns: new[] { "TenantId", "AccountingBookSelectionEvidenceId", "AccountingBookId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookSelectionEvidenceBooks_TenantId_AccountingBookSelectionEvidenceId_SelectionOrder",
                table: "AccountingBookSelectionEvidenceBooks",
                columns: new[] { "TenantId", "AccountingBookSelectionEvidenceId", "SelectionOrder" },
                unique: true);

            // Runtime uses the serializable tenant writer lock. These triggers close direct-SQL and concurrent
            // commit gaps at the approved-authority and frozen-evidence boundaries. Approval must prove that
            // an explicit rule still owns at least one exact same-tenant full-book identity; never weaken this
            // to silently discard corrupt selections.
            migrationBuilder.Sql(@"
CREATE TRIGGER [TR_AccountingBookApplicabilityPolicies_C5Authority]
ON [AccountingBookApplicabilityPolicies]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
        THROW 51000, 'C5_POLICY_IMMUTABLE: policies cannot be physically deleted.', 1;

    IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.[Id]=i.[Id]
               WHERE d.[Id] IS NULL AND i.[PolicyStatus] IN (3,5))
        THROW 51000, 'C5_POLICY_TRANSITION_INVALID: approved or retired authority must be reached through a governed transition.', 1;

    -- Retired policy authority is a closed historical fact. Even direct SQL must create the request in one
    -- committed update and approve that pre-existing request in a later update; a caller cannot synthesize
    -- request and decision evidence while retiring, nor rewrite any part of an already-retired row.
    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id] WHERE d.[PolicyStatus]=5)
        THROW 51000, 'C5_POLICY_RETIRED_IMMUTABLE: retired policy authority cannot be rewritten.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
        WHERE i.[PolicyStatus]<>d.[PolicyStatus]
          AND NOT ((d.[PolicyStatus]=1 AND i.[PolicyStatus]=2)
                OR (d.[PolicyStatus]=2 AND i.[PolicyStatus] IN (3,4))
                OR (d.[PolicyStatus]=3 AND i.[PolicyStatus]=5))
    ) THROW 51000, 'C5_POLICY_TRANSITION_INVALID: direct policy status demotion or ungoverned transition is forbidden.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
        WHERE d.[PolicyStatus] IN (3,5)
          AND (i.[TenantId]<>d.[TenantId]
            OR i.[PolicyCode] COLLATE Latin1_General_100_BIN2<>d.[PolicyCode] COLLATE Latin1_General_100_BIN2
            OR i.[Version]<>d.[Version]
            OR ISNULL(i.[SupersedesPolicyId],'00000000-0000-0000-0000-000000000000')<>ISNULL(d.[SupersedesPolicyId],'00000000-0000-0000-0000-000000000000')
            OR i.[Name] COLLATE Latin1_General_100_BIN2<>d.[Name] COLLATE Latin1_General_100_BIN2
            OR ISNULL(i.[Description],N'') COLLATE Latin1_General_100_BIN2<>ISNULL(d.[Description],N'') COLLATE Latin1_General_100_BIN2
            OR i.[Reason] COLLATE Latin1_General_100_BIN2<>d.[Reason] COLLATE Latin1_General_100_BIN2
            OR i.[EffectiveFrom]<>d.[EffectiveFrom]
            OR (ISNULL(i.[EffectiveTo],CONVERT(datetime2,'9999-12-31'))<>ISNULL(d.[EffectiveTo],CONVERT(datetime2,'9999-12-31'))
                AND NOT (d.[PolicyStatus]=3 AND i.[PolicyStatus]=5 AND i.[RetiredAtUtc] IS NOT NULL
                         AND i.[EffectiveTo]=CASE WHEN d.[EffectiveTo] IS NULL OR CONVERT(date,i.[RetiredAtUtc])<d.[EffectiveTo]
                            THEN CONVERT(date,i.[RetiredAtUtc]) ELSE d.[EffectiveTo] END))
            OR ISNULL(i.[ApprovedByUserId],'00000000-0000-0000-0000-000000000000')<>ISNULL(d.[ApprovedByUserId],'00000000-0000-0000-0000-000000000000')
            OR ISNULL(i.[ApprovedAtUtc],CONVERT(datetime2,'1900-01-01'))<>ISNULL(d.[ApprovedAtUtc],CONVERT(datetime2,'1900-01-01'))
            OR i.[PreparedByUserId]<>d.[PreparedByUserId] OR i.[PreparedAtUtc]<>d.[PreparedAtUtc]
            OR ISNULL(i.[WorkflowInstanceId],'00000000-0000-0000-0000-000000000000')<>ISNULL(d.[WorkflowInstanceId],'00000000-0000-0000-0000-000000000000')
            OR ISNULL(i.[DecidedByUserId],'00000000-0000-0000-0000-000000000000')<>ISNULL(d.[DecidedByUserId],'00000000-0000-0000-0000-000000000000')
            OR ISNULL(i.[DecidedAtUtc],CONVERT(datetime2,'1900-01-01'))<>ISNULL(d.[DecidedAtUtc],CONVERT(datetime2,'1900-01-01'))
            OR ISNULL(i.[DecisionReason],N'') COLLATE Latin1_General_100_BIN2<>ISNULL(d.[DecisionReason],N'') COLLATE Latin1_General_100_BIN2)
    ) THROW 51000, 'C5_POLICY_IMMUTABLE: approved or retired structural and approval authority is immutable except for bounded governed retirement closure.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
        WHERE ((d.[PolicyStatus]=3 AND i.[PolicyStatus]=5)
            OR ISNULL(i.[RetiredByUserId],'00000000-0000-0000-0000-000000000000')<>ISNULL(d.[RetiredByUserId],'00000000-0000-0000-0000-000000000000')
            OR ISNULL(i.[RetiredAtUtc],CONVERT(datetime2,'1900-01-01'))<>ISNULL(d.[RetiredAtUtc],CONVERT(datetime2,'1900-01-01'))
            OR ISNULL(i.[RetirementRequestedByUserId],'00000000-0000-0000-0000-000000000000')<>ISNULL(d.[RetirementRequestedByUserId],'00000000-0000-0000-0000-000000000000')
            OR ISNULL(i.[RetirementRequestedAtUtc],CONVERT(datetime2,'1900-01-01'))<>ISNULL(d.[RetirementRequestedAtUtc],CONVERT(datetime2,'1900-01-01'))
            OR ISNULL(i.[RetirementReason],N'') COLLATE Latin1_General_100_BIN2<>ISNULL(d.[RetirementReason],N'') COLLATE Latin1_General_100_BIN2
            OR ISNULL(i.[RetirementWorkflowInstanceId],'00000000-0000-0000-0000-000000000000')<>ISNULL(d.[RetirementWorkflowInstanceId],'00000000-0000-0000-0000-000000000000')
            OR ISNULL(i.[RetirementDecisionStatus],N'')<>ISNULL(d.[RetirementDecisionStatus],N'')
            OR ISNULL(i.[RetirementDecidedByUserId],'00000000-0000-0000-0000-000000000000')<>ISNULL(d.[RetirementDecidedByUserId],'00000000-0000-0000-0000-000000000000')
            OR ISNULL(i.[RetirementDecidedAtUtc],CONVERT(datetime2,'1900-01-01'))<>ISNULL(d.[RetirementDecidedAtUtc],CONVERT(datetime2,'1900-01-01'))
            OR ISNULL(i.[RetirementDecisionReason],N'') COLLATE Latin1_General_100_BIN2<>ISNULL(d.[RetirementDecisionReason],N'') COLLATE Latin1_General_100_BIN2)
          AND NOT (
              (d.[PolicyStatus]=3 AND i.[PolicyStatus]=3 AND ISNULL(d.[RetirementDecisionStatus],N'') IN (N'',N'Rejected')
               AND i.[RetirementDecisionStatus]=N'Pending' AND i.[RetirementRequestedByUserId] IS NOT NULL
               AND i.[RetirementRequestedAtUtc] IS NOT NULL AND NULLIF(LTRIM(RTRIM(i.[RetirementReason])),N'') IS NOT NULL
               AND i.[RetirementWorkflowInstanceId] IS NOT NULL AND i.[RetirementDecidedByUserId] IS NULL
               AND i.[RetirementDecidedAtUtc] IS NULL AND i.[RetirementDecisionReason] IS NULL
               AND i.[RetiredByUserId] IS NULL AND i.[RetiredAtUtc] IS NULL)
           OR (d.[PolicyStatus]=3 AND i.[PolicyStatus]=3 AND d.[RetirementDecisionStatus]=N'Pending'
               AND i.[RetirementDecisionStatus]=N'Rejected' AND i.[RetirementRequestedByUserId]=d.[RetirementRequestedByUserId]
               AND i.[RetirementRequestedAtUtc]=d.[RetirementRequestedAtUtc]
               AND i.[RetirementReason] COLLATE Latin1_General_100_BIN2=d.[RetirementReason] COLLATE Latin1_General_100_BIN2
               AND i.[RetirementWorkflowInstanceId]=d.[RetirementWorkflowInstanceId]
               AND i.[RetirementDecidedByUserId] IS NOT NULL AND i.[RetirementDecidedByUserId]<>d.[RetirementRequestedByUserId]
               AND i.[RetirementDecidedAtUtc] IS NOT NULL AND i.[RetirementDecidedAtUtc]>=d.[RetirementRequestedAtUtc]
               AND NULLIF(LTRIM(RTRIM(i.[RetirementDecisionReason])),N'') IS NOT NULL
               AND i.[RetiredByUserId] IS NULL AND i.[RetiredAtUtc] IS NULL)
           OR (d.[PolicyStatus]=3 AND i.[PolicyStatus]=5 AND d.[RetirementDecisionStatus]=N'Pending'
               AND i.[RetirementDecisionStatus]=N'Approved' AND i.[RetirementRequestedByUserId]=d.[RetirementRequestedByUserId]
               AND d.[RetirementRequestedByUserId] IS NOT NULL AND d.[RetirementRequestedAtUtc] IS NOT NULL
               AND NULLIF(LTRIM(RTRIM(d.[RetirementReason])),N'') IS NOT NULL AND d.[RetirementWorkflowInstanceId] IS NOT NULL
               AND i.[RetirementRequestedAtUtc]=d.[RetirementRequestedAtUtc]
               AND i.[RetirementReason] COLLATE Latin1_General_100_BIN2=d.[RetirementReason] COLLATE Latin1_General_100_BIN2
               AND i.[RetirementWorkflowInstanceId]=d.[RetirementWorkflowInstanceId]
               AND i.[RetirementDecidedByUserId] IS NOT NULL AND i.[RetirementDecidedByUserId]<>d.[RetirementRequestedByUserId]
               AND i.[RetirementDecidedAtUtc] IS NOT NULL AND i.[RetirementDecidedAtUtc]>=d.[RetirementRequestedAtUtc]
               AND NULLIF(LTRIM(RTRIM(i.[RetirementDecisionReason])),N'') IS NOT NULL
               AND i.[RetiredByUserId]=i.[RetirementDecidedByUserId] AND i.[RetiredAtUtc] IS NOT NULL
               AND i.[RetiredAtUtc]<=i.[RetirementDecidedAtUtc]
               AND CONVERT(date,i.[RetiredAtUtc])>=CONVERT(date,d.[RetirementRequestedAtUtc])
               AND CONVERT(date,i.[RetiredAtUtc])>=CONVERT(date,d.[EffectiveFrom])
               AND i.[EffectiveTo]=CASE WHEN d.[EffectiveTo] IS NOT NULL AND CONVERT(date,d.[EffectiveTo])<CONVERT(date,i.[RetiredAtUtc])
                    THEN CONVERT(datetime2,CONVERT(date,d.[EffectiveTo])) ELSE CONVERT(datetime2,CONVERT(date,i.[RetiredAtUtc])) END)
          )
    ) THROW 51000, 'C5_POLICY_RETIREMENT_TRANSITION_INVALID: retirement evidence may change only through the governed maker-checker request and decision path.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
        WHERE (ISNULL(i.[ApprovedByUserId],'00000000-0000-0000-0000-000000000000')<>ISNULL(d.[ApprovedByUserId],'00000000-0000-0000-0000-000000000000')
            OR ISNULL(i.[ApprovedAtUtc],CONVERT(datetime2,'1900-01-01'))<>ISNULL(d.[ApprovedAtUtc],CONVERT(datetime2,'1900-01-01')))
          AND NOT (d.[PolicyStatus]=2 AND i.[PolicyStatus]=3 AND d.[ApprovedByUserId] IS NULL AND d.[ApprovedAtUtc] IS NULL
                   AND i.[ApprovedByUserId] IS NOT NULL AND i.[ApprovedAtUtc] IS NOT NULL
                   AND i.[DecidedByUserId] IS NOT NULL AND i.[DecidedAtUtc] IS NOT NULL AND i.[DecisionReason] IS NOT NULL)
    ) THROW 51000, 'C5_POLICY_APPROVAL_IMMUTABLE: approval identity may be established only by the governed pending-to-approved decision.', 1;

    -- Version numbers are relational authority. The trigger complements the shape check by requiring
    -- the exact immediate same-tenant/same-code predecessor before any version can be approved or resolved.
    IF EXISTS (
        SELECT 1 FROM inserted i
        WHERE i.[Version]>1 AND NOT EXISTS (
            SELECT 1 FROM [AccountingBookApplicabilityPolicies] p WITH (UPDLOCK,HOLDLOCK)
            WHERE p.[Id]=i.[SupersedesPolicyId] AND p.[TenantId]=i.[TenantId] AND p.[IsDeleted]=0
              AND p.[PolicyCode] COLLATE Latin1_General_100_BIN2=i.[PolicyCode] COLLATE Latin1_General_100_BIN2
              AND p.[Version]=i.[Version]-1)
    ) THROW 51000, 'C5_POLICY_VERSION_LINEAGE: each successor must be exactly predecessor version plus one in the same tenant/code lineage.', 1;

    IF EXISTS (SELECT 1 FROM inserted i WHERE i.[PolicyStatus]=3 AND i.[IsDeleted]=0
               AND NOT EXISTS (SELECT 1 FROM [AccountingBookApplicabilityRules] r WITH (UPDLOCK,HOLDLOCK)
                               WHERE r.[TenantId]=i.[TenantId] AND r.[AccountingBookApplicabilityPolicyId]=i.[Id] AND r.[IsDeleted]=0))
        THROW 51000, 'C5_EMPTY_SELECTION: every approved explicit policy must contain a governed rule.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.[Id] = i.[Id]
        WHERE EXISTS (SELECT 1 FROM [AccountingBookSelectionEvidence] e WITH (UPDLOCK,HOLDLOCK)
                      WHERE e.[TenantId]=d.[TenantId] AND e.[AccountingBookApplicabilityPolicyId]=d.[Id])
          AND (i.[TenantId]<>d.[TenantId]
            OR i.[PolicyCode] COLLATE Latin1_General_100_BIN2<>d.[PolicyCode] COLLATE Latin1_General_100_BIN2
            OR i.[Version]<>d.[Version]
            OR ISNULL(i.[SupersedesPolicyId],'00000000-0000-0000-0000-000000000000')<>ISNULL(d.[SupersedesPolicyId],'00000000-0000-0000-0000-000000000000')
            OR i.[EffectiveFrom]<>d.[EffectiveFrom]
            OR (ISNULL(i.[EffectiveTo],CONVERT(datetime2,'9999-12-31'))<>ISNULL(d.[EffectiveTo],CONVERT(datetime2,'9999-12-31'))
                AND NOT (d.[PolicyStatus]=3 AND i.[PolicyStatus]=5 AND i.[RetiredAtUtc] IS NOT NULL
                         AND i.[EffectiveTo]=CASE WHEN d.[EffectiveTo] IS NULL OR CONVERT(date,i.[RetiredAtUtc])<d.[EffectiveTo]
                            THEN CONVERT(date,i.[RetiredAtUtc]) ELSE d.[EffectiveTo] END)))
    ) THROW 51000, 'C5_POLICY_IMMUTABLE: structural policy evidence cannot change after first approved use.', 1;

    IF EXISTS (
        SELECT 1 FROM [AccountingBookApplicabilityPolicies] a WITH (UPDLOCK,HOLDLOCK)
        JOIN [AccountingBookApplicabilityPolicies] b WITH (UPDLOCK,HOLDLOCK)
          ON b.[TenantId]=a.[TenantId]
         AND b.[PolicyCode] COLLATE Latin1_General_100_BIN2=a.[PolicyCode] COLLATE Latin1_General_100_BIN2
         AND b.[Id]<>a.[Id] AND b.[PolicyStatus]=3 AND b.[IsDeleted]=0 AND b.[Version]>a.[Version]
        WHERE a.[PolicyStatus]=3 AND a.[IsDeleted]=0
          AND b.[EffectiveFrom]<=a.[EffectiveFrom]
          AND EXISTS (SELECT 1 FROM inserted i WHERE i.[TenantId]=a.[TenantId])
    ) THROW 51000, 'C5_POLICY_REPLACEMENT_ORDER: an approved successor must start after its predecessor.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i
        JOIN [AccountingBookApplicabilityRules] r WITH (UPDLOCK,HOLDLOCK)
          ON r.[TenantId]=i.[TenantId] AND r.[AccountingBookApplicabilityPolicyId]=i.[Id] AND r.[IsDeleted]=0
        WHERE i.[PolicyStatus]=3 AND i.[IsDeleted]=0
          AND NOT EXISTS (SELECT 1 FROM [AccountingBookApplicabilityRuleBooks] rb WITH (UPDLOCK,HOLDLOCK)
                          WHERE rb.[TenantId]=r.[TenantId] AND rb.[AccountingBookApplicabilityRuleId]=r.[Id] AND rb.[IsDeleted]=0)
    ) THROW 51000, 'C5_EMPTY_SELECTION: every approved explicit rule must select at least one governed full book.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i
        JOIN [AccountingBookApplicabilityRules] r WITH (UPDLOCK,HOLDLOCK)
          ON r.[TenantId]=i.[TenantId] AND r.[AccountingBookApplicabilityPolicyId]=i.[Id] AND r.[IsDeleted]=0
        JOIN [AccountingBookApplicabilityRuleBooks] rb WITH (UPDLOCK,HOLDLOCK)
          ON rb.[TenantId]=r.[TenantId] AND rb.[AccountingBookApplicabilityRuleId]=r.[Id] AND rb.[IsDeleted]=0
        LEFT JOIN [AccountingBooks] b WITH (UPDLOCK,HOLDLOCK)
          ON b.[TenantId]=rb.[TenantId] AND b.[Id]=rb.[AccountingBookId]
         AND b.[Code] COLLATE Latin1_General_100_BIN2=rb.[AccountingBookCodeSnapshot] COLLATE Latin1_General_100_BIN2
        WHERE i.[PolicyStatus]=3 AND i.[IsDeleted]=0
          AND (b.[Id] IS NULL OR b.[IsDeleted]=1 OR b.[BookType] NOT IN (1,2))
    ) THROW 51000, 'C5_SELECTED_BOOK_INVALID: approved rules require exact same-tenant PrimaryFull or ParallelFull book evidence.', 1;

    IF EXISTS (
        SELECT 1 FROM [AccountingBookApplicabilityPolicies] a WITH (UPDLOCK,HOLDLOCK)
        JOIN [AccountingBookApplicabilityRules] ar WITH (UPDLOCK,HOLDLOCK)
          ON ar.[TenantId]=a.[TenantId] AND ar.[AccountingBookApplicabilityPolicyId]=a.[Id]
        JOIN [AccountingBookApplicabilityPolicies] b WITH (UPDLOCK,HOLDLOCK)
         ON b.[TenantId]=a.[TenantId] AND b.[Id]<>a.[Id] AND b.[PolicyStatus]=3 AND b.[IsDeleted]=0
         AND b.[PolicyCode] COLLATE Latin1_General_100_BIN2<>a.[PolicyCode] COLLATE Latin1_General_100_BIN2
         AND a.[EffectiveFrom]<=ISNULL(b.[EffectiveTo],CONVERT(datetime2,'9999-12-31'))
         AND b.[EffectiveFrom]<=ISNULL(a.[EffectiveTo],CONVERT(datetime2,'9999-12-31'))
        JOIN [AccountingBookApplicabilityRules] br WITH (UPDLOCK,HOLDLOCK)
          ON br.[TenantId]=b.[TenantId] AND br.[AccountingBookApplicabilityPolicyId]=b.[Id]
         AND br.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2=ar.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2
         AND br.[SourceDocumentType] COLLATE Latin1_General_100_BIN2=ar.[SourceDocumentType] COLLATE Latin1_General_100_BIN2
         AND br.[PostingAction] COLLATE Latin1_General_100_BIN2=ar.[PostingAction] COLLATE Latin1_General_100_BIN2
         AND br.[Priority]=ar.[Priority]
        WHERE a.[PolicyStatus]=3 AND a.[IsDeleted]=0
          AND EXISTS (SELECT 1 FROM inserted i WHERE i.[TenantId]=a.[TenantId])
    ) THROW 51000, 'C5_RULE_AMBIGUITY: overlapping approved policies cannot retain an equal-priority exact source/action rule.', 1;
END;
");

            migrationBuilder.Sql(@"
CREATE TRIGGER [TR_AccountingBookApplicabilityRules_C5Immutable]
ON [AccountingBookApplicabilityRules]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
        THROW 51000, 'C5_RULE_IMMUTABLE: rules cannot be physically deleted.', 1;
    IF EXISTS (
        SELECT 1 FROM (SELECT [TenantId],[AccountingBookApplicabilityPolicyId] FROM inserted UNION SELECT [TenantId],[AccountingBookApplicabilityPolicyId] FROM deleted) x
        JOIN [AccountingBookApplicabilityPolicies] p WITH (UPDLOCK,HOLDLOCK)
          ON p.[TenantId]=x.[TenantId] AND p.[Id]=x.[AccountingBookApplicabilityPolicyId]
        WHERE p.[PolicyStatus] IN (3,5)
           OR EXISTS (SELECT 1 FROM [AccountingBookSelectionEvidence] e WITH (UPDLOCK,HOLDLOCK)
                      WHERE e.[TenantId]=p.[TenantId] AND e.[AccountingBookApplicabilityPolicyId]=p.[Id])
    ) THROW 51000, 'C5_RULE_IMMUTABLE: rules cannot change after policy approval or first approved use.', 1;
END;
");

            migrationBuilder.Sql(@"
CREATE TRIGGER [TR_AccountingBookApplicabilityRuleBooks_C5Immutable]
ON [AccountingBookApplicabilityRuleBooks]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
        THROW 51000, 'C5_RULE_BOOK_IMMUTABLE: selected-book rules cannot be physically deleted.', 1;
    IF EXISTS (
        SELECT 1 FROM (SELECT [TenantId],[AccountingBookApplicabilityRuleId] FROM inserted UNION SELECT [TenantId],[AccountingBookApplicabilityRuleId] FROM deleted) x
        JOIN [AccountingBookApplicabilityRules] r WITH (UPDLOCK,HOLDLOCK)
          ON r.[TenantId]=x.[TenantId] AND r.[Id]=x.[AccountingBookApplicabilityRuleId]
        JOIN [AccountingBookApplicabilityPolicies] p WITH (UPDLOCK,HOLDLOCK)
          ON p.[TenantId]=r.[TenantId] AND p.[Id]=r.[AccountingBookApplicabilityPolicyId]
        WHERE p.[PolicyStatus] IN (3,5)
           OR EXISTS (SELECT 1 FROM [AccountingBookSelectionEvidence] e WITH (UPDLOCK,HOLDLOCK)
                      WHERE e.[TenantId]=p.[TenantId] AND e.[AccountingBookApplicabilityPolicyId]=p.[Id])
    ) THROW 51000, 'C5_RULE_BOOK_IMMUTABLE: selected books cannot change after policy approval or first approved use.', 1;
END;
");

            migrationBuilder.Sql(@"
CREATE TRIGGER [TR_AccountingBookSelectionEvidence_C5Immutable]
ON [AccountingBookSelectionEvidence] AFTER UPDATE, DELETE AS
BEGIN SET NOCOUNT ON; THROW 51000, 'C5_SELECTION_IMMUTABLE: frozen selection evidence cannot be changed or deleted.', 1; END;
");

            migrationBuilder.Sql(@"
CREATE TRIGGER [TR_AccountingBookSelectionEvidenceBooks_C5Immutable]
ON [AccountingBookSelectionEvidenceBooks] AFTER UPDATE, DELETE AS
BEGIN SET NOCOUNT ON; THROW 51000, 'C5_SELECTION_BOOK_IMMUTABLE: frozen ordered book evidence cannot be changed or deleted.', 1; END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [AccountingBookSelectionEvidenceBooks])
   OR EXISTS (SELECT 1 FROM [AccountingBookSelectionEvidence])
   OR EXISTS (SELECT 1 FROM [AccountingBookApplicabilityRuleBooks])
   OR EXISTS (SELECT 1 FROM [AccountingBookApplicabilityRules])
   OR EXISTS (SELECT 1 FROM [AccountingBookApplicabilityPolicies])
    THROW 51000, 'C5_DOWN_GUARD: applicability configuration or frozen selection evidence cannot be represented by the predecessor schema.', 1;
");

            migrationBuilder.DropTable(
                name: "AccountingBookApplicabilityRuleBooks");

            migrationBuilder.DropTable(
                name: "AccountingBookSelectionEvidenceBooks");

            migrationBuilder.DropTable(
                name: "AccountingBookSelectionEvidence");

            migrationBuilder.DropTable(
                name: "AccountingBookApplicabilityRules");

            migrationBuilder.DropTable(
                name: "AccountingBookApplicabilityPolicies");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AccountingBooks_TenantId_Id_Code",
                table: "AccountingBooks");
        }
    }
}
