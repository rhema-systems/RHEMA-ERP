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
  Send,
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
import { useToast } from '@/hooks/use-toast';
import { ASSET_REQUISITION_PRIORITIES } from '@/types/hr/assets';
import type { AssetAssignmentSummary, AssetRequisitionSummary } from '@/types/hr/assets';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

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
 * Four things live here, and they are the four an employee actually does: see what is in their
 * hands, sign for it, print what they signed, and ask for something. Everything is read from the
 * `api/employee-portal` routes, which take no employee id — the token supplies it.
 *
 * ⚠ Acknowledging is deliberately the only act on this page that nobody else can do for the
 * employee, HR included. If somebody cannot reach this screen at all, the answer is the printed
 * responsibility form and a physical signature, not an administrator ticking the box in their name.
 */
export default function MyAssetsPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [requesting, setRequesting] = useState(false);
  const [form, setForm] = useState(EMPTY_FORM);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [recalling, setRecalling] = useState<AssetRequisitionSummary | null>(null);
  const [recallReason, setRecallReason] = useState('');

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['hr', 'assets', 'mine'] });
  };

  const { data: summary, isLoading } = useQuery({
    queryKey: ['hr', 'assets', 'mine', 'summary'],
    queryFn: () => assetPortalService.getMyAssetSummary(),
  });

  const { data: history = [] } = useQuery({
    queryKey: ['hr', 'assets', 'mine', 'history'],
    queryFn: () => assetPortalService.getMyAssetHistory(),
  });

  const { data: requisitions = [] } = useQuery({
    queryKey: ['hr', 'assets', 'mine', 'requisitions'],
    queryFn: () => assetPortalService.getMyRequisitions(),
  });

  // Only fetched once the form is open — most employees are nobody's manager, and this is a
  // request the vast majority of page loads would make for nothing.
  const { data: assetTypes = [] } = useQuery({
    queryKey: ['hr', 'assets', 'types'],
    queryFn: () => assetPortalService.getAssetTypes(),
    enabled: requesting,
  });

  const { data: directReports = [] } = useQuery({
    queryKey: ['hr', 'employees', 'my-direct-reports'],
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
      toast({ title: 'Receipt acknowledged', description: 'Thank you — the record now shows your signature.' });
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Refused', description: error?.message, variant: 'destructive' }),
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
        toast({
          title: 'Pop-up blocked',
          description: 'Allow pop-ups for this site to print your responsibility form.',
          variant: 'destructive',
        });
        return;
      }
      w.document.write(letter.htmlBody);
      w.document.close();
    },
    onError: (error: any) =>
      toast({ title: 'Could not open the document', description: error?.message, variant: 'destructive' }),
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
      toast({
        title: editingId ? 'Request updated' : 'Request saved as a draft',
        description: editingId
          ? undefined
          : 'It has not gone anywhere yet — send it for approval when you are ready.',
      });
      setRequesting(false);
      setEditingId(null);
      setForm(EMPTY_FORM);
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Refused', description: error?.message, variant: 'destructive' }),
  });

  const submitRequest = useMutation({
    mutationFn: (id: string) => assetPortalService.submitRequisition(id),
    onSuccess: () => {
      toast({ title: 'Sent for approval' });
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Refused', description: error?.message, variant: 'destructive' }),
  });

  const recallRequest = useMutation({
    mutationFn: () => {
      if (!recalling) throw new Error('No request selected.');
      return assetPortalService.recallRequisition(recalling.id, recallReason || undefined);
    },
    onSuccess: () => {
      toast({ title: 'Pulled back to draft' });
      setRecalling(null);
      setRecallReason('');
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Refused', description: error?.message, variant: 'destructive' }),
  });

  const withdrawRequest = useMutation({
    mutationFn: (id: string) => assetPortalService.withdrawRequisition(id),
    onSuccess: () => {
      toast({ title: 'Request withdrawn' });
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Refused', description: error?.message, variant: 'destructive' }),
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
    } catch (error: any) {
      toast({ title: 'Could not open the request', description: error?.message, variant: 'destructive' });
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
    <div className="space-y-6 p-6">
      <PageHeader
        title="My assets"
        description="The company property in your hands, what you have signed for, and anything you have asked for."
        backHref="/hr"
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
          ]}
        />
      )}

      <Tabs defaultValue="held">
        <TabsList>
          <TabsTrigger value="held">In your hands ({held.length})</TabsTrigger>
          <TabsTrigger value="history">Previously held ({returned.length})</TabsTrigger>
          <TabsTrigger value="requests">Requests ({requisitions.length})</TabsTrigger>
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
    </div>
  );
}
