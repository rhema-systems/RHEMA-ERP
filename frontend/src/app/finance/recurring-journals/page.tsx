'use client';

import { useCallback, useEffect, useState } from 'react';
import Link from 'next/link';
import { AlertTriangle, CalendarClock, CheckCircle2, Loader2, Play, Plus, ShieldCheck, type LucideIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import {
  recurringJournalDataService,
  type RecurringJournalTemplate,
} from '@/services/finance/recurring-journal-data.service';

const label = (value: string) => value.replace(/([a-z])([A-Z])/g, '$1 $2');
const money = (amount: number, currency: string) =>
  new Intl.NumberFormat('en-GH', { style: 'currency', currency }).format(amount);

export default function RecurringJournalsPage() {
  const { toast } = useToast();
  const { hasPermission } = useAuth();
  const canCreate = hasPermission('Finance.JournalEntries.Create');
  const [rows, setRows] = useState<RecurringJournalTemplate[]>([]);
  const [loading, setLoading] = useState(true);
  const [processing, setProcessing] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await recurringJournalDataService.getAll());
    } catch (error) {
      toast({ title: 'Recurring journals could not be loaded', description: error instanceof Error ? error.message : 'Please retry.', variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => { void load(); }, [load]);

  const processDue = async () => {
    setProcessing(true);
    try {
      const result = await recurringJournalDataService.processDue();
      toast({
        title: 'Due schedules processed',
        description: `${result.generatedCount} occurrence(s) created for approval; ${result.existingCount} already existed.`,
      });
      await load();
    } catch (error) {
      toast({ title: 'Schedule processing failed', description: error instanceof Error ? error.message : 'Please retry.', variant: 'destructive' });
    } finally {
      setProcessing(false);
    }
  };

  // The list endpoint returns an aggregate rather than every historical
  // occurrence, keeping this control metric accurate as schedules grow.
  const exceptions = rows.reduce((count, row) => count + row.exceptionCount, 0);
  const stats: Array<{ title: string; value: number; Icon: LucideIcon }> = [
    { title: 'Templates', value: rows.length, Icon: CalendarClock },
    { title: 'Active', value: rows.filter(item => item.status === 'Active').length, Icon: CheckCircle2 },
    { title: 'Awaiting approval', value: rows.filter(item => item.status === 'PendingApproval').length, Icon: ShieldCheck },
    { title: 'Exceptions', value: exceptions, Icon: AlertTriangle },
  ];

  return <div className="space-y-6">
    <div className="flex flex-wrap items-start justify-between gap-4">
      <div>
        <h1 className="flex items-center gap-2 text-3xl font-bold"><CalendarClock className="h-8 w-8" />Recurring Journals</h1>
        <p className="text-muted-foreground">Versioned standing instructions with controlled occurrence approval and Finance posting.</p>
      </div>
      <div className="flex gap-2">
        {canCreate && <Button variant="outline" onClick={processDue} disabled={processing}>
          {processing ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Play className="mr-2 h-4 w-4" />}Process due schedules
        </Button>}
        {canCreate && <Button asChild><Link href="/finance/recurring-journals/new"><Plus className="mr-2 h-4 w-4" />New recurring journal</Link></Button>}
      </div>
    </div>

    <div className="grid gap-4 md:grid-cols-4">{stats.map(({ title, value, Icon }) => <Card key={title}><CardContent className="flex items-center justify-between p-5"><div><p className="text-sm text-muted-foreground">{title}</p><p className="text-2xl font-bold">{value}</p></div><Icon className="h-7 w-7 text-blue-600" /></CardContent></Card>)}</div>

    <Card>
      <CardHeader><CardTitle>Recurring journal templates</CardTitle></CardHeader>
      <CardContent className="p-0">
        <div className="overflow-x-auto"><table className="w-full text-sm">
          <thead className="border-y bg-muted/40 text-left"><tr>{['Template', 'Schedule', 'Next due', 'Amount', 'Status', ''].map(item => <th key={item} className="px-5 py-3 font-medium">{item}</th>)}</tr></thead>
          <tbody>
            {loading && <tr><td colSpan={6} className="px-5 py-10 text-center text-muted-foreground"><Loader2 className="mr-2 inline h-4 w-4 animate-spin" />Loading production templates...</td></tr>}
            {!loading && rows.length === 0 && <tr><td colSpan={6} className="px-5 py-10 text-center text-muted-foreground">No recurring journal has been configured.</td></tr>}
            {rows.map(row => {
              const amount = row.lines.filter(line => line.isDebit).reduce((sum, line) => sum + line.fixedAmount, 0);
              return <tr key={row.id} className="border-b hover:bg-muted/30">
                <td className="px-5 py-4"><Link className="font-semibold text-blue-700 hover:underline" href={`/finance/recurring-journals/${row.id}`}>{row.templateNumber}</Link><div>{row.name}</div><div className="text-xs text-muted-foreground">Version {row.version}</div></td>
                <td className="px-5 py-4"><div>{label(row.frequency)}</div><div className="text-xs text-muted-foreground">Every {row.interval} cycle(s)</div></td>
                <td className="px-5 py-4">{row.nextDueDate ?? '—'}</td>
                <td className="px-5 py-4 font-medium">{money(amount, row.currencyCode)}</td>
                <td className="px-5 py-4"><Badge variant={row.status === 'Active' ? 'default' : 'outline'}>{label(row.status)}</Badge></td>
                <td className="px-5 py-4"><Button variant="outline" size="sm" asChild><Link href={`/finance/recurring-journals/${row.id}`}>View</Link></Button></td>
              </tr>;
            })}
          </tbody>
        </table></div>
      </CardContent>
    </Card>
  </div>;
}
