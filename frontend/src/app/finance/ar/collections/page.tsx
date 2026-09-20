'use client';

import React, { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import {
  AlertTriangle,
  BellRing,
  CheckCircle2,
  Clock3,
  History,
  Loader2,
  RefreshCw,
  Search,
  UserPlus,
  Users,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import {
  arCollectionDataService,
  type ArCollectionAssignee,
  type ArCollectionHistoryItem,
  type ArCollectionSummary,
  type ArCollectionTaskStatus,
  type ArCollectionWorkItem,
} from '@/services/finance/ar-collection-data.service';

const statuses: ArCollectionTaskStatus[] = [
  'Pending', 'InProgress', 'PromiseToPay', 'Escalated', 'Resolved', 'WrittenOff',
];
const channels = ['Email', 'SMS', 'Letter', 'Phone', 'Visit', 'Internal'];
const label = (value: string) => value.replace(/([a-z])([A-Z])/g, '$1 $2');
const dateValue = (value?: string) => value ? value.slice(0, 10) : '';
const formatDate = (value?: string) => value
  ? new Intl.DateTimeFormat('en-GH', { dateStyle: 'medium' }).format(new Date(value))
  : '—';
const money = (amount: number, currency = 'GHS') =>
  currency === 'UNSPECIFIED'
    ? `Currency unspecified ${new Intl.NumberFormat('en-GH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(amount)}`
    : new Intl.NumberFormat('en-GH', { style: 'currency', currency }).format(amount);

export default function ArCollectionsPage() {
  const { toast } = useToast();
  const { hasPermission } = useAuth();
  // UI checks improve discoverability for read-only users. The API repeats these
  // checks server-side, so hiding a button is never treated as the security control.
  const canManage = hasPermission('Finance.AR.Collections.Manage');
  const canRecordReminder = hasPermission('Finance.AR.Collections.Reminders.Record');
  const [rows, setRows] = useState<ArCollectionWorkItem[]>([]);
  const [summary, setSummary] = useState<ArCollectionSummary>();
  const [assignees, setAssignees] = useState<ArCollectionAssignee[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string>();
  const [saving, setSaving] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [search, setSearch] = useState('');
  const [submittedSearch, setSubmittedSearch] = useState('');
  const [status, setStatus] = useState('all');
  const [minimumDays, setMinimumDays] = useState(1);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(0);
  const [selected, setSelected] = useState<ArCollectionWorkItem>();
  const [taskOpen, setTaskOpen] = useState(false);
  const [reminderOpen, setReminderOpen] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [generateOpen, setGenerateOpen] = useState(false);
  const [history, setHistory] = useState<ArCollectionHistoryItem[]>([]);

  const [taskForm, setTaskForm] = useState({
    collectionStatus: 'Pending', assignedToId: '', followUpDate: '', priority: '5',
    promisedAmount: '0', promisedPayDate: '', outcome: '', notes: '',
  });
  const [reminderForm, setReminderForm] = useState({
    channel: 'Email', recipient: '', message: '', nextFollowUpDate: '', confirmedDispatched: false,
  });
  const [generateForm, setGenerateForm] = useState({ assignedToId: '', followUpInDays: '1', priority: '5' });

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const [queue, totals, users] = await Promise.all([
        arCollectionDataService.getWorkQueue({
          minimumDaysOverdue: minimumDays,
          status: status === 'all' ? undefined : status,
          search: submittedSearch || undefined,
          page,
          pageSize: 25,
        }),
        arCollectionDataService.getSummary(),
        arCollectionDataService.getAssignees(),
      ]);
      setRows(queue.items);
      setTotalPages(queue.totalPages);
      setSummary(totals);
      setAssignees(users);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Please retry.';
      setRows([]);
      setTotalPages(0);
      setSummary(undefined);
      setAssignees([]);
      setLoadError(message);
      toast({
        title: 'Collection workspace could not be loaded',
        description: message,
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  }, [minimumDays, page, status, submittedSearch, toast]);

  useEffect(() => { void load(); }, [load]);

  const cards = useMemo(() => [
    { title: 'Overdue exposures', value: summary?.overdueExposureCount ?? 0, Icon: AlertTriangle, tone: 'text-amber-600' },
    { title: 'Unassigned', value: summary?.unassignedExposureCount ?? 0, Icon: Users, tone: 'text-blue-600' },
    { title: 'Follow-ups overdue', value: summary?.overdueFollowUpCount ?? 0, Icon: Clock3, tone: 'text-red-600' },
    { title: 'Breached promises', value: summary?.breachedPromiseCount ?? 0, Icon: BellRing, tone: 'text-rose-600' },
  ], [summary]);

  const openTask = (row: ArCollectionWorkItem) => {
    setSelected(row);
    setTaskForm({
      collectionStatus: row.taskId ? row.taskStatus : 'Pending',
      assignedToId: row.assignedToId ?? '',
      followUpDate: dateValue(row.followUpDate) || dateValue(new Date(Date.now() + 86400000).toISOString()),
      priority: String(row.priority || 5),
      promisedAmount: String(row.promisedAmount || 0),
      promisedPayDate: dateValue(row.promisedPayDate),
      outcome: row.outcome ?? '',
      notes: row.notes ?? '',
    });
    setTaskOpen(true);
  };

  const saveTask = async () => {
    if (!selected) return;
    setSaving(true);
    try {
      if (!selected.taskId) {
        await arCollectionDataService.createTask({
          settlementBalanceId: selected.settlementBalanceId,
          assignedToId: taskForm.assignedToId || undefined,
          followUpDate: taskForm.followUpDate || undefined,
          priority: Number(taskForm.priority),
          notes: taskForm.notes || undefined,
        });
        toast({ title: 'Collection task created', description: `${selected.invoiceNumber} is now in the controlled follow-up queue.` });
      } else {
        if (!selected.rowVersion) throw new Error('Refresh the work queue before updating this task.');
        await arCollectionDataService.updateTask(selected.taskId, {
          collectionStatus: taskForm.collectionStatus,
          assignedToId: taskForm.assignedToId || undefined,
          followUpDate: taskForm.followUpDate || undefined,
          priority: Number(taskForm.priority),
          promisedAmount: Number(taskForm.promisedAmount || 0),
          promisedPayDate: taskForm.promisedPayDate || undefined,
          outcome: taskForm.outcome || undefined,
          notes: taskForm.notes || undefined,
          rowVersion: selected.rowVersion,
        });
        toast({ title: 'Collection task updated', description: 'Assignment, promise, and outcome evidence were saved.' });
      }
      setTaskOpen(false);
      await load();
    } catch (error) {
      toast({ title: 'Task could not be saved', description: error instanceof Error ? error.message : 'Please retry.', variant: 'destructive' });
    } finally {
      setSaving(false);
    }
  };

  const generateTasks = async () => {
    setGenerating(true);
    try {
      const result = await arCollectionDataService.generateTasks({
        minimumDaysOverdue: minimumDays,
        assignedToId: generateForm.assignedToId || undefined,
        followUpInDays: Number(generateForm.followUpInDays),
        priority: Number(generateForm.priority),
      });
      toast({
        title: 'Overdue work queue synchronized',
        description: `${result.createdCount} created, ${result.refreshedCount} refreshed, ${result.autoResolvedCount} resolved, ${result.reactivatedCount} reactivated, and ${result.skippedUnresolvedPartnerCount} skipped because partner identity could not be resolved.`,
      });
      setGenerateOpen(false);
      await load();
    } catch (error) {
      toast({ title: 'Task generation failed', description: error instanceof Error ? error.message : 'Please retry.', variant: 'destructive' });
    } finally {
      setGenerating(false);
    }
  };

  const openReminder = (row: ArCollectionWorkItem) => {
    if (!row.taskId) return;
    setSelected(row);
    const recipient = row.customerEmail ?? row.customerPhone ?? '';
    setReminderForm({
      channel: row.customerEmail ? 'Email' : row.customerPhone ? 'Phone' : 'Letter',
      recipient,
      message: `Reminder: invoice ${row.invoiceNumber} has an outstanding balance of ${money(row.outstandingAmount, row.currencyCode)} and was due on ${formatDate(row.dueDate)}. Please contact TDC Finance to confirm settlement arrangements.`,
      nextFollowUpDate: dateValue(new Date(Date.now() + 7 * 86400000).toISOString()),
      confirmedDispatched: false,
    });
    setReminderOpen(true);
  };

  const recordReminder = async () => {
    if (!selected?.taskId || !selected.rowVersion) return;
    setSaving(true);
    try {
      await arCollectionDataService.recordReminder(selected.taskId, {
        ...reminderForm,
        recipient: reminderForm.recipient || undefined,
        nextFollowUpDate: reminderForm.nextFollowUpDate || undefined,
        rowVersion: selected.rowVersion,
      });
      toast({
        title: reminderForm.confirmedDispatched ? 'Reminder dispatch recorded' : 'Reminder prepared',
        description: 'The communication evidence is now part of the collection history and Finance audit trail.',
      });
      setReminderOpen(false);
      await load();
    } catch (error) {
      toast({ title: 'Reminder could not be recorded', description: error instanceof Error ? error.message : 'Please retry.', variant: 'destructive' });
    } finally {
      setSaving(false);
    }
  };

  const showHistory = async (row: ArCollectionWorkItem) => {
    if (!row.taskId) return;
    setSelected(row);
    setHistoryOpen(true);
    setHistory([]);
    try {
      setHistory(await arCollectionDataService.getHistory(row.taskId));
    } catch (error) {
      toast({ title: 'History could not be loaded', description: error instanceof Error ? error.message : 'Please retry.', variant: 'destructive' });
    }
  };

  return <div className="space-y-6">
    <div className="flex flex-wrap items-start justify-between gap-4">
      <div>
        <h1 className="flex items-center gap-2 text-3xl font-bold"><BellRing className="h-8 w-8" />AR Collection Follow-up</h1>
        <p className="text-muted-foreground">Assign and evidence debt follow-up directly from posted AR settlement balances.</p>
      </div>
      <div className="flex gap-2">
        <Button variant="outline" onClick={() => void load()} disabled={loading}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
        {canManage && <Button onClick={() => setGenerateOpen(true)} disabled={generating || loading || Boolean(loadError)}>
          <UserPlus className="mr-2 h-4 w-4" />Generate follow-up tasks
        </Button>}
      </div>
    </div>

    {loading ? <Card data-testid="collection-loading"><CardContent className="p-12 text-center text-muted-foreground"><Loader2 className="mr-2 inline h-5 w-5 animate-spin" />Loading posted AR exposures…</CardContent></Card>
      : loadError ? <Card role="alert" className="border-destructive"><CardContent className="space-y-4 p-8"><div><p className="font-semibold text-destructive">Collection workspace could not be loaded</p><p className="mt-1 text-sm text-muted-foreground">{loadError}</p></div><Button onClick={() => void load()}><RefreshCw className="mr-2 h-4 w-4" />Retry</Button></CardContent></Card>
      : <>
    <div className="grid gap-4 md:grid-cols-4">{cards.map(({ title, value, Icon, tone }) =>
      <Card key={title}><CardContent className="flex items-center justify-between p-5"><div><p className="text-sm text-muted-foreground">{title}</p><p className="text-2xl font-bold">{value}</p></div><Icon className={`h-7 w-7 ${tone}`} /></CardContent></Card>)}</div>

    <Card>
      <CardContent className="grid gap-3 p-4 md:grid-cols-[1fr_190px_160px_auto]">
        <div className="flex gap-2"><Input value={search} onChange={event => setSearch(event.target.value)} placeholder="Customer or invoice…" onKeyDown={event => { if (event.key === 'Enter') { setPage(1); setSubmittedSearch(search); } }} /><Button variant="outline" onClick={() => { setPage(1); setSubmittedSearch(search); }}><Search className="h-4 w-4" /></Button></div>
        <Select value={status} onValueChange={value => { setPage(1); setStatus(value); }}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All statuses</SelectItem><SelectItem value="Unassigned">Unassigned</SelectItem>{statuses.map(item => <SelectItem key={item} value={item}>{label(item)}</SelectItem>)}</SelectContent></Select>
        <Input type="number" min={1} max={3650} value={minimumDays} onChange={event => { setPage(1); setMinimumDays(Math.max(1, Number(event.target.value))); }} aria-label="Minimum days overdue" />
        <div className="flex items-center text-sm text-muted-foreground">days overdue minimum</div>
      </CardContent>
    </Card>

    <div className="grid gap-4 lg:grid-cols-3">
      <Card><CardContent className="p-5"><p className="text-sm text-muted-foreground">Native outstanding by currency</p><div className="mt-2 space-y-1">{summary?.nativeCurrencyTotals.length ? summary.nativeCurrencyTotals.map(total => <p key={total.currencyCode} className="text-xl font-bold">{money(total.outstandingAmount, total.currencyCode)}</p>) : <p className="text-sm text-muted-foreground">No overdue native-currency exposure.</p>}</div></CardContent></Card>
      <Card><CardContent className="p-5"><p className="text-sm text-muted-foreground">Native promises by currency</p><div className="mt-2 space-y-1">{summary?.nativeCurrencyTotals.length ? summary.nativeCurrencyTotals.map(total => <p key={total.currencyCode} className="text-xl font-bold">{money(total.promisedAmount, total.currencyCode)}</p>) : <p className="text-sm text-muted-foreground">No promises currently recorded.</p>}</div></CardContent></Card>
      <Card><CardContent className="p-5"><p className="text-sm text-muted-foreground">Functional-currency outstanding</p>{summary?.functionalOutstandingTotal != null && summary.functionalCurrencyCode ? <><p className="mt-2 text-xl font-bold">{money(summary.functionalOutstandingTotal, summary.functionalCurrencyCode)}</p><p className="mt-1 text-xs text-muted-foreground">{summary.functionalTotalBasis}</p></> : <p className="mt-2 text-sm text-muted-foreground">Unavailable — {summary?.functionalTotalUnavailableReason ?? 'authoritative carrying-balance evidence was not returned.'}</p>}</CardContent></Card>
    </div>

    <Card>
      <CardHeader><CardTitle>Overdue exposure work queue</CardTitle></CardHeader>
      <CardContent className="p-0">
        <div className="overflow-x-auto"><table className="w-full text-sm">
          <thead className="border-y bg-muted/40 text-left"><tr>{['Customer / invoice', 'Due', 'Outstanding', 'Follow-up', 'Owner', 'Status', 'Actions'].map(item => <th key={item} className="px-4 py-3 font-medium">{item}</th>)}</tr></thead>
          <tbody>
            {rows.length === 0 && <tr><td colSpan={7} className="px-4 py-12 text-center text-muted-foreground"><CheckCircle2 className="mx-auto mb-2 h-8 w-8 text-green-600" />No overdue exposure matches these filters.</td></tr>}
            {rows.map(row => <tr key={row.settlementBalanceId} className="border-b align-top hover:bg-muted/30">
              <td className="px-4 py-4"><div className="font-semibold">{row.customerName}</div>{!row.isPartnerResolved && <Badge variant="destructive" className="mt-1">Unresolved partner</Badge>}<div>{row.invoiceNumber}</div><div className="text-xs text-muted-foreground">{row.customerCode || 'No customer code'} · {row.daysOverdue} days overdue · {row.agingBucket}</div>{row.partnerResolutionMessage && <div className="mt-1 text-xs text-destructive">{row.partnerResolutionMessage}</div>}</td>
              <td className="px-4 py-4">{formatDate(row.dueDate)}</td>
              <td className="px-4 py-4 font-semibold">{money(row.outstandingAmount, row.currencyCode)}{row.isPromiseBreached && <Badge variant="destructive" className="ml-2">Promise breached</Badge>}</td>
              <td className="px-4 py-4"><div className={row.isFollowUpOverdue ? 'font-semibold text-red-600' : ''}>{formatDate(row.followUpDate)}</div>{row.lastActivityAt && <div className="text-xs text-muted-foreground">Last activity {formatDate(row.lastActivityAt)}</div>}</td>
              <td className="px-4 py-4">{row.assignedToName ?? '—'}</td>
              <td className="px-4 py-4"><Badge variant={row.taskStatus === 'Unassigned' ? 'outline' : row.taskStatus === 'Resolved' ? 'default' : 'secondary'}>{label(row.taskStatus)}</Badge>{row.priority > 0 && <div className="mt-1 text-xs text-muted-foreground">Priority {row.priority}/10</div>}</td>
              <td className="px-4 py-4"><div className="flex flex-wrap gap-2">{canManage && <Button size="sm" variant="outline" onClick={() => openTask(row)}>{row.taskId ? 'Update' : 'Assign'}</Button>}{row.taskId && <>{canRecordReminder && <Button size="sm" variant="outline" onClick={() => openReminder(row)}>Reminder</Button>}<Button size="icon" variant="ghost" onClick={() => void showHistory(row)} aria-label="View history"><History className="h-4 w-4" /></Button></>}{!row.taskId && !canManage && <span className="text-muted-foreground">â€”</span>}</div></td>
            </tr>)}
          </tbody>
        </table></div>
        {totalPages > 1 && <div className="flex items-center justify-between p-4"><span className="text-sm text-muted-foreground">Page {page} of {totalPages}</span><div className="flex gap-2"><Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage(value => value - 1)}>Previous</Button><Button size="sm" variant="outline" disabled={page >= totalPages} onClick={() => setPage(value => value + 1)}>Next</Button></div></div>}
      </CardContent>
    </Card>
    </>}

    <Dialog open={generateOpen} onOpenChange={setGenerateOpen}><DialogContent><DialogHeader><DialogTitle>Generate follow-up tasks</DialogTitle><DialogDescription>This action is not driven by an approval workflow. It synchronizes overdue settlement exposure and assigns newly created tasks to the officer selected here.</DialogDescription></DialogHeader>
      <div className="space-y-4">
        <Field label="Assign generated tasks to"><Select value={generateForm.assignedToId || 'self'} onValueChange={value => setGenerateForm(form => ({ ...form, assignedToId: value === 'self' ? '' : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="self">Current user</SelectItem>{assignees.map(item => <SelectItem key={item.userId} value={item.userId}>{item.displayName}</SelectItem>)}</SelectContent></Select></Field>
        <div className="grid grid-cols-2 gap-4"><Field label="Follow up in days"><Input type="number" min={0} max={3650} value={generateForm.followUpInDays} onChange={event => setGenerateForm(form => ({ ...form, followUpInDays: event.target.value }))} /></Field><Field label="Priority (1-10)"><Input type="number" min={1} max={10} value={generateForm.priority} onChange={event => setGenerateForm(form => ({ ...form, priority: event.target.value }))} /></Field></div>
      </div><DialogFooter><Button variant="outline" onClick={() => setGenerateOpen(false)}>Cancel</Button><Button onClick={generateTasks} disabled={generating}>{generating && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Generate tasks</Button></DialogFooter>
    </DialogContent></Dialog>

    <Dialog open={taskOpen} onOpenChange={setTaskOpen}><DialogContent className="max-w-2xl"><DialogHeader><DialogTitle>{selected?.taskId ? 'Update collection task' : 'Assign collection task'}</DialogTitle></DialogHeader>
      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Collection officer"><Select value={taskForm.assignedToId || 'self'} onValueChange={value => setTaskForm(form => ({ ...form, assignedToId: value === 'self' ? '' : value }))}><SelectTrigger><SelectValue placeholder="Current user" /></SelectTrigger><SelectContent><SelectItem value="self">Current user</SelectItem>{assignees.map(item => <SelectItem key={item.userId} value={item.userId}>{item.displayName}</SelectItem>)}</SelectContent></Select></Field>
        <Field label="Status"><Select value={taskForm.collectionStatus} onValueChange={value => setTaskForm(form => ({ ...form, collectionStatus: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{statuses.map(item => <SelectItem key={item} value={item}>{label(item)}</SelectItem>)}</SelectContent></Select></Field>
        <Field label="Next follow-up"><Input type="date" value={taskForm.followUpDate} onChange={event => setTaskForm(form => ({ ...form, followUpDate: event.target.value }))} /></Field>
        <Field label="Priority (1-10)"><Input type="number" min={1} max={10} value={taskForm.priority} onChange={event => setTaskForm(form => ({ ...form, priority: event.target.value }))} /></Field>
        {taskForm.collectionStatus === 'PromiseToPay' && <><Field label="Promised amount"><Input type="number" min={0} step="0.01" value={taskForm.promisedAmount} onChange={event => setTaskForm(form => ({ ...form, promisedAmount: event.target.value }))} /></Field><Field label="Promised payment date"><Input type="date" value={taskForm.promisedPayDate} onChange={event => setTaskForm(form => ({ ...form, promisedPayDate: event.target.value }))} /></Field></>}
        <Field label="Outcome"><Input value={taskForm.outcome} onChange={event => setTaskForm(form => ({ ...form, outcome: event.target.value }))} placeholder="Contacted, disputed, handed over…" /></Field>
        <div className="md:col-span-2"><Field label="Notes"><Textarea value={taskForm.notes} onChange={event => setTaskForm(form => ({ ...form, notes: event.target.value }))} rows={4} /></Field></div>
      </div>
      <p className="text-xs text-muted-foreground">Outstanding amount is read from posted settlement evidence and cannot be edited here.</p>
      <DialogFooter><Button variant="outline" onClick={() => setTaskOpen(false)}>Cancel</Button><Button onClick={saveTask} disabled={saving}>{saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Save task</Button></DialogFooter>
    </DialogContent></Dialog>

    <Dialog open={reminderOpen} onOpenChange={setReminderOpen}><DialogContent className="max-w-2xl"><DialogHeader><DialogTitle>Prepare or record customer reminder</DialogTitle></DialogHeader>
      <div className="grid gap-4 md:grid-cols-2"><Field label="Channel"><Select value={reminderForm.channel} onValueChange={value => setReminderForm(form => ({ ...form, channel: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{channels.map(item => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select></Field><Field label="Recipient/contact reference"><Input value={reminderForm.recipient} onChange={event => setReminderForm(form => ({ ...form, recipient: event.target.value }))} /></Field><Field label="Next follow-up"><Input type="date" value={reminderForm.nextFollowUpDate} onChange={event => setReminderForm(form => ({ ...form, nextFollowUpDate: event.target.value }))} /></Field><label className="flex items-end gap-2 pb-2 text-sm"><input type="checkbox" checked={reminderForm.confirmedDispatched} onChange={event => setReminderForm(form => ({ ...form, confirmedDispatched: event.target.checked }))} />Officer confirms this reminder was dispatched</label><div className="md:col-span-2"><Field label="Reminder message"><Textarea value={reminderForm.message} onChange={event => setReminderForm(form => ({ ...form, message: event.target.value }))} rows={6} /></Field></div></div>
      <p className="text-xs text-muted-foreground">Email/SMS delivery is not assumed. The workspace records prepared or officer-confirmed communication evidence for audit.</p>
      <DialogFooter><Button variant="outline" onClick={() => setReminderOpen(false)}>Cancel</Button><Button onClick={recordReminder} disabled={saving}>{saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Record reminder</Button></DialogFooter>
    </DialogContent></Dialog>

    <Dialog open={historyOpen} onOpenChange={setHistoryOpen}><DialogContent className="max-w-2xl"><DialogHeader><DialogTitle>Collection history — {selected?.invoiceNumber}</DialogTitle></DialogHeader><div className="max-h-[60vh] space-y-3 overflow-y-auto">{history.length === 0 && <p className="py-8 text-center text-muted-foreground">No history entries loaded.</p>}{history.map(item => <div key={item.id} className="rounded-lg border p-4"><div className="flex items-start justify-between gap-3"><div><p className="font-semibold">{item.subject}</p><p className="text-xs text-muted-foreground">{item.activityType} · {item.actorName ?? 'Finance user'}</p></div><span className="text-xs text-muted-foreground">{formatDate(item.activityDate)}</span></div>{item.outcome && <Badge className="mt-2" variant="outline">{item.outcome}</Badge>}<p className="mt-2 whitespace-pre-wrap text-sm">{item.description ?? item.notes ?? 'No additional note.'}</p>{item.reminderRecipient && <p className="mt-1 text-xs text-muted-foreground">{item.reminderChannel}: {item.reminderRecipient}</p>}</div>)}</div></DialogContent></Dialog>
  </div>;
}

function Field({ label: fieldLabel, children }: { label: string; children: ReactNode }) {
  return <div className="space-y-2"><Label>{fieldLabel}</Label>{children}</div>;
}
