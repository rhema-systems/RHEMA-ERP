'use client';

import { useEffect } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  CandidateFormFields,
  candidateSchema,
  emptyCandidate,
  type CandidateFormValues,
} from '@/components/hr/recruitment/CandidateFormFields';
import { useToast } from '@/hooks/use-toast';
import { jobCandidateService } from '@/services/hr/recruitment-pipeline.service';
import type { Gender } from '@/types/hr/recruitment-pipeline';

export default function EditCandidatePage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'candidate', id],
    queryFn: () => jobCandidateService.getById(id),
    enabled: !!id,
  });

  const form = useForm<CandidateFormValues>({
    resolver: zodResolver(candidateSchema),
    defaultValues: emptyCandidate,
  });

  useEffect(() => {
    if (!data) return;
    form.reset({
      firstName: data.firstName,
      middleName: data.middleName ?? null,
      lastName: data.lastName,
      // The API returns a full timestamp; <input type="date"> needs the date part only.
      dateOfBirth: data.dateOfBirth?.slice(0, 10) ?? '',
      gender: data.gender,
      email: data.email,
      phone: data.phone,
      alternatePhone: data.alternatePhone ?? null,
      postalAddress: data.postalAddress ?? null,
      digitalAddress: data.digitalAddress ?? null,
      city: data.city,
      // Optional on the read since slice 13b (internal shadow candidates carry no country), but
      // still required on this form — HR filling in a candidate record must name one.
      countryId: data.countryId ?? '',
      linkedInProfile: data.linkedInProfile ?? null,
      portfolioUrl: data.portfolioUrl ?? null,
      gitHubUrl: data.gitHubUrl ?? null,
      nationalIdTypeId: data.nationalIdTypeId ?? null,
      nationalIdNumber: data.nationalIdNumber ?? null,
      nationalIdExpiryDate: data.nationalIdExpiryDate?.slice(0, 10) ?? null,
      isInTalentPool: data.isInTalentPool,
    });
  }, [data, form]);

  const save = useMutation({
    mutationFn: (values: CandidateFormValues) =>
      jobCandidateService.update(id, {
        ...values,
        id,
        gender: values.gender as Gender,
        middleName: values.middleName || null,
        alternatePhone: values.alternatePhone || null,
        postalAddress: values.postalAddress || null,
        digitalAddress: values.digitalAddress || null,
        linkedInProfile: values.linkedInProfile || null,
        portfolioUrl: values.portfolioUrl || null,
        gitHubUrl: values.gitHubUrl || null,
        nationalIdTypeId: values.nationalIdTypeId || null,
        nationalIdNumber: values.nationalIdNumber?.trim() || null,
        nationalIdExpiryDate: values.nationalIdExpiryDate || null,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'candidate', id] });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'candidate-detail', id] });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'candidates'] });
      toast({ title: 'Candidate saved' });
      router.push(`/hr/recruitment/candidates/${id}`);
    },
    onError: (e: any) => toast({ title: 'Could not save', description: e?.message, variant: 'destructive' }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !data) {
    return <EmptyState title="Candidate not found" description="It may have been removed." />;
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={`Edit ${data.fullName}`}
        description="The candidate's own professional profile is not editable here — it stays as they supplied it."
        backHref={`/hr/recruitment/candidates/${id}`}
        actions={
          <Button onClick={form.handleSubmit((v) => save.mutate(v))} disabled={save.isPending}>
            {save.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            Save
          </Button>
        }
      />
      <CandidateFormFields form={form} />
    </div>
  );
}
