'use client';

import { useEffect, useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { currencyService, type CurrencyListDto } from '@/services/financeCommonService';
import { CreateProjectExpenseDto, ProjectApprovalQueueSummaryDto, ProjectCatalogEntryDto, ProjectDetailDto, ProjectExpenseApprovalQueueItemDto, ProjectExpenseDto, ProjectLookupDto, projectService } from '@/services/projectService';
import { userService } from '@/services/user';
import type { User } from '@/types';
import { toast } from 'sonner';

const today = () => new Date().toISOString().slice(0, 10);
const DEFAULT_EXPENSE_CATEGORIES = ['Travel', 'Meals', 'Lodging', 'Supplies', 'Equipment', 'Other'];
const DEFAULT_CURRENCIES = ['USD'];

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

const formatCurrencyLabel = (currency: CurrencyListDto) => `${currency.code} | ${currency.name}`;

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

const draftTemplate = (userId: string): CreateProjectExpenseDto => ({
  userId,
  expenseDate: today(),
  category: 'General',
  currency: 'USD',
  amount: 0,
  taxAmount: 0,
  isBillable: false,
  notes: '',
  status: 'Draft',
});

export default function DevelopmentExpensesPage() {
  const currentUserId = useMemo(() => getCurrentUserId(), []);
  const [projects, setProjects] = useState<ProjectLookupDto[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [expenseCategoryCatalog, setExpenseCategoryCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [currencies, setCurrencies] = useState<CurrencyListDto[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<string>('none');
  const [project, setProject] = useState<ProjectDetailDto | null>(null);
  const [myEntries, setMyEntries] = useState<ProjectExpenseDto[]>([]);
  const [summary, setSummary] = useState<ProjectApprovalQueueSummaryDto | null>(null);
  const [approvalQueue, setApprovalQueue] = useState<ProjectExpenseApprovalQueueItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [queueStatus, setQueueStatus] = useState<string>('Submitted');
  const [draft, setDraft] = useState<CreateProjectExpenseDto>(draftTemplate(currentUserId));
  const activeUsers = useMemo(() => users.filter((user) => user.isActive), [users]);
  const activeWorkItems = useMemo(() => flattenWorkItems(project?.workItems), [project?.workItems]);
  const userLabels = useMemo(
    () =>
      new Map(
        activeUsers.map((user) => [user.id, formatUserLabel(user)]),
      ),
    [activeUsers],
  );
  const currencyOptions = useMemo(() => {
    const codes = currencies
      .filter((currency) => currency.isActive)
      .map((currency) => currency.code.trim().toUpperCase())
      .filter(Boolean);
    const values = codes.length > 0 ? codes : DEFAULT_CURRENCIES;
    return draft.currency && !values.includes(draft.currency) ? [draft.currency, ...values] : values;
  }, [currencies, draft.currency]);
  const expenseCategoryOptions = useMemo(
    () => resolveCatalogOptions(expenseCategoryCatalog, DEFAULT_EXPENSE_CATEGORIES, draft.category),
    [expenseCategoryCatalog, draft.category],
  );

  const load = async (projectId?: string) => {
    try {
      setLoading(true);
      const [projectItems, mine, queue, loadedUsers, loadedCategories, loadedCurrencies] = await Promise.all([
        projectService.lookupProjects(),
        projectService.getMyExpenses(),
        projectService.getExpenseApprovalQueue(undefined, queueStatus === 'all' ? undefined : queueStatus, undefined, 100),
        userService.searchUsers('').catch(() => []),
        projectService.getCatalogEntries('expense-categories').catch(() => []),
        currencyService.getActive().catch(() => []),
      ]);
      setProjects(projectItems);
      setMyEntries(mine);
      setApprovalQueue(queue);
      setUsers(loadedUsers);
      setExpenseCategoryCatalog(loadedCategories);
      setCurrencies(loadedCurrencies);

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
        projectService.getExpenseApprovalSummary(resolvedProjectId),
      ]);
      setSelectedProjectId(resolvedProjectId);
      setProject(detail);
      setSummary(totals);
    } catch (error: any) {
      toast.error(error.message || 'Failed to load expense workspace');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load(selectedProjectId !== 'none' ? selectedProjectId : undefined);
  }, [queueStatus]);

  const startCreate = () => {
    setEditingId(null);
    setDraft(draftTemplate(currentUserId));
  };

  const editEntry = (entry: ProjectExpenseDto) => {
    setEditingId(entry.id);
    setDraft({
      workItemId: entry.workItemId,
      userId: entry.userId,
      expenseDate: entry.expenseDate ? String(entry.expenseDate).slice(0, 10) : today(),
      category: entry.category,
      currency: entry.currency,
      amount: entry.amount,
      taxAmount: entry.taxAmount,
      isBillable: entry.isBillable,
      receiptDocumentId: entry.receiptDocumentId,
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
        const updated = await projectService.updateExpense(editingId, payload);
        if (submitAfterSave && updated.status !== 'Submitted') {
          await projectService.submitExpense(editingId);
        }
      } else {
        const created = await projectService.addExpense(selectedProjectId, payload);
        if (submitAfterSave && created.status !== 'Submitted') {
          await projectService.submitExpense(created.id);
        }
      }

      startCreate();
      await load(selectedProjectId);
      toast.success(submitAfterSave ? 'Expense submitted' : 'Expense saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save expense');
    }
  };

  const removeEntry = async (entryId: string) => {
    try {
      await projectService.deleteExpense(entryId);
      await load(selectedProjectId);
      if (editingId === entryId) startCreate();
      toast.success('Expense deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete expense');
    }
  };

  const decideEntry = async (entryId: string, action: 'approve' | 'reject') => {
    try {
      if (action === 'approve') {
        await projectService.approveExpense(entryId);
      } else {
        await projectService.rejectExpense(entryId, 'Needs correction');
      }
      await load(selectedProjectId);
      toast.success(action === 'approve' ? 'Expense approved' : 'Expense rejected');
    } catch (error: any) {
      toast.error(error.message || `Failed to ${action} expense`);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Expenses</h1>
        <p className="text-muted-foreground">Capture reimbursable and billable costs, then process approval queues by project.</p>
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
            <CardTitle>{editingId ? 'Edit Expense' : 'New Expense'}</CardTitle>
            <CardDescription>{project ? `${project.projectCode} | ${project.title}` : 'Select a project to capture cost'}</CardDescription>
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
                <Label>Expense Date</Label>
                <Input type="date" value={draft.expenseDate ? String(draft.expenseDate).slice(0, 10) : ''} onChange={(e) => setDraft((current) => ({ ...current, expenseDate: e.target.value }))} />
              </div>
              <div className="grid gap-2">
                <Label>Category</Label>
                <Select value={draft.category || expenseCategoryOptions[0]} onValueChange={(value) => setDraft((current) => ({ ...current, category: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {expenseCategoryOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Currency</Label>
                <Select value={draft.currency || currencyOptions[0]} onValueChange={(value) => setDraft((current) => ({ ...current, currency: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {currencyOptions.map((code) => {
                      const currency = currencies.find((item) => item.code.toUpperCase() === code.toUpperCase());
                      return (
                        <SelectItem key={code} value={code}>
                          {currency ? formatCurrencyLabel(currency) : code}
                        </SelectItem>
                      );
                    })}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Amount</Label>
                <Input type="number" value={draft.amount || 0} onChange={(e) => setDraft((current) => ({ ...current, amount: Number(e.target.value || '0') }))} />
              </div>
              <div className="grid gap-2">
                <Label>Tax Amount</Label>
                <Input type="number" value={draft.taxAmount || 0} onChange={(e) => setDraft((current) => ({ ...current, taxAmount: Number(e.target.value || '0') }))} />
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
              <CardTitle>My Expense Queue</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              {loading ? (
                <div className="py-8 text-center text-muted-foreground">Loading expenses...</div>
              ) : !myEntries.length ? (
                <div className="py-8 text-center text-muted-foreground">No expense entries yet.</div>
              ) : (
                myEntries.map((entry) => (
                  <div key={entry.id} className="rounded-lg border p-4">
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <div className="font-semibold">{entry.projectCode || entry.projectId} | {entry.currency} {(entry.amount + entry.taxAmount).toLocaleString()}</div>
                        <div className="text-sm text-muted-foreground">
                          {new Date(entry.expenseDate).toLocaleDateString()} | {entry.category} | {entry.workItemTitle || 'Project level'}
                        </div>
                        {entry.notes ? <div className="mt-2 text-sm text-muted-foreground whitespace-pre-wrap">{entry.notes}</div> : null}
                      </div>
                      <Badge variant={entry.status === 'Approved' ? 'outline' : entry.status === 'Rejected' ? 'destructive' : 'default'}>{entry.status}</Badge>
                    </div>
                    <div className="mt-3 flex flex-wrap gap-2">
                      {entry.canEdit ? <Button size="sm" variant="outline" onClick={() => editEntry(entry)}>Edit</Button> : null}
                      {entry.status === 'Draft' || entry.status === 'Rejected' ? <Button size="sm" onClick={() => projectService.submitExpense(entry.id).then(() => load(selectedProjectId)).then(() => toast.success('Expense submitted')).catch((error) => toast.error(error.message || 'Failed to submit expense'))}>Submit</Button> : null}
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
                  <CardDescription>Submitted and recent project costs across accessible projects.</CardDescription>
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
                <div className="text-sm text-muted-foreground">No expense approval items are available for the selected filter.</div>
              ) : (
                approvalQueue.map((entry) => (
                  <div key={entry.expenseId} className="rounded-lg border p-4">
                    <div className="flex items-center justify-between gap-3">
                      <div>
                        <div className="font-semibold">{entry.projectCode} | {entry.currency} {entry.totalAmount.toLocaleString()}</div>
                        <div className="text-sm text-muted-foreground">
                          {new Date(entry.expenseDate).toLocaleDateString()} | {entry.category} | {entry.workItemTitle || 'Project level'}
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
                      {entry.status === 'Submitted' ? <Button size="sm" onClick={() => decideEntry(entry.expenseId, 'approve')}>Approve</Button> : null}
                      {entry.status === 'Submitted' ? <Button size="sm" variant="outline" onClick={() => decideEntry(entry.expenseId, 'reject')}>Reject</Button> : null}
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
