'use client';

/**
 * What Finance holds for one HR source document (a medical claim, a travel claim, an advance) —
 * HR finish plan lane 8. One row per money event: Posted with its journal number, Unposted because
 * the rule is off, Failed with Finance's reason, Skipped, or Reversed.
 *
 * ⚠ Reads `api/hr/finance-posting/records/source/{id}` (HR.Company.Read). Retry and reverse are
 * HR.Company.Admin: a reversal creates a Finance journal, so the buttons are shown only to
 * administrators and 403 for anyone else regardless.
 *
 * The card says nothing when no rule has ever fired for the document — a draft claim has no
 * accounting yet, and an empty card would only invite the question "why is Finance empty?".
 */

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Landmark, RefreshCw, Undo2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { HR_ADMIN_ROLES } from '@/components/hr/common/PermissionGate';
import { financePostingService } from '@/services/hr/finance-posting.service';
import type { HrFinancePostingRecord, HrFinancePostingStatus } from '@/types/hr/finance-posting';

export const financePostingSourceKey = (sourceDocumentId: string) =>
  ['hr', 'finance-posting', 'source', sourceDocumentId] as const;

const STATUS_VARIANT: Record<HrFinancePostingStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Posted: 'default',
  Failed: 'destructive',
  Unposted: 'secondary',
  Skipped: 'outline',
  Reversed: 'outline',
};

export function fmtPostingMoney(amount: number, currency: string) {
  try {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency, minimumFractionDigits: 2 }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(2)}`;
  }
}

const fmtDate = (value?: string | null) => (value ? new Date(value).toLocaleDateString() : '—');

export function FinancePostingStatusBadge({ record }: { record: HrFinancePostingRecord }) {
  return (
    <Badge variant={STATUS_VARIANT[record.status] ?? 'outline'} title={record.statusReason ?? undefined}>
      {record.status}
      {record.journalEntryNumber ? ` · ${record.journalEntryNumber}` : ''}
    </Badge>
  );
}

/**
 * A one-glance summary for table rows (the advances table): the latest row's status, or nothing.
 */
export function FinancePostingInlineStatus({ sourceDocumentId }: { sourceDocumentId: string }) {
  const { data } = useQuery({
    queryKey: financePostingSourceKey(sourceDocumentId),
    queryFn: () => financePostingService.getRecordsForSource(sourceDocumentId),
    staleTime: 30_000,
  });
  if (!data || data.length === 0) return <span className="text-xs text-muted-foreground">—</span>;
  const latest = data[data.length - 1];
  return <FinancePostingStatusBadge record={latest} />;
}

interface Props {
  sourceDocumentId: string;
  /** Extra react-query keys to refresh after a retry or reversal (the parent document). */
  invalidateKeys?: readonly (readonly unknown[])[];
  className?: string;
}

export function FinancePostingCard({ sourceDocumentId, invalidateKeys = [], className }: Props) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyPermission, hasAnyRole } = useAuth();
  const isAdmin = hasAnyPermission(['HR.Company.Admin']) || hasAnyRole(HR_ADMIN_ROLES);

  const [reversing, setReversing] = useState<HrFinancePostingRecord | null>(null);
  const [reason, setReason] = useState('');

  const key = financePostingSourceKey(sourceDocumentId);
  const { data: records = [], isLoading } = useQuery({
    queryKey: key,
    queryFn: () => financePostingService.getRecordsForSource(sourceDocumentId),
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: key });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'finance-posting'] });
    for (const k of invalidateKeys) await queryClient.invalidateQueries({ queryKey: [...k] });
  };

  const retry = useMutation({
    mutationFn: (id: string) => financePostingService.retry(id),
    onSuccess: async (r) => {
      toast({
        title: r.status === 'Posted' ? `Posted as ${r.journalEntryNumber}` : `Recorded as ${r.status}`,
        description: r.statusReason ?? undefined,
      });
      await refresh();
    },
    onError: (e: Error) => toast({ variant: 'destructive', title: 'Finance did not accept the posting', description: e.message }),
  });

  const reverse = useMutation({
    mutationFn: ({ id, why }: { id: string; why: string }) => financePostingService.reverse(id, why),
    onSuccess: async (r) => {
      toast({ title: `Reversed as ${r.reversalJournalEntryNumber ?? 'a Finance reversal'}` });
      setReversing(null);
      setReason('');
      await refresh();
    },
    onError: (e: Error) => toast({ variant: 'destructive', title: 'Could not reverse', description: e.message }),
  });

  if (isLoading || records.length === 0) return null;

  return (
    <Card className={className}>
      <CardHeader className="flex flex-row items-center justify-between gap-4 pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <Landmark className="h-4 w-4" aria-hidden />
          Finance
        </CardTitle>
        <span className="text-xs text-muted-foreground">
          {records.filter((r) => r.status === 'Posted').length} of {records.length} event
          {records.length === 1 ? '' : 's'} in the ledger
        </span>
      </CardHeader>
      <CardContent className="space-y-3">
        {records.map((r) => (
          <div key={r.id} className="rounded-md border p-3 text-sm">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-medium">{r.eventName}</span>
                <FinancePostingStatusBadge record={r} />
              </div>
              <span className="whitespace-nowrap">{fmtPostingMoney(r.amount, r.currencyCode)}</span>
            </div>
            <div className="mt-1 text-xs text-muted-foreground">
              {r.status === 'Posted' && <>Posted {fmtDate(r.postedAt)} · book {r.accountingBookCode} · dated {fmtDate(r.postingDate)}</>}
              {r.status === 'Reversed' && (
                <>Reversed {fmtDate(r.reversedAt)} as {r.reversalJournalEntryNumber ?? '—'}: {r.reversalReason}</>
              )}
              {r.status !== 'Posted' && r.status !== 'Reversed' && r.statusReason}
              {r.transactionCurrencyCode && r.transactionAmount != null && (
                <> · original {fmtPostingMoney(r.transactionAmount, r.transactionCurrencyCode)}</>
              )}
            </div>
            {r.lines.length > 0 && (
              <ul className="mt-2 space-y-0.5 text-xs">
                {r.lines.map((l, i) => (
                  <li key={i} className="flex justify-between gap-3">
                    <span className="truncate">
                      {l.debit > 0 ? 'Dr' : 'Cr'} {l.accountCode} {l.accountName}
                    </span>
                    <span className="whitespace-nowrap">{fmtPostingMoney(l.debit > 0 ? l.debit : l.credit, r.currencyCode)}</span>
                  </li>
                ))}
              </ul>
            )}
            {isAdmin && (r.canRetry || r.canReverse) && (
              <div className="mt-2 flex gap-2">
                {r.canRetry && (
                  <Button size="sm" variant="outline" disabled={retry.isPending} onClick={() => retry.mutate(r.id)}>
                    <RefreshCw className="mr-1 h-3 w-3" /> Post now
                  </Button>
                )}
                {r.canReverse && (
                  <Button size="sm" variant="ghost" onClick={() => setReversing(r)}>
                    <Undo2 className="mr-1 h-3 w-3" /> Reverse
                  </Button>
                )}
              </div>
            )}
          </div>
        ))}
      </CardContent>

      <Dialog open={!!reversing} onOpenChange={(o) => !o && setReversing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reverse {reversing?.journalEntryNumber} in Finance</DialogTitle>
          </DialogHeader>
          <p className="text-sm text-muted-foreground">
            Finance posts an exact reversal of {reversing?.eventName.toLowerCase()} for {reversing?.sourceReference}.
            The claim itself is not changed; its edit locks lift once the row is no longer posted.
          </p>
          <div className="space-y-2">
            <Label htmlFor="reversal-reason">Reason</Label>
            <Textarea
              id="reversal-reason"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Why the posting is wrong — at least five characters"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setReversing(null)}>Cancel</Button>
            <Button
              variant="destructive"
              disabled={reason.trim().length < 5 || reverse.isPending}
              onClick={() => reversing && reverse.mutate({ id: reversing.id, why: reason.trim() })}
            >
              Reverse
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
