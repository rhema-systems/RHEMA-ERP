'use client';

/**
 * Area 25 slice 6 — the desk register of learning-path enrolments.
 *
 * The old /hr/training/my-learning page carried the learner's own view AND these org-wide
 * tabs; the learner's side re-homed to /me/learning (D3) and this register is what remains
 * for the desk. Rows deliberately open the PORTAL enrolment detail — one working surface
 * for a path's steps, not one per world (the slice-5 development-plans precedent). Both
 * reads here are HR.Training.Read-gated server-side.
 */

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { Route, Users, Search } from 'lucide-react';
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
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { learningPathService } from '@/services/hr/learning-path.service';

const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function LearningPathEnrollmentsPage() {
  const router = useRouter();
  const [view, setView] = useState<'everyone' | 'employee'>('everyone');
  const [search, setSearch] = useState('');

  const pickerForm = useForm<{ employeeId: string }>({ defaultValues: { employeeId: '' } });
  const selectedEmployee = pickerForm.watch('employeeId');

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

  const active = view === 'everyone' ? everyone ?? [] : forEmployee ?? [];
  const isLoading = view === 'everyone' ? loadingAll : loadingEmp;

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

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Learning Path Enrollments"
        description="Who is on which path, and how far along. A learner's own view lives in their self-service portal."
        backHref="/hr/training"
      />

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <CardTitle>Enrolments</CardTitle>
              <CardDescription>
                Open one to see its steps — the row opens the same working surface the learner
                uses.
              </CardDescription>
            </div>
            <div className="flex items-center gap-2">
              <Tabs value={view} onValueChange={(v) => setView(v as typeof view)}>
                <TabsList>
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
                  <TableHead>Employee</TableHead>
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
                      {[...Array(5)].map((__, j) => (
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
                    <TableCell colSpan={5}>
                      <EmptyState
                        icon={Route}
                        title="No enrolments"
                        description="Nobody has been enrolled on a learning path yet."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((e) => (
                    <TableRow
                      key={e.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/me/learning/${e.id}`)}
                    >
                      <TableCell className="font-medium">
                        {e.employeeName}
                        {e.organizationUnitName && (
                          <div className="text-xs text-muted-foreground">
                            {e.organizationUnitName}
                          </div>
                        )}
                      </TableCell>
                      <TableCell>{e.learningPathName}</TableCell>
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
