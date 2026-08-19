'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  CheckCircle2,
  Copy,
  FileText,
  GitBranch,
  Loader2,
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
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import { workflowApiService } from '@/services/workflow-api.service';
import type { JobDescriptionStatus } from '@/types/hr/job-architecture';

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

      <Tabs defaultValue="responsibilities">
        <TabsList className="flex-wrap">
          <TabsTrigger value="responsibilities">
            Responsibilities ({jd.responsibilities?.length ?? 0})
          </TabsTrigger>
          <TabsTrigger value="qualifications">
            Qualifications ({jd.qualifications?.length ?? 0})
          </TabsTrigger>
          <TabsTrigger value="competencies">Competencies ({jd.competencies?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="conditions">
            {/* ⚠ `workingConditions` on the DTO; the entity navigation is JobWorkingConditions. */}
            Conditions ({(jd.physicalDemands?.length ?? 0) + (jd.workingConditions?.length ?? 0)})
          </TabsTrigger>
          <TabsTrigger value="equipment">Equipment ({jd.equipmentTools?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="relationships">
            Relationships ({jd.reportingRelationships?.length ?? 0})
          </TabsTrigger>
          <TabsTrigger value="duties">Duties ({jd.dutyItems?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="safety">
            Safety &amp; medical ({(jd.ppeRequirements?.length ?? 0) + (jd.medicalRequirements?.length ?? 0)})
          </TabsTrigger>
          <TabsTrigger value="valuation">Valuation</TabsTrigger>
        </TabsList>

        <TabsContent value="responsibilities">
          <Panel empty={!jd.responsibilities?.length} emptyText="No responsibilities recorded yet.">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Responsibility</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead className="text-right">% of time</TableHead>
                  <TableHead>KPIs</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {jd.responsibilities.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell className="max-w-md">{r.responsibilityDescription}</TableCell>
                    <TableCell>{r.type}</TableCell>
                    <TableCell className="text-right">{r.percentageOfTime ?? '—'}</TableCell>
                    <TableCell className="text-xs text-muted-foreground">
                      {(r.kpis ?? []).length === 0
                        ? '—'
                        : (r.kpis ?? []).map((k) => k.kpiStatement).join('; ')}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Panel>
        </TabsContent>

        <TabsContent value="qualifications">
          <Panel empty={!jd.qualifications?.length} emptyText="No qualifications recorded yet.">
            <SimpleTable
              head={['Qualification', 'Type', 'Required']}
              rows={(jd.qualifications ?? []).map((q) => [q.title, q.type, q.isRequired ? 'Yes' : 'Desirable'])}
            />
          </Panel>
        </TabsContent>

        <TabsContent value="competencies">
          <Panel empty={!jd.competencies?.length} emptyText="No competencies recorded yet.">
            <SimpleTable
              head={['Competency', 'Type', 'Required level', 'Critical']}
              rows={(jd.competencies ?? []).map((c) => [
                c.competencyName,
                c.type,
                c.requiredLevel,
                c.isCritical ? 'Yes' : '—',
              ])}
            />
          </Panel>
        </TabsContent>

        <TabsContent value="conditions">
          <Panel
            empty={!jd.physicalDemands?.length && !jd.workingConditions?.length}
            emptyText="No physical demands or working conditions recorded yet."
          >
            <div className="space-y-6">
              {!!jd.physicalDemands?.length && (
                <SimpleTable
                  caption="Physical demands"
                  head={['Demand', 'Frequency', 'Essential']}
                  rows={jd.physicalDemands.map((d) => [
                    d.demandDescription,
                    d.frequency,
                    d.isEssential ? 'Yes' : '—',
                  ])}
                />
              )}
              {!!jd.workingConditions?.length && (
                <SimpleTable
                  caption="Working conditions"
                  head={['Environment', 'Exposure', 'PPE', 'Travel %']}
                  rows={jd.workingConditions.map((w) => [
                    w.description,
                    w.exposureLevel,
                    w.requiresPPE ? 'Required' : '—',
                    w.travelPercentage == null ? '—' : String(w.travelPercentage),
                  ])}
                />
              )}
            </div>
          </Panel>
        </TabsContent>

        <TabsContent value="equipment">
          <Panel empty={!jd.equipmentTools?.length} emptyText="No equipment or tools recorded yet.">
            <SimpleTable
              head={['Item', 'Type', 'Proficiency', 'Training']}
              rows={(jd.equipmentTools ?? []).map((e) => [
                e.itemName,
                e.type,
                e.requiredProficiency,
                (e.trainingRequirements ?? []).map((t) => t.requirementText).join('; ') || '—',
              ])}
            />
          </Panel>
        </TabsContent>

        <TabsContent value="relationships">
          <Panel empty={!jd.reportingRelationships?.length} emptyText="No reporting relationships recorded yet.">
            <SimpleTable
              head={['Role', 'Relationship', 'Description']}
              rows={(jd.reportingRelationships ?? []).map((r) => [
                r.titleOrRole,
                r.relationshipType,
                r.description,
              ])}
            />
          </Panel>
        </TabsContent>

        <TabsContent value="duties">
          <Panel empty={!jd.dutyItems?.length} emptyText="No duty statements recorded yet.">
            <SimpleTable
              head={['#', 'Duty']}
              rows={(jd.dutyItems ?? [])
                .slice()
                .sort((a, b) => a.sequenceNumber - b.sequenceNumber)
                .map((d) => [String(d.sequenceNumber), d.dutyStatement])}
            />
          </Panel>
        </TabsContent>

        <TabsContent value="safety">
          <Panel
            empty={!jd.ppeRequirements?.length && !jd.medicalRequirements?.length}
            emptyText="No PPE or medical requirements recorded yet."
          >
            <div className="space-y-6">
              {!!jd.ppeRequirements?.length && (
                <SimpleTable
                  caption="PPE"
                  head={['Item', 'Mandatory']}
                  rows={jd.ppeRequirements.map((p) => [
                    p.customPpeName ?? 'Standard issue',
                    p.isMandatory ? 'Yes' : 'Optional',
                  ])}
                />
              )}
              {!!jd.medicalRequirements?.length && (
                <SimpleTable
                  caption="Medical"
                  head={['Requirement', 'Category', 'Mandatory']}
                  rows={jd.medicalRequirements.map((m) => [
                    m.requirementDescription,
                    m.category,
                    m.isMandatory ? 'Yes' : 'Optional',
                  ])}
                />
              )}
            </div>
          </Panel>
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

function Panel({
  empty,
  emptyText,
  children,
}: {
  empty: boolean;
  emptyText: string;
  children: React.ReactNode;
}) {
  return (
    <Card>
      <CardContent className="pt-6">
        {empty ? <EmptyState icon={FileText} title="Nothing recorded" description={emptyText} /> : children}
      </CardContent>
    </Card>
  );
}

function SimpleTable({
  head,
  rows,
  caption,
}: {
  head: string[];
  rows: React.ReactNode[][];
  caption?: string;
}) {
  return (
    <div className="space-y-2">
      {caption && <h4 className="text-sm font-medium">{caption}</h4>}
      <Table>
        <TableHeader>
          <TableRow>
            {head.map((h) => (
              <TableHead key={h}>{h}</TableHead>
            ))}
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((r, i) => (
            <TableRow key={i}>
              {r.map((c, j) => (
                <TableCell key={j}>{c}</TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
