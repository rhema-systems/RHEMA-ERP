'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  QualificationForm,
  type QualificationFormValues,
} from '@/components/hr/lookup/QualificationForm';
import { qualificationService } from '@/services/hr/lookup.service';

export default function EditQualificationPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: qualification, isLoading, isError } = useQuery({
    queryKey: ['hr', 'qualifications', id],
    queryFn: () => qualificationService.getById(id),
    enabled: !!id,
  });

  const handleSubmit = async (values: QualificationFormValues) => {
    setSubmitting(true);
    try {
      await qualificationService.update(id, {
        name: values.name,
        shortCode: values.shortCode || null,
        description: values.description || null,
        type: values.type,
        issuingAuthority: values.issuingAuthority || null,
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'qualifications'] });
      toast({ title: 'Success', description: 'Qualification updated.' });
      router.push('/administration/hr/qualifications');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update qualification.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="Edit Qualification"
        description={qualification ? qualification.name : 'Update this qualification.'}
        backHref="/administration/hr/qualifications"
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !qualification ? (
        <EmptyState
          title="Qualification not found"
          description="This qualification may have been deleted."
        />
      ) : (
        <QualificationForm
          defaultValues={{
            name: qualification.name,
            shortCode: qualification.shortCode ?? '',
            type: qualification.type,
            issuingAuthority: qualification.issuingAuthority ?? '',
            description: qualification.description ?? '',
            isActive: qualification.isActive,
          }}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push('/administration/hr/qualifications')}
        />
      )}
    </div>
  );
}
