'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, Loader2, Pencil, Plus, Trash2, Users } from 'lucide-react';
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { talentPoolService } from '@/services/hr/talent-pool.service';
import {
  BULK_POOL_OPERATIONS,
  TALENT_POOL_SOURCES,
  TALENT_POOL_STATUSES,
  type BulkPoolOperation,
  type CandidateTalentSegment,
  type TalentPoolBulkResult,
  type TalentPoolSource,
  type TalentPoolStatus,
} from '@/types/hr/talent-pool';

const PAGE_SIZE = 25;
const ANY = '__any__';

interface SegmentDraft {
  id?: string;
  name: string;
  description: string;
  color: string;
  isActive: boolean;
}

/**
 * Recruitment's candidate CRM: who is in the talent pool, how warm they are, and the segments
 * the pool is worked through. ⚠ Not succession's employee talent pools (/hr/succession/pools).
 */
export default function TalentPoolPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyPermission, hasPermission } = useAuth();
  const canManage = hasAnyPermission(['HR.Recruitment.Write', 'HR.Recruitment.Admin']);
  const canAdmin = hasPermission('HR.Recruitment.Admin');

  // filters
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<string>(ANY);
  const [source, setSource] = useState<string>(ANY);
  const [segmentId, setSegmentId] = useState<string>(ANY);
  const [overdueOnly, setOverdueOnly] = useState(false);
  const [page, setPage] = useState(1);

  // bulk
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [bulkOp, setBulkOp] = useState<BulkPoolOperation>('AssignSegment');
  const [bulkSegmentId, setBulkSegmentId] = useState('');
  const [bulkStatus, setBulkStatus] = useState<TalentPoolStatus>('Active');
  const [bulkNotes, setBulkNotes] = useState('');
  const [bulkResult, setBulkResult] = useState<TalentPoolBulkResult | null>(null);

  // segments management
  const [segmentDraft, setSegmentDraft] = useState<SegmentDraft | null>(null);

  const analytics = useQuery({
    queryKey: ['hr', 'talent-pool', 'analytics'],
    queryFn: () => talentPoolService.getAnalytics(),
  });

  const segments = useQuery({
    queryKey: ['hr', 'talent-pool', 'segments-all'],
    queryFn: () => talentPoolService.getAllSegments(),
  });

  const candidates = useQuery({
    queryKey: [
      'hr', 'talent-pool', 'candidates',
      { search, status, source, segmentId, overdueOnly, page },
    ],
    queryFn: () =>
      talentPoolService.getCandidates({
        search: search.trim() || undefined,
        status: status === ANY ? undefined : (status as TalentPoolStatus),
        source: source === ANY ? undefined : (source as TalentPoolSource),
        segmentIds: segmentId === ANY ? undefined : [segmentId],
        overdueForReview: overdueOnly || undefined,
        pageNumber: page,
        pageSize: PAGE_SIZE,
        sortBy: 'fullname',
      }),
  });

  const refreshAll = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'talent-pool'] });
  };

  const bulk = useMutation({
    mutationFn: () =>
      talentPoolService.bulk({
        candidateIds: [...selected],
        operation: bulkOp,
        segmentId: bulkOp === 'AssignSegment' || bulkOp === 'RemoveSegment' ? bulkSegmentId : null,
        status: bulkOp === 'SetStatus' ? bulkStatus : null,
        notes: bulkNotes.trim() || null,
      }),
    onSuccess: async (result) => {
      setBulkResult(result);
      setSelected(new Set());
      await refreshAll();
      toast({
        title: `Applied to ${result.succeeded} candidate${result.succeeded === 1 ? '' : 's'}`,
        description: result.skipped > 0 ? `${result.skipped} skipped — details below the table.` : undefined,
      });
    },
    onError: (e: any) =>
      toast({ title: 'Bulk operation refused', description: e?.message, variant: 'destructive' }),
  });

  const saveSegment = useMutation({
    mutationFn: (d: SegmentDraft) => {
      const payload = {
        name: d.name.trim(),
        description: d.description.trim() || null,
        color: d.color.trim() || null,
        isActive: d.isActive,
      };
      return d.id
        ? talentPoolService.updateSegment(d.id, payload)
        : talentPoolService.createSegment(payload);
    },
    onSuccess: async () => {
      await refreshAll();
      setSegmentDraft(null);
      toast({ title: 'Segment saved' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not save the segment', description: e?.message, variant: 'destructive' }),
  });

  const deleteSegment = useMutation({
    mutationFn: (id: string) => talentPoolService.deleteSegment(id),
    onSuccess: async () => {
      await refreshAll();
      toast({ title: 'Segment deleted' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not delete it', description: e?.message, variant: 'destructive' }),
  });

  const rows = candidates.data?.items ?? [];
  const totalPages = candidates.data?.totalPages ?? 1;
  const allOnPageSelected = rows.length > 0 && rows.every((r) => selected.has(r.id));

  const bulkReady = useMemo(() => {
    if (selected.size === 0) return false;
    if ((bulkOp === 'AssignSegment' || bulkOp === 'RemoveSegment') && !bulkSegmentId) return false;
    return true;
  }, [selected, bulkOp, bulkSegmentId]);

  const a = analytics.data;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Talent pool"
        description="Pooled candidates kept warm for future vacancies, and the segments they are worked through."
        backHref="/hr/recruitment"
      />

      {a && (
        <MetricTiles
          tiles={[
            { label: 'In the pool', value: a.totalInPool },
            { label: 'Active', value: a.active },
            { label: 'Dormant', value: a.dormant },
            { label: 'Overdue for review', value: a.overdueForReview },
            { label: 'Added this month', value: a.addedThisMonth },
            { label: 'Converted this year', value: a.convertedThisYear },
          ]}
        />
      )}

      <Tabs defaultValue="pool">
        <TabsList>
          <TabsTrigger value="pool">Pool</TabsTrigger>
          <TabsTrigger value="segments">Segments ({(segments.data ?? []).length})</TabsTrigger>
        </TabsList>

        <TabsContent value="pool" className="mt-4 space-y-4">
          <Card>
            <CardContent className="flex flex-wrap items-end gap-3 pt-6">
              <div className="min-w-52 flex-1 space-y-1.5">
                <Label htmlFor="pool-search">Search</Label>
                <Input
                  id="pool-search"
                  placeholder="Name, email, headline, employer…"
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPage(1);
                  }}
                />
              </div>
              <div className="space-y-1.5">
                <Label>Status</Label>
                <Select value={status} onValueChange={(v) => { setStatus(v); setPage(1); }}>
                  <SelectTrigger className="w-36">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ANY}>Any status</SelectItem>
                    {TALENT_POOL_STATUSES.map((s) => (
                      <SelectItem key={s} value={s}>
                        {humanizeEnum(s)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label>Source</Label>
                <Select value={source} onValueChange={(v) => { setSource(v); setPage(1); }}>
                  <SelectTrigger className="w-44">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ANY}>Any source</SelectItem>
                    {TALENT_POOL_SOURCES.map((s) => (
                      <SelectItem key={s} value={s}>
                        {humanizeEnum(s)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label>Segment</Label>
                <Select value={segmentId} onValueChange={(v) => { setSegmentId(v); setPage(1); }}>
                  <SelectTrigger className="w-44">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ANY}>Any segment</SelectItem>
                    {(segments.data ?? []).map((s) => (
                      <SelectItem key={s.id} value={s.id}>
                        {s.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <label className="flex h-10 items-center gap-2 text-sm">
                <Checkbox
                  checked={overdueOnly}
                  onCheckedChange={(v) => {
                    setOverdueOnly(v === true);
                    setPage(1);
                  }}
                />
                Overdue for review
              </label>
            </CardContent>
          </Card>

          {canManage && selected.size > 0 && (
            <Card>
              <CardContent className="flex flex-wrap items-end gap-3 pt-6">
                <div className="text-sm font-medium">
                  {selected.size} selected
                </div>
                <div className="space-y-1.5">
                  <Label>Operation</Label>
                  <Select value={bulkOp} onValueChange={(v) => setBulkOp(v as BulkPoolOperation)}>
                    <SelectTrigger className="w-44">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {BULK_POOL_OPERATIONS.map((op) => (
                        <SelectItem key={op} value={op}>
                          {humanizeEnum(op)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                {(bulkOp === 'AssignSegment' || bulkOp === 'RemoveSegment') && (
                  <div className="space-y-1.5">
                    <Label>Segment</Label>
                    <Select value={bulkSegmentId} onValueChange={setBulkSegmentId}>
                      <SelectTrigger className="w-44">
                        <SelectValue placeholder="Choose a segment" />
                      </SelectTrigger>
                      <SelectContent>
                        {(segments.data ?? [])
                          .filter((s) => s.isActive)
                          .map((s) => (
                            <SelectItem key={s.id} value={s.id}>
                              {s.name}
                            </SelectItem>
                          ))}
                      </SelectContent>
                    </Select>
                  </div>
                )}
                {bulkOp === 'SetStatus' && (
                  <div className="space-y-1.5">
                    <Label>Status</Label>
                    <Select value={bulkStatus} onValueChange={(v) => setBulkStatus(v as TalentPoolStatus)}>
                      <SelectTrigger className="w-36">
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
                  </div>
                )}
                <div className="min-w-44 flex-1 space-y-1.5">
                  <Label htmlFor="bulk-notes">Notes</Label>
                  <Input
                    id="bulk-notes"
                    value={bulkNotes}
                    onChange={(e) => setBulkNotes(e.target.value)}
                    maxLength={1000}
                  />
                </div>
                <Button disabled={!bulkReady || bulk.isPending} onClick={() => bulk.mutate()}>
                  {bulk.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Apply
                </Button>
                <Button variant="ghost" onClick={() => setSelected(new Set())}>
                  Clear
                </Button>
              </CardContent>
            </Card>
          )}

          <Card>
            <CardContent className="p-0">
              {candidates.isLoading ? (
                <div className="flex items-center justify-center py-16">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : rows.length === 0 ? (
                <div className="py-12">
                  <EmptyState
                    icon={Users}
                    title="No pooled candidates match"
                    description="Add candidates to the pool from their candidate page."
                  />
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      {canManage && (
                        <TableHead className="w-10">
                          <Checkbox
                            checked={allOnPageSelected}
                            aria-label="Select everyone on this page"
                            onCheckedChange={(v) =>
                              setSelected((prev) => {
                                const next = new Set(prev);
                                rows.forEach((r) => (v === true ? next.add(r.id) : next.delete(r.id)));
                                return next;
                              })
                            }
                          />
                        </TableHead>
                      )}
                      <TableHead>Candidate</TableHead>
                      <TableHead>Segments</TableHead>
                      <TableHead className="w-28">Status</TableHead>
                      <TableHead className="w-28">Added</TableHead>
                      <TableHead className="w-32">Last engaged</TableHead>
                      <TableHead className="w-32">Next review</TableHead>
                      <TableHead className="w-24">Experience</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {rows.map((r) => (
<TableRow
                        key={r.id}
                        className="cursor-pointer hover:bg-muted/50"
                        onClick={() => router.push(`/hr/recruitment/candidates/${r.id}`)}
                      >
                        {canManage && (
                          <TableCell onClick={(e) => e.stopPropagation()}>
                            <Checkbox
                              checked={selected.has(r.id)}
                              aria-label={`Select ${r.fullName}`}
                              onCheckedChange={(v) =>
                                setSelected((prev) => {
                                  const next = new Set(prev);
                                  if (v === true) next.add(r.id);
                                  else next.delete(r.id);
                                  return next;
                                })
                              }
                            />
                          </TableCell>
                        )}
                        <TableCell>
                          <Link
                            href={`/hr/recruitment/candidates/${r.id}`}
                            className="font-medium hover:underline"
                          >
                            {r.fullName}
                          </Link>
                          <div className="text-xs text-muted-foreground">
                            {r.headline || r.currentJobTitle || r.email}
                          </div>
                        </TableCell>
                        <TableCell>
                          <div className="flex flex-wrap gap-1">
                            {r.segments.length === 0 ? (
                              <span className="text-xs text-muted-foreground">—</span>
                            ) : (
                              r.segments.map((m) => (
                                <Badge key={m.id} variant="secondary" className="text-xs">
                                  {m.segmentName}
                                </Badge>
                              ))
                            )}
                          </div>
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={r.talentPoolStatus} />
                        </TableCell>
                        <TableCell className="text-sm">{formatDate(r.talentPoolAddedDate)}</TableCell>
                        <TableCell className="text-sm">
                          {r.lastEngagedDate ? formatDate(r.lastEngagedDate) : 'Never'}
                        </TableCell>
                        <TableCell className="text-sm">
                          {r.talentPoolReviewDate ? (
                            <span className={r.isOverdueForReview ? 'font-medium text-destructive' : ''}>
                              {formatDate(r.talentPoolReviewDate)}
                            </span>
                          ) : (
                            '—'
                          )}
                        </TableCell>
                        <TableCell className="text-sm">
                          {r.totalYearsExperience != null ? `${r.totalYearsExperience} yrs` : '—'}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>

          {bulkResult && bulkResult.results.some((r) => !r.success) && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm">Skipped in the last bulk operation</CardTitle>
              </CardHeader>
              <CardContent>
                <ul className="space-y-1 text-sm text-muted-foreground">
                  {bulkResult.results
                    .filter((r) => !r.success)
                    .map((r) => (
                      <li key={r.candidateId ?? r.applicationId}>
                        {r.candidateId ?? r.applicationId}: {r.message ?? 'skipped'}
                      </li>
                    ))}
                </ul>
              </CardContent>
            </Card>
          )}

          {totalPages > 1 && (
            <div className="flex items-center justify-end gap-2">
              <span className="text-sm text-muted-foreground">
                Page {candidates.data?.page ?? page} of {totalPages} · {candidates.data?.totalCount ?? 0} candidates
              </span>
              <Button
                variant="outline"
                size="icon"
                disabled={page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                aria-label="Previous page"
              >
                <ChevronLeft className="h-4 w-4" />
              </Button>
              <Button
                variant="outline"
                size="icon"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
                aria-label="Next page"
              >
                <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          )}
        </TabsContent>

        <TabsContent value="segments" className="mt-4">
          <Card>
            <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-3">
              <div>
                <CardTitle className="text-base">Talent segments</CardTitle>
                <CardDescription>
                  Deleting a segment is Admin-tier; deactivate one to retire it from pickers instead.
                </CardDescription>
              </div>
              {canManage && (
                <Button
                  size="sm"
                  onClick={() =>
                    setSegmentDraft({ name: '', description: '', color: '', isActive: true })
                  }
                >
                  <Plus className="mr-1.5 h-4 w-4" />
                  New segment
                </Button>
              )}
            </CardHeader>
            <CardContent className="p-0">
              {segments.isLoading ? (
                <div className="flex items-center justify-center py-10">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : (segments.data ?? []).length === 0 ? (
                <div className="py-10">
                  <EmptyState
                    title="No segments yet"
                    description="Segments are how a big pool stays workable — “Senior engineers”, “Graduate cohort”, …"
                  />
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Name</TableHead>
                      <TableHead>Description</TableHead>
                      <TableHead className="w-24 text-right">Members</TableHead>
                      <TableHead className="w-24">Active</TableHead>
                      {canManage && <TableHead className="w-24" />}
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(segments.data ?? []).map((s: CandidateTalentSegment) => (
                      <TableRow key={s.id}>
                        <TableCell>
                          <span
                            className="mr-2 inline-block h-2.5 w-2.5 rounded-full align-middle"
                            style={{ backgroundColor: s.color ?? 'var(--muted-foreground)' }}
                          />
                          <span className="font-medium">{s.name}</span>
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {s.description ?? '—'}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">{s.memberCount}</TableCell>
                        <TableCell>
                          <StatusBadge status={s.isActive ? 'Active' : 'Inactive'} />
                        </TableCell>
                        {canManage && (
                          <TableCell>
                            <div className="flex justify-end gap-0.5">
                              <Button
                                variant="ghost"
                                size="icon"
                                aria-label={`Edit ${s.name}`}
                                onClick={() =>
                                  setSegmentDraft({
                                    id: s.id,
                                    name: s.name,
                                    description: s.description ?? '',
                                    color: s.color ?? '',
                                    isActive: s.isActive,
                                  })
                                }
                              >
                                <Pencil className="h-4 w-4" />
                              </Button>
                              {canAdmin && (
                                <Button
                                  variant="ghost"
                                  size="icon"
                                  aria-label={`Delete ${s.name}`}
                                  onClick={() => deleteSegment.mutate(s.id)}
                                >
                                  <Trash2 className="h-4 w-4 text-destructive" />
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
          </Card>
        </TabsContent>
      </Tabs>

      {/* segment create / edit */}
      <Dialog open={!!segmentDraft} onOpenChange={(o) => !o && setSegmentDraft(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{segmentDraft?.id ? 'Edit segment' : 'New segment'}</DialogTitle>
            <DialogDescription>A named grouping candidates can be assigned to.</DialogDescription>
          </DialogHeader>
          {segmentDraft && (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="segment-name">
                  Name <span className="text-red-500">*</span>
                </Label>
                <Input
                  id="segment-name"
                  value={segmentDraft.name}
                  maxLength={150}
                  onChange={(e) => setSegmentDraft((d) => (d ? { ...d, name: e.target.value } : d))}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="segment-description">Description</Label>
                <Textarea
                  id="segment-description"
                  value={segmentDraft.description}
                  maxLength={500}
                  onChange={(e) =>
                    setSegmentDraft((d) => (d ? { ...d, description: e.target.value } : d))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="segment-color">Badge colour (hex or CSS token)</Label>
                <Input
                  id="segment-color"
                  value={segmentDraft.color}
                  maxLength={30}
                  placeholder="#3b82f6"
                  onChange={(e) => setSegmentDraft((d) => (d ? { ...d, color: e.target.value } : d))}
                />
              </div>
              {segmentDraft.id && (
                <label className="flex items-center gap-2 text-sm">
                  <Checkbox
                    checked={segmentDraft.isActive}
                    onCheckedChange={(v) =>
                      setSegmentDraft((d) => (d ? { ...d, isActive: v === true } : d))
                    }
                  />
                  Active (shown on pickers)
                </label>
              )}
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setSegmentDraft(null)}>
              Cancel
            </Button>
            <Button
              disabled={!segmentDraft?.name.trim() || saveSegment.isPending}
              onClick={() => segmentDraft && saveSegment.mutate(segmentDraft)}
            >
              {saveSegment.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
