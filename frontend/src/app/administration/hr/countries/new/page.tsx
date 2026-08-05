'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { CountryForm, emptyCountry, type CountryFormValues } from '@/components/hr/country/CountryForm';
import { countryService } from '@/services/hr/country.service';

export default function NewCountryPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: CountryFormValues) => {
    setSubmitting(true);
    try {
      await countryService.create({
        name: values.name,
        code: values.code,
        alpha2Code: values.alpha2Code ?? '',
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'countries'] });
      toast({ title: 'Success', description: 'Country created.' });
      router.push('/administration/hr/countries');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create country.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Country"
        description="Add a country to the reference list."
        backHref="/administration/hr/countries"
      />
      <CountryForm
        defaultValues={emptyCountry}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Country"
        onCancel={() => router.push('/administration/hr/countries')}
        showActive={false}
      />
    </div>
  );
}
