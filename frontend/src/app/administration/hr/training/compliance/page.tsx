'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Search, MoreHorizontal, Eye, Trash2, ShieldAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { trainingComplianceService } from '@/services/hr/training-compliance.service';
import { COMPLIANCE_FREQUENCY_OPTIONS } from '@/types/hr/training-compliance';
import type { ComplianceTrainingRequirementSummary } from '@/types/hr/training-compliance';

const frequencyLabel = (v: string) =>
  COMPLIANCE_FREQUENCY_OPTIONS.find((o) => o.value === v)?.label ?? v;

/** A rate is only meaningful against a population — 0 assigned is "not started", not "0% compliant". */
function ComplianceRate({ rate, assigned }: { rate: number; assigned: number }) {
  if (assigned === 0) {
    return <span className="text-muted-foreground">Nobody assigned</span>;
  }
  const pct = Math.round(rate);
  const tone = pct >= 90 ? 'text-green-600' : pct >= 60 ? 'text-amber-600' : 'text-destructive';
  return (
    <span className={`font-medium ${tone}`}>
      {pct}%
      <span className="ml-1 text-xs font-normal text-muted-foreground">of {assigned}</span>
    </span>
  );
}

export default function ComplianceRequirementsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [search, setSearch] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<ComplianceTrainingRequirementSummary | null>(null);
  const [busy, setBusy] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'training', 'compliance', 'requirements'],
    queryFn: () => trainingComplianceService.getRequirements(),
  });

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = data ?? [];
    if (!term) return all;
    return all.filter(
      (r) =>
        r.requirementCode.toLowerCase().includes(term) ||
        r.requirementName.toLowerCase().includes(term) ||
        r.programName.toLowerCase().includes(term),
    );
  }, [data, search]);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Compliance Requirements"
        description="Training that specific populations must hold, and how often it has to be renewed."
        backHref="/administration/hr/training"
        actions={
          <Button onClick={() => router.push('/administration/hr/training/compliance/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Requirement
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <CardTitle>Requirements</CardTitle>
              <CardDescription>
                Configuration only — the live position per employee is on the compliance dashboard.
              </CardDescription>
            </div>
            <div className="relative w-64">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search requirements…"
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
                  <TableHead>Code</TableHead>
                  <TableHead>Requirement</TableHead>
                  <TableHead>Satisfied by</TableHead>
                  <TableHead>Frequency</TableHead>
                  <TableHead>Effective</TableHead>
                  <TableHead>Compliance</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(4)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(8)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[80px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={8}>
                      <EmptyState
                        icon={ShieldAlert}
                        title={search ? 'No matching requirements' : 'No compliance requirements'}
                        description={
                          search
                            ? 'Try a different search.'
                            : 'Define the training a population must hold — a safety induction, a licence renewal.'
                        }
                        action={
                          !search ? (
                            <Button
                              size="sm"
                              onClick={() => router.push('/administration/hr/training/compliance/new')}
                            >
                              <Plus className="mr-2 h-4 w-4" /> New Requirement
                            </Button>
                          ) : undefined
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((r) => (
                    <TableRow
                      key={r.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/administration/hr/training/compliance/${r.id}`)}
                    >
                      <TableCell className="font-mono text-xs">{r.requirementCode}</TableCell>
                      <TableCell className="font-medium">{r.requirementName}</TableCell>
                      <TableCell className="text-muted-foreground">{r.programName}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {frequencyLabel(r.frequency)}
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {new Date(r.effectiveDate).toLocaleDateString()}
                      </TableCell>
                      <TableCell>
                        <ComplianceRate rate={r.complianceRate} assigned={r.totalAssignedEmployees} />
                      </TableCell>
                      <TableCell>
                        <StatusBadge active={r.isActive} />
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
                                router.push(`/administration/hr/training/compliance/${r.id}`);
                              }}
                            >
                              <Eye className="mr-2 h-4 w-4" /> View details
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              className="text-destructive focus:text-destructive"
                              onClick={(e) => {
                                e.stopPropagation();
                                setDeleteTarget(r);
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
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={deleteTarget !== null}
        onOpenChange={(o) => !o && setDeleteTarget(null)}
        title="Delete requirement"
        description={
          deleteTarget
            ? `Delete "${deleteTarget.requirementName}"? ${
                deleteTarget.totalAssignedEmployees > 0
                  ? `${deleteTarget.totalAssignedEmployees} employee record(s) hang off it.`
                  : ''
              }`
            : ''
        }
        confirmText="Delete"
        variant="destructive"
        isLoading={busy}
        onConfirm={async () => {
          if (!deleteTarget) return false;
          setBusy(true);
          try {
            await trainingComplianceService.removeRequirement(deleteTarget.id);
            await queryClient.invalidateQueries({
              queryKey: ['hr', 'training', 'compliance'],
            });
            toast({ title: 'Deleted' });
            setDeleteTarget(null);
            return true;
          } catch (error: any) {
            toast({
              title: 'Error',
              description: error?.message || 'Failed to delete.',
              variant: 'destructive',
            });
            return false;
          } finally {
            setBusy(false);
          }
        }}
      />
    </div>
  );
}
