'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Loader2,
  Send,
  CheckCircle2,
  XCircle,
  Ban,
  Handshake,
  Undo2,
  PlayCircle,
  Download,
  Trash2,
  Plus,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { DocumentUploadField } from '@/components/hr/common/DocumentUploadField';
import { movementService } from '@/services/hr/movement.service';
import { useToast } from '@/hooks/use-toast';
import {
  ATTACHMENT_TYPES,
  CHECKLIST_CATEGORIES,
  IN_APPROVAL_STATUSES,
  type StaffMovement,
} from '@/types/hr/movements';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
const money = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2 });

type ActionKind = 'submit' | 'authorize' | 'reject' | 'cancel' | 'handover' | 'return' | null;

/**
 * A single movement: the before/after comparison, the workflow actions available in its current
 * status, and its approval chain, history, attachments and checklist.
 */
export default function MovementDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [action, setAction] = useState<ActionKind>(null);
  const [note, setNote] = useState('');
  const [returnDate, setReturnDate] = useState('');
  const [addingTask, setAddingTask] = useState(false);
  const [task, setTask] = useState({ taskDescription: '', category: 'HRTasks', isRequired: true, dueDate: '' });

  const { data: movement, isLoading } = useQuery({
    queryKey: ['hr', 'movement', id],
    queryFn: () => movementService.getById(id),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'movement', id] });

  const run = useMutation({
    mutationFn: async (kind: Exclude<ActionKind, null>) => {
      switch (kind) {
        case 'submit':
          return movementService.submit(id, note || undefined);
        case 'authorize':
          return movementService.authorize(id, note || undefined);
        case 'reject':
          return movementService.reject(id, note);
        case 'cancel':
          return movementService.cancel(id, note);
        case 'handover':
          return movementService.completeHandover(id, note || undefined);
        case 'return':
          return movementService.processReturn(id, new Date(returnDate).toISOString(), note || undefined);
      }
    },
    onSuccess: () => {
      toast({ title: 'Done' });
      setAction(null);
      setNote('');
      setReturnDate('');
      refresh();
    },
    onError: (error: any) =>
      toast({ title: 'Refused', description: error?.message ?? 'Unexpected error.', variant: 'destructive' }),
  });

  const implement = useMutation({
    mutationFn: () => movementService.implement(id),
    onSuccess: () => {
      toast({ title: 'Movement implemented' });
      refresh();
    },
    onError: (error: any) =>
      toast({ title: 'Refused', description: error?.message ?? 'Unexpected error.', variant: 'destructive' }),
  });

  const addTask = useMutation({
    mutationFn: () =>
      movementService.addChecklistItem(id, {
        taskDescription: task.taskDescription,
        category: task.category,
        isRequired: task.isRequired,
        dueDate: task.dueDate ? new Date(task.dueDate).toISOString() : null,
        displayOrder: (movement?.checklistItems?.length ?? 0) + 1,
      }),
    onSuccess: () => {
      setAddingTask(false);
      setTask({ taskDescription: '', category: 'HRTasks', isRequired: true, dueDate: '' });
      refresh();
    },
    onError: (error: any) =>
      toast({ title: 'Could not add the task', description: error?.message, variant: 'destructive' }),
  });

  const completeTask = useMutation({
    mutationFn: (itemId: string) => movementService.completeChecklistItem(itemId),
    onSuccess: refresh,
    onError: (error: any) =>
      toast({ title: 'Refused', description: error?.message, variant: 'destructive' }),
  });

  const removeMovement = useMutation({
    mutationFn: () => movementService.delete(id),
    onSuccess: () => {
      toast({ title: 'Movement deleted' });
      router.push('/hr/movements');
    },
    onError: (error: any) =>
      toast({ title: 'Refused', description: error?.message, variant: 'destructive' }),
  });

  if (isLoading || !movement) {
    return (
      <div className="flex items-center justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const m: StaffMovement = movement;
  const isDraft = m.status === 'Draft';
  const inApproval = IN_APPROVAL_STATUSES.includes(m.status);
  const isApproved = m.status === 'Approved';
  const isTerminal = ['Rejected', 'Implemented', 'Cancelled'].includes(m.status);

  const outstandingRequired = (m.checklistItems ?? []).filter((c) => c.isRequired && !c.isCompleted);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={m.movementNumber}
        description={`${m.movementTypeName} — ${m.employeeName}`}
        backHref="/hr/movements"
        actions={
          <div className="flex flex-wrap gap-2">
            {isDraft && (
              <Button onClick={() => setAction('submit')}>
                <Send className="mr-2 h-4 w-4" />
                Submit for approval
              </Button>
            )}
            {inApproval && (
              <Button onClick={() => setAction('authorize')}>
                <CheckCircle2 className="mr-2 h-4 w-4" />
                Authorise
              </Button>
            )}
            {!isTerminal && !isApproved && (
              <Button variant="outline" onClick={() => setAction('reject')}>
                <XCircle className="mr-2 h-4 w-4" />
                Reject
              </Button>
            )}
            {m.requiresHandover && isApproved && !m.handoverCompletionDate && (
              <Button variant="outline" onClick={() => setAction('handover')}>
                <Handshake className="mr-2 h-4 w-4" />
                Complete handover
              </Button>
            )}
            {isApproved && (
              <Button onClick={() => implement.mutate()} disabled={implement.isPending}>
                <PlayCircle className="mr-2 h-4 w-4" />
                Implement
              </Button>
            )}
            {m.isTemporary && !m.returnProcessed && (
              <Button variant="outline" onClick={() => setAction('return')}>
                <Undo2 className="mr-2 h-4 w-4" />
                Process return
              </Button>
            )}
            {m.status !== 'Implemented' && (
              <Button variant="outline" onClick={() => setAction('cancel')}>
                <Ban className="mr-2 h-4 w-4" />
                Cancel
              </Button>
            )}
            {isDraft && (
              <Button variant="ghost" onClick={() => removeMovement.mutate()}>
                <Trash2 className="mr-2 h-4 w-4" />
                Delete
              </Button>
            )}
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <StatusBadge status={m.status} />
        {m.isTemporary && <Badge variant="outline">Temporary until {fmtDate(m.temporaryEndDate)}</Badge>}
        {m.requiresEmployeeAcceptance && (
          <Badge variant={m.employeeAccepted ? 'default' : 'outline'}>
            {m.employeeAccepted === true
              ? 'Accepted by employee'
              : m.employeeAccepted === false
                ? 'Declined by employee'
                : 'Awaiting the employee'}
          </Badge>
        )}
        {m.returnProcessed && <Badge variant="outline">Return processed</Badge>}
      </div>

      {isApproved && outstandingRequired.length > 0 && (
        <Card className="border-amber-500/40 bg-amber-50/50 dark:bg-amber-950/20">
          <CardContent className="p-4 text-sm">
            {outstandingRequired.length} required checklist{' '}
            {outstandingRequired.length === 1 ? 'task is' : 'tasks are'} still outstanding.
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="approvals">Approvals ({m.approvalLevels?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="checklist">Checklist ({m.checklistItems?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="attachments">Documents ({m.attachments?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="history">History ({m.statusHistory?.length ?? 0})</TabsTrigger>
        </TabsList>

        {/* ── Overview ─────────────────────────────────────────────────────── */}
        <TabsContent value="overview" className="space-y-4">
          <div className="grid gap-4 lg:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Before</CardTitle>
              </CardHeader>
              <CardContent>
                <dl className="space-y-2 text-sm">
                  <Row label="Position" value={m.currentPositionTitle} />
                  <Row label="Organisation unit" value={m.currentOrganizationUnitName} />
                  <Row label="Location" value={m.currentLocationName} />
                  <Row label="Reports to" value={m.currentSupervisorName} />
                  <Row label="Salary grade" value={m.currentSalaryGradeName} />
                  <Row label="Salary" value={money(m.currentSalary)} />
                </dl>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-base">After</CardTitle>
              </CardHeader>
              <CardContent>
                <dl className="space-y-2 text-sm">
                  <Row label="Position" value={m.newPositionTitle} />
                  <Row label="Organisation unit" value={m.newOrganizationUnitName} />
                  <Row label="Location" value={m.newLocationName} />
                  <Row label="Reports to" value={m.newSupervisorName} />
                  <Row label="Salary grade" value={m.newSalaryGradeName} />
                  <Row
                    label="Salary"
                    value={
                      <span>
                        {money(m.newSalary)}
                        {m.salaryIncreasePercentage != null && (
                          <span className="ml-2 text-xs text-muted-foreground">
                            {m.salaryIncreasePercentage > 0 ? '+' : ''}
                            {m.salaryIncreasePercentage}%
                          </span>
                        )}
                      </span>
                    }
                  />
                </dl>
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Request</CardTitle>
            </CardHeader>
            <CardContent>
              <dl className="grid gap-3 text-sm sm:grid-cols-2">
                <Row label="Reason" value={m.reason} />
                <Row label="Justification" value={m.justification} />
                <Row label="Category" value={m.categoryName} />
                <Row label="Effective date" value={fmtDate(m.effectiveDate)} />
                <Row label="Raised by" value={m.requestedByName} />
                <Row label="Raised on" value={fmtDateTime(m.requestSubmissionDate)} />
                {m.authorizationDate && <Row label="Authorised by" value={m.authorizedByName} />}
                {m.rejectionDate && <Row label="Rejected" value={m.rejectionReason} />}
                {m.cancellationDate && <Row label="Cancelled" value={m.cancellationReason} />}
                {m.handoverCompletionDate && (
                  <Row label="Handover completed" value={fmtDateTime(m.handoverCompletionDate)} />
                )}
                {m.additionalNotes && <Row label="Notes" value={m.additionalNotes} />}
              </dl>
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Approvals ────────────────────────────────────────────────────── */}
        <TabsContent value="approvals">
          <Card>
            <CardContent className="p-0">
              {(m.approvalLevels ?? []).length === 0 ? (
                <EmptyState
                  title="No approval chain"
                  description="A movement with no approval levels cannot be authorised — its chain has to be built first."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Level</TableHead>
                      <TableHead>Role</TableHead>
                      <TableHead>Approver</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Actioned</TableHead>
                      <TableHead>Comments</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(m.approvalLevels ?? [])
                      .slice()
                      .sort((a, b) => a.level - b.level)
                      .map((l) => (
                        <TableRow key={l.id}>
                          <TableCell>{l.level}</TableCell>
                          <TableCell>{l.roleName}</TableCell>
                          <TableCell>
                            {l.approverName}
                            {l.delegatedToName && (
                              <div className="text-xs text-muted-foreground">
                                Delegated to {l.delegatedToName}
                              </div>
                            )}
                          </TableCell>
                          <TableCell>
                            <StatusBadge status={l.status} />
                          </TableCell>
                          <TableCell>{fmtDateTime(l.actionDate)}</TableCell>
                          <TableCell className="max-w-xs truncate">{l.comments ?? '—'}</TableCell>
                        </TableRow>
                      ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Checklist ────────────────────────────────────────────────────── */}
        <TabsContent value="checklist" className="space-y-4">
          <div className="flex justify-end">
            <Button size="sm" onClick={() => setAddingTask(true)}>
              <Plus className="mr-2 h-4 w-4" />
              Add task
            </Button>
          </div>
          <Card>
            <CardContent className="p-0">
              {(m.checklistItems ?? []).length === 0 ? (
                <EmptyState title="No tasks" description="Nothing has been added to this movement's checklist." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Task</TableHead>
                      <TableHead>Category</TableHead>
                      <TableHead>Responsible</TableHead>
                      <TableHead>Due</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(m.checklistItems ?? []).map((c) => (
                      <TableRow key={c.id}>
                        <TableCell>
                          {c.taskDescription}
                          {c.isRequired && <Badge variant="outline" className="ml-2">Required</Badge>}
                        </TableCell>
                        <TableCell>{c.category}</TableCell>
                        <TableCell>{c.responsiblePersonName ?? 'Unassigned'}</TableCell>
                        <TableCell>{fmtDate(c.dueDate)}</TableCell>
                        <TableCell>
                          {c.isCompleted ? (
                            <Badge>Complete</Badge>
                          ) : (
                            <Badge variant="outline">Outstanding</Badge>
                          )}
                        </TableCell>
                        <TableCell className="text-right">
                          {!c.isCompleted && (
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => completeTask.mutate(c.id)}
                            >
                              Mark complete
                            </Button>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
          <p className="text-xs text-muted-foreground">
            A task with a named responsible person can only be completed by that person — HR can close
            one out when its owner has moved on.
          </p>
        </TabsContent>

        {/* ── Attachments ──────────────────────────────────────────────────── */}
        <TabsContent value="attachments" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Attach a document</CardTitle>
            </CardHeader>
            <CardContent>
              <DocumentUploadField
                label="Supporting document"
                endpoint={`/staff-movements/${id}/attachments/upload`}
                fields={{ attachmentType: 'Justification' }}
                helpText="Approval letters, performance reviews, justifications and job descriptions. Scanned and registered in the document repository on upload."
                onUploaded={refresh}
              />
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-0">
              {(m.attachments ?? []).length === 0 ? (
                <EmptyState title="No documents" description="Nothing has been attached to this movement." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>File</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Uploaded by</TableHead>
                      <TableHead>When</TableHead>
                      <TableHead />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(m.attachments ?? []).map((a) => (
                      <TableRow key={a.id}>
                        <TableCell>{a.fileName}</TableCell>
                        <TableCell>{a.type}</TableCell>
                        <TableCell>{a.uploadedByName}</TableCell>
                        <TableCell>{fmtDateTime(a.uploadDate)}</TableCell>
                        <TableCell className="text-right">
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => movementService.downloadAttachment(a.id, a.fileName)}
                          >
                            <Download className="mr-2 h-4 w-4" />
                            Download
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── History ──────────────────────────────────────────────────────── */}
        <TabsContent value="history">
          <Card>
            <CardContent className="p-0">
              {(m.statusHistory ?? []).length === 0 ? (
                <EmptyState title="No history" description="No status changes have been recorded." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>When</TableHead>
                      <TableHead>From</TableHead>
                      <TableHead>To</TableHead>
                      <TableHead>By</TableHead>
                      <TableHead>Reason</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(m.statusHistory ?? []).map((h) => (
                      <TableRow key={h.id}>
                        <TableCell>{fmtDateTime(h.changedDate)}</TableCell>
                        <TableCell>{h.fromStatus}</TableCell>
                        <TableCell>{h.toStatus}</TableCell>
                        <TableCell>{h.changedByName}</TableCell>
                        <TableCell className="max-w-xs truncate">{h.reason ?? '—'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* ── Action dialog ───────────────────────────────────────────────────── */}
      <Dialog open={action !== null} onOpenChange={(open) => !open && setAction(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {action === 'submit' && 'Submit for approval'}
              {action === 'authorize' && 'Authorise this movement'}
              {action === 'reject' && 'Reject this movement'}
              {action === 'cancel' && 'Cancel this movement'}
              {action === 'handover' && 'Record handover'}
              {action === 'return' && 'Process return from the temporary assignment'}
            </DialogTitle>
            <DialogDescription>
              {action === 'authorize' &&
                'Every approval level must be cleared first, and a movement with no approval chain cannot be authorised at all.'}
              {action === 'reject' && 'The reason is recorded on the movement and in its history.'}
              {action === 'cancel' && 'The reason is recorded on the movement and in its history.'}
            </DialogDescription>
          </DialogHeader>

          {action === 'return' && (
            <div className="space-y-2">
              <Label htmlFor="returnDate">Actual return date</Label>
              <Input
                id="returnDate"
                type="date"
                value={returnDate}
                onChange={(e) => setReturnDate(e.target.value)}
              />
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="note">
              {action === 'reject' || action === 'cancel' ? 'Reason' : 'Notes'}
              {(action === 'reject' || action === 'cancel') && (
                <span className="ml-0.5 text-red-500">*</span>
              )}
            </Label>
            <Textarea id="note" rows={3} value={note} onChange={(e) => setNote(e.target.value)} />
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setAction(null)}>
              Cancel
            </Button>
            <Button
              onClick={() => action && run.mutate(action)}
              disabled={
                run.isPending ||
                ((action === 'reject' || action === 'cancel') && !note.trim()) ||
                (action === 'return' && !returnDate)
              }
            >
              {run.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Add-task dialog ─────────────────────────────────────────────────── */}
      <Dialog open={addingTask} onOpenChange={setAddingTask}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add a checklist task</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="taskDescription">Task</Label>
              <Input
                id="taskDescription"
                value={task.taskDescription}
                onChange={(e) => setTask({ ...task, taskDescription: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label>Category</Label>
              <Select value={task.category} onValueChange={(v) => setTask({ ...task, category: v })}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {CHECKLIST_CATEGORIES.map((c) => (
                    <SelectItem key={c} value={c}>
                      {c.replace(/([A-Z])/g, ' $1').trim()}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="dueDate">Due</Label>
              <Input
                id="dueDate"
                type="date"
                value={task.dueDate}
                onChange={(e) => setTask({ ...task, dueDate: e.target.value })}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddingTask(false)}>
              Cancel
            </Button>
            <Button onClick={() => addTask.mutate()} disabled={!task.taskDescription.trim() || addTask.isPending}>
              {addTask.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex justify-between gap-4">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="text-right">{value || '—'}</dd>
    </div>
  );
}
