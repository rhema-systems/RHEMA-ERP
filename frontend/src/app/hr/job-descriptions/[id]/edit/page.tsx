'use client';

import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { HR_ROLES } from '@/components/hr/common/PermissionGate';
import { JobDescriptionForm } from '@/components/hr/job-analysis/JobDescriptionForm';
import { toFormValues, toWritePayload } from '@/components/hr/job-analysis/job-description-payload';
import { useAuth } from '@/hooks/use-auth';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import {
  AUTHORABLE_JOB_DESCRIPTION_STATUSES,
  type JobDescriptionStatus,
} from '@/types/hr/job-architecture';

/**
 * Editing a job description's OWN fields — title, summary, effective date, classification,
 * valuation, authority.
 *
 * ⚠ **`PUT descriptions/{id}` had no caller anywhere in the frontend until this screen.** Area 17
 * built the register, the detail and, in the closure slice, authoring for the twelve child
 * collections — so a saved description's duties could be rewritten while its own title could not be
 * corrected. The only route out of a typo was Duplicate.
 *
 * ⚠ **The API refuses an update once the record is Approved** (`Cannot update an approved job
 * description. Create a new version instead.`) and the child panels already stand down outside
 * Draft/UnderRevision. This screen matches that rule rather than offering a save the server rejects.
 */
export default function EditJobDescriptionPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const qc = useQueryClient();
  const { hasAnyPermission, hasAnyRole } = useAuth();

  const { data: jd, isLoading, isError, error } = useQuery({
    queryKey: ['job-description', id, 'record'],
    queryFn: () => jobArchitectureService.getJobDescription(id),
    enabled: !!id,
  });

  // Mirrors the server's own fallback: permissions resolve from the database, so on a tenant
  // provisioned before the seeder ran a user holds none at all — which is exactly what
  // HrPermissions.RoleGrants covers. Gate on permission OR role, granting what the server grants.
  const canWrite =
    hasAnyPermission(['HR.JobArchitecture.Write', 'HR.JobArchitecture.Admin']) || hasAnyRole(HR_ROLES);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }

  if (isError || !jd) {
    return (
      <div className="space-y-6">
        <PageHeader title="Job description" description="" backHref="/hr/job-descriptions" />
        <EmptyState
          icon={AlertTriangle}
          title="Could not load this job description"
          description={error instanceof Error ? error.message : 'It may have been removed.'}
        />
      </div>
    );
  }

  const status = (jd.statusName ?? jd.status) as JobDescriptionStatus;
  const authorable = AUTHORABLE_JOB_DESCRIPTION_STATUSES.includes(status);

  if (!authorable || !canWrite) {
    return (
      <div className="space-y-6">
        <PageHeader
          title={jd.jobTitle}
          description={`${jd.jobDescriptionNumber} · version ${jd.versionNumber}`}
          backHref={`/hr/job-descriptions/${id}`}
        />
        <EmptyState
          icon={AlertTriangle}
          title={authorable ? 'You cannot change this job description' : 'This job description is no longer being drafted'}
          description={
            authorable
              ? 'You can read it but not author it.'
              : 'Its content is shown as recorded. To change what the job says, create a new version from the job description itself — the one in force stays intact until the new version is approved.'
          }
        />
      </div>
    );
  }

  return (
    <JobDescriptionForm
      mode="edit"
      initial={toFormValues(jd)}
      lockedPositionTitle={jd.positionTitle}
      title={`Edit ${jd.jobDescriptionNumber}`}
      description={`${jd.positionTitle} · version ${jd.versionNumber} · ${status}`}
      backHref={`/hr/job-descriptions/${id}`}
      saveLabel="Save changes"
      onSubmit={async (values) => {
        try {
          await jobArchitectureService.updateJobDescription(id, {
            ...toWritePayload(values),
            // The route and the body are compared server-side — a mismatch is a 400, not a silent
            // write to the other record.
            id,
            // The status the screen read back. The API never writes it — transitions belong to
            // submit, review and approve — but it refuses a value that contradicts the record, so
            // sending it is how this save says "I edited the document I was shown". (It used to be
            // written, which made an omitted status bind to 0 and strand the record.)
            status,
            // ⚠ Same rule, and the one this screen nearly got wrong: `nextReviewDate` is on the
            // update DTO but on no form control. Only approval sets it
            // (`ApplyApprovalConsequencesAsync`), and an approved record cannot reach this screen —
            // so today it is always null here and dropping it would have cost nothing. That is
            // exactly the kind of "harmless" that stops being harmless the day another path sets it.
            nextReviewDate: jd.nextReviewDate ?? null,
          });
          toast.success('Job description updated');
          qc.invalidateQueries({ queryKey: ['job-description', id] });
          qc.invalidateQueries({ queryKey: ['job-descriptions'] });
          router.replace(`/hr/job-descriptions/${id}`);
        } catch (e) {
          toast.error(e instanceof Error ? e.message : 'Could not save the changes');
        }
      }}
    />
  );
}
