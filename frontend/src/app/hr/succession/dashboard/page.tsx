'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import {
  AlertTriangle,
  CalendarClock,
  Loader2,
  Network,
  ShieldAlert,
  TrendingUp,
  Users2,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { successionService } from '@/services/hr/succession.service';
import type {
  PositionCriticality,
  SuccessionRisk,
} from '@/types/hr/succession';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const spaced = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z0-9])/g, '$1 $2') : '—');

const RISK_ORDER: SuccessionRisk[] = ['HighRisk', 'MediumRisk', 'LowRisk', 'NoRisk'];
const CRITICALITY_ORDER: PositionCriticality[] = ['Critical', 'High', 'Medium', 'Low'];

/** ⚠ Risk reads backwards: HighRisk is the bad end. Colour by meaning, never by position. */
const HEAT = (risk: SuccessionRisk, count: number) => {
  if (count === 0) return 'bg-muted/40 text-muted-foreground';
  if (risk === 'HighRisk') return 'bg-red-100 text-red-900 dark:bg-red-900/50 dark:text-red-100';
  if (risk === 'MediumRisk') return 'bg-amber-100 text-amber-900 dark:bg-amber-900/50 dark:text-amber-100';
  return 'bg-emerald-100 text-emerald-900 dark:bg-emerald-900/50 dark:text-emerald-100';
};

function Kpi({
  label,
  value,
  hint,
  tone,
}: {
  label: string;
  value: number | string;
  hint?: string;
  tone?: 'bad' | 'good';
}) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-xs uppercase tracking-wide text-muted-foreground">
          {label}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <p
          className={`text-2xl font-semibold ${
            tone === 'bad'
              ? 'text-red-600 dark:text-red-400'
              : tone === 'good'
                ? 'text-emerald-600 dark:text-emerald-400'
                : ''
          }`}
        >
          {value}
        </p>
        {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
      </CardContent>
    </Card>
  );
}

export default function SuccessionDashboardPage() {
  const [year, setYear] = useState<string>('all');

  const { data: dash, isLoading } = useQuery({
    queryKey: ['succession-dashboard', year],
    queryFn: () => successionService.getDashboard(year === 'all' ? undefined : Number(year)),
  });

  const years = Array.from({ length: 5 }, (_, i) => new Date().getFullYear() - 2 + i);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!dash) return null;

  const cellCount = (risk: SuccessionRisk, criticality: PositionCriticality) =>
    dash.riskHeatmap.find((c) => c.riskLevel === risk && c.criticality === criticality)?.count ?? 0;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Succession dashboard"
        description={`Coverage, risk and bench strength. Computed ${new Date(dash.computedAt).toLocaleString()}.`}
        backHref="/hr/succession"
        actions={
          <Select value={year} onValueChange={setYear}>
            <SelectTrigger className="w-40">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All years</SelectItem>
              {years.map((y) => (
                <SelectItem key={y} value={String(y)}>
                  {y}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        }
      />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Kpi label="Active plans" value={dash.totalActivePlans} />
        <Kpi
          label="Coverage"
          value={`${Math.round(dash.coveragePercentage)}%`}
          hint={`${dash.positionsWithReadyNowSuccessor} of ${dash.totalActivePlans} have someone ready now`}
          tone={dash.coveragePercentage >= 50 ? 'good' : 'bad'}
        />
        <Kpi
          label="No successor at all"
          value={dash.positionsWithoutSuccessors}
          hint="An empty plan records the risk without reducing it"
          tone={dash.positionsWithoutSuccessors > 0 ? 'bad' : 'good'}
        />
        <Kpi
          label="No emergency cover"
          value={dash.positionsWithoutEmergencyCover}
          hint="Nobody named to step in tomorrow"
          tone={dash.positionsWithoutEmergencyCover > 0 ? 'bad' : 'good'}
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <AlertTriangle className="h-4 w-4" />
              Risk against criticality
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-[auto_repeat(4,minmax(0,1fr))] gap-1 text-center text-sm">
              <div />
              {CRITICALITY_ORDER.map((c) => (
                <div key={c} className="pb-1 text-xs font-medium text-muted-foreground">
                  {c}
                </div>
              ))}
              {RISK_ORDER.map((risk) => (
                <HeatRow key={risk} risk={risk} cellCount={cellCount} />
              ))}
            </div>
            <p className="mt-3 text-xs text-muted-foreground">
              Top-left is the corner that matters: a critical post at high risk.
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Users2 className="h-4 w-4" />
              Talent pool readiness
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="grid grid-cols-3 gap-3 text-center">
              <div>
                <p className="text-2xl font-semibold">{dash.talentReadyNow}</p>
                <p className="text-xs text-muted-foreground">Ready now</p>
              </div>
              <div>
                <p className="text-2xl font-semibold">{dash.talentReady1To2Years}</p>
                <p className="text-xs text-muted-foreground">1–2 years</p>
              </div>
              <div>
                <p className="text-2xl font-semibold">{dash.talentLongTerm}</p>
                <p className="text-xs text-muted-foreground">Longer term</p>
              </div>
            </div>
            <p className="text-sm text-muted-foreground">
              {dash.totalTalentPoolMembers} people in pools across the organisation.
            </p>
            <div className="flex gap-2 text-xs">
              <Badge variant="outline">{dash.draftPlans} draft</Badge>
              <Badge variant="outline">{dash.underReviewPlans} out for approval</Badge>
              <Badge variant="outline">{dash.approvedPlans} approved</Badge>
            </div>
          </CardContent>
        </Card>
      </div>

      {dash.upcomingVacancies.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <CalendarClock className="h-4 w-4" />
              Vacancies coming
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Position</TableHead>
                  <TableHead>Incumbent</TableHead>
                  <TableHead>Why</TableHead>
                  <TableHead>When</TableHead>
                  <TableHead>Cover</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {dash.upcomingVacancies.map((v) => (
                  <TableRow key={v.planId}>
                    <TableCell>
                      <Link href={`/hr/succession/${v.planId}`} className="font-medium hover:underline">
                        {v.positionTitle}
                      </Link>
                    </TableCell>
                    <TableCell>{v.currentIncumbentName ?? 'Vacant'}</TableCell>
                    <TableCell>
                      {spaced(v.kind)}
                      {v.reason && ` · ${spaced(v.reason)}`}
                    </TableCell>
                    <TableCell>
                      {fmtDate(v.eventDate)}
                      <span
                        className={`ml-2 text-xs ${
                          v.daysUntil <= 90 ? 'text-red-600 dark:text-red-400' : 'text-muted-foreground'
                        }`}
                      >
                        {v.daysUntil} days
                      </span>
                    </TableCell>
                    <TableCell>
                      {v.hasReadyNowSuccessor ? (
                        <Badge variant="outline" className="bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200">
                          Ready now
                        </Badge>
                      ) : (
                        <Badge variant="outline" className="bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200">
                          Nobody ready
                        </Badge>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <ShieldAlert className="h-4 w-4" />
              No emergency cover ({dash.emergencyCoverageGaps.length})
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {dash.emergencyCoverageGaps.length === 0 ? (
              <EmptyState
                icon={ShieldAlert}
                title="Every plan has emergency cover"
                description="Somebody is named to step in on each post."
              />
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Position</TableHead>
                    <TableHead>Criticality</TableHead>
                    <TableHead>Risk</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {dash.emergencyCoverageGaps.map((g) => (
                    <TableRow key={g.planId}>
                      <TableCell>
                        <Link href={`/hr/succession/${g.planId}`} className="hover:underline">
                          {g.positionTitle}
                        </Link>
                      </TableCell>
                      <TableCell>{g.criticality}</TableCell>
                      <TableCell>{spaced(g.riskLevel)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Network className="h-4 w-4" />
              Bench strength
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {dash.benchStrength.length === 0 ? (
              <EmptyState icon={Network} title="No bench recorded" description="No plan has successors yet." />
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Position</TableHead>
                    <TableHead className="text-right">Successors</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {dash.benchStrength.map((b) => (
                    <TableRow key={b.planId}>
                      <TableCell>
                        <Link href={`/hr/succession/${b.planId}`} className="hover:underline">
                          {b.positionTitle}
                        </Link>
                      </TableCell>
                      <TableCell className="text-right">{b.successorCount}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      </div>

      {dash.overdueActionsCount > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <AlertTriangle className="h-4 w-4" />
              Overdue actions ({dash.overdueActionsCount} of {dash.totalActions})
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Action</TableHead>
                  <TableHead>Position</TableHead>
                  <TableHead>Owner</TableHead>
                  <TableHead>Due</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {dash.overdueAlerts.map((a) => (
                  <TableRow key={a.actionId}>
                    <TableCell>{a.actionDescription}</TableCell>
                    <TableCell>{a.positionTitle}</TableCell>
                    <TableCell>{a.responsiblePersonName ?? '—'}</TableCell>
                    <TableCell className="text-red-600 dark:text-red-400">{fmtDate(a.dueDate)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {dash.coverageTrend.length > 1 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <TrendingUp className="h-4 w-4" />
              Coverage over time
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Year</TableHead>
                  <TableHead className="text-right">Plans</TableHead>
                  <TableHead className="text-right">With someone ready now</TableHead>
                  <TableHead className="text-right">Strong bench</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {dash.coverageTrend.map((t) => (
                  <TableRow key={t.planYear}>
                    <TableCell>{t.planYear}</TableCell>
                    <TableCell className="text-right">{t.totalPlans}</TableCell>
                    <TableCell className="text-right">{t.readyNowPlans}</TableCell>
                    <TableCell className="text-right">{t.strongBenchPlans}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function HeatRow({
  risk,
  cellCount,
}: {
  risk: SuccessionRisk;
  cellCount: (r: SuccessionRisk, c: PositionCriticality) => number;
}) {
  return (
    <>
      <div className="flex items-center justify-end pr-2 text-xs font-medium text-muted-foreground">
        {spaced(risk)}
      </div>
      {CRITICALITY_ORDER.map((criticality) => {
        const count = cellCount(risk, criticality);
        return (
          <div
            key={criticality}
            className={`rounded py-3 text-sm font-medium ${HEAT(risk, count)}`}
            title={`${spaced(risk)} · ${criticality} criticality`}
          >
            {count}
          </div>
        );
      })}
    </>
  );
}
