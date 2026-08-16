'use client';

import { useParams } from 'next/navigation';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, ArrowRight } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { apiService } from '@/services/api.service';
import { employeeService } from '@/services/hr/employee.service';
import { movementService } from '@/services/hr/movement.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const money = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2 });

interface CareerPathStep {
  id: string;
  employeeId: string;
  positionId: string;
  positionTitle?: string | null;
  organizationUnitId: string;
  organizationUnitName?: string | null;
  locationName?: string | null;
  startDate: string;
  endDate?: string | null;
  isCurrent: boolean;
  movementId?: string | null;
  movementNumber?: string | null;
  salary: number;
  salaryGradeName?: string | null;
  achievements?: string | null;
  keyProjects?: string | null;
}

/**
 * One employee's career history: every position they have held, in order, with the movement that
 * caused each change.
 *
 * The steps are written by implementing a movement — closing the open one and opening the next — so
 * a gap here means a change was made to the employee record outside the movement process, not that
 * the history is broken.
 */
export default function CareerPathPage() {
  const { employeeId } = useParams<{ employeeId: string }>();

  const { data: employee } = useQuery({
    queryKey: ['hr', 'employee', employeeId],
    queryFn: () => employeeService.getById(employeeId),
  });

  const { data: steps = [], isLoading } = useQuery({
    queryKey: ['hr', 'career-paths', employeeId],
    queryFn: () =>
      apiService.get<CareerPathStep[]>(`/employee-career-paths/employee/${employeeId}`),
  });

  const { data: movements = [] } = useQuery({
    queryKey: ['hr', 'movements', 'by-employee', employeeId],
    queryFn: () => movementService.getByEmployee(employeeId),
  });

  const ordered = [...steps].sort(
    (a, b) => new Date(b.startDate).getTime() - new Date(a.startDate).getTime(),
  );

  const implemented = movements.filter((m) => m.status === 'Implemented');

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Career history"
        description={employee ? `${employee.fullName} — ${employee.employeeNumber}` : 'Loading…'}
        backHref="/hr/movements"
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Positions held</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : ordered.length === 0 ? (
            <EmptyState
              title="No career history"
              description="Career steps are written when a movement is implemented. This employee has none yet."
            />
          ) : (
            <ol className="relative space-y-6 border-l pl-6">
              {ordered.map((step) => (
                <li key={step.id} className="relative">
                  <span
                    className={`absolute -left-[1.6875rem] mt-1.5 h-3 w-3 rounded-full border-2 border-background ${
                      step.isCurrent ? 'bg-primary' : 'bg-muted-foreground/40'
                    }`}
                  />
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-medium">{step.positionTitle ?? 'Position'}</span>
                    {step.isCurrent && <Badge>Current</Badge>}
                    {step.movementNumber && (
                      <Link
                        href={`/hr/movements/${step.movementId}`}
                        className="text-xs text-muted-foreground hover:underline"
                      >
                        via {step.movementNumber}
                      </Link>
                    )}
                  </div>
                  <div className="text-sm text-muted-foreground">
                    {step.organizationUnitName}
                    {step.locationName ? ` · ${step.locationName}` : ''}
                  </div>
                  <div className="mt-1 flex flex-wrap gap-x-6 text-xs text-muted-foreground">
                    <span>
                      {fmtDate(step.startDate)} <ArrowRight className="inline h-3 w-3" />{' '}
                      {step.endDate ? fmtDate(step.endDate) : 'present'}
                    </span>
                    <span>Salary {money(step.salary)}</span>
                    {step.salaryGradeName && <span>Grade {step.salaryGradeName}</span>}
                  </div>
                  {step.achievements && (
                    <p className="mt-2 text-sm">{step.achievements}</p>
                  )}
                </li>
              ))}
            </ol>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Movements applied</CardTitle>
        </CardHeader>
        <CardContent>
          {implemented.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No movement has been implemented for this employee yet.
            </p>
          ) : (
            <ul className="space-y-2 text-sm">
              {implemented.map((m) => (
                <li key={m.id} className="flex flex-wrap items-center justify-between gap-2">
                  <Link href={`/hr/movements/${m.id}`} className="font-medium hover:underline">
                    {m.movementNumber}
                  </Link>
                  <span className="text-muted-foreground">
                    {m.movementTypeName} · effective {fmtDate(m.effectiveDate)}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
