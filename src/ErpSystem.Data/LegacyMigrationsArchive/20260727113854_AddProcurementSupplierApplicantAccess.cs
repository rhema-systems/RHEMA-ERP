using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementSupplierApplicantAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangedAtUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TemporaryPasswordExpiresAtUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProcurementSupplierApplicantAccesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VerifiedChannel = table.Column<int>(type: "int", nullable: false),
                    VerifiedContactHashSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VerifiedContactMasked = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VerifiedContact = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedIdentityRolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovedBusinessPartnerRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LoginIdentifier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TemporaryCredentialIssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TemporaryCredentialExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CredentialActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastNotificationAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotificationAttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastNotificationStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastNotificationFailure = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TerminalAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TerminalOutcome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSupplierApplicantAccesses", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSupplierApplicantAccesses_State", "[VerifiedChannel] BETWEEN 0 AND 1 AND [Status] BETWEEN 0 AND 5 AND LEN([VerifiedContactHashSha256]) = 64 AND LEN([IntegrityHash]) = 64 AND [NotificationAttemptCount] >= 0 AND ISJSON([ApprovedIdentityRolesJson]) = 1 AND (([Status] = 0 AND [TerminalOutcome] IS NULL AND [TerminalAtUtc] IS NULL) OR ([Status] = 4 AND [TerminalOutcome] = 'Rejected' AND [TerminalAtUtc] IS NOT NULL) OR ([Status] IN (1, 2, 3, 5) AND [TerminalOutcome] = 'Approved' AND [TerminalAtUtc] IS NOT NULL)) AND (([ApprovedUserId] IS NULL AND [BusinessPartnerId] IS NULL) OR ([ApprovedUserId] IS NOT NULL AND [BusinessPartnerId] IS NOT NULL AND [LoginIdentifier] IS NOT NULL AND [TemporaryCredentialIssuedAtUtc] IS NOT NULL AND [TemporaryCredentialExpiresAtUtc] >= [TemporaryCredentialIssuedAtUtc])) AND ([Status] NOT IN (2, 3, 5) OR [ApprovedUserId] IS NOT NULL) AND (([Status] = 3 AND [CredentialActivatedAtUtc] IS NOT NULL) OR ([Status] <> 3 AND [CredentialActivatedAtUtc] IS NULL))");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierApplicantAccesses_BusinessPartnerRegistrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "BusinessPartnerRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierApplicantAccesses_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierApplicantAccesses_ProcurementSupplierOnboardingTokens_TokenId",
                        column: x => x.TokenId,
                        principalTable: "ProcurementSupplierOnboardingTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierApplicantAccesses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierApplicantAccesses_Users_ApprovedUserId",
                        column: x => x.ApprovedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementSupplierApplicantSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicantAccessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionReference = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSupplierApplicantSessions", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSupplierApplicantSessions_State", "[Status] BETWEEN 0 AND 1 AND [ExpiresAtUtc] > [IssuedAtUtc] AND LEN([IntegrityHash]) = 64 AND (([Status] = 0 AND [RevokedAtUtc] IS NULL) OR ([Status] = 1 AND [RevokedAtUtc] IS NOT NULL AND [RevocationReason] IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierApplicantSessions_ProcurementSupplierApplicantAccesses_ApplicantAccessId",
                        column: x => x.ApplicantAccessId,
                        principalTable: "ProcurementSupplierApplicantAccesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierApplicantSessions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierApplicantAccesses_ApprovedUserId",
                table: "ProcurementSupplierApplicantAccesses",
                column: "ApprovedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierApplicantAccesses_BusinessPartnerId",
                table: "ProcurementSupplierApplicantAccesses",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierApplicantAccesses_RegistrationId",
                table: "ProcurementSupplierApplicantAccesses",
                column: "RegistrationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierApplicantAccesses_TenantId_ApprovedUserId",
                table: "ProcurementSupplierApplicantAccesses",
                columns: new[] { "TenantId", "ApprovedUserId" },
                unique: true,
                filter: "[ApprovedUserId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierApplicantAccesses_TenantId_RegistrationId",
                table: "ProcurementSupplierApplicantAccesses",
                columns: new[] { "TenantId", "RegistrationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierApplicantAccesses_TenantId_TokenId",
                table: "ProcurementSupplierApplicantAccesses",
                columns: new[] { "TenantId", "TokenId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierApplicantAccesses_TenantId_VerifiedContactHashSha256_Status",
                table: "ProcurementSupplierApplicantAccesses",
                columns: new[] { "TenantId", "VerifiedContactHashSha256", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierApplicantAccesses_TokenId",
                table: "ProcurementSupplierApplicantAccesses",
                column: "TokenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierApplicantSessions_ApplicantAccessId",
                table: "ProcurementSupplierApplicantSessions",
                column: "ApplicantAccessId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierApplicantSessions_TenantId_ApplicantAccessId_Status",
                table: "ProcurementSupplierApplicantSessions",
                columns: new[] { "TenantId", "ApplicantAccessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierApplicantSessions_TenantId_SessionReference",
                table: "ProcurementSupplierApplicantSessions",
                columns: new[] { "TenantId", "SessionReference" },
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_ProcurementSupplierApplicantAccesses_Protected]
                ON [dbo].[ProcurementSupplierApplicantAccesses]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1 FROM deleted d
                        LEFT JOIN inserted i ON i.Id = d.Id
                        WHERE i.Id IS NULL)
                        THROW 51830, 'Supplier-applicant access records cannot be physically deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId
                           OR i.RegistrationId <> d.RegistrationId
                           OR i.TokenId <> d.TokenId
                           OR i.VerifiedChannel <> d.VerifiedChannel
                           OR i.VerifiedContactHashSha256 <> d.VerifiedContactHashSha256
                           OR i.VerifiedContactMasked <> d.VerifiedContactMasked
                           OR i.VerifiedContact <> d.VerifiedContact
                           OR i.VerifiedAtUtc <> d.VerifiedAtUtc
                           OR i.CreatedAt <> d.CreatedAt
                           OR ISNULL(i.CreatedBy, '') <> ISNULL(d.CreatedBy, '')
                           OR ISNULL(i.CreatedById, '00000000-0000-0000-0000-000000000000')
                              <> ISNULL(d.CreatedById, '00000000-0000-0000-0000-000000000000')
                           OR i.IsDeleted <> d.IsDeleted
                           OR (d.ApprovedUserId IS NOT NULL
                               AND ISNULL(i.ApprovedUserId, '00000000-0000-0000-0000-000000000000')
                                   <> d.ApprovedUserId)
                           OR (d.BusinessPartnerId IS NOT NULL
                               AND ISNULL(i.BusinessPartnerId, '00000000-0000-0000-0000-000000000000')
                                   <> d.BusinessPartnerId)
                           OR (d.TerminalAtUtc IS NOT NULL
                               AND (ISNULL(i.TerminalAtUtc, '19000101') <> d.TerminalAtUtc
                                    OR ISNULL(i.TerminalOutcome, '') <> ISNULL(d.TerminalOutcome, '')))
                           OR i.NotificationAttemptCount < d.NotificationAttemptCount)
                        THROW 51831, 'Supplier-applicant verification, subject, terminal, and notification lineage is immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE NOT (
                            i.Status = d.Status
                            OR (d.Status = 0 AND i.Status IN (1, 4))
                            OR (d.Status = 1 AND i.Status IN (2, 5))
                            OR (d.Status = 2 AND i.Status IN (1, 3, 5))
                            OR (d.Status = 5 AND i.Status = 1)))
                        THROW 51832, 'Invalid supplier-applicant access lifecycle transition.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN BusinessPartnerRegistrations r
                          ON r.Id = i.RegistrationId AND r.TenantId = i.TenantId
                        LEFT JOIN ProcurementSupplierOnboardingTokens t
                          ON t.Id = i.TokenId
                         AND t.TenantId = i.TenantId
                         AND t.RegistrationId = i.RegistrationId
                        WHERE r.Id IS NULL OR t.Id IS NULL)
                        THROW 51833, 'Supplier-applicant registration and token tenant lineage is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN BusinessPartnerRegistrations r
                          ON r.Id = i.RegistrationId AND r.TenantId = i.TenantId
                        JOIN ProcurementSupplierOnboardingTokens t
                          ON t.Id = i.TokenId AND t.TenantId = i.TenantId
                        LEFT JOIN Users u
                          ON u.Id = i.ApprovedUserId AND u.TenantId = i.TenantId
                        LEFT JOIN BusinessPartners bp
                          ON bp.Id = i.BusinessPartnerId AND bp.TenantId = i.TenantId
                        WHERE (i.Status = 4
                               AND (r.Status <> 'Rejected' OR t.Status <> 2
                                    OR i.ApprovedUserId IS NOT NULL
                                    OR i.BusinessPartnerId IS NOT NULL))
                           OR (i.Status IN (1, 2, 3, 5)
                               AND (r.Status <> 'Approved' OR t.Status <> 2
                                    OR (i.ApprovedUserId IS NOT NULL AND u.Id IS NULL)
                                    OR (i.BusinessPartnerId IS NOT NULL
                                        AND (bp.Id IS NULL
                                             OR r.BusinessPartnerId <> i.BusinessPartnerId)))))
                        THROW 51834, 'Supplier-applicant terminal outcome and approved identity lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_ProcurementSupplierApplicantSessions_Protected]
                ON [dbo].[ProcurementSupplierApplicantSessions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1 FROM deleted d
                        LEFT JOIN inserted i ON i.Id = d.Id
                        WHERE i.Id IS NULL)
                        THROW 51835, 'Supplier-applicant sessions cannot be physically deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId
                           OR i.ApplicantAccessId <> d.ApplicantAccessId
                           OR i.SessionReference <> d.SessionReference
                           OR i.IssuedAtUtc <> d.IssuedAtUtc
                           OR i.ExpiresAtUtc <> d.ExpiresAtUtc
                           OR i.CreatedAt <> d.CreatedAt
                           OR ISNULL(i.CreatedBy, '') <> ISNULL(d.CreatedBy, '')
                           OR ISNULL(i.CreatedById, '00000000-0000-0000-0000-000000000000')
                              <> ISNULL(d.CreatedById, '00000000-0000-0000-0000-000000000000')
                           OR i.IsDeleted <> d.IsDeleted
                           OR (d.LastUsedAtUtc IS NOT NULL
                               AND (i.LastUsedAtUtc IS NULL OR i.LastUsedAtUtc < d.LastUsedAtUtc))
                           OR (d.RevokedAtUtc IS NOT NULL
                               AND (ISNULL(i.RevokedAtUtc, '19000101') <> d.RevokedAtUtc
                                    OR ISNULL(i.RevocationReason, '') <> ISNULL(d.RevocationReason, ''))))
                        THROW 51836, 'Supplier-applicant session identity and revocation lineage is immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE NOT (
                            i.Status = d.Status
                            OR (d.Status = 0 AND i.Status = 1)))
                        THROW 51837, 'Invalid supplier-applicant session lifecycle transition.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.Id = i.Id
                        LEFT JOIN ProcurementSupplierApplicantAccesses a
                          ON a.Id = i.ApplicantAccessId AND a.TenantId = i.TenantId
                        LEFT JOIN BusinessPartnerRegistrations r
                          ON r.Id = a.RegistrationId AND r.TenantId = i.TenantId
                        LEFT JOIN ProcurementSupplierOnboardingTokens t
                          ON t.Id = a.TokenId AND t.TenantId = i.TenantId
                        WHERE a.Id IS NULL OR r.Id IS NULL OR t.Id IS NULL
                           OR (d.Id IS NULL AND
                               (i.Status <> 0 OR a.Status <> 0 OR t.Status = 2
                                OR r.Status IN ('Approved', 'Rejected'))))
                        THROW 51838, 'Supplier-applicant session tenant or active-application lineage is invalid.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementSupplierApplicantSessions_Protected];");
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementSupplierApplicantAccesses_Protected];");

            migrationBuilder.DropTable(
                name: "ProcurementSupplierApplicantSessions");

            migrationBuilder.DropTable(
                name: "ProcurementSupplierApplicantAccesses");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordChangedAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TemporaryPasswordExpiresAtUtc",
                table: "Users");
        }
    }
}
