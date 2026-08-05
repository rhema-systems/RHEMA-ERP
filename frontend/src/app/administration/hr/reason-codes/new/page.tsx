'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  ReasonCodeForm,
  emptyReasonCode,
  type ReasonCodeFormValues,
} from '@/components/hr/lookup/ReasonCodeForm';
import { reasonCodeService } from '@/services/hr/lookup.service';

export default function NewReasonCodePage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: ReasonCodeFormValues) => {
    setSubmitting(true);
    try {
      await reasonCodeService.create({
        code: values.code,
        name: values.name,
        description: values.description || null,
        category: values.category,
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'reason-codes'] });
      toast({ title: 'Success', description: 'Reason code created.' });
      router.push('/administration/hr/reason-codes');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create reason code.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="New Reason Code"
        description="Add a standard reason for HR actions."
        backHref="/administration/hr/reason-codes"
      />
      <ReasonCodeForm
        defaultValues={emptyReasonCode}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Reason Code"
        onCancel={() => router.push('/administration/hr/reason-codes')}
      />
    </div>
  );
}
