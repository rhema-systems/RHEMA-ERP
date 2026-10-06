'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { ExternalLink, Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { fiscalCalendarService } from '@/services/hr/company-schedule.service';

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
const day = (iso: string) =>
  new Date(`${iso.slice(0, 10)}T00:00:00Z`).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' });

/**
 * The fiscal calendar, read-only (company schedule lane 4b, D-6). Finance owns the company's fiscal years and periods —
 * they are set up, opened and closed in Finance — and HR reads them: requisitions and manpower budgets take their fiscal
 * year from here. HR's own fiscal years and periods, a third copy of the calendar read by nothing, are retired.
 *
 * A year shows its own status beside each accounting book whose year-end is closed (the user's ruling): Finance closes a
 * year per book and never marks the year itself closed. A date in a year Finance has not opened continues Finance's
 * sequence — the next year numbered one higher, from the day after (the user's ruling); only with no Finance year at all
 * does the policy's "Fiscal year starts" month answer.
 */
export default function FiscalCalendarPage() {
  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'fiscal-calendar'],
    queryFn: () => fiscalCalendarService.get(),
  });
  const years = [...(data?.years ?? [])].sort((a, b) => b.year - a.year);
  const fallback = MONTHS[(data?.fallbackStartMonth ?? 1) - 1] ?? 'January';
  const next = data?.nextYear;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Fiscal calendar"
        description="The company's fiscal years and periods, as Finance keeps them. Read-only here."
        backHref="/administration/hr/company-schedule"
        actions={
          <Button variant="outline" asChild>
            <Link href="/finance/fiscal-years">
              <ExternalLink className="mr-2 h-4 w-4" /> Set up and closed in Finance
            </Link>
          </Button>
        }
      />

      {!isLoading && years.length > 0 && (
        <p className="text-sm text-muted-foreground">
          Requisitions and manpower budgets take their fiscal year from these years. A date in a year Finance has not
          opened yet continues the sequence, as Finance will have to open it
          {next && (
            <>
              {' '}— next, <strong>FY{next.fiscalYear}</strong>, {day(next.startDate)} – {day(next.endDate)}
            </>
          )}
          .
        </p>
      )}

      {isLoading ? (
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" /> Reading Finance's calendar…
        </div>
      ) : years.length === 0 ? (
        <Card>
          <CardContent className="p-6 text-sm text-muted-foreground">
            Finance has defined no fiscal years yet, so requisitions and manpower budgets use the year starting in{' '}
            <strong>{fallback}</strong> (HR policy settings) until it does.
          </CardContent>
        </Card>
      ) : (
        years.map((y) => (
          <Card key={y.id}>
            <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3 space-y-0">
              <div>
                <CardTitle className="text-base">{y.name}</CardTitle>
                <p className="text-sm text-muted-foreground">
                  {day(y.startDate)} – {day(y.endDate)} · {y.periods.length} period{y.periods.length === 1 ? '' : 's'}
                </p>
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <StatusBadge status={y.status} />
                {y.isLocked && <Badge variant="outline">Locked</Badge>}
                {y.books.map((b) => (
                  <Badge key={b.bookCode} variant={b.status === 'Closed' ? 'default' : 'secondary'}>
                    {b.bookName ?? b.bookCode}: year-end {b.status === 'Closed' ? 'closed' : 'closing'}
                  </Badge>
                ))}
              </div>
            </CardHeader>
            {y.periods.length > 0 && (
              <CardContent>
                <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
                  {y.periods.map((p) => (
                    <div key={p.number} className="flex items-center justify-between rounded-md border px-3 py-2 text-sm">
                      <div className="min-w-0">
                        <p className="truncate font-medium">{p.name}</p>
                        <p className="text-xs text-muted-foreground">
                          {day(p.startDate)} – {day(p.endDate)}
                        </p>
                      </div>
                      <StatusBadge status={p.status} />
                    </div>
                  ))}
                </div>
              </CardContent>
            )}
          </Card>
        ))
      )}
    </div>
  );
}
