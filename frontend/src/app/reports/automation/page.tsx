'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertTriangle, CalendarClock, CheckCircle2, Download, FileArchive, Loader2, Pause, Play, Plus } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { TenantGuard } from '@/components/auth/tenant-guard';
import { DashboardLayout } from '@/components/layout/dashboard-layout';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useToast } from '@/hooks/use-toast';
import {
  financeReportAutomationDataService as dataService,
  type CreateFinanceReportSchedule,
  type FinanceReportAutomationWorkspace,
  type FinanceReportSchedule,
} from '@/services/finance/finance-report-automation-data.service';

// TDC operates in Ghana (UTC without daylight-saving changes), so the browser
// can submit the same date/time semantics that the backend recurrence engine stores.
const today = () => new Date().toISOString().slice(0, 10);
const when = (value?: string) => value ? new Intl.DateTimeFormat('en-GH', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '—';
const size = (value?: number) => value == null ? '—' : `${(value / 1024).toFixed(1)} KB`;

export default function FinanceReportAutomationPage() {
  const { toast } = useToast();
  const [workspace, setWorkspace] = useState<FinanceReportAutomationWorkspace>();
  const [loading, setLoading] = useState(true);
  const [working, setWorking] = useState<string>();
  const [showCreate, setShowCreate] = useState(false);
  const [form, setForm] = useState<CreateFinanceReportSchedule>({
    reportTemplateId: '', name: '', frequency: 'Monthly', timeOfDay: '07:00',
    dayOfMonth: 1, startDate: today(), exportFormat: 'PDF', recipientUserIds: [], maximumRetryAttempts: 3,
  });

  const load = useCallback(async () => {
    setLoading(true);
    try { setWorkspace(await dataService.getWorkspace()); }
    catch (error) { toast({ title: 'Report automation could not be loaded', description: error instanceof Error ? error.message : 'Please retry.', variant: 'destructive' }); }
    finally { setLoading(false); }
  }, [toast]);
  useEffect(() => { void load(); }, [load]);

  // A schedule selects a concrete published template version. Later template
  // revisions therefore cannot silently change an already approved report pack.
  const selectedTemplate = useMemo(() => workspace?.templates.find(item => item.id === form.reportTemplateId), [workspace, form.reportTemplateId]);
  const summaryCards: Array<{ label: string; value: number; icon: LucideIcon }> = [
    { label: 'Active schedules', value: workspace?.activeSchedules ?? 0, icon: CheckCircle2 },
    { label: 'Failed attempts', value: workspace?.failedExecutions ?? 0, icon: AlertTriangle },
    { label: 'Artifacts ready', value: workspace?.artifactsReady ?? 0, icon: FileArchive },
  ];

  const create = async () => {
    setWorking('create');
    try {
      await dataService.create({
        ...form,
        dayOfWeek: form.frequency === 'Weekly' ? (form.dayOfWeek ?? 1) : undefined,
        dayOfMonth: ['Monthly', 'Quarterly', 'Yearly'].includes(form.frequency) ? (form.dayOfMonth ?? 1) : undefined,
      });
      toast({ title: 'Report schedule activated', description: 'The pinned template will run in the background and deliver a private artifact.' });
      setShowCreate(false);
      setForm({ reportTemplateId: '', name: '', frequency: 'Monthly', timeOfDay: '07:00', dayOfMonth: 1, startDate: today(), exportFormat: 'PDF', recipientUserIds: [], maximumRetryAttempts: 3 });
      await load();
    } catch (error) { toast({ title: 'Schedule was not created', description: error instanceof Error ? error.message : 'Please retry.', variant: 'destructive' }); }
    finally { setWorking(undefined); }
  };

  const act = async (key: string, action: () => Promise<unknown>, success: string) => {
    setWorking(key);
    try { await action(); toast({ title: success }); await load(); }
    catch (error) { toast({ title: 'Action could not be completed', description: error instanceof Error ? error.message : 'Please retry.', variant: 'destructive' }); }
    finally { setWorking(undefined); }
  };

  const toggleStatus = (item: FinanceReportSchedule) => {
    const active = item.status === 'Active';
    const reason = window.prompt(active ? 'Why is this schedule being paused?' : 'Why is this schedule being resumed?');
    if (!reason || reason.trim().length < 5) return;
    void act(`${item.id}-status`, () => active ? dataService.pause(item, reason.trim()) : dataService.resume(item, reason.trim()), active ? 'Schedule paused' : 'Schedule resumed');
  };

  const download = async (exportId: string, fileName: string) => {
    await act(`${exportId}-download`, async () => {
      const blob = await dataService.download(exportId);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName;
      anchor.click();
      URL.revokeObjectURL(url);
    }, 'Report download started');
  };

  return <TenantGuard><DashboardLayout><div className="space-y-6">
    <div className="flex flex-wrap items-start justify-between gap-4">
      <div><h1 className="flex items-center gap-2 text-3xl font-bold"><CalendarClock className="h-8 w-8" />Report Automation</h1><p className="text-muted-foreground">Schedule approved Finance templates, monitor every attempt, and collect retained output securely.</p></div>
      <div className="flex gap-2"><Button variant="outline" disabled={!!working} onClick={() => void act('due', dataService.processDue, 'Due schedules processed')}><Play className="mr-2 h-4 w-4" />Process due</Button><Button onClick={() => setShowCreate(value => !value)}><Plus className="mr-2 h-4 w-4" />New schedule</Button></div>
    </div>

    <div className="grid gap-4 md:grid-cols-3">
      {summaryCards.map(({ label, value, icon: Icon }) => <Card key={label}><CardContent className="flex items-center justify-between p-5"><div><p className="text-sm text-muted-foreground">{label}</p><p className="text-2xl font-bold">{value}</p></div><Icon className="h-7 w-7 text-blue-600" /></CardContent></Card>)}
    </div>

    {showCreate && <Card><CardHeader><CardTitle>Schedule a published Finance template</CardTitle></CardHeader><CardContent className="grid gap-5 md:grid-cols-3">
      <div className="md:col-span-2"><Label>Published template</Label><Select value={form.reportTemplateId} onValueChange={value => setForm(item => ({ ...item, reportTemplateId: value, exportFormat: workspace?.templates.find(template => template.id === value)?.outputFormats[0] ?? 'PDF' }))}><SelectTrigger><SelectValue placeholder="Choose a versioned template" /></SelectTrigger><SelectContent>{workspace?.templates.map(item => <SelectItem key={item.id} value={item.id}>{item.reportName} · {item.name} v{item.version}</SelectItem>)}</SelectContent></Select></div>
      <div><Label>Output</Label><Select value={form.exportFormat} onValueChange={value => setForm(item => ({ ...item, exportFormat: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{(selectedTemplate?.outputFormats ?? ['PDF']).map(item => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select></div>
      <div className="md:col-span-2"><Label htmlFor="schedule-name">Schedule name</Label><Input id="schedule-name" value={form.name} onChange={event => setForm(item => ({ ...item, name: event.target.value }))} placeholder="Monthly management accounts pack" /></div>
      <div><Label>Frequency</Label><Select value={form.frequency} onValueChange={value => setForm(item => ({ ...item, frequency: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['Daily', 'Weekly', 'Monthly', 'Quarterly', 'Yearly'].map(item => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select></div>
      <div><Label htmlFor="start-date">Start date</Label><Input id="start-date" type="date" value={form.startDate} onChange={event => setForm(item => ({ ...item, startDate: event.target.value }))} /></div>
      <div><Label htmlFor="run-time">Run time (Ghana/UTC)</Label><Input id="run-time" type="time" value={form.timeOfDay} onChange={event => setForm(item => ({ ...item, timeOfDay: event.target.value }))} /></div>
      {form.frequency === 'Weekly' && <div><Label>Day of week</Label><Select value={String(form.dayOfWeek ?? 1)} onValueChange={value => setForm(item => ({ ...item, dayOfWeek: Number(value) }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'].map((item, index) => <SelectItem key={item} value={String(index)}>{item}</SelectItem>)}</SelectContent></Select></div>}
      {['Monthly', 'Quarterly', 'Yearly'].includes(form.frequency) && <div><Label htmlFor="day-month">Day of month</Label><Input id="day-month" type="number" min={1} max={31} value={form.dayOfMonth ?? 1} onChange={event => setForm(item => ({ ...item, dayOfMonth: Number(event.target.value) }))} /></div>}
      <div className="md:col-span-3"><Label htmlFor="recipients">In-app recipients</Label><select id="recipients" multiple className="mt-2 min-h-28 w-full rounded-md border bg-background p-2 text-sm" value={form.recipientUserIds} onChange={event => setForm(item => ({ ...item, recipientUserIds: Array.from(event.target.selectedOptions, option => option.value) }))}>{workspace?.recipients.map(item => <option key={item.id} value={item.id}>{item.name} · {item.email}</option>)}</select><p className="mt-1 text-xs text-muted-foreground">The schedule owner is always included. External email remains an explicit future integration; this slice delivers through the secured in-app workspace.</p></div>
      <div className="md:col-span-3 flex justify-end gap-2"><Button variant="outline" onClick={() => setShowCreate(false)}>Cancel</Button><Button disabled={!form.reportTemplateId || form.name.trim().length === 0 || working === 'create'} onClick={() => void create()}>{working === 'create' && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Activate schedule</Button></div>
    </CardContent></Card>}

    <Card><CardHeader><CardTitle>Controlled schedules</CardTitle></CardHeader><CardContent className="p-0"><div className="overflow-x-auto"><table className="w-full text-sm"><thead className="border-y bg-muted/40 text-left"><tr>{['Schedule', 'Recurrence', 'Next run', 'Output', 'Status', 'Actions'].map(item => <th key={item} className="px-5 py-3 font-medium">{item}</th>)}</tr></thead><tbody>
      {loading && <tr><td colSpan={6} className="px-5 py-10 text-center text-muted-foreground"><Loader2 className="mr-2 inline h-4 w-4 animate-spin" />Loading schedules...</td></tr>}
      {!loading && !workspace?.schedules.length && <tr><td colSpan={6} className="px-5 py-10 text-center text-muted-foreground">No Finance report schedule has been configured.</td></tr>}
      {workspace?.schedules.map(item => <tr key={item.id} className="border-b"><td className="px-5 py-4"><p className="font-semibold">{item.name}</p><p className="text-xs text-muted-foreground">{item.reportName} · template v{item.templateVersion}</p>{item.lastError && <p className="mt-1 max-w-md text-xs text-red-600">{item.lastError}</p>}</td><td className="px-5 py-4">{item.frequency}<p className="text-xs text-muted-foreground">{item.timeOfDay} Ghana time</p></td><td className="px-5 py-4">{when(item.nextExecutionDate)}</td><td className="px-5 py-4">{item.exportFormat}</td><td className="px-5 py-4"><Badge variant={item.status === 'Active' ? 'default' : 'outline'}>{item.status}</Badge></td><td className="px-5 py-4"><div className="flex gap-2"><Button size="sm" variant="outline" disabled={!!working} onClick={() => void act(`${item.id}-run`, () => dataService.runNow(item.id), 'Report generation completed')}><Play className="h-4 w-4" /></Button><Button size="sm" variant="outline" disabled={!!working} onClick={() => toggleStatus(item)}>{item.status === 'Active' ? <Pause className="h-4 w-4" /> : <Play className="h-4 w-4" />}</Button></div></td></tr>)}
    </tbody></table></div></CardContent></Card>

    <Card><CardHeader><CardTitle>Execution and delivery history</CardTitle></CardHeader><CardContent className="p-0"><div className="overflow-x-auto"><table className="w-full text-sm"><thead className="border-y bg-muted/40 text-left"><tr>{['Report', 'Scheduled slot', 'Attempt', 'Result', 'Artifact'].map(item => <th key={item} className="px-5 py-3 font-medium">{item}</th>)}</tr></thead><tbody>
      {!workspace?.recentExecutions.length && <tr><td colSpan={5} className="px-5 py-10 text-center text-muted-foreground">No scheduled execution has run yet.</td></tr>}
      {workspace?.recentExecutions.map(item => <tr key={item.id} className="border-b"><td className="px-5 py-4"><p className="font-medium">{item.scheduleName}</p><p className="text-xs text-muted-foreground">{item.reportName} · {item.trigger}</p></td><td className="px-5 py-4">{when(item.scheduledFor)}</td><td className="px-5 py-4">{item.attemptNumber}</td><td className="px-5 py-4"><Badge variant={item.status === 'Succeeded' ? 'default' : 'outline'}>{item.status}</Badge>{item.errorMessage && <p className="mt-1 max-w-md text-xs text-red-600">{item.errorMessage}</p>}</td><td className="px-5 py-4">{item.exportId && item.fileName ? <Button variant="outline" size="sm" disabled={!!working} onClick={() => { if (item.exportId && item.fileName) void download(item.exportId, item.fileName); }}><Download className="mr-2 h-4 w-4" />{size(item.fileSize)}</Button> : '—'}</td></tr>)}
    </tbody></table></div></CardContent></Card>
  </div></DashboardLayout></TenantGuard>;
}
