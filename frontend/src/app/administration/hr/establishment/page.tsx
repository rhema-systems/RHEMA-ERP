'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Loader2, Search, ShieldCheck, ShieldOff } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import type { PositionEstablishment } from '@/types/hr/job-architecture';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * The approved establishment (FR-HR-136).
 *
 * ⚠ **A position is either ESTABLISHED or it is not, and that is the whole point of this screen.**
 * `ExpectedHeadcount` alone cannot say which: 132 of 146 live positions carry its default of 1,
 * untouched, while one holds over a thousand people. `EstablishmentApprovedOn` is what separates an
 * authorised number from a column default, and every rule — vacancy approval, staff movements —
 * keys off it. An unestablished post is constrained by nothing, whatever its headcount reads.
 *
 * ⚠ **Most positions should be established by an approved manpower budget, not here.** This screen
 * is the exception path for posts no budget covers, and it is the one place FR-HR-135's three-step
 * chain can be bypassed — which is why it is Admin-tier and why a number set here records no source
 * budget, so a reader can tell the two apart.
 */
export default function EstablishmentPage() {
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState<{ id: string; title: string; current: PositionEstablishment | null } | null>(null);
  const [headcount, setHeadcount] = useState(1);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  // Round 2b, R4a: withdrawing an establishment asks why, and confirms, instead of sending a
  // constant reason on a single click.
  const [withdrawing, setWithdrawing] = useState<{ id: string; title: string } | null>(null);
  const [withdrawReason, setWithdrawReason] = useState('');

  // One read for every position (R4a). This used to be one call per position — 231 requests
  // on the live tenant — because only the per-position endpoint computed the live filled count.
  const { data: establishments, isLoading } = useQuery({
    queryKey: ['establishment', 'all'],
    queryFn: () => jobArchitectureService.getEstablishmentList(),
  });

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase();
    return (establishments ?? [])
      .filter((e): e is PositionEstablishment => !!e)
      .filter((e) => !term || e.positionTitle.toLowerCase().includes(term))
      // Over-strength first: those are the posts refusing recruitment and movements right now.
      .sort((a, b) => {
        const aOver = a.isEstablished && a.currentlyFilled > a.expectedHeadcount ? 1 : 0;
        const bOver = b.isEstablished && b.currentlyFilled > b.expectedHeadcount ? 1 : 0;
        if (aOver !== bOver) return bOver - aOver;
        return a.positionTitle.localeCompare(b.positionTitle);
      });
  }, [establishments, search]);

  const refresh = () => qc.invalidateQueries({ queryKey: ['establishment'] });

  const save = async () => {
    if (!editing) return;
    setBusy(true);
    try {
      await jobArchitectureService.setPositionEstablishment(editing.id, headcount, reason);
      toast.success('Establishment set');
      refresh();
      setEditing(null);
      setReason('');
    } catch (e) {
      // The API refuses a headcount below the number already in post, and names both figures.
      toast.error(e instanceof Error ? e.message : 'Could not set the establishment');
    } finally {
      setBusy(false);
    }
  };

  const withdraw = async () => {
    if (!withdrawing) return;
    setBusy(true);
    try {
      await jobArchitectureService.withdrawPositionEstablishment(withdrawing.id, withdrawReason.trim());
      toast.success('Establishment withdrawn — the post is no longer constrained');
      refresh();
      setWithdrawing(null);
      setWithdrawReason('');
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Could not withdraw it');
    } finally {
      setBusy(false);
    }
  };

  const established = rows.filter((r) => r.isEstablished).length;
  const overStrength = rows.filter((r) => r.isEstablished && r.currentlyFilled > r.expectedHeadcount).length;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Manual Establishment"
        description="Establish a post no approved manpower budget covers — the exception path to FR-HR-135's three-step chain (FR-HR-136)."
        backHref="/administration/hr"
        actions={
          // The operational counterpart, which carried this screen's exact name in the other
          // menu until this one was renamed: where headcount stands against the establishment,
          // and the gaps that follow from it.
          <Button variant="outline" asChild>
            <Link href="/hr/recruitment/establishment">
              <Search className="mr-2 h-4 w-4" />
              Establishment gaps
            </Link>
          </Button>
        }
      />

      <div className="grid gap-4 sm:grid-cols-3">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <ShieldCheck className="h-4 w-4" />
              Established
            </div>
            <div className="mt-2 text-2xl font-semibold">
              {established} / {rows.length}
            </div>
            <div className="mt-1 text-xs text-muted-foreground">positions with an authorised headcount</div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <ShieldOff className="h-4 w-4" />
              Unconstrained
            </div>
            <div className="mt-2 text-2xl font-semibold">{rows.length - established}</div>
            <div className="mt-1 text-xs text-muted-foreground">
              nobody has authorised a headcount, so no rule applies
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <AlertTriangle className="h-4 w-4" />
              Over strength
            </div>
            <div className={`mt-2 text-2xl font-semibold ${overStrength > 0 ? 'text-amber-700' : ''}`}>
              {overStrength}
            </div>
            <div className="mt-1 text-xs text-muted-foreground">refusing recruitment and movements</div>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardContent className="space-y-4 pt-6">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Search positions"
              className="pl-9"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          {isLoading || !establishments ? (
            <div className="flex items-center justify-center py-16 text-muted-foreground">
              <Loader2 className="mr-2 h-5 w-5 animate-spin" />
              Loading…
            </div>
          ) : rows.length === 0 ? (
            <EmptyState icon={ShieldCheck} title="No positions" description="Nothing to establish." />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Position</TableHead>
                  <TableHead className="text-right">Established for</TableHead>
                  <TableHead className="text-right">In post</TableHead>
                  <TableHead>Authorised</TableHead>
                  <TableHead>Source</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((e) => {
                  const over = e.isEstablished && e.currentlyFilled > e.expectedHeadcount;
                  return (
                    <TableRow key={e.positionId} className={over ? 'bg-amber-50' : undefined}>
                      <TableCell className="font-medium">{e.positionTitle}</TableCell>
                      <TableCell className="text-right">
                        {/* An unestablished post's headcount is the column default and means nothing. */}
                        {e.isEstablished ? e.expectedHeadcount : <span className="text-muted-foreground">—</span>}
                      </TableCell>
                      <TableCell className={`text-right ${over ? 'font-semibold text-amber-800' : ''}`}>
                        {e.currentlyFilled}
                      </TableCell>
                      <TableCell>
                        {e.isEstablished ? (
                          <Badge className="bg-emerald-100 text-emerald-800">
                            {fmtDate(e.establishmentApprovedOn)}
                          </Badge>
                        ) : (
                          <Badge variant="outline" className="text-muted-foreground">
                            Not established
                          </Badge>
                        )}
                      </TableCell>
                      <TableCell className="text-xs text-muted-foreground">
                        {/* Null source = set here by HR; a number = it came up FR-HR-135's chain. */}
                        {e.establishmentSourceBudgetNumber && e.establishmentSourceBudgetId ? (
                          <Link className="underline" href={`/hr/manpower-budgets/${e.establishmentSourceBudgetId}`}>
                            Budget {e.establishmentSourceBudgetNumber}
                          </Link>
                        ) : e.isEstablished ? (
                          'Set by HR'
                        ) : (
                          '—'
                        )}
                      </TableCell>
                      <TableCell className="text-right">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => {
                            setEditing({ id: e.positionId, title: e.positionTitle, current: e });
                            setHeadcount(Math.max(e.expectedHeadcount, e.currentlyFilled));
                            setReason('');
                          }}
                        >
                          {e.isEstablished ? 'Revise' : 'Establish'}
                        </Button>
                        {e.isEstablished && (
                          <Button
                            variant="ghost"
                            size="sm"
                            disabled={busy}
                            onClick={() => { setWithdrawReason(''); setWithdrawing({ id: e.positionId, title: e.positionTitle }); }}
                          >
                            Withdraw
                          </Button>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={!!editing} onOpenChange={(open) => !open && setEditing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Establish {editing?.title}</DialogTitle>
          </DialogHeader>
          <div className="grid gap-4">
            <div className="space-y-2">
              <Label>Posts authorised *</Label>
              <Input
                type="number"
                min={editing?.current?.currentlyFilled ?? 0}
                value={headcount}
                onChange={(e) => setHeadcount(Number(e.target.value))}
              />
              <p className="text-xs text-muted-foreground">
                {editing?.current?.currentlyFilled
                  ? `${editing.current.currentlyFilled} already in post — it cannot be set below that.`
                  : 'Nobody is in post yet.'}
              </p>
            </div>
            <div className="space-y-2">
              <Label>Reason *</Label>
              <Input
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                placeholder="Why this number, for the audit trail"
              />
              {/* A budget approval carries its own trail; a number set here needs one written down. */}
              <p className="text-xs text-muted-foreground">
                Most positions should be established by an approved manpower budget instead.
              </p>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(null)}>
              Cancel
            </Button>
            <Button onClick={save} disabled={!reason.trim() || busy}>
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Set establishment
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!withdrawing} onOpenChange={(open) => !open && setWithdrawing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Withdraw the establishment of {withdrawing?.title}</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <p className="text-sm text-muted-foreground">
              The post goes back to unconstrained: requisitions and movements against it are no
              longer checked, and its headcount is kept only as a planning figure.
            </p>
            <Label htmlFor="withdraw-reason">Reason *</Label>
            <Input
              id="withdraw-reason"
              value={withdrawReason}
              onChange={(e) => setWithdrawReason(e.target.value)}
              placeholder="Why the authorisation no longer stands"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setWithdrawing(null)}>Cancel</Button>
            <Button variant="destructive" onClick={withdraw} disabled={!withdrawReason.trim() || busy}>
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Withdraw
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
