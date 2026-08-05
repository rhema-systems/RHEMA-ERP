'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  IdentificationTypeForm,
  type IdentificationTypeFormValues,
} from '@/components/hr/lookup/IdentificationTypeForm';
import { identificationTypeService } from '@/services/hr/lookup.service';
import { countryService } from '@/services/hr/country.service';

export default function EditIdentificationTypePage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: type, isLoading, isError } = useQuery({
    queryKey: ['hr', 'identification-types', id],
    queryFn: () => identificationTypeService.getById(id),
    enabled: !!id,
  });

  const { data: countries } = useQuery({
    queryKey: ['hr', 'countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  const handleSubmit = async (values: IdentificationTypeFormValues) => {
    setSubmitting(true);
    try {
      await identificationTypeService.update(id, {
        id,
        name: values.name,
        code: values.code || null,
        description: values.description || null,
        issuingAuthorityName: values.issuingAuthorityName,
        issuingCountryId: values.issuingCountryId || null,
        hasExpiryDate: values.hasExpiryDate,
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'identification-types'] });
      toast({ title: 'Success', description: 'Identification type updated.' });
      router.push('/administration/hr/identification-types');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update identification type.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="Edit Identification Type"
        description={type ? type.name : 'Update this identification type.'}
        backHref="/administration/hr/identification-types"
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !type ? (
        <EmptyState
          title="Identification type not found"
          description="This type may have been deleted."
        />
      ) : (
        <IdentificationTypeForm
          defaultValues={{
            name: type.name,
            code: type.code ?? '',
            issuingAuthorityName: type.issuingAuthorityName,
            issuingCountryId: type.issuingCountryId ?? '',
            description: type.description ?? '',
            hasExpiryDate: type.hasExpiryDate,
            isActive: type.isActive,
          }}
          countries={countries ?? []}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push('/administration/hr/identification-types')}
        />
      )}
    </div>
  );
}
