'use client';

/**
 * The reverse read: which employee-relations cases point at THIS record.
 *
 * Area 9c built the forward link (a case names the incident, PIP or disciplinary case it arose
 * from) and the reverse API and client method (`getCasesForSource`) — and then nothing rendered
 * it. Lane 6 closes that: a capability with no reader is the same defect as a client method with
 * no caller, one level further out.
 *
 * ⚠ **Mounted on the HR-desk screens only.** The read is gated on the employee-relations READ
 * permission, not the source module's, because the answer is about ER cases — so it belongs on
 * `/hr/...` detail pages and not on `/me/...`, where the subject of a disciplinary case must not be
 * shown the grievances filed about it.
 */

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Scale } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { employeeRelationsService } from '@/services/hr/employee-relations.service';
import {
  SETTLED_GRIEVANCE_STATUSES,
  type EmployeeRelationsLinkSource,
} from '@/types/hr/employee-relations';

const SOURCE_LABEL: Record<EmployeeRelationsLinkSource, string> = {
  SafetyIncident: 'this incident',
  PerformanceImprovementPlan: 'this improvement plan',
  DisciplinaryCase: 'this disciplinary case',
};

export function LinkedErCasesPanel({
  source,
  recordId,
}: {
  source: EmployeeRelationsLinkSource;
  recordId: string;
}) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'employee-relations', 'by-source', source, recordId],
    queryFn: () => employeeRelationsService.getCasesForSource(source, recordId),
    enabled: !!recordId,
    // ⚠ 403 is a real, expected answer here: a SHE or performance user without the ER read
    // permission gets one, and a retrying spinner would look like the panel is broken.
    retry: false,
  });

  // A user who cannot see ER cases should see nothing, not an error — the gate is the point.
  if (isError) return null;

  const cases = data ?? [];

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <Scale className="h-4 w-4 text-muted-foreground" />
          Employee-relations cases
          {!isLoading && (
            <Badge variant={cases.length ? 'secondary' : 'outline'}>{cases.length}</Badge>
          )}
        </CardTitle>
        <CardDescription>
          Grievances and other employee-relations cases that arose from, or concern,{' '}
          {SOURCE_LABEL[source]}.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <Skeleton className="h-10 w-full" />
        ) : cases.length === 0 ? (
          <p className="text-sm text-muted-foreground">None recorded.</p>
        ) : (
          <ul className="divide-y">
            {cases.map((c) => {
              const settled = SETTLED_GRIEVANCE_STATUSES.includes(c.status);
              return (
                <li key={c.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
                  <div className="min-w-0">
                    <Link
                      href={`/hr/employee-relations/${c.id}`}
                      className="font-medium hover:underline"
                    >
                      {c.grievanceNumber}
                    </Link>
                    <span className="text-muted-foreground"> · {c.caseTypeName}</span>
                    <div className="truncate text-sm text-muted-foreground">{c.subject}</div>
                  </div>
                  <div className="flex items-center gap-2 text-sm">
                    <span className="text-muted-foreground">{c.employeeName}</span>
                    <Badge variant={settled ? 'outline' : 'secondary'}>{c.statusName}</Badge>
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
