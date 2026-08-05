'use client';

import { useMemo, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  EmployeePositionForm,
  type EmployeePositionFormValues,
} from '@/components/hr/position/EmployeePositionForm';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import { staffLevelService } from '@/services/hr/staff-level.service';
import { salaryGradeService } from '@/services/hr/salary-grade.service';
import { skillService } from '@/services/hr/skill.service';
import { benefitPolicyService } from '@/services/hr/benefits.service';

const toIntOrNull = (v: string) => (v && v.trim() ? Number(v) : null);
const toStr = (v: number | null | undefined) => (v === null || v === undefined ? '' : String(v));

export default function EditEmployeePositionPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: position, isLoading, isError } = useQuery({
    queryKey: ['hr', 'employee-positions', id],
    queryFn: () => employeePositionService.getById(id),
    enabled: !!id,
  });

  const { data: levels } = useQuery({
    queryKey: ['hr', 'organization-levels', 'all'],
    queryFn: () => organizationLevelService.getAll(),
  });

  const { data: units } = useQuery({
    queryKey: ['hr', 'organization-units', 'summary'],
    queryFn: () => organizationUnitService.getSummary(),
  });

  const { data: positions } = useQuery({
    queryKey: ['hr', 'employee-positions'],
    queryFn: () => employeePositionService.getAll(),
  });

  const { data: staffLevels } = useQuery({
    queryKey: ['hr', 'staff-levels', 'active'],
    queryFn: () => staffLevelService.getActive(),
  });

  // Defined in Payroll, mirrored into HR — read-only picker data. Includes inactive grades so an
  // existing assignment to a withdrawn grade still renders instead of silently blanking.
  const { data: salaryGrades } = useQuery({
    queryKey: ['hr', 'salary-grades', 'all'],
    queryFn: () => salaryGradeService.getAll(),
  });

  const { data: skills } = useQuery({
    queryKey: ['hr', 'skills', 'active'],
    queryFn: () => skillService.getActive(),
  });

  // Only active policies can be newly entitled. An existing entitlement to a since-deactivated
  // policy still round-trips through the form because the value is held in form state, not
  // resolved from this list.
  const { data: benefitPolicies } = useQuery({
    queryKey: ['hr', 'benefit-policies', 'active'],
    queryFn: () => benefitPolicyService.getActive(),
  });

  // A position can't report to itself.
  const reportsToOptions = useMemo(
    () => (positions ?? []).filter((p) => p.id !== id),
    [positions, id],
  );

  const handleSubmit = async (values: EmployeePositionFormValues) => {
    setSubmitting(true);
    try {
      await employeePositionService.update(id, {
        title: values.title,
        code: values.code ?? '',
        description: values.description || null,
        organizationLevelId: values.organizationLevelId,
        organizationUnitId: values.organizationUnitId,
        reportsToPositionId: values.reportsToPositionId || null,
        staffLevelId: values.staffLevelId || null,
        salaryGradeId: values.salaryGradeId || null,
        level: values.level,
        expectedHeadcount: values.expectedHeadcount,
        workMode: values.workMode,
        probationPeriodMonths: toIntOrNull(values.probationPeriodMonths ?? ''),
        noticePeriodMonths: toIntOrNull(values.noticePeriodMonths ?? ''),
        minimumExperienceYears: toIntOrNull(values.minimumExperienceYears ?? ''),
        minimumAge: toIntOrNull(values.minimumAge ?? ''),
        maximumAge: toIntOrNull(values.maximumAge ?? ''),
        requiresCertification: values.requiresCertification,
        requiresGuarantor: values.requiresGuarantor,
        requiresLicense: values.requiresLicense,
        isActive: values.isActive,
        skillRequirements: values.skillRequirements.map((r) => ({
          skillId: r.skillId,
          requiredLevel: r.requiredLevel,
          isRequired: r.isRequired,
          priority: r.priority,
        })),
        positionBenefits: values.positionBenefits.map((b) => ({
          policyId: b.policyId,
          // '' means "no expiry" / "use the policy's own valuation" — both must go as null,
          // not as an empty string the model binder would reject.
          expiryDate: b.expiryDate ? b.expiryDate : null,
          positionAmount: b.positionAmount ? Number(b.positionAmount) : null,
        })),
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'employee-positions'] });
      toast({ title: 'Success', description: 'Position updated.' });
      router.push('/administration/hr/positions');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update position.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="Edit Position"
        description={position ? position.title : 'Update this job position.'}
        backHref="/administration/hr/positions"
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !position ? (
        <EmptyState title="Position not found" description="This position may have been deleted." />
      ) : (
        <EmployeePositionForm
          levels={levels ?? []}
          units={units ?? []}
          positions={reportsToOptions}
          staffLevels={staffLevels ?? []}
          salaryGrades={salaryGrades ?? []}
          skills={skills ?? []}
          benefitPolicies={benefitPolicies ?? []}
          defaultValues={{
            title: position.title,
            code: position.code ?? '',
            description: position.description ?? '',
            organizationUnitId: position.organizationUnitId,
            organizationLevelId: position.organizationLevelId,
            reportsToPositionId: position.reportsToPositionId ?? '',
            staffLevelId: position.staffLevelId ?? '',
            salaryGradeId: position.salaryGradeId ?? '',
            level: position.level,
            expectedHeadcount: position.expectedHeadcount,
            workMode: position.workMode,
            probationPeriodMonths: toStr(position.probationPeriodMonths),
            noticePeriodMonths: toStr(position.noticePeriodMonths),
            minimumExperienceYears: toStr(position.minimumExperienceYears),
            minimumAge: toStr(position.minimumAge),
            maximumAge: toStr(position.maximumAge),
            requiresCertification: position.requiresCertification,
            requiresGuarantor: position.requiresGuarantor,
            requiresLicense: position.requiresLicense,
            isActive: position.isActive,
            skillRequirements: (position.skillRequirements ?? []).map((r) => ({
              skillId: r.skillId,
              requiredLevel: r.requiredLevel,
              isRequired: r.isRequired,
              priority: r.priority,
            })),
            // Loading these back is not cosmetic: the server syncs entitlements to whatever the
            // save sends, so an edit that started blank would delete every one of them.
            positionBenefits: (position.positionBenefits ?? []).map((b) => ({
              policyId: b.policyId,
              expiryDate: b.expiryDate ?? '',
              positionAmount: b.positionAmount === null || b.positionAmount === undefined
                ? ''
                : String(b.positionAmount),
            })),
          }}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push('/administration/hr/positions')}
        />
      )}
    </div>
  );
}
