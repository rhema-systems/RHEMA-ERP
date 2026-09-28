'use client';

/**
 * What Finance says was actually spent against an HR budget — a manpower budget or a training
 * budget (HR finish plan lane 8, slice 6).
 *
 * ⚠ This is a READ of Finance's book balances on the budget's account over the budget's window.
 * HR writes nothing here, and the figure is not HR's own `spentAmount`: the two are allowed to
 * disagree, and when they do it is the disagreement that is the news. A budget with no account
 * mapped, or no accounting book resolvable, comes back `linked: false` with `problem` set — the
 * sentence is shown as-is rather than a zero, because a zero would be read as "nothing spent".
 *
 * Fiscal periods do not line up with budget windows. A period that starts before or ends after
 * the window is counted WHOLE, and those rows say so, so a total that looks too big can be
 * explained instead of doubted.
 */

import { Landmark } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { fmtPostingMoney } from '@/components/hr/common/FinancePostingCard';
import type { HrBudgetFinanceActuals } from '@/types/hr/finance-posting';

/** Structurally what a `useQuery<HrBudgetFinanceActuals>` result gives, so one can be passed straight in. */
export interface FinanceActualsQueryLike {
  data?: HrBudgetFinanceActuals;
  isLoading: boolean;
  isError?: boolean;
  error?: unknown;
}

const fmtDate = (value?: string | null) => (value ? new Date(value).toLocaleDateString() : '—');

export function FinanceActualsCard({
  title = 'Finance actuals',
  query,
  className,
}: {
  title?: string;
  query: FinanceActualsQueryLike;
  className?: string;
}) {
  const { data, isLoading, isError, error } = query;

  if (isLoading) {
    return (
      <Card className={className}>
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <Landmark className="h-4 w-4" aria-hidden />
            {title}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          <Skeleton className="h-4 w-2/3" />
          <Skeleton className="h-4 w-1/2" />
        </CardContent>
      </Card>
    );
  }

  if (isError || !data) {
    return (
      <Card className={className}>
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <Landmark className="h-4 w-4" aria-hidden />
            {title}
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">
            {(error as Error | undefined)?.message ?? 'Finance could not be read just now.'}
          </p>
        </CardContent>
      </Card>
    );
  }

  const money = (v: number) => fmtPostingMoney(v, data.functionalCurrencyCode);
  // Variance = budget − actual, so a POSITIVE number is money left and a negative one is an overspend.
  const over = data.variance < 0;

  return (
    <Card className={className}>
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <Landmark className="h-4 w-4" aria-hidden />
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-4 text-sm">
        {!data.linked ? (
          <p className="rounded-md bg-muted p-3 text-muted-foreground">
            {data.problem ?? 'No Finance account is mapped for this budget, so nothing can be read.'}
          </p>
        ) : (
          <>
            <p className="text-xs text-muted-foreground">
              {data.accountCode} {data.accountName}
              {data.unitName ? ` · ${data.unitName}` : ''} · {fmtDate(data.periodStart)} –{' '}
              {fmtDate(data.periodEnd)}
            </p>

            <div className="grid gap-3 sm:grid-cols-3">
              <div>
                <p className="text-xs text-muted-foreground">Budget</p>
                <p className="text-base font-semibold">{money(data.budget)}</p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Finance actual</p>
                <p className="text-base font-semibold">{money(data.actual)}</p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Variance</p>
                <p
                  className={
                    over
                      ? 'text-base font-semibold text-destructive'
                      : 'text-base font-semibold text-emerald-700 dark:text-emerald-400'
                  }
                >
                  {over ? `${money(-data.variance)} over` : `${money(data.variance)} left`}
                  <span className="ml-1 text-xs font-normal">({data.variancePercentage}%)</span>
                </p>
              </div>
            </div>

            {data.periods.length > 0 ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Period</TableHead>
                    <TableHead>Dates</TableHead>
                    <TableHead className="text-right">Debits</TableHead>
                    <TableHead className="text-right">Credits</TableHead>
                    <TableHead className="text-right">Net</TableHead>
                    <TableHead className="text-right">Entries</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.periods.map((p) => (
                    <TableRow key={p.fiscalPeriodId}>
                      <TableCell>
                        {p.periodName}
                        {p.partlyOutsideBudget && (
                          <span className="ml-2 text-xs text-muted-foreground">whole period counted</span>
                        )}
                      </TableCell>
                      <TableCell className="whitespace-nowrap text-xs text-muted-foreground">
                        {fmtDate(p.startDate)} – {fmtDate(p.endDate)}
                      </TableCell>
                      <TableCell className="text-right">{money(p.debits)}</TableCell>
                      <TableCell className="text-right">{money(p.credits)}</TableCell>
                      <TableCell className="text-right">{money(p.netMovement)}</TableCell>
                      <TableCell className="text-right">{p.transactionCount}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            ) : (
              <p className="text-xs text-muted-foreground">
                No fiscal period in the budget&apos;s window has any movement on this account yet.
              </p>
            )}
          </>
        )}

        <p className="text-xs text-muted-foreground">
          Read from Finance&apos;s book balances ({data.accountingBookCode ?? 'no book named'}); HR
          writes nothing.
        </p>
      </CardContent>
    </Card>
  );
}
