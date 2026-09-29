'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { hrCurrencyService } from '@/services/hr/hr-currency.service';
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
import { staffLevelService } from '@/services/hr/staff-level.service';
import { salaryGradeService } from '@/services/hr/salary-grade.service';
import { skillService } from '@/services/hr/skill.service';
import { benefitPolicyService } from '@/services/hr/benefits.service';
import { namedSetService } from '@/services/hr/named-set.service';

const toIntOrNull = (v: string) => (v && v.trim() ? Number(v) : null);

export default function NewEmployeePositionPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

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


  // Round 2, lane C3 — the named sets on offer. Their members come with them so the form can grey
  // out an item an attached set already provides, rather than letting the user compose a save the
  // server will refuse.
  const { data: benefitGroups } = useQuery({
    queryKey: ['hr', 'benefit-groups'],
    queryFn: () => namedSetService.getBenefitGroups(),
  });
  const { data: skillSets } = useQuery({
    queryKey: ['hr', 'skill-sets'],
    queryFn: () => namedSetService.getSkillSets(),
  });
  const { data: certificationSets } = useQuery({
    queryKey: ['hr', 'certification-sets'],
    queryFn: () => namedSetService.getCertificationSets(),
  });

  const benefitGroupOptions = useMemo(
    () =>
      (benefitGroups ?? []).map((g) => ({
        id: g.id,
        name: g.name,
        code: g.code,
        isActive: g.isActive,
        memberCount: g.memberCount,
        memberIds: (g.members ?? []).map((m) => m.policyId),
      })),
    [benefitGroups],
  );
  const skillSetOptions = useMemo(
    () =>
      (skillSets ?? []).map((s) => ({
        id: s.id,
        name: s.name,
        code: s.code,
        isActive: s.isActive,
        memberCount: s.memberCount,
        memberIds: (s.members ?? []).map((m) => m.skillId),
      })),
    [skillSets],
  );
  const certificationSetOptions = useMemo(
    () =>
      (certificationSets ?? []).map((s) => ({
        id: s.id,
        name: s.name,
        code: s.code,
        isActive: s.isActive,
        memberCount: s.memberCount,
        memberIds: (s.members ?? []).map((m) => m.certificationId),
      })),
    [certificationSets],
  );

  // Finance owns the currency list; the server refuses a code it does not hold, so a free-text
  // box would be offering a way to fail.
  const { data: currencies } = useQuery({
    queryKey: ['finance', 'currencies'],
    queryFn: () => hrCurrencyService.getActive(),
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
        // Blank means "no set amount", which the compliance read treats as "a guarantor, any sum".
        requiredGuarantorAmount: values.requiredGuarantorAmount
          ? Number(values.requiredGuarantorAmount)
          : null,
        requiredGuarantorCurrencyCode: values.requiredGuarantorCurrencyCode || null,
        requiresLicense: values.requiresLicense,
        isTechnicianRole: values.isTechnicianRole,
        // ⚠ Round 4, lane O found this missing from both pages since lane H1: the form offered the
        // picker and the page never sent it, so a choice made here was silently dropped.
        preEmploymentCheckTemplateId: values.preEmploymentCheckTemplateId,
        skillRequirements: values.skillRequirements.map((r) => ({
          skillId: r.skillId,
          requiredLevel: r.requiredLevel,
          isRequired: r.isRequired,
          priority: r.priority,
        })),
        certificationRequirements: values.certificationRequirements.map((r) => ({
          certificationId: r.certificationId,
          isMandatory: r.isMandatory,
          notes: r.notes || null,
        })),
        positionBenefits: values.positionBenefits.map((b) => ({
          policyId: b.policyId,
          // '' means "no expiry" / "use the policy's own valuation" — both must go as null,
          // not as an empty string the model binder would reject.
          expiryDate: b.expiryDate ? b.expiryDate : null,
          positionAmount: b.positionAmount ? Number(b.positionAmount) : null,
        })),
        // Round 2, lane C3 — attached sets, as the complete set of ids.
        benefitGroupIds: values.benefitGroupIds,
        skillSetIds: values.skillSetIds,
        certificationSetIds: values.certificationSetIds,
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

      {positionsLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : (
        <EmployeePositionForm
          positions={positions ?? []}
          staffLevels={staffLevels ?? []}
          salaryGrades={salaryGrades ?? []}
          skills={skills ?? []}
          benefitPolicies={benefitPolicies ?? []}
          benefitGroups={benefitGroupOptions}
          skillSets={skillSetOptions}
          certificationSets={certificationSetOptions}
          currencies={currencies ?? []}
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
