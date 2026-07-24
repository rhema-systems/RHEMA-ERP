using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementPolicyDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementPolicySets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    LifecycleStatus = table.Column<int>(type: "int", nullable: false),
                    ScopeType = table.Column<int>(type: "int", nullable: false),
                    SourceConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BasePolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupersedesPolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultCurrencyCode = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangeSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetiredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ProcurementPolicySets", x => x.Id);
                    table.CheckConstraint("CK_ProcurementPolicySets_Currency", "LEN([DefaultCurrencyCode]) = 3");
                    table.CheckConstraint("CK_ProcurementPolicySets_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_ProcurementPolicySets_Lifecycle", "[LifecycleStatus] IN (0, 1, 2)");
                    table.CheckConstraint("CK_ProcurementPolicySets_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_ProcurementPolicySets_ProcurementConfigurationProfiles_SourceConfigurationProfileId",
                        column: x => x.SourceConfigurationProfileId,
                        principalTable: "ProcurementConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPolicySets_ProcurementPolicySets_BasePolicySetId",
                        column: x => x.BasePolicySetId,
                        principalTable: "ProcurementPolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPolicySets_ProcurementPolicySets_SupersedesPolicySetId",
                        column: x => x.SupersedesPolicySetId,
                        principalTable: "ProcurementPolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPolicySets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPolicyAuthorityRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    AuthorityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AuthorityRole = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: true),
                    CurrencyCode = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    LowerBound = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UpperBound = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    LowerInclusive = table.Column<bool>(type: "bit", nullable: false),
                    UpperInclusive = table.Column<bool>(type: "bit", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Quorum = table.Column<int>(type: "int", nullable: false),
                    IsObserver = table.Column<bool>(type: "bit", nullable: false),
                    EscalationAuthority = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OverrideAction = table.Column<int>(type: "int", nullable: false),
                    SourceRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDecisionKey = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProcurementPolicyAuthorityRules", x => x.Id);
                    table.CheckConstraint("CK_ProcurementPolicyAuthorityRule_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_ProcurementPolicyAuthorityRule_SourceDecision", "[SourceDecisionKey] LIKE 'DEC-[0-9][0-9][0-9]'");
                    table.CheckConstraint("CK_ProcurementPolicyAuthorityRules_Bounds", "[LowerBound] >= 0 AND ([UpperBound] IS NULL OR [UpperBound] >= [LowerBound])");
                    table.ForeignKey(
                        name: "FK_ProcurementPolicyAuthorityRules_ProcurementPolicySets_PolicySetId",
                        column: x => x.PolicySetId,
                        principalTable: "ProcurementPolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPolicyAuthorityRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPolicyAuthorityRules_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPolicyCategoryRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    ServiceClass = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequiresSpecification = table.Column<bool>(type: "bit", nullable: false),
                    SpecificationTemplateCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OverrideAction = table.Column<int>(type: "int", nullable: false),
                    SourceRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDecisionKey = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProcurementPolicyCategoryRules", x => x.Id);
                    table.CheckConstraint("CK_ProcurementPolicyCategoryRule_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_ProcurementPolicyCategoryRule_SourceDecision", "[SourceDecisionKey] LIKE 'DEC-[0-9][0-9][0-9]'");
                    table.ForeignKey(
                        name: "FK_ProcurementPolicyCategoryRules_ProcurementPolicySets_PolicySetId",
                        column: x => x.PolicySetId,
                        principalTable: "ProcurementPolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPolicyCategoryRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPolicyEvidenceRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    EvidenceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: true),
                    Method = table.Column<int>(type: "int", nullable: true),
                    SharedRequirementKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    RequiresVerification = table.Column<bool>(type: "bit", nullable: false),
                    MaximumAgeDays = table.Column<int>(type: "int", nullable: true),
                    OverrideAction = table.Column<int>(type: "int", nullable: false),
                    SourceRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDecisionKey = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProcurementPolicyEvidenceRules", x => x.Id);
                    table.CheckConstraint("CK_ProcurementPolicyEvidenceRule_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_ProcurementPolicyEvidenceRule_SourceDecision", "[SourceDecisionKey] LIKE 'DEC-[0-9][0-9][0-9]'");
                    table.ForeignKey(
                        name: "FK_ProcurementPolicyEvidenceRules_ProcurementPolicySets_PolicySetId",
                        column: x => x.PolicySetId,
                        principalTable: "ProcurementPolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPolicyEvidenceRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPolicyExceptionRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ExceptionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExceptionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: true),
                    Method = table.Column<int>(type: "int", nullable: true),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    JustificationRequired = table.Column<bool>(type: "bit", nullable: false),
                    EvidenceRequired = table.Column<bool>(type: "bit", nullable: false),
                    PostAwardFilingRequired = table.Column<bool>(type: "bit", nullable: false),
                    ApproverRole = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaximumDurationDays = table.Column<int>(type: "int", nullable: true),
                    OverrideAction = table.Column<int>(type: "int", nullable: false),
                    SourceRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDecisionKey = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProcurementPolicyExceptionRules", x => x.Id);
                    table.CheckConstraint("CK_ProcurementPolicyExceptionRule_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_ProcurementPolicyExceptionRule_SourceDecision", "[SourceDecisionKey] LIKE 'DEC-[0-9][0-9][0-9]'");
                    table.ForeignKey(
                        name: "FK_ProcurementPolicyExceptionRules_ProcurementPolicySets_PolicySetId",
                        column: x => x.PolicySetId,
                        principalTable: "ProcurementPolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPolicyExceptionRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPolicyExceptionRules_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPolicyMethodRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    ServiceClass = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Method = table.Column<int>(type: "int", nullable: false),
                    IsAllowed = table.Column<bool>(type: "bit", nullable: false),
                    RequiresCompetition = table.Column<bool>(type: "bit", nullable: false),
                    MinimumQuotationCount = table.Column<int>(type: "int", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApplicabilityConditions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OverrideAction = table.Column<int>(type: "int", nullable: false),
                    SourceRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDecisionKey = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProcurementPolicyMethodRules", x => x.Id);
                    table.CheckConstraint("CK_ProcurementPolicyMethodRule_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_ProcurementPolicyMethodRule_SourceDecision", "[SourceDecisionKey] LIKE 'DEC-[0-9][0-9][0-9]'");
                    table.ForeignKey(
                        name: "FK_ProcurementPolicyMethodRules_ProcurementPolicySets_PolicySetId",
                        column: x => x.PolicySetId,
                        principalTable: "ProcurementPolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPolicyMethodRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPolicyMethodRules_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPolicyRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RuleKind = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ProcurementPolicyRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementPolicyRevisions_ProcurementPolicySets_PolicySetId",
                        column: x => x.PolicySetId,
                        principalTable: "ProcurementPolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPolicyRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPolicySodRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InitiatorRole = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ConflictingRole = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Enforcement = table.Column<int>(type: "int", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OverrideAction = table.Column<int>(type: "int", nullable: false),
                    SourceRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDecisionKey = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProcurementPolicySodRules", x => x.Id);
                    table.CheckConstraint("CK_ProcurementPolicySodRule_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_ProcurementPolicySodRule_SourceDecision", "[SourceDecisionKey] LIKE 'DEC-[0-9][0-9][0-9]'");
                    table.ForeignKey(
                        name: "FK_ProcurementPolicySodRules_ProcurementPolicySets_PolicySetId",
                        column: x => x.PolicySetId,
                        principalTable: "ProcurementPolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPolicySodRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPolicyThresholdRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    ServiceClass = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Method = table.Column<int>(type: "int", nullable: false),
                    CurrencyCode = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    LowerBound = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UpperBound = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    LowerInclusive = table.Column<bool>(type: "bit", nullable: false),
                    UpperInclusive = table.Column<bool>(type: "bit", nullable: false),
                    StatutoryReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OverrideAction = table.Column<int>(type: "int", nullable: false),
                    SourceRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDecisionKey = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProcurementPolicyThresholdRules", x => x.Id);
                    table.CheckConstraint("CK_ProcurementPolicyThresholdRule_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_ProcurementPolicyThresholdRule_SourceDecision", "[SourceDecisionKey] LIKE 'DEC-[0-9][0-9][0-9]'");
                    table.CheckConstraint("CK_ProcurementPolicyThresholdRules_Bounds", "[LowerBound] >= 0 AND ([UpperBound] IS NULL OR [UpperBound] >= [LowerBound])");
                    table.ForeignKey(
                        name: "FK_ProcurementPolicyThresholdRules_ProcurementPolicySets_PolicySetId",
                        column: x => x.PolicySetId,
                        principalTable: "ProcurementPolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPolicyThresholdRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyAuthorityRules_PolicySetId",
                table: "ProcurementPolicyAuthorityRules",
                column: "PolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyAuthorityRules_TenantId_PolicySetId_Category_Sequence",
                table: "ProcurementPolicyAuthorityRules",
                columns: new[] { "TenantId", "PolicySetId", "Category", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyAuthorityRules_TenantId_PolicySetId_EffectiveFrom",
                table: "ProcurementPolicyAuthorityRules",
                columns: new[] { "TenantId", "PolicySetId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyAuthorityRules_TenantId_PolicySetId_RuleCode",
                table: "ProcurementPolicyAuthorityRules",
                columns: new[] { "TenantId", "PolicySetId", "RuleCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyAuthorityRules_WorkflowDefinitionId",
                table: "ProcurementPolicyAuthorityRules",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyCategoryRules_PolicySetId",
                table: "ProcurementPolicyCategoryRules",
                column: "PolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyCategoryRules_TenantId_PolicySetId_Category_ServiceClass",
                table: "ProcurementPolicyCategoryRules",
                columns: new[] { "TenantId", "PolicySetId", "Category", "ServiceClass" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyCategoryRules_TenantId_PolicySetId_EffectiveFrom",
                table: "ProcurementPolicyCategoryRules",
                columns: new[] { "TenantId", "PolicySetId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyCategoryRules_TenantId_PolicySetId_RuleCode",
                table: "ProcurementPolicyCategoryRules",
                columns: new[] { "TenantId", "PolicySetId", "RuleCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyEvidenceRules_PolicySetId",
                table: "ProcurementPolicyEvidenceRules",
                column: "PolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyEvidenceRules_TenantId_PolicySetId_EffectiveFrom",
                table: "ProcurementPolicyEvidenceRules",
                columns: new[] { "TenantId", "PolicySetId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyEvidenceRules_TenantId_PolicySetId_RuleCode",
                table: "ProcurementPolicyEvidenceRules",
                columns: new[] { "TenantId", "PolicySetId", "RuleCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyEvidenceRules_TenantId_PolicySetId_Stage",
                table: "ProcurementPolicyEvidenceRules",
                columns: new[] { "TenantId", "PolicySetId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyExceptionRules_PolicySetId",
                table: "ProcurementPolicyExceptionRules",
                column: "PolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyExceptionRules_TenantId_PolicySetId_EffectiveFrom",
                table: "ProcurementPolicyExceptionRules",
                columns: new[] { "TenantId", "PolicySetId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyExceptionRules_TenantId_PolicySetId_ExceptionType",
                table: "ProcurementPolicyExceptionRules",
                columns: new[] { "TenantId", "PolicySetId", "ExceptionType" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyExceptionRules_TenantId_PolicySetId_RuleCode",
                table: "ProcurementPolicyExceptionRules",
                columns: new[] { "TenantId", "PolicySetId", "RuleCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyExceptionRules_WorkflowDefinitionId",
                table: "ProcurementPolicyExceptionRules",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyMethodRules_PolicySetId",
                table: "ProcurementPolicyMethodRules",
                column: "PolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyMethodRules_TenantId_PolicySetId_Category_Method",
                table: "ProcurementPolicyMethodRules",
                columns: new[] { "TenantId", "PolicySetId", "Category", "Method" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyMethodRules_TenantId_PolicySetId_EffectiveFrom",
                table: "ProcurementPolicyMethodRules",
                columns: new[] { "TenantId", "PolicySetId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyMethodRules_TenantId_PolicySetId_RuleCode",
                table: "ProcurementPolicyMethodRules",
                columns: new[] { "TenantId", "PolicySetId", "RuleCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyMethodRules_WorkflowDefinitionId",
                table: "ProcurementPolicyMethodRules",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyRevisions_PolicySetId",
                table: "ProcurementPolicyRevisions",
                column: "PolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyRevisions_TenantId_CorrelationId",
                table: "ProcurementPolicyRevisions",
                columns: new[] { "TenantId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyRevisions_TenantId_PolicySetId_CreatedAt",
                table: "ProcurementPolicyRevisions",
                columns: new[] { "TenantId", "PolicySetId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicySets_BasePolicySetId",
                table: "ProcurementPolicySets",
                column: "BasePolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicySets_SourceConfigurationProfileId",
                table: "ProcurementPolicySets",
                column: "SourceConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicySets_SupersedesPolicySetId",
                table: "ProcurementPolicySets",
                column: "SupersedesPolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicySets_TenantId_Code_Version",
                table: "ProcurementPolicySets",
                columns: new[] { "TenantId", "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicySets_TenantId_LifecycleStatus_EffectiveFrom",
                table: "ProcurementPolicySets",
                columns: new[] { "TenantId", "LifecycleStatus", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicySets_TenantId_PolicyKey_LifecycleStatus",
                table: "ProcurementPolicySets",
                columns: new[] { "TenantId", "PolicyKey", "LifecycleStatus" },
                unique: true,
                filter: "[LifecycleStatus] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicySets_TenantId_PolicyKey_Version",
                table: "ProcurementPolicySets",
                columns: new[] { "TenantId", "PolicyKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicySets_TenantId_SourceConfigurationProfileId",
                table: "ProcurementPolicySets",
                columns: new[] { "TenantId", "SourceConfigurationProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicySodRules_PolicySetId",
                table: "ProcurementPolicySodRules",
                column: "PolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicySodRules_TenantId_PolicySetId_EffectiveFrom",
                table: "ProcurementPolicySodRules",
                columns: new[] { "TenantId", "PolicySetId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicySodRules_TenantId_PolicySetId_EntityType_Action",
                table: "ProcurementPolicySodRules",
                columns: new[] { "TenantId", "PolicySetId", "EntityType", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicySodRules_TenantId_PolicySetId_RuleCode",
                table: "ProcurementPolicySodRules",
                columns: new[] { "TenantId", "PolicySetId", "RuleCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyThresholdRules_PolicySetId",
                table: "ProcurementPolicyThresholdRules",
                column: "PolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyThresholdRules_TenantId_PolicySetId_Category_Method_CurrencyCode",
                table: "ProcurementPolicyThresholdRules",
                columns: new[] { "TenantId", "PolicySetId", "Category", "Method", "CurrencyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyThresholdRules_TenantId_PolicySetId_EffectiveFrom",
                table: "ProcurementPolicyThresholdRules",
                columns: new[] { "TenantId", "PolicySetId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPolicyThresholdRules_TenantId_PolicySetId_RuleCode",
                table: "ProcurementPolicyThresholdRules",
                columns: new[] { "TenantId", "PolicySetId", "RuleCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcurementPolicyAuthorityRules");

            migrationBuilder.DropTable(
                name: "ProcurementPolicyCategoryRules");

            migrationBuilder.DropTable(
                name: "ProcurementPolicyEvidenceRules");

            migrationBuilder.DropTable(
                name: "ProcurementPolicyExceptionRules");

            migrationBuilder.DropTable(
                name: "ProcurementPolicyMethodRules");

            migrationBuilder.DropTable(
                name: "ProcurementPolicyRevisions");

            migrationBuilder.DropTable(
                name: "ProcurementPolicySodRules");

            migrationBuilder.DropTable(
                name: "ProcurementPolicyThresholdRules");

            migrationBuilder.DropTable(
                name: "ProcurementPolicySets");
        }
    }
}
