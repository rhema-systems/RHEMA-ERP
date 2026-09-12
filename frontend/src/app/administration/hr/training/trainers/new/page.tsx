'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { TrainerForm, emptyTrainer, type TrainerFormValues } from '@/components/hr/training/TrainerForm';
import { trainerService } from '@/services/hr/trainer.service';
import { trainingVendorService } from '@/services/hr/training-vendor.service';

export default function NewTrainerPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: vendors } = useQuery({
    queryKey: ['hr', 'training', 'vendors', 'active'],
    queryFn: () => trainingVendorService.getActive(),
  });

  const handleSubmit = async (values: TrainerFormValues) => {
    setSubmitting(true);
    try {
      const created = await trainerService.create({
        name: values.name,
        employeeId: values.employeeId || null,
        vendorId: values.vendorId || null,
        bio: values.bio || null,
        contact: values.contact || null,
        expertiseAreas: values.expertiseAreas || null,
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'trainers'] });
      toast({ title: 'Success', description: 'Trainer created.' });
      router.push(`/administration/hr/training/trainers/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create trainer.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Trainer"
        description="Register an internal or external trainer."
        backHref="/administration/hr/training/trainers"
      />
      <TrainerForm
        defaultValues={emptyTrainer}
        vendors={vendors ?? []}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Trainer"
        onCancel={() => router.push('/administration/hr/training/trainers')}
        showActive={false}
      />
    </div>
  );
}
