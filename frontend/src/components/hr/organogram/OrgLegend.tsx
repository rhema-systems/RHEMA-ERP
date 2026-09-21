'use client';

import { AlertTriangle, Ban, CheckCircle2, Info, UserX, XCircle } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { HeatScale, HeatSwatch } from './heat';
import { levelVar } from './heat';

/**
 * What the colours and line styles mean. Status swatches always carry an icon and a word, so a
 * reader who cannot tell the colours apart still gets the message.
 */

export interface LevelInfo {
  depth: number;
  /** The level name where every node at that depth shares one (e.g. "Department"), else null. */
  label: string | null;
}

export interface OrgLegendProps {
  levels: LevelInfo[];
  heat: HeatScale | null;
  hasDotted: boolean;
  className?: string;
  /** Static export: no interactive styles, always light. */
  variant?: 'floating' | 'static';
}

const STATUS_ICONS: Record<NonNullable<HeatSwatch['status']>, typeof CheckCircle2> = {
  good: CheckCircle2,
  warning: AlertTriangle,
  serious: AlertTriangle,
  critical: XCircle,
  info: Info,
};

export function OrgLegend({ levels, heat, hasDotted, className, variant = 'floating' }: OrgLegendProps) {
  return (
    <div className={cn('org-legend', variant === 'static' && 'org-legend-static', className)}>
      {heat ? (
        <section>
          <h4>{heat.mode === 'fill' ? 'Vacancy' : heat.mode === 'span' ? 'Direct reports' : 'Headcount'}</h4>
          <ul>
            {heat.legend.map((sw) => {
              const Icon = sw.status ? STATUS_ICONS[sw.status] : null;
              return (
                <li key={sw.label}>
                  <span className="org-legend-swatch" style={{ background: sw.color }} />
                  {Icon && <Icon className="h-3 w-3" />}
                  <span>{sw.label}</span>
                </li>
              );
            })}
          </ul>
        </section>
      ) : (
        <section>
          <h4>Levels</h4>
          <ul>
            {levels.map((l) => (
              <li key={l.depth}>
                <span className="org-legend-swatch" style={{ background: levelVar(l.depth) }} />
                <span>
                  Level {l.depth + 1}
                  {l.label ? <span className="org-legend-muted"> · {l.label}</span> : null}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}
      <section>
        <h4>Marks</h4>
        <ul>
          <li>
            <svg width="28" height="10" aria-hidden>
              <path d="M1,5 H27" className="org-legend-line" />
            </svg>
            <span>Reporting line</span>
          </li>
          {hasDotted && (
            <li>
              <svg width="28" height="10" aria-hidden>
                <path d="M1,5 H27" className="org-legend-line org-legend-line-dotted" />
              </svg>
              <span>Advisory / dotted line</span>
            </li>
          )}
          <li>
            <span className="org-legend-box org-legend-box-vacant" />
            <UserX className="h-3 w-3" />
            <span>Vacant</span>
          </li>
          <li>
            <span className="org-legend-box org-legend-box-inactive" />
            <Ban className="h-3 w-3" />
            <span>Inactive</span>
          </li>
        </ul>
      </section>
    </div>
  );
}
