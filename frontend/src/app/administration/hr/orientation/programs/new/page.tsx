'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  OrientationProgramForm,
  emptyOrientationProgram,
  toOrientationProgramRequest,
  type OrientationProgramFormValues,
} from '@/components/hr/orientation/OrientationProgramForm';
import { orientationProgramService } from '@/services/hr/orientation-program.service';
import { orientationCategoryService } from '@/services/hr/orientation-lookup.service';

/**
 * A new programme is created as a Draft — the status is the server's to set, so it is not on the
 * form. Publishing happens from the detail page once there is something to publish.
 */
export default function NewOrientationProgramPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: categories = [] } = useQuery({
    queryKey: ['hr', 'orientation-categories', 'lookup'],
    queryFn: () => orientationCategoryService.getLookup(),
  });

  const handleSubmit = async (values: OrientationProgramFormValues) => {
    setSubmitting(true);
    try {
      // programCode is omitted deliberately — the server generates ORI-{year}-NNNN.
      const created = await orientationProgramService.create(
        toOrientationProgramRequest(values) as any,
      );
      await queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-programs'] });
      toast({
        title: 'Programme created',
        description: `${created.programCode} — add its modules and content next.`,
      });
      router.push(`/administration/hr/orientation/programs/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create the programme.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Orientation Programme"
        description="Add an induction programme to the catalogue. It starts as a draft — its modules, content and assessment are added next, and it is published from there."
        backHref="/administration/hr/orientation/programs"
      />
      <OrientationProgramForm
        defaultValues={emptyOrientationProgram}
        categories={categories}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create programme"
        onCancel={() => router.push('/administration/hr/orientation/programs')}
      />
    </div>
  );
}
