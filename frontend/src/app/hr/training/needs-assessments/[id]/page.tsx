'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2 } from 'lucide-react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { SelectField, TextareaField } from '@/components/hr/employee/tabs/fields';
import {
  NeedsAssessmentForm,
  type NeedsAssessmentFormValues,
} from '@/components/hr/training/NeedsAssessmentForm';
import { trainingNeedsAssessmentService } from '@/services/hr/training-needs-assessment.service';
import { trainingProgramService } from '@/services/hr/training-program.service';
import { skillService } from '@/services/hr/skill.service';
import { PROFICIENCY_LEVEL_OPTIONS, TRAINING_PRIORITY_OPTIONS } from '@/types/hr/training';
import type { TrainingNeedsAssessmentProgram, TrainingNeedsAssessmentSkill } from '@/types/hr/training';

const programFormSchema = z.object({
  programId: z.string().min(1, 'A program is required'),
  priority: z.enum(['Critical', 'High', 'Medium', 'Low']),
  rationale: z.string().max(2000).optional().or(z.literal('')),
});
type ProgramForm = z.infer<typeof programFormSchema>;
const emptyProgramForm: ProgramForm = { programId: '', priority: 'Medium', rationale: '' };

const skillFormSchema = z.object({
  skillId: z.string().min(1, 'A skill is required'),
  currentProficiency: z.enum(['Basic', 'WorkingKnowledge', 'Proficient', 'Advanced', 'Expert']),
  requiredProficiency: z.enum(['Basic', 'WorkingKnowledge', 'Proficient', 'Advanced', 'Expert']),
  gapPriority: z.enum(['Critical', 'High', 'Medium', 'Low']),
});
type SkillForm = z.infer<typeof skillFormSchema>;
const emptySkillForm: SkillForm = {
  skillId: '',
  currentProficiency: 'Basic',
  requiredProficiency: 'WorkingKnowledge',
  gapPriority: 'Medium',
};

export default function NeedsAssessmentDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [savingOverview, setSavingOverview] = useState(false);

  const { data: assessment, isLoading, isError } = useQuery({
    queryKey: ['hr', 'training', 'needs-assessments', id],
    queryFn: () => trainingNeedsAssessmentService.getById(id),
    enabled: !!id,
  });

  const { data: programs } = useQuery({
    queryKey: ['hr', 'training', 'programs', 'active'],
    queryFn: () => trainingProgramService.getActive(),
  });
  const programOptions = (programs ?? []).map((p) => ({ value: p.id, label: `${p.programCode} — ${p.programName}` }));

  const { data: skills } = useQuery({
    queryKey: ['hr', 'skills', 'active'],
    queryFn: () => skillService.getActive(),
  });
  const skillOptions = (skills ?? []).map((s) => ({ value: s.id, label: s.name }));

  const handleOverviewSubmit = async (values: NeedsAssessmentFormValues) => {
    setSavingOverview(true);
    try {
      await trainingNeedsAssessmentService.update(id, {
        source: values.source,
        identifiedGaps: values.identifiedGaps,
        priority: values.priority,
        additionalNotes: values.additionalNotes || null,
        trainingProvided: values.trainingProvided,
        trainingProvidedDate: values.trainingProvided ? values.trainingProvidedDate || null : null,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'needs-assessments'] });
      toast({ title: 'Saved', description: 'Assessment updated.' });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update assessment.',
        variant: 'destructive',
      });
    } finally {
      setSavingOverview(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !assessment) {
    return (
      <div className="p-6">
        <EmptyState title="Assessment not found" description="It may have been removed." />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${assessment.employeeName} — ${assessment.year}`}
        description={assessment.employeeNumber}
        backHref="/hr/training/needs-assessments"
        actions={<StatusBadge status={assessment.trainingProvided ? 'Fulfilled' : 'Pending'} />}
      />

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="programs">Recommended Programs</TabsTrigger>
          <TabsTrigger value="skills">Skill Gaps</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="pt-4">
          <NeedsAssessmentForm
            mode="edit"
            defaultValues={{
              employeeId: assessment.employeeId,
              year: assessment.year,
              source: assessment.source,
              identifiedGaps: assessment.identifiedGaps,
              priority: assessment.priority,
              identifiedById: assessment.identifiedById ?? '',
              identifiedDate: assessment.identifiedDate.slice(0, 10),
              additionalNotes: assessment.additionalNotes ?? '',
              trainingProvided: assessment.trainingProvided,
              trainingProvidedDate: assessment.trainingProvidedDate?.slice(0, 10) ?? '',
            }}
            defaultEmployeeLabel={`${assessment.employeeName} (${assessment.employeeNumber})`}
            defaultIdentifiedByLabel={assessment.identifiedByName}
            onSubmit={handleOverviewSubmit}
            submitting={savingOverview}
            submitLabel="Save changes"
            onCancel={() => router.push('/hr/training/needs-assessments')}
          />
        </TabsContent>

        <TabsContent value="programs" className="pt-4">
          <ResourceCollectionTab<TrainingNeedsAssessmentProgram, ProgramForm>
            parentId={id}
            title="recommended programs"
            singular="recommended program"
            queryKey={['hr', 'training', 'needs-assessments', id, 'programs']}
            invalidateKeys={[['hr', 'training', 'needs-assessments', id]]}
            dialogHint="Programs recommended to close this employee's identified gap."
            list={() => trainingNeedsAssessmentService.getRecommendedPrograms(id)}
            create={(assessmentId, values) => trainingNeedsAssessmentService.addRecommendedProgram(assessmentId, values)}
            update={async () => {}}
            allowUpdate={false}
            remove={(_assessmentId, programId) => trainingNeedsAssessmentService.removeRecommendedProgram(programId)}
            getId={(p) => p.id}
            columns={[
              { header: 'Program', cell: (p) => <span className="font-medium">{p.programCode} — {p.programName}</span> },
              {
                header: 'Priority',
                cell: (p) => TRAINING_PRIORITY_OPTIONS.find((o) => o.value === p.priority)?.label ?? p.priority,
              },
              { header: 'Rationale', cell: (p) => p.rationale || '—' },
            ]}
            schema={programFormSchema as any}
            emptyForm={emptyProgramForm}
            toForm={(p) => ({ programId: p.programId, priority: p.priority, rationale: p.rationale ?? '' })}
            renderFields={(form) => (
              <>
                <SelectField form={form} name="programId" label="Program" required options={programOptions} />
                <SelectField form={form} name="priority" label="Priority" required options={TRAINING_PRIORITY_OPTIONS} />
                <TextareaField form={form} name="rationale" label="Rationale" rows={2} />
              </>
            )}
          />
        </TabsContent>

        <TabsContent value="skills" className="pt-4">
          <ResourceCollectionTab<TrainingNeedsAssessmentSkill, SkillForm>
            parentId={id}
            title="skill gaps"
            singular="skill gap"
            queryKey={['hr', 'training', 'needs-assessments', id, 'skill-gaps']}
            invalidateKeys={[['hr', 'training', 'needs-assessments', id]]}
            dialogHint="Skills where this employee's current level falls short of what the role requires."
            list={() => trainingNeedsAssessmentService.getSkillGaps(id)}
            create={(assessmentId, values) => trainingNeedsAssessmentService.addSkillGap(assessmentId, values)}
            update={async () => {}}
            allowUpdate={false}
            remove={(_assessmentId, skillId) => trainingNeedsAssessmentService.removeSkillGap(skillId)}
            getId={(s) => s.id}
            columns={[
              { header: 'Skill', cell: (s) => <span className="font-medium">{s.skillName}</span> },
              // Fall back to the raw value: an option list that drifts from the backend enum would
              // otherwise render an empty cell rather than something traceable.
              {
                header: 'Current',
                cell: (s) =>
                  PROFICIENCY_LEVEL_OPTIONS.find((o) => o.value === s.currentProficiency)?.label ??
                  s.currentProficiency,
              },
              {
                header: 'Required',
                cell: (s) =>
                  PROFICIENCY_LEVEL_OPTIONS.find((o) => o.value === s.requiredProficiency)?.label ??
                  s.requiredProficiency,
              },
              {
                header: 'Gap priority',
                cell: (s) =>
                  TRAINING_PRIORITY_OPTIONS.find((o) => o.value === s.gapPriority)?.label ?? s.gapPriority,
              },
            ]}
            schema={skillFormSchema as any}
            emptyForm={emptySkillForm}
            toForm={(s) => ({
              skillId: s.skillId,
              currentProficiency: s.currentProficiency,
              requiredProficiency: s.requiredProficiency,
              gapPriority: s.gapPriority,
            })}
            renderFields={(form) => (
              <>
                <SelectField form={form} name="skillId" label="Skill" required options={skillOptions} />
                <SelectField
                  form={form}
                  name="currentProficiency"
                  label="Current proficiency"
                  required
                  options={PROFICIENCY_LEVEL_OPTIONS}
                />
                <SelectField
                  form={form}
                  name="requiredProficiency"
                  label="Required proficiency"
                  required
                  options={PROFICIENCY_LEVEL_OPTIONS}
                />
                <SelectField
                  form={form}
                  name="gapPriority"
                  label="Gap priority"
                  required
                  options={TRAINING_PRIORITY_OPTIONS}
                />
              </>
            )}
          />
        </TabsContent>
      </Tabs>
    </div>
  );
}
