'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { CheckCircle2, Clock, Gauge, Settings2, TriangleAlert } from 'lucide-react';
import Link from 'next/link';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { CycleSelect } from '@/components/hr/performance/CycleSelect';
import { TeamGoalTable } from '@/components/hr/performance/TeamGoalTable';
import { atRiskGoalsService, goalRiskSettingsService } from '@/services/hr/goals.service';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';

/**
 * Goals at risk across the whole organisation — HR's counterpart to the manager workspace's
 * at-risk tab, with the manager scope removed.
 *
 * Same three rules and the same thresholds decide what appears — over every agreed goal not yet
 * completed (closure D-71); only the population differs.
 * The thresholds themselves are shown here so a surprising result can be read against the
 * rule that produced it rather than guessed at.
 *
 * Rows arrive sorted by severity, so the top of the list is where to start.
 */
const ANY = '__any__';

export default function OrgWideAtRiskPage() {
  const [cycleId, setCycleId] = useState('');
  const [orgUnitId, setOrgUnitId] = useState<string>(ANY);
  const [orgLevelId, setOrgLevelId] = useState<string>(ANY);

  const { data: levels } = useQuery({
    queryKey: ['hr', 'organization-levels'],
    queryFn: () => organizationLevelService.getAll(),
  });
  const { data: settings } = useQuery({
    queryKey: ['hr', 'goal-risk-settings'],
    queryFn: () => goalRiskSettingsService.get(),
  });

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'goals-at-risk', cycleId, orgUnitId, orgLevelId],
    queryFn: () =>
      atRiskGoalsService.get(cycleId, {
        organizationUnitId: orgUnitId === ANY ? null : orgUnitId,
        organizationLevelId: orgLevelId === ANY ? null : orgLevelId,
      }),
    enabled: !!cycleId,
  });

  const rows = data ?? [];

  const stats = useMemo(() => {
    const overdue = rows.filter((r) => r.isOverdue).length;
    const severe = rows.filter((r) => r.riskSeverityScore >= 70).length;
    const employees = new Set(rows.map((r) => r.employeeId)).size;
    const avgProgress =
      rows.length === 0
        ? 0
        : rows.reduce((s, r) => s + Number(r.progressPercent ?? 0), 0) / rows.length;
    return { overdue, severe, employees, avgProgress };
  }, [rows]);

  const levelOptions = useMemo(
    () => (levels ?? []).map((l) => ({ value: l.id, label: l.name })),
    [levels],
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Goals At Risk"
        description="Every goal across the organisation that trips the risk thresholds, most severe first."
        backHref="/hr/performance"
        actions={
          <Button variant="outline" asChild>
            <Link href="/administration/hr/performance/goal-risk-settings">
              <Settings2 className="mr-2 h-4 w-4" />
              Thresholds
            </Link>
          </Button>
        }
      />

      <CycleSelect value={cycleId} onChange={setCycleId} />

      {settings && (
        <Alert>
          <Gauge className="h-4 w-4" />
          <AlertTitle>
            Flagging goals inside {settings.daysRemainingThreshold} days of their due date below{' '}
            {settings.minimumProgressPercent}% progress
          </AlertTitle>
          <AlertDescription>
            A goal is also flagged when it is more than {settings.expectedProgressTolerancePercent}%
            behind the straight-line progress its dates imply, or when a progress update marked it at
            risk. Only goals agreed with the manager and not yet completed are watched.
            {!settings.isConfigured && ' These are the built-in defaults — nothing has been saved.'}
          </AlertDescription>
        </Alert>
      )}

      {!cycleId ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={TriangleAlert}
              title="No appraisal cycle selected"
              description="Risk is evaluated one cycle at a time."
            />
          </CardContent>
        </Card>
      ) : (
        <>
          <MetricTiles
            tiles={[
              {
                label: 'Goals at risk',
                value: rows.length,
                icon: TriangleAlert,
                tone: rows.length > 0 ? 'danger' : 'success',
              },
              {
                label: 'High severity',
                value: stats.severe,
                hint: 'Low progress with the deadline close',
                icon: TriangleAlert,
                tone: stats.severe > 0 ? 'danger' : 'default',
              },
              {
                label: 'Already overdue',
                value: stats.overdue,
                hint: 'Past the due date and not complete',
                icon: Clock,
                tone: stats.overdue > 0 ? 'warning' : 'default',
              },
              {
                label: 'Employees affected',
                value: stats.employees,
                hint: rows.length
                  ? `Average progress ${stats.avgProgress.toFixed(1)}%`
                  : undefined,
                icon: Gauge,
              },
            ]}
          />

          <Card>
            <CardContent className="flex flex-wrap items-end gap-4 p-4">
              <div className="w-[240px] space-y-2">
                <OrganizationUnitPicker
                  value={orgUnitId === ANY ? '' : orgUnitId}
                  onChange={(id) => setOrgUnitId(id || ANY)}
                  onLevelChange={(id) => setOrgLevelId(id || ANY)}
                  initialLevelId={orgLevelId === ANY ? '' : orgLevelId}
                  allowNoLevel="All levels"
                  allowNone="All units"
                  levelLabel="Organisation level"
                  unitLabel="Organisation unit"
                  idPrefix="at-risk-scope"
                  className="flex flex-wrap items-end gap-4"
                />
              </div>
              <p className="pb-2 text-xs text-muted-foreground">
                Filters match the employee&apos;s own unit and level, not the goal&apos;s.
              </p>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-0">
              {isError ? (
                <EmptyState
                  icon={TriangleAlert}
                  title="Could not load at-risk goals"
                  description={
                    (error as any)?.message ||
                    'This report is restricted to HR and admin roles.'
                  }
                />
              ) : (
                <TeamGoalTable
                  rows={rows}
                  isLoading={isLoading}
                  timing="due"
                  showRisk
                  emptyIcon={CheckCircle2}
                  emptyTitle="Nothing at risk"
                  emptyDescription="No goal in this cycle trips the thresholds above."
                />
              )}
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}
