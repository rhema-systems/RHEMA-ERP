'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  Archive,
  Boxes,
  CheckCircle2,
  Link2,
  Link2Off,
  Loader2,
  PenLine,
  Send,
  ShieldCheck,
  Trash2,
  Truck,
  UserPlus,
  Wrench,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { AssetAttributesPanel } from '@/components/hr/assets/AssetAttributesPanel';
import { AssetFilesPanel } from '@/components/hr/assets/AssetFilesPanel';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { useToast } from '@/hooks/use-toast';
import {
  ASSET_CONDITIONS,
  ASSET_MAINTENANCE_TYPES,
  ASSIGNMENT_PURPOSES,
  ASSIGNMENT_TYPES,
  DISPOSAL_METHODS,
} from '@/types/hr/assets';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtNum = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { maximumFractionDigits: 2 });

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm">{children}</dd>
    </div>
  );
}

const today = () => new Date().toISOString().slice(0, 10);

/**
 * One asset, and everything the register knows about it.
 *
 * The four panels come from `{id}/details` in a single call — the record plus its custom
 * attributes, custody history, service log and attachments — rather than five reads the page
 * would then have to keep consistent with each other.
 *
 * ⚠ The Maintenance-module link is a **handle, not an abdication** (decision D10). A linked asset
 * is still scheduled and chased by HR; the link only lets a job be pushed to the workshop. The
 * screen says so, because "linked to Maintenance" reads like "no longer our problem".
 */
export default function AssetDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [assigning, setAssigning] = useState(false);
  const [assignForm, setAssignForm] = useState({
    employeeId: '',
    assignmentDate: today(),
    expectedReturnDate: '',
    type: 'Permanent',
    purpose: 'RegularWork',
    conditionAtAssignment: 'Good',
    responsibleForLoss: true,
    responsibleForDamage: true,
    termsAndConditions: '',
    assignmentNotes: '',
  });

  const [scheduling, setScheduling] = useState(false);
  const [serviceForm, setServiceForm] = useState({
    maintenanceDate: today(),
    type: 'Preventive',
    description: '',
    cost: '',
    isInternalMaintenance: true,
    externalServiceProvider: '',
  });

  const [linking, setLinking] = useState(false);
  const [linkSearch, setLinkSearch] = useState('');

  const [disposing, setDisposing] = useState(false);
  const [disposeForm, setDisposeForm] = useState({
    disposalDate: today(),
    disposalMethod: '1',
    disposalNotes: '',
  });

  const [sendingToWorkshop, setSendingToWorkshop] = useState(false);
  const [workshopForm, setWorkshopForm] = useState({
    description: '',
    type: 'Corrective',
    admissionType: 'Scheduled',
    observedProblems: '',
    estimatedCompletionDate: '',
    cost: '',
  });

  const [completing, setCompleting] = useState<{ id: string; number: string } | null>(null);
  const [completionNotes, setCompletionNotes] = useState('');

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['hr', 'assets'] });

  const { data: asset, isLoading } = useQuery({
    queryKey: ['hr', 'assets', 'detail', id],
    queryFn: () => assetRegisterService.getAssetDetail(id),
    enabled: Boolean(id),
  });

  const { data: linkable = [] } = useQuery({
    queryKey: ['hr', 'assets', 'linkable-maintenance', linkSearch],
    queryFn: () => assetRegisterService.getLinkableMaintenanceAssets(linkSearch || undefined),
    enabled: linking,
  });

  const assign = useMutation({
    mutationFn: () =>
      assetRegisterService.createAssignment({
        assetId: id,
        employeeId: assignForm.employeeId,
        assignmentDate: assignForm.assignmentDate,
        expectedReturnDate: assignForm.expectedReturnDate || null,
        type: ASSIGNMENT_TYPES.find((t) => t.label === assignForm.type)?.value ?? 1,
        purpose: ASSIGNMENT_PURPOSES.find((p) => p.label === assignForm.purpose)?.value ?? 1,
        conditionAtAssignment:
          ASSET_CONDITIONS.find((c) => c.label === assignForm.conditionAtAssignment)?.value ?? 2,
        responsibleForLoss: assignForm.responsibleForLoss,
        responsibleForDamage: assignForm.responsibleForDamage,
        termsAndConditions: assignForm.termsAndConditions || null,
        assignmentNotes: assignForm.assignmentNotes || null,
      }),
    onSuccess: (created) => {
      invalidate();
      setAssigning(false);
      toast({ title: 'Asset issued', description: created.assignmentNumber });
      router.push(`/hr/assets/assignments/${created.id}`);
    },
    onError: (e: Error) =>
      toast({ title: 'Could not issue the asset', description: e.message, variant: 'destructive' }),
  });

  const schedule = useMutation({
    mutationFn: () =>
      assetRegisterService.createMaintenance({
        assetId: id,
        // ⚠ A full DateTime here — the asset's own date fields are DateOnly, this one is not.
        maintenanceDate: `${serviceForm.maintenanceDate}T00:00:00`,
        type: ASSET_MAINTENANCE_TYPES.find((t) => t.label === serviceForm.type)?.value ?? 1,
        description: serviceForm.description,
        isInternalMaintenance: serviceForm.isInternalMaintenance,
        externalServiceProvider: serviceForm.isInternalMaintenance
          ? null
          : serviceForm.externalServiceProvider || null,
        cost: serviceForm.cost === '' ? null : Number(serviceForm.cost),
        status: 1, // Scheduled
      }),
    onSuccess: () => {
      invalidate();
      setScheduling(false);
      toast({ title: 'Service scheduled' });
    },
    onError: (e: Error) =>
      toast({ title: 'Could not schedule the service', description: e.message, variant: 'destructive' }),
  });

  const link = useMutation({
    mutationFn: (maintenanceAssetId: string) =>
      assetRegisterService.linkMaintenanceAsset(id, maintenanceAssetId),
    onSuccess: () => { invalidate(); setLinking(false); toast({ title: 'Linked to the Maintenance register' }); },
    onError: (e: Error) =>
      toast({ title: 'Could not link', description: e.message, variant: 'destructive' }),
  });

  const unlink = useMutation({
    mutationFn: () => assetRegisterService.unlinkMaintenanceAsset(id),
    onSuccess: () => { invalidate(); toast({ title: 'Link removed' }); },
    onError: (e: Error) =>
      toast({ title: 'Could not unlink', description: e.message, variant: 'destructive' }),
  });

  const dispose = useMutation({
    mutationFn: () =>
      assetRegisterService.disposeAsset(id, {
        // ⚠ `assetId` in the BODY as well as the route — `DisposeAssetDto` marks it required, so
        // leaving it out is a ModelState 400 the route cannot rescue.
        assetId: id,
        disposalDate: disposeForm.disposalDate,
        disposalMethod: Number(disposeForm.disposalMethod),
        disposalNotes: disposeForm.disposalNotes || null,
      }),
    onSuccess: () => { invalidate(); setDisposing(false); toast({ title: 'Asset disposed' }); },
    onError: (e: Error) =>
      toast({ title: 'Could not dispose of it', description: e.message, variant: 'destructive' }),
  });

  const sendToWorkshop = useMutation({
    mutationFn: () =>
      assetRegisterService.sendForMaintenance(id, {
        // ⚠ `description`, NOT `reason`. A payload written from the route's name answers
        // `400 The Description field is required` — the slice-12 audit walked into exactly that.
        description: workshopForm.description,
        type: ASSET_MAINTENANCE_TYPES.find((t) => t.label === workshopForm.type)?.value ?? 2,
        admissionType: workshopForm.admissionType,
        observedProblems: workshopForm.observedProblems || null,
        estimatedCompletionDate: workshopForm.estimatedCompletionDate
          ? `${workshopForm.estimatedCompletionDate}T00:00:00`
          : null,
        cost: workshopForm.cost === '' ? null : Number(workshopForm.cost),
      }),
    onSuccess: (job) => {
      invalidate();
      setSendingToWorkshop(false);
      toast({ title: 'Sent to the workshop', description: job.maintenanceAdmissionNumber ?? undefined });
    },
    onError: (e: Error) =>
      toast({ title: 'Could not send it', description: e.message, variant: 'destructive' }),
  });

  const complete = useMutation({
    mutationFn: (jobId: string) =>
      assetRegisterService.completeMaintenance(jobId, completionNotes || 'Completed'),
    onSuccess: () => {
      invalidate();
      setCompleting(null);
      setCompletionNotes('');
      toast({ title: 'Service completed', description: 'The asset has been re-dated from its interval.' });
    },
    onError: (e: Error) =>
      toast({ title: 'Could not complete it', description: e.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: () => assetRegisterService.deleteAsset(id),
    onSuccess: () => { invalidate(); toast({ title: 'Asset removed' }); router.push('/hr/assets/register'); },
    onError: (e: Error) =>
      toast({ title: 'Could not remove the asset', description: e.message, variant: 'destructive' }),
  });

  if (isLoading || !asset) {
    return (
      <div className="flex justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={asset.assetName}
        description={`${asset.assetNumber} · ${asset.assetTypeName}`}
        backHref="/hr/assets/register"
        actions={
          <div className="flex flex-wrap gap-2">
            {asset.isAssignable && !asset.isCurrentlyAssigned && asset.status === 'Available' && (
              <Button onClick={() => setAssigning(true)}>
                <UserPlus className="mr-2 h-4 w-4" /> Issue to an employee
              </Button>
            )}
            <Button variant="outline" onClick={() => setScheduling(true)}>
              <Wrench className="mr-2 h-4 w-4" /> Schedule a service
            </Button>
            {/* The push into the Maintenance module. Only offered once the asset is linked —
                without a link there is nothing over there to raise the job against, and the API
                says so in words rather than guessing. */}
            {asset.isKnownToMaintenance && asset.status !== 'Disposed' && (
              <Button variant="outline" onClick={() => setSendingToWorkshop(true)}>
                <Truck className="mr-2 h-4 w-4" /> Send to the workshop
              </Button>
            )}
            {asset.status !== 'Disposed' && (
              <Button variant="outline" onClick={() => setDisposing(true)}>
                <Archive className="mr-2 h-4 w-4" /> Dispose
              </Button>
            )}
            <Button variant="outline" asChild>
              <Link href={`/hr/assets/register/${id}/edit`}>
                <PenLine className="mr-2 h-4 w-4" /> Edit
              </Link>
            </Button>
            <Button variant="outline" onClick={() => remove.mutate()} disabled={remove.isPending}>
              <Trash2 className="mr-2 h-4 w-4" /> Remove
            </Button>
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        <StatusBadge status={asset.statusName} />
        <span className="text-sm text-muted-foreground">Condition: {asset.conditionName}</span>
        {asset.isCurrentlyAssigned && (
          <span className="text-sm">
            Held by <span className="font-medium">{asset.currentAssignedToName ?? 'someone'}</span>
          </span>
        )}
        {asset.isInsuranceExpired && (
          <span className="flex items-center gap-1 text-sm text-red-600 dark:text-red-500">
            <AlertTriangle className="h-4 w-4" /> Cover has lapsed
          </span>
        )}
      </div>

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="custody">Custody ({asset.recentAssignments.length})</TabsTrigger>
          <TabsTrigger value="service">Service log ({asset.recentMaintenance.length})</TabsTrigger>
          <TabsTrigger value="attributes">Attributes ({asset.attributeValues.length})</TabsTrigger>
          <TabsTrigger value="attachments">Files ({asset.attachments.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="space-y-4 pt-4">
          <Card>
            <CardHeader><CardTitle className="text-base">Identification</CardTitle></CardHeader>
            <CardContent>
              <dl className="grid gap-4 sm:grid-cols-3">
                <Field label="Manufacturer">{asset.manufacturer ?? '—'}</Field>
                <Field label="Model">{asset.modelNumber ?? '—'}</Field>
                <Field label="Serial number">{asset.serialNumber ?? '—'}</Field>
                <Field label="Organisation unit">{asset.unitName ?? 'Not set'}</Field>
                <Field label="Location">{asset.locationName ?? 'Not set'}</Field>
                <Field label="Location details">{asset.locationDetails ?? '—'}</Field>
                {asset.description && (
                  <div className="sm:col-span-3">
                    <Field label="Description">{asset.description}</Field>
                  </div>
                )}
                {asset.additionalRemarks && (
                  <div className="sm:col-span-3">
                    <Field label="Additional remarks">{asset.additionalRemarks}</Field>
                  </div>
                )}
              </dl>
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle className="text-base">Cost and warranty</CardTitle></CardHeader>
            <CardContent>
              <dl className="grid gap-4 sm:grid-cols-3">
                <Field label="Purchase date">{fmtDate(asset.purchaseDate)}</Field>
                {/* ⚠ Acquisition cost, not a current value. See defect D-jj. */}
                <Field label="Purchase cost">{fmtNum(asset.purchaseCost)}</Field>
                <Field label="Supplier">{asset.supplier ?? '—'}</Field>
                <Field label="Warranty">
                  {asset.hasWarranty
                    ? `${fmtDate(asset.warrantyStartDate)} – ${fmtDate(asset.warrantyEndDate)}${asset.isWarrantyActive ? ' (active)' : ' (expired)'}`
                    : 'None recorded'}
                </Field>
                <Field label="Source">
                  {asset.source === 'FixedAssetsModule' ? 'Finance fixed assets' : 'Registered in HR'}
                </Field>
                {asset.fixedAsset && (
                  <Field label="Fixed asset">
                    {asset.fixedAsset.assetCode} · net book value {fmtNum(asset.fixedAsset.netBookValue)}
                  </Field>
                )}
              </dl>
              {asset.isFinanceOwned && (
                <p className="mt-4 rounded-md bg-muted p-3 text-sm text-muted-foreground">
                  Depreciation, valuation and disposal for this asset belong to Finance. HR records
                  who is holding it.
                </p>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <Wrench className="h-4 w-4" /> Servicing
              </CardTitle>
            </CardHeader>
            <CardContent>
              <dl className="grid gap-4 sm:grid-cols-3">
                <Field label="On a schedule">
                  {asset.requiresRegularMaintenance
                    ? `Every ${asset.maintenanceIntervalDays ?? '—'} days`
                    : 'No'}
                </Field>
                <Field label="Last serviced">{fmtDate(asset.lastMaintenanceDate)}</Field>
                <Field label="Next due">
                  {asset.nextMaintenanceDate
                    ? fmtDate(asset.nextMaintenanceDate)
                    : asset.requiresRegularMaintenance
                      ? <span className="text-amber-600 dark:text-amber-500">Never scheduled</span>
                      : '—'}
                </Field>
              </dl>

              <div className="mt-4 flex flex-wrap items-center gap-3 border-t pt-4">
                <div className="flex-1">
                  <p className="text-sm font-medium">Maintenance module</p>
                  <p className="text-sm text-muted-foreground">
                    {asset.isKnownToMaintenance
                      ? 'Linked, so work can be sent to the workshop. HR still schedules and chases it.'
                      : 'Not linked. Link it to send work to the workshop; HR keeps the schedule either way.'}
                  </p>
                </div>
                {asset.isKnownToMaintenance ? (
                  <Button variant="outline" size="sm" onClick={() => unlink.mutate()}
                    disabled={unlink.isPending}>
                    <Link2Off className="mr-2 h-4 w-4" /> Unlink
                  </Button>
                ) : (
                  <Button variant="outline" size="sm" onClick={() => setLinking(true)}>
                    <Link2 className="mr-2 h-4 w-4" /> Link
                  </Button>
                )}
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <ShieldCheck className="h-4 w-4" /> Insurance and rental
              </CardTitle>
            </CardHeader>
            <CardContent>
              <dl className="grid gap-4 sm:grid-cols-3">
                <Field label="Insured">{asset.isInsured ? 'Yes' : 'No'}</Field>
                <Field label="Policy">{asset.insurancePolicyNumber ?? '—'}</Field>
                <Field label="Insured for">{fmtNum(asset.insuredValue)}</Field>
                <Field label="Cover lapses">
                  {asset.isInsured && !asset.insuranceExpiryDate
                    ? <span className="text-amber-600 dark:text-amber-500">Never dated</span>
                    : fmtDate(asset.insuranceExpiryDate)}
                </Field>
                <Field label="Rentable">{asset.isRentable ? 'Yes' : 'No'}</Field>
                <Field label="Standard rate">
                  {asset.isRentable
                    ? `${asset.rentalCurrencyCode ?? ''} ${fmtNum(asset.standardRentalAmount)}`.trim()
                    : '—'}
                </Field>
              </dl>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="custody" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {asset.recentAssignments.length === 0 ? (
                <EmptyState icon={Boxes} title="Never issued"
                  description="Nobody has held this asset." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Assignment</TableHead>
                      <TableHead>Held by</TableHead>
                      <TableHead>Issued</TableHead>
                      <TableHead>Due back</TableHead>
                      <TableHead>Returned</TableHead>
                      <TableHead>Status</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {asset.recentAssignments.map((a) => (
                      <TableRow key={a.id}>
                        <TableCell>
                          <Link href={`/hr/assets/assignments/${a.id}`} className="hover:underline">
                            {a.assignmentNumber}
                          </Link>
                        </TableCell>
                        <TableCell>{a.employeeName}</TableCell>
                        <TableCell>{fmtDate(a.assignmentDate)}</TableCell>
                        <TableCell>{fmtDate(a.expectedReturnDate)}</TableCell>
                        <TableCell>{fmtDate(a.returnDate)}</TableCell>
                        <TableCell><StatusBadge status={a.statusName} /></TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="service" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {asset.recentMaintenance.length === 0 ? (
                <EmptyState icon={Wrench} title="No service history"
                  description="Nothing has been logged against this asset." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Job</TableHead>
                      <TableHead>Date</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="text-right">Cost</TableHead>
                      <TableHead>Workshop</TableHead>
                      <TableHead />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {asset.recentMaintenance.map((m) => (
                      <TableRow key={m.id}>
                        <TableCell>{m.maintenanceNumber}</TableCell>
                        <TableCell>{fmtDate(m.maintenanceDate)}</TableCell>
                        <TableCell>{m.typeName}</TableCell>
                        <TableCell><StatusBadge status={m.statusName} /></TableCell>
                        <TableCell className="text-right">{fmtNum(m.cost)}</TableCell>
                        <TableCell>
                          {m.isAtWorkshop
                            ? m.maintenanceAdmissionNumber ?? 'At the workshop'
                            : <span className="text-muted-foreground">—</span>}
                        </TableCell>
                        <TableCell className="text-right">
                          {/* ⚠ Completing is what re-dates the asset from its own interval and
                              clears it off the overdue watchlist — and discharges it from the
                              workshop where it went there. Without it those lists only grow. */}
                          {(m.status === 'Scheduled' || m.status === 'InProgress') && (
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() => setCompleting({ id: m.id, number: m.maintenanceNumber })}
                            >
                              <CheckCircle2 className="mr-1 h-4 w-4" /> Complete
                            </Button>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="attributes" className="pt-4">
          {/* Reads the TYPE's declared attributes and joins the asset's values onto them, so an
              attribute added to the type after this asset was registered still shows — empty and
              fillable. Listing only the stored values hid exactly the ones needing attention. */}
          <AssetAttributesPanel
            assetId={id}
            assetTypeId={asset.assetTypeId}
            hasExtraAttributes
          />
        </TabsContent>

        <TabsContent value="attachments" className="pt-4">
          <AssetFilesPanel assetId={id} />
        </TabsContent>
      </Tabs>

      {/* ── Issue to an employee ────────────────────────────────────────────── */}
      <Dialog open={assigning} onOpenChange={setAssigning}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Issue {asset.assetName}</DialogTitle>
            <DialogDescription>
              The holder signs for it on their own screen. Nobody can acknowledge on their behalf.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2 sm:col-span-2">
              <Label>Issue to *</Label>
              <EmployeePicker
                value={assignForm.employeeId}
                onChange={(v) => setAssignForm((f) => ({ ...f, employeeId: v ?? '' }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Issued on</Label>
              <Input type="date" value={assignForm.assignmentDate}
                onChange={(e) => setAssignForm((f) => ({ ...f, assignmentDate: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Due back</Label>
              <Input type="date" value={assignForm.expectedReturnDate}
                onChange={(e) => setAssignForm((f) => ({ ...f, expectedReturnDate: e.target.value }))} />
              <p className="text-xs text-muted-foreground">
                Left blank, this custody appears on no return list.
              </p>
            </div>
            <div className="space-y-2">
              <Label>Assignment type</Label>
              <Select value={assignForm.type}
                onValueChange={(v) => setAssignForm((f) => ({ ...f, type: v }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {ASSIGNMENT_TYPES.map((t) =>
                    <SelectItem key={t.label} value={t.label}>{t.text}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Purpose</Label>
              <Select value={assignForm.purpose}
                onValueChange={(v) => setAssignForm((f) => ({ ...f, purpose: v }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {ASSIGNMENT_PURPOSES.map((p) =>
                    <SelectItem key={p.label} value={p.label}>{p.text}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Condition when issued</Label>
              <Select value={assignForm.conditionAtAssignment}
                onValueChange={(v) => setAssignForm((f) => ({ ...f, conditionAtAssignment: v }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {ASSET_CONDITIONS.map((c) =>
                    <SelectItem key={c.label} value={c.label}>{c.text}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2 sm:col-span-2">
              <Label>Terms the holder is signing to</Label>
              <Textarea rows={3} value={assignForm.termsAndConditions}
                onChange={(e) => setAssignForm((f) => ({ ...f, termsAndConditions: e.target.value }))} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAssigning(false)}>Cancel</Button>
            <Button onClick={() => assign.mutate()}
              disabled={!assignForm.employeeId || assign.isPending}>
              {assign.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Issue asset
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Schedule a service ──────────────────────────────────────────────── */}
      <Dialog open={scheduling} onOpenChange={setScheduling}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Schedule a service</DialogTitle>
            <DialogDescription>
              Completing the job re-dates the asset from its own interval.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>Date</Label>
              <Input type="date" value={serviceForm.maintenanceDate}
                onChange={(e) => setServiceForm((f) => ({ ...f, maintenanceDate: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Type</Label>
              <Select value={serviceForm.type}
                onValueChange={(v) => setServiceForm((f) => ({ ...f, type: v }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {ASSET_MAINTENANCE_TYPES.map((t) =>
                    <SelectItem key={t.label} value={t.label}>{t.text}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2 sm:col-span-2">
              <Label>What is being done *</Label>
              <Textarea rows={2} value={serviceForm.description}
                onChange={(e) => setServiceForm((f) => ({ ...f, description: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Expected cost</Label>
              <Input type="number" step="0.01" value={serviceForm.cost}
                onChange={(e) => setServiceForm((f) => ({ ...f, cost: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Done by</Label>
              <Select
                value={serviceForm.isInternalMaintenance ? 'internal' : 'external'}
                onValueChange={(v) =>
                  setServiceForm((f) => ({ ...f, isInternalMaintenance: v === 'internal' }))}
              >
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="internal">In-house</SelectItem>
                  <SelectItem value="external">An external provider</SelectItem>
                </SelectContent>
              </Select>
            </div>
            {!serviceForm.isInternalMaintenance && (
              <div className="space-y-2 sm:col-span-2">
                <Label>Provider</Label>
                <Input value={serviceForm.externalServiceProvider}
                  onChange={(e) =>
                    setServiceForm((f) => ({ ...f, externalServiceProvider: e.target.value }))} />
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setScheduling(false)}>Cancel</Button>
            <Button onClick={() => schedule.mutate()}
              disabled={!serviceForm.description.trim() || schedule.isPending}>
              {schedule.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Schedule
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Link to the Maintenance register ────────────────────────────────── */}
      {/* ── Dispose ───────────────────────────────────────── */}
      <Dialog open={disposing} onOpenChange={setDisposing}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Dispose of {asset.assetName}</DialogTitle>
            <DialogDescription>
              The asset stays on the register as disposed — it is not deleted and its history stays
              readable. It drops off the insurance lists; a theft would not have.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>Disposed on</Label>
              <Input type="date" value={disposeForm.disposalDate}
                onChange={(e) => setDisposeForm((f) => ({ ...f, disposalDate: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>How</Label>
              <Select value={disposeForm.disposalMethod}
                onValueChange={(v) => setDisposeForm((f) => ({ ...f, disposalMethod: v }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {DISPOSAL_METHODS.map((m) => (
                    <SelectItem key={m.value} value={String(m.value)}>{m.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2 sm:col-span-2">
              <Label>Notes</Label>
              <Textarea rows={2} value={disposeForm.disposalNotes}
                onChange={(e) => setDisposeForm((f) => ({ ...f, disposalNotes: e.target.value }))} />
            </div>
            {asset.isFinanceOwned && (
              <p className="sm:col-span-2 rounded-md bg-muted p-3 text-sm text-muted-foreground">
                This asset came from Finance. Disposing it here records that HR no longer holds it —
                the accounting disposal is Finance&rsquo;s and is not done by this action.
              </p>
            )}
            {asset.isCurrentlyAssigned && (
              <p className="sm:col-span-2 text-sm text-amber-600 dark:text-amber-500">
                It is still in {asset.currentAssignedToName ?? 'somebody'}&rsquo;s hands. Take it back
                first, or record what happened to it.
              </p>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDisposing(false)}>Cancel</Button>
            <Button onClick={() => dispose.mutate()} disabled={dispose.isPending}>
              {dispose.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Dispose
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Send to the workshop ──────────────────────────────── */}
      <Dialog open={sendingToWorkshop} onOpenChange={setSendingToWorkshop}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Send {asset.assetName} to the workshop</DialogTitle>
            <DialogDescription>
              Raises an admission in the Maintenance module against this asset&rsquo;s counterpart
              there. HR keeps the schedule and keeps chasing it — the link is a handle, not a
              handover.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2 sm:col-span-2">
              <Label>What is wrong with it *</Label>
              <Textarea rows={2} value={workshopForm.description}
                onChange={(e) => setWorkshopForm((f) => ({ ...f, description: e.target.value }))} />
              <p className="text-xs text-muted-foreground">
                Required — an admission nobody can read is a lost asset.
              </p>
            </div>
            <div className="space-y-2">
              <Label>Kind of work</Label>
              <Select value={workshopForm.type}
                onValueChange={(v) => setWorkshopForm((f) => ({ ...f, type: v }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {ASSET_MAINTENANCE_TYPES.map((t) =>
                    <SelectItem key={t.label} value={t.label}>{t.text}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Admission type</Label>
              <Select value={workshopForm.admissionType}
                onValueChange={(v) => setWorkshopForm((f) => ({ ...f, admissionType: v }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="Scheduled">Scheduled</SelectItem>
                  <SelectItem value="Emergency">Emergency</SelectItem>
                  <SelectItem value="Breakdown">Breakdown</SelectItem>
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                The Maintenance module&rsquo;s own vocabulary, passed through untranslated — it
                starts a downtime record for the last two and not for the first.
              </p>
            </div>
            <div className="space-y-2 sm:col-span-2">
              <Label>Observed problems</Label>
              <Textarea rows={2} value={workshopForm.observedProblems}
                onChange={(e) =>
                  setWorkshopForm((f) => ({ ...f, observedProblems: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Expected back</Label>
              <Input type="date" value={workshopForm.estimatedCompletionDate}
                onChange={(e) =>
                  setWorkshopForm((f) => ({ ...f, estimatedCompletionDate: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Expected cost</Label>
              <Input type="number" step="0.01" value={workshopForm.cost}
                onChange={(e) => setWorkshopForm((f) => ({ ...f, cost: e.target.value }))} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setSendingToWorkshop(false)}>Cancel</Button>
            <Button onClick={() => sendToWorkshop.mutate()}
              disabled={!workshopForm.description.trim() || sendToWorkshop.isPending}>
              {sendToWorkshop.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Send it
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Complete a service ────────────────────────────────── */}
      <Dialog open={completing !== null} onOpenChange={(o) => { if (!o) setCompleting(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Complete {completing?.number}</DialogTitle>
            <DialogDescription>
              This re-dates the asset from its own maintenance interval, clears it off the overdue
              list, and discharges it from the workshop if it went there.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>What was done</Label>
            <Textarea rows={3} value={completionNotes}
              onChange={(e) => setCompletionNotes(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCompleting(null)}>Cancel</Button>
            <Button onClick={() => completing && complete.mutate(completing.id)}
              disabled={complete.isPending}>
              {complete.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Mark complete
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={linking} onOpenChange={setLinking}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Link to the Maintenance register</DialogTitle>
            <DialogDescription>
              Rows another HR asset already claims are shown greyed out rather than hidden, so you
              can see why the one you want is unavailable.
            </DialogDescription>
          </DialogHeader>
          <Input placeholder="Search the Maintenance register…" value={linkSearch}
            onChange={(e) => setLinkSearch(e.target.value)} />
          <div className="max-h-80 overflow-y-auto">
            {linkable.length === 0 ? (
              <EmptyState
                icon={Wrench}
                title="Nothing to link to"
                description="The Maintenance register holds no matching asset on this tenant."
              />
            ) : (
              <Table>
                <TableBody>
                  {linkable.map((m) => (
                    <TableRow key={m.id} className={m.alreadyLinked ? 'opacity-50' : ''}>
                      <TableCell>
                        <div className="font-medium">{m.name}</div>
                        <div className="text-xs text-muted-foreground">
                          {m.assetNumber}{m.categoryName ? ` · ${m.categoryName}` : ''}
                        </div>
                      </TableCell>
                      <TableCell className="text-right">
                        <Button size="sm" variant="outline" disabled={m.alreadyLinked || link.isPending}
                          onClick={() => link.mutate(m.id)}>
                          {m.alreadyLinked ? 'Already linked' : <><Send className="mr-2 h-4 w-4" />Link</>}
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
