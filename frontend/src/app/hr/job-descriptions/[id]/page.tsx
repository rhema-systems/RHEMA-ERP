'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  CheckCircle2,
  Copy,
  GitBranch,
  Loader2,
  Lock,
  Calculator,
  Pencil,
  Send,
} from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
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
import { employeePositionService } from '@/services/hr/employee-position.service';
import { workflowApiService } from '@/services/workflow-api.service';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
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
  // Round 3, lane J1: "Duplicate" asks where the copy goes — this position (a "(Copy)" draft) or
  // another one (title kept, staff level from the target, no reporting relationships).
  const [duplicateOpen, setDuplicateOpen] = useState(false);
  const [duplicateTarget, setDuplicateTarget] = useState<string>('');
  const { data: allPositions } = useQuery({
    queryKey: ['positions', 'all'],
    queryFn: () => employeePositionService.getAll(),
    enabled: duplicateOpen,
  });

  const {
    data: jd,
    isLoading,
    isError,
    error,
  } = useQuery({
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

  /**
   * What the role is worth as of now — the breakdown behind the stored figures.
   *
   * It reads freely because the GET is safe: it computes and returns and changes nothing. That was
   * not true before 2026-09-07, when the same endpoint persisted its result, and a query like this
   * one would have rewritten the record on every mount and refocus. Storing is now its own POST.
   */
  const { data: valuation } = useQuery({
    queryKey: ['job-description', id, 'valuation'],
    queryFn: () => jobArchitectureService.getValuation(id),
    enabled: !!id,
  });

  /**
   * Every version this position has had.
   *
   * ⚠ `getVersionHistory` and `getJobDescriptionsForPosition` had NO caller anywhere in the
   * frontend — the screen talked about versions constantly (a version number in the header, "New
   * version" in the toolbar, a supersession badge) and offered no way to see the others. The two
   * endpoints also run the identical query: same filter, same ordering, both returning summaries.
   */
  const positionId = jd?.positionId;
  const { data: versions } = useQuery({
    queryKey: ['job-description', id, 'versions', positionId],
    queryFn: () => jobArchitectureService.getVersionHistory(positionId as string),
    enabled: !!positionId,
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

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }

  /**
   * ⚠ A failed read used to fall into the branch above and spin for ever — `isLoading || !jd` is
   * true for a 404 and for a 500 alike, so the screen said "Loading…" about a request that had
   * already come back and failed. Someone arriving here from the register reads that as a dead
   * link rather than as an error they can act on, and it hides the one detail that matters: which
   * request failed and why.
   */
  if (isError || !jd) {
    return (
      <div className="space-y-6">
        <PageHeader title="Job description" description="" backHref="/hr/job-descriptions" />
        <EmptyState
          icon={AlertTriangle}
          title="Could not load this job description"
          description={
            error instanceof Error
              ? error.message
              : 'It may have been removed, or you may not have permission to read it.'
          }
        />
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

  /**
   * Whether the estimate ON the record still matches what the job adds up to today.
   *
   * Worth showing rather than hiding behind the button: the stored figures are what every other
   * screen and report reads, and a qualification added or re-valued this morning does not change
   * them until someone stores a new valuation. Comparing the live computation with the stored one
   * is only possible at all because the read no longer writes — before the split the two could
   * never disagree, since reading rewrote the record to match.
   */
  const valuationIsStale =
    !!valuation &&
    ((valuation.estimatedSalaryLow ?? null) !== (jd.estimatedSalaryLow ?? null) ||
      (valuation.estimatedSalaryHigh ?? null) !== (jd.estimatedSalaryHigh ?? null) ||
      (valuation.suggestedSalaryGradeId ?? null) !== (jd.suggestedSalaryGradeId ?? null));

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
            {/*
              The title, summary, classification and valuation live on the record itself, not in the
              tabs below — and until this button existed there was no way back to them. `canAuthor`
              is the same gate the tabs use: Draft or UnderRevision, and Write.
            */}
            {canAuthor && (
              <Button variant="outline" onClick={() => router.push(`/hr/job-descriptions/${id}/edit`)}>
                <Pencil className="mr-2 h-4 w-4" />
                Edit details
              </Button>
            )}

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

            {(status === 'Draft' || status === 'UnderRevision') && (
              <Button
                variant="outline"
                onClick={async () => {
                  setBusy('import');
                  try {
                    const r = await jobArchitectureService.importPositionRequirements(id);
                    const n = r.competenciesAdded + r.qualificationsAdded;
                    toast.success(n > 0 ? `Brought in ${n} requirement${n === 1 ? '' : 's'} from the position` : 'Nothing to bring in — every requirement is already here');
                    qc.invalidateQueries({ queryKey: ['job-descriptions', id] });
                    qc.invalidateQueries({ queryKey: ['job-architecture'] });
                  } catch (e: any) {
                    toast.error(e?.message ?? 'Could not bring in the requirements');
                  } finally {
                    setBusy(null);
                  }
                }}
                disabled={busy !== null}
              >
                Bring in the position&apos;s requirements
              </Button>
            )}

            <Button variant="outline" onClick={() => { setDuplicateTarget(''); setDuplicateOpen(true); }} disabled={busy !== null}>
              <Copy className="mr-2 h-4 w-4" />
              Duplicate
            </Button>

            <Dialog open={duplicateOpen} onOpenChange={setDuplicateOpen}>
              <DialogContent className="sm:max-w-[520px]">
                <DialogHeader>
                  <DialogTitle>Duplicate this job description</DialogTitle>
                  <DialogDescription>
                    On the same position it becomes a &quot;(Copy)&quot; draft. On another position it keeps
                    its title, takes that position&apos;s staff level, starts its own version line, and
                    carries no reporting relationships.
                  </DialogDescription>
                </DialogHeader>
                <div className="space-y-1.5 py-2">
                  <Label>Copy onto</Label>
                  <Select value={duplicateTarget || '__same__'} onValueChange={(v) => setDuplicateTarget(v === '__same__' ? '' : v)}>
                    <SelectTrigger>
                      <SelectValue placeholder="This position" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="__same__">This position (a copy)</SelectItem>
                      {(allPositions ?? [])
                        .filter((p) => p.id !== jd.positionId)
                        .map((p) => (
                          <SelectItem key={p.id} value={p.id}>
                            {p.title}
                          </SelectItem>
                        ))}
                    </SelectContent>
                  </Select>
                </div>
                <DialogFooter>
                  <Button variant="outline" onClick={() => setDuplicateOpen(false)}>Cancel</Button>
                  <Button
                    onClick={async () => {
                      setBusy('clone');
                      try {
                        const clone = await jobArchitectureService.cloneJobDescription(id, duplicateTarget || null);
                        toast.success(duplicateTarget ? 'Copied onto the other position' : 'Copied');
                        setDuplicateOpen(false);
                        router.push(`/hr/job-descriptions/${clone.id}`);
                      } catch (e: any) {
                        toast.error(e?.message ?? 'Could not copy');
                      } finally {
                        setBusy(null);
                      }
                    }}
                    disabled={busy !== null}
                  >
                    Duplicate
                  </Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        <Badge className={STATUS_TONE[status] ?? 'bg-slate-100 text-slate-700'}>{status}</Badge>
        {/* The record names its successor, so the badge is a way there rather than a dead end:
            someone told their description is superseded needs the one that replaced it. */}
        {jd.supersededByVersionId && (
          <Link href={`/hr/job-descriptions/${jd.supersededByVersionId}`}>
            <Badge variant="outline" className="gap-1 hover:bg-muted">
              <AlertTriangle className="h-3 w-3" />
              Superseded — open the version that replaced it
            </Badge>
          </Link>
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
              {/* The only place engagement type is recorded at all — the position has no such column. */}
              <Field label="Employment type" value={jd.intendedEmploymentType} />
              {/* ⚠ The FLAG, not just the union. `IsBargainingUnitRole` routes the approval and
                  prints the offer letter's bargaining-unit clause, and a role can carry it with no
                  union named — in which case showing only the union showed nothing at all. */}
              <Field
                label="Bargaining unit"
                value={jd.isBargainingUnitRole ? 'Covered by a CBA' : 'Not covered'}
              />
              <Field label="Union" value={jd.unionName} />
            </dl>

            {/* Settable since the port, displayed nowhere until now. It is the ADA-shaped
                statement — what the role cannot be performed without — so it belongs beside the
                summary rather than buried in a tab. */}
            {jd.essentialFunctionsSummary && (
              <div>
                <div className="text-xs text-muted-foreground">Essential functions</div>
                <p className="whitespace-pre-wrap text-sm">{jd.essentialFunctionsSummary}</p>
              </div>
            )}
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
              {/* Null until it is superseded — or until someone sets an expiry deliberately. Past
                  this date the description is no longer the position's current one. */}
              <Field
                label="Expires"
                value={jd.expiryDate ? fmtDate(jd.expiryDate) : 'Does not expire'}
              />
              <Field
                label="Next review"
                value={fmtDate(jd.nextReviewDate)}
                sub={`every ${jd.reviewCycleMonths} months`}
              />
              {/* Why this version exists. Set by "New version" and editable afterwards — and the
                  one field on the record that explains the version number beside it. */}
              <Field label="Reason for this version" value={jd.revisionReason} />
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
          <TabsTrigger value="versions">Versions ({versions?.length ?? 0})</TabsTrigger>
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

        <TabsContent value="valuation" className="space-y-4 pt-4">
          {/*
            What the valuation is FOR: the money entered against each qualification and competency,
            plus the role's intrinsic value, blended with any industry benchmark, banded at ±10%
            and matched to a salary grade. Until this button existed the endpoint that does it had
            no caller anywhere, so the estimated range below could never be anything but a dash and
            the monetary values the panels collect fed nothing at all.

            ⚠ Offered only to an author of a draft. The API gates it on Read even though it WRITES
            the result onto the record — so a read-only user could overwrite the stored figures of
            an in-force document. This is the narrower rule the server ought to be holding.
          */}
          {canAuthor && (
            <div
              className={`flex flex-wrap items-center justify-between gap-3 rounded-md border p-4 ${
                valuationIsStale ? 'border-amber-200 bg-amber-50' : 'bg-muted/40'
              }`}
            >
              <p className={`text-sm ${valuationIsStale ? 'text-amber-900' : 'text-muted-foreground'}`}>
                {valuationIsStale
                  ? 'The figures above are not what the job now adds up to — the qualifications, competencies or values have changed since the estimate was stored.'
                  : 'The stored estimate matches what the job currently adds up to.'}
              </p>
              <Button
                variant="outline"
                disabled={busy !== null}
                onClick={() =>
                  run(
                    'store the valuation',
                    () => jobArchitectureService.recalculateValuation(id),
                    'Valuation stored on the job description',
                  )
                }
              >
                {busy === 'store the valuation' ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Calculator className="mr-2 h-4 w-4" />
                )}
                {valuationIsStale ? 'Store the new estimate' : 'Recalculate'}
              </Button>
            </div>
          )}

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
              <Field label="Approval authority" value={jd.approvalAuthorityNotes} />
              {jd.valuationNotes && (
                <div className="sm:col-span-2">
                  <div className="text-xs text-muted-foreground">Notes</div>
                  <p className="whitespace-pre-wrap text-sm">{jd.valuationNotes}</p>
                </div>
              )}
            </CardContent>
          </Card>

          {/* Where the figures above came from — shown only for a valuation this screen just ran,
              because the breakdown is not stored on the record. */}
          {valuation && (
            <Card>
              <CardHeader>
                <CardTitle>How that was worked out</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <dl className="grid gap-3 sm:grid-cols-4">
                  <Field label="Qualifications" value={fmtMoney(valuation.totalQualificationValue)} />
                  <Field label="Competencies" value={fmtMoney(valuation.totalCompetencyValue)} />
                  <Field label="Role intrinsic value" value={fmtMoney(valuation.roleIntrinsicValue)} />
                  <Field label="Total" value={fmtMoney(valuation.totalEstimatedValue)} />
                </dl>

                {valuation.suggestedGradeMinSalary != null && (
                  <p className="text-sm text-muted-foreground">
                    Matched to {valuation.suggestedSalaryGradeName} (
                    {fmtMoney(valuation.suggestedGradeMinSalary)} –{' '}
                    {fmtMoney(valuation.suggestedGradeMaxSalary)}). The post&rsquo;s actual grade is set
                    on the position, not here.
                  </p>
                )}

                <ValuationLines title="Qualifications" lines={valuation.qualificationLines} />
                <ValuationLines title="Competencies" lines={valuation.competencyLines} />
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="versions" className="pt-4">
          <Card>
            <CardHeader>
              <CardTitle>Every version of this position&rsquo;s job description</CardTitle>
            </CardHeader>
            <CardContent>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-0">Version</TableHead>
                    <TableHead>Number</TableHead>
                    <TableHead>Job title</TableHead>
                    <TableHead>Effective</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {(versions ?? []).map((v) => {
                    const vStatus = (v.statusName ?? v.status) as JobDescriptionStatus;
                    const isThisOne = v.id === id;
                    return (
                      <TableRow
                        key={v.id}
                        className={isThisOne ? 'bg-muted/50' : 'cursor-pointer'}
                        onClick={isThisOne ? undefined : () => router.push(`/hr/job-descriptions/${v.id}`)}
                      >
                        <TableCell className="font-mono">v{v.versionNumber}</TableCell>
                        <TableCell className="font-mono text-xs">{v.jobDescriptionNumber}</TableCell>
                        <TableCell className="font-medium">{v.jobTitle}</TableCell>
                        <TableCell>{fmtDate(v.effectiveDate)}</TableCell>
                        <TableCell>
                          <Badge className={STATUS_TONE[vStatus] ?? 'bg-slate-100 text-slate-700'}>
                            {vStatus}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-right text-xs text-muted-foreground">
                          {/* "In force" is read off the status rather than off supersession: the
                              summary projection carries no SupersededByVersionId. Two rows showing
                              it at once is not a rendering bug — it is the supersession defect
                              OfferLetterService trips over, and worth seeing. */}
                          {isThisOne && <span className="font-medium text-foreground">You are here</span>}
                          {!isThisOne && (vStatus === 'Approved' || vStatus === 'Active') && 'In force'}
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>

              {(versions ?? []).length <= 1 && (
                <p className="pt-4 text-sm text-muted-foreground">
                  This is the only description the position has ever had. Approving a successor
                  retires it automatically.
                </p>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}

/**
 * The rows a valuation added up. A line carrying no money is shown as such rather than skipped —
 * a qualification nobody has valued is exactly why a total comes out lower than expected.
 */
function ValuationLines({
  title,
  lines,
}: {
  title: string;
  lines: { id: string; name: string; monetaryValue?: number | null }[];
}) {
  if (!lines.length) return null;
  return (
    <div>
      <div className="mb-1 text-xs text-muted-foreground">{title}</div>
      <ul className="divide-y rounded-md border text-sm">
        {lines.map((l) => (
          <li key={l.id} className="flex items-center justify-between px-3 py-2">
            <span>{l.name}</span>
            <span className={l.monetaryValue == null ? 'text-muted-foreground' : 'font-medium'}>
              {l.monetaryValue == null ? 'not valued' : fmtMoney(l.monetaryValue)}
            </span>
          </li>
        ))}
      </ul>
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
