'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  ExternalAssociateForm,
  emptyExternalAssociate,
  type ExternalAssociateFormValues,
} from '@/components/hr/external-associates/ExternalAssociateForm';
import { externalAssociateService } from '@/services/hr/external-associate.service';

export default function NewExternalAssociatePage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: ExternalAssociateFormValues) => {
    setSubmitting(true);
    try {
      // No associateNumber here on purpose: it is minted server-side, and since slice 8 it is never
      // reissued — a deleted associate's EXT-nnnn stays retired, because it is the identity under
      // which a panel decision was recorded.
      const created = await externalAssociateService.create({
        title: values.title || null,
        firstName: values.firstName,
        middleName: values.middleName || '',
        lastName: values.lastName,
        email: values.email,
        phoneNumber: values.phoneNumber,
        companyName: values.companyName || null,
        role: values.role || null,
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'external-associates'] });
      toast({
        title: 'Associate added',
        description: `${created.fullName} was added as ${created.associateNumber}.`,
      });
      router.push(`/administration/hr/external-associates/${created.id}`);
    } catch (error) {
      // A duplicate email comes back as a 409 with the sentence the service wrote.
      toast({
        title: 'Could not add the associate',
        description: (error as Error)?.message || 'Failed to add the associate.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="New External Associate"
        description="Add someone who acts for the organisation without an ERP login."
        backHref="/administration/hr/external-associates"
      />
      <ExternalAssociateForm
        defaultValues={emptyExternalAssociate}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Add Associate"
        onCancel={() => router.push('/administration/hr/external-associates')}
      />
    </div>
  );
}
