'use client';

import { useQuery } from '@tanstack/react-query';
import {
  Users,
  UserCheck,
  CreditCard,
  Settings,
  CalendarDays,
  Clock,
  Briefcase,
  Coins,
  ShieldPlus,
} from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';
import { employeeService } from '@/services/hr/employee.service';

function StatCard({
  label,
  value,
  loading,
  icon: Icon,
}: {
  label: string;
  value?: number;
  loading: boolean;
  icon: typeof Users;
}) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">{label}</CardTitle>
        <Icon className="h-4 w-4 text-muted-foreground" />
      </CardHeader>
      <CardContent>
        {loading ? (
          <Skeleton className="h-8 w-16" />
        ) : (
          <p className="text-2xl font-semibold">{value ?? 0}</p>
        )}
      </CardContent>
    </Card>
  );
}

/**
 * HR landing page. Headline counts come from the employee stats endpoints; the cards link
 * to the areas that exist today. Later area dashboards plug in here as they are built.
 */
export default function HrHomePage() {
  const { data: total, isLoading: totalLoading } = useQuery({
    queryKey: ['hr', 'employees', 'stats', 'total'],
    queryFn: () => employeeService.getTotalCount(),
  });

  const { data: active, isLoading: activeLoading } = useQuery({
    queryKey: ['hr', 'employees', 'stats', 'active'],
    queryFn: () => employeeService.getActiveCount(),
  });

  const { data: byStatus, isLoading: statusLoading } = useQuery({
    queryKey: ['hr', 'employees', 'stats', 'by-status'],
    queryFn: () => employeeService.getCountByStatus(),
  });

  const onProbation = byStatus?.['Probation'] ?? byStatus?.['OnProbation'];
  const terminated = byStatus?.['Terminated'];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Human Resources"
        description="Employee records, payroll, and HR setup."
      />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard label="Total employees" value={total} loading={totalLoading} icon={Users} />
        <StatCard label="Active" value={active} loading={activeLoading} icon={UserCheck} />
        <StatCard
          label="On probation"
          value={onProbation}
          loading={statusLoading}
          icon={UserCheck}
        />
        <StatCard label="Terminated" value={terminated} loading={statusLoading} icon={UserCheck} />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Areas</h2>
        <NavCardGrid
          items={[
            {
              title: 'Employees',
              description: 'Directory, profiles and lifecycle.',
              href: '/hr/employees',
              icon: Users,
            },
            {
              title: 'Leave',
              description: 'Requests, approvals, balances, plans and year-end.',
              href: '/hr/leave',
              icon: CalendarDays,
            },
            {
              title: 'Emoluments',
              description: 'The resolved pay package: basic, allowances and deductions.',
              href: '/hr/emoluments',
              icon: Coins,
            },
            {
              title: 'Benefits',
              description: 'Benefit enrolments, coverage balances and claims.',
              href: '/hr/benefits',
              icon: ShieldPlus,
            },
            {
              title: 'Attendance & Time',
              description: 'Daily attendance, overtime, remote work, alerts and payroll exports.',
              href: '/hr/attendance',
              icon: Clock,
            },
            {
              title: 'Consulting',
              description: 'Client engagements, consultant timesheets and invoices.',
              href: '/hr/consulting',
              icon: Briefcase,
            },
            {
              title: 'Payroll',
              description: 'Runs, payslips and transactions.',
              href: '/hr/payroll',
              icon: CreditCard,
            },
            {
              title: 'HR Setup',
              description: 'Organization, positions and reference data.',
              href: '/administration/hr',
              icon: Settings,
            },
          ]}
        />
      </div>
    </div>
  );
}
