'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  EmployeePositionForm,
  emptyEmployeePosition,
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

export default function NewEmployeePositionPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: levels, isLoading: levelsLoading } = useQuery({
    queryKey: ['hr', 'organization-levels', 'all'],
    queryFn: () => organizationLevelService.getAll(),
  });

  const { data: units, isLoading: unitsLoading } = useQuery({
    queryKey: ['hr', 'organization-units', 'summary'],
    queryFn: () => organizationUnitService.getSummary(),
  });

  const { data: positions, isLoading: positionsLoading } = useQuery({
    queryKey: ['hr', 'employee-positions'],
    queryFn: () => employeePositionService.getAll(),
  });

  const { data: staffLevels } = useQuery({
    queryKey: ['hr', 'staff-levels', 'active'],
    queryFn: () => staffLevelService.getActive(),
  });

  // Defined in Payroll, mirrored into HR — read-only picker data.
  const { data: salaryGrades } = useQuery({
    queryKey: ['hr', 'salary-grades', 'active'],
    queryFn: () => salaryGradeService.getActive(),
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

  const handleSubmit = async (values: EmployeePositionFormValues) => {
    setSubmitting(true);
    try {
      await employeePositionService.create({
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
      toast({ title: 'Success', description: 'Position created.' });
      router.push('/administration/hr/positions');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create position.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="New Position"
        description="Add a job position to an organization unit."
        backHref="/administration/hr/positions"
      />

      {levelsLoading || unitsLoading || positionsLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : (
        <EmployeePositionForm
          levels={levels ?? []}
          units={units ?? []}
          positions={positions ?? []}
          staffLevels={staffLevels ?? []}
          salaryGrades={salaryGrades ?? []}
          skills={skills ?? []}
          benefitPolicies={benefitPolicies ?? []}
          defaultValues={emptyEmployeePosition}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Create Position"
          onCancel={() => router.push('/administration/hr/positions')}
        />
      )}
    </div>
  );
}
