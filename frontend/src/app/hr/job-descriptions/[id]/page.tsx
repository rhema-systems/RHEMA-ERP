'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  CheckCircle2,
  Copy,
  GitBranch,
  Loader2,
  Lock,
  Send,
} from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { HR_ADMIN_ROLES, HR_ROLES } from '@/components/hr/common/PermissionGate';
import { DutyItemsPanel } from '@/components/hr/job-analysis/DutyItemsPanel';
import { EquipmentToolsPanel } from '@/components/hr/job-analysis/EquipmentToolsPanel';
import { JobCompetenciesPanel } from '@/components/hr/job-analysis/JobCompetenciesPanel';
import { MedicalRequirementsPanel } from '@/components/hr/job-analysis/MedicalRequirementsPanel';
import { PhysicalDemandsPanel } from '@/components/hr/job-analysis/PhysicalDemandsPanel';
import { PpeRequirementsPanel } from '@/components/hr/job-analysis/PpeRequirementsPanel';
import { QualificationsPanel } from '@/components/hr/job-analysis/QualificationsPanel';
import { ReportingRelationshipsPanel } from '@/components/hr/job-analysis/ReportingRelationshipsPanel';
import { ResponsibilitiesPanel } from '@/components/hr/job-analysis/ResponsibilitiesPanel';
import { WorkingConditionsPanel } from '@/components/hr/job-analysis/WorkingConditionsPanel';
import { useAuth } from '@/hooks/use-auth';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import { workflowApiService } from '@/services/workflow-api.service';
import {
  AUTHORABLE_JOB_DESCRIPTION_STATUSES,
  type JobDescriptionStatus,
} from '@/types/hr/job-architecture';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtMoney = (v?: number | null) =>
  v == null ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const STATUS_TONE: Record<string, string> = {
  Draft: 'bg-slate-100 text-slate-700',
  PendingReview: 'bg-amber-100 text-amber-800',
  UnderRevision: 'bg-orange-100 text-orange-800',
  Approved: 'bg-emerald-100 text-emerald-800',
  Active: 'bg-emerald-100 text-emerald-800',
  Superseded: 'bg-slate-100 text-slate-500',
  Archived: 'bg-slate-100 text-slate-500',
};

export default function JobDescriptionDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const qc = useQueryClient();
  const { hasAnyPermission, hasAnyRole } = useAuth();
  const [busy, setBusy] = useState<string | null>(null);

  const { data: jd, isLoading } = useQuery({
    queryKey: ['job-description', id],
    queryFn: () => jobArchitectureService.getJobDescriptionDetail(id),
    enabled: !!id,
  });

  /**
   * ⚠ What decides whether the approve button renders — NOT a permission check.
   *
   * Once a tenant publishes a JobDescription workflow, the approver is whoever the definition names
   * (a job-family owner, a department head) and they hold no HR permission at all; the direct
   * approve route 409s for everyone. `canCurrentUserApprove` is the only honest source for this.
   * Rendering the button on a permission would offer an action the API then refuses, which reads to
   * a user as a broken backend.
   */
  const { data: workflow } = useQuery({
    queryKey: ['job-description', id, 'workflow'],
    queryFn: () => workflowApiService.getWorkflowEntitySummary('JobDescription', id),
    enabled: !!id,
  });

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['job-description', id] });
    qc.invalidateQueries({ queryKey: ['job-descriptions'] });
  };

  const run = async (label: string, fn: () => Promise<unknown>, success: string) => {
    setBusy(label);
    try {
      await fn();
      toast.success(success);
      refresh();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : `Could not ${label}`);
    } finally {
      setBusy(null);
    }
  };

  if (isLoading || !jd) {
    return (
      <div className="flex items-center justify-center py-24 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }

  const status = (jd.statusName ?? jd.status) as JobDescriptionStatus;
  const hasWorkflow = workflow?.hasActiveInstance === true;
  const canApproveOnWorkflow = workflow?.canCurrentUserApprove === true;

  /**
   * Whether the twelve child collections may be authored.
   *
   * Two independent conditions, and they fail for different reasons:
   *
   * **Status.** Once a job description is approved it is the document in force for its position,
   * and the API will still let its duties be rewritten — not one child-collection write checks
   * status, they all check tenancy and stop. So this is the only thing standing between an
   * in-force document and a silent edit with no version and no trail. "New version" in the header
   * is the supported route, which is why the notice points at it.
   *
   * **Permission.** POST and PUT are `HR.JobArchitecture.Write`, which HR holds. Every one of the
   * twelve DELETEs is `HR.JobArchitecture.Admin`, which HR does NOT hold — see `HrStaffGrants`.
   * So an HR author adds and edits, and only an administrator removes. The delete affordance is
   * hidden rather than offered and refused.
   *
   * ⚠ The role check beside each permission is not belt-and-braces, it mirrors the API. Permissions
   * resolve from the database, so on a tenant provisioned before the seeder ran a user holds none
   * at all — which is exactly why `HrPermissions.RoleGrants` exists on the server. Gating on the
   * permission alone would black out the screen for the very tenants that fallback keeps working.
   * The two sides must grant the same thing: HR gets Write, the admin roles get Admin.
   */
  const authorableStatus = AUTHORABLE_JOB_DESCRIPTION_STATUSES.includes(status);
  const canWrite =
    hasAnyPermission(['HR.JobArchitecture.Write', 'HR.JobArchitecture.Admin']) || hasAnyRole(HR_ROLES);
  const canAuthor = authorableStatus && canWrite;
  const canDelete =
    authorableStatus &&
    (hasAnyPermission(['HR.JobArchitecture.Admin']) || hasAnyRole(HR_ADMIN_ROLES));

  const childProps = {
    jobDescriptionId: id,
    canAuthor,
    canDelete,
    invalidateKeys: [['job-description', id], ['job-descriptions']] as unknown[][],
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={jd.jobTitle}
        description={`${jd.jobDescriptionNumber} · ${jd.positionTitle} · version ${jd.versionNumber}`}
        backHref="/hr/job-descriptions"
        actions={
          <div className="flex flex-wrap gap-2">
            {status === 'Draft' || status === 'UnderRevision' ? (
              <Button
                onClick={() => run('submit', () => jobArchitectureService.submitJobDescription(id), 'Submitted for review')}
                disabled={busy !== null}
              >
                {busy === 'submit' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Send className="mr-2 h-4 w-4" />}
                Submit for review
              </Button>
            ) : null}

            {/* The engine's answer, not a permission's. */}
            {canApproveOnWorkflow && (
              <>
                <Button
                  onClick={() =>
                    run('approve', () => jobArchitectureService.approveJobDescriptionOnWorkflow(id), 'Approval recorded')
                  }
                  disabled={busy !== null}
                >
                  <CheckCircle2 className="mr-2 h-4 w-4" />
                  Approve
                </Button>
                <Button
                  variant="outline"
                  onClick={() =>
                    run(
                      'reject',
                      () => jobArchitectureService.rejectJobDescriptionOnWorkflow(id, 'Returned for revision'),
                      'Returned to its author',
                    )
                  }
                  disabled={busy !== null}
                >
                  Return for revision
                </Button>
              </>
            )}

            {status === 'Approved' && (
              <Button
                variant="outline"
                onClick={async () => {
                  const created = await jobArchitectureService.createNewVersion(id, 'Annual review');
                  toast.success(`Version ${created.versionNumber} created`);
                  router.push(`/hr/job-descriptions/${created.id}`);
                }}
                disabled={busy !== null}
              >
                <GitBranch className="mr-2 h-4 w-4" />
                New version
              </Button>
            )}

            <Button
              variant="outline"
              onClick={async () => {
                const clone = await jobArchitectureService.cloneJobDescription(id);
                toast.success('Copied');
                router.push(`/hr/job-descriptions/${clone.id}`);
              }}
              disabled={busy !== null}
            >
              <Copy className="mr-2 h-4 w-4" />
              Duplicate
            </Button>
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        <Badge className={STATUS_TONE[status] ?? 'bg-slate-100 text-slate-700'}>{status}</Badge>
        {jd.supersededByVersionId && (
          <Badge variant="outline" className="gap-1">
            <AlertTriangle className="h-3 w-3" />
            Superseded by a later version
          </Badge>
        )}
        {hasWorkflow && workflow?.currentStepName && (
          <Badge variant="outline">Awaiting: {workflow.currentStepName}</Badge>
        )}
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Summary</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="whitespace-pre-wrap text-sm">{jd.jobSummary}</p>
            <dl className="grid gap-3 sm:grid-cols-2">
              <Field label="Job family" value={jd.jobFamilyName} />
              <Field label="Sub-family" value={jd.jobSubFamilyName} />
              <Field label="Career level" value={jd.jobLevelName} />
              <Field label="Staff level" value={jd.staffLevelName} />
              <Field label="Occupation code" value={jd.occupationCode} />
              <Field label="Union" value={jd.unionName} />
            </dl>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Governance</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="space-y-3">
              <Field label="Prepared by" value={jd.preparedByName} sub={fmtDate(jd.preparedDate)} />
              <Field label="Reviewed by" value={jd.reviewedByName} sub={fmtDate(jd.reviewedDate)} />
              <Field label="Approved by" value={jd.approvedByName} sub={fmtDate(jd.approvalDate)} />
              <Field label="Effective" value={fmtDate(jd.effectiveDate)} />
              <Field
                label="Next review"
                value={fmtDate(jd.nextReviewDate)}
                sub={`every ${jd.reviewCycleMonths} months`}
              />
            </dl>
          </CardContent>
        </Card>
      </div>

      {!authorableStatus && (
        <div className="flex items-start gap-3 rounded-md border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
          <Lock className="mt-0.5 h-4 w-4 shrink-0" />
          <div>
            <p className="font-medium">This job description is no longer being drafted.</p>
            <p>
              Its content is shown as recorded. To change what the job says, create a new version —
              the one in force stays intact and is retired when the new version is approved.
            </p>
          </div>
        </div>
      )}

      {authorableStatus && !canWrite && (
        <div className="flex items-start gap-3 rounded-md border bg-muted/40 p-4 text-sm text-muted-foreground">
          <Lock className="mt-0.5 h-4 w-4 shrink-0" />
          <p>You can read this job description but not change it.</p>
        </div>
      )}

      <Tabs defaultValue="responsibilities">
        <TabsList className="flex-wrap">
          <TabsTrigger value="responsibilities">
            Responsibilities ({jd.responsibilities?.length ?? 0})
          </TabsTrigger>
          <TabsTrigger value="duties">Duties ({jd.dutyItems?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="qualifications">
            Qualifications ({jd.qualifications?.length ?? 0})
          </TabsTrigger>
          <TabsTrigger value="competencies">Competencies ({jd.competencies?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="demands">
            Physical demands ({jd.physicalDemands?.length ?? 0})
          </TabsTrigger>
          <TabsTrigger value="conditions">
            {/* ⚠ `workingConditions` on the DTO; the entity navigation is JobWorkingConditions. */}
            Working conditions ({jd.workingConditions?.length ?? 0})
          </TabsTrigger>
          <TabsTrigger value="equipment">Equipment ({jd.equipmentTools?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="relationships">
            Relationships ({jd.reportingRelationships?.length ?? 0})
          </TabsTrigger>
          <TabsTrigger value="ppe">PPE ({jd.ppeRequirements?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="medical">
            Medical ({jd.medicalRequirements?.length ?? 0})
          </TabsTrigger>
          <TabsTrigger value="valuation">Valuation</TabsTrigger>
        </TabsList>

        {/*
          Each panel reads its own collection rather than slicing the `details` payload, because
          authoring needs a list it can refetch after a write. The counts on the triggers above
          still come from `details`, so every panel invalidates it — otherwise a tab would say
          "Duties (3)" over a table showing four.
        */}

        <TabsContent value="responsibilities" className="pt-4">
          <ResponsibilitiesPanel {...childProps} />
        </TabsContent>

        <TabsContent value="duties" className="pt-4">
          <DutyItemsPanel {...childProps} />
        </TabsContent>

        <TabsContent value="qualifications" className="pt-4">
          <QualificationsPanel {...childProps} />
        </TabsContent>

        <TabsContent value="competencies" className="pt-4">
          <JobCompetenciesPanel {...childProps} />
        </TabsContent>

        <TabsContent value="demands" className="pt-4">
          <PhysicalDemandsPanel {...childProps} />
        </TabsContent>

        <TabsContent value="conditions" className="pt-4">
          <WorkingConditionsPanel {...childProps} />
        </TabsContent>

        <TabsContent value="equipment" className="pt-4">
          <EquipmentToolsPanel {...childProps} />
        </TabsContent>

        <TabsContent value="relationships" className="pt-4">
          <ReportingRelationshipsPanel {...childProps} />
        </TabsContent>

        <TabsContent value="ppe" className="pt-4">
          <PpeRequirementsPanel {...childProps} />
        </TabsContent>

        <TabsContent value="medical" className="pt-4">
          <MedicalRequirementsPanel {...childProps} />
        </TabsContent>

        <TabsContent value="valuation">
          <Card>
            <CardContent className="grid gap-4 pt-6 sm:grid-cols-2">
              <Field label="Criticality" value={jd.roleCriticalityName ?? jd.roleCriticality} />
              <Field label="Intrinsic value" value={fmtMoney(jd.roleIntrinsicValue)} />
              <Field label="Benchmark salary" value={fmtMoney(jd.industryBenchmarkSalary)} />
              <Field
                label="Estimated range"
                value={
                  jd.estimatedSalaryLow == null && jd.estimatedSalaryHigh == null
                    ? '—'
                    : `${fmtMoney(jd.estimatedSalaryLow)} – ${fmtMoney(jd.estimatedSalaryHigh)}`
                }
              />
              {/* Payroll owns the grade store; this is a suggestion, never an assignment. */}
              <Field label="Suggested salary grade" value={jd.suggestedSalaryGradeName} />
              <Field label="Autonomy" value={jd.autonomyLevel} />
              <Field label="Decision scope" value={jd.decisionMakingScope} />
              <Field label="Financial authority" value={fmtMoney(jd.financialAuthorityLimit)} />
              {jd.valuationNotes && (
                <div className="sm:col-span-2">
                  <div className="text-xs text-muted-foreground">Notes</div>
                  <p className="whitespace-pre-wrap text-sm">{jd.valuationNotes}</p>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}

function Field({ label, value, sub }: { label: string; value?: React.ReactNode; sub?: string }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="text-sm font-medium">{value || '—'}</dd>
      {sub && <dd className="text-xs text-muted-foreground">{sub}</dd>}
    </div>
  );
}
