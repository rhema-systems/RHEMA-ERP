'use client';
import { useEffect, useState } from 'react';
import Link from 'next/link';
import { CalendarClock, Plus, ShieldCheck, AlertTriangle, CheckCircle2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { getDemoTemplates, type DemoRecurringJournal } from '@/lib/finance/recurring-journal-demo';

export default function RecurringJournalsPage() {
  const [rows, setRows] = useState<DemoRecurringJournal[]>([]);
  useEffect(() => setRows(getDemoTemplates()), []);
  const money = (n:number) => new Intl.NumberFormat('en-GH',{style:'currency',currency:'GHS'}).format(n);
  return <div className="space-y-6">
    <div className="flex flex-wrap items-start justify-between gap-4"><div><div className="mb-2 flex items-center gap-2"><Badge variant="outline" className="border-blue-300 text-blue-700">Demo Preview</Badge><span className="text-xs text-muted-foreground">Browser-local data · no GL posting</span></div><h1 className="flex items-center gap-2 text-3xl font-bold"><CalendarClock className="h-8 w-8"/>Recurring Journals</h1><p className="text-muted-foreground">Configure standing journals, review schedules and monitor generated occurrences.</p></div><Button asChild><Link href="/finance/recurring-journals/new"><Plus className="mr-2 h-4 w-4"/>New recurring journal</Link></Button></div>
    <div className="grid gap-4 md:grid-cols-4">{[
      ['Templates',rows.length,CalendarClock],['Active',rows.filter(x=>x.status==='Active').length,CheckCircle2],['Awaiting approval',rows.filter(x=>x.status==='Pending Approval').length,ShieldCheck],['Exceptions',0,AlertTriangle]
    ].map(([label,value,Icon]:any)=><Card key={label}><CardContent className="flex items-center justify-between p-5"><div><p className="text-sm text-muted-foreground">{label}</p><p className="text-2xl font-bold">{value}</p></div><Icon className="h-7 w-7 text-blue-600"/></CardContent></Card>)}</div>
    <Card><CardHeader><CardTitle>Recurring journal templates</CardTitle></CardHeader><CardContent className="p-0"><div className="overflow-x-auto"><table className="w-full text-sm"><thead className="border-y bg-muted/40 text-left"><tr>{['Template','Schedule','Next due','Amount','Owner','Status',''].map(x=><th key={x} className="px-5 py-3 font-medium">{x}</th>)}</tr></thead><tbody>{rows.map(r=><tr key={r.id} className="border-b hover:bg-muted/30"><td className="px-5 py-4"><Link className="font-semibold text-blue-700 hover:underline" href={`/finance/recurring-journals/${r.id}`}>{r.templateNumber}</Link><div>{r.name}</div></td><td className="px-5 py-4"><div>{r.frequency}</div><div className="text-xs text-muted-foreground">{r.schedule}</div></td><td className="px-5 py-4">{new Date(r.nextDue+'T00:00:00').toLocaleDateString('en-GB',{day:'2-digit',month:'short',year:'numeric'})}</td><td className="px-5 py-4 font-medium">{money(r.amount)}</td><td className="px-5 py-4">{r.owner}</td><td className="px-5 py-4"><Badge variant={r.status==='Active'?'default':'outline'}>{r.status}</Badge></td><td className="px-5 py-4"><Button variant="outline" size="sm" asChild><Link href={`/finance/recurring-journals/${r.id}`}>View</Link></Button></td></tr>)}</tbody></table></div></CardContent></Card>
  </div>;
}
