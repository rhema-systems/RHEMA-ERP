'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  BadgeCheck,
  Clock,
  HandCoins,
  Scale,
  ShieldCheck,
  TriangleAlert,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatMoney, today } from '@/lib/hr/attendance-format';
import { trainingServiceBondService } from '@/services/hr/outcomes.service';
import type { TrainingServiceBond } from '@/types/hr/outcomes';

/**
 * Training service bonds — the enforcement side of a sponsored training nomination.
 *
 * The employer pays for the training; the employee agrees to stay for a set number of months.
 * Leaving early breaches the bond and owes a pro-rated repayment, which HR then settles or
 * waives.
 *
 * **The pending-acceptance queue is the one that matters day to day.** A bond nobody has
 * accepted is not yet binding: the obligation period has not started, so the clock is not
 * running and there is nothing to enforce if the employee leaves.
 */
type Filter = 'pending' | 'active' | 'owing' | 'all';

type BondAction = 'accept' | 'exit' | 'waive' | 'settle';

export default function ServiceBondsPage() {
  const [filter, setFilter] = useState<Filter>('pending');
  const [action, setAction] = useState<{ bond: TrainingServiceBond; kind: BondAction } | null>(null);
  const [notes, setNotes] = useState('');
  const [actionDate, setActionDate] = useState(today());
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'service-bonds'],
    queryFn: () => trainingServiceBondService.getAll(),
  });

  const rows = useMemo(() => data ?? [], [data]);

  const stats = useMemo(
    () => ({
      pending: rows.filter((b) => b.status === 'PendingAcceptance').length,
      active: rows.filter((b) => b.status === 'Active').length,
      owing: rows.filter((b) => b.status === 'Breached').length,
      exposure: rows
        .filter((b) => b.status === 'Active')
        .reduce((sum, b) => sum + (b.bondAmount ?? 0), 0),
    }),
    [rows],
  );

  const filtered = useMemo(() => {
    switch (filter) {
      case 'pending':
        return rows.filter((b) => b.status === 'PendingAcceptance');
      case 'active':
        return rows.filter((b) => b.status === 'Active');
      case 'owing':
        return rows.filter((b) => b.status === 'Breached');
      default:
        return rows;
    }
  }, [rows, filter]);

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'service-bonds'] });

  const close = () => {
    setAction(null);
    setNotes('');
    setActionDate(today());
  };

  const run = useMutation({
    // The target is passed in rather than read off state, so the call cannot be made without one.
    mutationFn: ({ bond, kind }: { bond: TrainingServiceBond; kind: BondAction }) => {
      switch (kind) {
        case 'accept':
          return trainingServiceBondService.acceptOnBehalf(bond.id, notes.trim() || undefined);
        case 'exit':
          return trainingServiceBondService.recordExit(bond.id, actionDate, notes.trim() || undefined);
        case 'waive':
          return trainingServiceBondService.waive(bond.id, notes.trim());
        case 'settle':
          return trainingServiceBondService.settle(bond.id, actionDate, notes.trim() || undefined);
      }
    },
    onSuccess: (bond) => {
      toast({
        title: 'Bond updated',
        description:
          bond.status === 'Breached' && bond.repaymentAmount
            ? `Repayment owed: ${formatMoney(bond.repaymentAmount, bond.currency)}.`
            : `Now ${bond.statusName}.`,
      });
      close();
      refresh();
    },
    onError: (e: Error) =>
      toast({ title: 'Could not update the bond', description: e.message, variant: 'destructive' }),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Training service bonds"
        description="Service obligations attached to sponsored training — who owes time, who owes money, and what has been settled."
        backHref="/hr"
      />

      <MetricTiles
        tiles={[
          {
            label: 'Awaiting acceptance',
            value: stats.pending,
            hint: 'Not binding until accepted',
            icon: Clock,
            tone: stats.pending > 0 ? 'warning' : 'default',
          },
          { label: 'Running', value: stats.active, icon: ShieldCheck },
          {
            label: 'Breached',
            value: stats.owing,
            hint: 'Repayment owed',
            icon: TriangleAlert,
            tone: stats.owing > 0 ? 'danger' : 'default',
          },
          {
            label: 'Bonded value',
            value: formatMoney(stats.exposure),
            hint: 'Across running bonds',
            icon: HandCoins,
          },
        ]}
      />

      <Tabs value={filter} onValueChange={(v) => setFilter(v as Filter)}>
        <TabsList>
          <TabsTrigger value="pending">Awaiting acceptance ({stats.pending})</TabsTrigger>
          <TabsTrigger value="active">Running ({stats.active})</TabsTrigger>
          <TabsTrigger value="owing">Breached ({stats.owing})</TabsTrigger>
          <TabsTrigger value="all">All ({rows.length})</TabsTrigger>
        </TabsList>
      </Tabs>

      <Card>
        <CardContent className="p-0">
          {isError ? (
            <EmptyState
              icon={TriangleAlert}
              title="Could not load service bonds"
              description={(error as Error)?.message ?? 'Try again in a moment.'}
            />
          ) : isLoading ? (
            <div className="space-y-2 p-4">
              {[0, 1, 2].map((i) => (
                <Skeleton key={i} className="h-12 w-full" />
              ))}
            </div>
          ) : filtered.length === 0 ? (
            <EmptyState
              icon={Scale}
              title="No bonds here"
              description={
                filter === 'all'
                  ? 'No training nomination has a service bond attached yet.'
                  : 'Nothing matches this filter.'
              }
            />
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Employee</TableHead>
                    <TableHead>Programme</TableHead>
                    <TableHead className="text-right">Bond</TableHead>
                    <TableHead>Obligation</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="text-right">Owed</TableHead>
                    <TableHead className="w-48" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {filtered.map((bond) => (
                    <TableRow key={bond.id}>
                      <TableCell>
                        <div className="font-medium">{bond.employeeName ?? '—'}</div>
                        <div className="text-xs text-muted-foreground">
                          {bond.nominationNumber}
                        </div>
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {bond.programName ?? '—'}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {formatMoney(bond.bondAmount, bond.currency)}
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {bond.bondDurationMonths} months
                        {bond.bondStartDate && (
                          <div className="text-xs">
                            {formatDate(bond.bondStartDate)} – {formatDate(bond.bondEndDate)}
                          </div>
                        )}
                        {bond.status === 'Active' && (
                          <div className="text-xs">{bond.monthsRemaining} months left</div>
                        )}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={bond.status} />
                        {bond.acceptedDate && (
                          <div className="mt-1 text-xs text-muted-foreground">
                            Accepted {formatDate(bond.acceptedDate)}
                          </div>
                        )}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {bond.repaymentAmount != null
                          ? formatMoney(bond.repaymentAmount, bond.currency)
                          : '—'}
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-1">
                          {bond.status === 'PendingAcceptance' && (
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() => setAction({ bond, kind: 'accept' })}
                            >
                              <BadgeCheck className="mr-1 h-4 w-4" />
                              Record acceptance
                            </Button>
                          )}
                          {bond.status === 'Active' && (
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() => setAction({ bond, kind: 'exit' })}
                            >
                              Record exit
                            </Button>
                          )}
                          {bond.status === 'Breached' && (
                            <>
                              <Button
                                size="sm"
                                variant="ghost"
                                onClick={() => setAction({ bond, kind: 'settle' })}
                              >
                                Settle
                              </Button>
                              <Button
                                size="sm"
                                variant="ghost"
                                onClick={() => setAction({ bond, kind: 'waive' })}
                              >
                                Waive
                              </Button>
                            </>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={!!action} onOpenChange={(o) => !o && close()}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{actionTitle(action?.kind)}</DialogTitle>
            <DialogDescription>{actionBlurb(action?.kind)}</DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            {(action?.kind === 'exit' || action?.kind === 'settle') && (
              <div className="space-y-2">
                <Label htmlFor="actionDate">
                  {action.kind === 'exit' ? 'Exit date' : 'Settled on'}
                </Label>
                <Input
                  id="actionDate"
                  type="date"
                  value={actionDate}
                  onChange={(e) => setActionDate(e.target.value)}
                />
              </div>
            )}

            <div className="space-y-2">
              <Label htmlFor="bondNotes">
                {action?.kind === 'waive' ? 'Reason for waiving' : 'Notes'}
                {action?.kind === 'waive' && <span className="ml-0.5 text-red-500">*</span>}
              </Label>
              <Textarea
                id="bondNotes"
                rows={3}
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={close}>
              Cancel
            </Button>
            <Button
              onClick={() => action && run.mutate(action)}
              disabled={!action || run.isPending || (action.kind === 'waive' && !notes.trim())}
            >
              {run.isPending ? 'Saving…' : 'Confirm'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function actionTitle(kind?: BondAction): string {
  switch (kind) {
    case 'accept':
      return 'Record acceptance on the employee’s behalf';
    case 'exit':
      return 'Record an early exit';
    case 'waive':
      return 'Waive the obligation';
    case 'settle':
      return 'Record settlement';
    default:
      return '';
  }
}

function actionBlurb(kind?: BondAction): string {
  switch (kind) {
    case 'accept':
      return 'For a bond signed offline. The obligation period starts from here, and the employee can accept their own bonds in self-service.';
    case 'exit':
      return 'Computes the pro-rated repayment owed for the unserved months and marks the bond breached.';
    case 'waive':
      return 'No repayment is pursued. The reason is kept on the record.';
    case 'settle':
      return 'The repayment has been recovered or otherwise resolved.';
    default:
      return '';
  }
}
