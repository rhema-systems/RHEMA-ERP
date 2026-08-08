'use client';

import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { apiService } from '@/services/api.service';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { employeeService } from '@/services/hr/employee.service';
import type {
  EmployeeDependent,
  EmployeeDependentBenefit,
} from '@/types/hr/employee-subresources';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { DateField, FieldRow, NumberField, SelectField, SwitchField } from './fields';

const schema = z.object({
  policyId: z.string().min(1, 'Select a benefit policy'),
  enrolledDate: z.string().min(1, 'Enrolled date is required'),
  coverageStartDate: z.string().optional().or(z.literal('')),
  coverageEndDate: z.string().optional().or(z.literal('')),
  benefitAmountUsed: z.coerce.number().min(0, 'Cannot be negative'),
  isActive: z.boolean(),
});

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  policyId: '',
  enrolledDate: new Date().toISOString().slice(0, 10),
  coverageStartDate: '',
  coverageEndDate: '',
  benefitAmountUsed: 0,
  isActive: true,
};

/** Minimal shape needed for the policy picker; the Benefits area owns the full type. */
interface BenefitPolicyOption {
  id: string;
  name: string;
}

/**
 * Benefit enrolments for a single dependent. This is a resource nested two levels deep
 * (employee → dependent → benefit), so it reuses EmployeeSubResourceTab with the
 * dependent id captured in the closures rather than getting its own tab.
 */
export function DependentBenefitsDialog({
  employeeId,
  dependent,
  open,
  onOpenChange,
}: {
  employeeId: string;
  dependent: EmployeeDependent | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const dependentId = dependent?.id ?? '';

  // Read-only lookup from the Benefits area (api/hr/benefit-policies), which has no UI yet.
  const { data: policies } = useQuery({
    queryKey: ['hr', 'benefit-policies', 'active'],
    queryFn: () => apiService.get<BenefitPolicyOption[]>('/hr/benefit-policies/active'),
    enabled: open,
  });

  const policyOptions = (policies ?? []).map((p) => ({ value: p.id, label: p.name }));

  const toPayload = (v: FormValues) => ({
    employeeDependentId: dependentId,
    policyId: v.policyId,
    enrolledDate: v.enrolledDate,
    coverageStartDate: v.coverageStartDate || null,
    coverageEndDate: v.coverageEndDate || null,
    benefitAmountUsed: v.benefitAmountUsed,
    isActive: v.isActive,
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[820px]">
        <DialogHeader>
          <DialogTitle>
            Benefits — {dependent ? `${dependent.firstName} ${dependent.lastName}` : ''}
          </DialogTitle>
          <DialogDescription>
            Benefit policies this dependent is enrolled in.
          </DialogDescription>
        </DialogHeader>

        {dependentId && (
          <EmployeeSubResourceTab<EmployeeDependentBenefit, FormValues>
            employeeId={employeeId}
            title="benefit enrolments"
            singular="benefit enrolment"
            queryKey={`dependents/${dependentId}/benefits`}
            getId={(b) => b.id}
            list={(empId) => employeeService.getDependentBenefits(empId, dependentId)}
            create={(empId, v) =>
              employeeService.addDependentBenefit(empId, dependentId, toPayload(v))
            }
            update={(empId, benefitId, v) =>
              employeeService.updateDependentBenefit(empId, dependentId, benefitId, {
                id: benefitId,
                coverageStartDate: v.coverageStartDate || null,
                coverageEndDate: v.coverageEndDate || null,
                benefitAmountUsed: v.benefitAmountUsed,
                isActive: v.isActive,
              })
            }
            remove={(empId, benefitId) =>
              employeeService.removeDependentBenefit(empId, dependentId, benefitId)
            }
            actions={[
              {
                label: (b) => (b.isActive ? 'Deactivate' : 'Activate'),
                run: (b) =>
                  b.isActive
                    ? employeeService.deactivateDependentBenefit(employeeId, dependentId, b.id)
                    : employeeService.activateDependentBenefit(employeeId, dependentId, b.id),
              },
            ]}
            columns={[
              { header: 'Policy', cell: (b) => b.policyName || '—' },
              { header: 'Enrolled', cell: (b) => b.enrolledDate?.slice(0, 10) || '—' },
              { header: 'Coverage from', cell: (b) => b.coverageStartDate?.slice(0, 10) || '—' },
              { header: 'Coverage to', cell: (b) => b.coverageEndDate?.slice(0, 10) || '—' },
              { header: 'Amount used', cell: (b) => b.benefitAmountUsed?.toLocaleString() ?? '—' },
              {
                header: 'Status',
                cell: (b) =>
                  b.isActive ? <Badge variant="secondary">Active</Badge> : <Badge variant="outline">Inactive</Badge>,
              },
            ]}
            schema={schema}
            emptyForm={empty}
            toForm={(b) => ({
              policyId: b.policyId,
              enrolledDate: b.enrolledDate?.slice(0, 10) ?? '',
              coverageStartDate: b.coverageStartDate?.slice(0, 10) ?? '',
              coverageEndDate: b.coverageEndDate?.slice(0, 10) ?? '',
              benefitAmountUsed: b.benefitAmountUsed,
              isActive: b.isActive,
            })}
            renderFields={(form) => (
              <>
                <SelectField
                  form={form}
                  name="policyId"
                  label="Benefit policy"
                  required
                  options={policyOptions}
                />
                <FieldRow>
                  <DateField form={form} name="enrolledDate" label="Enrolled date" required />
                  <NumberField
                    form={form}
                    name="benefitAmountUsed"
                    label="Amount used"
                    step="0.01"
                  />
                </FieldRow>
                <FieldRow>
                  <DateField form={form} name="coverageStartDate" label="Coverage from" />
                  <DateField form={form} name="coverageEndDate" label="Coverage to" />
                </FieldRow>
                <SwitchField form={form} name="isActive" label="Active" />
              </>
            )}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}
