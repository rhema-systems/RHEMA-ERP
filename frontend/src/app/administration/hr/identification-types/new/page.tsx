'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  IdentificationTypeForm,
  emptyIdentificationType,
  type IdentificationTypeFormValues,
} from '@/components/hr/lookup/IdentificationTypeForm';
import { identificationTypeService } from '@/services/hr/lookup.service';
import { countryService } from '@/services/hr/country.service';

export default function NewIdentificationTypePage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: countries } = useQuery({
    queryKey: ['hr', 'countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  const handleSubmit = async (values: IdentificationTypeFormValues) => {
    setSubmitting(true);
    try {
      await identificationTypeService.create({
        name: values.name,
        code: values.code || null,
        description: values.description || null,
        issuingAuthorityName: values.issuingAuthorityName,
        issuingCountryId: values.issuingCountryId || null,
        hasExpiryDate: values.hasExpiryDate,
        // Blank stays null, and a type that does not expire cannot carry a warning time at all —
        // the sweep would ignore it, so storing one would be a setting that only looks like a
        // feature. Both are the same instruction to the server: raise nothing.
        expiryNotificationLeadDays:
          values.hasExpiryDate && values.expiryNotificationLeadDays
            ? Number(values.expiryNotificationLeadDays)
            : null,
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'identification-types'] });
      toast({ title: 'Success', description: 'Identification type created.' });
      router.push('/administration/hr/identification-types');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create identification type.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="New Identification Type"
        description="Add an identity document type."
        backHref="/administration/hr/identification-types"
      />
      <IdentificationTypeForm
        defaultValues={emptyIdentificationType}
        countries={countries ?? []}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Type"
        onCancel={() => router.push('/administration/hr/identification-types')}
      />
    </div>
  );
}
