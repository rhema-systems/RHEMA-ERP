/*
Local database export, inspected 2026-10-04.
Source: Property Requests and Listing Applications, published version 6.
Source definition: 80B5A54E-04A2-4C0C-BCB1-69A5D2019B28.
Contains 5 stages, 4 transitions, 11 checklist items, and the designer layout.

Run this ENTIRE file in a NEW SSMS query connected to the database used by the API.
The database in SSMS may differ from the application's configured database.
Set @TargetTenantCode below. Set @Apply = 0 for a rollback rehearsal.
Re-running skips an existing active import. Existing cases/instances are not migrated.
Older definitions remain available for their existing instances.
*/
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TargetTenantCode nvarchar(50) = N'DEFAULT';
DECLARE @Apply bit = 1;
DECLARE @ImportMarker nvarchar(500) = N'Local Estate export 80B5A54E-04A2-4C0C-BCB1-69A5D2019B28 v6';
DECLARE @Payload nvarchar(max) = N'{
  "EntityType": {
    "Code": "EstatePropertyManagementListingApplication",
    "Name": "Property Requests / Listing Applications",
    "Description": "Customer rental requests and purchase bids submitted from published Estate property listings.",
    "EntityClassName": null,
    "PropertySchema": null,
    "DisplayOrder": 60,
    "Icon": "workflow",
    "ColorCode": "#0F766E"
  },
  "Definition": {
    "Id": "80B5A54E-04A2-4C0C-BCB1-69A5D2019B28",
    "Name": "Property Requests and Listing Applications",
    "Description": "Central workflow for validating, deciding, handing off, and closing customer property requests.",
    "Version": 6,
    "Configuration": "{\"designer\":{\"nodes\":[{\"id\":\"start\",\"type\":\"start\",\"position\":{\"x\":260,\"y\":50},\"data\":{\"label\":\"Start\"},\"width\":120,\"height\":38},{\"id\":\"b32719e3-e48b-4aee-9951-f788aaa8ba47\",\"type\":\"task\",\"position\":{\"x\":260.7071067775348,\"y\":150},\"data\":{\"label\":\"Intake and validate property request\",\"instructions\":\"Confirm the customer account, listing, request type, and submitted details before review.\",\"estimatedHours\":\"4\",\"dueDate\":\"4h\",\"stepChecklist\":[{\"id\":\"property-request-1-check-1\",\"name\":\"Customer or represented Business Partner account is confirmed\",\"description\":\"Customer or represented Business Partner account is confirmed\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null},{\"id\":\"property-request-1-check-2\",\"name\":\"Published listing and submitted request details match\",\"description\":\"Published listing and submitted request details match\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null},{\"id\":\"property-request-1-check-3\",\"name\":\"Duplicate and basic eligibility checks are complete\",\"description\":\"Duplicate and basic eligibility checks are complete\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null}],\"documentRequirements\":[],\"taskActionType\":\"general\",\"taskAssigneeType\":\"role\",\"taskAssigneeRole\":\"Property Management Officer\",\"assignee\":\"Property Management Officer\",\"documentName\":\"\",\"priority\":\"medium\"},\"width\":294,\"height\":106,\"selected\":true,\"positionAbsolute\":{\"x\":260.7071067775348,\"y\":150},\"dragging\":false},{\"id\":\"c90e3960-4043-4955-9bd7-f79d19723aeb\",\"type\":\"task\",\"position\":{\"x\":260,\"y\":270},\"data\":{\"label\":\"Commercial and availability review\",\"instructions\":\"Confirm current availability, listing terms, competing requests, and commercial exceptions.\",\"estimatedHours\":\"4\",\"dueDate\":\"4h\",\"stepChecklist\":[{\"id\":\"property-request-2-check-1\",\"name\":\"Current unit availability and competing requests are reviewed\",\"description\":\"Current unit availability and competing requests are reviewed\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null},{\"id\":\"property-request-2-check-2\",\"name\":\"Price, rent, lease term, and commercial exceptions are recorded\",\"description\":\"Price, rent, lease term, and commercial exceptions are recorded\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null}],\"documentRequirements\":[],\"taskActionType\":\"general\",\"taskAssigneeType\":\"role\",\"taskAssigneeRole\":\"Property Management Supervisor\",\"assignee\":\"Property Management Supervisor\",\"documentName\":\"\",\"priority\":\"medium\"},\"width\":282,\"height\":105},{\"id\":\"574cbd94-40ce-44b8-b158-aee3e4fddf28\",\"type\":\"task\",\"position\":{\"x\":260,\"y\":390},\"data\":{\"label\":\"Management decision\",\"instructions\":\"Approve, reject, return, or waitlist the request and record the decision conditions.\",\"estimatedHours\":\"8\",\"dueDate\":\"8h\",\"stepChecklist\":[{\"id\":\"property-request-3-check-1\",\"name\":\"Decision outcome, date, reason, and conditions are recorded\",\"description\":\"Decision outcome, date, reason, and conditions are recorded\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null},{\"id\":\"property-request-3-check-2\",\"name\":\"Reservation requirement is confirmed only for an approved request\",\"description\":\"Reservation requirement is confirmed only for an approved request\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null}],\"documentRequirements\":[],\"taskActionType\":\"general\",\"taskAssigneeType\":\"role\",\"taskAssigneeRole\":\"Property Manager\",\"assignee\":\"Property Manager\",\"documentName\":\"\",\"priority\":\"medium\"},\"width\":198,\"height\":105},{\"id\":\"8831b53b-c70c-4ae0-a89c-21438b310b18\",\"type\":\"task\",\"position\":{\"x\":260,\"y\":510},\"data\":{\"label\":\"Approved transaction handoff\",\"instructions\":\"For an approved request, record the owning lease, sale, allocation, Legal, Finance, or reservation handoff.\",\"estimatedHours\":\"4\",\"dueDate\":\"4h\",\"stepChecklist\":[{\"id\":\"property-request-4-check-1\",\"name\":\"Owning lease or sale transaction reference is recorded\",\"description\":\"Owning lease or sale transaction reference is recorded\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null},{\"id\":\"property-request-4-check-2\",\"name\":\"Reservation or availability update is linked only after approval\",\"description\":\"Reservation or availability update is linked only after approval\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null}],\"documentRequirements\":[],\"taskActionType\":\"general\",\"taskAssigneeType\":\"role\",\"taskAssigneeRole\":\"Property Management Officer\",\"assignee\":\"Property Management Officer\",\"documentName\":\"\",\"priority\":\"medium\"},\"width\":250,\"height\":105},{\"id\":\"58d2b8a5-7e2c-442f-b79f-cd805044a3b6\",\"type\":\"task\",\"position\":{\"x\":260,\"y\":630},\"data\":{\"label\":\"Customer update and close\",\"instructions\":\"Publish the outcome for customer tracking and close the request with its audit references.\",\"estimatedHours\":\"4\",\"dueDate\":\"4h\",\"stepChecklist\":[{\"id\":\"property-request-5-check-1\",\"name\":\"Customer-facing request status reflects the final outcome\",\"description\":\"Customer-facing request status reflects the final outcome\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null},{\"id\":\"property-request-5-check-2\",\"name\":\"Request is closed with its decision and transaction audit references\",\"description\":\"Request is closed with its decision and transaction audit references\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null}],\"documentRequirements\":[],\"taskActionType\":\"general\",\"taskAssigneeType\":\"role\",\"taskAssigneeRole\":\"Property Management Officer\",\"assignee\":\"Property Management Officer\",\"documentName\":\"\",\"priority\":\"medium\"},\"width\":234,\"height\":105},{\"id\":\"end\",\"type\":\"end\",\"position\":{\"x\":260,\"y\":770},\"data\":{\"label\":\"End\"},\"width\":120,\"height\":38}],\"edges\":[{\"id\":\"edge-start\",\"source\":\"start\",\"target\":\"b32719e3-e48b-4aee-9951-f788aaa8ba47\"},{\"id\":\"2de72dab-3759-47b9-beb3-f7acfd9bbf72\",\"source\":\"b32719e3-e48b-4aee-9951-f788aaa8ba47\",\"target\":\"c90e3960-4043-4955-9bd7-f79d19723aeb\",\"label\":\"Complete Intake and validate property request\"},{\"id\":\"8b24b365-5d98-4a4a-8bd7-499b9836f387\",\"source\":\"c90e3960-4043-4955-9bd7-f79d19723aeb\",\"target\":\"574cbd94-40ce-44b8-b158-aee3e4fddf28\",\"label\":\"Complete Commercial and availability review\"},{\"id\":\"3b2bb6d4-a5ff-4270-9e6a-94db03103c42\",\"source\":\"574cbd94-40ce-44b8-b158-aee3e4fddf28\",\"target\":\"8831b53b-c70c-4ae0-a89c-21438b310b18\",\"label\":\"Complete Management decision\"},{\"id\":\"f2ef981d-618b-4af8-902c-c8462eecb8da\",\"source\":\"8831b53b-c70c-4ae0-a89c-21438b310b18\",\"target\":\"58d2b8a5-7e2c-442f-b79f-cd805044a3b6\",\"label\":\"Complete Approved transaction handoff\"},{\"id\":\"edge-end\",\"source\":\"58d2b8a5-7e2c-442f-b79f-cd805044a3b6\",\"target\":\"end\"}]}}"
  },
  "Steps": [
    {
      "Id": "609727BE-3A69-452A-A656-459DDE23F317",
      "Name": "Intake and validate property request",
      "Description": "Confirm the customer account, listing, request type, and submitted details before review.",
      "StepType": 0,
      "Order": 1,
      "IsStartStep": true,
      "IsEndStep": false,
      "AssignmentType": null,
      "AssignmentConfiguration": null,
      "IsRequired": true,
      "RequiredRole": "Property Management Officer",
      "EstimatedHours": 4,
      "Configuration": "{\"assignmentRules\":[{\"approvalGroup\":1,\"condition\":null,\"assignmentType\":\"Role\",\"userId\":null,\"role\":\"Property Management Officer\",\"dynamicExpression\":null,\"priority\":1}],\"approvalConfig\":null,\"qualityConfig\":{\"qualityChecks\":[{\"id\":\"property-request-1-check-1\",\"name\":\"Customer or represented Business Partner account is confirmed\",\"description\":\"Customer or represented Business Partner account is confirmed\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null},{\"id\":\"property-request-1-check-2\",\"name\":\"Published listing and submitted request details match\",\"description\":\"Published listing and submitted request details match\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null},{\"id\":\"property-request-1-check-3\",\"name\":\"Duplicate and basic eligibility checks are complete\",\"description\":\"Duplicate and basic eligibility checks are complete\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null}],\"inspectionOfficerRules\":null,\"autoPassCondition\":null},\"notificationConfig\":null,\"taskConfig\":{\"taskActionType\":\"general\",\"documentName\":null,\"requiresDocument\":false,\"documentRequirementKey\":null,\"documentRequirements\":[],\"instructions\":\"Confirm the customer account, listing, request type, and submitted details before review.\"},\"escalationRules\":null,\"formFields\":null,\"skipCondition\":null}"
    },
    {
      "Id": "80393987-6DD9-4D8D-8E0A-D4420E421549",
      "Name": "Commercial and availability review",
      "Description": "Confirm current availability, listing terms, competing requests, and commercial exceptions.",
      "StepType": 0,
      "Order": 2,
      "IsStartStep": false,
      "IsEndStep": false,
      "AssignmentType": null,
      "AssignmentConfiguration": null,
      "IsRequired": true,
      "RequiredRole": "Property Management Supervisor",
      "EstimatedHours": 4,
      "Configuration": "{\"assignmentRules\":[{\"approvalGroup\":1,\"condition\":null,\"assignmentType\":\"Role\",\"userId\":null,\"role\":\"Property Management Supervisor\",\"dynamicExpression\":null,\"priority\":1}],\"approvalConfig\":null,\"qualityConfig\":{\"qualityChecks\":[{\"id\":\"property-request-2-check-1\",\"name\":\"Current unit availability and competing requests are reviewed\",\"description\":\"Current unit availability and competing requests are reviewed\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null},{\"id\":\"property-request-2-check-2\",\"name\":\"Price, rent, lease term, and commercial exceptions are recorded\",\"description\":\"Price, rent, lease term, and commercial exceptions are recorded\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null}],\"inspectionOfficerRules\":null,\"autoPassCondition\":null},\"notificationConfig\":null,\"taskConfig\":{\"taskActionType\":\"general\",\"documentName\":null,\"requiresDocument\":false,\"documentRequirementKey\":null,\"documentRequirements\":[],\"instructions\":\"Confirm current availability, listing terms, competing requests, and commercial exceptions.\"},\"escalationRules\":null,\"formFields\":null,\"skipCondition\":null}"
    },
    {
      "Id": "EEF347D6-3045-410A-8610-65022058BED7",
      "Name": "Management decision",
      "Description": "Approve, reject, return, or waitlist the request and record the decision conditions.",
      "StepType": 0,
      "Order": 3,
      "IsStartStep": false,
      "IsEndStep": false,
      "AssignmentType": null,
      "AssignmentConfiguration": null,
      "IsRequired": true,
      "RequiredRole": "Property Manager",
      "EstimatedHours": 8,
      "Configuration": "{\"assignmentRules\":[{\"approvalGroup\":1,\"condition\":null,\"assignmentType\":\"Role\",\"userId\":null,\"role\":\"Property Manager\",\"dynamicExpression\":null,\"priority\":1}],\"approvalConfig\":null,\"qualityConfig\":{\"qualityChecks\":[{\"id\":\"property-request-3-check-1\",\"name\":\"Decision outcome, date, reason, and conditions are recorded\",\"description\":\"Decision outcome, date, reason, and conditions are recorded\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null},{\"id\":\"property-request-3-check-2\",\"name\":\"Reservation requirement is confirmed only for an approved request\",\"description\":\"Reservation requirement is confirmed only for an approved request\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null}],\"inspectionOfficerRules\":null,\"autoPassCondition\":null},\"notificationConfig\":null,\"taskConfig\":{\"taskActionType\":\"general\",\"documentName\":null,\"requiresDocument\":false,\"documentRequirementKey\":null,\"documentRequirements\":[],\"instructions\":\"Approve, reject, return, or waitlist the request and record the decision conditions.\"},\"escalationRules\":null,\"formFields\":null,\"skipCondition\":null}"
    },
    {
      "Id": "2592CEB1-E83A-43D3-B989-5B7E2F2D60BD",
      "Name": "Approved transaction handoff",
      "Description": "For an approved request, record the owning lease, sale, allocation, Legal, Finance, or reservation handoff.",
      "StepType": 0,
      "Order": 4,
      "IsStartStep": false,
      "IsEndStep": false,
      "AssignmentType": null,
      "AssignmentConfiguration": null,
      "IsRequired": true,
      "RequiredRole": "Property Management Officer",
      "EstimatedHours": 4,
      "Configuration": "{\"assignmentRules\":[{\"approvalGroup\":1,\"condition\":null,\"assignmentType\":\"Role\",\"userId\":null,\"role\":\"Property Management Officer\",\"dynamicExpression\":null,\"priority\":1}],\"approvalConfig\":null,\"qualityConfig\":{\"qualityChecks\":[{\"id\":\"property-request-4-check-1\",\"name\":\"Owning lease or sale transaction reference is recorded\",\"description\":\"Owning lease or sale transaction reference is recorded\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null},{\"id\":\"property-request-4-check-2\",\"name\":\"Reservation or availability update is linked only after approval\",\"description\":\"Reservation or availability update is linked only after approval\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null}],\"inspectionOfficerRules\":null,\"autoPassCondition\":null},\"notificationConfig\":null,\"taskConfig\":{\"taskActionType\":\"general\",\"documentName\":null,\"requiresDocument\":false,\"documentRequirementKey\":null,\"documentRequirements\":[],\"instructions\":\"For an approved request, record the owning lease, sale, allocation, Legal, Finance, or reservation handoff.\"},\"escalationRules\":null,\"formFields\":null,\"skipCondition\":null}"
    },
    {
      "Id": "C1BADF9D-B869-423E-9B65-7192311D2251",
      "Name": "Customer update and close",
      "Description": "Publish the outcome for customer tracking and close the request with its audit references.",
      "StepType": 0,
      "Order": 5,
      "IsStartStep": false,
      "IsEndStep": true,
      "AssignmentType": null,
      "AssignmentConfiguration": null,
      "IsRequired": true,
      "RequiredRole": "Property Management Officer",
      "EstimatedHours": 4,
      "Configuration": "{\"assignmentRules\":[{\"approvalGroup\":1,\"condition\":null,\"assignmentType\":\"Role\",\"userId\":null,\"role\":\"Property Management Officer\",\"dynamicExpression\":null,\"priority\":1}],\"approvalConfig\":null,\"qualityConfig\":{\"qualityChecks\":[{\"id\":\"property-request-5-check-1\",\"name\":\"Customer-facing request status reflects the final outcome\",\"description\":\"Customer-facing request status reflects the final outcome\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null},{\"id\":\"property-request-5-check-2\",\"name\":\"Request is closed with its decision and transaction audit references\",\"description\":\"Request is closed with its decision and transaction audit references\",\"isRequired\":true,\"requiresDocument\":false,\"documentType\":null,\"documentName\":null,\"applicabilityCondition\":null,\"expectedValue\":null,\"validationExpression\":null}],\"inspectionOfficerRules\":null,\"autoPassCondition\":null},\"notificationConfig\":null,\"taskConfig\":{\"taskActionType\":\"general\",\"documentName\":null,\"requiresDocument\":false,\"documentRequirementKey\":null,\"documentRequirements\":[],\"instructions\":\"Publish the outcome for customer tracking and close the request with its audit references.\"},\"escalationRules\":null,\"formFields\":null,\"skipCondition\":null}"
    }
  ],
  "Transitions": [
    {
      "FromStepId": "609727BE-3A69-452A-A656-459DDE23F317",
      "ToStepId": "80393987-6DD9-4D8D-8E0A-D4420E421549",
      "Name": "Complete Intake and validate property request",
      "Description": null,
      "Condition": null,
      "IsDefault": true,
      "Priority": 0
    },
    {
      "FromStepId": "80393987-6DD9-4D8D-8E0A-D4420E421549",
      "ToStepId": "EEF347D6-3045-410A-8610-65022058BED7",
      "Name": "Complete Commercial and availability review",
      "Description": null,
      "Condition": null,
      "IsDefault": true,
      "Priority": 0
    },
    {
      "FromStepId": "EEF347D6-3045-410A-8610-65022058BED7",
      "ToStepId": "2592CEB1-E83A-43D3-B989-5B7E2F2D60BD",
      "Name": "Complete Management decision",
      "Description": null,
      "Condition": null,
      "IsDefault": true,
      "Priority": 0
    },
    {
      "FromStepId": "2592CEB1-E83A-43D3-B989-5B7E2F2D60BD",
      "ToStepId": "C1BADF9D-B869-423E-9B65-7192311D2251",
      "Name": "Complete Approved transaction handoff",
      "Description": null,
      "Condition": null,
      "IsDefault": true,
      "Priority": 0
    }
  ]
}';

DECLARE @TenantId uniqueidentifier;
DECLARE @EntityId uniqueidentifier;
DECLARE @DefinitionId uniqueidentifier;
DECLARE @DefinitionKey uniqueidentifier;
DECLARE @PreviousId uniqueidentifier;
DECLARE @Version int;
DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @Code nvarchar(50) = JSON_VALUE(@Payload, '$.EntityType.Code');
DECLARE @OwnTransaction bit = CASE WHEN @@TRANCOUNT = 0 THEN 1 ELSE 0 END;

SELECT @TenantId = Id FROM dbo.Tenants
WHERE Code = @TargetTenantCode AND IsDeleted = 0 AND Status = 1;
IF @TenantId IS NULL
    THROW 51000, 'Target tenant is missing or inactive. Check the selected database and tenant code.', 1;

SELECT @@SERVERNAME AS ServerName, DB_NAME() AS TargetDatabase, @TargetTenantCode AS TenantCode, @TenantId AS TenantId;
IF @OwnTransaction = 1 BEGIN TRANSACTION;
ELSE SAVE TRANSACTION EstateWorkflowImport;

BEGIN TRY
    IF (SELECT COUNT(*) FROM dbo.WorkflowEntityTypes WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND Code = @Code AND IsDeleted = 0) > 1
        THROW 51001, 'Multiple matching entity types exist. Resolve the duplicates before importing.', 1;

    SELECT @EntityId = Id FROM dbo.WorkflowEntityTypes
    WHERE TenantId = @TenantId AND Code = @Code AND IsDeleted = 0;

    IF @EntityId IS NULL
    BEGIN
        SET @EntityId = NEWID();
        INSERT INTO dbo.WorkflowEntityTypes
            (Id, Code, Name, Description, EntityClassName, PropertySchema, IsActive,
             DisplayOrder, Icon, ColorCode, CreatedAt, CreatedBy, IsDeleted, TenantId)
        SELECT @EntityId, Code, Name, Description, EntityClassName, PropertySchema, 1,
            DisplayOrder, Icon, ColorCode, @Now, N'EstateWorkflowImport', 0, @TenantId
        FROM OPENJSON(@Payload, '$.EntityType') WITH
            (Code nvarchar(50), Name nvarchar(100), Description nvarchar(500),
             EntityClassName nvarchar(500), PropertySchema nvarchar(max),
             DisplayOrder int, Icon nvarchar(50), ColorCode nvarchar(20));
    END;

    SELECT TOP (1) @DefinitionId = Id
    FROM dbo.WorkflowDefinitions WITH (UPDLOCK, HOLDLOCK)
    WHERE TenantId = @TenantId AND EntityTypeId = @EntityId AND IsDeleted = 0
      AND IsActive = 1 AND LifecycleStatus = 1
      AND ChangeSummary = @ImportMarker
    ORDER BY Version DESC;

    IF @DefinitionId IS NULL
    BEGIN
        SELECT TOP (1) @PreviousId = Id, @DefinitionKey = DefinitionKey
        FROM dbo.WorkflowDefinitions
        WHERE TenantId = @TenantId AND EntityTypeId = @EntityId AND IsDeleted = 0
        ORDER BY IsActive DESC, Version DESC, CreatedAt DESC;

        SELECT @Version = ISNULL(MAX(Version), 0) + 1
        FROM dbo.WorkflowDefinitions
        WHERE TenantId = @TenantId AND EntityTypeId = @EntityId;
        IF @Version < 6 SET @Version = 6;
        SET @DefinitionId = NEWID();
        SET @DefinitionKey = COALESCE(@DefinitionKey, @DefinitionId);

        INSERT INTO dbo.WorkflowDefinitions
            (Id, DefinitionKey, Name, Description, EntityTypeId, Version, LifecycleStatus,
             IsActive, ChangeSummary, SupersedesDefinitionId, PublishedAt, Configuration,
             CreatedAt, CreatedBy, IsDeleted, TenantId)
        SELECT @DefinitionId, @DefinitionKey, Name, Description, @EntityId, @Version, 1,
            1, @ImportMarker, @PreviousId, @Now, Configuration,
            @Now, N'EstateWorkflowImport', 0, @TenantId
        FROM OPENJSON(@Payload, '$.Definition') WITH
            (Name nvarchar(100), Description nvarchar(500), Configuration nvarchar(max));

        DECLARE @StepMap TABLE (SourceId uniqueidentifier PRIMARY KEY, TargetId uniqueidentifier NOT NULL);
        INSERT INTO @StepMap
        SELECT Id, NEWID() FROM OPENJSON(@Payload, '$.Steps') WITH (Id uniqueidentifier);

        INSERT INTO dbo.WorkflowSteps
            (Id, WorkflowDefinitionId, Name, Description, StepType, [Order], IsStartStep,
             IsEndStep, AssignmentType, AssignmentConfiguration, IsRequired, RequiredRole,
             EstimatedHours, Configuration, CreatedAt, CreatedBy, IsDeleted, TenantId)
        SELECT m.TargetId, @DefinitionId, s.Name, s.Description, s.StepType, s.[Order],
            s.IsStartStep, s.IsEndStep, s.AssignmentType, s.AssignmentConfiguration,
            s.IsRequired, s.RequiredRole, s.EstimatedHours, s.Configuration,
            @Now, N'EstateWorkflowImport', 0, @TenantId
        FROM OPENJSON(@Payload, '$.Steps') WITH
            (Id uniqueidentifier, Name nvarchar(100), Description nvarchar(500), StepType int,
             [Order] int, IsStartStep bit, IsEndStep bit, AssignmentType nvarchar(50),
             AssignmentConfiguration nvarchar(max), IsRequired bit, RequiredRole nvarchar(100),
             EstimatedHours decimal(18,2), Configuration nvarchar(max)) s
        JOIN @StepMap m ON m.SourceId = s.Id;

        INSERT INTO dbo.WorkflowTransitions
            (Id, WorkflowDefinitionId, FromStepId, ToStepId, Name, Description, Condition,
             IsDefault, Priority, CreatedAt, CreatedBy, IsDeleted, TenantId)
        SELECT NEWID(), @DefinitionId, f.TargetId, t.TargetId, x.Name, x.Description,
            x.Condition, x.IsDefault, x.Priority, @Now, N'EstateWorkflowImport', 0, @TenantId
        FROM OPENJSON(@Payload, '$.Transitions') WITH
            (FromStepId uniqueidentifier, ToStepId uniqueidentifier, Name nvarchar(100),
             Description nvarchar(500), Condition nvarchar(max), IsDefault bit, Priority int) x
        JOIN @StepMap f ON f.SourceId = x.FromStepId
        JOIN @StepMap t ON t.SourceId = x.ToStepId;

        -- Only new cases choose the imported version; old instances retain their definitions.
        UPDATE dbo.WorkflowDefinitions SET IsActive = 0, LifecycleStatus = 2,
            RetiredAt = COALESCE(RetiredAt, @Now), UpdatedAt = @Now, UpdatedBy = N'EstateWorkflowImport'
        WHERE TenantId = @TenantId AND EntityTypeId = @EntityId
          AND Id <> @DefinitionId AND IsDeleted = 0 AND IsActive = 1;

        UPDATE dbo.WorkflowEntityTypes SET
            Name = JSON_VALUE(@Payload, '$.EntityType.Name'), IsActive = 1,
            UpdatedAt = @Now, UpdatedBy = N'EstateWorkflowImport'
        WHERE Id = @EntityId AND TenantId = @TenantId;
    END;

    IF (SELECT COUNT(*) FROM dbo.WorkflowSteps
        WHERE WorkflowDefinitionId = @DefinitionId AND TenantId = @TenantId AND IsDeleted = 0) <> 5
        THROW 51002, 'Expected five workflow steps. Import rolled back.', 1;
    IF (SELECT COUNT(*) FROM dbo.WorkflowTransitions
        WHERE WorkflowDefinitionId = @DefinitionId AND TenantId = @TenantId AND IsDeleted = 0) <> 4
        THROW 51003, 'Expected four workflow transitions. Import rolled back.', 1;
    IF EXISTS (SELECT 1 FROM dbo.WorkflowSteps WHERE WorkflowDefinitionId = @DefinitionId
        AND IsDeleted = 0 AND ISNULL(ISJSON(Configuration), 0) <> 1)
        THROW 51004, 'A checklist configuration is not valid JSON. Import rolled back.', 1;
    IF (SELECT COUNT(*) FROM dbo.WorkflowSteps s
        CROSS APPLY OPENJSON(s.Configuration, '$.qualityConfig.qualityChecks') c
        WHERE s.WorkflowDefinitionId = @DefinitionId AND s.IsDeleted = 0) <> 11
        THROW 51005, 'Expected eleven checklist items. Import rolled back.', 1;

    SELECT DB_NAME() AS DatabaseName, @TargetTenantCode AS TenantCode, d.Id AS WorkflowDefinitionId,
        d.Name, d.Version, d.IsActive, d.LifecycleStatus, @Apply AS ApplyRequested
    FROM dbo.WorkflowDefinitions d WHERE d.Id = @DefinitionId;
    SELECT s.[Order], s.Name, s.RequiredRole,
        (SELECT COUNT(*) FROM OPENJSON(s.Configuration, '$.qualityConfig.qualityChecks')) AS ChecklistItems
    FROM dbo.WorkflowSteps s WHERE s.WorkflowDefinitionId = @DefinitionId AND s.IsDeleted = 0
    ORDER BY s.[Order];

    IF @Apply = 0
    BEGIN
        IF @OwnTransaction = 1 ROLLBACK TRANSACTION;
        ELSE ROLLBACK TRANSACTION EstateWorkflowImport;
        PRINT 'Rehearsal passed; all changes rolled back.';
    END
    ELSE
    BEGIN
        IF @OwnTransaction = 1 COMMIT TRANSACTION;
        PRINT 'Import validated. Search Workflow Administration for: Property Requests and Listing Applications';
    END;
END TRY
BEGIN CATCH
    IF @OwnTransaction = 1 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    ELSE IF @OwnTransaction = 0 AND XACT_STATE() = 1 ROLLBACK TRANSACTION EstateWorkflowImport;
    THROW;
END CATCH;
GO
