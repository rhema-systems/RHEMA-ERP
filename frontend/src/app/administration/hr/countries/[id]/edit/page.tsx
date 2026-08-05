'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { CountryForm, type CountryFormValues } from '@/components/hr/country/CountryForm';
import { countryService } from '@/services/hr/country.service';

export default function EditCountryPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: country, isLoading, isError } = useQuery({
    queryKey: ['hr', 'countries', id],
    queryFn: () => countryService.getById(id),
    enabled: !!id,
  });

  const handleSubmit = async (values: CountryFormValues) => {
    setSubmitting(true);
    try {
      await countryService.update(id, {
        id,
        name: values.name,
        code: values.code,
        alpha2Code: values.alpha2Code ?? '',
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'countries'] });
      toast({ title: 'Success', description: 'Country updated.' });
      router.push('/administration/hr/countries');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update country.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Edit Country"
        description={country ? country.name : 'Update this country.'}
        backHref="/administration/hr/countries"
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !country ? (
        <EmptyState title="Country not found" description="This country may have been deleted." />
      ) : (
        <CountryForm
          defaultValues={{
            name: country.name,
            code: country.code,
            alpha2Code: country.alpha2Code ?? '',
            isActive: country.isActive,
          }}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push('/administration/hr/countries')}
        />
      )}
    </div>
  );
}
