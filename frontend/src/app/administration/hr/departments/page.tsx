'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Search, Building2, Info } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { departmentService } from '@/services/hr/lookup.service';

/**
 * Departments are a read-only lookup here. The backend exposes GET only — HR's own
 * hierarchy is Organization Structure → Level → Unit, and Department exists to satisfy
 * Employee.DepartmentId and other modules (maintenance, scheduling). There is deliberately
 * no create/edit affordance on this page.
 */
export default function DepartmentsPage() {
  const [search, setSearch] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'departments'],
    queryFn: () => departmentService.getAll(),
  });

  const departments = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = data ?? [];
    if (!term) return all;
    return all.filter(
      (d) => d.name.toLowerCase().includes(term) || d.code.toLowerCase().includes(term),
    );
  }, [data, search]);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Departments"
        description="A read-only lookup shared with other modules."
      />

      <div className="flex items-start gap-2 rounded-md border bg-muted/40 p-3 text-sm text-muted-foreground">
        <Info className="mt-0.5 h-4 w-4 shrink-0" />
        <p>
          Departments are maintained outside HR and cannot be edited here. HR&apos;s own
          structure lives under Organization — Structures, Levels and Units.
        </p>
      </div>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Departments</CardTitle>
            <div className="relative w-64">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search departments…"
                className="pl-8"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Code</TableHead>
                  <TableHead>Parent</TableHead>
                  <TableHead>Head</TableHead>
                  <TableHead className="text-right">Employees</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      <TableCell><Skeleton className="h-4 w-[180px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[70px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[140px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[140px]" /></TableCell>
                      <TableCell><Skeleton className="ml-auto h-4 w-[40px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[70px]" /></TableCell>
                    </TableRow>
                  ))
                ) : departments.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <EmptyState
                        icon={Building2}
                        title={search ? 'No matching departments' : 'No departments'}
                        description={
                          search
                            ? 'Try a different search.'
                            : 'Departments are created outside HR.'
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  departments.map((d) => (
                    <TableRow key={d.id}>
                      <TableCell className="font-medium">{d.name}</TableCell>
                      <TableCell className="text-muted-foreground">{d.code}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {d.parentDepartmentName || '—'}
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {d.departmentHeadName || '—'}
                      </TableCell>
                      <TableCell className="text-right">{d.employeeCount ?? 0}</TableCell>
                      <TableCell>
                        <StatusBadge active={d.isActive} />
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
