'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import Link from 'next/link';
import { CheckCircle2, Lightbulb, Plus, RefreshCw, TriangleAlert, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { appraisalOutcomeRecommendationService } from '@/services/hr/outcomes.service';
import {
  RECOMMENDATION_TARGET_ROUTES,
  type AppraisalOutcomeRecommendation,
  type RecommendationType,
} from '@/types/hr/outcomes';

const TYPES: RecommendationType[] = [
  'MeritIncrease',
  'Bonus',
  'Promotion',
  'TrainingNomination',
  'SuccessionNomination',
  'PerformanceImprovementPlan',
  'ConfirmProbation',
  'ExtendProbation',
  'ContractRenewal',
  'Demotion',
  'Termination',
  'Recognition',
];

interface OutcomeRecommendationsPanelProps {
  appraisalId: string;
  /** Show the propose form. Off for read-only contexts. */
  allowPropose?: boolean;
  /** Show approve / reject / dismiss. HR only. */
  allowDecide?: boolean;
}

/**
 * The recommendations attached to one appraisal — proposed, decided, and what each became.
 *
 * **`Approved` is a warning here, not a success.** Approving dispatches the recommendation to
 * the module that owns the outcome; when that works the row lands on `Actioned` with a link to
 * the record it created. A row sitting on `Approved` means the dispatch failed and the outcome
 * does not exist yet, so it is styled as outstanding work and offered a retry.
 *
 * Proposing is limited to the appraisee's manager or HR; deciding is HR. Both are refused
 * server-side, so `allowPropose` / `allowDecide` only control what is worth showing.
 */
export function OutcomeRecommendationsPanel({
  appraisalId,
  allowPropose = true,
  allowDecide = false,
}: OutcomeRecommendationsPanelProps) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [proposeOpen, setProposeOpen] = useState(false);
  const [type, setType] = useState<RecommendationType>('MeritIncrease');
  const [notes, setNotes] = useState('');
  const [closing, setClosing] = useState<{
    item: AppraisalOutcomeRecommendation;
    action: 'reject' | 'dismiss';
  } | null>(null);
  const [closingNotes, setClosingNotes] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'appraisal-recommendations', appraisalId],
    queryFn: () => appraisalOutcomeRecommendationService.getByAppraisal(appraisalId),
    enabled: !!appraisalId,
  });

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-recommendations', appraisalId] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'recommendation-worklist'] });
  };

  const fail = (title: string) => (e: Error) =>
    toast({ title, description: e.message, variant: 'destructive' });

  const propose = useMutation({
    mutationFn: () =>
      appraisalOutcomeRecommendationService.propose({
        performanceAppraisalId: appraisalId,
        recommendationType: type,
        notes: notes.trim() || null,
      }),
    onSuccess: () => {
      toast({ title: 'Recommendation proposed', description: 'HR will pick it up from here.' });
      setProposeOpen(false);
      setNotes('');
      refresh();
    },
    onError: fail('Could not propose'),
  });

  const approve = useMutation({
    mutationFn: (id: string) => appraisalOutcomeRecommendationService.approve(id),
    onSuccess: (result) => {
      if (result.status === 'Actioned') {
        toast({
          title: 'Approved and actioned',
          description: `A ${humanizeEnum(result.targetEntityType)} record has been created.`,
        });
      } else {
        toast({
          title: 'Approved, but not actioned',
          description:
            'The owning module could not create the downstream record. Retry it, or action the outcome there directly.',
          variant: 'destructive',
        });
      }
      refresh();
    },
    onError: fail('Could not approve'),
  });

  const retry = useMutation({
    mutationFn: (id: string) => appraisalOutcomeRecommendationService.retryDispatch(id),
    onSuccess: (result) => {
      toast({
        title: 'Dispatched',
        description: `A ${humanizeEnum(result.targetEntityType)} record has been created.`,
      });
      refresh();
    },
    onError: fail('Could not dispatch'),
  });

  const close = useMutation({
    // The target is passed in rather than read off state, so the call cannot be made without one.
    mutationFn: ({
      item,
      action,
    }: {
      item: AppraisalOutcomeRecommendation;
      action: 'reject' | 'dismiss';
    }) => {
      const payload = { notes: closingNotes.trim() || null };
      return action === 'reject'
        ? appraisalOutcomeRecommendationService.reject(item.id, payload)
        : appraisalOutcomeRecommendationService.dismiss(item.id, payload);
    },
    onSuccess: () => {
      toast({ title: 'Recommendation closed' });
      setClosing(null);
      setClosingNotes('');
      refresh();
    },
    onError: fail('Could not close the recommendation'),
  });

  const rows = data ?? [];

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0">
        <CardTitle className="text-base">Outcome recommendations</CardTitle>
        {allowPropose && (
          <Button size="sm" variant="outline" onClick={() => setProposeOpen(true)}>
            <Plus className="mr-2 h-4 w-4" />
            Propose
          </Button>
        )}
      </CardHeader>
      <CardContent className="p-0">
        {isLoading ? (
          <div className="space-y-2 p-4">
            {[0, 1].map((i) => (
              <Skeleton key={i} className="h-10 w-full" />
            ))}
          </div>
        ) : rows.length === 0 ? (
          <EmptyState
            icon={Lightbulb}
            title="No recommendations"
            description="Nothing has been proposed off the back of this appraisal yet."
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Outcome</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Notes</TableHead>
                <TableHead>Result</TableHead>
                {allowDecide && <TableHead className="w-48" />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((row) => (
                <TableRow key={row.id}>
                  <TableCell>
                    <div className="font-medium">{humanizeEnum(row.recommendationType)}</div>
                    {row.recommendedDate && (
                      <div className="text-xs text-muted-foreground">
                        Proposed {formatDate(row.recommendedDate)}
                      </div>
                    )}
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={row.status} />
                    {row.status === 'Approved' && (
                      <div className="mt-1 flex items-center gap-1 text-xs text-amber-600 dark:text-amber-500">
                        <TriangleAlert className="h-3 w-3" />
                        Not dispatched
                      </div>
                    )}
                  </TableCell>
                  <TableCell className="max-w-xs text-sm text-muted-foreground">
                    {row.notes ?? '—'}
                    {row.resolutionNotes && (
                      <div className="mt-1 text-xs">HR: {row.resolutionNotes}</div>
                    )}
                  </TableCell>
                  <TableCell className="text-sm">
                    <TargetLink recommendation={row} />
                  </TableCell>
                  {allowDecide && (
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-1">
                        {row.status === 'Proposed' && (
                          <>
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() => approve.mutate(row.id)}
                              disabled={approve.isPending}
                            >
                              <CheckCircle2 className="mr-1 h-4 w-4" />
                              Approve
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() => setClosing({ item: row, action: 'reject' })}
                            >
                              <XCircle className="mr-1 h-4 w-4" />
                              Reject
                            </Button>
                          </>
                        )}
                        {row.status === 'Approved' && (
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={() => retry.mutate(row.id)}
                            disabled={retry.isPending}
                          >
                            <RefreshCw className="mr-1 h-4 w-4" />
                            Retry
                          </Button>
                        )}
                      </div>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      {/* ── Propose ──────────────────────────────────────────────────────────── */}
      <Dialog open={proposeOpen} onOpenChange={setProposeOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Propose an outcome</DialogTitle>
            <DialogDescription>
              HR reviews this. Once approved it creates a real record in the module that owns the
              outcome — a pay proposal, an employment-action proposal, a training request, a PIP.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="recommendationType">Outcome</Label>
              <Select value={type} onValueChange={(v) => setType(v as RecommendationType)}>
                <SelectTrigger id="recommendationType">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {TYPES.map((t) => (
                    <SelectItem key={t} value={t}>
                      {humanizeEnum(t)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="recommendationNotes">Why</Label>
              <Textarea
                id="recommendationNotes"
                rows={4}
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder="What in this appraisal supports it. Carried onto the record that gets created."
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setProposeOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => propose.mutate()} disabled={propose.isPending}>
              {propose.isPending ? 'Proposing…' : 'Propose'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Reject / dismiss ─────────────────────────────────────────────────── */}
      <Dialog open={!!closing} onOpenChange={(o) => !o && setClosing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {closing?.action === 'reject' ? 'Reject' : 'Dismiss'}{' '}
              {closing ? humanizeEnum(closing.item.recommendationType) : ''}
            </DialogTitle>
            <DialogDescription>
              Nothing is created downstream. The reason is kept on the recommendation.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="closingNotes">Reason</Label>
            <Textarea
              id="closingNotes"
              rows={3}
              value={closingNotes}
              onChange={(e) => setClosingNotes(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setClosing(null)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={() => closing && close.mutate(closing)}
              disabled={!closing || close.isPending}
            >
              {close.isPending ? 'Closing…' : 'Confirm'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}

/** Links a dispatched recommendation to whatever it became, where that screen exists. */
export function TargetLink({
  recommendation,
}: {
  recommendation: AppraisalOutcomeRecommendation;
}) {
  const { targetEntityType, targetEntityId } = recommendation;
  if (!targetEntityType || !targetEntityId) {
    return <span className="text-muted-foreground">—</span>;
  }

  const route = RECOMMENDATION_TARGET_ROUTES[targetEntityType];
  const label = humanizeEnum(targetEntityType);

  if (!route) {
    // The outcome exists, but its module's UI is not built yet — say so rather than
    // rendering a link that goes nowhere.
    return <span className="text-muted-foreground">{label} created</span>;
  }

  return (
    <Button variant="link" size="sm" className="h-auto p-0" asChild>
      <Link href={route(targetEntityId)}>Open {label}</Link>
    </Button>
  );
}
