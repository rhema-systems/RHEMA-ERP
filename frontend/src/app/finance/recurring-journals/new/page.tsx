'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowLeft, Loader2, Save, ShieldCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { financeDataService } from '@/services/finance/finance-data.service';
import {
  recurringJournalDataService,
  type BusinessDayConvention,
  type CreateRecurringJournalTemplate,
  type RecurrenceFrequency,
} from '@/services/finance/recurring-journal-data.service';
import type { Account } from '@/types/finance';

type ScheduleChoice = 'month-end' | 'day-one' | 'semi-monthly';

export default function NewRecurringJournalPage() {
  const router = useRouter();
  const { toast } = useToast();
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [saving, setSaving] = useState(false);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [referencePattern, setReferencePattern] = useState('{TemplateNumber}-{Period}-{Sequence}');
  const [amount, setAmount] = useState('');
  const [debitAccountId, setDebitAccountId] = useState('');
  const [creditAccountId, setCreditAccountId] = useState('');
  const [effectiveFrom, setEffectiveFrom] = useState(() => new Date().toISOString().slice(0, 10));
  const [schedule, setSchedule] = useState<ScheduleChoice>('month-end');
  const [autoReverse, setAutoReverse] = useState(false);
  const [submissionReason, setSubmissionReason] = useState('Configured for independent Finance review and controlled activation.');

  useEffect(() => {
    void financeDataService.getAccounts({ status: 'Active', take: 2000 })
      .then(items => setAccounts(items.filter(item => item.allowDirectPosting && !item.isControlAccount)))
      .catch(error => toast({ title: 'Chart of accounts could not be loaded', description: error instanceof Error ? error.message : 'Please retry.', variant: 'destructive' }));
  }, [toast]);

  const scheduleDefinition = useMemo((): {
    frequency: RecurrenceFrequency;
    recurrenceRuleJson: string;
    convention: BusinessDayConvention;
    label: string;
  } => {
    if (schedule === 'day-one') return {
      frequency: 'Monthly', recurrenceRuleJson: JSON.stringify({ daysOfMonth: [1] }),
      convention: 'NextBusinessDay', label: 'First business day of each month',
    };
    if (schedule === 'semi-monthly') return {
      frequency: 'SemiMonthly', recurrenceRuleJson: JSON.stringify({ daysOfMonth: [15, 31] }),
      convention: 'PreviousBusinessDay', label: '15th and month-end, adjusted backward',
    };
    // Month-end schedules move backward so a weekend/holiday never pushes an
    // accrual into the next fiscal period.
    return {
      frequency: 'Monthly', recurrenceRuleJson: JSON.stringify({ lastCalendarDay: true }),
      convention: 'PreviousBusinessDay', label: 'Last business day of each month',
    };
  }, [schedule]);

  const submit = async () => {
    const fixedAmount = Number(amount);
    if (!name.trim() || !debitAccountId || !creditAccountId || !effectiveFrom || !Number.isFinite(fixedAmount) || fixedAmount <= 0) {
      toast({ title: 'Complete the required fields', description: 'Name, effective date, debit account, credit account and a positive amount are required.', variant: 'destructive' });
      return;
    }
    if (debitAccountId === creditAccountId) {
      toast({ title: 'Select different accounts', description: 'A recurring journal cannot debit and credit the same account.', variant: 'destructive' });
      return;
    }
    if (submissionReason.trim().length < 10) {
      toast({ title: 'Add a meaningful submission reason', description: 'The reason must contain at least 10 characters for the audit trail.', variant: 'destructive' });
      return;
    }

    const request: CreateRecurringJournalTemplate = {
      name: name.trim(), description: description.trim() || undefined,
      journalType: 'Recurring', bookClassification: 'IFRS', currencyCode: 'GHS',
      referencePattern: referencePattern.trim() || undefined,
      effectiveFrom, timeZoneId: 'Africa/Accra', frequency: scheduleDefinition.frequency,
      interval: 1, recurrenceRuleJson: scheduleDefinition.recurrenceRuleJson,
      businessDayConvention: scheduleDefinition.convention,
      autoReverse, reversalRule: autoReverse ? 'FirstDayOfNextFiscalPeriod' : 'None',
      lines: [
        { accountId: debitAccountId, isDebit: true, fixedAmount, description: description.trim() || name.trim(), dimensionValuesJson: '{}' },
        { accountId: creditAccountId, isDebit: false, fixedAmount, description: description.trim() || name.trim(), dimensionValuesJson: '{}' },
      ],
    };

    setSaving(true);
    try {
      const created = await recurringJournalDataService.create(request);
      await recurringJournalDataService.submit(created.id, submissionReason.trim());
      toast({ title: 'Recurring journal submitted', description: `${created.templateNumber} now awaits an independent approver.` });
      router.push(`/finance/recurring-journals/${created.id}`);
    } catch (error) {
      toast({ title: 'Recurring journal was not submitted', description: error instanceof Error ? error.message : 'Please retry.', variant: 'destructive' });
    } finally {
      setSaving(false);
    }
  };

  return <div className="mx-auto max-w-6xl space-y-6">
    <div>
      <Button variant="ghost" asChild className="mb-3"><Link href="/finance/recurring-journals"><ArrowLeft className="mr-2 h-4 w-4" />Recurring journals</Link></Button>
      <h1 className="text-3xl font-bold">New Recurring Journal</h1>
      <p className="text-muted-foreground">Create a balanced standing instruction and send it through maker-checker activation.</p>
    </div>

    <Card><CardHeader><CardTitle>Template intent</CardTitle></CardHeader><CardContent className="grid gap-5 md:grid-cols-2">
      <div><Label htmlFor="name">Name</Label><Input id="name" value={name} onChange={event => setName(event.target.value)} placeholder="Monthly rent accrual" /></div>
      <div><Label htmlFor="reference">Reference pattern</Label><Input id="reference" value={referencePattern} onChange={event => setReferencePattern(event.target.value)} /><p className="mt-1 text-xs text-muted-foreground">Tokens: {'{TemplateNumber} {Period} {ScheduledDate} {Sequence}'}</p></div>
      <div className="md:col-span-2"><Label htmlFor="description">Purpose and accounting background</Label><Textarea id="description" value={description} onChange={event => setDescription(event.target.value)} placeholder="Explain why this entry recurs and what it recognises." /></div>
    </CardContent></Card>

    <Card><CardHeader><CardTitle>Balanced IFRS journal</CardTitle></CardHeader><CardContent className="grid gap-5 md:grid-cols-3">
      <div><Label htmlFor="amount">Fixed amount (GHS)</Label><Input id="amount" type="number" min="0.01" step="0.01" value={amount} onChange={event => setAmount(event.target.value)} /></div>
      <div><Label>Debit account</Label><Select value={debitAccountId} onValueChange={setDebitAccountId}><SelectTrigger><SelectValue placeholder="Select posting account" /></SelectTrigger><SelectContent>{accounts.map(item => <SelectItem key={item.id} value={item.id}>{item.accountCode} · {item.accountName}</SelectItem>)}</SelectContent></Select></div>
      <div><Label>Credit account</Label><Select value={creditAccountId} onValueChange={setCreditAccountId}><SelectTrigger><SelectValue placeholder="Select posting account" /></SelectTrigger><SelectContent>{accounts.map(item => <SelectItem key={item.id} value={item.id}>{item.accountCode} · {item.accountName}</SelectItem>)}</SelectContent></Select></div>
      <div className="rounded-md bg-emerald-50 p-3 text-sm text-emerald-800 md:col-span-3">Debits and credits will use the same fixed amount. The server validates balance and posting-account eligibility again before saving.</div>
    </CardContent></Card>

    <Card><CardHeader><CardTitle>Schedule, reversal and submission</CardTitle></CardHeader><CardContent className="grid gap-5 md:grid-cols-2">
      <div><Label>Schedule</Label><Select value={schedule} onValueChange={value => setSchedule(value as ScheduleChoice)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="month-end">Last business day monthly</SelectItem><SelectItem value="day-one">First business day monthly</SelectItem><SelectItem value="semi-monthly">15th and month-end</SelectItem></SelectContent></Select><p className="mt-1 text-xs text-muted-foreground">{scheduleDefinition.label}</p></div>
      <div><Label htmlFor="effective">Effective from</Label><Input id="effective" type="date" value={effectiveFrom} onChange={event => setEffectiveFrom(event.target.value)} /></div>
      <div className="flex items-center justify-between rounded-lg border p-4 md:col-span-2"><div><p className="font-medium">Automatic reversal</p><p className="text-sm text-muted-foreground">When the occurrence is approved, the checker also authorizes its exact reversing journal to post automatically on the first day of the next tenant fiscal period.</p></div><Switch checked={autoReverse} onCheckedChange={setAutoReverse} /></div>
      <div className="md:col-span-2"><Label htmlFor="reason">Submission reason</Label><Textarea id="reason" value={submissionReason} onChange={event => setSubmissionReason(event.target.value)} /></div>
      <div className="flex items-start gap-3 rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900 md:col-span-2"><ShieldCheck className="mt-0.5 h-5 w-5 shrink-0" /><p>Submitting does not activate or post the template. A different user must approve the standing instruction; every generated occurrence receives a separate approval. For automatic reversal, that occurrence approval explicitly authorizes both the original and its immutable scheduled negation.</p></div>
    </CardContent></Card>

    <div className="flex justify-between"><Button variant="outline" asChild><Link href="/finance/recurring-journals">Cancel</Link></Button><Button onClick={submit} disabled={saving}>{saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}Create and submit</Button></div>
  </div>;
}
