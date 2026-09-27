using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

internal static class PhysicalCountCommitteeGuards
{
    public static void Install(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(ExistingGuardPatch);
        migrationBuilder.Sql(CounterGuard);
        migrationBuilder.Sql(CountGuard);
        migrationBuilder.Sql(DefectiveObservationGuard);
    }

    public static void Remove(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(RemoveGuards);

    public const string ExistingGuardPatch = """
        DECLARE @action nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_PhysicalCountActions_AppendOnly'));
        DECLARE @line nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_PhysicalCountItems_ControlledMutation'));
        DECLARE @life nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_PhysicalCounts_ControlledLifecycle'));
        IF @action IS NULL OR @line IS NULL OR @life IS NULL
            THROW 51980, 'INV_COUNT_COMMITTEE_GUARD_MISSING: existing count guards are required.', 1;
        IF CHARINDEX(N'i.ActionType NOT BETWEEN 1 AND 16',@action)=0 OR
           CHARINDEX(N'i.CountedById <> p.CountedById',@line)=0 OR
           CHARINDEX(N'i.RecountedById IN (p.InitiatedById,p.CountedById)',@line)=0 OR
           CHARINDEX(N'a.ActionType=14 AND a.ActorUserId=i.CountedById',@life)=0 OR
           CHARINDEX(N'a.ActionType=16 AND a.ActorUserId=i.CountedById',@life)=0 OR
           CHARINDEX(N'a.ActionType IN (5,7) AND a.ActorUserId=i.CountedById',@life)=0
            THROW 51980, 'INV_COUNT_COMMITTEE_GUARD_DRIFT: expected count actor predicates are missing.', 1;
        SET @action=REPLACE(@action,N'i.ActionType NOT BETWEEN 1 AND 16',N'i.ActionType NOT BETWEEN 1 AND 17');
        SET @line=REPLACE(@line,N'i.CountedById <> p.CountedById',N'(
            (NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=p.Id AND c.TenantId=p.TenantId) AND i.CountedById<>p.CountedById)
            OR (EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=p.Id AND c.TenantId=p.TenantId)
                AND NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=p.Id AND c.TenantId=p.TenantId AND c.IsActive=1 AND c.IsDeleted=0 AND c.UserId=i.CountedById)))');
        SET @line=REPLACE(@line,N'i.RecountedById IN (p.InitiatedById,p.CountedById)',N'(i.RecountedById IN (p.InitiatedById,p.CountedById) OR EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=p.Id AND c.TenantId=p.TenantId AND c.UserId=i.RecountedById))');
        DECLARE @committeeActor nvarchar(max)=N'((NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=i.Id AND c.TenantId=i.TenantId) AND a.ActorUserId=i.CountedById)
            OR EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=i.Id AND c.TenantId=i.TenantId AND c.IsActive=1 AND c.IsDeleted=0 AND c.UserId=a.ActorUserId))';
        SET @life=REPLACE(@life,N'a.ActionType=14 AND a.ActorUserId=i.CountedById',N'a.ActionType=14 AND '+@committeeActor);
        SET @life=REPLACE(@life,N'a.ActionType=16 AND a.ActorUserId=i.CountedById',N'a.ActionType=16 AND '+@committeeActor);
        SET @life=REPLACE(@life,N'a.ActionType IN (5,7) AND a.ActorUserId=i.CountedById',N'a.ActionType IN (5,7) AND '+@committeeActor);
        SET @action=STUFF(@action,1,CHARINDEX(N'TRIGGER',UPPER(@action))-1,N'CREATE OR ALTER ');
        SET @line=STUFF(@line,1,CHARINDEX(N'TRIGGER',UPPER(@line))-1,N'CREATE OR ALTER ');
        SET @life=STUFF(@life,1,CHARINDEX(N'TRIGGER',UPPER(@life))-1,N'CREATE OR ALTER ');
        EXEC sys.sp_executesql @action;
        EXEC sys.sp_executesql @line;
        EXEC sys.sp_executesql @life;
        """;

    public const string CounterGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_PhysicalCountCounters_ControlledMutation
        ON dbo.PhysicalCountCounters AFTER INSERT, UPDATE, DELETE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
            THROW 51981, 'INV_COUNT_COUNTER_HISTORY: counter assignment history cannot be deleted.', 1;
          IF EXISTS(SELECT 1 FROM inserted i
              LEFT JOIN dbo.PhysicalCounts p ON p.Id=i.PhysicalCountId AND p.TenantId=i.TenantId AND p.IsDeleted=0
              LEFT JOIN dbo.Employees e ON e.Id=i.EmployeeId AND e.TenantId=i.TenantId
              LEFT JOIN dbo.Users u ON u.Id=i.UserId
              LEFT JOIN dbo.Notifications n ON n.Id=i.InAppNotificationId AND n.TenantId=i.TenantId AND n.RecipientId=i.UserId AND n.EntityId=i.PhysicalCountId AND n.DeliveryMethods=N'InApp' AND n.NotificationType=N'PhysicalCountCounterAssigned' AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(n.AdditionalData,'$.counterAssignmentId'))=i.Id
              LEFT JOIN dbo.Notifications m ON m.Id=i.EmailNotificationId AND m.TenantId=i.TenantId AND m.RecipientId='00000000-0000-0000-0000-000000000000' AND m.EmailAddress=i.EmailAddress AND m.EntityId=i.PhysicalCountId AND m.DeliveryMethods=N'Email' AND m.NotificationType=N'PhysicalCountCounterAssigned' AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(m.AdditionalData,'$.counterAssignmentId'))=i.Id
              WHERE p.Id IS NULL OR (p.Status<>N'Draft' AND NOT (
                  p.Status=N'UnderInvestigation' AND p.ObservationSubmittedAtUtc IS NOT NULL
                  AND p.RootPhysicalCountId IS NULL AND p.ParentPhysicalCountId IS NULL AND p.StockAdjustmentId IS NULL
                  AND NOT EXISTS(SELECT 1 FROM deleted d WHERE d.Id=i.Id)
                  AND NOT EXISTS(SELECT 1 FROM dbo.PhysicalCounts child WHERE child.TenantId=p.TenantId AND child.RootPhysicalCountId=p.Id)
                  AND i.AssignedById<>p.InitiatedById AND i.AssignedById<>ISNULL(p.CountedById,'00000000-0000-0000-0000-000000000000')
                  AND i.AssignedById<>i.UserId
                  AND NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountItems line WHERE line.PhysicalCountId=p.Id AND line.TenantId=p.TenantId AND (line.CountedById=i.AssignedById OR line.RecountedById=i.AssignedById))
                  AND EXISTS(SELECT 1 FROM dbo.PhysicalCountActions a CROSS APPLY OPENJSON(a.SnapshotJson,'$.payload.employeeIds') selected
                      WHERE a.TenantId=p.TenantId AND a.PhysicalCountId=p.Id AND a.ActionType=17 AND a.ActorRole=N'LegacyCommitteeRecovery'
                        AND a.ActorUserId=i.AssignedById AND a.IsDeleted=0 AND NULLIF(LTRIM(RTRIM(a.Comment)),N'') IS NOT NULL
                        AND TRY_CONVERT(uniqueidentifier,selected.value)=i.EmployeeId AND a.OccurredAtUtc<=i.AssignedAtUtc)
                  AND NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters prior WHERE prior.PhysicalCountId=p.Id AND prior.TenantId=p.TenantId
                      AND (prior.IsDeleted=1 OR prior.IsActive=0 OR prior.AssignedById<>i.AssignedById OR prior.UserId=i.AssignedById))
              )) OR e.Id IS NULL OR u.Id IS NULL OR n.Id IS NULL OR m.Id IS NULL OR i.IsDeleted=1
                OR (NOT EXISTS(SELECT 1 FROM deleted d WHERE d.Id=i.Id) AND
                    (e.IsDeleted=1 OR e.IsActive=0 OR u.IsActive=0 OR ISNULL(u.EmployeeId,'00000000-0000-0000-0000-000000000000')<>i.EmployeeId
                     OR NOT EXISTS(SELECT 1 FROM dbo.UserTenants ut WHERE ut.TenantId=i.TenantId AND ut.UserId=i.UserId AND ut.IsDeleted=0 AND ut.Status=0 AND (ut.ExpiresAt IS NULL OR ut.ExpiresAt>SYSUTCDATETIME()))))
                OR (i.IsActive=1 AND (i.RemovedAtUtc IS NOT NULL OR i.RemovedById IS NOT NULL))
                OR (i.IsActive=0 AND (i.RemovedAtUtc IS NULL OR i.RemovedById IS NULL)))
            THROW 51982, 'INV_COUNT_COUNTER_SCOPE: assignments require a draft count or audited legacy investigation recovery, linked employee/user and queued notifications.', 1;
          IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.Id=d.Id WHERE
              i.TenantId<>d.TenantId OR i.PhysicalCountId<>d.PhysicalCountId OR i.EmployeeId<>d.EmployeeId OR i.UserId<>d.UserId
              OR i.AssignedById<>d.AssignedById OR i.AssignedAtUtc<>d.AssignedAtUtc
              OR i.EmployeeNumber<>d.EmployeeNumber OR i.EmployeeName<>d.EmployeeName
              OR ISNULL(i.EmailAddress,N'')<>ISNULL(d.EmailAddress,N'') OR i.InAppNotificationId<>d.InAppNotificationId OR i.EmailNotificationId<>d.EmailNotificationId
              OR d.IsActive=0 OR i.IsActive<>0)
            THROW 51983, 'INV_COUNT_COUNTER_IMMUTABLE: only removal of an active draft assignment is permitted.', 1;
        END
        """;

    public const string CountGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_PhysicalCounts_CommitteeActors
        ON dbo.PhysicalCounts AFTER INSERT, UPDATE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.Id=d.Id WHERE d.Status=N'Draft' AND i.Status=N'InProgress'
              AND EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=i.Id AND c.TenantId=i.TenantId)
              AND NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=i.Id AND c.TenantId=i.TenantId AND c.IsActive=1 AND c.IsDeleted=0 AND c.UserId=i.CountedById))
            THROW 51984, 'INV_COUNT_COUNTER_START: only an assigned counter may begin the count.', 1;
          IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.Id=d.Id
              JOIN dbo.PhysicalCountCounters c ON c.PhysicalCountId=i.Id AND c.TenantId=i.TenantId WHERE
              (i.StoresApprovedById=c.UserId AND ISNULL(d.StoresApprovedById,'00000000-0000-0000-0000-000000000000')<>c.UserId)
              OR (i.FinanceApprovedById=c.UserId AND ISNULL(d.FinanceApprovedById,'00000000-0000-0000-0000-000000000000')<>c.UserId)
              OR (i.AuditAttestedById=c.UserId AND ISNULL(d.AuditAttestedById,'00000000-0000-0000-0000-000000000000')<>c.UserId))
            THROW 51985, 'INV_COUNT_COMMITTEE_INDEPENDENCE: count reviewers must be independent of the committee.', 1;
        END
        """;
    public const string DefectiveObservationGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_PhysicalCountItems_DefectiveObservation
        ON dbo.PhysicalCountItems AFTER INSERT, UPDATE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
              JOIN dbo.PhysicalCounts p ON p.Id=i.PhysicalCountId AND p.TenantId=i.TenantId
              WHERE (i.DefectiveQuantity<>ISNULL(d.DefectiveQuantity,0) OR ISNULL(i.DefectiveNotes,N'')<>ISNULL(d.DefectiveNotes,N''))
                AND (p.Status NOT IN(N'InProgress',N'UnderReview') OR i.IsCounted=0
                  OR i.CountedById IS NULL
                  OR (NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=p.Id AND c.TenantId=p.TenantId)
                      AND i.CountedById<>p.CountedById)
                  OR (EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=p.Id AND c.TenantId=p.TenantId)
                      AND NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=p.Id AND c.TenantId=p.TenantId AND c.IsActive=1 AND c.IsDeleted=0 AND c.UserId=i.CountedById))))
            THROW 51987, 'INV_COUNT_DEFECTIVE_OBSERVATION_LOCKED: only assigned counters may record defects before submission.', 1;
        END
        """;

    public const string RemoveGuards = """
        IF EXISTS(SELECT 1 FROM dbo.PhysicalCountItems WHERE DefectiveQuantity<>0 OR DefectiveNotes IS NOT NULL)
            THROW 51986, 'INV_COUNT_DEFECTIVE_DOWN_BLOCKED: retained defective observations must not be removed.', 1;
        IF EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters) OR EXISTS(SELECT 1 FROM dbo.PhysicalCountActions WHERE ActionType=17)
            THROW 51986, 'INV_COUNT_COMMITTEE_DOWN_BLOCKED: retained assignment history must not be removed.', 1;
        DECLARE @action nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_PhysicalCountActions_AppendOnly'));
        DECLARE @line nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_PhysicalCountItems_ControlledMutation'));
        DECLARE @life nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_PhysicalCounts_ControlledLifecycle'));
        IF @action IS NULL OR @line IS NULL OR @life IS NULL
            THROW 51986, 'INV_COUNT_COMMITTEE_DOWN_DRIFT: existing count guards are missing.', 1;
        IF CHARINDEX(N'i.ActionType NOT BETWEEN 1 AND 17',@action)=0
            THROW 51986, 'INV_COUNT_COMMITTEE_DOWN_DRIFT: expected committee predicate is missing.', 1;
        SET @action=REPLACE(@action,N'i.ActionType NOT BETWEEN 1 AND 17',N'i.ActionType NOT BETWEEN 1 AND 16');
        IF CHARINDEX(N'(
            (NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=p.Id AND c.TenantId=p.TenantId) AND i.CountedById<>p.CountedById)
            OR (EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=p.Id AND c.TenantId=p.TenantId)
                AND NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=p.Id AND c.TenantId=p.TenantId AND c.IsActive=1 AND c.IsDeleted=0 AND c.UserId=i.CountedById)))',@line)=0
            THROW 51986, 'INV_COUNT_COMMITTEE_DOWN_DRIFT: expected committee predicate is missing.', 1;
        SET @line=REPLACE(@line,N'(
            (NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=p.Id AND c.TenantId=p.TenantId) AND i.CountedById<>p.CountedById)
            OR (EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=p.Id AND c.TenantId=p.TenantId)
                AND NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=p.Id AND c.TenantId=p.TenantId AND c.IsActive=1 AND c.IsDeleted=0 AND c.UserId=i.CountedById)))',N'i.CountedById <> p.CountedById');
        IF CHARINDEX(N'(i.RecountedById IN (p.InitiatedById,p.CountedById) OR EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=p.Id AND c.TenantId=p.TenantId AND c.UserId=i.RecountedById))',@line)=0
            THROW 51986, 'INV_COUNT_COMMITTEE_DOWN_DRIFT: expected committee predicate is missing.', 1;
        SET @line=REPLACE(@line,N'(i.RecountedById IN (p.InitiatedById,p.CountedById) OR EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=p.Id AND c.TenantId=p.TenantId AND c.UserId=i.RecountedById))',N'i.RecountedById IN (p.InitiatedById,p.CountedById)');
        IF CHARINDEX(N'a.ActionType=14 AND ((NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=i.Id AND c.TenantId=i.TenantId) AND a.ActorUserId=i.CountedById)
            OR EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=i.Id AND c.TenantId=i.TenantId AND c.IsActive=1 AND c.IsDeleted=0 AND c.UserId=a.ActorUserId))',@life)=0
            THROW 51986, 'INV_COUNT_COMMITTEE_DOWN_DRIFT: expected committee predicate is missing.', 1;
        SET @life=REPLACE(@life,N'a.ActionType=14 AND ((NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=i.Id AND c.TenantId=i.TenantId) AND a.ActorUserId=i.CountedById)
            OR EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=i.Id AND c.TenantId=i.TenantId AND c.IsActive=1 AND c.IsDeleted=0 AND c.UserId=a.ActorUserId))',N'a.ActionType=14 AND a.ActorUserId=i.CountedById');
        IF CHARINDEX(N'a.ActionType=16 AND ((NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=i.Id AND c.TenantId=i.TenantId) AND a.ActorUserId=i.CountedById)
            OR EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=i.Id AND c.TenantId=i.TenantId AND c.IsActive=1 AND c.IsDeleted=0 AND c.UserId=a.ActorUserId))',@life)=0
            THROW 51986, 'INV_COUNT_COMMITTEE_DOWN_DRIFT: expected committee predicate is missing.', 1;
        SET @life=REPLACE(@life,N'a.ActionType=16 AND ((NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=i.Id AND c.TenantId=i.TenantId) AND a.ActorUserId=i.CountedById)
            OR EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=i.Id AND c.TenantId=i.TenantId AND c.IsActive=1 AND c.IsDeleted=0 AND c.UserId=a.ActorUserId))',N'a.ActionType=16 AND a.ActorUserId=i.CountedById');
        IF CHARINDEX(N'a.ActionType IN (5,7) AND ((NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=i.Id AND c.TenantId=i.TenantId) AND a.ActorUserId=i.CountedById)
            OR EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=i.Id AND c.TenantId=i.TenantId AND c.IsActive=1 AND c.IsDeleted=0 AND c.UserId=a.ActorUserId))',@life)=0
            THROW 51986, 'INV_COUNT_COMMITTEE_DOWN_DRIFT: expected committee predicate is missing.', 1;
        SET @life=REPLACE(@life,N'a.ActionType IN (5,7) AND ((NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=i.Id AND c.TenantId=i.TenantId) AND a.ActorUserId=i.CountedById)
            OR EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=i.Id AND c.TenantId=i.TenantId AND c.IsActive=1 AND c.IsDeleted=0 AND c.UserId=a.ActorUserId))',N'a.ActionType IN (5,7) AND a.ActorUserId=i.CountedById');
        SET @action=STUFF(@action,1,CHARINDEX(N'TRIGGER',UPPER(@action))-1,N'CREATE OR ALTER ');
        EXEC sys.sp_executesql @action;
        SET @line=STUFF(@line,1,CHARINDEX(N'TRIGGER',UPPER(@line))-1,N'CREATE OR ALTER ');
        EXEC sys.sp_executesql @line;
        SET @life=STUFF(@life,1,CHARINDEX(N'TRIGGER',UPPER(@life))-1,N'CREATE OR ALTER ');
        EXEC sys.sp_executesql @life;
        DROP TRIGGER IF EXISTS dbo.TR_PhysicalCountCounters_ControlledMutation;
        DROP TRIGGER IF EXISTS dbo.TR_PhysicalCounts_CommitteeActors;
        DROP TRIGGER IF EXISTS dbo.TR_PhysicalCountItems_DefectiveObservation;
        """;
}
