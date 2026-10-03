'use client';

import { use, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, Plus, Send, Trash2, Upload } from 'lucide-react';
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
} from '@/components/hr/employee/tabs/fields';
import { CurrencyField } from '@/components/hr/common/CurrencyPicker';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { fmtTravelMoney as fmtMoney } from '@/components/hr/travel/travel-format';
import { useToast } from '@/hooks/use-toast';
import { travelService } from '@/services/hr/travel.service';
import type { StaffTravelExpenseClaimLine, TravelExpenseCategory } from '@/types/hr/travel-finance';

const EXPENSE_CATEGORIES: TravelExpenseCategory[] = [
  'Airfare', 'Accommodation', 'Meals', 'LocalTransport', 'TaxiRideshare', 'CarRental', 'Fuel',
  'VisaFees', 'Insurance', 'Communication', 'ConferenceFees', 'GiftsEntertainment', 'TipsGratuity',
  'Laundry', 'Medical', 'BaggageFees', 'Miscellaneous',
];

const humanize = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const options = (values: readonly string[]) => values.map((v) => ({ value: v, label: humanize(v) }));
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');

const lineSchema = z.object({
  expenseCategory: z.enum(EXPENSE_CATEGORIES as [string, ...string[]]),
  expenseDate: z.string().min(1, 'Required'),
  description: z.string().max(500).optional(),
  merchantName: z.string().max(200).optional(),
  amountOriginal: z.coerce.number().positive('Enter the amount spent'),
  currencyOriginal: z.string().min(1, 'Select a currency'),
  receiptAttachmentId: z.string().optional(),
  isPerDiem: z.boolean(),
  // Lane 6 (D-30, D-32): a fuel expense's company-vehicle trip, the litres, and why a fill Fleet already logs is claimed.
  fleetTripId: z.string().optional(),
  fuelQuantity: z.coerce.number().min(0, 'Litres cannot be negative').optional(),
  fuelDuplicateReason: z.string().max(1000).optional(),
}).superRefine((v, ctx) => {
  if (v.expenseCategory === 'Fuel' && v.fleetTripId && !(Number(v.fuelQuantity) > 0)) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['fuelQuantity'], message: 'Enter the litres bought' });
  }
});

function InfoRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="py-1.5">
      <p className="text-xs text-muted-foreground">{label}</p>
      <div className="text-sm">{value ?? '—'}</div>
    </div>
  );
}

/**
 * One of the traveller's own expense claims (travel final closure, lane 7, slice 7d — D-38, T-54).
 *
 * The traveller adds the expenses — each with its receipt, one of the trip's files (uploaded here or under the trip's
 * Files tab) — and sends the claim to the travel desk. The desk reviews each expense and pays the claim, as before: there
 * is no review or payment here, and the server refuses the claimant both (D-2). The rules are the desk's, from lane 3:
 * a claim's expenses are fixed once it is submitted and the traveller's again when it is returned; under an approved
 * policy every expense above its receipt threshold needs a receipt (a per diem does not — D-43) and a first submission
 * comes within the claim window after the trip. The traveller removes an expense while the claim is a draft or returned,
 * and the whole claim while it is a draft (D-44). Every amount is valued in the organisation's currency at Finance's rate
 * for the expense's date, by the server.
 *
 * ⚠ A 404 means the claim is not yours — the surface answers the same for one that does not exist.
 */
export default function MyTravelClaimPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [lineDialog, setLineDialog] = useState<{ line: StaffTravelExpenseClaimLine | null } | null>(null);
  const [uploading, setUploading] = useState(false);
  const receiptInputRef = useRef<HTMLInputElement>(null);

  const { data: claim, isLoading, isError, error } = useQuery({
    queryKey: ['my-travel-claim', id],
    queryFn: () => travelService.getMyClaim(id),
    retry: false,
  });
  const tripId = claim?.staffTravelRequestId;
  // The trip's files are the receipts a line can name.
  const { data: trip, isSuccess: tripLoaded, refetch: refetchTrip } = useQuery({
    queryKey: ['my-travel-request', tripId],
    queryFn: () => travelService.getMineById(tripId as string),
    enabled: !!tripId,
  });
  const { data: fleetFuel, isSuccess: fleetFuelLoaded } = useQuery({
    queryKey: ['my-travel-claim-fleet-fuel', id],
    queryFn: () => travelService.getMyClaimFleetFuel(id),
    enabled: !!claim,
  });
  const fleetTrip = (fleetTripId?: string | null) => fleetFuel?.trips.find((t) => t.fleetTripId === fleetTripId);
  const vehicleLabel = (fleetTripId?: string | null) => {
    const t = fleetTrip(fleetTripId);
    return t ? (t.vehiclePlate ? `${t.vehicleName} (${t.vehiclePlate})` : t.vehicleName) : 'Company vehicle';
  };

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['my-travel-claim', id] });
    if (tripId) await queryClient.invalidateQueries({ queryKey: ['my-travel-request', tripId] });
  };

  const lineForm = useForm<z.input<typeof lineSchema>>({
    resolver: zodResolver(lineSchema),
    defaultValues: {
      expenseCategory: 'Meals', expenseDate: '', amountOriginal: 0, currencyOriginal: '', receiptAttachmentId: '',
      isPerDiem: false, fleetTripId: '', fuelQuantity: undefined, fuelDuplicateReason: '',
    },
  });

  const attachments = trip?.attachments ?? [];
  const attachmentName = new Map(attachments.map((a) => [a.id, a.fileName]));
  const receiptOptions = attachments.map((a) => ({ value: a.id, label: `${a.fileName} (${humanize(a.attachmentTypeName)})` }));

  // ⚠ It opens only once the trip's files and the fleet fuel are loaded: a SelectField blanks a value that arrives before
  // its options, which would unlink an edited expense's receipt or fleet trip on save.
  const openLineDialog = (line: StaffTravelExpenseClaimLine | null) => {
    lineForm.reset(line
      ? {
        expenseCategory: line.expenseCategory, expenseDate: String(line.expenseDate).slice(0, 10),
        description: line.description ?? '', merchantName: line.merchantName ?? '',
        amountOriginal: line.amountOriginal, currencyOriginal: line.currencyOriginal,
        receiptAttachmentId: line.receiptAttachmentId ?? '', isPerDiem: line.isPerDiem,
        fleetTripId: line.fleetTripId ?? '', fuelQuantity: line.fuelQuantity ?? undefined, fuelDuplicateReason: '',
      }
      : {
        expenseCategory: 'Meals', expenseDate: '', amountOriginal: 0, currencyOriginal: claim?.currencyCode ?? '',
        receiptAttachmentId: '', isPerDiem: false, fleetTripId: '', fuelQuantity: undefined, fuelDuplicateReason: '',
      });
    setLineDialog({ line });
  };

  const watchedCategory = lineForm.watch('expenseCategory');
  const watchedTrip = lineForm.watch('fleetTripId');
  const watchedDate = lineForm.watch('expenseDate');
  const editingLine = lineDialog?.line ?? null;
  const fuelTripOptions = (fleetFuel?.trips ?? [])
    .filter((t) => t.live || t.fleetTripId === editingLine?.fleetTripId)
    .map((t) => ({
      value: t.fleetTripId,
      label: `${t.vehiclePlate ? `${t.vehicleName} (${t.vehiclePlate})` : t.vehicleName} · ${fmtDate(t.plannedStartAt)} to ${fmtDate(t.plannedEndAt)}`
        + (t.live ? '' : ` — ${t.status.toLowerCase()} in Fleet`),
    }));
  const showFuel = watchedCategory === 'Fuel' && fuelTripOptions.length > 0;
  const fillMoved = !editingLine || (editingLine.fleetTripId ?? null) !== (watchedTrip || null)
    || String(editingLine.expenseDate).slice(0, 10) !== watchedDate || editingLine.expenseCategory !== watchedCategory;
  const sameDayFuel = showFuel && watchedTrip && watchedDate && fillMoved
    ? (fleetTrip(watchedTrip)?.fuel ?? []).filter((f) => String(f.fuelledAt).slice(0, 10) === watchedDate)
    : [];

  const saveLine = useMutation({
    mutationFn: (values: z.input<typeof lineSchema>) => {
      const v = lineSchema.parse(values);
      const editing = lineDialog?.line;
      const fuel = v.expenseCategory === 'Fuel' && !!v.fleetTripId;
      const payload = {
        expenseCategory: v.expenseCategory as TravelExpenseCategory,
        expenseDate: v.expenseDate,
        description: v.description || null,
        merchantName: v.merchantName || null,
        amountOriginal: v.amountOriginal,
        currencyOriginal: v.currencyOriginal,
        receiptAttachmentId: v.receiptAttachmentId || null,
        isPerDiem: v.isPerDiem,
        perDiemRateId: editing?.perDiemRateId ?? null,
        fleetTripId: fuel ? v.fleetTripId : null,
        fuelQuantity: fuel ? Number(v.fuelQuantity) : null,
        fuelDuplicateReason: fuel && v.fuelDuplicateReason?.trim() ? v.fuelDuplicateReason.trim() : null,
      };
      return editing
        ? travelService.updateMyClaimLine({ ...payload, id: editing.id })
        : travelService.addMyClaimLine(id, payload);
    },
    onSuccess: async () => {
      toast({ title: lineDialog?.line ? 'Expense changed' : 'Expense added' });
      setLineDialog(null);
      await refresh();
    },
    // A currency Finance has no rate for on that date is refused here, with the reason.
    onError: (e: Error) => toast({ variant: 'destructive', title: 'The expense was refused', description: e.message }),
  });

  const removeLine = useMutation({
    mutationFn: (lineId: string) => travelService.deleteMyClaimLine(lineId),
    onSuccess: async () => { toast({ title: 'Expense removed' }); await refresh(); },
    onError: (e: Error) => toast({ variant: 'destructive', title: 'Could not remove it', description: e.message }),
  });

  const submit = useMutation({
    mutationFn: () => travelService.submitMyClaim(id),
    onSuccess: async () => { toast({ title: 'Sent to the travel desk' }); await refresh(); },
    // The receipt threshold and the claim window are checked here, and say which expense or date.
    onError: (e: Error) => toast({ variant: 'destructive', title: 'Not submitted', description: e.message }),
  });

  const remove = useMutation({
    mutationFn: () => travelService.deleteMyClaim(id),
    onSuccess: async () => {
      toast({ title: 'Claim deleted' });
      if (tripId) await queryClient.invalidateQueries({ queryKey: ['my-travel-request', tripId] });
      router.push(tripId ? `/me/travel/${tripId}` : '/me/travel');
    },
    onError: (e: Error) => toast({ variant: 'destructive', title: 'Could not delete the claim', description: e.message }),
  });

  // A receipt uploaded from the expense dialog: one of the trip's files, then chosen for the expense.
  const uploadReceipt = async (file: File | undefined) => {
    if (!file || !tripId) return;
    setUploading(true);
    try {
      const uploaded = await travelService.uploadMyAttachment(tripId, file, 'Receipt', `Receipt for claim ${claim?.claimNumber ?? ''}`.trim());
      await refetchTrip();
      lineForm.setValue('receiptAttachmentId', uploaded.id, { shouldDirty: true });
      toast({ title: 'Receipt attached to the trip' });
    } catch (e) {
      toast({ variant: 'destructive', title: 'Upload refused', description: (e as Error).message });
    } finally {
      setUploading(false);
      if (receiptInputRef.current) receiptInputRef.current.value = '';
    }
  };

  if (isLoading) {
    return <div className="flex justify-center p-10"><Loader2 className="h-6 w-6 animate-spin text-muted-foreground" /></div>;
  }
  if (!claim && isError && (error as { status?: number } | null)?.status !== 404) {
    return <div className="p-6"><TravelQueryError error={error} what="this expense claim" /></div>;
  }
  if (!claim) {
    return <div className="p-6"><EmptyState title="Not available" description="This expense claim is not one of yours." /></div>;
  }

  const editable = claim.status === 'Draft' || claim.status === 'Returned';
  const canOpenDialog = tripLoaded && fleetFuelLoaded;
  const lines = claim.lines.slice().sort((a, b) => String(a.expenseDate).localeCompare(String(b.expenseDate)));

  return (
    <div className="space-y-6">
      <PageHeader
        title={claim.claimNumber}
        description={`${humanize(claim.claimTypeName)} claim on ${claim.requestNumber ?? 'your trip'}`}
        backHref={`/me/travel/${claim.staffTravelRequestId}`}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={humanize(claim.statusName)} />
            {editable && (
              <Button onClick={() => submit.mutate()} disabled={submit.isPending || lines.length === 0}
                title={lines.length === 0 ? 'Add an expense first' : undefined}>
                {submit.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Send className="mr-2 h-4 w-4" />}
                Send to the travel desk
              </Button>
            )}
            {claim.status === 'Draft' && (
              <Button variant="outline" disabled={remove.isPending} onClick={() => remove.mutate()}>
                <Trash2 className="mr-2 h-4 w-4" /> Delete the claim
              </Button>
            )}
          </div>
        }
      />

      {claim.status === 'Returned' && claim.reviewNotes && (
        <p role="note" className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm dark:border-amber-700 dark:bg-amber-950">
          The travel desk returned this claim to you: <span className="font-medium">{claim.reviewNotes}</span> Change what
          they asked for, then send it again.
        </p>
      )}

      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-base">The claim</CardTitle></CardHeader>
        <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
          <InfoRow label="Claimed" value={fmtMoney(claim.totalClaimed, claim.currencyCode)} />
          <InfoRow label="Approved" value={claim.status === 'Draft' || claim.status === 'Submitted' ? '—' : fmtMoney(claim.totalApproved, claim.currencyCode)} />
          <InfoRow label="Advance deducted" value={claim.travelAdvanceNumber
            ? `${fmtMoney(claim.advanceDeducted, claim.currencyCode)} (${claim.travelAdvanceNumber})` : 'No advance'} />
          <InfoRow label="Payable to you" value={fmtMoney(claim.netPayable, claim.currencyCode)} />
          <InfoRow label="Submitted" value={fmtDateTime(claim.submittedAt)} />
          <InfoRow label="Reviewed" value={claim.financeReviewedAt
            ? [fmtDateTime(claim.financeReviewedAt), claim.financeReviewedByName].filter(Boolean).join(' · ') : '—'} />
          {claim.paidAt && (
            <InfoRow label="Paid" value={[fmtDateTime(claim.paidAt), claim.paymentMethodName && humanize(claim.paymentMethodName),
              claim.paymentReference].filter(Boolean).join(' · ')} />
          )}
          {claim.reviewNotes && claim.status !== 'Returned' && <InfoRow label="The desk's notes" value={claim.reviewNotes} />}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-start justify-between gap-4">
          <div className="space-y-1.5">
            <CardTitle className="text-base">Expenses</CardTitle>
            <p className="text-sm text-muted-foreground">
              {editable
                ? 'Each in the currency you spent it in — the server values it in the organisation\'s currency at Finance\'s rate for its date.'
                : 'Fixed once the claim is sent. The travel desk returns it to you if anything should change.'}
            </p>
          </div>
          {editable && (
            <Button variant="outline" size="sm" disabled={!canOpenDialog} onClick={() => openLineDialog(null)}>
              <Plus className="mr-2 h-4 w-4" /> Add an expense
            </Button>
          )}
        </CardHeader>
        <CardContent className="p-0">
          {lines.length === 0 ? (
            <EmptyState title="No expenses yet" description="Add what you spent, with its receipt." />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Date</TableHead>
                  <TableHead>Expense</TableHead>
                  <TableHead className="text-right">Spent</TableHead>
                  <TableHead className="text-right">In {claim.currencyCode}</TableHead>
                  <TableHead>Receipt</TableHead>
                  <TableHead>Decision</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {lines.map((l) => (
                  <TableRow key={l.id}>
                    <TableCell className="whitespace-nowrap">{fmtDate(String(l.expenseDate))}</TableCell>
                    <TableCell>
                      {humanize(l.expenseCategoryName)}
                      {l.isPerDiem && <span className="text-muted-foreground"> · per diem</span>}
                      {l.fleetTripId && (
                        <span className="text-muted-foreground">
                          {' '}· {vehicleLabel(l.fleetTripId)}{l.fuelQuantity != null ? ` · ${l.fuelQuantity} L` : ''}
                        </span>
                      )}
                      {(l.merchantName || l.description) && (
                        <p className="text-xs text-muted-foreground">{[l.merchantName, l.description].filter(Boolean).join(' — ')}</p>
                      )}
                    </TableCell>
                    <TableCell className="whitespace-nowrap text-right">{fmtMoney(l.amountOriginal, l.currencyOriginal)}</TableCell>
                    <TableCell className="whitespace-nowrap text-right">{fmtMoney(l.amountBaseCurrency, claim.currencyCode)}</TableCell>
                    <TableCell className="text-sm">
                      {l.receiptAttachmentId
                        ? attachmentName.get(l.receiptAttachmentId) ?? 'Linked'
                        : <span className="text-muted-foreground">{l.isPerDiem ? 'Not needed' : 'None'}</span>}
                    </TableCell>
                    <TableCell className="text-sm">
                      {l.status === 'Pending' ? (
                        <span className="text-muted-foreground">Not yet reviewed</span>
                      ) : (
                        <>
                          <StatusBadge status={humanize(l.statusName)} />
                          {l.amountApproved != null && l.amountApproved < l.amountBaseCurrency && (
                            <p className="mt-1 text-xs">
                              {fmtMoney(l.amountApproved, claim.currencyCode)} approved
                              {l.rejectionReason ? ` — ${l.rejectionReason}` : ''}
                            </p>
                          )}
                        </>
                      )}
                    </TableCell>
                    <TableCell>
                      {editable && (
                        <div className="flex items-center gap-1">
                          <Button variant="ghost" size="icon" aria-label="Change this expense" disabled={!canOpenDialog}
                            onClick={() => openLineDialog(l)}>
                            <Pencil className="h-4 w-4" />
                          </Button>
                          <Button variant="ghost" size="icon" aria-label="Remove this expense" disabled={removeLine.isPending}
                            onClick={() => removeLine.mutate(l.id)}>
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={!!lineDialog} onOpenChange={(v) => !v && setLineDialog(null)}>
        <DialogContent className="max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{lineDialog?.line ? 'Change this expense' : 'Add an expense'}</DialogTitle>
            <DialogDescription>
              What you spent and in what currency. The rate and the converted amount come from Finance, for the date of
              the expense.
              {lineDialog?.line?.status && lineDialog.line.status !== 'Pending'
                ? ' The travel desk reviewed this expense; changing it sends it back to be reviewed again.'
                : ''}
            </DialogDescription>
          </DialogHeader>
          <form id="my-line-form" className="space-y-4" onSubmit={lineForm.handleSubmit((v) => saveLine.mutate(v))}>
            <FieldRow>
              <SelectField form={lineForm} name="expenseCategory" label="Category" required options={options(EXPENSE_CATEGORIES)} />
              <DateField form={lineForm} name="expenseDate" label="Date" required />
            </FieldRow>
            {showFuel && (
              <div className="space-y-3 rounded-md border p-3">
                <p className="text-xs text-muted-foreground">
                  {fleetFuel?.fuelNamesTrip
                    ? 'This trip travels by company vehicle and hires no car, so its fuel was the company vehicle\'s — name the vehicle\'s trip and the litres.'
                    : 'Fuel for the company vehicle names its trip and the litres; fuel for the hired car names neither.'}
                </p>
                <FieldRow>
                  <SelectField form={lineForm} name="fleetTripId" label="Company vehicle trip" required={!!fleetFuel?.fuelNamesTrip}
                    options={fuelTripOptions} allowEmpty={!fleetFuel?.fuelNamesTrip} emptyLabel="Not the company vehicle" />
                  {watchedTrip && <NumberField form={lineForm} name="fuelQuantity" label="Litres" required />}
                </FieldRow>
                {sameDayFuel.length > 0 && (
                  <div className="space-y-2 rounded-md border border-amber-300 bg-amber-50 p-3 text-xs dark:border-amber-700 dark:bg-amber-950">
                    <p>
                      Fleet already logs fuel for {vehicleLabel(watchedTrip)} on that day:{' '}
                      {sameDayFuel.map((f) => `${f.quantity} ${f.unit}${f.totalCost != null ? ` for ${fmtMoney(f.totalCost, claim.currencyCode)}` : ''}`).join('; ')}.
                      If this is another fill, say why it is claimed too — the travel desk sees the reason.
                    </p>
                    <TextField form={lineForm} name="fuelDuplicateReason" label="Why it is claimed too" required />
                  </div>
                )}
              </div>
            )}
            <TextField form={lineForm} name="description" label="Description" />
            <TextField form={lineForm} name="merchantName" label="Merchant" />
            <FieldRow>
              <NumberField form={lineForm} name="amountOriginal" label="Amount spent" required />
              <CurrencyField form={lineForm} name="currencyOriginal" label="Currency spent in" required />
            </FieldRow>
            <SelectField form={lineForm} name="receiptAttachmentId" label="Receipt" options={receiptOptions} allowEmpty
              emptyLabel={receiptOptions.length ? 'No receipt' : 'No file on the trip yet'} />
            <div className="flex flex-wrap items-center gap-2">
              <Input ref={receiptInputRef} type="file" className="hidden" id="my-claim-receipt"
                onChange={(e) => uploadReceipt(e.target.files?.[0])} />
              <Button type="button" variant="outline" size="sm" disabled={uploading}
                onClick={() => receiptInputRef.current?.click()}>
                {uploading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Upload className="mr-2 h-4 w-4" />}
                Upload a receipt
              </Button>
              <span className="text-xs text-muted-foreground">
                Scanned, then kept with the trip&apos;s files and chosen for this expense.
              </span>
            </div>
            <p className="text-xs text-muted-foreground">
              Under the travel policy, an expense above its receipt threshold needs one before the claim can be sent — a
              per diem does not.
            </p>
            <SwitchField form={lineForm} name="isPerDiem" label="This is a per-diem allowance" />
          </form>
          <DialogFooter>
            <Button variant="outline" onClick={() => setLineDialog(null)}>Cancel</Button>
            <Button type="submit" form="my-line-form" disabled={saveLine.isPending}>
              {saveLine.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {lineDialog?.line ? 'Save' : 'Add'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
