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
  DateField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { TrainerForm, type TrainerFormValues } from '@/components/hr/training/TrainerForm';
import { trainerService } from '@/services/hr/trainer.service';
import { trainingVendorService } from '@/services/hr/training-vendor.service';
import { skillService } from '@/services/hr/skill.service';
import { PROFICIENCY_LEVEL_OPTIONS, TRAINER_ENGAGEMENT_TYPE_OPTIONS } from '@/types/hr/training';
import type { TrainerSkill, TrainerAvailability } from '@/types/hr/training';

const skillFormSchema = z.object({
  skillId: z.string().min(1, 'A skill is required'),
  trainerProficiency: z.enum(['Basic', 'WorkingKnowledge', 'Proficient', 'Advanced', 'Expert']),
  isCertifiedToTrain: z.boolean(),
  certificateNumber: z.string().max(100).optional().or(z.literal('')),
  certificateName: z.string().max(200).optional().or(z.literal('')),
});
type SkillForm = z.infer<typeof skillFormSchema>;
const emptySkillForm: SkillForm = {
  skillId: '',
  trainerProficiency: 'WorkingKnowledge',
  isCertifiedToTrain: false,
  certificateNumber: '',
  certificateName: '',
};

const availabilityFormSchema = z.object({
  fromDate: z.string().min(1, 'Required'),
  toDate: z.string().min(1, 'Required'),
  isAvailable: z.boolean(),
  engagementType: z.enum(['Internal', 'External', 'Other', '']).optional(),
  notes: z.string().max(1000).optional().or(z.literal('')),
});
type AvailabilityForm = z.infer<typeof availabilityFormSchema>;
const emptyAvailabilityForm: AvailabilityForm = {
  fromDate: '',
  toDate: '',
  isAvailable: true,
  engagementType: '',
  notes: '',
};

export default function TrainerDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [savingOverview, setSavingOverview] = useState(false);

  const { data: trainer, isLoading, isError } = useQuery({
    queryKey: ['hr', 'training', 'trainers', id],
    queryFn: () => trainerService.getById(id),
    enabled: !!id,
  });

  const { data: vendors } = useQuery({
    queryKey: ['hr', 'training', 'vendors', 'active'],
    queryFn: () => trainingVendorService.getActive(),
  });

  const { data: skills } = useQuery({
    queryKey: ['hr', 'skills', 'active'],
    queryFn: () => skillService.getActive(),
  });
  const skillOptions = (skills ?? []).map((s) => ({ value: s.id, label: s.name }));

  const handleOverviewSubmit = async (values: TrainerFormValues) => {
    setSavingOverview(true);
    try {
      await trainerService.update(id, {
        name: values.name,
        employeeId: values.employeeId || null,
        vendorId: values.vendorId || null,
        bio: values.bio || null,
        contact: values.contact || null,
        expertiseAreas: values.expertiseAreas || null,
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'trainers'] });
      toast({ title: 'Saved', description: 'Trainer updated.' });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update trainer.',
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

  if (isError || !trainer) {
    return (
      <div className="p-6">
        <EmptyState title="Trainer not found" description="It may have been removed." />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={trainer.name}
        description={trainer.employeeName ?? trainer.vendorName ?? 'External trainer'}
        backHref="/administration/hr/training/trainers"
        actions={<StatusBadge active={trainer.isActive} />}
      />

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="skills">Skills</TabsTrigger>
          <TabsTrigger value="availability">Availability</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="pt-4">
          <TrainerForm
            defaultValues={{
              name: trainer.name,
              employeeId: trainer.employeeId ?? '',
              vendorId: trainer.vendorId ?? '',
              bio: trainer.bio ?? '',
              contact: trainer.contact ?? '',
              expertiseAreas: trainer.expertiseAreas ?? '',
              isActive: trainer.isActive,
            }}
            defaultEmployeeLabel={trainer.employeeName}
            vendors={vendors ?? []}
            onSubmit={handleOverviewSubmit}
            submitting={savingOverview}
            submitLabel="Save changes"
            onCancel={() => router.push('/administration/hr/training/trainers')}
          />
        </TabsContent>

        <TabsContent value="skills" className="pt-4">
          <ResourceCollectionTab<TrainerSkill, SkillForm>
            parentId={id}
            title="skills"
            singular="skill"
            queryKey={['hr', 'training', 'trainers', id, 'skills']}
            dialogHint="Skills this trainer is qualified to deliver training on."
            list={() => trainerService.getSkills(id)}
            create={(trainerId, values) => trainerService.addSkill(trainerId, values)}
            update={(_trainerId, skillId, values) => {
              const { skillId: _skillId, ...rest } = values;
              return trainerService.updateSkill(skillId, rest);
            }}
            remove={(_trainerId, skillId) => trainerService.removeSkill(skillId)}
            getId={(s) => s.id}
            columns={[
              { header: 'Skill', cell: (s) => <span className="font-medium">{s.skillName}</span> },
              {
                header: 'Proficiency',
                cell: (s) =>
                  PROFICIENCY_LEVEL_OPTIONS.find((o) => o.value === s.trainerProficiency)?.label ??
                  s.trainerProficiency,
              },
              { header: 'Certified to train', cell: (s) => (s.isCertifiedToTrain ? 'Yes' : 'No') },
              { header: 'Certificate', cell: (s) => s.certificateName || '—' },
            ]}
            schema={skillFormSchema as any}
            emptyForm={emptySkillForm}
            toForm={(s) => ({
              skillId: s.skillId,
              trainerProficiency: s.trainerProficiency,
              isCertifiedToTrain: s.isCertifiedToTrain,
              certificateNumber: s.certificateNumber ?? '',
              certificateName: s.certificateName ?? '',
            })}
            renderFields={(form, editing) => {
              const certified = !!form.watch('isCertifiedToTrain');
              return (
                <>
                  {editing ? (
                    <div className="rounded-md border bg-muted/50 px-3 py-2 text-sm">
                      Skill: <span className="font-medium">{skillOptions.find((o) => o.value === form.getValues('skillId'))?.label ?? '—'}</span>
                      <p className="mt-1 text-xs text-muted-foreground">The skill cannot be changed — remove and re-add to link a different one.</p>
                    </div>
                  ) : (
                    <SelectField form={form} name="skillId" label="Skill" required options={skillOptions} />
                  )}
                  <SelectField
                    form={form}
                    name="trainerProficiency"
                    label="Proficiency"
                    required
                    options={PROFICIENCY_LEVEL_OPTIONS}
                  />
                  <SwitchField form={form} name="isCertifiedToTrain" label="Certified to train" />
                  {certified && (
                    <FieldRow>
                      <TextField form={form} name="certificateName" label="Certificate name" />
                      <TextField form={form} name="certificateNumber" label="Certificate number" />
                    </FieldRow>
                  )}
                </>
              );
            }}
          />
        </TabsContent>

        <TabsContent value="availability" className="pt-4">
          <ResourceCollectionTab<TrainerAvailability, AvailabilityForm>
            parentId={id}
            title="availability windows"
            singular="availability window"
            queryKey={['hr', 'training', 'trainers', id, 'availability']}
            dialogHint="Mark periods this trainer is booked elsewhere or unavailable."
            list={() => trainerService.getAvailability(id)}
            create={(trainerId, values) => trainerService.addAvailability(trainerId, {
              ...values,
              engagementType: values.engagementType || null,
              notes: values.notes || null,
            } as any)}
            update={(_trainerId, availabilityId, values) => trainerService.updateAvailability(availabilityId, {
              ...values,
              engagementType: values.engagementType || null,
              notes: values.notes || null,
            } as any)}
            remove={(_trainerId, availabilityId) => trainerService.removeAvailability(availabilityId)}
            getId={(a) => a.id}
            columns={[
              { header: 'From', cell: (a) => new Date(a.fromDate).toLocaleDateString() },
              { header: 'To', cell: (a) => new Date(a.toDate).toLocaleDateString() },
              { header: 'Available', cell: (a) => (a.isAvailable ? 'Yes' : 'No — booked') },
              {
                header: 'Engagement',
                cell: (a) => TRAINER_ENGAGEMENT_TYPE_OPTIONS.find((o) => o.value === a.engagementType)?.label ?? '—',
              },
            ]}
            schema={availabilityFormSchema as any}
            emptyForm={emptyAvailabilityForm}
            toForm={(a) => ({
              fromDate: a.fromDate.slice(0, 10),
              toDate: a.toDate.slice(0, 10),
              isAvailable: a.isAvailable,
              engagementType: a.engagementType ?? '',
              notes: a.notes ?? '',
            })}
            renderFields={(form) => (
              <>
                <FieldRow>
                  <DateField form={form} name="fromDate" label="From date" required />
                  <DateField form={form} name="toDate" label="To date" required />
                </FieldRow>
                <SwitchField
                  form={form}
                  name="isAvailable"
                  label="Available"
                  description="Off marks this window as booked/unavailable instead."
                />
                <SelectField
                  form={form}
                  name="engagementType"
                  label="Engagement type"
                  options={TRAINER_ENGAGEMENT_TYPE_OPTIONS}
                  allowEmpty
                  emptyLabel="Not specified"
                />
                <TextareaField form={form} name="notes" label="Notes" rows={2} />
              </>
            )}
          />
        </TabsContent>
      </Tabs>
    </div>
  );
}
