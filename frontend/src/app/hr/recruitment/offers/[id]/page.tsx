'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter, useSearchParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Ban, HandCoins, Loader2, Pencil, Send, ShieldCheck, Split, Trash2, UserCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { OfferBenefitsPanel } from '@/components/hr/recruitment/OfferBenefitsPanel';
import { OfferLetterPanel } from '@/components/hr/recruitment/OfferLetterPanel';
import { OfferNotesPanel } from '@/components/hr/recruitment/OfferNotesPanel';
import { PreEmploymentChecksPanel } from '@/components/hr/recruitment/PreEmploymentChecksPanel';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatDateTime, formatMoney, humanizeEnum } from '@/lib/hr/attendance-format';
import { jobHireService, jobOfferService } from '@/services/hr/offers.service';
import { OFFER_RESPONSES, UNREVOKABLE_OFFER_STATUSES, type OfferResponse } from '@/types/hr/offers';

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

type DialogAction = 'issue' | 'recordResponse' | 'acceptConditionally' | 'revoke' | 'revise' | 'delete';

/** Tabs `?tab=` may select. Anything else falls back to Overview rather than showing nothing. */
const ALLOWED_TABS = ['overview', 'benefits', 'checks', 'letter', 'notes'];

export default function JobOfferDetailPage() {
  const router = useRouter();
  const params = useParams();
  const searchParams = useSearchParams();
  const requestedTab = searchParams?.get('tab') ?? 'overview';
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const { toast } = useToast();

  const [action, setAction] = useState<DialogAction | null>(null);
  const [offerDate, setOfferDate] = useState('');
  const [expiryDate, setExpiryDate] = useState('');
  const [response, setResponse] = useState<OfferResponse>('Accepted');
  const [notes, setNotes] = useState('');
  const [reason, setReason] = useState('');
  const [newBaseSalary, setNewBaseSalary] = useState('');
  const [newProposedStartDate, setNewProposedStartDate] = useState('');
  const [newAdditionalTerms, setNewAdditionalTerms] = useState('');
  const [revisionReason, setRevisionReason] = useState('');

  const { data: offer, isLoading, isError } = useQuery({
    queryKey: ['hr', 'offers', id],
    queryFn: () => jobOfferService.getDetails(id),
    enabled: !!id,
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'offers', id] });

  // A hire record is created against the offer once the candidate has accepted (or, for a
  // conditional offer, once checks have cleared). Checked here so the button becomes a link once
  // one exists, rather than letting create-hire be tried twice.
  const applicationId = offer?.jobApplicationId ?? null;

  const hire = useQuery({
    queryKey: ['hr', 'hire-for-offer', applicationId],
    queryFn: () => jobHireService.getByApplication(applicationId as string),
    enabled: !!applicationId,
  });

  const createHire = useMutation({
    mutationFn: () =>
      jobHireService.create({
        applicationId: applicationId as string,
        offerId: id,
        expectedStartDate: offer?.proposedStartDate ?? new Date().toISOString().slice(0, 10),
        notes: null,
      }),
    onSuccess: (created) => {
      toast({ title: 'Hire record created', description: created.hireNumber });
      router.push(`/hr/recruitment/hires/${created.id}`);
    },
    onError: (e: any) =>
      toast({ title: 'Could not create the hire record', description: e?.message, variant: 'destructive' }),
  });

  /**
   * Submit, approve, reject and recall come from the generic engine, same as every other
   * workflow-driven HR record.
   *
   * ⚠ **This comment used to say "Inoperable until a `JobOffer` workflow definition is published",
   * and that was exactly backwards** (G-10.1, corrected 2026-09-15). With no definition published
   * the engine returned `Approved`, and `JobOfferWorkflowStatusAdapter` mapped that to
   * `OfferStatus.Approved` *and stamped `ApprovedDate`* — so Submit took an offer Draft → Approved
   * in one step and immediately unlocked **Issue to candidate**. That is the step which authorises
   * sending salary, start date, notice and probation terms to someone outside the organisation.
   *
   * What happens now, with no definition published: Submit lands the offer at **PendingApproval**
   * — which nothing could previously produce, so `canApproveReject` below was unreachable —
   * and Approve requires a recruitment administrator who did not prepare the offer.
   */
  const workflow = useWorkflowRecord({
    entityType: 'JobOffer',
    entityId: id,
    entityLabel: 'Job Offer',
    entityNumber: offer?.offerNumber,
    status: offer?.offerStatus ?? 'Draft',
    canSubmit: offer?.offerStatus === 'Draft' || offer?.offerStatus === 'Rejected',
    canApproveReject: offer?.offerStatus === 'PendingApproval',
    // G-10.4: the preparer taking their own offer back before anybody has ruled. The second
    // instance of the identical gap — the endpoint, the adapter and the client method all existed,
    // and nothing called any of them. Only reachable now that Submit lands at PendingApproval
    // instead of Approved (G-10.1): an approved offer is past recalling.
    canRecall:
      offer?.offerStatus === 'PendingApproval' &&
      !!user?.employeeId &&
      offer?.preparedById === user.employeeId,
    enabled: !!offer,
    commands: {
      submit: () => jobOfferService.submit(id),
      approve: (ctx) => jobOfferService.approve(id, ctx.comments || null),
      reject: (ctx) => jobOfferService.reject(id, ctx.comments || 'Rejected'),
      recall: () => jobOfferService.recall(id),
      afterAction: async () => {
        await refresh();
      },
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  const close = () => {
    setAction(null);
    setOfferDate('');
    setExpiryDate('');
    setResponse('Accepted');
    setNotes('');
    setReason('');
    setNewBaseSalary('');
    setNewProposedStartDate('');
    setNewAdditionalTerms('');
    setRevisionReason('');
  };

  const issue = useMutation({
    mutationFn: () => jobOfferService.issue(id, { offerDate, expiryDate: expiryDate || null }),
    onSuccess: async () => {
      await refresh();
      close();
      toast({ title: 'Offer issued', description: 'The candidate has been emailed their response link.' });
    },
    onError: (e: any) => toast({ title: 'Could not issue', description: e?.message, variant: 'destructive' }),
  });

  const recordResponse = useMutation({
    mutationFn: () =>
      jobOfferService.recordResponse(id, { response, candidateResponseNotes: notes.trim() || null }),
    onSuccess: async () => {
      await refresh();
      close();
      toast({ title: 'Response recorded' });
    },
    onError: (e: any) => toast({ title: 'Could not record it', description: e?.message, variant: 'destructive' }),
  });

  const acceptConditionally = useMutation({
    mutationFn: () => jobOfferService.acceptConditionally(id, notes.trim() || null),
    onSuccess: async () => {
      await refresh();
      close();
      toast({ title: 'Conditionally accepted', description: 'Pre-employment checks can now be started.' });
    },
    onError: (e: any) => toast({ title: 'Could not record it', description: e?.message, variant: 'destructive' }),
  });

  const revoke = useMutation({
    mutationFn: () => jobOfferService.revoke(id, reason.trim()),
    onSuccess: async () => {
      await refresh();
      close();
      toast({ title: 'Offer revoked' });
    },
    onError: (e: any) => toast({ title: 'Could not revoke it', description: e?.message, variant: 'destructive' }),
  });

  const revise = useMutation({
    mutationFn: () =>
      jobOfferService.revise(id, {
        newBaseSalary: newBaseSalary ? Number(newBaseSalary) : null,
        newProposedStartDate: newProposedStartDate || null,
        newAdditionalTerms: newAdditionalTerms.trim() || null,
        revisionReason: revisionReason.trim() || null,
      }),
    onSuccess: (revised) => {
      close();
      toast({
        title: 'Revised offer created',
        description: `${revised.offerNumber} (v${revised.version}) saved as a draft.`,
      });
      router.push(`/hr/recruitment/offers/${revised.id}`);
    },
    onError: (e: any) => toast({ title: 'Could not revise it', description: e?.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: () => jobOfferService.remove(id),
    onSuccess: () => {
      toast({ title: 'Offer deleted' });
      router.push('/hr/recruitment/offers');
    },
    onError: (e: any) => toast({ title: 'Could not delete it', description: e?.message, variant: 'destructive' }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !offer) {
    return (
      <div className="p-6">
        <EmptyState title="Offer not found" description="It may have been removed." />
      </div>
    );
  }

  const editable = offer.offerStatus === 'Draft' || offer.offerStatus === 'PendingApproval';
  const canDelete = offer.offerStatus === 'Draft';
  const canIssue = offer.offerStatus === 'Approved';
  const canRecordResponse = offer.offerStatus === 'Sent';
  const canAcceptConditionally = offer.offerStatus === 'Sent' || offer.offerStatus === 'Negotiating';
  const canRevise = offer.offerStatus === 'Sent' || offer.offerStatus === 'Negotiating';
  const canRevoke = !UNREVOKABLE_OFFER_STATUSES.includes(offer.offerStatus);
  const canCreateHire =
    !hire.data &&
    (offer.isConditional
      ? offer.offerStatus === 'ChecksCleared'
      : offer.offerStatus === 'Accepted' || offer.offerStatus === 'ChecksCleared');

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={offer.offerNumber}
        description={`${offer.candidateName} · ${offer.positionTitle}${offer.version > 1 ? ` · v${offer.version}` : ''}`}
        backHref="/hr/recruitment/offers"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={offer.offerStatus} />

            {editable && (
              <Button variant="outline" onClick={() => router.push(`/hr/recruitment/offers/${id}/edit`)}>
                <Pencil className="mr-2 h-4 w-4" /> Edit
              </Button>
            )}

            {/* Submit / approve / reject / recall are the engine's. */}
            <WorkflowApprovalActions {...workflow.actionProps} />

            {canIssue && (
              <Button
                onClick={() => {
                  setOfferDate(new Date().toISOString().slice(0, 10));
                  setExpiryDate(offer.expiryDate?.slice(0, 10) ?? '');
                  setAction('issue');
                }}
              >
                <Send className="mr-2 h-4 w-4" /> Issue to candidate
              </Button>
            )}

            {canRecordResponse && (
              <Button variant="outline" onClick={() => setAction('recordResponse')}>
                Record response
              </Button>
            )}

            {canAcceptConditionally && (
              <Button variant="outline" onClick={() => setAction('acceptConditionally')}>
                <ShieldCheck className="mr-2 h-4 w-4" /> Accept conditionally
              </Button>
            )}

            {canRevise && (
              <Button variant="outline" onClick={() => setAction('revise')}>
                <Split className="mr-2 h-4 w-4" /> Revise
              </Button>
            )}

            {canRevoke && (
              <Button variant="outline" onClick={() => setAction('revoke')}>
                <Ban className="mr-2 h-4 w-4" /> Revoke
              </Button>
            )}

            {canDelete && (
              <Button variant="destructive" onClick={() => setAction('delete')}>
                <Trash2 className="mr-2 h-4 w-4" /> Delete
              </Button>
            )}

            {hire.data ? (
              <Button asChild variant="outline">
                <Link href={`/hr/recruitment/hires/${hire.data.id}`}>
                  <UserCheck className="mr-2 h-4 w-4" />
                  {hire.data.hireNumber} · {hire.data.statusName}
                </Link>
              </Button>
            ) : (
              canCreateHire && (
                <Button onClick={() => createHire.mutate()} disabled={createHire.isPending}>
                  <UserCheck className="mr-2 h-4 w-4" />
                  {createHire.isPending ? 'Creating…' : 'Create hire record'}
                </Button>
              )
            )}
          </div>
        }
      />

      {/* G-11.4 (2026-09-15): `?tab=checks` deep-links here from the clearance queue, which used to
          land on Overview and leave the user to find the tab themselves — three clicks to reach the
          thing the queue had just pointed at. The vacancy page already took a `tab` param this way. */}
      <Tabs defaultValue={ALLOWED_TABS.includes(requestedTab) ? requestedTab : 'overview'}>
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="benefits">Benefits</TabsTrigger>
          <TabsTrigger value="checks">Pre-employment checks</TabsTrigger>
          <TabsTrigger value="letter">Letter</TabsTrigger>
          <TabsTrigger value="notes">Notes</TabsTrigger>
          <WorkflowTabTrigger value="workflow" />
        </TabsList>

        <TabsContent value="overview" className="space-y-4 pt-4">
          <InfoCard title="The role — server-owned, taken from the vacancy">
            <InfoRow label="Position" value={offer.positionTitle} />
            <InfoRow label="Reports to" value={offer.reportsToTitle} />
            <InfoRow label="Grade" value={offer.gradeTitle} />
            <InfoRow label="Department" value={offer.departmentName} />
            <InfoRow label="Employment type" value={humanizeEnum(offer.employmentType)} />
            <InfoRow label="Work mode" value={humanizeEnum(offer.workMode)} />
            <InfoRow label="Location" value={offer.locationName} />
            {/* G-10.5 (2026-09-15): say when the band check did not run. `EnsureSalaryWithinBand`
                returns early when SalaryGradeMax <= 0 or the band is inverted — correct defensive
                behaviour — but the band comes from the position's salary grade, and a position
                with no grade has no band. For those posts an offer can carry ANY base salary, with
                nothing on screen saying the guardrail was absent. Given § 3.2's finding that most
                positions are not even established, it is likely most are ungraded too, so silence
                here reads as "checked and fine" on the majority of offers. */}
            <InfoRow
              label="Grade band"
              value={
                (offer.salaryGradeMax ?? 0) > 0
                  ? `${offer.salaryGradeMin != null ? formatMoney(offer.salaryGradeMin, offer.currencyCode ?? 'GHS') : '—'} to ${formatMoney(offer.salaryGradeMax!, offer.currencyCode ?? 'GHS')}`
                  : 'No band on this position — the salary was not checked against one'
              }
            />
          </InfoCard>

          <InfoCard title="Compensation">
            <InfoRow
              label="Base salary"
              value={offer.baseSalary != null ? formatMoney(offer.baseSalary, offer.currencyCode ?? 'GHS') : '—'}
            />
            <InfoRow
              label="Bonus"
              value={offer.bonus != null ? formatMoney(offer.bonus, offer.currencyCode ?? 'GHS') : '—'}
            />
            <InfoRow label="Bonus terms" value={offer.bonusTerms} />
            <InfoRow
              label="Commission"
              value={offer.commission != null ? formatMoney(offer.commission, offer.currencyCode ?? 'GHS') : '—'}
            />
            <InfoRow label="Commission structure" value={offer.commissionStructure} />
            <InfoRow label="Weekly hours" value={offer.weeklyHours} />
          </InfoCard>

          <InfoCard title="Dates and terms">
            <InfoRow label="Proposed start" value={formatDate(offer.proposedStartDate)} />
            <InfoRow label="Offer date" value={formatDate(offer.offerDate)} />
            <InfoRow label="Expires" value={formatDate(offer.expiryDate)} />
            <InfoRow label="Contract length" value={offer.contractDurationMonths ? `${offer.contractDurationMonths} months` : '—'} />
            <InfoRow label="Probation" value={offer.probationPeriodMonths ? `${offer.probationPeriodMonths} months` : '—'} />
            <InfoRow label="Notice period" value={offer.noticePeriodMonths ? `${offer.noticePeriodMonths} months` : '—'} />
            <InfoRow label="Annual leave" value={offer.annualLeaveDays ? `${offer.annualLeaveDays} days` : '—'} />
            <InfoRow label="NDA required" value={offer.ndaRequired ? 'Yes' : 'No'} />
            <InfoRow label="Conditional" value={offer.isConditional ? 'Yes — pre-employment checks apply' : 'No'} />
          </InfoCard>

          {offer.additionalTerms && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">Additional terms</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="whitespace-pre-wrap text-sm">{offer.additionalTerms}</p>
              </CardContent>
            </Card>
          )}

          <InfoCard title="Approval and response">
            <InfoRow label="Prepared by" value={offer.preparedByName} />
            <InfoRow label="Approved by" value={offer.approvedByName} />
            <InfoRow label="Approved" value={formatDateTime(offer.approvedDate)} />
            {offer.approvalRejectionReason && (
              <InfoRow label="Rejection reason" value={offer.approvalRejectionReason} />
            )}
            <InfoRow label="Accepted" value={formatDateTime(offer.acceptedDate)} />
            <InfoRow label="Declined" value={formatDateTime(offer.declinedDate)} />
            {offer.declineReason && <InfoRow label="Decline reason" value={offer.declineReason} />}
            <InfoRow label="Revoked" value={formatDateTime(offer.revokedDate)} />
            {offer.revocationReason && <InfoRow label="Revocation reason" value={offer.revocationReason} />}
            {offer.candidateResponseNotes && (
              <InfoRow label="Candidate notes" value={offer.candidateResponseNotes} />
            )}
            {offer.preEmploymentCheckStatusName && (
              <InfoRow label="Pre-employment checks" value={offer.preEmploymentCheckStatusName} />
            )}
            {offer.previousOfferId && (
              <InfoRow
                label="Revised from"
                value={
                  <Link
                    href={`/hr/recruitment/offers/${offer.previousOfferId}`}
                    className="text-primary hover:underline"
                  >
                    Previous version
                  </Link>
                }
              />
            )}
          </InfoCard>
        </TabsContent>

        <TabsContent value="benefits" className="pt-4">
          <OfferBenefitsPanel offerId={id} currencyCode={offer.currencyCode} editable={editable} />
        </TabsContent>

        <TabsContent value="checks" className="pt-4">
          <PreEmploymentChecksPanel
            offerId={id}
            offerIsConditional={offer.isConditional}
            canManage
            candidateId={offer.candidateId}
          />
        </TabsContent>

        <TabsContent value="letter" className="pt-4">
          <OfferLetterPanel offer={offer} canManage />
        </TabsContent>

        <TabsContent value="notes" className="pt-4">
          <OfferNotesPanel offerId={id} />
        </TabsContent>

        <WorkflowTabContent
          value="workflow"
          entityType="JobOffer"
          entityId={id}
          entityLabel="Job Offer"
          entityNumber={offer.offerNumber}
          status={offer.offerStatus}
        />
      </Tabs>

      {/* ── issue ─────────────────────────────────────────────────────────── */}
      <Dialog open={action === 'issue'} onOpenChange={(o) => !o && close()}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Issue this offer to the candidate</DialogTitle>
            <DialogDescription>
              Mints a single-use response link and emails it. The letter uploaded on the Letter tab
              is what the email references.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2">
            <div className="space-y-1.5">
              <Label htmlFor="offerDate">Offer date</Label>
              <Input id="offerDate" type="date" value={offerDate} onChange={(e) => setOfferDate(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="expiryDateIssue">Expires</Label>
              <Input
                id="expiryDateIssue"
                type="date"
                value={expiryDate}
                onChange={(e) => setExpiryDate(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={close}>
              Cancel
            </Button>
            <Button onClick={() => issue.mutate()} disabled={!offerDate || issue.isPending}>
              {issue.isPending ? 'Issuing…' : 'Issue'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── record response (off-system) ─────────────────────────────────── */}
      <Dialog open={action === 'recordResponse'} onOpenChange={(o) => !o && close()}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record the candidate's response</DialogTitle>
            <DialogDescription>
              For when they responded off-system — a call, a letter. If they used the emailed link,
              this is already recorded.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2">
            <div className="space-y-1.5">
              <Label>Response</Label>
              <Select value={response} onValueChange={(v) => setResponse(v as OfferResponse)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {OFFER_RESPONSES.map((r) => (
                    <SelectItem key={r} value={r}>
                      {humanizeEnum(r)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="responseNotes">Notes</Label>
              <Textarea id="responseNotes" rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={close}>
              Cancel
            </Button>
            <Button onClick={() => recordResponse.mutate()} disabled={recordResponse.isPending}>
              {recordResponse.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── accept conditionally ─────────────────────────────────────────── */}
      <ConfirmationDialog
        open={action === 'acceptConditionally'}
        onOpenChange={(o) => !o && close()}
        title="Accept conditionally"
        description="Marks the offer conditional on pre-employment checks. It moves to ChecksCleared only once every mandatory and blocking check has passed, been waived, or been marked not applicable."
        confirmText={acceptConditionally.isPending ? 'Saving…' : 'Confirm'}
        onConfirm={async () => {
          await acceptConditionally.mutateAsync();
          return true;
        }}
      >
        <div className="space-y-1.5">
          <Label htmlFor="acceptNotes">Notes</Label>
          <Textarea id="acceptNotes" rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </div>
      </ConfirmationDialog>

      {/* ── revoke ────────────────────────────────────────────────────────── */}
      <ConfirmationDialog
        open={action === 'revoke'}
        onOpenChange={(o) => !o && close()}
        title="Revoke this offer"
        description="Withdrawal is final — a revoked offer cannot be reopened. Raise a new one if terms change again."
        confirmText={revoke.isPending ? 'Revoking…' : 'Revoke'}
        variant="destructive"
        confirmDisabled={!reason.trim()}
        onConfirm={async () => {
          await revoke.mutateAsync();
          return true;
        }}
      >
        <div className="space-y-1.5">
          <Label htmlFor="revokeReason">Reason</Label>
          <Textarea id="revokeReason" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />
        </div>
      </ConfirmationDialog>

      {/* ── revise ────────────────────────────────────────────────────────── */}
      <Dialog open={action === 'revise'} onOpenChange={(o) => !o && close()}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Revise this offer</DialogTitle>
            <DialogDescription>
              <HandCoins className="mr-1 inline h-3.5 w-3.5" />
              Creates a new Draft offer at v{offer.version + 1}, superseding this one. Leave a field
              blank to carry the current value forward. The new salary is checked against the grade
              band.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2">
            <div className="space-y-1.5">
              <Label htmlFor="newBaseSalary">New base salary</Label>
              <Input
                id="newBaseSalary"
                type="number"
                step="0.01"
                value={newBaseSalary}
                onChange={(e) => setNewBaseSalary(e.target.value)}
                placeholder={offer.baseSalary != null ? String(offer.baseSalary) : 'Unchanged'}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="newProposedStartDate">New start date</Label>
              <Input
                id="newProposedStartDate"
                type="date"
                value={newProposedStartDate}
                onChange={(e) => setNewProposedStartDate(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="newAdditionalTerms">New additional terms</Label>
              <Textarea
                id="newAdditionalTerms"
                rows={3}
                value={newAdditionalTerms}
                onChange={(e) => setNewAdditionalTerms(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="revisionReason">Reason for the revision</Label>
              <Textarea
                id="revisionReason"
                rows={2}
                value={revisionReason}
                onChange={(e) => setRevisionReason(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={close}>
              Cancel
            </Button>
            <Button onClick={() => revise.mutate()} disabled={revise.isPending}>
              {revise.isPending ? 'Revising…' : 'Create revised offer'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── delete ────────────────────────────────────────────────────────── */}
      <ConfirmationDialog
        open={action === 'delete'}
        onOpenChange={(o) => !o && close()}
        title="Delete this draft offer?"
        description="This cannot be undone."
        confirmText={remove.isPending ? 'Deleting…' : 'Delete'}
        variant="destructive"
        onConfirm={async () => {
          await remove.mutateAsync();
          return true;
        }}
      />
    </div>
  );
}
