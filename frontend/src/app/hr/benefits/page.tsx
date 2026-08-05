'use client';

import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { ShieldPlus, Loader2, RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { EnrollmentCoverageDialog } from '@/components/hr/benefits/EnrollmentCoverageDialog';
import {
  NumberField,
  DateField,
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import {
  benefitPolicyService,
  employeeBenefitEnrollmentService,
} from '@/services/hr/benefits.service';
import { formatDate, formatMoney, humanizeEnum, today } from '@/lib/hr/attendance-format';
import { ENROLLMENT_STATUS_OPTIONS } from '@/types/hr/benefits';
import type {
  EmployeeBenefitEnrollmentListItem,
  EnrollmentStatus,
} from '@/types/hr/benefits';

/**
 * An employee's benefit enrolments, their coverage balance and their claims.
 *
 * Enrolments arrive two ways: automatically from the employee's position or grade, or created by
 * hand. "Reconcile" re-derives the automatic ones — creating what the employee now qualifies for
 * and retiring what they no longer do — and deliberately leaves manual enrolments alone, so it is
 * safe to run repeatedly.
 */
const enrollmentSchema = z
  .object({
    benefitPolicyId: z.string().min(1, 'Select a policy'),
    effectiveFrom: z.string().min(1, 'Required'),
    effectiveTo: z.string().optional(),
    assessedValueOverride: z.coerce.number().min(0).optional(),
    notes: z.string().max(1000).optional(),
  })
  .refine((v) => !v.effectiveTo || v.effectiveTo >= v.effectiveFrom, {
    message: 'The effective-to date cannot be before the effective-from date',
    path: ['effectiveTo'],
  });

type EnrollmentForm = z.input<typeof enrollmentSchema>;

const toDateInput = (v?: string | null) => (v ? v.slice(0, 10) : '');

const STATUS_FLOW: { value: EnrollmentStatus; label: string }[] = ENROLLMENT_STATUS_OPTIONS;

export default function EmployeeBenefitsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [employeeLabel, setEmployeeLabel] = useState<string | null>(null);
  const [reconciling, setReconciling] = useState(false);
  const [statusTarget, setStatusTarget] = useState<EmployeeBenefitEnrollmentListItem | null>(null);
  const [coverageTarget, setCoverageTarget] = useState<EmployeeBenefitEnrollmentListItem | null>(
    null,
  );
  const [nextStatus, setNextStatus] = useState<EnrollmentStatus>('Active');
  const [statusReason, setStatusReason] = useState('');
  const [busy, setBusy] = useState(false);

  const { data: policies } = useQuery({
    queryKey: ['hr', 'benefit-policies', 'active'],
    queryFn: () => benefitPolicyService.getActive(),
  });

  const policyOptions = useMemo(
    () =>
      (policies ?? []).map((p) => ({
        value: p.id,
        label: `${p.policyName}${p.policyCode ? ` (${p.policyCode})` : ''}`,
      })),
    [policies],
  );

  const emptyEnrollment: EnrollmentForm = {
    benefitPolicyId: '',
    effectiveFrom: today(),
    effectiveTo: '',
    assessedValueOverride: undefined,
    notes: '',
  };

  const runReconcile = async () => {
    if (!employeeId) return;
    setReconciling(true);
    try {
      await employeeBenefitEnrollmentService.reconcile(employeeId);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'benefit-enrollments', employeeId] });
      toast({
        title: 'Reconciled',
        description: 'Automatic enrolments were re-derived from the employee’s position and grade.',
      });
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to reconcile enrolments.',
        variant: 'destructive',
      });
    } finally {
      setReconciling(false);
    }
  };

  const applyStatus = async () => {
    if (!statusTarget) return false;
    setBusy(true);
    try {
      await employeeBenefitEnrollmentService.changeStatus(statusTarget.id, {
        status: nextStatus,
        reason: statusReason.trim() || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'benefit-enrollments', employeeId] });
      toast({ title: 'Updated', description: `Enrolment is now ${humanizeEnum(nextStatus)}.` });
      setStatusTarget(null);
      setStatusReason('');
      return true;
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to change the status.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Employee Benefits"
        description="Benefit enrolments, coverage balances and claims."
        backHref="/hr"
        actions={
          employeeId ? (
            <Button variant="outline" onClick={runReconcile} disabled={reconciling}>
              {reconciling ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <RefreshCw className="mr-2 h-4 w-4" />
              )}
              Reconcile automatic enrolments
            </Button>
          ) : undefined
        }
      />

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Employee</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="max-w-md">
            <EmployeePicker
              value={employeeId}
              initialLabel={employeeLabel}
              onChange={(id, label) => {
                setEmployeeId(id);
                setEmployeeLabel(label);
              }}
            />
          </div>
        </CardContent>
      </Card>

      {!employeeId ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={ShieldPlus}
              title="Choose an employee"
              description="Benefit enrolments are held per employee."
            />
          </CardContent>
        </Card>
      ) : (
        <ResourceCollectionTab<EmployeeBenefitEnrollmentListItem, EnrollmentForm>
          parentId={employeeId}
          title="enrolments"
          singular="enrolment"
          queryKey={['hr', 'benefit-enrollments', employeeId]}
          dialogHint="Leave the assessed value blank to take the policy's own valuation."
          emptyDescription="This employee is not enrolled in any benefit. Reconcile to pick up automatic ones."
          list={(id) => employeeBenefitEnrollmentService.getByEmployee(id)}
          create={(id, values) => {
            const v = enrollmentSchema.parse(values);
            return employeeBenefitEnrollmentService.create({
              employeeId: id,
              benefitPolicyId: v.benefitPolicyId,
              effectiveFrom: v.effectiveFrom,
              effectiveTo: v.effectiveTo || null,
              assessedValueOverride: v.assessedValueOverride ?? null,
              notes: v.notes || null,
              // Dependants and beneficiaries are managed on the enrolment after it exists;
              // asking for them up front would make a simple enrolment a long form.
              dependents: [],
              beneficiaries: [],
            });
          }}
          update={(_id, rowId, values) => {
            const v = enrollmentSchema.parse(values);
            // The policy cannot be changed after enrolment — terminate and enrol again.
            return employeeBenefitEnrollmentService.update(rowId, {
              effectiveFrom: v.effectiveFrom,
              effectiveTo: v.effectiveTo || null,
              assessedValueOverride: v.assessedValueOverride ?? null,
              notes: v.notes || null,
            });
          }}
          getId={(r) => r.id}
          actions={[
            {
              // Dependants and beneficiaries need the enrolment to exist first, so they are
              // managed here rather than being folded into the create form.
              label: 'Cover and nominations',
              run: async (r) => setCoverageTarget(r),
            },
            {
              label: 'Change status',
              run: async (r) => {
                setStatusTarget(r);
                setNextStatus(r.status === 'Draft' ? 'Active' : r.status);
                setStatusReason('');
              },
            },
          ]}
          columns={[
            {
              header: 'Policy',
              cell: (r) => <span className="font-medium">{r.benefitPolicyName}</span>,
            },
            {
              header: 'Source',
              cell: (r) => (
                // Automatic enrolments are re-derived by reconcile; manual ones are not, so the
                // distinction decides whether an edit here survives.
                <Badge variant={r.source === 'Manual' ? 'default' : 'outline'}>
                  {humanizeEnum(r.source)}
                </Badge>
              ),
            },
            {
              header: 'Assessed',
              cell: (r) => formatMoney(r.assessedValue, r.currency),
              className: 'text-right',
            },
            {
              header: 'Taxable',
              cell: (r) => formatMoney(r.taxableValue, r.currency),
              className: 'text-right',
            },
            {
              header: 'Used / limit',
              cell: (r) => (
                <span
                  className={
                    r.remainingAmount <= 0 && r.coverageLimit > 0 ? 'text-red-600' : undefined
                  }
                >
                  {formatMoney(r.utilizedAmount, r.currency)} /{' '}
                  {formatMoney(r.coverageLimit, r.currency)}
                </span>
              ),
              className: 'text-right',
            },
            { header: 'From', cell: (r) => formatDate(r.effectiveFrom) },
            { header: 'Status', cell: (r) => <StatusBadge status={r.status} /> },
          ]}
          schema={enrollmentSchema as any}
          emptyForm={emptyEnrollment}
          toForm={(r) => ({
            benefitPolicyId: r.benefitPolicyId,
            effectiveFrom: toDateInput(r.effectiveFrom),
            effectiveTo: toDateInput(r.effectiveTo),
            assessedValueOverride: undefined,
            notes: '',
          })}
          renderFields={(form) => (
            <>
              <SelectField
                form={form}
                name="benefitPolicyId"
                label="Benefit policy"
                required
                options={policyOptions}
                placeholder={policyOptions.length ? 'Select a policy…' : 'No active policies'}
              />
              <FieldRow>
                <DateField form={form} name="effectiveFrom" label="Effective from" required />
                <DateField form={form} name="effectiveTo" label="Effective to" />
              </FieldRow>
              <NumberField
                form={form}
                name="assessedValueOverride"
                label="Assessed value override"
                step="0.01"
                placeholder="Blank to use the policy's valuation"
              />
              <TextareaField form={form} name="notes" label="Notes" rows={2} />
            </>
          )}
        />
      )}

      <EnrollmentCoverageDialog
        enrollment={coverageTarget}
        onOpenChange={(open) => {
          if (!open) setCoverageTarget(null);
        }}
      />

      <ConfirmationDialog
        open={statusTarget !== null}
        onOpenChange={(open) => !open && setStatusTarget(null)}
        title="Change enrolment status"
        description={
          statusTarget
            ? `${statusTarget.benefitPolicyName} — currently ${humanizeEnum(statusTarget.status)}.`
            : ''
        }
        confirmText="Apply"
        isLoading={busy}
        onConfirm={applyStatus}
      >
        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="nextStatus">New status</Label>
            <Select
              value={nextStatus}
              onValueChange={(v) => setNextStatus(v as EnrollmentStatus)}
            >
              <SelectTrigger id="nextStatus">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {STATUS_FLOW.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="statusReason">Reason</Label>
            <Textarea
              id="statusReason"
              rows={3}
              value={statusReason}
              onChange={(e) => setStatusReason(e.target.value)}
              placeholder="Recorded against the enrolment — required for a termination."
            />
          </div>
        </div>
      </ConfirmationDialog>
    </div>
  );
}
