'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Calculator, Inbox, Loader2, MoveRight, XCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
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
import { GatedPhoto } from '@/components/hr/common/PhotoDialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { formatDate } from '@/lib/hr/attendance-format';
import { jobVacancyService } from '@/services/hr/recruitment.service';
import { applicationPipelineService, jobCandidateService } from '@/services/hr/recruitment-pipeline.service';
import {
  INBOX_STAGE_ID,
  type BulkOperationResult,
  type PipelineStageHeader,
} from '@/types/hr/recruitment-pipeline';

/** Renders the per-item outcomes of a bulk operation — these are partial by design. */
function BulkResultNote({ result }: { result: BulkOperationResult | null }) {
  if (!result) return null;
  const failures = result.results.filter((r) => !r.success);
  return (
    <div className="space-y-1 rounded-md border p-3 text-sm">
      <p>
        <strong>{result.succeeded}</strong> succeeded, <strong>{result.skipped}</strong> skipped.
      </p>
      {failures.length > 0 && (
        <ul className="list-inside list-disc text-xs text-muted-foreground">
          {failures.slice(0, 8).map((f) => (
            <li key={f.applicationId}>{f.message}</li>
          ))}
          {failures.length > 8 && <li>…and {failures.length - 8} more</li>}
        </ul>
      )}
    </div>
  );
}

/**
 * The pipeline board for one vacancy.
 *
 * Deliberately a **stage bar plus a paginated table**, not a drag-and-drop Kanban. The stage list is
 * the endpoint that filters, sorts and pages; the board read returns every card in every column at
 * once, which does not scale past a busy vacancy. The bar gives the same at-a-glance shape without
 * the payload.
 *
 * ⚠ The first bucket is the **inbox** — applications that have arrived but have not been placed in
 * any stage. It is synthetic, with an all-zeroes stage id, and is where most work starts.
 */
export default function VacancyPipelinePage() {
  const params = useParams();
  const vacancyId = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [stageId, setStageId] = useState<string>(INBOX_STAGE_ID);
  const [page, setPage] = useState(1);
  const [nameSearch, setNameSearch] = useState('');
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [bulkAction, setBulkAction] = useState<null | 'move' | 'reject'>(null);
  const [targetStageId, setTargetStageId] = useState('');
  const [reason, setReason] = useState('');
  const [lastResult, setLastResult] = useState<BulkOperationResult | null>(null);

  const vacancy = useQuery({
    queryKey: ['hr', 'vacancies', vacancyId],
    queryFn: () => jobVacancyService.getById(vacancyId),
    enabled: !!vacancyId,
  });

  const overview = useQuery({
    queryKey: ['hr', 'pipeline-overview', vacancyId],
    queryFn: () => applicationPipelineService.getOverview(vacancyId),
    enabled: !!vacancyId,
  });

  const list = useQuery({
    queryKey: ['hr', 'pipeline-stage-apps', vacancyId, stageId, page, nameSearch],
    queryFn: () =>
      applicationPipelineService.getStageApplications(vacancyId, stageId, {
        page,
        pageSize: 20,
        nameSearch: nameSearch.trim() || undefined,
        sortBy: 'date',
        sortDesc: true,
      }),
    enabled: !!vacancyId,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'pipeline-overview', vacancyId] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'pipeline-stage-apps', vacancyId] });
    setSelected(new Set());
  };

  const runScoring = useMutation({
    mutationFn: () => applicationPipelineService.runScoring(vacancyId),
    onSuccess: async (r) => {
      await refresh();
      toast({
        title: `Scored ${r.succeeded} of ${r.total}`,
        description: r.failed > 0 ? `${r.failed} could not be scored.` : undefined,
      });
    },
    onError: (e: any) => toast({ title: 'Could not run scoring', description: e?.message, variant: 'destructive' }),
  });

  const bulk = useMutation({
    mutationFn: () => {
      const ids = [...selected];
      return bulkAction === 'move'
        ? applicationPipelineService.bulkMove(ids, targetStageId)
        : applicationPipelineService.bulkPipelineReject(ids, reason.trim());
    },
    onSuccess: async (result) => {
      await refresh();
      setLastResult(result);
      setBulkAction(null);
      setTargetStageId('');
      setReason('');
    },
    onError: (e: any) => toast({ title: 'Refused', description: e?.message, variant: 'destructive' }),
  });

  const stages: PipelineStageHeader[] = overview.data?.stages ?? [];
  const rows = list.data?.items ?? [];
  const allSelected = rows.length > 0 && rows.every((r) => selected.has(r.applicationId));

  const realStages = useMemo(() => stages.filter((s) => !s.isInbox), [stages]);

  const toggleAll = () => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (allSelected) rows.forEach((r) => next.delete(r.applicationId));
      else rows.forEach((r) => next.add(r.applicationId));
      return next;
    });
  };

  const toggleOne = (id: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={vacancy.data?.jobTitle || vacancy.data?.vacancyNumber || 'Pipeline'}
        description="Applications by stage. Moves are checked against the pipeline's transition rules."
        backHref={`/hr/recruitment/vacancies/${vacancyId}`}
        actions={
          <div className="flex gap-2">
            <Button variant="outline" asChild>
              <Link href={`/hr/recruitment/vacancies/${vacancyId}/screening`}>Screening</Link>
            </Button>
            <Button variant="outline" onClick={() => runScoring.mutate()} disabled={runScoring.isPending}>
              {runScoring.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Calculator className="mr-2 h-4 w-4" />
              )}
              Score all
            </Button>
          </div>
        }
      />

      {overview.isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : overview.data && !overview.data.hasPipeline ? (
        <Card>
          <CardContent className="pt-6">
            <EmptyState
              title="No pipeline assigned"
              description="This vacancy has no recruitment pipeline, so its applications cannot be moved through stages. Assign one on the vacancy, then come back."
            />
          </CardContent>
        </Card>
      ) : (
        <>
          <div className="flex flex-wrap gap-2">
            {stages.map((s) => (
              <button
                key={s.stageId}
                type="button"
                onClick={() => {
                  setStageId(s.stageId);
                  setPage(1);
                  setSelected(new Set());
                }}
                className={`flex items-center gap-2 rounded-md border px-3 py-2 text-sm transition-colors ${
                  s.stageId === stageId ? 'border-primary bg-primary/5' : 'hover:bg-muted'
                }`}
              >
                {s.isInbox && <Inbox className="h-3.5 w-3.5" />}
                <span className="font-medium">{s.stageName}</span>
                <Badge variant="secondary">{s.applicationCount}</Badge>
              </button>
            ))}
          </div>

          <Card>
            <CardHeader className="flex-row items-end justify-between gap-4 space-y-0 pb-4">
              <div className="w-full max-w-xs space-y-1.5">
                <Label htmlFor="pipeline-search">Search by candidate name</Label>
                <Input
                  id="pipeline-search"
                  value={nameSearch}
                  onChange={(e) => {
                    setNameSearch(e.target.value);
                    setPage(1);
                  }}
                  placeholder="Type a name"
                />
              </div>
              {selected.size > 0 && (
                <div className="flex items-center gap-2">
                  <span className="text-sm text-muted-foreground">{selected.size} selected</span>
                  <Button size="sm" variant="outline" onClick={() => setBulkAction('move')}>
                    <MoveRight className="mr-2 h-3.5 w-3.5" />
                    Move
                  </Button>
                  <Button size="sm" variant="outline" onClick={() => setBulkAction('reject')}>
                    <XCircle className="mr-2 h-3.5 w-3.5" />
                    Reject
                  </Button>
                </div>
              )}
            </CardHeader>
            <CardContent className="p-0">
              {list.isLoading ? (
                <div className="flex items-center justify-center py-12">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : rows.length === 0 ? (
                <EmptyState
                  title="Nothing in this stage"
                  description={
                    stageId === INBOX_STAGE_ID
                      ? 'Every application has been placed in a pipeline stage.'
                      : 'Move applications here from the inbox or an earlier stage.'
                  }
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-10">
                        <Checkbox checked={allSelected} onCheckedChange={toggleAll} aria-label="Select all" />
                      </TableHead>
                      <TableHead>Candidate</TableHead>
                      <TableHead className="w-36">Number</TableHead>
                      <TableHead className="w-32">Applied</TableHead>
                      <TableHead className="w-36">Status</TableHead>
                      <TableHead className="w-24 text-right">Score</TableHead>
                      <TableHead className="w-24 text-right">Exp.</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {rows.map((r) => (
                      <TableRow key={r.applicationId}>
                        <TableCell>
                          <Checkbox
                            checked={selected.has(r.applicationId)}
                            onCheckedChange={() => toggleOne(r.applicationId)}
                            aria-label={`Select ${r.candidateName}`}
                          />
                        </TableCell>
                        <TableCell>
                          {/* Round 4, lane B5 — the photograph the candidate screens have had
                              since round 3, on the board a recruiter actually works from. */}
                          <div className="flex items-center gap-3">
                            <GatedPhoto
                              endpoint={jobCandidateService.photoUrl(r.candidateId)}
                              enabled={r.candidateHasPhoto}
                              alt={r.candidateName}
                              className="h-8 w-8"
                            />
                            <div>
                              <Link
                                href={`/hr/recruitment/applications/${r.applicationId}`}
                                className="font-medium hover:underline"
                              >
                                {r.candidateName || '—'}
                              </Link>
                              <div className="text-xs text-muted-foreground">
                                {r.candidateEmail}
                                {r.isInternalCandidate && (
                                  <Badge variant="secondary" className="ml-2">
                                    Internal
                                  </Badge>
                                )}
                              </div>
                            </div>
                          </div>
                        </TableCell>
                        <TableCell className="font-mono text-xs">{r.applicationNumber}</TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {formatDate(r.dateApplied)}
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={r.statusName} />
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {r.autoScore != null ? (
                            <span className={r.scoreIsStale ? 'text-muted-foreground' : undefined}>
                              {r.autoScore.toFixed(1)}
                              {r.scoreIsStale && '*'}
                            </span>
                          ) : (
                            '—'
                          )}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {r.yearsOfExperience ?? '—'}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>

          {(list.data?.totalCount ?? 0) > 0 && (
            <div className="flex items-center justify-between text-sm text-muted-foreground">
              <span>
                Page {list.data?.page ?? 1} of {list.data?.totalPages ?? 1} ·{' '}
                {list.data?.totalCount ?? 0} in this stage
              </span>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!list.data?.hasPrevious}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!list.data?.hasNext}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          )}

          {lastResult && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">Last bulk operation</CardTitle>
              </CardHeader>
              <CardContent>
                <BulkResultNote result={lastResult} />
              </CardContent>
            </Card>
          )}
        </>
      )}

      <Dialog open={!!bulkAction} onOpenChange={(o) => !o && setBulkAction(null)}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>
              {bulkAction === 'move'
                ? `Move ${selected.size} application${selected.size === 1 ? '' : 's'}`
                : `Reject ${selected.size} application${selected.size === 1 ? '' : 's'}`}
            </DialogTitle>
            <DialogDescription>
              Each application is processed on its own, so one refusal does not stop the rest — the
              per-application outcomes come back afterwards.
            </DialogDescription>
          </DialogHeader>

          {bulkAction === 'move' ? (
            <div className="space-y-1.5">
              <Label>Target stage</Label>
              <Select value={targetStageId} onValueChange={setTargetStageId}>
                <SelectTrigger>
                  <SelectValue placeholder="Select a stage" />
                </SelectTrigger>
                <SelectContent>
                  {realStages.map((s) => (
                    <SelectItem key={s.stageId} value={s.stageId}>
                      {s.order}. {s.stageName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          ) : (
            <div className="space-y-1.5">
              <Label>
                Rejection reason<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Textarea value={reason} onChange={(e) => setReason(e.target.value)} rows={4} />
            </div>
          )}

          <DialogFooter>
            <Button variant="outline" onClick={() => setBulkAction(null)}>
              Cancel
            </Button>
            <Button
              disabled={
                bulk.isPending || (bulkAction === 'move' ? !targetStageId : !reason.trim())
              }
              onClick={() => bulk.mutate()}
            >
              {bulk.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
