'use client';

import { useState } from 'react';
import { AlertTriangle, ChevronDown, ChevronUp, Loader2, RefreshCw } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import type { ManpowerPlanningBaseline, ManpowerPlanningExit } from '@/types/hr/job-architecture';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtMoney = (v: number) => v.toLocaleString(undefined, { maximumFractionDigits: 0 });

/**
 * What the system knows about a unit's subtree for a period (round 2b, R2): who is on strength,
 * what they cost, who is due to leave, and where each post stands against its establishment.
 *
 * Hosted twice: on the budget form (the three figures it can supply are offered to the form) and
 * on the budget detail (recomputed live, labelled "as of today", with the budget's own typed
 * figures beside it so a drift is visible rather than silent).
 *
 * ⚠ An unestablished post shows "not established", never a gap of 0/1. Its headcount is the
 * column default and means nothing — the same rule every enforcement path uses.
 */
export function PlanningBaselinePanel({
  baseline,
  loading,
  error,
  idle,
  title = 'What the system knows',
  actions,
  onRefresh,
  compareTo,
}: {
  baseline?: ManpowerPlanningBaseline;
  loading?: boolean;
  error?: string;
  /** True while the unit or period is not yet chosen. */
  idle?: boolean;
  title?: string;
  actions?: React.ReactNode;
  onRefresh?: () => void;
  /** The budget's own typed figures, for the detail page to show drift against. */
  compareTo?: { currentHeadcount: number; plannedTerminations: number };
}) {
  const [showPositions, setShowPositions] = useState(false);

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-2">
        <div>
          <CardTitle>{title}</CardTitle>
          {baseline && (
            <p className="mt-1 text-xs text-muted-foreground">
              {baseline.organizationUnitName} and the {Math.max(0, baseline.unitIds.length - 1)} unit
              {baseline.unitIds.length === 2 ? '' : 's'} under it · {fmtDate(baseline.periodStart)} – {fmtDate(baseline.periodEnd)}
            </p>
          )}
        </div>
        <div className="flex shrink-0 items-center gap-2">
          {actions}
          {onRefresh && (
            <Button type="button" variant="ghost" size="sm" onClick={onRefresh} title="Recompute" disabled={loading}>
              <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {idle && (
          <p className="text-sm text-muted-foreground">
            Choose the unit and the period and the system will say who is on strength, what they
            cost, and who is due to leave.
          </p>
        )}
        {!idle && loading && !baseline && (
          <div className="flex items-center text-sm text-muted-foreground">
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            Computing…
          </div>
        )}
        {error && <p className="text-sm text-destructive">{error}</p>}

        {baseline && (
          <>
            <div className="grid gap-3 sm:grid-cols-3">
              <Tile
                label="On strength today"
                value={baseline.currentHeadcount}
                drift={compareTo && compareTo.currentHeadcount !== baseline.currentHeadcount
                  ? `budget says ${compareTo.currentHeadcount}` : undefined}
              />
              <Tile
                label="Monthly basic pay (estimate)"
                value={fmtMoney(baseline.currentSalaryCost)}
                hint={baseline.employeesWithoutPay > 0 ? `${baseline.employeesWithoutPay} with no figure on record` : undefined}
              />
              <Tile
                label="Exits due in the period"
                value={baseline.exitsDueTotal}
                hint={`${baseline.retirementsDue.length} retiring · ${baseline.contractExpiriesDue.length} contract${baseline.contractExpiriesDue.length === 1 ? '' : 's'} ending · ${baseline.separationsInFlight.length} separation${baseline.separationsInFlight.length === 1 ? '' : 's'} in flight`}
                drift={compareTo && compareTo.plannedTerminations !== baseline.exitsDueTotal
                  ? `budget plans ${compareTo.plannedTerminations}` : undefined}
              />
            </div>
            <p className="text-xs text-muted-foreground">{baseline.salaryCostNote}</p>

            {baseline.exitsDueTotal > 0 && (
              <div className="grid gap-4 lg:grid-cols-3">
                <ExitList title="Retiring" rows={baseline.retirementsDue} dateLabel="Retires" />
                <ExitList title="Contracts ending" rows={baseline.contractExpiriesDue} dateLabel="Ends" />
                <ExitList title="Separations in flight" rows={baseline.separationsInFlight} dateLabel="Last day" />
              </div>
            )}
            <p className="text-xs text-muted-foreground">
              Terminations nobody has raised yet cannot be known; adjust the planned figure if you
              expect more.
            </p>

            <div>
              <Button type="button" variant="ghost" size="sm" onClick={() => setShowPositions((s) => !s)}>
                {showPositions ? <ChevronUp className="mr-2 h-4 w-4" /> : <ChevronDown className="mr-2 h-4 w-4" />}
                {baseline.positions.length} post{baseline.positions.length === 1 ? '' : 's'} in the unit and below
              </Button>
              {showPositions && (
                <div className="mt-2 overflow-x-auto">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Position</TableHead>
                        <TableHead>Unit</TableHead>
                        <TableHead className="text-right">Filled</TableHead>
                        <TableHead className="text-right">Established</TableHead>
                        <TableHead className="text-right">Gap</TableHead>
                        <TableHead className="text-right">Exits due</TableHead>
                        <TableHead className="text-right">Suggested hires</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {baseline.positions.map((p) => (
                        <TableRow key={p.positionId}>
                          <TableCell className="font-medium">
                            {p.title}
                            {p.code && <span className="ml-1 text-xs text-muted-foreground">{p.code}</span>}
                          </TableCell>
                          <TableCell>{p.organizationUnitName ?? '—'}</TableCell>
                          <TableCell className="text-right">{p.filled}</TableCell>
                          <TableCell className="text-right">
                            {p.isEstablished ? p.expectedHeadcount : <span className="text-muted-foreground">not established</span>}
                          </TableCell>
                          <TableCell className="text-right">{p.gap == null ? '—' : p.gap}</TableCell>
                          <TableCell className="text-right">{p.exitsDue}</TableCell>
                          <TableCell className="text-right font-medium">{p.suggestedNewHires}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}

function Tile({ label, value, hint, drift }: { label: string; value: React.ReactNode; hint?: string; drift?: string }) {
  return (
    <div className="rounded-md border p-3">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="mt-1 text-xl font-semibold">{value}</div>
      {hint && <div className="mt-1 text-xs text-muted-foreground">{hint}</div>}
      {drift && (
        <div className="mt-1 flex items-center gap-1 text-xs text-amber-700">
          <AlertTriangle className="h-3 w-3" />
          {drift}
        </div>
      )}
    </div>
  );
}

function ExitList({ title, rows, dateLabel }: { title: string; rows: ManpowerPlanningExit[]; dateLabel: string }) {
  if (rows.length === 0) {
    return (
      <div>
        <div className="text-sm font-medium">{title}</div>
        <p className="text-xs text-muted-foreground">None in the period.</p>
      </div>
    );
  }
  return (
    <div>
      <div className="text-sm font-medium">{title}</div>
      <ul className="mt-1 space-y-1 text-sm">
        {rows.map((r) => (
          <li key={`${r.kind}-${r.employeeId}`} className="flex flex-wrap items-baseline gap-x-2">
            <span>{r.employeeName}</span>
            <span className="text-xs text-muted-foreground">
              {r.positionTitle ?? '—'} · {dateLabel} {fmtDate(r.date)}
            </span>
            {r.isOverdue && <Badge className="bg-amber-100 text-amber-800">overdue</Badge>}
            {r.hasSeparation && r.kind !== 'Separation' && (
              <Badge variant="outline">separation {r.separationNumber ?? 'raised'}</Badge>
            )}
            {r.kind === 'Separation' && r.separationStatus && <Badge variant="outline">{r.separationStatus}</Badge>}
          </li>
        ))}
      </ul>
    </div>
  );
}
