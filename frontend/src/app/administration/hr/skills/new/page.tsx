'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { SkillForm, emptySkill, type SkillFormValues } from '@/components/hr/skill/SkillForm';
import { skillService } from '@/services/hr/skill.service';

export default function NewSkillPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: categories } = useQuery({
    queryKey: ['hr', 'skill-categories'],
    queryFn: () => skillService.getCategories(),
  });

  const handleSubmit = async (values: SkillFormValues) => {
    setSubmitting(true);
    try {
      await skillService.create({
        name: values.name,
        category: values.category || null,
        description: values.description || null,
        requiresCertification: values.requiresCertification,
        // The whole set (round 2, lane C2): the server replaces what is stored with this.
        certifications: values.certifications.map((c) => ({
          certificationId: c.certificationId,
          isMandatory: c.isMandatory,
          notes: c.notes || null,
        })),
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'skills'] });
      toast({ title: 'Success', description: 'Skill created.' });
      router.push('/administration/hr/skills');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create skill.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Skill"
        description="Add a skill to the catalog."
        backHref="/administration/hr/skills"
      />
      <SkillForm
        defaultValues={emptySkill}
        categories={categories ?? []}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Skill"
        onCancel={() => router.push('/administration/hr/skills')}
        showActive={false}
      />
    </div>
  );
}
