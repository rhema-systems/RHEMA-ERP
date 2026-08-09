using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260809173500_AllowControlledSupplierApplicantContactCorrection")]
public partial class AllowControlledSupplierApplicantContactCorrection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(ControlledTriggerSql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(BaselineTriggerSql);
    }

    private const string ControlledTriggerSql =
        """
        CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementSupplierApplicantAccesses_Protected]
        ON [dbo].[ProcurementSupplierApplicantAccesses]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            DECLARE @correctionAccessId uniqueidentifier =
                TRY_CONVERT(uniqueidentifier,
                    SESSION_CONTEXT(N'TDC_SUPPLIER_CONTACT_ACCESS_ID'));
            DECLARE @correctionActorId uniqueidentifier =
                TRY_CONVERT(uniqueidentifier,
                    SESSION_CONTEXT(N'TDC_SUPPLIER_CONTACT_ACTOR_ID'));
            DECLARE @correctionContactHash nvarchar(64) =
                TRY_CONVERT(nvarchar(64),
                    SESSION_CONTEXT(N'TDC_SUPPLIER_CONTACT_HASH'));

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
                   OR i.NotificationAttemptCount < d.NotificationAttemptCount
                   OR (
                       (i.VerifiedChannel <> d.VerifiedChannel
                        OR i.VerifiedContactHashSha256 <> d.VerifiedContactHashSha256
                        OR i.VerifiedContactMasked <> d.VerifiedContactMasked
                        OR i.VerifiedContact <> d.VerifiedContact
                        OR i.VerifiedAtUtc <> d.VerifiedAtUtc)
                       AND NOT (
                           @correctionAccessId = i.Id
                           AND @correctionActorId IS NOT NULL
                           AND @correctionContactHash = i.VerifiedContactHashSha256
                           AND LEN(i.VerifiedContactHashSha256) = 64
                           AND LEN(LTRIM(RTRIM(i.VerifiedContact))) > 0
                           AND LEN(LTRIM(RTRIM(i.VerifiedContactMasked))) > 0
                           AND i.VerifiedAtUtc > d.VerifiedAtUtc
                           AND d.Status IN (1, 5)
                           AND i.Status = d.Status
                           AND d.ApprovedUserId IS NULL
                           AND i.ApprovedUserId IS NULL
                           AND d.BusinessPartnerId IS NOT NULL
                           AND i.BusinessPartnerId = d.BusinessPartnerId
                           AND d.TerminalAtUtc IS NOT NULL
                           AND i.TerminalAtUtc = d.TerminalAtUtc
                           AND ISNULL(i.TerminalOutcome, '') =
                               ISNULL(d.TerminalOutcome, '')
                           AND EXISTS (
                               SELECT 1
                               FROM dbo.Users actor
                               WHERE actor.Id = @correctionActorId
                                 AND actor.TenantId = i.TenantId
                                 AND actor.IsActive = 1)
                           AND EXISTS (
                               SELECT 1
                               FROM dbo.BusinessPartnerRegistrations registration
                               WHERE registration.Id = i.RegistrationId
                                 AND registration.TenantId = i.TenantId
                                 AND registration.Status = 'Approved'
                                 AND registration.BusinessPartnerId =
                                     i.BusinessPartnerId))))
                THROW 51831, 'Supplier-applicant verification, subject, terminal, and notification lineage is immutable outside a verified contact-correction transaction.', 1;

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
        """;

    private const string BaselineTriggerSql =
        """
        CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementSupplierApplicantAccesses_Protected]
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
        """;
}
