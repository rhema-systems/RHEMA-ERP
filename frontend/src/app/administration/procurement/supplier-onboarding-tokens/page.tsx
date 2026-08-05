'use client';

import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import type { ColumnDef } from '@tanstack/react-table';
import {
  Banknote,
  CheckCircle2,
  Eye,
  KeyRound,
  RefreshCw,
  RotateCw,
  ShieldCheck,
} from 'lucide-react';
import { toast } from 'sonner';

import { DataTable } from '@/components/ui/DataTable/DataTable';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { procurementSupplierOnboardingTokenService as service } from '@/services/procurement-supplier-onboarding-token.service';
import type {
  SupplierOnboardingExemption,
  SupplierOnboardingPayment,
  SupplierOnboardingTokenListItem,
  SupplierOnboardingTokenSearch,
} from '@/types/procurement-supplier-onboarding-token';

type Action =
  | 'issue'
  | 'reissue'
  | 'payment'
  | 'reconcile'
  | 'exemption'
  | 'approve-exemption'
  | 'reject-exemption';

const actionTitles: Record<Action, string> = {
  issue: 'Issue supplier onboarding token',
  reissue: 'Reissue supplier onboarding token',
  payment: 'Submit token payment claim',
  reconcile: 'Verify and post token payment',
  exemption: 'Request payment exemption',
  'approve-exemption': 'Approve payment exemption',
  'reject-exemption': 'Reject payment exemption',
};

const money = (amount: number, currency = 'GHS') =>
  new Intl.NumberFormat('en-GH', { style: 'currency', currency }).format(amount);
const dateTime = (value?: string) =>
  value ? new Date(value).toLocaleString() : '—';

export default function SupplierOnboardingTokensPage() {
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canManage = hasPermission('procurement.supplier.manage');
  const canReview = hasPermission('procurement.supplier.review');
  const [filters, setFilters] = useState<SupplierOnboardingTokenSearch>({
    page: 1,
    pageSize: 25,
  });
  const [selectedId, setSelectedId] = useState<string>();
  const [action, setAction] = useState<Action>();
  const [targetPayment, setTargetPayment] =
    useState<SupplierOnboardingPayment>();
  const [targetExemption, setTargetExemption] =
    useState<SupplierOnboardingExemption>();
  const [registrationId, setRegistrationId] = useState('');
  const [reason, setReason] = useState('');
  const [reference, setReference] = useState('');
  const [paymentMethodId, setPaymentMethodId] = useState('');
  const [notes, setNotes] = useState('');
  const [busy, setBusy] = useState(false);
  const [plaintextToken, setPlaintextToken] = useState<string>();

  const summary = useQuery({
    queryKey: ['supplier-onboarding-token-summary'],
    queryFn: service.summary,
  });
  const tokens = useQuery({
    queryKey: ['supplier-onboarding-tokens', filters],
    queryFn: () => service.search(filters),
  });
  const detail = useQuery({
    queryKey: ['supplier-onboarding-token', selectedId],
    queryFn: () => {
      if (!selectedId) throw new Error('Token id is required.');
      return service.get(selectedId);
    },
    enabled: Boolean(selectedId),
  });
  const paymentMethods = useQuery({
    queryKey: ['supplier-onboarding-token-payment-methods', selectedId],
    queryFn: () => {
      if (!selectedId) throw new Error('Token id is required.');
      return service.paymentMethods(selectedId);
    },
    enabled: Boolean(selectedId && action === 'payment'),
  });

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ['supplier-onboarding-token-summary'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['supplier-onboarding-tokens'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['supplier-onboarding-token'],
      }),
    ]);
  };

  const closeAction = () => {
    setAction(undefined);
    setTargetPayment(undefined);
    setTargetExemption(undefined);
    setReason('');
    setReference('');
    setPaymentMethodId('');
    setNotes('');
  };

  const run = async () => {
    try {
      setBusy(true);
      let message = 'Action completed';
      if (action === 'issue') {
        const result = await service.issue(registrationId.trim());
        setPlaintextToken(result.plaintextToken);
        setSelectedId(result.token.id);
        message = 'Application-bound token issued';
      } else if (action === 'reissue' && detail.data) {
        const result = await service.reissue(
          detail.data.id,
          reason.trim(),
          detail.data.rowVersion
        );
        setPlaintextToken(result.plaintextToken);
        message = 'Token rotated and reissued';
      } else if (action === 'payment' && detail.data) {
        await service.recordPayment(detail.data.id, {
          paymentMethodId,
          paymentReference: reference.trim() || undefined,
          rowVersion: detail.data.rowVersion,
        });
        message = 'Payment claim submitted for trusted verification';
      } else if (
        action === 'reconcile' &&
        detail.data &&
        targetPayment
      ) {
        await service.reconcile(
          detail.data.id,
          targetPayment.id,
          reference.trim(),
          notes.trim(),
          targetPayment.rowVersion
        );
        message = 'Payment verified, posted, receipted, and reconciled';
      } else if (action === 'exemption' && detail.data) {
        await service.requestExemption(detail.data.id, {
          reason: reason.trim(),
          rowVersion: detail.data.rowVersion,
          evidence: [
            {
              referenceKind: 'ExternalReference',
              reference: reference.trim(),
              label: 'Supplier-onboarding fee exemption evidence',
              requirementKey: 'DEC-007',
            },
          ],
        });
        message = 'Exemption submitted to the configured workflow';
      } else if (
        (action === 'approve-exemption' ||
          action === 'reject-exemption') &&
        detail.data &&
        targetExemption
      ) {
        await service.decideExemption(detail.data.id, targetExemption.id, {
          approve: action === 'approve-exemption',
          comment: notes.trim(),
          rowVersion: targetExemption.rowVersion,
          evidence: [
            {
              referenceKind: 'ExternalReference',
              reference: reference.trim(),
              label: 'Independent exemption decision evidence',
              requirementKey: 'DEC-007',
            },
          ],
        });
        message =
          action === 'approve-exemption'
            ? 'Exemption approved'
            : 'Exemption rejected';
      } else {
        return;
      }
      toast.success(message);
      closeAction();
      setRegistrationId('');
      await refresh();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Action failed');
    } finally {
      setBusy(false);
    }
  };

  const columns = useMemo<ColumnDef<SupplierOnboardingTokenListItem>[]>(
    () => [
      {
        accessorKey: 'tokenReference',
        header: 'Token',
        cell: ({ row }) => (
          <div>
            <div className="font-medium">{row.original.tokenReference}</div>
            <div className="text-xs text-muted-foreground">
              {row.original.maskedToken} · generation {row.original.generation}
            </div>
          </div>
        ),
      },
      {
        accessorKey: 'registrationNumber',
        header: 'Application',
        cell: ({ row }) => (
          <div>
            <div>{row.original.registrationNumber}</div>
            <div className="text-xs text-muted-foreground">
              {row.original.applicantName}
            </div>
          </div>
        ),
      },
      {
        accessorKey: 'status',
        header: 'Token status',
        cell: ({ row }) => <Badge variant="outline">{row.original.status}</Badge>,
      },
      {
        accessorKey: 'paymentStatus',
        header: 'Payment',
        cell: ({ row }) => (
          <Badge variant="secondary">{row.original.paymentStatus}</Badge>
        ),
      },
      {
        accessorKey: 'totalAmount',
        header: 'Amount',
        cell: ({ row }) =>
          row.original.feeMode === 'Free'
            ? 'Free'
            : money(row.original.totalAmount, row.original.currencyCode),
      },
      {
        accessorKey: 'issuedAtUtc',
        header: 'Issued',
        cell: ({ row }) => dateTime(row.original.issuedAtUtc),
      },
    ],
    []
  );

  const value = detail.data;
  const pendingExemption = value?.exemptions.find(
    (item) => item.status === 'PendingApproval'
  );
  const verifiablePayment = value?.payments.find(
    (item) => item.status === 'Pending' || item.status === 'Posted'
  );

  return (
    <div
      className="space-y-6 p-6"
      data-testid="supplier-onboarding-token-page"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Supplier onboarding tokens</h1>
          <p className="text-sm text-muted-foreground">
            DEC-007 issuance, Finance posting, exemption, reconciliation, and
            application-terminal expiry.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={refresh}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
          {canManage && (
            <Button onClick={() => setAction('issue')}>
              <KeyRound className="mr-2 h-4 w-4" />
              Issue token
            </Button>
          )}
        </div>
      </div>

      <div className="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
        {[
          ['Total', summary.data?.totalCount ?? 0],
          ['Awaiting payment', summary.data?.awaitingPaymentCount ?? 0],
          ['Active', summary.data?.activeCount ?? 0],
          ['Expired', summary.data?.expiredCount ?? 0],
          ['To verify', summary.data?.pendingReconciliationCount ?? 0],
          ['Posted', money(summary.data?.postedAmount ?? 0)],
        ].map(([label, count]) => (
          <Card key={label}>
            <CardHeader className="pb-2">
              <CardTitle className="text-xs font-medium text-muted-foreground">
                {label}
              </CardTitle>
            </CardHeader>
            <CardContent className="text-xl font-semibold">{count}</CardContent>
          </Card>
        ))}
      </div>

      <Card>
        <CardContent className="grid gap-3 pt-6 md:grid-cols-3">
          <Input
            placeholder="Search token, application, or applicant"
            value={filters.search ?? ''}
            onChange={(event) =>
              setFilters((current) => ({
                ...current,
                page: 1,
                search: event.target.value || undefined,
              }))
            }
          />
          <Select
            value={filters.status ?? 'all'}
            onValueChange={(status) =>
              setFilters((current) => ({
                ...current,
                page: 1,
                status:
                  status === 'all'
                    ? undefined
                    : (status as SupplierOnboardingTokenSearch['status']),
              }))
            }
          >
            <SelectTrigger><SelectValue placeholder="All token statuses" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All token statuses</SelectItem>
              <SelectItem value="AwaitingPayment">Awaiting payment</SelectItem>
              <SelectItem value="Active">Active</SelectItem>
              <SelectItem value="Expired">Expired</SelectItem>
            </SelectContent>
          </Select>
          <Select
            value={filters.paymentStatus ?? 'all'}
            onValueChange={(status) =>
              setFilters((current) => ({
                ...current,
                page: 1,
                paymentStatus:
                  status === 'all'
                    ? undefined
                    : (status as SupplierOnboardingTokenSearch['paymentStatus']),
              }))
            }
          >
            <SelectTrigger><SelectValue placeholder="All payment statuses" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All payment statuses</SelectItem>
              {['Pending', 'Posted', 'Reconciled', 'Exempt', 'NotRequired', 'Failed'].map(
                (status) => <SelectItem key={status} value={status}>{status}</SelectItem>
              )}
            </SelectContent>
          </Select>
        </CardContent>
      </Card>

      <DataTable
        compact
        title="Application-bound token history"
        description="Newest issuance activity first. Plaintext values are never retained."
        data={tokens.data?.items ?? []}
        columns={columns}
        loading={tokens.isLoading}
        error={tokens.error ? 'Failed to load supplier-onboarding tokens.' : null}
        emptyStateMessage="No supplier-onboarding tokens match the current filters."
        enablePagination
        pageSize={25}
        onRowDoubleClick={(row) => setSelectedId(row.original.id)}
        rowActions={[
          {
            id: 'open',
            label: 'Open control record',
            icon: Eye,
            onClick: (row) => setSelectedId(row.original.id),
          },
        ]}
      />

      <Dialog open={Boolean(selectedId)} onOpenChange={(open) => !open && setSelectedId(undefined)}>
        <DialogContent className="max-h-[92vh] max-w-5xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{value?.tokenReference ?? 'Loading token…'}</DialogTitle>
            <DialogDescription>
              {value?.registrationNumber} · {value?.applicantName}
            </DialogDescription>
          </DialogHeader>
          {value && (
            <div className="space-y-5">
              <div className="grid gap-3 md:grid-cols-4">
                <Card><CardContent className="pt-5"><div className="text-xs text-muted-foreground">Status</div><div className="font-medium">{value.status}</div></CardContent></Card>
                <Card><CardContent className="pt-5"><div className="text-xs text-muted-foreground">Payment</div><div className="font-medium">{value.paymentStatus}</div></CardContent></Card>
                <Card><CardContent className="pt-5"><div className="text-xs text-muted-foreground">Amount</div><div className="font-medium">{value.feeMode === 'Free' ? 'Free' : money(value.totalAmount, value.currencyCode)}</div></CardContent></Card>
                <Card><CardContent className="pt-5"><div className="text-xs text-muted-foreground">Configuration</div><div className="font-medium">{value.sourceConfigurationProfileCode} v{value.sourceConfigurationProfileVersion}</div></CardContent></Card>
              </div>
              <Alert>
                <ShieldCheck className="h-4 w-4" />
                <AlertTitle>Application-bound control</AlertTitle>
                <AlertDescription>
                  This token has no time expiry. It expires only when application
                  {` ${value.registrationNumber} `} is approved or rejected.
                  Current value: {value.maskedToken}.
                </AlertDescription>
              </Alert>
              <div className="flex flex-wrap gap-2">
                {canManage && value.status !== 'Expired' && (
                  <Button variant="outline" onClick={() => setAction('reissue')}>
                    <RotateCw className="mr-2 h-4 w-4" />Reissue
                  </Button>
                )}
                {canManage &&
                  value.paymentStatus === 'Pending' &&
                  value.payments.length === 0 && (
                  <Button onClick={() => setAction('payment')}>
                    <Banknote className="mr-2 h-4 w-4" />Submit payment
                  </Button>
                )}
                {canManage && value.paymentStatus === 'Pending' && !pendingExemption && (
                  <Button variant="outline" onClick={() => setAction('exemption')}>
                    Request exemption
                  </Button>
                )}
                {canReview && verifiablePayment && (
                  <Button
                    variant="outline"
                    onClick={() => {
                      setTargetPayment(verifiablePayment);
                      setAction('reconcile');
                    }}
                  >
                    <CheckCircle2 className="mr-2 h-4 w-4" />Verify &amp; post
                  </Button>
                )}
                {canReview && pendingExemption && (
                  <>
                    <Button
                      onClick={() => {
                        setTargetExemption(pendingExemption);
                        setAction('approve-exemption');
                      }}
                    >
                      Approve exemption
                    </Button>
                    <Button
                      variant="destructive"
                      onClick={() => {
                        setTargetExemption(pendingExemption);
                        setAction('reject-exemption');
                      }}
                    >
                      Reject exemption
                    </Button>
                  </>
                )}
              </div>
              <section>
                <h2 className="mb-2 font-semibold">Finance posting and receipts</h2>
                <div className="space-y-2">
                  {value.payments.length === 0 && <p className="text-sm text-muted-foreground">No payment activity.</p>}
                  {value.payments.map((payment) => (
                    <div key={payment.id} className="rounded-md border p-3 text-sm">
                      <div className="flex justify-between gap-2">
                        <span className="font-medium">{payment.paymentMethodName} · {payment.status}</span>
                        <span>{money(payment.totalAmount, payment.currencyCode)}</span>
                      </div>
                      <div className="text-muted-foreground">
                        Receipt {payment.receiptNumber ?? 'pending'} · Journal {payment.journalEntryId ?? 'pending'} · Paid {dateTime(payment.paidAtUtc)}
                      </div>
                    </div>
                  ))}
                </div>
              </section>
              <section>
                <h2 className="mb-2 font-semibold">Exemption workflow history</h2>
                <div className="space-y-2">
                  {value.exemptions.length === 0 && <p className="text-sm text-muted-foreground">No exemption activity.</p>}
                  {value.exemptions.map((exemption) => (
                    <div key={exemption.id} className="rounded-md border p-3 text-sm">
                      <div className="font-medium">{exemption.status} · {dateTime(exemption.requestedAtUtc)}</div>
                      <div>{exemption.reason}</div>
                      <div className="text-muted-foreground">Workflow {exemption.workflowInstanceId ?? exemption.workflowDefinitionId}</div>
                    </div>
                  ))}
                </div>
              </section>
            </div>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(action)} onOpenChange={(open) => !open && closeAction()}>
        <DialogContent className="bg-white dark:bg-slate-950">
          <DialogHeader>
            <DialogTitle>{action ? actionTitles[action] : ''}</DialogTitle>
            <DialogDescription>
              The action is tenant-scoped, correlation-idempotent, audited, and
              protected by the shared procurement controls.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            {action === 'issue' && (
              <div><Label>Registration ID</Label><Input value={registrationId} onChange={(event) => setRegistrationId(event.target.value)} placeholder="Business-partner registration UUID" /></div>
            )}
            {action === 'payment' && (
              <>
                <div>
                  <Label>Payment method</Label>
                  <Select value={paymentMethodId} onValueChange={setPaymentMethodId}>
                    <SelectTrigger><SelectValue placeholder="Select an allowed posting-ready method" /></SelectTrigger>
                    <SelectContent>
                      {(paymentMethods.data ?? []).map((method) => (
                        <SelectItem key={method.id} value={method.id} disabled={!method.isPostingReady}>
                          {method.code} · {method.name}{method.isPostingReady ? '' : ' (GL missing)'}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div><Label>Payment reference</Label><Input value={reference} onChange={(event) => setReference(event.target.value)} /></div>
              </>
            )}
            {(action === 'reissue' || action === 'exemption') && (
              <div><Label>{action === 'reissue' ? 'Rotation reason' : 'Exemption reason'}</Label><Textarea value={reason} onChange={(event) => setReason(event.target.value)} /></div>
            )}
            {action === 'reconcile' && (
              <Alert className="border-blue-200 bg-blue-50 text-blue-950 dark:border-blue-900 dark:bg-blue-950/40 dark:text-blue-100">
                <ShieldCheck className="h-4 w-4" />
                <AlertTitle>Independent payment verification required</AlertTitle>
                <AlertDescription>
                  Confirm becomes available after both audit fields below are completed.
                  Use evidence obtained independently from the provider, bank, POS or
                  official cashier record—not an unverified reference supplied only by
                  the applicant.
                </AlertDescription>
              </Alert>
            )}
            {action === 'reconcile' && (
              <div className="space-y-2">
                <Label htmlFor="payment-verification-reference">
                  Provider transaction / cashier receipt reference <span aria-hidden="true">*</span>
                </Label>
                <Input
                  id="payment-verification-reference"
                  value={reference}
                  onChange={(event) => setReference(event.target.value)}
                  placeholder="MoMo transaction ID, bank reference, POS or cashier receipt no."
                />
                <p className="text-xs text-muted-foreground">
                  Enter the independently verifiable transaction or official receipt
                  reference used to match this payment.
                </p>
              </div>
            )}
            {(action === 'exemption' || action === 'approve-exemption' || action === 'reject-exemption') && (
              <div><Label>Shared evidence reference</Label><Input value={reference} onChange={(event) => setReference(event.target.value)} placeholder="Document or external evidence reference" /></div>
            )}
            {action === 'reconcile' && (
              <div className="space-y-2">
                <Label htmlFor="payment-verification-note">
                  Payment verification note <span aria-hidden="true">*</span>
                </Label>
                <Textarea
                  id="payment-verification-note"
                  value={notes}
                  onChange={(event) => setNotes(event.target.value)}
                  placeholder="How the amount, payer, date and settlement or receipt were independently confirmed"
                />
                <p className="text-xs text-muted-foreground">
                  Required audit evidence explaining what was checked before the token
                  is activated and the Finance posting is created.
                </p>
              </div>
            )}
            {(action === 'approve-exemption' || action === 'reject-exemption') && (
              <div><Label>Independent decision comment</Label><Textarea value={notes} onChange={(event) => setNotes(event.target.value)} /></div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={closeAction}>Cancel</Button>
            <Button
              onClick={run}
              disabled={
                busy ||
                (action === 'issue' && !registrationId.trim()) ||
                (action === 'payment' && !paymentMethodId) ||
                ((action === 'reissue' || action === 'exemption') && !reason.trim()) ||
                ((action === 'reconcile' || action === 'exemption' || action === 'approve-exemption' || action === 'reject-exemption') && !reference.trim()) ||
                ((action === 'reconcile' || action === 'approve-exemption' || action === 'reject-exemption') && !notes.trim())
              }
            >
              {busy ? 'Working…' : 'Confirm'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(plaintextToken)} onOpenChange={(open) => !open && setPlaintextToken(undefined)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Copy the token now</DialogTitle>
            <DialogDescription>
              This plaintext value is returned once and is not stored or shown again.
            </DialogDescription>
          </DialogHeader>
          <Input readOnly value={plaintextToken ?? ''} className="font-mono" />
          <DialogFooter>
            <Button
              onClick={async () => {
                await navigator.clipboard.writeText(plaintextToken ?? '');
                toast.success('Token copied');
              }}
            >
              Copy token
            </Button>
            <Button variant="outline" onClick={() => setPlaintextToken(undefined)}>I have saved it</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
