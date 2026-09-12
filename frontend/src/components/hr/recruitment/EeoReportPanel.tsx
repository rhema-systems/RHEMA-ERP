'use client';

import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { formatDateTime } from '@/lib/hr/attendance-format';
import { jobApplicationService } from '@/services/hr/recruitment-pipeline.service';
import type { EeoReport } from '@/types/hr/recruitment-pipeline';

function StageCard({ title, stage }: { title: string; stage: EeoReport['allApplicants'] }) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm text-muted-foreground">{title}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-1">
        <p className="text-2xl font-bold tabular-nums">{stage.total}</p>
        {stage.total > 0 ? (
          <p className="text-xs text-muted-foreground">
            {stage.male} male ({stage.malePercent ?? 0}%) · {stage.female} female (
            {stage.femalePercent ?? 0}%) · {stage.other} other · {stage.preferNotToSay} undisclosed
          </p>
        ) : (
          <p className="text-xs text-muted-foreground">No applicants at this stage.</p>
        )}
        <p className="text-xs text-muted-foreground">
          Avg. age {stage.averageAge != null ? Math.round(stage.averageAge) : '—'} ·{' '}
          {stage.internalCandidateCount} internal
        </p>
      </CardContent>
    </Card>
  );
}

/**
 * EEO / diversity compliance report — one vacancy at a time, HR-only (the same gate as the rest
 * of `api/job-applications`; the demographic breakdown is exactly the PII that controller
 * restricts). Read-only: this is a compliance report, not a working screen.
 */
export function EeoReportPanel({ vacancyId }: { vacancyId: string }) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'vacancy-eeo-report', vacancyId],
    queryFn: () => jobApplicationService.getEeoReport(vacancyId),
    enabled: !!vacancyId,
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-10">
        <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !data) {
    return <EmptyState title="Could not load the report" description="Try again shortly." />;
  }

  return (
    <div className="space-y-4">
      <p className="text-xs text-muted-foreground">Generated {formatDateTime(data.generatedAt)}</p>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StageCard title="All applicants" stage={data.allApplicants} />
        <StageCard title="Shortlisted" stage={data.shortlisted} />
        <StageCard title="Rejected" stage={data.rejected} />
        <StageCard title="Hired" stage={data.hired} />
      </div>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">By pipeline status</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {data.byStatus.length === 0 ? (
            <div className="p-6">
              <EmptyState title="Nothing to break down" description="No applications on this vacancy yet." />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Status</TableHead>
                  <TableHead className="text-right">Total</TableHead>
                  <TableHead className="text-right">Male</TableHead>
                  <TableHead className="text-right">Female</TableHead>
                  <TableHead className="text-right">Other</TableHead>
                  <TableHead className="text-right">Undisclosed</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.byStatus.map((row) => (
                  <TableRow key={row.status}>
                    <TableCell>{row.status}</TableCell>
                    <TableCell className="text-right tabular-nums">{row.total}</TableCell>
                    <TableCell className="text-right tabular-nums">{row.demographics.male}</TableCell>
                    <TableCell className="text-right tabular-nums">{row.demographics.female}</TableCell>
                    <TableCell className="text-right tabular-nums">{row.demographics.other}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {row.demographics.preferNotToSay}
                    </TableCell>
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
