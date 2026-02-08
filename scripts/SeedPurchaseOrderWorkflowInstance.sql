/*
  Seed a Purchase Order workflow definition + instance with conditional routing.

  What this creates:
  - WorkflowEntityType: PurchaseOrder (if missing)
  - WorkflowDefinition: "PO Approval (SQL Seed) <suffix>"
  - Steps:
      1) Manager Approval (Approval step)
      2) Finance Approval (Approval step)
      3) Finalize (Automatic step / end)
  - Transitions:
      - Manager -> Finance when totalAmount > @AmountThreshold
      - Manager -> Finalize as default
      - Finance -> Finalize as default
  - WorkflowInstance for a PurchaseOrder
  - Initial WorkflowStepInstance for Manager step
  - Initial WorkflowApproval row for Manager step

  Notes:
  - Enum integers used by the app:
      WorkflowStepType: Approval=2, Automatic=1
      WorkflowInstanceStatus: InProgress=1
      WorkflowStepInstanceStatus: Pending=0
      WorkflowApprovalStatus: Pending=0
      WorkflowPriority: Normal=3
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    ------------------------------------------------------------
    -- Parameters (override as needed)
    ------------------------------------------------------------
    DECLARE @TenantId UNIQUEIDENTIFIER = NULL;               -- NULL = auto-pick by PO
    DECLARE @PurchaseOrderId UNIQUEIDENTIFIER = NULL;        -- NULL = latest PO for tenant
    DECLARE @InitiatorUserId UNIQUEIDENTIFIER = NULL;        -- NULL = PO.RequestedById or latest user in tenant
    DECLARE @ApproverUserId UNIQUEIDENTIFIER = NULL;         -- optional direct approver for first step
    DECLARE @ApproverRole NVARCHAR(100) = N'WorkflowAdmin';  -- used when @ApproverUserId is NULL
    DECLARE @AmountThreshold DECIMAL(18,2) = 1000.00;        -- condition threshold

    ------------------------------------------------------------
    -- Resolve PO + tenant + user
    ------------------------------------------------------------
    IF @PurchaseOrderId IS NULL
    BEGIN
        IF @TenantId IS NULL
        BEGIN
            SELECT TOP (1)
                @PurchaseOrderId = po.Id,
                @TenantId = po.TenantId
            FROM PurchaseOrders po
            WHERE po.IsDeleted = 0
            ORDER BY po.CreatedAt DESC;
        END
        ELSE
        BEGIN
            SELECT TOP (1)
                @PurchaseOrderId = po.Id
            FROM PurchaseOrders po
            WHERE po.TenantId = @TenantId
              AND po.IsDeleted = 0
            ORDER BY po.CreatedAt DESC;
        END
    END
    ELSE IF @TenantId IS NULL
    BEGIN
        SELECT @TenantId = po.TenantId
        FROM PurchaseOrders po
        WHERE po.Id = @PurchaseOrderId
          AND po.IsDeleted = 0;
    END

    IF @PurchaseOrderId IS NULL
        RAISERROR('No PurchaseOrder found. Set @PurchaseOrderId explicitly.', 16, 1);

    IF @TenantId IS NULL
        RAISERROR('Tenant could not be resolved. Set @TenantId explicitly.', 16, 1);

    DECLARE @OrderNumber NVARCHAR(50);
    DECLARE @TotalAmount DECIMAL(18,2);
    DECLARE @RequestedById UNIQUEIDENTIFIER;

    SELECT
        @OrderNumber = po.OrderNumber,
        @TotalAmount = po.TotalAmount,
        @RequestedById = po.RequestedById
    FROM PurchaseOrders po
    WHERE po.Id = @PurchaseOrderId
      AND po.TenantId = @TenantId
      AND po.IsDeleted = 0;

    IF @OrderNumber IS NULL
        RAISERROR('PurchaseOrder not found for tenant or is deleted.', 16, 1);

    IF @InitiatorUserId IS NULL
        SET @InitiatorUserId = @RequestedById;

    IF @InitiatorUserId IS NULL
    BEGIN
        SELECT TOP (1) @InitiatorUserId = u.Id
        FROM Users u
        WHERE u.TenantId = @TenantId
          AND u.IsDeleted = 0
        ORDER BY u.CreatedAt DESC;
    END

    IF @InitiatorUserId IS NULL
        RAISERROR('No initiator user found. Set @InitiatorUserId explicitly.', 16, 1);

    ------------------------------------------------------------
    -- Ensure entity type exists
    ------------------------------------------------------------
    DECLARE @EntityTypeId UNIQUEIDENTIFIER;

    SELECT TOP (1) @EntityTypeId = et.Id
    FROM WorkflowEntityTypes et
    WHERE et.TenantId = @TenantId
      AND et.IsDeleted = 0
      AND (et.Name = 'PurchaseOrder' OR et.Code = 'PURCHASE_ORDER')
    ORDER BY et.DisplayOrder, et.CreatedAt;

    IF @EntityTypeId IS NULL
    BEGIN
        SET @EntityTypeId = NEWID();

        INSERT INTO WorkflowEntityTypes
        (
            Id, Code, Name, Description, IsActive, DisplayOrder,
            CreatedAt, CreatedBy, CreatedById,
            IsDeleted, TenantId
        )
        VALUES
        (
            @EntityTypeId, 'PURCHASE_ORDER', 'PurchaseOrder', 'Procurement purchase orders',
            1, 30,
            SYSUTCDATETIME(), 'sql-seed', @InitiatorUserId,
            0, @TenantId
        );
    END
    ELSE
    BEGIN
        UPDATE WorkflowEntityTypes
        SET IsActive = 1,
            UpdatedAt = SYSUTCDATETIME(),
            UpdatedBy = 'sql-seed',
            LastModifiedById = @InitiatorUserId
        WHERE Id = @EntityTypeId
          AND IsActive = 0;
    END

    ------------------------------------------------------------
    -- Create definition + steps + transitions
    ------------------------------------------------------------
    DECLARE @DefinitionId UNIQUEIDENTIFIER = NEWID();
    DECLARE @ManagerStepId UNIQUEIDENTIFIER = NEWID();
    DECLARE @FinanceStepId UNIQUEIDENTIFIER = NEWID();
    DECLARE @FinalizeStepId UNIQUEIDENTIFIER = NEWID();

    DECLARE @DefinitionName NVARCHAR(100) =
        CONCAT('PO Approval (SQL Seed) ', RIGHT(CONVERT(NVARCHAR(36), NEWID()), 8));

    INSERT INTO WorkflowDefinitions
    (
        Id, Name, Description, EntityTypeId, Version, IsActive, Configuration,
        CreatedAt, CreatedBy, CreatedById,
        IsDeleted, TenantId
    )
    VALUES
    (
        @DefinitionId, @DefinitionName, '2-step approval + conditional routing', @EntityTypeId, 1, 1, NULL,
        SYSUTCDATETIME(), 'sql-seed', @InitiatorUserId,
        0, @TenantId
    );

    INSERT INTO WorkflowSteps
    (
        Id, WorkflowDefinitionId, Name, Description,
        StepType, [Order], IsRequired, IsStartStep, IsEndStep,
        EstimatedHours,
        CreatedAt, CreatedBy, CreatedById,
        IsDeleted, TenantId
    )
    VALUES
    (
        @ManagerStepId, @DefinitionId, 'Manager Approval', 'First approval step',
        2, 1, 1, 1, 0,
        24,
        SYSUTCDATETIME(), 'sql-seed', @InitiatorUserId,
        0, @TenantId
    ),
    (
        @FinanceStepId, @DefinitionId, 'Finance Approval', 'Second approval step',
        2, 2, 1, 0, 0,
        24,
        SYSUTCDATETIME(), 'sql-seed', @InitiatorUserId,
        0, @TenantId
    ),
    (
        @FinalizeStepId, @DefinitionId, 'Finalize', 'Auto-finalization step',
        1, 3, 1, 0, 1,
        1,
        SYSUTCDATETIME(), 'sql-seed', @InitiatorUserId,
        0, @TenantId
    );

    DECLARE @ConditionJson NVARCHAR(MAX) =
        CONCAT(
            '{"ConditionType":0,"Expression":"totalAmount > ',
            CONVERT(NVARCHAR(32), CAST(@AmountThreshold AS DECIMAL(18,2))),
            '","LogicalOperator":0}'
        );

    INSERT INTO WorkflowTransitions
    (
        Id, WorkflowDefinitionId, FromStepId, ToStepId, Name, Description, Condition,
        IsDefault, Priority,
        CreatedAt, CreatedBy, CreatedById,
        IsDeleted, TenantId
    )
    VALUES
    (
        NEWID(), @DefinitionId, @ManagerStepId, @FinanceStepId,
        'Manager -> Finance', 'Route to finance when amount exceeds threshold', @ConditionJson,
        0, 100,
        SYSUTCDATETIME(), 'sql-seed', @InitiatorUserId,
        0, @TenantId
    ),
    (
        NEWID(), @DefinitionId, @ManagerStepId, @FinalizeStepId,
        'Manager -> Finalize', 'Default path when threshold condition is not met', NULL,
        1, 0,
        SYSUTCDATETIME(), 'sql-seed', @InitiatorUserId,
        0, @TenantId
    ),
    (
        NEWID(), @DefinitionId, @FinanceStepId, @FinalizeStepId,
        'Finance -> Finalize', 'Complete after finance approval', NULL,
        1, 0,
        SYSUTCDATETIME(), 'sql-seed', @InitiatorUserId,
        0, @TenantId
    );

    ------------------------------------------------------------
    -- Create workflow instance at first step
    ------------------------------------------------------------
    DECLARE @InstanceId UNIQUEIDENTIFIER = NEWID();
    DECLARE @StepInstanceId UNIQUEIDENTIFIER = NEWID();

    DECLARE @DataContext NVARCHAR(MAX) =
        CONCAT(
            '{"entityId":"', CONVERT(NVARCHAR(36), @PurchaseOrderId),
            '","entityType":"PurchaseOrder"',
            ',"purchaseOrderNumber":"', ISNULL(REPLACE(@OrderNumber, '"', '\"'), ''),
            '","totalAmount":', CONVERT(NVARCHAR(64), CAST(ISNULL(@TotalAmount, 0) AS DECIMAL(18,2))),
            '}'
        );

    INSERT INTO WorkflowInstances
    (
        Id, WorkflowDefinitionId, EntityId, EntityTypeId,
        Status, Priority, InitiatedById, StartedById,
        CreatedDate, StartedDate,
        CurrentStepId, DataContext,
        CreatedAt, CreatedBy, CreatedById,
        IsDeleted, TenantId
    )
    VALUES
    (
        @InstanceId, @DefinitionId, @PurchaseOrderId, @EntityTypeId,
        1, 3, @InitiatorUserId, @InitiatorUserId,
        SYSUTCDATETIME(), SYSUTCDATETIME(),
        @ManagerStepId, @DataContext,
        SYSUTCDATETIME(), 'sql-seed', @InitiatorUserId,
        0, @TenantId
    );

    INSERT INTO WorkflowStepInstances
    (
        Id, WorkflowInstanceId, WorkflowStepId, Status, AssignedToId,
        CreatedDate, StartedDate,
        CreatedAt, CreatedBy, CreatedById,
        RetryCount, IsDeleted, TenantId
    )
    VALUES
    (
        @StepInstanceId, @InstanceId, @ManagerStepId, 0, @ApproverUserId,
        SYSUTCDATETIME(), SYSUTCDATETIME(),
        SYSUTCDATETIME(), 'sql-seed', @InitiatorUserId,
        0, 0, @TenantId
    );

    INSERT INTO WorkflowApprovals
    (
        Id, StepInstanceId, ApproverId, ApproverRole, Status,
        RequestedDate, DueDate, Priority,
        CreatedAt, CreatedBy, CreatedById,
        IsDeleted, TenantId
    )
    VALUES
    (
        NEWID(), @StepInstanceId, @ApproverUserId,
        CASE WHEN @ApproverUserId IS NULL THEN @ApproverRole ELSE NULL END,
        0,
        SYSUTCDATETIME(), DATEADD(HOUR, 24, SYSUTCDATETIME()), 3,
        SYSUTCDATETIME(), 'sql-seed', @InitiatorUserId,
        0, @TenantId
    );

    -- Optional: reflect submission status on PO if still draft
    UPDATE PurchaseOrders
    SET Status = CASE WHEN Status = 'Draft' THEN 'Pending Approval' ELSE Status END,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = 'sql-seed'
    WHERE Id = @PurchaseOrderId
      AND TenantId = @TenantId
      AND IsDeleted = 0;

    COMMIT;

    SELECT
        @TenantId AS TenantId,
        @PurchaseOrderId AS PurchaseOrderId,
        @DefinitionId AS WorkflowDefinitionId,
        @InstanceId AS WorkflowInstanceId,
        @StepInstanceId AS CurrentStepInstanceId,
        @AmountThreshold AS ConditionThreshold,
        @TotalAmount AS PurchaseOrderTotalAmount;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK;

    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrState INT = ERROR_STATE();
    RAISERROR(@ErrMsg, @ErrSeverity, @ErrState);
END CATCH;

