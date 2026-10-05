'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, Trash2, TriangleAlert, UserPlus, X } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
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
import { Textarea } from '@/components/ui/textarea';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { formatDate } from '@/lib/hr/attendance-format';
import {
  peerNominationService,
  performanceAppraisalService,
} from '@/services/hr/appraisal-run.service';

/**
 * Peer nominations for one appraisal — nominate, approve, reject.
 *
 * The same panel serves the appraisee and their manager because the *server* decides who may
 * do what, not this component. Two things follow from that:
 *
 *   • `nominationMode` says who is expected to nominate (the employee or the manager), and is
 *     shown rather than enforced here — the API refuses a nomination from the wrong party.
 *   • Approval is `canApprove`, passed in by the manager's screen. Approving is what creates
 *     the peers' evaluation records and notifies them, so it is not something the appraisee
 *     does for themselves. In Manager mode there is no approval step: the manager chooses the
 *     peers, and a nomination is approved as it is made (performance closure D4).
 *
 * `canSubmit` on the summary is about the *active* count (pending + approved) being within the
 * configured range, not about approval — it is what the self-evaluation submit rule checks. A
 * rejected nomination leaves room for a replacement (D2). Only a pending nomination can be
 * withdrawn: an approved peer has been asked for their feedback.
 */
export function PeerNominationPanel({
  appraisalId,
  canManage = false,
  canApprove = false,
}: {
  appraisalId: string;
  /** Whether this viewer may add and remove nominations. */
  canManage?: boolean;
  /** Whether this viewer may approve or reject them — the manager, not the appraisee. */
  canApprove?: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [addOpen, setAddOpen] = useState(false);
  const [rejectOpen, setRejectOpen] = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [selected, setSelected] = useState<string[]>([]);
  const [peerId, setPeerId] = useState<string | null>(null);
  const [dueDate, setDueDate] = useState('');
  const [instructions, setInstructions] = useState('');

  const summaryKey = ['hr', 'peer-nominations', appraisalId];
  const { data: summary, isLoading } = useQuery({
    queryKey: summaryKey,
    queryFn: () => performanceAppraisalService.getPeerNominationSummary(appraisalId),
    enabled: !!appraisalId,
  });

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: summaryKey });
    setSelected([]);
  };

  const fail = (title: string) => (e: Error) =>
    toast({ title, description: e.message, variant: 'destructive' });

  const managerChooses = summary?.nominationMode === 'Manager';

  const nominate = useMutation({
    mutationFn: () =>
      performanceAppraisalService.nominatePeers(appraisalId, {
        appraisalId,
        peerEmployeeIds: peerId ? [peerId] : [],
        dueDate: dueDate || null,
        instructionsToPeer: instructions.trim() || null,
      }),
    onSuccess: () => {
      toast({
        title: 'Peer nominated',
        description: managerChooses
          ? 'They have been asked for their feedback.'
          : 'Waiting for approval before they are asked.',
      });
      setAddOpen(false);
      setPeerId(null);
      setDueDate('');
      setInstructions('');
      refresh();
    },
    onError: fail('Could not nominate'),
  });

  const approve = useMutation({
    mutationFn: () =>
      performanceAppraisalService.approvePeerNominations(appraisalId, {
        appraisalId,
        nominationIds: selected,
      }),
    onSuccess: (rows) => {
      toast({
        title: `${rows.length} peer(s) approved`,
        description: 'Their feedback forms are open and they have been notified.',
      });
      refresh();
    },
    onError: fail('Could not approve'),
  });

  const reject = useMutation({
    mutationFn: () =>
      performanceAppraisalService.rejectPeerNominations(appraisalId, {
        appraisalId,
        nominationIds: selected,
        rejectionReason: rejectReason.trim(),
      }),
    onSuccess: () => {
      toast({ title: 'Nominations rejected' });
      setRejectOpen(false);
      setRejectReason('');
      refresh();
    },
    onError: fail('Could not reject'),
  });

  const remove = useMutation({
    mutationFn: (id: string) => peerNominationService.remove(id),
    onSuccess: () => {
      toast({ title: 'Nomination removed' });
      refresh();
    },
    onError: fail('Could not remove'),
  });

  const rows = summary?.nominations ?? [];
  const pendingSelected = selected.length > 0;
  const atMax = summary ? summary.activeNominations >= summary.maxAllowed : false;

  return (
    <Card>
      <CardHeader>
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div>
            <CardTitle className="text-base">Peer nominations</CardTitle>
            {summary && (
              <p className="mt-1 text-sm text-muted-foreground">
                {summary.activeNominations} nominated · {summary.approvedCount} approved ·{' '}
                {summary.rejectedCount > 0 && <>{summary.rejectedCount} not approved · </>}
                {summary.minRequired}–{summary.maxAllowed} required ·{' '}
                {summary.nominationMode === 'Employee'
                  ? 'the employee nominates'
                  : 'the manager nominates'}
              </p>
            )}
          </div>
          <div className="flex items-center gap-2">
            {canApprove && pendingSelected && (
              <>
                <Button size="sm" onClick={() => approve.mutate()} disabled={approve.isPending}>
                  <Check className="mr-2 h-4 w-4" />
                  Approve {selected.length}
                </Button>
                <Button size="sm" variant="outline" onClick={() => setRejectOpen(true)}>
                  <X className="mr-2 h-4 w-4" />
                  Reject
                </Button>
              </>
            )}
            {canManage && summary?.canEdit && (
              <Button size="sm" variant="outline" onClick={() => setAddOpen(true)} disabled={atMax}>
                <UserPlus className="mr-2 h-4 w-4" />
                Nominate
              </Button>
            )}
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {summary && !summary.canSubmit && summary.activeNominations < summary.minRequired && (
          <Alert>
            <TriangleAlert className="h-4 w-4" />
            <AlertTitle>
              {summary.minRequired - summary.activeNominations} more nomination(s) needed
            </AlertTitle>
            <AlertDescription>
              This cycle requires between {summary.minRequired} and {summary.maxAllowed} peers.{' '}
              {managerChooses
                ? `The appraisal does not move past peer nomination until ${summary.minRequired} are nominated.`
                : 'The self-evaluation cannot be submitted until the list is within that range.'}
            </AlertDescription>
          </Alert>
        )}

        {isLoading ? (
          <p className="py-6 text-center text-sm text-muted-foreground">Loading…</p>
        ) : summary?.peersWithheld ? (
          // D-40: the manager chose the peers and the reviews are anonymous, so the appraisee is
          // told how many, not who.
          <p className="py-6 text-center text-sm text-muted-foreground">
            Your manager chooses your peer evaluators in this cycle, and peer reviews are
            anonymous: {summary.approvedCount} colleague(s) have been asked for feedback.
          </p>
        ) : rows.length === 0 ? (
          <EmptyState
            icon={UserPlus}
            title="No peers nominated"
            description="Nominated peers are asked for feedback once their nomination is approved."
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                {canApprove && <TableHead className="w-10" />}
                <TableHead>Peer</TableHead>
                <TableHead>Nominated by</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Due</TableHead>
                {canManage && <TableHead className="w-16" />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((row) => (
                <TableRow key={row.id}>
                  {canApprove && (
                    <TableCell>
                      <Checkbox
                        checked={selected.includes(row.id)}
                        disabled={row.nominationStatus !== 'Pending'}
                        onCheckedChange={(checked) =>
                          setSelected((prev) =>
                            checked ? [...prev, row.id] : prev.filter((id) => id !== row.id),
                          )
                        }
                        aria-label={`Select ${row.peerEmployeeName}`}
                      />
                    </TableCell>
                  )}
                  <TableCell>
                    <div className="font-medium">{row.peerEmployeeName}</div>
                    {row.peerEmployeeNumber && (
                      <div className="text-xs text-muted-foreground">{row.peerEmployeeNumber}</div>
                    )}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {row.nominatedByName}
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={row.nominationStatus} />
                    {row.rejectionReason && (
                      <div className="mt-1 text-xs text-muted-foreground">{row.rejectionReason}</div>
                    )}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {row.dueDate ? formatDate(row.dueDate) : '—'}
                  </TableCell>
                  {canManage && (
                    <TableCell className="text-right">
                      {/* Only a pending nomination can be withdrawn — an approved peer has been asked. */}
                      {row.nominationStatus === 'Pending' && summary?.canEdit && (
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={() => remove.mutate(row.id)}
                          aria-label={`Remove ${row.peerEmployeeName}`}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      )}
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      <Dialog open={addOpen} onOpenChange={setAddOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Nominate a peer</DialogTitle>
            <DialogDescription>
              {managerChooses
                ? 'You choose the peers in this cycle, so they are asked for their feedback as soon as you nominate them.'
                : 'They are only asked for feedback once the nomination is approved.'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Colleague</Label>
              <EmployeePicker value={peerId} onChange={(id) => setPeerId(id)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="peer-due">Due date</Label>
              <Input
                id="peer-due"
                type="date"
                value={dueDate}
                onChange={(e) => setDueDate(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="peer-instructions">Instructions to the peer</Label>
              <Textarea
                id="peer-instructions"
                rows={3}
                maxLength={500}
                value={instructions}
                onChange={(e) => setInstructions(e.target.value)}
                placeholder="What would you like them to comment on?"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => nominate.mutate()} disabled={!peerId || nominate.isPending}>
              Nominate
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={rejectOpen} onOpenChange={setRejectOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reject {selected.length} nomination(s)</DialogTitle>
            <DialogDescription>
              The reason is recorded against each nomination and is visible to the employee.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="reject-reason">Reason</Label>
            <Textarea
              id="reject-reason"
              rows={3}
              maxLength={500}
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejectOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={() => reject.mutate()}
              disabled={!rejectReason.trim() || reject.isPending}
            >
              Reject
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
