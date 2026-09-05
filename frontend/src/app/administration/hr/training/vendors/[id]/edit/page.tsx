'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import {
  TrainingVendorForm,
  type TrainingVendorFormValues,
} from '@/components/hr/training/TrainingVendorForm';
import { trainingVendorService } from '@/services/hr/training-vendor.service';

export default function EditTrainingVendorPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: vendor, isLoading, isError } = useQuery({
    queryKey: ['hr', 'training', 'vendors', id],
    queryFn: () => trainingVendorService.getById(id),
    enabled: !!id,
  });

  const handleSubmit = async (values: TrainingVendorFormValues) => {
    setSubmitting(true);
    try {
      const { vendorCode: _vendorCode, ...fields } = values;
      await trainingVendorService.update(id, {
        ...fields,
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
      toast({ title: 'Success', description: 'Training vendor updated.' });
      router.push('/administration/hr/training/vendors');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update vendor.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Edit Training Vendor"
        description={vendor ? vendor.name : 'Update this vendor.'}
        backHref="/administration/hr/training/vendors"
        actions={vendor ? <StatusBadge active={vendor.isActive} /> : undefined}
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !vendor ? (
        <EmptyState title="Vendor not found" description="This vendor may have been deleted." />
      ) : (
        <TrainingVendorForm
          vendorCodeEditable={false}
          defaultValues={{
            vendorCode: vendor.vendorCode,
            name: vendor.name,
            vendorType: vendor.vendorType,
            isActive: vendor.isActive,
            isPreferred: vendor.isPreferred,
            preferredSince: vendor.preferredSince ?? '',
            accreditationBody: vendor.accreditationBody ?? '',
            accreditationNumber: vendor.accreditationNumber ?? '',
            accreditationStatus: vendor.accreditationStatus ?? '',
            accreditationExpiryDate: vendor.accreditationExpiryDate ?? '',
            primaryContactName: vendor.primaryContactName ?? '',
            primaryContactEmail: vendor.primaryContactEmail ?? '',
            primaryContactPhone: vendor.primaryContactPhone ?? '',
            website: vendor.website ?? '',
            address: vendor.address ?? '',
            currency: vendor.currency,
            defaultDailyRate: vendor.defaultDailyRate ?? undefined,
            contractReference: vendor.contractReference ?? '',
            contractStartDate: vendor.contractStartDate ?? '',
            contractEndDate: vendor.contractEndDate ?? '',
            notes: vendor.notes ?? '',
          }}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push('/administration/hr/training/vendors')}
        />
      )}
    </div>
  );
}
