'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { Loader2, UserPlus, ShieldOff, Users } from 'lucide-react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Button } from '@/components/ui/button';
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
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { TextareaField, DateField } from '@/components/hr/employee/tabs/fields';
import {
  ComplianceRequirementForm,
  toRequirementRequest,
  type ComplianceRequirementFormValues,
} from '@/components/hr/training/ComplianceRequirementForm';
import { trainingComplianceService } from '@/services/hr/training-compliance.service';
import {
  COMPLIANCE_FREQUENCY_OPTIONS,
  COMPLIANCE_STATUS_OPTIONS,
} from '@/types/hr/training-compliance';
import type { EmployeeComplianceRecordSummary } from '@/types/hr/training-compliance';

const freqLabel = (v: string) => COMPLIANCE_FREQUENCY_OPTIONS.find((o) => o.value === v)?.label ?? v;
const statusLabel = (v: string) => COMPLIANCE_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function ComplianceRequirementDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [saving, setSaving] = useState(false);
  const [assignOpen, setAssignOpen] = useState(false);
  const [exemptTarget, setExemptTarget] = useState<EmployeeComplianceRecordSummary | null>(null);
  const [busy, setBusy] = useState(false);

  const reqKey = ['hr', 'training', 'compliance', 'requirements', id];
  const recordsKey = ['hr', 'training', 'compliance', 'requirements', id, 'records'];

  const { data: requirement, isLoading, isError } = useQuery({
    queryKey: reqKey,
    queryFn: () => trainingComplianceService.getRequirementById(id),
    enabled: !!id,
  });

  const { data: records, isLoading: loadingRecords } = useQuery({
    queryKey: recordsKey,
    queryFn: () => trainingComplianceService.getRecordsForRequirement(id),
    enabled: !!id,
  });

  const assignForm = useForm<{ employeeId: string }>({ defaultValues: { employeeId: '' } });
  const exemptForm = useForm<{ exemptionReason: string; exemptionExpiryDate: string }>({
    defaultValues: { exemptionReason: '', exemptionExpiryDate: '' },
  });

  const invalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: reqKey }),
      queryClient.invalidateQueries({ queryKey: recordsKey }),
      queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'compliance', 'requirements'] }),
    ]);

  const handleSave = async (values: ComplianceRequirementFormValues) => {
    setSaving(true);
    try {
      await trainingComplianceService.updateRequirement(id, toRequirementRequest(values));
      await invalidate();
      toast({ title: 'Saved' });
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Failed to save.', variant: 'destructive' });
    } finally {
      setSaving(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !requirement) {
    return (
      <div className="p-6">
        <EmptyState title="Requirement not found" description="It may have been removed." />
      </div>
    );
  }

  const rows = records ?? [];
  const scope =
    [requirement.organizationLevelName, requirement.organizationUnitName, requirement.positionTitle]
      .filter(Boolean)
      .join(' · ') || 'Organisation-wide';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={requirement.requirementName}
        description={`${requirement.requirementCode} · satisfied by ${requirement.programName}`}
        backHref="/administration/hr/training/compliance"
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge active={requirement.isActive} />
            <Button size="sm" onClick={() => setAssignOpen(true)}>
              <UserPlus className="mr-2 h-4 w-4" /> Assign employee
            </Button>
          </div>
        }
      />

      <MetricTiles
        tiles={[
          { label: 'Assigned', value: requirement.totalAssignedEmployees, icon: Users },
          { label: 'Compliant', value: requirement.compliantEmployeesCount },
          {
            label: 'Compliance rate',
            value:
              requirement.totalAssignedEmployees > 0
                ? `${Math.round(requirement.complianceRate)}%`
                : '—',
            hint: requirement.totalAssignedEmployees === 0 ? 'Nobody assigned yet' : undefined,
            tone:
              requirement.totalAssignedEmployees > 0 && requirement.complianceRate < 60
                ? 'danger'
                : undefined,
          },
          { label: 'Frequency', value: freqLabel(requirement.frequency) },
        ]}
      />

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Configuration</TabsTrigger>
          <TabsTrigger value="people">Who it applies to ({rows.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="pt-4">
          <ComplianceRequirementForm
            defaultValues={{
              requirementCode: requirement.requirementCode,
              requirementName: requirement.requirementName,
              description: requirement.description ?? '',
              regulatoryReference: requirement.regulatoryReference ?? '',
              programId: requirement.programId,
              organizationLevelId: requirement.organizationLevelId ?? '',
              organizationUnitId: requirement.organizationUnitId ?? '',
              positionId: requirement.positionId ?? '',
              frequency: requirement.frequency,
              customFrequencyDays: requirement.customFrequencyDays?.toString() ?? '',
              gracePeriodDays: requirement.gracePeriodDays?.toString() ?? '',
              isActive: requirement.isActive,
              effectiveDate: requirement.effectiveDate.slice(0, 10),
              expiryDate: requirement.expiryDate?.slice(0, 10) ?? '',
              nonComplianceConsequences: requirement.nonComplianceConsequences ?? '',
            }}
            onSubmit={handleSave}
            submitting={saving}
            submitLabel="Save changes"
            onCancel={() => router.push('/administration/hr/training/compliance')}
          />
        </TabsContent>

        <TabsContent value="people" className="pt-4">
          <Card>
            <CardHeader>
              <CardTitle>Assigned employees</CardTitle>
              <CardDescription>
                Scope: {scope}. Assignment is explicit — narrowing the scope above does not add or
                remove anybody on its own.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Next due</TableHead>
                      <TableHead>Exemption</TableHead>
                      <TableHead className="w-[110px]"></TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {loadingRecords ? (
                      [...Array(3)].map((_, i) => (
                        <TableRow key={i}>
                          {[...Array(5)].map((__, j) => (
                            <TableCell key={j}>
                              <Skeleton className="h-4 w-[90px]" />
                            </TableCell>
                          ))}
                        </TableRow>
                      ))
                    ) : rows.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={5}>
                          <EmptyState
                            icon={Users}
                            title="Nobody assigned"
                            description="Assign the employees this requirement covers. Until then its compliance rate means nothing."
                          />
                        </TableCell>
                      </TableRow>
                    ) : (
                      rows.map((r) => (
                        <TableRow key={r.id}>
                          <TableCell className="font-medium">{r.employeeName}</TableCell>
                          <TableCell>
                            <div className="flex items-center gap-1.5">
                              <StatusBadge status={statusLabel(r.status)} />
                              {r.isOverdue && (
                                <Badge variant="destructive" className="text-[10px]">
                                  Overdue
                                </Badge>
                              )}
                            </div>
                          </TableCell>
                          <TableCell className="text-muted-foreground">{fmt(r.nextDueDate)}</TableCell>
                          <TableCell className="text-muted-foreground">
                            {r.isExempt ? (
                              <span className="text-sm">
                                Exempt
                                {r.exemptedByName && (
                                  <div className="text-xs">by {r.exemptedByName}</div>
                                )}
                              </span>
                            ) : (
                              '—'
                            )}
                          </TableCell>
                          <TableCell>
                            {!r.isExempt && (
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => {
                                  exemptForm.reset({ exemptionReason: '', exemptionExpiryDate: '' });
                                  setExemptTarget(r);
                                }}
                              >
                                <ShieldOff className="mr-2 h-4 w-4" /> Exempt
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
        </TabsContent>
      </Tabs>

      <Dialog open={assignOpen} onOpenChange={setAssignOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Assign this requirement</DialogTitle>
            <DialogDescription>
              They start non-compliant until the training is completed and recorded against them.
            </DialogDescription>
          </DialogHeader>
          <div className="py-2">
            <EmployeePickerField form={assignForm} name="employeeId" label="Employee" required />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAssignOpen(false)} disabled={busy}>
              Cancel
            </Button>
            <Button
              disabled={busy}
              onClick={async () => {
                const employeeId = assignForm.getValues('employeeId');
                if (!employeeId) {
                  toast({ title: 'Pick an employee', variant: 'destructive' });
                  return;
                }
                setBusy(true);
                try {
                  await trainingComplianceService.assign({ employeeId, requirementId: id });
                  await invalidate();
                  toast({ title: 'Assigned' });
                  assignForm.reset({ employeeId: '' });
                  setAssignOpen(false);
                } catch (error: any) {
                  toast({
                    title: 'Error',
                    description: error?.message || 'Failed to assign.',
                    variant: 'destructive',
                  });
                } finally {
                  setBusy(false);
                }
              }}
            >
              Assign
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={exemptTarget !== null} onOpenChange={(o) => !o && setExemptTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Exempt from this requirement</DialogTitle>
            <DialogDescription>
              {exemptTarget
                ? `${exemptTarget.employeeName} will stop counting against the compliance rate. You will be recorded as granting it.`
                : ''}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <TextareaField form={exemptForm} name="exemptionReason" label="Reason" rows={3} required />
            <DateField form={exemptForm} name="exemptionExpiryDate" label="Exemption expires" />
            <p className="-mt-2 text-xs text-muted-foreground">
              Leave the date blank for a permanent exemption — worth being deliberate about, since a
              requirement with a regulatory reference behind it rarely stops applying for good.
            </p>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setExemptTarget(null)} disabled={busy}>
              Cancel
            </Button>
            <Button
              disabled={busy}
              onClick={async () => {
                if (!exemptTarget) return;
                const reason = exemptForm.getValues('exemptionReason').trim();
                if (!reason) {
                  toast({ title: 'A reason is required', variant: 'destructive' });
                  return;
                }
                const expiry = exemptForm.getValues('exemptionExpiryDate');
                setBusy(true);
                try {
                  await trainingComplianceService.exempt(exemptTarget.id, {
                    exemptionReason: reason,
                    exemptionExpiryDate: expiry ? new Date(expiry).toISOString() : null,
                  });
                  await invalidate();
                  toast({ title: 'Exempted' });
                  setExemptTarget(null);
                } catch (error: any) {
                  toast({
                    title: 'Error',
                    description: error?.message || 'Failed to exempt.',
                    variant: 'destructive',
                  });
                } finally {
                  setBusy(false);
                }
              }}
            >
              Exempt
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
