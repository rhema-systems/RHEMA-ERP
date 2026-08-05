'use client';

import { useMemo, useState } from 'react';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Timer } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  NumberField,
  DateField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { positionOvertimePolicyService } from '@/services/hr/attendance-setup.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { OVERTIME_ALLOWANCE_TYPE_OPTIONS } from '@/types/hr/attendance';
import type {
  PositionOvertimePolicy,
  EmployeeOvertimeOverride,
} from '@/types/hr/attendance';

/**
 * Overtime eligibility is set per position, then adjusted per employee where someone's
 * circumstances differ from their position's rule.
 *
 * The list endpoint is keyed by position, so a position must be chosen first — there is no
 * tenant-wide "all policies" read on the controller.
 */
const policySchema = z
  .object({
    allowanceType: z.enum([
      'Overtime',
      'NightAllowance',
      'ShiftDifferential',
      'WeekendAllowance',
      'HolidayAllowance',
      'TransportAllowance',
      'Other',
    ]),
    isEligible: z.boolean(),
    isExempt: z.boolean(),
    exemptionReason: z.string().max(1000).optional(),
    maxHoursPerDay: z.coerce.number().min(0).max(24).optional(),
    maxHoursPerWeek: z.coerce.number().min(0).max(168).optional(),
    requiresPreApproval: z.boolean(),
    effectiveDate: z.string().min(1, 'Required'),
    expiryDate: z.string().optional(),
    notes: z.string().max(1000).optional(),
  })
  .refine((v) => !v.isExempt || !!v.exemptionReason?.trim(), {
    message: 'An exemption needs a reason',
    path: ['exemptionReason'],
  })
  .refine((v) => !v.expiryDate || v.expiryDate >= v.effectiveDate, {
    message: 'Expiry cannot precede the effective date',
    path: ['expiryDate'],
  });

type PolicyForm = z.input<typeof policySchema>;

const emptyPolicy: PolicyForm = {
  allowanceType: 'Overtime',
  isEligible: true,
  isExempt: false,
  exemptionReason: '',
  maxHoursPerDay: undefined,
  maxHoursPerWeek: undefined,
  requiresPreApproval: true,
  effectiveDate: '',
  expiryDate: '',
  notes: '',
};

const overrideSchema = z
  .object({
    employeeId: z.string().min(1, 'Select an employee'),
    allowanceType: z.enum([
      'Overtime',
      'NightAllowance',
      'ShiftDifferential',
      'WeekendAllowance',
      'HolidayAllowance',
      'TransportAllowance',
      'Other',
    ]),
    isEligible: z.boolean(),
    isExempt: z.boolean(),
    overrideReason: z.string().min(1, 'A reason is required').max(1000),
    effectiveDate: z.string().min(1, 'Required'),
    expiryDate: z.string().optional(),
  })
  .refine((v) => !v.expiryDate || v.expiryDate >= v.effectiveDate, {
    message: 'Expiry cannot precede the effective date',
    path: ['expiryDate'],
  });

type OverrideForm = z.input<typeof overrideSchema>;

const emptyOverride: OverrideForm = {
  employeeId: '',
  allowanceType: 'Overtime',
  isEligible: true,
  isExempt: false,
  overrideReason: '',
  effectiveDate: '',
  expiryDate: '',
};

/** The server takes the approving user from the token; this satisfies the required field. */
const ACTOR_FROM_TOKEN = '00000000-0000-0000-0000-000000000000';

export default function OvertimePoliciesPage() {
  const [positionId, setPositionId] = useState<string>('');
  const [expandedPolicyId, setExpandedPolicyId] = useState<string | null>(null);

  const { data: positions } = useQuery({
    queryKey: ['hr', 'positions', 'all'],
    queryFn: () => employeePositionService.getAll(),
  });

  const positionOptions = useMemo(
    () => (positions ?? []).map((p) => ({ value: p.id, label: p.title })),
    [positions],
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Overtime Policies"
        description="Which positions may claim overtime and allowances, and the per-employee exceptions."
        backHref="/administration/hr/attendance"
      />

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Position</CardTitle>
        </CardHeader>
        <CardContent>
          <Select value={positionId} onValueChange={setPositionId}>
            <SelectTrigger className="max-w-md">
              <SelectValue placeholder="Select a position…" />
            </SelectTrigger>
            <SelectContent>
              {positionOptions.map((o) => (
                <SelectItem key={o.value} value={o.value}>
                  {o.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </CardContent>
      </Card>

      {!positionId ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={Timer}
              title="Choose a position"
              description="Overtime policies are held per position."
            />
          </CardContent>
        </Card>
      ) : (
        <>
          <ResourceCollectionTab<PositionOvertimePolicy, PolicyForm>
            parentId={positionId}
            title="policies"
            singular="policy"
            queryKey={['hr', 'overtime-policies', positionId]}
            dialogHint="One policy per allowance type. Supersede an old rule with a new effective date rather than editing history."
            emptyDescription="Without a policy, this position falls back to its work schedule's overtime settings."
            list={(pid) => positionOvertimePolicyService.getByPosition(pid)}
            create={(pid, values) => {
              const v = policySchema.parse(values);
              return positionOvertimePolicyService.create({
                ...v,
                positionId: pid,
                exemptionReason: v.exemptionReason || null,
                maxHoursPerDay: v.maxHoursPerDay ?? null,
                maxHoursPerWeek: v.maxHoursPerWeek ?? null,
                expiryDate: v.expiryDate || null,
                notes: v.notes || null,
              });
            }}
            update={(_pid, policyId, values) => {
              const v = policySchema.parse(values);
              // Position and allowance type are immutable; create a new policy to change them.
              return positionOvertimePolicyService.update(policyId, {
                id: policyId,
                isEligible: v.isEligible,
                isExempt: v.isExempt,
                exemptionReason: v.exemptionReason || null,
                maxHoursPerDay: v.maxHoursPerDay ?? null,
                maxHoursPerWeek: v.maxHoursPerWeek ?? null,
                requiresPreApproval: v.requiresPreApproval,
                effectiveDate: v.effectiveDate,
                expiryDate: v.expiryDate || null,
                notes: v.notes || null,
              });
            }}
            remove={(_pid, policyId) => positionOvertimePolicyService.remove(policyId)}
            getId={(p) => p.id}
            actions={[
              {
                label: (p) => (expandedPolicyId === p.id ? 'Hide overrides' : 'Show overrides'),
                run: async (p) => {
                  setExpandedPolicyId((current) => (current === p.id ? null : p.id));
                },
              },
            ]}
            columns={[
              {
                header: 'Allowance',
                cell: (p) => <span className="font-medium">{humanizeEnum(p.allowanceType)}</span>,
              },
              {
                header: 'Eligibility',
                cell: (p) =>
                  p.isExempt ? (
                    <Badge variant="destructive">Exempt</Badge>
                  ) : p.isEligible ? (
                    <Badge>Eligible</Badge>
                  ) : (
                    <Badge variant="secondary">Not eligible</Badge>
                  ),
              },
              {
                header: 'Max / day',
                cell: (p) => p.maxHoursPerDay ?? '—',
                className: 'text-right',
              },
              {
                header: 'Max / week',
                cell: (p) => p.maxHoursPerWeek ?? '—',
                className: 'text-right',
              },
              { header: 'Pre-approval', cell: (p) => (p.requiresPreApproval ? 'Yes' : 'No') },
              { header: 'Effective', cell: (p) => formatDate(p.effectiveDate) },
              { header: 'Expires', cell: (p) => formatDate(p.expiryDate) },
            ]}
            schema={policySchema as any}
            emptyForm={emptyPolicy}
            toForm={(p) => ({
              allowanceType: p.allowanceType,
              isEligible: p.isEligible,
              isExempt: p.isExempt,
              exemptionReason: p.exemptionReason ?? '',
              maxHoursPerDay: p.maxHoursPerDay ?? undefined,
              maxHoursPerWeek: p.maxHoursPerWeek ?? undefined,
              requiresPreApproval: p.requiresPreApproval,
              effectiveDate: p.effectiveDate,
              expiryDate: p.expiryDate ?? '',
              notes: p.notes ?? '',
            })}
            renderFields={(form) => (
              <>
                <SelectField
                  form={form}
                  name="allowanceType"
                  label="Allowance type"
                  required
                  options={OVERTIME_ALLOWANCE_TYPE_OPTIONS}
                />
                <SwitchField form={form} name="isEligible" label="Eligible" />
                <SwitchField
                  form={form}
                  name="isExempt"
                  label="Exempt"
                  description="Excluded from this allowance regardless of hours worked."
                />
                {!!form.watch('isExempt') && (
                  <TextareaField
                    form={form}
                    name="exemptionReason"
                    label="Exemption reason"
                    rows={2}
                  />
                )}
                <FieldRow>
                  <NumberField form={form} name="maxHoursPerDay" label="Max hours / day" step="0.25" />
                  <NumberField form={form} name="maxHoursPerWeek" label="Max hours / week" step="0.25" />
                </FieldRow>
                <SwitchField
                  form={form}
                  name="requiresPreApproval"
                  label="Requires pre-approval"
                />
                <FieldRow>
                  <DateField form={form} name="effectiveDate" label="Effective from" required />
                  <DateField form={form} name="expiryDate" label="Expires" />
                </FieldRow>
                <TextareaField form={form} name="notes" label="Notes" rows={2} />
              </>
            )}
          />

          {expandedPolicyId && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Employee overrides</CardTitle>
              </CardHeader>
              <CardContent>
                <ResourceCollectionTab<EmployeeOvertimeOverride, OverrideForm>
                  parentId={expandedPolicyId}
                  title="overrides"
                  singular="override"
                  queryKey={['hr', 'overtime-policies', 'overrides', expandedPolicyId]}
                  dialogHint="Overrides take precedence over the position policy for one employee."
                  emptyDescription="Everyone in this position follows the policy above."
                  list={(policyId) => positionOvertimePolicyService.getOverrides(policyId)}
                  create={(policyId, values) => {
                    const v = overrideSchema.parse(values);
                    return positionOvertimePolicyService.addOverride(policyId, {
                      ...v,
                      policyId,
                      approvedById: ACTOR_FROM_TOKEN,
                      expiryDate: v.expiryDate || null,
                    });
                  }}
                  update={(_policyId, overrideId, values) => {
                    const v = overrideSchema.parse(values);
                    return positionOvertimePolicyService.updateOverride(overrideId, {
                      id: overrideId,
                      isEligible: v.isEligible,
                      isExempt: v.isExempt,
                      overrideReason: v.overrideReason,
                      effectiveDate: v.effectiveDate,
                      expiryDate: v.expiryDate || null,
                    });
                  }}
                  remove={(_policyId, overrideId) =>
                    positionOvertimePolicyService.removeOverride(overrideId)
                  }
                  getId={(o) => o.id}
                  columns={[
                    {
                      header: 'Employee',
                      cell: (o) => <span className="font-medium">{o.employeeName}</span>,
                    },
                    { header: 'Allowance', cell: (o) => humanizeEnum(o.allowanceType) },
                    {
                      header: 'Eligibility',
                      cell: (o) =>
                        o.isExempt ? (
                          <Badge variant="destructive">Exempt</Badge>
                        ) : o.isEligible ? (
                          <Badge>Eligible</Badge>
                        ) : (
                          <Badge variant="secondary">Not eligible</Badge>
                        ),
                    },
                    { header: 'Reason', cell: (o) => o.overrideReason },
                    { header: 'Effective', cell: (o) => formatDate(o.effectiveDate) },
                    { header: 'Expires', cell: (o) => formatDate(o.expiryDate) },
                  ]}
                  schema={overrideSchema as any}
                  emptyForm={emptyOverride}
                  toForm={(o) => ({
                    employeeId: o.employeeId,
                    allowanceType: o.allowanceType,
                    isEligible: o.isEligible,
                    isExempt: o.isExempt,
                    overrideReason: o.overrideReason,
                    effectiveDate: o.effectiveDate,
                    expiryDate: o.expiryDate ?? '',
                  })}
                  renderFields={(form) => (
                    <>
                      <EmployeePickerField
                        form={form}
                        name="employeeId"
                        label="Employee"
                        required
                      />
                      <SelectField
                        form={form}
                        name="allowanceType"
                        label="Allowance type"
                        required
                        options={OVERTIME_ALLOWANCE_TYPE_OPTIONS}
                      />
                      <SwitchField form={form} name="isEligible" label="Eligible" />
                      <SwitchField form={form} name="isExempt" label="Exempt" />
                      <TextareaField
                        form={form}
                        name="overrideReason"
                        label="Override reason"
                        rows={2}
                      />
                      <FieldRow>
                        <DateField form={form} name="effectiveDate" label="Effective from" required />
                        <DateField form={form} name="expiryDate" label="Expires" />
                      </FieldRow>
                    </>
                  )}
                />
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  );
}
