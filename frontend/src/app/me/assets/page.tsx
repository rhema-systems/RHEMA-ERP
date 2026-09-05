'use client';

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Boxes,
  CalendarClock,
  FileSignature,
  Loader2,
  Package,
  PenLine,
  Plus,
  Printer,
  Receipt,
  Send,
  ThumbsUp,
  Trash2,
  Undo2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { assetPortalService } from '@/services/hr/asset-portal.service';
import { employeeService } from '@/services/hr/employee.service';
import { toast } from 'sonner';
import { ASSET_REQUISITION_PRIORITIES } from '@/types/hr/assets';
import type {
  AssetAssignmentSummary,
  AssetRequisitionSummary,
  AssetSurchargeSummary,
} from '@/types/hr/assets';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const fmtMoney = (amount: number, currency: string) =>
  `${currency} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

const SELF = '__self__';

const EMPTY_FORM = {
  assetTypeId: '',
  beneficiaryEmployeeId: SELF,
  description: '',
  quantity: '1',
  priority: '3',
  justification: '',
  requiredByDate: '',
};

/**
 * The employee's own view of the company assets they hold — AST-6, AST-6b, AST-8, decision D4.
 *
 * Area 25 slice 9: re-homed from /hr/assets/me into the portal shell (D3 — moved, not
 * redirected), closing the area-16 residual: the acknowledge / terms-document / requisition /
 * surcharge-respond employee surface now lives where employees live. This slice also added the
 * charge DETAIL dialog — the by-id surcharge read existed with no screen.
 *
 * Four things live here, and they are the four an employee actually does: see what is in their
 * hands, sign for it, print what they signed, and ask for something. Everything is read from the
 * `api/employee-portal` routes, which take no employee id — the token supplies it.
 *
 * ⚠ Acknowledging is deliberately the only act on this page that nobody else can do for the
 * employee, HR included. If somebody cannot reach this screen at all, the answer is the printed
 * responsibility form and a physical signature, not an administrator ticking the box in their name.
 */
export default function MyAssetsPage() {
  const queryClient = useQueryClient();

  const [requesting, setRequesting] = useState(false);
  const [form, setForm] = useState(EMPTY_FORM);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [recalling, setRecalling] = useState<AssetRequisitionSummary | null>(null);
  const [recallReason, setRecallReason] = useState('');
  const [answering, setAnswering] = useState<{ row: AssetSurchargeSummary; accepted: boolean } | null>(null);
  const [answerComments, setAnswerComments] = useState('');
  const [chargeDetailId, setChargeDetailId] = useState<string | null>(null);

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['me', 'assets'] });
  };

  const { data: summary, isLoading } = useQuery({
    queryKey: ['me', 'assets', 'summary'],
    queryFn: () => assetPortalService.getMyAssetSummary(),
  });

  const { data: history = [] } = useQuery({
    queryKey: ['me', 'assets', 'history'],
    queryFn: () => assetPortalService.getMyAssetHistory(),
  });

  const { data: requisitions = [] } = useQuery({
    queryKey: ['me', 'assets', 'requisitions'],
    queryFn: () => assetPortalService.getMyRequisitions(),
  });

  const { data: surcharges = [] } = useQuery({
    queryKey: ['me', 'assets', 'surcharges'],
    queryFn: () => assetPortalService.getMySurcharges(),
  });

  const { data: chargeDetail } = useQuery({
    queryKey: ['me', 'assets', 'surcharges', chargeDetailId],
    queryFn: () => assetPortalService.getMySurcharge(chargeDetailId as string),
    enabled: !!chargeDetailId,
  });

  // Only fetched once the form is open — most employees are nobody's manager, and this is a
  // request the vast majority of page loads would make for nothing.
  const { data: assetTypes = [] } = useQuery({
    queryKey: ['me', 'assets', 'types'],
    queryFn: () => assetPortalService.getAssetTypes(),
    enabled: requesting,
  });

  const { data: directReports = [] } = useQuery({
    queryKey: ['me', 'direct-reports'],
    queryFn: () => employeeService.getMyDirectReports(),
    enabled: requesting,
  });

  const held = summary?.held ?? [];
  const returned = history.filter((a) => a.status !== 'Active');

  // ⚠ The requests list holds BOTH what this employee raised and what was raised for them, so
  // "can I act on this row" is a real question and not a formality. Editing, sending, withdrawing
  // and pulling back all belong to the requester alone — the server refuses the beneficiary — and
  // offering the buttons anyway would be an invitation to a 403. The summary supplies the caller's
  // own employee id, which is the only reason this page never has to ask for one.
  const myEmployeeId = summary?.employeeId;

  const acknowledge = useMutation({
    mutationFn: (assignmentId: string) => assetPortalService.acknowledge(assignmentId),
    onSuccess: () => {
      toast.success('Receipt acknowledged — the record now shows your signature.');
      invalidate();
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : 'Refused'),
  });

  /**
   * Opens the letter in its own window. The document is a complete, self-contained HTML page meant
   * for a printer and a pen, so it is not injected into this one — a whole document inside a
   * screen's DOM is how a print stylesheet ends up printing the navigation around it.
   */
  const printTerms = useMutation({
    mutationFn: (assignmentId: string) => assetPortalService.getTermsDocument(assignmentId),
    onSuccess: (letter) => {
      const w = window.open('', '_blank');
      if (!w) {
        toast.error('Pop-up blocked — allow pop-ups for this site to print your responsibility form.');
        return;
      }
      w.document.write(letter.htmlBody);
      w.document.close();
    },
    onError: (error) =>
      toast.error(error instanceof Error ? error.message : 'Could not open the document'),
  });

  const saveRequest = useMutation({
    mutationFn: () => {
      const payload = {
        assetTypeId: form.assetTypeId,
        beneficiaryEmployeeId:
          form.beneficiaryEmployeeId === SELF ? null : form.beneficiaryEmployeeId,
        description: form.description,
        quantity: Number(form.quantity) || 1,
        priority: Number(form.priority),
        justification: form.justification,
        requiredByDate: form.requiredByDate || null,
      };
      return editingId
        ? assetPortalService.updateRequisition(editingId, payload)
        : assetPortalService.createRequisition(payload);
    },
    onSuccess: () => {
      toast.success(
        editingId
          ? 'Request updated'
          : 'Request saved as a draft — send it for approval when you are ready.',
      );
      setRequesting(false);
      setEditingId(null);
      setForm(EMPTY_FORM);
      invalidate();
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : 'Refused'),
  });

  const submitRequest = useMutation({
    mutationFn: (id: string) => assetPortalService.submitRequisition(id),
    onSuccess: () => {
      toast.success('Sent for approval');
      invalidate();
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : 'Refused'),
  });

  const recallRequest = useMutation({
    mutationFn: () => {
      if (!recalling) throw new Error('No request selected.');
      return assetPortalService.recallRequisition(recalling.id, recallReason || undefined);
    },
    onSuccess: () => {
      toast.success('Pulled back to draft');
      setRecalling(null);
      setRecallReason('');
      invalidate();
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : 'Refused'),
  });

  const withdrawRequest = useMutation({
    mutationFn: (id: string) => assetPortalService.withdrawRequisition(id),
    onSuccess: () => {
      toast.success('Request withdrawn');
      invalidate();
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : 'Refused'),
  });

  /**
   * The employee's right of reply — AST-3, decision D9.
   *
   * Disputing does not cancel the charge and the copy says so, because a button that reads like a
   * veto and behaves like a comment is worse than no button. What it does is put their account in
   * front of the approver, who may then lower the charge and may never raise it.
   */
  const answerSurcharge = useMutation({
    mutationFn: () => {
      if (!answering) throw new Error('No charge selected.');
      return assetPortalService.respondToSurcharge(
        answering.row.id,
        answering.accepted,
        answerComments || undefined,
      );
    },
    onSuccess: () => {
      toast.success(
        answering?.accepted
          ? 'Charge accepted'
          : 'Response recorded — your account goes to the approver with the charge.',
      );
      setAnswering(null);
      setAnswerComments('');
      invalidate();
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : 'Refused'),
  });

  const openNew = () => {
    setEditingId(null);
    setForm(EMPTY_FORM);
    setRequesting(true);
  };

  const openEdit = async (row: AssetRequisitionSummary) => {
    try {
      const full = await assetPortalService.getMyRequisition(row.id);
      setEditingId(full.id);
      setForm({
        assetTypeId: full.assetTypeId,
        beneficiaryEmployeeId: full.beneficiaryEmployeeId ?? SELF,
        description: full.description,
        quantity: String(full.quantity),
        priority: String(
          ASSET_REQUISITION_PRIORITIES.find((p) => p.label === full.priority)?.value ?? 3,
        ),
        justification: full.justification,
        requiredByDate: full.requiredByDate ? full.requiredByDate.slice(0, 10) : '',
      });
      setRequesting(true);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Could not open the request');
    }
  };

  const canSave =
    form.assetTypeId.trim() !== '' &&
    form.description.trim() !== '' &&
    form.justification.trim() !== '' &&
    Number(form.quantity) > 0;

  const renderHeldRow = (a: AssetAssignmentSummary) => (
    <TableRow key={a.id}>
      <TableCell className="font-medium">
        {a.assetName}
        <div className="text-xs text-muted-foreground">
          {a.assetNumber} · {a.assetTypeName}
        </div>
        {/*
          AST-10. Shown on the row rather than a click away, because this is where somebody finds
          out they are paying rent for a company flat — and a deduction discovered on a payslip
          instead of here is the version of this feature nobody wants. `!= null` on purpose: a rent
          of zero is a real arrangement and says "provided free", which is worth stating.
        */}
        {a.rentalAmount != null && (
          <div className="text-xs font-medium text-amber-700 dark:text-amber-500">
            {a.rentalAmount > 0
              ? `${a.rentalCurrencyCode} ${a.rentalAmount.toLocaleString()} ${(
                  a.rentalFrequencyName ?? ''
                ).toLowerCase()}`
              : 'Provided free'}
            {a.isBenefitInKind && ' · taxable benefit'}
          </div>
        )}
      </TableCell>
      <TableCell>{fmtDate(a.assignmentDate)}</TableCell>
      <TableCell>{fmtDate(a.expectedReturnDate)}</TableCell>
      <TableCell>
        <StatusBadge status={a.statusName} />
      </TableCell>
      <TableCell>
        {a.employeeAcknowledged ? (
          <span className="text-sm text-muted-foreground">
            Signed {fmtDate(a.acknowledgementDate)}
          </span>
        ) : (
          <Button
            size="sm"
            onClick={() => acknowledge.mutate(a.id)}
            disabled={acknowledge.isPending}
          >
            <FileSignature className="mr-2 h-4 w-4" />
            Acknowledge receipt
          </Button>
        )}
      </TableCell>
      <TableCell className="text-right">
        <Button
          size="sm"
          variant="ghost"
          onClick={() => printTerms.mutate(a.id)}
          disabled={printTerms.isPending}
        >
          <Printer className="mr-2 h-4 w-4" />
          Terms
        </Button>
      </TableCell>
    </TableRow>
  );

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Assets"
        description="The company property in your hands, what you have signed for, and anything you have asked for."
        backHref="/me"
        actions={
          <Button onClick={openNew}>
            <Plus className="mr-2 h-4 w-4" />
            Request an asset
          </Button>
        }
      />

      {isLoading ? (
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" /> Loading your assets…
        </div>
      ) : (
        <MetricTiles
          tiles={[
            { label: 'In your hands', value: summary?.heldCount ?? 0, icon: Package },
            {
              label: 'Not yet signed for',
              value: summary?.awaitingAcknowledgementCount ?? 0,
              icon: FileSignature,
              tone: (summary?.awaitingAcknowledgementCount ?? 0) > 0 ? 'warning' : 'default',
              hint: (summary?.awaitingAcknowledgementCount ?? 0) > 0 ? 'Acknowledge below' : undefined,
            },
            {
              label: 'Past their return date',
              value: summary?.overdueReturnCount ?? 0,
              icon: CalendarClock,
              tone: (summary?.overdueReturnCount ?? 0) > 0 ? 'danger' : 'default',
            },
            {
              label: 'Open requests',
              value: summary?.openRequisitionCount ?? 0,
              icon: Boxes,
              hint:
                (summary?.draftRequisitionCount ?? 0) > 0
                  ? `${summary?.draftRequisitionCount} still a draft`
                  : undefined,
            },
            {
              label: 'Charges to answer',
              value: summary?.surchargesAwaitingMyResponseCount ?? 0,
              icon: Receipt,
              // The one figure on this page where the employee's own silence has a consequence:
              // a charge they never answer goes to an approver anyway, with a note saying why.
              tone: (summary?.surchargesAwaitingMyResponseCount ?? 0) > 0 ? 'danger' : 'default',
              hint:
                (summary?.openSurchargeCount ?? 0) > 0
                  ? `${summary?.openSurchargeCount} charge(s) open`
                  : undefined,
            },
          ]}
        />
      )}

      <Tabs defaultValue="held">
        <TabsList>
          <TabsTrigger value="held">In your hands ({held.length})</TabsTrigger>
          <TabsTrigger value="history">Previously held ({returned.length})</TabsTrigger>
          <TabsTrigger value="requests">Requests ({requisitions.length})</TabsTrigger>
          <TabsTrigger value="charges">Charges ({surcharges.length})</TabsTrigger>
        </TabsList>

        {/* ── what I hold ─────────────────────────────────────────────────── */}
        <TabsContent value="held" className="mt-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Company property issued to you</CardTitle>
              <CardDescription>
                Signing for an asset records that you received it and accept the terms printed on
                the responsibility form. Nobody can do that for you.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {held.length === 0 ? (
                <EmptyState
                  icon={Package}
                  title="Nothing is currently issued to you"
                  description="Assets you are given will appear here as soon as HR records them."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Asset</TableHead>
                      <TableHead>Issued</TableHead>
                      <TableHead>Due back</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Receipt</TableHead>
                      <TableHead className="text-right">Document</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>{held.map(renderHeldRow)}</TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── history ─────────────────────────────────────────────────────── */}
        <TabsContent value="history" className="mt-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Assets you have held before</CardTitle>
            </CardHeader>
            <CardContent>
              {returned.length === 0 ? (
                <EmptyState icon={Undo2} title="You have not returned anything yet" />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Asset</TableHead>
                      <TableHead>Issued</TableHead>
                      <TableHead>Closed</TableHead>
                      <TableHead>Outcome</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {returned.map((a) => (
                      <TableRow key={a.id}>
                        <TableCell className="font-medium">
                          {a.assetName}
                          <div className="text-xs text-muted-foreground">{a.assetNumber}</div>
                        </TableCell>
                        <TableCell>{fmtDate(a.assignmentDate)}</TableCell>
                        <TableCell>{fmtDate(a.returnDate)}</TableCell>
                        <TableCell>
                          <StatusBadge status={a.statusName} />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── requests ────────────────────────────────────────────────────── */}
        <TabsContent value="requests" className="mt-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Requests you raised, and requests raised for you</CardTitle>
              <CardDescription>
                A new request is a draft until you send it for approval. Requests your manager or HR
                raised on your behalf appear here too.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {requisitions.length === 0 ? (
                <EmptyState
                  icon={Boxes}
                  title="You have not asked for anything"
                  description="Use “Request an asset” above to raise one."
                  action={
                    <Button size="sm" onClick={openNew}>
                      <Plus className="mr-2 h-4 w-4" />
                      Request an asset
                    </Button>
                  }
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Request</TableHead>
                      <TableHead>For</TableHead>
                      <TableHead>Raised</TableHead>
                      <TableHead>Priority</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {requisitions.map((r) => {
                      const mine = !!myEmployeeId && r.requestedById === myEmployeeId;
                      return (
                        <TableRow key={r.id}>
                          <TableCell className="font-medium">
                            {r.requisitionNumber}
                            <div className="text-xs text-muted-foreground">
                              {r.quantity} × {r.assetTypeName}
                            </div>
                          </TableCell>
                          <TableCell>
                            {r.forEmployeeName}
                            {r.isOnBehalf && (
                              <div className="text-xs text-muted-foreground">
                                raised by {r.requestedByName}
                              </div>
                            )}
                          </TableCell>
                          <TableCell>{fmtDate(r.requestDate)}</TableCell>
                          <TableCell>{r.priorityName}</TableCell>
                          <TableCell>
                            <StatusBadge status={r.statusName} />
                          </TableCell>
                          <TableCell className="text-right">
                            <div className="flex justify-end gap-1">
                              {r.status === 'Draft' && mine && (
                                <>
                                  <Button size="sm" variant="ghost" onClick={() => openEdit(r)}>
                                    <PenLine className="mr-1 h-4 w-4" />
                                    Edit
                                  </Button>
                                  <Button
                                    size="sm"
                                    onClick={() => submitRequest.mutate(r.id)}
                                    disabled={submitRequest.isPending}
                                  >
                                    <Send className="mr-1 h-4 w-4" />
                                    Send
                                  </Button>
                                  <Button
                                    size="sm"
                                    variant="ghost"
                                    onClick={() => withdrawRequest.mutate(r.id)}
                                    disabled={withdrawRequest.isPending}
                                  >
                                    <Trash2 className="h-4 w-4" />
                                  </Button>
                                </>
                              )}
                              {r.status === 'Submitted' && mine && (
                                <Button
                                  size="sm"
                                  variant="outline"
                                  onClick={() => {
                                    setRecalling(r);
                                    setRecallReason('');
                                  }}
                                >
                                  <Undo2 className="mr-1 h-4 w-4" />
                                  Pull back
                                </Button>
                              )}
                            </div>
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── charges raised against me ────────────────────────────────────── */}
        <TabsContent value="charges" className="mt-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Charges raised against you</CardTitle>
              <CardDescription>
                If a company asset in your care is damaged or lost, you may be asked to pay towards
                it. You will always be asked before a charge goes to an approver — and what you say
                goes with it.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {surcharges.length === 0 ? (
                <EmptyState
                  icon={Receipt}
                  title="Nothing has been charged to you"
                  description="Charges appear here as soon as they are put to you."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Charge</TableHead>
                      <TableHead>Asset</TableHead>
                      <TableHead>Amount</TableHead>
                      <TableHead>Outstanding</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="text-right">Your response</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {surcharges.map((c) => {
                      const mustAnswer =
                        c.status === 'WithEmployee' && c.employeeResponse === 'NotYetGiven';
                      return (
                        <TableRow key={c.id}>
                          <TableCell className="font-medium">
                            {c.surchargeNumber}
                            <div className="text-xs text-muted-foreground">
                              {c.reasonName} · {fmtDate(c.raisedAt)}
                            </div>
                          </TableCell>
                          <TableCell>
                            {c.assetName}
                            <div className="text-xs text-muted-foreground">{c.assetNumber}</div>
                          </TableCell>
                          <TableCell className="tabular-nums">
                            {fmtMoney(c.assessedAmount, c.currencyCode)}
                          </TableCell>
                          <TableCell className="tabular-nums">
                            {fmtMoney(c.amountOutstanding, c.currencyCode)}
                          </TableCell>
                          <TableCell>
                            <StatusBadge status={c.statusName} />
                          </TableCell>
                          <TableCell className="text-right">
                            <div className="flex items-center justify-end gap-2">
                              {mustAnswer ? (
                                <>
                                  <Button
                                    size="sm"
                                    onClick={() => {
                                      setAnswering({ row: c, accepted: true });
                                      setAnswerComments('');
                                    }}
                                  >
                                    <ThumbsUp className="mr-1 h-4 w-4" />
                                    Accept
                                  </Button>
                                  <Button
                                    size="sm"
                                    variant="outline"
                                    onClick={() => {
                                      setAnswering({ row: c, accepted: false });
                                      setAnswerComments('');
                                    }}
                                  >
                                    Dispute
                                  </Button>
                                </>
                              ) : (
                                <span className="text-sm text-muted-foreground">
                                  {c.employeeResponse === 'NotYetGiven'
                                    ? 'You did not respond'
                                    : c.employeeResponseName}
                                </span>
                              )}
                              <Button
                                size="sm"
                                variant="ghost"
                                onClick={() => setChargeDetailId(c.id)}
                              >
                                Details
                              </Button>
                            </div>
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* ── the request form ──────────────────────────────────────────────── */}
      <Dialog
        open={requesting}
        onOpenChange={(open) => {
          if (!open) {
            setRequesting(false);
            setEditingId(null);
          }
        }}
      >
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{editingId ? 'Edit your request' : 'Request an asset'}</DialogTitle>
            <DialogDescription>
              This is saved as a draft. Nothing goes to an approver until you send it.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label>What do you need?</Label>
              <Select
                value={form.assetTypeId}
                onValueChange={(v) => setForm((f) => ({ ...f, assetTypeId: v }))}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Choose an asset type" />
                </SelectTrigger>
                <SelectContent>
                  {assetTypes.map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/*
              AST-6b. Only rendered when the caller actually has reports — the server refuses a
              beneficiary who is not one of them, so offering the field to everybody would be an
              invitation to a 403.
            */}
            {directReports.length > 0 && (
              <div className="space-y-2">
                <Label>Who is it for?</Label>
                <Select
                  value={form.beneficiaryEmployeeId}
                  onValueChange={(v) => setForm((f) => ({ ...f, beneficiaryEmployeeId: v }))}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={SELF}>Me</SelectItem>
                    {directReports.map((e) => (
                      <SelectItem key={e.id} value={e.id}>
                        {e.displayName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label>How many?</Label>
                <Input
                  type="number"
                  min={1}
                  value={form.quantity}
                  onChange={(e) => setForm((f) => ({ ...f, quantity: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label>How urgent?</Label>
                <Select
                  value={form.priority}
                  onValueChange={(v) => setForm((f) => ({ ...f, priority: v }))}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {ASSET_REQUISITION_PRIORITIES.map((p) => (
                      <SelectItem key={p.value} value={String(p.value)}>
                        {p.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="space-y-2">
              <Label>What exactly?</Label>
              <Textarea
                rows={2}
                placeholder="e.g. A 14-inch laptop with 16GB of memory"
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              />
            </div>

            <div className="space-y-2">
              <Label>Why do you need it?</Label>
              <Textarea
                rows={3}
                placeholder="The approver reads this and nothing else, so say enough."
                value={form.justification}
                onChange={(e) => setForm((f) => ({ ...f, justification: e.target.value }))}
              />
            </div>

            <div className="space-y-2">
              <Label>Needed by (optional)</Label>
              <Input
                type="date"
                value={form.requiredByDate}
                onChange={(e) => setForm((f) => ({ ...f, requiredByDate: e.target.value }))}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setRequesting(false)}>
              Cancel
            </Button>
            <Button onClick={() => saveRequest.mutate()} disabled={!canSave || saveRequest.isPending}>
              {saveRequest.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {editingId ? 'Save changes' : 'Save as draft'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── pulling a submitted request back ──────────────────────────────── */}
      <Dialog open={!!recalling} onOpenChange={(open) => !open && setRecalling(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Pull back {recalling?.requisitionNumber}</DialogTitle>
            <DialogDescription>
              This returns the request to draft so you can change it. It can only be pulled back
              while it is still waiting for a decision.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Why (optional)</Label>
            <Textarea
              rows={3}
              value={recallReason}
              onChange={(e) => setRecallReason(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRecalling(null)}>
              Cancel
            </Button>
            <Button onClick={() => recallRequest.mutate()} disabled={recallRequest.isPending}>
              {recallRequest.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Pull back to draft
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── the full charge (area 25 slice 9 — the by-id read finally has a screen) ── */}
      <Dialog open={!!chargeDetailId} onOpenChange={(open) => !open && setChargeDetailId(null)}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>
              Charge <span className="font-mono text-base">{chargeDetail?.surchargeNumber ?? ''}</span>
            </DialogTitle>
          </DialogHeader>
          {chargeDetail && (
            <div className="space-y-3 text-sm">
              <div>
                <p className="text-muted-foreground">What it is for</p>
                <p>{chargeDetail.description}</p>
              </div>
              <div className="grid gap-x-6 gap-y-2 sm:grid-cols-2">
                <div>
                  <p className="text-muted-foreground">Asset</p>
                  <p>
                    {chargeDetail.assetName}{' '}
                    <span className="text-muted-foreground font-mono text-xs">
                      {chargeDetail.assetNumber}
                    </span>
                  </p>
                </div>
                <div>
                  <p className="text-muted-foreground">Status</p>
                  <StatusBadge status={chargeDetail.statusName} />
                </div>
                <div>
                  <p className="text-muted-foreground">Amount assessed</p>
                  <p className="tabular-nums">
                    {fmtMoney(chargeDetail.assessedAmount, chargeDetail.currencyCode)}
                  </p>
                </div>
                <div>
                  <p className="text-muted-foreground">Still outstanding</p>
                  <p className="tabular-nums">
                    {fmtMoney(chargeDetail.amountOutstanding, chargeDetail.currencyCode)}
                  </p>
                </div>
                <div>
                  <p className="text-muted-foreground">Judged from</p>
                  <p className="tabular-nums">
                    repair {fmtMoney(chargeDetail.basisRepairCost ?? 0, chargeDetail.currencyCode)} ·
                    replacement{' '}
                    {fmtMoney(chargeDetail.basisReplacementCost ?? 0, chargeDetail.currencyCode)}
                  </p>
                </div>
                <div>
                  <p className="text-muted-foreground">Put to you</p>
                  <p>{fmtDate(chargeDetail.notifiedAt)}</p>
                </div>
              </div>
              <div>
                <p className="text-muted-foreground">Your response</p>
                <p>
                  {chargeDetail.employeeResponse === 'NotYetGiven'
                    ? 'Not answered yet'
                    : `${chargeDetail.employeeResponseName} on ${fmtDate(chargeDetail.employeeRespondedAt)}`}
                </p>
                {chargeDetail.employeeResponseComments && (
                  <p className="text-muted-foreground mt-1 italic">
                    &ldquo;{chargeDetail.employeeResponseComments}&rdquo;
                  </p>
                )}
              </div>
              {chargeDetail.approvalDate && (
                <div>
                  <p className="text-muted-foreground">Decision</p>
                  <p>
                    Approved {fmtDate(chargeDetail.approvalDate)}
                    {chargeDetail.approvalComments ? ` — ${chargeDetail.approvalComments}` : ''}
                  </p>
                </div>
              )}
              {chargeDetail.waivedAt && (
                <div>
                  <p className="text-muted-foreground">Waived</p>
                  <p>
                    {fmtDate(chargeDetail.waivedAt)}
                    {chargeDetail.waiverReason ? ` — ${chargeDetail.waiverReason}` : ''}
                  </p>
                </div>
              )}
              {chargeDetail.recoveries.length > 0 && (
                <div>
                  <p className="text-muted-foreground">Recovered so far</p>
                  <p className="tabular-nums">
                    {fmtMoney(chargeDetail.amountRecovered, chargeDetail.currencyCode)}
                  </p>
                </div>
              )}
            </div>
          )}
        </DialogContent>
      </Dialog>

      {/* ── answering a charge (AST-3, decision D9) ───────────────────────── */}
      <Dialog open={!!answering} onOpenChange={(open) => !open && setAnswering(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>
              {answering?.accepted ? 'Accept this charge' : 'Dispute this charge'}
            </DialogTitle>
            <DialogDescription>
              {answering
                ? `${answering.row.surchargeNumber} — ${fmtMoney(
                    answering.row.assessedAmount,
                    answering.row.currencyCode,
                  )} for ${answering.row.assetName}.`
                : ''}
            </DialogDescription>
          </DialogHeader>

          {/*
            Said plainly, because the alternative is a button that reads like a veto and behaves
            like a comment. A dispute does not cancel the charge; it goes to the approver with it,
            and they may reduce it. They cannot increase it.
          */}
          <p className="text-sm text-muted-foreground">
            {answering?.accepted
              ? 'Accepting confirms you agree to pay this amount. It still goes to an approver before anything is recovered.'
              : 'Disputing does not cancel the charge. Your account goes to the approver together with it, and they may reduce the amount — they cannot increase it.'}
          </p>

          <div className="space-y-2">
            <Label>
              {answering?.accepted ? 'Anything to add (optional)' : 'What happened, in your words'}
            </Label>
            <Textarea
              rows={4}
              value={answerComments}
              onChange={(e) => setAnswerComments(e.target.value)}
              placeholder={
                answering?.accepted ? '' : 'The approver reads this before deciding.'
              }
            />
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setAnswering(null)}>
              Cancel
            </Button>
            <Button
              onClick={() => answerSurcharge.mutate()}
              disabled={answerSurcharge.isPending || (!answering?.accepted && !answerComments.trim())}
            >
              {answerSurcharge.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {answering?.accepted ? 'Accept the charge' : 'Submit my response'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
