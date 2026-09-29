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
import type { Account, AccountingBook } from '@/types/finance';
import { RecurringJournalLineGrid } from '../recurring-journal-line-grid';
import {
  newEditableRecurringJournalLine,
  summarizeRecurringJournalLines,
  toRecurringJournalLineInputs,
  type EditableRecurringJournalLine,
} from '../recurring-journal-lines';

type ScheduleChoice = 'month-end' | 'day-one' | 'semi-monthly';

export default function NewRecurringJournalPage() {
  const router = useRouter();
  const { toast } = useToast();
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [books, setBooks] = useState<AccountingBook[]>([]);
  const [currencyCode, setCurrencyCode] = useState('');
  const [bookClassification, setBookClassification] = useState<CreateRecurringJournalTemplate['bookClassification'] | ''>('');
  const [timeZoneId, setTimeZoneId] = useState('');
  const [saving, setSaving] = useState(false);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [referencePattern, setReferencePattern] = useState('{TemplateNumber}-{Period}-{Sequence}');
  const [lines, setLines] = useState<EditableRecurringJournalLine[]>(() => [
    newEditableRecurringJournalLine('initial-line-1'), newEditableRecurringJournalLine('initial-line-2'),
  ]);
  const [effectiveFrom, setEffectiveFrom] = useState(() => new Date().toISOString().slice(0, 10));
  const [schedule, setSchedule] = useState<ScheduleChoice>('month-end');
  const [autoReverse, setAutoReverse] = useState(false);
  const [submissionReason, setSubmissionReason] = useState('Configured for independent Finance review and controlled activation.');

  useEffect(() => {
    setTimeZoneId(Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC');
    void Promise.all([
      financeDataService.getAccounts({ status: 'Active', take: 2000 }),
      financeDataService.getFinanceSettings(),
      financeDataService.getAccountingBooks(),
    ])
      .then(([accountItems, settings, accountingBooks]) => {
        setAccounts(accountItems.filter(item => item.allowDirectPosting && !item.isControlAccount));
        setCurrencyCode(settings.baseCurrency);
        const supportedCodes = new Set(['IFRS', 'LOCAL_STATUTORY', 'MANAGEMENT']);
        const eligibleBooks = accountingBooks.filter(book =>
          book.isActive && book.allowsPosting && supportedCodes.has(book.code.toUpperCase()));
        setBooks(eligibleBooks);
        const preferred = eligibleBooks.find(book => book.isDefault) ?? eligibleBooks[0];
        if (preferred)
          setBookClassification(preferred.code.toUpperCase() as CreateRecurringJournalTemplate['bookClassification']);
      })
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

  const lineSummary = useMemo(() => summarizeRecurringJournalLines(lines), [lines]);
  const canSubmit = !!name.trim() && !!effectiveFrom && !!bookClassification && !!currencyCode && !!timeZoneId.trim() && submissionReason.trim().length >= 10 && lineSummary.isValid;

  const submit = async () => {
    if (!name.trim() || !effectiveFrom || !bookClassification || !currencyCode || !timeZoneId.trim() || !lineSummary.isValid) {
      toast({ title: 'Complete a balanced journal', description: lineSummary.errors[0] ?? 'Name and effective date are required.', variant: 'destructive' });
      return;
    }
    if (submissionReason.trim().length < 10) {
      toast({ title: 'Add a meaningful submission reason', description: 'The reason must contain at least 10 characters for the audit trail.', variant: 'destructive' });
      return;
    }

    const request: CreateRecurringJournalTemplate = {
      name: name.trim(), description: description.trim() || undefined,
      journalType: 'Recurring', bookClassification, currencyCode,
      referencePattern: referencePattern.trim() || undefined,
      effectiveFrom, timeZoneId: timeZoneId.trim(), frequency: scheduleDefinition.frequency,
      interval: 1, recurrenceRuleJson: scheduleDefinition.recurrenceRuleJson,
      businessDayConvention: scheduleDefinition.convention,
      autoReverse, reversalRule: autoReverse ? 'FirstDayOfNextFiscalPeriod' : 'None',
      lines: toRecurringJournalLineInputs(lines).map(line => ({
        ...line, description: (line.description ?? description.trim()) || name.trim(),
      })),
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

    <Card><CardHeader><CardTitle>Balanced journal definition</CardTitle></CardHeader><CardContent className="space-y-5">
      <div className="grid gap-4 md:grid-cols-3">
        <div><Label>Accounting book</Label><Select value={bookClassification} onValueChange={value => setBookClassification(value as CreateRecurringJournalTemplate['bookClassification'])}><SelectTrigger><SelectValue placeholder="Select an active posting book" /></SelectTrigger><SelectContent>{books.map(book => <SelectItem key={book.id} value={book.code.toUpperCase()}>{book.name}</SelectItem>)}</SelectContent></Select>{books.length === 0 && <p className="mt-1 text-xs text-destructive">No supported active posting book is configured.</p>}</div>
        <div><Label htmlFor="currency">Functional currency</Label><Input id="currency" value={currencyCode} readOnly aria-readonly="true" /></div>
        <div><Label htmlFor="timezone">Schedule time zone</Label><Input id="timezone" value={timeZoneId} onChange={event => setTimeZoneId(event.target.value)} placeholder="Africa/Accra" /></div>
      </div>
      <RecurringJournalLineGrid accounts={accounts} currencyCode={currencyCode || '—'} lines={lines} onChange={setLines} />
    </CardContent></Card>

    <Card><CardHeader><CardTitle>Schedule, reversal and submission</CardTitle></CardHeader><CardContent className="grid gap-5 md:grid-cols-2">
      <div><Label>Schedule</Label><Select value={schedule} onValueChange={value => setSchedule(value as ScheduleChoice)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="month-end">Last business day monthly</SelectItem><SelectItem value="day-one">First business day monthly</SelectItem><SelectItem value="semi-monthly">15th and month-end</SelectItem></SelectContent></Select><p className="mt-1 text-xs text-muted-foreground">{scheduleDefinition.label}</p></div>
      <div><Label htmlFor="effective">Effective from</Label><Input id="effective" type="date" value={effectiveFrom} onChange={event => setEffectiveFrom(event.target.value)} /></div>
      <div className="flex items-center justify-between rounded-lg border p-4 md:col-span-2"><div><p className="font-medium">Automatic reversal</p><p className="text-sm text-muted-foreground">When the occurrence is approved, the checker also authorizes its exact reversing journal to post automatically on the first day of the next tenant fiscal period.</p></div><Switch checked={autoReverse} onCheckedChange={setAutoReverse} /></div>
      <div className="md:col-span-2"><Label htmlFor="reason">Submission reason</Label><Textarea id="reason" value={submissionReason} onChange={event => setSubmissionReason(event.target.value)} /></div>
      <div className="flex items-start gap-3 rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900 md:col-span-2"><ShieldCheck className="mt-0.5 h-5 w-5 shrink-0" /><p>Submitting does not activate or post the template. A different user must approve the standing instruction; every generated occurrence receives a separate approval. For automatic reversal, that occurrence approval explicitly authorizes both the original and its immutable scheduled negation.</p></div>
    </CardContent></Card>

    <div className="flex justify-between"><Button variant="outline" asChild><Link href="/finance/recurring-journals">Cancel</Link></Button><Button onClick={submit} disabled={saving || !canSubmit}>{saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}Create and submit</Button></div>
  </div>;
}
