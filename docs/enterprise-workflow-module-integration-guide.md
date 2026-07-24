# Enterprise Workflow Module Integration Guide

## Rule

A module owns its business transaction and status meanings. The workflow platform owns routing, approvers, checklist evidence, approval policy, authorization, history, notifications, signatures, delegation, SLA, and audit.

Do not create module-specific approval dialogs, checklist tables, evidence screens, workflow summary APIs, delegation logic, SLA jobs, or parallel workflow state machines.

## Shared Building Blocks

Use these shared backend services:

| Need | Shared API |
| --- | --- |
| Submit a record into workflow | `IWorkflowIntegrationService.SubmitAsync` |
| Approve or reject a record | `IWorkflowIntegrationService.ProcessApprovalAsync` |
| Check if current user can approve | `IWorkflowIntegrationService.CanUserApproveAsync` |
| Recall a workflow | `IWorkflowIntegrationService.RecallAsync` |
| Apply module status after workflow outcome | `IWorkflowStatusAdapterRegistry.GetAdapter(entityType)` |
| Map module-specific status fields | `IWorkflowStatusAdapter` |

Use these shared frontend controls and hooks:

| Need | Shared frontend control |
| --- | --- |
| Detail-page submit/approve/reject/recall/task controls | `WorkflowApprovalActions` |
| Detail-page workflow state wiring | `useWorkflowRecord` |
| Detail-page history tab or side panel | `WorkflowTabContent`, `WorkflowRecordPanel` |
| List/grid workflow summaries | `useWorkflowEntitySummaries` |
| Pending-approver display | `formatPendingApprovers` |
| Workflow reason/audit dialog | `WorkflowReasonDialog` |

## Do Not Recreate

Module teams must not create:

- Separate approve/reject dialogs.
- `window.prompt`, `window.confirm`, or browser-native workflow prompts.
- Module-specific checklist-evidence tables.
- Module-specific workflow history widgets.
- Module-specific delegation, out-of-office, SLA, escalation, or evidence policy logic.
- Per-row workflow-summary API calls on grids.
- Status-only permission checks such as `record.status === 'Submitted'` to decide approval access.

The shared engine rechecks authorization, checklist evidence, signatures, SOD, delegation, and policy during execution. UI visibility is convenience, not security.

## Backend Integration Steps

### 1. Choose The Canonical Entity Type

Use one stable entity type everywhere: workflow definition, backend adapter, frontend hook, and API calls.

Example:

```csharp
private const string EntityType = "PurchaseRequisition";
```

If legacy names already exist, declare aliases in the status adapter, not in scattered module code.

### 2. Add A Status Adapter

Implement `IWorkflowStatusAdapter` in the Core assembly. It is discovered automatically; do not add a central registration line.

```csharp
public sealed class PurchaseRequisitionWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } =
    [
        "PurchaseRequisition",
        "Purchase Requisition",
        "PURCHASE_REQUISITION"
    ];

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var requisition = (PurchaseRequisition)entity;
        requisition.Status = outcome switch
        {
            WorkflowOutcome.Pending => "Submitted",
            WorkflowOutcome.Approved => "Approved",
            WorkflowOutcome.Rejected => "Rejected",
            _ => requisition.Status
        };
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var requisition = (PurchaseRequisition)entity;
        requisition.Status = outcome switch
        {
            WorkflowOutcome.Pending => "Pending Approval",
            WorkflowOutcome.Approved => "Approved",
            WorkflowOutcome.Rejected => "Rejected",
            _ => requisition.Status
        };
        requisition.ApprovedById = outcome == WorkflowOutcome.Approved ? userId : null;
        requisition.ApprovedDate = outcome == WorkflowOutcome.Approved ? DateTime.UtcNow : null;
        requisition.RejectionReason = outcome == WorkflowOutcome.Rejected ? rejectionReason : null;
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var requisition = (PurchaseRequisition)entity;
        requisition.Status = "Draft";
        requisition.RejectionReason = reason;
    }
}
```

### 3. Submit Through The Shared Integration Service

The module service should save the business record and workflow state in the same operation where possible.

```csharp
public async Task SubmitAsync(Guid id, Guid userId, CancellationToken cancellationToken)
{
    var requisition = await db.PurchaseRequisitions
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new InvalidOperationException("Purchase requisition not found.");

    if (requisition.Status != "Draft")
    {
        throw new InvalidOperationException("Only draft requisitions can be submitted.");
    }

    var workflowResult = await workflowIntegration.SubmitAsync(EntityType, requisition.Id);
    statusAdapters.GetAdapter(EntityType).ApplySubmitOutcome(requisition, workflowResult.Outcome, userId);

    requisition.SubmittedById = userId;
    requisition.SubmittedDate = DateTime.UtcNow;
    await db.SaveChangesAsync(cancellationToken);
}
```

### 4. Approve Or Reject Through The Shared Integration Service

Always pass `ApplicationUser.Id`. Do not pass `Employee.Id`.

```csharp
public async Task ReviewAsync(Guid id, Guid userId, bool approved, string? comments, CancellationToken cancellationToken)
{
    var requisition = await db.PurchaseRequisitions
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new InvalidOperationException("Purchase requisition not found.");

    var canApprove = await workflowIntegration.CanUserApproveAsync(EntityType, requisition.Id, userId);
    if (!canApprove)
    {
        throw new UnauthorizedAccessException("User cannot approve the current workflow step.");
    }

    var action = approved ? "Approve" : "Reject";
    var workflowResult = await workflowIntegration.ProcessApprovalAsync(
        EntityType,
        requisition.Id,
        userId,
        action,
        comments);

    statusAdapters.GetAdapter(EntityType).ApplyApprovalOutcome(
        requisition,
        workflowResult.Outcome,
        userId,
        comments);

    await db.SaveChangesAsync(cancellationToken);
}
```

### 5. Recall Through The Shared Integration Service

```csharp
public async Task RecallAsync(Guid id, Guid userId, string? reason, CancellationToken cancellationToken)
{
    var requisition = await db.PurchaseRequisitions
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new InvalidOperationException("Purchase requisition not found.");

    var workflowResult = await workflowIntegration.RecallAsync(EntityType, requisition.Id, userId, reason);
    statusAdapters.GetAdapter(EntityType).ApplyRecallOutcome(requisition, userId, reason);

    await db.SaveChangesAsync(cancellationToken);
}
```

### 6. Controller Pattern

Controllers should be thin. They should authenticate, translate DTOs, and call the module service.

```csharp
[HttpPost("{id:guid}/submit")]
public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
{
    var userId = RequireApplicationUserId();
    await requisitionService.SubmitAsync(id, userId, cancellationToken);
    return Ok(new { success = true });
}

[HttpPost("{id:guid}/review")]
public async Task<IActionResult> Review(Guid id, [FromBody] ReviewRequest request, CancellationToken cancellationToken)
{
    var userId = RequireApplicationUserId();
    await requisitionService.ReviewAsync(id, userId, request.Approved, request.Comments, cancellationToken);
    return Ok(new { success = true });
}
```

## Frontend Detail Page Pattern

Use `useWorkflowRecord` and `WorkflowApprovalActions`. Do not build custom submit, approve, reject, recall, checklist, task, or signature dialogs.

```tsx
import {
  WorkflowApprovalActions,
  WorkflowTabContent,
  WorkflowTabTrigger,
  useWorkflowRecord,
} from '@/components/workflow';

const workflow = useWorkflowRecord({
  entityType: 'PurchaseRequisition',
  entityId: requisition.id,
  entityLabel: 'Purchase Requisition',
  entityNumber: requisition.requisitionNumber,
  status: requisition.status,
  commands: {
    submit: () => procurementApi.submitPurchaseRequisition(requisition.id),
    approve: ({ comments }) =>
      procurementApi.reviewPurchaseRequisition(requisition.id, { approved: true, comments }),
    reject: ({ comments }) =>
      procurementApi.reviewPurchaseRequisition(requisition.id, { approved: false, comments }),
    afterAction: reloadRequisition,
  },
});

return (
  <>
    <WorkflowApprovalActions {...workflow.actionProps} />

    <Tabs>
      <TabsList>
        <TabsTrigger value="details">Details</TabsTrigger>
        <WorkflowTabTrigger />
      </TabsList>
      <TabsContent value="details">...</TabsContent>
      <WorkflowTabContent
        entityType="PurchaseRequisition"
        entityId={requisition.id}
        entityLabel="Purchase Requisition"
        entityNumber={requisition.requisitionNumber}
      />
    </Tabs>
  </>
);
```

`WorkflowApprovalActions` automatically handles:

- Submit confirmation.
- Approve and reject comments.
- Required checklist responses.
- Required checklist document uploads.
- Task-step completion.
- Signature staging.
- Recall.
- Delegate/send-back controls.
- Current-step and pending-approver display.

## Frontend List Or Grid Pattern

Use the batch summary hook. Do not fetch one workflow summary per row.

```tsx
import {
  WorkflowApprovalActions,
} from '@/components/workflow';
import {
  formatPendingApprovers,
  useWorkflowEntitySummaries,
} from '@/hooks/useWorkflowEntitySummaries';

const ids = rows.map(row => row.id);
const { summariesById } = useWorkflowEntitySummaries('PurchaseRequisition', ids, rows.length > 0, refreshKey);

return rows.map(row => {
  const summary = summariesById[row.id];
  const pending = formatPendingApprovers(summary?.pendingApprovers ?? []);

  return (
    <TableRow key={row.id}>
      <TableCell>{row.requisitionNumber}</TableCell>
      <TableCell>{summary?.currentStepName ?? row.status}</TableCell>
      <TableCell title={pending.full}>{pending.short || 'None'}</TableCell>
      <TableCell>
        <WorkflowApprovalActions
          entityType="PurchaseRequisition"
          entityId={row.id}
          entityLabel="Purchase Requisition"
          entityNumber={row.requisitionNumber}
          status={row.status}
          workflowSummary={summary}
          loadWorkflowSummary={false}
          renderMode="menu-items"
          onAfterAction={reloadRows}
          onSubmit={() => procurementApi.submitPurchaseRequisition(row.id)}
          onApprove={(comments) =>
            procurementApi.reviewPurchaseRequisition(row.id, { approved: true, comments })}
          onReject={(comments) =>
            procurementApi.reviewPurchaseRequisition(row.id, { approved: false, comments })}
        />
      </TableCell>
    </TableRow>
  );
});
```

## Task Steps And Checklist Evidence

Task and approval steps can both require checklist items and document evidence. Module teams should not create separate upload controls for those items.

Use workflow designer configuration:

- Step type: `Task` or `Approval`.
- Checklist item: required or optional.
- `Requires document evidence`: enabled when a document must be uploaded.
- Document type and document name: configured per checklist item.

At runtime:

- `WorkflowApprovalActions` shows checklist controls.
- Required evidence uploads go through the shared step attachment APIs.
- The engine blocks progress when required checklist/evidence is missing.
- Evidence verification, legal hold, retention, and audit are administered in workflow governance.

## Reason Dialog Pattern

For workflow reasons in custom admin surfaces, use `WorkflowReasonDialog`.

```tsx
<WorkflowReasonDialog
  open={!!selectedItem}
  onOpenChange={open => !open && setSelectedItem(null)}
  title="Remove approver"
  description="This action changes the active workflow approval chain."
  reasonLabel="Removal reason"
  reasonPlaceholder="Explain why this approver is being removed"
  confirmText="Remove approver"
  variant="destructive"
  isLoading={saving}
  onConfirm={async reason => {
    await workflowApi.removeAdHocApprover(selectedItem.id, reason);
    await reload();
  }}
/>
```

Never use browser-native `prompt` or `confirm` in workflow UI.

## Workflow Administration Ownership

Workflow administration owns:

- Definition design, versioning, draft/published/retired lifecycle.
- Approval policies.
- Checklist and evidence policy.
- Delegation and out-of-office substitution.
- Working calendars and SLA escalation rules.
- Ad hoc approvers.
- Evidence verification, legal hold, retention, and audit archive.
- Template import/export.

Module teams should only provide the business transaction data and call the shared workflow services.

## Definition Lifecycle

Workflow definitions are versioned configuration, not editable runtime records:

- Build and test changes in a Draft.
- Publish only after validation.
- Publishing atomically retires the previously published version in that workflow family.
- Published and Retired versions are immutable.
- Clone a Published or Retired definition to create the next Draft.
- Do not change an instance's `WorkflowDefinitionId`.
- Runtime module integrations resolve Published and active definitions only.
- Module code must never activate a draft by updating `IsActive` directly.

## Segregation Of Duties

Use approval-step controls for rules local to one step:

- Prevent the workflow initiator from approving.
- Require a different user for each approval slot.

Use cross-step SOD rules when the current approver must differ from:

- The actor who completed the immediately preceding step.
- Anyone who approved an earlier workflow step.
- The actor assigned to or approving a named earlier step.
- A user ID in workflow context, such as `requestedById`, `createdById`, `evaluatorId`, or `receiverId`.

Context-user fields must resolve to an `ApplicationUser.Id` GUID.

## Testing Checklist

Every module integration must include focused tests or test evidence for:

- Submit starts the shared workflow and moves the module status correctly.
- Approve updates module status only after the shared engine returns an approved outcome.
- Reject requires comments where the module policy requires them and stores the reason.
- Recall returns the module record to the correct requester-owned state.
- Direct API attempts by a non-approver are rejected.
- Required checklist/evidence blocks progression until satisfied.
- SOD rules block prohibited users.
- Tenant isolation prevents cross-tenant workflow access.
- List pages use batch summaries, not one request per row.
- Detail pages use shared workflow controls and history.

## Module Acceptance Checklist

- One canonical entity type is used consistently by backend, frontend, workflow definition, and adapter.
- One and only one status adapter resolves every alias.
- Submit, approve, reject, and recall use `IWorkflowIntegrationService`.
- The authenticated application-user ID is passed, never an employee ID.
- Module status changes are applied through `IWorkflowStatusAdapterRegistry`.
- Detail pages use `useWorkflowRecord`, `WorkflowApprovalActions`, and `WorkflowTabContent` or `WorkflowRecordPanel`.
- List pages use `useWorkflowEntitySummaries`.
- Checklist evidence uses shared workflow attachment APIs.
- Module code has no custom approval dialogs, evidence screens, delegation logic, SLA jobs, or workflow history widgets.
- Module code has no `window.prompt` or `window.confirm` for workflow actions.
- Direct API attempts are covered by authorization/SOD tests.
- Module status and workflow outcome remain synchronized after every action.
