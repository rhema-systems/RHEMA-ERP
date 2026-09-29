'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Search, MoreHorizontal, Eye, Pencil, UserX, UserCheck, Trash2, Users, Scale } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { useDebounce } from '@/hooks/use-debounce';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { employeeService } from '@/services/hr/employee.service';
import type { Employee } from '@/types/hr/employee';

const PAGE_SIZE = 20;

type PendingAction = { type: 'deactivate' | 'activate' | 'delete'; employee: Employee };

export default function EmployeesPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounce(search, 400);
  const [page, setPage] = useState(1);
  const [pending, setPending] = useState<PendingAction | null>(null);
  const [busy, setBusy] = useState(false);
  // 'all' | 'on' | 'off' — whether the person is paid through the payroll run.
  const [payrollFilter, setPayrollFilter] = useState<'all' | 'on' | 'off'>('all');
  const isOnPayroll = payrollFilter === 'all' ? undefined : payrollFilter === 'on';
  // Round 4, lane O — the people Maintenance may assign work to. The first caller the search's
  // MaintenanceTechniciansOnly criterion ever had.
  const [techniciansOnly, setTechniciansOnly] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'employees', page, PAGE_SIZE, debouncedSearch, payrollFilter, techniciansOnly],
    queryFn: () =>
      employeeService.searchPaged(
        {
          searchTerm: debouncedSearch || undefined,
          isOnPayroll,
          maintenanceTechniciansOnly: techniciansOnly || undefined,
        },
        page,
        PAGE_SIZE,
      ),
  });

  const employees = data?.items ?? [];
  const filtering = !!debouncedSearch || payrollFilter !== 'all' || techniciansOnly;

  const runAction = async () => {
    if (!pending) return false;
    setBusy(true);
    try {
      const { type, employee } = pending;
      if (type === 'deactivate') await employeeService.deactivate(employee.id);
      else if (type === 'activate') await employeeService.activate(employee.id);
      else if (type === 'delete') await employeeService.remove(employee.id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'employees'] });
      toast({ title: 'Done', description: `${employee.fullName} updated.` });
      setPending(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Action failed.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setBusy(false);
    }
  };

  const dialogCopy: Record<PendingAction['type'], { title: string; confirm: string; destructive: boolean }> = {
    deactivate: { title: 'Deactivate employee', confirm: 'Deactivate', destructive: true },
    activate: { title: 'Activate employee', confirm: 'Activate', destructive: false },
    delete: { title: 'Delete employee', confirm: 'Delete', destructive: true },
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Employees"
        description="Manage your organization's employee records."
        actions={
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => router.push('/hr/employees/payroll-reconciliation')}>
              <Scale className="mr-2 h-4 w-4" /> Payroll reconciliation
            </Button>
            <Button onClick={() => router.push('/hr/employees/new')}>
              <Plus className="mr-2 h-4 w-4" /> New Employee
            </Button>
          </div>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between gap-3">
            <CardTitle>Employee List</CardTitle>
            <div className="flex items-center gap-2">
              <Select
                value={payrollFilter}
                onValueChange={(v) => {
                  setPayrollFilter(v as 'all' | 'on' | 'off');
                  setPage(1);
                }}
              >
                <SelectTrigger className="w-44" aria-label="Payroll filter">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All staff</SelectItem>
                  <SelectItem value="on">On payroll</SelectItem>
                  <SelectItem value="off">Not on payroll</SelectItem>
                </SelectContent>
              </Select>
              <Select
                value={techniciansOnly ? 'technicians' : 'all'}
                onValueChange={(v) => {
                  setTechniciansOnly(v === 'technicians');
                  setPage(1);
                }}
              >
                <SelectTrigger className="w-52" aria-label="Maintenance filter">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">Any role</SelectItem>
                  <SelectItem value="technicians">Maintenance technicians</SelectItem>
                </SelectContent>
              </Select>
              <div className="relative w-72">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search by name, number, email…"
                  className="pl-8"
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPage(1);
                  }}
                />
              </div>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>Position</TableHead>
                  <TableHead>Organization Unit</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(6)].map((_, i) => (
                    <TableRow key={i}>
                      <TableCell><Skeleton className="h-4 w-[200px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[140px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[140px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[70px]" /></TableCell>
                      <TableCell><Skeleton className="h-8 w-8" /></TableCell>
                    </TableRow>
                  ))
                ) : employees.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5}>
                      <EmptyState
                        icon={Users}
                        title={filtering ? 'No matching employees' : 'No employees yet'}
                        description={
                          !filtering
                            ? 'Add your first employee.'
                            : techniciansOnly && !debouncedSearch && payrollFilter === 'all'
                              ? 'Nobody is available to Maintenance yet. Mark a position as a technician role, or include a person by hand on their record.'
                              : 'Try a different search or filter.'
                        }
                        action={
                          !filtering ? (
                            <Button size="sm" onClick={() => router.push('/hr/employees/new')}>
                              <Plus className="mr-2 h-4 w-4" /> New Employee
                            </Button>
                          ) : undefined
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  employees.map((emp) => (
                    <TableRow
                      key={emp.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/hr/employees/${emp.id}`)}
                    >
                      <TableCell>
                        <div className="flex flex-col">
                          <span className="font-medium">{emp.fullName || `${emp.firstName} ${emp.lastName}`}</span>
                          <span className="text-xs text-muted-foreground">
                            {emp.employeeNumber}
                            {emp.emailAddress ? ` · ${emp.emailAddress}` : ''}
                          </span>
                        </div>
                      </TableCell>
                      <TableCell className="text-muted-foreground">{emp.positionTitle || '—'}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {emp.organizationUnitName || '—'}
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap items-center gap-1.5">
                          <StatusBadge status={emp.staffStatus} />
                          {emp.isOnPayroll === false && (
                            <span className="inline-flex items-center rounded-full bg-slate-200 px-2 py-0.5 text-[11px] font-medium text-slate-700 dark:bg-slate-700 dark:text-slate-100">
                              Not on payroll
                            </span>
                          )}
                          {emp.canBeAssignedToMaintenance && (
                            <span className="inline-flex items-center rounded-full bg-sky-100 px-2 py-0.5 text-[11px] font-medium text-sky-800 dark:bg-sky-900 dark:text-sky-100">
                              Technician
                            </span>
                          )}
                        </div>
                      </TableCell>
                      <TableCell>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button
                              variant="ghost"
                              className="h-8 w-8 p-0"
                              onClick={(e) => e.stopPropagation()}
                            >
                              <span className="sr-only">Open menu</span>
                              <MoreHorizontal className="h-4 w-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuLabel>Actions</DropdownMenuLabel>
                            <DropdownMenuItem
                              onClick={(e) => {
                                e.stopPropagation();
                                router.push(`/hr/employees/${emp.id}`);
                              }}
                            >
                              <Eye className="mr-2 h-4 w-4" /> View
                            </DropdownMenuItem>
                            <DropdownMenuItem
                              onClick={(e) => {
                                e.stopPropagation();
                                router.push(`/hr/employees/${emp.id}/edit`);
                              }}
                            >
                              <Pencil className="mr-2 h-4 w-4" /> Edit
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            {emp.isActive ? (
                              <DropdownMenuItem
                                onClick={(e) => {
                                  e.stopPropagation();
                                  setPending({ type: 'deactivate', employee: emp });
                                }}
                              >
                                <UserX className="mr-2 h-4 w-4" /> Deactivate
                              </DropdownMenuItem>
                            ) : (
                              <DropdownMenuItem
                                onClick={(e) => {
                                  e.stopPropagation();
                                  setPending({ type: 'activate', employee: emp });
                                }}
                              >
                                <UserCheck className="mr-2 h-4 w-4" /> Activate
                              </DropdownMenuItem>
                            )}
                            <DropdownMenuItem
                              className="text-destructive focus:text-destructive"
                              onClick={(e) => {
                                e.stopPropagation();
                                setPending({ type: 'delete', employee: emp });
                              }}
                            >
                              <Trash2 className="mr-2 h-4 w-4" /> Delete
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>

          {data && data.totalPages > 1 && (
            <div className="flex items-center justify-end space-x-2 py-4">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={!data.hasPrevious}
              >
                Previous
              </Button>
              <div className="text-sm text-muted-foreground">
                Page {data.page} of {data.totalPages}
              </div>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => p + 1)}
                disabled={!data.hasNext}
              >
                Next
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={pending !== null}
        onOpenChange={(open) => !open && setPending(null)}
        title={pending ? dialogCopy[pending.type].title : ''}
        description={
          pending
            ? `${dialogCopy[pending.type].confirm} ${pending.employee.fullName}?`
            : ''
        }
        confirmText={pending ? dialogCopy[pending.type].confirm : 'Confirm'}
        variant={pending && dialogCopy[pending.type].destructive ? 'destructive' : 'default'}
        isLoading={busy}
        onConfirm={runAction}
      />
    </div>
  );
}
