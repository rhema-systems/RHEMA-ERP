'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  BadgeCheck,
  Clock,
  HandCoins,
  Pencil,
  Scale,
  ShieldCheck,
  Trash2,
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
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { trainingNominationService } from '@/services/hr/training-nomination.service';
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
  /**
   * ⚠ **The correction, and it is money.** A bond's amount and duration are copied from the
   * programme when the server mints it on nomination submit, so a programme priced wrongly mints
   * every bond wrongly. Until this dialog existed the only remedy was to delete the bond and raise
   * another — which throws away the acceptance the employee has already signed.
   */
  const [editing, setEditing] = useState<null | {
    id: string; employeeName: string;
    bondDurationMonths: string; bondAmount: string; currency: string;
    termsText: string; notes: string;
  }>(null);
  const [deleting, setDeleting] = useState<TrainingServiceBond | null>(null);
  /**
   * ⚠ Raising one by hand is the exception. The server mints a bond when a sponsored nomination
   * is submitted, so the case for this is the approved nomination that never got one — sponsorship
   * agreed after the fact, or a programme whose terms were set later. The nomination is required by
   * the API, so a bond cannot be raised against nothing.
   */
  const [raising, setRaising] = useState<null | {
    nominationId: string; bondDurationMonths: string; bondAmount: string;
    currency: string; termsText: string; notes: string;
  }>(null);
  const [notes, setNotes] = useState('');
  const [actionDate, setActionDate] = useState(today());
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'service-bonds'],
    queryFn: () => trainingServiceBondService.getAll(),
  });

  const rows = useMemo(() => data ?? [], [data]);

  const { data: approvedNominations = [] } = useQuery({
    queryKey: ['hr', 'training-nominations', 'Approved'],
    queryFn: () => trainingNominationService.getByStatus('Approved'),
    enabled: raising !== null,
  });

  const raiseBond = useMutation({
    mutationFn: () => {
      if (!raising) throw new Error('Nothing to raise.');
      return trainingServiceBondService.create({
        nominationId: raising.nominationId,
        bondDurationMonths: Number(raising.bondDurationMonths || 0),
        bondAmount: Number(raising.bondAmount || 0),
        currency: raising.currency.trim(),
        termsText: raising.termsText.trim() || null,
        notes: raising.notes.trim() || null,
      });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'service-bonds'] });
      toast({ title: 'Bond raised', description: 'It is pending the employee\'s acceptance.' });
      setRaising(null);
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'The bond was refused',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

  const saveEdit = useMutation({
    mutationFn: () => {
      if (!editing) throw new Error('Nothing to save.');
      return trainingServiceBondService.update(editing.id, {
        id: editing.id,
        bondDurationMonths: Number(editing.bondDurationMonths || 0),
        bondAmount: Number(editing.bondAmount || 0),
        currency: editing.currency.trim(),
        termsText: editing.termsText.trim() || null,
        notes: editing.notes.trim() || null,
      });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'service-bonds'] });
      toast({ title: 'Bond corrected' });
      setEditing(null);
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'The correction was refused',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

  const removeBond = useMutation({
    mutationFn: (id: string) => trainingServiceBondService.remove(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'service-bonds'] });
      toast({ title: 'Bond deleted' });
      setDeleting(null);
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'It could not be deleted',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

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
        actions={
          <Button
            variant="outline"
            onClick={() =>
              setRaising({
                nominationId: '', bondDurationMonths: '', bondAmount: '',
                currency: 'GHS', termsText: '', notes: '',
              })
            }
          >
            Raise a bond
          </Button>
        }
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
                          <Button
                            size="sm"
                            variant="ghost"
                            title="Correct the terms"
                            onClick={() =>
                              setEditing({
                                id: bond.id,
                                employeeName: bond.employeeName ?? 'this bond',
                                bondDurationMonths: String(bond.bondDurationMonths ?? ''),
                                bondAmount: String(bond.bondAmount ?? ''),
                                currency: bond.currency ?? 'GHS',
                                termsText: bond.termsText ?? '',
                                notes: bond.notes ?? '',
                              })
                            }
                          >
                            <Pencil className="h-4 w-4" />
                          </Button>
                          {bond.status === 'PendingAcceptance' && (
                            <Button
                              size="sm"
                              variant="ghost"
                              title="Delete a bond raised in error"
                              onClick={() => setDeleting(bond)}
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
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

      <Dialog open={raising !== null} onOpenChange={(o) => !o && setRaising(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Raise a service bond</DialogTitle>
            <DialogDescription>
              For an approved nomination that never got one — sponsorship agreed after the fact. The
              usual path is the server minting it when the nomination is submitted.
            </DialogDescription>
          </DialogHeader>
          {raising && (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label>Nomination</Label>
                <Select
                  value={raising.nominationId}
                  onValueChange={(v) => setRaising({ ...raising, nominationId: v })}
                >
                  <SelectTrigger><SelectValue placeholder="Choose an approved nomination" /></SelectTrigger>
                  <SelectContent>
                    {approvedNominations.map((n: any) => (
                      <SelectItem key={n.id} value={n.id}>
                        {[n.employeeName, n.programName, n.nominationNumber].filter(Boolean).join(' · ')}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {approvedNominations.length === 0 && (
                  <p className="text-xs text-muted-foreground">
                    No approved nominations without a bond.
                  </p>
                )}
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="newBondMonths">Months</Label>
                  <Input id="newBondMonths" type="number" min={1} value={raising.bondDurationMonths}
                    onChange={(e) => setRaising({ ...raising, bondDurationMonths: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="newBondAmount">Amount</Label>
                  <Input id="newBondAmount" type="number" min={0} value={raising.bondAmount}
                    onChange={(e) => setRaising({ ...raising, bondAmount: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="newBondCurrency">Currency</Label>
                  <Input id="newBondCurrency" maxLength={3} value={raising.currency}
                    onChange={(e) => setRaising({ ...raising, currency: e.target.value.toUpperCase() })} />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="newBondTerms">Terms</Label>
                <Textarea id="newBondTerms" rows={3} value={raising.termsText}
                  onChange={(e) => setRaising({ ...raising, termsText: e.target.value })} />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setRaising(null)}>Cancel</Button>
            <Button
              disabled={
                raiseBond.isPending || !raising?.nominationId ||
                !raising?.bondDurationMonths || !raising?.bondAmount
              }
              onClick={() => raiseBond.mutate()}
            >
              {raiseBond.isPending ? 'Raising…' : 'Raise'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={editing !== null} onOpenChange={(o) => !o && setEditing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Correct the bond for {editing?.employeeName}</DialogTitle>
            <DialogDescription>
              The terms were copied from the programme when the bond was raised. Correcting them here
              keeps the bond and any acceptance already given — deleting and re-raising would not.
            </DialogDescription>
          </DialogHeader>
          {editing && (
            <div className="space-y-4">
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="bondMonths">Months</Label>
                  <Input id="bondMonths" type="number" min={1} value={editing.bondDurationMonths}
                    onChange={(e) => setEditing({ ...editing, bondDurationMonths: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="bondAmount">Amount</Label>
                  <Input id="bondAmount" type="number" min={0} value={editing.bondAmount}
                    onChange={(e) => setEditing({ ...editing, bondAmount: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="bondCurrency">Currency</Label>
                  <Input id="bondCurrency" maxLength={3} value={editing.currency}
                    onChange={(e) => setEditing({ ...editing, currency: e.target.value.toUpperCase() })} />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="bondTerms">Terms</Label>
                <Textarea id="bondTerms" rows={3} value={editing.termsText}
                  onChange={(e) => setEditing({ ...editing, termsText: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="bondNotes">Notes</Label>
                <Textarea id="bondNotes" rows={2} value={editing.notes}
                  onChange={(e) => setEditing({ ...editing, notes: e.target.value })} />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(null)}>Cancel</Button>
            <Button disabled={saveEdit.isPending} onClick={() => saveEdit.mutate()}>
              {saveEdit.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={deleting !== null}
        onOpenChange={(o) => !o && setDeleting(null)}
        title={`Delete the bond for ${deleting?.employeeName ?? 'this employee'}?`}
        description="Only for one raised in error, and only before it is accepted. A bond the employee has signed should be waived instead, which keeps the record of what was agreed."
        confirmText="Delete"
        variant="destructive"
        onConfirm={() => { if (deleting) removeBond.mutate(deleting.id); }}
        isLoading={removeBond.isPending}
      />
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
