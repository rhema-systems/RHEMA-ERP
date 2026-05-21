import { type Dispatch, type SetStateAction } from 'react';
import { Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowRecordPanel } from '@/components/workflow/WorkflowRecordTab';
import {
  type CreateProjectDeliverableDto,
  type CreateProjectExpenseDto,
  type CreateProjectTimesheetEntryDto,
  type ProjectDetailDto,
  projectService,
} from '@/services/projectService';

type FlatWorkItem = { id: string; title: string };
type IdentifiedRecord = { id: string };

type ProjectExecutionTabProps = {
  project: ProjectDetailDto;
  flat: FlatWorkItem[];
  milestoneTitles: Map<string, string>;
  workItemTitles: Map<string, string>;
  deliverable: CreateProjectDeliverableDto;
  setDeliverable: Dispatch<SetStateAction<CreateProjectDeliverableDto>>;
  deliverableStatusOptions: string[];
  boolValue: (value?: boolean | null) => string;
  formatCatalogLabel: (value?: string | null) => string;
  formatDateLabel: (value?: string) => string;
  expandedDeliverableHistoryId: string | null;
  setExpandedDeliverableHistoryId: Dispatch<SetStateAction<string | null>>;
  onLoad: () => Promise<void>;
  onOpenWorkflows: () => void;
  onAddDeliverable: () => Promise<void> | void;
  onDeleteDeliverable: (deliverableId: string) => Promise<void> | void;
  activeUsers: IdentifiedRecord[];
  formatUserLabel: (user: IdentifiedRecord) => string;
  timesheet: CreateProjectTimesheetEntryDto;
  setTimesheet: Dispatch<SetStateAction<CreateProjectTimesheetEntryDto>>;
  timesheetWorkTypeOptions: string[];
  currentUserId?: string;
  onAddTimesheet: () => Promise<void> | void;
  onApproveTimesheet: (timesheetId: string) => Promise<void> | void;
  expense: CreateProjectExpenseDto;
  setExpense: Dispatch<SetStateAction<CreateProjectExpenseDto>>;
  expenseCategoryOptions: string[];
  expenseCurrencyOptions: string[];
  baseCurrencyCode: string;
  getCurrencyOptionLabel: (code: string) => string;
  onAddExpense: () => Promise<void> | void;
  onApproveExpense: (expenseId: string) => Promise<void> | void;
  formatMoney: (value: number | undefined, currency?: string | null, maximumFractionDigits?: number) => string;
  getResolvedUserLabel: (userId?: string, displayName?: string, fallback?: string) => string;
};

export function ProjectExecutionTab({
  project,
  flat,
  milestoneTitles,
  workItemTitles,
  deliverable,
  setDeliverable,
  deliverableStatusOptions,
  boolValue,
  formatCatalogLabel,
  formatDateLabel,
  expandedDeliverableHistoryId,
  setExpandedDeliverableHistoryId,
  onLoad,
  onOpenWorkflows,
  onAddDeliverable,
  onDeleteDeliverable,
  activeUsers,
  formatUserLabel,
  timesheet,
  setTimesheet,
  timesheetWorkTypeOptions,
  currentUserId,
  onAddTimesheet,
  onApproveTimesheet,
  expense,
  setExpense,
  expenseCategoryOptions,
  expenseCurrencyOptions,
  baseCurrencyCode,
  getCurrencyOptionLabel,
  onAddExpense,
  onApproveExpense,
  formatMoney,
  getResolvedUserLabel,
}: ProjectExecutionTabProps) {
  return (
    <div className="space-y-6">
      <Card>
        <CardHeader><CardTitle>Deliverables</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={deliverable.title} onChange={(event) => setDeliverable((current) => ({ ...current, title: event.target.value }))} /></div>
            <div className="grid gap-2"><Label>Status</Label><Select value={deliverable.status || deliverableStatusOptions[0]} onValueChange={(value) => setDeliverable((current) => ({ ...current, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{deliverableStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Target Date</Label><Input type="date" value={deliverable.targetDate ? String(deliverable.targetDate).slice(0, 10) : ''} onChange={(event) => setDeliverable((current) => ({ ...current, targetDate: event.target.value || undefined }))} /></div>
            <div className="grid gap-2"><Label>Work Item</Label><Select value={deliverable.workItemId || 'none'} onValueChange={(value) => setDeliverable((current) => ({ ...current, workItemId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No work item</SelectItem>{flat.map((item) => <SelectItem key={item.id} value={item.id}>{item.title}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Milestone</Label><Select value={deliverable.milestoneId || 'none'} onValueChange={(value) => setDeliverable((current) => ({ ...current, milestoneId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No milestone</SelectItem>{project.milestones.map((item) => <SelectItem key={item.id} value={item.id}>{item.title}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>External Submission</Label><Select value={boolValue(deliverable.externalSubmissionAllowed)} onValueChange={(value) => setDeliverable((current) => ({ ...current, externalSubmissionAllowed: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">Internal only</SelectItem><SelectItem value="true">Allowed</SelectItem></SelectContent></Select></div>
            <div className="grid gap-2"><Label>External Sign-off</Label><Select value={boolValue(deliverable.externalSignOffRequired)} onValueChange={(value) => setDeliverable((current) => ({ ...current, externalSignOffRequired: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">Not required</SelectItem><SelectItem value="true">Required</SelectItem></SelectContent></Select></div>
            <div className="grid gap-2"><Label>Portal Visibility</Label><Select value={boolValue(deliverable.isExternalVisible)} onValueChange={(value) => setDeliverable((current) => ({ ...current, isExternalVisible: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">Internal only</SelectItem><SelectItem value="true">Visible externally</SelectItem></SelectContent></Select></div>
            <div className="grid gap-2 md:col-span-3"><Label>Description</Label><Textarea rows={2} value={deliverable.description || ''} onChange={(event) => setDeliverable((current) => ({ ...current, description: event.target.value }))} /></div>
            <div className="flex items-end"><Button onClick={onAddDeliverable}><Plus className="mr-2 h-4 w-4" />Add Deliverable</Button></div>
          </div>
          <div className="space-y-3">
            {project.deliverables.length === 0 ? <div className="text-sm text-muted-foreground">No deliverables are available.</div> : null}
            {project.deliverables.map((item) => (
              <div key={item.id} className="rounded-lg border p-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="space-y-2">
                    <div className="font-medium">{item.title}</div>
                    <div className="text-sm text-muted-foreground">
                      {item.status}
                      {item.targetDate ? ` | due ${formatDateLabel(item.targetDate)}` : ''}
                      {item.workItemId ? ` | ${workItemTitles.get(item.workItemId) || item.workItemId}` : ''}
                      {item.milestoneId ? ` | ${milestoneTitles.get(item.milestoneId) || item.milestoneId}` : ''}
                    </div>
                    <div className="flex flex-wrap gap-2">
                      {item.externalSubmissionAllowed ? <Badge variant="outline">Portal submission</Badge> : null}
                      {item.externalSignOffRequired ? <Badge variant="outline">External sign-off required</Badge> : null}
                      {item.isExternalVisible ? <Badge variant="outline">Visible externally</Badge> : null}
                    </div>
                  </div>
                  <div className="flex min-w-[220px] flex-col items-stretch gap-2">
                    <WorkflowApprovalActions
                      entityType="ProjectDeliverable"
                      entityId={item.id}
                      entityLabel="Deliverable"
                      entityNumber={item.title}
                      status={item.status}
                      loadWorkflowSummary
                      showStepBadge
                      canSubmit={item.status === 'Draft' || item.status === 'Rejected'}
                      canApproveReject={item.status === 'PendingApproval'}
                      onSubmit={async () => { await projectService.submitDeliverable(item.id, { notes: 'Submitted from workspace' }); }}
                      onApprove={async (comments) => { await projectService.approveDeliverable(item.id, comments); }}
                      onReject={async (comments) => { await projectService.rejectDeliverable(item.id, comments || 'Rejected from workspace'); }}
                      onAfterAction={onLoad}
                      onOpenWorkflows={onOpenWorkflows}
                    />
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setExpandedDeliverableHistoryId((current) => current === item.id ? null : item.id)}
                    >
                      {expandedDeliverableHistoryId === item.id ? 'Hide Approval History' : 'View Approval History'}
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => onDeleteDeliverable(item.id)}>Remove</Button>
                  </div>
                </div>
                {item.externalApprovedAt ? (
                  <div className="mt-2 text-sm text-muted-foreground">
                    External sign-off recorded {formatDateLabel(item.externalApprovedAt)}
                    {item.externalApprovalNotes ? ` | ${item.externalApprovalNotes}` : ''}
                  </div>
                ) : null}
                {item.acceptanceNotes ? <div className="mt-2 text-sm text-muted-foreground">{item.acceptanceNotes}</div> : null}
                {item.externalReviews.length ? (
                  <div className="mt-3 space-y-2 rounded-md border bg-muted/20 p-3">
                    <div className="text-sm font-medium">External Review Trail</div>
                    {item.externalReviews.map((review) => (
                      <div key={review.id} className="rounded-md border bg-background p-3 text-sm">
                        <div className="flex flex-wrap items-center gap-2">
                          <Badge variant="outline">{review.decision}</Badge>
                          <span className="text-muted-foreground">{formatDateLabel(review.reviewDate)}</span>
                          {review.statusSnapshot ? <span className="text-muted-foreground">| status {review.statusSnapshot}</span> : null}
                        </div>
                        {review.submittedDocumentName ? <div className="mt-1 text-muted-foreground">Evidence: {review.submittedDocumentName}</div> : null}
                        {review.notes ? <div className="mt-1 text-muted-foreground">{review.notes}</div> : null}
                      </div>
                    ))}
                  </div>
                ) : null}
                {expandedDeliverableHistoryId === item.id ? (
                  <div className="mt-4">
                    <WorkflowRecordPanel
                      entityType="ProjectDeliverable"
                      entityId={item.id}
                      entityLabel="Deliverable"
                      entityNumber={item.title}
                      status={item.status}
                      canSubmit={item.status === 'Draft' || item.status === 'Rejected'}
                      canApproveReject={item.status === 'PendingApproval'}
                      onSubmit={async () => { await projectService.submitDeliverable(item.id, { notes: 'Submitted from workspace' }); }}
                      onApprove={async (comments) => { await projectService.approveDeliverable(item.id, comments); }}
                      onReject={async (comments) => { await projectService.rejectDeliverable(item.id, comments || 'Rejected from workspace'); }}
                      onAfterAction={onLoad}
                      onOpenWorkflows={onOpenWorkflows}
                    />
                  </div>
                ) : null}
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Timesheets</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <div className="grid gap-2">
              <Label>User</Label>
              <Select value={timesheet.userId || 'none'} onValueChange={(value) => setTimesheet((current) => ({ ...current, userId: value === 'none' ? '' : value }))}>
                <SelectTrigger><SelectValue placeholder="Select user" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No user</SelectItem>
                  {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2"><Label>Work Item</Label><Select value={timesheet.workItemId || 'none'} onValueChange={(value) => setTimesheet((current) => ({ ...current, workItemId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Project level</SelectItem>{flat.map((item) => <SelectItem key={item.id} value={item.id}>{item.title}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Entry Date</Label><Input type="date" value={timesheet.entryDate ? String(timesheet.entryDate).slice(0, 10) : ''} onChange={(event) => setTimesheet((current) => ({ ...current, entryDate: event.target.value || undefined }))} /></div>
            <div className="grid gap-2"><Label>Hours</Label><Input type="number" value={timesheet.hours} onChange={(event) => setTimesheet((current) => ({ ...current, hours: Number(event.target.value || '0') }))} /></div>
            <div className="grid gap-2"><Label>Hourly Rate</Label><Input type="number" value={timesheet.hourlyRate ?? 0} onChange={(event) => setTimesheet((current) => ({ ...current, hourlyRate: Number(event.target.value || '0') }))} /></div>
            <div className="grid gap-2"><Label>Work Type</Label><Select value={timesheet.workType || timesheetWorkTypeOptions[0]} onValueChange={(value) => setTimesheet((current) => ({ ...current, workType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{timesheetWorkTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Billable</Label><Select value={boolValue(timesheet.isBillable)} onValueChange={(value) => setTimesheet((current) => ({ ...current, isBillable: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">Non-billable</SelectItem><SelectItem value="true">Billable</SelectItem></SelectContent></Select></div>
            <div className="grid gap-2 md:col-span-2"><Label>Notes</Label><Textarea rows={2} value={timesheet.notes || ''} onChange={(event) => setTimesheet((current) => ({ ...current, notes: event.target.value }))} /></div>
            <div className="flex items-end"><Button disabled={!timesheet.userId} onClick={onAddTimesheet}><Plus className="mr-2 h-4 w-4" />Add Entry</Button></div>
          </div>
          <div className="space-y-3">
            {project.timesheetEntries.length === 0 ? <div className="text-sm text-muted-foreground">No timesheet entries are available.</div> : null}
            {project.timesheetEntries.map((item) => <div key={item.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{getResolvedUserLabel(item.userId, item.userDisplayName)} | {item.hours}h</div><div className="text-sm text-muted-foreground">{formatDateLabel(item.entryDate)} | {item.workType} | {item.status} | cost {formatMoney(item.costAmount)}{item.approvedById ? ` | approved by ${getResolvedUserLabel(item.approvedById, item.approvedByDisplayName)}` : ''}</div></div>{item.status !== 'Approved' ? <Button variant="outline" size="sm" onClick={() => onApproveTimesheet(item.id)}>Approve</Button> : null}</div>)}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Expenses</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <div className="grid gap-2">
              <Label>User</Label>
              <Select value={expense.userId || 'none'} onValueChange={(value) => setExpense((current) => ({ ...current, userId: value === 'none' ? '' : value }))}>
                <SelectTrigger><SelectValue placeholder="Select user" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No user</SelectItem>
                  {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2"><Label>Work Item</Label><Select value={expense.workItemId || 'none'} onValueChange={(value) => setExpense((current) => ({ ...current, workItemId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Project level</SelectItem>{flat.map((item) => <SelectItem key={item.id} value={item.id}>{item.title}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Expense Date</Label><Input type="date" value={expense.expenseDate ? String(expense.expenseDate).slice(0, 10) : ''} onChange={(event) => setExpense((current) => ({ ...current, expenseDate: event.target.value || undefined }))} /></div>
            <div className="grid gap-2"><Label>Category</Label><Select value={expense.category || expenseCategoryOptions[0]} onValueChange={(value) => setExpense((current) => ({ ...current, category: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{expenseCategoryOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2">
              <Label>Currency</Label>
              <Select value={expense.currency || expenseCurrencyOptions[0] || baseCurrencyCode} onValueChange={(value) => setExpense((current) => ({ ...current, currency: value }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {expenseCurrencyOptions.map((code) => (
                    <SelectItem key={code} value={code}>
                      {getCurrencyOptionLabel(code)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2"><Label>Amount</Label><Input type="number" value={expense.amount} onChange={(event) => setExpense((current) => ({ ...current, amount: Number(event.target.value || '0') }))} /></div>
            <div className="grid gap-2"><Label>Tax Amount</Label><Input type="number" value={expense.taxAmount ?? 0} onChange={(event) => setExpense((current) => ({ ...current, taxAmount: Number(event.target.value || '0') }))} /></div>
            <div className="grid gap-2"><Label>Billable</Label><Select value={boolValue(expense.isBillable)} onValueChange={(value) => setExpense((current) => ({ ...current, isBillable: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">Non-billable</SelectItem><SelectItem value="true">Billable</SelectItem></SelectContent></Select></div>
            <div className="grid gap-2 md:col-span-3"><Label>Notes</Label><Textarea rows={2} value={expense.notes || ''} onChange={(event) => setExpense((current) => ({ ...current, notes: event.target.value }))} /></div>
            <div className="flex items-end"><Button disabled={!expense.userId} onClick={onAddExpense}><Plus className="mr-2 h-4 w-4" />Add Expense</Button></div>
          </div>
          <div className="space-y-3">
            {project.expenses.length === 0 ? <div className="text-sm text-muted-foreground">No expense entries are available.</div> : null}
            {project.expenses.map((item) => <div key={item.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{getResolvedUserLabel(item.userId, item.userDisplayName)} | {formatMoney(item.amount + item.taxAmount, item.currency)}</div><div className="text-sm text-muted-foreground">{formatDateLabel(item.expenseDate)} | {item.category} | {item.status}{item.approvedById ? ` | approved by ${getResolvedUserLabel(item.approvedById, item.approvedByDisplayName)}` : ''}</div></div>{item.status !== 'Approved' ? <Button variant="outline" size="sm" onClick={() => onApproveExpense(item.id)}>Approve</Button> : null}</div>)}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
