'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Ban, Briefcase, CheckCheck, Loader2, PauseCircle, Pencil } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { AttachmentsPanel } from '@/components/hr/common/AttachmentsPanel';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { BudgetCheckPanel } from '@/components/hr/recruitment/BudgetCheckPanel';
import { RequisitionCostsPanel } from '@/components/hr/recruitment/RequisitionCostsPanel';
import { RequisitionCommentsPanel } from '@/components/hr/recruitment/RequisitionCommentsPanel';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatDateTime, humanizeEnum } from '@/lib/hr/attendance-format';
import { staffRequisitionService } from '@/services/hr/recruitment.service';

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

function InfoCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base">{title}</CardTitle>
      </CardHeader>
      <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">{children}</CardContent>
    </Card>
  );
}

export default function RequisitionDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  // One read of the budget + establishment check for the panel and the card (R5).
  const check = useQuery({
    queryKey: ['hr', 'requisition-budget', id],
    queryFn: () => staffRequisitionService.checkBudget(id),
    enabled: !!id,
  });
  const { hasAnyRole } = useAuth();

  const isHr = hasAnyRole(['SuperAdmin', 'HR']);

  const [action, setAction] = useState<null | 'hold' | 'cancel' | 'fulfill'>(null);
  const [reason, setReason] = useState('');
  const [positionsFilled, setPositionsFilled] = useState(1);
  const [busy, setBusy] = useState(false);

  const { data: r, isLoading, isError } = useQuery({
    queryKey: ['hr', 'requisitions', id],
    queryFn: () => staffRequisitionService.getById(id),
    enabled: !!id,
  });

  const history = useQuery({
    queryKey: ['hr', 'requisition-history', id],
    queryFn: () => staffRequisitionService.getHistory(id),
    enabled: !!id,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'requisitions'] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'requisition-history', id] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'requisition-budget', id] });
  };

  /**
   * Submit, approve, reject and recall all come from the generic engine. The backend endpoints
   * drive it and then apply the resulting status through StaffRequisitionWorkflowStatusAdapter —
   * so this page never sets a status itself, it just refetches.
   *
   * ⚠ Inoperable until a `StaffRequisition` workflow definition is published. The actions surface
   * the engine's own error in that case, which is clearer than anything we could guess at here.
   */
  const workflow = useWorkflowRecord({
    entityType: 'StaffRequisition',
    entityId: id,
    entityLabel: 'Staff Requisition',
    entityNumber: r?.requisitionNumber,
    status: r?.status ?? 'Draft',
    canSubmit: r?.status === 'Draft' || r?.status === 'Rejected',
    canApproveReject: r?.status === 'Submitted' || r?.status === 'UnderReview',
    enabled: !!r,
    commands: {
      submit: () => staffRequisitionService.submit(id),
      approve: (ctx) => staffRequisitionService.approve(id, ctx.comments || null),
      reject: (ctx) => staffRequisitionService.reject(id, ctx.comments || 'Rejected'),
      afterAction: refresh,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  const runAction = async () => {
    if (!action || !r) return false;
    setBusy(true);
    try {
      if (action === 'hold') {
        if (!reason.trim()) {
          toast({ title: 'A reason is required', variant: 'destructive' });
          return false;
        }
        await staffRequisitionService.hold(id, reason.trim());
      } else if (action === 'cancel') {
        if (!reason.trim()) {
          toast({ title: 'A reason is required', variant: 'destructive' });
          return false;
        }
        await staffRequisitionService.cancel(id, reason.trim());
      } else {
        await staffRequisitionService.fulfill(id, positionsFilled);
      }
      await refresh();
      await workflow.refresh();
      toast({ title: 'Done' });
      setAction(null);
      setReason('');
      return true;
    } catch (e: any) {
      // The server's 422 message is the rule in its own words — show that, not a generic line.
      toast({ title: 'Refused', description: e?.message || 'Action failed.', variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  const createVacancy = useMutation({
    mutationFn: async () => {
      router.push(`/hr/recruitment/vacancies/new?requisitionId=${id}`);
    },
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !r) {
    return (
      <div className="p-6">
        <EmptyState title="Requisition not found" description="It may have been removed." />
      </div>
    );
  }

  const editable = r.status === 'Draft' || r.status === 'Rejected';
  const live = !['Cancelled', 'Fulfilled'].includes(r.status);
  const canFulfill = r.status === 'Approved' || r.status === 'PartiallyFulfilled';
  const canOpenVacancy = r.status === 'Approved' && !r.jobVacancyId;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={r.requisitionNumber}
        description={`${r.requisitionTitle} · ${r.positionTitle}`}
        backHref="/hr/recruitment/requisitions"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={r.status} />

            {editable && (
              <Button
                variant="outline"
                onClick={() => router.push(`/hr/recruitment/requisitions/${id}/edit`)}
              >
                <Pencil className="mr-2 h-4 w-4" /> Edit
              </Button>
            )}

            {/* Submit / approve / reject / recall are the engine's. */}
            <WorkflowApprovalActions {...workflow.actionProps} />

            {canOpenVacancy && (
              <Button onClick={() => createVacancy.mutate()}>
                <Briefcase className="mr-2 h-4 w-4" /> Open a vacancy
              </Button>
            )}

            {isHr && canFulfill && (
              <Button
                variant="outline"
                onClick={() => {
                  setPositionsFilled(Math.min(r.positionsFilled + 1, r.numberOfPositions));
                  setAction('fulfill');
                }}
              >
                <CheckCheck className="mr-2 h-4 w-4" /> Record a hire
              </Button>
            )}

            {isHr && live && r.status !== 'OnHold' && (
              <Button variant="outline" onClick={() => setAction('hold')}>
                <PauseCircle className="mr-2 h-4 w-4" /> Put on hold
              </Button>
            )}

            {isHr && live && (
              <Button variant="destructive" onClick={() => setAction('cancel')}>
                <Ban className="mr-2 h-4 w-4" /> Cancel
              </Button>
            )}
          </div>
        }
      />

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="costs">Costs</TabsTrigger>
          <TabsTrigger value="discussion">Discussion</TabsTrigger>
          <TabsTrigger value="attachments">Attachments</TabsTrigger>
          <TabsTrigger value="history">History</TabsTrigger>
          <WorkflowTabTrigger value="workflow" />
        </TabsList>

        <TabsContent value="overview" className="space-y-4 pt-4">
          {/* At every status since R5: the approver a month later wants the same figures the
              requester saw. In Block mode the server refuses on exactly this. */}
          <BudgetCheckPanel requisitionId={id} data={check.data} />

          <InfoCard title="The role">
            <InfoRow label="Position" value={r.positionTitle} />
            <InfoRow
              label="Job description"
              value={
                r.jobDescriptionId ? (
                  <Link
                    href={`/hr/job-descriptions/${r.jobDescriptionId}`}
                    className="text-primary hover:underline"
                  >
                    {r.jobDescriptionTitle ?? 'Open job description'}
                  </Link>
                ) : (
                  'None named'
                )
              }
            />
            <InfoRow
              label="Organisation unit"
              value={
                r.organizationLevelName
                  ? `${r.organizationUnitName ?? '—'} (${r.organizationLevelName})`
                  : r.organizationUnitName
              }
            />
            <InfoRow
              label="Location"
              value={
                r.locationLevelName
                  ? `${r.locationName ?? '—'} (${r.locationLevelName})`
                  : r.locationName
              }
            />
            <InfoRow label="Type" value={humanizeEnum(r.type)} />
            <InfoRow label="Priority" value={r.priority} />
            <InfoRow
              label="Headcount"
              value={`${r.positionsFilled} of ${r.numberOfPositions} filled`}
            />
            <InfoRow
              label="Audience"
              value={[r.allowInternalCandidates && 'Internal', r.allowExternalCandidates && 'External']
                .filter(Boolean)
                .join(' and ') || '—'}
            />
            <InfoRow
              label="Vacancy"
              value={
                r.jobVacancyId ? (
                  <Link
                    href={`/hr/recruitment/vacancies/${r.jobVacancyId}`}
                    className="text-primary hover:underline"
                  >
                    {r.jobVacancyNumber ?? 'Open vacancy'}
                  </Link>
                ) : (
                  'Not opened yet'
                )
              }
            />
          </InfoCard>

          {r.type === 'Replacement' && (
            <InfoCard title="Replacement">
              <InfoRow label="Outgoing employee" value={r.replacementForEmployeeName} />
              <InfoRow label="Reason" value={r.replacementReason ? humanizeEnum(r.replacementReason) : null} />
              <InfoRow label="Departure date" value={formatDate(r.employeeDepartureDate)} />
            </InfoCard>
          )}

          <InfoCard title="Timing">
            <InfoRow label="Raised" value={formatDate(r.requestDate)} />
            <InfoRow label="Desired start" value={formatDate(r.desiredStartDate)} />
            <InfoRow label="Latest acceptable start" value={formatDate(r.latestAcceptableStartDate)} />
            {r.targetStartDateReason && (
              <InfoRow label="Why that start date" value={r.targetStartDateReason} />
            )}
            <InfoRow label="Expected offer date" value={formatDate(r.expectedOfferDate)} />
            <InfoRow label="Target fill date" value={formatDate(r.targetFillDate)} />
            <InfoRow label="Days to fill" value={r.daysToFill ?? '—'} />
            <InfoRow label="Fulfilled" value={formatDate(r.fulfilledDate)} />
          </InfoCard>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">The case for it</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              {r.description && (
                <div>
                  <p className="text-xs text-muted-foreground">Description</p>
                  <p className="whitespace-pre-wrap text-sm">{r.description}</p>
                </div>
              )}
              <div>
                <p className="text-xs text-muted-foreground">Business justification</p>
                <p className="whitespace-pre-wrap text-sm">{r.businessJustification || '—'}</p>
              </div>
              {r.impactIfNotFilled && (
                <div>
                  <p className="text-xs text-muted-foreground">Impact if not filled</p>
                  <p className="whitespace-pre-wrap text-sm">{r.impactIfNotFilled}</p>
                </div>
              )}
              {r.notes && (
                <div>
                  <p className="text-xs text-muted-foreground">Notes</p>
                  <p className="whitespace-pre-wrap text-sm">{r.notes}</p>
                </div>
              )}
            </CardContent>
          </Card>

          {/* Round 2b, R5 (D-2): the establishment as it stood at submit is the record; the live
              figure beside it is a courtesy, and a drift is shown, never used to refuse. */}
          <InfoCard title="Budget and establishment">
            <InfoRow
              label="Budgeted"
              value={
                r.isBudgeted && r.budgetCode ? (
                  check.data?.linkedBudgetId ? (
                    <Link href={`/hr/manpower-budgets/${check.data.linkedBudgetId}`} className="text-primary hover:underline">
                      Yes · {r.budgetCode}
                    </Link>
                  ) : (
                    `Yes · ${r.budgetCode}`
                  )
                ) : (
                  'No — not raised against an approved budget line'
                )
              }
            />
            {check.data?.hasBudgetLine && (
              <InfoRow
                label="Drawdown"
                value={`${check.data.drawdown} requested by others · ${check.data.remaining ?? 0} left · this one asks for ${check.data.requestedPositions} of ${check.data.budgetedNewPosts ?? 0} budgeted`}
              />
            )}
            {r.exceptionJustification && <InfoRow label="Exception justification" value={r.exceptionJustification} />}
            {check.data?.exceptionRequired && !r.exceptionJustification && (
              <InfoRow label="Exception" value={`Required to submit: ${check.data.exceptionReason}`} />
            )}
            <InfoRow
              label="Establishment at submit"
              value={
                r.establishmentSnapshotOn
                  ? r.establishmentSnapshotIsEstablished
                    ? `${r.establishmentSnapshotExpected} authorised · ${r.establishmentSnapshotFilled} in post · gap ${Math.max(0, (r.establishmentSnapshotExpected ?? 0) - (r.establishmentSnapshotFilled ?? 0))}${r.establishmentSnapshotSourceBudgetNumber ? ` · set by ${r.establishmentSnapshotSourceBudgetNumber}` : ' · set by HR'} (${formatDate(r.establishmentSnapshotOn)})`
                    : `Not established (${formatDate(r.establishmentSnapshotOn)})`
                  : 'Not yet submitted'
              }
            />
            {check.data?.establishment && (
              <InfoRow
                label="Establishment now"
                value={
                  <span>
                    {check.data.establishment.isEstablished
                      ? `${check.data.establishment.expectedHeadcount} authorised · ${check.data.establishment.filled} in post · gap ${check.data.establishment.gap}`
                      : 'Not established'}
                    {r.establishmentSnapshotOn && (
                      (check.data.establishment.isEstablished !== r.establishmentSnapshotIsEstablished ||
                        check.data.establishment.expectedHeadcount !== r.establishmentSnapshotExpected ||
                        check.data.establishment.filled !== r.establishmentSnapshotFilled) && (
                        <span className="ml-2 rounded bg-amber-100 px-1.5 py-0.5 text-xs text-amber-800">changed since submit</span>
                      )
                    )}
                  </span>
                }
              />
            )}
            <InfoRow label="Raised by" value={r.requestedByName} />
            {r.cancelledByName && (
              <>
                <InfoRow label="Cancelled by" value={r.cancelledByName} />
                <InfoRow label="Cancelled" value={formatDate(r.cancelledDate)} />
                <InfoRow label="Cancellation reason" value={r.cancellationReason} />
              </>
            )}
          </InfoCard>
        </TabsContent>

        <TabsContent value="costs" className="pt-4">
          <RequisitionCostsPanel requisitionId={id} canManage={isHr} />
        </TabsContent>

        <TabsContent value="discussion" className="pt-4">
          <RequisitionCommentsPanel requisitionId={id} />
        </TabsContent>

        <TabsContent value="attachments" className="pt-4">
          <AttachmentsPanel
            title="Requisition attachments"
            queryKey={['hr', 'requisition-attachments', id]}
            list={() => staffRequisitionService.getAttachments(id) as any}
            upload={(file, description) =>
              staffRequisitionService.uploadAttachment(id, file, description) as any
            }
            download={async (attachment: any) => {
              const blob = await staffRequisitionService.downloadAttachment(id, attachment.id);
              const url = URL.createObjectURL(blob);
              const a = document.createElement('a');
              a.href = url;
              a.download = attachment.fileName;
              a.click();
              URL.revokeObjectURL(url);
            }}
            remove={isHr ? (attachmentId) => staffRequisitionService.deleteAttachment(attachmentId) : undefined}
            emptyDescription="Org charts, budget approvals, signed job descriptions."
            note="Files are scanned and stored in the document repository; they are never public links."
          />
        </TabsContent>

        <TabsContent value="history" className="pt-4">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">Status history</CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              {history.isLoading ? (
                <div className="flex items-center justify-center py-10">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : (history.data?.length ?? 0) === 0 ? (
                <div className="py-8">
                  <EmptyState title="Nothing yet" description="Transitions appear here once it is submitted." />
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>When</TableHead>
                      <TableHead>From</TableHead>
                      <TableHead>To</TableHead>
                      <TableHead>By</TableHead>
                      <TableHead>Comments</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(history.data ?? []).map((h) => (
                      <TableRow key={h.id}>
                        <TableCell className="whitespace-nowrap text-sm">
                          {formatDateTime(h.actionDate)}
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={h.fromStatus} />
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={h.toStatus} />
                        </TableCell>
                        <TableCell>{h.changedByName || '—'}</TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {h.comments || '—'}
                        </TableCell>
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
          entityType="StaffRequisition"
          entityId={id}
          entityLabel="Staff Requisition"
          entityNumber={r.requisitionNumber}
          status={r.status}
        />
      </Tabs>

      <ConfirmationDialog
        open={action !== null}
        onOpenChange={(open) => {
          if (!open) {
            setAction(null);
            setReason('');
          }
        }}
        title={
          action === 'hold'
            ? 'Put this requisition on hold'
            : action === 'cancel'
              ? 'Cancel this requisition'
              : 'Record a hire against this requisition'
        }
        description={
          action === 'fulfill'
            ? 'How many of the requested positions are now filled? Recording all of them closes the requisition as Fulfilled.'
            : action === 'cancel'
              ? 'Cancelling is final — the requisition cannot be reopened afterwards.'
              : 'A held requisition stays open but is parked. It can be taken off hold later.'
        }
        confirmText={busy ? 'Working…' : 'Confirm'}
        onConfirm={runAction}
      >
        {action === 'fulfill' ? (
          <div className="space-y-1.5">
            <Label htmlFor="positionsFilled">Positions filled</Label>
            <Input
              id="positionsFilled"
              type="number"
              min={1}
              max={r.numberOfPositions}
              value={positionsFilled}
              onChange={(e) => setPositionsFilled(Number(e.target.value) || 1)}
            />
            <p className="text-xs text-muted-foreground">
              Between 1 and {r.numberOfPositions}. This is the running total, not an increment.
            </p>
          </div>
        ) : (
          <div className="space-y-1.5">
            <Label htmlFor="reason">Reason</Label>
            <Textarea
              id="reason"
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          </div>
        )}
      </ConfirmationDialog>
    </div>
  );
}
