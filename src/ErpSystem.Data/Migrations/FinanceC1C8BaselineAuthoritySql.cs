using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Preserves the final reviewed C5-C8 database-only trigger authority in the disposable-development baseline.
/// The relational model creates the underlying C1-C8 tables, keys, indexes, constraints, and defaults.
/// Each trigger is emitted as its own SQL command because CREATE/ALTER TRIGGER must begin its batch.
/// </summary>
internal static class FinanceC1C8BaselineAuthoritySql
{
    internal static void Apply(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"CREATE TRIGGER [TR_AccountingBookApplicabilityPolicies_C5Authority]
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

    -- Retirement authority can originate only on an already-approved policy. Draft, pending-approval,
    -- and rejected rows must remain retirement-clean, and approval cannot carry evidence prepared before
    -- approval into the approved authority boundary.
    IF EXISTS (
        SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.[Id]=i.[Id]
        WHERE (i.[PolicyStatus] IN (1,2,4) OR (i.[PolicyStatus]=3 AND ISNULL(d.[PolicyStatus],0)<>3))
          AND (i.[RetiredByUserId] IS NOT NULL OR i.[RetiredAtUtc] IS NOT NULL
            OR i.[RetirementRequestedByUserId] IS NOT NULL OR i.[RetirementRequestedAtUtc] IS NOT NULL
            OR i.[RetirementReason] IS NOT NULL OR i.[RetirementWorkflowInstanceId] IS NOT NULL
            OR i.[RetirementDecisionStatus] IS NOT NULL OR i.[RetirementDecidedByUserId] IS NOT NULL
            OR i.[RetirementDecidedAtUtc] IS NOT NULL OR i.[RetirementDecisionReason] IS NOT NULL)
    ) THROW 51000, 'C5_POLICY_RETIREMENT_STATUS_INVALID: retirement evidence may originate only after governed policy approval.', 1;

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
END;");

        migrationBuilder.Sql(@"CREATE TRIGGER [TR_AccountingBookApplicabilityRules_C5Immutable]
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
END;");

        migrationBuilder.Sql(@"CREATE TRIGGER [TR_AccountingBookApplicabilityRuleBooks_C5Immutable]
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
END;");

        migrationBuilder.Sql(@"CREATE TRIGGER [TR_AccountingBookSelectionEvidence_C5Immutable]
ON [AccountingBookSelectionEvidence] AFTER UPDATE, DELETE AS
BEGIN SET NOCOUNT ON; THROW 51000, 'C5_SELECTION_IMMUTABLE: frozen selection evidence cannot be changed or deleted.', 1; END;");

        migrationBuilder.Sql(@"CREATE TRIGGER [TR_AccountingBookSelectionEvidenceBooks_C5Immutable]
ON [AccountingBookSelectionEvidenceBooks] AFTER UPDATE, DELETE AS
BEGIN SET NOCOUNT ON; THROW 51000, 'C5_SELECTION_BOOK_IMMUTABLE: frozen ordered book evidence cannot be changed or deleted.', 1; END;");

        migrationBuilder.Sql(@"CREATE TRIGGER [TR_AccountingEvents_C6Authority]
ON [AccountingEvents] AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id]=d.[Id] WHERE i.[Id] IS NULL)
        THROW 51000, 'C6_EVENT_IMMUTABLE: AccountingEvents cannot be deleted.', 1;

    IF EXISTS (SELECT 1 FROM inserted i WHERE i.[TenantId]='00000000-0000-0000-0000-000000000000'
       OR i.[SourceDocumentId]='00000000-0000-0000-0000-000000000000'
       OR i.[PreparedByUserId]='00000000-0000-0000-0000-000000000000'
       OR LEN(i.[OriginatingModuleCode])=0 OR LEN(i.[SourceDocumentType])=0 OR LEN(i.[PostingAction])=0 OR LEN(i.[IdempotencyKey])=0
       OR i.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[OriginatingModuleCode]))) COLLATE Latin1_General_100_BIN2
       OR i.[SourceDocumentType] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[SourceDocumentType]))) COLLATE Latin1_General_100_BIN2
       OR i.[PostingAction] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[PostingAction]))) COLLATE Latin1_General_100_BIN2
       OR i.[IdempotencyKey] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[IdempotencyKey]))) COLLATE Latin1_General_100_BIN2
       OR DATALENGTH(i.[OriginatingModuleCode])<>DATALENGTH(UPPER(LTRIM(RTRIM(i.[OriginatingModuleCode]))))
       OR DATALENGTH(i.[SourceDocumentType])<>DATALENGTH(UPPER(LTRIM(RTRIM(i.[SourceDocumentType]))))
       OR DATALENGTH(i.[PostingAction])<>DATALENGTH(UPPER(LTRIM(RTRIM(i.[PostingAction]))))
       OR DATALENGTH(i.[IdempotencyKey])<>DATALENGTH(UPPER(LTRIM(RTRIM(i.[IdempotencyKey]))))
       OR i.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2 NOT IN (N'FIN',N'INV',N'PROC',N'SALES',N'HR',N'QS',N'ESTATE',N'LEGAL',N'MAINT')
       OR LEFT(i.[SourceDocumentType],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
       OR i.[SourceDocumentType] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
       OR i.[SourceDocumentType] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')
       OR LEFT(i.[PostingAction],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
       OR i.[PostingAction] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
       OR i.[PostingAction] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS'))
        THROW 51000, 'C6_EVENT_IDENTITY: canonical nonblank tenant, source, action, actor and idempotency identity is required.', 1;

    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
       WHERE d.[TenantId]<>i.[TenantId] OR d.[OriginatingModuleCode]<>i.[OriginatingModuleCode]
          OR d.[SourceDocumentType]<>i.[SourceDocumentType] OR d.[SourceDocumentId]<>i.[SourceDocumentId]
          OR d.[PostingAction]<>i.[PostingAction] OR d.[IdempotencyKey]<>i.[IdempotencyKey]
          OR d.[EventKind]<>i.[EventKind] OR d.[Version]<>i.[Version] OR d.[RootAccountingEventId]<>i.[RootAccountingEventId]
          OR ISNULL(d.[SupersedesAccountingEventId],'00000000-0000-0000-0000-000000000000')<>ISNULL(i.[SupersedesAccountingEventId],'00000000-0000-0000-0000-000000000000')
          OR ISNULL(d.[CorrectsAccountingEventId],'00000000-0000-0000-0000-000000000000')<>ISNULL(i.[CorrectsAccountingEventId],'00000000-0000-0000-0000-000000000000')
          OR ISNULL(d.[ReversesAccountingEventId],'00000000-0000-0000-0000-000000000000')<>ISNULL(i.[ReversesAccountingEventId],'00000000-0000-0000-0000-000000000000')
          OR d.[RequestFingerprint]<>i.[RequestFingerprint] OR d.[EventDate]<>i.[EventDate]
          OR d.[RequestedAtUtc]<>i.[RequestedAtUtc] OR d.[RequestedByUserId]<>i.[RequestedByUserId]
          OR d.[PreparedByUserId]<>i.[PreparedByUserId] OR d.[PreparedAtUtc]<>i.[PreparedAtUtc]
          OR (d.[AccountingBookSelectionEvidenceId] IS NOT NULL AND (i.[AccountingBookSelectionEvidenceId] IS NULL OR d.[AccountingBookSelectionEvidenceId]<>i.[AccountingBookSelectionEvidenceId] OR d.[SelectionFingerprint]<>i.[SelectionFingerprint]))
          OR (d.[ReleasedByUserId] IS NOT NULL AND (i.[ReleasedByUserId] IS NULL OR i.[ReleasedAtUtc] IS NULL OR i.[ReleaseReason] IS NULL OR d.[ReleasedByUserId]<>i.[ReleasedByUserId] OR d.[ReleasedAtUtc]<>i.[ReleasedAtUtc] OR d.[ReleaseReason]<>i.[ReleaseReason])))
        THROW 51000, 'C6_EVENT_IMMUTABLE: economic, version, reversal and preparer identity cannot be rewritten.', 1;

    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
       WHERE (d.[Status]=N'Posted' AND i.[Status]<>N'Posted')
          OR (d.[Status]=N'PendingApproval' AND i.[Status] NOT IN (N'PendingApproval',N'Pending',N'Failed'))
          OR (d.[Status]=N'Pending' AND i.[Status] NOT IN (N'Pending',N'Posted',N'Failed'))
          OR (d.[Status]=N'Failed' AND i.[Status] NOT IN (N'Failed',N'Pending')))
        THROW 51000, 'C6_EVENT_STATUS: only governed preparation, release, failure and retry transitions are permitted.', 1;

    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
       WHERE d.[Status]=i.[Status] AND d.[Status] IN (N'Posted',N'Failed') AND
         (ISNULL(d.[CompletedAtUtc],CONVERT(datetime2,'1900-01-01'))<>ISNULL(i.[CompletedAtUtc],CONVERT(datetime2,'1900-01-01'))
          OR ISNULL(d.[FailureMessage],N'')<>ISNULL(i.[FailureMessage],N'')))
        THROW 51000, 'C6_EVENT_OUTCOME_IMMUTABLE: persisted Posted and Failed outcome evidence cannot be rewritten.', 1;

    IF EXISTS (SELECT 1 FROM inserted i JOIN [AccountingEvents] p WITH (UPDLOCK,HOLDLOCK)
       ON p.[TenantId]=i.[TenantId] AND p.[Id]=i.[SupersedesAccountingEventId]
       WHERE i.[EventKind] IN (N'Correction',N'Reversal') AND (p.[Status]<>N'Posted'
          OR i.[RootAccountingEventId]<>p.[RootAccountingEventId] OR i.[Version]<>p.[Version]+1
          OR i.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2<>p.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(i.[OriginatingModuleCode])<>DATALENGTH(p.[OriginatingModuleCode])
          OR i.[SourceDocumentType] COLLATE Latin1_General_100_BIN2<>p.[SourceDocumentType] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(i.[SourceDocumentType])<>DATALENGTH(p.[SourceDocumentType])
          OR i.[SourceDocumentId]<>p.[SourceDocumentId]
          OR i.[PostingAction] COLLATE Latin1_General_100_BIN2<>p.[PostingAction] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(i.[PostingAction])<>DATALENGTH(p.[PostingAction])
          OR i.[AccountingBookSelectionEvidenceId]<>p.[AccountingBookSelectionEvidenceId]
          OR i.[SelectionFingerprint]<>p.[SelectionFingerprint]))
        THROW 51000, 'C6_EVENT_LINEAGE: successor must bind the posted predecessor canonical source, root, next version and frozen selection.', 1;

    IF EXISTS (SELECT 1 FROM [AccountingEvents] a WITH (UPDLOCK,HOLDLOCK)
       JOIN [AccountingEvents] b WITH (UPDLOCK,HOLDLOCK) ON b.[TenantId]=a.[TenantId]
        AND b.[SupersedesAccountingEventId]=a.[SupersedesAccountingEventId] AND b.[Id]<>a.[Id]
       WHERE a.[SupersedesAccountingEventId] IS NOT NULL)
        THROW 51000, 'C6_EVENT_LINEAGE: a predecessor may have only one canonical successor.', 1;

    IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN [AccountingBookSelectionEvidence] e WITH (UPDLOCK,HOLDLOCK)
       ON e.[TenantId]=i.[TenantId] AND e.[Id]=i.[AccountingBookSelectionEvidenceId]
       WHERE (i.[Status] IN (N'Pending',N'Posted') OR i.[AccountingBookSelectionEvidenceId] IS NOT NULL) AND (e.[Id] IS NULL
          OR e.[SelectionFingerprint]<>i.[SelectionFingerprint]
          OR e.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2<>i.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(e.[OriginatingModuleCode])<>DATALENGTH(i.[OriginatingModuleCode])
          OR e.[SourceDocumentType] COLLATE Latin1_General_100_BIN2<>i.[SourceDocumentType] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(e.[SourceDocumentType])<>DATALENGTH(i.[SourceDocumentType])
          OR e.[PostingAction] COLLATE Latin1_General_100_BIN2<>i.[PostingAction] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(e.[PostingAction])<>DATALENGTH(i.[PostingAction])
          OR (i.[EventKind]=N'Original' AND CONVERT(date,e.[EffectiveDate])<>i.[EventDate])))
        THROW 51000, 'C6_EVENT_SELECTION: released event identity must match its immutable C5 selection.', 1;

    IF EXISTS (SELECT 1 FROM inserted i WHERE i.[Status]=N'Posted' AND (
       NOT EXISTS (SELECT 1 FROM [AccountingEventPostings] p WHERE p.[TenantId]=i.[TenantId] AND p.[AccountingEventId]=i.[Id])
       OR EXISTS (SELECT eb.[AccountingBookId],eb.[SelectionOrder],eb.[AccountingBookCodeSnapshot],eb.[AuthorityFingerprint]
          FROM [AccountingBookSelectionEvidenceBooks] eb WHERE eb.[TenantId]=i.[TenantId] AND eb.[AccountingBookSelectionEvidenceId]=i.[AccountingBookSelectionEvidenceId]
          EXCEPT SELECT p.[AccountingBookId],p.[SelectionOrder],p.[AccountingBookCodeSnapshot],p.[AuthorityFingerprint]
          FROM [AccountingEventPostings] p WHERE p.[TenantId]=i.[TenantId] AND p.[AccountingEventId]=i.[Id] AND p.[Status]=N'Posted')
       OR EXISTS (SELECT p.[AccountingBookId],p.[SelectionOrder],p.[AccountingBookCodeSnapshot],p.[AuthorityFingerprint]
          FROM [AccountingEventPostings] p WHERE p.[TenantId]=i.[TenantId] AND p.[AccountingEventId]=i.[Id] AND p.[Status]=N'Posted'
          EXCEPT SELECT eb.[AccountingBookId],eb.[SelectionOrder],eb.[AccountingBookCodeSnapshot],eb.[AuthorityFingerprint]
          FROM [AccountingBookSelectionEvidenceBooks] eb WHERE eb.[TenantId]=i.[TenantId] AND eb.[AccountingBookSelectionEvidenceId]=i.[AccountingBookSelectionEvidenceId])))
        THROW 51000, 'C6_EVENT_RELEASE: every and only frozen selected-book representation must be posted.', 1;
END;");

        migrationBuilder.Sql(@"CREATE TRIGGER [TR_AccountingEventPostings_C6Authority]
ON [AccountingEventPostings] AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id]=d.[Id] WHERE i.[Id] IS NULL)
        THROW 51000, 'C6_POSTING_IMMUTABLE: exact-book evidence cannot be deleted.', 1;
    IF EXISTS (SELECT 1 FROM deleted d JOIN inserted i ON i.[Id]=d.[Id]
       WHERE d.[TenantId]<>i.[TenantId] OR d.[AccountingEventId]<>i.[AccountingEventId]
          OR d.[EventVersion]<>i.[EventVersion] OR d.[AccountingBookId]<>i.[AccountingBookId]
          OR d.[SelectionOrder]<>i.[SelectionOrder] OR d.[AccountingBookCodeSnapshot]<>i.[AccountingBookCodeSnapshot]
          OR d.[AuthorityFingerprint]<>i.[AuthorityFingerprint] OR d.[Status]<>N'Pending' OR i.[Status] NOT IN (N'Posted',N'Failed'))
        THROW 51000, 'C6_POSTING_IMMUTABLE: only Pending-to-final result completion is permitted.', 1;
    IF EXISTS (SELECT 1 FROM inserted p WHERE p.[Status]<>N'Posted'
       AND (p.[FinancePostingEventId] IS NOT NULL OR p.[JournalEntryId] IS NOT NULL OR p.[PostedAtUtc] IS NOT NULL))
        THROW 51000, 'C6_POSTING_RESULT: only a Posted exact-book representation may own leaf result evidence.', 1;
    IF EXISTS (SELECT 1 FROM inserted p JOIN [AccountingEvents] e WITH (UPDLOCK,HOLDLOCK)
       ON e.[TenantId]=p.[TenantId] AND e.[Id]=p.[AccountingEventId]
       LEFT JOIN [AccountingBookSelectionEvidenceBooks] eb WITH (UPDLOCK,HOLDLOCK)
       ON eb.[TenantId]=p.[TenantId] AND eb.[AccountingBookSelectionEvidenceId]=e.[AccountingBookSelectionEvidenceId]
        AND eb.[AccountingBookId]=p.[AccountingBookId] AND eb.[SelectionOrder]=p.[SelectionOrder]
       WHERE e.[Version]<>p.[EventVersion] OR e.[AccountingBookSelectionEvidenceId] IS NULL OR eb.[Id] IS NULL
        OR eb.[AccountingBookCodeSnapshot]<>p.[AccountingBookCodeSnapshot] OR eb.[AuthorityFingerprint]<>p.[AuthorityFingerprint])
        THROW 51000, 'C6_POSTING_SELECTION: per-book evidence must exactly match the frozen C5 coordinate.', 1;
    IF EXISTS (SELECT 1 FROM inserted p
       JOIN [AccountingEvents] e ON e.[TenantId]=p.[TenantId] AND e.[Id]=p.[AccountingEventId] AND e.[Version]=p.[EventVersion]
       LEFT JOIN [AccountingEventPostings] predecessor ON predecessor.[TenantId]=p.[TenantId]
        AND predecessor.[AccountingEventId]=e.[ReversesAccountingEventId] AND predecessor.[AccountingBookId]=p.[AccountingBookId]
        AND predecessor.[EventVersion]=e.[Version]-1 AND predecessor.[Status]=N'Posted'
       LEFT JOIN [FinancePostingEvents] f ON f.[TenantId]=p.[TenantId] AND f.[Id]=p.[FinancePostingEventId]
       LEFT JOIN [JournalEntries] j ON j.[TenantId]=p.[TenantId] AND j.[Id]=p.[JournalEntryId]
       WHERE p.[Status]=N'Posted' AND (f.[Id] IS NULL OR j.[Id] IS NULL OR f.[AccountingBookId]<>p.[AccountingBookId]
        OR j.[AccountingBookId]<>p.[AccountingBookId] OR f.[JournalEntryId] IS NULL OR f.[JournalEntryId]<>p.[JournalEntryId]
        OR f.[PostingStatus]<>N'Posted' OR j.[PostingStatus]<>N'Posted'
        OR (e.[EventKind]=N'Original' AND (
             ISNULL(f.[OriginModuleCode],f.[SourceModule]) COLLATE Latin1_General_100_BIN2<>e.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(ISNULL(f.[OriginModuleCode],f.[SourceModule]))<>DATALENGTH(e.[OriginatingModuleCode])
          OR f.[SourceDocumentType] COLLATE Latin1_General_100_BIN2<>e.[SourceDocumentType] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(f.[SourceDocumentType])<>DATALENGTH(e.[SourceDocumentType]) OR f.[SourceDocumentId]<>e.[SourceDocumentId]
          OR f.[PostingAction] COLLATE Latin1_General_100_BIN2<>e.[PostingAction] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(f.[PostingAction])<>DATALENGTH(e.[PostingAction])))
        OR (e.[EventKind]=N'Correction' AND (
             f.[SourceModule] COLLATE Latin1_General_100_BIN2<>N'GL' OR DATALENGTH(f.[SourceModule])<>DATALENGTH(N'GL')
          OR f.[OriginModuleCode] COLLATE Latin1_General_100_BIN2<>N'FIN' OR DATALENGTH(f.[OriginModuleCode])<>DATALENGTH(N'FIN')
          OR f.[SourceDocumentType] COLLATE Latin1_General_100_BIN2<>N'AccountingEventCorrection' OR DATALENGTH(f.[SourceDocumentType])<>DATALENGTH(N'AccountingEventCorrection')
          OR f.[SourceDocumentId]<>e.[Id]
          OR f.[PostingAction] COLLATE Latin1_General_100_BIN2<>e.[PostingAction] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(f.[PostingAction])<>DATALENGTH(e.[PostingAction])))
        OR (e.[EventKind]=N'Reversal' AND (
             predecessor.[Id] IS NULL OR predecessor.[FinancePostingEventId] IS NULL OR f.[SourceDocumentId]<>predecessor.[FinancePostingEventId]
          OR f.[SourceModule] COLLATE Latin1_General_100_BIN2<>N'GL' OR DATALENGTH(f.[SourceModule])<>DATALENGTH(N'GL')
          OR f.[OriginModuleCode] COLLATE Latin1_General_100_BIN2<>N'FIN' OR DATALENGTH(f.[OriginModuleCode])<>DATALENGTH(N'FIN')
          OR f.[SourceDocumentType] COLLATE Latin1_General_100_BIN2<>N'FinancePostingEventReversal' OR DATALENGTH(f.[SourceDocumentType])<>DATALENGTH(N'FinancePostingEventReversal')
          OR f.[PostingAction] COLLATE Latin1_General_100_BIN2<>N'Reverse' OR DATALENGTH(f.[PostingAction])<>DATALENGTH(N'Reverse')))))
        THROW 51000, 'C6_POSTING_RESULT: leaf journal and posting event must be Posted in the exact selected book.', 1;
END;");

        migrationBuilder.Sql(@"CREATE TRIGGER [TR_AccountingEventAttempts_C6AppendOnly]
ON [AccountingEventAttempts] AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted)
        THROW 51000, 'C6_ATTEMPT_IMMUTABLE: final attempt outcomes are append-only.', 1;
    IF EXISTS (SELECT 1 FROM inserted WHERE [Status]=N'Pending')
        THROW 51000, 'C6_ATTEMPT_FINAL_REQUIRED: attempt evidence must be inserted once with its final outcome.', 1;
    IF EXISTS (SELECT 1 FROM inserted a JOIN [AccountingEvents] e
       ON e.[TenantId]=a.[TenantId] AND e.[Id]=a.[AccountingEventId]
       WHERE a.[RequestFingerprint]<>e.[RequestFingerprint])
        THROW 51000, 'C6_ATTEMPT_IDENTITY: attempt fingerprint must match the immutable event request.', 1;
END;");

        migrationBuilder.Sql(@"CREATE TRIGGER [TR_AccountingEventProducerReceipts_C7Immutable]
ON [AccountingEventProducerReceipts] AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted)
        THROW 51000, 'C7_RECEIPT_IMMUTABLE: producer receipt evidence is append-only.', 1;
    IF EXISTS (SELECT 1 FROM inserted r LEFT JOIN [AccountingEvents] e
      ON e.[TenantId]=r.[TenantId] AND e.[Id]=r.[AccountingEventId]
      WHERE e.[Id] IS NULL OR e.[ProducerDecisionStatus]<>N'Approved'
         OR e.[Status] NOT IN (N'Pending',N'Posted')
         OR e.[ProducerParticipantIdentity]<>r.[ParticipantCode]
         OR e.[RequestFingerprint]<>r.[RequestFingerprint]
         OR r.[OwnerEntityId]='00000000-0000-0000-0000-000000000000'
         OR LEN(LTRIM(RTRIM(r.[OwnerEntityType])))=0 OR LEN(LTRIM(RTRIM(r.[OwnerAction])))=0)
        THROW 51000, 'C7_RECEIPT_AUTHORITY: receipt must match one approved producer event in execution.', 1;
END;");

        migrationBuilder.Sql(@"CREATE TRIGGER [TR_AccountingEvents_C7ProducerDecision]
ON [AccountingEvents] AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.[Id]=i.[Id]
       WHERE d.[Id] IS NULL AND i.[ProducerDecisionStatus] NOT IN (N'NotRequired',N'Pending'))
        THROW 51000, 'C7_INSERT_STATE: producer decisions cannot be fabricated during prepare.', 1;

    IF EXISTS (SELECT 1 FROM inserted i WHERE i.[ProducerDecisionStatus] <> N'NotRequired' AND
      (i.[ProducerParticipantIdentity] COLLATE Latin1_General_100_BIN2 <> UPPER(LTRIM(RTRIM(i.[ProducerParticipantIdentity]))) COLLATE Latin1_General_100_BIN2
       OR DATALENGTH(i.[ProducerParticipantIdentity]) <> DATALENGTH(UPPER(LTRIM(RTRIM(i.[ProducerParticipantIdentity]))))
       OR LEFT(i.[ProducerParticipantIdentity],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
       OR i.[ProducerParticipantIdentity] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'))
        THROW 51000, 'C7_PARTICIPANT_IDENTITY: canonical stable participant identity is required.', 1;

    IF EXISTS (SELECT 1 FROM inserted i WHERE i.[ProducerDecisionStatus] IN (N'Approved',N'Rejected')
       AND (i.[ProducerDecidedByUserId] = i.[PreparedByUserId] OR LEN(LTRIM(RTRIM(i.[ProducerDecisionReason]))) = 0))
        THROW 51000, 'C7_MAKER_CHECKER: a distinct checker and governed reason are required.', 1;

    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id] WHERE
       ISNULL(d.[ProducerParticipantIdentity],N'') <> ISNULL(i.[ProducerParticipantIdentity],N'')
       OR ISNULL(d.[ProducerIntentSnapshotHash],N'') <> ISNULL(i.[ProducerIntentSnapshotHash],N'')
       OR ISNULL(d.[ProducerIntentSnapshotJson],N'') <> ISNULL(i.[ProducerIntentSnapshotJson],N'')
       OR (d.[ProducerDecisionStatus] = N'Pending' AND i.[ProducerDecisionStatus] NOT IN (N'Pending',N'Approved',N'Rejected'))
       OR (d.[ProducerDecisionStatus] IN (N'NotRequired',N'Approved',N'Rejected') AND d.[ProducerDecisionStatus] <> i.[ProducerDecisionStatus])
       OR (d.[ProducerDecidedByUserId] IS NOT NULL AND (i.[ProducerDecidedByUserId] IS NULL OR d.[ProducerDecidedByUserId] <> i.[ProducerDecidedByUserId]
          OR d.[ProducerDecidedAtUtc] <> i.[ProducerDecidedAtUtc] OR d.[ProducerDecisionReason] <> i.[ProducerDecisionReason])))
        THROW 51000, 'C7_DECISION_IMMUTABLE: participant and maker-checker decision evidence cannot be rewritten.', 1;

    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id] WHERE
       d.[ProducerDecisionStatus] = N'Pending' AND i.[ProducerDecisionStatus] IN (N'Approved',N'Rejected')
       AND (d.[Status] <> N'PendingApproval' OR i.[Status] <> N'PendingApproval'))
        THROW 51000, 'C7_DECISION_ONLY: approval or rejection cannot execute C6 effects in the same statement.', 1;

    IF EXISTS (SELECT 1 FROM inserted i WHERE
       (i.[ProducerDecisionStatus] = N'Pending' AND i.[Status] <> N'PendingApproval')
       OR (i.[ProducerDecisionStatus] = N'Rejected' AND i.[Status] <> N'PendingApproval')
       OR (i.[ProducerDecisionStatus] = N'Approved' AND i.[Status] NOT IN (N'PendingApproval',N'Pending',N'Posted',N'Failed')))
        THROW 51000, 'C7_EXECUTION_GATE: only an approved producer intent may enter C6 execution.', 1;
END;");

        migrationBuilder.Sql(@"CREATE TRIGGER [TR_ProducerIntentGroupMembers_C8Immutable] ON [ProducerIntentGroupMembers] AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51000, 'C8_MEMBER_IMMUTABLE: group membership is append-only.', 1;
 IF EXISTS(SELECT 1 FROM inserted m JOIN [ProducerIntentGroups] g ON g.[TenantId]=m.[TenantId] AND g.[Id]=m.[ProducerIntentGroupId]
   LEFT JOIN [AccountingEvents] e ON e.[TenantId]=m.[TenantId] AND e.[Id]=m.[AccountingEventId]
   WHERE g.[Status]<>N'PendingApproval' OR m.[MemberOrder]>g.[MemberCount] OR e.[Id] IS NULL
      OR e.[Status]<>N'PendingApproval' OR e.[ProducerDecisionStatus]<>N'Pending'
      OR e.[EventKind]<>g.[GroupKind] OR e.[Version]<>g.[Version]
      OR e.[IdempotencyKey] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(e.[IdempotencyKey]))) COLLATE Latin1_General_100_BIN2
      OR DATALENGTH(e.[IdempotencyKey])<>DATALENGTH(UPPER(LTRIM(RTRIM(e.[IdempotencyKey]))))
      OR DATALENGTH(e.[IdempotencyKey])<>LEN(e.[IdempotencyKey])*2
      OR LEFT(e.[IdempotencyKey],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z0-9]'
      OR e.[IdempotencyKey] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.:-]%'
      OR e.[IdempotencyKey] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')
      OR e.[RequestFingerprint]<>m.[MemberFingerprint] OR e.[ProducerParticipantIdentity]<>g.[ParticipantCode])
  THROW 51000, 'C8_MEMBER_AUTHORITY: ordered members must bind pending C7 events and the group participant.', 1;
 IF EXISTS(SELECT 1 FROM inserted m JOIN [ProducerIntentGroups] g ON g.[TenantId]=m.[TenantId] AND g.[Id]=m.[ProducerIntentGroupId]
   LEFT JOIN [ProducerIntentGroupMembers] pm ON pm.[TenantId]=g.[TenantId] AND pm.[ProducerIntentGroupId]=g.[SupersedesProducerIntentGroupId] AND pm.[MemberOrder]=m.[MemberOrder]
   LEFT JOIN [AccountingEvents] e ON e.[TenantId]=m.[TenantId] AND e.[Id]=m.[AccountingEventId]
   LEFT JOIN [AccountingEvents] pe ON pe.[TenantId]=pm.[TenantId] AND pe.[Id]=pm.[AccountingEventId]
   WHERE g.[GroupKind]<>N'Original' AND (pm.[Id] IS NULL OR pe.[Status]<>N'Posted'
     OR e.[SupersedesAccountingEventId]<>pe.[Id] OR e.[RootAccountingEventId]<>pe.[RootAccountingEventId]
     OR e.[Version]<>pe.[Version]+1
     OR e.[OriginatingModuleCode]<>pe.[OriginatingModuleCode] OR e.[SourceDocumentType]<>pe.[SourceDocumentType]
     OR e.[SourceDocumentId]<>pe.[SourceDocumentId] OR e.[PostingAction]<>pe.[PostingAction]
     OR (g.[GroupKind]=N'Correction' AND (e.[CorrectsAccountingEventId]<>pe.[Id] OR e.[ReversesAccountingEventId] IS NOT NULL))
     OR (g.[GroupKind]=N'Reversal' AND (e.[ReversesAccountingEventId]<>pe.[Id] OR e.[CorrectsAccountingEventId] IS NOT NULL))))
  THROW 51000, 'C8_MEMBER_LINEAGE: successor members must bind exact same-order posted predecessor roots.', 1;
END;");

        migrationBuilder.Sql(@"CREATE TRIGGER [TR_ProducerIntentGroupReceipts_C8Immutable] ON [ProducerIntentGroupReceipts] AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51000, 'C8_RECEIPT_IMMUTABLE: group receipt evidence is append-only.', 1;
  IF EXISTS(SELECT 1 FROM inserted r WHERE
   r.[ParticipantCode] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(r.[ParticipantCode]))) COLLATE Latin1_General_100_BIN2
   OR DATALENGTH(r.[ParticipantCode])<>DATALENGTH(UPPER(LTRIM(RTRIM(r.[ParticipantCode]))))
   OR DATALENGTH(r.[ParticipantCode])<>LEN(r.[ParticipantCode])*2
   OR LEFT(r.[ParticipantCode],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
   OR r.[ParticipantCode] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
   OR r.[ParticipantCode] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')
   OR r.[OwnerEntityType] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(r.[OwnerEntityType]))) COLLATE Latin1_General_100_BIN2
   OR DATALENGTH(r.[OwnerEntityType])<>DATALENGTH(UPPER(LTRIM(RTRIM(r.[OwnerEntityType])))) OR DATALENGTH(r.[OwnerEntityType])<>LEN(r.[OwnerEntityType])*2
   OR LEFT(r.[OwnerEntityType],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
   OR r.[OwnerEntityType] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
   OR r.[OwnerEntityType] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')
   OR r.[OwnerAction] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(r.[OwnerAction]))) COLLATE Latin1_General_100_BIN2
   OR DATALENGTH(r.[OwnerAction])<>DATALENGTH(UPPER(LTRIM(RTRIM(r.[OwnerAction])))) OR DATALENGTH(r.[OwnerAction])<>LEN(r.[OwnerAction])*2
   OR LEFT(r.[OwnerAction],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
   OR r.[OwnerAction] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
   OR r.[OwnerAction] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS'))
   THROW 51000, 'C8_RECEIPT_IDENTITY: receipt identities must use the reviewed canonical grammar.', 1;
  DECLARE @ownerTenant uniqueidentifier, @ownerParticipant nvarchar(100), @ownerEffect char(64), @ownerResource nvarchar(255), @ownerResult int;
  DECLARE owner_effects CURSOR LOCAL FAST_FORWARD FOR
   SELECT DISTINCT [TenantId],[ParticipantCode],[EffectFingerprint] FROM inserted ORDER BY [TenantId],[ParticipantCode],[EffectFingerprint];
  OPEN owner_effects; FETCH NEXT FROM owner_effects INTO @ownerTenant,@ownerParticipant,@ownerEffect;
  WHILE @@FETCH_STATUS=0 BEGIN
   SET @ownerResource=CONCAT(N'FIN:C7C8:',CONVERT(nvarchar(36),@ownerTenant),N'|',@ownerParticipant,N'|',@ownerEffect);
   EXEC @ownerResult=sp_getapplock @Resource=@ownerResource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;
   IF @ownerResult<0 BEGIN CLOSE owner_effects; DEALLOCATE owner_effects; THROW 51000, 'C8_RECEIPT_LOCK_FAILED: owner-effect identity could not be serialized.', 1; END;
   FETCH NEXT FROM owner_effects INTO @ownerTenant,@ownerParticipant,@ownerEffect;
  END; CLOSE owner_effects; DEALLOCATE owner_effects;
  IF EXISTS(SELECT 1 FROM inserted r JOIN [ProducerIntentGroups] g ON g.[TenantId]=r.[TenantId] AND g.[Id]=r.[ProducerIntentGroupId]
   WHERE g.[Status] NOT IN (N'Approved',N'Failed') OR g.[GroupFingerprint]<>r.[GroupFingerprint]
    OR g.[ParticipantCode]<>r.[ParticipantCode] OR g.[OwnerEntityType]<>r.[OwnerEntityType]
    OR g.[OwnerEntityId]<>r.[OwnerEntityId] OR g.[OwnerAction]<>r.[OwnerAction]
    OR g.[ExpectedOwnerEffectFingerprint]<>r.[EffectFingerprint])
  THROW 51000, 'C8_RECEIPT_AUTHORITY: one approved immutable group must own the exact receipt.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN [AccountingEventProducerReceipts] r ON r.[TenantId]=i.[TenantId]
   AND r.[ParticipantCode]=i.[ParticipantCode] AND r.[EffectFingerprint]=i.[EffectFingerprint])
  THROW 51000, 'C8_RECEIPT_REUSED: owner-effect evidence already belongs to a single event.', 1;
END;");

        // Baseline-only grammar correction for archived 20260908120000_AddProducerIntentGroupsC8:
        // close the outer IF EXISTS before THROW; the governed predicate and trigger behavior are unchanged.
        migrationBuilder.Sql(@"CREATE TRIGGER [TR_ProducerIntentGroupAttempts_C8Immutable] ON [ProducerIntentGroupAttempts] INSTEAD OF INSERT AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted GROUP BY [TenantId],[ProducerIntentGroupId] HAVING COUNT(*)<>1)
  THROW 51000, 'C8_ATTEMPT_CARDINALITY: one terminal transition requires exactly one attempt.', 1;
 IF EXISTS(SELECT 1 FROM inserted a LEFT JOIN [ProducerIntentGroups] g WITH (UPDLOCK,HOLDLOCK)
   ON g.[TenantId]=a.[TenantId] AND g.[Id]=a.[ProducerIntentGroupId]
  WHERE g.[Id] IS NULL OR a.[Status] NOT IN (N'Posted',N'Failed') OR g.[GroupFingerprint]<>a.[GroupFingerprint]
   OR a.[AttemptNumber]<>(SELECT ISNULL(MAX(prior.[AttemptNumber]),0)+1 FROM [ProducerIntentGroupAttempts] prior
      WHERE prior.[TenantId]=a.[TenantId] AND prior.[ProducerIntentGroupId]=a.[ProducerIntentGroupId])
   OR (a.[Status]=N'Failed' AND (g.[Status]<>N'Approved' OR a.[FailureMessage] IS NULL
      OR (a.[FailedMemberOrder] IS NULL AND a.[FailedAccountingEventId] IS NOT NULL)
      OR (a.[FailedMemberOrder] IS NOT NULL AND a.[FailedAccountingEventId] IS NULL)
      OR (a.[FailedMemberOrder] IS NOT NULL AND NOT EXISTS(SELECT 1 FROM [ProducerIntentGroupMembers] failed
         WHERE failed.[TenantId]=g.[TenantId] AND failed.[ProducerIntentGroupId]=g.[Id]
          AND failed.[MemberOrder]=a.[FailedMemberOrder] AND failed.[AccountingEventId]=a.[FailedAccountingEventId])))
   OR (a.[Status]=N'Posted' AND (g.[Status] NOT IN (N'Approved',N'Failed') OR a.[FailureMessage] IS NOT NULL
      OR a.[FailedMemberOrder] IS NOT NULL OR a.[FailedAccountingEventId] IS NOT NULL
      OR NOT EXISTS(SELECT 1 FROM [ProducerIntentGroupReceipts] r WHERE r.[TenantId]=g.[TenantId] AND r.[ProducerIntentGroupId]=g.[Id])
      OR EXISTS(SELECT 1 FROM [ProducerIntentGroupMembers] m JOIN [AccountingEvents] e
         ON e.[TenantId]=m.[TenantId] AND e.[Id]=m.[AccountingEventId]
         WHERE m.[TenantId]=g.[TenantId] AND m.[ProducerIntentGroupId]=g.[Id] AND e.[Status]<>N'Posted')))))
  THROW 51000, 'C8_ATTEMPT_AUTHORITY: one exact terminal attempt must atomically drive an authorized group transition.', 1;
 INSERT [ProducerIntentGroupAttempts]([Id],[ProducerIntentGroupId],[AttemptNumber],[GroupFingerprint],[Status],[StartedAtUtc],
  [CompletedAtUtc],[FailedMemberOrder],[FailedAccountingEventId],[FailureMessage],[CreatedAt],[UpdatedAt],[CreatedBy],[UpdatedBy],
  [CreatedById],[LastModifiedById],[IsDeleted],[DeletedAt],[DeletedBy],[TenantId])
 SELECT [Id],[ProducerIntentGroupId],[AttemptNumber],[GroupFingerprint],[Status],[StartedAtUtc],
  [CompletedAtUtc],[FailedMemberOrder],[FailedAccountingEventId],[FailureMessage],[CreatedAt],[UpdatedAt],[CreatedBy],[UpdatedBy],
  [CreatedById],[LastModifiedById],[IsDeleted],[DeletedAt],[DeletedBy],[TenantId] FROM inserted;
 UPDATE g SET [Status]=a.[Status],[CompletedAtUtc]=a.[CompletedAtUtc],
  [FailureMessage]=CASE WHEN a.[Status]=N'Failed' THEN a.[FailureMessage] ELSE NULL END
 FROM [ProducerIntentGroups] g JOIN inserted a ON a.[TenantId]=g.[TenantId] AND a.[ProducerIntentGroupId]=g.[Id];
END;");

        migrationBuilder.Sql(@"CREATE TRIGGER [TR_ProducerIntentGroupAttempts_C8NoMutation] ON [ProducerIntentGroupAttempts] AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 THROW 51000, 'C8_ATTEMPT_IMMUTABLE: durable group attempts are append-only.', 1;
END;");

        migrationBuilder.Sql(@"CREATE TRIGGER [TR_ProducerIntentGroups_C8Authority] ON [ProducerIntentGroups] AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id]=d.[Id] WHERE i.[Id] IS NULL)
  THROW 51000, 'C8_GROUP_IMMUTABLE: producer groups cannot be deleted.', 1;
 IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.[Id]=i.[Id] WHERE d.[Id] IS NULL AND i.[Status]<>N'PendingApproval')
  THROW 51000, 'C8_GROUP_INSERT_STATE: decision or execution cannot be preseeded.', 1;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.[ParticipantCode] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[ParticipantCode]))) COLLATE Latin1_General_100_BIN2
   OR DATALENGTH(i.[ParticipantCode])<>DATALENGTH(UPPER(LTRIM(RTRIM(i.[ParticipantCode]))))
   OR DATALENGTH(i.[ParticipantCode])<>LEN(i.[ParticipantCode])*2
   OR LEFT(i.[ParticipantCode],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
   OR i.[ParticipantCode] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
   OR i.[ParticipantCode] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')
   OR i.[IdempotencyKey] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[IdempotencyKey]))) COLLATE Latin1_General_100_BIN2
   OR DATALENGTH(i.[IdempotencyKey])<>DATALENGTH(UPPER(LTRIM(RTRIM(i.[IdempotencyKey]))))
   OR DATALENGTH(i.[IdempotencyKey])<>LEN(i.[IdempotencyKey])*2
   OR LEFT(i.[IdempotencyKey],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z0-9]'
   OR i.[IdempotencyKey] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.:-]%'
   OR i.[IdempotencyKey] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')
   OR i.[OwnerEntityType] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[OwnerEntityType]))) COLLATE Latin1_General_100_BIN2
   OR DATALENGTH(i.[OwnerEntityType])<>DATALENGTH(UPPER(LTRIM(RTRIM(i.[OwnerEntityType])))) OR DATALENGTH(i.[OwnerEntityType])<>LEN(i.[OwnerEntityType])*2
   OR LEFT(i.[OwnerEntityType],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
   OR i.[OwnerEntityType] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
   OR i.[OwnerEntityType] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')
   OR i.[OwnerAction] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[OwnerAction]))) COLLATE Latin1_General_100_BIN2
   OR DATALENGTH(i.[OwnerAction])<>DATALENGTH(UPPER(LTRIM(RTRIM(i.[OwnerAction])))) OR DATALENGTH(i.[OwnerAction])<>LEN(i.[OwnerAction])*2
   OR LEFT(i.[OwnerAction],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
   OR i.[OwnerAction] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
   OR i.[OwnerAction] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')
   OR i.[OwnerEntityId]='00000000-0000-0000-0000-000000000000')
  THROW 51000, 'C8_GROUP_IDENTITY: canonical participant and owner identity are required.', 1;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.[Status] IN (N'Approved',N'Rejected',N'Posted',N'Failed')
   AND (i.[DecidedByUserId]=i.[PreparedByUserId] OR LEN(LTRIM(RTRIM(i.[DecisionReason])))=0))
  THROW 51000, 'C8_GROUP_MAKER_CHECKER: a distinct checker and governed reason are required.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id] WHERE
   d.[TenantId]<>i.[TenantId] OR d.[IdempotencyKey]<>i.[IdempotencyKey] OR d.[GroupKind]<>i.[GroupKind]
   OR d.[Version]<>i.[Version] OR d.[RootProducerIntentGroupId]<>i.[RootProducerIntentGroupId]
   OR ISNULL(d.[SupersedesProducerIntentGroupId],'00000000-0000-0000-0000-000000000000')<>ISNULL(i.[SupersedesProducerIntentGroupId],'00000000-0000-0000-0000-000000000000')
   OR ISNULL(d.[CorrectsProducerIntentGroupId],'00000000-0000-0000-0000-000000000000')<>ISNULL(i.[CorrectsProducerIntentGroupId],'00000000-0000-0000-0000-000000000000')
   OR ISNULL(d.[ReversesProducerIntentGroupId],'00000000-0000-0000-0000-000000000000')<>ISNULL(i.[ReversesProducerIntentGroupId],'00000000-0000-0000-0000-000000000000')
   OR d.[MemberCount]<>i.[MemberCount] OR d.[ParticipantCode]<>i.[ParticipantCode] OR d.[OwnerEntityType]<>i.[OwnerEntityType]
   OR d.[OwnerEntityId]<>i.[OwnerEntityId] OR d.[OwnerAction]<>i.[OwnerAction]
   OR d.[ExpectedOwnerEffectFingerprint]<>i.[ExpectedOwnerEffectFingerprint] OR d.[RequestSnapshotJson]<>i.[RequestSnapshotJson]
   OR d.[RequestSnapshotHash]<>i.[RequestSnapshotHash] OR d.[GroupFingerprint]<>i.[GroupFingerprint]
   OR d.[PreparedByUserId]<>i.[PreparedByUserId] OR d.[PreparedAtUtc]<>i.[PreparedAtUtc]
   OR (d.[DecidedByUserId] IS NOT NULL AND (i.[DecidedByUserId]<>d.[DecidedByUserId] OR i.[DecidedAtUtc]<>d.[DecidedAtUtc] OR i.[DecisionReason]<>d.[DecisionReason])))
  THROW 51000, 'C8_GROUP_EVIDENCE_IMMUTABLE: identity, lineage, membership, snapshot and decision cannot be rewritten.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
   WHERE (d.[Status]=N'PendingApproval' AND i.[Status] NOT IN (N'PendingApproval',N'Approved',N'Rejected'))
      OR (d.[Status]=N'Approved' AND i.[Status] NOT IN (N'Approved',N'Posted',N'Failed'))
      OR (d.[Status]=N'Failed' AND i.[Status] NOT IN (N'Failed',N'Posted'))
       OR (d.[Status] IN (N'Rejected',N'Posted') AND d.[Status]<>i.[Status]))
   THROW 51000, 'C8_GROUP_WORKFLOW: combined decision/execution and invalid transitions are forbidden.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
   WHERE i.[Status] IN (N'Posted',N'Failed') AND d.[Status]<>i.[Status]
    AND NOT EXISTS(SELECT 1 FROM [ProducerIntentGroupAttempts] a
      WHERE a.[TenantId]=i.[TenantId] AND a.[ProducerIntentGroupId]=i.[Id]
       AND a.[AttemptNumber]=(SELECT MAX(latest.[AttemptNumber]) FROM [ProducerIntentGroupAttempts] latest
          WHERE latest.[TenantId]=i.[TenantId] AND latest.[ProducerIntentGroupId]=i.[Id])
       AND a.[GroupFingerprint]=i.[GroupFingerprint] AND a.[Status]=i.[Status]
       AND a.[CompletedAtUtc]=i.[CompletedAtUtc]
       AND ISNULL(a.[FailureMessage],N'')=ISNULL(i.[FailureMessage],N'')))
  THROW 51000, 'C8_GROUP_ATTEMPT_REQUIRED: terminal transition requires its exact immutable attempt in the same operation.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
   WHERE d.[Status] IN (N'Rejected',N'Posted',N'Failed') AND i.[Status]=d.[Status]
    AND (ISNULL(i.[CompletedAtUtc],'0001-01-01')<>ISNULL(d.[CompletedAtUtc],'0001-01-01')
      OR ISNULL(i.[FailureMessage],N'')<>ISNULL(d.[FailureMessage],N'')))
  THROW 51000, 'C8_GROUP_OUTCOME_IMMUTABLE: terminal outcome evidence cannot be rewritten.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
   WHERE d.[Status]=N'Failed' AND i.[Status]=N'Posted'
    AND (i.[CompletedAtUtc]<=d.[CompletedAtUtc] OR i.[FailureMessage] IS NOT NULL))
  THROW 51000, 'C8_GROUP_RECOVERY: recovery must record a later posted completion and retain failed-attempt evidence.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
   WHERE d.[Status]=N'PendingApproval' AND i.[Status] IN (N'Approved',N'Rejected')
    AND ((SELECT COUNT(*) FROM [ProducerIntentGroupMembers] m WHERE m.[TenantId]=i.[TenantId] AND m.[ProducerIntentGroupId]=i.[Id])<>i.[MemberCount]
      OR EXISTS(SELECT 1 FROM [ProducerIntentGroupMembers] m JOIN [AccountingEvents] e ON e.[TenantId]=m.[TenantId] AND e.[Id]=m.[AccountingEventId]
          WHERE m.[TenantId]=i.[TenantId] AND m.[ProducerIntentGroupId]=i.[Id] AND (e.[Status]<>N'PendingApproval' OR e.[ProducerDecisionStatus]<>N'Pending'))))
  THROW 51000, 'C8_GROUP_COMPLETE: the checker decides one complete pending immutable member set.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id] WHERE i.[Status]=N'Posted' AND d.[Status]<>N'Posted'
   AND (NOT EXISTS(SELECT 1 FROM [ProducerIntentGroupReceipts] r WHERE r.[TenantId]=i.[TenantId] AND r.[ProducerIntentGroupId]=i.[Id])
    OR EXISTS(SELECT 1 FROM [ProducerIntentGroupMembers] m JOIN [AccountingEvents] e ON e.[TenantId]=m.[TenantId] AND e.[Id]=m.[AccountingEventId]
       WHERE m.[TenantId]=i.[TenantId] AND m.[ProducerIntentGroupId]=i.[Id] AND e.[Status]<>N'Posted')))
  THROW 51000, 'C8_GROUP_OUTCOME: every member and the exact receipt must post before the group.', 1;
 IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN [ProducerIntentGroups] p ON p.[TenantId]=i.[TenantId] AND p.[Id]=i.[SupersedesProducerIntentGroupId]
   WHERE i.[GroupKind]<>N'Original' AND (p.[Id] IS NULL OR p.[Status]<>N'Posted'
     OR i.[Version]<>p.[Version]+1 OR i.[RootProducerIntentGroupId]<>p.[RootProducerIntentGroupId]
     OR (i.[GroupKind]=N'Correction' AND i.[CorrectsProducerIntentGroupId]<>p.[Id])
     OR (i.[GroupKind]=N'Reversal' AND i.[ReversesProducerIntentGroupId]<>p.[Id])))
  THROW 51000, 'C8_GROUP_LINEAGE: successor requires the exact posted predecessor version and root.', 1;
END;");

        migrationBuilder.Sql(@"ALTER TRIGGER [TR_AccountingEventProducerReceipts_C7Immutable] ON [AccountingEventProducerReceipts] AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51000, 'C7_RECEIPT_IMMUTABLE: producer receipt evidence is append-only.', 1;
  IF EXISTS(SELECT 1 FROM inserted r LEFT JOIN [AccountingEvents] e ON e.[TenantId]=r.[TenantId] AND e.[Id]=r.[AccountingEventId]
   WHERE e.[Id] IS NULL OR e.[ProducerDecisionStatus]<>N'Approved' OR e.[Status] NOT IN (N'Pending',N'Posted')
    OR e.[ProducerParticipantIdentity]<>r.[ParticipantCode] OR e.[RequestFingerprint]<>r.[RequestFingerprint]
     OR r.[OwnerEntityId]='00000000-0000-0000-0000-000000000000'
     OR r.[ParticipantCode] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(r.[ParticipantCode]))) COLLATE Latin1_General_100_BIN2
     OR DATALENGTH(r.[ParticipantCode])<>DATALENGTH(UPPER(LTRIM(RTRIM(r.[ParticipantCode])))) OR DATALENGTH(r.[ParticipantCode])<>LEN(r.[ParticipantCode])*2
     OR LEFT(r.[ParticipantCode],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
     OR r.[ParticipantCode] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
     OR r.[ParticipantCode] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')
     OR r.[OwnerEntityType] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(r.[OwnerEntityType]))) COLLATE Latin1_General_100_BIN2
     OR DATALENGTH(r.[OwnerEntityType])<>DATALENGTH(UPPER(LTRIM(RTRIM(r.[OwnerEntityType])))) OR DATALENGTH(r.[OwnerEntityType])<>LEN(r.[OwnerEntityType])*2
     OR LEFT(r.[OwnerEntityType],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
     OR r.[OwnerEntityType] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
     OR r.[OwnerEntityType] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')
     OR r.[OwnerAction] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(r.[OwnerAction]))) COLLATE Latin1_General_100_BIN2
     OR DATALENGTH(r.[OwnerAction])<>DATALENGTH(UPPER(LTRIM(RTRIM(r.[OwnerAction])))) OR DATALENGTH(r.[OwnerAction])<>LEN(r.[OwnerAction])*2
     OR LEFT(r.[OwnerAction],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
     OR r.[OwnerAction] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
     OR r.[OwnerAction] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS'))
   THROW 51000, 'C7_RECEIPT_AUTHORITY: receipt must match one approved producer event in execution.', 1;
  DECLARE @ownerTenant uniqueidentifier, @ownerParticipant nvarchar(100), @ownerEffect char(64), @ownerResource nvarchar(255), @ownerResult int;
  DECLARE owner_effects CURSOR LOCAL FAST_FORWARD FOR
   SELECT DISTINCT [TenantId],[ParticipantCode],[EffectFingerprint] FROM inserted ORDER BY [TenantId],[ParticipantCode],[EffectFingerprint];
  OPEN owner_effects; FETCH NEXT FROM owner_effects INTO @ownerTenant,@ownerParticipant,@ownerEffect;
  WHILE @@FETCH_STATUS=0 BEGIN
   SET @ownerResource=CONCAT(N'FIN:C7C8:',CONVERT(nvarchar(36),@ownerTenant),N'|',@ownerParticipant,N'|',@ownerEffect);
   EXEC @ownerResult=sp_getapplock @Resource=@ownerResource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;
   IF @ownerResult<0 BEGIN CLOSE owner_effects; DEALLOCATE owner_effects; THROW 51000, 'C7_RECEIPT_LOCK_FAILED: owner-effect identity could not be serialized.', 1; END;
   FETCH NEXT FROM owner_effects INTO @ownerTenant,@ownerParticipant,@ownerEffect;
  END; CLOSE owner_effects; DEALLOCATE owner_effects;
  IF EXISTS(SELECT 1 FROM inserted i JOIN [ProducerIntentGroupReceipts] r ON r.[TenantId]=i.[TenantId]
   AND r.[ParticipantCode]=i.[ParticipantCode] AND r.[EffectFingerprint]=i.[EffectFingerprint])
  THROW 51000, 'C7_RECEIPT_REUSED: owner-effect evidence already belongs to a producer group.', 1;
END;");

        migrationBuilder.Sql(@"ALTER TRIGGER [TR_AccountingEvents_C7ProducerDecision] ON [AccountingEvents] AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.[Id]=i.[Id] WHERE d.[Id] IS NULL AND i.[ProducerDecisionStatus] NOT IN (N'NotRequired',N'Pending'))
  THROW 51000, 'C7_INSERT_STATE: producer decisions cannot be fabricated during prepare.', 1;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.[ProducerDecisionStatus]<>N'NotRequired' AND (i.[ProducerParticipantIdentity] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[ProducerParticipantIdentity]))) COLLATE Latin1_General_100_BIN2 OR DATALENGTH(i.[ProducerParticipantIdentity])<>DATALENGTH(UPPER(LTRIM(RTRIM(i.[ProducerParticipantIdentity])))) OR DATALENGTH(i.[ProducerParticipantIdentity])<>LEN(i.[ProducerParticipantIdentity])*2 OR LEFT(i.[ProducerParticipantIdentity],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]' OR i.[ProducerParticipantIdentity] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%' OR i.[ProducerParticipantIdentity] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS') OR i.[IdempotencyKey] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[IdempotencyKey]))) COLLATE Latin1_General_100_BIN2 OR DATALENGTH(i.[IdempotencyKey])<>LEN(i.[IdempotencyKey])*2 OR LEFT(i.[IdempotencyKey],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z0-9]' OR i.[IdempotencyKey] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.:-]%' OR i.[IdempotencyKey] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')))
  THROW 51000, 'C7_PARTICIPANT_IDENTITY: canonical stable participant identity is required.', 1;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.[ProducerDecisionStatus] IN (N'Approved',N'Rejected') AND (i.[ProducerDecidedByUserId]=i.[PreparedByUserId] OR LEN(LTRIM(RTRIM(i.[ProducerDecisionReason])))=0))
  THROW 51000, 'C7_MAKER_CHECKER: a distinct checker and governed reason are required.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id] WHERE ISNULL(d.[ProducerParticipantIdentity],N'')<>ISNULL(i.[ProducerParticipantIdentity],N'') OR ISNULL(d.[ProducerIntentSnapshotHash],N'')<>ISNULL(i.[ProducerIntentSnapshotHash],N'') OR ISNULL(d.[ProducerIntentSnapshotJson],N'')<>ISNULL(i.[ProducerIntentSnapshotJson],N'') OR (d.[ProducerDecisionStatus]<>i.[ProducerDecisionStatus] AND EXISTS(SELECT 1 FROM [ProducerIntentGroupMembers] m WHERE m.[TenantId]=i.[TenantId] AND m.[AccountingEventId]=i.[Id])) OR (d.[ProducerDecisionStatus]=N'Pending' AND i.[ProducerDecisionStatus] NOT IN (N'Pending',N'Approved',N'Rejected')) OR (d.[ProducerDecisionStatus] IN (N'NotRequired',N'Approved',N'Rejected') AND d.[ProducerDecisionStatus]<>i.[ProducerDecisionStatus]) OR (d.[ProducerDecidedByUserId] IS NOT NULL AND (i.[ProducerDecidedByUserId] IS NULL OR d.[ProducerDecidedByUserId]<>i.[ProducerDecidedByUserId] OR d.[ProducerDecidedAtUtc]<>i.[ProducerDecidedAtUtc] OR d.[ProducerDecisionReason]<>i.[ProducerDecisionReason])))
  THROW 51000, 'C7_DECISION_IMMUTABLE: participant and maker-checker evidence cannot be rewritten.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id] WHERE d.[ProducerDecisionStatus]=N'Pending' AND i.[ProducerDecisionStatus] IN (N'Approved',N'Rejected') AND (d.[Status]<>N'PendingApproval' OR i.[Status]<>N'PendingApproval'))
  THROW 51000, 'C7_DECISION_ONLY: approval cannot execute effects in the same statement.', 1;
 IF EXISTS(SELECT 1 FROM inserted i WHERE (i.[ProducerDecisionStatus]=N'Pending' AND i.[Status]<>N'PendingApproval'
   AND NOT EXISTS(SELECT 1 FROM [ProducerIntentGroupMembers] m JOIN [ProducerIntentGroups] g ON g.[TenantId]=m.[TenantId] AND g.[Id]=m.[ProducerIntentGroupId] JOIN [ProducerIntentGroupReceipts] r ON r.[TenantId]=g.[TenantId] AND r.[ProducerIntentGroupId]=g.[Id] WHERE m.[TenantId]=i.[TenantId] AND m.[AccountingEventId]=i.[Id] AND g.[Status] IN (N'Approved',N'Failed')))
   OR (i.[ProducerDecisionStatus]=N'Rejected' AND i.[Status]<>N'PendingApproval') OR (i.[ProducerDecisionStatus]=N'Approved' AND i.[Status] NOT IN (N'PendingApproval',N'Pending',N'Posted',N'Failed')))
  THROW 51000, 'C7_EXECUTION_GATE: only individual approval or complete approved group authority may enter C6.', 1;
END;");

    }
}
