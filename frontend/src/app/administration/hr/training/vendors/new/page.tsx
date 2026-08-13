'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  TrainingVendorForm,
  emptyTrainingVendor,
  type TrainingVendorFormValues,
} from '@/components/hr/training/TrainingVendorForm';
import { trainingVendorService } from '@/services/hr/training-vendor.service';

export default function NewTrainingVendorPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: TrainingVendorFormValues) => {
    setSubmitting(true);
    try {
      await trainingVendorService.create({
        ...values,
        preferredSince: values.preferredSince || null,
        accreditationBody: values.accreditationBody || null,
        accreditationNumber: values.accreditationNumber || null,
        accreditationStatus: values.accreditationStatus || null,
        accreditationExpiryDate: values.accreditationExpiryDate || null,
        primaryContactName: values.primaryContactName || null,
        primaryContactEmail: values.primaryContactEmail || null,
        primaryContactPhone: values.primaryContactPhone || null,
        website: values.website || null,
        address: values.address || null,
        contractReference: values.contractReference || null,
        contractStartDate: values.contractStartDate || null,
        contractEndDate: values.contractEndDate || null,
        notes: values.notes || null,
      } as any);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'vendors'] });
      toast({ title: 'Success', description: 'Training vendor created.' });
      router.push('/administration/hr/training/vendors');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create vendor.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Training Vendor"
        description="Register an external training provider."
        backHref="/administration/hr/training/vendors"
      />
      <TrainingVendorForm
        defaultValues={emptyTrainingVendor}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Vendor"
        onCancel={() => router.push('/administration/hr/training/vendors')}
      />
    </div>
  );
}
