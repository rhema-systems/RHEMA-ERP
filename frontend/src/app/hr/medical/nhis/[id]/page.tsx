'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Gavel, Pencil, Send, Wallet } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { FinancePostingCard } from '@/components/hr/common/FinancePostingCard';
import { HR_ADMIN_ROLES, HR_ROLES } from '@/components/hr/common/PermissionGate';
import { NhisClaimDocumentsPanel } from '@/components/hr/medical/NhisClaimDocumentsPanel';
import {
  NhisClaimFormDialog,
  NhisDecisionDialog,
  NhisPaymentDialog,
  NhisStatusBadge,
  NhisSubmitDialog,
  nhisClaimStage,
} from '@/components/hr/medical/NhisClaimDialogs';
import { useAuth } from '@/hooks/use-auth';
import { nhisClaimService } from '@/services/hr/medical-claims.service';
import { MEDICAL_SERVICE_TYPE_OPTIONS } from '@/types/hr/medical';
import type { ReactNode } from 'react';

/**
 * One NHIS claim: what was claimed, where it stands with the scheme, its documents, and what Finance
 * holds for it.
 *
 * The register lists five fields of a claim; everything else — the diagnosis, the amounts covered
 * and co-paid, the batch it went out in, the scheme's decision and payment — was visible nowhere
 * until this page. The lifecycle steps sit in the header, offered only where the status admits them.
 */
const money = (v?: number | null) =>
  v === null || v === undefined
    ? '—'
    : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const serviceLabel = (v: string) =>
  MEDICAL_SERVICE_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;

export default function NhisClaimDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const queryClient = useQueryClient();
  const { hasAnyPermission, hasAnyRole } = useAuth();
  const canWrite =
    hasAnyPermission(['HR.Medical.Write', 'HR.Medical.Admin']) || hasAnyRole(HR_ROLES);
  /** Removing a document is Admin, a tier above the HR desk that files the claim. */
  const canDelete = hasAnyPermission(['HR.Medical.Admin']) || hasAnyRole(HR_ADMIN_ROLES);

  const [editOpen, setEditOpen] = useState(false);
  const [submitOpen, setSubmitOpen] = useState(false);
  const [decideOpen, setDecideOpen] = useState(false);
  const [settleOpen, setSettleOpen] = useState(false);

  const claimKey = ['hr', 'nhis-claims', id];
  const { data: claim, isLoading, isError, error } = useQuery({
    queryKey: claimKey,
    queryFn: () => nhisClaimService.getClaim(id),
  });

  // The prefix covers this claim and both registers (all, pending).
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'nhis-claims'] });

  const stage = claim ? nhisClaimStage(claim.status) : null;

  if (isError) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="NHIS claim" backHref="/hr/medical/nhis" />
        <EmptyState
          title="Could not load this claim"
          description={(error as any)?.message || 'It may have been deleted.'}
        />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={claim?.claimNumber ?? 'NHIS claim'}
        description={
          claim
            ? `${claim.employeeName} · ${serviceLabel(claim.serviceType)} at ${claim.facilityName}`
            : isLoading
              ? 'Loading…'
              : undefined
        }
        backHref="/hr/medical/nhis"
        actions={
          claim &&
          stage &&
          canWrite && (
            <div className="flex flex-wrap gap-2">
              {stage.canEdit && (
                <Button variant="outline" onClick={() => setEditOpen(true)}>
                  <Pencil className="mr-2 h-4 w-4" /> Edit
                </Button>
              )}
              {stage.canSubmit && (
                <Button onClick={() => setSubmitOpen(true)}>
                  <Send className="mr-2 h-4 w-4" /> Submit to NHIS
                </Button>
              )}
              {stage.canDecide && (
                <Button onClick={() => setDecideOpen(true)}>
                  <Gavel className="mr-2 h-4 w-4" /> Record decision
                </Button>
              )}
              {stage.canSettle && (
                <Button onClick={() => setSettleOpen(true)}>
                  <Wallet className="mr-2 h-4 w-4" /> Record payment
                </Button>
              )}
            </div>
          )
        }
      />

      {claim?.status === 'Rejected' && (
        <Card className="border-destructive">
          <CardContent className="space-y-1 p-4 text-sm">
            <p className="font-medium text-destructive">
              Rejected by the scheme{claim.rejectionDate ? ` on ${fmtDate(claim.rejectionDate)}` : ''}.
            </p>
            <p>{claim.rejectionReason || 'No reason was recorded.'}</p>
          </CardContent>
        </Card>
      )}

      {claim && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">The claim</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Detail label="Status" value={<NhisStatusBadge status={claim.status} />} />
            <Detail label="Member" value={claim.employeeName} />
            <Detail label="NHIS membership number" value={claim.nhisMembershipNumber} />
            <Detail label="Date of service" value={fmtDate(claim.serviceDate)} />
            <Detail label="Service" value={serviceLabel(claim.serviceType)} />
            <Detail label="Facility" value={claim.facilityName} />
            <Detail label="Physician" value={claim.physicianName} />
            <Detail label="ICD code" value={claim.icdCode} />
            <Detail label="Total cost" value={money(claim.totalCost)} />
            <Detail label="Covered by NHIS" value={money(claim.nhisCoveredAmount)} />
            <Detail label="Co-pay" value={money(claim.coPayAmount)} />
            <Detail
              label="Employer reimbursement"
              value={
                claim.linkedMedicalClaimId ? (
                  <Link
                    href={`/hr/medical/claims/${claim.linkedMedicalClaimId}`}
                    className="font-mono text-primary hover:underline"
                  >
                    {claim.linkedMedicalClaimNumber || 'Open the claim'}
                  </Link>
                ) : (
                  'None — NHIS only'
                )
              }
            />
            <div className="sm:col-span-2">
              <Detail label="What was done" value={claim.serviceDescription} />
            </div>
            <div className="sm:col-span-2">
              <Detail label="Diagnosis" value={claim.diagnosis} />
            </div>
            {claim.notes && (
              <div className="sm:col-span-2 lg:col-span-4">
                <Detail label="Notes" value={claim.notes} />
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Nothing has been put to the scheme while the claim is a draft, so there is nothing to show. */}
      {claim && claim.status !== 'Draft' && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">With the scheme</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Detail label="Batch number" value={claim.batchNumber} />
            <Detail label="Submitted" value={fmtDate(claim.submissionDate)} />
            <Detail
              label="Decided"
              value={fmtDate(claim.approvalDate ?? claim.rejectionDate)}
            />
            <Detail label="Amount approved" value={money(claim.approvedAmount)} />
            <Detail label="Paid" value={fmtDate(claim.paymentDate)} />
            <Detail label="Payment reference" value={claim.paymentReference} />
          </CardContent>
        </Card>
      )}

      {/* Documents belong to a claim at every status — the scheme's rejection letter arrives after
          the decision, and the attendance record before it. */}
      {claim && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Documents</CardTitle>
          </CardHeader>
          <CardContent>
            <NhisClaimDocumentsPanel claimId={id} canWrite={canWrite} canDelete={canDelete} />
          </CardContent>
        </Card>
      )}

      {/* What Finance holds for the claim: what the scheme owes, then what it paid. */}
      {claim && <FinancePostingCard sourceDocumentId={id} invalidateKeys={[claimKey]} />}

      <NhisClaimFormDialog
        open={editOpen}
        onOpenChange={setEditOpen}
        claim={claim ?? null}
        onDone={refresh}
      />
      <NhisSubmitDialog
        claim={submitOpen && claim ? claim : null}
        onClose={() => setSubmitOpen(false)}
        onDone={refresh}
      />
      <NhisDecisionDialog
        claim={decideOpen && claim ? claim : null}
        onClose={() => setDecideOpen(false)}
        onDone={refresh}
      />
      <NhisPaymentDialog
        claim={settleOpen && claim ? claim : null}
        onClose={() => setSettleOpen(false)}
        onDone={refresh}
      />
    </div>
  );
}

function Detail({ label, value }: { label: string; value?: ReactNode }) {
  return (
    <div>
      <p className="text-sm text-muted-foreground">{label}</p>
      <div className="text-sm">{value || '—'}</div>
    </div>
  );
}
