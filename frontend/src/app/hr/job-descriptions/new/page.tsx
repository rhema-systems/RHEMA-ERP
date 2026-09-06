'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { JobDescriptionForm } from '@/components/hr/job-analysis/JobDescriptionForm';
import { toWritePayload } from '@/components/hr/job-analysis/job-description-payload';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';

/**
 * Authoring a new job description. The form itself is shared with the edit screen; this page owns
 * the save and where the author lands afterwards.
 *
 * ⚠ **Landing on the new record is part of the feature, not a courtesy.** Everything a job
 * description actually says — responsibilities, duties, qualifications, competencies, PPE — is
 * authored on its detail screen, so a create that leaves the author on an empty form has delivered
 * a shell and hidden the place to fill it in.
 */
export default function NewJobDescriptionPage() {
  const router = useRouter();
  const qc = useQueryClient();
  const params = useSearchParams();
  const presetPosition = params.get('positionId');

  return (
    <JobDescriptionForm
      mode="create"
      initial={presetPosition ? { positionId: presetPosition } : undefined}
      title="New job description"
      description="Describe what the position is accountable for. It takes effect once approved."
      backHref="/hr/job-descriptions"
      saveLabel="Save draft"
      onSubmit={async (values) => {
        try {
          const created = await jobArchitectureService.createJobDescription(toWritePayload(values));

          // The register is cached; without this the new draft is missing from the list the author
          // is about to be sent back to.
          qc.invalidateQueries({ queryKey: ['job-descriptions'] });

          // ⚠ An id we cannot trust must not become a URL. `/hr/job-descriptions/undefined` matches
          // the route, so the detail screen would mount, ask the API for a record that cannot exist
          // and sit there — which reads as "nothing happened" rather than as the failure it is.
          if (!created?.id) {
            toast.error('Saved, but the server did not return the new record — opening the register.');
            router.push('/hr/job-descriptions');
            return;
          }

          toast.success(`${created.jobDescriptionNumber ?? 'Job description'} created`);
          // replace, not push: Back belongs to the register, not to a form that has already saved.
          router.replace(`/hr/job-descriptions/${created.id}`);
        } catch (e) {
          toast.error(e instanceof Error ? e.message : 'Could not save the job description');
        }
      }}
    />
  );
}
