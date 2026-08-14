'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { safetyPerformanceService } from '@/services/hr/safety-performance.service';
import type { SheSnapshotPeriodType } from '@/types/hr/safety';

const pct = (v?: number | null) => (v === null || v === undefined ? '—' : `${v}%`);
const num = (v?: number | null) => (v === null || v === undefined ? '—' : `${v}`);

/** Cell colour for the 5×5 grid: green → amber → red by likelihood × severity product. */
const heatClass = (likelihood: number, severity: number) => {
  const score = likelihood * severity;
  if (score <= 4) return 'bg-emerald-100 dark:bg-emerald-900/40';
  if (score <= 9) return 'bg-yellow-100 dark:bg-yellow-900/40';
  if (score <= 15) return 'bg-orange-100 dark:bg-orange-900/40';
  return 'bg-red-100 dark:bg-red-900/40';
};

function Heatmap({ grid }: { grid: number[][] }) {
  // grid is [likelihood-1][severity-1]; render likelihood 5 at the top.
  return (
    <div className="overflow-x-auto">
      <table className="border-collapse text-center text-sm">
        <thead>
          <tr>
            <th className="text-muted-foreground p-2 text-xs font-normal">Likelihood ↓</th>
            {[1, 2, 3, 4, 5].map((s) => (
              <th key={s} className="text-muted-foreground p-2 text-xs font-normal">
                S{s}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {[5, 4, 3, 2, 1].map((l) => (
            <tr key={l}>
              <td className="text-muted-foreground p-2 text-xs">L{l}</td>
              {[1, 2, 3, 4, 5].map((s) => {
                const count = grid[l - 1]?.[s - 1] ?? 0;
                return (
                  <td
                    key={s}
                    className={`h-12 w-14 border font-medium tabular-nums ${heatClass(l, s)}`}
                  >
                    {count || ''}
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/**
 * Period analytics computed live from the registers (nothing here is hand-reported):
 * a computed-KPI preview, departmental compliance (FR-SHE-230), contractor ranking
 * (FR-CON-001) and the hazard risk heat-map (FR-SHE-232). To persist computed figures
 * into a snapshot, use "Compute from live data" on the snapshot itself.
 */
export default function PerformanceAnalyticsPage() {
  const now = new Date();
  const [periodType, setPeriodType] = useState<SheSnapshotPeriodType>('Monthly');
  const [year, setYear] = useState(now.getFullYear());
  const [periodNumber, setPeriodNumber] = useState(now.getMonth() + 1);
  const [manHours, setManHours] = useState<number>(0);
  const [heatmapView, setHeatmapView] = useState<'inherent' | 'residual'>('residual');

  const effectivePeriod = periodType === 'Annual' ? undefined : periodNumber;

  const { data: preview, isLoading: previewLoading } = useQuery({
    queryKey: ['hr', 'safety-kpi', 'preview', periodType, year, effectivePeriod, manHours],
    queryFn: () =>
      safetyPerformanceService.computePreview(periodType, year, effectivePeriod, null, manHours),
  });

  const { data: departments = [], isLoading: deptLoading } = useQuery({
    queryKey: ['hr', 'safety-kpi', 'departmental', periodType, year, effectivePeriod],
    queryFn: () =>
      safetyPerformanceService.getDepartmentalCompliance(periodType, year, effectivePeriod),
  });

  const { data: ranking = [], isLoading: rankingLoading } = useQuery({
    queryKey: ['hr', 'safety-kpi', 'contractor-ranking', periodType, year, effectivePeriod],
    queryFn: () => safetyPerformanceService.getContractorRanking(periodType, year, effectivePeriod),
  });

  const { data: heatmap, isLoading: heatmapLoading } = useQuery({
    queryKey: ['hr', 'safety-kpi', 'hazard-heatmap'],
    queryFn: () => safetyPerformanceService.getHazardHeatmap(),
  });

  const periodOptions = useMemo(() => {
    if (periodType === 'Monthly')
      return Array.from({ length: 12 }, (_, i) => ({ value: i + 1, label: `Month ${i + 1}` }));
    if (periodType === 'Quarterly')
      return Array.from({ length: 4 }, (_, i) => ({ value: i + 1, label: `Q${i + 1}` }));
    return [];
  }, [periodType]);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="SHE Analytics"
        description="Computed live from the registers — departmental compliance, contractor ranking, the hazard heat-map, and what a snapshot for the period would compute."
        backHref="/hr/safety/performance"
      />

      <div className="flex flex-wrap items-end gap-3">
        <div className="space-y-1">
          <Label>Period type</Label>
          <Select
            value={periodType}
            onValueChange={(v) => {
              setPeriodType(v as SheSnapshotPeriodType);
              setPeriodNumber(1);
            }}
          >
            <SelectTrigger className="w-36">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="Monthly">Monthly</SelectItem>
              <SelectItem value="Quarterly">Quarterly</SelectItem>
              <SelectItem value="Annual">Annual</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <Label>Year</Label>
          <Input
            type="number"
            className="w-28"
            value={year}
            onChange={(e) => setYear(Number(e.target.value) || now.getFullYear())}
          />
        </div>
        {periodType !== 'Annual' && (
          <div className="space-y-1">
            <Label>Period</Label>
            <Select
              value={String(periodNumber)}
              onValueChange={(v) => setPeriodNumber(Number(v))}
            >
              <SelectTrigger className="w-32">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {periodOptions.map((o) => (
                  <SelectItem key={o.value} value={String(o.value)}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        )}
        <div className="space-y-1">
          <Label>Man-hours (for the frequency rates)</Label>
          <Input
            type="number"
            className="w-44"
            min={0}
            value={manHours || ''}
            placeholder="e.g. 180000"
            onChange={(e) => setManHours(Math.max(0, Number(e.target.value) || 0))}
          />
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Computed KPI preview</CardTitle>
        </CardHeader>
        <CardContent>
          {previewLoading || !preview ? (
            <div className="flex items-center justify-center py-10">
              <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
            </div>
          ) : (
            <dl className="grid grid-cols-2 gap-x-6 gap-y-2 text-sm md:grid-cols-4">
              {(
                [
                  ['Incidents', num(preview.totalIncidents)],
                  ['Accidents', num(preview.totalAccidents)],
                  ['Near misses', num(preview.totalNearMisses)],
                  ['Lost-time injuries', num(preview.totalLostTimeInjuries)],
                  ['Fatalities', num(preview.totalFatalities)],
                  ['Lost days', num(preview.totalLostDays)],
                  ['LTIFR', num(preview.lostTimeInjuryFrequencyRate)],
                  ['TRIR', num(preview.totalRecordableIncidentRate)],
                  ['Near-miss rate', num(preview.nearMissFrequencyRate)],
                  ['Inspections conducted', num(preview.inspectionsConducted)],
                  ['Avg inspection score', pct(preview.averageInspectionComplianceScore)],
                  ['Housekeeping', pct(preview.housekeepingComplianceRating)],
                  ['CAs issued', num(preview.correctiveActionsIssued)],
                  ['CA closure rate', pct(preview.correctiveActionClosureRate)],
                  ['CAs overdue', num(preview.correctiveActionsOverdue)],
                  ['Training conducted', num(preview.trainingProgramsConducted)],
                  ['Training completion', pct(preview.trainingCompletionRate)],
                  ['Contractor compliance', pct(preview.contractorComplianceRate)],
                  ['Env incidents', num(preview.environmentalIncidents)],
                  ['Waste recycling', pct(preview.wasteRecyclingRate)],
                  ['Drills conducted', num(preview.emergencyDrillsConducted)],
                  ['Drill objectives met', pct(preview.fireDrillObjectivesMetRate)],
                  ['PPE compliance', pct(preview.ppeComplianceRate)],
                  ['Obligations compliant', num(preview.regulatoryObligationsCompliant)],
                ] as [string, string][]
              ).map(([label, value]) => (
                <div
                  key={label}
                  className="flex items-baseline justify-between gap-2 border-b py-1"
                >
                  <dt className="text-muted-foreground">{label}</dt>
                  <dd className="font-medium tabular-nums">{value}</dd>
                </div>
              ))}
            </dl>
          )}
          <p className="text-muted-foreground mt-3 text-xs">
            Frequency rates need man-hours (hand-entered above or on the snapshot). PPE compliance
            computes once the PPE requirement matrix&apos;s job-role codes match position codes
            {preview?.ppeEmployeesAssessed
              ? ` (currently assessing ${preview.ppeEmployeesAssessed} employees)`
              : ' (currently matching none — the figure stays hand-reported)'}
            .
          </p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Departmental compliance (FR-SHE-230)</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {deptLoading ? (
            <div className="flex items-center justify-center py-10">
              <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
            </div>
          ) : departments.length === 0 ? (
            <EmptyState
              title="Nothing to score"
              description="No inspections or incidents fall in this period."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Organization unit</TableHead>
                  <TableHead className="text-right">Inspections</TableHead>
                  <TableHead className="text-right">Avg compliance</TableHead>
                  <TableHead className="text-right">Open findings</TableHead>
                  <TableHead className="text-right">Incidents</TableHead>
                  <TableHead className="text-right">LTIs</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {departments.map((d) => (
                  <TableRow key={d.organizationUnitId ?? 'none'}>
                    <TableCell className="font-medium">{d.organizationUnitName}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {d.inspectionsConducted}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {pct(d.averageComplianceScore)}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {d.openInspectionFindings}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{d.incidents}</TableCell>
                    <TableCell className="text-right tabular-nums">{d.lostTimeInjuries}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Contractor SHE ranking (FR-CON-001)</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {rankingLoading ? (
            <div className="flex items-center justify-center py-10">
              <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
            </div>
          ) : ranking.length === 0 ? (
            <EmptyState title="No active contractors" description="The register is empty." />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-12">#</TableHead>
                  <TableHead>Contractor</TableHead>
                  <TableHead className="text-right">Avg inspection score</TableHead>
                  <TableHead className="text-right">Inspections</TableHead>
                  <TableHead className="text-right">Notices issued</TableHead>
                  <TableHead className="text-right">Open NCs</TableHead>
                  <TableHead className="text-right">Pre-qual</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {ranking.map((r) => (
                  <TableRow key={r.contractorId}>
                    <TableCell className="tabular-nums">{r.rank}</TableCell>
                    <TableCell className="font-medium">
                      {r.companyName}
                      <span className="text-muted-foreground ml-2 font-mono text-xs">
                        {r.contractorCode}
                      </span>
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {pct(r.averageComplianceScore)}
                      {r.averageComplianceScore === null && (
                        <span className="text-muted-foreground ml-1 text-xs">(unscored)</span>
                      )}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {r.inspectionsConducted}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {r.nonComplianceNoticesIssued}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{r.openNonCompliances}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {num(r.preQualificationScore)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex flex-wrap items-center gap-3 text-base">
            Hazard risk heat-map (FR-SHE-232)
            <Tabs
              value={heatmapView}
              onValueChange={(v) => setHeatmapView(v as 'inherent' | 'residual')}
            >
              <TabsList>
                <TabsTrigger value="inherent">Inherent</TabsTrigger>
                <TabsTrigger value="residual">Residual</TabsTrigger>
              </TabsList>
            </Tabs>
          </CardTitle>
        </CardHeader>
        <CardContent>
          {heatmapLoading || !heatmap ? (
            <div className="flex items-center justify-center py-10">
              <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
            </div>
          ) : heatmap.totalHazards === 0 ? (
            <EmptyState title="No active hazards" description="The hazard register is empty." />
          ) : (
            <div className="space-y-3">
              <Heatmap grid={heatmapView === 'inherent' ? heatmap.inherent : heatmap.residual} />
              <div className="flex flex-wrap gap-2">
                <Badge variant="outline">{heatmap.totalHazards} active hazards</Badge>
                {heatmap.excludedOutOfRange > 0 && (
                  <Badge variant="destructive">
                    {heatmap.excludedOutOfRange} excluded — risk factors outside 1–5
                  </Badge>
                )}
                <span className="text-muted-foreground text-xs">
                  Rows are likelihood (L5 top), columns severity. Point-in-time over the whole
                  register — the heat-map has no period.
                </span>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
