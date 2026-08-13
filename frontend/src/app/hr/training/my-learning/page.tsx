'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { Route, Users, CheckCircle2, Search } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { learningPathService } from '@/services/hr/learning-path.service';

const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * The learner's own paths, plus an org-wide view for HR.
 *
 * "Mine" is token-derived — the page never passes an employee id for the caller's own data, which is
 * what keeps it from becoming a way to read someone else's record. The org-wide tab uses the richer
 * enrolment list that carries where each learner sits.
 */
export default function MyLearningPage() {
  const router = useRouter();
  const [view, setView] = useState<'mine' | 'everyone' | 'employee'>('mine');
  const [search, setSearch] = useState('');

  const pickerForm = useForm<{ employeeId: string }>({ defaultValues: { employeeId: '' } });
  const selectedEmployee = pickerForm.watch('employeeId');

  const { data: mine, isLoading: loadingMine } = useQuery({
    queryKey: ['hr', 'training', 'learning-paths', 'enrollments', 'mine'],
    queryFn: () => learningPathService.getMyEnrollments(),
  });
  const { data: everyone, isLoading: loadingAll } = useQuery({
    queryKey: ['hr', 'training', 'learning-paths', 'enrollments', 'all'],
    queryFn: () => learningPathService.getAllEnrollments(),
    enabled: view === 'everyone',
  });
  const { data: forEmployee, isLoading: loadingEmp } = useQuery({
    queryKey: ['hr', 'training', 'learning-paths', 'enrollments', 'employee', selectedEmployee],
    queryFn: () => learningPathService.getEnrollmentsForEmployee(selectedEmployee),
    enabled: view === 'employee' && !!selectedEmployee,
  });

  const active =
    view === 'mine' ? mine ?? [] : view === 'everyone' ? everyone ?? [] : forEmployee ?? [];
  const isLoading = view === 'mine' ? loadingMine : view === 'everyone' ? loadingAll : loadingEmp;

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = active as any[];
    if (!term) return all;
    return all.filter(
      (e) =>
        (e.learningPathName ?? '').toLowerCase().includes(term) ||
        (e.employeeName ?? '').toLowerCase().includes(term),
    );
  }, [active, search]);

  const myRows = mine ?? [];
  const tiles = useMemo(
    () => [
      { label: 'My paths', value: myRows.length, icon: Route },
      {
        label: 'In progress',
        value: myRows.filter((e) => !e.isCompleted).length,
      },
      { label: 'Completed', value: myRows.filter((e) => e.isCompleted).length, icon: CheckCircle2 },
      {
        label: 'Average progress',
        value: myRows.length
          ? `${Math.round(myRows.reduce((a, e) => a + e.progressPercentage, 0) / myRows.length)}%`
          : '—',
      },
    ],
    [myRows],
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Learning Paths"
        description="Curricula you are working through, step by step."
        backHref="/hr/training"
      />

      <MetricTiles tiles={tiles} />

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <CardTitle>Enrolments</CardTitle>
              <CardDescription>Open one to see its steps and what is unlocked next.</CardDescription>
            </div>
            <div className="flex items-center gap-2">
              <Tabs value={view} onValueChange={(v) => setView(v as typeof view)}>
                <TabsList>
                  <TabsTrigger value="mine">Mine</TabsTrigger>
                  <TabsTrigger value="everyone">Everyone</TabsTrigger>
                  <TabsTrigger value="employee">By employee</TabsTrigger>
                </TabsList>
              </Tabs>
              <div className="relative w-56">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search…"
                  className="pl-8"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
              </div>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {view === 'employee' && (
            <div className="max-w-sm">
              <EmployeePickerField form={pickerForm} name="employeeId" label="Employee" />
            </div>
          )}

          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  {view !== 'mine' && <TableHead>Employee</TableHead>}
                  <TableHead>Path</TableHead>
                  <TableHead>Enrolled</TableHead>
                  <TableHead>Target</TableHead>
                  <TableHead className="w-[200px]">Progress</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(3)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(view !== 'mine' ? 5 : 4)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[90px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : view === 'employee' && !selectedEmployee ? (
                  <TableRow>
                    <TableCell colSpan={5}>
                      <EmptyState
                        icon={Users}
                        title="Pick an employee"
                        description="Choose someone above to see the paths they are on."
                      />
                    </TableCell>
                  </TableRow>
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={view !== 'mine' ? 5 : 4}>
                      <EmptyState
                        icon={Route}
                        title={view === 'mine' ? 'You are not on a path' : 'No enrolments'}
                        description={
                          view === 'mine'
                            ? 'Learning paths you are enrolled on will appear here.'
                            : 'Nobody has been enrolled on a learning path yet.'
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((e) => (
                    <TableRow
                      key={e.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/hr/training/my-learning/${e.id}`)}
                    >
                      {view !== 'mine' && (
                        <TableCell className="font-medium">
                          {e.employeeName}
                          {e.organizationUnitName && (
                            <div className="text-xs text-muted-foreground">
                              {e.organizationUnitName}
                            </div>
                          )}
                        </TableCell>
                      )}
                      <TableCell className={view === 'mine' ? 'font-medium' : undefined}>
                        {e.learningPathName}
                      </TableCell>
                      <TableCell className="text-muted-foreground">{fmt(e.enrolledDate)}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {fmt(e.targetCompletionDate)}
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <Progress value={e.progressPercentage} className="h-2" />
                          <span className="w-10 text-right text-xs text-muted-foreground">
                            {e.progressPercentage}%
                          </span>
                          {e.isCompleted && (
                            <Badge variant="default" className="text-[10px]">
                              Done
                            </Badge>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
