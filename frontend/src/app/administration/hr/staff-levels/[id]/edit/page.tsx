'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  StaffLevelForm,
  type StaffLevelFormValues,
} from '@/components/hr/staff-level/StaffLevelForm';
import { staffLevelService } from '@/services/hr/staff-level.service';

export default function EditStaffLevelPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: level, isLoading, isError } = useQuery({
    queryKey: ['hr', 'staff-levels', id],
    queryFn: () => staffLevelService.getById(id),
    enabled: !!id,
  });

  const handleSubmit = async (values: StaffLevelFormValues) => {
    setSubmitting(true);
    try {
      await staffLevelService.update(id, {
        id,
        name: values.name,
        code: values.code ?? '',
        rank: values.rank,
        description: values.description || null,
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'staff-levels'] });
      toast({ title: 'Success', description: 'Staff level updated.' });
      router.push('/administration/hr/staff-levels');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update staff level.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Edit Staff Level"
        description={level ? level.name : 'Update this staff-level classification.'}
        backHref="/administration/hr/staff-levels"
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !level ? (
        <EmptyState title="Staff level not found" description="This staff level may have been deleted." />
      ) : (
        <StaffLevelForm
          defaultValues={{
            name: level.name,
            code: level.code ?? '',
            rank: level.rank,
            description: level.description ?? '',
            isActive: level.isActive,
          }}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push('/administration/hr/staff-levels')}
        />
      )}
    </div>
  );
}
