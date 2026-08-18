using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantitySurveyAdvanceRecoveryLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "QuantitySurveyAdvanceRecoveryAgreementId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "RetentionReleasedBefore",
                table: "ProcurementWorksCloseoutActions",
                type: "decimal(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "RetentionReleasedAfter",
                table: "ProcurementWorksCloseoutActions",
                type: "decimal(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "RetentionHeldSnapshot",
                table: "ProcurementWorksCloseoutActions",
                type: "decimal(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "QuantitySurveyAdvanceRecoveryAgreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    LastMutationClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastMutationRequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    RecoveryNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ContractNumberSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContractorNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PaymentNumberSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaymentDateSnapshot = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrencyCodeSnapshot = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OriginalAdvanceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RecoveryPercentage = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValuationDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    PreparedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyAdvanceRecoveryAgreements", x => x.Id);
                    table.CheckConstraint("CK_QsAdvanceRecovery_Amounts", "[OriginalAdvanceAmount] > 0 AND [RecoveryPercentage] > 0 AND [RecoveryPercentage] <= 100");
                    table.CheckConstraint("CK_QsAdvanceRecovery_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                    table.CheckConstraint("CK_QsAdvanceRecovery_State", "([Status] = 'Draft' AND [ApprovalStatus] = 'Draft' AND [ApprovedAt] IS NULL) OR ([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending' AND [SubmittedById] IS NOT NULL AND [SubmittedAt] IS NOT NULL AND [ApprovedAt] IS NULL) OR ([Status] IN ('Approved','Closed') AND [ApprovalStatus] = 'Approved' AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL) OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected' AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [RejectionReason] IS NOT NULL)");
                    table.CheckConstraint("CK_QsAdvanceRecovery_Status", "[Status] IN ('Draft','PendingApproval','Approved','Rejected','Closed')");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyAdvanceRecoveryAgreements_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyAdvanceRecoveryAgreements_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyAdvanceRecoveryAgreements_QuantitySurveyConfigurationDecisions_ValuationDecisionId",
                        column: x => x.ValuationDecisionId,
                        principalTable: "QuantitySurveyConfigurationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyAdvanceRecoveryAgreements_QuantitySurveyConfigurationProfiles_ConfigurationProfileId",
                        column: x => x.ConfigurationProfileId,
                        principalTable: "QuantitySurveyConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyAdvanceRecoveryAgreements_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyAdvanceRecoveryAgreements_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyAdvanceRecoveryAgreements_Users_PreparedById",
                        column: x => x.PreparedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyAdvanceRecoveryAgreements_Users_SubmittedById",
                        column: x => x.SubmittedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyAdvanceRecoveryAgreements_VendorPayment_VendorPaymentId",
                        column: x => x.VendorPaymentId,
                        principalTable: "VendorPayment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyAdvanceRecoveryRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyAdvanceRecoveryRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyAdvanceRecoveryRevisions_QuantitySurveyAdvanceRecoveryAgreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "QuantitySurveyAdvanceRecoveryAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyAdvanceRecoveryRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyAdvanceRecoveryRevisions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_QuantitySurveyAdvanceRecoveryAgreementId",
                table: "ProjectPaymentCertificates",
                column: "QuantitySurveyAdvanceRecoveryAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_TenantId_QuantitySurveyAdvanceRecoveryAgreementId_Status",
                table: "ProjectPaymentCertificates",
                columns: new[] { "TenantId", "QuantitySurveyAdvanceRecoveryAgreementId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_QsAdvanceRecovery_Tenant_ClientRequest",
                table: "QuantitySurveyAdvanceRecoveryAgreements",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QsAdvanceRecovery_Tenant_Number",
                table: "QuantitySurveyAdvanceRecoveryAgreements",
                columns: new[] { "TenantId", "RecoveryNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QsAdvanceRecovery_Tenant_VendorPayment_Active",
                table: "QuantitySurveyAdvanceRecoveryAgreements",
                columns: new[] { "TenantId", "VendorPaymentId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] <> 'Rejected'");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyAdvanceRecoveryAgreements_ApprovedById",
                table: "QuantitySurveyAdvanceRecoveryAgreements",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyAdvanceRecoveryAgreements_ConfigurationProfileId",
                table: "QuantitySurveyAdvanceRecoveryAgreements",
                column: "ConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyAdvanceRecoveryAgreements_ContractId",
                table: "QuantitySurveyAdvanceRecoveryAgreements",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyAdvanceRecoveryAgreements_PreparedById",
                table: "QuantitySurveyAdvanceRecoveryAgreements",
                column: "PreparedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyAdvanceRecoveryAgreements_ProjectId",
                table: "QuantitySurveyAdvanceRecoveryAgreements",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyAdvanceRecoveryAgreements_SubmittedById",
                table: "QuantitySurveyAdvanceRecoveryAgreements",
                column: "SubmittedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyAdvanceRecoveryAgreements_TenantId_ProjectId_ContractId_Status",
                table: "QuantitySurveyAdvanceRecoveryAgreements",
                columns: new[] { "TenantId", "ProjectId", "ContractId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyAdvanceRecoveryAgreements_ValuationDecisionId",
                table: "QuantitySurveyAdvanceRecoveryAgreements",
                column: "ValuationDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyAdvanceRecoveryAgreements_VendorPaymentId",
                table: "QuantitySurveyAdvanceRecoveryAgreements",
                column: "VendorPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyAdvanceRecoveryRevisions_ActorUserId",
                table: "QuantitySurveyAdvanceRecoveryRevisions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyAdvanceRecoveryRevisions_AgreementId",
                table: "QuantitySurveyAdvanceRecoveryRevisions",
                column: "AgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyAdvanceRecoveryRevisions_TenantId_AgreementId_CreatedAt",
                table: "QuantitySurveyAdvanceRecoveryRevisions",
                columns: new[] { "TenantId", "AgreementId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyAdvanceRecoveryRevisions_TenantId_CorrelationId",
                table: "QuantitySurveyAdvanceRecoveryRevisions",
                columns: new[] { "TenantId", "CorrelationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_QuantitySurveyAdvanceRecoveryAgreements_QuantitySurveyAdvanceRecoveryAgreementId",
                table: "ProjectPaymentCertificates",
                column: "QuantitySurveyAdvanceRecoveryAgreementId",
                principalTable: "QuantitySurveyAdvanceRecoveryAgreements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_QsAdvanceRecoveryAgreements_QS0505Guard]
                ON [dbo].[QuantitySurveyAdvanceRecoveryAgreements]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id] = d.[Id] WHERE i.[Id] IS NULL)
                        THROW 55051, 'QS-0505 advance-recovery agreements cannot be deleted.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE i.[TenantId] <> d.[TenantId]
                           OR i.[ProjectId] <> d.[ProjectId]
                           OR i.[ContractId] <> d.[ContractId]
                           OR i.[VendorPaymentId] <> d.[VendorPaymentId]
                           OR i.[ClientRequestId] <> d.[ClientRequestId]
                           OR i.[RequestHash] <> d.[RequestHash]
                           OR i.[RecoveryNumber] <> d.[RecoveryNumber]
                           OR i.[ContractNumberSnapshot] <> d.[ContractNumberSnapshot]
                           OR i.[ContractorNameSnapshot] <> d.[ContractorNameSnapshot]
                           OR i.[PaymentNumberSnapshot] <> d.[PaymentNumberSnapshot]
                           OR i.[PaymentDateSnapshot] <> d.[PaymentDateSnapshot]
                           OR i.[CurrencyCodeSnapshot] <> d.[CurrencyCodeSnapshot]
                           OR i.[OriginalAdvanceAmount] <> d.[OriginalAdvanceAmount]
                           OR i.[RecoveryPercentage] <> d.[RecoveryPercentage]
                           OR i.[ConfigurationProfileId] <> d.[ConfigurationProfileId]
                           OR i.[ValuationDecisionId] <> d.[ValuationDecisionId]
                           OR i.[PolicyHash] <> d.[PolicyHash]
                           OR i.[PreparedById] <> d.[PreparedById]
                           OR i.[PreparedAt] <> d.[PreparedAt]
                           OR i.[CorrelationId] <> d.[CorrelationId]
                           OR i.[IsDeleted] <> d.[IsDeleted]
                    )
                        THROW 55052, 'QS-0505 source, commercial terms, policy lineage and preparer evidence are immutable.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[Projects] p
                          ON p.[Id] = i.[ProjectId] AND p.[TenantId] = i.[TenantId] AND p.[IsDeleted] = 0
                        LEFT JOIN [dbo].[Contracts] c
                          ON c.[Id] = i.[ContractId] AND c.[TenantId] = i.[TenantId] AND c.[IsDeleted] = 0
                         AND UPPER(c.[ContractType]) = 'WORKS' AND UPPER(c.[Status]) = 'ACTIVE'
                        LEFT JOIN [dbo].[BusinessPartners] bp
                          ON bp.[Id] = c.[BusinessPartnerId] AND bp.[TenantId] = i.[TenantId] AND bp.[IsDeleted] = 0
                        LEFT JOIN [dbo].[VendorPayment] vp
                          ON vp.[Id] = i.[VendorPaymentId] AND vp.[TenantId] = i.[TenantId] AND vp.[IsDeleted] = 0
                         AND vp.[IsSupplierAdvance] = 1 AND vp.[JournalEntryId] IS NOT NULL
                         AND vp.[Status] NOT IN (6, 7, 9)
                        LEFT JOIN [dbo].[Suppliers] s
                          ON s.[Id] = vp.[SupplierId] AND s.[TenantId] = i.[TenantId] AND s.[IsDeleted] = 0
                        LEFT JOIN [dbo].[QuantitySurveyConfigurationProfiles] cp
                          ON cp.[Id] = i.[ConfigurationProfileId] AND cp.[TenantId] = i.[TenantId]
                         AND cp.[LifecycleStatus] = 1 AND cp.[IsDeleted] = 0
                         AND cp.[EffectiveFrom] <= i.[PreparedAt]
                         AND (cp.[EffectiveTo] IS NULL OR cp.[EffectiveTo] >= i.[PreparedAt])
                        LEFT JOIN [dbo].[QuantitySurveyConfigurationDecisions] cd
                          ON cd.[Id] = i.[ValuationDecisionId] AND cd.[TenantId] = i.[TenantId]
                         AND cd.[ProfileId] = cp.[Id] AND cd.[DecisionKey] = 'QS-DEC-008'
                         AND cd.[Status] = 2 AND cd.[ApprovalStatus] = 1 AND cd.[EvidenceStatus] = 2
                         AND cd.[IsDeleted] = 0
                         AND (cd.[EffectiveFrom] IS NULL OR cd.[EffectiveFrom] <= i.[PreparedAt])
                         AND (cd.[EffectiveTo] IS NULL OR cd.[EffectiveTo] >= i.[PreparedAt])
                        WHERE p.[Id] IS NULL OR c.[Id] IS NULL OR bp.[Id] IS NULL OR vp.[Id] IS NULL OR s.[Id] IS NULL
                           OR NOT
                              (
                                  s.[Id] = bp.[Id]
                                  OR (NULLIF(LTRIM(RTRIM(s.[SupplierCode])), '') IS NOT NULL
                                      AND s.[SupplierCode] = bp.[PartnerCode])
                                  OR (NULLIF(LTRIM(RTRIM(s.[Name])), '') IS NOT NULL AND s.[Name] = bp.[PartnerName])
                              )
                           OR UPPER(vp.[CurrencyCode]) <> UPPER(c.[Currency])
                           OR UPPER(i.[CurrencyCodeSnapshot]) <> UPPER(vp.[CurrencyCode])
                           OR i.[OriginalAdvanceAmount] <> vp.[TotalAmount]
                           OR vp.[TotalAmount] <= vp.[AllocatedAmount]
                           OR cp.[Id] IS NULL OR cd.[Id] IS NULL
                           OR NOT EXISTS
                              (
                                  SELECT 1 FROM [dbo].[ProjectInterimValuations] v
                                  WHERE v.[TenantId] = i.[TenantId] AND v.[ProjectId] = i.[ProjectId]
                                    AND v.[ContractId] = i.[ContractId] AND v.[IsDeleted] = 0
                                  UNION ALL
                                  SELECT 1 FROM [dbo].[ProjectPaymentCertificates] pc
                                  WHERE pc.[TenantId] = i.[TenantId] AND pc.[ProjectId] = i.[ProjectId]
                                    AND pc.[ContractId] = i.[ContractId] AND pc.[IsDeleted] = 0
                              )
                    )
                        THROW 55053, 'QS-0505 requires same-tenant project, active Works contract, Finance-owned posted supplier advance and DEC-008 lineage.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE (d.[Id] IS NULL AND (i.[Status] <> 'Draft' OR i.[ApprovalStatus] <> 'Draft'))
                           OR (d.[Id] IS NOT NULL AND i.[Status] <> d.[Status] AND NOT
                              (
                                  (d.[Status] = 'Draft' AND i.[Status] = 'PendingApproval')
                                  OR (d.[Status] = 'PendingApproval' AND i.[Status] IN ('Approved', 'Rejected'))
                                  OR (d.[Status] = 'Approved' AND i.[Status] = 'Closed')
                              ))
                           OR (i.[Status] IN ('Approved', 'Rejected') AND
                              (i.[ApprovedById] = i.[PreparedById] OR i.[ApprovedById] = i.[SubmittedById]))
                    )
                        THROW 55054, 'QS-0505 lifecycle transition or maker-checker separation is invalid.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProjectPaymentCertificates_QS0505AdvanceRecoveryGuard]
                ON [dbo].[ProjectPaymentCertificates]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE d.[Id] IS NOT NULL
                          AND
                          (
                              ISNULL(i.[QuantitySurveyAdvanceRecoveryAgreementId], '00000000-0000-0000-0000-000000000000')
                                <> ISNULL(d.[QuantitySurveyAdvanceRecoveryAgreementId], '00000000-0000-0000-0000-000000000000')
                              OR i.[AdvanceRecoveryAmount] <> d.[AdvanceRecoveryAmount]
                          )
                    )
                        THROW 55055, 'QS-0505 certificate advance-recovery agreement and amount are immutable.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        LEFT JOIN [dbo].[QuantitySurveyAdvanceRecoveryAgreements] a
                          ON a.[Id] = i.[QuantitySurveyAdvanceRecoveryAgreementId]
                         AND a.[TenantId] = i.[TenantId]
                         AND a.[ProjectId] = i.[ProjectId]
                         AND a.[ContractId] = i.[ContractId]
                         AND a.[Status] IN ('Approved', 'Closed')
                         AND a.[IsDeleted] = 0
                        OUTER APPLY
                        (
                            SELECT ISNULL(SUM(pc.[AdvanceRecoveryAmount]), 0) AS [CommittedBefore]
                            FROM [dbo].[ProjectPaymentCertificates] pc
                            WHERE pc.[TenantId] = i.[TenantId]
                              AND pc.[QuantitySurveyAdvanceRecoveryAgreementId] = i.[QuantitySurveyAdvanceRecoveryAgreementId]
                              AND pc.[Id] <> i.[Id]
                              AND pc.[Status] <> 'Cancelled'
                              AND pc.[IsDeleted] = 0
                        ) totals
                        WHERE
                            (d.[Id] IS NULL OR i.[AdvanceRecoveryAmount] <> d.[AdvanceRecoveryAmount]
                             OR ISNULL(i.[QuantitySurveyAdvanceRecoveryAgreementId], '00000000-0000-0000-0000-000000000000')
                                <> ISNULL(d.[QuantitySurveyAdvanceRecoveryAgreementId], '00000000-0000-0000-0000-000000000000'))
                            AND
                            (
                            (
                                (
                                    (i.[AdvanceRecoveryAmount] = 0 AND i.[QuantitySurveyAdvanceRecoveryAgreementId] IS NOT NULL)
                                    OR (i.[AdvanceRecoveryAmount] > 0 AND i.[QuantitySurveyAdvanceRecoveryAgreementId] IS NULL)
                                )
                            )
                            OR
                            (
                                i.[QuantitySurveyAdvanceRecoveryAgreementId] IS NOT NULL
                                AND
                                (
                                    i.[AdvanceRecoveryAmount] <= 0 OR a.[Id] IS NULL
                                    OR totals.[CommittedBefore] + i.[AdvanceRecoveryAmount] > a.[OriginalAdvanceAmount] + 0.01
                                    OR ABS
                                       (
                                           i.[AdvanceRecoveryAmount] -
                                           CASE
                                               WHEN a.[OriginalAdvanceAmount] - totals.[CommittedBefore]
                                                    < ROUND(i.[GrossCertifiedAmount] * a.[RecoveryPercentage] / 100.0, 2)
                                               THEN a.[OriginalAdvanceAmount] - totals.[CommittedBefore]
                                               ELSE ROUND(i.[GrossCertifiedAmount] * a.[RecoveryPercentage] / 100.0, 2)
                                           END
                                       ) > 0.01
                                )
                            )
                            )
                    )
                        THROW 55056, 'QS-0505 certificate recovery must use the approved same-contract agreement, governed percentage and remaining balance.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_QsAdvanceRecoveryRevisions_QS0505AppendOnly]
                ON [dbo].[QuantitySurveyAdvanceRecoveryRevisions]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 55057, 'QS-0505 advance-recovery audit revisions are append-only.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QsAdvanceRecoveryRevisions_QS0505AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectPaymentCertificates_QS0505AdvanceRecoveryGuard];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QsAdvanceRecoveryAgreements_QS0505Guard];");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_QuantitySurveyAdvanceRecoveryAgreements_QuantitySurveyAdvanceRecoveryAgreementId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropTable(
                name: "QuantitySurveyAdvanceRecoveryRevisions");

            migrationBuilder.DropTable(
                name: "QuantitySurveyAdvanceRecoveryAgreements");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_QuantitySurveyAdvanceRecoveryAgreementId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_TenantId_QuantitySurveyAdvanceRecoveryAgreementId_Status",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "QuantitySurveyAdvanceRecoveryAgreementId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.AlterColumn<decimal>(
                name: "RetentionReleasedBefore",
                table: "ProcurementWorksCloseoutActions",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "RetentionReleasedAfter",
                table: "ProcurementWorksCloseoutActions",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "RetentionHeldSnapshot",
                table: "ProcurementWorksCloseoutActions",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldNullable: true);
        }
    }
}
