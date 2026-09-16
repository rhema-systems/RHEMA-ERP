using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Installs TDC Finance close control set v3 for tenants that still use the system baseline.
/// The migration creates a new approved template version because previously approved close
/// definitions are immutable evidence. Custom active templates remain untouched as deliberate
/// tenant policy and can opt into the new providers through their normal maker-checker process.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260802203000_SeedFinanceCloseControlSetV3")]
public partial class SeedFinanceCloseControlSetV3 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @Baselines TABLE
            (
                CloseType nvarchar(20) NOT NULL,
                TemplateCode nvarchar(40) NOT NULL,
                TemplateName nvarchar(160) NOT NULL,
                DueDays int NOT NULL
            );

            INSERT INTO @Baselines (CloseType, TemplateCode, TemplateName, DueDays)
            VALUES
                (N'MonthEnd', N'TDC-MONTH-END', N'TDC month-end close', 5),
                (N'QuarterEnd', N'TDC-QUARTER-END', N'TDC quarter-end close', 10),
                (N'YearEnd', N'TDC-YEAR-END', N'TDC year-end close', 15);

            DECLARE @Targets TABLE
            (
                TenantId uniqueidentifier NOT NULL,
                CloseType nvarchar(20) NOT NULL,
                TemplateId uniqueidentifier NOT NULL,
                TemplateCode nvarchar(40) NOT NULL,
                TemplateName nvarchar(160) NOT NULL,
                DueDays int NOT NULL,
                TemplateVersion int NOT NULL
            );

            INSERT INTO @Targets
                (TenantId, CloseType, TemplateId, TemplateCode, TemplateName, DueDays, TemplateVersion)
            SELECT
                tenant.Id,
                baseline.CloseType,
                NEWID(),
                baseline.TemplateCode,
                baseline.TemplateName,
                baseline.DueDays,
                ISNULL((
                    SELECT MAX(existing.Version)
                    FROM FinanceCloseTemplates existing
                    WHERE existing.TenantId = tenant.Id
                      AND existing.CloseType = baseline.CloseType
                      AND existing.IsDeleted = 0
                ), 0) + 1
            FROM Tenants tenant
            CROSS JOIN @Baselines baseline
            WHERE tenant.Status = 1
              AND tenant.IsDeleted = 0
              -- A custom active template represents approved local policy and is never displaced by seed data.
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM FinanceCloseTemplates custom
                  WHERE custom.TenantId = tenant.Id
                    AND custom.CloseType = baseline.CloseType
                    AND custom.IsActive = 1
                    AND custom.IsSystemDefault = 0
                    AND custom.IsDeleted = 0
              )
              -- The full provider-code test makes replay idempotent without relying on a mutable version number.
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM FinanceCloseTemplates currentBaseline
                  WHERE currentBaseline.TenantId = tenant.Id
                    AND currentBaseline.CloseType = baseline.CloseType
                    AND currentBaseline.IsSystemDefault = 1
                    AND currentBaseline.Status = N'Approved'
                    AND currentBaseline.IsDeleted = 0
                    AND 11 =
                    (
                        SELECT COUNT(DISTINCT task.CheckCode)
                        FROM FinanceCloseTemplateTaskDefinitions task
                        WHERE task.FinanceCloseTemplateId = currentBaseline.Id
                          AND task.IsDeleted = 0
                          AND task.CheckCode IN
                          (
                              N'POSTING_INTEGRITY', N'TRIAL_BALANCE', N'RECURRING_JOURNAL_EXCEPTIONS',
                              N'AP_CONTROL_RECONCILIATION', N'AR_CONTROL_RECONCILIATION',
                              N'AP_UNAPPLIED_BALANCES', N'AR_UNAPPLIED_BALANCES',
                              N'BANK_RECONCILIATION', N'BUDGET_ADOPTION_REVIEW',
                              N'FIXED_ASSET_DEPRECIATION', N'FX_REVALUATION'
                          )
                    )
              );

            -- Supersession preserves the complete older definition for cycles already governed by it.
            UPDATE oldBaseline
            SET oldBaseline.IsActive = 0,
                oldBaseline.Status = N'Superseded',
                oldBaseline.SupersededAt = @Now,
                oldBaseline.UpdatedAt = @Now,
                oldBaseline.UpdatedBy = N'TDC system baseline'
            FROM FinanceCloseTemplates oldBaseline
            INNER JOIN @Targets target
                ON target.TenantId = oldBaseline.TenantId
               AND target.CloseType = oldBaseline.CloseType
            WHERE oldBaseline.IsSystemDefault = 1
              AND oldBaseline.IsActive = 1
              AND oldBaseline.IsDeleted = 0;

            INSERT INTO FinanceCloseTemplates
            (
                Id, TemplateCode, Name, CloseType, Version, Status, IsActive, IsSystemDefault,
                Description, ApprovedByUserId, ApprovedByUserName, ApprovedAt, ApprovalDeclaration,
                SupersededAt, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById,
                LastModifiedById, IsDeleted, DeletedAt, DeletedBy, TenantId
            )
            SELECT
                target.TemplateId,
                target.TemplateCode,
                target.TemplateName,
                target.CloseType,
                target.TemplateVersion,
                N'Approved',
                1,
                1,
                N'TDC baseline v3: Finance integrity, recurring-journal exceptions, budget-adoption review, subledger reconciliation and maker-checker certification.',
                NULL,
                N'TDC system baseline',
                @Now,
                N'System-provided TDC Finance close control set v3.',
                NULL,
                @Now,
                NULL,
                N'TDC system baseline',
                NULL,
                NULL,
                NULL,
                0,
                NULL,
                NULL,
                target.TenantId
            FROM @Targets target;

            INSERT INTO FinanceCloseTemplateTaskDefinitions
            (
                Id, FinanceCloseTemplateId, TaskCode, Title, Category, DependsOnTaskCode,
                CheckCode, Sequence, IsMandatory, IsAutomated, DueDaysAfterPeriodEnd,
                DefaultAssigneeUserId, Instructions, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy,
                CreatedById, LastModifiedById, IsDeleted, DeletedAt, DeletedBy, TenantId
            )
            SELECT
                NEWID(),
                target.TemplateId,
                task.TaskCode,
                task.Title,
                task.Category,
                task.DependsOnTaskCode,
                task.CheckCode,
                task.Sequence,
                task.IsMandatory,
                task.IsAutomated,
                target.DueDays,
                NULL,
                task.Instructions,
                @Now,
                NULL,
                N'TDC system baseline',
                NULL,
                NULL,
                NULL,
                0,
                NULL,
                NULL,
                target.TenantId
            FROM @Targets target
            CROSS APPLY
            (
                VALUES
                    (N'GL_POSTING_INTEGRITY', N'General ledger and posting integrity', N'General Ledger', 10, N'POSTING_INTEGRITY', CAST(NULL AS nvarchar(60)), CAST(1 AS bit), CAST(1 AS bit), CAST(NULL AS nvarchar(2000))),
                    (N'TRIAL_BALANCE', N'Trial balance and posted journal balance', N'General Ledger', 20, N'TRIAL_BALANCE', N'GL_POSTING_INTEGRITY', CAST(1 AS bit), CAST(1 AS bit), CAST(NULL AS nvarchar(2000))),
                    (N'RECURRING_JOURNAL_EXCEPTIONS', N'Resolve recurring-journal generation exceptions', N'General Ledger', 30, N'RECURRING_JOURNAL_EXCEPTIONS', N'GL_POSTING_INTEGRITY', CAST(1 AS bit), CAST(1 AS bit), N'Resolve, regenerate or complete the existing controlled waiver workflow for every due recurring-journal occurrence before close.'),
                    (N'AP_CONTROL_RECONCILIATION', N'Accounts payable control-account reconciliation', N'Accounts Payable', 40, N'AP_CONTROL_RECONCILIATION', N'TRIAL_BALANCE', CAST(1 AS bit), CAST(1 AS bit), N'Resolve all AP subledger-to-control-account differences and settlement diagnostics before close.'),
                    (N'AR_CONTROL_RECONCILIATION', N'Accounts receivable control-account reconciliation', N'Accounts Receivable', 50, N'AR_CONTROL_RECONCILIATION', N'TRIAL_BALANCE', CAST(1 AS bit), CAST(1 AS bit), N'Resolve all AR subledger-to-control-account differences and settlement diagnostics before close.'),
                    (N'AP_UNAPPLIED_BALANCES', N'Review unapplied supplier payments and advances', N'Accounts Payable', 60, N'AP_UNAPPLIED_BALANCES', N'AP_CONTROL_RECONCILIATION', CAST(1 AS bit), CAST(0 AS bit), N'Review supplier advances and unapplied vendor payments; retain follow-up references for unusual balances.'),
                    (N'AR_UNAPPLIED_BALANCES', N'Review unapplied customer receipts and advances', N'Accounts Receivable', 70, N'AR_UNAPPLIED_BALANCES', N'AR_CONTROL_RECONCILIATION', CAST(1 AS bit), CAST(0 AS bit), N'Review customer advances and unapplied receipts; retain allocation or refund follow-up references where required.'),
                    (N'BANK_RECONCILIATION', N'Cash and bank reconciliation', N'Cash & Bank', 80, N'BANK_RECONCILIATION', N'TRIAL_BALANCE', CAST(1 AS bit), CAST(1 AS bit), CAST(NULL AS nvarchar(2000))),
                    (N'BUDGET_ADOPTION_REVIEW', N'Review approved budgets awaiting adoption', N'Budgeting', 90, N'BUDGET_ADOPTION_REVIEW', N'TRIAL_BALANCE', CAST(1 AS bit), CAST(0 AS bit), N'Confirm whether each approved but inactive scenario should be adopted as the official budget or retained as a non-adopted planning scenario.'),
                    (N'FIXED_ASSET_DEPRECIATION', N'Fixed-asset depreciation', N'Fixed Assets', 100, N'FIXED_ASSET_DEPRECIATION', N'GL_POSTING_INTEGRITY', CAST(1 AS bit), CAST(1 AS bit), CAST(NULL AS nvarchar(2000))),
                    (N'FX_REVALUATION', N'Foreign-currency revaluation', N'Foreign Exchange', 110, N'FX_REVALUATION', N'GL_POSTING_INTEGRITY', CAST(1 AS bit), CAST(1 AS bit), CAST(NULL AS nvarchar(2000))),
                    (N'PREPARER_CERTIFICATION', N'Preparer declaration and evidence sign-off', N'Certification', 120, CAST(NULL AS nvarchar(60)), N'FX_REVALUATION', CAST(0 AS bit), CAST(1 AS bit), CAST(NULL AS nvarchar(2000)))
            ) task
            (
                TaskCode, Title, Category, Sequence, CheckCode, DependsOnTaskCode,
                IsAutomated, IsMandatory, Instructions
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            -- Used v3 templates are audit evidence, so rollback removes only unreferenced rows.
            DELETE seeded
            FROM FinanceCloseTemplates seeded
            WHERE seeded.IsSystemDefault = 1
              AND seeded.Description = N'TDC baseline v3: Finance integrity, recurring-journal exceptions, budget-adoption review, subledger reconciliation and maker-checker certification.'
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM FinanceCloseCycles cycle
                  WHERE cycle.FinanceCloseTemplateId = seeded.Id
              );

            ;WITH PreviousSystemBaseline AS
            (
                SELECT
                    candidate.Id,
                    ROW_NUMBER() OVER
                    (
                        PARTITION BY candidate.TenantId, candidate.CloseType
                        ORDER BY candidate.Version DESC
                    ) AS RowNumber
                FROM FinanceCloseTemplates candidate
                WHERE candidate.IsSystemDefault = 1
                  AND candidate.IsDeleted = 0
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM FinanceCloseTemplates activeTemplate
                      WHERE activeTemplate.TenantId = candidate.TenantId
                        AND activeTemplate.CloseType = candidate.CloseType
                        AND activeTemplate.IsActive = 1
                        AND activeTemplate.IsDeleted = 0
                  )
            )
            UPDATE candidate
            SET candidate.IsActive = 1,
                candidate.Status = N'Approved',
                candidate.SupersededAt = NULL,
                candidate.UpdatedAt = @Now,
                candidate.UpdatedBy = N'TDC system baseline rollback'
            FROM FinanceCloseTemplates candidate
            INNER JOIN PreviousSystemBaseline previous
                ON previous.Id = candidate.Id
               AND previous.RowNumber = 1;
            """);
    }
}
