'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import {
  AlertTriangle,
  CalendarClock,
  Loader2,
  Network,
  Plus,
  UserX,
  Users,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { successionService } from '@/services/hr/succession.service';
import type { PositionCriticality, SuccessionPlanSummary } from '@/types/hr/succession';

const CRITICALITIES: PositionCriticality[] = ['Critical', 'High', 'Medium', 'Low'];

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * ⚠ Risk reads backwards from everything else in the module: `HighRisk` is the bad end and `NoRisk`
 * the good one. Colouring it by position in the list would get it exactly wrong, so the mapping is
 * explicit.
 */
const RISK_TONE: Record<string, string> = {
  HighRisk: 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200',
  MediumRisk: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
  LowRisk: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200',
  NoRisk: 'bg-muted text-muted-foreground',
};

type QuickView = 'all' | 'no-successors' | 'no-ready-now' | 'due-for-review' | 'impending-vacancy';

const VIEWS: { key: QuickView; label: string; icon: typeof Users; hint: string }[] = [
  {
    key: 'all',
    label: 'All plans',
    icon: Network,
    hint: 'Every succession plan on record, newest plan year first.',
  },
  {
    key: 'no-successors',
    label: 'No successors',
    icon: UserX,
    hint: 'Plans with nobody identified at all. An empty plan is worse than a thin one.',
  },
  {
    key: 'no-ready-now',
    label: 'Nobody ready now',
    icon: AlertTriangle,
    hint: 'A bench exists, but nobody on it could step up today.',
  },
  {
    key: 'due-for-review',
    label: 'Due for review',
    icon: CalendarClock,
    hint: 'Plans whose next review falls within 30 days.',
  },
  {
    key: 'impending-vacancy',
    label: 'Vacancy expected',
    icon: Users,
    hint: 'Posts expected to fall vacant within 90 days.',
  },
];

export default function SuccessionRegisterPage() {
  const [view, setView] = useState<QuickView>('all');
  const [criticality, setCriticality] = useState<PositionCriticality | 'all'>('all');

  const { data, isLoading } = useQuery({
    queryKey: ['succession-plans', view],
    queryFn: () => {
      if (view === 'no-successors') return successionService.getWithNoSuccessors();
      if (view === 'no-ready-now') return successionService.getWithNoReadyNowSuccessor();
      if (view === 'due-for-review') return successionService.getDueForReview(30);
      if (view === 'impending-vacancy') return successionService.getWithImpendingVacancy(90);
      return successionService.getAll();
    },
  });

  const activeView = VIEWS.find((v) => v.key === view) ?? VIEWS[0];
  const rows: SuccessionPlanSummary[] = (data ?? []).filter(
    (p) => criticality === 'all' || p.criticality === criticality,
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Succession planning"
        description="Which posts are covered, by whom, and how ready they are."
        backHref="/hr"
        actions={
          <Button asChild>
            <Link href="/hr/succession/new">
              <Plus className="mr-2 h-4 w-4" />
              New plan
            </Link>
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        {VIEWS.map((v) => (
          <Button
            key={v.key}
            variant={view === v.key ? 'default' : 'outline'}
            size="sm"
            onClick={() => setView(v.key)}
          >
            <v.icon className="mr-2 h-4 w-4" />
            {v.label}
          </Button>
        ))}
      </div>

      <Card>
        <CardContent className="p-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-sm text-muted-foreground">{activeView.hint}</p>
            <Select
              value={criticality}
              onValueChange={(v) => setCriticality(v as PositionCriticality | 'all')}
            >
              <SelectTrigger className="w-56">
                <SelectValue placeholder="All criticalities" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All criticalities</SelectItem>
                {CRITICALITIES.map((c) => (
                  <SelectItem key={c} value={c}>
                    {c}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Network}
              title="No succession plans here"
              description={
                view === 'all'
                  ? 'Nothing has been planned for yet. Start with the posts that would hurt most to lose.'
                  : 'Nothing matches this view — which, for this particular view, is good news.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Plan</TableHead>
                  <TableHead>Position</TableHead>
                  <TableHead>Incumbent</TableHead>
                  <TableHead>Criticality</TableHead>
                  <TableHead>Risk</TableHead>
                  <TableHead className="text-right">Successors</TableHead>
                  <TableHead>Cover</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Next review</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((p) => (
                  <TableRow key={p.id} className="cursor-pointer">
                    <TableCell>
                      <Link href={`/hr/succession/${p.id}`} className="font-medium hover:underline">
                        {p.planNumber}
                      </Link>
                      <div className="text-xs text-muted-foreground">
                        {p.planName}
                        {p.versionNumber > 1 && ` · v${p.versionNumber}`}
                        {!p.isActiveVersion && p.status === 'Archived' && ' · superseded'}
                      </div>
                    </TableCell>
                    <TableCell>{p.positionTitle || '—'}</TableCell>
                    <TableCell>{p.currentIncumbentName ?? 'Vacant'}</TableCell>
                    <TableCell>{p.criticality}</TableCell>
                    <TableCell>
                      <Badge variant="outline" className={RISK_TONE[p.riskLevel] ?? ''}>
                        {p.riskLevel.replace('Risk', ' risk')}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">{p.numberOfIdentifiedSuccessors}</TableCell>
                    <TableCell>
                      <div className="flex flex-wrap gap-1">
                        {p.hasReadyNowSuccessor && (
                          <Badge variant="outline" className="bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200">
                            Ready now
                          </Badge>
                        )}
                        {p.hasEmergencySuccessor && (
                          <Badge variant="outline">Emergency</Badge>
                        )}
                        {!p.hasReadyNowSuccessor && !p.hasEmergencySuccessor && (
                          <span className="text-xs text-muted-foreground">None</span>
                        )}
                      </div>
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={p.status} />
                    </TableCell>
                    <TableCell>{fmtDate(p.nextReviewDate)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
