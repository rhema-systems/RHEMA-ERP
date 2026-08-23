'use client';

import Link from 'next/link';
import type { LucideIcon } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';

export interface MetricTile {
  label: string;
  value: string | number;
  /** Small line under the value — a share, a comparison, a caveat. */
  hint?: string;
  icon?: LucideIcon;
  /** Draws attention when the number is one someone has to act on. */
  tone?: 'default' | 'warning' | 'danger' | 'success';
  /**
   * Where the number is worked off. A counter that means somebody has to do something is only
   * half a screen without it — the HR home's queue tiles are the case this was added for.
   */
  href?: string;
}

const TONE_CLASSES: Record<NonNullable<MetricTile['tone']>, string> = {
  default: 'text-foreground',
  warning: 'text-amber-600 dark:text-amber-500',
  danger: 'text-red-600 dark:text-red-500',
  success: 'text-emerald-600 dark:text-emerald-500',
};

/**
 * The header row of counters the goal dashboards open with.
 *
 * Deliberately dumb: every figure is computed server-side in one aggregate query per screen,
 * so this only formats. `tone` is for numbers that mean something is wrong — an at-risk count,
 * an unbalanced weight — and is left alone otherwise.
 */
export function MetricTiles({ tiles, className }: { tiles: MetricTile[]; className?: string }) {
  return (
    <div className={cn('grid gap-4 sm:grid-cols-2 lg:grid-cols-4', className)}>
      {tiles.map(({ label, value, hint, icon: Icon, tone = 'default', href }) => {
        const card = (
          <Card className={cn('h-full', href && 'transition-colors hover:bg-muted/50')}>
            <CardContent className="p-4">
              <div className="flex items-center justify-between gap-2">
                <p className="text-sm text-muted-foreground">{label}</p>
                {Icon && <Icon className="h-4 w-4 shrink-0 text-muted-foreground" />}
              </div>
              <p className={cn('mt-2 text-2xl font-bold tabular-nums', TONE_CLASSES[tone])}>
                {value}
              </p>
              {hint && <p className="mt-1 text-xs text-muted-foreground">{hint}</p>}
            </CardContent>
          </Card>
        );

        return href ? (
          <Link key={label} href={href} className="block">
            {card}
          </Link>
        ) : (
          <div key={label}>{card}</div>
        );
      })}
    </div>
  );
}
