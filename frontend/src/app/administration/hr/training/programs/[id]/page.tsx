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
import {
  TextField,
  SelectField,
  SwitchField,
  NumberField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import {
  TrainingProgramForm,
  type TrainingProgramFormValues,
} from '@/components/hr/training/TrainingProgramForm';
import { trainingProgramService } from '@/services/hr/training-program.service';
import { trainingCategoryService } from '@/services/hr/training-category.service';
import { trainingProgramGroupService } from '@/services/hr/training-program-group.service';
import { skillService } from '@/services/hr/skill.service';
import { competencyService } from '@/services/hr/competency.service';
import { MATERIAL_TYPE_OPTIONS, PROFICIENCY_LEVEL_OPTIONS } from '@/types/hr/training';
import type {
  TrainingMaterial,
  TrainingProgramCompetency,
  TrainingProgramSkill,
} from '@/types/hr/training';

const materialFormSchema = z.object({
  materialName: z.string().min(1, 'Name is required').max(200),
  type: z.enum(['Handbook', 'Slides', 'Video', 'Document', 'Exercise', 'Assessment', 'Reference']),
  filePath: z.string().max(1000).optional().or(z.literal('')),
  externalUrl: z.string().max(1000).optional().or(z.literal('')),
  isPublic: z.boolean(),
  isActive: z.boolean(),
});
type MaterialForm = z.infer<typeof materialFormSchema>;
const emptyMaterialForm: MaterialForm = {
  materialName: '',
  type: 'Document',
  filePath: '',
  externalUrl: '',
  isPublic: false,
  isActive: true,
};

const competencyFormSchema = z.object({
  competencyId: z.string().min(1, 'A competency is required'),
  targetLevel: z.coerce.number().min(1).max(5),
});
type CompetencyForm = z.infer<typeof competencyFormSchema>;
const emptyCompetencyForm: CompetencyForm = { competencyId: '', targetLevel: 3 };

const programSkillFormSchema = z.object({
  skillId: z.string().min(1, 'A skill is required'),
  targetProficiency: z.enum(['Basic', 'WorkingKnowledge', 'Proficient', 'Advanced', 'Expert']),
});
type ProgramSkillForm = z.infer<typeof programSkillFormSchema>;
const emptyProgramSkillForm: ProgramSkillForm = { skillId: '', targetProficiency: 'WorkingKnowledge' };

export default function TrainingProgramDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [savingOverview, setSavingOverview] = useState(false);

  const { data: program, isLoading, isError } = useQuery({
    queryKey: ['hr', 'training', 'programs', id],
    queryFn: () => trainingProgramService.getById(id),
    enabled: !!id,
  });

  const { data: categories } = useQuery({
    queryKey: ['hr', 'training', 'categories', 'active'],
    queryFn: () => trainingCategoryService.getAll(true),
  });
  const { data: groups } = useQuery({
    queryKey: ['hr', 'training', 'program-groups', 'active'],
    queryFn: () => trainingProgramGroupService.getAll(true),
  });
  const { data: skills } = useQuery({
    queryKey: ['hr', 'skills', 'active'],
    queryFn: () => skillService.getActive(),
  });
  const skillOptions = (skills ?? []).map((s) => ({ value: s.id, label: s.name }));

  const { data: competencies } = useQuery({
    queryKey: ['hr', 'competencies', 'lookup'],
    queryFn: () => competencyService.getLookup(),
  });
  const competencyOptions = (competencies ?? []).map((c) => ({ value: c.id, label: `${c.name} (${c.code})` }));

  const handleOverviewSubmit = async (values: TrainingProgramFormValues) => {
    setSavingOverview(true);
    try {
      await trainingProgramService.update(id, {
        ...values,
        description: values.description || '',
        categoryOptionId: values.categoryOptionId || null,
        programGroupId: values.programGroupId || null,
        prerequisites: values.prerequisites || null,
        learningObjectives: values.learningObjectives || null,
        certificateName: values.certificateName || null,
        serviceBondTerms: values.serviceBondTerms || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'programs'] });
      toast({ title: 'Saved', description: 'Program updated.' });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update program.',
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

  if (isError || !program) {
    return (
      <div className="p-6">
        <EmptyState title="Program not found" description="It may have been removed." />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={program.programName}
        description={`${program.programCode}${program.categoryName ? ` · ${program.categoryName}` : ''}`}
        backHref="/administration/hr/training/programs"
        actions={<StatusBadge active={program.isActive} />}
      />

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="materials">Materials</TabsTrigger>
          <TabsTrigger value="competencies">Competencies</TabsTrigger>
          <TabsTrigger value="skills">Skills</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="pt-4">
          <TrainingProgramForm
            programCodeEditable={false}
            defaultValues={{
              programCode: program.programCode,
              programName: program.programName,
              description: program.description,
              categoryOptionId: program.categoryOptionId ?? '',
              programGroupId: program.programGroupId ?? '',
              type: program.type,
              source: program.source,
              level: program.level,
              durationDays: program.durationDays,
              durationHours: program.durationHours,
              prerequisites: program.prerequisites ?? '',
              learningObjectives: program.learningObjectives ?? '',
              costPerParticipant: program.costPerParticipant,
              currency: program.currency,
              includesAccommodation: program.includesAccommodation,
              includesMeals: program.includesMeals,
              includesTransport: program.includesTransport,
              providesCertificate: program.providesCertificate,
              certificateName: program.certificateName ?? '',
              certificateValidityMonths: program.certificateValidityMonths ?? undefined,
              minParticipants: program.minParticipants ?? undefined,
              maxParticipants: program.maxParticipants ?? undefined,
              isActive: program.isActive,
              requiresApproval: program.requiresApproval,
              requiresServiceBond: program.requiresServiceBond,
              serviceBondMonths: program.serviceBondMonths ?? undefined,
              serviceBondTerms: program.serviceBondTerms ?? '',
            }}
            categories={categories ?? []}
            groups={groups ?? []}
            onSubmit={handleOverviewSubmit}
            submitting={savingOverview}
            submitLabel="Save changes"
            onCancel={() => router.push('/administration/hr/training/programs')}
          />
        </TabsContent>

        <TabsContent value="materials" className="pt-4">
          <ResourceCollectionTab<TrainingMaterial, MaterialForm>
            parentId={id}
            title="materials"
            singular="material"
            queryKey={['hr', 'training', 'programs', id, 'materials']}
            dialogHint="Handbooks, slides and other resources for this program."
            list={() => trainingProgramService.getMaterials(id)}
            create={(programId, values) => trainingProgramService.addMaterial(programId, {
              ...values,
              filePath: values.filePath || null,
              externalUrl: values.externalUrl || null,
            })}
            update={(_programId, materialId, values) => trainingProgramService.updateMaterial(materialId, {
              ...values,
              filePath: values.filePath || null,
              externalUrl: values.externalUrl || null,
            })}
            remove={(_programId, materialId) => trainingProgramService.removeMaterial(materialId)}
            getId={(m) => m.id}
            columns={[
              { header: 'Name', cell: (m) => <span className="font-medium">{m.materialName}</span> },
              {
                header: 'Type',
                cell: (m) => MATERIAL_TYPE_OPTIONS.find((o) => o.value === m.type)?.label ?? m.type,
              },
              { header: 'Public', cell: (m) => (m.isPublic ? 'Yes' : 'No') },
              { header: 'Status', cell: (m) => <StatusBadge active={m.isActive} /> },
            ]}
            schema={materialFormSchema as any}
            emptyForm={emptyMaterialForm}
            toForm={(m) => ({
              materialName: m.materialName,
              type: m.type,
              filePath: m.filePath ?? '',
              externalUrl: m.externalUrl ?? '',
              isPublic: m.isPublic,
              isActive: m.isActive,
            })}
            renderFields={(form) => (
              <>
                <FieldRow>
                  <TextField form={form} name="materialName" label="Name" required />
                  <SelectField form={form} name="type" label="Type" required options={MATERIAL_TYPE_OPTIONS} />
                </FieldRow>
                <TextField form={form} name="externalUrl" label="External URL" placeholder="https://…" />
                <FieldRow>
                  <SwitchField
                    form={form}
                    name="isPublic"
                    label="Public"
                    description="Visible to nominees before the session, not just facilitators."
                  />
                  <SwitchField form={form} name="isActive" label="Active" />
                </FieldRow>
              </>
            )}
          />
        </TabsContent>

        <TabsContent value="competencies" className="pt-4">
          <ResourceCollectionTab<TrainingProgramCompetency, CompetencyForm>
            parentId={id}
            title="competencies"
            singular="competency"
            queryKey={['hr', 'training', 'programs', id, 'competencies']}
            allowUpdate={false}
            dialogHint="Competencies this program is designed to build."
            emptyDescription="No competencies linked yet."
            list={() => trainingProgramService.getCompetencies(id)}
            create={(programId, values) => trainingProgramService.addCompetency(programId, values)}
            update={() => Promise.resolve()}
            remove={(_programId, competencyId) => trainingProgramService.removeCompetency(competencyId)}
            getId={(c) => c.id}
            columns={[
              { header: 'Competency', cell: (c) => <span className="font-medium">{c.competencyName}</span> },
              { header: 'Code', cell: (c) => c.competencyCode },
              { header: 'Target level', cell: (c) => `${c.targetLevel} / 5` },
            ]}
            schema={competencyFormSchema as any}
            emptyForm={emptyCompetencyForm}
            toForm={(c) => ({ competencyId: c.competencyId, targetLevel: c.targetLevel })}
            renderFields={(form) => (
              <>
                <SelectField form={form} name="competencyId" label="Competency" required options={competencyOptions} />
                <NumberField form={form} name="targetLevel" label="Target level (1–5)" required />
              </>
            )}
          />
        </TabsContent>

        <TabsContent value="skills" className="pt-4">
          <ResourceCollectionTab<TrainingProgramSkill, ProgramSkillForm>
            parentId={id}
            title="skills"
            singular="skill"
            queryKey={['hr', 'training', 'programs', id, 'skills']}
            allowUpdate={false}
            dialogHint="Skills this program is designed to build."
            emptyDescription="No skills linked yet."
            list={() => trainingProgramService.getSkills(id)}
            create={(programId, values) => trainingProgramService.addSkill(programId, values)}
            update={() => Promise.resolve()}
            remove={(_programId, skillId) => trainingProgramService.removeSkill(skillId)}
            getId={(s) => s.id}
            columns={[
              { header: 'Skill', cell: (s) => <span className="font-medium">{s.skillName}</span> },
              {
                header: 'Target proficiency',
                cell: (s) =>
                  PROFICIENCY_LEVEL_OPTIONS.find((o) => o.value === s.targetProficiency)?.label ??
                  s.targetProficiency,
              },
            ]}
            schema={programSkillFormSchema as any}
            emptyForm={emptyProgramSkillForm}
            toForm={(s) => ({ skillId: s.skillId, targetProficiency: s.targetProficiency })}
            renderFields={(form) => (
              <>
                <SelectField form={form} name="skillId" label="Skill" required options={skillOptions} />
                <SelectField
                  form={form}
                  name="targetProficiency"
                  label="Target proficiency"
                  required
                  options={PROFICIENCY_LEVEL_OPTIONS}
                />
              </>
            )}
          />
        </TabsContent>
      </Tabs>
    </div>
  );
}
