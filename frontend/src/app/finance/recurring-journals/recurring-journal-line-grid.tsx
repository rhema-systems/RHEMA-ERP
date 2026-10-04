'use client';

import { Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import type { Account } from '@/types/finance';
import {
  newEditableRecurringJournalLine,
  formatRecurringJournalAmount,
  summarizeRecurringJournalLines,
  type EditableRecurringJournalLine,
} from './recurring-journal-lines';

interface Props {
  accounts: Account[];
  currencyCode: string;
  lines: EditableRecurringJournalLine[];
  onChange: (lines: EditableRecurringJournalLine[]) => void;
}

export function RecurringJournalLineGrid({ accounts, currencyCode, lines, onChange }: Props) {
  const summary = summarizeRecurringJournalLines(lines);
  const money = (value: number) => formatRecurringJournalAmount(value, currencyCode);
  const update = (key: string, patch: Partial<EditableRecurringJournalLine>) =>
    onChange(lines.map(line => line.key === key ? { ...line, ...patch } : line));

  return <div className="space-y-4">
    <div className="overflow-x-auto rounded-lg border">
      <table className="w-full min-w-[900px] text-sm">
        <thead className="bg-muted/50 text-left"><tr>
          <th className="p-3">#</th><th className="p-3">Posting account</th><th className="p-3">Description</th>
          <th className="p-3 text-right">Debit</th><th className="p-3 text-right">Credit</th><th className="w-14 p-3"><span className="sr-only">Remove</span></th>
        </tr></thead>
        <tbody>{lines.map((line, index) => <tr className="border-t" key={line.key}>
          <td className="p-3 align-top text-muted-foreground">{index + 1}</td>
          <td className="p-3 align-top"><Label className="sr-only">Line {index + 1} posting account</Label><Select value={line.accountId} onValueChange={accountId => update(line.key, { accountId })}><SelectTrigger><SelectValue placeholder="Select posting account" /></SelectTrigger><SelectContent>{accounts.map(account => <SelectItem key={account.id} value={account.id}>{account.accountCode} · {account.accountName}</SelectItem>)}</SelectContent></Select></td>
          <td className="p-3 align-top"><Label className="sr-only" htmlFor={`line-description-${line.key}`}>Line {index + 1} description</Label><Input id={`line-description-${line.key}`} value={line.description} onChange={event => update(line.key, { description: event.target.value })} placeholder="Line description" /></td>
          <td className="p-3 align-top"><Label className="sr-only" htmlFor={`line-debit-${line.key}`}>Line {index + 1} debit</Label><Input className="text-right" id={`line-debit-${line.key}`} type="number" min="0" step="0.01" value={line.debitAmount} onChange={event => update(line.key, { debitAmount: event.target.value, creditAmount: event.target.value ? '' : line.creditAmount })} placeholder="0.00" /></td>
          <td className="p-3 align-top"><Label className="sr-only" htmlFor={`line-credit-${line.key}`}>Line {index + 1} credit</Label><Input className="text-right" id={`line-credit-${line.key}`} type="number" min="0" step="0.01" value={line.creditAmount} onChange={event => update(line.key, { creditAmount: event.target.value, debitAmount: event.target.value ? '' : line.debitAmount })} placeholder="0.00" /></td>
          <td className="p-3 align-top"><Button aria-label={`Remove line ${index + 1}`} size="icon" type="button" variant="ghost" onClick={() => onChange(lines.filter(item => item.key !== line.key))}><Trash2 className="h-4 w-4" /></Button></td>
        </tr>)}</tbody>
      </table>
    </div>
    <Button type="button" variant="outline" onClick={() => onChange([...lines, newEditableRecurringJournalLine()])}><Plus className="mr-2 h-4 w-4" />Add line</Button>
    <div className="grid gap-3 sm:grid-cols-4">
      <div className="rounded-md border p-3"><p className="text-xs text-muted-foreground">Total debit</p><p className="font-semibold">{money(summary.totalDebit)}</p></div>
      <div className="rounded-md border p-3"><p className="text-xs text-muted-foreground">Total credit</p><p className="font-semibold">{money(summary.totalCredit)}</p></div>
      <div className="rounded-md border p-3"><p className="text-xs text-muted-foreground">Difference</p><p className="font-semibold">{money(summary.difference)}</p></div>
      <div className="flex items-center rounded-md border p-3"><Badge variant={summary.isBalanced ? 'default' : 'destructive'}>{summary.isBalanced ? 'Balanced' : 'Unbalanced'}</Badge></div>
    </div>
    {!summary.isValid && <ul className="list-disc space-y-1 pl-5 text-sm text-destructive">{summary.errors.map(error => <li key={error}>{error}</li>)}</ul>}
    <p className="text-xs text-muted-foreground">Only active, directly postable, non-control accounts are available. Dimension and source-line evidence remain attached to each stable line identity.</p>
  </div>;
}
