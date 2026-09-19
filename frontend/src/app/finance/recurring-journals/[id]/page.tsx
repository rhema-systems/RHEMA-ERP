'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { ArrowLeft, CalendarClock, FileText, Loader2, Pause, Pencil, Play, RotateCcw, Save, ShieldCheck, ThumbsDown, ThumbsUp, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { financeDataService } from '@/services/finance/finance-data.service';
import {
  recurringJournalDataService,
  type RecurringJournalTemplate,
} from '@/services/finance/recurring-journal-data.service';
import type { Account } from '@/types/finance';
import { RecurringJournalLineGrid } from '../recurring-journal-line-grid';
import {
  editRecurringJournalLine,
  summarizeRecurringJournalLines,
  toRecurringJournalLineInputs,
  type EditableRecurringJournalLine,
} from '../recurring-journal-lines';

const label = (value: string) => value.replace(/([a-z])([A-Z])/g, '$1 $2');

export default function RecurringJournalDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { toast } = useToast();
  const { hasPermission } = useAuth();
  const canRetryReversal = hasPermission('Finance.JournalEntries.Post');
  const [item, setItem] = useState<RecurringJournalTemplate>();
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [editingLines, setEditingLines] = useState<EditableRecurringJournalLine[]>();
  const [loading, setLoading] = useState(true);
  const [working, setWorking] = useState<string>();
  const [reason, setReason] = useState('Reviewed against the supporting recurring accounting instruction.');

  const load = useCallback(async () => {
    setLoading(true);
    try { setItem(await recurringJournalDataService.get(id)); }
    catch (error) { toast({ title: 'Recurring journal could not be loaded', description: error instanceof Error ? error.message : 'Please retry.', variant: 'destructive' }); }
    finally { setLoading(false); }
  }, [id, toast]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    void financeDataService.getAccounts({ status: 'Active', take: 2000 })
      .then(items => setAccounts(items.filter(account => account.allowDirectPosting && !account.isControlAccount)))
      .catch(error => toast({ title: 'Chart of accounts could not be loaded', description: error instanceof Error ? error.message : 'Please retry.', variant: 'destructive' }));
  }, [toast]);

  const editSummary = useMemo(
    () => editingLines ? summarizeRecurringJournalLines(editingLines) : undefined,
    [editingLines]
  );

  const run = async (key: string, action: () => Promise<unknown>, success: string) => {
    if (reason.trim().length < 10 && key !== 'post' && !key.startsWith('retry-')) {
      toast({ title: 'Add a meaningful decision reason', description: 'At least 10 characters are required for the Finance audit trail.', variant: 'destructive' });
      return;
    }
    setWorking(key);
    try { await action(); toast({ title: success }); await load(); }
    catch (error) { toast({ title: 'Action could not be completed', description: error instanceof Error ? error.message : 'Please retry.', variant: 'destructive' }); }
    finally { setWorking(undefined); }
  };

  const saveLines = async () => {
    if (!item || !editingLines || !editSummary?.isValid) return;
    setWorking('save-lines');
    try {
      await recurringJournalDataService.update(item.id, {
        name: item.name, description: item.description, journalType: item.journalType,
        bookClassification: item.bookClassification, currencyCode: item.currencyCode,
        referencePattern: item.referencePattern, notes: item.notes, ownerUserId: item.ownerUserId,
        effectiveFrom: item.effectiveFrom, endDate: item.endDate, maximumOccurrences: item.maximumOccurrences,
        timeZoneId: item.timeZoneId, frequency: item.frequency, interval: item.interval,
        recurrenceRuleJson: item.recurrenceRuleJson, businessDayConvention: item.businessDayConvention,
        autoReverse: item.autoReverse, reversalRule: item.reversalRule,
        reversalDayOffset: item.reversalDayOffset, rowVersion: item.rowVersion,
        lines: toRecurringJournalLineInputs(editingLines),
      });
      setEditingLines(undefined);
      toast({ title: 'Recurring-journal lines updated' });
      await load();
    } catch (error) {
      toast({ title: 'Journal lines could not be updated', description: error instanceof Error ? error.message : 'Please retry.', variant: 'destructive' });
    } finally { setWorking(undefined); }
  };

  if (loading) return <div className="p-8 text-muted-foreground"><Loader2 className="mr-2 inline h-4 w-4 animate-spin" />Loading recurring journal...</div>;
  if (!item) return <div className="p-8">Recurring journal not found.</div>;

  const total = item.lines.filter(line => line.isDebit).reduce((sum, line) => sum + line.fixedAmount, 0);
  const formatMoney = (value: number) => new Intl.NumberFormat('en-GH', { style: 'currency', currency: item.currencyCode }).format(value);

  return <div className="space-y-6">
    <div>
      <Button variant="ghost" asChild className="mb-3"><Link href="/finance/recurring-journals"><ArrowLeft className="mr-2 h-4 w-4" />Recurring journals</Link></Button>
      <div className="flex flex-wrap items-start justify-between gap-3"><div><h1 className="text-3xl font-bold">{item.name}</h1><p className="text-muted-foreground">{item.templateNumber} · Version {item.version}</p></div><Badge className="text-sm" variant={item.status === 'Active' ? 'default' : 'outline'}>{label(item.status)}</Badge></div>
    </div>

    <div className="grid gap-4 md:grid-cols-4">{[
      ['Total debit', formatMoney(total)], ['Schedule', label(item.frequency)],
      ['Next due', item.nextDueDate ?? 'Not activated'], ['Generated', String(item.generatedOccurrenceCount)],
    ].map(([title, value]) => <Card key={title}><CardContent className="p-5"><p className="text-xs text-muted-foreground">{title}</p><p className="mt-1 font-semibold">{value}</p></CardContent></Card>)}</div>

    <Card><CardHeader><CardTitle className="flex items-center gap-2"><ShieldCheck className="h-5 w-5" />Controlled actions</CardTitle></CardHeader><CardContent className="space-y-4">
      <div><Label htmlFor="decision-reason">Decision / operational reason</Label><Textarea id="decision-reason" value={reason} onChange={event => setReason(event.target.value)} /></div>
      <div className="flex flex-wrap gap-2">
        {(item.status === 'Draft' || item.status === 'Rejected') && !editingLines && <Button variant="outline" onClick={() => setEditingLines(item.lines.map(editRecurringJournalLine))} disabled={!!working}><Pencil className="mr-2 h-4 w-4" />Edit journal lines</Button>}
        {item.status === 'PendingApproval' && <><Button onClick={() => run('approve-template', () => recurringJournalDataService.approve(item.id, reason), 'Template activated')} disabled={!!working}><ThumbsUp className="mr-2 h-4 w-4" />Approve template</Button><Button variant="destructive" onClick={() => run('reject-template', () => recurringJournalDataService.reject(item.id, reason), 'Template rejected')} disabled={!!working}><ThumbsDown className="mr-2 h-4 w-4" />Reject template</Button></>}
        {item.status === 'Active' && <Button variant="outline" onClick={() => run('pause', () => recurringJournalDataService.pause(item.id, reason), 'Template paused')} disabled={!!working}><Pause className="mr-2 h-4 w-4" />Pause schedule</Button>}
        {item.status === 'Paused' && <Button onClick={() => run('resume', () => recurringJournalDataService.resume(item.id, reason), 'Template resumed')} disabled={!!working}><Play className="mr-2 h-4 w-4" />Resume schedule</Button>}
        {!!working && <Loader2 className="h-5 w-5 animate-spin self-center" />}
      </div>
      <p className="text-xs text-muted-foreground">Permissions and maker-checker separation are enforced by the API. A template maker cannot approve it or its generated accounting events.</p>
    </CardContent></Card>

    {editingLines && <Card><CardHeader><CardTitle>Edit balanced journal lines</CardTitle></CardHeader><CardContent className="space-y-4">
      <RecurringJournalLineGrid accounts={accounts} currencyCode={item.currencyCode} lines={editingLines} onChange={setEditingLines} />
      <div className="flex justify-end gap-2"><Button type="button" variant="outline" onClick={() => setEditingLines(undefined)} disabled={!!working}><X className="mr-2 h-4 w-4" />Cancel</Button><Button type="button" onClick={saveLines} disabled={!!working || !editSummary?.isValid}>{working === 'save-lines' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}Save lines</Button></div>
    </CardContent></Card>}

    <div className="grid gap-6 lg:grid-cols-3">
      <Card className="lg:col-span-2"><CardHeader><CardTitle className="flex items-center gap-2"><FileText className="h-5 w-5" />Generated occurrences</CardTitle></CardHeader><CardContent className="p-0"><div className="overflow-x-auto"><table className="w-full text-sm">
        <thead className="border-y bg-muted/40"><tr><th className="p-3 text-left">Scheduled / effective</th><th>Status</th><th>Journals</th><th>Automatic reversal</th><th className="text-right">Action</th></tr></thead>
        <tbody>
          {item.occurrences.length === 0 && <tr><td colSpan={5} className="p-8 text-center text-muted-foreground">No due occurrence has been generated.</td></tr>}
          {item.occurrences.map(occurrence => <tr className="border-b" key={occurrence.id}>
            <td className="p-4"><div>{occurrence.scheduledDate}</div><div className="text-xs text-muted-foreground">Effective {occurrence.effectiveDate}{occurrence.adjustmentExplanation ? ` · ${occurrence.adjustmentExplanation}` : ''}</div></td>
            <td className="text-center"><Badge variant={occurrence.status === 'Posted' ? 'default' : 'outline'}>{label(occurrence.status)}</Badge>{occurrence.errorMessage && <div className="mt-1 max-w-xs text-xs text-destructive">{occurrence.errorMessage}</div>}</td>
            <td className="p-3 text-center"><div>{occurrence.journalEntryId ? <Link className="text-blue-700 hover:underline" href={`/finance/journal-entries/${occurrence.journalEntryId}`}>Original journal</Link> : '—'}</div>{occurrence.postedAt && <div className="text-xs text-muted-foreground">Posted {new Date(occurrence.postedAt).toLocaleString()}</div>}{occurrence.reversalJournalEntryId && <div><Link className="text-blue-700 hover:underline" href={`/finance/journal-entries/${occurrence.reversalJournalEntryId}`}>Reversal journal</Link></div>}</td>
            <td className="p-3 text-center">{occurrence.reversalDueDate ? <div className="space-y-1"><div>Due {occurrence.reversalDueDate}</div><Badge variant={occurrence.reversalStatus === 'Posted' ? 'default' : occurrence.reversalStatus === 'Failed' ? 'destructive' : 'outline'}>{label(occurrence.reversalStatus)}</Badge>{occurrence.reversedAt && <div className="text-xs text-muted-foreground">Reversed {new Date(occurrence.reversedAt).toLocaleString()}</div>}{occurrence.reversalAttemptCount > 0 && <div className="text-xs text-muted-foreground">Attempts {occurrence.reversalAttemptCount}{occurrence.reversalLastAttemptAt ? ` · ${new Date(occurrence.reversalLastAttemptAt).toLocaleString()}` : ''}</div>}{occurrence.reversalError && <div className="max-w-xs text-xs text-destructive">{occurrence.reversalError}</div>}</div> : 'Not configured'}</td>
            <td className="p-3 text-right"><div className="flex justify-end gap-2">
              {occurrence.status === 'PendingApproval' && <><Button size="sm" onClick={() => run(`approve-${occurrence.id}`, () => recurringJournalDataService.approveOccurrence(occurrence.id, reason), occurrence.reversalDueDate ? 'Occurrence and exact automatic reversal authorized' : 'Occurrence approved')} disabled={!!working}>{occurrence.reversalDueDate ? 'Approve + authorize reversal' : 'Approve'}</Button><Button size="sm" variant="destructive" onClick={() => run(`reject-${occurrence.id}`, () => recurringJournalDataService.rejectOccurrence(occurrence.id, reason), 'Occurrence rejected')} disabled={!!working}>Reject</Button></>}
              {(occurrence.status === 'Approved' || occurrence.status === 'SubmissionFailed') && <Button size="sm" onClick={() => run('post', () => recurringJournalDataService.postOccurrence(occurrence.id), 'Occurrence posted')} disabled={!!working}>Post to GL</Button>}
              {canRetryReversal && occurrence.status === 'Posted' && occurrence.reversalStatus === 'Failed' && <Button size="sm" variant="outline" onClick={() => run(`retry-${occurrence.id}`, () => recurringJournalDataService.retryReversal(occurrence.id), 'Automatic reversal retry processed')} disabled={!!working}><RotateCcw className="mr-1 h-4 w-4" />Retry reversal</Button>}
            </div></td>
          </tr>)}
        </tbody>
      </table></div></CardContent></Card>

      <div className="space-y-4">
        <Card><CardHeader><CardTitle className="flex items-center gap-2"><CalendarClock className="h-5 w-5" />Standing instruction</CardTitle></CardHeader><CardContent className="space-y-3 text-sm"><p><b>Frequency:</b> {label(item.frequency)}</p><p><b>Business day:</b> {label(item.businessDayConvention)}</p><p><b>Effective from:</b> {item.effectiveFrom}</p><p><b>Automatic reversal:</b> {item.autoReverse ? label(item.reversalRule) : 'No'}</p>{item.autoReverse && <p className="text-xs text-muted-foreground">Occurrence approval authorizes the exact reversing journal to post automatically on its scheduled date. Any changed value requires the ordinary controlled correction workflow.</p>}<p><b>Reference:</b> {item.referencePattern ?? 'System default'}</p></CardContent></Card>
        <Card><CardHeader><CardTitle>Journal lines</CardTitle></CardHeader><CardContent className="space-y-3 text-sm">{item.lines.map(line => <div key={line.id} className="rounded border p-3"><p className="font-medium">{line.accountCode} · {line.accountName}</p><p>{line.isDebit ? 'Debit' : 'Credit'} {formatMoney(line.fixedAmount)}</p></div>)}</CardContent></Card>
      </div>
    </div>
  </div>;
}
