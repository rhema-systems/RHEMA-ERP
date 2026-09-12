'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarClock, Loader2, Plus, Star, StarOff, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { talentPoolService } from '@/services/hr/talent-pool.service';
import {
  TALENT_POOL_SOURCES,
  TALENT_POOL_STATUSES,
  type TalentPoolSource,
  type TalentPoolStatus,
} from '@/types/hr/talent-pool';

/**
 * The candidate's talent-pool membership: status, source, review date, segments, and the
 * vacancies their profile matches. Add/remove go through the rich endpoints so the source,
 * reason and review date are recorded — the old flat toggle recorded none of them.
 */
export function TalentPoolPanel({
  candidateId,
  canManage,
}: {
  candidateId: string;
  canManage: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [addOpen, setAddOpen] = useState(false);
  const [addSource, setAddSource] = useState<TalentPoolSource>('RecruiterAdded');
  const [addNotes, setAddNotes] = useState('');
  const [addReviewDate, setAddReviewDate] = useState('');
  const [addSegmentIds, setAddSegmentIds] = useState<string[]>([]);

  const [removeOpen, setRemoveOpen] = useState(false);
  const [removeReason, setRemoveReason] = useState('');

  const [segmentOpen, setSegmentOpen] = useState(false);
  const [segmentToAdd, setSegmentToAdd] = useState('');
  const [segmentNotes, setSegmentNotes] = useState('');

  const [reviewDateDraft, setReviewDateDraft] = useState<string | null>(null);

  const pool = useQuery({
    queryKey: ['hr', 'talent-pool-candidate', candidateId],
    queryFn: () => talentPoolService.getCandidate(candidateId),
    enabled: !!candidateId,
  });

  const segments = useQuery({
    queryKey: ['hr', 'talent-segments'],
    queryFn: () => talentPoolService.getSegments(),
    enabled: canManage,
  });

  const matches = useQuery({
    queryKey: ['hr', 'talent-pool-candidate-matches', candidateId],
    queryFn: () => talentPoolService.matchCandidateToVacancies(candidateId),
    enabled: !!candidateId && pool.data?.isInTalentPool === true,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'talent-pool-candidate', candidateId] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'candidate-detail', candidateId] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'talent-pool'] });
  };

  const fail = (title: string) => (e: any) =>
    toast({ title, description: e?.message, variant: 'destructive' });

  const add = useMutation({
    mutationFn: () =>
      talentPoolService.addToPool(candidateId, {
        source: addSource,
        segmentIds: addSegmentIds,
        notes: addNotes.trim() || null,
        reviewDate: addReviewDate || null,
      }),
    onSuccess: async () => {
      await refresh();
      setAddOpen(false);
      setAddNotes('');
      setAddReviewDate('');
      setAddSegmentIds([]);
      toast({ title: 'Added to the talent pool' });
    },
    onError: fail('Could not add to the pool'),
  });

  const remove = useMutation({
    mutationFn: () =>
      talentPoolService.removeFromPool(candidateId, { reason: removeReason.trim() || null }),
    onSuccess: async () => {
      await refresh();
      setRemoveOpen(false);
      setRemoveReason('');
      toast({ title: 'Removed from the talent pool' });
    },
    onError: fail('Could not remove from the pool'),
  });

  const setStatus = useMutation({
    mutationFn: (status: TalentPoolStatus) => talentPoolService.updateStatus(candidateId, status),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Status updated' });
    },
    onError: fail('Could not update the status'),
  });

  const setReviewDate = useMutation({
    mutationFn: (date: string) => talentPoolService.updateReviewDate(candidateId, date),
    onSuccess: async () => {
      await refresh();
      setReviewDateDraft(null);
      toast({ title: 'Review date updated' });
    },
    onError: fail('Could not set the review date'),
  });

  const addSegment = useMutation({
    mutationFn: () =>
      talentPoolService.addToSegment(candidateId, segmentToAdd, segmentNotes.trim() || null),
    onSuccess: async () => {
      await refresh();
      setSegmentOpen(false);
      setSegmentToAdd('');
      setSegmentNotes('');
      toast({ title: 'Segment assigned' });
    },
    onError: fail('Could not assign the segment'),
  });

  const removeSegment = useMutation({
    mutationFn: (segmentId: string) => talentPoolService.removeFromSegment(candidateId, segmentId),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Segment removed' });
    },
    onError: fail('Could not remove the segment'),
  });

  if (pool.isLoading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const c = pool.data;
  if (!c) {
    return <EmptyState title="Candidate not found" description="It may have been removed." />;
  }

  const availableSegments = (segments.data ?? []).filter(
    (s) => !c.segments.some((m) => m.segmentId === s.id),
  );

  return (
    <div className="space-y-4">
      {!c.isInTalentPool ? (
        <Card>
          <CardContent className="py-10">
            <EmptyState
              icon={Star}
              title="Not in the talent pool"
              description={
                c.talentPoolRemovalReason
                  ? `Removed previously: ${c.talentPoolRemovalReason}`
                  : 'Pooled candidates stay on the radar for future vacancies.'
              }
            />
            {canManage && (
              <div className="mt-4 flex justify-center">
                <Button onClick={() => setAddOpen(true)}>
                  <Star className="mr-2 h-4 w-4" />
                  Add to the pool
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      ) : (
        <>
          <Card>
            <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-3">
              <div>
                <CardTitle className="text-base">Pool membership</CardTitle>
                <CardDescription>
                  In the pool {c.daysInPool} days · {c.engagementCount} engagement
                  {c.engagementCount === 1 ? '' : 's'}
                </CardDescription>
              </div>
              {canManage && (
                <Button variant="outline" size="sm" onClick={() => setRemoveOpen(true)}>
                  <StarOff className="mr-1.5 h-4 w-4" />
                  Remove from pool
                </Button>
              )}
            </CardHeader>
            <CardContent className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <div className="space-y-1.5">
                <Label className="text-xs text-muted-foreground">Status</Label>
                {canManage ? (
                  <Select
                    value={c.talentPoolStatus}
                    onValueChange={(v) => setStatus.mutate(v as TalentPoolStatus)}
                    disabled={setStatus.isPending}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {TALENT_POOL_STATUSES.map((s) => (
                        <SelectItem key={s} value={s}>
                          {humanizeEnum(s)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                ) : (
                  <div>
                    <StatusBadge status={c.talentPoolStatus} />
                  </div>
                )}
              </div>
              <div className="space-y-1.5">
                <Label className="text-xs text-muted-foreground">Source</Label>
                <p className="text-sm">{humanizeEnum(c.talentPoolSource)}</p>
              </div>
              <div className="space-y-1.5">
                <Label className="text-xs text-muted-foreground">Added</Label>
                <p className="text-sm">{formatDate(c.talentPoolAddedDate)}</p>
              </div>
              <div className="space-y-1.5">
                <Label className="text-xs text-muted-foreground">
                  Next review
                  {c.isOverdueForReview && (
                    <Badge variant="destructive" className="ml-2">
                      Overdue
                    </Badge>
                  )}
                </Label>
                {canManage ? (
                  reviewDateDraft !== null ? (
                    <div className="flex items-center gap-1.5">
                      <Input
                        type="date"
                        value={reviewDateDraft}
                        onChange={(e) => setReviewDateDraft(e.target.value)}
                        className="h-8"
                      />
                      <Button
                        size="sm"
                        disabled={!reviewDateDraft || setReviewDate.isPending}
                        onClick={() => reviewDateDraft && setReviewDate.mutate(reviewDateDraft)}
                      >
                        Save
                      </Button>
                      <Button size="sm" variant="ghost" onClick={() => setReviewDateDraft(null)}>
                        Cancel
                      </Button>
                    </div>
                  ) : (
                    <button
                      type="button"
                      className="flex items-center gap-1.5 text-sm hover:underline"
                      onClick={() =>
                        setReviewDateDraft(c.talentPoolReviewDate ? c.talentPoolReviewDate.slice(0, 10) : '')
                      }
                    >
                      <CalendarClock className="h-3.5 w-3.5 text-muted-foreground" />
                      {c.talentPoolReviewDate ? formatDate(c.talentPoolReviewDate) : 'Set a date'}
                    </button>
                  )
                ) : (
                  <p className="text-sm">
                    {c.talentPoolReviewDate ? formatDate(c.talentPoolReviewDate) : '—'}
                  </p>
                )}
              </div>
              {c.talentPoolNotes && (
                <div className="space-y-1.5 sm:col-span-2 lg:col-span-4">
                  <Label className="text-xs text-muted-foreground">Notes</Label>
                  <p className="whitespace-pre-wrap text-sm">{c.talentPoolNotes}</p>
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-3">
              <div>
                <CardTitle className="text-base">Segments</CardTitle>
                <CardDescription>Named groupings the pool is worked through.</CardDescription>
              </div>
              {canManage && (
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setSegmentOpen(true)}
                  disabled={segments.isLoading}
                >
                  <Plus className="mr-1.5 h-4 w-4" />
                  Assign
                </Button>
              )}
            </CardHeader>
            <CardContent>
              {c.segments.length === 0 ? (
                <p className="text-sm text-muted-foreground">Not in any segment.</p>
              ) : (
                <div className="flex flex-wrap gap-2">
                  {c.segments.map((m) => (
                    <Badge
                      key={m.id}
                      variant="secondary"
                      className="gap-1.5 py-1 pl-2.5"
                      style={m.segmentColor ? { borderColor: m.segmentColor } : undefined}
                    >
                      {m.segmentName}
                      {canManage && (
                        <button
                          type="button"
                          aria-label={`Remove from ${m.segmentName}`}
                          onClick={() => removeSegment.mutate(m.segmentId)}
                          className="rounded-full p-0.5 hover:bg-muted-foreground/20"
                        >
                          <X className="h-3 w-3" />
                        </button>
                      )}
                    </Badge>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">Matching open vacancies</CardTitle>
              <CardDescription>
                Scored on experience, work-mode preference and the application deadline.
              </CardDescription>
            </CardHeader>
            <CardContent className="p-0">
              {matches.isLoading ? (
                <div className="flex items-center justify-center py-8">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : (matches.data ?? []).length === 0 ? (
                <div className="py-8">
                  <EmptyState title="No open vacancies to match against" />
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Vacancy</TableHead>
                      <TableHead className="w-28">Deadline</TableHead>
                      <TableHead className="w-20 text-right">Score</TableHead>
                      <TableHead>Why</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(matches.data ?? []).map((m) => (
                      <TableRow key={m.vacancyId}>
                        <TableCell>
                          <Link
                            href={`/hr/recruitment/vacancies/${m.vacancyId}`}
                            className="font-medium hover:underline"
                          >
                            {m.jobTitle}
                          </Link>
                          <div className="text-xs text-muted-foreground">{m.vacancyNumber}</div>
                        </TableCell>
                        <TableCell className="text-sm">
                          {m.applicationDeadline ? formatDate(m.applicationDeadline) : '—'}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">{m.matchScore}</TableCell>
                        <TableCell className="text-xs text-muted-foreground">
                          {m.matchReasons.join(' · ')}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </>
      )}

      {/* add to pool */}
      <Dialog open={addOpen} onOpenChange={setAddOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Add to the talent pool</DialogTitle>
            <DialogDescription>
              Where this candidate came from, and when to check back in on them.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Source</Label>
              <Select value={addSource} onValueChange={(v) => setAddSource(v as TalentPoolSource)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {TALENT_POOL_SOURCES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {humanizeEnum(s)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="pool-review">Review date</Label>
              <Input
                id="pool-review"
                type="date"
                value={addReviewDate}
                onChange={(e) => setAddReviewDate(e.target.value)}
              />
            </div>
            {(segments.data ?? []).length > 0 && (
              <div className="space-y-2">
                <Label>Segments</Label>
                <div className="grid max-h-40 gap-2 overflow-y-auto rounded-md border p-3 sm:grid-cols-2">
                  {(segments.data ?? []).map((s) => (
                    <label key={s.id} className="flex items-center gap-2 text-sm">
                      <Checkbox
                        checked={addSegmentIds.includes(s.id)}
                        onCheckedChange={(v) =>
                          setAddSegmentIds((ids) =>
                            v === true ? [...ids, s.id] : ids.filter((x) => x !== s.id),
                          )
                        }
                      />
                      {s.name}
                    </label>
                  ))}
                </div>
              </div>
            )}
            <div className="space-y-2">
              <Label htmlFor="pool-notes">Notes</Label>
              <Textarea
                id="pool-notes"
                value={addNotes}
                onChange={(e) => setAddNotes(e.target.value)}
                maxLength={2000}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddOpen(false)}>
              Cancel
            </Button>
            <Button disabled={add.isPending} onClick={() => add.mutate()}>
              {add.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add to pool
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* remove from pool */}
      <Dialog open={removeOpen} onOpenChange={setRemoveOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Remove from the talent pool</DialogTitle>
            <DialogDescription>
              The candidate record survives; only the pool membership ends.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="pool-remove-reason">Reason</Label>
            <Textarea
              id="pool-remove-reason"
              value={removeReason}
              onChange={(e) => setRemoveReason(e.target.value)}
              maxLength={1000}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRemoveOpen(false)}>
              Cancel
            </Button>
            <Button variant="destructive" disabled={remove.isPending} onClick={() => remove.mutate()}>
              {remove.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Remove
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* assign segment */}
      <Dialog open={segmentOpen} onOpenChange={setSegmentOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Assign a segment</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Segment</Label>
              <Select value={segmentToAdd} onValueChange={setSegmentToAdd}>
                <SelectTrigger>
                  <SelectValue placeholder="Choose a segment" />
                </SelectTrigger>
                <SelectContent>
                  {availableSegments.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {availableSegments.length === 0 && (
                <p className="text-xs text-muted-foreground">
                  Already in every active segment — create more from the talent pool screen.
                </p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="segment-notes">Notes</Label>
              <Textarea
                id="segment-notes"
                value={segmentNotes}
                onChange={(e) => setSegmentNotes(e.target.value)}
                maxLength={500}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setSegmentOpen(false)}>
              Cancel
            </Button>
            <Button disabled={!segmentToAdd || addSegment.isPending} onClick={() => addSegment.mutate()}>
              {addSegment.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Assign
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
