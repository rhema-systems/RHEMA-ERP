# Enterprise Workflow Module Integration Guide

## Rule

A module owns its business transaction and status meanings. The workflow platform owns routing, approvers, checklist evidence, approval policy, authorization, history, notifications, and audit.

Do not create module-specific approval dialogs, checklist tables, workflow summary APIs, or parallel workflow state machines.

## Backend Integration

1. Choose one canonical entity type, such as `PurchaseRequisition`, and declare any legacy aliases in one `IWorkflowStatusAdapter`.
2. Implement `IWorkflowStatusAdapter` in the Core assembly. It is discovered automatically; no central registration line is required.
3. Inject `IWorkflowIntegrationService` into the module service.
4. Call `SubmitAsync`, `ProcessApprovalAsync`, or `RecallAsync` using the canonical entity type and authenticated `ApplicationUser.Id`.
5. Apply the returned `WorkflowOutcome` through `IWorkflowStatusAdapterRegistry` and save the module transaction in the same service operation.
6. Never infer approval permission from the module status. Call `CanUserApproveAsync`; the shared engine enforces checklist evidence and SOD again during processing.

```csharp
var canApprove = await workflowIntegration.CanUserApproveAsync(EntityType, entity.Id, userId);
if (!canApprove)
{
    throw new UnauthorizedAccessException("User cannot approve the current workflow step.");
}

var result = await workflowIntegration.ProcessApprovalAsync(EntityType, entity.Id, userId, "Approve", comments);
statusAdapters.GetAdapter(EntityType).ApplyApprovalOutcome(entity, result.Outcome, userId, comments);
await unitOfWork.SaveChangesAsync();
```

## Frontend Integration

Use the shared workflow exports and `useWorkflowRecord`. The hook loads the normalized summary and converts module commands into the props expected by the global controls.

```tsx
const workflow = useWorkflowRecord({
  entityType: 'PurchaseRequisition',
  entityId: requisition.id,
  entityLabel: 'Purchase Requisition',
  entityNumber: requisition.requisitionNumber,
  status: requisition.status,
  commands: {
    submit: () => purchasingService.submitPurchaseRequisition(requisition.id),
    approve: ({ comments }) => purchasingService.approvePurchaseRequisition(requisition.id, { approved: true, comments }),
    reject: ({ comments }) => purchasingService.approvePurchaseRequisition(requisition.id, { approved: false, comments }),
    afterAction: reloadRequisition,
  },
});

<WorkflowApprovalActions {...workflow.actionProps} />
```

Use `WorkflowRecordPanel` or `WorkflowTabContent` for full history. Use `useWorkflowEntitySummaries` on list/grid pages so one batch request serves every row.

Module teams do not build separate evidence or signature screens. Checklist/task uploads flow through the shared step attachment APIs, while evidence policy, verification, retention, and legal hold are administered globally. When a step requires an electronic signature, `WorkflowApprovalActions` stages the signed attestation before the module command runs; the engine validates and commits it inside approval processing.

## Definition Lifecycle

Workflow definitions are versioned configuration, not editable runtime records:

- Build and test changes in a Draft.
- Publish only after validation. Publishing atomically retires the previously published version in that workflow family.
- Published and Retired versions are immutable. Clone either one to create the next Draft.
- Do not change an instance's `WorkflowDefinitionId`. It pins that instance to the exact steps, transitions, approvers, and evidence rules active when it started.
- Runtime module integrations resolve Published and active definitions only.
- Review version history and structural comparison before publishing. Removed or materially changed steps and transitions are flagged as potentially breaking for future instances.

Module code must never activate a draft by updating `IsActive` directly. Definition lifecycle changes belong to the shared workflow administration APIs.

## Segregation Of Duties

Use the approval-step controls for rules local to one step:

- Prevent the workflow initiator from approving.
- Require a different user for each approval slot.

Use cross-step SOD rules when the current approver must differ from:

- The actor who completed the immediately preceding step.
- Anyone who approved an earlier workflow step.
- The actor assigned to or approving a named earlier step.
- A user ID in workflow context, such as `requestedById`, `createdById`, `evaluatorId`, or `receiverId`.

Context-user fields must resolve to an `ApplicationUser.Id` GUID. SOD rules are checked when approval capability is calculated and again by the workflow engine, so hiding a button or calling an API directly cannot bypass them.

## Operational Capabilities

- Configure delegation, out-of-office substitution, working calendars, evidence policy, and approval policy sets in shared workflow administration.
- Use configured escalation rules for notify, reassign, auto-approve, or cancel behavior. Do not schedule module-specific reminder jobs.
- Use send-back and correction ownership for rework loops; do not simulate rework by changing the module status directly.
- Use the shared integration queue for external calls that require idempotency, retries, dead-letter handling, and reconciliation.
- Use workflow templates to promote definitions between tenants/environments. Imports always create a tenant-mapped Draft.
- Use the mobile workflow inbox and offline queue for approval decisions. Only explicit approve/reject actions are replayable, and server authorization is always re-evaluated.
- Use the audit archive endpoint for date-bounded, hash-verifiable workflow evidence exports.

## Module Acceptance Checklist

- One canonical entity type is used consistently by backend, frontend, workflow definition, and adapter.
- One and only one status adapter resolves every alias.
- Submit, approve, reject, and recall use the shared integration service.
- The authenticated application-user ID is passed, never an employee ID.
- Detail pages use shared workflow actions/history.
- List pages use batch summaries rather than one request per row.
- Direct API attempts are covered by authorization/SOD tests.
- Module status and workflow outcome remain synchronized after every action.
