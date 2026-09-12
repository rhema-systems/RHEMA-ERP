'use client';

import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import type { PerformanceRating, PotentialRating, TalentReviewRatingSummary } from '@/types/hr/succession';

/**
 * Rows are potential (high at the top, the way every nine box is drawn); columns are performance,
 * weakest on the left. The cell names are the conventional talent-grid labels.
 */
const POTENTIAL_ROWS: PotentialRating[] = ['HighPotential', 'MediumPotential', 'LowPotential'];
const PERFORMANCE_COLS: PerformanceRating[] = [
  'BelowExpectations',
  'MeetsExpectations',
  'ExceedsExpectations',
];

/**
 * ⚠ Performance has FIVE values but the grid has three columns. `Unsatisfactory` folds into the
 * lowest column and `Outstanding` into the highest — otherwise a rating placed at either extreme
 * would vanish from the grid entirely, which is the worst possible failure for a screen whose whole
 * job is showing where people sit.
 */
const COLUMN_FOR: Record<PerformanceRating, PerformanceRating> = {
  Unsatisfactory: 'BelowExpectations',
  BelowExpectations: 'BelowExpectations',
  MeetsExpectations: 'MeetsExpectations',
  ExceedsExpectations: 'ExceedsExpectations',
  Outstanding: 'ExceedsExpectations',
};

const CELL_LABEL: Record<string, string> = {
  'HighPotential|BelowExpectations': 'Enigma',
  'HighPotential|MeetsExpectations': 'Growth employee',
  'HighPotential|ExceedsExpectations': 'Star',
  'MediumPotential|BelowExpectations': 'Dilemma',
  'MediumPotential|MeetsExpectations': 'Core employee',
  'MediumPotential|ExceedsExpectations': 'High performer',
  'LowPotential|BelowExpectations': 'Under-performer',
  'LowPotential|MeetsExpectations': 'Effective',
  'LowPotential|ExceedsExpectations': 'Trusted professional',
};

const CELL_TONE: Record<string, string> = {
  'HighPotential|ExceedsExpectations': 'bg-emerald-50 dark:bg-emerald-950/40 border-emerald-300 dark:border-emerald-800',
  'HighPotential|MeetsExpectations': 'bg-emerald-50/60 dark:bg-emerald-950/20 border-emerald-200 dark:border-emerald-900',
  'MediumPotential|ExceedsExpectations': 'bg-emerald-50/60 dark:bg-emerald-950/20 border-emerald-200 dark:border-emerald-900',
  'LowPotential|BelowExpectations': 'bg-red-50 dark:bg-red-950/40 border-red-300 dark:border-red-800',
  'HighPotential|BelowExpectations': 'bg-amber-50 dark:bg-amber-950/30 border-amber-200 dark:border-amber-900',
  'MediumPotential|BelowExpectations': 'bg-amber-50 dark:bg-amber-950/30 border-amber-200 dark:border-amber-900',
};

const spaced = (v: string) => v.replace(/([a-z])([A-Z0-9])/g, '$1 $2');

/**
 * The nine box for one calibration session.
 *
 * ⚠ Reads from the ratings already loaded with the session rather than calling the per-cell
 * endpoint nine times. The endpoint exists and is used for drilling into a single cell; painting
 * the whole grid from it would be nine round trips to display data the caller already has.
 */
export function NineBoxGrid({
  ratings,
  onSelect,
}: {
  ratings: TalentReviewRatingSummary[];
  onSelect?: (rating: TalentReviewRatingSummary) => void;
}) {
  const cellOf = (potential: PotentialRating, performance: PerformanceRating) =>
    ratings.filter(
      (r) => r.potential === potential && COLUMN_FOR[r.performance] === performance,
    );

  return (
    <div className="space-y-2">
      <div className="grid grid-cols-[auto_repeat(3,minmax(0,1fr))] gap-2">
        <div />
        {PERFORMANCE_COLS.map((p) => (
          <div key={p} className="pb-1 text-center text-xs font-medium uppercase tracking-wide text-muted-foreground">
            {spaced(p)}
          </div>
        ))}

        {POTENTIAL_ROWS.map((potential) => (
          <PotentialRow
            key={potential}
            potential={potential}
            cellOf={cellOf}
            onSelect={onSelect}
          />
        ))}
      </div>

      <div className="flex justify-between text-xs text-muted-foreground">
        <span>← lower performance</span>
        <span>
          Unsatisfactory folds into the left column, Outstanding into the right — nobody is hidden.
        </span>
        <span>higher performance →</span>
      </div>
    </div>
  );
}

function PotentialRow({
  potential,
  cellOf,
  onSelect,
}: {
  potential: PotentialRating;
  cellOf: (p: PotentialRating, perf: PerformanceRating) => TalentReviewRatingSummary[];
  onSelect?: (rating: TalentReviewRatingSummary) => void;
}) {
  return (
    <>
      <div className="flex w-24 items-center justify-end pr-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
        {spaced(potential).replace(' Potential', '')}
      </div>
      {PERFORMANCE_COLS.map((performance) => {
        const key = `${potential}|${performance}`;
        const occupants = cellOf(potential, performance);
        return (
          <Card key={key} className={`min-h-32 border ${CELL_TONE[key] ?? ''}`}>
            <CardContent className="space-y-1 p-2">
              <div className="flex items-baseline justify-between">
                <span className="text-[11px] font-medium text-muted-foreground">
                  {CELL_LABEL[key]}
                </span>
                {occupants.length > 0 && (
                  <span className="text-[11px] text-muted-foreground">{occupants.length}</span>
                )}
              </div>
              {occupants.map((r) => (
                <button
                  key={r.id}
                  type="button"
                  onClick={() => onSelect?.(r)}
                  className="block w-full truncate rounded px-1 py-0.5 text-left text-xs hover:bg-background/70"
                  title={`${r.employeeName}${r.employeePosition ? ` — ${r.employeePosition}` : ''}`}
                >
                  {r.employeeName}
                  {/* An uncalibrated placement is provisional and must not read as settled. */}
                  {!r.calibrationConfirmed && (
                    <Badge variant="outline" className="ml-1 px-1 py-0 text-[10px]">
                      draft
                    </Badge>
                  )}
                  {r.previousPotential && r.previousPotential !== r.potential && (
                    <span className="ml-1 text-[10px] text-muted-foreground">moved</span>
                  )}
                </button>
              ))}
            </CardContent>
          </Card>
        );
      })}
    </>
  );
}
