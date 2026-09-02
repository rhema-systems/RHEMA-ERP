'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { CheckCircle2, ClipboardList, MessageSquare, RefreshCw, RotateCcw, Send, ShieldX } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { projectService, type ProjectDto } from '@/services/projectService';
import { civilEngineeringDirectTaskService } from '@/services/civil-engineering-direct-task.service';
import type { CivilEngineeringDirectTask, CivilEngineeringDirectTaskFeedback, CivilEngineeringDirectTaskFeedbackAction, CivilEngineeringDirectTaskFeedbackLookups, CivilEngineeringDirectTaskLookups, CivilEngineeringUrgency } from '@/types/civil-engineering-direct-task';

const managePermission = 'civil-engineering.assignments.manage';
const assignedWorkPermission = 'civil-engineering.assigned-work.manage';
const readPermission = 'civil-engineering.workspace.read';
const none = '__select__';
const urgencyLabels: Record<CivilEngineeringUrgency, string> = { Routine: 'Routine', Priority: 'Priority', Urgent: 'Urgent', Emergency: 'Emergency' };
const formatDate = (value: string) => new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
const toLocalDateTimeValue = (value: Date) => new Date(value.getTime() - value.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
const errorText = (error: unknown, fallback: string) => {
  const value = error as { response?: { detail?: string; correlationId?: string }; message?: string };
  return `${value.response?.detail || value.message || fallback}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};

export default function CivilEngineeringDirectTasksPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canManage = hasPermission(managePermission);
  const canManageAssignedWork = hasPermission(assignedWorkPermission);
  const canRead = hasPermission(readPermission) || canManage;
  const [projects, setProjects] = useState<ProjectDto[]>([]);
  const [projectId, setProjectId] = useState('');
  const [lookups, setLookups] = useState<CivilEngineeringDirectTaskLookups>();
  const [tasks, setTasks] = useState<CivilEngineeringDirectTask[]>([]);
  const [activeTaskId, setActiveTaskId] = useState('');
  const [feedbackLookups, setFeedbackLookups] = useState<CivilEngineeringDirectTaskFeedbackLookups>();
  const [feedbackHistory, setFeedbackHistory] = useState<CivilEngineeringDirectTaskFeedback[]>([]);
  const [feedbackAction, setFeedbackAction] = useState<CivilEngineeringDirectTaskFeedbackAction | ''>('');
  const [feedbackMessage, setFeedbackMessage] = useState('');
  const [feedbackProgress, setFeedbackProgress] = useState('');
  const [feedbackDocumentVersionId, setFeedbackDocumentVersionId] = useState(none);
  const [assigneeValue, setAssigneeValue] = useState(none);
  const [urgency, setUrgency] = useState<CivilEngineeringUrgency | ''>('');
  const [documentVersionId, setDocumentVersionId] = useState(none);
  const [title, setTitle] = useState('');
  const [instructions, setInstructions] = useState('');
  const [dueDate, setDueDate] = useState('');
  const [urgencyReason, setUrgencyReason] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const selectedAssignee = useMemo(() => lookups?.assignees.find((item) => `${item.userId}:${item.roleId}` === assigneeValue), [assigneeValue, lookups]);
  const selectedDocument = useMemo(() => lookups?.documents.find((item) => item.centralDocumentVersionId === documentVersionId), [documentVersionId, lookups]);
  const selectedFeedbackDocument = useMemo(() => feedbackLookups?.documents.find((item) => item.centralDocumentVersionId === feedbackDocumentVersionId), [feedbackDocumentVersionId, feedbackLookups]);
  const isUrgent = urgency === 'Urgent' || urgency === 'Emergency';

  const resetDraft = () => { setAssigneeValue(none); setUrgency(''); setDocumentVersionId(none); setTitle(''); setInstructions(''); setDueDate(''); setUrgencyReason(''); };
  const changeUrgency = (value: string) => {
    const next = value === none ? '' : value as CivilEngineeringUrgency;
    setUrgency(next);
    if ((next === 'Urgent' || next === 'Emergency') && !dueDate && lookups?.urgentResponseHours)
      setDueDate(toLocalDateTimeValue(new Date(Date.now() + lookups.urgentResponseHours * 3_600_000)));
  };
  const load = useCallback(async (selectedId?: string) => {
    if (!canRead) { setLoading(false); return; }
    setLoading(true);
    try {
      const result = await projectService.getProjects({ page: 1, pageSize: 250 });
      setProjects(result.items);
      const id = selectedId || projectId || result.items[0]?.id || '';
      setProjectId(id);
      if (!id) { setTasks([]); setLookups(undefined); return; }
      const [history, assignmentLookups] = await Promise.all([
        civilEngineeringDirectTaskService.list(id),
        canManage ? civilEngineeringDirectTaskService.lookups(id) : Promise.resolve(undefined),
      ]);
      setTasks(history); setLookups(assignmentLookups);
    } catch (error) {
      toast({ variant: 'destructive', title: 'Civil task board could not be loaded', description: errorText(error, 'Refresh and try again.') });
    } finally { setLoading(false); }
  }, [canManage, canRead, projectId, toast]);

  useEffect(() => { void load(); }, [load]);

  const resetFeedback = () => { setFeedbackAction(''); setFeedbackMessage(''); setFeedbackProgress(''); setFeedbackDocumentVersionId(none); };
  const openFeedback = async (task: CivilEngineeringDirectTask) => {
    if (!projectId || !canManageAssignedWork) return;
    if (activeTaskId === task.id) { setActiveTaskId(''); setFeedbackLookups(undefined); setFeedbackHistory([]); resetFeedback(); return; }
    setSaving(true);
    try {
      const [available, history] = await Promise.all([
        civilEngineeringDirectTaskService.feedbackLookups(projectId, task.id),
        civilEngineeringDirectTaskService.feedback(projectId, task.id),
      ]);
      setActiveTaskId(task.id); setFeedbackLookups(available); setFeedbackHistory(history); resetFeedback();
    } catch (error) {
      toast({ variant: 'destructive', title: 'Task feedback could not be opened', description: errorText(error, 'You must be the assigned worker or the independent reviewer for this task.') });
    } finally { setSaving(false); }
  };

  const submitFeedback = async (task: CivilEngineeringDirectTask) => {
    if (!projectId || !feedbackAction) return;
    if ((feedbackAction === 'UpdateProgress' && (!feedbackProgress || Number(feedbackProgress) >= 100)) || ((feedbackAction === 'UpdateProgress' || feedbackAction === 'Complete' || feedbackAction === 'Return') && !feedbackMessage.trim())) {
      toast({ variant: 'destructive', title: 'Complete the feedback', description: feedbackAction === 'UpdateProgress' ? 'Provide a progress update from 0 to 99.99 and a short progress note.' : 'Provide the required feedback note before submitting.' });
      return;
    }
    if (feedbackAction === 'Complete' && feedbackLookups?.requireFeedbackEvidence && !selectedFeedbackDocument) {
      toast({ variant: 'destructive', title: 'Completion evidence is required', description: 'Select a current Published central-DMS evidence file configured for this direct-task policy.' });
      return;
    }
    setSaving(true);
    const request = {
      clientRequestId: crypto.randomUUID(), action: feedbackAction, message: feedbackMessage.trim() || undefined,
      progressPercent: feedbackAction === 'UpdateProgress' ? Number(feedbackProgress) : undefined,
      centralDocumentRecordId: selectedFeedbackDocument?.centralDocumentRecordId,
      centralDocumentVersionId: selectedFeedbackDocument?.centralDocumentVersionId,
      rowVersion: task.rowVersion,
    };
    try {
      const saved = feedbackAction === 'Accept' || feedbackAction === 'Return'
        ? await civilEngineeringDirectTaskService.submitReviewFeedback(projectId, task.id, request)
        : await civilEngineeringDirectTaskService.submitAssigneeFeedback(projectId, task.id, request);
      setTasks((current) => current.map((item) => item.id === saved.id ? saved : item));
      setFeedbackHistory((current) => [...current, {
        id: request.clientRequestId, sequence: current.length + 1, action: feedbackAction, progressPercent: request.progressPercent,
        message: request.message || null, actorName: 'You', documentReference: selectedFeedbackDocument?.documentReference || null,
        workflowOutcome: null, correlationId: '', createdAt: new Date().toISOString(),
      }]);
      resetFeedback();
      toast({ title: 'Civil task feedback saved', description: saved.status === 'PendingAcceptance' ? 'The completion has been sent to the independent acceptance workflow.' : 'The governed task lifecycle and audit trail have been updated.' });
    } catch (error) {
      toast({ variant: 'destructive', title: 'Civil task feedback was not saved', description: errorText(error, 'Refresh the task and try again.') });
    } finally { setSaving(false); }
  };

  const create = async () => {
    if (!projectId || !selectedAssignee || !urgency || !title.trim() || !instructions.trim() || (lookups?.requireDueDate && !dueDate) || (isUrgent && (!dueDate || urgencyReason.trim().length < 5))) {
      toast({ variant: 'destructive', title: 'Complete the Civil task assignment', description: 'Select a project assignee and configured urgency, then provide the task title, instructions and required due date.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringDirectTaskService.create(projectId, {
        clientRequestId: crypto.randomUUID(), title: title.trim(), instructions: instructions.trim(), assignedToUserId: selectedAssignee.userId,
        assignedRoleId: selectedAssignee.roleId, urgency, urgencyReason: isUrgent ? urgencyReason.trim() : undefined,
        dueDate: dueDate ? (isUrgent ? new Date(dueDate).toISOString() : new Date(`${dueDate}T17:00:00`).toISOString()) : undefined,
        centralDocumentRecordId: selectedDocument?.centralDocumentRecordId, centralDocumentVersionId: selectedDocument?.centralDocumentVersionId,
      });
      resetDraft();
      toast({ title: 'Civil task assigned', description: 'The task is now a governed Projects work item. The assigned worker can acknowledge, report progress and submit closure from its controlled feedback panel.' });
      await load(projectId);
    } catch (error) {
      toast({ variant: 'destructive', title: 'Civil task was not assigned', description: errorText(error, 'Confirm that you are an assigned SCE or Civil Engineer and the selected recipient is an active project member with a configured role.') });
    } finally { setSaving(false); }
  };

  const escalateUrgent = async () => {
    if (!projectId) return;
    setSaving(true);
    try {
      const saved = await civilEngineeringDirectTaskService.escalateUrgent(projectId, { clientRequestId: crypto.randomUUID() });
      if (!saved.length) {
        toast({ title: 'No overdue urgent tasks', description: 'There are no un-escalated urgent Civil tasks past their configured response deadline.' });
        return;
      }
      setTasks((current) => current.map((task) => saved.find((item) => item.id === task.id) || task));
      toast({ title: 'Urgent tasks escalated', description: `${saved.length} overdue task${saved.length === 1 ? '' : 's'} was escalated to the configured independent project roles.` });
    } catch (error) {
      toast({ variant: 'destructive', title: 'Urgent-task escalation was not sent', description: errorText(error, 'Refresh and verify the project escalation roles in CIV-CFG-010.') });
    } finally { setSaving(false); }
  };

  if (!canRead) return <Alert><ShieldX className="h-4 w-4" /><AlertTitle>Civil assignment access required</AlertTitle><AlertDescription>You do not have access to Civil task assignments.</AlertDescription></Alert>;

  return <div className="space-y-5" data-testid="civil-direct-tasks-page">
    <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><h1 className="flex items-center gap-2 text-2xl font-semibold"><ClipboardList className="h-6 w-6" />Civil task assignments</h1><p className="mt-1 text-sm text-muted-foreground">Controlled direct assignment over the existing Projects task register. All assignees, due dates and DMS files are tenant-scoped selectors.</p></div><div className="flex flex-wrap gap-2"><Button variant="outline" size="sm" onClick={() => void load(projectId)} disabled={loading || saving}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>{canManage && projectId ? <Button variant="outline" size="sm" onClick={() => void escalateUrgent()} disabled={loading || saving}>Escalate overdue urgent tasks</Button> : null}</div></div>
    <Card><CardHeader><CardTitle className="text-base">Project task board</CardTitle></CardHeader><CardContent><Select value={projectId || none} onValueChange={(value) => { resetDraft(); void load(value === none ? '' : value); }}><SelectTrigger className="max-w-2xl" data-testid="civil-task-project-select"><SelectValue placeholder="Select project" /></SelectTrigger><SelectContent><SelectItem value={none} disabled>Select project</SelectItem>{projects.map((item) => <SelectItem key={item.id} value={item.id}>{item.projectCode} · {item.title}</SelectItem>)}</SelectContent></Select></CardContent></Card>
    {canManage && projectId && lookups ? <Card><CardHeader><CardTitle className="text-base">Assign Civil work</CardTitle><CardDescription>Create one governed Projects work item. A task cannot later be changed through the generic Projects editor; all feedback and independent closure acceptance use the controlled lifecycle below.</CardDescription></CardHeader><CardContent className="space-y-4"><div className="grid gap-3 md:grid-cols-3"><div className="space-y-1"><Label>Project assignee</Label><Select value={assigneeValue} onValueChange={setAssigneeValue}><SelectTrigger data-testid="civil-task-assignee-select"><SelectValue placeholder="Select assignee" /></SelectTrigger><SelectContent><SelectItem value={none} disabled>Select assignee</SelectItem>{lookups.assignees.map((item) => <SelectItem key={`${item.userId}:${item.roleId}`} value={`${item.userId}:${item.roleId}`}>{item.displayName} · {item.roleName}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Urgency</Label><Select value={urgency || none} onValueChange={changeUrgency}><SelectTrigger data-testid="civil-task-urgency-select"><SelectValue placeholder="Select urgency" /></SelectTrigger><SelectContent><SelectItem value={none} disabled>Select urgency</SelectItem>{lookups.urgencies.map((item) => <SelectItem key={item} value={item}>{urgencyLabels[item]}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>{isUrgent ? `SLA response deadline (within ${lookups.urgentResponseHours} hours)` : `Due date${lookups.requireDueDate ? '' : ' (optional)'}`}</Label><Input data-testid="civil-task-due-date" type={isUrgent ? 'datetime-local' : 'date'} value={dueDate} onChange={(event) => setDueDate(event.target.value)} /></div></div>{isUrgent ? <div className="space-y-1"><Label>Urgent reason</Label><Textarea value={urgencyReason} maxLength={1000} rows={2} placeholder="State why this task requires the urgent path and response SLA." onChange={(event) => setUrgencyReason(event.target.value)} /><p className="text-right text-xs text-muted-foreground">{urgencyReason.length}/1000</p></div> : null}<div className="grid gap-3 md:grid-cols-2"><div className="space-y-1"><Label>Task title</Label><Input data-testid="civil-task-title" value={title} maxLength={200} placeholder="e.g. Verify reinforcement before concrete pour" onChange={(event) => setTitle(event.target.value)} /></div><div className="space-y-1"><Label>Supporting file (optional)</Label><Select value={documentVersionId} onValueChange={setDocumentVersionId}><SelectTrigger><SelectValue placeholder="Select current Published DMS file" /></SelectTrigger><SelectContent><SelectItem value={none}>No supporting file</SelectItem>{lookups.documents.map((item) => <SelectItem key={item.centralDocumentVersionId} value={item.centralDocumentVersionId}>{item.documentReference} · {item.versionNumber} · {item.title}</SelectItem>)}</SelectContent></Select></div></div><div className="space-y-1"><Label>Instructions</Label><Textarea data-testid="civil-task-instructions" value={instructions} maxLength={4000} rows={5} placeholder="State the technical instruction, expected output and any site constraints." onChange={(event) => setInstructions(event.target.value)} /><p className="text-right text-xs text-muted-foreground">{instructions.length}/4000</p></div><div className="flex justify-end"><Button data-testid="civil-task-assign" disabled={saving || !lookups.assignees.length} onClick={() => void create()}><Send className="mr-2 h-4 w-4" />Assign task</Button></div></CardContent></Card> : null}
    <Card>
      <CardHeader><CardTitle className="text-base">Assigned work</CardTitle></CardHeader>
      <CardContent className="space-y-3">
        {loading ? <p className="text-sm text-muted-foreground">Loading direct tasks…</p> : null}
        {!loading && projectId && !tasks.length ? <p className="rounded border border-dashed p-3 text-sm text-muted-foreground">No governed Civil tasks have been assigned to this project.</p> : null}
        {tasks.map((item) => <article key={item.id} className="rounded-lg border p-4">
          <div className="flex flex-col gap-2 md:flex-row md:justify-between">
            <div>
              <div className="flex flex-wrap items-center gap-2"><Badge variant="outline">{urgencyLabels[item.urgency]}</Badge><Badge variant="secondary">{item.status}</Badge><Badge variant="outline">{item.progressPercent}%</Badge><span className="font-medium">{item.title}</span></div>
              <p className="mt-2 text-sm">{item.assignedToName} · {item.assignedRoleName}</p>
              <p className="mt-2 whitespace-pre-wrap break-words text-sm text-muted-foreground">{item.instructions}</p>
              {item.isUrgentPath ? <div className="mt-2 rounded border border-amber-300/70 bg-amber-50/40 p-2 text-sm dark:bg-amber-950/20"><p><span className="font-medium">Urgent reason:</span> {item.urgencyReason}</p><p className="mt-1 text-xs text-muted-foreground">SLA response deadline: {item.urgentResponseDueAt ? formatDate(item.urgentResponseDueAt) : 'Unavailable'}{item.urgentEscalatedAt ? ` · Escalated ${formatDate(item.urgentEscalatedAt)}` : ''}</p></div> : null}
              {item.documentReference ? <p className="mt-2 text-xs text-muted-foreground">DMS supporting file: {item.documentReference}</p> : null}
            </div>
            <div className="flex flex-col items-start gap-2 md:items-end"><p className="text-xs text-muted-foreground">Due {formatDate(item.dueDate)}</p>{canManageAssignedWork ? <Button variant="outline" size="sm" disabled={saving} onClick={() => void openFeedback(item)}><MessageSquare className="mr-2 h-4 w-4" />{activeTaskId === item.id ? 'Close feedback' : 'Feedback'}</Button> : null}</div>
          </div>
          {activeTaskId === item.id && feedbackLookups ? <div className="mt-4 space-y-4 border-t pt-4">
            <div className="grid gap-3 md:grid-cols-3">
              <div className="space-y-1"><Label>Controlled action</Label><Select value={feedbackAction || none} onValueChange={(value) => setFeedbackAction(value === none ? '' : value as CivilEngineeringDirectTaskFeedbackAction)}><SelectTrigger data-testid="civil-task-feedback-action"><SelectValue placeholder="Select action" /></SelectTrigger><SelectContent><SelectItem value={none} disabled>Select action</SelectItem>{feedbackLookups.availableActions.map((action) => <SelectItem key={action} value={action}>{action === 'UpdateProgress' ? 'Update progress' : action}</SelectItem>)}</SelectContent></Select></div>
              {feedbackAction === 'UpdateProgress' ? <div className="space-y-1"><Label>Progress (%)</Label><Input type="number" min="0" max="99.99" step="0.01" value={feedbackProgress} onChange={(event) => setFeedbackProgress(event.target.value)} placeholder="e.g. 65" /></div> : null}
              <div className="space-y-1"><Label>Central-DMS evidence{feedbackAction === 'Complete' && feedbackLookups.requireFeedbackEvidence ? '' : ' (optional)'}</Label><Select value={feedbackDocumentVersionId} onValueChange={setFeedbackDocumentVersionId}><SelectTrigger data-testid="civil-task-feedback-evidence"><SelectValue placeholder="Select current Published file" /></SelectTrigger><SelectContent><SelectItem value={none}>No evidence file</SelectItem>{feedbackLookups.documents.map((document) => <SelectItem key={document.centralDocumentVersionId} value={document.centralDocumentVersionId}>{document.documentReference} · {document.title}</SelectItem>)}</SelectContent></Select></div>
            </div>
            <div className="space-y-1"><Label>{feedbackAction === 'Return' ? 'Return reason' : feedbackAction === 'Complete' ? 'Completion summary' : feedbackAction === 'UpdateProgress' ? 'Progress update' : 'Note (optional)'}</Label><Textarea data-testid="civil-task-feedback-message" value={feedbackMessage} maxLength={2000} rows={3} onChange={(event) => setFeedbackMessage(event.target.value)} placeholder="Record an accurate, concise update for the immutable audit trail." /></div>
            <div className="flex justify-end"><Button data-testid="civil-task-save-feedback" disabled={saving || !feedbackAction} onClick={() => void submitFeedback(item)}>{feedbackAction === 'Return' ? <RotateCcw className="mr-2 h-4 w-4" /> : <CheckCircle2 className="mr-2 h-4 w-4" />}{feedbackAction === 'Return' ? 'Return task' : 'Save feedback'}</Button></div>
            <div className="space-y-2"><p className="text-sm font-medium">Feedback history</p>{feedbackHistory.length ? feedbackHistory.map((entry) => <div key={entry.id} className="rounded border p-3 text-sm"><div className="flex flex-wrap items-center gap-2"><Badge variant="outline">{entry.action}</Badge>{entry.progressPercent != null ? <span>{entry.progressPercent}%</span> : null}<span className="text-muted-foreground">{entry.actorName} · {formatDate(entry.createdAt)}</span></div>{entry.message ? <p className="mt-2 whitespace-pre-wrap break-words text-muted-foreground">{entry.message}</p> : null}{entry.documentReference ? <p className="mt-1 text-xs text-muted-foreground">DMS evidence: {entry.documentReference}</p> : null}</div>) : <p className="text-sm text-muted-foreground">No feedback has been recorded for this task.</p>}</div>
          </div> : null}
        </article>)}
      </CardContent>
    </Card>
  </div>;
}
