'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  UnionForm,
  emptyUnion,
  type UnionFormValues,
} from '@/components/hr/unions/UnionForm';
import { unionService } from '@/services/hr/union.service';

export default function NewUnionPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: UnionFormValues) => {
    setSubmitting(true);
    try {
      const created = await unionService.create({
        code: values.code ?? '',
        name: values.name,
        description: values.description || null,
        contactPerson: values.contactPerson || null,
        contactEmail: values.contactEmail || null,
        contactPhone: values.contactPhone || null,
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'unions'] });
      toast({ title: 'Union created', description: `“${created.name}” was added.` });
      // Straight to the detail: a union with no agreements is half a record, and this is where
      // the first one gets added.
      router.push(`/administration/hr/unions/${created.id}`);
    } catch (error) {
      // A duplicate code comes back as a 400 with the sentence the service wrote.
      toast({
        title: 'Could not create the union',
        description: (error as Error)?.message || 'Failed to create the union.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="New Union"
        description="Add a trade union to the register."
        backHref="/administration/hr/unions"
      />
      <UnionForm
        defaultValues={emptyUnion}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Union"
        onCancel={() => router.push('/administration/hr/unions')}
      />
    </div>
  );
}
