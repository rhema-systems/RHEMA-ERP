'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CalendarRange, Info, Loader2, Plus, Send, Sparkles, Trash2, XCircle } from 'lucide-react';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { awardsService } from '@/services/hr/awards.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const toIso = (v: string) => (v ? new Date(v).toISOString().slice(0, 19) : null);

/**
 * Award cycles — the run of an award, and the windows people take part in (AWD-11).
 *
 * ⚠ **A voting window is required if and only if the award is decided by a staff vote.** Supplying
 * one for a committee award is refused, and omitting one for a voted award is refused too. The form
 * shows the fields only for awards that take them, so the rule is visible before the API states it.
 *
 * ⚠ **Voting cannot open before nominations close.** Otherwise people would be voting on a ballot
 * that is still changing under them.
 *
 * ⚠ **A cycle is invisible to employees until it is published.** A draft cycle collects nothing.
 */
export default function AwardCyclesPage() {
  const queryClient = useQueryClient();
  const [awardTypeId, setAwardTypeId] = useState('');
  const [creating, setCreating] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState<string | null>(null);
  const [cancelReason, setCancelReason] = useState('');
  const [removing, setRemoving] = useState<{ id: string; name: string } | null>(null);

  const [form, setForm] = useState({
    cycleCode: '', name: '',
    year: new Date().getFullYear(),
    nominationOpensOn: '', nominationClosesOn: '',
    votingOpensOn: '', votingClosesOn: '',
  });

  const { data: types } = useQuery({
    queryKey: ['award-types'],
    queryFn: () => awardsService.getTypes(),
  });

  const activeTypes = (types ?? []).filter((t) => t.isActive);
  const chosen = activeTypes.find((t) => t.id === awardTypeId);
  const needsVotingWindow = chosen?.winnerDecision === 'StaffVote';

  const { data: cycles, isLoading } = useQuery({
    queryKey: ['award-cycles', awardTypeId],
    queryFn: () => awardsService.getCycles(awardTypeId),
    enabled: Boolean(awardTypeId),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['award-cycles', awardTypeId] });

  const create = useMutation({
    mutationFn: () => {
      const body = {
        awardTypeId,
        cycleCode: form.cycleCode.trim(),
        name: form.name.trim(),
        year: Number(form.year),
        nominationOpensOn: toIso(form.nominationOpensOn),
        nominationClosesOn: toIso(form.nominationClosesOn),
        // Sent only when the award takes them — the API refuses a voting window on an award that is
        // not decided by a vote.
        votingOpensOn: needsVotingWindow ? toIso(form.votingOpensOn) : null,
        votingClosesOn: needsVotingWindow ? toIso(form.votingClosesOn) : null,
      };
      return editingId
        ? awardsService.updateCycle(editingId, { ...body, id: editingId })
        : awardsService.createCycle(body);
    },
    onSuccess: () => {
      toast.success(editingId ? 'Saved.' : 'Cycle created as a draft.');
      setCreating(false); setEditingId(null); invalidate();
    },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The cycle was refused.'),
  });

  const publish = useMutation({
    mutationFn: (id: string) => awardsService.publishCycle(id),
    onSuccess: () => { toast.success('Published. Employees can see it now.'); invalidate(); },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'Publishing was refused.'),
  });

  const cancel = useMutation({
    mutationFn: () => {
      if (!cancelling) throw new Error('No cycle selected.');
      return awardsService.cancelCycle(cancelling, cancelReason.trim());
    },
    onSuccess: () => {
      toast.success('Cancelled.');
      setCancelling(null); setCancelReason(''); invalidate();
    },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The cancellation was refused.'),
  });

  const removeCycle = useMutation({
    mutationFn: () => {
      if (!removing) throw new Error('Nothing selected.');
      return awardsService.deleteCycle(removing.id);
    },
    onSuccess: () => { toast.success('Removed.'); setRemoving(null); invalidate(); },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The removal was refused.'),
  });

  const generate = useMutation({
    mutationFn: (cycleId: string) => awardsService.generateCandidates(cycleId),
    onSuccess: (r) => {
      // Every count, including the zeros — "0 candidates" has several very different causes and
      // they call for different responses.
      toast.success(
        `${r.created} nomination(s) created. ${r.skippedIneligible} ineligible, ` +
        `${r.skippedAlreadyNominated} already nominated, from ${r.appraisalsExamined} scored appraisals.`,
      );
      invalidate();
    },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'Generation was refused.'),
  });

  const rows = cycles ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Award cycles"
        description="The run of an award: when nominations are open, and when voting is."
        backHref="/administration/hr/awards"
        actions={
          <Button disabled={!awardTypeId} onClick={() => setCreating(true)}>
            <Plus className="mr-2 h-4 w-4" />
            New cycle
          </Button>
        }
      />

      <Card>
        <CardContent className="flex flex-wrap items-center gap-3 p-4">
          <CalendarRange className="h-4 w-4 text-muted-foreground" />
          <span className="text-sm text-muted-foreground">Award</span>
          <Select value={awardTypeId} onValueChange={setAwardTypeId}>
            <SelectTrigger className="w-96">
              <SelectValue placeholder="Choose an award" />
            </SelectTrigger>
            <SelectContent>
              {activeTypes.map((t) => (
                <SelectItem key={t.id} value={t.id}>{t.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          {chosen && (
            <Badge variant="secondary">{chosen.winnerDecisionName}</Badge>
          )}
        </CardContent>
      </Card>

      {!awardTypeId ? (
        <EmptyState
          icon={CalendarRange}
          title="Choose an award"
          description="Cycles belong to an award — its windows and its decision method are what make a cycle valid."
        />
      ) : (
        <Card>
          <CardContent className="p-0">
            {isLoading ? (
              <div className="flex items-center justify-center p-12">
                <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
              </div>
            ) : rows.length === 0 ? (
              <EmptyState
                icon={CalendarRange}
                title="No cycles"
                description="Nothing can be nominated for this award until a cycle exists and is published."
              />
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Code</TableHead>
                    <TableHead>Name</TableHead>
                    <TableHead>Year</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Open now</TableHead>
                    <TableHead className="text-right">Nominations</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((c) => (
                    <TableRow key={c.id}>
                      <TableCell className="font-medium">{c.cycleCode}</TableCell>
                      <TableCell>{c.name}</TableCell>
                      <TableCell>{c.year}</TableCell>
                      <TableCell><Badge variant="secondary">{c.statusName}</Badge></TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {c.isNominationOpen ? 'Nominations' : c.isVotingOpen ? 'Voting' : '—'}
                      </TableCell>
                      <TableCell className="text-right">{c.nominationCount}</TableCell>
                      <TableCell className="space-x-1 text-right">
                        {c.status === 'Draft' && (
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={() => {
                              setForm({
                                cycleCode: c.cycleCode,
                                name: c.name,
                                year: c.year,
                                nominationOpensOn: '',
                                nominationClosesOn: '',
                                votingOpensOn: '',
                                votingClosesOn: '',
                              });
                              setEditingId(c.id);
                              setCreating(true);
                            }}
                          >
                            Edit
                          </Button>
                        )}
                        {c.status === 'Draft' && (
                          <Button
                            size="sm"
                            variant="outline"
                            disabled={publish.isPending}
                            onClick={() => publish.mutate(c.id)}
                          >
                            <Send className="mr-2 h-4 w-4" />
                            Publish
                          </Button>
                        )}
                        {chosen?.nominationSource === 'PerformanceTriggered' && c.status === 'Published' && (
                          <Button
                            size="sm"
                            variant="outline"
                            disabled={generate.isPending}
                            onClick={() => generate.mutate(c.id)}
                          >
                            <Sparkles className="mr-2 h-4 w-4" />
                            Generate candidates
                          </Button>
                        )}
                        {c.status !== 'Cancelled' && c.status !== 'Closed' && (
                          <Button size="sm" variant="ghost" onClick={() => setCancelling(c.id)}>
                            <XCircle className="mr-2 h-4 w-4" />
                            Cancel
                          </Button>
                        )}
                        {/* ⚠ Only a draft. A published cycle people have nominated into is
                            CANCELLED, not deleted — the nominations must keep something to belong to. */}
                        {c.status === 'Draft' && (
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={() => setRemoving({ id: c.id, name: c.name })}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      )}

      {/* ── create ─────────────────────────────────────────────────────────── */}
      <Dialog
        open={creating}
        onOpenChange={(o) => { setCreating(o); if (!o) setEditingId(null); }}
      >
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {editingId ? 'Edit cycle' : `New cycle for ${chosen?.name}`}
            </DialogTitle>
            <DialogDescription>
              {editingId
                ? 'Only a draft can be edited — once published, its windows are what people are acting on.'
                : 'It is created as a draft — publish it when you are ready.'}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="cycleCode">Code</Label>
                <Input id="cycleCode" value={form.cycleCode}
                  onChange={(e) => setForm({ ...form, cycleCode: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="cycleName">Name</Label>
                <Input id="cycleName" value={form.name}
                  onChange={(e) => setForm({ ...form, name: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="year">Year</Label>
                <Input id="year" type="number" value={form.year}
                  onChange={(e) => setForm({ ...form, year: Number(e.target.value) })} />
              </div>
            </div>

            <div className="space-y-3 rounded-md border p-4">
              <p className="text-sm font-medium">Nomination window</p>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="nomOpens">Opens</Label>
                  <Input id="nomOpens" type="date" value={form.nominationOpensOn}
                    onChange={(e) => setForm({ ...form, nominationOpensOn: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="nomCloses">Closes</Label>
                  <Input id="nomCloses" type="date" value={form.nominationClosesOn}
                    onChange={(e) => setForm({ ...form, nominationClosesOn: e.target.value })} />
                </div>
              </div>
            </div>

            {needsVotingWindow ? (
              <div className="space-y-3 rounded-md border p-4">
                <p className="text-sm font-medium">Voting window</p>
                <Alert>
                  <Info className="h-4 w-4" />
                  <AlertDescription>
                    This award is decided by a staff vote, so a voting window is required — and it
                    cannot open before nominations close.
                  </AlertDescription>
                </Alert>
                <div className="grid gap-4 sm:grid-cols-2">
                  <div className="space-y-2">
                    <Label htmlFor="voteOpens">Opens</Label>
                    <Input id="voteOpens" type="date" value={form.votingOpensOn}
                      onChange={(e) => setForm({ ...form, votingOpensOn: e.target.value })} />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="voteCloses">Closes</Label>
                    <Input id="voteCloses" type="date" value={form.votingClosesOn}
                      onChange={(e) => setForm({ ...form, votingClosesOn: e.target.value })} />
                  </div>
                </div>
              </div>
            ) : (
              <Alert>
                <AlertTriangle className="h-4 w-4" />
                <AlertDescription>
                  This award is decided by {chosen?.winnerDecisionName?.toLowerCase()}, so it takes
                  no voting window. Giving it one would be refused.
                </AlertDescription>
              </Alert>
            )}

            <div className="flex justify-end gap-2">
              <Button
                variant="outline"
                onClick={() => { setCreating(false); setEditingId(null); }}
              >
                Cancel
              </Button>
              <Button
                disabled={
                  !form.cycleCode.trim() || !form.name.trim() ||
                  (needsVotingWindow && (!form.votingOpensOn || !form.votingClosesOn)) ||
                  create.isPending
                }
                onClick={() => create.mutate()}
              >
                {create.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {editingId ? 'Save' : 'Create'}
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={Boolean(removing)}
        onOpenChange={(o) => !o && setRemoving(null)}
        title={`Remove ${removing?.name}?`}
        description="Only a draft can be removed. A published cycle is cancelled instead, so the nominations raised against it still have something to belong to."
        variant="destructive"
        confirmText="Remove"
        isLoading={removeCycle.isPending}
        onConfirm={async () => {
          try { await removeCycle.mutateAsync(); } catch { return false; }
        }}
      />

      {/* ── cancel ─────────────────────────────────────────────────────────── */}
      <Dialog open={Boolean(cancelling)} onOpenChange={(o) => !o && setCancelling(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Cancel this cycle</DialogTitle>
            <DialogDescription>
              Nominations already raised against it stay on the record.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="cancelReason">Reason</Label>
              <Input id="cancelReason" value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)} />
            </div>
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setCancelling(null)}>Keep it</Button>
              <Button
                variant="destructive"
                disabled={!cancelReason.trim() || cancel.isPending}
                onClick={() => cancel.mutate()}
              >
                {cancel.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Cancel the cycle
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
