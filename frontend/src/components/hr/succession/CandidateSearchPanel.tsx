'use client';

import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Loader2, Search, UserPlus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/components/ui/use-toast';
import { successionSearchService } from '@/services/hr/succession.service';
import { competencyService } from '@/services/hr/competency.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import type { SuccessionCandidateSearchResult } from '@/types/hr/succession';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';

const NONE = '__none__';

/**
 * Criteria sent to POST /succession/candidate-search. Written from the server DTO
 * (`SuccessionCandidateSearchDto`), and the result shape was probed against the running API in
 * the lane-4 harness before this screen was built.
 */
export interface CandidateSearchCriteria {
  targetPositionId?: string | null;
  searchTerm?: string | null;
  organizationUnitId?: string | null;
  minYearsOfService?: number | null;
  minAge?: number | null;
  maxAge?: number | null;
  minServiceYearsLeft?: number | null;
  minPerformanceScore?: number | null;
  requiredCompetencyId?: string | null;
  requiredCompetencyMinLevel?: number | null;
  excludePoolId?: string | null;
  excludePlanId?: string | null;
  maxResults?: number;
}

const FIT_TONE: Record<string, string> = {
  Strong: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200',
  Good: 'bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200',
  Moderate: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
  Weak: 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200',
};

const num = (v: string): number | null => (v.trim() === '' ? null : Number(v));
const fmtDate = (d?: string | null) => (d ? d.slice(0, 10) : '—');

/**
 * Criteria-driven search for succession candidates: who in the organisation fits a target post,
 * by age band, service left, performance and a required competency.
 *
 * Finish-plan lane 4 (2026-09-01). The endpoint, its fit scoring and the client method had all
 * existed since area 13 and nothing called them — TDC's demo feedback said the criteria search had
 * no screen. Hosted twice: standalone under Succession Planning, and inside a plan's Candidates tab
 * pre-filled with that plan's post (and excluding people already on it), where a result row can be
 * carried straight into the nominate form.
 */
export function CandidateSearchPanel({
  defaults,
  lockedPositionId,
  onPick,
  pickLabel = 'Nominate',
}: {
  /** Criteria to start from (a plan passes its target post and its own id to exclude). */
  defaults?: Partial<CandidateSearchCriteria>;
  /** When set, the target post is fixed and shown rather than chosen. */
  lockedPositionId?: string | null;
  /** Offered per result row when given — the host decides what "pick" means. */
  onPick?: (row: SuccessionCandidateSearchResult) => void;
  pickLabel?: string;
}) {
  const { toast } = useToast();
  const [positionId, setPositionId] = useState<string>(
    lockedPositionId ?? defaults?.targetPositionId ?? NONE,
  );
  const [searchTerm, setSearchTerm] = useState(defaults?.searchTerm ?? '');
  const [unitId, setUnitId] = useState<string>(defaults?.organizationUnitId ?? NONE);
  const [minYearsOfService, setMinYearsOfService] = useState(
    defaults?.minYearsOfService != null ? String(defaults.minYearsOfService) : '',
  );
  const [minAge, setMinAge] = useState(defaults?.minAge != null ? String(defaults.minAge) : '');
  const [maxAge, setMaxAge] = useState(defaults?.maxAge != null ? String(defaults.maxAge) : '');
  const [minServiceYearsLeft, setMinServiceYearsLeft] = useState(
    defaults?.minServiceYearsLeft != null ? String(defaults.minServiceYearsLeft) : '',
  );
  const [minPerformanceScore, setMinPerformanceScore] = useState(
    defaults?.minPerformanceScore != null ? String(defaults.minPerformanceScore) : '',
  );
  const [competencyId, setCompetencyId] = useState<string>(defaults?.requiredCompetencyId ?? NONE);
  const [competencyMinLevel, setCompetencyMinLevel] = useState(
    defaults?.requiredCompetencyMinLevel != null ? String(defaults.requiredCompetencyMinLevel) : '',
  );
  const [results, setResults] = useState<SuccessionCandidateSearchResult[] | null>(null);

  const { data: positions } = useQuery({
    queryKey: ['hr', 'positions', 'all'],
    queryFn: () => employeePositionService.getAll(),
    enabled: !lockedPositionId,
  });
  const { data: competencies } = useQuery({
    queryKey: ['hr', 'competencies', 'lookup'],
    queryFn: () => competencyService.getLookup(),
  });

  const search = useMutation({
    mutationFn: () => {
      const criteria: CandidateSearchCriteria = {
        targetPositionId: lockedPositionId ?? (positionId === NONE ? null : positionId),
        searchTerm: searchTerm.trim() || null,
        organizationUnitId: unitId === NONE ? null : unitId,
        minYearsOfService: num(minYearsOfService),
        minAge: num(minAge),
        maxAge: num(maxAge),
        minServiceYearsLeft: num(minServiceYearsLeft),
        minPerformanceScore: num(minPerformanceScore),
        requiredCompetencyId: competencyId === NONE ? null : competencyId,
        requiredCompetencyMinLevel: competencyId === NONE ? null : num(competencyMinLevel),
        excludePoolId: defaults?.excludePoolId ?? null,
        excludePlanId: defaults?.excludePlanId ?? null,
        maxResults: defaults?.maxResults ?? 100,
      };
      return successionSearchService.searchCandidates(criteria as Record<string, unknown>);
    },
    onSuccess: (rows) => setResults(rows),
    onError: (e: any) =>
      toast({
        title: 'The search could not run',
        description: e?.body?.message ?? e?.message,
        variant: 'destructive',
      }),
  });

  const lockedPosition = (positions ?? []).find((p) => p.id === lockedPositionId);

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Criteria</CardTitle>
          <CardDescription>
            Leave a field blank to ignore it. With a target post, each result is scored against that
            post&apos;s competency requirements; without one, the list is filtered but not scored.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2">
              <Label>Target post</Label>
              {lockedPositionId ? (
                <Input value={lockedPosition?.title ?? 'This plan’s post'} disabled />
              ) : (
                <Select value={positionId} onValueChange={setPositionId}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NONE}>Any — no fit scoring</SelectItem>
                    {(positions ?? []).map((p) => (
                      <SelectItem key={p.id} value={p.id}>
                        {p.title}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            </div>
            <div className="space-y-2">
              <Label>Name or staff number</Label>
              <Input
                placeholder="Search…"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <OrganizationUnitPicker
                value={unitId === NONE ? '' : unitId}
                onChange={(id) => setUnitId(id || NONE)}
                allowNone="Any unit"
                unitLabel="Organisation unit"
                idPrefix="candidate-search-unit"
              />
            </div>
            <div className="space-y-2">
              <Label>Min. years of service</Label>
              <Input type="number" min={0} value={minYearsOfService} onChange={(e) => setMinYearsOfService(e.target.value)} />
            </div>
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-2">
                <Label>Min. age</Label>
                <Input type="number" min={16} value={minAge} onChange={(e) => setMinAge(e.target.value)} />
              </div>
              <div className="space-y-2">
                <Label>Max. age</Label>
                <Input type="number" min={16} value={maxAge} onChange={(e) => setMaxAge(e.target.value)} />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Min. service years left</Label>
              <Input
                type="number"
                min={0}
                value={minServiceYearsLeft}
                onChange={(e) => setMinServiceYearsLeft(e.target.value)}
                placeholder="Excludes people close to retirement"
              />
            </div>
            <div className="space-y-2">
              <Label>Min. latest appraisal score</Label>
              <Input
                type="number"
                min={0}
                step="0.1"
                value={minPerformanceScore}
                onChange={(e) => setMinPerformanceScore(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label>Required competency</Label>
              <Select value={competencyId} onValueChange={setCompetencyId}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>None</SelectItem>
                  {(competencies ?? []).map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>…at least level</Label>
              <Input
                type="number"
                min={1}
                value={competencyMinLevel}
                onChange={(e) => setCompetencyMinLevel(e.target.value)}
                disabled={competencyId === NONE}
              />
            </div>
          </div>
          <div className="mt-4 flex justify-end">
            <Button onClick={() => search.mutate()} disabled={search.isPending}>
              {search.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Search className="mr-2 h-4 w-4" />
              )}
              Search
            </Button>
          </div>
        </CardContent>
      </Card>

      {results !== null && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              {results.length} candidate{results.length === 1 ? '' : 's'}
            </CardTitle>
            <CardDescription>
              Fit is the target post&apos;s competency requirements met, weighted by level; gaps are
              the requirements the person falls short of.
            </CardDescription>
          </CardHeader>
          <CardContent className="p-0">
            {results.length === 0 ? (
              <EmptyState
                icon={Search}
                title="Nobody matches"
                description="Loosen a criterion — the age band and service-left filters are the usual culprits."
              />
            ) : (
              <div className="overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Fit</TableHead>
                      <TableHead>Gaps</TableHead>
                      <TableHead className="text-right">Age</TableHead>
                      <TableHead className="text-right">Service</TableHead>
                      <TableHead className="text-right">Years left</TableHead>
                      <TableHead>Retires</TableHead>
                      <TableHead className="text-right">Appraisal</TableHead>
                      {onPick && <TableHead className="text-right"></TableHead>}
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {results.map((r) => (
                      <TableRow key={r.employeeId}>
                        <TableCell>
                          <div className="font-medium">{r.employeeName}</div>
                          <div className="text-xs text-muted-foreground">
                            {r.employeeNumber}
                            {r.positionTitle && ` · ${r.positionTitle}`}
                            {r.organizationUnitName && ` · ${r.organizationUnitName}`}
                          </div>
                        </TableCell>
                        <TableCell>
                          <Badge variant="outline" className={FIT_TONE[r.fitBand] ?? ''}>
                            {r.fitScore} · {r.fitBand}
                          </Badge>
                          {r.competencyFitPercent != null && (
                            <div className="mt-0.5 text-xs text-muted-foreground">
                              {Math.round(r.competencyFitPercent)}% competency fit
                            </div>
                          )}
                        </TableCell>
                        <TableCell>
                          {r.requirementCount === 0 ? (
                            <span className="text-xs text-muted-foreground">No target</span>
                          ) : (
                            `${r.gapCount} of ${r.requirementCount}`
                          )}
                        </TableCell>
                        <TableCell className="text-right">{r.age ?? '—'}</TableCell>
                        <TableCell className="text-right">{r.yearsOfService ?? '—'}</TableCell>
                        <TableCell className="text-right">{r.serviceYearsLeft ?? '—'}</TableCell>
                        <TableCell>{fmtDate(r.retirementDate)}</TableCell>
                        <TableCell className="text-right">
                          {r.latestPerformanceScore != null
                            ? `${r.latestPerformanceScore}${r.latestPerformanceYear ? ` (${r.latestPerformanceYear})` : ''}`
                            : '—'}
                        </TableCell>
                        {onPick && (
                          <TableCell className="text-right">
                            <Button variant="outline" size="sm" onClick={() => onPick(r)}>
                              <UserPlus className="mr-1 h-3.5 w-3.5" />
                              {pickLabel}
                            </Button>
                          </TableCell>
                        )}
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
