'use client';

import { useEffect, useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import {
  DEFAULT_PROJECT_CURRENCY,
  formatProjectMoney,
  loadProjectCurrencyContext,
  type ProjectCurrencyReference,
} from '@/lib/project-currency';
import { CreateProjectTimesheetEntryDto, ProjectApprovalQueueSummaryDto, ProjectCatalogEntryDto, ProjectDetailDto, ProjectLookupDto, ProjectTimesheetApprovalQueueItemDto, ProjectTimesheetEntryDto, projectService } from '@/services/projectService';
import { userService } from '@/services/user';
import type { User } from '@/types';
import { toast } from 'sonner';

const today = () => new Date().toISOString().slice(0, 10);
const DEFAULT_TIMESHEET_WORK_TYPES = ['Standard', 'Overtime', 'Travel', 'Support', 'BillableDelivery', 'Admin'];

const formatCatalogLabel = (value: string) =>
  value
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/([A-Z])([A-Z][a-z])/g, '$1 $2')
    .replace(/[-_]/g, ' ');

const resolveCatalogOptions = (entries: ProjectCatalogEntryDto[], fallbackValues: string[], currentValue?: string) => {
  const configured = entries
    .filter((entry) => entry.isActive)
    .map((entry) => entry.name.trim())
    .filter(Boolean);

  const values = configured.length > 0 ? configured : fallbackValues;
  return currentValue && !values.includes(currentValue) ? [currentValue, ...values] : values;
};

const formatUserLabel = (user: User) => {
  const fullName = [user.firstName, user.lastName].filter(Boolean).join(' ').trim();
  return fullName ? `${fullName} (${user.username})` : user.username;
};

const flattenWorkItems = (items: ProjectDetailDto['workItems'] = []): NonNullable<ProjectDetailDto['workItems']> =>
  items.flatMap((item) => [item, ...(item.children || [])]);

const getCurrentUserId = () => {
  if (typeof window === 'undefined') return '';
  try {
    const raw = localStorage.getItem('user');
    if (!raw) return '';
    const parsed = JSON.parse(raw);
    return parsed?.id || parsed?.userId || '';
  } catch {
    return '';
  }
};

const draftTemplate = (userId: string): CreateProjectTimesheetEntryDto => ({
  userId,
  entryDate: today(),
  hours: 8,
  isBillable: false,
  hourlyRate: 0,
  workType: 'Standard',
  notes: '',
  status: 'Draft',
});

export default function DevelopmentTimesheetsPage() {
  const currentUserId = useMemo(() => getCurrentUserId(), []);
  const [projects, setProjects] = useState<ProjectLookupDto[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [timesheetWorkTypeCatalog, setTimesheetWorkTypeCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<string>('none');
  const [project, setProject] = useState<ProjectDetailDto | null>(null);
  const [myEntries, setMyEntries] = useState<ProjectTimesheetEntryDto[]>([]);
  const [summary, setSummary] = useState<ProjectApprovalQueueSummaryDto | null>(null);
  const [approvalQueue, setApprovalQueue] = useState<ProjectTimesheetApprovalQueueItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [queueStatus, setQueueStatus] = useState<string>('Submitted');
  const [draft, setDraft] = useState<CreateProjectTimesheetEntryDto>(draftTemplate(currentUserId));
  const [baseCurrency, setBaseCurrency] = useState<ProjectCurrencyReference>(DEFAULT_PROJECT_CURRENCY);
  const activeUsers = useMemo(() => users.filter((user) => user.isActive), [users]);
  const activeWorkItems = useMemo(() => flattenWorkItems(project?.workItems), [project?.workItems]);
  const userLabels = useMemo(
    () =>
      new Map(
        activeUsers.map((user) => [user.id, formatUserLabel(user)]),
      ),
    [activeUsers],
  );
  const timesheetWorkTypeOptions = useMemo(
    () => resolveCatalogOptions(timesheetWorkTypeCatalog, DEFAULT_TIMESHEET_WORK_TYPES, draft.workType),
    [timesheetWorkTypeCatalog, draft.workType],
  );

  const load = async (projectId?: string) => {
    try {
      setLoading(true);
      const [projectItems, mine, queue, loadedUsers, loadedWorkTypes, currencyContext] = await Promise.all([
        projectService.lookupProjects(),
        projectService.getMyTimesheets(),
        projectService.getTimesheetApprovalQueue(undefined, queueStatus === 'all' ? undefined : queueStatus, undefined, 100),
        userService.searchUsers('').catch(() => []),
        projectService.getCatalogEntries('timesheet-work-types').catch(() => []),
        loadProjectCurrencyContext(),
      ]);
      setProjects(projectItems);
      setMyEntries(mine);
      setApprovalQueue(queue);
      setUsers(loadedUsers);
      setTimesheetWorkTypeCatalog(loadedWorkTypes);
      setBaseCurrency(currencyContext.baseCurrency);

      const resolvedProjectId = projectId && projectId !== 'none'
        ? projectId
        : selectedProjectId !== 'none'
          ? selectedProjectId
          : projectItems[0]?.id;

      if (!resolvedProjectId) {
        setSelectedProjectId('none');
        setProject(null);
        setSummary(null);
        return;
      }

      const [detail, totals] = await Promise.all([
        projectService.getProjectById(resolvedProjectId),
        projectService.getTimesheetApprovalSummary(resolvedProjectId),
      ]);
      setSelectedProjectId(resolvedProjectId);
      setProject(detail);
      setSummary(totals);
    } catch (error: any) {
      toast.error(error.message || 'Failed to load timesheet workspace');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load(selectedProjectId !== 'none' ? selectedProjectId : undefined);
  }, [queueStatus]);

  const formatMoney = (value: number, currency?: string | null) => formatProjectMoney(value, currency, baseCurrency.code);

  const startCreate = () => {
    setEditingId(null);
    setDraft(draftTemplate(currentUserId));
  };

  const editEntry = (entry: ProjectTimesheetEntryDto) => {
    setEditingId(entry.id);
    setDraft({
      workItemId: entry.workItemId,
      userId: entry.userId,
      entryDate: entry.entryDate ? String(entry.entryDate).slice(0, 10) : today(),
      hours: entry.hours,
      isBillable: entry.isBillable,
      hourlyRate: entry.hourlyRate,
      workType: entry.workType,
      notes: entry.notes || '',
      status: entry.status,
    });
  };

  const saveDraft = async (submitAfterSave: boolean) => {
    if (!selectedProjectId || selectedProjectId === 'none') {
      toast.error('Select a project first');
      return;
    }

    try {
      const payload = {
        ...draft,
        userId: draft.userId || currentUserId,
        status: submitAfterSave ? 'Submitted' : 'Draft',
      };

      if (editingId) {
        const updated = await projectService.updateTimesheet(editingId, payload);
        if (submitAfterSave && updated.status !== 'Submitted') {
          await projectService.submitTimesheet(editingId);
        }
      } else {
        const created = await projectService.addTimesheet(selectedProjectId, payload);
        if (submitAfterSave && created.status !== 'Submitted') {
          await projectService.submitTimesheet(created.id);
        }
      }

      startCreate();
      await load(selectedProjectId);
      toast.success(submitAfterSave ? 'Timesheet submitted' : 'Timesheet saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save timesheet');
    }
  };

  const removeEntry = async (entryId: string) => {
    try {
      await projectService.deleteTimesheet(entryId);
      await load(selectedProjectId);
      if (editingId === entryId) startCreate();
      toast.success('Timesheet deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete timesheet');
    }
  };

  const decideEntry = async (entryId: string, action: 'approve' | 'reject') => {
    try {
      if (action === 'approve') {
        await projectService.approveTimesheet(entryId);
      } else {
        await projectService.rejectTimesheet(entryId, 'Needs correction');
      }
      await load(selectedProjectId);
      toast.success(action === 'approve' ? 'Timesheet approved' : 'Timesheet rejected');
    } catch (error: any) {
      toast.error(error.message || `Failed to ${action} timesheet`);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Timesheets</h1>
        <p className="text-muted-foreground">Capture draft time, submit for approval, and clear project worklog queues.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Controls</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 lg:grid-cols-[1.2fr_0.8fr_0.8fr_0.8fr]">
          <div className="grid gap-2">
            <Label>Project</Label>
            <Select value={selectedProjectId} onValueChange={(value) => load(value)}>
              <SelectTrigger><SelectValue placeholder="Select project" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">Select project</SelectItem>
                {projects.map((item) => <SelectItem key={item.id} value={item.id}>{item.projectCode} | {item.title}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Drafts</div>
            <div className="text-2xl font-semibold">{summary?.draftCount ?? 0}</div>
          </div>
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Pending Approval</div>
            <div className="text-2xl font-semibold">{summary?.submittedCount ?? 0}</div>
          </div>
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Approval Queue</div>
            <div className="text-2xl font-semibold">{approvalQueue.length}</div>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[0.9fr_1.1fr]">
        <Card>
          <CardHeader>
            <CardTitle>{editingId ? 'Edit Entry' : 'New Entry'}</CardTitle>
            <CardDescription>{project ? `${project.projectCode} | ${project.title}` : 'Select a project to capture time'}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="grid gap-2">
                <Label>User</Label>
                <Select value={draft.userId || 'none'} onValueChange={(value) => setDraft((current) => ({ ...current, userId: value === 'none' ? '' : value }))}>
                  <SelectTrigger><SelectValue placeholder="Select user" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No user</SelectItem>
                    {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Work Item</Label>
                <Select value={draft.workItemId || 'none'} onValueChange={(value) => setDraft((current) => ({ ...current, workItemId: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Project level</SelectItem>
                    {activeWorkItems.map((item) => <SelectItem key={item.id} value={item.id}>{item.title}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Entry Date</Label>
                <Input type="date" value={draft.entryDate ? String(draft.entryDate).slice(0, 10) : ''} onChange={(e) => setDraft((current) => ({ ...current, entryDate: e.target.value }))} />
              </div>
              <div className="grid gap-2">
                <Label>Hours</Label>
                <Input type="number" value={draft.hours || 0} onChange={(e) => setDraft((current) => ({ ...current, hours: Number(e.target.value || '0') }))} />
              </div>
              <div className="grid gap-2">
                <Label>Hourly Rate</Label>
                <Input type="number" value={draft.hourlyRate || 0} onChange={(e) => setDraft((current) => ({ ...current, hourlyRate: Number(e.target.value || '0') }))} />
              </div>
              <div className="grid gap-2">
                <Label>Work Type</Label>
                <Select value={draft.workType || timesheetWorkTypeOptions[0]} onValueChange={(value) => setDraft((current) => ({ ...current, workType: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {timesheetWorkTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Billable</Label>
                <Select value={draft.isBillable ? 'true' : 'false'} onValueChange={(value) => setDraft((current) => ({ ...current, isBillable: value === 'true' }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="false">Non-billable</SelectItem>
                    <SelectItem value="true">Billable</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="grid gap-2">
              <Label>Notes</Label>
              <Textarea rows={3} value={draft.notes || ''} onChange={(e) => setDraft((current) => ({ ...current, notes: e.target.value }))} />
            </div>
            <div className="flex flex-wrap gap-2">
              <Button disabled={!project} onClick={() => saveDraft(false)}>Save Draft</Button>
              <Button variant="outline" disabled={!project} onClick={() => saveDraft(true)}>Submit</Button>
              {editingId ? <Button variant="ghost" onClick={startCreate}>Cancel Edit</Button> : null}
            </div>
          </CardContent>
        </Card>

        <div className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>My Worklog Queue</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              {loading ? (
                <div className="py-8 text-center text-muted-foreground">Loading entries...</div>
              ) : !myEntries.length ? (
                <div className="py-8 text-center text-muted-foreground">No timesheet entries yet.</div>
              ) : (
                myEntries.map((entry) => (
                  <div key={entry.id} className="rounded-lg border p-4">
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <div className="font-semibold">{entry.projectCode || entry.projectId} | {entry.hours}h</div>
                        <div className="text-sm text-muted-foreground">
                          {new Date(entry.entryDate).toLocaleDateString()} | {entry.workType} | cost {formatMoney(entry.costAmount)}
                        </div>
                        {entry.notes ? <div className="mt-2 text-sm text-muted-foreground whitespace-pre-wrap">{entry.notes}</div> : null}
                      </div>
                      <Badge variant={entry.status === 'Approved' ? 'outline' : entry.status === 'Rejected' ? 'destructive' : 'default'}>{entry.status}</Badge>
                    </div>
                    <div className="mt-3 flex flex-wrap gap-2">
                      {entry.canEdit ? <Button size="sm" variant="outline" onClick={() => editEntry(entry)}>Edit</Button> : null}
                      {entry.status === 'Draft' || entry.status === 'Rejected' ? <Button size="sm" onClick={() => projectService.submitTimesheet(entry.id).then(() => load(selectedProjectId)).then(() => toast.success('Timesheet submitted')).catch((error) => toast.error(error.message || 'Failed to submit timesheet'))}>Submit</Button> : null}
                      {entry.canDelete ? <Button size="sm" variant="ghost" onClick={() => removeEntry(entry.id)}>Delete</Button> : null}
                    </div>
                  </div>
                ))
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <CardTitle>Approval Queue</CardTitle>
                  <CardDescription>Submitted and recent time records across accessible projects.</CardDescription>
                </div>
                <Select value={queueStatus} onValueChange={setQueueStatus}>
                  <SelectTrigger className="w-[180px]"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Submitted">Submitted</SelectItem>
                    <SelectItem value="Rejected">Rejected</SelectItem>
                    <SelectItem value="Approved">Approved</SelectItem>
                    <SelectItem value="Draft">Draft</SelectItem>
                    <SelectItem value="all">All statuses</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </CardHeader>
            <CardContent className="space-y-3">
              {!approvalQueue.length ? (
                <div className="text-sm text-muted-foreground">No timesheet approval items are available for the selected filter.</div>
              ) : (
                approvalQueue.map((entry) => (
                  <div key={entry.entryId} className="rounded-lg border p-4">
                    <div className="flex items-center justify-between gap-3">
                      <div>
                        <div className="font-semibold">{entry.projectCode} | {entry.hours}h</div>
                        <div className="text-sm text-muted-foreground">
                          {new Date(entry.entryDate).toLocaleDateString()} | {entry.workItemTitle || 'Project level'} | {entry.workType}
                        </div>
                        <div className="text-sm text-muted-foreground">
                          {userLabels.get(entry.userId) || 'Unknown user'} | {entry.queueStage} | {entry.daysOpen} day(s)
                        </div>
                      </div>
                      <Badge variant={entry.status === 'Rejected' ? 'destructive' : entry.status === 'Approved' ? 'outline' : 'secondary'}>{entry.status}</Badge>
                    </div>
                    {entry.notes ? <div className="mt-2 text-sm text-muted-foreground whitespace-pre-wrap">{entry.notes}</div> : null}
                    <div className="mt-3 flex gap-2">
                      <Button size="sm" variant="outline" onClick={() => load(entry.projectId)}>Review Project</Button>
                      {entry.status === 'Submitted' ? <Button size="sm" onClick={() => decideEntry(entry.entryId, 'approve')}>Approve</Button> : null}
                      {entry.status === 'Submitted' ? <Button size="sm" variant="outline" onClick={() => decideEntry(entry.entryId, 'reject')}>Reject</Button> : null}
                    </div>
                  </div>
                ))
              )}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
