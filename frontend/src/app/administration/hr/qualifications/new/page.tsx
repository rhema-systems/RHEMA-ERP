'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  QualificationForm,
  emptyQualification,
  type QualificationFormValues,
} from '@/components/hr/lookup/QualificationForm';
import { qualificationService } from '@/services/hr/lookup.service';

export default function NewQualificationPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: QualificationFormValues) => {
    setSubmitting(true);
    try {
      await qualificationService.create({
        name: values.name,
        shortCode: values.shortCode || null,
        description: values.description || null,
        type: values.type,
        issuingAuthority: values.issuingAuthority || null,
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'qualifications'] });
      toast({ title: 'Success', description: 'Qualification created.' });
      router.push('/administration/hr/qualifications');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create qualification.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="New Qualification"
        description="Add a qualification to the catalogue."
        backHref="/administration/hr/qualifications"
      />
      <QualificationForm
        defaultValues={emptyQualification}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Qualification"
        onCancel={() => router.push('/administration/hr/qualifications')}
      />
    </div>
  );
}
