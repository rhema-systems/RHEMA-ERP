using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementAwardReadinessControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementAwardReadinessDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    DecisionSequence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MethodRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MethodRuleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AuthorityRouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorityRouteReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecommendationSubjectType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecommendedSubjectIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecommendedBusinessPartnerIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecommendationSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvaluationLineageJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupplierLineageJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrequalificationLineageJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VerificationLineageJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorityLineageJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceLineageJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrerequisiteSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimelineSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlockedReasonsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvaluatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementAwardReadinessDecisions", x => x.Id);
                    table.CheckConstraint("CK_ProcurementAwardReadinessDecisions_State", "[SourceType] BETWEEN 0 AND 2 AND [Method] BETWEEN 0 AND 8 AND [DecisionSequence] >= 1 AND [Status] BETWEEN 0 AND 1 AND LEN([SourceIntegrityHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([RecommendedSubjectIdsJson]) = 1 AND ISJSON([RecommendedBusinessPartnerIdsJson]) = 1 AND ISJSON([RecommendationSnapshotJson]) = 1 AND ISJSON([EvaluationLineageJson]) = 1 AND ISJSON([SupplierLineageJson]) = 1 AND ISJSON([PrequalificationLineageJson]) = 1 AND ISJSON([VerificationLineageJson]) = 1 AND ISJSON([AuthorityLineageJson]) = 1 AND ISJSON([EvidenceLineageJson]) = 1 AND ISJSON([PrerequisiteSnapshotJson]) = 1 AND ISJSON([TimelineSnapshotJson]) = 1 AND ISJSON([BlockedReasonsJson]) = 1");
                    table.ForeignKey(
                        name: "FK_ProcurementAwardReadinessDecisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAwardReadinessDecisions_TenantId_SourceType_SourceId_DecisionSequence",
                table: "ProcurementAwardReadinessDecisions",
                columns: new[] { "TenantId", "SourceType", "SourceId", "DecisionSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAwardReadinessDecisions_TenantId_SourceType_SourceId_EvaluatedAtUtc",
                table: "ProcurementAwardReadinessDecisions",
                columns: new[] { "TenantId", "SourceType", "SourceId", "EvaluatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAwardReadinessDecisions_TenantId_SourceType_SourceId_IdempotencyKey",
                table: "ProcurementAwardReadinessDecisions",
                columns: new[] { "TenantId", "SourceType", "SourceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAwardReadinessDecisions_TenantId_Status_EvaluatedAtUtc",
                table: "ProcurementAwardReadinessDecisions",
                columns: new[] { "TenantId", "Status", "EvaluatedAtUtc" });

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementAwardReadinessDecisions_Immutable]
                ON [dbo].[ProcurementAwardReadinessDecisions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51400, 'Procurement award-readiness decisions are append-only.', 1;

                    IF (SELECT COUNT(*) FROM inserted) <> 1
                        THROW 51402, 'Award-readiness decisions must be appended one source evaluation at a time.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.IsDeleted = 1
                           OR i.UpdatedAt IS NOT NULL OR i.UpdatedBy IS NOT NULL
                           OR i.LastModifiedById IS NOT NULL
                           OR i.DeletedAt IS NOT NULL OR i.DeletedBy IS NOT NULL
                           OR i.CreatedById IS NULL OR i.CreatedById <> i.EvaluatedByUserId
                           OR i.CreatedAt <> i.EvaluatedAtUtc
                           OR i.EvaluatedByUserId = '00000000-0000-0000-0000-000000000000'
                           OR LEN(LTRIM(RTRIM(i.EvaluatedByName))) = 0
                           OR LEN(LTRIM(RTRIM(i.IdempotencyKey))) = 0
                           OR LEN(LTRIM(RTRIM(i.CorrelationId))) = 0
                           OR i.SourceIntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR NOT EXISTS (
                               SELECT 1
                               FROM [dbo].[Users] u
                               WHERE u.Id = i.EvaluatedByUserId
                                 AND u.IsActive = 1
                                 AND (
                                     u.TenantId = i.TenantId
                                     OR EXISTS (
                                         SELECT 1
                                         FROM [dbo].[UserTenants] ut
                                         WHERE ut.UserId = u.Id
                                           AND ut.TenantId = i.TenantId
                                           AND ut.Status = 0
                                           AND ut.IsDeleted = 0
                                           AND (ut.ExpiresAt IS NULL OR ut.ExpiresAt > i.EvaluatedAtUtc)
                                     )
                                 )
                           )
                    )
                        THROW 51401, 'Award-readiness actor, tenant, append-only audit, correlation, or hash envelope is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.DecisionSequence <> (
                            SELECT ISNULL(MAX(prior.DecisionSequence), 0) + 1
                            FROM [dbo].[ProcurementAwardReadinessDecisions] prior
                            WHERE prior.TenantId = i.TenantId
                              AND prior.SourceType = i.SourceType
                              AND prior.SourceId = i.SourceId
                              AND prior.Id <> i.Id
                        )
                    )
                        THROW 51402, 'Award-readiness decision sequence is not contiguous for the source.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE LEFT(LTRIM(i.RecommendedSubjectIdsJson), 1) <> '['
                           OR LEFT(LTRIM(i.RecommendedBusinessPartnerIdsJson), 1) <> '['
                           OR LEFT(LTRIM(i.EvaluationLineageJson), 1) <> '['
                           OR LEFT(LTRIM(i.SupplierLineageJson), 1) <> '['
                           OR LEFT(LTRIM(i.PrequalificationLineageJson), 1) <> '['
                           OR LEFT(LTRIM(i.VerificationLineageJson), 1) <> '['
                           OR LEFT(LTRIM(i.EvidenceLineageJson), 1) <> '['
                           OR LEFT(LTRIM(i.PrerequisiteSnapshotJson), 1) <> '['
                           OR LEFT(LTRIM(i.TimelineSnapshotJson), 1) <> '['
                           OR LEFT(LTRIM(i.BlockedReasonsJson), 1) <> '['
                           OR LEFT(LTRIM(i.RecommendationSnapshotJson), 1) <> '{'
                           OR LEFT(LTRIM(i.AuthorityLineageJson), 1) <> '{'
                           OR EXISTS (
                               SELECT 1
                               FROM OPENJSON(i.RecommendedSubjectIdsJson) j
                               WHERE j.[type] <> 1
                                  OR TRY_CONVERT(uniqueidentifier, j.[value]) IS NULL
                                  OR TRY_CONVERT(uniqueidentifier, j.[value])
                                     = '00000000-0000-0000-0000-000000000000'
                           )
                           OR EXISTS (
                               SELECT 1
                               FROM OPENJSON(i.RecommendedBusinessPartnerIdsJson) j
                               WHERE j.[type] <> 1
                                  OR TRY_CONVERT(uniqueidentifier, j.[value]) IS NULL
                                  OR TRY_CONVERT(uniqueidentifier, j.[value])
                                     = '00000000-0000-0000-0000-000000000000'
                           )
                           OR EXISTS (
                               SELECT 1
                               FROM OPENJSON(i.RecommendedSubjectIdsJson)
                               GROUP BY [value]
                               HAVING COUNT(*) > 1
                           )
                           OR EXISTS (
                               SELECT 1
                               FROM OPENJSON(i.RecommendedBusinessPartnerIdsJson)
                               GROUP BY [value]
                               HAVING COUNT(*) > 1
                           )
                           OR JSON_VALUE(i.RecommendationSnapshotJson, '$.subjectType')
                              <> i.RecommendationSubjectType
                           OR EXISTS (
                               SELECT TRY_CONVERT(uniqueidentifier, j.[value])
                               FROM OPENJSON(i.RecommendedSubjectIdsJson) j
                               EXCEPT
                               SELECT TRY_CONVERT(uniqueidentifier, j.[value])
                               FROM OPENJSON(JSON_QUERY(
                                   i.RecommendationSnapshotJson, '$.subjectIds')) j
                           )
                           OR EXISTS (
                               SELECT TRY_CONVERT(uniqueidentifier, j.[value])
                               FROM OPENJSON(JSON_QUERY(
                                   i.RecommendationSnapshotJson, '$.subjectIds')) j
                               EXCEPT
                               SELECT TRY_CONVERT(uniqueidentifier, j.[value])
                               FROM OPENJSON(i.RecommendedSubjectIdsJson) j
                           )
                           OR EXISTS (
                               SELECT TRY_CONVERT(uniqueidentifier, j.[value])
                               FROM OPENJSON(i.RecommendedBusinessPartnerIdsJson) j
                               EXCEPT
                               SELECT TRY_CONVERT(uniqueidentifier, j.[value])
                               FROM OPENJSON(JSON_QUERY(
                                   i.RecommendationSnapshotJson, '$.businessPartnerIds')) j
                           )
                           OR EXISTS (
                               SELECT TRY_CONVERT(uniqueidentifier, j.[value])
                               FROM OPENJSON(JSON_QUERY(
                                   i.RecommendationSnapshotJson, '$.businessPartnerIds')) j
                               EXCEPT
                               SELECT TRY_CONVERT(uniqueidentifier, j.[value])
                               FROM OPENJSON(i.RecommendedBusinessPartnerIdsJson) j
                           )
                           OR ISNULL(LOWER(JSON_VALUE(i.AuthorityLineageJson, '$.methodRuleId')), '')
                              <> ISNULL(LOWER(CONVERT(varchar(36), i.MethodRuleId)), '')
                           OR ISNULL(JSON_VALUE(i.AuthorityLineageJson, '$.methodRuleCode'), '')
                              <> ISNULL(i.MethodRuleCode, '')
                           OR ISNULL(LOWER(JSON_VALUE(i.AuthorityLineageJson, '$.authorityRouteId')), '')
                              <> ISNULL(LOWER(CONVERT(varchar(36), i.AuthorityRouteId)), '')
                           OR ISNULL(JSON_VALUE(i.AuthorityLineageJson, '$.authorityRouteReference'), '')
                              <> ISNULL(i.AuthorityRouteReference, '')
                           OR ISNULL(LOWER(JSON_VALUE(i.AuthorityLineageJson, '$.workflowDefinitionId')), '')
                              <> ISNULL(LOWER(CONVERT(varchar(36), i.WorkflowDefinitionId)), '')
                           OR ISNULL(LOWER(JSON_VALUE(i.AuthorityLineageJson, '$.workflowInstanceId')), '')
                              <> ISNULL(LOWER(CONVERT(varchar(36), i.WorkflowInstanceId)), '')
                    )
                        THROW 51403, 'Award-readiness JSON shape, identifier arrays, snapshot, authority, or hash encoding is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[RequestForQuotations] r
                          ON i.SourceType = 0 AND r.Id = i.SourceId
                         AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                        LEFT JOIN [dbo].[Tenders] t
                          ON i.SourceType IN (1, 2) AND t.Id = i.SourceId
                         AND t.TenantId = i.TenantId AND t.IsDeleted = 0
                        WHERE (i.SourceType = 0
                               AND (r.Id IS NULL OR r.RfqNumber <> i.SourceReference OR i.Method <> 0))
                           OR (i.SourceType IN (1, 2)
                               AND (t.Id IS NULL OR t.TenderNumber <> i.SourceReference))
                           OR (i.SourceType = 2 AND NOT EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementExceptionalSourcingControls] ec
                               WHERE ec.TenderId = i.SourceId
                                 AND ec.TenantId = i.TenantId AND ec.IsDeleted = 0
                           ))
                           OR (i.Status = 1 AND i.SourceType = 1 AND EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementExceptionalSourcingControls] ec
                               WHERE ec.TenderId = i.SourceId
                                 AND ec.TenantId = i.TenantId AND ec.IsDeleted = 0
                           ))
                    )
                        THROW 51404, 'Award-readiness source type, tenant, identity, reference, or procurement method lineage is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE (i.SourceType = 0
                               AND i.RecommendationSubjectType <> 'RequestForQuotationQuote')
                           OR (i.SourceType IN (1, 2)
                               AND i.RecommendationSubjectType <> 'TenderBid')
                           OR (i.SourceType = 0 AND EXISTS (
                               SELECT 1
                               FROM OPENJSON(i.RecommendedSubjectIdsJson) j
                               LEFT JOIN [dbo].[RequestForQuotationQuotes] q
                                 ON q.Id = TRY_CONVERT(uniqueidentifier, j.[value])
                                AND q.RfqId = i.SourceId
                                AND q.TenantId = i.TenantId
                                AND q.IsDeleted = 0
                               WHERE q.Id IS NULL
                           ))
                           OR (i.SourceType IN (1, 2) AND EXISTS (
                               SELECT 1
                               FROM OPENJSON(i.RecommendedSubjectIdsJson) j
                               LEFT JOIN [dbo].[TenderBids] b
                                 ON b.Id = TRY_CONVERT(uniqueidentifier, j.[value])
                                AND b.TenderId = i.SourceId
                                AND b.TenantId = i.TenantId
                                AND b.IsDeleted = 0
                                AND b.Status NOT IN ('Withdrawn', 'Rejected')
                               WHERE b.Id IS NULL
                           ))
                           OR (i.SourceType = 0 AND (
                               EXISTS (
                                   SELECT q.BusinessPartnerId
                                   FROM OPENJSON(i.RecommendedSubjectIdsJson) j
                                   JOIN [dbo].[RequestForQuotationQuotes] q
                                     ON q.Id = TRY_CONVERT(uniqueidentifier, j.[value])
                                   EXCEPT
                                   SELECT TRY_CONVERT(uniqueidentifier, p.[value])
                                   FROM OPENJSON(i.RecommendedBusinessPartnerIdsJson) p
                               )
                               OR EXISTS (
                                   SELECT TRY_CONVERT(uniqueidentifier, p.[value])
                                   FROM OPENJSON(i.RecommendedBusinessPartnerIdsJson) p
                                   EXCEPT
                                   SELECT q.BusinessPartnerId
                                   FROM OPENJSON(i.RecommendedSubjectIdsJson) j
                                   JOIN [dbo].[RequestForQuotationQuotes] q
                                     ON q.Id = TRY_CONVERT(uniqueidentifier, j.[value])
                               )
                           ))
                           OR (i.SourceType IN (1, 2) AND (
                               EXISTS (
                                   SELECT b.BusinessPartnerId
                                   FROM OPENJSON(i.RecommendedSubjectIdsJson) j
                                   JOIN [dbo].[TenderBids] b
                                     ON b.Id = TRY_CONVERT(uniqueidentifier, j.[value])
                                   EXCEPT
                                   SELECT TRY_CONVERT(uniqueidentifier, p.[value])
                                   FROM OPENJSON(i.RecommendedBusinessPartnerIdsJson) p
                               )
                               OR EXISTS (
                                   SELECT TRY_CONVERT(uniqueidentifier, p.[value])
                                   FROM OPENJSON(i.RecommendedBusinessPartnerIdsJson) p
                                   EXCEPT
                                   SELECT b.BusinessPartnerId
                                   FROM OPENJSON(i.RecommendedSubjectIdsJson) j
                                   JOIN [dbo].[TenderBids] b
                                     ON b.Id = TRY_CONVERT(uniqueidentifier, j.[value])
                               )
                           ))
                    )
                        THROW 51405, 'Award-readiness recommendation subjects and suppliers do not belong to the exact tenant source.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE (SELECT COUNT(*)
                               FROM OPENJSON(i.PrerequisiteSnapshotJson)
                               WITH ([group] int '$.group')) <> 9
                           OR (SELECT COUNT(DISTINCT p.[group])
                               FROM OPENJSON(i.PrerequisiteSnapshotJson)
                               WITH ([group] int '$.group') p) <> 9
                           OR EXISTS (
                               SELECT 1
                               FROM OPENJSON(i.PrerequisiteSnapshotJson)
                               WITH (
                                   [group] int '$.group',
                                   [status] int '$.status',
                                   [items] nvarchar(max) '$.items' AS JSON
                               ) p
                               WHERE p.[group] NOT BETWEEN 0 AND 8
                                  OR p.[status] NOT BETWEEN 0 AND 2
                                  OR p.[items] IS NULL
                                  OR NOT EXISTS (SELECT 1 FROM OPENJSON(p.[items]))
                                  OR EXISTS (
                                      SELECT 1
                                      FROM OPENJSON(p.[items])
                                      WITH ([status] int '$.status') pi
                                      WHERE pi.[status] NOT BETWEEN 0 AND 2
                                  )
                           )
                           OR (i.Status = 1 AND (
                               EXISTS (
                                   SELECT 1
                                   FROM OPENJSON(i.PrerequisiteSnapshotJson)
                                   WITH (
                                       [status] int '$.status',
                                       [items] nvarchar(max) '$.items' AS JSON
                                   ) p
                                   WHERE p.[status] = 1
                                      OR EXISTS (
                                          SELECT 1
                                          FROM OPENJSON(p.[items])
                                          WITH ([status] int '$.status') pi
                                          WHERE pi.[status] = 1
                                      )
                               )
                               OR EXISTS (SELECT 1 FROM OPENJSON(i.BlockedReasonsJson))
                               OR NOT EXISTS (SELECT 1 FROM OPENJSON(i.RecommendedSubjectIdsJson))
                               OR NOT EXISTS (
                                   SELECT 1 FROM OPENJSON(i.RecommendedBusinessPartnerIdsJson))
                           ))
                           OR (i.Status = 0 AND (
                               NOT EXISTS (
                                   SELECT 1
                                   FROM OPENJSON(i.PrerequisiteSnapshotJson)
                                   WITH (
                                       [status] int '$.status',
                                       [items] nvarchar(max) '$.items' AS JSON
                                   ) p
                                   WHERE p.[status] = 1
                                      OR EXISTS (
                                          SELECT 1
                                          FROM OPENJSON(p.[items])
                                          WITH ([status] int '$.status') pi
                                          WHERE pi.[status] = 1
                                      )
                               )
                               OR NOT EXISTS (SELECT 1 FROM OPENJSON(i.BlockedReasonsJson))
                           ))
                    )
                        THROW 51406, 'Award-readiness prerequisite groups, failed items, blocked reasons, or Ready state are internally inconsistent.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.Status = 1
                          AND (
                              (i.SourceType = 0 AND (
                                  NOT EXISTS (
                                      SELECT 1
                                      FROM [dbo].[ProcurementRfqEvaluations] e
                                      WHERE e.RfqId = i.SourceId
                                        AND e.TenantId = i.TenantId AND e.IsDeleted = 0
                                        AND e.Status = 2
                                        AND e.MethodRuleId = i.MethodRuleId
                                        AND e.MethodRuleCode = i.MethodRuleCode
                                        AND ISNULL(e.WorkflowDefinitionId,
                                            '00000000-0000-0000-0000-000000000000')
                                            = ISNULL(i.WorkflowDefinitionId,
                                            '00000000-0000-0000-0000-000000000000')
                                        AND ISNULL(e.WorkflowInstanceId,
                                            '00000000-0000-0000-0000-000000000000')
                                            = ISNULL(i.WorkflowInstanceId,
                                            '00000000-0000-0000-0000-000000000000')
                                  )
                                  OR EXISTS (
                                      SELECT l.QuoteId
                                      FROM [dbo].[ProcurementRfqEvaluations] e
                                      JOIN [dbo].[ProcurementRfqEvaluationLines] l
                                        ON l.EvaluationId = e.Id
                                       AND l.TenantId = e.TenantId AND l.IsDeleted = 0
                                      WHERE e.RfqId = i.SourceId
                                        AND e.TenantId = i.TenantId AND e.IsDeleted = 0
                                        AND e.Status = 2
                                      EXCEPT
                                      SELECT TRY_CONVERT(uniqueidentifier, j.[value])
                                      FROM OPENJSON(i.RecommendedSubjectIdsJson) j
                                  )
                                  OR EXISTS (
                                      SELECT TRY_CONVERT(uniqueidentifier, j.[value])
                                      FROM OPENJSON(i.RecommendedSubjectIdsJson) j
                                      EXCEPT
                                      SELECT l.QuoteId
                                      FROM [dbo].[ProcurementRfqEvaluations] e
                                      JOIN [dbo].[ProcurementRfqEvaluationLines] l
                                        ON l.EvaluationId = e.Id
                                       AND l.TenantId = e.TenantId AND l.IsDeleted = 0
                                      WHERE e.RfqId = i.SourceId
                                        AND e.TenantId = i.TenantId AND e.IsDeleted = 0
                                        AND e.Status = 2
                                  )
                              ))
                              OR (i.SourceType = 1 AND EXISTS (
                                  SELECT 1
                                  FROM [dbo].[ProcurementTenderControls] c
                                  WHERE c.TenderId = i.SourceId
                                    AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                              ) AND NOT EXISTS (
                                  SELECT 1
                                  FROM [dbo].[ProcurementTenderControls] c
                                  JOIN OPENJSON(i.RecommendedSubjectIdsJson) j
                                    ON TRY_CONVERT(uniqueidentifier, j.[value]) = c.RecommendedBidId
                                  JOIN [dbo].[TenderBids] b
                                    ON b.Id = c.RecommendedBidId
                                   AND b.TenderId = c.TenderId
                                   AND b.TenantId = c.TenantId
                                   AND b.IsDeleted = 0
                                   AND b.Status NOT IN ('Withdrawn', 'Rejected')
                                  WHERE c.TenderId = i.SourceId
                                    AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                                    AND c.Status = 5
                                    AND c.Method = i.Method
                                    AND c.MethodRuleId = i.MethodRuleId
                                    AND c.MethodRuleCode = i.MethodRuleCode
                                    AND c.AuthorityRouteId = i.AuthorityRouteId
                                    AND c.AuthorityRouteReference = i.AuthorityRouteReference
                                    AND c.WorkflowDefinitionId = i.WorkflowDefinitionId
                                    AND c.WorkflowInstanceId = i.WorkflowInstanceId
                                    AND b.BusinessPartnerId IN (
                                        SELECT TRY_CONVERT(uniqueidentifier, p.[value])
                                        FROM OPENJSON(i.RecommendedBusinessPartnerIdsJson) p)
                                  GROUP BY c.Id
                                  HAVING COUNT(*) = 1
                              ))
                              OR (i.SourceType = 1 AND NOT EXISTS (
                                  SELECT 1
                                  FROM [dbo].[ProcurementTenderControls] c
                                  WHERE c.TenderId = i.SourceId
                                    AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                              ) AND (
                                  EXISTS (
                                      SELECT 1
                                      FROM [dbo].[ProcurementExceptionalSourcingControls] ec
                                      WHERE ec.TenderId = i.SourceId
                                        AND ec.TenantId = i.TenantId AND ec.IsDeleted = 0)
                                  OR (SELECT COUNT(DISTINCT e.TenderBidId)
                                      FROM [dbo].[TenderEvaluations] e
                                      JOIN [dbo].[TenderBids] b ON b.Id = e.TenderBidId
                                      WHERE b.TenderId = i.SourceId
                                        AND b.TenantId = i.TenantId AND b.IsDeleted = 0
                                        AND b.Status NOT IN ('Withdrawn', 'Rejected')
                                        AND e.TenantId = i.TenantId AND e.IsDeleted = 0
                                        AND e.IsRecommended = 1 AND e.Status = 'Approved') <> 1
                                  OR EXISTS (
                                      SELECT 1
                                      FROM [dbo].[TenderEvaluations] e
                                      JOIN [dbo].[TenderBids] b ON b.Id = e.TenderBidId
                                      WHERE b.TenderId = i.SourceId
                                        AND e.TenantId = i.TenantId AND e.IsDeleted = 0
                                        AND e.Status <> 'Approved')
                                  OR EXISTS (
                                      SELECT e.TenderBidId
                                      FROM [dbo].[TenderEvaluations] e
                                      JOIN [dbo].[TenderBids] b ON b.Id = e.TenderBidId
                                      WHERE b.TenderId = i.SourceId
                                        AND b.TenantId = i.TenantId AND b.IsDeleted = 0
                                        AND e.TenantId = i.TenantId AND e.IsDeleted = 0
                                        AND e.IsRecommended = 1 AND e.Status = 'Approved'
                                      EXCEPT
                                      SELECT TRY_CONVERT(uniqueidentifier, j.[value])
                                      FROM OPENJSON(i.RecommendedSubjectIdsJson) j)
                              ))
                              OR (i.SourceType = 2 AND NOT EXISTS (
                                  SELECT 1
                                  FROM [dbo].[ProcurementExceptionalSourcingControls] c
                                  JOIN OPENJSON(i.RecommendedSubjectIdsJson) j
                                    ON TRY_CONVERT(uniqueidentifier, j.[value]) = c.RecommendedBidId
                                  JOIN [dbo].[TenderBids] b
                                    ON b.Id = c.RecommendedBidId
                                   AND b.TenderId = c.TenderId
                                   AND b.TenantId = c.TenantId
                                   AND b.IsDeleted = 0
                                   AND b.Status NOT IN ('Withdrawn', 'Rejected')
                                  WHERE c.TenderId = i.SourceId
                                    AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                                    AND c.Status = 4
                                    AND c.Method = i.Method
                                    AND c.MethodRuleId = i.MethodRuleId
                                    AND c.MethodRuleCode = i.MethodRuleCode
                                    AND c.AuthorityRouteId = i.AuthorityRouteId
                                    AND c.AuthorityRouteReference = i.AuthorityRouteReference
                                    AND c.WorkflowDefinitionId = i.WorkflowDefinitionId
                                    AND c.WorkflowInstanceId = i.WorkflowInstanceId
                                    AND b.BusinessPartnerId IN (
                                        SELECT TRY_CONVERT(uniqueidentifier, p.[value])
                                        FROM OPENJSON(i.RecommendedBusinessPartnerIdsJson) p)
                                  GROUP BY c.Id
                                  HAVING COUNT(*) = 1
                              ))
                          )
                    )
                        THROW 51407, 'Ready award-readiness decision does not match the exact approved source recommendation and lifecycle.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.Status = 1
                          AND (
                              (i.SourceType IN (0, 2)
                               OR (i.SourceType = 1 AND EXISTS (
                                   SELECT 1
                                   FROM [dbo].[ProcurementTenderControls] c
                                   WHERE c.TenderId = i.SourceId
                                     AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                               )))
                              AND (
                                  i.WorkflowDefinitionId IS NULL
                                  OR i.WorkflowInstanceId IS NULL
                                  OR JSON_VALUE(i.AuthorityLineageJson, '$.workflowStatus')
                                     <> 'Completed'
                                  OR NOT EXISTS (
                                      SELECT 1
                                      FROM [dbo].[WorkflowInstances] w
                                      WHERE w.Id = i.WorkflowInstanceId
                                        AND w.WorkflowDefinitionId = i.WorkflowDefinitionId
                                        AND w.EntityId = i.SourceId
                                        AND w.TenantId = i.TenantId
                                        AND w.IsDeleted = 0
                                        AND w.Status = 2
                                  )
                              )
                          )
                    )
                        THROW 51408, 'Ready award-readiness decision lacks the exact completed source-owned workflow instance.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.Status = 1 AND i.SourceType = 1
                          AND NOT EXISTS (
                              SELECT 1
                              FROM [dbo].[ProcurementTenderControls] c
                              WHERE c.TenderId = i.SourceId
                                AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                          )
                          AND (
                              i.WorkflowDefinitionId IS NOT NULL
                              OR i.WorkflowInstanceId IS NOT NULL
                              OR JSON_VALUE(i.AuthorityLineageJson, '$.approvalReference')
                                 <> 'award-readiness-decision'
                              OR TRY_CONVERT(uniqueidentifier,
                                  JSON_VALUE(i.AuthorityLineageJson, '$.approvedByUserId'))
                                 <> i.EvaluatedByUserId
                              OR NOT EXISTS (
                                  SELECT 1
                                  FROM OPENJSON(JSON_QUERY(
                                      i.AuthorityLineageJson, '$.approvalActorUserIds')) a
                                  WHERE TRY_CONVERT(uniqueidentifier, a.[value])
                                        = i.EvaluatedByUserId
                              )
                          )
                    )
                        THROW 51409, 'Ready legacy-tender approval must be the independently authorized award-readiness decision.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementAwardReadinessDecisions_Immutable];");

            migrationBuilder.DropTable(
                name: "ProcurementAwardReadinessDecisions");
        }
    }
}
