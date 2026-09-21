'use client';

import type { ReactNode } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { ExternalLink, Inbox, type LucideIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';

export interface RecordColumn<T> {
  key: string;
  header: string;
  render: (row: T) => ReactNode;
  className?: string;
}

interface RecordSummaryTabProps<T> {
  title: string;
  description?: string;
  /** Stable per employee — it keys the cache. */
  queryKey: readonly unknown[];
  queryFn: () => Promise<T[]>;
  columns: RecordColumn<T>[];
  rowKey: (row: T) => string;
  /** Where the row's full record lives; the row becomes a link when given. */
  rowHref?: (row: T) => string | null;
  emptyTitle: string;
  emptyDescription: string;
  emptyIcon?: LucideIcon;
  /** The module that OWNS these records — every write happens there, never here. */
  openHref: string;
  openLabel: string;
  /** Rendered above the table — a balances strip, a year picker. */
  toolbar?: ReactNode;
  /** Rendered under the table (a legend, a caveat). */
  footer?: ReactNode;
}

/**
 * One read-only record tab on the employee profile (round 3, lanes T2/T3; decision D-4).
 *
 * Every sub-module already answered "what does this employee have?" through a by-employee route
 * that nothing surfaced; the demo asked to see those answers on the profile. This tab is that
 * surface and nothing more: the module's own list DTO, a handful of columns, and a door to the
 * module for anything that changes state. Writes stay where their rules, approvals and audit live.
 *
 * ⚠ Read-only is a design decision, not a shortcut. A movement is implemented on the movements
 * screen because that is where the engine's actions, the salary placement and the return are; a
 * button here would be a second door onto the same rule, and the second door is where the rule
 * drifts.
 */
export function RecordSummaryTab<T>({
  title,
  description,
  queryKey,
  queryFn,
  columns,
  rowKey,
  rowHref,
  emptyTitle,
  emptyDescription,
  emptyIcon = Inbox,
  openHref,
  openLabel,
  toolbar,
  footer,
}: RecordSummaryTabProps<T>) {
  const { data, isLoading, isError, error } = useQuery({ queryKey, queryFn });
  const rows = data ?? [];

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-4 space-y-0">
        <div className="space-y-1.5">
          <CardTitle className="text-base">{title}</CardTitle>
          {description && <CardDescription>{description}</CardDescription>}
        </div>
        <Button variant="outline" size="sm" asChild>
          <Link href={openHref}>
            <ExternalLink className="mr-2 h-4 w-4" />
            {openLabel}
          </Link>
        </Button>
      </CardHeader>
      <CardContent className="space-y-4">
        {toolbar}
        {isLoading ? (
          <div className="space-y-2">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
          </div>
        ) : isError ? (
          // A 403 here is the module's own gate answering; say so rather than "no records".
          <p className="text-sm text-destructive" data-testid="record-tab-error">
            {(error as Error)?.message || 'These records could not be loaded.'}
          </p>
        ) : rows.length === 0 ? (
          <EmptyState icon={emptyIcon} title={emptyTitle} description={emptyDescription} />
        ) : (
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  {columns.map((c) => (
                    <TableHead key={c.key} className={c.className}>
                      {c.header}
                    </TableHead>
                  ))}
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => {
                  const href = rowHref?.(row) ?? null;
                  return (
                    <TableRow key={rowKey(row)} data-testid="record-tab-row">
                      {columns.map((c, i) => (
                        <TableCell key={c.key} className={c.className}>
                          {i === 0 && href ? (
                            <Link href={href} className="font-medium hover:underline">
                              {c.render(row)}
                            </Link>
                          ) : (
                            c.render(row)
                          )}
                        </TableCell>
                      ))}
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>
        )}
        {footer}
      </CardContent>
    </Card>
  );
}

/** A dash for the many optional strings these DTOs carry. */
export const dash = (v?: string | number | null) => (v === null || v === undefined || v === '' ? '—' : String(v));
