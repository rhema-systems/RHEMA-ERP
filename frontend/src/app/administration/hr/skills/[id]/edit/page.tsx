'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { SkillForm, type SkillFormValues } from '@/components/hr/skill/SkillForm';
import { skillService } from '@/services/hr/skill.service';

export default function EditSkillPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: skill, isLoading, isError } = useQuery({
    queryKey: ['hr', 'skills', id],
    queryFn: () => skillService.getById(id),
    enabled: !!id,
  });

  const { data: categories } = useQuery({
    queryKey: ['hr', 'skill-categories'],
    queryFn: () => skillService.getCategories(),
  });

  const handleSubmit = async (values: SkillFormValues) => {
    setSubmitting(true);
    try {
      await skillService.update(id, {
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
      toast({ title: 'Success', description: 'Skill updated.' });
      router.push('/administration/hr/skills');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update skill.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Edit Skill"
        description={skill ? skill.name : 'Update this skill.'}
        backHref="/administration/hr/skills"
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !skill ? (
        <EmptyState title="Skill not found" description="This skill may have been deleted." />
      ) : (
        <SkillForm
          defaultValues={{
            name: skill.name,
            category: skill.category ?? '',
            description: skill.description ?? '',
            requiresCertification: skill.requiresCertification,
            // Loading these back is not cosmetic: the server syncs to whatever the save sends.
            certifications: (skill.certifications ?? []).map((c) => ({
              certificationId: c.certificationId,
              isMandatory: c.isMandatory,
              notes: c.notes ?? '',
            })),
            isActive: skill.isActive,
          }}
          categories={categories ?? []}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push('/administration/hr/skills')}
        />
      )}
    </div>
  );
}
