'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Gavel, Landmark, MoreHorizontal, Pencil, Plus, Send, Wallet } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
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
import { FinancePostingInlineStatus } from '@/components/hr/common/FinancePostingCard';
import { useToast } from '@/hooks/use-toast';
import { nhisClaimService } from '@/services/hr/medical-claims.service';
import {
  NhisClaimFormDialog,
  NhisDecisionDialog,
  NhisPaymentDialog,
  NhisStatusBadge,
  NhisSubmitDialog,
  nhisClaimStage,
} from '@/components/hr/medical/NhisClaimDialogs';
import { HR_ROLES } from '@/components/hr/common/PermissionGate';
import { useAuth } from '@/hooks/use-auth';
import type { NHISClaim, NHISClaimSummary } from '@/types/hr/medical';

/**
 * Claims made against the National Health Insurance Scheme.
 *
 * These are claims the organisation makes to NHIS, not reimbursements to an employee — so the
 * lifecycle is submission and settlement rather than approval: draft → submitted (in a batch) →
 * the scheme's decision → payment recorded against it.
 *
 * A row opens the claim's own page, which holds its documents and Finance posting. The row menu
 * keeps the lifecycle steps to hand for working through a batch without opening each claim.
 */
const money = (v?: number | null) =>
  v === null || v === undefined
    ? '—'
    : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const claimHref = (id: string) => `/hr/medical/nhis/${id}`;

function NhisTable({
  items,
  empty,
  canWrite,
  onOpen,
  onEdit,
  onSubmit,
  onDecide,
  onSettle,
}: {
  items: NHISClaimSummary[];
  empty: string;
  canWrite: boolean;
  onOpen: (claim: NHISClaimSummary) => void;
  onEdit: (claim: NHISClaimSummary) => void;
  onSubmit: (claim: NHISClaimSummary) => void;
  onDecide: (claim: NHISClaimSummary) => void;
  onSettle: (claim: NHISClaimSummary) => void;
}) {
  if (items.length === 0) {
    return <EmptyState title="Nothing here" description={empty} icon={Landmark} />;
  }
  return (
    <Card>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Claim</TableHead>
              <TableHead>Employee</TableHead>
              <TableHead>Service date</TableHead>
              <TableHead className="text-right">Total cost</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="w-32">Finance</TableHead>
              <TableHead className="w-[60px]" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((c) => {
              // Every step is offered only at the point the service accepts it — see nhisClaimStage.
              const stage = nhisClaimStage(c.status);
              return (
                <TableRow key={c.id} className="cursor-pointer" onClick={() => onOpen(c)}>
                  <TableCell>
                    <Link
                      href={claimHref(c.id)}
                      className="font-mono text-sm text-primary hover:underline"
                      onClick={(e) => e.stopPropagation()}
                    >
                      {c.claimNumber}
                    </Link>
                  </TableCell>
                  <TableCell className="font-medium">{c.employeeName}</TableCell>
                  <TableCell>{fmtDate(c.serviceDate)}</TableCell>
                  <TableCell className="text-right tabular-nums">{money(c.totalCost)}</TableCell>
                  <TableCell>
                    <NhisStatusBadge status={c.status} />
                  </TableCell>
                  {/* The posting source is the NHIS claim itself — what the scheme owes and then pays. */}
                  <TableCell>
                    <FinancePostingInlineStatus sourceDocumentId={c.id} />
                  </TableCell>
                  {/* ⚠ The menu renders in a portal, but React events still bubble through the
                      component tree — without this, every menu click also opened the row. */}
                  <TableCell onClick={(e) => e.stopPropagation()}>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon" className="h-8 w-8">
                          <MoreHorizontal className="h-4 w-4" />
                          <span className="sr-only">Actions</span>
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem onClick={() => onOpen(c)}>Open</DropdownMenuItem>
                        {canWrite && stage.canEdit && (
                          <DropdownMenuItem onClick={() => onEdit(c)}>
                            <Pencil className="mr-2 h-4 w-4" />
                            Edit
                          </DropdownMenuItem>
                        )}
                        {canWrite && stage.canSubmit && (
                          <DropdownMenuItem onClick={() => onSubmit(c)}>
                            <Send className="mr-2 h-4 w-4" />
                            Submit to NHIS
                          </DropdownMenuItem>
                        )}
                        {canWrite && stage.canDecide && (
                          <DropdownMenuItem onClick={() => onDecide(c)}>
                            <Gavel className="mr-2 h-4 w-4" />
                            Record decision
                          </DropdownMenuItem>
                        )}
                        {canWrite && stage.canSettle && (
                          <DropdownMenuItem onClick={() => onSettle(c)}>
                            <Wallet className="mr-2 h-4 w-4" />
                            Record payment
                          </DropdownMenuItem>
                        )}
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

export default function NhisClaimsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [tab, setTab] = useState('pending');
  const [submitting, setSubmitting] = useState<NHISClaimSummary | null>(null);
  const [deciding, setDeciding] = useState<NHISClaimSummary | null>(null);
  const [settling, setSettling] = useState<NHISClaimSummary | null>(null);
  // ⚠ There was no create form at all until 2026-09-01 — five fields on the create DTO were
  // unreachable, and the reverse read `GetByLinkedMedicalClaimIdAsync` could only ever return empty.
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<NHISClaim | null>(null);
  const { hasAnyPermission, hasAnyRole } = useAuth();

  // The medical ladder: create, update and every lifecycle step are Write; the HR role holds Write.
  const canWrite =
    hasAnyPermission(['HR.Medical.Write', 'HR.Medical.Admin']) || hasAnyRole(HR_ROLES);

  const { data: all = [] } = useQuery({
    queryKey: ['hr', 'nhis-claims'],
    queryFn: () => nhisClaimService.getAll(),
  });
  const { data: pending = [] } = useQuery({
    queryKey: ['hr', 'nhis-claims', 'pending'],
    queryFn: () => nhisClaimService.getPending(),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'nhis-claims'] });

  const openRecord = () => {
    setEditing(null);
    setFormOpen(true);
  };

  /** Edit needs the DETAIL — the row carries five of the form's seventeen fields. */
  const openEdit = async (claim: NHISClaimSummary) => {
    try {
      setEditing(await nhisClaimService.getClaim(claim.id));
      setFormOpen(true);
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Could not load the claim',
        description: error instanceof Error ? error.message : 'Unexpected error',
      });
    }
  };

  const tableProps = {
    canWrite,
    onOpen: (c: NHISClaimSummary) => router.push(claimHref(c.id)),
    onEdit: openEdit,
    onSubmit: setSubmitting,
    onDecide: setDeciding,
    onSettle: setSettling,
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="NHIS Claims"
        description="Claims made to the National Health Insurance Scheme for treatment its members received."
        backHref="/hr/medical"
        actions={
          canWrite ? (
            <Button size="sm" onClick={openRecord}>
              <Plus className="mr-2 h-4 w-4" /> Record a claim
            </Button>
          ) : undefined
        }
      />

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="pending">Outstanding ({pending.length})</TabsTrigger>
          <TabsTrigger value="all">All claims ({all.length})</TabsTrigger>
        </TabsList>
        <TabsContent value="pending" className="mt-4">
          <NhisTable items={pending} empty="Nothing outstanding with the scheme." {...tableProps} />
        </TabsContent>
        <TabsContent value="all" className="mt-4">
          <NhisTable items={all} empty="No NHIS claims have been recorded yet." {...tableProps} />
        </TabsContent>
      </Tabs>

      <NhisClaimFormDialog
        open={formOpen}
        onOpenChange={(o) => {
          setFormOpen(o);
          if (!o) setEditing(null);
        }}
        claim={editing}
        onDone={refresh}
      />
      <NhisSubmitDialog claim={submitting} onClose={() => setSubmitting(null)} onDone={refresh} />
      <NhisDecisionDialog claim={deciding} onClose={() => setDeciding(null)} onDone={refresh} />
      <NhisPaymentDialog claim={settling} onClose={() => setSettling(null)} onDone={refresh} />
    </div>
  );
}
