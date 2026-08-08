'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  ReasonCodeForm,
  type ReasonCodeFormValues,
} from '@/components/hr/lookup/ReasonCodeForm';
import { reasonCodeService } from '@/services/hr/lookup.service';

export default function EditReasonCodePage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: reasonCode, isLoading, isError } = useQuery({
    queryKey: ['hr', 'reason-codes', id],
    queryFn: () => reasonCodeService.getById(id),
    enabled: !!id,
  });

  const handleSubmit = async (values: ReasonCodeFormValues) => {
    setSubmitting(true);
    try {
      await reasonCodeService.update(id, {
        code: values.code,
        name: values.name,
        description: values.description || null,
        category: values.category,
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'reason-codes'] });
      toast({ title: 'Success', description: 'Reason code updated.' });
      router.push('/administration/hr/reason-codes');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update reason code.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="Edit Reason Code"
        description={reasonCode ? reasonCode.name : 'Update this reason code.'}
        backHref="/administration/hr/reason-codes"
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !reasonCode ? (
        <EmptyState title="Reason code not found" description="This code may have been removed." />
      ) : (
        <ReasonCodeForm
          defaultValues={{
            code: reasonCode.code,
            name: reasonCode.name,
            category: reasonCode.category,
            description: reasonCode.description ?? '',
            isActive: reasonCode.isActive,
          }}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push('/administration/hr/reason-codes')}
        />
      )}
    </div>
  );
}
