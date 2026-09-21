'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Search, CalendarDays, PowerOff } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
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
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/hooks/use-toast';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import { useLeavePermissions } from '@/components/hr/leave/use-leave-permissions';
import type { LeaveType } from '@/types/hr/leave';

export default function LeaveTypesPage() {
  // Retiring a leave type is HR.Leave.Admin; the HR role holds Read and Write only (L-11).
  const { canAdminister } = useLeavePermissions();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [search, setSearch] = useState('');
  const [retiring, setRetiring] = useState<LeaveType | null>(null);

  /**
   * ⚠ Retiring is the ONLY way a leave type leaves the pickers: the controller has no delete
   * (405), deliberately, because a type is referenced by every request ever made against it. Until
   * this action existed a type added in error stayed in the picker for good.
   */
  const retire = useMutation({
    mutationFn: (id: string) => leaveTypeService.deactivate(id),
    onSuccess: () => {
      toast({ title: 'Retired', description: 'It stays on existing requests and leaves the pickers.' });
      setRetiring(null);
      queryClient.invalidateQueries({ queryKey: ['leave-types'] });
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'It could not be retired',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

  // Include inactive so the list is the full picture; status shows on each row.
  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'leave-types', 'all'],
    queryFn: () => leaveTypeService.getAll(false),
  });

  const leaveTypes = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = data ?? [];
    if (!term) return all;
    return all.filter(
      (t) => t.name.toLowerCase().includes(term) || t.code.toLowerCase().includes(term),
    );
  }, [data, search]);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Leave Types"
        description="Entitlement, carry-over and encashment rules for each kind of leave."
        actions={
          <Button onClick={() => router.push('/administration/hr/leave-types/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Leave Type
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Leave Types</CardTitle>
            <div className="relative w-64">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search leave types…"
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
                  <TableHead className="text-right">Default days</TableHead>
                  <TableHead className="text-right">Max days</TableHead>
                  <TableHead>Rules</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      <TableCell><Skeleton className="h-4 w-[170px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[60px]" /></TableCell>
                      <TableCell><Skeleton className="ml-auto h-4 w-[40px]" /></TableCell>
                      <TableCell><Skeleton className="ml-auto h-4 w-[40px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[160px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[70px]" /></TableCell>
                    </TableRow>
                  ))
                ) : leaveTypes.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <EmptyState
                        icon={CalendarDays}
                        title={search ? 'No matching leave types' : 'No leave types yet'}
                        description={
                          search
                            ? 'Try a different search.'
                            : 'Create the leave types employees can request.'
                        }
                        action={
                          !search ? (
                            <Button
                              size="sm"
                              onClick={() => router.push('/administration/hr/leave-types/new')}
                            >
                              <Plus className="mr-2 h-4 w-4" /> New Leave Type
                            </Button>
                          ) : undefined
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  leaveTypes.map((t) => (
                    <TableRow
                      key={t.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/administration/hr/leave-types/${t.id}`)}
                    >
                      <TableCell className="font-medium">
                        <span className="flex items-center gap-2">
                          {t.calendarColor && (
                            <span
                              className="inline-block h-2.5 w-2.5 rounded-full"
                              style={{ backgroundColor: t.calendarColor }}
                            />
                          )}
                          {t.name}
                        </span>
                      </TableCell>
                      <TableCell className="text-muted-foreground">{t.code}</TableCell>
                      <TableCell className="text-right">{t.defaultDaysPerYear}</TableCell>
                      <TableCell className="text-right">{t.maxDaysPerYear}</TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-1">
                          {!t.isPaid && <Badge variant="outline">Unpaid</Badge>}
                          {t.requiresApproval && <Badge variant="secondary">Approval</Badge>}
                          {t.allowCarryOver && <Badge variant="secondary">Carry-over</Badge>}
                          {t.allowCashConversion && <Badge variant="secondary">Encashable</Badge>}
                          {t.mandatoryAnnualLeave && <Badge variant="outline">Mandatory</Badge>}
                        </div>
                      </TableCell>
                      <TableCell>
                        <StatusBadge active={t.isActive} />
                      </TableCell>
                      <TableCell className="text-right">
                        {t.isActive && canAdminister && (
                          <Button
                            size="sm"
                            variant="ghost"
                            title="Retire this leave type"
                            onClick={(e) => {
                              // The row itself navigates; retiring must not.
                              e.stopPropagation();
                              setRetiring(t);
                            }}
                          >
                            <PowerOff className="h-4 w-4" />
                          </Button>
                        )}
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={Boolean(retiring)}
        onOpenChange={(o) => !o && setRetiring(null)}
        title={`Retire ${retiring?.name ?? 'this leave type'}?`}
        description="It stays on every request already made against it and disappears from the pickers. There is no delete: a leave type is referenced by its history."
        confirmText="Retire"
        onConfirm={() => { if (retiring) retire.mutate(retiring.id); }}
        isLoading={retire.isPending}
      />
    </div>
  );
}
