'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  Banknote,
  FileSignature,
  Loader2,
  Printer,
  Receipt,
  Undo2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { useToast } from '@/hooks/use-toast';
import { ASSET_CONDITIONS, RENTAL_FREQUENCIES } from '@/types/hr/assets';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtNum = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { maximumFractionDigits: 2 });
const today = () => new Date().toISOString().slice(0, 10);

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm">{children}</dd>
    </div>
  );
}

/**
 * One custody record, and the three ways it can end.
 *
 * A custody closes by a **return**, by an **incident** (it never came back), or by a **transfer**
 * to somebody else. All three are represented here, because before slice 7 the only closing act was
 * a return — so a lost laptop could be recorded only by pretending it had been handed back.
 *
 * ⚠ Acknowledgement is not an action on this screen and never will be. It is the employee's
 * testimony that they received the thing, refused for everybody but the holder, HR included. If
 * they cannot reach their own screen, the answer is the printed responsibility form and a physical
 * signature — not an administrator ticking a box in their name.
 */
export default function AssetAssignmentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [returning, setReturning] = useState(false);
  const [returnForm, setReturnForm] = useState({
    returnDate: today(),
    returnedToId: '',
    conditionAtReturn: 'Good',
    returnedInGoodCondition: true,
    returnNotes: '',
    damageReported: false,
    damageDescription: '',
    employeeLiable: false,
    repairCost: '',
    replacementCost: '',
  });

  const [incident, setIncident] = useState(false);
  const [incidentForm, setIncidentForm] = useState({
    outcome: '4', // Lost
    description: '',
    incidentDate: today(),
  });

  const [rentOpen, setRentOpen] = useState(false);
  const [rentForm, setRentForm] = useState({
    rentalAmount: '',
    rentalCurrencyCode: 'GHS',
    rentalFrequency: 'Monthly',
    rentalStartDate: today(),
    rentalEndDate: '',
    isBenefitInKind: false,
    benefitInKindValue: '',
    rentalNotes: '',
  });

  const [charging, setCharging] = useState(false);
  const [chargeForm, setChargeForm] = useState({ reason: '1', description: '', assessedAmount: '' });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['hr', 'assets'] });

  const { data: a, isLoading } = useQuery({
    queryKey: ['hr', 'assets', 'assignment', id],
    queryFn: () => assetRegisterService.getAssignment(id),
    enabled: Boolean(id),
  });

  const { data: surcharges = [] } = useQuery({
    queryKey: ['hr', 'assets', 'assignment', id, 'surcharges'],
    queryFn: () => assetRegisterService.getSurchargesForAssignment(id),
    enabled: Boolean(id),
  });

  /**
   * The responsibility document opens in its own window.
   *
   * `htmlBody` is a complete `<html>` document rendered from an HR-editable template plus the
   * tenant's company profile. Injecting a whole letter into this page's DOM is how a print
   * stylesheet ends up printing the navigation.
   */
  const openTerms = useMutation({
    mutationFn: () => assetRegisterService.getTermsDocument(id),
    onSuccess: (letter) => {
      const w = window.open('', '_blank', 'width=900,height=1000');
      if (!w) {
        toast({
          title: 'Your browser blocked the document window',
          description: 'Allow pop-ups for this site and try again.',
          variant: 'destructive',
        });
        return;
      }
      w.document.write(letter.htmlBody);
      w.document.close();
    },
    onError: (e: Error) =>
      toast({ title: 'Could not open the document', description: e.message, variant: 'destructive' }),
  });

  const doReturn = useMutation({
    mutationFn: () =>
      assetRegisterService.returnAsset(id, {
        returnDate: `${returnForm.returnDate}T00:00:00`,
        // ⚠ Required, and an EMPLOYEE id. Omit it and the API answers 404 naming an empty GUID.
        returnedToId: returnForm.returnedToId,
        conditionAtReturn:
          ASSET_CONDITIONS.find((c) => c.label === returnForm.conditionAtReturn)?.value ?? 2,
        returnedInGoodCondition: returnForm.returnedInGoodCondition,
        returnNotes: returnForm.returnNotes || null,
        damageReported: returnForm.damageReported,
        damageDescription: returnForm.damageReported ? returnForm.damageDescription || null : null,
        employeeLiable: returnForm.damageReported ? returnForm.employeeLiable : false,
        repairCost: returnForm.damageReported && returnForm.repairCost !== ''
          ? Number(returnForm.repairCost) : null,
        replacementCost: returnForm.damageReported && returnForm.replacementCost !== ''
          ? Number(returnForm.replacementCost) : null,
      }),
    onSuccess: () => { invalidate(); setReturning(false); toast({ title: 'Asset taken back' }); },
    onError: (e: Error) =>
      toast({ title: 'Could not record the return', description: e.message, variant: 'destructive' }),
  });

  const reportIncident = useMutation({
    mutationFn: () =>
      assetRegisterService.reportIncident(id, {
        outcome: Number(incidentForm.outcome),
        description: incidentForm.description,
        incidentDate: incidentForm.incidentDate || null,
      }),
    onSuccess: () => { invalidate(); setIncident(false); toast({ title: 'Incident recorded' }); },
    onError: (e: Error) =>
      toast({ title: 'Could not record the incident', description: e.message, variant: 'destructive' }),
  });

  const setRent = useMutation({
    mutationFn: () =>
      assetRegisterService.setRentalTerms(id, {
        rentalAmount: Number(rentForm.rentalAmount || 0),
        rentalCurrencyCode: rentForm.rentalCurrencyCode,
        rentalFrequency:
          RENTAL_FREQUENCIES.find((f) => f.label === rentForm.rentalFrequency)?.value ?? 1,
        rentalStartDate: rentForm.rentalStartDate,
        rentalEndDate: rentForm.rentalEndDate || null,
        isBenefitInKind: rentForm.isBenefitInKind,
        benefitInKindValue: rentForm.isBenefitInKind && rentForm.benefitInKindValue !== ''
          ? Number(rentForm.benefitInKindValue) : null,
        rentalNotes: rentForm.rentalNotes || null,
      }),
    onSuccess: () => { invalidate(); setRentOpen(false); toast({ title: 'Rental terms set' }); },
    onError: (e: Error) =>
      toast({ title: 'Could not set the terms', description: e.message, variant: 'destructive' }),
  });

  const clearRent = useMutation({
    mutationFn: () => assetRegisterService.clearRentalTerms(id),
    onSuccess: () => { invalidate(); toast({ title: 'Rental terms cleared' }); },
    onError: (e: Error) =>
      toast({ title: 'Could not clear the terms', description: e.message, variant: 'destructive' }),
  });

  const raiseCharge = useMutation({
    mutationFn: () =>
      assetRegisterService.createSurcharge({
        assignmentId: id,
        reason: Number(chargeForm.reason),
        description: chargeForm.description,
        assessedAmount: chargeForm.assessedAmount === '' ? null : Number(chargeForm.assessedAmount),
      }),
    onSuccess: () => { invalidate(); setCharging(false); toast({ title: 'Charge raised as a draft' }); },
    onError: (e: Error) =>
      toast({ title: 'Could not raise the charge', description: e.message, variant: 'destructive' }),
  });

  if (isLoading || !a) {
    return (
      <div className="flex justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const isOpen = a.status === 'Active';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={a.assignmentNumber}
        description={`${a.assetName} · held by ${a.employeeName}`}
        backHref="/hr/assets/assignments"
        actions={
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" onClick={() => openTerms.mutate()} disabled={openTerms.isPending}>
              {openTerms.isPending
                ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                : <Printer className="mr-2 h-4 w-4" />}
              Responsibility document
            </Button>
            {isOpen && (
              <>
                <Button onClick={() => setReturning(true)}>
                  <Undo2 className="mr-2 h-4 w-4" /> Take it back
                </Button>
                <Button variant="outline" onClick={() => setIncident(true)}>
                  <AlertTriangle className="mr-2 h-4 w-4" /> It never came back
                </Button>
              </>
            )}
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <StatusBadge status={a.statusName} />
        {a.employeeAcknowledged ? (
          <span className="flex items-center gap-1 text-sm text-emerald-600 dark:text-emerald-500">
            <FileSignature className="h-4 w-4" /> Signed for on {fmtDate(a.acknowledgementDate)}
          </span>
        ) : (
          <span className="flex items-center gap-1 text-sm text-amber-600 dark:text-amber-500">
            <FileSignature className="h-4 w-4" /> Not yet signed for by the holder
          </span>
        )}
        {isOpen && a.daysUntilReturnDue !== null && a.daysUntilReturnDue < 0 && (
          <span className="text-sm text-red-600 dark:text-red-500">
            {Math.abs(a.daysUntilReturnDue)} days past its return date
          </span>
        )}
      </div>

      <Card>
        <CardHeader><CardTitle className="text-base">The custody</CardTitle></CardHeader>
        <CardContent>
          <dl className="grid gap-4 sm:grid-cols-3">
            <Field label="Asset">
              <Link href={`/hr/assets/register/${a.assetId}`} className="hover:underline">
                {a.assetName} ({a.assetNumber})
              </Link>
            </Field>
            {/* D-ll: the record could not say what KIND of thing it was until slice 12 — the list
                row that opens this page has carried it since slice 6. */}
            <Field label="Asset type">{a.assetTypeName}</Field>
            <Field label="Held by">{a.employeeName}{a.employeeNumber ? ` · ${a.employeeNumber}` : ''}</Field>
            <Field label="Assignment type">{a.typeName}</Field>
            <Field label="Issued">{fmtDate(a.assignmentDate)}</Field>
            <Field label="Due back">
              {a.expectedReturnDate ? fmtDate(a.expectedReturnDate) : 'Open-ended'}
            </Field>
            <Field label="Purpose">{a.purposeName}</Field>
            <Field label="Condition when issued">{a.conditionAtAssignmentName}</Field>
            <Field label="Answerable for loss">{a.responsibleForLoss ? 'Yes' : 'No'}</Field>
            <Field label="Answerable for damage">{a.responsibleForDamage ? 'Yes' : 'No'}</Field>
            {a.requisitionNumber && (
              <Field label="From requisition">
                <Link href={`/hr/assets/requisitions/${a.requisitionId}`} className="hover:underline">
                  {a.requisitionNumber}
                </Link>
              </Field>
            )}
            {a.transferNumber && (
              <Field label="From transfer">
                <Link href={`/hr/assets/transfers/${a.transferId}`} className="hover:underline">
                  {a.transferNumber}
                </Link>
              </Field>
            )}
            <Field label="Terms document sent">
              {a.termsDocumentSentAt
                ? `${fmtDate(a.termsDocumentSentAt)} to ${a.termsDocumentSentTo ?? 'the holder'}`
                : 'Not sent'}
            </Field>
            {a.assignmentNotes && (
              <div className="sm:col-span-3"><Field label="Notes">{a.assignmentNotes}</Field></div>
            )}
          </dl>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center justify-between gap-2 text-base">
            <span className="flex items-center gap-2"><Banknote className="h-4 w-4" /> Rental — AST-10</span>
            {isOpen && (
              <span className="flex gap-2">
                <Button size="sm" variant="outline" onClick={() => setRentOpen(true)}>
                  {a.rentalAmount === null ? 'Set terms' : 'Change terms'}
                </Button>
                {a.rentalAmount !== null && (
                  <Button size="sm" variant="ghost" onClick={() => clearRent.mutate()}
                    disabled={clearRent.isPending}>Clear</Button>
                )}
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {a.rentalAmount === null ? (
            <p className="text-sm text-muted-foreground">
              No rental terms have been set for this custody.
            </p>
          ) : (
            <>
              <dl className="grid gap-4 sm:grid-cols-3">
                <Field label="Charge">
                  {a.rentalAmount === 0
                    ? 'Provided free of charge'
                    : `${a.rentalCurrencyCode ?? ''} ${fmtNum(a.rentalAmount)} ${a.rentalFrequencyName ?? ''}`.trim()}
                </Field>
                <Field label="Benefit in kind">{a.isBenefitInKind ? 'Yes' : 'No'}</Field>
              </dl>
              <p className="mt-3 text-xs text-muted-foreground">
                HR declares what is owed; payroll runs the deduction. The figure is not prorated —
                each pay run applies its own calendar to the periodic rate.
              </p>
            </>
          )}
        </CardContent>
      </Card>

      {(a.status === 'Returned' || a.status === 'Lost' || a.status === 'Damaged') && (
        <Card>
          <CardHeader><CardTitle className="text-base">How it ended</CardTitle></CardHeader>
          <CardContent>
            <dl className="grid gap-4 sm:grid-cols-3">
              <Field label="Closed on">{fmtDate(a.returnDate)}</Field>
              <Field label="Condition">{a.conditionAtReturnName ?? '—'}</Field>
              <Field label="Taken back by">{a.returnedToName ?? '—'}</Field>
              <Field label="Damage reported">{a.damageReported ? 'Yes' : 'No'}</Field>
              <Field label="Employee answerable">{a.employeeLiable ? 'Yes' : 'No'}</Field>
              <Field label="Repair / replacement">
                {fmtNum(a.repairCost)} / {fmtNum(a.replacementCost)}
              </Field>
              {a.damageDescription && (
                <div className="sm:col-span-3">
                  <Field label="What was wrong">{a.damageDescription}</Field>
                </div>
              )}
              {a.returnNotes && (
                <div className="sm:col-span-3"><Field label="Notes">{a.returnNotes}</Field></div>
              )}
            </dl>
            {/* The recorded costs establish that a charge is PERMISSIBLE and seed its default —
                what the employee is actually asked to pay is a separate decision (D9). */}
            {a.employeeLiable && surcharges.length === 0 && (
              <div className="mt-4 flex items-center justify-between gap-3 border-t pt-4">
                <p className="text-sm text-muted-foreground">
                  The record says the holder is answerable. Raising a charge is a separate decision,
                  and they have a right of reply before it is approved.
                </p>
                <Button size="sm" onClick={() => setCharging(true)}>
                  <Receipt className="mr-2 h-4 w-4" /> Raise a charge
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {surcharges.length > 0 && (
        <Card>
          <CardHeader><CardTitle className="text-base">Charges raised over this custody</CardTitle></CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Charge</TableHead>
                  <TableHead>Reason</TableHead>
                  <TableHead className="text-right">Assessed</TableHead>
                  <TableHead className="text-right">Outstanding</TableHead>
                  <TableHead>Employee&rsquo;s answer</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {surcharges.map((s) => (
                  <TableRow key={s.id}>
                    <TableCell>
                      <Link href={`/hr/assets/surcharges/${s.id}`} className="hover:underline">
                        {s.surchargeNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{s.reasonName}</TableCell>
                    <TableCell className="text-right">
                      {s.currencyCode} {fmtNum(s.assessedAmount)}
                    </TableCell>
                    <TableCell className="text-right">
                      {s.currencyCode} {fmtNum(s.amountOutstanding)}
                    </TableCell>
                    <TableCell>{s.employeeResponseName}</TableCell>
                    <TableCell><StatusBadge status={s.statusName} /></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {/* ── Take it back ────────────────────────────────────────────────────── */}
      <Dialog open={returning} onOpenChange={setReturning}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Take {a.assetName} back</DialogTitle>
            <DialogDescription>
              What you record here decides whether the holder can be charged at all.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>Returned on</Label>
              <Input type="date" value={returnForm.returnDate}
                onChange={(e) => setReturnForm((f) => ({ ...f, returnDate: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Condition</Label>
              <Select value={returnForm.conditionAtReturn}
                onValueChange={(v) => setReturnForm((f) => ({ ...f, conditionAtReturn: v }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {ASSET_CONDITIONS.map((c) =>
                    <SelectItem key={c.label} value={c.label}>{c.text}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2 sm:col-span-2">
              <Label>Taken back by *</Label>
              <EmployeePicker
                value={returnForm.returnedToId}
                onChange={(v) => setReturnForm((f) => ({ ...f, returnedToId: v ?? '' }))}
              />
            </div>
            <div className="flex items-center gap-2 sm:col-span-2">
              <Checkbox id="good" checked={returnForm.returnedInGoodCondition}
                onCheckedChange={(c) =>
                  setReturnForm((f) => ({ ...f, returnedInGoodCondition: c === true }))} />
              <Label htmlFor="good" className="font-normal">Came back in good condition</Label>
            </div>
            <div className="flex items-center gap-2 sm:col-span-2">
              <Checkbox id="damage" checked={returnForm.damageReported}
                onCheckedChange={(c) => setReturnForm((f) => ({ ...f, damageReported: c === true }))} />
              <Label htmlFor="damage" className="font-normal">There is damage to record</Label>
            </div>
            {returnForm.damageReported && (
              <>
                <div className="space-y-2 sm:col-span-2">
                  <Label>What is wrong</Label>
                  <Textarea rows={2} value={returnForm.damageDescription}
                    onChange={(e) =>
                      setReturnForm((f) => ({ ...f, damageDescription: e.target.value }))} />
                </div>
                <div className="flex items-center gap-2 sm:col-span-2">
                  <Checkbox id="liable" checked={returnForm.employeeLiable}
                    onCheckedChange={(c) =>
                      setReturnForm((f) => ({ ...f, employeeLiable: c === true }))} />
                  <Label htmlFor="liable" className="font-normal">
                    The holder is answerable for it
                  </Label>
                </div>
                <div className="space-y-2">
                  <Label>Repair cost</Label>
                  <Input type="number" step="0.01" value={returnForm.repairCost}
                    onChange={(e) => setReturnForm((f) => ({ ...f, repairCost: e.target.value }))} />
                </div>
                <div className="space-y-2">
                  <Label>Replacement cost</Label>
                  <Input type="number" step="0.01" value={returnForm.replacementCost}
                    onChange={(e) =>
                      setReturnForm((f) => ({ ...f, replacementCost: e.target.value }))} />
                </div>
              </>
            )}
            <div className="space-y-2 sm:col-span-2">
              <Label>Notes</Label>
              <Textarea rows={2} value={returnForm.returnNotes}
                onChange={(e) => setReturnForm((f) => ({ ...f, returnNotes: e.target.value }))} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setReturning(false)}>Cancel</Button>
            <Button onClick={() => doReturn.mutate()}
              disabled={!returnForm.returnedToId || doReturn.isPending}>
              {doReturn.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record the return
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── It never came back ──────────────────────────────────────────────── */}
      <Dialog open={incident} onOpenChange={setIncident}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record that {a.assetName} did not come back</DialogTitle>
            <DialogDescription>
              This closes the custody without pretending the asset was handed over.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>What happened</Label>
              <Select value={incidentForm.outcome}
                onValueChange={(v) => setIncidentForm((f) => ({ ...f, outcome: v }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="4">Lost or stolen</SelectItem>
                  <SelectItem value="5">Damaged beyond use</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>When</Label>
              <Input type="date" value={incidentForm.incidentDate}
                onChange={(e) => setIncidentForm((f) => ({ ...f, incidentDate: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Account of it *</Label>
              <Textarea rows={3} value={incidentForm.description}
                onChange={(e) => setIncidentForm((f) => ({ ...f, description: e.target.value }))} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIncident(false)}>Cancel</Button>
            <Button onClick={() => reportIncident.mutate()}
              disabled={!incidentForm.description.trim() || reportIncident.isPending}>
              {reportIncident.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record it
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Rental terms ────────────────────────────────────────────────────── */}
      <Dialog open={rentOpen} onOpenChange={setRentOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Rental terms</DialogTitle>
            <DialogDescription>
              HR declares what is owed. Payroll runs the deduction with its own calendar — nothing
              here is prorated.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>Amount</Label>
              <Input type="number" step="0.01" value={rentForm.rentalAmount}
                onChange={(e) => setRentForm((f) => ({ ...f, rentalAmount: e.target.value }))} />
              <p className="text-xs text-muted-foreground">
                Zero means provided free — a stated arrangement, usually taxable. To remove the
                terms entirely, use Clear.
              </p>
            </div>
            <div className="space-y-2">
              <Label>Currency</Label>
              <Input maxLength={3} value={rentForm.rentalCurrencyCode}
                onChange={(e) =>
                  setRentForm((f) => ({ ...f, rentalCurrencyCode: e.target.value.toUpperCase() }))} />
            </div>
            <div className="space-y-2">
              <Label>Frequency</Label>
              <Select value={rentForm.rentalFrequency}
                onValueChange={(v) => setRentForm((f) => ({ ...f, rentalFrequency: v }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {RENTAL_FREQUENCIES.map((f) =>
                    <SelectItem key={f.label} value={f.label}>{f.text}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Charged from</Label>
              <Input type="date" value={rentForm.rentalStartDate}
                onChange={(e) => setRentForm((f) => ({ ...f, rentalStartDate: e.target.value }))} />
            </div>
            <div className="flex items-center gap-2 sm:col-span-2">
              <Checkbox id="bik" checked={rentForm.isBenefitInKind}
                onCheckedChange={(c) => setRentForm((f) => ({ ...f, isBenefitInKind: c === true }))} />
              <Label htmlFor="bik" className="font-normal">
                Treat as a benefit in kind rather than a deduction
              </Label>
            </div>
            {rentForm.isBenefitInKind && (
              <div className="space-y-2 sm:col-span-2">
                <Label>Value of the benefit</Label>
                <Input type="number" step="0.01" value={rentForm.benefitInKindValue}
                  onChange={(e) =>
                    setRentForm((f) => ({ ...f, benefitInKindValue: e.target.value }))} />
                <p className="text-xs text-muted-foreground">
                  A flag cannot carry the value of a subsidy, so the amount is recorded beside it.
                </p>
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRentOpen(false)}>Cancel</Button>
            <Button onClick={() => setRent.mutate()} disabled={setRent.isPending}>
              {setRent.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Set terms
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Raise a charge ──────────────────────────────────────────────────── */}
      <Dialog open={charging} onOpenChange={setCharging}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Raise a charge</DialogTitle>
            <DialogDescription>
              It starts as a draft. Nothing reaches the employee until you serve it on them, and
              they may answer before it goes for approval.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Reason</Label>
              <Select value={chargeForm.reason}
                onValueChange={(v) => setChargeForm((f) => ({ ...f, reason: v }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="1">Damage</SelectItem>
                  <SelectItem value="2">Loss</SelectItem>
                  <SelectItem value="3">Not returned</SelectItem>
                  <SelectItem value="4">Other</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Amount</Label>
              <Input type="number" step="0.01" placeholder="Defaults to the replacement cost"
                value={chargeForm.assessedAmount}
                onChange={(e) => setChargeForm((f) => ({ ...f, assessedAmount: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>What is being charged for *</Label>
              <Textarea rows={3} value={chargeForm.description}
                onChange={(e) => setChargeForm((f) => ({ ...f, description: e.target.value }))} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCharging(false)}>Cancel</Button>
            <Button onClick={() => raiseCharge.mutate()}
              disabled={!chargeForm.description.trim() || raiseCharge.isPending}>
              {raiseCharge.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Raise as draft
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
