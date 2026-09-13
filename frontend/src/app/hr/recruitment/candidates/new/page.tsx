'use client';

import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
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

export default function NewCandidatePage() {
  const router = useRouter();
  const { toast } = useToast();

  const form = useForm<CandidateFormValues>({
    resolver: zodResolver(candidateSchema),
    defaultValues: emptyCandidate,
  });

  const create = useMutation({
    mutationFn: (values: CandidateFormValues) =>
      jobCandidateService.create({
        ...values,
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
    onSuccess: (created) => {
      toast({ title: 'Candidate created', description: created.candidateNumber });
      router.push(`/hr/recruitment/candidates/${created.id}`);
    },
    // The API refuses a duplicate email with the address in the message — worth showing verbatim.
    onError: (e: any) =>
      toast({ title: 'Could not create candidate', description: e?.message, variant: 'destructive' }),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="New candidate"
        description="Qualifications, work history, referees and documents are added once the record exists."
        backHref="/hr/recruitment/candidates"
        actions={
          <Button onClick={form.handleSubmit((v) => create.mutate(v))} disabled={create.isPending}>
            {create.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            Create
          </Button>
        }
      />
      <CandidateFormFields form={form} />
    </div>
  );
}
