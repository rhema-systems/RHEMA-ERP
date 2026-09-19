using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260906030000_AlignLegacyAwardReadinessWithLockedCurrentEvaluations")]
public sealed class AlignLegacyAwardReadinessWithLockedCurrentEvaluations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Derive the exact applied baseline fragment without modifying that migration.
        // Only the legacy branch changes; statutory/RFQ/exceptional, envelope,
        // append-only, tenant, and independent-authority guards remain byte-for-byte.
        var baseline = new AddProcurementAwardReadinessControls().UpOperations
            .OfType<SqlOperation>().Single(item => item.Sql.Contains(
                "CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementAwardReadinessDecisions_Immutable]",
                StringComparison.Ordinal)).Sql;
        const string legacyStart = "OR (i.SourceType = 1 AND NOT EXISTS (";
        const string exceptionalStart = "OR (i.SourceType = 2 AND NOT EXISTS (";
        var start = baseline.IndexOf(legacyStart, StringComparison.Ordinal);
        if (start < 0)
            throw new InvalidOperationException("The canonical award-readiness legacy guard could not be located.");
        var end = baseline.IndexOf(exceptionalStart, start, StringComparison.Ordinal);
        var checkStart = baseline.LastIndexOf("IF EXISTS (", start, StringComparison.Ordinal);
        if (end <= start || checkStart < 0)
            throw new InvalidOperationException("The canonical award-readiness legacy guard could not be located.");
        const string rfqStart = "(i.SourceType = 0 AND (";
        var prefixEnd = baseline.IndexOf(rfqStart, checkStart, StringComparison.Ordinal) + rfqStart.Length;
        if (start < 0 || end <= start || checkStart < 0 || prefixEnd <= checkStart)
            throw new InvalidOperationException("The canonical award-readiness legacy guard could not be located.");
        var original = baseline[start..end];
        var prefix = baseline[checkStart..prefixEnd];
        var replacement = LegacyPredicate + Environment.NewLine;
        var preparation = CurrentProjections + Environment.NewLine;
        static string Quote(string sql) => sql.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal);

        migrationBuilder.Sql($$"""
            DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(
                OBJECT_ID(N'[dbo].[TR_ProcurementAwardReadinessDecisions_Immutable]', N'TR'));
            IF @definition IS NULL
                THROW 51419, 'The immutable award-readiness trigger is required before this correction.', 1;
            SET @definition = REPLACE(@definition, CHAR(13) + CHAR(10), CHAR(10));
            DECLARE @original nvarchar(max) = N'{{Quote(original)}}';
            DECLARE @replacement nvarchar(max) = N'{{Quote(replacement)}}';
            DECLARE @prefix nvarchar(max) = N'{{Quote(prefix)}}';
            DECLARE @preparation nvarchar(max) = N'{{Quote(preparation)}}';
            -- The SQL Server migration generator rewrites multiline commands,
            -- including embedded SQL literals, to the host's line endings.
            -- Normalize every operand here, after generation, before exact matching.
            SET @original = REPLACE(@original, CHAR(13) + CHAR(10), CHAR(10));
            SET @replacement = REPLACE(@replacement, CHAR(13) + CHAR(10), CHAR(10));
            SET @prefix = REPLACE(@prefix, CHAR(13) + CHAR(10), CHAR(10));
            SET @preparation = REPLACE(@preparation, CHAR(13) + CHAR(10), CHAR(10));

            IF CHARINDEX(N'TDC-LEGACY-READINESS-CURRENT-LOCKS-v1', @definition) > 0
            BEGIN
                -- REPLACE limits its search pattern to 8,000 bytes. Locate each
                -- large retained fragment by a short, unique anchor, then compare
                -- every character and byte length instead of truncating a pattern.
                DECLARE @replacementAnchor nvarchar(64) = LEFT(@replacement, 64);
                DECLARE @preparationAnchor nvarchar(64) = LEFT(@preparation, 64);
                DECLARE @replacementPosition int = CHARINDEX(@replacementAnchor, @definition COLLATE Latin1_General_100_BIN2);
                DECLARE @preparationPosition int = CHARINDEX(@preparationAnchor, @definition COLLATE Latin1_General_100_BIN2);
                DECLARE @retainedReplacement nvarchar(max) = SUBSTRING(@definition, @replacementPosition, DATALENGTH(@replacement) / 2);
                DECLARE @retainedPreparation nvarchar(max) = SUBSTRING(@definition, @preparationPosition, DATALENGTH(@preparation) / 2);
                IF @replacementPosition = 0 OR @preparationPosition = 0
                   OR CHARINDEX(@replacementAnchor, @definition COLLATE Latin1_General_100_BIN2, @replacementPosition + 1) > 0
                   OR CHARINDEX(@preparationAnchor, @definition COLLATE Latin1_General_100_BIN2, @preparationPosition + 1) > 0
                   OR DATALENGTH(@retainedReplacement) <> DATALENGTH(@replacement)
                   OR DATALENGTH(@retainedPreparation) <> DATALENGTH(@preparation)
                   OR @retainedReplacement COLLATE Latin1_General_100_BIN2 <> @replacement COLLATE Latin1_General_100_BIN2
                   OR @retainedPreparation COLLATE Latin1_General_100_BIN2 <> @preparation COLLATE Latin1_General_100_BIN2
                    THROW 51419, 'The already-installed legacy readiness correction differs from its canonical guard.', 1;
                RETURN;
            END;
            IF (DATALENGTH(@definition) - DATALENGTH(REPLACE(@definition COLLATE Latin1_General_100_BIN2, @original, N''))) / DATALENGTH(@original) <> 1
               OR (DATALENGTH(@definition) - DATALENGTH(REPLACE(@definition COLLATE Latin1_General_100_BIN2, @prefix, N''))) / DATALENGTH(@prefix) <> 1
                THROW 51419, 'The legacy readiness guard is not the verified baseline; review trigger drift before applying.', 1;

            SET @definition = REPLACE(@definition COLLATE Latin1_General_100_BIN2, @original, @replacement);
            SET @definition = REPLACE(@definition COLLATE Latin1_General_100_BIN2, @prefix, @preparation + @prefix);
            DECLARE @triggerPosition int = CHARINDEX(N'TRIGGER', UPPER(@definition));
            IF @triggerPosition = 0
                THROW 51419, 'The immutable award-readiness trigger declaration could not be altered safely.', 1;
            SET @definition = N'ALTER ' + SUBSTRING(@definition, @triggerPosition, LEN(@definition));
            EXEC sys.sp_executesql @definition;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        THROW 51419, 'The corrected immutable legacy award-readiness guard requires a reviewed recovery migration; retained Ready decisions and score history must not be invalidated.', 1;
        """);

    private const string CurrentProjections = """
        -- TDC-LEGACY-READINESS-CURRENT-LOCKS-v1
        DECLARE @LegacyCurrent TABLE (
            DecisionId uniqueidentifier NOT NULL, EvaluationId uniqueidentifier NOT NULL,
            BidId uniqueidentifier NOT NULL, EvaluatorId uniqueidentifier NOT NULL,
            Status nvarchar(50) NULL, IsRecommended bit NOT NULL,
            ExpectedSnapshot nvarchar(max) NOT NULL
        );
        INSERT @LegacyCurrent
        SELECT ranked.DecisionId, ranked.Id, ranked.TenderBidId, ranked.TenderEvaluatorId,
               ranked.Status, ranked.IsRecommended, ranked.ExpectedSnapshot
        FROM (
            SELECT i.Id AS DecisionId, e.Id, e.TenderBidId, e.TenderEvaluatorId,
                   e.Status, e.IsRecommended,
                   ROW_NUMBER() OVER (
                       PARTITION BY i.Id, e.TenderBidId, e.TenderEvaluatorId
                       ORDER BY COALESCE(e.SubmittedDate, e.UpdatedAt, e.EvaluationDate) DESC,
                                e.CreatedAt DESC,
                                CONVERT(char(36), e.Id) COLLATE Latin1_General_100_BIN2 DESC) AS Position,
                   (SELECT 'tdc.legacy-tender-score-sheet.v1' AS schemaVersion,
                           LOWER(CONVERT(char(36), e.Id)) AS evaluationId,
                           LOWER(CONVERT(char(36), e.TenderBidId)) AS TenderBidId,
                           LOWER(CONVERT(char(36), e.TenderEvaluatorId)) AS TenderEvaluatorId,
                           'Submitted' AS status,
                           TODATETIMEOFFSET(COALESCE(e.SubmittedDate, e.UpdatedAt, e.EvaluationDate), '+00:00') AS submittedAtUtc,
                           LOWER(CONVERT(char(36), evaluator.UserId)) AS evaluatorUserId,
                           e.PriceScore, e.QualityScore, e.DeliveryScore, e.ExperienceScore,
                           e.TechnicalScore, e.ComplianceScore, e.TotalScore,
                           e.EvaluationCriteriaJson, e.TechnicalComments, e.CommercialComments,
                           e.OverallComments, e.IsRecommended, e.Recommendation
                    FOR JSON PATH, INCLUDE_NULL_VALUES, WITHOUT_ARRAY_WRAPPER) AS ExpectedSnapshot
            FROM inserted i
            JOIN [dbo].[TenderBids] b ON b.TenderId = i.SourceId
                 AND b.TenantId = i.TenantId AND b.IsDeleted = 0
            JOIN [dbo].[TenderEvaluations] e ON e.TenderBidId = b.Id
                 AND e.TenantId = i.TenantId AND e.IsDeleted = 0
            LEFT JOIN [dbo].[TenderEvaluators] evaluator ON evaluator.Id = e.TenderEvaluatorId
                 AND evaluator.TenantId = i.TenantId AND evaluator.TenderId = i.SourceId
                 AND evaluator.IsDeleted = 0
            WHERE i.Status = 1 AND i.SourceType = 1
              AND NOT EXISTS (SELECT 1 FROM [dbo].[ProcurementTenderControls] c
                  WHERE c.TenderId = i.SourceId AND c.TenantId = i.TenantId AND c.IsDeleted = 0)
        ) ranked
        WHERE ranked.Position = 1;

        DECLARE @LegacySheets TABLE (
            DecisionId uniqueidentifier NOT NULL, SheetId uniqueidentifier NOT NULL,
            TenantId uniqueidentifier NOT NULL, CommitteeControlId uniqueidentifier NOT NULL,
            AppointmentId uniqueidentifier NOT NULL, BidId uniqueidentifier NOT NULL,
            SubmittedByUserId uniqueidentifier NOT NULL, Attempt int NOT NULL,
            Status int NOT NULL, ScoreSnapshotJson nvarchar(max) NOT NULL
        );
        INSERT @LegacySheets
        SELECT ranked.DecisionId, ranked.Id, ranked.TenantId, ranked.CommitteeControlId,
               ranked.AppointmentId, ranked.ScoreSubjectId, ranked.SubmittedByUserId,
               ranked.Attempt, ranked.Status, ranked.ScoreSnapshotJson
        FROM (
            SELECT i.Id AS DecisionId, s.*,
                   ROW_NUMBER() OVER (
                       PARTITION BY i.Id, s.AppointmentId, s.ScoreSubjectId
                       ORDER BY s.Attempt DESC, s.SubmittedAtUtc DESC) AS Position
            FROM inserted i
            CROSS APPLY (SELECT TOP (1) c.Id
                FROM [dbo].[ProcurementEvaluationCommitteeControls] c
                WHERE c.TenantId = i.TenantId AND c.SourceType = 0
                  AND c.SourceId = i.SourceId AND c.IsDeleted = 0
                ORDER BY c.Version DESC) committee
            JOIN [dbo].[ProcurementEvaluationScoreSheets] s ON s.CommitteeControlId = committee.Id
                AND s.Phase = 2 AND s.ScoreSubjectType = 'TenderEvaluation' AND s.IsDeleted = 0
            WHERE EXISTS (SELECT 1 FROM @LegacyCurrent e
                WHERE e.DecisionId = i.Id AND e.BidId = s.ScoreSubjectId)
        ) ranked WHERE ranked.Position = 1;
        """;

    private const string LegacyPredicate = """
        OR (i.SourceType = 1 AND NOT EXISTS (
            SELECT 1 FROM [dbo].[ProcurementTenderControls] c
            WHERE c.TenderId = i.SourceId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
        ) AND (
            EXISTS (SELECT 1 FROM [dbo].[ProcurementExceptionalSourcingControls] ec
                WHERE ec.TenderId = i.SourceId AND ec.TenantId = i.TenantId AND ec.IsDeleted = 0)
            OR EXISTS (SELECT 1 FROM [dbo].[Tenders] t WHERE t.Id = i.SourceId
                AND t.TenantId = i.TenantId AND (t.Status IN ('Awarded', 'Cancelled') OR t.AwardDate IS NOT NULL))
            OR NOT EXISTS (SELECT 1 FROM @LegacyCurrent e WHERE e.DecisionId = i.Id)
            OR EXISTS (SELECT 1 FROM @LegacyCurrent e WHERE e.DecisionId = i.Id
                AND (e.Status IS NULL OR e.Status NOT IN ('Submitted', 'Approved')))
            OR (SELECT COUNT(DISTINCT e.BidId) FROM @LegacyCurrent e
                WHERE e.DecisionId = i.Id AND e.IsRecommended = 1) <> 1
            OR EXISTS (SELECT e.BidId FROM @LegacyCurrent e
                WHERE e.DecisionId = i.Id AND e.IsRecommended = 1
                EXCEPT SELECT TRY_CONVERT(uniqueidentifier, j.[value]) FROM OPENJSON(i.RecommendedSubjectIdsJson) j)
            OR EXISTS (SELECT TRY_CONVERT(uniqueidentifier, j.[value]) FROM OPENJSON(i.RecommendedSubjectIdsJson) j
                EXCEPT SELECT e.BidId FROM @LegacyCurrent e WHERE e.DecisionId = i.Id AND e.IsRecommended = 1)
            OR (SELECT COUNT(*) FROM @LegacySheets s WHERE s.DecisionId = i.Id)
                <> (SELECT COUNT(*) FROM @LegacyCurrent e WHERE e.DecisionId = i.Id)
            OR EXISTS (
                SELECT 1 FROM @LegacyCurrent e WHERE e.DecisionId = i.Id AND (
                    SELECT COUNT(*) FROM @LegacySheets s
                    JOIN [dbo].[TenderEvaluators] evaluator ON evaluator.Id = e.EvaluatorId
                        AND evaluator.TenantId = i.TenantId AND evaluator.TenderId = i.SourceId AND evaluator.IsDeleted = 0
                    JOIN [dbo].[ProcurementEvaluationCommitteeAppointments] appointment ON appointment.Id = s.AppointmentId
                        AND appointment.TenantId = i.TenantId AND appointment.CommitteeControlId = s.CommitteeControlId
                        AND appointment.UserId = evaluator.UserId AND appointment.IsDeleted = 0
                    WHERE s.DecisionId = i.Id AND s.BidId = e.BidId AND s.TenantId = i.TenantId
                      AND s.SubmittedByUserId = evaluator.UserId AND s.Status = 0
                      AND ISJSON(s.ScoreSnapshotJson) = 1
                      AND (SELECT COUNT(*) FROM OPENJSON(CASE WHEN ISJSON(s.ScoreSnapshotJson) = 1 THEN s.ScoreSnapshotJson ELSE '{}' END))
                          = (SELECT COUNT(*) FROM OPENJSON(e.ExpectedSnapshot))
                      AND NOT EXISTS (
                          SELECT 1 FROM OPENJSON(CASE WHEN ISJSON(s.ScoreSnapshotJson) = 1 THEN s.ScoreSnapshotJson ELSE '{}' END) actual
                          GROUP BY actual.[key] COLLATE Latin1_General_100_BIN2 HAVING COUNT(*) <> 1)
                      AND NOT EXISTS (
                          SELECT 1 FROM OPENJSON(e.ExpectedSnapshot) expected
                          FULL JOIN OPENJSON(CASE WHEN ISJSON(s.ScoreSnapshotJson) = 1 THEN s.ScoreSnapshotJson ELSE '{}' END) actual
                            ON actual.[key] COLLATE Latin1_General_100_BIN2 = expected.[key] COLLATE Latin1_General_100_BIN2
                          WHERE expected.[key] IS NULL OR actual.[key] IS NULL OR expected.[type] <> actual.[type]
                             OR (expected.[type] = 2 AND (TRY_CONVERT(decimal(38,28), actual.[value]) IS NULL
                                 OR TRY_CONVERT(decimal(38,28), actual.[value]) <> TRY_CONVERT(decimal(38,28), expected.[value])))
                             OR (expected.[key] = 'submittedAtUtc' AND (TRY_CONVERT(datetimeoffset(7), actual.[value], 127) IS NULL
                                 OR TRY_CONVERT(datetimeoffset(7), actual.[value], 127) <> TRY_CONVERT(datetimeoffset(7), expected.[value], 127)))
                             OR (expected.[type] NOT IN (0,2) AND expected.[key] <> 'submittedAtUtc'
                                 AND actual.[value] COLLATE Latin1_General_100_BIN2 <> expected.[value] COLLATE Latin1_General_100_BIN2)
                      )
                ) <> 1
            )
            OR EXISTS (SELECT 1 FROM @LegacySheets s
                JOIN [dbo].[ProcurementEvaluationScoreRecalls] r ON r.ScoreSheetId = s.SheetId
                    AND r.TenantId = i.TenantId AND r.IsDeleted = 0 AND r.Status IN (0,1)
                WHERE s.DecisionId = i.Id)
            OR EXISTS (SELECT 1 FROM @LegacySheets s WHERE s.DecisionId = i.Id AND (
                s.Attempt < 1
                OR s.Attempt <> (SELECT COUNT(DISTINCT prior.Attempt)
                    FROM [dbo].[ProcurementEvaluationScoreSheets] prior
                    WHERE prior.CommitteeControlId = s.CommitteeControlId AND prior.AppointmentId = s.AppointmentId
                      AND prior.ScoreSubjectId = s.BidId AND prior.Phase = 2 AND prior.ScoreSubjectType = 'TenderEvaluation'
                      AND prior.TenantId = i.TenantId AND prior.IsDeleted = 0)
                OR EXISTS (SELECT 1 FROM [dbo].[ProcurementEvaluationScoreSheets] prior
                    WHERE prior.CommitteeControlId = s.CommitteeControlId AND prior.AppointmentId = s.AppointmentId
                      AND prior.ScoreSubjectId = s.BidId AND prior.Phase = 2 AND prior.ScoreSubjectType = 'TenderEvaluation'
                      AND prior.TenantId = i.TenantId AND prior.IsDeleted = 0 AND prior.Attempt < s.Attempt
                      AND (SELECT COUNT(*) FROM [dbo].[ProcurementEvaluationScoreRecalls] r
                          WHERE r.ScoreSheetId = prior.Id AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                            AND r.Status = 1 AND r.AuthorizedNewAttempt = prior.Attempt + 1) <> 1)
            ))
        ))
        """;
}
