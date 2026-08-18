'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  ArrowUpRight,
  CheckCircle2,
  FileText,
  History,
  Loader2,
  Pencil,
  ShieldAlert,
  Users,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { successionService } from '@/services/hr/succession.service';
import { CandidatesPanel } from '@/components/hr/succession/CandidatesPanel';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import type { SuccessionPlan } from '@/types/hr/succession';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const READINESS_TONE: Record<string, string> = {
  ReadyNow: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200',
  ReadyIn12Months: 'bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200',
  ReadyIn24Months: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
  ReadyIn36PlusMonths: 'bg-orange-100 text-orange-800 dark:bg-orange-900/40 dark:text-orange-200',
  NotReady: 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200',
};

/** ⚠ Backwards, like succession risk: `HighRisk` means likely to leave. */
const RETENTION_TONE: Record<string, string> = {
  HighRisk: 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200',
  MediumRisk: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
  LowRisk: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200',
  Secure: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200',
};

const spaced = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z0-9])/g, '$1 $2') : '—');

function Detail({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm">{children}</dd>
    </div>
  );
}

export default function SuccessionPlanDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();

  const { data: plan, isLoading } = useQuery({
    queryKey: ['succession-plans', id],
    queryFn: () => successionService.getById(id),
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['succession-plans'] });
  };

  /**
   * ⚠ **Approval is the workflow engine's, not this page's.** Slice 8 retired the bespoke
   * submit/review/approve chain: whether a plan is out for approval, who may act on it and which
   * step it sits on are the workflow instance's business. Everything below goes through
   * `useWorkflowRecord`, and **nothing here ever writes a status** — a multi-step definition leaves
   * the plan at UnderReview after an intermediate approval, so a screen that assumed Approved would
   * be lying the moment someone configured a second step.
   *
   * The old `review` action is gone entirely. It took a caller-chosen status, which meant a
   * reviewer could move a plan straight to Approved around whatever approval was configured.
   */
  const workflow = useWorkflowRecord({
    entityType: 'SuccessionPlan',
    entityId: id,
    entityLabel: 'Succession Plan',
    entityNumber: plan?.planNumber,
    status: plan?.status ?? 'Draft',
    // Rejected is submittable too — a rejected plan is reworked and sent back, which is why
    // rejection lands there rather than bouncing to Draft.
    canSubmit: plan?.status === 'Draft' || plan?.status === 'Rejected',
    canApproveReject: plan?.status === 'UnderReview',
    enabled: !!plan,
    commands: {
      submit: () => successionService.submit(id),
      // No approver id is sent — the server resolves the approver from the token against the
      // published definition.
      approve: (ctx) =>
        successionService.approve(id, { planId: id, approvalNotes: ctx.comments || null }),
      reject: (ctx) =>
        successionService.reject(id, {
          planId: id,
          rejectionReason: ctx.comments || 'Rejected',
        }),
      afterAction: refresh,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!plan) return null;

  const p: SuccessionPlan = plan;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${p.planNumber} · ${p.positionTitle}`}
        description={p.planName}
        backHref="/hr/succession"
        actions={
          <div className="flex flex-wrap gap-2">
            {p.status !== 'Approved' && (
              <Button variant="outline" asChild>
                <Link href={`/hr/succession/${id}/edit`}>
                  <Pencil className="mr-2 h-4 w-4" />
                  Edit
                </Link>
              </Button>
            )}
            {/* Submit / approve / reject are all the engine's, rendered by the generic control. */}
            <WorkflowApprovalActions {...workflow.actionProps} />
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        <StatusBadge status={p.status} />
        <Badge variant="outline">{p.criticality} criticality</Badge>
        <Badge variant="outline">{spaced(p.riskLevel)}</Badge>
        <Badge variant="outline">
          Version {p.versionNumber}
          {p.isActiveVersion ? ' · active' : ''}
        </Badge>
        {p.supersededByPlanId && (
          <Button variant="link" size="sm" asChild className="h-auto p-0">
            <Link href={`/hr/succession/${p.supersededByPlanId}`}>
              Superseded by {p.supersededByPlanNumber ?? 'a later plan'}
              <ArrowUpRight className="ml-1 h-3 w-3" />
            </Link>
          </Button>
        )}
      </div>

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="candidates">
            Successors ({p.candidates.length})
          </TabsTrigger>
          <TabsTrigger value="competencies">
            Competencies ({p.competencyRequirements.length})
          </TabsTrigger>
          <TabsTrigger value="actions">Actions ({p.actions.length})</TabsTrigger>
          <TabsTrigger value="documents">Documents ({p.documents.length})</TabsTrigger>
          <WorkflowTabTrigger value="workflow" />
        </TabsList>

        <TabsContent value="overview" className="space-y-4 pt-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">The post and its incumbent</CardTitle>
            </CardHeader>
            <CardContent>
              <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                <Detail label="Position">{p.positionTitle || '—'}</Detail>
                <Detail label="Plan year">{p.planYear}</Detail>
                <Detail label="Incumbent">
                  {p.currentIncumbentName ?? <span className="text-muted-foreground">Vacant</span>}
                  {p.currentIncumbentNumber && (
                    <span className="text-muted-foreground"> · {p.currentIncumbentNumber}</span>
                  )}
                </Detail>
                <Detail label="Incumbent age">{p.incumbentAge ?? '—'}</Detail>
                <Detail label="Service years left">{p.incumbentServiceYearsLeft ?? '—'}</Detail>
                <Detail label="Effective retirement">
                  {fmtDate(p.incumbentEffectiveRetirementDate)}
                </Detail>
                <Detail label="Retirement date">{fmtDate(p.incumbentRetirementDate)}</Detail>
                <Detail label="Anticipated vacancy">{fmtDate(p.anticipatedVacancyDate)}</Detail>
                <Detail label="Reason">{spaced(p.anticipatedVacancyReason)}</Detail>
              </dl>
              {p.incumbentSuccessionNotes && (
                <p className="mt-4 whitespace-pre-wrap text-sm">{p.incumbentSuccessionNotes}</p>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <AlertTriangle className="h-4 w-4" />
                Risk
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <dl className="grid gap-4 sm:grid-cols-3">
                <Detail label="Succession risk">{spaced(p.riskLevel)}</Detail>
                <Detail label="Criticality">{p.criticality}</Detail>
                <Detail label="Months to ready">{p.estimatedTimeToReadyMonths ?? '—'}</Detail>
              </dl>
              {p.riskAssessmentNotes && (
                <p className="whitespace-pre-wrap text-sm">{p.riskAssessmentNotes}</p>
              )}
              {p.businessImpactIfVacant && (
                <div>
                  <p className="text-xs uppercase tracking-wide text-muted-foreground">
                    Impact if vacant
                  </p>
                  <p className="mt-1 whitespace-pre-wrap text-sm">{p.businessImpactIfVacant}</p>
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <ShieldAlert className="h-4 w-4" />
                Emergency cover
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <dl className="grid gap-4 sm:grid-cols-2">
                <Detail label="Emergency successor">
                  {p.emergencySuccessorName ?? (
                    <span className="text-red-600 dark:text-red-400">Nobody nominated</span>
                  )}
                </Detail>
                <Detail label="Target succession date">{fmtDate(p.targetSuccessionDate)}</Detail>
              </dl>
              {p.emergencyProtocol && (
                <p className="whitespace-pre-wrap text-sm">{p.emergencyProtocol}</p>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <History className="h-4 w-4" />
                Review and approval
              </CardTitle>
            </CardHeader>
            <CardContent>
              <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                <Detail label="Reviewed by">{p.reviewedByName ?? '—'}</Detail>
                <Detail label="Review date">{fmtDate(p.reviewDate)}</Detail>
                <Detail label="Approved by">{p.approvedByName ?? '—'}</Detail>
                <Detail label="Approval date">{fmtDate(p.approvalDate)}</Detail>
                <Detail label="Review every">{p.reviewFrequencyMonths} months</Detail>
                <Detail label="Next review">{fmtDate(p.nextReviewDate)}</Detail>
              </dl>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="candidates" className="pt-4">
          <CandidatesPanel plan={p} />
        </TabsContent>

        <TabsContent value="competencies" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {p.competencyRequirements.length === 0 ? (
                <EmptyState
                  icon={FileText}
                  title="No competency requirements"
                  description="The competency library is not built yet, so there is nothing to require against. This fills in when area 17 lands."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Competency</TableHead>
                      <TableHead>Category</TableHead>
                      <TableHead className="text-right">Required level</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {p.competencyRequirements.map((r) => (
                      <TableRow key={r.id}>
                        <TableCell>
                          <div className="font-medium">{r.competencyName}</div>
                          <div className="text-xs text-muted-foreground">{r.competencyCode}</div>
                        </TableCell>
                        <TableCell>{spaced(r.competencyCategory)}</TableCell>
                        <TableCell className="text-right">
                          {/* The scale max travels with the row — never assume it is 5. */}
                          {r.requiredLevel} / {r.proficiencyScaleMax}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="actions" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {p.actions.length === 0 ? (
                <EmptyState
                  icon={CheckCircle2}
                  title="No actions recorded"
                  description="Actions are the work the plan commits to — recruit, develop, retain, assess."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Action</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Priority</TableHead>
                      <TableHead>Owner</TableHead>
                      <TableHead>Due</TableHead>
                      <TableHead>Status</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {p.actions.map((a) => (
                      <TableRow key={a.id}>
                        <TableCell>{a.actionDescription}</TableCell>
                        <TableCell>{a.type}</TableCell>
                        <TableCell>{a.priority}</TableCell>
                        <TableCell>{a.responsiblePersonName ?? '—'}</TableCell>
                        <TableCell>{fmtDate(a.dueDate)}</TableCell>
                        <TableCell>
                          <StatusBadge status={a.status} />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="documents" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {p.documents.length === 0 ? (
                <EmptyState
                  icon={FileText}
                  title="No documents"
                  description="Confidential documents are a separate, administrator-only list and do not appear here."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Document</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Uploaded by</TableHead>
                      <TableHead>Uploaded</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {p.documents.map((d) => (
                      <TableRow key={d.id}>
                        <TableCell>
                          {d.documentName}
                          {d.isConfidential && (
                            <Badge variant="outline" className="ml-2">
                              Confidential
                            </Badge>
                          )}
                        </TableCell>
                        <TableCell>{d.documentType}</TableCell>
                        <TableCell>{d.uploadedByName}</TableCell>
                        <TableCell>{fmtDate(d.uploadDate)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <WorkflowTabContent
          value="workflow"
          entityType="SuccessionPlan"
          entityId={id}
          entityLabel="Succession Plan"
          entityNumber={p.planNumber}
          status={p.status}
          onAfterAction={async () => {
            await refresh();
            await workflow.refresh();
          }}
        />
      </Tabs>
    </div>
  );
}
